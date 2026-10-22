#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "CContourMatchCV2.h"
#include "ContourCv2EdgeOccupancy.h"
#include "ContourCv2GradientSimd.h"
#include "ContourCv2Sobel.h"
#include "ContourSubpixelEdges.h"
#include "ContourVmRecoveredMath.h"
// Evidence and bounded accuracy/performance results:
// research/vm-class2-20261006/README.md. This class never loads a vendor matcher.
#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <limits>
#include <numeric>
#include <opencv2/imgproc.hpp>
#include <stdexcept>
#include <unordered_map>
#if defined(_M_X64) || defined(__x86_64__) || defined(_M_IX86) || defined(__i386__)
#include <emmintrin.h>
#endif

namespace
{
using Parameters = CContourMatchCV2::Parameters;
using Options = CContourMatchCV2::Options;
using Polarity = CContourMatchCV2::Polarity;
constexpr double pi = 3.14159265358979323846;
constexpr float directionGate = contourvm_detail::directionGate;
using Clock = std::chrono::steady_clock;
double milliseconds(Clock::time_point start)
{
    return std::chrono::duration<double, std::milli>(Clock::now() - start).count();
}
struct Feature
{
    cv::Point2f point, normal;
    float strength;
};
struct Pose
{
    double x = 0, y = 0, angle = 0, sx = 1, sy = 1, score = 0;
};
struct Field
{
    cv::Mat nx, ny, amplitude, coarseAmplitude, edges, geometry, mask;
    cv::Mat dx, dy, magnitude;
    std::vector<std::uint64_t> edgeWords;
    std::vector<int> edgeOffsets;
    cv::Point origin;
    int contrast = 1;
};
struct Pattern
{
    Pose pose;
    std::vector<cv::Point> offsets;
    std::vector<unsigned char> bins;
};
bool imageValid(const cv::Mat &image)
{
    return !image.empty() && image.cols >= 8 && image.rows >= 8 && image.cols <= 32768 &&
           image.rows <= 32768 && image.total() <= 32U * 1024U * 1024U &&
           (image.type() == CV_8UC1 || image.type() == CV_8UC3 || image.type() == CV_8UC4);
}
bool maskValid(const cv::Mat &mask, cv::Size size)
{
    return mask.empty() || (mask.type() == CV_8UC1 && mask.size() == size && cv::countNonZero(mask) > 0);
}
bool parametersValid(const Parameters &p, const Options &o)
{
    return std::isfinite(p.lowThreshold) && std::isfinite(p.highThreshold) && p.lowThreshold > 0 &&
           p.highThreshold > p.lowThreshold && p.highThreshold <= 255 && p.numLevels >= 0 &&
           p.numLevels <= 10 && std::isfinite(p.angleStart) && std::isfinite(p.angleEnd) &&
           p.angleStart >= -180 && p.angleEnd <= 180 && p.angleStart <= p.angleEnd &&
           std::isfinite(p.angleStep) && p.angleStep >= 0 && p.angleStep <= 180 && p.minContrast >= 1 &&
           p.minContrast <= 255 && std::isfinite(p.scaleToleranceX) && p.scaleToleranceX >= 0 &&
           p.scaleToleranceX < 1 && std::isfinite(p.scaleToleranceY) && p.scaleToleranceY >= 0 &&
           p.scaleToleranceY < 1 && std::isfinite(o.minContourLength) && o.minContourLength >= 0 &&
           o.minContourLength <= 1e7 && o.timeoutMs >= 0 &&
           (o.polarity == Polarity::Ignore || o.polarity == Polarity::Use);
}
cv::Mat grayscale(const cv::Mat &image)
{
    if (image.type() == CV_8UC1)
        return image;
    cv::Mat gray;
    cv::cvtColor(image, gray, image.channels() == 3 ? cv::COLOR_BGR2GRAY : cv::COLOR_BGRA2GRAY);
    return gray;
}
int orientation(float nx, float ny, bool polarity = false)
{
    double angle = std::atan2(ny, nx);
    if (angle < 0)
        angle += 2 * pi;
    const int count = polarity ? 16 : 8;
    return static_cast<int>(std::floor(angle * (8 / pi) + 0.5)) % count;
}
cv::Point2d mapped(const Feature &f, const Pose &p)
{
    const double c = std::cos(p.angle), s = std::sin(p.angle);
    return {p.x + c * p.sx * f.point.x - s * p.sy * f.point.y,
            p.y + s * p.sx * f.point.x + c * p.sy * f.point.y};
}
cv::Point2d normal(const Feature &f, const Pose &p)
{
    const double c = std::cos(p.angle), s = std::sin(p.angle);
    const double x = f.normal.x / p.sx, y = f.normal.y / p.sy, length = std::hypot(x, y);
    return {(c * x - s * y) / length, (s * x + c * y) / length};
}
void constrain(Pose &p, const Parameters &parameters)
{
    const double low = parameters.angleStart * pi / 180, high = parameters.angleEnd * pi / 180;
    if (high - low >= 2 * pi - 1e-10)
    {
        while (p.angle > high)
            p.angle -= 2 * pi;
        while (p.angle < low)
            p.angle += 2 * pi;
    }
    else
        p.angle = std::clamp(p.angle, low, high);
    p.sx = std::clamp(p.sx, 1 - parameters.scaleToleranceX, 1 + parameters.scaleToleranceX);
    p.sy = std::clamp(p.sy, 1 - parameters.scaleToleranceY, 1 + parameters.scaleToleranceY);
}
std::vector<Feature> sampled(const std::vector<Feature> &features, std::size_t maximum)
{
    const std::size_t count = std::min(maximum, features.size());
    std::vector<Feature> result;
    result.reserve(count);
    for (std::size_t i = 0; i < count; ++i)
        result.push_back(features[i * features.size() / count]);
    return result;
}

// Raw 3x3 Sobel, L1 contrast and unit directions are directly recovered from
// bee3d0. The Canny/facet edge locator below is an explicit reconstruction of
// the still partly unknown target edge-index builder, not claimed byte-exact.
Field makeField(const cv::Mat &gray, const cv::Mat &mask, const Parameters &p, bool geometry)
{
    Field field;
    field.mask = mask;
    field.contrast = p.minContrast;
    cv::Mat dx, dy;
    contourcv2_detail::sobelPair(gray, dx, dy);
    if (!geometry)
    {
        field.nx.create(gray.size(), CV_32F);
        field.ny.create(gray.size(), CV_32F);
        field.amplitude.create(gray.size(), CV_16S);
        field.coarseAmplitude.create(gray.size(), CV_8U);
    }
    if (geometry)
    {
        // Refinement reads normals only at Canny edges. Final scoring samples
        // raw gradients at transformed feature pixels. Preserve the same
        // float magnitude/division while avoiding full-image normal planes.
        field.dx = dx;
        field.dy = dy;
        field.magnitude.create(gray.size(), CV_32F);
    }
    if (!geometry)
        cv::parallel_for_(cv::Range(0, gray.rows), [&](const cv::Range &range) {
            for (int y = range.start; y < range.end; ++y)
            {
                const auto *xrow = dx.ptr<short>(y);
                const auto *yrow = dy.ptr<short>(y);
                auto *nx = geometry ? nullptr : field.nx.ptr<float>(y);
                auto *ny = geometry ? nullptr : field.ny.ptr<float>(y);
                auto *amp = geometry ? nullptr : field.amplitude.ptr<short>(y);
                auto *mag = geometry ? field.magnitude.ptr<float>(y) : nullptr;
                auto *coarseAmp = geometry ? nullptr : field.coarseAmplitude.ptr<uchar>(y);
                contourcv2_detail::normalizeGradientRow(xrow, yrow, nx, ny, amp, mag, coarseAmp, gray.cols);
            }
        });
    if (!geometry)
        return field;
    // Search contrast is independent of model extraction's low/high thresholds.
    // Convert its byte amplitude unit back to raw Sobel magnitude for this
    // reconstructed Canny locator (the vendor edge-index builder is different).
    const double searchHigh = p.minContrast / 0.22360679507255554;
    cv::Canny(dx, dy, field.edges, searchHigh * .5, searchHigh, true);
    if (!mask.empty())
        field.edges.setTo(0, mask == 0);
    field.edges.row(0).setTo(0);
    field.edges.row(gray.rows - 1).setTo(0);
    field.edges.col(0).setTo(0);
    field.edges.col(gray.cols - 1).setTo(0);
    // Facets read only the 3x3 magnitude neighbourhood of retained Canny
    // seeds. Compute those values with the original sqrt, leaving unused
    // background uninitialised. Scoring computes its own sampled magnitude.
    cv::Mat needed;
    cv::dilate(field.edges, needed, cv::Mat(), cv::Point(-1, -1), 1, cv::BORDER_CONSTANT, cv::Scalar(0));
    cv::parallel_for_(cv::Range(0, gray.rows), [&](const cv::Range &range) {
        for (int y = range.start; y < range.end; ++y)
            contourcv2_detail::sparseGradientMagnitudeRow(dx.ptr<short>(y), dy.ptr<short>(y),
                                                          field.magnitude.ptr<float>(y), needed.ptr<uchar>(y),
                                                          gray.cols);
    });
    field.edgeWords = contourcv2_detail::buildEdgeOccupancy(field.edges);
    field.edgeOffsets = contourcv2_detail::edgeWordOffsets(field.edgeWords);
    field.geometry.create(field.edgeOffsets.back(), 1, CV_32FC4);
    const std::size_t wordsPerRow = (static_cast<std::size_t>(gray.cols) + 63) / 64;
    cv::parallel_for_(cv::Range(0, gray.rows), [&](const cv::Range &range) {
        for (int y = range.start; y < range.end; ++y)
        {
            for (std::size_t word = 0; word < wordsPerRow; ++word)
            {
                const std::size_t index = static_cast<std::size_t>(y) * wordsPerRow + word;
                auto bits = field.edgeWords[index];
                int edgeIndex = field.edgeOffsets[index];
                while (bits)
                {
                    const int x = int(word * 64) + int(contourcv2_detail::firstEdgeBit(bits));
                    bits &= bits - 1;
                    const auto sub =
                        contourcv_detail::facetPosition(field.magnitude, x, y, cv::Point2d(x, y));
                    const float length = field.magnitude.at<float>(y, x);
                    const float nx = length > 0 ? float(dx.at<short>(y, x)) / length : 0;
                    const float ny = length > 0 ? float(dy.at<short>(y, x)) / length : 0;
                    field.geometry.at<cv::Vec4f>(edgeIndex++) = {float(sub.x), float(sub.y), nx, ny};
                }
            }
        }
    });
    return field;
}

// Exact integer-sampling and thresholded unit-normal score recovered from
// bf6830/bf6b70: invalid/low-contrast points still contribute to the denominator.
double vmScore(const Pose &p, const std::vector<Feature> &features, const Field &field, bool polarity)
{
    if (features.empty())
        return 0;
    const float c = float(std::cos(p.angle)), s = float(std::sin(p.angle)), sx = float(p.sx),
                sy = float(p.sy);
    const float xx = c * sx, xy = -s * sy, yx = s * sx, yy = c * sy;
    float sum = 0;
    for (const auto &f : features)
    {
        const float fx = float(p.x) + xx * f.point.x + xy * f.point.y,
                    fy = float(p.y) + yx * f.point.x + yy * f.point.y;
        if (!(fx >= -0.5f && fy >= -0.5f && fx < float(field.dx.cols) - 0.5f &&
              fy < float(field.dx.rows) - 0.5f))
            continue;
        const int x = int(fx + 0.5f), y = int(fy + 0.5f);
        if (!field.mask.empty() && !field.mask.at<uchar>(y, x))
            continue;
        const short gx = field.dx.at<short>(y, x), gy = field.dy.at<short>(y, x);
        if (std::abs(gx) + std::abs(gy) < field.contrast)
            continue;
        float nx = c * (f.normal.x / sx) - s * (f.normal.y / sy),
              ny = s * (f.normal.x / sx) + c * (f.normal.y / sy);
        const float length = std::sqrt(nx * nx + ny * ny);
        if (length == 0)
            continue;
        nx /= length;
        ny /= length;
        const float targetGx = gx, targetGy = gy;
        const float targetLength = std::sqrt(targetGx * targetGx + targetGy * targetGy);
        const float targetNx = float(gx) / targetLength, targetNy = float(gy) / targetLength;
        float dot = nx * targetNx + ny * targetNy;
        if (!polarity)
            dot = std::abs(dot);
        if (dot > directionGate)
            sum += dot;
    }
    return sum / float(features.size());
}

// beffc0 confirms the 3x3 OR neighbourhood. The directional weight is recovered
// from static PE LUTs, not an assumed cosine. This reconstruction samples it
// into 8 unsigned / 16 signed bins; VM's cbcce0/cbcd50 bit encoding and its
// continuous-short-angle response branch have different private layouts.
std::vector<cv::Mat> coarseMaps(const Field &field, int contrast, bool polarity)
{
    const int count = polarity ? 16 : 8;
    cv::Mat bits = cv::Mat::zeros(field.nx.size(), CV_16U);
    for (int y = 1; y < bits.rows - 1; ++y)
    {
        const float *nx = field.nx.ptr<float>(y);
        const float *ny = field.ny.ptr<float>(y);
        const uchar *amp = field.coarseAmplitude.ptr<uchar>(y);
        auto *row = bits.ptr<unsigned short>(y);
        for (int x = 1; x < bits.cols - 1; ++x)
            if (amp[x] >= contrast)
                row[x] = static_cast<unsigned short>(1U << orientation(nx[x], ny[x], polarity));
    }
    cv::Mat spread = cv::Mat::zeros(bits.size(), bits.type());
    for (int y = 1; y < bits.rows - 1; ++y)
    {
        auto *dst = spread.ptr<unsigned short>(y);
        for (int x = 1; x < bits.cols - 1; ++x)
        {
            unsigned short value = 0;
            for (int dy = -1; dy <= 1; ++dy)
            {
                const auto *row = bits.ptr<unsigned short>(y + dy);
                value |= row[x - 1] | row[x] | row[x + 1];
            }
            dst[x] = value;
        }
    }
    std::vector<cv::Mat> maps(count);
    cv::parallel_for_(cv::Range(0, count), [&](const cv::Range &range) {
        for (int bin = range.start; bin < range.end; ++bin)
        {
            // At the current 512-unit bin spacing the recovered LUT is exactly
            // one at the same bin and zero at every other bin (both polarities).
            // Store that response as 0/1. Patterns contain at most 72 points,
            // so byte accumulation is exact and cannot overflow.
            const unsigned short bit = static_cast<unsigned short>(1U << bin);
            maps[bin].create(bits.size(), CV_8U);
            for (int y = 0; y < bits.rows; ++y)
            {
                auto *dst = maps[bin].ptr<uchar>(y);
                const auto *source = spread.ptr<unsigned short>(y);
                for (int x = 0; x < bits.cols; ++x)
                    dst[x] = (source[x] & bit) != 0;
            }
        }
    });
    return maps;
}
// Same responses, laid out by (x+y)&1 so checkerboard accumulation is
// contiguous. No positions, score values or candidate thresholds change.
using PackedMaps = std::vector<std::array<cv::Mat, 2>>;
void addResponses(uchar *target, const uchar *source, int count)
{
    int i = 0;
#if defined(_M_X64) || defined(__x86_64__) || defined(_M_IX86) || defined(__i386__)
    for (; i + 16 <= count; i += 16)
    {
        const auto a = _mm_loadu_si128(reinterpret_cast<const __m128i *>(target + i));
        const auto b = _mm_loadu_si128(reinterpret_cast<const __m128i *>(source + i));
        _mm_storeu_si128(reinterpret_cast<__m128i *>(target + i), _mm_add_epi8(a, b));
    }
#endif
    for (; i < count; ++i)
        target[i] += source[i];
}
#if defined(_M_X64) || defined(__x86_64__) || defined(_M_IX86) || defined(__i386__)
unsigned peakMask16(const uchar *row, const uchar *previous, const uchar *next, int parity, int threshold)
{
    const auto load = [](const uchar *p) { return _mm_loadu_si128(reinterpret_cast<const __m128i *>(p)); };
    // Responses are in [0,72], so signed byte comparisons are exact.
    const auto value = load(row);
    auto pass = _mm_cmpgt_epi8(value, _mm_set1_epi8(static_cast<char>(threshold - 1)));
    pass = _mm_and_si128(pass, _mm_cmpgt_epi8(value, load(row - 1)));
    pass = _mm_andnot_si128(_mm_cmpgt_epi8(load(row + 1), value), pass);
    pass = _mm_and_si128(pass, _mm_cmpgt_epi8(value, load(previous + parity - 1)));
    pass = _mm_andnot_si128(_mm_cmpgt_epi8(load(previous + parity), value), pass);
    pass = _mm_and_si128(pass, _mm_cmpgt_epi8(value, load(next + parity - 1)));
    pass = _mm_andnot_si128(_mm_cmpgt_epi8(load(next + parity), value), pass);
    return static_cast<unsigned>(_mm_movemask_epi8(pass));
}
#endif
PackedMaps packMaps(const std::vector<cv::Mat> &maps)
{
    PackedMaps packed(maps.size());
    for (std::size_t bin = 0; bin < maps.size(); ++bin)
        for (int phase = 0; phase < 2; ++phase)
        {
            auto &dst = packed[bin][phase];
            dst = cv::Mat::zeros(maps[bin].rows, (maps[bin].cols + 1) / 2, CV_8U);
            for (int y = 0; y < dst.rows; ++y)
            {
                const auto *source = maps[bin].ptr<uchar>(y);
                auto *target = dst.ptr<uchar>(y);
                for (int k = 0, x = (y + phase) & 1; x < maps[bin].cols; x += 2, ++k)
                    target[k] = source[x];
            }
        }
    return packed;
}
struct PoseAxes
{
    double xx, yx, xy, yy;
};
PoseAxes poseAxes(const Pose &p)
{
    const double c = std::cos(p.angle), s = std::sin(p.angle);
    return {c * p.sx, s * p.sx, s * p.sy, c * p.sy};
}
double projectedDistance(const Pose &a, const Pose &b, const PoseAxes &ma, const PoseAxes &mb,
                         cv::Size2d size)
{
    if (a.angle == b.angle && a.sx == b.sx && a.sy == b.sy)
        return (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y);
    const double ax = ma.xx - mb.xx, ay = ma.yx - mb.yx, bx = ma.xy - mb.xy, by = ma.yy - mb.yy;
    return (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) +
           .25 * (size.width * size.width * (ax * ax + ay * ay) +
                  size.height * size.height * (bx * bx + by * by));
}
std::vector<Pose> suppress(std::vector<Pose> poses, cv::Size2d size, double separation, std::size_t limit)
{
    std::stable_sort(poses.begin(), poses.end(),
                     [](const Pose &a, const Pose &b) { return a.score > b.score; });
    std::unordered_map<std::uint64_t, std::vector<std::size_t>> cells;
    std::vector<Pose> output;
    std::vector<PoseAxes> axes;
    auto key = [](int x, int y) { return (std::uint64_t(std::uint32_t(x)) << 32) | std::uint32_t(y); };
    for (const auto &p : poses)
    {
        const auto currentAxes = poseAxes(p);
        const int x = int(std::floor(p.x / separation)), y = int(std::floor(p.y / separation));
        bool duplicate = false;
        for (int dy = -1; dy <= 1 && !duplicate; ++dy)
            for (int dx = -1; dx <= 1 && !duplicate; ++dx)
            {
                const auto found = cells.find(key(x + dx, y + dy));
                if (found == cells.end())
                    continue;
                for (auto index : found->second)
                    if (projectedDistance(p, output[index], currentAxes, axes[index], size) <
                        separation * separation)
                    {
                        duplicate = true;
                        break;
                    }
            }
        if (!duplicate)
        {
            cells[key(x, y)].push_back(output.size());
            output.push_back(p);
            axes.push_back(currentAxes);
            if (output.size() >= limit)
                break;
        }
    }
    return output;
}

// Local edge correspondences and weighted point-to-normal equations follow
// b761d0/b764d0/b765f0. The private edge-index builder and solver ABI are not
// copied; a bounded oriented-neighbour index and a typed 5-DOF solve replace them.
bool refine(Pose &pose, const std::vector<Feature> &features, const Field &field, const Parameters &p,
            bool polarity, int radius, int iterations)
{
    const double startX = pose.x, startY = pose.y;
    bool changed = false;
    for (int iteration = 0; iteration < iterations; ++iteration)
    {
        const double c = std::cos(pose.angle), s = std::sin(pose.angle);
        cv::Matx<double, 5, 5> matrix = cv::Matx<double, 5, 5>::zeros();
        cv::Vec<double, 5> rhs = cv::Vec<double, 5>::all(0);
        int supported = 0;
        double totalWeight = 0;
        for (const auto &f : features)
        {
            const double xx = c * pose.sx * f.point.x - s * pose.sy * f.point.y,
                         yy = s * pose.sx * f.point.x + c * pose.sy * f.point.y;
            const double x = pose.x + xx, y = pose.y + yy;
            const double fnx = f.normal.x / pose.sx, fny = f.normal.y / pose.sy,
                         length = std::hypot(fnx, fny);
            const cv::Point2d n((c * fnx - s * fny) / length, (s * fnx + c * fny) / length);
            double best = double(radius * radius) + 1;
            cv::Vec4f selected{};
            double selectedDot = 0;
            const int cx = cvRound(x), cy = cvRound(y);
            const int left = std::max(1, cx - radius), right = std::min(field.edges.cols - 2, cx + radius);
            const std::size_t wordsPerRow = (static_cast<std::size_t>(field.edges.cols) + 63) / 64;
            for (int iy = std::max(1, cy - radius);
                 left <= right && iy <= std::min(field.edges.rows - 2, cy + radius); ++iy)
            {
                const std::size_t rowOffset = static_cast<std::size_t>(iy) * wordsPerRow;
                const auto *rowWords = field.edgeWords.data() + rowOffset;
                auto bits = contourcv2_detail::windowEdgeBits(rowWords, left, right);
                if (!bits)
                    continue;
                // findNonZero and the bit prefix both enumerate seeds in y/x
                // order. All occupied pixels inside the window are consecutive
                // geometry entries, including across a 64-pixel word boundary.
                const int shift = left & 63;
                int edgeIndex =
                    field.edgeOffsets[rowOffset + (static_cast<std::size_t>(left) >> 6)] +
                    contourcv2_detail::edgeBitCount(rowWords[left >> 6] & ((std::uint64_t(1) << shift) - 1));
                const auto *geometry = field.geometry.ptr<cv::Vec4f>();
                while (bits)
                {
                    bits &= bits - 1;
                    const auto edge = geometry[edgeIndex++];
                    double dot = n.x * edge[2] + n.y * edge[3];
                    if (!polarity)
                        dot = std::abs(dot);
                    if (dot <= directionGate)
                        continue;
                    const double distance = (x - edge[0]) * (x - edge[0]) + (y - edge[1]) * (y - edge[1]);
                    if (distance < best)
                    {
                        best = distance;
                        selected = edge;
                        selectedDot = dot;
                    }
                }
            }
            if (best > radius * radius)
                continue;
            const double residual = (x - selected[0]) * selected[2] + (y - selected[1]) * selected[3];
            // Piecewise residual weighting recovered from b765f0. The radii
            // are local-search engineering parameters, not recovered VM fields.
            const double weight =
                selectedDot *
                std::clamp((radius - std::abs(residual)) / std::max(1.0, double(radius) - .5), 0.0, 1.0);
            const cv::Vec<double, 5> j(selected[2], selected[3], -yy * selected[2] + xx * selected[3],
                                       f.point.x * (c * selected[2] + s * selected[3]),
                                       f.point.y * (-s * selected[2] + c * selected[3]));
            for (int a = 0; a < 5; ++a)
            {
                rhs[a] -= weight * j[a] * residual;
                for (int b = 0; b < 5; ++b)
                    matrix(a, b) += weight * j[a] * j[b];
            }
            ++supported;
            totalWeight += weight;
        }
        if (supported < int(features.size() * .35) || totalWeight < 8)
            break;
        for (int axis = 0; axis < 5; ++axis)
        {
            const bool locked = (axis == 2 && p.angleStart == p.angleEnd) ||
                                (axis == 3 && p.scaleToleranceX == 0) ||
                                (axis == 4 && p.scaleToleranceY == 0);
            if (locked)
            {
                for (int i = 0; i < 5; ++i)
                {
                    matrix(axis, i) = 0;
                    matrix(i, axis) = 0;
                }
                matrix(axis, axis) = 1;
                rhs[axis] = 0;
            }
            else
                matrix(axis, axis) += 1e-6;
        }
        cv::Vec<double, 5> update;
        if (!cv::solve(matrix, rhs, update, cv::DECOMP_CHOLESKY))
            break;
        if (!std::isfinite(update[0]) || !std::isfinite(update[1]) || !std::isfinite(update[2]) ||
            !std::isfinite(update[3]) || !std::isfinite(update[4]))
            break;
        pose.x += std::clamp(update[0], -1.5, 1.5);
        pose.y += std::clamp(update[1], -1.5, 1.5);
        pose.angle += std::clamp(update[2], -.06, .06);
        pose.sx += std::clamp(update[3], -.03, .03);
        pose.sy += std::clamp(update[4], -.03, .03);
        constrain(pose, p);
        changed = true;
        if (std::hypot(pose.x - startX, pose.y - startY) > radius * 4)
            break;
        if (std::hypot(update[0], update[1]) < .005 && std::abs(update[2]) < .0001 &&
            std::abs(update[3]) < .0001 && std::abs(update[4]) < .0001)
            break;
    }
    pose.score = vmScore(pose, features, field, polarity);
    return changed;
}
double overlap(const Pose &a, const Pose &b, cv::Size2d size)
{
    const cv::RotatedRect first(cv::Point2f(float(a.x), float(a.y)),
                                cv::Size2f(float(size.width * a.sx), float(size.height * a.sy)),
                                float(a.angle * 180 / pi));
    const cv::RotatedRect second(cv::Point2f(float(b.x), float(b.y)),
                                 cv::Size2f(float(size.width * b.sx), float(size.height * b.sy)),
                                 float(b.angle * 180 / pi));
    std::vector<cv::Point2f> points;
    const int kind = cv::rotatedRectangleIntersection(first, second, points);
    if (kind == cv::INTERSECT_NONE)
        return 0;
    if (kind == cv::INTERSECT_FULL)
        return 1;
    return points.size() < 3 ? 0
                             : std::abs(cv::contourArea(points)) /
                                   std::max(1e-12, double(std::min(first.size.area(), second.size.area())));
}
} // namespace

