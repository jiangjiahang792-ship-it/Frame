#pragma once

#include <algorithm>
#include <cstdint>
#include <opencv2/core.hpp>
#include <vector>

#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
#include <emmintrin.h>
#endif
#if defined(_MSC_VER)
#include <intrin.h>
#endif

namespace contourcv2_detail
{
inline int edgeBitCount(std::uint64_t bits)
{
    bits -= (bits >> 1) & 0x5555555555555555ULL;
    bits = (bits & 0x3333333333333333ULL) + ((bits >> 2) & 0x3333333333333333ULL);
    bits = (bits + (bits >> 4)) & 0x0f0f0f0f0f0f0f0fULL;
    return static_cast<int>((bits * 0x0101010101010101ULL) >> 56);
}
inline std::vector<int> edgeWordOffsets(const std::vector<std::uint64_t> &words)
{
    std::vector<int> offsets(words.size() + 1);
    for (std::size_t i = 0; i < words.size(); ++i)
        offsets[i + 1] = offsets[i] + edgeBitCount(words[i]);
    return offsets;
}
// One bit per original Canny pixel. Occupancy only skips known-empty seed
// pixels; it does not change their coordinates, traversal order or geometry.
inline std::vector<std::uint64_t> buildEdgeOccupancy(const cv::Mat &edges)
{
    CV_Assert(edges.type() == CV_8UC1);
    const std::size_t wordsPerRow = (static_cast<std::size_t>(edges.cols) + 63) / 64;
    std::vector<std::uint64_t> result(static_cast<std::size_t>(edges.rows) * wordsPerRow);
    if (edges.empty())
        return result;
    cv::parallel_for_(cv::Range(0, edges.rows), [&](const cv::Range &range) {
        for (int y = range.start; y < range.end; ++y)
        {
            const unsigned char *source = edges.ptr<unsigned char>(y);
            std::uint64_t *target = result.data() + static_cast<std::size_t>(y) * wordsPerRow;
            for (std::size_t word = 0; word < wordsPerRow; ++word)
            {
                const int begin = static_cast<int>(word * 64);
                const int count = std::min(64, edges.cols - begin);
                std::uint64_t bits = 0;
                int offset = 0;
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
                const __m128i zero = _mm_setzero_si128();
                for (; offset + 16 <= count; offset += 16)
                {
                    const __m128i pixels =
                        _mm_loadu_si128(reinterpret_cast<const __m128i *>(source + begin + offset));
                    const unsigned nonzero =
                        static_cast<unsigned>(~_mm_movemask_epi8(_mm_cmpeq_epi8(pixels, zero))) & 0xffffU;
                    bits |= static_cast<std::uint64_t>(nonzero) << offset;
                }
#endif
                for (; offset < count; ++offset)
                    if (source[begin + offset] != 0)
                        bits |= std::uint64_t(1) << offset;
                target[word] = bits;
            }
        }
    });
    return result;
}

// Caller guarantees rightInclusive is inside the original image row. The
// returned bit zero corresponds to left, including at a 64-pixel boundary.
inline std::uint64_t windowEdgeBits(const std::uint64_t *rowWords, int left, int rightInclusive)
{
    CV_DbgAssert(left >= 0 && rightInclusive >= left && rightInclusive - left < 9);
    const int count = rightInclusive - left + 1;
    const int shift = left & 63;
    const std::size_t word = static_cast<std::size_t>(left) >> 6;
    std::uint64_t bits = rowWords[word] >> shift;
    if (shift + count > 64)
        bits |= rowWords[word + 1] << (64 - shift);
    return bits & ((std::uint64_t(1) << count) - 1);
}

inline unsigned firstEdgeBit(std::uint64_t bits)
{
    CV_DbgAssert(bits != 0);
#if defined(_MSC_VER) && defined(_M_X64)
    unsigned long index;
    _BitScanForward64(&index, bits);
    return static_cast<unsigned>(index);
#elif defined(__GNUC__) || defined(__clang__)
    return static_cast<unsigned>(__builtin_ctzll(bits));
#else
    unsigned index = 0;
    while ((bits & 1) == 0)
    {
        bits >>= 1;
        ++index;
    }
    return index;
#endif
}
} // namespace contourcv2_detail
