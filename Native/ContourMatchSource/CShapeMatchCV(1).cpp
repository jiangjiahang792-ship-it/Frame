#include "CShapeMatchCV.h"

#include <opencv2/imgproc.hpp>
#include <opencv2/video/tracking.hpp>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <exception>
#include <limits>
#include <mutex>
#include <unordered_set>
#include <vector>

#ifdef _OPENMP
#include <omp.h>
#endif

namespace {

constexpr int kDirections = 16;
constexpr int kMaximumDirectionScore = 8;
constexpr int kInvalidDirection = 255;
constexpr int kMaxLevels = 6;
constexpr double kPi = 3.14159265358979323846;
constexpr double kExactScoreWeight = 0.40;
constexpr double kSpatialScoreWeight = 0.60;
constexpr double kStrictScoreFraction = 0.80;
constexpr std::size_t kIntensityRefinementMaxPixels = 4096;

struct MatchPose
{
    cv::Point2d center;
    double angleDegrees = 0.0;
    double score = 0.0;
    double width = 0.0;
    double height = 0.0;
};

struct ShapeMatchProfile
{
    std::size_t coarseAngleMultiplier = 1;
    std::size_t coarseFeatureStride = 1;
    int maximumPeaksPerAngle = 16;
    std::size_t candidateLimit = 96;
    std::size_t coarseCandidateMinimum = 16;
    std::size_t coarseCandidatePadding = 16;
    std::size_t fineCandidateMinimum = 8;
    std::size_t fineCandidatePadding = 8;
    double finalAngleWindowFine = 4.0;
    double finalAngleWindowPyramid = 8.0;
    int finalPositionRadius = 2;
    bool enableParabolicRefinement = true;
    bool enableContourRefinement = true;
    bool enableIntensityRefinement = true;
};

ShapeMatchProfile profileFor(int mode)
{
    ShapeMatchProfile profile;
    if (mode == 0) {
        profile.coarseAngleMultiplier = 2;
        profile.coarseFeatureStride = 2;
        profile.maximumPeaksPerAngle = 12;
        profile.candidateLimit = 64;
        profile.coarseCandidateMinimum = 12;
        profile.coarseCandidatePadding = 8;
        profile.fineCandidateMinimum = 6;
        profile.fineCandidatePadding = 4;
        profile.finalAngleWindowFine = 2.0;
        profile.finalAngleWindowPyramid = 4.0;
        profile.finalPositionRadius = 1;
        profile.enableParabolicRefinement = false;
        profile.enableContourRefinement = false;
        profile.enableIntensityRefinement = false;
    } else if (mode == 1) {
        profile.enableIntensityRefinement = false;
    }
    return profile;
}

int parallelThreadCount(std::size_t taskCount)
{
#ifdef _OPENMP
    return std::max(1, std::min({
        4, static_cast<int>(taskCount), omp_get_max_threads()}));
#else
    (void)taskCount;
    return 1;
#endif
}

double backendAngleDegrees(double productAngleDegrees)
{
    return -productAngleDegrees;
}

cv::Mat gray8(const cv::Mat& image)
{
    if (image.empty())
        return {};
    if (image.type() == CV_8UC1)
        return image;
    cv::Mat gray;
    if (image.type() == CV_8UC3)
        cv::cvtColor(image, gray, cv::COLOR_BGR2GRAY);
    else if (image.type() == CV_8UC4)
        cv::cvtColor(image, gray, cv::COLOR_BGRA2GRAY);
    return gray;
}

cv::Mat mask8(const cv::Mat& input, const cv::Size& size)
{
    if (input.empty())
        return cv::Mat(size, CV_8UC1, cv::Scalar(255));
    cv::Mat gray;
    if (input.type() == CV_8UC1)
        gray = input;
    else if (input.type() == CV_8UC3)
        cv::cvtColor(input, gray, cv::COLOR_BGR2GRAY);
    else if (input.type() == CV_8UC4)
        cv::cvtColor(input, gray, cv::COLOR_BGRA2GRAY);
    if (gray.empty() || gray.size() != size)
        return {};
    cv::Mat result;
    cv::threshold(gray, result, 0, 255, cv::THRESH_BINARY);
    return cv::countNonZero(result) ? result : cv::Mat();
}

bool validAngleRange(double startDegrees, double endDegrees)
{
    return std::isfinite(startDegrees) && std::isfinite(endDegrees)
        && startDegrees >= -180.0 && startDegrees <= 180.0
        && endDegrees >= -180.0 && endDegrees <= 180.0;
}

bool validModel(int numLevels, double angleStartDegrees,
                double angleEndDegrees, double angleStepDegrees,
                double contrast, double minContrast, int featureCount)
{
    return numLevels >= 0 && numLevels <= 12
        && validAngleRange(angleStartDegrees, angleEndDegrees)
        && std::isfinite(angleStepDegrees)
        && angleStepDegrees > 0.0 && angleStepDegrees <= 180.0
        && std::isfinite(contrast) && contrast >= 0.0
        && std::isfinite(minContrast) && minContrast >= 0.0
        && featureCount >= 0 && featureCount <= 10000;
}

bool validSearch(double angleStartDegrees, double angleEndDegrees,
                 double minScore, int maxMatches, double maxOverlap,
                 int numLevels)
{
    return validAngleRange(angleStartDegrees, angleEndDegrees)
        && std::isfinite(minScore) && minScore >= 0.0 && minScore <= 1.0
        && maxMatches >= 1 && maxMatches <= 100
        && std::isfinite(maxOverlap)
        && maxOverlap >= 0.0 && maxOverlap <= 1.0
        && numLevels >= 0 && numLevels <= 12;
}

std::vector<double> modelAngles(double angleStartDegrees,
                                double angleEndDegrees,
                                double angleStepDegrees)
{
    const double extent = angleEndDegrees - angleStartDegrees;
    const double sign = extent < 0.0 ? -1.0 : 1.0;
    std::vector<double> result(1, angleStartDegrees);
    if (std::abs(extent) <= 1e-9)
        return result;
    for (double angle = angleStartDegrees + sign * angleStepDegrees;
         sign > 0.0 ? angle < angleEndDegrees - 1e-9
                    : angle > angleEndDegrees + 1e-9;
         angle += sign * angleStepDegrees) {
        result.push_back(angle);
    }
    if (std::abs(result.back() - angleEndDegrees) > 1e-9)
        result.push_back(angleEndDegrees);
    return result;
}

int autoLevels(const cv::Size& size)
{
    int levels = 1;
    int minimum = std::min(size.width, size.height);
    // 中大模板的内部细节不能压缩到8像素再找候选；小模板沿用原有两层策略。
    const int coarseMinimumSize = minimum >= 64 ? 14 : 8;
    while (levels < kMaxLevels && minimum / 2 >= coarseMinimumSize) {
        minimum /= 2;
        ++levels;
    }
    return levels;
}

/// <summary>在有效 ROI 梯度幅值上做 Otsu 分组，以偏向弱组的保守分界估算 Canny 高阈值。</summary>
double estimateContrast(const cv::Mat& gradientX, const cv::Mat& gradientY, const cv::Mat& mask)
{
    // Sobel 3×3 的单轴范围为 ±1020；2048 个整数幅值桶足以覆盖 L2 幅值。
    // 固定大小直方图避免排序全部像素，O(ROI面积)，不依赖背景的绝对灰度。
    std::array<double, 2048> histogram{};
    double total = 0.0;
    double sum = 0.0;
    for (int y = 1; y < gradientX.rows - 1; ++y) {
        const auto* gx = gradientX.ptr<short>(y);
        const auto* gy = gradientY.ptr<short>(y);
        const auto* selected = mask.ptr<unsigned char>(y);
        for (int x = 1; x < gradientX.cols - 1; ++x) {
            if (!selected[x])
                continue;
            const int magnitude = std::clamp(cvRound(std::hypot(gx[x], gy[x])), 0, 2047);
            histogram[static_cast<std::size_t>(magnitude)] += 1.0;
            total += 1.0;
            sum += magnitude;
        }
    }
    double lowerCount = 0.0;
    double lowerSum = 0.0;
    double bestVariance = -1.0;
    double threshold = 4.0;
    for (std::size_t index = 0; index < histogram.size(); ++index) {
        lowerCount += histogram[index];
        lowerSum += histogram[index] * index;
        const double upperCount = total - lowerCount;
        if (lowerCount <= 0.0 || upperCount <= 0.0)
            continue;
        const double lowerMean = lowerSum / lowerCount;
        const double upperMean = (sum - lowerSum) / upperCount;
        const double difference = upperMean - lowerMean;
        const double variance = lowerCount * upperCount * difference * difference;
        if (variance > bestVariance) {
            bestVariance = variance;
            // 两组均值中点对小目标的粗金字塔层偏高；保留弱组到强组间 1/4 的分界。
            threshold = lowerMean + (upperMean - lowerMean) * 0.25;
        }
    }
    // 保留一位小数，界面显示值即实际使用值；平坦图仍由建模的最少特征检查拒绝。
    return std::clamp(std::round(threshold * 10.0) / 10.0, 4.0, 1024.0);
}

/// <summary>复用 16 位导数完成自动阈值估计与 L2 Canny；零高阈值表示自动。</summary>
cv::Mat cannyGradients(const cv::Mat& gray, const cv::Mat& mask, double& highThreshold,
                      cv::Mat& gradientX, cv::Mat& gradientY)
{
    cv::Sobel(gray, gradientX, CV_16S, 1, 0, 3);
    cv::Sobel(gray, gradientY, CV_16S, 0, 1, 3);
    if (highThreshold == 0.0)
        highThreshold = estimateContrast(gradientX, gradientY, mask);
    cv::Mat edges;
    // 不额外平滑原图，避免抹掉十几像素小目标的内外边；金字塔本身使用 pyrDown 平滑。
    // Canny 完成非极大值抑制及双阈值连接，匹配仍保留原始梯度方向。
    cv::Canny(gradientX, gradientY, edges,
              highThreshold * 0.5, highThreshold, true);
    return edges;
}

unsigned char direction(float gradientX, float gradientY)
{
    const float absoluteX = std::abs(gradientX);
    const float absoluteY = std::abs(gradientY);
    int quadrant = 4;
    if (absoluteY < absoluteX * 0.198912F)
        quadrant = 0;
    else if (absoluteY < absoluteX * 0.668179F)
        quadrant = 1;
    else if (absoluteY < absoluteX * 1.496606F)
        quadrant = 2;
    else if (absoluteY < absoluteX * 5.027339F)
        quadrant = 3;
    int label = 0;
    if (gradientX >= 0.0F)
        label = gradientY >= 0.0F ? quadrant : 16 - quadrant;
    else
        label = gradientY >= 0.0F ? 8 - quadrant : 8 + quadrant;
    return static_cast<unsigned char>(label & (kDirections - 1));
}

unsigned char direction(short gradientX, short gradientY)
{
    const int x = gradientX;
    const int y = gradientY;
    const int absoluteX = std::abs(x);
    const int absoluteY = std::abs(y);
    // 与原有四段方向量化严格等价，消除纹理区方向随机时的多级分支预测开销。
    const int scaledY = absoluteY * 1024;
    const int quadrant = (scaledY >= absoluteX * 204) + (scaledY >= absoluteX * 684)
        + (scaledY >= absoluteX * 1533) + (scaledY >= absoluteX * 5148);
    int label = 0;
    if (x >= 0)
        label = y >= 0 ? quadrant : 16 - quadrant;
    else
        label = y >= 0 ? 8 - quadrant : 8 + quadrant;
    return static_cast<unsigned char>(label & (kDirections - 1));
}

struct BaseFeature
{
    float x = 0.0F;
    float y = 0.0F;
    float gradientX = 0.0F;
    float gradientY = 0.0F;
    float strength = 0.0F;
};

std::vector<BaseFeature> selectFeatures(
    const cv::Mat& gray, const cv::Mat& mask,
    double threshold, int requestedLimit, double* usedThreshold = nullptr)
{
    cv::Mat gradientX;
    cv::Mat gradientY;
    const cv::Mat edges = cannyGradients(gray, mask, threshold, gradientX, gradientY);
    if (usedThreshold != nullptr)
        *usedThreshold = threshold;
    std::vector<BaseFeature> candidates;
    candidates.reserve(std::min<std::size_t>(
        gray.total(), static_cast<std::size_t>(requestedLimit * 8)));
    const float centerX = gray.cols * 0.5F;
    const float centerY = gray.rows * 0.5F;
    for (int y = 0; y < gray.rows; ++y) {
        const auto* selected = mask.ptr<unsigned char>(y);
        const auto* edge = edges.ptr<unsigned char>(y);
        const auto* gx = gradientX.ptr<short>(y);
        const auto* gy = gradientY.ptr<short>(y);
        for (int x = 0; x < gray.cols; ++x) {
            const float strength = static_cast<float>(gx[x] * gx[x] + gy[x] * gy[x]);
            // 先提取边缘再应用 ROI 遮罩，避免遮罩边界产生假轮廓；保留滞后连接的弱边缘。
            if (!selected[x] || !edge[x] || strength <= 0.0F)
                continue;
            const float inverseMagnitude =
                1.0F / std::sqrt(std::max(strength, 1.0F));
            candidates.push_back({
                x - centerX, y - centerY,
                gx[x] * inverseMagnitude, gy[x] * inverseMagnitude,
                strength});
        }
    }
    std::sort(candidates.begin(), candidates.end(),
              [](const BaseFeature& first, const BaseFeature& second) {
                  return first.strength > second.strength;
              });
    if (static_cast<int>(candidates.size()) <= requestedLimit)
        return candidates;

    const double selectedArea = static_cast<double>(
        std::max(1, cv::countNonZero(mask)));
    const int cellSize = std::max(
        1, static_cast<int>(std::sqrt(selectedArea / requestedLimit)));
    const int columns = (gray.cols + cellSize - 1) / cellSize;
    const int rows = (gray.rows + cellSize - 1) / cellSize;
    std::vector<unsigned char> occupied(
        static_cast<std::size_t>(columns * rows), 0);
    std::unordered_set<std::uint64_t> chosen;
    std::vector<BaseFeature> result;
    result.reserve(static_cast<std::size_t>(requestedLimit));
    // 覆盖网格不能让微弱背景纹理挤占强轮廓的预算；阈值只影响限额采样优先级，
    // 不改变 Canny 候选或不限额时的手动阈值语义。后续仍按强度补齐预算。
    const float coverageFloor = candidates[static_cast<std::size_t>(requestedLimit - 1)].strength * 0.25F;
    for (const BaseFeature& feature : candidates) {
        if (feature.strength < coverageFloor)
            break;
        const int imageX = cvRound(feature.x + centerX);
        const int imageY = cvRound(feature.y + centerY);
        const int index =
            (imageY / cellSize) * columns + imageX / cellSize;
        if (occupied[static_cast<std::size_t>(index)])
            continue;
        occupied[static_cast<std::size_t>(index)] = 1;
        const std::uint64_t key =
            (static_cast<std::uint64_t>(
                 static_cast<std::uint32_t>(imageY)) << 32)
            | static_cast<std::uint32_t>(imageX);
        chosen.insert(key);
        result.push_back(feature);
        if (static_cast<int>(result.size()) == requestedLimit)
            return result;
    }
    for (const BaseFeature& feature : candidates) {
        const int imageX = cvRound(feature.x + centerX);
        const int imageY = cvRound(feature.y + centerY);
        const std::uint64_t key =
            (static_cast<std::uint64_t>(
                 static_cast<std::uint32_t>(imageY)) << 32)
            | static_cast<std::uint32_t>(imageX);
        if (!chosen.insert(key).second)
            continue;
        result.push_back(feature);
        if (static_cast<int>(result.size()) == requestedLimit)
            break;
    }
    return result;
}

struct Feature
{
    short x = 0;
    short y = 0;
    unsigned char label = 0;
};

struct TemplateLevel
{
    cv::Size size;
    cv::Point2d reference;
    std::vector<Feature> features;
};

struct AngleTemplate
{
    double angle = 0.0;
    std::vector<TemplateLevel> levels;
};

TemplateLevel rotateFeatures(
    const std::vector<BaseFeature>& source, double productAngle)
{
    TemplateLevel result;
    if (source.empty())
        return result;
    const double radians =
        backendAngleDegrees(productAngle) * kPi / 180.0;
    const float cosine = static_cast<float>(std::cos(radians));
    const float sine = static_cast<float>(std::sin(radians));
    struct Rotated
    {
        int x;
        int y;
        unsigned char label;
    };
    std::vector<Rotated> rotated;
    rotated.reserve(source.size());
    std::unordered_set<std::uint64_t> occupied;
    int minimumX = std::numeric_limits<int>::max();
    int minimumY = std::numeric_limits<int>::max();
    int maximumX = std::numeric_limits<int>::min();
    int maximumY = std::numeric_limits<int>::min();
    for (const BaseFeature& feature : source) {
        const int x = cvRound(
            cosine * feature.x + sine * feature.y);
        const int y = cvRound(
            -sine * feature.x + cosine * feature.y);
        const std::uint64_t key =
            (static_cast<std::uint64_t>(
                 static_cast<std::uint32_t>(y)) << 32)
            | static_cast<std::uint32_t>(x);
        if (!occupied.insert(key).second)
            continue;
        const float gradientX =
            cosine * feature.gradientX + sine * feature.gradientY;
        const float gradientY =
            -sine * feature.gradientX + cosine * feature.gradientY;
        rotated.push_back({x, y, direction(gradientX, gradientY)});
        minimumX = std::min(minimumX, x);
        minimumY = std::min(minimumY, y);
        maximumX = std::max(maximumX, x);
        maximumY = std::max(maximumY, y);
    }
    if (rotated.empty())
        return result;
    result.size = cv::Size(
        maximumX - minimumX + 1,
        maximumY - minimumY + 1);
    result.reference = cv::Point2d(-minimumX, -minimumY);
    result.features.reserve(rotated.size());
    for (const Rotated& feature : rotated) {
        result.features.push_back({
            static_cast<short>(feature.x - minimumX),
            static_cast<short>(feature.y - minimumY),
            feature.label});
    }
    return result;
}

struct ResponseLevel
{
    std::array<cv::Mat, kDirections> maps;
    cv::Mat bits;
    cv::Mat exactBits;
    cv::Mat gradientX;
    cv::Mat gradientY;
    bool ignorePolarity = false;
};

using BitSimilarityTables =
    std::array<std::vector<unsigned char>, kDirections>;

int leastBitIndex(unsigned int value)
{
    static constexpr std::array<unsigned char, 32> indices{{
        0, 1, 28, 2, 29, 14, 24, 3,
        30, 22, 20, 15, 25, 17, 4, 8,
        31, 27, 13, 23, 21, 19, 16, 7,
        26, 12, 18, 6, 11, 5, 10, 9}};
    const unsigned int lowest = value & (~value + 1U);
    return indices[(lowest * 0x077CB531U) >> 27];
}

std::array<std::array<unsigned char, 256>, kDirections>
similarityTables(bool ignorePolarity)
{
    std::array<std::array<unsigned char, 256>, kDirections> tables{};
    for (int model = 0; model < kDirections; ++model) {
        for (int found = 0; found < kDirections; ++found) {
            const double difference =
                2.0 * kPi * (model - found) / kDirections;
            double similarity = std::cos(difference);
            if (ignorePolarity)
                similarity = std::abs(similarity);
            similarity = std::max(0.0, similarity);
            tables[model][found] = static_cast<unsigned char>(
                cvRound(similarity * kMaximumDirectionScore));
        }
        tables[model][kInvalidDirection] = 0;
    }
    return tables;
}

BitSimilarityTables createBitSimilarityTables(bool ignorePolarity)
{
    BitSimilarityTables tables;
    for (auto& table : tables)
        table.resize(1 << kDirections);
    const auto directionScores = similarityTables(ignorePolarity);
    for (int model = 0; model < kDirections; ++model) {
        for (int bits = 1; bits < (1 << kDirections); ++bits) {
            const int previous = bits & (bits - 1);
            const int found = leastBitIndex(
                static_cast<unsigned int>(bits ^ previous));
            tables[model][bits] = std::max(
                tables[model][previous],
                directionScores[model][found]);
        }
    }
    return tables;
}

const BitSimilarityTables& bitSimilarityTables(bool ignorePolarity)
{
    if (ignorePolarity) {
        static const BitSimilarityTables tables =
            createBitSimilarityTables(true);
        return tables;
    }
    static const BitSimilarityTables tables =
        createBitSimilarityTables(false);
    return tables;
}

ResponseLevel createResponseLevel(
    const cv::Mat& gray, double threshold,
    bool ignorePolarity, bool createMaps, bool spread)
{
    cv::Mat gradientX;
    cv::Mat gradientY;
    // 搜索保留梯度带宽以容纳旋转栅格化误差。双侧 Canny 的实测会造成旋转零件漏检。
    cv::Sobel(gray, gradientX, CV_16S, 1, 0, 3);
    cv::Sobel(gray, gradientY, CV_16S, 0, 1, 3);
    cv::Mat quantized;
    if (createMaps)
        quantized = cv::Mat(gray.size(), CV_8U, cv::Scalar(kInvalidDirection));
    ResponseLevel result;
    result.ignorePolarity = ignorePolarity;
    result.exactBits = cv::Mat::zeros(gray.size(), CV_16U);
    const double thresholdSquared = threshold * threshold;
#ifdef _OPENMP
    // 按原图行分工，避免按金字塔层分工时最大层独占一个线程。
#pragma omp parallel for if(gray.total() >= 65536) num_threads(parallelThreadCount(gray.rows)) schedule(static)
#endif
    for (int y = 0; y < gray.rows; ++y) {
        const auto* gx = gradientX.ptr<short>(y);
        const auto* gy = gradientY.ptr<short>(y);
        auto* output = createMaps ? quantized.ptr<unsigned char>(y) : nullptr;
        auto* exact = result.exactBits.ptr<std::uint16_t>(y);
        for (int x = 0; x < gray.cols; ++x) {
            const int strength = gx[x] * gx[x] + gy[x] * gy[x];
            if (strength <= 0 || strength < thresholdSquared)
                continue;
            const unsigned char label = direction(gx[x], gy[x]);
            if (output)
                output[x] = label;
            exact[x] = static_cast<std::uint16_t>(1U << label);
        }
    }

    if (spread) {
        result.bits = cv::Mat::zeros(gray.size(), CV_16U);
        for (int offsetY = -1; offsetY <= 1; ++offsetY) {
            for (int offsetX = -1; offsetX <= 1; ++offsetX) {
                const int sourceX = std::max(0, -offsetX);
                const int sourceY = std::max(0, -offsetY);
                const int targetX = std::max(0, offsetX);
                const int targetY = std::max(0, offsetY);
                const int width = gray.cols - std::abs(offsetX);
                const int height = gray.rows - std::abs(offsetY);
                cv::Mat target = result.bits(cv::Rect(
                    targetX, targetY, width, height));
                cv::bitwise_or(
                    target,
                    result.exactBits(cv::Rect(
                        sourceX, sourceY, width, height)),
                    target);
            }
        }
    } else {
        result.bits = result.exactBits;
    }
    if (createMaps) {
        const auto tables = similarityTables(ignorePolarity);
        const cv::Mat kernel = cv::Mat::ones(3, 3, CV_8U);
        for (int label = 0; label < kDirections; ++label) {
            const cv::Mat table(
                1, 256, CV_8U,
                const_cast<unsigned char*>(tables[label].data()));
            cv::Mat raw;
            cv::LUT(quantized, table, raw);
            cv::dilate(raw, result.maps[label], kernel);
        }
    }
    result.gradientX = std::move(gradientX);
    result.gradientY = std::move(gradientY);
    return result;
}

float pointScore(const ResponseLevel& response,
                 const TemplateLevel& templ,
                 int locationX, int locationY,
                 bool exact = false)
{
    const BitSimilarityTables& tables =
        bitSimilarityTables(response.ignorePolarity);
    const cv::Mat& bits = exact
        ? response.exactBits : response.bits;
    int score = 0;
    for (const Feature& feature : templ.features) {
        score += tables[feature.label][
            bits.ptr<std::uint16_t>(
                locationY + feature.y)[locationX + feature.x]];
    }
    return static_cast<float>(score)
        / static_cast<float>(
            kMaximumDirectionScore * templ.features.size());
}

float spatialPointScore(const ResponseLevel& response,
                        const TemplateLevel& templ,
                        int locationX, int locationY)
{
    const BitSimilarityTables& tables =
        bitSimilarityTables(response.ignorePolarity);
    const cv::Mat& exactBits = response.exactBits;
    int score = 0;
    for (const Feature& feature : templ.features) {
        const int centerX = locationX + feature.x;
        const int centerY = locationY + feature.y;
        const int firstX = std::max(0, centerX - 1);
        const int lastX = std::min(exactBits.cols - 1, centerX + 1);
        const int firstY = std::max(0, centerY - 1);
        const int lastY = std::min(exactBits.rows - 1, centerY + 1);
        std::uint16_t nearbyDirections = 0;
        for (int y = firstY; y <= lastY; ++y) {
            const auto* row = exactBits.ptr<std::uint16_t>(y);
            for (int x = firstX; x <= lastX; ++x)
                nearbyDirections |= row[x];
        }
        score += tables[feature.label][nearbyDirections];
    }
    return static_cast<float>(score)
        / static_cast<float>(
            kMaximumDirectionScore * templ.features.size());
}

bool sampleGradient(const ResponseLevel& response,
                    double x, double y,
                    double& gradientX, double& gradientY)
{
    if (x < 0.0 || y < 0.0
        || x > response.gradientX.cols - 1.0
        || y > response.gradientX.rows - 1.0) {
        return false;
    }
    const int x0 = static_cast<int>(std::floor(x));
    const int y0 = static_cast<int>(std::floor(y));
    const int x1 = std::min(response.gradientX.cols - 1, x0 + 1);
    const int y1 = std::min(response.gradientX.rows - 1, y0 + 1);
    const double fractionX = x - x0;
    const double fractionY = y - y0;
    const auto interpolate = [&](const cv::Mat& gradient) {
        const auto* first = gradient.ptr<short>(y0);
        const auto* second = gradient.ptr<short>(y1);
        const double top = first[x0]
            + fractionX * (first[x1] - first[x0]);
        const double bottom = second[x0]
            + fractionX * (second[x1] - second[x0]);
        return top + fractionY * (bottom - top);
    };
    gradientX = interpolate(response.gradientX);
    gradientY = interpolate(response.gradientY);
    return true;
}

double continuousGradientScore(
    const ResponseLevel& response,
    const std::vector<BaseFeature>& features,
    double centerX, double centerY, double productAngle,
    double threshold, bool ignorePolarity)
{
    const double radians =
        backendAngleDegrees(productAngle) * kPi / 180.0;
    const double cosine = std::cos(radians);
    const double sine = std::sin(radians);
    const double thresholdSquared = threshold * threshold;
    double score = 0.0;
    for (const BaseFeature& feature : features) {
        const double x = centerX
            + cosine * feature.x + sine * feature.y;
        const double y = centerY
            - sine * feature.x + cosine * feature.y;
        const double modelX =
            cosine * feature.gradientX + sine * feature.gradientY;
        const double modelY =
            -sine * feature.gradientX + cosine * feature.gradientY;
        double foundX = 0.0;
        double foundY = 0.0;
        if (!sampleGradient(response, x, y, foundX, foundY))
            continue;
        const double magnitudeSquared =
            foundX * foundX + foundY * foundY;
        if (magnitudeSquared < thresholdSquared)
            continue;
        double similarity = (modelX * foundX + modelY * foundY)
            / std::sqrt(magnitudeSquared);
        if (ignorePolarity)
            similarity = std::abs(similarity);
        score += std::max(0.0, similarity);
    }
    return features.empty() ? 0.0 : score / features.size();
}

bool gradientPoseCorrection(
    const ResponseLevel& response,
    const std::vector<BaseFeature>& features,
    double centerX, double centerY, double productAngle,
    double threshold, bool ignorePolarity,
    cv::Vec3d& correction)
{
    const double radians =
        backendAngleDegrees(productAngle) * kPi / 180.0;
    const double cosine = std::cos(radians);
    const double sine = std::sin(radians);
    const double thresholdSquared = threshold * threshold;
    cv::Matx33d normal = cv::Matx33d::zeros();
    cv::Vec3d rightHandSide(0.0, 0.0, 0.0);
    int correspondenceCount = 0;
    for (const BaseFeature& feature : features) {
        const double rotatedX =
            cosine * feature.x + sine * feature.y;
        const double rotatedY =
            -sine * feature.x + cosine * feature.y;
        const double x = centerX + rotatedX;
        const double y = centerY + rotatedY;
        const double modelX =
            cosine * feature.gradientX + sine * feature.gradientY;
        const double modelY =
            -sine * feature.gradientX + cosine * feature.gradientY;
        double bestQuality = 0.0;
        double bestDot = 0.0;
        double bestOffset = 0.0;
        for (int sample = -8; sample <= 8; ++sample) {
            const double normalOffset = sample * 0.25;
            double foundX = 0.0;
            double foundY = 0.0;
            if (!sampleGradient(
                    response,
                    x + modelX * normalOffset,
                    y + modelY * normalOffset,
                    foundX, foundY)) {
                continue;
            }
            const double magnitudeSquared =
                foundX * foundX + foundY * foundY;
            if (magnitudeSquared < thresholdSquared)
                continue;
            const double inverseMagnitude =
                1.0 / std::sqrt(magnitudeSquared);
            foundX *= inverseMagnitude;
            foundY *= inverseMagnitude;
            double dot = modelX * foundX + modelY * foundY;
            if (ignorePolarity && dot < 0.0)
                dot = -dot;
            if (dot < 0.75)
                continue;
            const double magnitude = std::sqrt(magnitudeSquared);
            const double strengthWeight = std::min(
                1.0, magnitude / std::max(1.0, threshold * 4.0));
            const double distanceWeight =
                1.0 - 0.04 * std::abs(normalOffset);
            const double quality = dot * strengthWeight * distanceWeight;
            if (quality > bestQuality) {
                bestQuality = quality;
                bestDot = dot;
                bestOffset = normalOffset;
            }
        }
        if (bestQuality <= 0.0)
            continue;
        // The public product angle rotates image coordinates by
        // (x, y) -> (cos*x - sin*y, sin*x + cos*y). Its derivative
        // in radians is therefore (-rotatedY, rotatedX).
        const cv::Vec3d equation(
            modelX, modelY,
            -modelX * rotatedY + modelY * rotatedX);
        const double weight = bestQuality * bestDot
            / (1.0 + 0.20 * bestOffset * bestOffset);
        for (int row = 0; row < 3; ++row) {
            rightHandSide[row] +=
                weight * equation[row] * bestOffset;
            for (int column = 0; column < 3; ++column) {
                normal(row, column) +=
                    weight * equation[row] * equation[column];
            }
        }
        ++correspondenceCount;
    }
    if (correspondenceCount < 6)
        return false;
    normal(0, 0) += 1e-6;
    normal(1, 1) += 1e-6;
    normal(2, 2) += 1e-4;
    correction =
        normal.inv(cv::DECOMP_SVD) * rightHandSide;
    return std::isfinite(correction[0])
        && std::isfinite(correction[1])
        && std::isfinite(correction[2]);
}

struct ContinuousPose
{
    double x = 0.0;
    double y = 0.0;
    double angle = 0.0;
    double score = 0.0;
};

/// <summary>用三个相邻样本估计局部抛物线顶点。</summary>
double parabolicOffset(double before, double center, double after);

/// <summary>只在候选轮廓附近计算双线性 Sobel 梯度，避免为大图分配整幅原分辨率响应图。</summary>
bool sparseGradient(const cv::Mat& gray, double x, double y, double& gx, double& gy)
{
    const int ix = cvFloor(x), iy = cvFloor(y);
    if (ix < 1 || iy < 1 || ix + 2 >= gray.cols || iy + 2 >= gray.rows) return false;
    gx = gy = 0.0;
    for (int dy = 0; dy <= 1; ++dy) for (int dx = 0; dx <= 1; ++dx) {
        const int px = ix + dx, py = iy + dy;
        const auto* above = gray.ptr<unsigned char>(py - 1);
        const auto* row = gray.ptr<unsigned char>(py);
        const auto* below = gray.ptr<unsigned char>(py + 1);
        const double weight = (dx ? x - ix : 1.0 - x + ix) * (dy ? y - iy : 1.0 - y + iy);
        gx += weight * (above[px + 1] + 2 * row[px + 1] + below[px + 1]
            - above[px - 1] - 2 * row[px - 1] - below[px - 1]);
        gy += weight * (below[px - 1] + 2 * below[px] + below[px + 1]
            - above[px - 1] - 2 * above[px] - above[px + 1]);
    }
    return true;
}

/// <summary>沿模型法线寻找同极性的边缘峰值，并在相邻采样之间拟合亚像素位置。</summary>
bool sparseEdgeOffset(const cv::Mat& gray, double x, double y, double nx, double ny,
    double radius, double threshold, bool ignorePolarity, double& offset, double& quality)
{
    double best = 0.0;
    offset = quality = 0.0;
    const int steps = cvCeil(radius);
    for (int step = -steps; step <= steps; ++step) {
        double gx, gy;
        if (!sparseGradient(gray, x + nx * step, y + ny * step, gx, gy)) continue;
        const double magnitude = std::hypot(gx, gy);
        double projection = gx * nx + gy * ny;
        if (ignorePolarity) projection = std::abs(projection);
        if (magnitude < threshold || projection < magnitude * 0.95) continue;
        const double score = projection / (1.0 + 0.04 * step * step);
        if (score > best) { best = score; offset = step; quality = projection / magnitude; }
    }
    if (best <= 0.0) return false;
    const auto projected = [&](double shift) {
        double gx, gy;
        if (!sparseGradient(gray, x + nx * shift, y + ny * shift, gx, gy)) return 0.0;
        const double value = gx * nx + gy * ny;
        return ignorePolarity ? std::abs(value) : value;
    };
    offset += 0.5 * parabolicOffset(projected(offset - 0.5), projected(offset), projected(offset + 0.5));
    return true;
}

/// <summary>将模型整数 Canny 点校准到其自身灰度边缘峰值，消除原图定位的固有量化偏差。</summary>
std::vector<BaseFeature> precisionFeatures(const cv::Mat& gray, const std::vector<BaseFeature>& features)
{
    auto result = features;
    for (auto& feature : result) {
        double offset, quality;
        if (sparseEdgeOffset(gray, feature.x + gray.cols * 0.5, feature.y + gray.rows * 0.5,
                feature.gradientX, feature.gradientY, 1.0, 1.0, false, offset, quality)) {
            feature.x += static_cast<float>(feature.gradientX * offset);
            feature.y += static_cast<float>(feature.gradientY * offset);
        }
    }
    return result;
}

/// <summary>原分辨率稳健点到法线配准；搜索窗口逐步收敛，变换只含平移与旋转。</summary>
ContinuousPose refineSparsePose(const cv::Mat& gray, const std::vector<BaseFeature>& features,
    ContinuousPose pose, int scale, double threshold, bool ignorePolarity, double lowAngle, double highAngle,
    bool subPixel)
{
    const auto constrainAngle = [&](double angle) {
        if (highAngle - lowAngle >= 359.0) return std::remainder(angle, 360.0);
        return std::clamp(angle, lowAngle, highAngle);
    };
    for (int iteration = 0; iteration < 8; ++iteration) {
        const double radius = std::max(2.0, scale * 1.5 / std::pow(1.6, iteration));
        const double radians = pose.angle * kPi / 180.0;
        const double cosine = std::cos(radians), sine = std::sin(radians);
        cv::Matx33d normal = cv::Matx33d::zeros();
        cv::Vec3d rhs(0, 0, 0);
        int supported = 0;
        for (const auto& feature : features) {
            const double rx = cosine * feature.x - sine * feature.y, ry = sine * feature.x + cosine * feature.y;
            const double nx = cosine * feature.gradientX - sine * feature.gradientY;
            const double ny = sine * feature.gradientX + cosine * feature.gradientY;
            double offset, quality;
            if (!sparseEdgeOffset(gray, pose.x + rx, pose.y + ry, nx, ny, radius, threshold, ignorePolarity, offset, quality)) continue;
            const double weight = quality * quality / (1.0 + offset * offset / 9.0);
            const cv::Vec3d equation(nx, ny, -nx * ry + ny * rx);
            for (int row = 0; row < 3; ++row) {
                rhs[row] += weight * equation[row] * offset;
                for (int col = 0; col < 3; ++col) normal(row, col) += weight * equation[row] * equation[col];
            }
            ++supported;
        }
        if (supported < 8) break;
        normal(0, 0) += 1e-6; normal(1, 1) += 1e-6; normal(2, 2) += 1e-4;
        const cv::Vec3d delta = normal.inv(cv::DECOMP_SVD) * rhs;
        pose.x += std::clamp(delta[0], -radius, radius); pose.y += std::clamp(delta[1], -radius, radius);
        pose.angle = constrainAngle(pose.angle + std::clamp(delta[2] * 180.0 / kPi, -1.0, 1.0));
        if (iteration >= 3 && std::abs(delta[0]) < 0.005 && std::abs(delta[1]) < 0.005 && std::abs(delta[2]) < 1e-5) break;
    }
    if (!subPixel) { pose.x = std::round(pose.x); pose.y = std::round(pose.y); pose.angle = constrainAngle(std::round(pose.angle)); }
    const double radians = pose.angle * kPi / 180.0, cosine = std::cos(radians), sine = std::sin(radians);
    double supported = 0.0;
    // 分区覆盖防止局部圆孔或长直边解释整个组合模板；小分区不作为独立判据。
    std::array<double, 16> support{};
    std::array<int, 16> counts{};
    double minX = 0, maxX = 0, minY = 0, maxY = 0;
    for (const auto& feature : features) {
        minX = std::min(minX, static_cast<double>(feature.x)); maxX = std::max(maxX, static_cast<double>(feature.x));
        minY = std::min(minY, static_cast<double>(feature.y)); maxY = std::max(maxY, static_cast<double>(feature.y));
    }
    for (const auto& feature : features) {
        const int binX = std::clamp(static_cast<int>(4 * (feature.x - minX) / std::max(1.0, maxX - minX)), 0, 3);
        const int binY = std::clamp(static_cast<int>(4 * (feature.y - minY) / std::max(1.0, maxY - minY)), 0, 3);
        const int bin = binY * 4 + binX;
        ++counts[bin];
        double offset, quality;
        if (sparseEdgeOffset(gray, pose.x + cosine * feature.x - sine * feature.y,
            pose.y + sine * feature.x + cosine * feature.y,
            cosine * feature.gradientX - sine * feature.gradientY, sine * feature.gradientX + cosine * feature.gradientY,
            2.0, threshold, ignorePolarity, offset, quality)) { supported += quality; support[bin] += quality; }
    }
    pose.score = supported / std::max<std::size_t>(1, features.size());
    double weakest = 1.0;
    for (std::size_t bin = 0; bin < counts.size(); ++bin)
        if (counts[bin] >= std::max(8, static_cast<int>(features.size() / 12)))
            weakest = std::min(weakest, support[bin] / counts[bin]);
    pose.score *= std::min(1.0, weakest / 0.4);
    return pose;
}

ContinuousPose refineContinuousPose(
    const ResponseLevel& response,
    const std::vector<BaseFeature>& features,
    const cv::Point2d& initialCenter, double initialAngle,
    double lowAngle, double highAngle, double threshold,
    bool ignorePolarity)
{
    ContinuousPose pose{
        initialCenter.x, initialCenter.y, initialAngle, 0.0};
    const bool fullCircle = highAngle - lowAngle >= 359.0;
    const auto constrainAngle = [&](double angle) {
        if (fullCircle) {
            while (angle > 180.0)
                angle -= 360.0;
            while (angle < -180.0)
                angle += 360.0;
            return angle;
        }
        return std::clamp(angle, lowAngle, highAngle);
    };
    const auto evaluate = [&](double x, double y, double angle) {
        return continuousGradientScore(
            response, features, x, y, constrainAngle(angle),
            threshold, ignorePolarity);
    };
    pose.angle = constrainAngle(pose.angle);
    pose.score = evaluate(pose.x, pose.y, pose.angle);

    for (int iteration = 0; iteration < 2; ++iteration) {
        cv::Vec3d correction;
        if (!gradientPoseCorrection(
                response, features, pose.x, pose.y, pose.angle,
                threshold, ignorePolarity, correction)) {
            break;
        }
        const double deltaX = std::clamp(correction[0], -0.75, 0.75);
        const double deltaY = std::clamp(correction[1], -0.75, 0.75);
        const double deltaAngle = std::clamp(
            correction[2] * 180.0 / kPi, -0.75, 0.75);
        pose.x += deltaX;
        pose.y += deltaY;
        pose.angle = constrainAngle(pose.angle + deltaAngle);
        if (std::abs(deltaX) < 0.01
            && std::abs(deltaY) < 0.01
            && std::abs(deltaAngle) < 0.01) {
            break;
        }
    }
    const auto optimizeAngle = [&](double step, int radius) {
        const double center = pose.angle;
        for (int offset = -radius; offset <= radius; ++offset) {
            const double candidate = constrainAngle(center + offset * step);
            const double score = evaluate(pose.x, pose.y, candidate);
            if (score > pose.score) {
                pose.angle = candidate;
                pose.score = score;
            }
        }
    };
    pose.score = evaluate(pose.x, pose.y, pose.angle);
    optimizeAngle(0.5, 6);
    optimizeAngle(0.1, 5);
    pose.score = evaluate(pose.x, pose.y, pose.angle);
    return pose;
}

bool refineIntensityPose(const cv::Mat& model, const cv::Mat& image,
                         ContinuousPose& pose)
{
    if (model.empty() || image.empty())
        return false;
    const int radius = cvCeil(
        0.5 * std::hypot(model.cols, model.rows)) + 6;
    const cv::Rect requested(
        cvRound(pose.x) - radius, cvRound(pose.y) - radius,
        2 * radius + 1, 2 * radius + 1);
    const cv::Rect area = requested
        & cv::Rect(0, 0, image.cols, image.rows);
    if (area.width < model.cols || area.height < model.rows)
        return false;

    const double radians = pose.angle * kPi / 180.0;
    const float cosine = static_cast<float>(std::cos(radians));
    const float sine = static_cast<float>(std::sin(radians));
    const float modelCenterX = model.cols * 0.5F;
    const float modelCenterY = model.rows * 0.5F;
    const float localCenterX = static_cast<float>(pose.x - area.x);
    const float localCenterY = static_cast<float>(pose.y - area.y);
    cv::Mat warp = (cv::Mat_<float>(2, 3)
        << cosine, -sine,
           localCenterX - cosine * modelCenterX
               + sine * modelCenterY,
           sine, cosine,
           localCenterY - sine * modelCenterX
               - cosine * modelCenterY);
    try {
        const double correlation = cv::findTransformECC(
            model, image(area), warp, cv::MOTION_EUCLIDEAN,
            cv::TermCriteria(
                cv::TermCriteria::COUNT | cv::TermCriteria::EPS,
                6, 1e-4), cv::noArray(), 3);
        const double refinedAngle = std::atan2(
            static_cast<double>(warp.at<float>(1, 0)),
            static_cast<double>(warp.at<float>(0, 0))) * 180.0 / kPi;
        const double refinedCenterX = area.x
            + warp.at<float>(0, 0) * modelCenterX
            + warp.at<float>(0, 1) * modelCenterY
            + warp.at<float>(0, 2);
        const double refinedCenterY = area.y
            + warp.at<float>(1, 0) * modelCenterX
            + warp.at<float>(1, 1) * modelCenterY
            + warp.at<float>(1, 2);
        const double centerDelta = std::hypot(
            refinedCenterX - pose.x, refinedCenterY - pose.y);
        const double angleDelta = std::abs(std::remainder(
            refinedAngle - pose.angle, 360.0));
        if (!std::isfinite(correlation) || correlation < 0.75
            || centerDelta > 1.5 || angleDelta > 2.0) {
            return false;
        }
        pose.x = refinedCenterX;
        pose.y = refinedCenterY;
        pose.angle = refinedAngle;
        return true;
    } catch (const cv::Exception&) {
        return false;
    }
}

struct Candidate
{
    std::size_t templateIndex = 0;
    int level = 0;
    cv::Point location;
    float score = 0.0F;
};

void trimCandidates(
    std::vector<Candidate>& candidates, std::size_t limit,
    const std::vector<AngleTemplate>& templates)
{
    std::sort(candidates.begin(), candidates.end(),
              [](const Candidate& first, const Candidate& second) {
                  return first.score > second.score;
              });
    std::vector<Candidate> kept;
    kept.reserve(std::min(limit, candidates.size()));
    for (const Candidate& candidate : candidates) {
        bool duplicate = false;
        for (const Candidate& previous : kept) {
            if (previous.level != candidate.level)
                continue;
            const TemplateLevel& candidateTemplate =
                templates[candidate.templateIndex].levels[
                    static_cast<std::size_t>(candidate.level)];
            const TemplateLevel& previousTemplate =
                templates[previous.templateIndex].levels[
                    static_cast<std::size_t>(previous.level)];
            const cv::Point2d candidateCenter(
                candidate.location.x + candidateTemplate.reference.x,
                candidate.location.y + candidateTemplate.reference.y);
            const cv::Point2d previousCenter(
                previous.location.x + previousTemplate.reference.x,
                previous.location.y + previousTemplate.reference.y);
            const double angleDistance = std::abs(std::remainder(
                templates[previous.templateIndex].angle - templates[candidate.templateIndex].angle, 360.0));
            // 不能只按中心合并：对称外框可能在同一中心保留不同内部结构的姿态假设。
            if (std::abs(previousCenter.x - candidateCenter.x) <= 1.5
                && std::abs(previousCenter.y - candidateCenter.y) <= 1.5
                && angleDistance <= 4.0 * std::pow(2.0, candidate.level)) {
                duplicate = true;
                break;
            }
        }
        if (!duplicate)
            kept.push_back(candidate);
        if (kept.size() == limit)
            break;
    }
    candidates.swap(kept);
}

double rotatedOverlap(const MatchPose& first, const MatchPose& second)
{
    const cv::RotatedRect firstRectangle(
        cv::Point2f(
            static_cast<float>(first.center.x),
            static_cast<float>(first.center.y)),
        cv::Size2f(
            static_cast<float>(first.width),
            static_cast<float>(first.height)),
        static_cast<float>(backendAngleDegrees(first.angleDegrees)));
    const cv::RotatedRect secondRectangle(
        cv::Point2f(
            static_cast<float>(second.center.x),
            static_cast<float>(second.center.y)),
        cv::Size2f(
            static_cast<float>(second.width),
            static_cast<float>(second.height)),
        static_cast<float>(backendAngleDegrees(second.angleDegrees)));
    std::vector<cv::Point2f> intersection;
    if (cv::rotatedRectangleIntersection(
            firstRectangle, secondRectangle, intersection)
        == cv::INTERSECT_NONE || intersection.size() < 3) {
        return 0.0;
    }
    const double denominator = std::min(
        first.width * first.height,
        second.width * second.height);
    return denominator > 0.0
        ? std::abs(cv::contourArea(intersection)) / denominator
        : 0.0;
}

double parabolicOffset(double before, double center, double after)
{
    const double denominator = before - 2.0 * center + after;
    if (std::abs(denominator) <= 1e-9)
        return 0.0;
    return std::clamp(
        0.5 * (before - after) / denominator, -0.5, 0.5);
}

} // namespace

