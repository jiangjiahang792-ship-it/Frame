#pragma once

#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>
#include <cmath>
#include <array>
#include <vector>

namespace contourcv_detail {

struct ModelEdges {
    cv::Point origin;
    cv::Mat edges, gx, gy, magnitude;
};

inline ModelEdges modelEdges(const cv::Mat& gray, const cv::Mat& mask, double low, double high)
{
    CV_Assert(gray.type() == CV_8UC1 && (mask.empty() || (mask.type() == CV_8UC1 && mask.size() == gray.size())));
    ModelEdges result;
    const cv::Rect imageBounds(0, 0, gray.cols, gray.rows);
    const cv::Rect domain = mask.empty() ? imageBounds : cv::boundingRect(mask);
    // Two pixels of filter support plus one for subpixel interpolation. The
    // original image is filtered through the mask boundary; no synthetic edge
    // is introduced by painting excluded pixels black.
    const cv::Rect roi = cv::Rect(domain.x - 3, domain.y - 3, domain.width + 6, domain.height + 6) & imageBounds;
    result.origin = roi.tl();
    const double sigma = 0.5;
    const double rootTwoSigma = std::sqrt(2.0) * sigma;
    const double s0 = std::erf(0.5 / rootTwoSigma);
    const double s2 = 0.5 * std::erfc(1.5 / rootTwoSigma);
    const double s1 = 0.5 * (1.0 - s0) - s2;
    // Integrate the Gaussian over each pixel cell. The derivative is the
    // difference of its boundary values, with scale sigma*sqrt(2*pi) so the
    // response is expressed in the same gray-level units as low/high.
    const double d2 = std::exp(-1.5 * 1.5 / (2.0 * sigma * sigma));
    const double d1 = std::exp(-0.5 * 0.5 / (2.0 * sigma * sigma)) - d2;
    const cv::Mat smoothing = (cv::Mat_<float>(1, 5) << s2, s1, s0, s1, s2);
    const cv::Mat derivative = (cv::Mat_<float>(1, 5) << -d2, -d1, 0, d1, d2);
    cv::sepFilter2D(gray(roi), result.gx, CV_32F, derivative, smoothing, cv::Point(-1, -1), 0, cv::BORDER_REPLICATE);
    cv::sepFilter2D(gray(roi), result.gy, CV_32F, smoothing, derivative, cv::Point(-1, -1), 0, cv::BORDER_REPLICATE);
    cv::magnitude(result.gx, result.gy, result.magnitude);
    // Canny accepts short derivatives. Fractional precision is retained with
    // a fixed numeric scale, applied equally to derivatives and thresholds.
    // With sigma=.5 each derivative stays below 255*exp(-.5), hence no clipping.
    constexpr double quantization = 128.0;
    cv::Mat dx, dy;
    result.gx.convertTo(dx, CV_16S, quantization);
    result.gy.convertTo(dy, CV_16S, quantization);
    cv::Mat candidates;
    cv::Canny(dx, dy, candidates, std::lround(low) * quantization, std::lround(low) * quantization, true);
    if (!mask.empty()) candidates.setTo(0, mask(roi) == 0);
    // Hysteresis is confined to the model domain. An excluded strong edge
    // must not keep a weak fragment inside the mask alive through an outside
    // connection. Filtering/NMS still sees the original neighboring pixels.
    result.edges = cv::Mat::zeros(candidates.size(), CV_8U);
    std::vector<cv::Point> pixels, pending;
    cv::findNonZero(candidates, pixels);
    pending.reserve(pixels.size());
    for (cv::Point p : pixels) {
        if (result.magnitude.at<float>(p) >= std::lround(high)) {
            result.edges.at<uchar>(p) = 255;
            pending.push_back(p);
        }
    }
    for (size_t index = 0; index < pending.size(); ++index) {
        const cv::Point p = pending[index];
        for (int oy = -1; oy <= 1; ++oy) for (int ox = -1; ox <= 1; ++ox) {
            const cv::Point q = p + cv::Point(ox, oy);
            if (q.x < 0 || q.y < 0 || q.x >= candidates.cols || q.y >= candidates.rows
                || !candidates.at<uchar>(q) || result.edges.at<uchar>(q)) continue;
            result.edges.at<uchar>(q) = 255;
            pending.push_back(q);
        }
    }
    result.edges.row(0).setTo(0);
    result.edges.row(result.edges.rows - 1).setTo(0);
    result.edges.col(0).setTo(0);
    result.edges.col(result.edges.cols - 1).setTo(0);
    return result;
}
// A 3x3 quadratic facet fitted to gradient magnitude locates the ridge along
// the most negative Hessian eigenvector. This treats diagonal edges in 2D,
// avoiding the sampling bias of a one-dimensional magnitude parabola.
inline cv::Point2d facetPosition(const cv::Mat& magnitude, int x, int y,
                                 cv::Point2d fallback)
{
    if (x < 1 || y < 1 || x + 1 >= magnitude.cols || y + 1 >= magnitude.rows) return fallback;
    const float* previous = magnitude.ptr<float>(y - 1) + x;
    const float* current = magnitude.ptr<float>(y) + x;
    const float* next = magnitude.ptr<float>(y + 1) + x;
    const double fx = (previous[1] - previous[-1] + current[1] - current[-1] + next[1] - next[-1]) / 6.0;
    const double fy = (next[-1] - previous[-1] + next[0] - previous[0] + next[1] - previous[1]) / 6.0;
    const double fxx = (previous[-1] - 2.0 * previous[0] + previous[1]
        + current[-1] - 2.0 * current[0] + current[1] + next[-1] - 2.0 * next[0] + next[1]) / 3.0;
    const double fyy = (previous[-1] - 2.0 * current[-1] + next[-1]
        + previous[0] - 2.0 * current[0] + next[0] + previous[1] - 2.0 * current[1] + next[1]) / 3.0;
    const double fxy = (previous[-1] - previous[1] - next[-1] + next[1]) / 4.0;
    const double lambda = 0.5 * (fxx + fyy - std::hypot(fxx - fyy, 2.0 * fxy));
    if (lambda >= -1e-9) return fallback;
    cv::Point2d normal = std::abs(fxx - lambda) > std::abs(fyy - lambda)
        ? cv::Point2d(fxy, lambda - fxx) : cv::Point2d(lambda - fyy, fxy);
    const double norm = cv::norm(normal);
    if (norm < 1e-12) return fallback;
    normal *= 1.0 / norm;
    const double t = -(fx * normal.x + fy * normal.y) / lambda;
    const cv::Point2d offset = t * normal;
    // Keep the seed's local fit: near junctions a flat facet may otherwise
    // extrapolate arbitrarily far. A diagonal displacement may exceed .6 in
    // length while remaining within .6 in both image coordinates.
    if (!std::isfinite(t) || std::abs(offset.x) > 0.6 || std::abs(offset.y) > 0.6) return fallback;
    return cv::Point2d(x, y) + offset;
}
// Trace the centerline graph of a one-pixel edge map. findContours traces the
// boundary of nonzero regions, so using it on a thin open edge walks the same
// physical edge in both directions. Here each graph link is consumed once;
// junctions end chains and may be shared by their incident chains.
inline std::vector<std::vector<cv::Point>> traceCenterlines(const cv::Mat& edges)
{
    CV_Assert(edges.type() == CV_8UC1);
    constexpr std::array<int, 8> dx{{1, 1, 0, -1, -1, -1, 0, 1}};
    constexpr std::array<int, 8> dy{{0, 1, 1, 1, 0, -1, -1, -1}};
    cv::Mat links = cv::Mat::zeros(edges.size(), CV_8U);
    cv::Mat visited = cv::Mat::zeros(edges.size(), CV_8U);
    std::vector<cv::Point> pixels;
    cv::findNonZero(edges, pixels);
    const auto edgeAt = [&](int x, int y) {
        return x >= 0 && y >= 0 && x < edges.cols && y < edges.rows
            && edges.at<uchar>(y, x) != 0;
    };
    const auto degree = [](unsigned bits) {
        int count = 0;
        while (bits) { bits &= bits - 1; ++count; }
        return count;
    };
    for (const cv::Point p : pixels) {
        unsigned adjacency = 0;
        for (int direction = 0; direction < 8; ++direction) {
            if (!edgeAt(p.x + dx[direction], p.y + dy[direction])) continue;
            // A diagonal shortcut through an existing orthogonal connection
            // would create a triangle at an ordinary digital corner.
            if (dx[direction] && dy[direction]
                && (edgeAt(p.x + dx[direction], p.y) || edgeAt(p.x, p.y + dy[direction]))) continue;
            adjacency |= 1U << direction;
        }
        links.at<uchar>(p) = static_cast<uchar>(adjacency);
    }
    std::vector<std::vector<cv::Point>> result;
    const auto walk = [&](cv::Point start, int firstDirection) {
        std::vector<cv::Point> chain{start};
        cv::Point current = start;
        int direction = firstDirection;
        for (;;) {
            visited.at<uchar>(current) |= static_cast<uchar>(1U << direction);
            const cv::Point next = current + cv::Point(dx[direction], dy[direction]);
            visited.at<uchar>(next) |= static_cast<uchar>(1U << ((direction + 4) % 8));
            chain.push_back(next);
            if (next == start || degree(links.at<uchar>(next)) != 2) break;
            const unsigned remaining = links.at<uchar>(next) & ~visited.at<uchar>(next);
            if (!remaining) break;
            direction = 0;
            while (!(remaining & (1U << direction))) ++direction;
            current = next;
        }
        return chain;
    };
    // Open chains and branches are traced before closed cycles.
    for (const cv::Point p : pixels) {
        const unsigned adjacency = links.at<uchar>(p);
        if (degree(adjacency) == 2) continue;
        if (!adjacency) { result.push_back({p}); continue; }
        for (int direction = 0; direction < 8; ++direction)
            if ((adjacency & (1U << direction)) && !(visited.at<uchar>(p) & (1U << direction)))
                result.push_back(walk(p, direction));
    }
    for (const cv::Point p : pixels) {
        const unsigned remaining = links.at<uchar>(p) & ~visited.at<uchar>(p);
        if (!remaining) continue;
        int direction = 0;
        while (!(remaining & (1U << direction))) ++direction;
        result.push_back(walk(p, direction));
    }
    return result;
}

} // namespace contourcv_detail