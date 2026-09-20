#include "ShapeMatchNative.h"
#include <opencv2/imgproc.hpp>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <iostream>
#include <vector>

namespace {

/// <summary>在灰度测试图中绘制实心矩形，并自动裁剪到图像范围。</summary>
void fillRectangle(
    std::vector<std::uint8_t>& image,
    int width,
    int height,
    int left,
    int top,
    int rectangleWidth,
    int rectangleHeight,
    std::uint8_t value)
{
    const int x0 = std::clamp(left, 0, width);
    const int y0 = std::clamp(top, 0, height);
    const int x1 = std::clamp(left + rectangleWidth, 0, width);
    const int y1 = std::clamp(top + rectangleHeight, 0, height);
    for (int y = y0; y < y1; ++y) {
        for (int x = x0; x < x1; ++x)
            image[static_cast<std::size_t>(y * width + x)] = value;
    }
}

/// <summary>生成带有非对称轮廓的模板，避免镜像或周期性结构产生歧义。</summary>
std::vector<std::uint8_t> createTemplateImage(int width, int height)
{
    std::vector<std::uint8_t> image(
        static_cast<std::size_t>(width * height), 20U);

    // 外框、右侧凸台和内部缺口共同形成方向明确的非对称轮廓。
    fillRectangle(image, width, height, 14, 14, 62, 5, 230U);
    fillRectangle(image, width, height, 14, 14, 5, 58, 230U);
    fillRectangle(image, width, height, 14, 67, 45, 5, 230U);
    fillRectangle(image, width, height, 54, 41, 5, 31, 230U);
    fillRectangle(image, width, height, 54, 37, 25, 5, 230U);
    fillRectangle(image, width, height, 74, 37, 5, 18, 230U);
    fillRectangle(image, width, height, 30, 30, 16, 12, 150U);
    fillRectangle(image, width, height, 34, 46, 8, 13, 245U);
    return image;
}

/// <summary>把模板像素复制到更大的搜索图中。</summary>
void copyPatch(
    const std::vector<std::uint8_t>& source,
    int sourceWidth,
    int sourceHeight,
    std::vector<std::uint8_t>& destination,
    int destinationWidth,
    int destinationX,
    int destinationY)
{
    for (int y = 0; y < sourceHeight; ++y) {
        const auto sourceOffset = static_cast<std::size_t>(y * sourceWidth);
        const auto destinationOffset = static_cast<std::size_t>(
            (destinationY + y) * destinationWidth + destinationX);
        std::copy_n(
            source.begin() + static_cast<std::ptrdiff_t>(sourceOffset),
            sourceWidth,
            destination.begin() + static_cast<std::ptrdiff_t>(destinationOffset));
    }
}

/// <summary>输出失败原因并返回适合进程退出码的错误值。</summary>
int failTest(SM_Handle handle, const char* stage)
{
    std::cerr << stage << ": " << sm_get_last_message(handle) << '\n';
    return 1;
}

} // namespace

