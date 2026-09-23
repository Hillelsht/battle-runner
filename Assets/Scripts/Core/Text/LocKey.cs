namespace BattleRunner.Core.Text
{
    /// <summary>
    /// Every translatable string in the game, as a key.
    ///
    /// ONE MEMBER PER STRING, grouped by where it is read rather than by which file it used to
    /// live in — a player meets these in an order, and the order they are declared in is the
    /// order someone translating them should see them.
    ///
    /// Values are never persisted, so members may be inserted anywhere. What must hold is that
    /// all three tables in <see cref="LocTables"/> carry every member exactly once, which is a
    /// test rather than a convention.
    /// </summary>
    public enum LocKey
    {
        // ---- the tutorial -------------------------------------------------------------
        TutorialSteerTitle,
        TutorialSteerDetail,
        TutorialGateTitle,
        TutorialGateDetail,
        TutorialCastTitle,
        TutorialCastDetail,
        TutorialBlockTitle,
        TutorialBlockDetail,

        // ---- the four heroes ----------------------------------------------------------
        HeroWardenName,
        HeroWardenTagline,
        HeroAshcallerName,
        HeroAshcallerTagline,
        HeroHoundmasterName,
        HeroHoundmasterTagline,
        HeroRevenantName,
        HeroRevenantTagline,

        // ---- the eight worlds ---------------------------------------------------------
        WorldAshenRoad,
        WorldThistlewood,
        WorldSunkenCrypt,
        WorldEmberFields,
        WorldBoneWastes,
        WorldFrozenReach,
        WorldBloodMarsh,
        WorldThroneOfDust,

        // ---- boss affixes -------------------------------------------------------------
        AffixFrenzied,
        AffixArmoured,
        AffixVampiric,
        AffixHaunted,
        AffixColossal,

        /// <summary>
        /// How an affix and a boss name go together. A template rather than a concatenation
        /// because Hebrew puts the adjective AFTER the noun — "{1} {0}" — where English and
        /// Russian put it before.
        /// </summary>
        AffixDecorate,

        // ---- paragon ------------------------------------------------------------------
        ParagonMight,
        ParagonVigor,
        ParagonGates,
        ParagonFortune,
        ParagonFocus,
        ParagonSpeed,
        ParagonUnknownPath,
        ParagonNeedKeystone,
        ParagonCostsPoints,

        // ---- save slots ---------------------------------------------------------------
        SlotNumber,
        SlotNewGame,
        SlotLevel,
        SlotTalents,
        SlotUnspent
    }
}
