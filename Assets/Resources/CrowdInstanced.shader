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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _BobAmount;
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
                float3 viewDirWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                // Per-instance bob phase hashed from the instance's world translation:
                // zero extra CPU data, and no two units march in lockstep.
                float3 instancePos = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float phase = frac(sin(dot(instancePos.xz, float2(12.9898, 78.233))) * 43758.5453);

                float3 positionOS = input.positionOS.xyz;
                float bob = abs(sin(_Time.y * 9.0 + phase * 6.2831)) * _BobAmount;
                positionOS.y += bob * saturate(positionOS.y + 0.5); // feet stay planted

                float3 positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                Light mainLight = GetMainLight();
                half halfLambert = saturate(dot(normalWS, mainLight.direction)) * 0.55h + 0.45h;
                half3 ambient = SampleSH(normalWS);

                half3 color = _BaseColor.rgb * (mainLight.color * halfLambert + ambient);

                // Rim light sells silhouettes against the dark environment (doc 01, R5).
                half rim = pow(1.0h - saturate(dot(viewDirWS, normalWS)), 2.5h);
                color += _EmissionColor.rgb * (rim * 0.9h + 0.15h);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    // Never "Off": if this SubShader is ever ineligible (no SRP active, variants
    // stripped), an explicit fallback renders dull grey instead of error magenta.
    Fallback "Diffuse"
}
