using System;
using BattleRunner.Core.Stats;
using BattleRunner.Core.World;

namespace BattleRunner.Core.Heroes
{
    /// <summary>Who the player is. Chosen once per save slot, before the first round.</summary>
    public enum HeroClass
    {
        /// <summary>Broad, tower shield, crested helm. Survives what others dodge.</summary>
        Warden = 0,
        /// <summary>Tall and thin, burning staff. Clears the road before it arrives.</summary>
        Ashcaller = 1,
        /// <summary>Crouched, fur cloak, two hounds. Grows the army fastest.</summary>
        Houndmaster = 2,
        /// <summary>Skeletal, broken crown. Loses men and gets them back.</summary>
        Revenant = 3
    }

    /// <summary>
    /// The silhouette parameters a hero mesh is built from.
    ///
    /// Numbers rather than a mesh, because a mesh needs UnityEngine and this table has to be
    /// testable — and because the ONE constraint that matters here is arithmetic:
    /// `CrowdInstanced` swings everything below y = 0.30 about that line to make the march
    /// cycle, so a hero whose legs start above it walks from the waist. A test can hold that;
    /// an eyeball cannot.
    /// </summary>
    public readonly struct HeroSilhouette
    {
        /// <summary>Overall height in unit-mesh space. The crowd soldier is 1.0.</summary>
        public readonly float Height;
        /// <summary>Shoulder width. Where "broad" and "thin" actually live.</summary>
        public readonly float Shoulders;
        /// <summary>Forward lean in degrees. A crouched hunter reads at a glance.</summary>
        public readonly float Stoop;
        /// <summary>How far above the head the crest, crown or hood reaches.</summary>
        public readonly float Crest;
        /// <summary>Height of whatever is carried — staff, banner, spear.</summary>
        public readonly float Haft;
        /// <summary>Companions drawn at the hero's flank. Only the Houndmaster has any.</summary>
        public readonly int Companions;

        public HeroSilhouette(float height, float shoulders, float stoop, float crest,
            float haft, int companions)
        {
            Height = height;
            Shoulders = shoulders;
            Stoop = stoop;
            Crest = crest;
            Haft = haft;
            Companions = companions;
        }

        /// <summary>
        /// The hip line the crowd shader pivots the walk cycle about. Everything below it is
        /// a leg; everything above it is the body.
        /// </summary>
        public const float HipLine = 0.30f;
    }

    /// <summary>One hero: how they look, what they start with, and the one rule that is theirs.</summary>
    public readonly struct HeroProfile
    {
        public readonly HeroClass Id;
        public readonly string Name;
        public readonly string Tagline;
        /// <summary>What the hero's own body is painted.</summary>
        public readonly Rgb Body;
        /// <summary>What their soldiers are painted. The army reads as theirs.</summary>
        public readonly Rgb Army;
        /// <summary>Emission, so the hero reads against a dark road.</summary>
        public readonly Rgb Glow;
        public readonly HeroSilhouette Silhouette;
        /// <summary>Which of the four soldier shapes this hero's army carries.</summary>
        public readonly int SoldierKind;
        public readonly StatModifier[] Stats;

        public HeroProfile(HeroClass id, string name, string tagline, Rgb body, Rgb army,
            Rgb glow, HeroSilhouette silhouette, int soldierKind, StatModifier[] stats)
        {
            Id = id;
            Name = name;
            Tagline = tagline;
            Body = body;
            Army = army;
            Glow = glow;
            Silhouette = silhouette;
            SoldierKind = soldierKind;
            Stats = stats;
        }
    }

