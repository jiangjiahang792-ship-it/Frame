#include "ShapeMatchNative.h"

#include "CShapeMatchCV.h"

#include <opencv2/core.hpp>

#include <algorithm>
#include <exception>
#include <limits>
#include <new>
#include <string>
#include <vector>

namespace {

/// <summary>DLL 句柄背后的实例状态。</summary>
struct ShapeMatcherContext
{
    /// <summary>真正执行算法的 C++ 匹配器。</summary>
    CShapeMatchCV matcher;

    /// <summary>供托管端读取的最近一次消息。</summary>
    std::string lastMessage = "OK";
};

/// <summary>将不透明句柄转换成内部上下文。</summary>
ShapeMatcherContext* contextFrom(SM_Handle handle) noexcept
{
    return static_cast<ShapeMatcherContext*>(handle);
}

/// <summary>验证并零拷贝包装托管端传入的图像缓冲区。</summary>
cv::Mat wrapImage(
    const std::uint8_t* data,
    std::int32_t width,
    std::int32_t height,
    std::int32_t stride,
    std::int32_t channels)
{
    if (data == nullptr || width <= 0 || height <= 0
        || (channels != 1 && channels != 3 && channels != 4)
        || stride < width * channels) {
        return {};
    }

    return cv::Mat(
        height,
        width,
        CV_MAKETYPE(CV_8U, channels),
        const_cast<std::uint8_t*>(data),
        static_cast<std::size_t>(stride));
}

/// <summary>统一处理跨 ABI 边界的标准异常。</summary>
std::int32_t fail(ShapeMatcherContext* context, const char* message) noexcept
{
    if (context != nullptr)
        context->lastMessage = message == nullptr ? "Unknown error." : message;
    return 0;
}

} // namespace

SM_Handle SM_CALL sm_create()
{
    try {
        return new ShapeMatcherContext();
    } catch (...) {
        return nullptr;
    }
}

std::int32_t SM_CALL sm_get_contours(SM_Handle handle, float* xy, std::int32_t* pathIds,
    std::int32_t pointCapacity, std::int32_t* pointCount)
{
    auto* context = contextFrom(handle);
    if (!context || !pointCount)
        return 0;
    *pointCount = 0;
    try {
        if (pointCapacity < 0 || (pointCapacity > 0 && (!xy || !pathIds)))
            return fail(context, "Invalid contour output buffer.");
        const auto& paths = context->matcher.modelContours();
        std::size_t count = 0;
        for (const auto& path : paths)
            count += path.size();
        if (count > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()))
            return fail(context, "Too many contour points.");
        *pointCount = static_cast<std::int32_t>(count);
        if (pointCapacity == 0)
            return 1;
        if (pointCapacity < *pointCount)
            return fail(context, "Contour buffer is too small.");
        std::size_t index = 0;
        for (std::size_t id = 0; id < paths.size(); ++id) {
            for (const auto& point : paths[id]) {
                xy[index * 2] = static_cast<float>(point.x);
                xy[index * 2 + 1] = static_cast<float>(point.y);
                pathIds[index++] = static_cast<std::int32_t>(id);
            }
        }
        return 1;
    } catch (const std::exception& exception) {
        return fail(context, exception.what());
    } catch (...) {
        return fail(context, "Unknown exception reading model contours.");
    }
}

std::int32_t SM_CALL sm_get_features(
    SM_Handle handle, float* xy, std::int32_t pointCapacity, std::int32_t* pointCount)
{
    auto* context = contextFrom(handle);
    if (context == nullptr || pointCount == nullptr)
        return 0;
    *pointCount = 0;
    try {
        if (pointCapacity < 0 || (pointCapacity > 0 && xy == nullptr))
            return fail(context, "Invalid feature output buffer.");
        const auto points = context->matcher.modelFeatures();
        *pointCount = static_cast<std::int32_t>(points.size());
        if (pointCapacity == 0)
            return 1;
        if (pointCapacity < *pointCount)
            return fail(context, "Feature buffer is too small.");
        for (std::size_t i = 0; i < points.size(); ++i) {
            xy[i * 2] = points[i].x;
            xy[i * 2 + 1] = points[i].y;
        }
        return 1;
    } catch (const std::exception& exception) {
        return fail(context, exception.what());
    } catch (...) {
        return fail(context, "Unknown exception reading model features.");
    }
}

std::int32_t SM_CALL sm_get_model_contrast(SM_Handle handle, double* contrast)
{
    const auto* context = contextFrom(handle);
    if (contrast == nullptr)
        return 0;
    *contrast = 0.0;
    if (context == nullptr || !context->matcher.hasModel())
        return 0;
    *contrast = context->matcher.modelContrast();
    return 1;
}

std::int32_t SM_CALL sm_erase_features(
    SM_Handle handle, const std::uint8_t* maskData,
    std::int32_t width, std::int32_t height, std::int32_t stride)
{
    auto* context = contextFrom(handle);
    if (context == nullptr)
        return 0;
    try {
        const cv::Mat mask = wrapImage(maskData, width, height, stride, 1);
        std::string message;
        const bool result = context->matcher.eraseFeatures(mask, message);
        context->lastMessage = message;
        return result ? 1 : 0;
    } catch (const std::exception& exception) {
        return fail(context, exception.what());
    } catch (...) {
        return fail(context, "Unknown exception erasing model features.");
    }
}

