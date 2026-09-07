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

        // Rim shape is per-MATERIAL, not baked. The lane lines, speed rungs and finish
        // line are 2 cm-tall boxes whose only bright term is this rim on their top face,
        // so the crowd cannot narrow the lobe or mask up-faces globally without putting
        // the road markings out. Defaults reproduce the previous hard-coded expression
        // exactly, so every material that does not opt in is bit-identical.
        _RimPower ("Rim Power", Range(1, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 4)) = 0.9
        _RimUpMask ("Rim Up-Face Mask", Range(0, 1)) = 0
        _EmissionFlat ("Flat Emission", Range(0, 4)) = 0.15

        // Per-unit tone spread, and OFF by default. The phase this rides on is decoded from
        // instance scale, and every non-crowd object using this shader (gates, rails, road
        // markings, finish line, boss) has a scale far outside the crowd's 0.44-0.50 window,
        // so their phase pins to 1.0 and they would all silently brighten. Only the crowd
        // and hero materials turn this on.
        _ToneSpread ("Per-Unit Tone Spread", Range(0, 1)) = 0
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
            half _RimPower;
            half _RimStrength;
            half _RimUpMask;
            half _EmissionFlat;
            half _ToneSpread;
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
        // Keep in lockstep with CrowdRenderer.ScaleMin / ScaleSpan. The drawn scale
        // dropped from 0.94..1.06 to 0.44..0.50 because the formation is pinned to one
        // lane (1.56 m across) while a body is 0.60 m over the pauldrons: at n=90 the
        // lateral pitch is 0.157 m, so units were drawn nearly four body-widths into one
        // another and the army was geometrically a solid slab before any shader ran.
        #define CROWD_SCALE_MIN  0.44
        #define CROWD_SCALE_SPAN 0.06

        // The hip line. ProceduralMeshes.BuildSoldier keeps every archetype's legs below
        // this and its body above it, because everything under it is swung about it here.
        #define CROWD_HIP_Y 0.30
        #define CROWD_STRIDE 4.4
        // Legs sit at |x| = 0.105; every weapon starts at |x| >= 0.23. Without this band a
        // spear butt (which hangs to y = 0.10, well under the hip) would swing with the
        // right leg while its head stayed put, and the shaft would visibly BEND at the hip
        // line. The x test is what makes the swing select legs rather than "everything low".
        #define CROWD_LEG_X 0.18

        // A real WALK, not just a bob. The army was sliding along the road at 10 m/s with
        // its feet welded together and only a vertical wobble to suggest motion — which is
        // most of why it read as objects being carried rather than as soldiers marching.
        //
        // Legs rotate about the hip, and the two sides are in antiphase because the swing
        // is multiplied by sign(x): anything on the midline (torso, head, crest) has
        // sign() == 0 and does not move at all, so the effect selects the limbs by itself
        // with no bone weights, no skinning and no extra vertex channel.
        //
        // Scaled by _BobAmount, which is already 0 on every non-crowd material — gates,
        // rails, road markings, the finish line and the boss are all untouched by this.
        #define APPLY_RUN_BOB(posOS, outPhase)                                                \
            {                                                                                 \
                float _s = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10,            \
                                         UNITY_MATRIX_M._m20));                               \
                float _p = saturate((_s - CROWD_SCALE_MIN) / CROWD_SCALE_SPAN);               \
                outPhase = _p;                                                                \
                float _t = _Time.y * 9.0 + _p * 6.2831;                                       \
                float _sw = sin(_t) * sign(posOS.x) * CROWD_STRIDE * _BobAmount;              \
                if (posOS.y < CROWD_HIP_Y && abs(posOS.x) < CROWD_LEG_X)                      \
                {                                                                             \
                    float _dy = CROWD_HIP_Y - posOS.y;                                        \
                    posOS.z += sin(_sw) * _dy;                                                \
                    posOS.y  = CROWD_HIP_Y - cos(_sw) * _dy;                                  \
                }                                                                             \
                float _b = abs(sin(_t)) * _BobAmount;                                         \
                posOS.y += _b * saturate(posOS.y * 2.5 - 0.05); /* 0 at the feet */           \
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
                half phase : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionOS = input.positionOS.xyz;
                float phase = 0.0;
                APPLY_RUN_BOB(positionOS, phase)
                output.phase = (half)phase;

                float3 positionWS = TransformObjectToWorld(positionOS);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
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
                // WRAPPED half-lambert, not a clamped one. saturate(dot) collapses every
                // back-facing normal onto the same value, so a figure lit from behind had
                // literally one diffuse number across its whole visible surface: the boss
                // is backlit (light direction +0.797, +0.530, +0.290; boss faces -Z) and
                // every camera-facing face on it — torso, head, both pauldrons, the horns —
                // came out at exactly 0.45. Flat paint on a silhouette.
                //
                // dot * 0.5 + 0.5 keeps the gradient running through the back hemisphere
                // instead of clipping it, and the 0.7/0.3 remap holds a floor so nothing
                // that used to be visible falls to black: the range is [0.30, 1.00] where
                // it was [0.45, 1.00], and a face at dot = -0.39 reads 0.51 rather than
                // 0.45. The crowd's own camera-facing side gets BRIGHTER (0.45 -> 0.55)
                // and its side faces spread apart, so the army gains shape too.
                half wrapped = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half halfLambert = wrapped * 0.7h + 0.3h;
                half3 ambient = SampleSH(normalWS);

                // Identical armour on nine hundred men reads as one object. A cheap tonal
                // spread off the per-instance phase breaks that up without a second draw
                // call, a second material or a per-instance colour channel.
                half3 albedo = _BaseColor.rgb * lerp(1.0h, lerp(0.74h, 1.30h, input.phase), _ToneSpread);
                half3 color = albedo * (mainLight.color * halfLambert * shadow + ambient);

                // Rim light sells silhouettes against the dark environment (doc 01, R5) —
                // but pow(1 - dot(V,N), k) is NOT an edge term on this geometry.
                // ProceduralMeshes.AddBox duplicates vertices per face for HARD normals,
                // so this is a per-FACE CONSTANT: at k = 2.5 every face more than 76
                // degrees off the view axis glows at over half strength. Of the three
                // faces the camera can see on a unit, the top sits at ~79 degrees and the
                // visible side at ~78 — two of three flooded — and the flat term then
                // floods the third. The crowd was 79-97% pure emission: self-lit blocks,
                // not lit figures. The up-face mask and a tighter power give the crowd a
                // real silhouette while the road decals keep the wide default.
                half rim = pow(1.0h - saturate(dot(viewDirWS, normalWS)), _RimPower);
                rim *= lerp(1.0h, saturate(1.0h - normalWS.y * 2.0h), _RimUpMask);
                color += _EmissionColor.rgb * (rim * _RimStrength + _EmissionFlat);

                // PER-PIXEL fog, not the interpolated per-vertex factor URP hands you.
                // The ground, the four lane lines and both rails are each ONE stretched box
                // spanning the entire level — over 400 m from eight corner vertices — so a
                // fog factor evaluated at those corners is then interpolated across the
                // whole visible road. The result depends on where the camera happens to sit
                // along the box rather than on how far away the pixel actually is: measured
                // on device, the near road brightened about 2x and warmed toward the fog
                // colour between the start of a level and its boss, with nothing about the
                // material or the distance having changed. Recomputing the clip position
                // here costs one matrix multiply and makes the factor exact. Using URP's own
                // ComputeFogFactor rather than unpacking unity_FogParams keeps this correct
                // across reversed-Z and any future fog mode.
                color = MixFog(color, ComputeFogFactor(TransformWorldToHClip(input.positionWS).z));
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
                float _unusedPhase = 0.0;
                APPLY_RUN_BOB(positionOS, _unusedPhase)

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
