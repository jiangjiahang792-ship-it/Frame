#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "CContourMatchCV.h"
#include "ContourScorePeaks.h"
#include "ContourCoarseResponse.h"
#include "ContourSubpixelEdges.h"
#include "ContourResponseMaps.h"
#include "ContourScoreSimd.h"
#include "ContourResponseAffineCache.h"
#include "ContourGradientScore.h"

#include <opencv2/imgproc.hpp>
#include <algorithm>
#include <array>
#include <chrono>
#include <stdexcept>
#include <cmath>
#include <limits>
#include <numeric>
#include <unordered_map>
#include <set>
#include <cstdint>
#include <cstring>

namespace {
constexpr double kPi = 3.14159265358979323846;
constexpr int kDirections = 12;
constexpr int kMaximumLevels = 10;
constexpr size_t kMaximumPixels = 32U * 1024U * 1024U;
using Parameters = CContourMatchCV::Parameters;
using Contours = CContourMatchCV::Contours;
using Options = CContourMatchCV::Options;

struct Feature {
    cv::Point2d point;
    cv::Point2d normal;
    double strength = 1.0;
};
struct Pose {
    double x = 0, y = 0, angle = 0, sx = 1, sy = 1, score = 0;
    int profileIndex = -1;
};
struct Variant {
    Pose pose;
    std::size_t group = 0;
    int sampleCount = 0;
};
struct CoarsePeak {
    cv::Point position;
    double score = 0;
};
struct SearchLevel {
    cv::Point2d origin;
    cv::Mat edges, gx, gy, magnitude, mask;
    std::array<cv::Mat, kDirections * 2> response;
    contourcv_score::ResponseView responseView;
    contourcv_score::GradientView gradientView;
    bool usePolarity = false;
    double minContrast = 1;

