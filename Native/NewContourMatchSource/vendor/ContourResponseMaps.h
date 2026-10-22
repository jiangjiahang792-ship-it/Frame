#pragma once

#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>
#include <algorithm>
#include <array>
#include <cmath>
#include <limits>
#include <utility>

namespace contourcv_detail {

// The direction label image uses 0..11 (ignore polarity), or 0..23 (use
// polarity); all other labels, including background 255, have zero response.
// Keep the original arithmetic and OpenCV operations: this helper only caches
// immutable LUTs and schedules independent direction maps concurrently.
class DirectionResponseMaps final {
public:
    static constexpr int directionCount = 12;
    using Maps = std::array<cv::Mat, directionCount * 2>;

    static void build(const cv::Mat& labels, bool usePolarity, Maps& response,
                      bool parallel = true)
    {
        CV_Assert(!labels.empty() && labels.type() == CV_8UC1);
        // Initialize outside the parallel region. Function-local const storage
        // is initialized once, with C++11's thread-safe initialization guarantee.
        const LookupTables& tables = lookupTables();
        const Maps& lut = usePolarity ? tables.polarity : tables.unsignedDirection;
        const int count = usePolarity ? directionCount * 2 : directionCount;
        Maps generated;
        // A regular shared arena permits four-lane byte-addressed gathers.
        // Four zero padding bytes on EVERY row keep the final two-pixel sample
        // safely readable as a 32-bit word. Restrict offsets to signed i32;
        // oversized layouts retain independent planes and the scalar-load path.
        constexpr int padding = 4;
        const size_t limit = static_cast<size_t>(std::numeric_limits<int>::max());
        const bool packed = labels.cols <= std::numeric_limits<int>::max() - padding
            && static_cast<size_t>(labels.rows) <= limit / count
            && static_cast<size_t>(labels.rows) * count <= limit / (static_cast<size_t>(labels.cols) + padding);
        cv::Mat arena;
        if (packed) {
            arena = cv::Mat(labels.rows * count, labels.cols + padding, CV_8UC1, cv::Scalar(0));
            for (int bin = 0; bin < count; ++bin)
                generated[bin] = arena(cv::Rect(0, bin * labels.rows, labels.cols, labels.rows));
        }
        const auto generate = [&](const cv::Range& range) {
            for (int bin = range.start; bin < range.end; ++bin) {
                cv::Mat exact, filtered;
                cv::LUT(labels, lut[bin], exact);
                cv::dilate(exact, filtered, cv::Mat());
                // Filtering remains on an independent image, never an arena
                // ROI: OpenCV must not see padding or adjacent direction planes.
                cv::GaussianBlur(filtered, filtered, cv::Size(3, 3), 0.65);
                if (packed) filtered.copyTo(generated[bin]);
                else generated[bin] = std::move(filtered);
            }
        };
        if (parallel) {
            // OpenCV 4.9's parallel_for_ serializes nested OpenCV parallel
            // regions, so filters inside this body do not open another OpenCV
            // worker team. Do not call setNumThreads here: it is global and
            // explicitly not thread-safe. Backend-internal/vendor threading
            // still needs checking in the deployed OpenCV build.
            // Source: opencv/4.9.0/modules/core/src/parallel.cpp, parallel_for_.
            cv::parallel_for_(cv::Range(0, count), generate, count);
        } else {
            generate(cv::Range(0, count));
        }
        // Local output pixel rectangles cannot overlap the input or each other, even when a
        // caller reuses/aliases old response matrices. Inactive maps are empty.
        response = std::move(generated);
    }

private:
    struct LookupTables {
        Maps unsignedDirection;
        Maps polarity;
        LookupTables()
        {
            constexpr double pi = 3.14159265358979323846;
            for (int usePolarity = 0; usePolarity < 2; ++usePolarity) {
                Maps& maps = usePolarity ? polarity : unsignedDirection;
                const int count = usePolarity ? 2 * directionCount : directionCount;
                for (int bin = 0; bin < count; ++bin) {
                    maps[bin] = cv::Mat(1, 256, CV_8U, cv::Scalar(0));
                    for (int found = 0; found < count; ++found) {
                        const double cosine = std::cos((found - bin) * pi / directionCount);
                        const double similarity = usePolarity ? std::max(0.0, cosine) : std::abs(cosine);
                        maps[bin].at<uchar>(found) = static_cast<uchar>(cvRound(255 * std::pow(similarity, 4)));
                    }
                }
            }
        }
    };

    static const LookupTables& lookupTables()
    {
        static const LookupTables tables;
        return tables;
    }
};

} // namespace contourcv_detail
