#pragma once

#include <algorithm>
#include <opencv2/core.hpp>
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
#include <emmintrin.h>
#endif

namespace contourcv2_detail
{
// Exact simultaneous 3x3 Sobel, scale=1, delta=0, BORDER_REPLICATE.
// Input rows may be strided. Like cv::Sobel without BORDER_ISOLATED, a submatrix
// samples its parent outside the ROI and replicates at the parent boundary.
// Output buffers must be distinct; existing CV_16S output ROIs are retained by
// Mat::create. Interior vector loads do not cross a logical ROI row.
inline void sobelPair(const cv::Mat &gray, cv::Mat &dx, cv::Mat &dy)
{
    CV_Assert(!gray.empty() && gray.type() == CV_8UC1 && &dx != &dy);
    // Retain the input header/data when a caller reuses its input Mat as a
    // differently typed output (Mat::create will allocate a separate buffer).
    const cv::Mat source = gray;
    cv::Size wholeSize;
    cv::Point offset;
    source.locateROI(wholeSize, offset);
    dx.create(source.size(), CV_16S);
    dy.create(source.size(), CV_16S);
    CV_Assert(dx.data != dy.data);
    cv::parallel_for_(cv::Range(0, source.rows), [&](const cv::Range &range) {
        for (int y = range.start; y < range.end; ++y)
        {
            const int parentY = y + offset.y;
            const auto parentRow = [&](int row) {
                return source.datastart +
                       std::size_t(std::clamp(row, 0, wholeSize.height - 1)) * source.step[0] + offset.x;
            };
            const auto *top = parentRow(parentY - 1);
            const auto *middle = parentRow(parentY);
            const auto *bottom = parentRow(parentY + 1);
            auto *targetX = dx.ptr<short>(y);
            auto *targetY = dy.ptr<short>(y);
            const auto scalar = [&](int x) {
                const int left = std::max(-offset.x, x - 1);
                const int right = std::min(wholeSize.width - 1 - offset.x, x + 1);
                targetX[x] =
                    static_cast<short>(int(top[right]) - top[left] + 2 * (int(middle[right]) - middle[left]) +
                                       int(bottom[right]) - bottom[left]);
                targetY[x] =
                    static_cast<short>(int(bottom[left]) - top[left] + 2 * (int(bottom[x]) - top[x]) +
                                       int(bottom[right]) - top[right]);
            };
            scalar(0);
            int x = 1;
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
            const __m128i zero = _mm_setzero_si128();
            const auto load = [](const uchar *p) {
                return _mm_loadu_si128(reinterpret_cast<const __m128i *>(p));
            };
            const auto half = [](const __m128i tl, const __m128i tm, const __m128i tr, const __m128i ml,
                                 const __m128i mr, const __m128i bl, const __m128i bm, const __m128i br,
                                 __m128i &gx, __m128i &gy) {
                gx = _mm_add_epi16(
                    _mm_add_epi16(_mm_sub_epi16(tr, tl), _mm_slli_epi16(_mm_sub_epi16(mr, ml), 1)),
                    _mm_sub_epi16(br, bl));
                gy = _mm_add_epi16(
                    _mm_add_epi16(_mm_sub_epi16(bl, tl), _mm_slli_epi16(_mm_sub_epi16(bm, tm), 1)),
                    _mm_sub_epi16(br, tr));
            };
            // Sixteen interior pixels require source neighbours through x+16.
            // The center of the middle row has zero coefficient in both kernels.
            for (; x + 16 < source.cols; x += 16)
            {
                const auto tl = load(top + x - 1), tm = load(top + x), tr = load(top + x + 1);
                const auto ml = load(middle + x - 1), mr = load(middle + x + 1);
                const auto bl = load(bottom + x - 1), bm = load(bottom + x), br = load(bottom + x + 1);
                __m128i gx, gy;
                half(_mm_unpacklo_epi8(tl, zero), _mm_unpacklo_epi8(tm, zero), _mm_unpacklo_epi8(tr, zero),
                     _mm_unpacklo_epi8(ml, zero), _mm_unpacklo_epi8(mr, zero), _mm_unpacklo_epi8(bl, zero),
                     _mm_unpacklo_epi8(bm, zero), _mm_unpacklo_epi8(br, zero), gx, gy);
                _mm_storeu_si128(reinterpret_cast<__m128i *>(targetX + x), gx);
                _mm_storeu_si128(reinterpret_cast<__m128i *>(targetY + x), gy);
                half(_mm_unpackhi_epi8(tl, zero), _mm_unpackhi_epi8(tm, zero), _mm_unpackhi_epi8(tr, zero),
                     _mm_unpackhi_epi8(ml, zero), _mm_unpackhi_epi8(mr, zero), _mm_unpackhi_epi8(bl, zero),
                     _mm_unpackhi_epi8(bm, zero), _mm_unpackhi_epi8(br, zero), gx, gy);
                _mm_storeu_si128(reinterpret_cast<__m128i *>(targetX + x + 8), gx);
                _mm_storeu_si128(reinterpret_cast<__m128i *>(targetY + x + 8), gy);
            }
#endif
            for (; x < source.cols - 1; ++x)
                scalar(x);
            if (source.cols > 1)
                scalar(source.cols - 1);
        }
    });
}
} // namespace contourcv2_detail
