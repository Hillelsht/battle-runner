using System;
using System.Collections.Generic;
using BattleRunner.Core.Audio;
using BattleRunner.Core.World;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The mix and the music mood.
    ///
    /// Nothing here can be listened to from CI, so what is checked is what would fail
    /// silently: a cue with no clip, a clip nobody plays, a throttle so long a burst goes
    /// quiet, a priority order that lets a boss blow be evicted by a gate chime, and eight
    /// worlds that all sound the same — which is the audio version of the complaint that
    /// started this whole piece of work.
    /// </summary>
    [TestFixture]
    public class AudioTests
    {
        [Test]
        public void EveryCueHasAMix()
        {
            // The enum is the contract. A cue added without a table row is a silent bug: it
            // compiles, it plays nothing, and nothing says why.
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                CueMix mix = AudioCues.For(cue);
                Assert.IsNotNull(mix.Clip, cue.ToString());
                Assert.IsNotEmpty(mix.Clip, cue.ToString());
                Assert.Greater(mix.Volume, 0f, $"{cue} is inaudible");
                Assert.LessOrEqual(mix.Volume, 1f, $"{cue} would clip");
                Assert.GreaterOrEqual(mix.PitchJitter, 0f, cue.ToString());
                Assert.Less(mix.PitchJitter, 0.4f, $"{cue} would sound out of tune");
                Assert.GreaterOrEqual(mix.MinInterval, 0f, cue.ToString());
                Assert.GreaterOrEqual(mix.Priority, 1, cue.ToString());
            }
            Assert.AreEqual(Enum.GetValues(typeof(AudioCue)).Length, AudioCues.Count);
        }

        [Test]
        public void NoTwoCuesShareAClip()
        {
            // Two cues on one file is almost always a copy-paste in the table, and it makes
            // one of the two events unlearnable.
            var seen = new HashSet<string>();
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
                Assert.IsTrue(seen.Add(AudioCues.For(cue).Clip),
                    $"{cue} reuses the clip {AudioCues.For(cue).Clip}");
        }

        [Test]
        public void ClipNamesAreLoadableResourcePaths()
        {
            // Resources.Load takes a path with no extension and no leading slash. A name with
            // either loads null at runtime and logs nothing useful.
            foreach (string name in AudioCues.AllClipNames())
            {
                Assert.IsFalse(name.Contains("."), $"'{name}' carries an extension");
                Assert.IsFalse(name.StartsWith("/"), $"'{name}' starts with a slash");
                Assert.IsFalse(name.Contains(" "), $"'{name}' has a space in it");
            }
            Assert.AreEqual(AudioCues.Count + 2, AudioCues.AllClipNames().Length,
                "the two music beds are not in the loadable set");
            StringAssert.EndsWith("/", AudioCues.ResourceFolder);
        }

        [Test]
        public void TheLoudestCuesAreAlsoTheHardestToEvict()
        {
            // The voice pool evicts the lowest priority first. If a gate chime outranked a
            // boss blow, a player weaving a Ladder during a wind-up would stop hearing the
            // one cue they have to react to — a fairness problem, not an audio one.
            int blow = AudioCues.For(AudioCue.BossBlow).Priority;
            int block = AudioCues.For(AudioCue.ShieldBlock).Priority;
            int death = AudioCues.For(AudioCue.BossDeath).Priority;
            foreach (AudioCue quiet in new[]
                     { AudioCue.GateAdd, AudioCue.GateMultiply, AudioCue.GateSubtract,
                       AudioCue.EnemyBite, AudioCue.UiTap, AudioCue.BossHit })
            {
                Assert.Less(AudioCues.For(quiet).Priority, blow, $"{quiet} can evict a boss blow");
                Assert.Less(AudioCues.For(quiet).Priority, death, $"{quiet} can evict a boss death");
            }
            Assert.GreaterOrEqual(block, 4, "a blocked blow can be evicted");
        }

        [Test]
        public void TheThrottleIsShortEnoughToKeepUpWithTheGame()
        {
            // A Ladder chunk can pass gates about six metres apart at 10 m/s, so roughly one
            // every 0.6 s — but the crowd hits several lanes' worth in bursts. The gate cues
            // must throttle enough to stop a buzz and not so much that a burst goes silent.
            foreach (AudioCue cue in new[]
                     { AudioCue.GateAdd, AudioCue.GateMultiply, AudioCue.GateSubtract,
                       AudioCue.EnemyBite })
                Assert.Less(AudioCues.For(cue).MinInterval, 0.12f,
                    $"{cue} would swallow a burst");

            // And the rare, long cues must not stack on top of themselves.
            Assert.GreaterOrEqual(AudioCues.For(AudioCue.BossDeath).MinInterval, 0.5f);
            Assert.GreaterOrEqual(AudioCues.For(AudioCue.RoundStart).MinInterval, 0.3f);
        }

        [Test]
        public void EveryWorldSoundsDifferentFromEveryOther()
        {
            // One bed serves all eight worlds. That only works if the bending is real: two
            // worlds with the same pitch and the same filter are the same world with a
            // different sky, which is precisely the complaint this work exists to answer.
            var moods = new List<MusicMood>();
            foreach (WorldTheme t in WorldThemes.All)
                moods.Add(MusicMood.For(t.FogEnd, t.SkyZenith.R, t.SkyZenith.B, t.Accent.Chroma));

            for (int a = 0; a < moods.Count; a++)
            for (int b = a + 1; b < moods.Count; b++)
            {
                float pitchGap = Math.Abs(moods[a].Pitch - moods[b].Pitch);
                float cutoffGap = Math.Abs(moods[a].Cutoff - moods[b].Cutoff) / MusicMood.MaxCutoff;
                Assert.Greater(pitchGap + cutoffGap, 0.012f,
                    $"{WorldThemes.At(a).DisplayName} and {WorldThemes.At(b).DisplayName} "
                    + "sound identical");
            }
        }

        [Test]
        public void MoodStaysInsideWhatAPitchShiftCanSurvive()
        {
            // Past about a fifth either way a re-pitched loop audibly stretches and the drone
            // stops being a drone. The clamps exist for that; this proves the derivation
            // never leans on them, which would mean two worlds silently landing on the rail.
            foreach (WorldTheme t in WorldThemes.All)
            {
                MusicMood m = MusicMood.For(t.FogEnd, t.SkyZenith.R, t.SkyZenith.B, t.Accent.Chroma);
                Assert.Greater(m.Pitch, MusicMood.MinPitch, t.DisplayName);
                Assert.Less(m.Pitch, MusicMood.MaxPitch, t.DisplayName);
                Assert.GreaterOrEqual(m.Cutoff, MusicMood.MinCutoff, t.DisplayName);
                Assert.LessOrEqual(m.Cutoff, MusicMood.MaxCutoff, t.DisplayName);
                Assert.Greater(m.Volume, 0.2f, $"{t.DisplayName} is nearly silent");
                Assert.LessOrEqual(m.Volume, 1f, t.DisplayName);
            }
        }

        [Test]
        public void ACloseWorldSoundsMoreMuffledThanAnOpenOne()
        {
            // The whole derivation in one assertion: fog is enclosure, and enclosure is a
            // low-pass. The Sunken Crypt fogs out at 105 m, the Bone Wastes at 185.
            MusicMood crypt = MoodOf("The Sunken Crypt");
            MusicMood wastes = MoodOf("The Bone Wastes");
            Assert.Less(crypt.Cutoff, wastes.Cutoff,
                "the flooded crypt sounds as open as the open wastes");
        }

        [Test]
        public void AColdSkyPitchesUpAndAWarmOneDown()
        {
            // The Frozen Reach's zenith is (0.020, 0.030, 0.058) — blue-dominant. Ember
            // Fields is (0.045, 0.022, 0.020) — red-dominant. They must not agree.
            Assert.Greater(MoodOf("The Frozen Reach").Pitch, MoodOf("Ember Fields").Pitch,
                "a frozen world does not sit above a burning one");
        }

        [Test]
        public void AWorldWithNoColourStillGetsAUsableMood()
        {
            // Guards the degenerate inputs a ninth world could arrive with: a black sky and
            // no fog. Nothing may divide by zero, and nothing may come back silent.
            MusicMood m = MusicMood.For(0f, 0f, 0f, 0f);
            Assert.GreaterOrEqual(m.Pitch, MusicMood.MinPitch);
            Assert.LessOrEqual(m.Pitch, MusicMood.MaxPitch);
            Assert.GreaterOrEqual(m.Cutoff, MusicMood.MinCutoff);
            Assert.Greater(m.Volume, 0f);
        }

        private static MusicMood MoodOf(string displayName)
        {
            foreach (WorldTheme t in WorldThemes.All)
                if (t.DisplayName == displayName)
                    return MusicMood.For(t.FogEnd, t.SkyZenith.R, t.SkyZenith.B, t.Accent.Chroma);
            throw new AssertionException($"no world called {displayName}");
        }
    }
}
