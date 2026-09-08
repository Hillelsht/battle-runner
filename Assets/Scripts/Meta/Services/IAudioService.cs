using BattleRunner.Core.Audio;

namespace BattleRunner.Meta.Services
{
    /// <summary>
    /// Everything the game asks of audio. An interface for the same reason the ad and IAP
    /// services have one: a caller must never care whether sound is on, whether a clip
    /// loaded, or whether this device has an audio device at all.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>Whether sound plays at all. Persisted globally, not per save slot.</summary>
        bool Enabled { get; set; }

        /// <summary>Fire a one-shot. Silently ignored when muted, throttled or unloaded.</summary>
        void Play(AudioCue cue);

        /// <summary>Fire a one-shot louder or quieter than its table entry, 0..2.</summary>
        void Play(AudioCue cue, float scale);

        /// <summary>Bend the ambient bed to a world. Called once a round, from the loader.</summary>
        void SetMood(MusicMood mood);

        /// <summary>Cross-fade to the boss layer, or back to the ambient bed.</summary>
        void SetCombat(bool fighting);

        /// <summary>Stop the beds. Used when the run ends and the menus take over.</summary>
        void StopMusic();
    }
}