    cv::Mat edgeRowPrefix;
    cv::Mat edgeGeometry;
};

bool validImage(const cv::Mat& image)
{
    return !image.empty() && image.cols >= 8 && image.rows >= 8
        && image.cols <= 32768 && image.rows <= 32768
        && image.total() <= kMaximumPixels
        && (image.type() == CV_8UC1 || image.type() == CV_8UC3
            || image.type() == CV_8UC4);
}
cv::Mat gray8(const cv::Mat& image)
{
    if (image.type() == CV_8UC1)
        return image;
    cv::Mat gray;
    cv::cvtColor(image, gray, image.channels() == 3
        ? cv::COLOR_BGR2GRAY : cv::COLOR_BGRA2GRAY);
    return gray;
}
bool validParameters(const Parameters& p)
{
    return std::isfinite(p.lowThreshold) && std::isfinite(p.highThreshold)
        && p.lowThreshold > 0 && p.highThreshold > p.lowThreshold
        && p.highThreshold <= 255 && p.numLevels >= 0 && p.numLevels <= 10
        && std::isfinite(p.angleStart) && std::isfinite(p.angleEnd)
        && p.angleStart >= -180 && p.angleEnd <= 180 && p.angleEnd >= p.angleStart
        && std::isfinite(p.angleStep) && p.angleStep >= 0 && p.angleStep <= 180
        && p.minContrast >= 1 && p.minContrast <= 255
        && std::isfinite(p.scaleToleranceX) && p.scaleToleranceX >= 0 && p.scaleToleranceX < 1
        && std::isfinite(p.scaleToleranceY) && p.scaleToleranceY >= 0 && p.scaleToleranceY < 1;
}
bool validMask(const cv::Mat& mask, cv::Size size)
{
    return mask.empty() || (mask.type() == CV_8UC1 && mask.size() == size
                           && cv::countNonZero(mask) != 0);
}

float bilinear(const cv::Mat& image, double x, double y)
{
    // Valid interpolation coordinates are nonnegative, so integer truncation
    // equals floor exactly. Reject the boundary first without libm floor calls.
    if (!(x >= 0 && y >= 0 && x < image.cols - 1.0 && y < image.rows - 1.0))
        return 0;
    const int ix = static_cast<int>(x);
    const int iy = static_cast<int>(y);
    const double fx = x - ix, fy = y - iy;
    const float* row = image.ptr<float>(iy) + ix;
    const float* next = image.ptr<float>(iy + 1) + ix;
    return static_cast<float>((1 - fy) * ((1 - fx) * row[0] + fx * row[1])
                             + fy * ((1 - fx) * next[0] + fx * next[1]));
}

void edgeImage(const cv::Mat& gray, const cv::Mat& mask, double low, double high,
               cv::Mat& edges, cv::Mat& gx, cv::Mat& gy, cv::Mat& magnitude, bool keepGeometry)
{
    // Derivatives see the original image: a mask boundary must never create an edge.
    cv::Mat smooth;
    cv::GaussianBlur(gray, smooth, cv::Size(3, 3), 0.65, 0.65, cv::BORDER_REPLICATE);
    cv::Mat dx, dy;
    cv::Sobel(smooth, dx, CV_16S, 1, 0, 3, 1, 0, cv::BORDER_REPLICATE);
    cv::Sobel(smooth, dy, CV_16S, 0, 1, 3, 1, 0, cv::BORDER_REPLICATE);
    cv::Canny(dx, dy, edges, low, high, true);
    if (keepGeometry) {
        dx.convertTo(gx, CV_32F);
        dy.convertTo(gy, CV_32F);
        cv::magnitude(gx, gy, magnitude);
    } else {
        gx = dx;
        gy = dy;
    }
    if (!mask.empty())
        edges.setTo(0, mask == 0);
    edges.row(0).setTo(0);
    edges.row(edges.rows - 1).setTo(0);
    edges.col(0).setTo(0);
    edges.col(edges.cols - 1).setTo(0);
}

Feature edgeFeature(const cv::Mat& gx, const cv::Mat& gy,
                    const cv::Mat& magnitude, int x, int y)
{
    const double strength = std::max(1e-6, static_cast<double>(magnitude.at<float>(y, x)));
    const cv::Point2d normal(gx.at<float>(y, x) / strength,
                            gy.at<float>(y, x) / strength);
    const double before = bilinear(magnitude, x - normal.x, y - normal.y);
    const double after = bilinear(magnitude, x + normal.x, y + normal.y);
    const double denominator = before - 2 * strength + after;
    const double offset = denominator < -1e-6
        ? std::clamp(0.5 * (before - after) / denominator, -0.6, 0.6) : 0.0;
    return {{x + offset * normal.x, y + offset * normal.y}, normal, strength};
}

int direction(const cv::Point2d& normal, bool usePolarity = false)
{
    // Twelve unsigned orientation bins, without atan2 in the scoring loop.
    // Boundaries are tan(7.5 + 15*k degrees).
    const double x = std::abs(normal.x), y = std::abs(normal.y);
    int bin = 6;
    if (y < x * 0.131652497587) bin = 0;
    else if (y < x * 0.414213562373) bin = 1;
    else if (y < x * 0.767326987979) bin = 2;
    else if (y < x * 1.303225372841) bin = 3;
    else if (y < x * 2.414213562373) bin = 4;
    else if (y < x * 7.595754112725) bin = 5;
    if (normal.x >= 0) bin = normal.y >= 0 ? bin : 24 - bin;
    else bin = normal.y >= 0 ? 12 - bin : 12 + bin;
    return bin % (usePolarity ? 24 : 12);
}
cv::Point2d transform(const cv::Point2d& point, const Pose& pose)
{
    const double c = std::cos(pose.angle), s = std::sin(pose.angle);
    return {pose.x + c * pose.sx * point.x - s * pose.sy * point.y,
            pose.y + s * pose.sx * point.x + c * pose.sy * point.y};
}
cv::Point2d transformNormal(const cv::Point2d& normal, const Pose& pose)
{
    const double c = std::cos(pose.angle), s = std::sin(pose.angle);
    const double nx = normal.x / pose.sx, ny = normal.y / pose.sy;
    const double norm = std::sqrt(nx * nx + ny * ny);
    return {(c * nx - s * ny) / norm, (s * nx + c * ny) / norm};
}

// One set of coefficients per pose, shared by all of its contour samples.
struct PoseTransform {
    const Pose& pose;
    double c, s;
    explicit PoseTransform(const Pose& p) : pose(p), c(std::cos(p.angle)), s(std::sin(p.angle)) {}
    cv::Point2d point(const cv::Point2d& p) const {
        return {pose.x + c * pose.sx * p.x - s * pose.sy * p.y,
                pose.y + s * pose.sx * p.x + c * pose.sy * p.y};
    }
    cv::Point2d normal(const cv::Point2d& n) const {
        const double nx = n.x / pose.sx, ny = n.y / pose.sy;
        const double norm = std::sqrt(nx * nx + ny * ny);
        return {(c * nx - s * ny) / norm, (s * nx + c * ny) / norm};
    }
};

void cacheEdgeGeometry(SearchLevel& level)
{
    // Index sparse edge geometry instead of allocating four full-resolution
    // floating-point maps. Every target edge is localized only once per search.
    std::vector<cv::Point> pixels;
    cv::findNonZero(level.edges, pixels);

    level.edgeGeometry.create(static_cast<int>(pixels.size()), 1, CV_64FC4);
    cv::parallel_for_(cv::Range(0, static_cast<int>(pixels.size())), [&](const cv::Range& range) {
        for (int index = range.start; index < range.end; ++index) {
            const cv::Point pixel = pixels[index];
            const Feature feature = edgeFeature(level.gx, level.gy, level.magnitude, pixel.x, pixel.y);

            // Preserve the established float-offset/normal quantization, then
            // cache the exact global doubles reused by every ICP query.
            level.edgeGeometry.ptr<cv::Vec4d>()[index] = cv::Vec4d(
                pixel.x + static_cast<double>(static_cast<float>(feature.point.x - pixel.x)) + level.origin.x,
                pixel.y + static_cast<double>(static_cast<float>(feature.point.y - pixel.y)) + level.origin.y,
                static_cast<double>(static_cast<float>(feature.normal.x)),
                static_cast<double>(static_cast<float>(feature.normal.y)));
        }
    });
    level.edgeRowPrefix.create(level.edges.rows, level.edges.cols + 1, CV_32S);

    std::vector<int> rowStarts(level.edges.rows + 1, 0);
    for (const cv::Point pixel : pixels) ++rowStarts[pixel.y + 1];
    std::partial_sum(rowStarts.begin(), rowStarts.end(), rowStarts.begin());
    cv::parallel_for_(cv::Range(0, level.edges.rows), [&](const cv::Range& range) {
        for (int y = range.start; y < range.end; ++y) {
            int* row = level.edgeRowPrefix.ptr<int>(y);
            int index = rowStarts[y];
            const uchar* edges = level.edges.ptr<uchar>(y);
            for (int x = 0; x < level.edges.cols; ++x) {
                row[x] = index;
                index += edges[x] != 0;
            }
            row[level.edges.cols] = index;
        }
    });
    level.magnitude.release();
}
std::vector<Feature> sampled(const std::vector<Feature>& source, int limit, double divisor)
{
    std::vector<Feature> result;
    const int count = std::min(limit, static_cast<int>(source.size()));
    result.reserve(count);
    if (source.size() <= static_cast<size_t>(limit)) {
        result = source;
        for (auto& feature : result) { feature.point *= 1.0 / divisor; feature.strength = 1; }
        return result;
    }
    // Contrast-weighted arc-length sampling suppresses unstable low-contrast
    // surface texture without turning the model into unrelated gradient maxima.
    double mass = 0;
    for (const Feature& feature : source) mass += feature.strength;
    size_t index = 0;
    double cumulative = source.empty() ? 0 : source.front().strength;
    for (int i = 0; i < count; ++i) {
        const double target = (i + 0.5) * mass / count;
        while (index + 1 < source.size() && cumulative < target)
            cumulative += source[++index].strength;
        Feature feature = source[index];
        feature.point *= 1.0 / divisor;
        feature.strength = 1; // Do not apply contrast weighting twice at the next sampling stage.
        result.push_back(feature);
    }
    return result;
}

std::vector<double> intervals(double first, double last, double step)
{
    if (last - first < 1e-12)
        return {first};
    const int count = std::max(1, static_cast<int>(std::ceil((last - first) / step)));
    std::vector<double> values;
    values.reserve(count + 1);
    for (int i = 0; i <= count; ++i)
        values.push_back(first + (last - first) * i / count);
    return values;
}

SearchLevel searchLevel(const cv::Mat& image, const cv::Mat& inputMask,
                        const Parameters& parameters, const Options& options, cv::Rect roi = {}, bool buildResponse = true, bool keepGeometry = true)
{
    SearchLevel result;
    if (roi.empty()) roi = cv::Rect(0, 0, image.cols, image.rows);
    const cv::Mat gray = image(roi);
    result.origin = cv::Point2d(roi.x, roi.y);
    result.usePolarity = options.polarity == CContourMatchCV::Polarity::Use;
    result.minContrast = parameters.minContrast;
    result.mask = inputMask.empty() ? cv::Mat() : inputMask(roi);
    edgeImage(gray, {}, std::max(1.0, parameters.minContrast * 0.5),
              parameters.minContrast, result.edges, result.gx, result.gy, result.magnitude, keepGeometry);
    if (keepGeometry)
        result.gradientView = contourcv_score::makeGradientView(result.gx, result.gy,
            result.origin.x, result.origin.y, result.minContrast, result.usePolarity);
    if (!buildResponse || !cv::countNonZero(result.edges)) return result;
    cv::Mat labels(gray.size(), CV_8U, cv::Scalar(255));
    for (int y = 0; y < gray.rows; ++y) {
        const uchar* edge = result.edges.ptr<uchar>(y);
        uchar* label = labels.ptr<uchar>(y);
        if (keepGeometry) {
            const float* gx = result.gx.ptr<float>(y);
            const float* gy = result.gy.ptr<float>(y);
            for (int x = 0; x < gray.cols; ++x)
                if (edge[x]) label[x] = static_cast<uchar>(direction({gx[x], gy[x]}, result.usePolarity));
        } else {
            const short* gx = result.gx.ptr<short>(y);
            const short* gy = result.gy.ptr<short>(y);
            for (int x = 0; x < gray.cols; ++x)
                if (edge[x]) label[x] = static_cast<uchar>(direction({double(gx[x]), double(gy[x])}, result.usePolarity));
        }
    }
    contourcv_detail::DirectionResponseMaps::build(labels, result.usePolarity, result.response);
    result.responseView = contourcv_score::makeResponseView(result.response, result.origin.x, result.origin.y, result.usePolarity);
    if (!keepGeometry) { result.gx.release(); result.gy.release(); }
    return result;
}

bool centerAllowed(const SearchLevel& level, const Pose& pose)
{
    const int x = cvRound(pose.x - level.origin.x), y = cvRound(pose.y - level.origin.y);
    return x >= 0 && y >= 0 && x < level.edges.cols && y < level.edges.rows
        && (level.mask.empty() || level.mask.at<uchar>(y, x) != 0);
}
double responseScore(const SearchLevel& level, const contourcv_score::FeatureSoA& features,
                     const Pose& pose, double cutoff = 0)
{
    if (!centerAllowed(level, pose)) return 0;
    return contourcv_score::scoreResponse(level.responseView, features,
        {pose.x, pose.y, pose.angle, pose.sx, pose.sy}, contourcv_score::Backend::Auto, cutoff);
}
void constrain(Pose& pose, const Parameters& p)
{
    pose.angle = std::clamp(pose.angle, p.angleStart * kPi / 180, p.angleEnd * kPi / 180);
    pose.sx = std::clamp(pose.sx, 1 - p.scaleToleranceX, 1 + p.scaleToleranceX);
    pose.sy = std::clamp(pose.sy, 1 - p.scaleToleranceY, 1 + p.scaleToleranceY);
}
double finalScore(const Pose& pose, const SearchLevel& level,
                  const contourcv_score::FeatureSoA& features, double cutoff = 0);

struct ScoreEvaluationStats {
    size_t calls = 0, cacheHits = 0, affineLookups = 0, affineHits = 0, sharedScoreHits = 0;
};

// Worker-local cache for one immutable level/image/feature binding. Positive
// values are complete scores; a cutoff-truncated zero can be reused only at
// an equal or stricter cutoff. This stores scores, never optimization suffixes.
class ResponseScoreCache {
public:
    using Key = std::array<uint64_t, 5>;
    struct Entry {
        Key key{};
        double score = 0, cutoff = 0;
        bool occupied = false;
    };
    static constexpr size_t capacity = 8192;
    ResponseScoreCache(const SearchLevel& level, const contourcv_score::FeatureSoA& features)
        : level_(&level), features_(&features), entries_(capacity) {}
    ResponseScoreCache(const ResponseScoreCache&) = delete;
    ResponseScoreCache& operator=(const ResponseScoreCache&) = delete;

