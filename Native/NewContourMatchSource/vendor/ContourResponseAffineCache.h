#pragma once

#include "ContourScoreSimd.h"
#include <array>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <utility>

namespace contourcv_score {

// Worker-local, direct-mapped cache of affine feature preparation ONLY. No
// image, translation, cutoff or score is stored. Features must outlive the
// cache and remain immutable (including their vector elements) for its entire
// lifetime. Polarity is fixed at construction; use another instance to change
// either binding. There is no shared/static mutable state and no locking.
//
// Returned references are guaranteed only until the next get/clear/release on
// this instance. Score each reference before requesting another key: a miss
// may overwrite the same slot. Never share an instance between workers.
//
// Slots allocate their PreparedResponse vectors lazily, on the first miss.
// With the production 600-feature cap and 128 slots, the element payload is
// 128 * 600 * (4*sizeof(double)+sizeof(int)) = 2,764,800 bytes at full occupancy,
// plus fixed slot/vector metadata and allocator overhead. A smaller power-of-
// two SlotCount is available for small jobs. Capacity never grows with calls.
template<std::size_t SlotCount = 128>
class ResponseAffineCache final {
    static_assert(SlotCount >= 1 && SlotCount <= 128 && (SlotCount & (SlotCount - 1)) == 0,
                  "Response affine cache requires a power-of-two slot count in 1..128");
    static_assert(sizeof(double) == sizeof(std::uint64_t), "Exact double-bit keys require 64-bit double");
public:
    struct Statistics {
        std::size_t lookups = 0, hits = 0, misses = 0, evictions = 0;
    };

    explicit ResponseAffineCache(const FeatureSoA& features, bool usePolarity)
        : features_(features), usePolarity_(usePolarity)
    {
        CV_Assert(features.py.size() == features.size() && features.nx.size() == features.size()
                  && features.ny.size() == features.size());
    }
    ResponseAffineCache(FeatureSoA&&, bool) = delete;
    ResponseAffineCache(const ResponseAffineCache&) = delete;
    ResponseAffineCache& operator=(const ResponseAffineCache&) = delete;
    ResponseAffineCache(ResponseAffineCache&&) = delete;
    ResponseAffineCache& operator=(ResponseAffineCache&&) = delete;

    const PreparedResponse& get(double angle, double sx, double sy)
    {
        // Match prepareResponse's domain, including rejection on an otherwise
        // populated slot. Signed-zero angles remain distinct exact-bit keys.
        CV_Assert(std::isfinite(angle) && std::isfinite(sx) && std::isfinite(sy) && sx > 0 && sy > 0);
        const Key key = makeKey(angle, sx, sy);
        Entry& entry = entries_[slot(key)];
        ++statistics_.lookups;
        if (entry.occupied && entry.key == key) {
            ++statistics_.hits;
            return entry.prepared;
        }
        ++statistics_.misses;
        if (entry.occupied) {
            ++statistics_.evictions;
            --residentEntries_;
        }
        // An allocation failure cannot leave a partly written value hittable.
        entry.occupied = false;
        prepareResponse(features_, angle, sx, sy, usePolarity_, entry.prepared);
        entry.key = key;
        entry.occupied = true;
        ++residentEntries_;
        return entry.prepared;
    }

    const Statistics& statistics() const noexcept { return statistics_; }
    static constexpr std::size_t capacity() noexcept { return SlotCount; }
    std::size_t residentEntries() const noexcept { return residentEntries_; }

    // Diagnostics only; excludes fixed metadata/allocator overhead. No scan is
    // required on the scoring path. Empty and never-used slots report zero.
    std::size_t payloadCapacityBytes() const noexcept
    {
        std::size_t bytes = 0;
        for (const Entry& entry : entries_) {
            const auto& p = entry.prepared;
            bytes += (p.xA.capacity() + p.xB.capacity() + p.yA.capacity() + p.yB.capacity()) * sizeof(double)
                   + p.bin.capacity() * sizeof(int);
        }
        return bytes;
    }

    // Invalidate all entries and reset counters while retaining allocation
    // capacity for the same immutable feature binding. release also frees it.
    void clear() noexcept
    {
        for (Entry& entry : entries_) entry.occupied = false;
        statistics_ = {};
        residentEntries_ = 0;
    }
    void release() noexcept
    {
        for (Entry& entry : entries_) {
            PreparedResponse empty;
            std::swap(entry.prepared, empty);
        }
        clear();
    }

private:
    using Key = std::array<std::uint64_t, 3>;
    struct Entry {
        Key key{};
        PreparedResponse prepared;
        bool occupied = false;
    };
    static Key makeKey(double angle, double sx, double sy) noexcept
    {
        Key key;
        std::memcpy(&key[0], &angle, sizeof(angle));
        std::memcpy(&key[1], &sx, sizeof(sx));
        std::memcpy(&key[2], &sy, sizeof(sy));
        return key;
    }
    static std::uint64_t avalanche(std::uint64_t value) noexcept
    {
        value ^= value >> 30;
        value *= UINT64_C(0xbf58476d1ce4e5b9);
        value ^= value >> 27;
        value *= UINT64_C(0x94d049bb133111eb);
        return value ^ (value >> 31);
    }
    static std::size_t slot(const Key& key) noexcept
    {
        std::uint64_t hash = UINT64_C(0x6a09e667f3bcc909);
        for (const auto bits : key) hash = avalanche(hash ^ bits ^ UINT64_C(0x9e3779b97f4a7c15));
        return static_cast<std::size_t>(hash & (SlotCount - 1));
    }

    const FeatureSoA& features_;
    const bool usePolarity_;
    std::array<Entry, SlotCount> entries_{};
    Statistics statistics_{};
    std::size_t residentEntries_ = 0;
};
} // namespace contourcv_score
