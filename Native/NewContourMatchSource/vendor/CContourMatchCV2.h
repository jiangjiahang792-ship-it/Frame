#pragma once

#include "CContourMatchCV.h"

// Independent VM-inspired implementation. Shared API value types only;
// neither CContourMatchCV nor a vendor matching runtime is called.
class CContourMatchCV2 final
{
  public:
    using Parameters = CContourMatchCV::Parameters;
    using Match = CContourMatchCV::Match;
    using Contours = CContourMatchCV::Contours;
    using Polarity = CContourMatchCV::Polarity;
    using Options = CContourMatchCV::Options;
    using FindProfile = CContourMatchCV::FindProfile;

    CContourMatchCV2();
    ~CContourMatchCV2();
    CContourMatchCV2(CContourMatchCV2 &&) noexcept;
    CContourMatchCV2 &operator=(CContourMatchCV2 &&) noexcept;
    bool createModel(const cv::Mat &, const cv::Mat &, const Parameters &, std::string &);
    bool createModel(const cv::Mat &, const cv::Mat &, const Parameters &, const Options &, std::string &);
    bool find(const cv::Mat &, double minScore, int maxMatches, double maxOverlap, std::vector<Match> &,
              std::string &, const cv::Mat &searchMask = {}, FindProfile * = nullptr) const;
    const Contours &templateContours() const;
    bool hasModel() const;
    void clear();

  private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};
