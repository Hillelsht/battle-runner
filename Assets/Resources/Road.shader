// The road surface. It is the largest single area of the frame and was an untextured
// slab of flat charcoal — no amount of lighting makes a featureless plane interesting,
// because there is nothing on it for the light to catch.
//
// Everything here is derived from world XZ: brick-bonded cobbles, per-stone tone, damp
// blotches, and a wet sheen that picks up the moonlight. No textures to author, import,
// stream or strip, and it tiles forever down a road of any length.
Shader "BattleRunner/Road"
{
    Properties
    {
        _BaseColor ("Stone", Color) = (0.115, 0.105, 0.135, 1)
        _MortarColor ("Mortar", Color) = (0.045, 0.042, 0.058, 1)
        _DampColor ("Damp Sheen", Color) = (0.16, 0.17, 0.26, 1)
        _Tiling ("Cobbles Per Metre", Float) = 1.6
        _MortarWidth ("Mortar Width", Range(0.01, 0.3)) = 0.075
        _StoneVariation ("Stone Tone Variation", Range(0, 1)) = 0.45
        _Wetness ("Wetness", Range(0, 1)) = 0.55
        _Gloss ("Sheen Tightness", Float) = 24
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _MortarColor;
                half4 _DampColor;
                half _Tiling;
                half _MortarWidth;
                half _StoneVariation;
                half _Wetness;
                half _Gloss;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            // Smooth value noise, for grime at a scale much larger than one stone.
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionWS.xz * _Tiling;

                // Brick bond: every other row shifts half a stone, so the mortar never
                // lines up into long straight seams running down the road.
                float row = floor(uv.y);
                uv.x += frac(row * 0.5) * 1.0;

                float2 cell = floor(uv);
                float2 f = frac(uv);

                // Distance to the nearest cell edge, widened into a mortar gap.
                float edge = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
                half mortar = smoothstep(0.0, _MortarWidth, edge);

                half tone = (Hash21(cell) - 0.5h) * _StoneVariation;
                half3 stone = saturate(_BaseColor.rgb * (1.0h + tone));
                half3 albedo = lerp(_MortarColor.rgb, stone, mortar);

                // Grime at three metres, damp at ten. Two octaves is enough to break up
                // the regularity without looking like noise for its own sake.
                half grime = ValueNoise(input.positionWS.xz * 0.33) * 0.6h
                           + ValueNoise(input.positionWS.xz * 0.10) * 0.4h;
                albedo *= lerp(0.72h, 1.12h, grime);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(0.18h, 1.0h, mainLight.shadowAttenuation);
                half lambert = saturate(dot(normalWS, mainLight.direction)) * 0.6h + 0.4h;
                half3 ambient = SampleSH(normalWS);

                half3 color = albedo * (mainLight.color * lambert * shadow + ambient);

                // A wet sheen that only the mortar-free stone tops catch, strongest where
                // the grime says the stone is damp. This is what makes the road read as a
                // surface rather than as a colour.
                half3 halfVector = normalize(mainLight.direction + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfVector)), _Gloss);
                color += _DampColor.rgb * spec * _Wetness * mortar * grime * shadow;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback "Diffuse"
}