struct CContourMatchCV2::Impl
{
    Parameters parameters;
    Options options;
    Contours contours;
    cv::Point2d origin;
    cv::Size2d size;
    std::vector<std::vector<Feature>> features;
    std::vector<Pattern> patterns;
};
CContourMatchCV2::CContourMatchCV2() : impl_(new Impl) {}
CContourMatchCV2::~CContourMatchCV2() = default;
CContourMatchCV2::CContourMatchCV2(CContourMatchCV2 &&) noexcept = default;
CContourMatchCV2 &CContourMatchCV2::operator=(CContourMatchCV2 &&) noexcept = default;
void CContourMatchCV2::clear()
{
    impl_.reset(new Impl);
}
bool CContourMatchCV2::hasModel() const
{
    return impl_ && !impl_->features.empty();
}
const CContourMatchCV2::Contours &CContourMatchCV2::templateContours() const
{
    static const Contours empty;
    return impl_ ? impl_->contours : empty;
}
bool CContourMatchCV2::createModel(const cv::Mat &image, const cv::Mat &mask, const Parameters &p,
                                   std::string &message)
{
    return createModel(image, mask, p, Options{}, message);
}
bool CContourMatchCV2::createModel(const cv::Mat &image, const cv::Mat &mask, const Parameters &p,
                                   const Options &options, std::string &message)
{
    clear();
    message.clear();
    if (!imageValid(image) || !maskValid(mask, image.size()) || !parametersValid(p, options))
    {
        message = "Invalid model image, mask, options or parameters.";
        return false;
    }
    try
    {
        const cv::Mat gray = grayscale(image);
        const auto data = contourcv_detail::modelEdges(gray, mask, p.lowThreshold, p.highThreshold);
        const auto chains = contourcv_detail::traceCenterlines(data.edges);
        std::vector<Feature> all;
        cv::Mat selected = cv::Mat::zeros(data.edges.size(), CV_8U);
        cv::Point2d low(image.cols, image.rows), high(0, 0);
        for (const auto &chain : chains)
        {
            if (chain.size() < 2 || cv::arcLength(chain, false) < options.minContourLength)
                continue;
            std::vector<cv::Point2d> contour;
            for (const auto pixel : chain)
            {
                const float strength = data.magnitude.at<float>(pixel);
                if (strength <= 0)
                    continue;
                const auto point =
                    contourcv_detail::facetPosition(data.magnitude, pixel.x, pixel.y, cv::Point2d(pixel)) +
                    cv::Point2d(data.origin);
                contour.push_back(point);
                low.x = std::min(low.x, point.x);
                low.y = std::min(low.y, point.y);
                high.x = std::max(high.x, point.x);
                high.y = std::max(high.y, point.y);
                if (!selected.at<uchar>(pixel))
                {
                    all.push_back({cv::Point2f(point),
                                   {data.gx.at<float>(pixel) / strength, data.gy.at<float>(pixel) / strength},
                                   strength});
                    selected.at<uchar>(pixel) = 255;
                }
            }
            if (!contour.empty())
                impl_->contours.push_back(std::move(contour));
        }
        if (all.size() < 12 || high.x - low.x < 4 || high.y - low.y < 4)
        {
            clear();
            message = "Insufficient template edge features.";
            return false;
        }
        impl_->origin = (low + high) * .5;
        impl_->size = {high.x - low.x, high.y - low.y};
        impl_->parameters = p;
        impl_->options = options;
        for (auto &f : all)
            f.point -= cv::Point2f(impl_->origin);
        int levels = 1;
        if (p.numLevels > 0)
            levels = p.numLevels;
        else
            while (levels < 10 && std::min(impl_->size.width, impl_->size.height) / (1 << levels) >= 12)
                ++levels;
        while (levels > 1 && std::min(image.cols, image.rows) / (1 << (levels - 1)) < 8)
            --levels;
        cv::Mat levelGray = gray, levelMask = mask;
        impl_->features.push_back(sampled(all, 1200));
        for (int level = 1; level < levels; ++level)
        {
            cv::pyrDown(levelGray, levelGray);
            if (!mask.empty())
                cv::resize(mask, levelMask, levelGray.size(), 0, 0, cv::INTER_NEAREST);
            const auto edges =
                contourcv_detail::modelEdges(levelGray, levelMask, p.lowThreshold, p.highThreshold);
            std::vector<cv::Point> pixels;
            cv::findNonZero(edges.edges, pixels);
            std::vector<Feature> features;
            for (const auto pixel : pixels)
            {
                const float amplitude = edges.magnitude.at<float>(pixel);
                if (amplitude <= 0)
                    continue;
                const auto point =
                    contourcv_detail::facetPosition(edges.magnitude, pixel.x, pixel.y, cv::Point2d(pixel)) +
                    cv::Point2d(edges.origin) - impl_->origin / double(1 << level);
                features.push_back(
                    {cv::Point2f(point),
                     {edges.gx.at<float>(pixel) / amplitude, edges.gy.at<float>(pixel) / amplitude},
                     amplitude});
            }
            if (features.size() < 8)
                break;
            impl_->features.push_back(sampled(features, level == levels - 1 ? 72 : 320));
        }
        const int top = int(impl_->features.size()) - 1;
        const auto coarse = sampled(impl_->features.back(), 72);
        const double radius = .5 * std::hypot(impl_->size.width, impl_->size.height) / double(1 << top);
        const double angleStep =
            p.angleStep > 0 ? p.angleStep : std::clamp(180 / pi * .8 / std::max(2.0, radius), 2.0, 10.0);
        if ((p.angleEnd - p.angleStart) / angleStep > 3600)
        {
            clear();
            message = "Explicit angle step exceeds model budget.";
            return false;
        }
        auto scales = [](double tolerance) {
            return tolerance == 0 ? std::vector<double>{1}
                                  : std::vector<double>{1 - tolerance, 1, 1 + tolerance};
        };
        const int angleCount = int(std::ceil((p.angleEnd - p.angleStart) / angleStep));
        for (int ai = 0; ai <= angleCount; ++ai)
            for (double sx : scales(p.scaleToleranceX))
                for (double sy : scales(p.scaleToleranceY))
                {
                    Pattern pattern;
                    pattern.pose.angle = std::min(p.angleEnd, p.angleStart + ai * angleStep) * pi / 180;
                    pattern.pose.sx = sx;
                    pattern.pose.sy = sy;
                    for (const auto &f : coarse)
                    {
                        const auto point = mapped(f, pattern.pose);
                        const auto n = normal(f, pattern.pose);
                        pattern.offsets.emplace_back(cvRound(point.x), cvRound(point.y));
                        pattern.bins.push_back(
                            uchar(orientation(float(n.x), float(n.y), options.polarity == Polarity::Use)));
                    }
                    impl_->patterns.push_back(std::move(pattern));
                }
        message = "OK; independent VM-style gradient scoring and local edge refinement.";
        return true;
    }
    catch (const std::exception &e)
    {
        clear();
        message = e.what();
        return false;
    }
}