struct CShapeMatchCV::Impl
{
    /// <summary>大模板的低分辨率候选模型；小模板不使用该入口。</summary>
    std::unique_ptr<CShapeMatchCV> coarseModel;
    /// <summary>原图到候选图的二次幂比例。</summary>
    int coarseScale = 1;
    /// <summary>奇数模板尺寸在降采样后的模型中心偏移。</summary>
    cv::Point2d coarseCenterOffset;
    /// <summary>用于原分辨率精修的模型亚像素边缘。</summary>
    std::vector<BaseFeature> preciseFeatures;
    /// <summary>按用户建模阈值提取的完整边缘，与搜索采样预算分开保存。</summary>
    cv::Mat contourEdges;
    /// <summary>完整边缘的有序路径，只在建模/编辑时更新，不在每次搜索时提取。</summary>
    std::vector<std::vector<cv::Point>> contourPaths;
    /// <summary>可靠封闭内部轮廓数量及面积区间（孔洞、字符闭环等通用拓扑特征）。</summary>
    int closedStructureCount = 0;
    /// <summary>重复闭环的模型中心，用于一对一空间对应，防止侧面细节凑足数量。</summary>
    std::vector<cv::Point2d> closedStructureCenters;
    /// <summary>闭环面积下界，过滤微小噪点。</summary>
    double closedMinimumArea = 0.0;
    /// <summary>闭环面积上界，过滤整个物体外框。</summary>
    double closedMaximumArea = 0.0;
    /// <summary>完整边缘的内部结构校验数据，独立于稀疏正向特征采样。</summary>
    cv::Mat structureEdges;
    /// <summary>凸包内缩后的有效区域，不惩罚模板外围背景；涂抹区域同步移除。</summary>
    cv::Mat structureMask;
    /// <summary>到模型完整边缘的距离，供搜索边缘反向验证。</summary>
    cv::Mat structureDistance;
    /// <summary>内部预期轮廓像素数，用于规范化多余轮廓。</summary>
    int structureEdgeCount = 0;
    /// <summary>有效内部模型边缘坐标，用于检查缺失结构。</summary>
    std::vector<cv::Point> structurePoints;
    /// <summary>旋转栅格化和轻微透视的空间容差，单位为原图像素。</summary>
    double structureTolerance = 2.0;
    /// <summary>反向结构检验的稳健强边缘阈值，避免低手动阈值的背景纹理稀释惩罚。</summary>
    double structureContrast = 0.0;
    std::vector<AngleTemplate> templates;
    std::vector<BaseFeature> baseFeatures;
    // 保留各层实际入选特征，编辑时过滤原特征，避免重新提取后补入已删除区域。
    std::vector<std::vector<BaseFeature>> editableLevels;
    cv::Mat modelGray;
    cv::Size modelSize;
    double angleStepDegrees = 1.0;
    double minContrast = 10.0;
    /// <summary>原图层实际采用的 Canny 高阈值，自动/手动均可查询。</summary>
    double modelContrast = 0.0;
    int levels = 1;
    bool ignorePolarity = false;
    bool intensityRefinementAllowed = false;
};

