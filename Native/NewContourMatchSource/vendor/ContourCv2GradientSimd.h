#pragma once

#include "ContourVmRecoveredMath.h"
#include <algorithm>
#include <cmath>

#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
#include <emmintrin.h>
#endif

namespace contourcv2_detail
{
// Raw Sobel derivatives fit in [-1020,1020]. SSE2 therefore performs the same
// float products, sum, square root and divisions as the scalar field builder,
// without reciprocal-square-root approximation or fused multiply-add.
inline void normalizeGradientRow(const short *dx, const short *dy, float *nx, float *ny, short *amplitude,
                                 float *magnitude, unsigned char *coarseAmplitude, int width)
{
    int x = 0;
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
    const __m128 zero = _mm_setzero_ps();
    const __m128 one = _mm_set1_ps(1.0f);
    const __m128 amplitudeScale = _mm_set1_ps(contourvm_detail::amplitudeScale);
    for (; x + 8 <= width; x += 8)
    {
        const __m128i rawX = _mm_loadu_si128(reinterpret_cast<const __m128i *>(dx + x));
        const __m128i rawY = _mm_loadu_si128(reinterpret_cast<const __m128i *>(dy + x));
        const __m128i signX = _mm_srai_epi16(rawX, 15);
        const __m128i signY = _mm_srai_epi16(rawY, 15);
        const __m128 gx0 = _mm_cvtepi32_ps(_mm_unpacklo_epi16(rawX, signX));
        const __m128 gx1 = _mm_cvtepi32_ps(_mm_unpackhi_epi16(rawX, signX));
        const __m128 gy0 = _mm_cvtepi32_ps(_mm_unpacklo_epi16(rawY, signY));
        const __m128 gy1 = _mm_cvtepi32_ps(_mm_unpackhi_epi16(rawY, signY));
        const __m128 length0 = _mm_sqrt_ps(_mm_add_ps(_mm_mul_ps(gx0, gx0), _mm_mul_ps(gy0, gy0)));
        const __m128 length1 = _mm_sqrt_ps(_mm_add_ps(_mm_mul_ps(gx1, gx1), _mm_mul_ps(gy1, gy1)));
        if (nx || ny)
        {
            const __m128 nonzero0 = _mm_cmpgt_ps(length0, zero);
            const __m128 nonzero1 = _mm_cmpgt_ps(length1, zero);
            const __m128 divisor0 = _mm_or_ps(_mm_and_ps(nonzero0, length0), _mm_andnot_ps(nonzero0, one));
            const __m128 divisor1 = _mm_or_ps(_mm_and_ps(nonzero1, length1), _mm_andnot_ps(nonzero1, one));
            if (nx)
            {
                _mm_storeu_ps(nx + x, _mm_and_ps(nonzero0, _mm_div_ps(gx0, divisor0)));
                _mm_storeu_ps(nx + x + 4, _mm_and_ps(nonzero1, _mm_div_ps(gx1, divisor1)));
            }
            if (ny)
            {
                _mm_storeu_ps(ny + x, _mm_and_ps(nonzero0, _mm_div_ps(gy0, divisor0)));
                _mm_storeu_ps(ny + x + 4, _mm_and_ps(nonzero1, _mm_div_ps(gy1, divisor1)));
            }
        }
        if (amplitude)
        {
            const __m128i absX = _mm_sub_epi16(_mm_xor_si128(rawX, signX), signX);
            const __m128i absY = _mm_sub_epi16(_mm_xor_si128(rawY, signY), signY);
            _mm_storeu_si128(reinterpret_cast<__m128i *>(amplitude + x), _mm_add_epi16(absX, absY));
        }
        if (magnitude)
        {
            _mm_storeu_ps(magnitude + x, length0);
            _mm_storeu_ps(magnitude + x + 4, length1);
        }
        if (coarseAmplitude)
        {
            const __m128i coarse0 = _mm_cvttps_epi32(_mm_mul_ps(length0, amplitudeScale));
            const __m128i coarse1 = _mm_cvttps_epi32(_mm_mul_ps(length1, amplitudeScale));
            // Signed Sobel magnitude fits in a short; unsigned-byte saturation
            // exactly implements min(255, int(length * amplitudeScale)).
            const __m128i coarse16 = _mm_packs_epi32(coarse0, coarse1);
            const __m128i coarse8 = _mm_packus_epi16(coarse16, _mm_setzero_si128());
            _mm_storel_epi64(reinterpret_cast<__m128i *>(coarseAmplitude + x), coarse8);
        }
    }
#endif
    for (; x < width; ++x)
    {
        const float gx = dx[x], gy = dy[x], length = std::sqrt(gx * gx + gy * gy);
        if (nx)
            nx[x] = length > 0 ? gx / length : 0;
        if (ny)
            ny[x] = length > 0 ? gy / length : 0;
        if (amplitude)
            amplitude[x] = static_cast<short>(std::abs(dx[x]) + std::abs(dy[x]));
        if (magnitude)
            magnitude[x] = length;
        if (coarseAmplitude)
            coarseAmplitude[x] =
                static_cast<unsigned char>(std::min(255, int(length * contourvm_detail::amplitudeScale)));
    }
}

// The caller supplies a 3x3 dilation of the retained Canny seeds so every
// magnitude sampled by a quadratic facet is needed. Entire empty SIMD groups
// avoid gradient loads and float writes; occupied groups use the same exact
// products, sum and square root as normalizeGradientRow.
inline void sparseGradientMagnitudeRow(const short *dx, const short *dy, float *magnitude,
                                       const unsigned char *needed, int width)
{
    int x = 0;
#if defined(_M_X64) || defined(_M_IX86) || defined(__x86_64__) || defined(__i386__)
    const __m128i zero = _mm_setzero_si128();
    for (; x + 8 <= width; x += 8)
    {
        const __m128i mask = _mm_loadl_epi64(reinterpret_cast<const __m128i *>(needed + x));
        if ((_mm_movemask_epi8(_mm_cmpeq_epi8(mask, zero)) & 0xff) == 0xff)
            continue;
        const __m128i rawX = _mm_loadu_si128(reinterpret_cast<const __m128i *>(dx + x));
        const __m128i rawY = _mm_loadu_si128(reinterpret_cast<const __m128i *>(dy + x));
        const __m128i signX = _mm_srai_epi16(rawX, 15);
        const __m128i signY = _mm_srai_epi16(rawY, 15);
        const __m128 gx0 = _mm_cvtepi32_ps(_mm_unpacklo_epi16(rawX, signX));
        const __m128 gx1 = _mm_cvtepi32_ps(_mm_unpackhi_epi16(rawX, signX));
        const __m128 gy0 = _mm_cvtepi32_ps(_mm_unpacklo_epi16(rawY, signY));
        const __m128 gy1 = _mm_cvtepi32_ps(_mm_unpackhi_epi16(rawY, signY));
        _mm_storeu_ps(magnitude + x, _mm_sqrt_ps(_mm_add_ps(_mm_mul_ps(gx0, gx0), _mm_mul_ps(gy0, gy0))));
        _mm_storeu_ps(magnitude + x + 4, _mm_sqrt_ps(_mm_add_ps(_mm_mul_ps(gx1, gx1), _mm_mul_ps(gy1, gy1))));
    }
#endif
    for (; x < width; ++x)
        if (needed[x])
        {
            const float gx = dx[x], gy = dy[x];
            magnitude[x] = std::sqrt(gx * gx + gy * gy);
        }
}
} // namespace contourcv2_detail