/// <summary>逐点对比 OpenCV Canny 真值，并验证遮罩不会生成假边、弱边和空图保护。</summary>
bool verifyCannyFeatures()
{
    cv::Mat gray(80, 96, CV_8U, cv::Scalar(20));
    cv::rectangle(gray, cv::Rect(12, 12, 65, 50), cv::Scalar(220), 2);
    cv::rectangle(gray, cv::Rect(30, 25, 20, 15), cv::Scalar(35), 2);
    cv::Mat mask(gray.size(), CV_8U, cv::Scalar(255));
    mask(cv::Rect(0, 0, 25, gray.rows)).setTo(0);
    SM_Handle handle = sm_create();
    if (!handle)
        return false;
    bool valid = true;
    for (double contrast : {20.0, 100.0}) {
        for (bool masked : {false, true}) {
            cv::Mat gx, gy, expected;
            cv::Sobel(gray, gx, CV_16S, 1, 0, 3);
            cv::Sobel(gray, gy, CV_16S, 0, 1, 3);
            cv::Canny(gx, gy, expected, contrast * 0.5, contrast, true);
            if (masked)
                cv::bitwise_and(expected, mask, expected);
            const int created = sm_create_model(handle, gray.data, gray.cols, gray.rows,
                static_cast<int>(gray.step), 1, 0, 0, gray.cols, gray.rows,
                1, 0, 0, 1, "use_polarity", contrast, 10, 10000,
                masked ? mask.data : nullptr, masked ? static_cast<int>(mask.step) : 0);
            std::int32_t count = 0;
            valid = valid && created && sm_get_features(handle, nullptr, 0, &count);
            valid = valid && count == cv::countNonZero(expected);
            std::vector<float> xy(static_cast<std::size_t>(count) * 2);
            if (count > 0)
                valid = valid && sm_get_features(handle, xy.data(), count, &count);
            cv::Mat actual = cv::Mat::zeros(gray.size(), CV_8U);
            for (int i = 0; i < count; ++i) {
                const int x = cvRound(xy[static_cast<std::size_t>(i) * 2]);
                const int y = cvRound(xy[static_cast<std::size_t>(i) * 2 + 1]);
                if (x < 0 || x >= gray.cols || y < 0 || y >= gray.rows) {
                    valid = false;
                    continue;
                }
                actual.at<unsigned char>(y, x) = 255;
            }
            valid = valid && cv::countNonZero(actual != expected) == 0;
        }
    }
    gray.setTo(20);
    // 无边缘建模必须失败，C ABI 的候选模型机制必须保留此前成功的模板。
    valid = valid && !sm_create_model(handle, gray.data, gray.cols, gray.rows,
        static_cast<int>(gray.step), 1, 0, 0, gray.cols, gray.rows,
        1, 0, 0, 1, "use_polarity", 20, 10, 10000, nullptr, 0);
    valid = valid && sm_has_model(handle);
    sm_destroy(handle);
    std::cout << "Canny feature/mask/threshold/rollback checks: " << (valid ? "passed" : "FAILED") << '\n';
    return valid;
}

/// <summary>自动阈值随对比度变化，显示值可手动复现特征，失败和清空状态保持一致。</summary>
bool verifyAutomaticContrast()
{
    SM_Handle handle = sm_create();
    if (!handle)
        return false;
    bool valid = true;
    double previousThreshold = 0.0;
    for (int brightness : {60, 220}) {
        cv::Mat gray(80, 96, CV_8U, cv::Scalar(20));
        cv::rectangle(gray, cv::Rect(14, 14, 60, 48), cv::Scalar(brightness), 3);
        const auto create = [&](double contrast) {
            return sm_create_model(handle, gray.data, gray.cols, gray.rows,
                static_cast<int>(gray.step), 1, 0, 0, gray.cols, gray.rows,
                1, 0, 0, 1, "use_polarity", contrast, 10, 300, nullptr, 0);
        };
        valid = valid && create(0);
        double threshold = 0.0;
        valid = valid && sm_get_model_contrast(handle, &threshold)
            && threshold > previousThreshold && threshold <= 1024;
        std::int32_t count = 0;
        valid = valid && sm_get_features(handle, nullptr, 0, &count);
        std::vector<float> automatic(static_cast<std::size_t>(count) * 2);
        valid = valid && sm_get_features(handle, automatic.data(), count, &count);
        valid = valid && create(threshold);
        std::vector<float> manual(automatic.size());
        valid = valid && sm_get_features(handle, manual.data(), count, &count) && manual == automatic;
        valid = valid && !create(-1);
        gray.setTo(20);
        valid = valid && !create(0);
        double retained = 0.0;
        valid = valid && sm_get_model_contrast(handle, &retained) && retained == threshold;
        previousThreshold = threshold;
        std::cout << "Automatic contrast: brightness=" << brightness << ", high=" << threshold << '\n';
    }
    sm_clear_model(handle);
    double cleared = -1.0;
    valid = valid && !sm_get_model_contrast(handle, &cleared) && cleared == 0;
    sm_destroy(handle);
    return valid;
}

