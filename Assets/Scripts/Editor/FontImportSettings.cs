using UnityEditor;

namespace BattleRunner.Editor
{
    /// <summary>
    /// Import settings for the one font this game ships, asserted by Unity itself rather than
    /// trusted to a hand-written .meta.
    ///
    /// THE SAME ARGUMENT AS SurfaceImportSettings, and for the same reason. tooling/gen_meta.py
    /// writes a TrueTypeFontImporter block so a fresh clone has a stable GUID, but a .meta is a
    /// serialized blob whose schema moves between Unity versions, and a field this project gets
    /// subtly wrong there fails SILENTLY: the font still imports, still renders Latin, and is
    /// simply missing the thing it was brought in for.
    ///
    /// THE FIELD THAT WOULD ACTUALLY BREAK THINGS is fontRenderingMode. A dynamic font rasterises
    /// glyphs on demand, which is the only mode that can serve three alphabets out of one 316 KB
    /// file — the alternative bakes a fixed character set into an atlas at import, and whichever
    /// set that turned out to be, two of the game's three languages would render as empty boxes.
    /// It is also the mode the three world-space TextMesh labels depend on, since they draw
    /// through font.material and that material IS the dynamic atlas.
    ///
    /// includeFontData matters almost as much: with it off, Unity references the font by NAME and
    /// resolves it against the operating system at runtime. On the build machine that looks
    /// identical. On a phone without Arimo installed it is nothing at all.
    /// </summary>
    public sealed class FontImportSettings : AssetPostprocessor
    {
        /// <summary>Assets/Resources/Fonts — matched as a path fragment, not a prefix.</summary>
        private const string Folder = "/Resources/Fonts/";

        private void OnPreprocessAsset()
        {
            if (assetPath == null
                || assetPath.Replace('\\', '/').IndexOf(Folder, System.StringComparison.Ordinal) < 0)
                return;
            if (!(assetImporter is TrueTypeFontImporter importer)) return;

            // Rasterise on demand. See the class note: this is the one that decides whether
            // Russian and Hebrew can be drawn at all.
            importer.fontTTFName = "Arimo";
            importer.fontRenderingMode = FontRenderingMode.Smooth;
            // Ship the outlines, do not look for them on the device.
            importer.includeFontData = true;
            // The atlas's starting size only. Every Text sets its own fontSize, and a dynamic
            // font grows its atlas as it meets glyphs it has not drawn yet.
            importer.fontSize = 40;
            // A pixel of padding either side of each glyph. Without it, neighbouring glyphs in
            // the atlas bleed into one another under bilinear filtering — visible as a faint
            // ghost stroke, and worst on the dense Hebrew forms where the gaps are smallest.
            importer.characterPadding = 1;
        }
    }
}