bool CContourMatchCV2::find(const cv::Mat &image, double minScore, int maxMatches, double maxOverlap,
                            std::vector<Match> &matches, std::string &message, const cv::Mat &searchMask,
                            FindProfile *profile) const
{
    matches.clear();
    message.clear();
    if (profile)
        *profile = FindProfile{};
    if (!hasModel())
    {
        message = "No contour model.";
        return false;
    }
    if (!imageValid(image) || !maskValid(searchMask, image.size()) || !std::isfinite(minScore) ||
        minScore < 0 || minScore > 1 || maxMatches < 1 || maxMatches > 9999 || !std::isfinite(maxOverlap) ||
        maxOverlap < 0 || maxOverlap > 1)
    {
        message = "Invalid search image, mask or matching parameters.";
        return false;
    }
    const auto start = Clock::now();
    auto mark = start;
    auto stage = [&](double *value) {
        const auto now = Clock::now();
        if (value)
            *value = std::chrono::duration<double, std::milli>(now - mark).count();
        mark = now;
    };
    auto checkDeadline = [&]() {
        if (impl_->options.timeoutMs > 0 && milliseconds(start) >= impl_->options.timeoutMs)
            throw std::runtime_error("Contour match timeout.");
    };
    try
    {
        const bool polarity = impl_->options.polarity == Polarity::Use;
        const auto &p = impl_->parameters;
        const int top = int(impl_->features.size()) - 1;
        std::vector<cv::Mat> pyramid(top + 1), masks(top + 1);
        pyramid[0] = grayscale(image);
        masks[0] = searchMask;
        for (int i = 1; i <= top; ++i)
        {
            cv::pyrDown(pyramid[i - 1], pyramid[i]);
            if (!searchMask.empty())
                cv::resize(searchMask, masks[i], pyramid[i].size(), 0, 0, cv::INTER_NEAREST);
        }
        checkDeadline();
        stage(profile ? &profile->pyramidMs : nullptr);
        if (profile)
        {
            profile->topLevel = top;
            profile->coarseVariants = impl_->patterns.size();
            profile->coarseUniquePatterns = impl_->patterns.size();
        }
        Field coarseField = makeField(pyramid[top], masks[top], p, false);
        const auto maps = coarseMaps(coarseField, p.minContrast, polarity);
        const auto packedMaps = packMaps(maps);
        stage(profile ? &profile->coarsePrepMs : nullptr);
        std::vector<std::vector<Pose>> localCandidates(impl_->patterns.size());
        cv::parallel_for_(cv::Range(0, int(impl_->patterns.size())), [&](const cv::Range &range) {
            for (int patternIndex = range.start; patternIndex < range.end; ++patternIndex)
            {
                const auto &pattern = impl_->patterns[patternIndex];
                int left = 1, upper = 1, right = maps[0].cols - 1, bottom = maps[0].rows - 1;
                cv::Mat score = cv::Mat::zeros(maps[0].rows, (maps[0].cols + 1) / 2, CV_8U);
                for (std::size_t feature = 0; feature < pattern.offsets.size(); ++feature)
                {
                    const auto offset = pattern.offsets[feature];
                    const auto &map = packedMaps[pattern.bins[feature]][(offset.x + offset.y) & 1];
                    const int featureLeft = std::max(left, 1 - offset.x),
                              featureRight = std::min(right, maps[0].cols - 1 - offset.x);
                    const int featureTop = std::max(upper, 1 - offset.y),
                              featureBottom = std::min(bottom, map.rows - 1 - offset.y);
                    for (int y = featureTop; y < featureBottom; ++y)
                    {
                        auto *target = score.ptr<uchar>(y);
                        const auto *source = map.ptr<uchar>(y + offset.y);
                        const int parity = y & 1, shift = (parity + offset.x - ((parity + offset.x) & 1)) / 2;
                        const int begin = (featureLeft + 1 - parity) / 2,
                                  end = (featureRight + 1 - parity) / 2;
                        if (end > begin)
                            addResponses(target + begin, source + begin + shift, end - begin);
                    }
                }
                const int threshold =
                    int(std::ceil(255 * pattern.offsets.size() * std::max(.3, minScore * .62)));
                const int countThreshold = (threshold + 254) / 255;
                for (int y = upper + 1; y < bottom - 1; ++y)
                {
                    const auto *row = score.ptr<uchar>(y);
                    const auto *prev = score.ptr<uchar>(y - 1);
                    const auto *next = score.ptr<uchar>(y + 1);
                    auto append = [&](int x, int value) {
                        if (!masks[top].empty() && !masks[top].at<uchar>(y, x))
                            return;
                        Pose candidate = pattern.pose;
                        candidate.x = x;
                        candidate.y = y;
                        candidate.score = double(value * 255) / (255 * pattern.offsets.size());
                        localCandidates[patternIndex].push_back(candidate);
                    };
                    int x = left + 2 + ((left + 2 + y) & 1);
#if defined(_M_X64) || defined(__x86_64__) || defined(_M_IX86) || defined(__i386__)
                    for (; x + 30 < right - 2; x += 32)
                    {
                        const int k = x / 2;
                        const unsigned mask = peakMask16(row + k, prev + k, next + k, y & 1, countThreshold);
                        if (mask)
                            for (int bit = 0; bit < 16; ++bit)
                                if (mask & (1U << bit))
                                    append(x + 2 * bit, row[k + bit]);
                    }
#endif
                    for (; x < right - 2; x += 2)
                    {
                        const int k = x / 2, parity = y & 1, value = row[k];
                        if (value < countThreshold || value <= row[k - 1] || value < row[k + 1] ||
                            value <= prev[k + parity - 1] || value < prev[k + parity] ||
                            value <= next[k + parity - 1] || value < next[k + parity])
                            continue;
                        append(x, value);
                    }
                }
                const double extent =
                    std::min(impl_->size.width * pattern.pose.sx, impl_->size.height * pattern.pose.sy) /
                    double(1 << top);
                localCandidates[patternIndex] =
                    suppress(std::move(localCandidates[patternIndex]), impl_->size / double(1 << top),
                             std::max(2.0, extent * .2), std::numeric_limits<std::size_t>::max());
            }
        });
        checkDeadline();
        stage(profile ? &profile->coarseSearchMs : nullptr);
        std::vector<Pose> candidates;
        for (auto &local : localCandidates)
            candidates.insert(candidates.end(), local.begin(), local.end());
        if (profile)
            profile->coarseCandidates = candidates.size();
        const auto coarseSize = impl_->size / double(1 << top);
        // Geometric suppression and limits are explicit reconstruction choices;
        // full vendor candidate-limit/step policy has not been recovered.
        candidates = suppress(std::move(candidates), coarseSize,
                              std::max(2.0, std::min(coarseSize.width, coarseSize.height) * .16),
                              std::size_t(std::max(1000, maxMatches * 8)));
        stage(profile ? &profile->coarseDiverseMs : nullptr);
        for (int level = top; level >= 0 && !candidates.empty(); --level)
        {
            checkDeadline();
            if (level != top)
                for (auto &pose : candidates)
                {
                    pose.x *= 2;
                    pose.y *= 2;
                }
            Field field = makeField(pyramid[level], masks[level], p, true);
            stage(profile ? &profile->levelPrepMs[level] : nullptr);
            if (profile)
                profile->levelCandidates[level] = candidates.size();
            cv::parallel_for_(cv::Range(0, int(candidates.size())), [&](const cv::Range &range) {
                for (int i = range.start; i < range.end; ++i)
                    refine(candidates[i], impl_->features[level], field, p, polarity, level == 0 ? 3 : 4,
                           level == 0 ? 10 : 6);
            });
            stage(profile ? &profile->levelOptimizeMs[level] : nullptr);
            const double gate = level == 0 ? minScore : std::max(.2, minScore * .62);
            candidates.erase(std::remove_if(candidates.begin(), candidates.end(),
                                            [&](const Pose &pose) { return pose.score < gate; }),
                             candidates.end());
            candidates = suppress(std::move(candidates), impl_->size / double(1 << level),
                                  level == 0 ? 2.0 : 1.5, std::size_t(std::max(1000, maxMatches * 8)));
            if (profile)
                profile->levelRetainedCandidates[level] = candidates.size();
            stage(profile ? &profile->levelDiverseMs[level] : nullptr);
        }
        checkDeadline();
        std::stable_sort(candidates.begin(), candidates.end(),
                         [](const Pose &a, const Pose &b) { return a.score > b.score; });
        std::vector<Pose> retained;
        for (const auto &pose : candidates)
        {
            const int x = cvRound(pose.x), y = cvRound(pose.y);
            if (x < 0 || y < 0 || x >= image.cols || y >= image.rows ||
                (!searchMask.empty() && !searchMask.at<uchar>(y, x)))
                continue;
            bool duplicate = false;
            for (const auto &existing : retained)
                if (std::hypot(pose.x - existing.x, pose.y - existing.y) <
                        std::max(impl_->size.width, impl_->size.height) * 2 &&
                    overlap(pose, existing, impl_->size) > maxOverlap)
                {
                    duplicate = true;
                    break;
                }
            if (duplicate)
                continue;
            retained.push_back(pose);
            if (int(retained.size()) >= maxMatches)
                break;
        }
        if (profile)
            profile->finalCandidates = candidates.size();
        stage(profile ? &profile->finalRefineMs : nullptr);
        for (const auto &pose : retained)
        {
            Match result;
            result.center = {pose.x, pose.y};
            result.angle = pose.angle * 180 / pi;
            result.scaleX = pose.sx;
            result.scaleY = pose.sy;
            result.score = pose.score;
            for (const auto &chain : impl_->contours)
            {
                std::vector<cv::Point2d> output;
                output.reserve(chain.size());
                for (const auto point : chain)
                {
                    Feature f{cv::Point2f(point - impl_->origin), {}, 0};
                    output.push_back(mapped(f, pose));
                }
                result.contours.push_back(std::move(output));
            }
            matches.push_back(std::move(result));
        }
        checkDeadline();
        stage(profile ? &profile->outputMs : nullptr);
        if (profile)
            profile->totalMs = milliseconds(start);
        message = "OK";
        return true;
    }
    catch (const std::exception &e)
    {
        matches.clear();
        if (profile)
            profile->totalMs = milliseconds(start);
        message = e.what();
        return false;
    }
}
