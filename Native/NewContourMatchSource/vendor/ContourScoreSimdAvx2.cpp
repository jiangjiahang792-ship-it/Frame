#include "ContourScoreSimd.h"

#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
#ifndef __AVX2__
#error Compile only ContourScoreSimdAvx2.cpp with /arch:AVX2 (or -mavx2), not the whole application.
#endif

#include <immintrin.h>
#include <cmath>
#include <cstdint>
#include <cstring>

// /fp:strict on this source prevents FMA contraction in v142 as well as
// reassociation. /GL- keeps its instructions out of baseline dispatch/scalar TUs.
namespace contourcv_score {
namespace {
__m256d directionBins(__m256d normalX, __m256d normalY, bool usePolarity)
{
    const __m256d sign = _mm256_set1_pd(-0.0);
    const __m256d x = _mm256_andnot_pd(sign, normalX);
    const __m256d y = _mm256_andnot_pd(sign, normalY);
    const double thresholds[6] = {0.131652497587, 0.414213562373, 0.767326987979,
                                 1.303225372841, 2.414213562373, 7.595754112725};
    __m256d bins = _mm256_set1_pd(6);
    // Reverse selection exactly reproduces the scalar first strict-< match.
    for (int bin = 5; bin >= 0; --bin) {
        const __m256d below = _mm256_cmp_pd(y,
            _mm256_mul_pd(x, _mm256_set1_pd(thresholds[bin])), _CMP_LT_OQ);
        bins = _mm256_blendv_pd(bins, _mm256_set1_pd(double(bin)), below);
    }
    const __m256d zero = _mm256_setzero_pd();
    const __m256d positiveX = _mm256_cmp_pd(normalX, zero, _CMP_GE_OQ);
    const __m256d positiveY = _mm256_cmp_pd(normalY, zero, _CMP_GE_OQ);
    const __m256d right = _mm256_blendv_pd(_mm256_sub_pd(_mm256_set1_pd(24), bins), bins, positiveY);
    const __m256d left = _mm256_blendv_pd(_mm256_add_pd(_mm256_set1_pd(12), bins),
        _mm256_sub_pd(_mm256_set1_pd(12), bins), positiveY);
    bins = _mm256_blendv_pd(left, right, positiveX);
    const __m256d modulo = _mm256_set1_pd(usePolarity ? 24 : 12);
    // All bins are in [0,24], so one subtraction is enough for polarity and
    // two for the unsigned zero-angle wrap at 24.
    bins = _mm256_blendv_pd(bins, _mm256_sub_pd(bins, modulo),
        _mm256_cmp_pd(bins, modulo, _CMP_GE_OQ));
    if (!usePolarity)
        bins = _mm256_blendv_pd(bins, _mm256_sub_pd(bins, modulo),
            _mm256_cmp_pd(bins, modulo, _CMP_GE_OQ));
    return bins;
}

#if defined(_MSC_VER)
__forceinline
#elif defined(__GNUC__) || defined(__clang__)
__attribute__((always_inline)) inline
#endif
__m256d sampleResponse4(const ResponseView& response, __m256d x, __m256d y, __m256d bins)
{
    const __m256d zero = _mm256_setzero_pd(), one = _mm256_set1_pd(1);
    const __m256d lastX = _mm256_set1_pd(response.cols - 1.0), lastY = _mm256_set1_pd(response.rows - 1.0);
    const __m256d divisor = _mm256_set1_pd(255);
    const __m256d valid = _mm256_and_pd(_mm256_and_pd(
        _mm256_cmp_pd(x, zero, _CMP_GE_OQ), _mm256_cmp_pd(x, lastX, _CMP_LT_OQ)),
        _mm256_and_pd(_mm256_cmp_pd(y, zero, _CMP_GE_OQ), _mm256_cmp_pd(y, lastY, _CMP_LT_OQ)));
    const __m256d floorX = _mm256_floor_pd(x), floorY = _mm256_floor_pd(y);
    const __m128i pixelX = _mm256_cvttpd_epi32(floorX), pixelY = _mm256_cvttpd_epi32(floorY);
    const __m128i bank = _mm256_cvttpd_epi32(bins);
    __m128i topBytes, bottomBytes;
    if (response.packedBase) {
        // Extract one complete 32-bit all-ones/zero mask per double lane.
        // Invalid coordinates may produce arbitrary integer offsets, but the
        // masked gather never accesses those lanes. Verified valid offsets and
        // each full four-byte read lie inside the single padded allocation.
        const __m128i mask = _mm256_castsi256_si128(_mm256_permutevar8x32_epi32(
            _mm256_castpd_si256(valid), _mm256_setr_epi32(0, 2, 4, 6, 0, 2, 4, 6)));
        const __m128i rowStride = _mm_set1_epi32(response.packedRowStride);
        const __m128i offsets = _mm_add_epi32(_mm_add_epi32(
            _mm_mullo_epi32(bank, _mm_set1_epi32(response.packedPlaneStride)),
            _mm_mullo_epi32(pixelY, rowStride)), pixelX);
        const int* base = reinterpret_cast<const int*>(response.packedBase);
        const __m128i pairMask = _mm_set1_epi32(0xffff);
        topBytes = _mm_and_si128(_mm_mask_i32gather_epi32(_mm_setzero_si128(), base,
            offsets, mask, 1), pairMask);
        bottomBytes = _mm_and_si128(_mm_mask_i32gather_epi32(_mm_setzero_si128(), base,
            _mm_add_epi32(offsets, rowStride), mask, 1), pairMask);
    } else {
        const int validBits = _mm256_movemask_pd(valid);
        alignas(16) int ix[4], iy[4], bin[4];
        _mm_storeu_si128(reinterpret_cast<__m128i*>(ix), pixelX);
        _mm_storeu_si128(reinterpret_cast<__m128i*>(iy), pixelY);
        _mm_storeu_si128(reinterpret_cast<__m128i*>(bin), bank);
        alignas(16) std::uint32_t topPair[4] = {}, bottomPair[4] = {};
        for (int lane = 0; lane < 4; ++lane) {
            // Load exactly the two legal neighbouring bytes, including at the
            // final row. No padding is assumed for an arbitrary response view.
            if (!(validBits & (1 << lane)) || !response.data[bin[lane]]) continue;
            const auto stride = response.rowStride[bin[lane]];
            const unsigned char* row = response.data[bin[lane]] + std::size_t(iy[lane]) * stride + ix[lane];
            const unsigned char* next = row + stride;
            std::uint16_t topValue, bottomValue;
            std::memcpy(&topValue, row, sizeof(topValue));
            std::memcpy(&bottomValue, next, sizeof(bottomValue));
            topPair[lane] = topValue; bottomPair[lane] = bottomValue;
        }
        topBytes = _mm_loadu_si128(reinterpret_cast<const __m128i*>(topPair));
        bottomBytes = _mm_loadu_si128(reinterpret_cast<const __m128i*>(bottomPair));
    }
    const __m128i byteMask = _mm_set1_epi32(255);
    const __m256d topLeft = _mm256_cvtepi32_pd(_mm_and_si128(topBytes, byteMask));
    const __m256d topRight = _mm256_cvtepi32_pd(_mm_srli_epi32(topBytes, 8));
    const __m256d bottomLeft = _mm256_cvtepi32_pd(_mm_and_si128(bottomBytes, byteMask));
    const __m256d bottomRight = _mm256_cvtepi32_pd(_mm_srli_epi32(bottomBytes, 8));
    const __m256d fx = _mm256_sub_pd(x, floorX), fy = _mm256_sub_pd(y, floorY);
    const __m256d inverseX = _mm256_sub_pd(one, fx), inverseY = _mm256_sub_pd(one, fy);
    const __m256d top = _mm256_add_pd(_mm256_mul_pd(inverseX, topLeft), _mm256_mul_pd(fx, topRight));
    const __m256d bottom = _mm256_add_pd(_mm256_mul_pd(inverseX, bottomLeft), _mm256_mul_pd(fx, bottomRight));
    const __m256d interpolated = _mm256_div_pd(_mm256_add_pd(_mm256_mul_pd(inverseY, top),
                                                           _mm256_mul_pd(fy, bottom)), divisor);
    // The original bilinearResponse returns float, even though its inputs
    // and arithmetic are double. Preserve that round trip intentionally.
    const __m256d rounded = _mm256_and_pd(valid, _mm256_cvtps_pd(_mm256_cvtpd_ps(interpolated)));
    return rounded;
}
}

namespace detail {
#if defined(_MSC_VER)
__declspec(noinline)
#elif defined(__GNUC__) || defined(__clang__)
__attribute__((noinline))
#endif
void prepareResponseAvx2(const FeatureSoA& features, double cosine, double sine,
                         double scaleX, double scaleY, bool usePolarity, PreparedResponse& output)
{
    const __m256d c = _mm256_set1_pd(cosine), s = _mm256_set1_pd(sine);
    const __m256d sx = _mm256_set1_pd(scaleX), sy = _mm256_set1_pd(scaleY);
    // Preserve (cosine*scale)*coordinate, and divide the input normals before
    // rotation. No reciprocal approximation, reassociation or FMA is used.
    const __m256d csx = _mm256_set1_pd(cosine * scaleX), ssy = _mm256_set1_pd(sine * scaleY);
    const __m256d ssx = _mm256_set1_pd(sine * scaleX), csy = _mm256_set1_pd(cosine * scaleY);
    std::size_t i = 0;
    for (; i + 4 <= features.size(); i += 4) {
        const __m256d px = _mm256_loadu_pd(features.px.data() + i);
        const __m256d py = _mm256_loadu_pd(features.py.data() + i);
        _mm256_storeu_pd(output.xA.data() + i, _mm256_mul_pd(csx, px));
        _mm256_storeu_pd(output.xB.data() + i, _mm256_mul_pd(ssy, py));
        _mm256_storeu_pd(output.yA.data() + i, _mm256_mul_pd(ssx, px));
        _mm256_storeu_pd(output.yB.data() + i, _mm256_mul_pd(csy, py));
        const __m256d nx = _mm256_div_pd(_mm256_loadu_pd(features.nx.data() + i), sx);
        const __m256d ny = _mm256_div_pd(_mm256_loadu_pd(features.ny.data() + i), sy);
        const __m256d bins = directionBins(
            _mm256_sub_pd(_mm256_mul_pd(c, nx), _mm256_mul_pd(s, ny)),
            _mm256_add_pd(_mm256_mul_pd(s, nx), _mm256_mul_pd(c, ny)), usePolarity);
        _mm_storeu_si128(reinterpret_cast<__m128i*>(output.bin.data() + i), _mm256_cvttpd_epi32(bins));
    }
    _mm256_zeroupper();
    if (i != features.size())
        prepareResponseScalar(features, cosine, sine, scaleX, scaleY, usePolarity, output, i);
}
#if defined(_MSC_VER)
__declspec(noinline)
#elif defined(__GNUC__) || defined(__clang__)
__attribute__((noinline))
#endif
double scoreResponseAvx2(const ResponseView& response, const FeatureSoA& features,
                         const ScorePose& pose, double cutoff)
{
    const double cosine = std::cos(pose.angle), sine = std::sin(pose.angle);
    const __m256d c = _mm256_set1_pd(cosine), s = _mm256_set1_pd(sine);
    const __m256d sx = _mm256_set1_pd(pose.sx), sy = _mm256_set1_pd(pose.sy);
    const __m256d csx = _mm256_set1_pd(cosine * pose.sx), ssy = _mm256_set1_pd(sine * pose.sy);
    const __m256d ssx = _mm256_set1_pd(sine * pose.sx), csy = _mm256_set1_pd(cosine * pose.sy);
    const __m256d tx = _mm256_set1_pd(pose.x), ty = _mm256_set1_pd(pose.y);
    const __m256d originX = _mm256_set1_pd(response.originX), originY = _mm256_set1_pd(response.originY);
    const double cutoffSum = cutoff * features.size();
    const double margin = 1e-12 * features.size();
    double score = 0;
    std::size_t i = 0;
    for (; i + 4 <= features.size(); i += 4) {
        const __m256d px = _mm256_loadu_pd(features.px.data() + i);
        const __m256d py = _mm256_loadu_pd(features.py.data() + i);
        // Preserve original left-to-right grouping, including subtracting the
        // ROI origin after completing the transform in image coordinates.
        const __m256d x = _mm256_sub_pd(_mm256_sub_pd(
            _mm256_add_pd(tx, _mm256_mul_pd(csx, px)), _mm256_mul_pd(ssy, py)), originX);
        const __m256d y = _mm256_sub_pd(_mm256_add_pd(
            _mm256_add_pd(ty, _mm256_mul_pd(ssx, px)), _mm256_mul_pd(csy, py)), originY);
        const __m256d nx = _mm256_div_pd(_mm256_loadu_pd(features.nx.data() + i), sx);
        const __m256d ny = _mm256_div_pd(_mm256_loadu_pd(features.ny.data() + i), sy);
        const __m256d bins = directionBins(
            _mm256_sub_pd(_mm256_mul_pd(c, nx), _mm256_mul_pd(s, ny)),
            _mm256_add_pd(_mm256_mul_pd(s, nx), _mm256_mul_pd(c, ny)), response.usePolarity);
        const __m256d rounded = sampleResponse4(response, x, y, bins);
        alignas(32) double lanes[4];
        _mm256_storeu_pd(lanes, rounded);
        score += lanes[0]; score += lanes[1]; score += lanes[2]; score += lanes[3];
        const std::size_t processed = i + 4;
        if (cutoff > 0 && (processed & 31) == 0
            && score + double(features.size() - processed) + margin < cutoffSum) {
            _mm256_zeroupper();
            return 0;
        }
    }
    // Leave AVX state clean before entering baseline scalar code/CRT callers.
    _mm256_zeroupper();
    if (i != features.size())
        score = accumulateResponseScalar(response, features, pose, cosine, sine, i, score, cutoff);
    return score / features.size();
}
#if defined(_MSC_VER)
__declspec(noinline)
#elif defined(__GNUC__) || defined(__clang__)
__attribute__((noinline))
#endif
double scorePreparedAvx2(const ResponseView& response, const PreparedResponse& prepared,
                         double x, double y, double cutoff)
{
    const __m256d tx = _mm256_set1_pd(x), ty = _mm256_set1_pd(y);
    const __m256d originX = _mm256_set1_pd(response.originX), originY = _mm256_set1_pd(response.originY);
    const double cutoffSum = cutoff * prepared.size(), margin = 1e-12 * prepared.size();
    double score = 0;
    std::size_t i = 0;
    for (; i + 4 <= prepared.size(); i += 4) {
        const __m256d px = _mm256_sub_pd(_mm256_sub_pd(_mm256_add_pd(tx,
            _mm256_loadu_pd(prepared.xA.data() + i)), _mm256_loadu_pd(prepared.xB.data() + i)), originX);
        const __m256d py = _mm256_sub_pd(_mm256_add_pd(_mm256_add_pd(ty,
            _mm256_loadu_pd(prepared.yA.data() + i)), _mm256_loadu_pd(prepared.yB.data() + i)), originY);
        const __m256d bins = _mm256_cvtepi32_pd(_mm_loadu_si128(
            reinterpret_cast<const __m128i*>(prepared.bin.data() + i)));
        const __m256d rounded = sampleResponse4(response, px, py, bins);
        alignas(32) double lanes[4];
        _mm256_storeu_pd(lanes, rounded);
        score += lanes[0]; score += lanes[1]; score += lanes[2]; score += lanes[3];
        const std::size_t processed = i + 4;
        if (cutoff > 0 && (processed & 31) == 0
            && score + double(prepared.size() - processed) + margin < cutoffSum) {
            _mm256_zeroupper();
            return 0;
        }
    }
    _mm256_zeroupper();
    if (i != prepared.size())
        score = accumulatePreparedScalar(response, prepared, x, y, i, score, cutoff);
    return score / prepared.size();
}
} // namespace detail
} // namespace contourcv_score
#endif