/// <summary>生成带非对称外框和重复孔洞的工业形状，检验包含/缺失而非骰子专用分类。</summary>
cv::Mat createHoleFixture(int holes)
{
    cv::Mat patch(140, 140, CV_8U, cv::Scalar(15));
    cv::rectangle(patch, cv::Rect(10, 10, 120, 120), cv::Scalar(232), cv::FILLED);
    cv::rectangle(patch, cv::Rect(10, 10, 16, 12), cv::Scalar(15), cv::FILLED);
    const cv::Point centers[] = {{38,38},{70,70},{102,102},{38,102},{102,38},{70,104}};
    for (int index = 0; index < holes; ++index)
        cv::circle(patch, centers[index], 8, cv::Scalar(25), cv::FILLED, cv::LINE_AA);
    return patch;
}

/// <summary>固定0.65门限验证孔洞交叉混淆、已知旋转/变暗、建模遮罩和涂抹生效。</summary>
bool verifyStructureDiscrimination()
{
    SM_Handle handle = sm_create();
    if (!handle)
        return false;
    bool valid = true;
    const auto create = [&](const cv::Mat& patch, const cv::Mat& mask) {
        return sm_create_model(handle, patch.data, patch.cols, patch.rows, static_cast<int>(patch.step),
            1, 0, 0, patch.cols, patch.rows, 0, -180, 180, 2, "use_polarity", 0, 10, 300,
            mask.empty() ? nullptr : mask.data, mask.empty() ? 0 : static_cast<int>(mask.step));
    };
    const auto check = [&](int templateHoles, int targetHoles, double angle, double brightness, bool expected) {
        cv::Mat patch = createHoleFixture(targetHoles);
        patch.convertTo(patch, CV_8U, brightness);
        cv::Mat transform = cv::getRotationMatrix2D(cv::Point2f(70,70), -angle, 1.0);
        transform.at<double>(0,2) += 80;
        transform.at<double>(1,2) += 80;
        cv::Mat search;
        cv::warpAffine(patch, search, transform, cv::Size(300,300), cv::INTER_LINEAR,
            cv::BORDER_CONSTANT, cv::Scalar(15 * brightness));
        SM_Match matches[8]{};
        std::int32_t count = 0;
        const bool found = sm_find(handle, search.data, search.cols, search.rows, static_cast<int>(search.step),
            1, -180, 180, 0.65, 8, 0.2, 1, 0, 1, matches, 8, &count) != 0;
        const bool correct = found && (expected ? count == 1
            && std::hypot(matches[0].centerX - 150, matches[0].centerY - 150) < 2.0
            && std::abs(matches[0].angleDegrees - angle) < 1.5 : count == 0);
        if (!correct)
            std::cerr << "Structure fixture failed: model=" << templateHoles << ", target=" << targetHoles
                << ", angle=" << angle << ", expected=" << expected << ", count=" << count
                << ", pose=" << matches[0].centerX << ',' << matches[0].centerY << ',' << matches[0].angleDegrees << '\n';
        return correct;
    };
    for (int holes = 3; holes <= 6; ++holes) {
        const cv::Mat patch = createHoleFixture(holes);
        valid = (create(patch, cv::Mat()) != 0) && valid;
        for (int target = 3; target <= 6; ++target)
            valid = check(holes, target, 0, 1.0, holes == target) && valid;
        valid = check(holes, holes, 13.5, 0.65, true) && valid;
    }
    const cv::Mat five = createHoleFixture(5);
    valid = (create(five, cv::Mat()) != 0) && valid;
    valid = check(5, 3, 0, 1.0, false) && valid;
    cv::Mat erase = cv::Mat::zeros(five.size(), CV_8U);
    cv::circle(erase, cv::Point(38,102), 14, cv::Scalar(255), cv::FILLED);
    cv::circle(erase, cv::Point(102,38), 14, cv::Scalar(255), cv::FILLED);
    valid = (sm_erase_features(handle, erase.data, erase.cols, erase.rows, static_cast<int>(erase.step)) != 0) && valid;
    valid = check(5, 3, 0, 1.0, true) && valid;
    valid = (create(five, 255 - erase) != 0) && valid;
    valid = check(5, 3, 13.5, 1.0, true) && valid;
    sm_destroy(handle);
    std::cout << "Structure discrimination and masking: " << (valid ? "passed" : "FAILED") << '\n';
    return valid;
}

