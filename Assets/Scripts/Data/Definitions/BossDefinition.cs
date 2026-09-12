using BattleRunner.Core.Boss;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Boss", fileName = "Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        public string DisplayName = "Bone Colossus";

        [Tooltip("What this boss DOES and what it looks like — the two are deliberately coupled.")]
        public BossArchetype Archetype = BossArchetype.Slam;
        [Tooltip("HP at level 0; grows exponentially per level (BossSim.BossHp).")]
        public float BaseHp = 500f;
        /// <summary>
        /// How much harder this boss is than the same boss one act earlier, as a fraction.
        ///
        /// IT USED TO MEAN SOMETHING ELSE AND THE DIFFERENCE WAS THE GAME'S WORST BUG. It was
        /// compounded on the ROUND index while a boss is fought once per ACT, so 0.25 meant
        /// about 3.0x more health between consecutive fights. It now compounds per act, on top
        /// of a health figure already scaled by the army and the stat points the player
        /// provably has at that depth, so this number carries ONLY the difficulty screw — a
        /// few per cent per act, meaning exactly "a little harder than the last one".
        ///
        /// Hence 0.25-0.29 becoming 0.05-0.07. It is not the same quantity made smaller; it is
        /// a different quantity. See BossSim.BossHp.
        /// </summary>
        [Range(0f, 1f)] public float PerLevelGrowth = 0.06f;
        [Tooltip("Seconds between boss attacks.")]
        public float AttackIntervalSeconds = 4f;
        [Tooltip("Seconds of telegraph before each attack lands — the shield window.")]
        public float TelegraphSeconds = 1.2f;
        [Range(0f, 1f)] public float HitFraction = 0.3f;
        public Color TintColor = new Color(0.6f, 0.2f, 0.2f);

        [Tooltip("Emission the boss heats toward while winding up. Its own colour, so a player learns whose telegraph they are watching.")]
        [ColorUsage(false, true)] public Color TelegraphColor = new Color(1.4f, 0.5f, 0.2f);
    }
}