CShapeMatchCV::CShapeMatchCV() : impl_(new Impl)
{
    // OpenCV 静态链接在本 DLL 内。固定内部线程预算，避免24线程对中小图的调度开销；
    // call_once 保证多个句柄并发创建时不会在运行过程中反复修改并行后端。
    static std::once_flag threadBudget;
    std::call_once(threadBudget, [] { cv::setNumThreads(std::min(4, cv::getNumberOfCPUs())); });
}
CShapeMatchCV::~CShapeMatchCV() = default;
CShapeMatchCV& CShapeMatchCV::operator=(CShapeMatchCV&&) noexcept = default;

bool CShapeMatchCV::createModel(
    const cv::Mat& image,
    int roiX, int roiY, int roiWidth, int roiHeight,
    int numLevels, double angleStartDegrees, double angleEndDegrees,
    double angleStepDegrees, const std::string& metric,
    double contrast, double minContrast, int featureCount,
    const cv::Mat& inputMask,
    std::string& message)
{
    message.clear();
    clear();
    const cv::Rect roi(roiX, roiY, roiWidth, roiHeight);
    const cv::Mat gray = gray8(image);
    if (gray.empty()) {
        message =
            "The model image must be 8-bit gray, BGR, or BGRA.";
        return false;
    }
    if ((roi & cv::Rect(0, 0, gray.cols, gray.rows)) != roi
        || roi.width < 8 || roi.height < 8
        || !validModel(numLevels, angleStartDegrees, angleEndDegrees,
                       angleStepDegrees, contrast, minContrast,
                       featureCount)) {
        message = "The model ROI or parameters are invalid.";
        return false;
    }
    try {
        cv::Mat selected;
        if (!inputMask.empty()) {
            selected = inputMask.size() == gray.size()
                ? inputMask(roi) : inputMask;
        }
        cv::Mat levelImage = gray(roi).clone();
        cv::Mat levelMask = mask8(selected, roi.size());
        if (levelMask.empty()) {
            message =
                "The model mask is empty or has an invalid size.";
            return false;
        }
        impl_->intensityRefinementAllowed =
            cv::countNonZero(levelMask)
                == static_cast<int>(levelMask.total());

        impl_->levels = numLevels > 0
            ? std::min(numLevels, kMaxLevels)
            : autoLevels(roi.size());
        std::vector<std::vector<BaseFeature>> baseLevels;
        baseLevels.reserve(static_cast<std::size_t>(impl_->levels));
        double resolvedContrast = contrast;
        for (int level = 0; level < impl_->levels; ++level) {
            const int automaticFeatureLimit = std::clamp(
                static_cast<int>(levelImage.total() / 32),
                32, 512);
            // featureCount 表示原图层期望的特征数量；金字塔每下降一层，
            // 按面积比例缩减，既让界面参数真正生效，也避免粗层过密。
            const int requestedFeatureLimit = featureCount > 0
                ? std::clamp(
                    cvRound(featureCount / std::pow(4.0, level)),
                    std::min(featureCount, 24), 10000)
                : automaticFeatureLimit;
            // 自动阈值描述原图细节。缩小后的小轮廓会混叠，粗层仅用于找候选，
            // 将其高阈值限制为搜索阈值的两倍，使低阈值不高于搜索可见梯度。
            // 手动模式不改变原来的金字塔阈值语义。
            const double coarseContrast = contrast == 0.0
                ? std::min(resolvedContrast, std::max(1.0, minContrast * 2.0))
                : resolvedContrast;
            std::vector<BaseFeature> selectedFeatures = selectFeatures(
                levelImage, levelMask,
                level == 0 ? resolvedContrast : std::max(
                    1.0, coarseContrast / std::pow(1.2, level)),
                requestedFeatureLimit, level == 0 ? &resolvedContrast : nullptr);
            if (selectedFeatures.size() < 8)
                break;
            baseLevels.push_back(std::move(selectedFeatures));
            if (level + 1 == impl_->levels)
                break;
            cv::Mat nextImage;
            cv::Mat nextMask;
            cv::pyrDown(levelImage, nextImage);
            cv::resize(
                levelMask, nextMask, nextImage.size(),
                0.0, 0.0, cv::INTER_NEAREST);
            levelImage = std::move(nextImage);
            levelMask = std::move(nextMask);
        }
        if (baseLevels.empty()) {
            message =
                "The ROI contains too few shape features.";
            return false;
        }
        impl_->levels = static_cast<int>(baseLevels.size());
        impl_->baseFeatures = baseLevels.front();
        impl_->modelContrast = resolvedContrast;
        impl_->editableLevels = baseLevels;
        impl_->modelGray = gray(roi).clone();

        // 完整轮廓沿用原图层真实阈值与遮罩，不对300个采样点插值造线。
        cv::Mat contourX, contourY;
        double contourThreshold = resolvedContrast;
        impl_->contourEdges = cannyGradients(impl_->modelGray, mask8(selected, roi.size()),
            contourThreshold, contourX, contourY);
        cv::bitwise_and(impl_->contourEdges, mask8(selected, roi.size()), impl_->contourEdges);
        impl_->contourEdges.row(0).setTo(0);
        impl_->contourEdges.row(roi.height - 1).setTo(0);
        impl_->contourEdges.col(0).setTo(0);
        impl_->contourEdges.col(roi.width - 1).setTo(0);
        cv::findContours(impl_->contourEdges, impl_->contourPaths, cv::RETR_LIST, cv::CHAIN_APPROX_NONE);

        // 正向点匹配无法区别“模板包含于目标”的结构。另存完整 Canny 边缘，
        // 只在其凸包内部检查多余轮廓，避免外围背景和边界形变主导反向评分。
        cv::Mat structureX, structureY;
        double structureThreshold = 0.0;
        impl_->structureEdges = cannyGradients(impl_->modelGray,
            mask8(selected, roi.size()), structureThreshold, structureX, structureY);
        impl_->structureContrast = std::max(resolvedContrast, structureThreshold);
        if (impl_->structureContrast != structureThreshold)
            cv::Canny(structureX, structureY, impl_->structureEdges,
                impl_->structureContrast * 0.5, impl_->structureContrast, true);
        cv::bitwise_and(impl_->structureEdges, mask8(selected, roi.size()), impl_->structureEdges);
        std::vector<cv::Point> edgeLocations, hull;
        cv::findNonZero(impl_->structureEdges, edgeLocations);
        impl_->structureMask = cv::Mat::zeros(roi.size(), CV_8U);
        impl_->structureTolerance = std::clamp(std::min(roi.width, roi.height) * 0.035, 1.5, 4.0);
        if (edgeLocations.size() >= 3) {
            cv::convexHull(edgeLocations, hull);
            cv::fillConvexPoly(impl_->structureMask, hull, cv::Scalar(255));
            const int radius = cvCeil(impl_->structureTolerance) + 1;
            cv::erode(impl_->structureMask, impl_->structureMask,
                cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(radius * 2 + 1, radius * 2 + 1)),
                cv::Point(-1, -1), 1, cv::BORDER_CONSTANT, cv::Scalar(0));
            cv::bitwise_and(impl_->structureMask, mask8(selected, roi.size()), impl_->structureMask);
        }
        cv::distanceTransform(255 - impl_->structureEdges, impl_->structureDistance, cv::DIST_L2, 3);
        impl_->structureEdgeCount = cv::countNonZero(impl_->structureEdges & impl_->structureMask);
        cv::findNonZero(impl_->structureEdges & impl_->structureMask, impl_->structurePoints);
        std::vector<std::vector<cv::Point>> closedContours;
        cv::Mat internalEdges = impl_->structureEdges & impl_->structureMask;
        cv::morphologyEx(internalEdges, internalEdges, cv::MORPH_CLOSE, cv::Mat::ones(3, 3, CV_8U));
        cv::findContours(internalEdges, closedContours, cv::RETR_LIST, cv::CHAIN_APPROX_SIMPLE);
        std::vector<double> loopAreas;
        const double minimumLoopArea = std::max(4.0, roi.area() * 0.0005);
        for (const auto& contour : closedContours) {
            // 单像素闭环的内外轮廓方向相反，只取正面积避免重复计数。
            const double area = cv::contourArea(contour, true);
            if (area >= minimumLoopArea)
                loopAreas.push_back(area);
        }
        // 非矩形掩膜可能截断闭环，多个不相连区域还会被凸包裁出新的局部闭环。
        // 这些局部闭环不能与搜索整图的完整闭环数量直接比较。仅全矩形模板
        // 使用拓扑数量约束；掩膜模板仍保留掩膜内的正向、反向与缺失轮廓评分。
        if (impl_->intensityRefinementAllowed && !loopAreas.empty()) {
            const double largest = *std::max_element(loopAreas.begin(), loopAreas.end());
            loopAreas.erase(std::remove_if(loopAreas.begin(), loopAreas.end(),
                [&](double area) { return area < largest * 0.15; }), loopAreas.end());
            const double smallest = *std::min_element(loopAreas.begin(), loopAreas.end());
            // 仅对面积相近的重复封闭结构使用数量约束；复杂零件的碎小闭环不稳定。
            if (loopAreas.size() >= 2 && loopAreas.size() <= 32 && largest <= smallest * 2.0) {
                impl_->closedStructureCount = static_cast<int>(loopAreas.size());
                impl_->closedMinimumArea = smallest * 0.2;
                impl_->closedMaximumArea = largest * 2.5;
                for (const auto& contour : closedContours) {
                    if (cv::contourArea(contour, true) < std::max(minimumLoopArea, largest * 0.15))
                        continue;
                    const auto moments = cv::moments(contour);
                    impl_->closedStructureCenters.emplace_back(moments.m10 / moments.m00, moments.m01 / moments.m00);
                }
            }
        }

        const std::vector<double> angles = modelAngles(
            // 模型创建参数继续使用原有角度约定。
            angleStartDegrees, angleEndDegrees, angleStepDegrees);
        impl_->templates.reserve(angles.size());
        for (double angle : angles) {
            AngleTemplate templ;
            templ.angle = angle;
            templ.levels.reserve(baseLevels.size());
            for (const auto& base : baseLevels) {
                TemplateLevel rotated = rotateFeatures(base, angle);
                if (rotated.features.size() < 8)
                    break;
                templ.levels.push_back(std::move(rotated));
            }
            if (!templ.levels.empty())
                impl_->templates.push_back(std::move(templ));
        }
        if (impl_->templates.empty()) {
            message =
                "The ROI contains too few shape features.";
            return false;
        }
        impl_->modelSize = roi.size();
        impl_->angleStepDegrees = angleStepDegrees;
        impl_->minContrast = minContrast;
        impl_->ignorePolarity = metric.find("ignore") != std::string::npos;
        // 空白间隔占多数的大跨度组合模板才走稀疏路径。
        // 忽略极性、完整矩形和重复孔洞模型继续使用完整反向结构检查，避免扩大近似适用范围。
        const cv::Mat selectedMask = mask8(selected, roi.size());
        const bool sparseSelection = cv::countNonZero(selectedMask) < selectedMask.total() * 0.6;
        if (numLevels == 0 && !impl_->ignorePolarity && sparseSelection
            && impl_->closedStructureCount == 0 && std::max(roi.width, roi.height) >= 768) {
            cv::Mat coarseGray = impl_->modelGray, coarseMask = mask8(selected, roi.size());
            int scale = 1;
            while (std::max(coarseGray.cols, coarseGray.rows) > 192) {
                cv::Mat nextGray, nextMask;
                cv::pyrDown(coarseGray, nextGray);
                cv::resize(coarseMask, nextMask, nextGray.size(), 0, 0, cv::INTER_NEAREST);
                coarseGray = std::move(nextGray); coarseMask = std::move(nextMask); scale *= 2;
            }
            auto coarse = std::make_unique<CShapeMatchCV>();
            std::string coarseMessage;
            if (coarse->createModel(coarseGray, 0, 0, coarseGray.cols, coarseGray.rows, 0,
                angleStartDegrees, angleEndDegrees, angleStepDegrees, metric,
                contrast == 0.0 ? 0.0 : contrast / std::sqrt(scale), minContrast / std::sqrt(scale),
                featureCount == 0 ? 128 : std::min(featureCount, 128), coarseMask, coarseMessage)) {
                // 候选层的闭环会因缩小合并，不以其数量淘汰原图仍可解释的形状。
                coarse->impl_->closedStructureCount = 0;
                impl_->coarseCenterOffset = cv::Point2d(roi.width * 0.5 - coarseGray.cols * scale * 0.5,
                    roi.height * 0.5 - coarseGray.rows * scale * 0.5);
                impl_->coarseScale = scale; impl_->coarseModel = std::move(coarse);
                impl_->preciseFeatures = precisionFeatures(impl_->modelGray, impl_->baseFeatures);
            }
        }
        (void)bitSimilarityTables(impl_->ignorePolarity);
        message = "OK";
        return true;
    } catch (const cv::Exception& exception) {
        clear();
        message = exception.what();
    } catch (const std::exception& exception) {
        clear();
        message = exception.what();
    }
    return false;
}