/// <summary>两个分离取样区域不应因凸包局部闭环被误判；忽略区变化不影响匹配。</summary>
bool verifyDisjointRegionMask()
{
    cv::Mat image(240, 320, CV_8U, cv::Scalar(20));
    cv::rectangle(image, cv::Rect(32, 40, 58, 55), cv::Scalar(235), 4);
    cv::line(image, cv::Point(40, 50), cv::Point(75, 80), cv::Scalar(170), 3);
    cv::circle(image, cv::Point(250, 140), 29, cv::Scalar(225), 4);
    cv::line(image, cv::Point(236, 130), cv::Point(263, 150), cv::Scalar(190), 3);
    cv::rectangle(image, cv::Rect(134, 65, 43, 91), cv::Scalar(250), 4);
    cv::Mat mask = cv::Mat::zeros(image.size(), CV_8U);
    mask(cv::Rect(19, 25, 85, 85)).setTo(255);
    mask(cv::Rect(208, 98, 82, 82)).setTo(255);
    SM_Handle handle = sm_create();
    if (!handle) return false;
    bool valid = sm_create_model(handle, image.data, image.cols, image.rows, static_cast<int>(image.step),
        1, 19, 25, 271, 155, 1, -30, 30, 1, "use_polarity", 0, 10, 0, mask.data, static_cast<int>(mask.step)) != 0;
    const auto check = [&](const cv::Mat& search, bool expected, double x, double y, double angle) {
        SM_Match matches[1]{};
        std::int32_t count = 0;
        const bool found = sm_find(handle, search.data, search.cols, search.rows, static_cast<int>(search.step),
            1, -30, 30, 0.65, 1, 0.3, 1, 1, 1, matches, 1, &count) != 0;
        const bool correct = found && (expected ? count == 1 && matches[0].score > 0.85
            && std::hypot(matches[0].centerX - x, matches[0].centerY - y) < 2
            && std::abs(matches[0].angleDegrees - angle) < 1.5 : count == 0);
        if (!correct) std::cerr << "Disjoint mask failed: count=" << count << ", score=" << matches[0].score << '\n';
        return correct;
    };
    valid = check(image, true, 154.5, 102.5, 0) && valid;
    cv::Mat changed = image.clone();
    changed(cv::Rect(115, 20, 80, 180)).setTo(20);
    valid = check(changed, true, 154.5, 102.5, 0) && valid;
    cv::Mat transform = cv::getRotationMatrix2D(cv::Point2f(154.5F,102.5F), -13, 1);
    transform.at<double>(0,2) += 55.5; transform.at<double>(1,2) += 57.5;
    cv::Mat rotated;
    cv::warpAffine(changed, rotated, transform, cv::Size(440,340), cv::INTER_LINEAR, cv::BORDER_CONSTANT, cv::Scalar(20));
    valid = check(rotated, true, 210, 160, 13) && valid;
    changed(cv::Rect(200, 90, 110, 110)).setTo(20);
    valid = check(changed, false, 0, 0, 0) && valid;
    sm_destroy(handle);
    std::cout << "Disjoint ROI mask, ignored background, rotation and missing region: " << (valid ? "passed" : "FAILED") << '\n';
    return valid;
}

