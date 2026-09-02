// Greybox everything-shader: instanced crowd bodies (procedural run-bob), gates,
// ground, hero, boss. One shader for the whole greybox keeps the variant count tiny
// and, living in Resources with an instancing-enabled material, immune to build
// stripping (doc 04 / validation report F).
Shader "BattleRunner/CrowdInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.28, 0.38, 1)
        _EmissionColor ("Emission / Rim", Color) = (0.35, 0.5, 0.9, 1)
        _BobAmount ("Run Bob Amount", Float) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmissionColor;
            half _BobAmount;
        CBUFFER_END

        // Per-instance bob phase recovered from the instance's uniform SCALE, which
        // CrowdRenderer bakes as 0.94 + phase*0.12 from a stable per-slot value.
        //
        // It used to be hashed from the instance's world translation. That runs at
        // 10 m/s, so the hash argument moved dot(dz, 78.233) = 13 rad every frame and
        // each unit's phase was re-randomised per frame: the "run bob" was vertical
        // white noise, a shimmer, not a walk cycle. Scale is constant per slot, so
        // the phase now holds still and the units actually march.
        //
        // A macro, not a function: the shadow pass must displace vertices IDENTICALLY to
        // the forward pass or every unit's shadow detaches from its feet and slides. A
        // macro cannot drift out of step the way two copied blocks can, and it keeps
        // UNITY_MATRIX_M at the call site where the instance id is already set up.
        #define APPLY_RUN_BOB(posOS)                                                          \
            {                                                                                 \
                float _s = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10,            \
                                         UNITY_MATRIX_M._m20));                               \
                float _p = saturate((_s - 0.94) / 0.12);                                      \
                float _b = abs(sin(_Time.y * 9.0 + _p * 6.2831)) * _BobAmount;                \
                posOS.y += _b * saturate(posOS.y + 0.5); /* feet stay planted */              \
            }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // GPU instancing requires shader model 3.0; the default 2.5 silently
            // drops the instancing variants on some mobile targets.
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionOS = input.positionOS.xyz;
                APPLY_RUN_BOB(positionOS)

                float3 positionWS = TransformObjectToWorld(positionOS);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // Shadow attenuates only the DIRECT term. Ambient and rim survive it, so a
                // shadowed face darkens instead of becoming a black hole — which in a game
                // this dark would read as a missing polygon rather than as shade.
                half shadow = lerp(0.15h, 1.0h, mainLight.shadowAttenuation);
                half halfLambert = saturate(dot(normalWS, mainLight.direction)) * 0.55h + 0.45h;
                half3 ambient = SampleSH(normalWS);

                half3 color = _BaseColor.rgb * (mainLight.color * halfLambert * shadow + ambient);

                // Rim light sells silhouettes against the dark environment (doc 01, R5).
                half rim = pow(1.0h - saturate(dot(viewDirWS, normalWS)), 2.5h);
                color += _EmissionColor.rgb * (rim * 0.9h + 0.15h);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Without this pass the army casts nothing and reads as hovering over the road
        // rather than marching on it. No amount of colour grading fixes an ungrounded
        // silhouette; a contact shadow does it for one extra depth-only draw.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionOS = input.positionOS.xyz;
                APPLY_RUN_BOB(positionOS)

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS =
                    TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    // Never "Off": if this SubShader is ever ineligible (no SRP active, variants
    // stripped), an explicit fallback renders dull grey instead of error magenta.
    Fallback "Diffuse"
}