    /// <summary>
    /// The four heroes.
    ///
    /// THE REPORT WAS "my main figure — yellow soldier is very boring". It was one mesh and
    /// one gold material, chosen at bootstrap before a save slot even exists, identical for
    /// every player forever.
    ///
    /// WHAT MAKES THESE FOUR DIFFERENT GAMES RATHER THAN FOUR PAINT JOBS. Each carries three
    /// things, and the third is the one that matters:
    ///
    ///   1. a silhouette — broad, thin, crouched, skeletal
    ///   2. a stat block, which is a nudge and nothing more
    ///   3. ONE RULE that changes how the run is played
    ///
    /// The rules are deliberately spread across the four verbs the game already has — the
    /// shield, the spell, the gates, and losing men — so choosing a hero chooses which part
    /// of the existing game you lean on, rather than adding a fifth system nobody asked for.
    ///
    /// Stats are modest ON PURPOSE. A hero who starts 40% ahead is a hero everyone picks; the
    /// differences here are worth a few per cent, and the rule is what you actually feel.
    /// </summary>
    public static class HeroRoster
    {
        public const int Count = 4;

        /// <summary>The one a new player gets if they never choose. The gold soldier they know.</summary>
        public const HeroClass Default = HeroClass.Warden;

        private static readonly HeroProfile[] Table =
        {
            new HeroProfile(HeroClass.Warden, "WARDEN",
                "Holds the line. Two shields, and a blocked ambush joins you.",
                new Rgb(0.60f, 0.45f, 0.15f), new Rgb(0.22f, 0.32f, 0.78f),
                new Rgb(1.10f, 0.75f, 0.20f),
                // Broad and upright, with a crest. The only hero wider than he is tall in the
                // shoulders, which is what reads at ten pixels.
                new HeroSilhouette(1.06f, 0.46f, 0f, 0.20f, 1.05f, 0),
                1,  // SoldierKind.Shield
                new[]
                {
                    new StatModifier(StatIds.Health, ModifierKind.Flat, 30f),
                    new StatModifier(StatIds.ShieldDuration, ModifierKind.Flat, 0.5f),
                    new StatModifier(StatIds.ShieldCharges, ModifierKind.Flat, 1f)
                }),

            new HeroProfile(HeroClass.Ashcaller, "ASHCALLER",
                "Burns the road ahead. Two spells, and they reach further.",
                new Rgb(0.52f, 0.20f, 0.10f), new Rgb(0.85f, 0.32f, 0.12f),
                new Rgb(1.70f, 0.55f, 0.18f),
                // Tall, narrow, hooded, with the longest haft in the set — a staff that breaks
                // the skyline is the whole silhouette at distance.
                new HeroSilhouette(1.14f, 0.28f, -3f, 0.26f, 1.45f, 0),
                0,  // SoldierKind.Spear
                new[]
                {
                    new StatModifier(StatIds.SpellPower, ModifierKind.Flat, 0.15f),
                    new StatModifier(StatIds.Cooldown, ModifierKind.Flat, 0.10f),
                    new StatModifier(StatIds.SpellCharges, ModifierKind.Flat, 1f)
                }),

            new HeroProfile(HeroClass.Houndmaster, "HOUNDMASTER",
                "Two hounds at the flank. Every recruit gate pays more.",
                new Rgb(0.26f, 0.40f, 0.18f), new Rgb(0.34f, 0.62f, 0.26f),
                new Rgb(0.60f, 1.30f, 0.45f),
                // Crouched and low, and the only one with anything beside him.
                new HeroSilhouette(0.94f, 0.38f, 12f, 0.10f, 0.85f, 2),
                2,  // SoldierKind.Axe
                new[]
                {
                    new StatModifier(StatIds.GateYield, ModifierKind.Flat, 0.10f),
                    new StatModifier(StatIds.Magnetism, ModifierKind.Flat, 0.55f),
                    new StatModifier(StatIds.RunSpeed, ModifierKind.Flat, 0.04f)
                }),

            new HeroProfile(HeroClass.Revenant, "REVENANT",
                "The fallen come back. What you lose, you mostly get again.",
                new Rgb(0.44f, 0.42f, 0.50f), new Rgb(0.52f, 0.40f, 0.72f),
                new Rgb(0.95f, 0.70f, 1.55f),
                // Skeletal: tall, very narrow, stooped, with a broken crown.
                new HeroSilhouette(1.10f, 0.26f, 7f, 0.30f, 0.95f, 0),
                3,  // SoldierKind.Banner
                new[]
                {
                    new StatModifier(StatIds.Damage, ModifierKind.Flat, 6f),
                    new StatModifier(StatIds.SecondWind, ModifierKind.Flat, 0.20f),
                    new StatModifier(StatIds.Health, ModifierKind.Flat, -10f)
                })
        };

