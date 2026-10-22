#pragma once

#include <opencv2/core.hpp>
#include "ContourCoarseBlocked.h"
#include <algorithm>
#include <array>
#include <map>
#include <utility>
#include <vector>

namespace contourcv_detail {

// Raw integer sample: x offset, y offset, direction-bank index.
using CoarseSample = std::array<int, 3>;
struct CoarsePattern {
    // Canonical entries: x, y, direction, multiplicity.
    using Key = std::vector<std::array<int, 4>>;
    Key offsets;
    int sampleCount = 0;
};

inline CoarsePattern canonicalCoarsePattern(std::vector<CoarseSample> samples)
{
    // Every input contribution is an unsigned byte. Below this bound every
    // partial sum, product and final sum is an exactly representable float
    // integer, irrespective of the order in which equal samples are grouped.
    CV_Assert(!samples.empty() && samples.size() <= (1U << 24) / 255U);
    CoarsePattern result;
    result.sampleCount = static_cast<int>(samples.size());
    std::sort(samples.begin(), samples.end());
    for (const CoarseSample& sample : samples) {
        if (!result.offsets.empty()) {
            auto& last = result.offsets.back();
            if (last[0] == sample[0] && last[1] == sample[1] && last[2] == sample[2]) {
                ++last[3];
                continue;
            }
        }
        result.offsets.push_back({sample[0], sample[1], sample[2], 1});
    }
    return result;
}

inline std::size_t internCoarsePattern(CoarsePattern pattern,
    std::map<CoarsePattern::Key, std::size_t>& index, std::vector<CoarsePattern>& patterns)
{
    const auto found = index.find(pattern.offsets);
    if (found != index.end()) return found->second;
    const std::size_t group = patterns.size();
    index.emplace(pattern.offsets, group);
    patterns.push_back(std::move(pattern));
    return group;
}

inline cv::Mat coarseResponse(const std::array<cv::Mat, 24>& response,
                              cv::Size size, const CoarsePattern& pattern)
{
    cv::Mat scores;
    if (coarseResponseBlocked(response, size, pattern.offsets, pattern.sampleCount, scores)) return scores;
    scores = cv::Mat::zeros(size, CV_32F);
    for (const auto& offset : pattern.offsets) {
        const int dx = offset[0], dy = offset[1], bank = offset[2], count = offset[3];
        CV_DbgAssert(bank >= 0 && bank < 24 && response[bank].type() == CV_8UC1
            && response[bank].size() == size && count > 0);
        const int x0 = std::max(0, -dx), y0 = std::max(0, -dy);
        const int x1 = std::min(scores.cols, scores.cols - dx);
        const int y1 = std::min(scores.rows, scores.rows - dy);
        if (x1 <= x0 || y1 <= y0) continue;
        for (int y = y0; y < y1; ++y) {
            float* destination = scores.ptr<float>(y);
            const uchar* source = response[bank].ptr<uchar>(y + dy);
            // The common count==1 case retains the original vectorizable loop.
            if (count == 1) {
                for (int x = x0; x < x1; ++x)
                    destination[x] += source[x + dx];
            } else {
                for (int x = x0; x < x1; ++x)
                    destination[x] += count * source[x + dx];
            }
        }
    }
    return scores;
}

} // namespace contourcv_detail