// Imported scenery: castles, cottages, mills, trees, gravestones.
//
// This is CrowdInstanced's lighting with two changes, and both are the point of the file.
//
// 1. COLOUR COMES FROM THE VERTEX, not from the material. Every other mesh in the game is
//    one flat colour per material, which is why 119 imported models could not have been
//    drawn by the existing shader without 119 materials and 119 draw calls. The pack bakes
//    Kenney's atlas down to a vertex colour, so a whole castle — sandstone, roof tile, the
//    red of a flag — is one instanced draw.
//
// 2. NO RUN-BOB AND NO SCALE DECODE. CrowdInstanced recovers each soldier's bob phase from
//    INSTANCE SCALE inside a 0.44-0.50 window; anything outside pins to 1.0 and brightens
//    about 30%. Scenery is placed at 2x to 6x, so borrowing that shader would have meant
//    remembering to zero _ToneSpread on every material forever. The coupling is deleted
//    here rather than worked around.
//
// _Tint is how "bright landmarks, dark verge" is expressed: the same pack serves all eight
// worlds, and each zone is dragged its own distance toward the world's own stone at runtime.
Shader "BattleRunner/Scenery"
{
    Properties
    {
        // Multiplies the baked vertex colour. White leaves Kenney's palette alone.
        _BaseColor ("Overall Tint", Color) = (1, 1, 1, 1)
        // The colour the zone is dragged toward, and how far. This is a LERP in albedo, not
        // a multiply: multiplying toward grey desaturates but also darkens everything
        // equally, which flattens a verge instead of making it grim.
        _TintColor ("Mood", Color) = (0.28, 0.27, 0.25, 1)
        _TintAmount ("Mood Amount", Range(0, 1)) = 0
        _EmissionColor ("Rim", Color) = (0.35, 0.5, 0.9, 1)
        _RimPower ("Rim Power", Range(1, 8)) = 4
        _RimStrength ("Rim Strength", Range(0, 4)) = 0.30
        _EmissionFlat ("Flat Emission", Range(0, 4)) = 0.02
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
                half4 _TintColor;
                half4 _EmissionColor;
                half _TintAmount;
                half _RimPower;
                half _RimStrength;
                half _EmissionFlat;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // Identical to CrowdInstanced on purpose: an imported castle and a procedural
                // soldier standing in the same frame under different lighting models is the
                // fastest way to make imported art look pasted on.
                half shadow = lerp(0.15h, 1.0h, mainLight.shadowAttenuation);
                half wrapped = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half halfLambert = wrapped * 0.7h + 0.3h;
                half3 ambient = SampleSH(normalWS);

                half3 albedo = input.color.rgb * _BaseColor.rgb;
                albedo = lerp(albedo, _TintColor.rgb, _TintAmount);
                half3 color = albedo * (mainLight.color * halfLambert * shadow + ambient);

                half rim = pow(1.0h - saturate(dot(viewDirWS, normalWS)), _RimPower);
                color += _EmissionColor.rgb * (rim * _RimStrength + _EmissionFlat);

                // PER-PIXEL fog. Scenery spans 90 m of lateral distance and 200 m of depth in
                // a single instanced draw, so a per-vertex factor on a 12-triangle rock would
                // fog it by where its own corners are rather than by where the pixel is —
                // the same error Road.shader documents, at a different scale.
                color = MixFog(color, ComputeFogFactor(TransformWorldToHClip(input.positionWS).z));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Landmarks are the largest silhouettes in the game after the boss, and a castle that
        // casts nothing sits on the land the way the army used to hover over the road. The
        // field decides per zone whether to use this; the verge does not cast.
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lightDirectionWS = _MainLightPosition.xyz;
                output.positionCS =
                    TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback "Diffuse"
}
