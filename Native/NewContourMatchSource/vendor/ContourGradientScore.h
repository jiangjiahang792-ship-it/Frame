#pragma once

#include "ContourScoreSimd.h"
#include <cstddef>

// Compile ContourGradientScore.cpp at the baseline ISA with /fp:strict.
// Compile ONLY ContourGradientScoreAvx2.cpp with /arch:AVX2 /fp:strict /GL-.
namespace contourcv_score {

struct GradientView {
    const float* gx = nullptr;
    const float* gy = nullptr;
    std::size_t strideX = 0, strideY = 0; // FLOAT elements, not bytes.
    int cols = 0, rows = 0;
    double originX = 0, originY = 0;
    double minContrast = 1;
    bool usePolarity = false;
};

GradientView makeGradientView(const cv::Mat& gx, const cv::Mat& gy,
                              double originX, double originY,
                              double minContrast, bool usePolarity);

// centerAllowed/search-mask acceptance remains the caller's responsibility.
// Uses the original normal normalization, bilinear float round trip, std::hypot
// and eighth-power alignment. Accumulation retains feature order. No FMA,
// approximate reciprocal, altered normal formula, or feature subsampling.
//
// Production-domain preconditions: finite feature coordinates and nonzero
// normals with lengths in [1e-100,1e100], finite gradients bounded by 1e30,
// finite positive scales, finite pose/origin, and positive minContrast.
// The current Sobel maps and extracted model normals satisfy these bounds.
// Empty features/maps return zero; unavailable AVX2 safely falls back.
//
// cutoff=0 returns the complete score. A positive cutoff may return zero for a
// score provably below it, using a deliberately conservative rounding margin.
// Scores at/above cutoff remain complete. Cutoff is disabled for extreme scales
// or unusually large feature sets; it never clamps any computed contribution.
double scoreGradient(const GradientView&, const FeatureSoA&, const ScorePose&,
                     Backend backend = Backend::Auto, double cutoff = 0);

namespace detail {
struct GradientCutoff {
    double threshold = 0, margin = 0;
    std::size_t count = 0;
    bool enabled = false;
    bool reject(double sum, std::size_t processed) const noexcept {
        return enabled && (processed & 31) == 0
            && sum + static_cast<double>(count - processed) + margin < threshold;
    }
};
GradientCutoff makeGradientCutoff(std::size_t count, const ScorePose& pose, double cutoff);
double accumulateGradientScalar(const GradientView&, const FeatureSoA&, const ScorePose&,
                                double cosine, double sine, std::size_t begin,
                                double initialSum, const GradientCutoff&);
double scoreGradientAvx2(const GradientView&, const FeatureSoA&, const ScorePose&, double cutoff);
} // namespace detail
} // namespace contourcv_score