    bool boundTo(const SearchLevel& level, const contourcv_score::FeatureSoA& features) const {
        return level_ == &level && features_ == &features;
    }
    static Key makeKey(const std::array<double, 5>& values) {
        static_assert(sizeof(double) == sizeof(uint64_t), "Exact score keys require 64-bit doubles");
        Key key;
        std::memcpy(key.data(), values.data(), values.size() * sizeof(double));
        return key;
    }
    Entry& entry(const Key& key) {
        uint64_t hash = UINT64_C(0x6a09e667f3bcc909);
        for (uint64_t bits : key) hash = (hash ^ bits) * UINT64_C(0x9e3779b97f4a7c15);
        hash ^= hash >> 30;
        hash *= UINT64_C(0xbf58476d1ce4e5b9);
        hash ^= hash >> 27;
        hash *= UINT64_C(0x94d049bb133111eb);
        hash ^= hash >> 31;
        return entries_[static_cast<size_t>(hash & (capacity - 1))];
    }
private:
    const SearchLevel* const level_;
    const contourcv_score::FeatureSoA* const features_;
    std::vector<Entry> entries_;
};
void localOptimize(Pose& pose, const SearchLevel& level,
                   const std::vector<Feature>& features, const Parameters& p, double radius, bool exact = false,
                   const contourcv_score::FeatureSoA* responseFeatures = nullptr, ScoreEvaluationStats* statistics = nullptr,
                   contourcv_score::PreparedResponse* preparedStorage = nullptr,
                   contourcv_score::ResponseAffineCache<>* affineCache = nullptr,
                   ResponseScoreCache* sharedScoreCache = nullptr)
{
    struct Evaluation {
        std::array<double, 5> key{};
        double score = 0, cutoff = 0;
        bool occupied = false;
    };
    std::array<Evaluation, 32> evaluations{};
    // Final gradient refinement retains its original per-call cache. A shared
    // response score must never be used for another level or scoring mode.
    if (exact) sharedScoreCache = nullptr;
    else if (sharedScoreCache) CV_Assert(sharedScoreCache->boundTo(level, *responseFeatures));
    // Prepared scratch validity remains local when no affine cache is supplied.
    contourcv_score::PreparedResponse localPrepared;
    contourcv_score::PreparedResponse& prepared = preparedStorage ? *preparedStorage : localPrepared;
    double preparedAngle = 0, preparedScaleX = 0, preparedScaleY = 0;
    bool preparedValid = false;
    const auto scorePose = [&](const Pose& candidate, double cutoff = 0.0, bool translationTrial = false) {
        if (statistics) ++statistics->calls;
        const std::array<double, 5> key{{candidate.x, candidate.y, candidate.angle, candidate.sx, candidate.sy}};
        Evaluation* localSaved = nullptr;
        ResponseScoreCache::Key sharedKey;
        ResponseScoreCache::Entry* sharedSaved = nullptr;
        if (sharedScoreCache) {
            sharedKey = ResponseScoreCache::makeKey(key);
            sharedSaved = &sharedScoreCache->entry(sharedKey);
            if (sharedSaved->occupied && sharedSaved->key == sharedKey
                && (sharedSaved->score != 0 || sharedSaved->cutoff <= 0 || cutoff >= sharedSaved->cutoff)) {
                if (statistics) { ++statistics->cacheHits; ++statistics->sharedScoreHits; }
                return sharedSaved->score;
            }
        } else {
            uint64_t hash = 0;
            for (const double value : key) {
                uint64_t bits;
                std::memcpy(&bits, &value, sizeof(bits));
                hash = (hash ^ bits) * UINT64_C(0x9e3779b97f4a7c15);
            }
            localSaved = &evaluations[(hash ^ (hash >> 32)) & 31];
            if (localSaved->occupied && localSaved->key == key
                && (localSaved->score != 0 || localSaved->cutoff <= 0 || cutoff >= localSaved->cutoff)) {
                if (statistics) ++statistics->cacheHits;
                return localSaved->score;
            }
        }
        double value;
        if (exact) value = finalScore(candidate, level, *responseFeatures, cutoff);
        else if (affineCache) {
            if (!centerAllowed(level, candidate)) value = 0;
            else {
                const auto previousHits = affineCache->statistics().hits;
                const auto& transformed = affineCache->get(candidate.angle, candidate.sx, candidate.sy);
                if (statistics) {
                    ++statistics->affineLookups;
                    statistics->affineHits += affineCache->statistics().hits != previousHits;
                }
                value = contourcv_score::scorePrepared(level.responseView, transformed, candidate.x, candidate.y,
                    contourcv_score::Backend::Auto, cutoff);
            }
        }
        else if (translationTrial && centerAllowed(level, candidate)) {
            if (!preparedValid || candidate.angle != preparedAngle
                || candidate.sx != preparedScaleX || candidate.sy != preparedScaleY) {
                contourcv_score::prepareResponse(*responseFeatures, candidate.angle,
                    candidate.sx, candidate.sy, level.usePolarity, prepared);
                preparedAngle = candidate.angle; preparedScaleX = candidate.sx; preparedScaleY = candidate.sy;
                preparedValid = true;
            }
            value = contourcv_score::scorePrepared(level.responseView, prepared, candidate.x, candidate.y,
                contourcv_score::Backend::Auto, cutoff);
        } else value = responseScore(level, *responseFeatures, candidate, cutoff);
        if (sharedSaved) {
            sharedSaved->key = sharedKey; sharedSaved->score = value;
            sharedSaved->cutoff = cutoff; sharedSaved->occupied = true;
        } else {
            localSaved->key = key; localSaved->score = value;
            localSaved->cutoff = cutoff; localSaved->occupied = true;
        }
        return value;
    };
    pose.score = scorePose(pose);
    std::array<double, 5> step{{1.0, 1.0, 0.8 / std::max(5.0, radius),
                                0.8 / std::max(5.0, radius), 0.8 / std::max(5.0, radius)}};
    for (int resolution = 0; resolution < 3; ++resolution) {
        for (int pass = 0; pass < 4; ++pass) {
            bool improved = false;
            for (int axis = 0; axis < 5; ++axis) {
                if ((axis == 2 && p.angleStart == p.angleEnd)
                    || (axis == 3 && p.scaleToleranceX == 0)
                    || (axis == 4 && p.scaleToleranceY == 0))
                    continue;
                Pose best = pose;
                for (int sign : {-1, 1}) {
                    Pose next = pose;
                    if (axis == 0) next.x += sign * step[axis];
                    if (axis == 1) next.y += sign * step[axis];
                    if (axis == 2) next.angle += sign * step[axis];
                    if (axis == 3) next.sx += sign * step[axis];
                    if (axis == 4) next.sy += sign * step[axis];
                    constrain(next, p);
                    if (next.x == pose.x && next.y == pose.y && next.angle == pose.angle
                        && next.sx == pose.sx && next.sy == pose.sy) continue;
                    next.score = scorePose(next, best.score + 1e-7, axis < 2);
                    if (next.score > best.score + 1e-7)
                        best = next;
                }
                if (best.score > pose.score + 1e-7) {
                    pose = best;
                    improved = true;
                }
            }
            if (!improved)
                break;
        }
        for (double& value : step)
            value *= 0.5;
    }
}

bool nearestEdge(const SearchLevel& level, const cv::Point2d& point,
                 const cv::Point2d& normal, int radius, Feature& nearest)
{
    const int px = cvRound(point.x - level.origin.x), py = cvRound(point.y - level.origin.y);
    const int x0 = std::max(1, px - radius), x1 = std::min(level.edges.cols - 2, px + radius);
    const int y0 = std::max(1, py - radius), y1 = std::min(level.edges.rows - 2, py + radius);
    if (x0 > x1 || y0 > y1) return false;
    double best = radius * radius + 1.0;
    bool found = false;
    for (int y = y0; y <= y1; ++y) {
        const int* row = level.edgeRowPrefix.ptr<int>(y);
        for (int index = row[x0]; index < row[x1 + 1]; ++index) {
            const cv::Vec4d& cached = level.edgeGeometry.ptr<cv::Vec4d>()[index];
            Feature candidate;
            candidate.point = {cached[0], cached[1]};
            candidate.normal = {cached[2], cached[3]};
            const double dot = candidate.normal.dot(normal);
            const double alignment = level.usePolarity ? dot : std::abs(dot);
            if (alignment < 0.80) continue;
            const cv::Point2d delta = point - candidate.point;
            const double squaredDistance = delta.dot(delta);
            const double cost = squaredDistance + 2 * (1 - alignment);
            if (cost < best) {
                best = cost;
                nearest = candidate;
                found = true;
            }
        }
    }
    return found;
}
void refineEdges(Pose& pose, const SearchLevel& level,
                 const std::vector<Feature>& features, const Parameters& p, double minimumScore,
                 CContourMatchCV::FindProfile::IcpCandidate* diagnostic = nullptr)
{
    // Iteratively reweighted point-to-normal least squares. Only edge geometry
    // enters the objective; this is not intensity/NCC/ECC registration.
    double initialCorrelation = 0;
    bool stopAfterFirst = false;
    for (int iteration = 0; iteration < 18; ++iteration) {
        if (diagnostic) ++diagnostic->iterations;
        cv::Matx<double, 5, 5> hessian = cv::Matx<double, 5, 5>::zeros();
        cv::Vec<double, 5> gradient(0, 0, 0, 0, 0);
        int support = 0;
        const PoseTransform mapping(pose);
        const double c = mapping.c, s = mapping.s;
        for (const Feature& feature : features) {
            const cv::Point2d point = mapping.point(feature.point);
            const cv::Point2d normal = mapping.normal(feature.normal);
            Feature nearest;
            if (!nearestEdge(level, point, normal, iteration < 5 ? 4 : 2, nearest))
                continue;
            if (iteration == 0) {
                const double alignment = nearest.normal.dot(normal);
                const double squared = alignment * alignment;
                const double fourth = squared * squared;
                initialCorrelation += fourth * fourth;
            }
            const cv::Point2d n = nearest.normal;
            const double residual = n.dot(point - nearest.point);
            const double weight = 1.0 / (1.0 + residual * residual / 1.5);
            const double x = feature.point.x, y = feature.point.y;
            cv::Vec<double, 5> j(n.x, n.y,
                n.x * (-s * pose.sx * x - c * pose.sy * y)
                    + n.y * (c * pose.sx * x - s * pose.sy * y),
                x * (n.x * c + n.y * s), y * (-n.x * s + n.y * c));
            if (p.angleStart == p.angleEnd) j[2] = 0;
            if (p.scaleToleranceX == 0) j[3] = 0;
            if (p.scaleToleranceY == 0) j[4] = 0;
            for (int row = 0; row < 5; ++row) {
                const double weightedJ = weight * j[row];
                gradient[row] += weightedJ * residual;
                for (int column = row; column < 5; ++column)
                    hessian(row, column) += weightedJ * j[column];
            }
            ++support;
        }
        if (iteration == 0) {
            initialCorrelation /= features.size();
            // Empirical recovery margin, not a mathematical score upper bound.
            // Low initial correspondence quality limits expensive ICP work; the
            // candidate still reaches the unchanged final score and acceptance.
            stopAfterFirst = initialCorrelation < minimumScore * 0.8;
            if (diagnostic) {
                diagnostic->initialCorrelation = initialCorrelation;
                diagnostic->initialSupport = static_cast<double>(support) / features.size();
            }
        }
        if (support < 8)
            break;
        for (int row = 0; row < 5; ++row) {
            hessian(row, row) += 1e-5;
            for (int column = 0; column < row; ++column)
                hessian(row, column) = hessian(column, row);
        }
        cv::Vec<double, 5> delta;
        if (!cv::solve(hessian, -gradient, delta, cv::DECOMP_CHOLESKY))
            break;
        Pose next = pose;
        next.x += std::clamp(delta[0], -1.0, 1.0);
        next.y += std::clamp(delta[1], -1.0, 1.0);
        next.angle += std::clamp(delta[2], -0.025, 0.025);
        next.sx += std::clamp(delta[3], -0.025, 0.025);
        next.sy += std::clamp(delta[4], -0.025, 0.025);
        constrain(next, p);
        if (!centerAllowed(level, next))
            break;
        pose = next;
        if (stopAfterFirst) break;
        if (std::abs(delta[0]) + std::abs(delta[1]) < 0.002
            && std::abs(delta[2]) + std::abs(delta[3]) + std::abs(delta[4]) < 0.0001)
            break;
    }
}

double finalScore(const Pose& pose, const SearchLevel& level,
                  const contourcv_score::FeatureSoA& features, double cutoff)
{
    if (!centerAllowed(level, pose)) return 0;
    return contourcv_score::scoreGradient(level.gradientView, features,
        {pose.x, pose.y, pose.angle, pose.sx, pose.sy}, contourcv_score::Backend::Auto, cutoff);
}
// Coarse smoothing can merge the two sides of a narrow stripe. Test scale
// changes that hold either opposite model boundary fixed, so refinement can
// cross this valley without moving the already aligned side. Every proposal
// uses the same directional objective and the caller's original scale limits.
bool refineAnchoredScales(Pose& pose, const SearchLevel& level,
                          const contourcv_score::FeatureSoA& features, const Parameters& p,
                          cv::Size2d size, double maximumShift)
{
    const Pose parent = pose;
    const PoseTransform mapping(parent);
    for (int axis = 0; axis < 2; ++axis) {
        if ((axis == 0 && p.scaleToleranceX == 0) || (axis == 1 && p.scaleToleranceY == 0)) continue;
        const double extent = axis == 0 ? size.width : size.height;
        for (int anchor : {-1, 1}) {
            double previousScale = std::numeric_limits<double>::quiet_NaN();
            for (double shift = -maximumShift; shift <= maximumShift; shift += 2.0) {
                if (std::abs(shift) < 1e-9) continue;
                Pose next = parent;
                if (axis == 0) next.sx += shift / extent;
                else next.sy += shift / extent;
                constrain(next, p);
                const double scale = axis == 0 ? next.sx : next.sy;
                if (scale == previousScale) continue;
                previousScale = scale;
                if (scale == (axis == 0 ? parent.sx : parent.sy)) continue;
                // Use the actual constrained scale change to preserve the anchor.
                const double moved = extent * (axis == 0 ? next.sx - parent.sx : next.sy - parent.sy);
                next.x += anchor * moved * 0.5 * (axis == 0 ? mapping.c : -mapping.s);
                next.y += anchor * moved * 0.5 * (axis == 0 ? mapping.s : mapping.c);
                next.score = finalScore(next, level, features, pose.score);
                if (next.score > pose.score) pose = next;
            }
        }
    }
    return pose.score > parent.score;
}
// Geometric separation is measured at the transformed model corners. Fixed
// angle/scale tolerances would merge distinct edge basins on large templates.
std::vector<Pose> diverse(std::vector<Pose> candidates, int limit, double separation, cv::Size2d size)
{
    std::sort(candidates.begin(), candidates.end(),
              [](const Pose& a, const Pose& b) { return a.score > b.score; });
    struct AffineCoefficients {
        std::array<double, 4> values{};
        bool ready = false;
        void prepare(const Pose& pose) {
            if (ready) return;
            const PoseTransform mapping(pose);
            // Preserve the original product order and double intermediates.
            values = {mapping.c * pose.sx, mapping.s * pose.sx,
                      mapping.s * pose.sy, mapping.c * pose.sy};
            ready = true;
        }
    };
    std::vector<Pose> result;
    std::vector<AffineCoefficients> resultCoefficients;
    const std::size_t capacity = std::min(candidates.size(), static_cast<std::size_t>(limit));
    result.reserve(capacity);
    resultCoefficients.reserve(capacity);
    // Only adjacent cells can pass the center-distance test. Preserve the
    // geometric duplicate criterion without scanning every retained pose.
    const auto cellKey = [](int x, int y) {
        return (static_cast<std::uint64_t>(static_cast<std::uint32_t>(x)) << 32)
            | static_cast<std::uint32_t>(y);
    };
    std::unordered_map<std::uint64_t, std::vector<std::size_t>> cells;
    std::vector<std::size_t> nearby;
    for (const Pose& pose : candidates) {
        const int cellX = static_cast<int>(std::floor(pose.x / separation));
        const int cellY = static_cast<int>(std::floor(pose.y / separation));
        nearby.clear();
        for (int y = cellY - 1; y <= cellY + 1; ++y) {
            for (int x = cellX - 1; x <= cellX + 1; ++x) {
                const auto found = cells.find(cellKey(x, y));
                if (found != cells.end())
                    nearby.insert(nearby.end(), found->second.begin(), found->second.end());
            }
        }
        bool duplicate = false;
        AffineCoefficients first;
        for (std::size_t index : nearby) {
            const Pose& existing = result[index];
            const double dx = pose.x - existing.x, dy = pose.y - existing.y;
            if (dx * dx + dy * dy >= separation * separation) continue;
            // Lazily cache only poses that pass the unchanged center test.
            first.prepare(pose);
            AffineCoefficients& second = resultCoefficients[index];
            second.prepare(existing);
            const double xx = first.values[0] - second.values[0];
            const double yx = first.values[1] - second.values[1];
            const double xy = first.values[2] - second.values[2];
            const double yy = first.values[3] - second.values[3];
            const double projection = dx * dx + dy * dy
                + 0.25 * size.width * size.width * (xx * xx + yx * yx)
                + 0.25 * size.height * size.height * (xy * xy + yy * yy);
            if (projection < separation * separation) {
                duplicate = true;
                break;
            }
        }
        if (!duplicate) {
            cells[cellKey(cellX, cellY)].push_back(result.size());
            result.push_back(pose);
            resultCoefficients.push_back(first);
        }
        if (static_cast<int>(result.size()) >= limit)
            break;
    }
    return result;
}
double overlap(const Pose& a, const Pose& b, cv::Size2d size)
{
    const cv::RotatedRect first(cv::Point2f(static_cast<float>(a.x), static_cast<float>(a.y)),
        cv::Size2f(static_cast<float>(size.width * a.sx), static_cast<float>(size.height * a.sy)),
        static_cast<float>(a.angle * 180 / kPi));
    const cv::RotatedRect second(cv::Point2f(static_cast<float>(b.x), static_cast<float>(b.y)),
        cv::Size2f(static_cast<float>(size.width * b.sx), static_cast<float>(size.height * b.sy)),
        static_cast<float>(b.angle * 180 / kPi));
    std::vector<cv::Point2f> intersection;
    const int type = cv::rotatedRectangleIntersection(first, second, intersection);
    if (type == cv::INTERSECT_NONE)
        return 0;
    if (type == cv::INTERSECT_FULL)
        return 1;
    return intersection.size() < 3 ? 0 : std::abs(cv::contourArea(intersection))
        / std::max(1e-8, std::min(first.size.area(), second.size.area()) * 1.0);
}
} // namespace

