using System;

namespace BattleRunner.Meta.Services
{
    public enum AdPlacement
    {
        LootDouble = 0,
        Resurrect = 1
    }

    /// <summary>
    /// Monetization seam (doc 01, R7): gameplay talks to this interface only; a real
    /// SDK (LevelPlay/AdMob) binds behind it post-MVP without touching game code.
    /// </summary>
    public interface IAdService
    {
        bool IsRewardedReady(AdPlacement placement);

        /// <summary>Callback receives true only when the reward was actually earned.</summary>
        void ShowRewarded(AdPlacement placement, Action<bool> onDone);

        /// <summary>Pumped by the game loop so mock delays work without a MonoBehaviour.</summary>
        void Tick(float deltaTime);
    }
}
