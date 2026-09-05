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
        _GlowColor ("Ember Glow", Color) = (0.50, 0.33, 0.23, 1)
        _GlowDirection ("Glow Direction", Vector) = (0, 0, 1, 0)
        _GlowPower ("Glow Azimuth Tightness", Float) = 8.0
        _GlowHeight ("Glow Height Falloff", Float) = 16.0
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
                float _GlowPower;
                float _GlowHeight;
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

                // An ember BAND along the horizon ahead, not a radial lobe.
                //
                // This was pow(saturate(dot(dir, glowDir)), 6). A dot-power lobe is
                // radially symmetric, and at exponent 6 it falls to half only 27 degrees
                // off axis — against a horizontal half-FOV of 18 and a vertical half of
                // 30. Every pixel of visible sky sat between 0.68 and 1.00 of full glow,
                // which is a uniform wash, not a light source. It rendered as a red dome.
                //
                // No exponent fixes the shape, only the size: a real horizon glow is
                // WIDE in azimuth and TIGHT in elevation. So the two axes are separated —
                // azimuth^8 spreads it about 24 degrees left and right of the road ahead,
                // while exp2(-|height| * 16) halves it 3.6 degrees above the horizon and
                // is gone by 10.
                float3 glowDir = normalize(_GlowDirection.xyz);
                float2 dirAz = normalize(float2(dir.x, dir.z) + 1e-5);
                float2 glowAz = normalize(float2(glowDir.x, glowDir.z) + 1e-5);
                float azimuth = saturate(dot(dirAz, glowAz));
                float band = exp2(-abs(height) * _GlowHeight);
                float glow = pow(azimuth, _GlowPower) * band;
                color += _GlowColor.rgb * glow;

                // Stars. The first version hashed the CELL and used that value directly,
                // which gave every pixel in a cell the same brightness — so a "star" was
                // the whole cell. At this projection one cell is ~18 px across on a 1080
                // frame, so they rendered as grey quads scattered over the sky like
                // confetti. Peak brightness was (1 - 0.985) * 66 * 0.55 ~= 0.54, mid-grey,
                // which is why they read as debris rather than light.
                //
                // A star has to be a POINT inside its cell: hash a position, measure the
                // distance to it, and fall off sharply.
                float2 grid = dir.xz / max(0.0001, abs(dir.y) + 0.35) * 60.0;
                float2 cell = floor(grid);
                float2 f = frac(grid);

                float present = Hash21(cell);
                float2 starPos = float2(Hash21(cell + 17.31), Hash21(cell + 41.77));
                float d = length(f - starPos);

                // Sub-pixel-tight, so a star is a point of light rather than a blob.
                float star = smoothstep(0.055, 0.0, d) * step(0.986, present);

                // Above white on purpose: bloom is on now, so a star should be a genuine
                // light source that scatters a little, not a grey dot.
                color += star * _StarStrength * saturate(height * 2.5);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
