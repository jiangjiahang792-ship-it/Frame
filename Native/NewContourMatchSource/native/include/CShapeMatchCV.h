#pragma once
#include "CContourMatchCV.h"
#include "CContourMatchCV2.h"
#include <opencv2/imgproc.hpp>
#include <algorithm>
#include <cmath>
#include <stdexcept>

/// <summary>将两套 Demo 轮廓算法适配到框架建模、擦除与搜索接口。</summary>
class CShapeMatchCV {
    /// <summary>原始模板及有效区域；擦除只改变掩膜，不产生人造灰度边缘。</summary>
    cv::Mat image_, mask_;
    /// <summary>当前有效算法实例。</summary>
    std::unique_ptr<CContourMatchCV> first_;
    std::unique_ptr<CContourMatchCV2> second_;
    /// <summary>已缓存的搜索模型参数。</summary>
    CContourMatchCV::Parameters parameters_;
    /// <summary>轮廓过滤、极性及协作超时设置。</summary>
    CContourMatchCV::Options options_;
    /// <summary>默认选择独立第二版算法。</summary>
    int version_ = 2;
    /// <summary>Demo 轮廓原点，用于转换为框架 ROI 中心。</summary>
    cv::Point2d origin_;
    /// <summary>保留亚像素的 ROI 局部有序轮廓。</summary>
    CContourMatchCV::Contours contours_;
    /// <summary>事务式建模，失败不覆盖现有模型及掩膜。</summary>
    bool rebuild(const cv::Mat& image, const cv::Mat& mask,
                 const CContourMatchCV::Parameters& p, std::string& message) {
        auto first = std::make_unique<CContourMatchCV>();
        auto second = std::make_unique<CContourMatchCV2>();
        bool ok = version_ == 2 ? second->createModel(image, mask, p, options_, message)
                                : first->createModel(image, mask, p, options_, message);
        if (!ok) return false;
        const auto& paths = version_ == 2 ? second->templateContours() : first->templateContours();
        cv::Point2d low(image.cols, image.rows), high(0, 0);
        for (const auto& chain : paths) for (auto point : chain) {
            low.x = std::min(low.x, point.x); low.y = std::min(low.y, point.y);
            high.x = std::max(high.x, point.x); high.y = std::max(high.y, point.y);
        }
        contours_ = paths; origin_ = (low + high) * .5;
        first_ = std::move(first); second_ = std::move(second);
        image_ = image; mask_ = mask; parameters_ = p;
        return true;
    }
public:
    /// <summary>最近一次匹配的独立尺度，供 ABI 输出。</summary>
    std::vector<cv::Point2d> scales;
    /// <summary>设置本次建模使用的算法与几何搜索范围。</summary>
    void configure(int version, double sx, double sy, double low, double length, int timeout) {
        if ((version != 1 && version != 2) || !std::isfinite(sx) || !std::isfinite(sy)
            || sx < 0 || sx >= 1 || sy < 0 || sy >= 1 || !std::isfinite(low)
            || low < 0 || !std::isfinite(length) || length < 0 || timeout < 0)
            throw std::invalid_argument("新版轮廓算法配置无效。");
        version_ = version; parameters_.scaleToleranceX = sx; parameters_.scaleToleranceY = sy;
        parameters_.lowThreshold = low; options_.minContourLength = length; options_.timeoutMs = timeout;
    }
    /// <summary>裁剪保留原始灰度及掩膜，创建可复用的 Demo 模型。</summary>
    bool createModel(const cv::Mat& image, int x, int y, int w, int h, int levels,
        double start, double end, double step, const std::string& metric, double contrast,
        double minContrast, int, const cv::Mat& mask, std::string& message) {
        const cv::Rect roi(x,y,w,h);
        if (w < 8 || h < 8 || (roi & cv::Rect(0,0,image.cols,image.rows)) != roi) {
            message = "模板区域无效。"; return false;
        }
        auto p = parameters_; p.numLevels = levels; p.angleStart = start; p.angleEnd = end;
        p.angleStep = step; p.highThreshold = contrast; p.minContrast = cvRound(minContrast);
        options_.polarity = metric.find("ignore") != std::string::npos
            ? CContourMatchCV::Polarity::Ignore : CContourMatchCV::Polarity::Use;
        cv::Mat selected = mask.empty() ? cv::Mat(h,w,CV_8U,cv::Scalar(255)) : mask(roi).clone();
        return rebuild(image(roi).clone(), selected, p, message);
    }
    /// <summary>重放擦除，失败保持已建模型；原图像素不变。</summary>
    bool eraseFeatures(const cv::Mat& erased, std::string& message) {
        if (!hasModel() || erased.size()!=mask_.size() || erased.type()!=CV_8U) {
            message="擦除掩膜尺寸无效。"; return false;
        }
        cv::Mat selected = mask_.clone(); selected.setTo(0, erased);
        return rebuild(image_, selected, parameters_, message);
    }
    /// <summary>搜索参数改变时重建并缓存模型，后续相同配置直接复用。</summary>
    bool find(const cv::Mat& image, double start, double end, double score, int count,
        double overlap, bool, int levels, int, std::string& message,
        std::vector<cv::Point2d>& centers, std::vector<double>& angles,
        std::vector<double>& scores, std::vector<double>& widths, std::vector<double>& heights) {
        scales.clear();
        if (!hasModel()) { message="请先创建模板。"; return false; }
        if (parameters_.angleStart != start || parameters_.angleEnd != end
            || (levels > 0 && levels != parameters_.numLevels)) {
            auto p=parameters_; p.angleStart=start; p.angleEnd=end;
            if (levels>0) p.numLevels=levels;
            if (!rebuild(image_,mask_,p,message)) return false;
        }
        std::vector<CContourMatchCV::Match> matches;
        bool ok=version_==2 ? second_->find(image,score,count,overlap,matches,message)
                            : first_->find(image,score,count,overlap,matches,message);
        if (!ok) return false;
        for (const auto& match:matches) {
            // 框架以 ROI 中心为定位基准，不能直接使用 Demo 的轮廓包围盒原点。
            const double radians=match.angle*CV_PI/180, c=std::cos(radians), s=std::sin(radians);
            const double dx=(image_.cols*.5-origin_.x)*match.scaleX;
            const double dy=(image_.rows*.5-origin_.y)*match.scaleY;
            centers.push_back(match.center+cv::Point2d(c*dx-s*dy,s*dx+c*dy));
            angles.push_back(match.angle); scores.push_back(match.score);
            widths.push_back(image_.cols*match.scaleX); heights.push_back(image_.rows*match.scaleY);
            scales.emplace_back(match.scaleX,match.scaleY);
        }
        return true;
    }
    /// <summary>检查当前模型是否可用。</summary>
    bool hasModel() const { return version_==2 ? second_ && second_->hasModel() : first_ && first_->hasModel(); }
    /// <summary>释放模板及搜索缓存。</summary>
    void clear() { first_.reset();second_.reset();image_.release();mask_.release();contours_.clear();scales.clear(); }
    /// <summary>返回实际高阈值。</summary>
    double modelContrast() const { return parameters_.highThreshold; }
    /// <summary>提供编辑器所需的真实亚像素轮廓点。</summary>
    std::vector<cv::Point2f> modelFeatures() const {
        std::vector<cv::Point2f> points;
        for(const auto& path:contours_) for(auto point:path) points.emplace_back(point);
        return points;
    }
    /// <summary>返回实际参与建模的有序轮廓。</summary>
    const CContourMatchCV::Contours& modelContours() const {return contours_;}
};