bool CShapeMatchCV::find(
    const cv::Mat& image, double angleStartDegrees,
    double angleEndDegrees, double minScore, int maxMatches,
    double maxOverlap, bool subPixel, int numLevels,
    int mode, std::string& message,
    std::vector<cv::Point2d>& centers,
    std::vector<double>& anglesDegrees,
    std::vector<double>& scores,
    std::vector<double>& widths,
    std::vector<double>& heights) const
{
    message.clear();
    centers.clear();
    anglesDegrees.clear();
    scores.clear();
    widths.clear();
    heights.clear();
    if (!hasModel()) {
        message = "Create the shape model before searching.";
        return false;
    }
    const cv::Mat gray = gray8(image);
    if (gray.empty()) {
        message =
            "The search image must be 8-bit gray, BGR, or BGRA.";
        return false;
    }
    if (!validSearch(angleStartDegrees, angleEndDegrees, minScore,
                     maxMatches, maxOverlap, numLevels)) {
        message = "The search parameters are invalid.";
        return false;
    }

    try {
        if (impl_->coarseModel && numLevels == 0) {
            cv::Mat reduced = gray;
            for (int scale = 1; scale < impl_->coarseScale; scale *= 2) { cv::Mat next; cv::pyrDown(reduced, next); reduced = std::move(next); }
            std::vector<cv::Point2d> foundCenters;
            std::vector<double> foundAngles, foundScores, foundWidths, foundHeights;
            // 多留少量候选供原图复核；大批量调用不隐式截断用户请求的数量。
            const int candidateCount = std::min(100, std::max(2, maxMatches * 2));
            impl_->coarseModel->find(reduced, angleStartDegrees, angleEndDegrees, minScore * 0.9,
                candidateCount, maxOverlap, true, 0, mode == 2 ? 2 : 1, message, foundCenters, foundAngles, foundScores, foundWidths, foundHeights);
            std::vector<MatchPose> poses;
            for (std::size_t index = 0; index < foundCenters.size(); ++index) {
                const double angle = foundAngles[index] * kPi / 180.0;
                const cv::Point2d offset(std::cos(angle) * impl_->coarseCenterOffset.x - std::sin(angle) * impl_->coarseCenterOffset.y,
                    std::sin(angle) * impl_->coarseCenterOffset.x + std::cos(angle) * impl_->coarseCenterOffset.y);
                const cv::Point2d initial = foundCenters[index] * impl_->coarseScale + offset;
                auto pose = refineSparsePose(gray, impl_->preciseFeatures,
                    ContinuousPose{initial.x, initial.y, foundAngles[index], 0.0}, impl_->coarseScale,
                    impl_->minContrast, impl_->ignorePolarity, std::min(angleStartDegrees, angleEndDegrees),
                    std::max(angleStartDegrees, angleEndDegrees), subPixel);
                const double score = 0.6 * foundScores[index] + 0.4 * pose.score;
                if (score < minScore || pose.score < minScore) continue;
                poses.push_back(MatchPose{cv::Point2d(pose.x, pose.y), pose.angle, score,
                    static_cast<double>(impl_->modelSize.width), static_cast<double>(impl_->modelSize.height)});
            }
            std::sort(poses.begin(), poses.end(), [](const MatchPose& a, const MatchPose& b) { return a.score > b.score; });
            std::vector<MatchPose> accepted;
            for (const auto& pose : poses) {
                bool suppressed = false;
                for (const auto& previous : accepted) {
                    if (rotatedOverlap(pose, previous) > maxOverlap || (cv::norm(pose.center - previous.center) < 0.25 * std::min(pose.width, pose.height)
                        && std::abs(std::remainder(pose.angleDegrees - previous.angleDegrees, 360.0)) <= 10.0)) { suppressed = true; break; }
                }
                if (suppressed) continue;
                accepted.push_back(pose);
                centers.push_back(pose.center); anglesDegrees.push_back(pose.angleDegrees); scores.push_back(pose.score);
                widths.push_back(pose.width); heights.push_back(pose.height);
                if (static_cast<int>(accepted.size()) >= maxMatches) break;
            }
            message = centers.empty() ? "No shape match met the score." : "OK";
            return !centers.empty();
        }
        const ShapeMatchProfile profile = profileFor(mode);
        const int levelCount = numLevels > 0
            ? std::min(numLevels, impl_->levels)
            : impl_->levels;
        std::vector<cv::Mat> pyramid(1, gray);
        pyramid.reserve(static_cast<std::size_t>(levelCount));
        for (int level = 1; level < levelCount; ++level) {
            cv::Mat next;
            cv::pyrDown(pyramid.back(), next);
            pyramid.push_back(std::move(next));
        }
        std::vector<ResponseLevel> responses(
            static_cast<std::size_t>(levelCount));
        // 每层内部按行并行，避免嵌套并行及层间工作量严重不均衡。
        for (int level = 0; level < levelCount; ++level) {
            responses[static_cast<std::size_t>(level)] = createResponseLevel(
                pyramid[static_cast<std::size_t>(level)],
                std::max(
                    1.0, impl_->minContrast
                        / std::pow(1.2, level)),
                impl_->ignorePolarity, level == levelCount - 1,
                level > 0);
        }
        const double low = std::min(
            angleStartDegrees, angleEndDegrees) - 1e-6;
        const double high = std::max(
            angleStartDegrees, angleEndDegrees) + 1e-6;
        const auto angleEligible = [&](std::size_t index) {
            const double angle = impl_->templates[index].angle;
            return angle >= low && angle <= high;
        };
        std::vector<std::size_t> eligibleAngles;
        eligibleAngles.reserve(impl_->templates.size());
        for (std::size_t index = 0;
             index < impl_->templates.size(); ++index) {
            if (angleEligible(index))
                eligibleAngles.push_back(index);
        }
        if (eligibleAngles.empty()) {
            message = "No shape match met the score.";
            return false;
        }
        // 角度抽样必须受当前层的模板半径约束。大跨度组合区域即使在粗层，
        // 跳过数度也会让边缘移动多个像素，导致正确姿态在进入精修前就丢失。
        // 在建模角度步长允许的范围内，将相邻抽样的最远点位移限制为两像素。
        // 间隔保持二的幂，保证各层角度网格一致，同时保留小模板的快速抽样预算。
        const auto angularStrideForLevel = [&](int level, std::size_t requested) {
            const double radius = std::hypot(impl_->modelSize.width, impl_->modelSize.height)
                * 0.5 / std::pow(2.0, level);
            const double maximumStep = 2.0 / std::max(1.0, radius) * 180.0 / CV_PI;
            const std::size_t spatialLimit = static_cast<std::size_t>(std::max(
                1.0, std::floor(maximumStep / impl_->angleStepDegrees)));
            std::size_t stride = requested;
            while (stride > spatialLimit && stride > 1) stride /= 2;
            return stride;
        };
        const std::size_t coarseAngleStride = angularStrideForLevel(levelCount - 1,
            profile.coarseAngleMultiplier * (std::size_t(1)
               << static_cast<unsigned>(std::min(3, levelCount - 1))));
        std::vector<std::size_t> coarseAngles;
        for (std::size_t position = 0;
             position < eligibleAngles.size();
             position += coarseAngleStride) {
            coarseAngles.push_back(eligibleAngles[position]);
        }
        if (coarseAngles.back() != eligibleAngles.back())
            coarseAngles.push_back(eligibleAngles.back());

        const int peaksPerAngle = std::min(
            profile.maximumPeaksPerAngle,
            std::max(2, maxMatches));
        const double coarseMinimum = std::min(
            0.30, std::max(0.12, minScore * 0.42));
        std::vector<std::vector<Candidate>> angleCandidates(
            coarseAngles.size());
        const int coarseThreads = parallelThreadCount(coarseAngles.size());
#ifdef _OPENMP
#pragma omp parallel for if(coarseAngles.size() >= 24) num_threads(coarseThreads) schedule(static)
#endif
        for (std::ptrdiff_t anglePosition = 0;
             anglePosition < static_cast<std::ptrdiff_t>(coarseAngles.size());
             ++anglePosition) {
            const std::size_t templateIndex = coarseAngles[
                static_cast<std::size_t>(anglePosition)];
            std::vector<Candidate>& localCandidates = angleCandidates[
                static_cast<std::size_t>(anglePosition)];
            const AngleTemplate& angleTemplate =
                impl_->templates[templateIndex];
            const int level = std::min(
                levelCount - 1,
                static_cast<int>(angleTemplate.levels.size()) - 1);
            const TemplateLevel& templ =
                angleTemplate.levels[static_cast<std::size_t>(level)];
            const cv::Mat& searchMap =
                responses[static_cast<std::size_t>(level)].maps[0];
            if (templ.size.width > searchMap.cols
                || templ.size.height > searchMap.rows) {
                continue;
            }
            const int columns =
                searchMap.cols - templ.size.width + 1;
            const int rows =
                searchMap.rows - templ.size.height + 1;
            cv::Mat responseScores = cv::Mat::zeros(rows, columns, CV_32F);
            std::size_t usedFeatureCount = 0;
            for (std::size_t featureIndex = 0;
                 featureIndex < templ.features.size();
                 featureIndex += profile.coarseFeatureStride) {
                const Feature& feature = templ.features[featureIndex];
                cv::accumulate(
                    responses[static_cast<std::size_t>(level)]
                        .maps[feature.label](
                            cv::Rect(
                                feature.x, feature.y,
                                columns, rows)),
                    responseScores);
                ++usedFeatureCount;
            }
            const double scoreScale =
                1.0 / static_cast<double>(
                    kMaximumDirectionScore * usedFeatureCount);
            for (int peak = 0; peak < peaksPerAngle; ++peak) {
                double maximum = 0.0;
                cv::Point location;
                cv::minMaxLoc(
                    responseScores, nullptr, &maximum, nullptr, &location);
                const double normalized = maximum * scoreScale;
                if (!std::isfinite(normalized)
                    || normalized < coarseMinimum) {
                    break;
                }
                localCandidates.push_back({
                    templateIndex, level, location,
                    static_cast<float>(normalized)});
                const int radiusX =
                    std::max(2, templ.size.width / 4);
                const int radiusY =
                    std::max(2, templ.size.height / 4);
                const int x = std::max(0, location.x - radiusX);
                const int y = std::max(0, location.y - radiusY);
                responseScores(cv::Rect(
                    x, y,
                    std::min(responseScores.cols - x, radiusX * 2 + 1),
                    std::min(responseScores.rows - y, radiusY * 2 + 1)))
                    .setTo(-1.0F);
            }
        }
        std::vector<Candidate> candidates;
        candidates.reserve(coarseAngles.size()
            * static_cast<std::size_t>(peaksPerAngle));
        for (const std::vector<Candidate>& localCandidates : angleCandidates) {
            candidates.insert(
                candidates.end(),
                localCandidates.begin(), localCandidates.end());
        }
        if (candidates.empty()) {
            message = "No shape match met the score.";
            return false;
        }

        const std::size_t coarseLimit = std::min<std::size_t>(
            profile.candidateLimit, std::max<std::size_t>(
                profile.coarseCandidateMinimum,
                static_cast<std::size_t>(maxMatches)
                    + profile.coarseCandidatePadding));
        trimCandidates(candidates, coarseLimit, impl_->templates);
        // 不同角度在旋转取整去重后可能具有不同的有效层数，因此不能
        // 只依据最高分候选的层级决定整个集合是否已经回到原图层。
        while (std::any_of(
            candidates.begin(), candidates.end(),
            [](const Candidate& candidate) { return candidate.level > 0; })) {
            std::vector<Candidate> refined;
            refined.reserve(candidates.size());
            for (const Candidate& candidate : candidates) {
                if (candidate.level <= 0) {
                    refined.push_back(candidate);
                    continue;
                }
                const int nextLevel = candidate.level - 1;
                const TemplateLevel& previous =
                    impl_->templates[candidate.templateIndex]
                        .levels[static_cast<std::size_t>(candidate.level)];
                const cv::Point2d previousCenter(
                    candidate.location.x + previous.reference.x,
                    candidate.location.y + previous.reference.y);
                const cv::Point2d predictedCenter =
                    previousCenter * 2.0;
                Candidate best;
                best.level = nextLevel;
                best.score = -1.0F;
                // 逐层角度精修也采用空间约束，防止大模板直到原图层才补到正确角度。
                const std::size_t angleStride = angularStrideForLevel(nextLevel,
                    std::size_t(1) << static_cast<unsigned>(nextLevel));
                const std::size_t firstAngle =
                    candidate.templateIndex >= angleStride
                    ? candidate.templateIndex - angleStride
                    : candidate.templateIndex;
                const std::size_t lastAngle = std::min(
                    candidate.templateIndex + angleStride,
                    impl_->templates.size() - 1);
                for (std::size_t templateIndex = firstAngle;
                     templateIndex <= lastAngle;
                     templateIndex += angleStride) {
                    if (!angleEligible(templateIndex))
                        continue;
                    const AngleTemplate& angleTemplate =
                        impl_->templates[templateIndex];
                    if (static_cast<int>(angleTemplate.levels.size())
                        <= nextLevel) {
                        continue;
                    }
                    const TemplateLevel& templ =
                        angleTemplate.levels[
                            static_cast<std::size_t>(nextLevel)];
                    const cv::Mat& searchMap =
                        responses[static_cast<std::size_t>(nextLevel)]
                            .bits;
                    const int maximumX =
                        searchMap.cols - templ.size.width;
                    const int maximumY =
                        searchMap.rows - templ.size.height;
                    const int predictedX = cvRound(
                        predictedCenter.x - templ.reference.x);
                    const int predictedY = cvRound(
                        predictedCenter.y - templ.reference.y);
                    const int firstX =
                        std::max(0, predictedX - 2);
                    const int firstY =
                        std::max(0, predictedY - 2);
                    const int lastX =
                        std::min(maximumX, predictedX + 2);
                    const int lastY =
                        std::min(maximumY, predictedY + 2);
                    for (int y = firstY; y <= lastY; ++y) {
                        for (int x = firstX; x <= lastX; ++x) {
                            const float score = pointScore(
                                responses[
                                    static_cast<std::size_t>(nextLevel)],
                                templ, x, y);
                            if (score > best.score) {
                                best.templateIndex = templateIndex;
                                best.location = cv::Point(x, y);
                                best.score = score;
                            }
                        }
                    }
                }
                if (best.score >= 0.0F)
                    refined.push_back(best);
            }
            candidates.swap(refined);
            const std::size_t fineLimit = std::min<std::size_t>(
                profile.candidateLimit, std::max<std::size_t>(
                    profile.fineCandidateMinimum,
                    static_cast<std::size_t>(maxMatches)
                        + profile.fineCandidatePadding));
            trimCandidates(candidates, fineLimit, impl_->templates);
        }
        std::vector<MatchPose> poses;
        poses.reserve(candidates.size());
        const ResponseLevel& finalResponse = responses.front();
        // 一次提取并按行压缩搜索边缘，候选校验仅遍历局部边缘，不扫描整块 ROI。
        std::vector<std::vector<int>> structureRows(static_cast<std::size_t>(gray.rows));
        cv::Mat searchStructureDistance;
        // 每个元素为中心 X、中心 Y、面积；面积用于局部透视尺度过滤。
        std::vector<cv::Vec3d> searchClosedCenters;
        if (impl_->structureEdgeCount >= 8) {
            cv::Mat edges;
            cv::Canny(finalResponse.gradientX, finalResponse.gradientY, edges,
                impl_->structureContrast * 0.5, impl_->structureContrast, true);
            const double recallThreshold = std::max(impl_->minContrast, impl_->structureContrast * 0.15);
            cv::Mat recallBackground(gray.size(), CV_8U);
            const double recallSquared = recallThreshold * recallThreshold;
#ifdef _OPENMP
#pragma omp parallel for if(gray.total() >= 65536) num_threads(parallelThreadCount(gray.rows)) schedule(static)
#endif
            for (int y = 0; y < gray.rows; ++y) {
                const auto* gx = finalResponse.gradientX.ptr<short>(y);
                const auto* gy = finalResponse.gradientY.ptr<short>(y);
                auto* row = recallBackground.ptr<unsigned char>(y);
                for (int x = 0; x < gray.cols; ++x)
                    row[x] = gx[x] * gx[x] + gy[x] * gy[x] >= recallSquared ? 0 : 255;
            }
            // 缺失检查只需要低阈值梯度支持，复用导数而不再运行一次全图 Canny。
            // 多余结构与封闭轮廓仍严格使用强 Canny，弱纹理不参与拓扑计数。
            cv::distanceTransform(recallBackground, searchStructureDistance, cv::DIST_L2, 3);
            if (impl_->closedStructureCount >= 2) {
                cv::Mat closedEdges;
                cv::morphologyEx(edges, closedEdges, cv::MORPH_CLOSE, cv::Mat::ones(3, 3, CV_8U));
                std::vector<std::vector<cv::Point>> contours;
                cv::findContours(closedEdges, contours, cv::RETR_LIST, cv::CHAIN_APPROX_SIMPLE);
                for (const auto& contour : contours) {
                    const double area = cv::contourArea(contour, true);
                    if (area < impl_->closedMinimumArea || area > impl_->closedMaximumArea)
                        continue;
                    const cv::Moments moments = cv::moments(contour);
                    if (moments.m00 != 0)
                        searchClosedCenters.emplace_back(moments.m10 / moments.m00, moments.m01 / moments.m00, area);
                }
            }
            for (int y = 0; y < gray.rows; ++y) {
                const auto* row = edges.ptr<unsigned char>(y);
                auto& positions = structureRows[static_cast<std::size_t>(y)];
                for (int x = 0; x < gray.cols; ++x) {
                    if (row[x])
                        positions.push_back(x);
                }
            }
        }
        const auto structureScore = [&](const cv::Point2d& center, double angle, double toleranceMargin) {
            if (impl_->structureEdgeCount < 8)
                return 1.0;
            const double radians = angle * kPi / 180.0;
            const double cosine = std::cos(radians), sine = std::sin(radians);
            const double halfWidth = impl_->modelSize.width * 0.5;
            const double halfHeight = impl_->modelSize.height * 0.5;
            const int radiusX = cvCeil(std::abs(cosine) * halfWidth + std::abs(sine) * halfHeight);
            const int radiusY = cvCeil(std::abs(sine) * halfWidth + std::abs(cosine) * halfHeight);
            const int left = std::max(0, cvFloor(center.x) - radiusX);
            const int right = std::min(gray.cols - 1, cvCeil(center.x) + radiusX);
            int unexpected = 0;
            for (int y = std::max(0, cvFloor(center.y) - radiusY);
                 y <= std::min(gray.rows - 1, cvCeil(center.y) + radiusY); ++y) {
                const auto& positions = structureRows[static_cast<std::size_t>(y)];
                for (auto it = std::lower_bound(positions.begin(), positions.end(), left);
                     it != positions.end() && *it <= right; ++it) {
                    const double dx = *it - center.x, dy = y - center.y;
                    const int mx = cvRound(cosine * dx + sine * dy + halfWidth);
                    const int my = cvRound(-sine * dx + cosine * dy + halfHeight);
                    if (mx < 0 || my < 0 || mx >= impl_->modelSize.width || my >= impl_->modelSize.height
                        || !impl_->structureMask.at<unsigned char>(my, mx))
                        continue;
                    if (impl_->structureDistance.at<float>(my, mx) > impl_->structureTolerance + toleranceMargin)
                        ++unexpected;
                }
            }
            // 同时检查缺失轮廓，避免五点模板仅凭外框把三点判成高分。
            // 正向验证多留半个容差，允许真实物体轻微透视和缩放；大块缺失仍扣分。
            int missing = 0, samples = 0;
            const std::size_t stride = std::max<std::size_t>(1, impl_->structurePoints.size() / 512);
            for (std::size_t index = 0; index < impl_->structurePoints.size(); index += stride) {
                const cv::Point& point = impl_->structurePoints[index];
                const double mx = point.x - halfWidth, my = point.y - halfHeight;
                const int x = cvRound(center.x + cosine * mx - sine * my);
                const int y = cvRound(center.y + sine * mx + cosine * my);
                ++samples;
                if (x < 0 || y < 0 || x >= gray.cols || y >= gray.rows
                    || searchStructureDistance.at<float>(y, x) > impl_->structureTolerance * 1.5 + toleranceMargin)
                    ++missing;
            }
            const double missingEdges = static_cast<double>(impl_->structureEdgeCount) * missing / std::max(1, samples);
            double topology = 1.0;
            if (impl_->closedStructureCount >= 2) {
                std::vector<cv::Vec3d> localLoops;
                for (const auto& loop : searchClosedCenters) {
                    const double dx = loop[0] - center.x, dy = loop[1] - center.y;
                    const int mx = cvRound(cosine * dx + sine * dy + halfWidth);
                    const int my = cvRound(-sine * dx + cosine * dy + halfHeight);
                    if (mx >= 0 && my >= 0 && mx < impl_->modelSize.width && my < impl_->modelSize.height
                        && impl_->structureMask.at<unsigned char>(my, mx))
                        localLoops.emplace_back(cosine * dx + sine * dy + halfWidth,
                            -sine * dx + cosine * dy + halfHeight, loop[2]);
                }
                double areaFloor = 0.0;
                for (const auto& loop : localLoops)
                    areaFloor = std::max(areaFloor, loop[2] * 0.4);
                localLoops.erase(std::remove_if(localLoops.begin(), localLoops.end(),
                    [&](const auto& loop) { return loop[2] < areaFloor; }), localLoops.end());
                const int found = static_cast<int>(localLoops.size());
                const double ratio = static_cast<double>(std::min(found, impl_->closedStructureCount))
                    / std::max(found, impl_->closedStructureCount);
                // 最大二分匹配保证闭环一对一对应，不能用同一孔洞解释多个模型闭环。
                const double tolerance = std::max(8.0, std::min(halfWidth, halfHeight) * 0.24) + toleranceMargin;
                std::vector<int> owners(localLoops.size(), -1);
                const auto assignLoop = [&](auto&& self, int modelIndex, std::vector<unsigned char>& visited) -> bool {
                    const auto& modelPoint = impl_->closedStructureCenters[static_cast<std::size_t>(modelIndex)];
                    for (std::size_t index = 0; index < localLoops.size(); ++index) {
                        const double dx = localLoops[index][0] - modelPoint.x, dy = localLoops[index][1] - modelPoint.y;
                        if (visited[index] || dx * dx + dy * dy > tolerance * tolerance)
                            continue;
                        visited[index] = 1;
                        if (owners[index] < 0 || self(self, owners[index], visited)) {
                            owners[index] = modelIndex;
                            return true;
                        }
                    }
                    return false;
                };
                int paired = 0;
                for (int index = 0; index < impl_->closedStructureCount; ++index) {
                    std::vector<unsigned char> visited(localLoops.size(), 0);
                    paired += assignLoop(assignLoop, index, visited) ? 1 : 0;
                }
                const double coverage = static_cast<double>(paired) / impl_->closedStructureCount;
                topology = ratio * ratio * coverage * coverage;
            }
            return topology * static_cast<double>(impl_->structureEdgeCount)
                / (impl_->structureEdgeCount + 1.25 * (unexpected + missingEdges));
        };
#ifdef _OPENMP
#pragma omp parallel for \
    num_threads(parallelThreadCount(candidates.size())) \
    if(candidates.size() > 1)
#endif
        for (std::ptrdiff_t candidateNumber = 0;
             candidateNumber < static_cast<std::ptrdiff_t>(candidates.size());
             ++candidateNumber) {
            const Candidate& candidate = candidates[
                static_cast<std::size_t>(candidateNumber)];
            const TemplateLevel& candidateTemplate =
                impl_->templates[candidate.templateIndex].levels.front();
            const cv::Point2d candidateCenter(
                candidate.location.x + candidateTemplate.reference.x,
                candidate.location.y + candidateTemplate.reference.y);
            // 大容差预筛放在角度/位置穷举精修之前，减少无效候选。
            if (structureScore(candidateCenter, impl_->templates[candidate.templateIndex].angle, 8.0)
                < minScore * 0.8)
                continue;
            std::size_t exactTemplateIndex = candidate.templateIndex;
            cv::Point exactLocation = candidate.location;
            float exactScore = -1.0F;
            const double finalAngleWindow = levelCount >= 3
                ? profile.finalAngleWindowPyramid
                : profile.finalAngleWindowFine;
            const int finalAngleRadius = std::clamp(
                cvCeil(finalAngleWindow
                    / impl_->angleStepDegrees), 1, 12);
            const int finalPositionRadius = levelCount >= 3
                ? profile.finalPositionRadius : 1;
            const bool fullAngleSearch = high - low >= 359.0;
            for (int angleOffset = -finalAngleRadius;
                 angleOffset <= finalAngleRadius; ++angleOffset) {
                std::ptrdiff_t signedTemplateIndex =
                    static_cast<std::ptrdiff_t>(candidate.templateIndex)
                    + angleOffset;
                if (fullAngleSearch && impl_->templates.size() > 1) {
                    const std::ptrdiff_t uniqueAngleCount =
                        static_cast<std::ptrdiff_t>(
                            impl_->templates.size() - 1);
                    signedTemplateIndex %= uniqueAngleCount;
                    if (signedTemplateIndex < 0)
                        signedTemplateIndex += uniqueAngleCount;
                } else if (signedTemplateIndex < 0
                    || signedTemplateIndex >= static_cast<std::ptrdiff_t>(
                        impl_->templates.size())) {
                    continue;
                }
                const std::size_t templateIndex =
                    static_cast<std::size_t>(signedTemplateIndex);
                if (!angleEligible(templateIndex))
                    continue;
                const TemplateLevel& angleTemplate =
                    impl_->templates[templateIndex].levels.front();
                const int predictedX = cvRound(
                    candidateCenter.x - angleTemplate.reference.x);
                const int predictedY = cvRound(
                    candidateCenter.y - angleTemplate.reference.y);
                for (int offsetY = -finalPositionRadius;
                     offsetY <= finalPositionRadius; ++offsetY) {
                    for (int offsetX = -finalPositionRadius;
                         offsetX <= finalPositionRadius; ++offsetX) {
                        const int x = predictedX + offsetX;
                        const int y = predictedY + offsetY;
                        if (x < 0 || y < 0
                            || x + angleTemplate.size.width
                                > finalResponse.bits.cols
                            || y + angleTemplate.size.height
                                > finalResponse.bits.rows) {
                            continue;
                        }
                        const float score = pointScore(
                            finalResponse, angleTemplate, x, y, true);
                        if (score > exactScore) {
                            exactTemplateIndex = templateIndex;
                            exactScore = score;
                            exactLocation = cv::Point(x, y);
                        }
                    }
                }
            }
            const TemplateLevel& templ =
                impl_->templates[exactTemplateIndex].levels.front();
            if (exactScore < minScore * kStrictScoreFraction)
                continue;
            const double spatialScore = spatialPointScore(
                finalResponse, templ, exactLocation.x, exactLocation.y);
            // Rotation rasterization can move a valid contour by one pixel.
            // Keep exact support dominant enough to reject clutter while giving
            // adjacent contour support a bounded contribution to model quality.
            const double calibratedScore = std::clamp(
                kExactScoreWeight * static_cast<double>(exactScore)
                    + kSpatialScoreWeight * spatialScore,
                0.0, 1.0);
            if (calibratedScore < minScore)
                continue;
            // 明显多出内部结构的候选提前退出，不再执行昂贵的亚像素精修。
            // 粗筛额外放宽2像素且留10%评分余量，最终姿态仍需严格复核。
            if (calibratedScore * structureScore(cv::Point2d(
                    exactLocation.x + templ.reference.x, exactLocation.y + templ.reference.y),
                    impl_->templates[exactTemplateIndex].angle, 2.0) < minScore * 0.9)
                continue;
            const double centerScore = pointScore(
                finalResponse, templ,
                exactLocation.x, exactLocation.y);
            cv::Point2d refinedLocation(exactLocation);
            if (subPixel && profile.enableParabolicRefinement) {
                const int x = exactLocation.x;
                const int y = exactLocation.y;
                if (x > 0 && x + templ.size.width
                    < finalResponse.bits.cols) {
                    const double before =
                        pointScore(finalResponse, templ, x - 1, y);
                    const double after =
                        pointScore(finalResponse, templ, x + 1, y);
                    refinedLocation.x += parabolicOffset(
                        before, centerScore, after);
                }
                if (y > 0 && y + templ.size.height
                    < finalResponse.bits.rows) {
                    const double before =
                        pointScore(finalResponse, templ, x, y - 1);
                    const double after =
                        pointScore(finalResponse, templ, x, y + 1);
                    refinedLocation.y += parabolicOffset(
                        before, centerScore, after);
                }
            }

            double refinedAngle =
                impl_->templates[exactTemplateIndex].angle;
            if (subPixel && profile.enableParabolicRefinement
                && exactTemplateIndex > 0
                && exactTemplateIndex + 1
                    < impl_->templates.size()
                && angleEligible(exactTemplateIndex - 1)
                && angleEligible(exactTemplateIndex + 1)) {
                const cv::Point2d modelCenter(
                    exactLocation.x + templ.reference.x,
                    exactLocation.y + templ.reference.y);
                const TemplateLevel& beforeTemplate =
                    impl_->templates[exactTemplateIndex - 1]
                        .levels.front();
                const TemplateLevel& afterTemplate =
                    impl_->templates[exactTemplateIndex + 1]
                        .levels.front();
                const int beforeX = cvRound(
                    modelCenter.x - beforeTemplate.reference.x);
                const int beforeY = cvRound(
                    modelCenter.y - beforeTemplate.reference.y);
                const int afterX = cvRound(
                    modelCenter.x - afterTemplate.reference.x);
                const int afterY = cvRound(
                    modelCenter.y - afterTemplate.reference.y);
                const auto validLocation = [&](const TemplateLevel& item,
                                               int x, int y) {
                    return x >= 0 && y >= 0
                        && x + item.size.width
                            <= finalResponse.bits.cols
                        && y + item.size.height
                            <= finalResponse.bits.rows;
                };
                if (validLocation(
                        beforeTemplate, beforeX, beforeY)
                    && validLocation(
                        afterTemplate, afterX, afterY)) {
                    const double before = pointScore(
                        finalResponse, beforeTemplate,
                        beforeX, beforeY);
                    const double after = pointScore(
                        finalResponse, afterTemplate,
                        afterX, afterY);
                    const double offset = parabolicOffset(
                        before, centerScore, after);
                    const double angleStep =
                        (impl_->templates[exactTemplateIndex + 1].angle
                         - impl_->templates[exactTemplateIndex - 1].angle)
                        * 0.5;
                    refinedAngle += offset * angleStep;
                }
            }

            MatchPose pose;
            cv::Point2d poseCenter(
                refinedLocation.x + templ.reference.x,
                refinedLocation.y + templ.reference.y);
            const bool fixedSearchAngle = std::abs(
                angleEndDegrees - angleStartDegrees) <= 1e-9;
            if (subPixel && profile.enableContourRefinement
                && !fixedSearchAngle) {
                const ContinuousPose continuous = refineContinuousPose(
                    finalResponse, impl_->baseFeatures,
                    poseCenter, refinedAngle, low, high,
                    std::max(1.0, impl_->minContrast),
                    impl_->ignorePolarity);
                poseCenter = cv::Point2d(continuous.x, continuous.y);
                refinedAngle = continuous.angle;
                if (profile.enableIntensityRefinement
                    && impl_->intensityRefinementAllowed
                    && !impl_->ignorePolarity
                    && impl_->modelGray.total()
                        <= kIntensityRefinementMaxPixels) {
                    ContinuousPose intensity{
                        poseCenter.x, poseCenter.y, refinedAngle, 0.0};
                    if (refineIntensityPose(
                            impl_->modelGray, gray, intensity)) {
                        poseCenter = cv::Point2d(
                            intensity.x, intensity.y);
                        refinedAngle = intensity.angle;
                    }
                }
            } else if (fixedSearchAngle) {
                refinedAngle = angleStartDegrees;
            }
            const double originRadians = refinedAngle * kPi / 180.0;
            const double originCosine = std::cos(originRadians);
            const double originSine = std::sin(originRadians);
            poseCenter.x += 0.5
                * (1.0 - originCosine + originSine);
            poseCenter.y += 0.5
                * (1.0 - originSine - originCosine);
            pose.center = poseCenter;
            pose.angleDegrees = refinedAngle;
            pose.score = calibratedScore * structureScore(poseCenter, refinedAngle, 0.0);
            if (pose.score < minScore)
                continue;
            pose.width = static_cast<double>(impl_->modelSize.width);
            pose.height = static_cast<double>(impl_->modelSize.height);
#ifdef _OPENMP
#pragma omp critical(shape_match_pose_output)
#endif
            poses.push_back(pose);
        }
        std::sort(poses.begin(), poses.end(),
                  [](const MatchPose& first, const MatchPose& second) {
                      return first.score > second.score;
                  });
        std::vector<MatchPose> acceptedPoses;
        acceptedPoses.reserve(static_cast<std::size_t>(maxMatches));
        for (const MatchPose& pose : poses) {
            bool suppressed = false;
            for (const MatchPose& accepted : acceptedPoses) {
                const double centerDistance =
                    cv::norm(pose.center - accepted.center);
                const double angleDistance = std::abs(std::remainder(
                    pose.angleDegrees - accepted.angleDegrees, 360.0));
                const bool samePhysicalTarget =
                    centerDistance <= 0.25
                        * std::min(pose.width, pose.height)
                    && angleDistance <= 10.0;
                if (samePhysicalTarget
                    || rotatedOverlap(pose, accepted)
                        > maxOverlap) {
                    suppressed = true;
                    break;
                }
            }
            if (!suppressed)
                acceptedPoses.push_back(pose);
            if (static_cast<int>(acceptedPoses.size())
                == maxMatches) {
                break;
            }
        }
        centers.reserve(acceptedPoses.size());
        anglesDegrees.reserve(acceptedPoses.size());
        scores.reserve(acceptedPoses.size());
        widths.reserve(acceptedPoses.size());
        heights.reserve(acceptedPoses.size());
        for (const MatchPose& pose : acceptedPoses) {
            centers.push_back(pose.center);
            anglesDegrees.push_back(pose.angleDegrees);
            scores.push_back(pose.score);
            widths.push_back(pose.width);
            heights.push_back(pose.height);
        }
        message = acceptedPoses.empty()
            ? "No shape match met the score." : "OK";
        return !acceptedPoses.empty();
    } catch (const cv::Exception& exception) {
        message = exception.what();
    } catch (const std::exception& exception) {
        message = exception.what();
    }
    return false;
}

