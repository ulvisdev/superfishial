//UNITY_SHADER_NO_UPGRADE
#ifndef SUPERFISHIAL_PLAYER_REVEAL_INCLUDED
#define SUPERFISHIAL_PLAYER_REVEAL_INCLUDED

float _SFRevealEnabled;
float4 _SFRevealCenter;
float4 _SFRevealShape;
float4 _SFRevealCameraPosition;
float4 _SFRevealCameraForward;
float4 _SFRevealDepth;

// ScreenUV: Screen Position node in Default mode, XY channels.
// WorldPosition: Position node in Absolute World space.
// The output is dithered coverage, connected to Alpha with clip threshold 0.5.
void PlayerReveal_float(float3 WorldPosition, float2 ScreenUV, out float Alpha)
{
    Alpha = 1.0;
    #if defined(SHADERGRAPH_PREVIEW)
        return;
    #endif
    // Keep the original solid silhouette in shadow maps.
    #if defined(SHADERPASS) && defined(SHADERPASS_SHADOWCASTER)
        #if SHADERPASS == SHADERPASS_SHADOWCASTER
            return;
        #endif
    #endif
    if (_SFRevealEnabled < 0.5) return;

    float2 offset = ScreenUV - _SFRevealCenter.xy;
    offset.x *= _SFRevealCenter.w;
    float radius = max(_SFRevealShape.x, 0.001);
    float softness = clamp(_SFRevealShape.y, 0.0001, radius);
    float circle = 1.0 - smoothstep(radius - softness, radius, length(offset));
    float fragmentDepth = dot(WorldPosition - _SFRevealCameraPosition.xyz, _SFRevealCameraForward.xyz);
    float foreground = smoothstep(_SFRevealDepth.x, _SFRevealDepth.x + max(_SFRevealDepth.y, 0.0001), _SFRevealCenter.z - fragmentDepth);
    float opacity = lerp(1.0, saturate(_SFRevealShape.z), circle * foreground);

    // A common screen-space pattern aligns holes across overlapping surfaces.
    const float thresholds[16] = {
        0.5, 8.5, 2.5, 10.5,
        12.5, 4.5, 14.5, 6.5,
        3.5, 11.5, 1.5, 9.5,
        15.5, 7.5, 13.5, 5.5
    };
    uint2 pixel = (uint2)floor(ScreenUV * _ScreenParams.xy);
    uint index = (pixel.x % 4u) + (pixel.y % 4u) * 4u;
    Alpha = step(thresholds[index] / 16.0, opacity);
}

#endif
