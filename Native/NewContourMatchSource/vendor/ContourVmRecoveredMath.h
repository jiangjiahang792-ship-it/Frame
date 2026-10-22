#pragma once
#include <algorithm>
#include <cmath>

// Reconstructed scalar mathematics only. No SDK/private-layout dependency.
// Verified against installed MVD_Algorithm.dll SHA256
// 49c413f1a40ce5d929811d5dad63933afa7ff387369dc1acb307788bac979095.
namespace contourvm_detail
{
constexpr float amplitudeScale = 0.22360679507255554f; // bee0f0, 181697c50.
constexpr float angleUnits = 1303.7972412109375f;      // 4096/pi, 181697c88.
constexpr float directionGate = 0.8659999966621399f;   // bf6830, 181698978.

// Static LUTs: ignore polarity 181fe92e0/181fc90e0 (period 4096),
// use polarity 1820196e0/1820014e0 (period 8192).
// Inner plateau: 256 units (11.25 deg); outer cutoff: 512 (22.5 deg).
// Integer table uses steps of 32. The float table rounds the corresponding
// fraction to six decimal places before storage as float (not byte-exact 1/8192).
inline int directionWeight8192(int difference, bool polarity)
{
    const int period = polarity ? 8192 : 4096;
    int distance = difference % period;
    if (distance < 0)
        distance += period;
    distance = std::min(distance, period - distance);
    return std::clamp(512 - distance, 0, 256) * 32;
}
inline float directionWeight(int difference, bool polarity)
{
    const double value = directionWeight8192(difference, polarity) * (1.0 / 8192.0);
    return static_cast<float>(std::floor(value * 1000000.0 + 0.5) / 1000000.0);
}
// b765f0 accepts caller-supplied inner/outer residual radii. The radius
// selection policy is intentionally separate from this recovered formula.
inline float residualWeight(float residual, float inner, float outer)
{
    const float distance = std::abs(residual);
    if (distance >= outer)
        return 0;
    if (distance <= inner)
        return 1;
    return (outer - distance) / (outer - inner);
}
} // namespace contourvm_detail
