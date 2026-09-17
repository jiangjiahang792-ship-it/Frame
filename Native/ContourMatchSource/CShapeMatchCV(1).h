#pragma once

#include <opencv2/core.hpp>
#include <memory>
#include <string>
#include <vector>

class CShapeMatchCV
{
public:
    CShapeMatchCV();
    ~CShapeMatchCV();
    CShapeMatchCV& operator=(CShapeMatchCV&&) noexcept;

    bool createModel(
        const cv::Mat& image,
        int roiX, int roiY, int roiWidth, int roiHeight,
        int numLevels,
        double angleStartDegrees, double angleEndDegrees,
        double angleStepDegrees, const std::string& metric,
        double contrast, double minContrast, int featureCount,
        const cv::Mat& mask,
        std::string& message);
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
    void clear();
    bool hasModel() const;

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};
