using BattleRunner.Core.Audio;
using UnityEngine;

namespace BattleRunner.Meta.Services
{
    /// <summary>
    /// The game's whole audio system.
    ///
    /// THE PROJECT HAD NO SOUND AT ALL. Not "placeholder audio" — no clips, no AudioListener
    /// anywhere (the runtime camera never added one), no AudioManager.asset, and no code that
    /// so much as named UnityEngine.AudioSource. `com.unity.modules.audio` was in the manifest
    /// and nothing used it.
    ///
    /// A POOL WITH A HARD VOICE CAP, because doc 04 already specified one: sixteen sources,
    /// prewarmed at boot, never allocated during a run. A Ladder chunk can pass six gates in
    /// under a second while the crowd is biting packs and a boss is winding up, and unbounded
    /// AudioSource.PlayClipAtPoint would allocate a GameObject per hit.
    ///
    /// EVICTION IS BY PRIORITY, NOT BY AGE. A boss blow lost because the army was passing
    /// gates is a fairness problem rather than an audio one, so cues carry a priority and a
    /// louder one takes the quietest voice rather than being dropped.
    ///
    /// THE MUTE PREFERENCE IS IN PlayerPrefs, DELIBERATELY. PlayerProfile is per save SLOT and
    /// GameContext.SaveProfile refuses to write while no slot is active — so a preference
    /// stored there would be unwritable on exactly the screens that offer the button.
    /// </summary>
    public sealed class AudioDirector : IAudioService
    {
        public const string EnabledKey = "audio.enabled";

        /// <summary>Doc 04's budget. Sixteen is generous for a game with fifteen cues.</summary>
        private const int VoiceCount = 16;
        private const float MusicFadeSeconds = 1.2f;

        private readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        private readonly int[] _voicePriority = new int[VoiceCount];
        private readonly AudioClip[] _clips = new AudioClip[AudioCues.Count];
        private readonly float[] _lastPlayed = new float[AudioCues.Count];

        private AudioSource _ambient;
        private AudioSource _combat;
        private AudioLowPassFilter _ambientFilter;
        private AudioLowPassFilter _combatFilter;