struct CContourMatchCV::Impl {
    Parameters parameters;
    Options options;
    Contours contours;
    cv::Point2d origin;
    cv::Size2d size;
    double radius = 0;
    std::vector<std::vector<Feature>> features;
    std::vector<contourcv_score::FeatureSoA> responseFeatures;
    std::vector<Variant> coarseVariants;
    std::vector<contourcv_detail::CoarsePattern> coarsePatterns;
};

CContourMatchCV::CContourMatchCV() : impl_(new Impl) {}
CContourMatchCV::~CContourMatchCV() = default;
CContourMatchCV::CContourMatchCV(CContourMatchCV&&) noexcept = default;
CContourMatchCV& CContourMatchCV::operator=(CContourMatchCV&&) noexcept = default;

bool CContourMatchCV::createModel(const cv::Mat& image, const cv::Mat& mask,
                                 const Parameters& p, std::string& message)
{
    return createModel(image, mask, p, Options{}, message);
}

bool CContourMatchCV::createModel(const cv::Mat& image, const cv::Mat& mask,
                                 const Parameters& p, const Options& options,
                                 std::string& message)
{
    clear();
    message.clear();
    if (!validImage(image) || !validParameters(p) || !validMask(mask, image.size())
        || !std::isfinite(options.minContourLength) || options.minContourLength < 0
        || options.minContourLength > 1e7 || options.timeoutMs < 0
        || (options.polarity != Polarity::Ignore && options.polarity != Polarity::Use)) {
        message = "Invalid image, mask or model parameters (8-bit image, maximum 32 MP).";
        return false;
    }
    try {
        const cv::Mat gray = gray8(image);
        const auto modelEdges = contourcv_detail::modelEdges(gray, mask, p.lowThreshold, p.highThreshold);
        const cv::Mat& edges = modelEdges.edges;
        const cv::Mat& gx = modelEdges.gx;
        const cv::Mat& gy = modelEdges.gy;
        const cv::Mat& magnitude = modelEdges.magnitude;
        const auto pixelContours = contourcv_detail::traceCenterlines(edges);
        std::vector<Feature> all;
        cv::Mat selected = cv::Mat::zeros(edges.size(), CV_8U);
        double minX = image.cols, minY = image.rows, maxX = 0, maxY = 0;
        for (const auto& chain : pixelContours) {
            const double chainLength = cv::arcLength(chain, false);
            if (chain.size() < 2 || chainLength < options.minContourLength)
                continue;
            std::vector<cv::Point2d> contour;
            contour.reserve(chain.size());
            for (const cv::Point& pixel : chain) {
                Feature feature = edgeFeature(gx, gy, magnitude, pixel.x, pixel.y);
                feature.point = contourcv_detail::facetPosition(magnitude, pixel.x, pixel.y, feature.point);
                feature.point += cv::Point2d(modelEdges.origin);
                contour.push_back(feature.point);
                minX = std::min(minX, feature.point.x); minY = std::min(minY, feature.point.y);
                maxX = std::max(maxX, feature.point.x); maxY = std::max(maxY, feature.point.y);
                if (!selected.at<uchar>(pixel)) {
                    all.push_back(feature);
                    selected.at<uchar>(pixel) = 255;
                }
            }
            impl_->contours.push_back(std::move(contour));
        }
        if (all.size() < 12 || maxX - minX < 4 || maxY - minY < 4) {
            clear();
            message = "Insufficient non-degenerate edge contours in the template mask.";
            return false;
        }
        impl_->origin = {(minX + maxX) * 0.5, (minY + maxY) * 0.5};
        impl_->size = {maxX - minX, maxY - minY};
        impl_->radius = 0.5 * std::hypot(impl_->size.width, impl_->size.height);
        impl_->parameters = p;
        impl_->options = options;
        for (Feature& feature : all)
            feature.point -= impl_->origin;
        int levels = 1;
        if (p.numLevels == 0) {
            while (levels < kMaximumLevels
                && std::min(impl_->size.width, impl_->size.height) / (1 << levels) >= 10)
                ++levels;
        } else {
            levels = std::min(p.numLevels, kMaximumLevels);
            while (levels > 1 && std::min(image.cols, image.rows) / (1 << (levels - 1)) < 8)
                --levels;
        }
        cv::Mat levelGray = gray, levelMask = mask;
        for (int level = 0; level < levels; ++level) {
            const double divisor = static_cast<double>(1 << level);
            if (level == 0) {
                impl_->features.push_back(sampled(all, 600, 1));
                continue;
            }
            cv::Mat nextGray;
            cv::pyrDown(levelGray, nextGray);
            levelGray = nextGray;
            if (!mask.empty())
                cv::resize(mask, levelMask, levelGray.size(), 0, 0, cv::INTER_NEAREST);
            const auto levelData = contourcv_detail::modelEdges(levelGray, levelMask, p.lowThreshold, p.highThreshold);
            const cv::Mat& levelEdges = levelData.edges;
            const cv::Mat& levelGx = levelData.gx;
            const cv::Mat& levelGy = levelData.gy;
            const cv::Mat& levelMagnitude = levelData.magnitude;
            const auto chains = contourcv_detail::traceCenterlines(levelEdges);
            cv::Mat taken = cv::Mat::zeros(levelEdges.size(), CV_8U);
            std::vector<Feature> layer;
            for (const auto& chain : chains) {
                const double chainLength = cv::arcLength(chain, false) * divisor;
                if (chain.size() < 2 || chainLength < options.minContourLength)
                    continue;
                for (const auto& point : chain) {
                    if (taken.at<uchar>(point)) continue;
                    Feature feature = edgeFeature(levelGx, levelGy, levelMagnitude, point.x, point.y);
                    feature.point = contourcv_detail::facetPosition(levelMagnitude, point.x, point.y, feature.point);
                    feature.point += cv::Point2d(levelData.origin) - impl_->origin / divisor;
                    layer.push_back(feature);
                    taken.at<uchar>(point) = 255;
                }
            }
            // Do not add a coarse level after its distinctive contours disappear.
            if (layer.size() < 16)
                break;
            impl_->features.push_back(sampled(layer, std::max(96, 600 / (1 << level)), 1));
        }
        impl_->responseFeatures.resize(impl_->features.size());
        for (size_t level = 0; level < impl_->features.size(); ++level) {
            auto& soa = impl_->responseFeatures[level];
            soa.reserve(impl_->features[level].size());
            for (const Feature& feature : impl_->features[level])
                soa.push_back(feature.point.x, feature.point.y, feature.normal.x, feature.normal.y);
        }
        levels = static_cast<int>(impl_->features.size());
        const double coarseRadius = impl_->radius / (1 << (levels - 1));
        const double angleStep = p.angleStep > 0 ? p.angleStep * kPi / 180
            : 1.5 / std::max(4.0, coarseRadius);
        const double angleIntervals = (p.angleEnd - p.angleStart) * kPi / 180 / angleStep;
        if (!std::isfinite(angleIntervals) || std::ceil(angleIntervals) > 11999) {
            clear();
            message = "Explicit angle step exceeds the bounded 12000-variant model budget.";
            return false;
        }
        const auto angles = intervals(p.angleStart * kPi / 180, p.angleEnd * kPi / 180, angleStep);
        const auto scalesX = intervals(1 - p.scaleToleranceX, 1 + p.scaleToleranceX,
                                      2.0 / std::max(8.0, impl_->size.width / (1 << (levels - 1))));
        const auto scalesY = intervals(1 - p.scaleToleranceY, 1 + p.scaleToleranceY,
                                      2.0 / std::max(8.0, impl_->size.height / (1 << (levels - 1))));
        if (angles.size() * scalesX.size() * scalesY.size() > 12000) {
            clear();
            message = "Model search space is too large; use automatic pyramid levels or narrower ranges.";
            return false;
        }
        const auto coarse = sampled(impl_->features.back(), 72, 1);
        std::map<contourcv_detail::CoarsePattern::Key, std::size_t> patternIndex;
        for (double angle : angles) for (double sx : scalesX) for (double sy : scalesY) {
            Variant variant;
            variant.pose.angle = angle; variant.pose.sx = sx; variant.pose.sy = sy;
            std::vector<contourcv_detail::CoarseSample> offsets;
            offsets.reserve(coarse.size());
            for (const Feature& feature : coarse) {
                const cv::Point2d point = transform(feature.point, variant.pose);
                offsets.push_back({cvRound(point.x), cvRound(point.y),
                    direction(transformNormal(feature.normal, variant.pose), options.polarity == Polarity::Use)});
            }
            variant.sampleCount = static_cast<int>(offsets.size());
            variant.group = contourcv_detail::internCoarsePattern(
                contourcv_detail::canonicalCoarsePattern(std::move(offsets)), patternIndex, impl_->coarsePatterns);
            impl_->coarseVariants.push_back(std::move(variant));
        }
        message = "OK";
        return true;
    } catch (const std::exception& e) {
        clear();
        message = e.what();
        return false;
    }
}

