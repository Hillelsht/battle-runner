// Additive VFX: shockwave rings and debris motes. The FOURTH shader in this project,
// and the one with the most history behind it — a shader reachable only through
// Shader.Find gets stripped from an Android build and renders solid magenta, which is
// exactly how v0.1.0 shipped. So it lives in Resources beside a hand-written material
// that references it by GUID, the same arrangement CrowdInstanced/Crowd.mat has used
// since v0.1.1, and VfxSystem refuses to draw anything at all if that material fails to
// resolve against the active pipeline. Worst case is no effects, never magenta ones.
//
// Deliberately keyword-free: no multi_compile of any kind, so there is exactly ONE
// variant and nothing for variant stripping to get wrong.
//
// No fog, on purpose. Fog LERPS toward the fog colour, which on additive geometry
// ADDS light in the distance instead of removing it. Everything drawn here lives
// within 30 m of the camera, well inside the 70 m fog start, so the correct amount of
// fog is none.
Shader "BattleRunner/Vfx"
{
    Properties
    {
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        // 0 = draw the whole surface flat (motes), 1 = fade up the shock wall's object-
        // space height (shockwaves). One shader, three shapes, no keywords.
        _Band ("Height Falloff", Range(0, 1)) = 0

        // The shield dome, and only the dome. A barrier has to be nearly invisible where
        // you look straight through it and bright where it turns away, or it is an opaque
        // ball sitting on your army. Off for motes and walls.
        _Fresnel ("Fresnel (dome)", Range(0, 1)) = 0
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VfxAdditive"
            Tags { "LightMode" = "UniversalForward" }

            // Additive over whatever is behind, and no depth WRITE so overlapping rings
            // do not occlude each other. Depth TEST stays on: a ring lying on the road
            // should still be hidden by the crowd standing in front of it.
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _Fade;
                half _Band;
                half _Fresnel;
                half _FresnelPower;
            CBUFFER_END

            // NO UV CHANNEL ANYWHERE. The first version shaped the shockwave from the
            // ring mesh's UV.x, which made that mesh the only one in the project carrying
            // UVs — and on device the rings drew nothing at all while the UV-free debris
            // motes on the same material drew fine. Whether the channel failed to reach
            // the shader or the flat ring was simply invisible edge-on, deriving the
            // falloff from object-space height instead removes the question: every mesh
            // has a position, and the shock wall is built to run y = 0 at its base to
            // y = 1 at its top.
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half height : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.height = (half)input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Squared, not linear: brightest where the wave meets the ground and
                // falling away fast up the wall, which is what gives it a crest instead
                // of reading as a lit cylinder.
                half wall = 1.0h - saturate(input.height);
                wall *= wall;
                half shape = lerp(1.0h, wall, _Band);

                // Dome only. Additive plus fresnel is what makes a barrier: the surface
                // facing you contributes almost nothing so the army stays readable through
                // it, while the turning edge lights up and describes the shell. The ripple
                // is what stops it reading as a static prop — a barrier the player is
                // holding up should look like it is being held up.
                half3 n = normalize(input.normalWS);
                half3 v = (half3)normalize(GetWorldSpaceViewDir(input.positionWS));
                // abs(), not saturate(). The pass is Cull Off, so the FAR half of the dome is
                // drawn too and its normals point away from the camera — dot goes negative,
                // saturate clamps it to 0, and the back hemisphere would come out at full
                // fresnel: a bright disc sitting behind the army. abs() mirrors the term, so
                // both shells glow at their rim and go transparent through their middle,
                // which is what a double-sided barrier should do.
                half fres = pow(1.0h - abs(dot(n, v)), _FresnelPower);
                half ripple = 0.78h + 0.22h * sin(input.height * 16.0h - _Time.y * 5.0h);
                shape *= lerp(1.0h, saturate(fres * 1.7h) * ripple, _Fresnel);

                return half4(_TintColor.rgb * shape * _Fade, 1.0h);
            }
            ENDHLSL
        }
    }

    // No fallback. If this cannot run, VfxSystem draws nothing; a stock fallback shader
    // here would be an opaque untinted surface flashing across the road.
    Fallback Off
}