        private MusicMood _mood = new MusicMood(1f, MusicMood.MaxCutoff, 0.6f);
        private bool _fighting;
        private bool _enabled = true;
        private bool _ready;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!_ready) return;
                if (!value) SilenceEverything();
                else ApplyMusic(instant: true);
            }
        }

        /// <summary>
        /// Build the listener, the pool and the two music sources, and load every clip.
        ///
        /// Everything degrades to silence rather than throwing. A missing clip logs once and
        /// leaves its slot null; Play then does nothing. The game shipped without sound for
        /// its whole life, so nothing here may be load-bearing.
        /// </summary>
        public void Initialize(Transform listenerHost, Transform poolRoot)
        {
            _enabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0;

            // There is no AudioListener in Main.unity and none was ever created at runtime,
            // so without this line every source below would play to nobody.
            if (listenerHost != null && listenerHost.GetComponent<AudioListener>() == null)
                listenerHost.gameObject.AddComponent<AudioListener>();

            for (int i = 0; i < AudioCues.Count; i++)
            {
                var mix = AudioCues.For((AudioCue)i);
                _clips[i] = Resources.Load<AudioClip>(AudioCues.ResourceFolder + mix.Clip);
                if (_clips[i] == null)
                    Debug.LogWarning($"[Audio] Missing clip '{mix.Clip}' for {(AudioCue)i}.");
                _lastPlayed[i] = float.NegativeInfinity;
            }

            for (int i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(poolRoot, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                // 2D. The camera sits behind the army looking down a corridor; panning a gate
                // chime by its lane would be a cue the player cannot act on and a distraction
                // from the one they can.
                s.spatialBlend = 0f;
                s.bypassReverbZones = true;
                _voices[i] = s;
            }

            _ambient = BuildMusicSource(poolRoot, "MusicAmbient", AudioCues.AmbientBed, out _ambientFilter);
            _combat = BuildMusicSource(poolRoot, "MusicCombat", AudioCues.BossBed, out _combatFilter);
            _ready = true;
            ApplyMusic(instant: true);
        }

        private static AudioSource BuildMusicSource(Transform root, string name, string clipName,
            out AudioLowPassFilter filter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = Resources.Load<AudioClip>(AudioCues.ResourceFolder + clipName);
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.bypassReverbZones = true;
            filter = go.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = MusicMood.MaxCutoff;
            if (s.clip == null) Debug.LogWarning($"[Audio] Missing music clip '{clipName}'.");
            return s;
        }

        public void Play(AudioCue cue) => Play(cue, 1f);

        public void Play(AudioCue cue, float scale)
        {
            if (!_ready || !_enabled) return;
            int index = (int)cue;
            if (index < 0 || index >= _clips.Length) return;
            AudioClip clip = _clips[index];
            if (clip == null) return;

            CueMix mix = AudioCues.For(cue);
            // The retrigger gate. Weaving a Ladder fires six add-gates in well under a second
            // and a dozen identical bells inside 200 ms is a buzz, not six times the payoff.
            float now = Time.unscaledTime;
            if (now - _lastPlayed[index] < mix.MinInterval) return;
            _lastPlayed[index] = now;

            AudioSource voice = TakeVoice(mix.Priority);
            if (voice == null) return;

            voice.clip = clip;
            voice.volume = Mathf.Clamp(mix.Volume * scale, 0f, 1f);
            voice.pitch = 1f + (Random.value - 0.5f) * 2f * mix.PitchJitter;
            voice.Play();
        }

        /// <summary>
        /// A free voice, or the quietest lower-priority one. Null when every voice is busy
        /// with something more important — which is the correct outcome, not a failure.
        /// </summary>
        private AudioSource TakeVoice(int priority)
        {
            int weakest = -1;
            int weakestPriority = int.MaxValue;
            float weakestVolume = float.MaxValue;

            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource s = _voices[i];
                if (!s.isPlaying)
                {
                    _voicePriority[i] = priority;
                    return s;
                }
                if (_voicePriority[i] < weakestPriority
                    || (_voicePriority[i] == weakestPriority && s.volume < weakestVolume))
                {
                    weakest = i;
                    weakestPriority = _voicePriority[i];
                    weakestVolume = s.volume;
                }
            }

            if (weakest < 0 || weakestPriority >= priority) return null;
            _voicePriority[weakest] = priority;
            return _voices[weakest];
        }

        public void SetMood(MusicMood mood)
        {
            _mood = mood;
            if (_ready) ApplyMusic(instant: false);
        }

        public void SetCombat(bool fighting)
        {
            if (_fighting == fighting) return;
            _fighting = fighting;
            if (_ready) ApplyMusic(instant: false);
        }

        public void StopMusic()
        {
            if (!_ready) return;
            _ambient.Stop();
            _combat.Stop();
        }

        /// <summary>
        /// Point both beds at the current mood and cross-fade between them.
        ///
        /// The fade is done with AudioSource.volume over frames rather than with a coroutine
        /// because there is no MonoBehaviour here on purpose — this is a plain service, like
        /// the ad and save services, and giving it a component would put it in the scene.
        /// GameFlowController drives Tick.
        /// </summary>
        private void ApplyMusic(bool instant)
        {
            if (!_enabled) { SilenceEverything(); return; }

            _ambient.pitch = _mood.Pitch;
            _combat.pitch = Mathf.Lerp(1f, _mood.Pitch, 0.5f);   // the fight is less themed
            _ambientFilter.cutoffFrequency = _mood.Cutoff;
            _combatFilter.cutoffFrequency = Mathf.Max(_mood.Cutoff, 3200f);

            if (_ambient.clip != null && !_ambient.isPlaying) _ambient.Play();
            if (_combat.clip != null && !_combat.isPlaying) _combat.Play();

            if (instant)
            {
                _ambient.volume = _fighting ? 0f : _mood.Volume;
                _combat.volume = _fighting ? _mood.Volume : 0f;
            }
        }

        private void SilenceEverything()
        {
            foreach (AudioSource s in _voices) if (s != null) s.Stop();
            if (_ambient != null) _ambient.volume = 0f;
            if (_combat != null) _combat.volume = 0f;
        }

        /// <summary>Drives the music cross-fade. Called once a frame from GameFlowController.</summary>
        public void Tick(float unscaledDelta)
        {
            if (!_ready) return;
            float step = unscaledDelta / MusicFadeSeconds;
            float ambientTarget = !_enabled || _fighting ? 0f : _mood.Volume;
            float combatTarget = !_enabled || !_fighting ? 0f : _mood.Volume;
            _ambient.volume = Mathf.MoveTowards(_ambient.volume, ambientTarget, step);
            _combat.volume = Mathf.MoveTowards(_combat.volume, combatTarget, step);
        }
    }
}
