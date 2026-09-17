#pragma once

#include <cstdint>

#if defined(_WIN32)
#if defined(SHAPEMATCH_NATIVE_EXPORTS)
#define SM_API __declspec(dllexport)
#else
#define SM_API __declspec(dllimport)
#endif
#define SM_CALL __cdecl
#else
#define SM_API __attribute__((visibility("default")))
#define SM_CALL
#endif

/// <summary>不透明的原生匹配器句柄。</summary>
using SM_Handle = void*;

/// <summary>原生匹配结果；字段布局与 C# Sequential 结构保持一致。</summary>
struct SM_Match
{
    /// <summary>目标中心横坐标。</summary>
    double centerX;
    /// <summary>目标中心纵坐标。</summary>
    double centerY;
    /// <summary>目标旋转角度，单位为度。</summary>
    double angleDegrees;
    /// <summary>归一化匹配分数。</summary>
    double score;
    /// <summary>原始模板宽度。</summary>
    double width;
    /// <summary>原始模板高度。</summary>
    double height;
};

extern "C" {

/// <summary>查询或复制完整有序轮廓；xy为坐标，pathIds标识路径分段，零容量查询点数。</summary>
SM_API std::int32_t SM_CALL sm_get_contours(SM_Handle handle, float* xy, std::int32_t* pathIds,
    std::int32_t pointCapacity, std::int32_t* pointCount);

/// <summary>读取成功建模后实际使用的 Canny 高阈值；无模型或空输出时失败。</summary>
SM_API std::int32_t SM_CALL sm_get_model_contrast(SM_Handle handle, double* contrast);

/// <summary>读取 ROI 局部坐标的模板特征；空输出/零容量用于查询点数。</summary>
SM_API std::int32_t SM_CALL sm_get_features(
    SM_Handle handle, float* xy, std::int32_t pointCapacity, std::int32_t* pointCount);

/// <summary>删除非零遮罩所覆盖的模板特征；遮罩尺寸必须等于模型 ROI。</summary>
SM_API std::int32_t SM_CALL sm_erase_features(
    SM_Handle handle, const std::uint8_t* maskData,
    std::int32_t width, std::int32_t height, std::int32_t stride);

/// <summary>创建匹配器实例；失败时返回空指针。</summary>
SM_API SM_Handle SM_CALL sm_create();

/// <summary>销毁匹配器实例；允许传入空指针。</summary>
SM_API void SM_CALL sm_destroy(SM_Handle handle);

/// <summary>清除指定实例中的模型。</summary>
SM_API void SM_CALL sm_clear_model(SM_Handle handle);

/// <summary>判断实例是否已经创建模型。</summary>
SM_API std::int32_t SM_CALL sm_has_model(SM_Handle handle);

/// <summary>返回 DLL 的 ABI 版本。</summary>
SM_API std::int32_t SM_CALL sm_get_abi_version();

/// <summary>返回当前实例最近一次操作产生的 UTF-8 消息。</summary>
SM_API const char* SM_CALL sm_get_last_message(SM_Handle handle);

/// <summary>创建形状模型；contrast 为零表示根据 ROI 自动估算 Canny 阈值，正值表示手动。</summary>
SM_API std::int32_t SM_CALL sm_create_model(
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
    std::int32_t maskStride);

/// <summary>在 BGR、BGRA 或灰度缓冲区中搜索形状模型。</summary>
SM_API std::int32_t SM_CALL sm_find(
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
    std::int32_t* outputCount);

}