bool CContourMatchCV::find(const cv::Mat& image, double minScore, int maxMatches,
                          double maxOverlap, std::vector<Match>& matches,
                          std::string& message, const cv::Mat& searchMask, FindProfile* profile) const
{
    const auto started = std::chrono::steady_clock::now();
    if (profile) *profile = FindProfile{};
    struct ProfileTotal {
        FindProfile* profile;
        std::chrono::steady_clock::time_point started;
        ~ProfileTotal() {
            if (profile) profile->totalMs = std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - started).count();
        }
    } profileTotal{profile, started};
    auto profileStamp = started;
    const auto profileMark = [&](double& destination) {
        const auto now = std::chrono::steady_clock::now();
        destination += std::chrono::duration<double, std::milli>(now - profileStamp).count();
        profileStamp = now;
    };
    matches.clear();
    message.clear();
    if (!hasModel() || !validImage(image) || !validMask(searchMask, image.size())
        || !std::isfinite(minScore) || minScore < 0 || minScore > 1
        || maxMatches < 1 || maxMatches > 9999 || !std::isfinite(maxOverlap)
        || maxOverlap < 0 || maxOverlap > 1) {
        message = "Invalid model, image, search mask or search parameters.";
        return false;
    }
    try {
        const auto timedOut = [&]() {
            return impl_->options.timeoutMs > 0
                && std::chrono::duration_cast<std::chrono::milliseconds>(
                    std::chrono::steady_clock::now() - started).count() >= impl_->options.timeoutMs;
        };
        const auto checkTimeout = [&]() {
            if (timedOut()) throw std::runtime_error("Contour search timeout; no partial results returned.");
        };
        checkTimeout();
        const int top = static_cast<int>(impl_->features.size()) - 1;
        std::vector<cv::Mat> grayPyramid{gray8(image)}, masks{searchMask};
        for (int level = 1; level <= top; ++level) {
            cv::Mat gray, mask;
            cv::pyrDown(grayPyramid.back(), gray);
            if (!searchMask.empty())
                cv::resize(searchMask, mask, gray.size(), 0, 0, cv::INTER_NEAREST);
            grayPyramid.push_back(gray);
            masks.push_back(mask);
        }
        if (grayPyramid.back().cols < 8 || grayPyramid.back().rows < 8) {
            message = "Search image is too small for the model pyramid.";
            return false;
        }
        if (profile) {
            profile->topLevel = top;
            profile->coarseVariants = impl_->coarseVariants.size();
            profile->coarseUniquePatterns = impl_->coarsePatterns.size();
            for (const Variant& variant : impl_->coarseVariants)
                profile->coarseOriginalOffsets += variant.sampleCount;
            for (const auto& pattern : impl_->coarsePatterns)
                profile->coarseUniqueOffsets += pattern.offsets.size();
            profileMark(profile->pyramidMs);
        }
        // Valid full-angle searches on large images can exceed a fixed work
        // estimate. Execution time is governed by the configured find deadline;
        // zero deliberately permits an unlimited search.
        std::vector<SearchLevel> search(top + 1);
        search[top] = searchLevel(grayPyramid[top], masks[top], impl_->parameters, impl_->options, {}, true, top == 0);
        checkTimeout();
        if (profile) profileMark(profile->coarsePrepMs);
        const SearchLevel& coarse = search.back();
        if (!cv::countNonZero(coarse.edges)) {
            message = "No qualifying edge contours.";
            return true;
        }

        const int peaks = std::max(4, maxMatches * 2);
        // Identical quantized multisets have identical score maps, ties and
        // suppressed peak sequences. Keep every continuous variant below.
        std::vector<std::vector<CoarsePeak>> groupsFound(impl_->coarsePatterns.size());
        cv::parallel_for_(cv::Range(0, static_cast<int>(impl_->coarsePatterns.size())), [&](const cv::Range& range) {
            for (int index = range.start; index < range.end; ++index) {
                if (timedOut()) break;
                const auto& pattern = impl_->coarsePatterns[index];
                cv::Mat scores = contourcv_detail::coarseResponse(coarse.response, coarse.edges.size(), pattern);
                if (!coarse.mask.empty())
                    scores.setTo(0, coarse.mask == 0);
                visiondemo_research::TiledScorePeaks peakExtractor(scores);
                for (int peak = 0; peak < peaks; ++peak) {
                    const auto foundPeak = peakExtractor.maximum();
                    double maximum = foundPeak.value;
                    const cv::Point position = foundPeak.location;
                    maximum /= 255.0 * pattern.sampleCount;
                    if (maximum < std::max(0.12, minScore * 0.48))
                        break;
                    groupsFound[index].push_back({position, maximum});
                    const int radius = std::max(2, cvRound(std::min(impl_->size.width, impl_->size.height)
                                                         / (1 << top) * 0.15));
                    peakExtractor.suppressSquare(position, radius);
                }
            }
        });
        checkTimeout();
        if (profile) profileMark(profile->coarseSearchMs);
        std::vector<Pose> candidates;
        // Restore the original variant-major / peak-minor order, including
        // different affine poses that happen to share one integer response.
        for (const Variant& variant : impl_->coarseVariants) {
            for (const CoarsePeak& peak : groupsFound[variant.group]) {
                Pose pose = variant.pose;
                pose.x = peak.position.x; pose.y = peak.position.y; pose.score = peak.score;
                candidates.push_back(pose);
            }
        }
        if (profile) profile->coarseCandidates = candidates.size();
        const int candidateLimit = std::max(32, maxMatches * 16);
        candidates = diverse(std::move(candidates), candidateLimit, 1.0, impl_->size / static_cast<double>(1 << top));
        if (profile) profileMark(profile->coarseDiverseMs);
        for (int level = top; level >= 0 && !candidates.empty(); --level) {
            checkTimeout();
            if (level != top) {
                for (Pose& pose : candidates) { pose.x *= 2; pose.y *= 2; }
                // Once a coarse candidate exists, only its surrounding image can
                // affect refinement. A generous halo permits all local pose updates.
                double left = image.cols, upper = image.rows, right = 0, lower = 0;
                for (const Pose& pose : candidates) {
                    const double c = std::abs(std::cos(pose.angle)), s = std::abs(std::sin(pose.angle));
                    const double width = impl_->size.width * (1 + impl_->parameters.scaleToleranceX) / (1 << level);
                    const double height = impl_->size.height * (1 + impl_->parameters.scaleToleranceY) / (1 << level);
                    const double rx = 0.5 * (c * width + s * height) + 16;
                    const double ry = 0.5 * (s * width + c * height) + 16;
                    left = std::min(left, pose.x - rx); upper = std::min(upper, pose.y - ry);
                    right = std::max(right, pose.x + rx); lower = std::max(lower, pose.y + ry);
                }
                const int x0 = std::max(0, static_cast<int>(std::floor(left)));
                const int y0 = std::max(0, static_cast<int>(std::floor(upper)));
                const int x1 = std::min(grayPyramid[level].cols, static_cast<int>(std::ceil(right)) + 1);
                const int y1 = std::min(grayPyramid[level].rows, static_cast<int>(std::ceil(lower)) + 1);
                const cv::Rect roi(x0, y0, std::max(1, x1 - x0), std::max(1, y1 - y0));
                search[level] = searchLevel(grayPyramid[level], masks[level], impl_->parameters, impl_->options, roi, level != 0, level == 0);
                if (!cv::countNonZero(search[level].edges)) { candidates.clear(); break; }
                search[level + 1] = SearchLevel();
            }
            if (level == std::min(2, top - 1)) {
                // A coarse blur may merge adjacent physical edges. Carry nearby
                // affine basins into the layer where these edges separate.
                const double width = impl_->size.width / (1 << level);
                const double height = impl_->size.height / (1 << level);
                const auto parents = candidates;
                for (const Pose& parent : parents) {
                    for (int axis = 0; axis < 5; ++axis) {
                        if ((axis == 2 && impl_->parameters.angleStart == impl_->parameters.angleEnd)
                            || (axis == 3 && impl_->parameters.scaleToleranceX == 0)
                            || (axis == 4 && impl_->parameters.scaleToleranceY == 0)) continue;
                        for (int sign : {-1, 1}) {
                            Pose child = parent;
                            if (axis == 0) child.x += sign * 2.0;
                            if (axis == 1) child.y += sign * 2.0;
                            if (axis == 2) child.angle += sign * 2.0 / std::max(5.0, impl_->radius / (1 << level));
                            if (axis == 3) child.sx += sign * 4.0 / std::max(8.0, width);
                            if (axis == 4) child.sy += sign * 4.0 / std::max(8.0, height);
                            constrain(child, impl_->parameters);
                            candidates.push_back(child);
                            if (axis >= 3) {
                                const double c = std::cos(parent.angle), s = std::sin(parent.angle);
                                for (int anchor : {-1, 1}) {
                                    Pose anchored = child;
                                    anchored.x += anchor * 2.0 * (axis == 3 ? c : -s);
                                    anchored.y += anchor * 2.0 * (axis == 3 ? s : c);
                                    candidates.push_back(anchored);
                                }
                            }
                        }
                    }
                }
            }
            // Bound-constrained branches can be exactly the same affine pose.
            // Optimize each unique pose once, retaining first-occurrence order.
            if (level != 0 || top == 0) {
                std::set<std::array<double, 5>> uniquePoses;
                size_t uniqueCount = 0;
                for (const Pose& candidate : candidates) {
                    if (uniquePoses.insert({candidate.x, candidate.y, candidate.angle, candidate.sx, candidate.sy}).second)
                        candidates[uniqueCount++] = candidate;
                }
                candidates.resize(uniqueCount);
            }
            if (profile) {
                profile->levelCandidates[level] = candidates.size();
                profileMark(profile->levelPrepMs[level]);
            }
            std::vector<ScoreEvaluationStats> scoreStatistics(profile ? candidates.size() : 0);
            if (level != 0 || top == 0) {
                // Explicit blocks let exact affine terms survive across candidates,
                // even when the OpenCV backend splits its range into single tasks.
                // Feature transforms and response scores stay within this immutable level.
                std::vector<int> evaluationOrder(candidates.size());
                std::iota(evaluationOrder.begin(), evaluationOrder.end(), 0);
                // Change execution order only. Independent results are written back
                // to their original indices before the unchanged sort/suppression.
                std::sort(evaluationOrder.begin(), evaluationOrder.end(), [&](int first, int second) {
                    const Pose& a = candidates[first]; const Pose& b = candidates[second];
                    if (a.angle != b.angle) return a.angle < b.angle;
                    if (a.sx != b.sx) return a.sx < b.sx;
                    if (a.sy != b.sy) return a.sy < b.sy;
                    if (a.y != b.y) return a.y < b.y;
                    if (a.x != b.x) return a.x < b.x;
                    return first < second;
                });
                const int workers = std::max(1, cv::getNumThreads());
                const int candidatesPerBlock = std::clamp(
                    static_cast<int>((candidates.size() + workers * 4 - 1) / (workers * 4)), 1, 256);
                const int candidateBlocks = (static_cast<int>(candidates.size()) + candidatesPerBlock - 1) / candidatesPerBlock;
                cv::parallel_for_(cv::Range(0, candidateBlocks), [&](const cv::Range& range) {
                    contourcv_score::PreparedResponse preparedStorage;
                    contourcv_score::ResponseAffineCache<> affineCache(impl_->responseFeatures[level], search[level].usePolarity);
                    ResponseScoreCache sharedScoreCache(search[level], impl_->responseFeatures[level]);
                    const int begin = range.start * candidatesPerBlock;
                    const int end = std::min(static_cast<int>(candidates.size()), range.end * candidatesPerBlock);
                    for (int task = begin; task < end; ++task) {
                        const int i = evaluationOrder[task];
                        localOptimize(candidates[i], search[level], impl_->features[level], impl_->parameters,
                                      impl_->radius / (1 << level), false, &impl_->responseFeatures[level],
                                      profile ? &scoreStatistics[i] : nullptr, &preparedStorage, &affineCache, &sharedScoreCache);
                    }
                });
            }
            if (profile) {
                profileMark(profile->levelOptimizeMs[level]);
                for (const auto& statistics : scoreStatistics) {
                    profile->levelScoreCalls[level] += statistics.calls;
                    profile->levelScoreCacheHits[level] += statistics.cacheHits;
                    profile->levelSharedScoreHits[level] += statistics.sharedScoreHits;
                    profile->levelAffineLookups[level] += statistics.affineLookups;
                    profile->levelAffineHits[level] += statistics.affineHits;
                }
            }
            candidates = diverse(std::move(candidates), level > 0 ? candidateLimit
                : std::max(8, maxMatches * 4), 1.0, impl_->size / static_cast<double>(1 << level));
            if (profile) {
                profile->levelRetainedCandidates[level] = candidates.size();
                profileMark(profile->levelDiverseMs[level]);
            }
        }
        checkTimeout();
        if (!candidates.empty()) cacheEdgeGeometry(search[0]);
        if (profile) {
            profileMark(profile->cacheMs);
            profile->icpCandidates.resize(candidates.size());
        }
        checkTimeout();
        cv::parallel_for_(cv::Range(0, static_cast<int>(candidates.size())), [&](const cv::Range& range) {
            for (int index = range.start; index < range.end; ++index) {
                if (timedOut()) break;
                Pose& pose = candidates[index];
                FindProfile::IcpCandidate* diagnostic = nullptr;
                if (profile) {
                    pose.profileIndex = index;
                    diagnostic = &profile->icpCandidates[index];
                    diagnostic->initialX = pose.x; diagnostic->initialY = pose.y;
                }
                refineEdges(pose, search[0], impl_->features[0], impl_->parameters, minScore, diagnostic);
                pose.score = finalScore(pose, search[0], impl_->responseFeatures[0]);
            }
        });
        if (profile) profileMark(profile->icpMs);
        candidates = diverse(std::move(candidates), candidateLimit, 2.0, impl_->size);
        if (profile) {
            profile->finalCandidates = candidates.size();
            profileMark(profile->icpDiverseMs);
        }
        cv::parallel_for_(cv::Range(0, static_cast<int>(candidates.size())), [&](const cv::Range& range) {
            for (int index = range.start; index < range.end; ++index) {
                if (timedOut()) break;
                Pose& pose = candidates[index];
                // Preserve a margin below the requested score for local recovery;
                // very weak background candidates cannot justify expensive polishing.
                if (pose.score < minScore * 0.75) continue;
                localOptimize(pose, search[0], impl_->features[0], impl_->parameters, impl_->radius, true, &impl_->responseFeatures[0]);
                pose.score = finalScore(pose, search[0], impl_->responseFeatures[0]);
                if (refineAnchoredScales(pose, search[0], impl_->responseFeatures[0], impl_->parameters,
                    impl_->size, std::min(32.0, 2.0 * static_cast<double>(1 << top))))
                    localOptimize(pose, search[0], impl_->features[0], impl_->parameters, impl_->radius, true, &impl_->responseFeatures[0]);
            }
        });
        checkTimeout();
        if (profile) {
            profileMark(profile->finalRefineMs);
            for (const Pose& pose : candidates)
                if (pose.profileIndex >= 0) profile->icpCandidates[pose.profileIndex].finalScore = pose.score;
        }
        std::sort(candidates.begin(), candidates.end(),
                  [](const Pose& a, const Pose& b) { return a.score > b.score; });
        std::vector<Pose> accepted;
        for (const Pose& pose : candidates) {
            checkTimeout();
            if (!centerAllowed(search[0], pose) || pose.score < minScore)
                continue;
            bool duplicate = false;
            for (const Pose& existing : accepted) {
                if (overlap(pose, existing, impl_->size) > maxOverlap
                    || (std::hypot(pose.x - existing.x, pose.y - existing.y) < 1.0
                        && std::abs(std::remainder(pose.angle - existing.angle, 2 * kPi)) < 0.01
                        && std::abs(pose.sx - existing.sx) < 0.02
                        && std::abs(pose.sy - existing.sy) < 0.02)) {
                    duplicate = true;
                    break;
                }
            }
            if (duplicate)
                continue;
            accepted.push_back(pose);
            if (profile && pose.profileIndex >= 0) profile->icpCandidates[pose.profileIndex].accepted = true;
            Match match;
            match.center = {pose.x, pose.y};
            match.angle = pose.angle * 180 / kPi;
            match.scaleX = pose.sx; match.scaleY = pose.sy; match.score = pose.score;
            const PoseTransform mapping(pose);
            for (const auto& source : impl_->contours) {
                std::vector<cv::Point2d> contour;
                contour.reserve(source.size());
                for (const auto& point : source)
                    contour.push_back(mapping.point(point - impl_->origin));
                match.contours.push_back(std::move(contour));
            }
            matches.push_back(std::move(match));
            if (static_cast<int>(matches.size()) == maxMatches)
                break;
        }
        checkTimeout();
        if (profile) profileMark(profile->outputMs);
        message = matches.empty() ? "No contour match reaches the minimum score." : "OK";
        if (matches.empty() && !candidates.empty()) {
            const auto& best = candidates.front();
            message += " Best score=" + std::to_string(best.score)
                + " center=" + std::to_string(best.x) + "," + std::to_string(best.y)
                + " angle=" + std::to_string(best.angle * 180 / kPi)
                + " scale=" + std::to_string(best.sx) + "," + std::to_string(best.sy);
        }
        return true;
    } catch (const std::exception& e) {
        matches.clear();
        message = e.what();
        return false;
    }
}

const CContourMatchCV::Contours& CContourMatchCV::templateContours() const
{
    static const Contours empty;
    return impl_ ? impl_->contours : empty;
}
bool CContourMatchCV::hasModel() const
{
    return impl_ && !impl_->features.empty() && !impl_->coarseVariants.empty();
}
void CContourMatchCV::clear() { impl_.reset(new Impl); }