/// <summary>大跨度稀疏组合区域的角度抽样必须考虑空间位移，自动金字塔不能漏掉原图。</summary>
bool verifyWideRegionMask()
{
    cv::Mat image(2048, 3072, CV_8U, cv::Scalar(20));
    cv::Mat mask = cv::Mat::zeros(image.size(), CV_8U);
    const cv::Rect regions[] = { {265, 171, 211, 378}, {229, 1708, 228, 339}, {2034, 976, 405, 425} };
    for (const auto& region : regions) {
        mask(region).setTo(255);
        cv::rectangle(image, cv::Rect(region.x + 20, region.y + 20, region.width - 40, region.height - 40), cv::Scalar(230), 6);
        cv::circle(image, cv::Point(region.x + region.width / 2, region.y + region.height / 2), 63, cv::Scalar(200), 5);
        cv::line(image, cv::Point(region.x + 35, region.y + 60),
            cv::Point(region.x + region.width - 30, region.y + region.height - 50), cv::Scalar(150), 7);
    }
    SM_Handle handle = sm_create();
    if (!handle) return false;
    bool valid = sm_create_model(handle, image.data, image.cols, image.rows, static_cast<int>(image.step),
        1, 229, 171, 2210, 1876, 0, -30, 30, 1, "use_polarity", 0, 10, 300, mask.data, static_cast<int>(mask.step)) != 0;
    const auto check = [&](const cv::Mat& search, double x, double y, double angle) {
        SM_Match matches[1]{};
        std::int32_t count = 0;
        const bool found = sm_find(handle, search.data, search.cols, search.rows, static_cast<int>(search.step),
            1, -30, 30, 0.8, 1, 0.3, 1, 0, 0, matches, 1, &count) != 0;
        const bool correct = found && count == 1 && matches[0].score >= 0.8
            && std::hypot(matches[0].centerX - x, matches[0].centerY - y) < 3
            && std::abs(matches[0].angleDegrees - angle) < 0.5;
        if (!correct) std::cerr << "Wide mask failed: angle=" << angle << ", count=" << count
            << ", score=" << matches[0].score << '\n';
        return correct;
    };
    valid = check(image, 1334, 1109, 0) && valid;
    for (double angle : {13.0, -7.0, 0.3, -0.5}) {
        cv::Mat transform = cv::getRotationMatrix2D(cv::Point2f(1334, 1109), -angle, 1);
        transform.at<double>(0, 2) += 300; transform.at<double>(1, 2) += 400;
        cv::Mat shifted;
        cv::warpAffine(image, shifted, transform, cv::Size(3600, 3200), cv::INTER_LINEAR, cv::BORDER_CONSTANT, cv::Scalar(20));
        valid = check(shifted, 1634, 1509, angle) && valid;
    }
    // 未见过的无目标输入和缺失组合区域必须拒绝，不能靠低分辨率局部轮廓过关。
    const auto reject = [&](const cv::Mat& search) {
        SM_Match matches[2]{}; std::int32_t count = 0;
        sm_find(handle, search.data, search.cols, search.rows, static_cast<int>(search.step),
            1, -30, 30, 0.8, 2, 0.3, 1, 0, 0, matches, 2, &count);
        if (count != 0) std::cerr << "Unexpected wide ROI detection: " << matches[0].score << '\n';
        return count == 0;
    };
    cv::Mat blank(image.size(), CV_8U, cv::Scalar(20));
    valid = reject(blank) && valid;
    cv::Mat noise(image.size(), CV_8U); cv::RNG random(31987); random.fill(noise, cv::RNG::UNIFORM, 0, 256);
    valid = reject(noise) && valid;
    for (const auto& region : regions) {
        cv::Mat missing = image.clone(); missing(region).setTo(20);
        valid = reject(missing) && valid;
    }
    // 多目标必须按最终精修分数排序，返回数量受调用参数和输出缓冲区共同约束。
    cv::Mat doubleImage(image.rows, image.cols * 2, CV_8U, cv::Scalar(20));
    image.copyTo(doubleImage(cv::Rect(0, 0, image.cols, image.rows)));
    image.copyTo(doubleImage(cv::Rect(image.cols, 0, image.cols, image.rows)));
    SM_Match repeated[4]{}; std::int32_t repeatedCount = 0;
    sm_find(handle, doubleImage.data, doubleImage.cols, doubleImage.rows, static_cast<int>(doubleImage.step),
        1, -30, 30, 0.8, 2, 0.3, 1, 0, 0, repeated, 4, &repeatedCount);
    valid = repeatedCount == 2 && repeated[0].score >= repeated[1].score && valid;
    if (repeatedCount != 2) std::cerr << "Wide ROI two targets: " << repeatedCount << '\n';
    std::int32_t integerCount = 0;
    sm_find(handle, image.data, image.cols, image.rows, static_cast<int>(image.step),
        1, -30, 30, 0.8, 1, 0.3, 0, 0, 0, repeated, 4, &integerCount);
    valid = integerCount == 1 && repeated[0].centerX == std::round(repeated[0].centerX)
        && repeated[0].centerY == std::round(repeated[0].centerY) && valid;
    sm_destroy(handle);
    std::cout << "Wide ROI automatic pyramid and rotation: " << (valid ? "passed" : "FAILED") << '\n';
    return valid;
}