void CShapeMatchCV::clear()
{
    if (!impl_)
        return;
    impl_->templates.clear();
    impl_->coarseModel.reset(); impl_->coarseScale = 1; impl_->preciseFeatures.clear();
    impl_->contourEdges.release();
    impl_->contourPaths.clear();
    impl_->structureEdges.release();
    impl_->structureMask.release();
    impl_->structureDistance.release();
    impl_->structureEdgeCount = 0;
    impl_->structurePoints.clear();
    impl_->closedStructureCount = 0;
    impl_->closedStructureCenters.clear();
    impl_->closedMinimumArea = 0.0;
    impl_->closedMaximumArea = 0.0;
    impl_->baseFeatures.clear();
    impl_->editableLevels.clear();
    impl_->modelGray.release();
    impl_->intensityRefinementAllowed = false;
    impl_->modelSize = {};
    impl_->levels = 1;
}

bool CShapeMatchCV::hasModel() const
{
    return impl_ && !impl_->templates.empty();
}

double CShapeMatchCV::modelContrast() const
{
    return hasModel() ? impl_->modelContrast : 0.0;
}

// 导出实际匹配特征，而不是对显示图另外执行一套边缘检测。
std::vector<cv::Point2f> CShapeMatchCV::modelFeatures() const
{
    std::vector<cv::Point2f> points;
    if (!hasModel())
        return points;
    points.reserve(impl_->baseFeatures.size());
    for (const BaseFeature& feature : impl_->baseFeatures)
        points.emplace_back(feature.x + impl_->modelSize.width * 0.5F,
                            feature.y + impl_->modelSize.height * 0.5F);
    return points;
}

