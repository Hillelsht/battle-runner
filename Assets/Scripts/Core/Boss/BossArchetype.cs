namespace BattleRunner.Core.Boss
{
    /// <summary>
    /// What a boss DOES, which is also what it looks like.
    ///
    /// The game shipped with two bosses that differed in tint and stats and in nothing
    /// else: same mesh, same single telegraph-then-hit pattern, and a level lookup that
    /// clamped past the last authored level, so from round six onward it was the same
    /// fight forever. A boss that is only a different number is not a different boss.
    ///
    /// One archetype is one silhouette AND one power, deliberately coupled. A player has
    /// about a second of telegraph to decide what to do, and they will only ever learn
    /// "the hunched one drains you" if the hunched one is always the one that drains.
    /// Splitting look from behaviour would double the content and halve the legibility.
    ///
    /// The values behind each of these live in BossSim, engine-free, so the fight can be
    /// tested without a device — which matters more here than anywhere else in the game,
    /// because a boss pattern that is wrong is only discoverable by losing to it.
    /// </summary>
    public enum BossArchetype
    {
        /// <summary>One big telegraphed blow. The fight the game already had.</summary>
        Slam = 0,

        /// <summary>Three fast blows in a row; one well-timed shield covers all of them.</summary>
        Volley = 1,

        /// <summary>Carries a ward that eats damage until it breaks. Spells strip it fastest.</summary>
        Warded = 2,

        /// <summary>Bleeds force continuously. Only a raised shield stops the tick.</summary>
        Drain = 3,

        /// <summary>Calls adds that bite on the next cycle unless a spell clears them.</summary>
        Summoner = 4,

        /// <summary>Attacks faster and faster as its own health falls.</summary>
        Enrage = 5
    }
}
