using BattleRunner.Core.Save;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class SaveSlotTests
    {
        [Test]
        public void EachSlotGetsItsOwnFile()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int slot = 0; slot < SaveSlots.Count; slot++)
                Assert.IsTrue(seen.Add(SaveSlots.FileName(slot)), $"slot {slot} collides with another");

            Assert.AreEqual(SaveSlots.Count, seen.Count);
        }

        [Test]
        public void NoSlotStealsTheLegacyFileName()
        {
            // Slot 0 ADOPTS the pre-slots save by moving it; if it simply shared the name,
            // erasing slot 0 would delete a file the migration still expects to find.
            for (int slot = 0; slot < SaveSlots.Count; slot++)
                Assert.AreNotEqual(SaveSlots.LegacyFileName, SaveSlots.FileName(slot));
        }

        [Test]
        public void SlotsOutsideTheRangeAreRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SaveSlots.FileName(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SaveSlots.FileName(SaveSlots.Count));
        }

        [Test]
        public void AnAbsentFileReadsAsAnEmptySlot()
        {
            SaveSlotSummary summary = SaveSlots.Summarize(1, null, fileExists: false);

            Assert.IsFalse(summary.Occupied);
            StringAssert.Contains("SLOT 2", summary.Describe());
            StringAssert.Contains("New game", summary.Describe());
        }

        [Test]
        public void AnExistingFileReportsProgress()
        {
            var profile = new PlayerProfile { CurrentLevelIndex = 2, UnspentStatPoints = 3 };
            profile.SkillNodes.Add("wl_edge");
            profile.SkillNodes.Add("wl_cleave");

            SaveSlotSummary summary = SaveSlots.Summarize(0, profile, fileExists: true);

            Assert.IsTrue(summary.Occupied);
            Assert.AreEqual(2, summary.LevelIndex);
            Assert.AreEqual(2, summary.TalentsLearned);

            string text = summary.Describe();
            StringAssert.Contains("SLOT 1", text);
            StringAssert.Contains("Level 3", text, "levels are 1-based on screen");
            StringAssert.Contains("2 talents", text);
            StringAssert.Contains("3 unspent", text);
        }

        [Test]
        public void OneTalentReadsAsSingular()
        {
            var profile = new PlayerProfile();
            profile.SkillNodes.Add("wl_edge");
            StringAssert.Contains("1 talent ", SaveSlots.Summarize(0, profile, true).Describe() + " ");
        }

        [Test]
        public void NoUnspentPointsAreNotMentioned()
        {
            var profile = new PlayerProfile { CurrentLevelIndex = 1 };
            StringAssert.DoesNotContain("unspent", SaveSlots.Summarize(0, profile, true).Describe());
        }

        [Test]
        public void AnUnplayedProfileIsRecognisedAsUntouched()
        {
            Assert.IsTrue(SaveSlots.IsUntouched(null));
            Assert.IsTrue(SaveSlots.IsUntouched(new PlayerProfile()));

            var played = new PlayerProfile { CurrentLevelIndex = 1 };
            Assert.IsFalse(SaveSlots.IsUntouched(played));

            var taught = new PlayerProfile { TutorialMask = 1 };
            Assert.IsFalse(SaveSlots.IsUntouched(taught), "a started tutorial counts as played");
        }
    }
}
