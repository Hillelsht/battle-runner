using BattleRunner.Core.World;
using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>
    /// The one thing in this game that flies.
    ///
    /// *"another can be a fairy tale style with castles and flying pony"* — and the pony is
    /// the single item in that sentence the asset pack could not have supplied. 119 pieces
    /// across five Kenney kits, inventoried: graveyard, nature, fantasy town, castle and
    /// survival, and **not one creature or character mesh of any kind**. It was always going
    /// to be built.
    ///
    /// THREE DRAW CALLS, in one world, and the reason it is three is the same reason the
    /// champion got its own draw: uniform scale is the only per-instance animation channel
    /// this project has, `CrowdInstanced` has already spent it on the walk cycle, and anything
    /// that needs a part to move relative to its body needs its own transform. So the body is
    /// one `RenderMesh` and each wing is another, hinged at the shoulder. Affordable for one
    /// creature; it is exactly the trick that would not scale to a flock.
    ///
    /// The path itself is in Core (`SkyRiders`) because altitude is a CORRECTNESS property
    /// here: nothing in this game collides with anything, so a flier whose path crosses an
    /// arch does not stop — it passes through it, once a lap, forever.
    /// </summary>
    public sealed class SkyRiderView : MonoBehaviour
    {
        /// <summary>Big enough to read at 58 m and 16 m up, where it is about 20 px long.</summary>
        private const float Scale = 3.2f;

        private CrowdController _crowd;
        private Material _material;
        private Mesh _body;

        private SkyPath _path;
        private bool _active;
        private float _elapsed;

        public void Initialize(CrowdController crowd, Material baseMaterial)
        {
            _crowd = crowd;
            if (baseMaterial == null) return;

            _material = ShaderSafety.CreateMaterial(baseMaterial);
            // No walk cycle and no per-instance tone: both decode from a scale window this is
            // nowhere near, so leaving them on would pin the whole animal to the top of that
            // curve and make it march.
            _material.SetFloatSafe("_BobAmount", 0f);
            _material.SetFloatSafe("_ToneSpread", 0f);
            // A strong rim and a real flat term, because it is seen against the SKY. Every
            // other lighting decision in the game is about reading against a dark road; this
            // one is the opposite problem, and a mesh lit for the ground would come out as a
            // silhouette against a bright sky rather than as a white pony.
            _material.SetFloatSafe("_RimPower", 2.2f);
            _material.SetFloatSafe("_RimStrength", 0.85f);
            _material.SetFloatSafe("_EmissionFlat", 0.22f);

            _body = ProceduralMeshes.Pegasus;
        }

        /// <summary>Put this world's flier in the air, or take it away.</summary>
        public void Dress(WorldTheme theme)
        {
            SkyRiderKind kind = theme?.Scenery?.SkyRider ?? SkyRiderKind.None;
            _active = kind != SkyRiderKind.None && _material != null && _body != null;
            if (!_active) return;

            _path = SkyRiders.For(kind);
            _elapsed = 0f;
            // Pale, and lit by the world's own accent rather than painted white: a white pony
            // over Thistlewood's pale horizon would vanish into it.
            _material.SetColorSafe("_BaseColor", new Color(0.93f, 0.92f, 0.95f));
            _material.SetColorSafe("_EmissionColor",
                ThemePalette.ToColor(theme.Accent.Scaled(0.85f)));
        }

        public void Clear() => _active = false;

        private void LateUpdate()
        {
            if (!_active || _crowd == null) return;

            _elapsed += Time.deltaTime;
            SkyRiders.SampleAt(_path, _elapsed, out float ox, out float oy, out float oz,
                out float heading, out float roll);

            // AHEAD OF THE ARMY, not at a fixed z. The circle travels with the run, so the
            // pony is always somewhere in front rather than something passed once and left
            // behind at the start of the round.
            var at = new Vector3(ox, oy, _crowd.CenterZ + _path.Ahead + oz);
            Quaternion facing = Quaternion.Euler(0f, heading, roll);
            Matrix4x4 body = Matrix4x4.TRS(at, facing, Vector3.one * Scale);

            var rp = new RenderParams(_material)
            {
                // Casts nothing. It is 16 m up over a road whose shadows the player reads for
                // gate timing, and a shadow sweeping across the lane every twenty-two seconds
                // is a moving dark shape the game does not mean anything by.
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(at, Vector3.one * (Scale * 6f))
            };
            Graphics.RenderMesh(rp, _body, 0, body);

            float beat = SkyRiders.WingDegrees(_path, _elapsed);
            for (int side = -1; side <= 1; side += 2)
            {
                Mesh wing = ProceduralMeshes.PegasusWing(side);
                if (wing == null) continue;
                // The shoulder, in the body's own space, then through the body transform — so
                // the wings bank and turn with the animal and only the BEAT is theirs. Scale
                // stays POSITIVE: the left wing is its own mesh, because a mirror done with a
                // negative scale leaves the shader's Cull Back alone and the wing disappears.
                Matrix4x4 hinge = Matrix4x4.TRS(
                    new Vector3(side * 0.16f, 0.22f, 0.24f),
                    Quaternion.Euler(0f, 0f, side * -beat),
                    Vector3.one);
                Graphics.RenderMesh(rp, wing, 0, body * hinge);
            }
        }
    }
}
