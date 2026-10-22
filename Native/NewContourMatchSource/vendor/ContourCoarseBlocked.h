#pragma once
#include <opencv2/core.hpp>
#include <algorithm>
#include <array>
#include <cstring>
#include <vector>
#if defined(_M_X64) || defined(__x86_64__)
#include <emmintrin.h>
#endif

namespace contourcv_detail {
bool coarseResponseBlockedAvx2(const std::array<cv::Mat,24>& response, cv::Size size,
    const std::vector<std::array<int,4>>& offsets, int sampleCount, cv::Mat& scores);
// Integer coarse response only: each input is u8, and <=257 contributions keep
// every partial sum <=65535. No rounding, saturation, pruning, or changed score
// denominator. SSE2 is baseline on x64; other targets use the caller's fallback.
inline bool coarseResponseBlocked(const std::array<cv::Mat,24>& response, cv::Size size,
    const std::vector<std::array<int,4>>& offsets, int sampleCount, cv::Mat& scores)
{
#if defined(_M_X64) || defined(__x86_64__)
    if(sampleCount<=0 || sampleCount>257 || size.width<16) return false;
    if(cv::checkHardwareSupport(CV_CPU_AVX2) && size.width>=32)
        return coarseResponseBlockedAvx2(response,size,offsets,sampleCount,scores);
    struct RowSample { const unsigned char* data; int dx, count; };
    std::vector<RowSample> rowSamples; rowSamples.reserve(offsets.size());
    scores.create(size,CV_32F);
    const __m128i zero=_mm_setzero_si128();
    for(int y=0;y<size.height;++y) {
        rowSamples.clear();
        for(const auto& offset:offsets) {
            const int sourceY=y+offset[1];
            if(sourceY>=0 && sourceY<size.height && offset[0]>-size.width && offset[0]<size.width)
                rowSamples.push_back({response[offset[2]].ptr<unsigned char>(sourceY),offset[0],offset[3]});
        }
        float* destination=scores.ptr<float>(y);
        int x=0;
        for(;x+16<=size.width;x+=16) {
            __m128i lo=zero,hi=zero;
            for(const RowSample& row:rowSamples) {
                const int sx=x+row.dx;
                __m128i bytes;
                if(sx>=0 && sx+16<=size.width) {
                    bytes=_mm_loadu_si128(reinterpret_cast<const __m128i*>(row.data+sx));
                } else {
                    const int first=std::max(0,-sx),end=std::min(16,size.width-sx);
                    if(end<=first) continue;
                    alignas(16) unsigned char partial[16]={};
                    // Copy only legal bytes. No row padding or tail overread.
                    std::memcpy(partial+first,row.data+sx+first,std::size_t(end-first));
                    bytes=_mm_load_si128(reinterpret_cast<const __m128i*>(partial));
                }
                __m128i lower=_mm_unpacklo_epi8(bytes,zero),upper=_mm_unpackhi_epi8(bytes,zero);
                if(row.count!=1) {
                    const __m128i weight=_mm_set1_epi16(static_cast<short>(row.count));
                    lower=_mm_mullo_epi16(lower,weight);upper=_mm_mullo_epi16(upper,weight);
                }
                lo=_mm_add_epi16(lo,lower);hi=_mm_add_epi16(hi,upper);
            }
            _mm_storeu_ps(destination+x,_mm_cvtepi32_ps(_mm_unpacklo_epi16(lo,zero)));
            _mm_storeu_ps(destination+x+4,_mm_cvtepi32_ps(_mm_unpackhi_epi16(lo,zero)));
            _mm_storeu_ps(destination+x+8,_mm_cvtepi32_ps(_mm_unpacklo_epi16(hi,zero)));
            _mm_storeu_ps(destination+x+12,_mm_cvtepi32_ps(_mm_unpackhi_epi16(hi,zero)));
        }
        for(;x<size.width;++x) {
            unsigned int sum=0;
            for(const RowSample& row:rowSamples) {
                const int sx=x+row.dx;
                if(sx>=0 && sx<size.width) sum+=row.data[sx]*row.count;
            }
            destination[x]=static_cast<float>(sum);
        }
    }
    return true;
#else
    (void)response;(void)size;(void)offsets;(void)sampleCount;(void)scores;
    return false;
#endif
}
}