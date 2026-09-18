#pragma once

#include <opencv2/core.hpp>

#include <memory>
#include <string>
#include <vector>

/// <summary>
/// 基于 Canny 模型边缘与方向梯度搜索的多角度轮廓模板匹配器。
/// </summary>
class CShapeMatchCV
{
public:
    /// <summary>创建一个尚未包含模板的匹配器。</summary>
    CShapeMatchCV();

    /// <summary>释放模板及其关联资源。</summary>
    ~CShapeMatchCV();

    /// <summary>移动另一个匹配器持有的模型。</summary>
    CShapeMatchCV& operator=(CShapeMatchCV&&) noexcept;

    /// <summary>从图像 ROI 和可选遮罩创建多层、多角度形状模型。</summary>
    bool createModel(
        const cv::Mat& image,
        int roiX, int roiY, int roiWidth, int roiHeight,
        int numLevels,
        double angleStartDegrees, double angleEndDegrees,
        double angleStepDegrees, const std::string& metric,
        double contrast, double minContrast, int featureCount,
        const cv::Mat& mask,
        std::string& message);

    /// <summary>在输入图像中查找模型并返回按分数排序的姿态。</summary>
    bool find(
        const cv::Mat& image,
        double angleStartDegrees, double angleEndDegrees,
        double minScore, int maxMatches, double maxOverlap,
        bool subPixel, int numLevels, int mode,
        std::string& message,
        std::vector<cv::Point2d>& centers,
        std::vector<double>& anglesDegrees,
        std::vector<double>& scores,
        std::vector<double>& widths,
        std::vector<double>& heights) const;

    /// <summary>清除当前模型。</summary>
    void clear();

    /// <summary>判断当前是否已有可搜索的模型。</summary>
    bool hasModel() const;

    /// <summary>返回当前模型原图层实际使用的 Canny 高阈值；无模型时返回零。</summary>
    double modelContrast() const;

    /// <summary>返回真正参与原图层匹配的特征点，坐标相对于模板 ROI 左上角。</summary>
    std::vector<cv::Point2f> modelFeatures() const;

    /// <summary>读取建模时保留的完整Canny有序轮廓；已删除区域不会返回。</summary>
    const std::vector<std::vector<cv::Point>>& modelContours() const;

    /// <summary>按 ROI 大小的删除遮罩过滤已有特征并重建角度缓存；失败时保留原模型。</summary>
    bool eraseFeatures(const cv::Mat& eraseMask, std::string& message);

private:
    /// <summary>隐藏 OpenCV 实现细节。</summary>
    struct Impl;

    /// <summary>匹配器私有实现。</summary>
    std::unique_ptr<Impl> impl_;
};