// 使用临时缓存完成整个编辑，全部验证成功后再提交，确保失败不会损坏原模板。
const std::vector<std::vector<cv::Point>>& CShapeMatchCV::modelContours() const
{
    return impl_->contourPaths;
}

// 完整轮廓和搜索特征一起事务式提交，允许删除未被稀疏采样选中的真实细节。
bool CShapeMatchCV::eraseFeatures(const cv::Mat& eraseMask, std::string& message)
{
    if (!hasModel() || eraseMask.type() != CV_8UC1
        || eraseMask.size() != impl_->modelSize) {
        message = "The erase mask must match an existing model ROI.";
        return false;
    }
    auto edited = impl_->editableLevels;
    cv::Size levelSize = impl_->modelSize;
    for (std::size_t level = 0; level < edited.size(); ++level) {
        auto& features = edited[level];
        const double scale = std::pow(2.0, static_cast<double>(level));
        features.erase(std::remove_if(features.begin(), features.end(),
            [&](const BaseFeature& feature) {
                // pyrDown 的采样中心按 2^level 映射；奇数 ROI 用本层实际尺寸还原。
                const int x = std::clamp(cvRound(
                    (feature.x + levelSize.width * 0.5) * scale), 0, eraseMask.cols - 1);
                const int y = std::clamp(cvRound(
                    (feature.y + levelSize.height * 0.5) * scale), 0, eraseMask.rows - 1);
                return eraseMask.at<unsigned char>(y, x) != 0;
            }), features.end());
        if (features.size() < 8) {
            if (level == 0) {
                message = "At least 8 model features must remain. The model was not changed.";
                return false;
            }
            edited.resize(level);
            break;
        }
        levelSize = cv::Size((levelSize.width + 1) / 2, (levelSize.height + 1) / 2);
    }
    std::vector<AngleTemplate> templates;
    templates.reserve(impl_->templates.size());
    for (const AngleTemplate& previous : impl_->templates) {
        AngleTemplate rebuilt;
        rebuilt.angle = previous.angle;
        for (const auto& base : edited) {
            TemplateLevel rotated = rotateFeatures(base, rebuilt.angle);
            if (rotated.features.size() < 8)
                break;
            rebuilt.levels.push_back(std::move(rotated));
        }
        if (rebuilt.levels.empty()) {
            message = "Too few features remain at a model angle. The model was not changed.";
            return false;
        }
        templates.push_back(std::move(rebuilt));
    }
    // 反向结构校验也必须忽略用户涂抹的区域，不能把已删除的细节重新纳入评分。
    cv::Mat editedStructureMask = impl_->structureMask.clone();
    cv::Mat ignored;
    const int ignoreRadius = cvCeil(impl_->structureTolerance);
    cv::dilate(eraseMask, ignored,
        cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(ignoreRadius * 2 + 1, ignoreRadius * 2 + 1)));
    editedStructureMask.setTo(0, ignored);
    const int editedStructureCount = cv::countNonZero(impl_->structureEdges & editedStructureMask);
    std::vector<cv::Point> editedStructurePoints;
    cv::findNonZero(impl_->structureEdges & editedStructureMask, editedStructurePoints);
    auto baseFeatures = edited.front();
    cv::Mat editedContourEdges = impl_->contourEdges.clone();
    editedContourEdges.setTo(0, eraseMask);
    std::vector<std::vector<cv::Point>> editedContourPaths;
    cv::findContours(editedContourEdges, editedContourPaths, cv::RETR_LIST, cv::CHAIN_APPROX_NONE);
    impl_->baseFeatures.swap(baseFeatures);
    impl_->contourEdges = std::move(editedContourEdges);
    impl_->contourPaths.swap(editedContourPaths);
    impl_->editableLevels.swap(edited);
    impl_->templates.swap(templates);
    impl_->structureMask = std::move(editedStructureMask);
    impl_->structureEdgeCount = editedStructureCount;
    impl_->structurePoints.swap(editedStructurePoints);
    // 用户主动删去轮廓后，不再要求原始完整闭环数量；局部边缘校验仍遵守删除遮罩。
    impl_->closedStructureCount = 0;
    impl_->closedStructureCenters.clear();
    impl_->levels = static_cast<int>(impl_->editableLevels.size());
    // 灰度 ECC 使用整块模板，编辑后禁用，防止删除的细节通过灰度精修重新影响姿态。
    impl_->intensityRefinementAllowed = false;
    if (impl_->coarseModel) {
        // 降采样足迹内只要有擦除像素就屏蔽候选特征，细小笔画不能被最近邻缩放漏掉。
        cv::Mat expandedErase;
        cv::dilate(eraseMask, expandedErase, cv::getStructuringElement(cv::MORPH_RECT,
            cv::Size(impl_->coarseScale * 2 + 1, impl_->coarseScale * 2 + 1)));
        cv::Mat coarseErase(impl_->coarseModel->impl_->modelSize, CV_8U);
        for (int y = 0; y < coarseErase.rows; ++y) for (int x = 0; x < coarseErase.cols; ++x)
            coarseErase.at<unsigned char>(y, x) = expandedErase.at<unsigned char>(
                std::min(y * impl_->coarseScale, expandedErase.rows - 1), std::min(x * impl_->coarseScale, expandedErase.cols - 1));
        std::string coarseMessage;
        if (!impl_->coarseModel->eraseFeatures(coarseErase, coarseMessage)) impl_->coarseModel.reset();
        impl_->preciseFeatures = precisionFeatures(impl_->modelGray, impl_->baseFeatures);
    }
    message = "OK";
    return true;
}
