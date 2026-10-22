#pragma once

#include <opencv2/core.hpp>
#include <array>
#include <cstddef>
#include <vector>

// Runtime-dispatched response scoring. Keep the dispatcher/scalar translation
// unit at the application's baseline ISA. Compile ONLY ContourScoreSimdAvx2.cpp
// with /arch:AVX2 /fp:strict /GL- (MSVC); never enable AVX2 for the entire program.
namespace contourcv_score {
constexpr std::size_t ResponseBinCount = 24;

struct FeatureSoA {
    std::vector<double> px, py, nx, ny;
    std::size_t size() const noexcept { return px.size(); }
    bool empty() const noexcept { return px.empty(); }
    void reserve(std::size_t n) { px.reserve(n); py.reserve(n); nx.reserve(n); ny.reserve(n); }
    void push_back(double x, double y, double normalX, double normalY) {
        px.push_back(x); py.push_back(y); nx.push_back(normalX); ny.push_back(normalY);
    }
};

struct ScorePose {
    double x = 0, y = 0, angle = 0, sx = 1, sy = 1; // angle is in radians
};

// Non-owning view: the response Mats must outlive all scoring calls. Different
// banks may have different byte strides, including non-contiguous submatrices.
// Coordinates and origin use the same pyramid level as the feature points.
struct ResponseView {
    std::array<const unsigned char*, ResponseBinCount> data{};
    std::array<std::size_t, ResponseBinCount> rowStride{};
    int cols = 0, rows = 0;
    double originX = 0, originY = 0;
    bool usePolarity = false;
    // Optional byte-addressed gather layout, populated only by makeResponseView
    // after verifying a shared allocation, regular planes and row padding.
    // All offsets, including a final 32-bit load, fit a signed i32 index.
    const unsigned char* packedBase = nullptr;
    int packedRowStride = 0, packedPlaneStride = 0;
};

ResponseView makeResponseView(const std::array<cv::Mat, ResponseBinCount>& response,
                             double originX, double originY, bool usePolarity);

enum class Backend { Auto, Scalar, Avx2 };
// Includes OpenCV's CPU/OS AVX2 check; respects cv::setUseOptimized(false).
bool avx2Available() noexcept;

// centerAllowed/search-mask acceptance deliberately remains the caller's job.
// No feature reordering/subsampling, approximate reciprocal, or FMA. With a
// positive cutoff, return zero early only if all remaining responses scoring
// one still cannot reach cutoff (plus a conservative rounding margin). Scores
// that reach cutoff remain exact; cutoff=0 disables this optimization.
// Input feature components must be finite; sx and sy must be finite and positive.
// Empty features or banks score zero. Avx2 requests safely fall back on machines
// without AVX2, so tests and application code cannot accidentally execute it.
double scoreResponse(const ResponseView& response, const FeatureSoA& features,
                     const ScorePose& pose, Backend backend = Backend::Auto, double cutoff = 0);

// Terms whose values do not change during translation-only trials. Keeping
// separate addends is intentional: (x+xA)-xB must not become x+(xA-xB).
// Build with prepareResponse; reuse the vectors' capacity between affine poses.
// Preparation uses the same runtime AVX2 guard, with an unchanged scalar tail.
struct PreparedResponse {
    std::vector<double> xA, xB, yA, yB;
    std::vector<int> bin;
    bool usePolarity = false;
    double angle = 0, sx = 1, sy = 1;
    std::size_t size() const noexcept { return xA.size(); }
};
void prepareResponse(const FeatureSoA& features, double angle, double sx, double sy,
                     bool usePolarity, PreparedResponse& output);
double scorePrepared(const ResponseView& response, const PreparedResponse& prepared,
                     double x, double y, Backend backend = Backend::Auto, double cutoff = 0);

namespace detail {
// Internal cross-TU entry points; invoke scoreResponse rather than these directly.
// initialSum preserves the exact feature accumulation order across SIMD tails.
// Preparation entry points require equal-sized, preallocated output vectors;
// the public prepareResponse performs validation, resizing and CPU dispatch.
void prepareResponseScalar(const FeatureSoA&, double cosine, double sine, double sx, double sy,
                           bool usePolarity, PreparedResponse&, std::size_t begin);
void prepareResponseAvx2(const FeatureSoA&, double cosine, double sine, double sx, double sy,
                         bool usePolarity, PreparedResponse&);
double accumulateResponseScalar(const ResponseView&, const FeatureSoA&, const ScorePose&,
                                double cosine, double sine, std::size_t begin, double initialSum, double cutoff);
double scoreResponseAvx2(const ResponseView&, const FeatureSoA&, const ScorePose&, double cutoff);
double accumulatePreparedScalar(const ResponseView&, const PreparedResponse&, double x, double y,
                                std::size_t begin, double initialSum, double cutoff);
double scorePreparedAvx2(const ResponseView&, const PreparedResponse&, double x, double y, double cutoff);
}
} // namespace contourcv_score

