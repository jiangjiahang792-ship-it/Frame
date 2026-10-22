#include "ContourScoreSimd.h"

#include <cmath>
#include <limits>

namespace contourcv_score {
namespace {
int direction(double normalX, double normalY, bool usePolarity)
{
    // Identical constants, strict comparisons, quadrants and modulo to the
    // original responseScore. Equality belongs to the next orientation bin.
    const double x = std::abs(normalX), y = std::abs(normalY);
    int bin = 6;
    if (y < x * 0.131652497587) bin = 0;
    else if (y < x * 0.414213562373) bin = 1;
    else if (y < x * 0.767326987979) bin = 2;
    else if (y < x * 1.303225372841) bin = 3;
    else if (y < x * 2.414213562373) bin = 4;
    else if (y < x * 7.595754112725) bin = 5;
    if (normalX >= 0) bin = normalY >= 0 ? bin : 24 - bin;
    else bin = normalY >= 0 ? 12 - bin : 12 + bin;
    return bin % (usePolarity ? 24 : 12);
}

float bilinear(const ResponseView& response, int bin, double x, double y)
{
    // Check in floating point before converting: this also safely excludes
    // non-finite/huge out-of-image coordinates without an overflowing int cast.
    if (!(x >= 0 && y >= 0 && x < response.cols - 1.0 && y < response.rows - 1.0)
        || !response.data[bin])
        return 0;
    const int ix = static_cast<int>(std::floor(x));
    const int iy = static_cast<int>(std::floor(y));
    const double fx = x - ix, fy = y - iy;
    const unsigned char* row = response.data[bin] + std::size_t(iy) * response.rowStride[bin] + ix;
    const unsigned char* next = row + response.rowStride[bin];
    return static_cast<float>(((1 - fy) * ((1 - fx) * row[0] + fx * row[1])
                             + fy * ((1 - fx) * next[0] + fx * next[1])) / 255.0);
}
}

ResponseView makeResponseView(const std::array<cv::Mat, ResponseBinCount>& response,
                             double originX, double originY, bool usePolarity)
{
    ResponseView view;
    view.originX = originX; view.originY = originY; view.usePolarity = usePolarity;
    const std::size_t count = usePolarity ? 24 : 12;
    for (std::size_t bin = 0; bin < count; ++bin) {
        const cv::Mat& image = response[bin];
        if (image.empty()) continue;
        CV_Assert(image.type() == CV_8UC1 && image.dims == 2);
        if (view.cols == 0) { view.cols = image.cols; view.rows = image.rows; }
        CV_Assert(image.cols == view.cols && image.rows == view.rows);
        view.data[bin] = image.ptr<unsigned char>();
        view.rowStride[bin] = image.step[0];
    }
    // Recognize only owned, fully populated planes in one regular padded arena.
    // Arbitrary external-memory, missing-bank or independent/strided maps keep
    // the existing two-byte load path; no padding is assumed from step alone.
    const cv::Mat& first = response[0];
    if (view.cols >= 2 && view.rows >= 2 && !first.empty() && first.u != nullptr) {
        const std::size_t stride = first.step[0];
        const std::size_t limit = static_cast<std::size_t>(std::numeric_limits<int>::max());
        if (stride >= static_cast<std::size_t>(view.cols) + 2
            && stride <= limit / count / static_cast<std::size_t>(view.rows)) {
            const std::size_t planeStride = stride * static_cast<std::size_t>(view.rows);
            const std::size_t arenaBytes = planeStride * count;
            bool packed = first.dataend >= first.data
                && static_cast<std::size_t>(first.dataend - first.data) >= arenaBytes;
            for (std::size_t bin = 0; packed && bin < count; ++bin) {
                const cv::Mat& image = response[bin];
                packed = !image.empty() && image.u == first.u && image.datastart == first.datastart
                    && image.dataend == first.dataend && image.step[0] == stride
                    && image.data == first.data + bin * planeStride;
            }
            if (packed) {
                view.packedBase = first.data;
                view.packedRowStride = static_cast<int>(stride);
                view.packedPlaneStride = static_cast<int>(planeStride);
            }
        }
    }
    return view;
}

bool avx2Available() noexcept
{
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
    return cv::checkHardwareSupport(CV_CPU_AVX2);
#else
    return false;
#endif
}

namespace detail {
void prepareResponseScalar(const FeatureSoA& features, double c, double s, double sx, double sy,
                           bool usePolarity, PreparedResponse& output, std::size_t begin)
{
    for (std::size_t i = begin; i < features.size(); ++i) {
        output.xA[i] = c * sx * features.px[i];
        output.xB[i] = s * sy * features.py[i];
        output.yA[i] = s * sx * features.px[i];
        output.yB[i] = c * sy * features.py[i];
        const double nx = features.nx[i] / sx, ny = features.ny[i] / sy;
        output.bin[i] = direction(c * nx - s * ny, s * nx + c * ny, usePolarity);
    }
}
double accumulateResponseScalar(const ResponseView& response, const FeatureSoA& features,
                                const ScorePose& pose, double c, double s,
                                std::size_t begin, double score, double cutoff)
{
    const double cutoffSum = cutoff * features.size();
    const double margin = 1e-12 * features.size();
    for (std::size_t i = begin; i < features.size(); ++i) {
        const double px = pose.x + c * pose.sx * features.px[i] - s * pose.sy * features.py[i];
        const double py = pose.y + s * pose.sx * features.px[i] + c * pose.sy * features.py[i];
        const double nx = features.nx[i] / pose.sx, ny = features.ny[i] / pose.sy;
        const int bin = direction(c * nx - s * ny, s * nx + c * ny, response.usePolarity);
        score += bilinear(response, bin, px - response.originX, py - response.originY);
        const std::size_t processed = i + 1;
        if (cutoff > 0 && (processed & 31) == 0
            && score + double(features.size() - processed) + margin < cutoffSum)
            return 0;
    }
    return score;
}
double accumulatePreparedScalar(const ResponseView& response, const PreparedResponse& prepared,
                                double x, double y, std::size_t begin, double score, double cutoff)
{
    const double cutoffSum = cutoff * prepared.size();
    const double margin = 1e-12 * prepared.size();
    for (std::size_t i = begin; i < prepared.size(); ++i) {
        const double px = (x + prepared.xA[i]) - prepared.xB[i];
        const double py = (y + prepared.yA[i]) + prepared.yB[i];
        score += bilinear(response, prepared.bin[i], px - response.originX, py - response.originY);
        const std::size_t processed = i + 1;
        if (cutoff > 0 && (processed & 31) == 0
            && score + double(prepared.size() - processed) + margin < cutoffSum)
            return 0;
    }
    return score;
}
} // namespace detail

void prepareResponse(const FeatureSoA& features, double angle, double sx, double sy,
                     bool usePolarity, PreparedResponse& output)
{
    CV_Assert(features.py.size() == features.size() && features.nx.size() == features.size()
              && features.ny.size() == features.size());
    CV_Assert(std::isfinite(angle) && std::isfinite(sx) && std::isfinite(sy) && sx > 0 && sy > 0);
    const std::size_t count = features.size();
    output.xA.resize(count); output.xB.resize(count); output.yA.resize(count); output.yB.resize(count);
    output.bin.resize(count);
    output.angle = angle; output.sx = sx; output.sy = sy; output.usePolarity = usePolarity;
    const double c = std::cos(angle), s = std::sin(angle);
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
    if (count >= 4 && avx2Available()) {
        detail::prepareResponseAvx2(features, c, s, sx, sy, usePolarity, output);
        return;
    }
#endif
    detail::prepareResponseScalar(features, c, s, sx, sy, usePolarity, output, 0);
}

double scorePrepared(const ResponseView& response, const PreparedResponse& prepared,
                     double x, double y, Backend backend, double cutoff)
{
    CV_Assert(prepared.xB.size() == prepared.size() && prepared.yA.size() == prepared.size()
              && prepared.yB.size() == prepared.size() && prepared.bin.size() == prepared.size());
    CV_Assert(prepared.usePolarity == response.usePolarity);
    if (prepared.size() == 0 || response.cols < 2 || response.rows < 2) return 0;
    CV_Assert(std::isfinite(x) && std::isfinite(y));
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
    if (backend != Backend::Scalar && prepared.size() >= 4 && avx2Available())
        return detail::scorePreparedAvx2(response, prepared, x, y, cutoff);
#else
    (void)backend;
#endif
    return detail::accumulatePreparedScalar(response, prepared, x, y, 0, 0, cutoff) / prepared.size();
}

double scoreResponse(const ResponseView& response, const FeatureSoA& features,
                     const ScorePose& pose, Backend backend, double cutoff)
{
    CV_Assert(features.py.size() == features.size() && features.nx.size() == features.size()
              && features.ny.size() == features.size());
    if (features.empty() || response.cols < 2 || response.rows < 2) return 0;
    CV_Assert(std::isfinite(pose.x) && std::isfinite(pose.y) && std::isfinite(pose.angle)
              && std::isfinite(pose.sx) && std::isfinite(pose.sy) && pose.sx > 0 && pose.sy > 0);
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
    if (backend != Backend::Scalar && features.size() >= 4 && avx2Available())
        return detail::scoreResponseAvx2(response, features, pose, cutoff);
#else
    (void)backend;
#endif
    return detail::accumulateResponseScalar(response, features, pose,
        std::cos(pose.angle), std::sin(pose.angle), 0, 0, cutoff) / features.size();
}
} // namespace contourcv_score

