using System.IO;
using BattleRunner.Core.World;
using UnityEditor;
using UnityEngine;

namespace BattleRunner.Editor
{
    /// <summary>
    /// Import settings for the generated ground textures, asserted by Unity itself rather
    /// than trusted to a hand-written .meta.
    ///
    /// WHY THIS EXISTS AT ALL. These are the first textures this project has ever had — there
    /// was not a single .png under Assets before them — so there is no existing import
    /// convention to inherit and no TextureImporter meta anywhere to copy. tooling/gen_surfaces.py
    /// writes a .meta template so a fresh clone has a stable GUID, but a .meta is a serialized
    /// blob whose schema moves between Unity versions, and a field this project gets subtly
    /// wrong there fails silently: the texture still imports, still renders, and is simply
    /// wrong. An AssetPostprocessor is the same settings expressed as code, applied on every
    /// import, and impossible to get half-applied.
    ///
    /// THE ONE THAT WOULD ACTUALLY BREAK THINGS is sRGBTexture. Unity's default for a .png is
    /// ON, because the overwhelmingly common case is a photograph. These are not pictures:
    /// R is a stone tone, G a face mask, B a wetness and A a height, and the project renders
    /// in LINEAR colour space, so a gamma decode on upload silently bends all four into
    /// numbers the shader never asked for. The mask would read too dark and the normal map —
    /// where the channels are vector components — would decode to normals that do not point
    /// where the height field says they should.
    /// </summary>
    public sealed class SurfaceImportSettings : AssetPostprocessor
    {
        /// <summary>Assets/Resources/Surfaces — matched as a path fragment, not a prefix.</summary>
        private const string Folder = "/Resources/Surfaces/";

        private void OnPreprocessTexture()
        {
            if (assetPath == null || assetPath.Replace('\\', '/').IndexOf(Folder, System.StringComparison.Ordinal) < 0)
                return;

            var importer = (TextureImporter)assetImporter;

            // DATA, not colour. See the class comment — this is the line that matters.
            importer.sRGBTexture = false;

            // Default, NOT NormalMap, for the _n textures too. A NormalMap import swizzles
            // into DXT5nm or its ASTC equivalent, and which swizzle you get depends on the
            // build target — so the shader would have to call UnpackNormal and hope the
            // platform define matched. Road.shader decodes `tex.xyz * 2 - 1` by hand, which
            // is one instruction and is the same instruction on every device this ships to.
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;

            // Repeat. These tile down four hundred metres of road; clamping would stretch a
            // single row of pixels the whole way.
            importer.wrapMode = TextureWrapMode.Repeat;

            // Trilinear plus anisotropy, because the road is seen almost edge-on out to the
            // fog wall. That grazing angle is exactly the case where bilinear mip transitions
            // show up as bands across the road, and where without aniso the surface dissolves
            // into flat grey about ten metres out — which is the failure this whole piece of
            // work is meant to fix, reintroduced by an import setting.
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;

            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 256;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;

            // ASTC 6x6 on Android: the alpha channel is a height field, so a format that
            // drops or halves alpha (ETC2 RGB, or RGB-only ASTC) would flatten the cavity
            // shading and the normal generation both. 6x6 is ~3.6 bits per pixel — sixteen
            // 256-squared textures land near 1 MB in total, against a budget that had no
            // texture memory allocated at all because there were no textures.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 256,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = (int)TextureCompressionQuality.Normal,
                textureCompression = TextureImporterCompression.Compressed
            });
        }

        /// <summary>
        /// Every surface Core names must actually be on disk. Core loads them by name through
        /// Resources, so a texture that is missing or misspelled produces one symptom: a road
        /// that silently falls back to the flat grey slab, with a warning nobody reads. The
        /// check is cheap and runs when the folder is touched.
        /// </summary>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            bool touched = false;
            foreach (string path in imported)
                if (path.Replace('\\', '/').Contains(Folder)) { touched = true; break; }
            if (!touched) return;

            foreach (RoadSurface surface in RoadSurfaces.All)
            foreach (string resource in new[] { surface.MaskResource, surface.NormalResource })
            {
                string path = "Assets/Resources/" + resource + ".png";
                if (!File.Exists(path))
                    Debug.LogWarning($"[Surfaces] {surface.Name} names '{resource}' but "
                                     + $"{path} does not exist. Run: python3 tooling/gen_surfaces.py");
            }
        }
    }
}
