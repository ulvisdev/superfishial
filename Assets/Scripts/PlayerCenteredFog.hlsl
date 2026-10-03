#ifndef SUPERFISHIAL_PLAYER_CENTERED_FOG_INCLUDED
#define SUPERFISHIAL_PLAYER_CENTERED_FOG_INCLUDED

float4 _SFFogPlayerPosition;
float _SFFogEnabled;

void PlayerCenteredFog_float(float3 WorldPosition, float FogStart, float FogEnd, out float Fog, out float Visibility)
{
    Fog = 0.0;
    Visibility = 1.0;

    #if defined(SHADERGRAPH_PREVIEW)
        return;
    #endif

    if (_SFFogEnabled < 0.5)
        return;

    // float playerDistance = distance(WorldPosition, _SFFogPlayerPosition.xyz);

    float3 offset = WorldPosition - _SFFogPlayerPosition.xyz;
    offset.y *= offset.y < 0.0 ? 3.0 : 1.0;
    float playerDistance = length(offset);

    float fogRange = max(FogEnd - FogStart, 0.001);

    Fog = saturate((playerDistance - FogStart) / fogRange);
    Visibility = 1.0 - Fog;
}

#endif