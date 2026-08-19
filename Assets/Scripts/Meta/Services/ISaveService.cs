using BattleRunner.Core.Save;

namespace BattleRunner.Meta.Services
{
    public interface ISaveService
    {
        /// <summary>Loads and migrates the profile; returns a fresh profile when no save exists or the file is corrupt.</summary>
        PlayerProfile Load();

        void Save(PlayerProfile profile);
    }
}
