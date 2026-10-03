Shader "Superfishial/PlayerCenteredWorldFog"
{
    Properties
    {
        _FogColor("Fog Color", Color) = (0.02, 0.07, 0.12, 1)
        _FogStart("Fog Start", Float) = 10
        _FogEnd("Fog End", Float) = 50
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PlayerCenteredWorldFog"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FogStart;
                float _FogEnd;
            CBUFFER_END

            float4 _SFFogPlayerPosition;
            float _SFFogEnabled;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0);

                if (_SFFogEnabled < 0.5)
                    return sceneColor;

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.000001)
                        return half4(_FogColor.rgb, sceneColor.a);

                    float depth = rawDepth;
                #else
                    if (rawDepth >= 0.999999)
                        return half4(_FogColor.rgb, sceneColor.a);

                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 worldPosition = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);
                // float playerDistance = distance(worldPosition, _SFFogPlayerPosition.xyz);

                float3 offset = worldPosition - _SFFogPlayerPosition.xyz;
                offset.y *= offset.y < 0.0 ? 2.0 : 1.0;
                float playerDistance = length(offset);

                float fogRange = max(_FogEnd - _FogStart, 0.001);
                float fogAmount = saturate((playerDistance - _FogStart) / fogRange);

                return half4(lerp(sceneColor.rgb, _FogColor.rgb, fogAmount), sceneColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
