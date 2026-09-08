namespace BattleRunner.Core.World
{
    /// <summary>
    /// A colour, without an engine.
    ///
    /// The theme table has to live in Core: the test assembly references Core and nothing
    /// else, on purpose, so that every test runs identically under headless `dotnet test` and
    /// under Unity's runner. A palette expressed in UnityEngine.Color would be untestable
    /// here, and the relationship between a world's sky and its fog is exactly the kind of
    /// thing that should be pinned rather than eyeballed.
    ///
    /// Values are LINEAR-INTENT sRGB, matching what the .mat files and the existing material
    /// constants already carry; the Gameplay adapter hands them to Unity unchanged.
    /// </summary>
    public readonly struct Rgb
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public Rgb Scaled(float factor) => new Rgb(R * factor, G * factor, B * factor);

        public static Rgb Lerp(Rgb a, Rgb b, float t) =>
            new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

        public override string ToString() => $"({R:0.###}, {G:0.###}, {B:0.###})";
    }
}
