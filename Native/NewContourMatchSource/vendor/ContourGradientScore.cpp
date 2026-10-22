#include "ContourGradientScore.h"

#include <algorithm>
#include <cmath>
#include <limits>

namespace contourcv_score {
GradientView makeGradientView(const cv::Mat& gx, const cv::Mat& gy,
                              double originX, double originY,
                              double minContrast, bool usePolarity)
{
    GradientView result;
    result.originX = originX; result.originY = originY;
    result.minContrast = minContrast; result.usePolarity = usePolarity;
    CV_Assert(std::isfinite(originX) && std::isfinite(originY)
        && std::isfinite(minContrast) && minContrast > 0);
    if (gx.empty() || gy.empty()) {
        CV_Assert(gx.empty() && gy.empty());
        return result;
    }
    CV_Assert(gx.type() == CV_32FC1 && gy.type() == CV_32FC1
        && gx.dims == 2 && gy.dims == 2 && gx.size() == gy.size()
        && gx.step[0] % sizeof(float) == 0 && gy.step[0] % sizeof(float) == 0);
    result.gx = gx.ptr<float>(); result.gy = gy.ptr<float>();
    result.strideX = gx.step[0] / sizeof(float); result.strideY = gy.step[0] / sizeof(float);
    result.cols = gx.cols; result.rows = gx.rows;
    return result;
}

namespace detail {
GradientCutoff makeGradientCutoff(std::size_t count, const ScorePose& pose, double cutoff)
{
    GradientCutoff result;
    result.count = count;
    // With supported finite normals, these scale limits keep squared lengths
    // far from double underflow/overflow. For <=65536 nonnegative terms the
    // sequential-sum error is much smaller than this 2e-10-per-feature margin;
    // it also covers normal/sin/cos/hypot/division/eighth-power rounding around
    // the theoretical contribution bound of one. No contribution is clamped.
    result.enabled = std::isfinite(cutoff) && cutoff > 0 && count <= 65536
        && pose.sx >= 1e-6 && pose.sx <= 1e6 && pose.sy >= 1e-6 && pose.sy <= 1e6;
    result.threshold = std::nextafter(cutoff * static_cast<double>(count),
        -std::numeric_limits<double>::infinity());
    result.margin = 2e-10 * (static_cast<double>(count) + 1);
    return result;
}

double accumulateGradientScalar(const GradientView& view, const FeatureSoA& features,
                                const ScorePose& pose, double c, double s,
                                std::size_t begin, double score, const GradientCutoff& cutoff)
{
    for (std::size_t i = begin; i < features.size(); ++i) {
        const double px = pose.x + c * pose.sx * features.px[i] - s * pose.sy * features.py[i];
        const double py = pose.y + s * pose.sx * features.px[i] + c * pose.sy * features.py[i];
        const double nx = features.nx[i] / pose.sx, ny = features.ny[i] / pose.sy;
        const double norm = std::sqrt(nx * nx + ny * ny);
        const double normalX = (c * nx - s * ny) / norm;
        const double normalY = (s * nx + c * ny) / norm;
        const double x = px - view.originX, y = py - view.originY;
        if (x >= 0 && y >= 0 && x < view.cols - 1.0 && y < view.rows - 1.0) {
            const int ix = static_cast<int>(x), iy = static_cast<int>(y);
            const double fx = x - ix, fy = y - iy;
            const float* rowX = view.gx + std::size_t(iy) * view.strideX + ix;
            const float* nextX = rowX + view.strideX;
            const float* rowY = view.gy + std::size_t(iy) * view.strideY + ix;
            const float* nextY = rowY + view.strideY;
            const double gx = static_cast<float>((1 - fy) * ((1 - fx) * rowX[0] + fx * rowX[1])
                + fy * ((1 - fx) * nextX[0] + fx * nextX[1]));
            const double gy = static_cast<float>((1 - fy) * ((1 - fx) * rowY[0] + fx * rowY[1])
                + fy * ((1 - fx) * nextY[0] + fx * nextY[1]));
            const double magnitude = std::hypot(gx, gy);
            if (!(magnitude < view.minContrast)) {
                const double dot = (normalX * gx + normalY * gy) / magnitude;
                const double alignment = view.usePolarity ? std::max(0.0, dot) : std::abs(dot);
                const double squared = alignment * alignment, fourth = squared * squared;
                score += fourth * fourth;
            }
        }
        if (cutoff.reject(score, i + 1)) return 0;
    }
    return score;
}
} // namespace detail

double scoreGradient(const GradientView& view, const FeatureSoA& features,
                     const ScorePose& pose, Backend backend, double cutoff)
{
    CV_Assert(features.py.size() == features.size() && features.nx.size() == features.size()
        && features.ny.size() == features.size());
    if (features.empty() || view.cols < 2 || view.rows < 2 || !view.gx || !view.gy) return 0;
    CV_Assert(std::isfinite(pose.x) && std::isfinite(pose.y) && std::isfinite(pose.angle)
        && std::isfinite(pose.sx) && std::isfinite(pose.sy) && pose.sx > 0 && pose.sy > 0);
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
    // AVX2 gathers use signed 32-bit element indices. Unusual huge-stride views
    // retain the scalar path rather than truncating addresses.
    const std::size_t indexLimit = static_cast<std::size_t>(std::numeric_limits<int>::max() - view.cols);
    const bool gatherSafe = view.strideX <= indexLimit / std::size_t(view.rows - 1)
        && view.strideY <= indexLimit / std::size_t(view.rows - 1);
    if (backend != Backend::Scalar && features.size() >= 4 && gatherSafe && avx2Available())
        return detail::scoreGradientAvx2(view, features, pose, cutoff);
#else
    (void)backend;
#endif
    return detail::accumulateGradientScalar(view, features, pose,
        std::cos(pose.angle), std::sin(pose.angle), 0, 0,
        detail::makeGradientCutoff(features.size(), pose, cutoff)) / features.size();
}
} // namespace contourcv_score
