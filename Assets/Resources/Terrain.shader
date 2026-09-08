// The land the road runs through. Until this existed there was no land: the whole world
// beside the road was one 8.3 m wide box (TrackController.SpawnGroundStrip built it to
// x = +/-4.158) and everything further out — every gravestone, every dead tree — was
// hovering over the skybox. That is why eight authored worlds still read as one place with
// a colour filter over it: a palette cannot differentiate ground that is not there.
//
// Same discipline as Road.shader, and for the same reason: everything is derived from world
// XZ, so there is no texture to author, import, stream or strip, no UV channel on the mesh,
// and it tiles forever across a band of any size. Two colours, a patch mask that clumps them
// at roughly ten metres, a fine speckle so the surface catches the key light, and an
// optional sheen for the worlds that are wet or frozen.
Shader "BattleRunner/Terrain"
{
    Properties
    {
        // Linear colour space: these sRGB values are gamma->linear converted on upload, so
        // authored darkness costs more than it looks. Same trap Road.shader documents at
        // length — ground darker than about 0.12 sRGB arrives as single-digit-percent
        // reflectance and reads as a hole rather than as earth.
        _GroundColor ("Ground", Color) = (0.20, 0.19, 0.16, 1)
        _GroundColorAlt ("Ground Patches", Color) = (0.26, 0.25, 0.21, 1)
        _PatchScale ("Patch Size (per metre)", Float) = 0.09
        _SpeckleScale ("Speckle Size (per metre)", Float) = 1.8
        _Speckle ("Speckle Strength", Range(0, 1)) = 0.30
        _SheenColor ("Sheen", Color) = (0.30, 0.33, 0.44, 1)
        _Sheen ("Sheen Strength", Range(0, 1)) = 0.15
        _Gloss ("Sheen Tightness", Float) = 12
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
                half4 _GroundColor;
                half4 _GroundColorAlt;
                half4 _SheenColor;
                half _PatchScale;
                half _SpeckleScale;
                half _Speckle;
                half _Sheen;
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
            };

            // Deliberately the same hash and the same value noise as Road.shader. Two
            // different noise functions meeting at the kerb would draw a seam along the
            // whole length of the level, which is the one place the eye is guaranteed to be.
            float Hash21(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;

                // Two octaves an order of magnitude apart: the coarse one decides where a
                // patch of the alternate colour is, the fine one keeps its edge from
                // reading as a smooth blob. Squared to bias toward the base colour, so the
                // alternate stays a patch rather than becoming half the ground.
                half patch = ValueNoise(p * _PatchScale) * 0.72h
                           + ValueNoise(p * _PatchScale * 3.7h) * 0.28h;
                patch = saturate(patch * patch * 1.6h);
                half3 albedo = lerp(_GroundColor.rgb, _GroundColorAlt.rgb, patch);

                // Per-metre speckle. Without it the band is a flat plane and the key light
                // has nothing to catch across 65 m of it — the exact failure the road had
                // before it was given cobbles.
                half grain = Hash21(floor(p * _SpeckleScale));
                albedo *= lerp(1.0h - _Speckle * 0.5h, 1.0h + _Speckle * 0.5h, grain);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(0.18h, 1.0h, mainLight.shadowAttenuation);
                half lambert = saturate(dot(normalWS, mainLight.direction)) * 0.6h + 0.4h;
                half3 ambient = SampleSH(normalWS);

                half3 color = albedo * (mainLight.color * lambert * shadow + ambient);

                // Wet marsh and frozen reach want a glint; ash and bone do not. Gated on the
                // patch mask so the sheen sits in the low ground rather than covering
                // everything, which is where water actually collects.
                half3 halfVector = normalize(mainLight.direction + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfVector)), _Gloss);
                color += _SheenColor.rgb * spec * _Sheen * patch * shadow;

                // PER-PIXEL fog, for the same reason Road.shader computes it per pixel and
                // documents why: this band is TWO stretched boxes spanning the entire level,
                // so a fog factor evaluated at their eight corners and interpolated across
                // 65 x 400 m depends on where the camera sits along the box rather than on
                // how far away the pixel is. On a surface this large that error is the
                // difference between a horizon and a gradient.
                color = MixFog(color, ComputeFogFactor(TransformWorldToHClip(input.positionWS).z));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback "Diffuse"
}