void SM_CALL sm_destroy(SM_Handle handle)
{
    delete contextFrom(handle);
}

void SM_CALL sm_clear_model(SM_Handle handle)
{
    ShapeMatcherContext* context = contextFrom(handle);
    if (context == nullptr)
        return;

    context->matcher.clear();
    context->lastMessage = "OK";
}

std::int32_t SM_CALL sm_has_model(SM_Handle handle)
{
    const ShapeMatcherContext* context = contextFrom(handle);
    return context != nullptr && context->matcher.hasModel() ? 1 : 0;
}

std::int32_t SM_CALL sm_get_abi_version()
{
    return 1;
}

const char* SM_CALL sm_get_last_message(SM_Handle handle)
{
    ShapeMatcherContext* context = contextFrom(handle);
    return context == nullptr ? "Invalid matcher handle."
                              : context->lastMessage.c_str();
}

std::int32_t SM_CALL sm_create_model(
    SM_Handle handle,
    const std::uint8_t* imageData,
    std::int32_t imageWidth,
    std::int32_t imageHeight,
    std::int32_t imageStride,
    std::int32_t imageChannels,
    std::int32_t roiX,
    std::int32_t roiY,
    std::int32_t roiWidth,
    std::int32_t roiHeight,
    std::int32_t numLevels,
    double angleStartDegrees,
    double angleEndDegrees,
    double angleStepDegrees,
    const char* metricUtf8,
    double contrast,
    double minContrast,
    std::int32_t featureCount,
    const std::uint8_t* maskData,
    std::int32_t maskStride)
{
    ShapeMatcherContext* context = contextFrom(handle);
    if (context == nullptr)
        return 0;

    try {
        const cv::Mat image = wrapImage(
            imageData, imageWidth, imageHeight, imageStride, imageChannels);
        if (image.empty())
            return fail(context, "The input image buffer is invalid.");

        cv::Mat mask;
        if (maskData != nullptr) {
            mask = wrapImage(
                maskData, imageWidth, imageHeight, maskStride, 1);
            if (mask.empty())
                return fail(context, "The mask buffer is invalid.");
        }

        std::string message;
        // 先创建候选模型，建模失败时已有模型仍然可用。
        CShapeMatchCV candidate;
        const bool succeeded = candidate.createModel(
            image,
            roiX, roiY, roiWidth, roiHeight,
            numLevels,
            angleStartDegrees, angleEndDegrees,
            angleStepDegrees,
            metricUtf8 == nullptr ? "use_polarity" : metricUtf8,
            contrast, minContrast, featureCount,
            mask, message);
        context->lastMessage = message;
        if (succeeded)
            context->matcher = std::move(candidate);
        return succeeded ? 1 : 0;
    } catch (const std::exception& exception) {
        return fail(context, exception.what());
    } catch (...) {
        return fail(context, "Unknown native exception while creating the model.");
    }
}

std::int32_t SM_CALL sm_find(
    SM_Handle handle,
    const std::uint8_t* imageData,
    std::int32_t imageWidth,
    std::int32_t imageHeight,
    std::int32_t imageStride,
    std::int32_t imageChannels,
    double angleStartDegrees,
    double angleEndDegrees,
    double minScore,
    std::int32_t maxMatches,
    double maxOverlap,
    std::int32_t subPixel,
    std::int32_t numLevels,
    std::int32_t mode,
    SM_Match* outputMatches,
    std::int32_t outputCapacity,
    std::int32_t* outputCount)
{
    ShapeMatcherContext* context = contextFrom(handle);
    if (context == nullptr || outputCount == nullptr)
        return 0;

    *outputCount = 0;
    if (outputCapacity < 0 || (outputCapacity > 0 && outputMatches == nullptr))
        return fail(context, "The output match buffer is invalid.");

    try {
        const cv::Mat image = wrapImage(
            imageData, imageWidth, imageHeight, imageStride, imageChannels);
        if (image.empty())
            return fail(context, "The input image buffer is invalid.");

        std::string message;
        std::vector<cv::Point2d> centers;
        std::vector<double> angles;
        std::vector<double> scores;
        std::vector<double> widths;
        std::vector<double> heights;
        const bool found = context->matcher.find(
            image,
            angleStartDegrees, angleEndDegrees,
            minScore, maxMatches, maxOverlap,
            subPixel != 0, numLevels, mode,
            message, centers, angles, scores, widths, heights);
        context->lastMessage = message;

        // “未找到”是一次成功完成的搜索，不作为 ABI 错误抛给界面层。
        if (!found && message == "No shape match met the score.")
            return 1;
        if (!found)
            return 0;

        const std::int32_t count = std::min(
            outputCapacity, static_cast<std::int32_t>(centers.size()));
        for (std::int32_t index = 0; index < count; ++index) {
            const std::size_t item = static_cast<std::size_t>(index);
            outputMatches[index] = {
                centers[item].x,
                centers[item].y,
                angles[item],
                scores[item],
                widths[item],
                heights[item]};
        }
        *outputCount = count;
        return 1;
    } catch (const std::exception& exception) {
        return fail(context, exception.what());
    } catch (...) {
        return fail(context, "Unknown native exception while finding matches.");
    }
}
