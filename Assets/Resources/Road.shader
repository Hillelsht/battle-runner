// The road surface. It is the largest single area of the frame and was an untextured
// slab of flat charcoal — no amount of lighting makes a featureless plane interesting,
// because there is nothing on it for the light to catch.
//
// Everything here is still derived from world XZ — that has not changed and is the whole
// reason a texture could be added at all. `uv = positionWS.xz * tiling` is a real UV, so a
// sampler needs no mesh UV channel, no importer and no change to a single mesh.
//
// WHY IT IS NOW SAMPLED RATHER THAN COMPUTED. The brick bond, mortar, per-stone tone and two
// octaves of grime were all evaluated per pixel, and the result still measured a luminance
// range of only 19-42 out of 255 against 54-139 for a photoreal reference. At 2-5% contrast
// the cobbles are invisible and the road reads as a flat slab, which is exactly what it was
// called. A fragment shader can afford a handful of instructions per pixel; a texture is a
// lookup table for arbitrarily expensive maths, and a voronoi cell diagram with per-stone
// tone is exactly that. tooling/gen_surfaces.py bakes eight of them; the generated cobble
// measures a range of 178 and 8.8x the edge density of what this shader used to produce.
//
// WHAT IS NOT IN THE TEXTURE: colour. Eight worlds author eight tuned road palettes and
// baking colour would throw all of that away. The mask carries STRUCTURE — R tone, G face
// mask, B wetness, A height — and this shader tints it with the world's own stone and mortar.
// The macro grime below stays computed, because a 256px texture repeating every few metres
// would otherwise show its tile across 400 m of road, and noise at 3 m and 10 m is what
// breaks that up.
Shader "BattleRunner/Road"
{
    Properties
    {
        // The project renders in Linear colour space, so these sRGB values are
        // gamma->linear converted on upload. The original 0.115 arrived at the shader as
        // 0.0125 linear — a 1.25% reflectance, seven times darker than dark asphalt and
        // darker than charcoal. No material can be that dark, and no grade could rescue
        // it: the lower half of the frame was black because the albedo was impossible.
        // Darkness in a night scene has to come from the light level, not the albedo.
        // Warm-neutral, NOT violet. SampleSH feeds this surface ambient straight off
        // the sky dome, which is a deep blue-violet, so an albedo that is itself
        // blue-violet (b > r > g, as this was) compounds the tint and the whole street
        // comes back lavender. Letting the cold ambient do the tinting against warm
        // stone is both the fix and the dark-fantasy reference.
        _BaseColor ("Stone", Color) = (0.31, 0.295, 0.285, 1)
        _MortarColor ("Mortar", Color) = (0.145, 0.138, 0.135, 1)
        // Still cool — it is a reflection of that same sky, and localised to the joints and
        // low ground by the surface mask's wetness channel — but pulled back so the sheen is
        // not a second full-coverage blue wash.
        _DampColor ("Damp Sheen", Color) = (0.30, 0.33, 0.44, 1)
        // "gray" rather than "white" as the fallback: an unbound mask decodes to zero tone
        // variation and a fully-open face, i.e. exactly the flat slab this replaced, instead
        // of a road with the stone tone pinned to maximum.
        _Surface ("Surface Mask (R tone, G face, B wet, A height)", 2D) = "gray" {}
        _SurfaceNormal ("Surface Normal", 2D) = "bump" {}
        // Tile repeats per metre, NOT cobbles per metre. RoadSurface.TileRepeatsPerMetre
        // converts between them using how many stones the generator put in one tile.
        _SurfaceTiling ("Tile Repeats Per Metre", Float) = 0.18
        _NormalStrength ("Normal Strength", Range(0, 1)) = 1
        _Cavity ("Cavity Shading", Range(0, 1)) = 0.45
        _MortarWidth ("Mortar Width", Range(0.01, 0.3)) = 0.075
        _StoneVariation ("Stone Tone Variation", Range(0, 1)) = 0.45
        _Wetness ("Wetness", Range(0, 1)) = 0.55
        _Gloss ("Sheen Tightness", Float) = 8
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

            // Textures live OUTSIDE UnityPerMaterial — a sampler inside the constant buffer
            // does not compile under SRP batching, and this material is batched.
            TEXTURE2D(_Surface);
            SAMPLER(sampler_Surface);
            TEXTURE2D(_SurfaceNormal);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _MortarColor;
                half4 _DampColor;
                // FLOAT, not half. This is a world-space coordinate multiplier and the road
                // runs past z = 400; at half precision the UV quantises into visible steps in
                // the distance, which is a stair-stepped road rather than a tiled one.
                float _SurfaceTiling;
                half _NormalStrength;
                half _Cavity;
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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionWS.xz * _SurfaceTiling;
                half4 surf = SAMPLE_TEXTURE2D(_Surface, sampler_Surface, uv);

                // The joint. G is a soft face mask — 1 on a stone, 0 in a gap — and the
                // mortar width raises the threshold, so a world with wide, deep joints and a
                // world with tight ones share one texture. All eight worlds sat on the same
                // 0.075 before the surfaces existed, and an identical joint pattern in every
                // world is a real part of why the road never looked like it changed.
                half face = smoothstep(_MortarWidth, _MortarWidth + 0.28h, surf.g);

                half tone = (surf.r - 0.5h) * _StoneVariation * 2.0h;
                half3 stone = saturate(_BaseColor.rgb * (1.0h + tone));
                half3 albedo = lerp(_MortarColor.rgb, stone, face);

                // Grime at three metres, damp at ten. STILL COMPUTED, and now for a second
                // reason: the texture repeats every few metres down a road that runs past
                // 400 m, and noise an order of magnitude larger than the tile is what stops
                // the eye from locking onto the repeat.
                half grime = ValueNoise(input.positionWS.xz * 0.33) * 0.6h
                           + ValueNoise(input.positionWS.xz * 0.10) * 0.4h;
                albedo *= lerp(0.72h, 1.12h, grime);

                // Cavity from the height channel: recesses get less ambient than faces do.
                // A contact shadow for one multiply, and the cheapest depth cue available.
                albedo *= lerp(1.0h, saturate(surf.a * 0.75h + 0.4h), _Cavity);

                // The tangent frame is a CONSTANT here and that is the only reason normal
                // mapping is possible at all: no mesh in this project has tangents, but the
                // road is a horizontal, axis-aligned plane, so T = (1,0,0), B = (0,0,1) and
                // N = (0,1,0) are known at compile time. worldN = (n.x, n.z, n.y) falls
                // straight out of that. Faded by the geometric normal's Y so the stretched
                // box's vertical sides are not shaded with a floor's normal map.
                half3 geoN = normalize(input.normalWS);
                half3 nT = SAMPLE_TEXTURE2D(_SurfaceNormal, sampler_Surface, uv).xyz * 2.0h - 1.0h;
                half3 bumped = normalize(half3(nT.x, nT.z, nT.y));
                half3 normalWS = normalize(lerp(geoN, bumped,
                                                _NormalStrength * saturate(geoN.y)));
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(0.18h, 1.0h, mainLight.shadowAttenuation);
                half lambert = saturate(dot(normalWS, mainLight.direction)) * 0.6h + 0.4h;
                half3 ambient = SampleSH(normalWS);

                half3 color = albedo * (mainLight.color * lambert * shadow + ambient);

                // The wet sheen now follows the texture's own B channel, which is where the
                // generator put standing water: in the joints and the low ground, because
                // that is where water actually collects. The old version put it on the stone
                // TOPS, which is backwards, and it only looked acceptable because there was
                // no relief for it to disagree with. Macro grime still gates it so a whole
                // road is not uniformly wet.
                half3 halfVector = normalize(mainLight.direction + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfVector)), _Gloss);
                color += _DampColor.rgb * spec * _Wetness * surf.b * grime * shadow;

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
    }

    Fallback "Diffuse"
}
