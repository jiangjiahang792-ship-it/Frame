#include "ContourGradientScore.h"

#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
#ifndef __AVX2__
#error Compile only ContourGradientScoreAvx2.cpp with /arch:AVX2 and /fp:strict /GL-.
#endif
#include <immintrin.h>
#include <algorithm>
#include <cmath>

namespace contourcv_score {
namespace {
__m256d interpolate(const float* data, __m128i offsets, int stride, __m128 mask,
                    __m256d fx, __m256d fy, __m256d inverseX, __m256d inverseY)
{
    const __m128 zero = _mm_setzero_ps();
    const __m128i one = _mm_set1_epi32(1), pitch = _mm_set1_epi32(stride);
    // Masked gathers suppress memory access for invalid coordinates. The last
    // valid interpolation cell reads exactly four floats, never a wider block.
    const __m256d a = _mm256_cvtps_pd(_mm_mask_i32gather_ps(zero, data, offsets, mask, 4));
    const __m256d b = _mm256_cvtps_pd(_mm_mask_i32gather_ps(zero, data, _mm_add_epi32(offsets, one), mask, 4));
    const __m256d c = _mm256_cvtps_pd(_mm_mask_i32gather_ps(zero, data, _mm_add_epi32(offsets, pitch), mask, 4));
    const __m256d d = _mm256_cvtps_pd(_mm_mask_i32gather_ps(zero, data,
        _mm_add_epi32(_mm_add_epi32(offsets, pitch), one), mask, 4));
    const __m256d top = _mm256_add_pd(_mm256_mul_pd(inverseX, a), _mm256_mul_pd(fx, b));
    const __m256d bottom = _mm256_add_pd(_mm256_mul_pd(inverseX, c), _mm256_mul_pd(fx, d));
    const __m256d result = _mm256_add_pd(_mm256_mul_pd(inverseY, top), _mm256_mul_pd(fy, bottom));
    // Original bilinear returns float despite its double arithmetic.
    return _mm256_cvtps_pd(_mm256_cvtpd_ps(result));
}
}
namespace detail {
#if defined(_MSC_VER)
__declspec(noinline)
#elif defined(__GNUC__) || defined(__clang__)
__attribute__((noinline))
#endif
double scoreGradientAvx2(const GradientView& view, const FeatureSoA& features,
                         const ScorePose& pose, double requestedCutoff)
{
    const double cosine = std::cos(pose.angle), sine = std::sin(pose.angle);
    const auto cutoff = makeGradientCutoff(features.size(), pose, requestedCutoff);
    const __m256d c = _mm256_set1_pd(cosine), s = _mm256_set1_pd(sine);
    const __m256d sx = _mm256_set1_pd(pose.sx), sy = _mm256_set1_pd(pose.sy);
    const __m256d csx = _mm256_set1_pd(cosine * pose.sx), ssy = _mm256_set1_pd(sine * pose.sy);
    const __m256d ssx = _mm256_set1_pd(sine * pose.sx), csy = _mm256_set1_pd(cosine * pose.sy);
    const __m256d tx = _mm256_set1_pd(pose.x), ty = _mm256_set1_pd(pose.y);
    const __m256d ox = _mm256_set1_pd(view.originX), oy = _mm256_set1_pd(view.originY);
    const __m256d zero = _mm256_setzero_pd(), one = _mm256_set1_pd(1);
    const __m256d lastX = _mm256_set1_pd(view.cols - 1.0), lastY = _mm256_set1_pd(view.rows - 1.0);
    const __m128i strideX = _mm_set1_epi32(static_cast<int>(view.strideX));
    const __m128i strideY = _mm_set1_epi32(static_cast<int>(view.strideY));
    double score = 0;
    std::size_t i = 0;
    for (; i + 4 <= features.size(); i += 4) {
        const __m256d px = _mm256_loadu_pd(features.px.data() + i), py = _mm256_loadu_pd(features.py.data() + i);
        const __m256d x = _mm256_sub_pd(_mm256_sub_pd(
            _mm256_add_pd(tx, _mm256_mul_pd(csx, px)), _mm256_mul_pd(ssy, py)), ox);
        const __m256d y = _mm256_sub_pd(_mm256_add_pd(
            _mm256_add_pd(ty, _mm256_mul_pd(ssx, px)), _mm256_mul_pd(csy, py)), oy);
        const __m256d nx = _mm256_div_pd(_mm256_loadu_pd(features.nx.data() + i), sx);
        const __m256d ny = _mm256_div_pd(_mm256_loadu_pd(features.ny.data() + i), sy);
        const __m256d norm = _mm256_sqrt_pd(_mm256_add_pd(_mm256_mul_pd(nx, nx), _mm256_mul_pd(ny, ny)));
        const __m256d normalX = _mm256_div_pd(_mm256_sub_pd(_mm256_mul_pd(c, nx), _mm256_mul_pd(s, ny)), norm);
        const __m256d normalY = _mm256_div_pd(_mm256_add_pd(_mm256_mul_pd(s, nx), _mm256_mul_pd(c, ny)), norm);
        const __m256d valid = _mm256_and_pd(_mm256_and_pd(
            _mm256_cmp_pd(x, zero, _CMP_GE_OQ), _mm256_cmp_pd(x, lastX, _CMP_LT_OQ)),
            _mm256_and_pd(_mm256_cmp_pd(y, zero, _CMP_GE_OQ), _mm256_cmp_pd(y, lastY, _CMP_LT_OQ)));
        const int validBits = _mm256_movemask_pd(valid);
        // Sanitizing before conversion also handles huge/NaN coordinates without
        // overflowing an int or forming an invalid active gather address.
        const __m256d safeX = _mm256_and_pd(valid, x), safeY = _mm256_and_pd(valid, y);
        const __m128i ix = _mm256_cvttpd_epi32(safeX), iy = _mm256_cvttpd_epi32(safeY);
        const __m256d fx = _mm256_sub_pd(safeX, _mm256_cvtepi32_pd(ix));
        const __m256d fy = _mm256_sub_pd(safeY, _mm256_cvtepi32_pd(iy));
        const __m256d inverseX = _mm256_sub_pd(one, fx), inverseY = _mm256_sub_pd(one, fy);
        const __m128 mask = _mm_castsi128_ps(_mm_set_epi32(
            (validBits & 8) ? -1 : 0, (validBits & 4) ? -1 : 0,
            (validBits & 2) ? -1 : 0, (validBits & 1) ? -1 : 0));
        const __m128i offsetsX = _mm_add_epi32(_mm_mullo_epi32(iy, strideX), ix);
        const __m128i offsetsY = _mm_add_epi32(_mm_mullo_epi32(iy, strideY), ix);
        const __m256d gx = interpolate(view.gx, offsetsX, static_cast<int>(view.strideX), mask, fx, fy, inverseX, inverseY);
        const __m256d gy = interpolate(view.gy, offsetsY, static_cast<int>(view.strideY), mask, fx, fy, inverseX, inverseY);
        alignas(32) double sampleX[4], sampleY[4], directionX[4], directionY[4];
        _mm256_storeu_pd(sampleX, gx); _mm256_storeu_pd(sampleY, gy);
        _mm256_storeu_pd(directionX, normalX); _mm256_storeu_pd(directionY, normalY);
        // Keep the CRT hypot implementation and ordered scalar accumulation.
        // Replacing hypot by sqrt(gx*gx+gy*gy) can change threshold decisions.
        _mm256_zeroupper();
        for (int lane = 0; lane < 4; ++lane) {
            if (!(validBits & (1 << lane))) continue;
            const double magnitude = std::hypot(sampleX[lane], sampleY[lane]);
            if (magnitude < view.minContrast) continue;
            const double dot = (directionX[lane] * sampleX[lane] + directionY[lane] * sampleY[lane]) / magnitude;
            const double alignment = view.usePolarity ? std::max(0.0, dot) : std::abs(dot);
            const double squared = alignment * alignment, fourth = squared * squared;
            score += fourth * fourth;
        }
        if (cutoff.reject(score, i + 4)) return 0;
    }
    _mm256_zeroupper();
    if (i != features.size())
        score = accumulateGradientScalar(view, features, pose, cosine, sine, i, score, cutoff);
    return score / features.size();
}
} // namespace detail
} // namespace contourcv_score
#endif
