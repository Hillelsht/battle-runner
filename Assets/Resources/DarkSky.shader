// Procedural night sky. Replaces a solid clear colour, which gave the world no horizon
// and no depth — the road simply ended in flat charcoal. Three bands plus a low ember
// glow and a scatter of stars, all evaluated per pixel from the view direction, so there
// is no cubemap to import, stream or strip.
Shader "BattleRunner/DarkSky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.035, 0.030, 0.075, 1)
        _HorizonColor ("Horizon", Color) = (0.16, 0.10, 0.16, 1)
        _GroundColor ("Below Horizon", Color) = (0.020, 0.018, 0.030, 1)
        _ZenithFalloff ("Zenith Falloff", Float) = 0.55
        _GroundFalloff ("Ground Falloff", Float) = 0.35
        _GlowColor ("Ember Glow", Color) = (0.55, 0.20, 0.10, 1)
        _GlowDirection ("Glow Direction", Vector) = (0, 0.10, 1, 0)
        _GlowPower ("Glow Tightness", Float) = 6.0
        _StarStrength ("Star Strength", Float) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _ZenithFalloff;
                half _GroundFalloff;
                half4 _GlowColor;
                float4 _GlowDirection;
                half _GlowPower;
                half _StarStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // The skybox mesh is a unit shape centred on the camera, so its object-space
                // position IS the view direction — the same trick Skybox/Cubemap uses.
                float3 directionOS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionOS = input.positionOS.xyz;
                return output;
            }

            // Cheap stable hash — the direction is constant for a fixed camera orientation,
            // so stars hold still instead of crawling.
            float Hash21(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.directionOS);
                float height = dir.y;

                half3 above = lerp(_HorizonColor.rgb, _ZenithColor.rgb,
                                   saturate(pow(saturate(height), _ZenithFalloff)));
                half3 below = lerp(_HorizonColor.rgb, _GroundColor.rgb,
                                   saturate(pow(saturate(-height), _GroundFalloff)));
                half3 color = height > 0.0 ? above : below;

                // A single ember source low on the horizon ahead. It gives the frame a
                // direction to run toward, which a uniform gradient cannot.
                float3 glowDir = normalize(_GlowDirection.xyz);
                half glow = pow(saturate(dot(dir, glowDir)), _GlowPower);
                color += _GlowColor.rgb * glow;

                // Stars only in the upper hemisphere, and faded out near the horizon where
                // the ember wash would swallow them anyway.
                float2 cell = floor(dir.xz / max(0.0001, abs(dir.y) + 0.35) * 60.0);
                float star = Hash21(cell);
                star = saturate(star - 0.985) * 66.0;
                color += star * _StarStrength * saturate(height * 2.0);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
