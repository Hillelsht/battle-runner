namespace BattleRunner.Meta.Services
{
    /// <summary>
    /// Battle pass seam only (doc 01, R7). The MVP ships no server, no purchase flow,
    /// and no season content; this interface exists so the LiveOps build can bind a
    /// remote-config-driven implementation without touching game code.
    /// </summary>
    public interface IBattlePassService
    {
        bool IsSeasonActive { get; }
        int CurrentTier { get; }
        void AddSeasonXp(int amount);
    }

    public sealed class DisabledBattlePassService : IBattlePassService
    {
        public bool IsSeasonActive => false;
        public int CurrentTier => 0;
        public void AddSeasonXp(int amount) { }
    }
}
