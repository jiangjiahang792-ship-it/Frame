#pragma once

#include <opencv2/core.hpp>
#include <memory>
#include <string>
#include <vector>

// HALCON XLD contour matching, following the ShapeMatchX example.
class CContourMatchHalcon final
{
public:
    using Contours = std::vector<std::vector<cv::Point2d>>;
    struct Parameters {
        double lowThreshold = 20.0;
        double highThreshold = 40.0;
        int numLevels = 0;
        double angleStart = -30.0; // Degrees, clockwise positive in the UI.
        double angleEnd = 30.0;
        double angleStep = 0.0;   // Automatic.
        int minContrast = 10;
        double scaleToleranceX = 0.1;
        double scaleToleranceY = 0.1;
    };
    struct Match {
        cv::Point2d center;
        double angle = 0.0;
        double scaleX = 1.0;
        double scaleY = 1.0;
        double score = 0.0;
        Contours contours; // Already transformed into search-image coordinates.
    };

    CContourMatchHalcon();
    ~CContourMatchHalcon();
    CContourMatchHalcon(CContourMatchHalcon&&) noexcept;
    CContourMatchHalcon& operator=(CContourMatchHalcon&&) noexcept;
    bool createModel(const cv::Mat& image, const cv::Mat& mask,
                     const Parameters& parameters, std::string& message);
    // True means the search completed; an empty result means no match.
    bool find(const cv::Mat& image, double minScore, int maxMatches,
              double maxOverlap, std::vector<Match>& matches,
              std::string& message, const cv::Mat& searchMask = {}) const;
    const Contours& templateContours() const;
    bool hasModel() const;
    void clear();

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};
