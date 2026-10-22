#pragma once

#include <opencv2/core.hpp>
#include <algorithm>
#include <cstdint>
#include <limits>
#include <vector>

namespace visiondemo_research {

// Exact greedy peak extraction for the finite CV_32FC1 score maps produced by
// CContourMatchCV. This changes only how a global maximum is found: it does not
// limit peaks, change thresholds, or replace square suppression with local NMS.
//
// Example (replace the minMaxLoc/rectangle part of the existing peak loop):
//   TiledScorePeaks peaks(scores); // After scores.setTo(0, searchMask == 0).
//   for (...) {
//       const auto peak = peaks.maximum();
//       const double score = peak.value / (255.0 * variant.offsets.size());
//       if (score < threshold) break;
//       // Emit the same pose using peak.location and score.
//       peaks.suppressSquare(peak.location, radius);
//   }
//
// Do not write scores through another alias after construction: the tile
// maxima must be updated together with each mutation. The input Mat's pixels
// are shared, so suppressSquare also changes the caller's original scores.
// Each coarse variant owns its own scores and extractor; no shared state.
class TiledScorePeaks {
public:
    struct Peak {
        double value = 0;
        cv::Point location;
    };

    explicit TiledScorePeaks(cv::Mat& scores, int tileSide = 16)
        : scores_(scores), tileSide_(tileSide)
    {
        CV_Assert(!scores_.empty() && scores_.type() == CV_32FC1 && tileSide_ > 0);
        tileColumns_ = (scores_.cols + tileSide_ - 1) / tileSide_;
        const int tileRows = (scores_.rows + tileSide_ - 1) / tileSide_;
        tiles_.reserve(static_cast<size_t>(tileColumns_) * tileRows);
        for (int tileY = 0; tileY < tileRows; ++tileY) {
            for (int tileX = 0; tileX < tileColumns_; ++tileX) {
                const int x = tileX * tileSide_, y = tileY * tileSide_;
                Tile tile;
                tile.bounds = cv::Rect(x, y, std::min(tileSide_, scores_.cols - x),
                    std::min(tileSide_, scores_.rows - y));
                refresh(tile);
                tiles_.push_back(tile);
            }
        }
        leafBase_ = 1;
        while (leafBase_ < tiles_.size()) leafBase_ *= 2;
        winners_.assign(2 * leafBase_, noTile());
        for (size_t i = 0; i < tiles_.size(); ++i) winners_[leafBase_ + i] = i;
        for (size_t node = leafBase_ - 1; node > 0; --node)
            winners_[node] = preferred(winners_[2 * node], winners_[2 * node + 1]);
    }

    Peak maximum() const
    {
        // The tournament uses the same global row-major tie rule as the
        // original linear scan. Empty padding leaves never defeat real tiles.
        return tiles_[winners_[1]].peak;
    }

    void suppressSquare(cv::Point center, int radius)
    {
        CV_Assert(radius >= 0);
        // cv::rectangle(center-radius, center+radius, FILLED) includes both
        // endpoints. Convert that inclusive rectangle to a clipped half-open
        // ROI; widen arithmetic before adding so even large radii are safe.
        const int x0 = static_cast<int>(std::clamp<int64_t>(
            static_cast<int64_t>(center.x) - radius, 0, scores_.cols));
        const int y0 = static_cast<int>(std::clamp<int64_t>(
            static_cast<int64_t>(center.y) - radius, 0, scores_.rows));
        const int x1 = static_cast<int>(std::clamp<int64_t>(
            static_cast<int64_t>(center.x) + radius + 1, 0, scores_.cols));
        const int y1 = static_cast<int>(std::clamp<int64_t>(
            static_cast<int64_t>(center.y) + radius + 1, 0, scores_.rows));
        if (x0 >= x1 || y0 >= y1) return;
        scores_(cv::Rect(x0, y0, x1 - x0, y1 - y0)).setTo(0.0f);

        const cv::Rect zeroed(x0, y0, x1 - x0, y1 - y0);
        for (int tileY = y0 / tileSide_; tileY <= (y1 - 1) / tileSide_; ++tileY) {
            for (int tileX = x0 / tileSide_; tileX <= (x1 - 1) / tileSide_; ++tileX) {
                const size_t index = static_cast<size_t>(tileY) * tileColumns_ + tileX;
                Tile& tile = tiles_[index];
                // Writing zero cannot change a surviving strictly positive
                // first maximum. Zero/negative peaks still need a refresh:
                // zeroing negative values may introduce an earlier zero tie.
                if (tile.peak.value > 0 && !zeroed.contains(tile.peak.location)) continue;
                refresh(tile);
                updateWinner(index);
            }
        }
    }

private:
    struct Tile {
        cv::Rect bounds;
        Peak peak;
    };

    static bool before(cv::Point a, cv::Point b)
    {
        return a.y < b.y || (a.y == b.y && a.x < b.x);
    }

    void refresh(Tile& tile)
    {
        cv::Point local;
        cv::minMaxLoc(scores_(tile.bounds), nullptr, &tile.peak.value, nullptr, &local);
        tile.peak.location = local + tile.bounds.tl();
    }

    static constexpr size_t noTile() { return std::numeric_limits<size_t>::max(); }

    size_t preferred(size_t first, size_t second) const
    {
        if (first == noTile()) return second;
        if (second == noTile()) return first;
        const Peak& a = tiles_[first].peak;
        const Peak& b = tiles_[second].peak;
        return b.value > a.value || (b.value == a.value && before(b.location, a.location))
            ? second : first;
    }

    void updateWinner(size_t tile)
    {
        // Always propagate to the root even when a node keeps the same tile
        // index: that tile's value/location may have changed beneath the node.
        for (size_t node = (leafBase_ + tile) / 2; node > 0; node /= 2)
            winners_[node] = preferred(winners_[2 * node], winners_[2 * node + 1]);
    }

    cv::Mat scores_;
    int tileSide_ = 16;
    int tileColumns_ = 0;
    std::vector<Tile> tiles_;
    size_t leafBase_ = 1;
    std::vector<size_t> winners_;
};

} // namespace visiondemo_research