/// <summary>验证 DLL 的自动阈值、建模、搜索、结果传输和句柄生命周期。</summary>
int main()
{
    if (!verifyCannyFeatures() || !verifyAutomaticContrast() || !verifyStructureDiscrimination()
        || !verifyDisjointRegionMask() || !verifyWideRegionMask())
        return 1;
    constexpr int templateWidth = 96;
    constexpr int templateHeight = 88;
    constexpr int searchWidth = 260;
    constexpr int searchHeight = 190;
    constexpr int targetX = 104;
    constexpr int targetY = 58;

    const std::vector<std::uint8_t> templateImage =
        createTemplateImage(templateWidth, templateHeight);
    std::vector<std::uint8_t> searchImage(
        static_cast<std::size_t>(searchWidth * searchHeight), 20U);
    copyPatch(
        templateImage,
        templateWidth,
        templateHeight,
        searchImage,
        searchWidth,
        targetX,
        targetY);

    SM_Handle handle = sm_create();
    if (handle == nullptr) {
        std::cerr << "sm_create returned a null handle.\n";
        return 1;
    }

    const auto destroyHandle = [&handle]() {
        sm_destroy(handle);
        handle = nullptr;
    };

    if (sm_get_abi_version() != 1) {
        std::cerr << "Unexpected native ABI version.\n";
        destroyHandle();
        return 1;
    }

    const std::int32_t modelCreated = sm_create_model(
        handle,
        templateImage.data(),
        templateWidth,
        templateHeight,
        templateWidth,
        1,
        0,
        0,
        templateWidth,
        templateHeight,
        3,
        0.0,
        0.0,
        1.0,
        "use_polarity",
        15.0,
        8.0,
        256,
        nullptr,
        0);
    if (modelCreated == 0 || sm_has_model(handle) == 0) {
        const int result = failTest(handle, "sm_create_model failed");
        destroyHandle();
        return result;
    }

    SM_Match matches[4]{};
    std::int32_t matchCount = 0;
    const std::int32_t searchSucceeded = sm_find(
        handle,
        searchImage.data(),
        searchWidth,
        searchHeight,
        searchWidth,
        1,
        0.0,
        0.0,
        0.75,
        4,
        0.2,
        1,
        3,
        2,
        matches,
        4,
        &matchCount);
    if (searchSucceeded == 0 || matchCount < 1) {
        const int result = failTest(handle, "sm_find failed");
        destroyHandle();
        return result;
    }

    const double expectedCenterX = targetX + templateWidth / 2.0;
    const double expectedCenterY = targetY + templateHeight / 2.0;
    const SM_Match& best = matches[0];
    const bool locationIsCorrect =
        std::abs(best.centerX - expectedCenterX) <= 2.0
        && std::abs(best.centerY - expectedCenterY) <= 2.0;
    const bool poseIsCorrect =
        std::abs(best.angleDegrees) <= 0.1 && best.score >= 0.75;
    if (!locationIsCorrect || !poseIsCorrect) {
        std::cerr << "Unexpected match: center=(" << best.centerX << ", "
                  << best.centerY << "), angle=" << best.angleDegrees
                  << ", score=" << best.score << '\n';
        destroyHandle();
        return 1;
    }

    std::cout << "ShapeMatch smoke test passed: center=(" << best.centerX
              << ", " << best.centerY << "), angle=" << best.angleDegrees
              << ", score=" << best.score << '\n';
    destroyHandle();
    return 0;
}