#include "ContourCoarseBlocked.h"
namespace contourcv_detail {
bool coarseResponseBlockedAvx2(const std::array<cv::Mat,24>& response, cv::Size size,
    const std::vector<std::array<int,4>>& offsets, int sampleCount, cv::Mat& scores)
{
#if defined(_M_X64) || defined(__x86_64__)
    if(sampleCount<=0 || sampleCount>257 || size.width<32) return false;
    struct RowSample { const unsigned char* data; int dx,count; };
    std::vector<RowSample> samples; samples.reserve(offsets.size());
    scores.create(size,CV_32F);
    const __m256i zero=_mm256_setzero_si256();
    for(int y=0;y<size.height;++y) {
        samples.clear();
        for(const auto& offset:offsets) {
            const int sy=y+offset[1];
            if(sy>=0&&sy<size.height&&offset[0]>-size.width&&offset[0]<size.width)
                samples.push_back({response[offset[2]].ptr<unsigned char>(sy),offset[0],offset[3]});
        }
        float* dst=scores.ptr<float>(y);int x=0;
        for(;x+32<=size.width;x+=32) {
            __m256i lo=zero,hi=zero;
            for(const auto& row:samples) {
                const int sx=x+row.dx;__m256i bytes;
                if(sx>=0&&sx+32<=size.width) bytes=_mm256_loadu_si256(reinterpret_cast<const __m256i*>(row.data+sx));
                else {
                    const int first=std::max(0,-sx),end=std::min(32,size.width-sx);
                    if(end<=first) continue;
                    alignas(32) unsigned char partial[32]={};
                    std::memcpy(partial+first,row.data+sx+first,std::size_t(end-first));
                    bytes=_mm256_load_si256(reinterpret_cast<const __m256i*>(partial));
                }
                __m256i a=_mm256_cvtepu8_epi16(_mm256_castsi256_si128(bytes));
                __m256i b=_mm256_cvtepu8_epi16(_mm256_extracti128_si256(bytes,1));
                if(row.count!=1) {
                    const __m256i weight=_mm256_set1_epi16(static_cast<short>(row.count));
                    a=_mm256_mullo_epi16(a,weight);b=_mm256_mullo_epi16(b,weight);
                }
                lo=_mm256_add_epi16(lo,a);hi=_mm256_add_epi16(hi,b);
            }
            _mm256_storeu_ps(dst+x,_mm256_cvtepi32_ps(_mm256_cvtepu16_epi32(_mm256_castsi256_si128(lo))));
            _mm256_storeu_ps(dst+x+8,_mm256_cvtepi32_ps(_mm256_cvtepu16_epi32(_mm256_extracti128_si256(lo,1))));
            _mm256_storeu_ps(dst+x+16,_mm256_cvtepi32_ps(_mm256_cvtepu16_epi32(_mm256_castsi256_si128(hi))));
            _mm256_storeu_ps(dst+x+24,_mm256_cvtepi32_ps(_mm256_cvtepu16_epi32(_mm256_extracti128_si256(hi,1))));
        }
        for(;x<size.width;++x) {
            unsigned int sum=0;
            for(const auto& row:samples){const int sx=x+row.dx;if(sx>=0&&sx<size.width)sum+=row.data[sx]*row.count;}
            dst[x]=static_cast<float>(sum);
        }
    }
    _mm256_zeroupper();return true;
#else
    (void)response;(void)size;(void)offsets;(void)sampleCount;(void)scores;return false;
#endif
}
}