        public static HeroProfile For(HeroClass hero)
        {
            int i = (int)hero;
            return i < 0 || i >= Table.Length ? Table[(int)Default] : Table[i];
        }

        /// <summary>Every hero, in the order the select screen shows them.</summary>
        public static HeroProfile[] All()
        {
            var copy = new HeroProfile[Table.Length];
            Array.Copy(Table, copy, Table.Length);
            return copy;
        }

        /// <summary>
        /// The stat block as the select screen prints it, through the SAME formatter gear
        /// and talents use. A hero whose stats read in their own private format would be a
        /// second set of units for the player to learn for no reason.
        /// </summary>
        public static string StatLine(HeroClass hero)
        {
            StatModifier[] stats = For(hero).Stats;
            if (stats == null || stats.Length == 0) return string.Empty;
            var parts = new string[stats.Length];
            for (int i = 0; i < stats.Length; i++) parts[i] = StatFormat.Affix(stats[i]);
            return string.Join("   ", parts);
        }

        /// <summary>Reads a saved id safely. Anything unrecognised is the default.</summary>
        public static HeroClass FromSaved(int saved) =>
            saved < 0 || saved >= Count ? Default : (HeroClass)saved;

        // ===================================================================
        // The rules. Each is INERT for the three heroes who do not have it, so
        // threading them through paths every player runs is safe.
        // ===================================================================

        /// <summary>
        /// WARDEN. A blocked ambush does not merely cost nothing — some of the men who were
        /// coming for you join instead.
        ///
        /// It is a share of what the block SAVED rather than a flat number, so it scales with
        /// the army exactly as everything else in the game does, and it makes the shield a way
        /// to grow rather than only a way to survive. That is the Warden's whole identity: the
        /// other three answer danger by avoiding it.
        /// </summary>
        public static double BlockConverts(HeroClass hero, double blockedLoss) =>
            hero == HeroClass.Warden && blockedLoss > 0.0 ? blockedLoss * 0.30 : 0.0;

        /// <summary>
        /// ASHCALLER. The spell sweeps further.
        ///
        /// A multiplier on the clear range rather than extra damage: the spell is already the
        /// answer to ambushes, and reach is what decides whether it can be aimed at all — the
        /// range was the actual bug behind "the spell doesn't destroy enemy packs".
        /// </summary>
        public static float SpellReach(HeroClass hero) =>
            hero == HeroClass.Ashcaller ? 1.35f : 1f;

        /// <summary>
        /// HOUNDMASTER. Recruit gates pay more — and ONLY recruit gates.
        ///
        /// Not the rally arch, deliberately: a bonus on the multiply would compound with
        /// itself across a run and be worth an order of magnitude by the finish, which is the
        /// same trap that killed the x2 gate. A flat extra share on the adds is worth a steady
        /// few per cent a round and nothing more.
        /// </summary>
        public static double RecruitBonus(HeroClass hero) =>
            hero == HeroClass.Houndmaster ? 0.35 : 0.0;

        /// <summary>
        /// REVENANT. A share of every loss comes back, a moment later.
        ///
        /// Applied to what an ambush or a pack actually took, so it is a partial refund rather
        /// than a shield: the Revenant still loses ground, just less of it, and still has to
        /// answer the things everyone else answers. The delay is what makes it read as the
        /// dead standing back up instead of as the loss having been smaller.
        /// </summary>
        public static double Returns(HeroClass hero, double lost) =>
            hero == HeroClass.Revenant && lost > 0.0 ? lost * 0.34 : 0.0;

        /// <summary>Seconds before returning men arrive.</summary>
        public const float ReturnDelaySeconds = 0.9f;
    }
}
