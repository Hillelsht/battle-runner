using BattleRunner.Core.Heroes;
using UnityEngine;

namespace BattleRunner.Gameplay.Crowd
{
    /// <summary>
    /// Dresses the arena as one hero: the leader's mesh and colours, the army's colours, and
    /// which soldier the army mostly is.
    ///
    /// ONE PLACE, CALLED FROM TWO. A slot is activated (Continue, or slot select) and a hero
    /// is chosen (the select screen) — both have to end with the same arena, and the way that
    /// goes wrong is one path repainting the army and the other forgetting to. So this reads
    /// the choice straight off the profile rather than taking it as an argument: there is no
    /// way to call it with a hero the save does not hold.
    ///
    /// It is safe before the arena exists. Every field it touches is checked, because the
    /// bootstrap builds the scene and activates a slot in an order this must not depend on.
    /// </summary>
    public static class HeroOutfit
    {
        public static void Apply(GameContext ctx)
        {
            if (ctx == null || ctx.Profile == null) return;
            HeroProfile hero = HeroRoster.For(HeroRoster.FromSaved(ctx.Profile.HeroId));

            ctx.Hero?.Wear(hero, ProceduralMeshes.Hero(hero.Id));
            ctx.CrowdRenderer?.Favor(hero.SoldierKind);

            // The army takes the hero's colours too. This is most of what makes a run FEEL
            // like a different character: the leader is one figure among a few hundred, and
            // at 0.47 scale the crowd is the thing the eye actually reads.
            if (ctx.CrowdMaterialInstance != null)
            {
                ctx.CrowdMaterialInstance.SetColorSafe("_BaseColor", ThemePalette.ToColor(hero.Army));
                // Emission is the hero's glow held well down: the crowd runs a tight rim and
                // almost no flat term, so the leader's full-brightness glow across three
                // hundred bodies would be a wall of bloom rather than an army.
                ctx.CrowdMaterialInstance.SetColorSafe("_EmissionColor",
                    ThemePalette.ToColor(hero.Glow.Scaled(0.55f)));
            }

            // AFTER the repaint, never before: the ward caches the resting colours it has to
            // put back, and repainting under it is exactly how an army ends a run stuck on
            // the previous hero's tint.
            ctx.Ward?.RefreshRest();
        }
    }
}
