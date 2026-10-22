#pragma once

// This header contains shared value types only; no HALCON headers or runtime
// are required by the OpenCV implementation.
#include "CContourMatchHalcon.h"
#include <array>
#include <cstddef>

class CContourMatchCV final
{
public:
    using Parameters = CContourMatchHalcon::Parameters;
    using Match = CContourMatchHalcon::Match;
    using Contours = CContourMatchHalcon::Contours;

    enum class Polarity { Ignore, Use };
    struct Options {
        double minContourLength = 0.0; // Optional minimum centerline arc length; zero keeps short contours.
        Polarity polarity = Polarity::Ignore;
        int timeoutMs = 0; // Find deadline; zero is unlimited. Cooperative, not hard real time.
    };

    // Optional, per-call wall-clock diagnostics. No global state or I/O.
    struct FindProfile {
        struct IcpCandidate {
            double initialX = 0, initialY = 0, initialCorrelation = 0, initialSupport = 0;
            double finalScore = 0;
            int iterations = 0;
            bool accepted = false;
        };
        std::vector<IcpCandidate> icpCandidates;
        double pyramidMs = 0, coarsePrepMs = 0, coarseSearchMs = 0, coarseDiverseMs = 0;
        double cacheMs = 0, icpMs = 0, icpDiverseMs = 0, finalRefineMs = 0;
        double outputMs = 0, totalMs = 0;
        std::array<double, 10> levelPrepMs{}, levelOptimizeMs{}, levelDiverseMs{};
        std::array<std::size_t, 10> levelCandidates{}, levelRetainedCandidates{};
        std::array<std::size_t, 10> levelScoreCalls{}, levelScoreCacheHits{};
        std::array<std::size_t, 10> levelSharedScoreHits{};
        std::array<std::size_t, 10> levelAffineLookups{}, levelAffineHits{};
        std::size_t coarseVariants = 0, coarseCandidates = 0, finalCandidates = 0;
        std::size_t coarseUniquePatterns = 0, coarseOriginalOffsets = 0, coarseUniqueOffsets = 0;
        int topLevel = 0;
    };
    CContourMatchCV();
    ~CContourMatchCV();
    CContourMatchCV(CContourMatchCV&&) noexcept;
    CContourMatchCV& operator=(CContourMatchCV&&) noexcept;
    bool createModel(const cv::Mat& image, const cv::Mat& mask,
                     const Parameters& parameters, std::string& message);
    bool createModel(const cv::Mat& image, const cv::Mat& mask,
                     const Parameters& parameters, const Options& options,
                     std::string& message);
    bool find(const cv::Mat& image, double minScore, int maxMatches,
              double maxOverlap, std::vector<Match>& matches,
              std::string& message, const cv::Mat& searchMask = {}, FindProfile* profile = nullptr) const;
    const Contours& templateContours() const;
    bool hasModel() const;
    void clear();

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};
