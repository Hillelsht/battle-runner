using System;
using System.Collections.Generic;
using BattleRunner.Core.Heroes;
using BattleRunner.Core.Save;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The four heroes. The tests that matter are not "does the table have four rows" — they
    /// are the two properties that decide whether a roster is a CHOICE or a trap: no hero is
    /// strictly better than another, and every rule is inert for the three who lack it.
    /// </summary>
    [TestFixture]
    public class HeroRosterTests
    {
        [Test]
        public void EveryHeroInTheEnumHasAProfile()
        {
            foreach (HeroClass hero in Enum.GetValues(typeof(HeroClass)))
            {
                HeroProfile p = HeroRoster.For(hero);
                Assert.AreEqual(hero, p.Id, $"{hero} resolved to {p.Id}");
                Assert.IsNotEmpty(p.Name);
                Assert.IsNotEmpty(p.Tagline);
                Assert.IsNotNull(p.Stats);
            }
            Assert.AreEqual(HeroRoster.Count, HeroRoster.All().Length);
        }

        [Test]
        public void NoHeroIsStrictlyBetterThanAnother()
        {
            // THE PROPERTY THAT MAKES IT A CHOICE. If one hero's stat block dominates
            // another's on every axis, the roster has a right answer and three decorations.
            HeroProfile[] all = HeroRoster.All();
            foreach (HeroProfile a in all)
                foreach (HeroProfile b in all)
                {
                    if (a.Id == b.Id) continue;
                    bool aBeatsBEverywhere = true;
                    foreach (string stat in StatIds.All)
                    {
                        if (Value(a, stat) < Value(b, stat)) { aBeatsBEverywhere = false; break; }
                    }
                    // Only a problem if a is also strictly ahead somewhere.
                    bool aAheadSomewhere = false;
                    foreach (string stat in StatIds.All)
                        if (Value(a, stat) > Value(b, stat)) { aAheadSomewhere = true; break; }

                    Assert.IsFalse(aBeatsBEverywhere && aAheadSomewhere,
                        $"{a.Name} dominates {b.Name} on every stat");
                }
        }

        [Test]
        public void EveryHeroLeansOnADifferentVerb()
        {
            // Four heroes whose rules all touched the same system would be one hero with four
            // hats. Each rule is checked to fire for exactly one of them.
            Assert.AreEqual(1, CountWhere(h => HeroRoster.BlockConverts(h, 100.0) > 0.0), "shield rule");
            Assert.AreEqual(1, CountWhere(h => HeroRoster.SpellReach(h) > 1f), "spell rule");
            Assert.AreEqual(1, CountWhere(h => HeroRoster.RecruitBonus(h) > 0.0), "gate rule");
            Assert.AreEqual(1, CountWhere(h => HeroRoster.Returns(h, 100.0) > 0.0), "loss rule");
        }

        [Test]
        public void EveryRuleIsInertForTheHeroesWhoLackIt()
        {
            // The safety property. These are threaded through paths every player runs, so a
            // rule that is not exactly zero for the other three changes everyone's game.
            foreach (HeroClass h in Enum.GetValues(typeof(HeroClass)))
            {
                if (h != HeroClass.Warden) Assert.AreEqual(0.0, HeroRoster.BlockConverts(h, 500.0));
                if (h != HeroClass.Ashcaller) Assert.AreEqual(1f, HeroRoster.SpellReach(h));
                if (h != HeroClass.Houndmaster) Assert.AreEqual(0.0, HeroRoster.RecruitBonus(h));
                if (h != HeroClass.Revenant) Assert.AreEqual(0.0, HeroRoster.Returns(h, 500.0));
            }
        }

        [Test]
        public void NoRuleTurnsALossIntoAGain()
        {
            // The Revenant refunds; he must not profit. A loss that pays more than it costs
            // would make walking into ambushes correct.
            Assert.Less(HeroRoster.Returns(HeroClass.Revenant, 100.0), 100.0);
            // And the Warden's conversion is a share of what the block SAVED, so it likewise
            // cannot exceed it.
            Assert.Less(HeroRoster.BlockConverts(HeroClass.Warden, 100.0), 100.0);
        }

        [Test]
        public void RulesAreInertOnZeroAndNegativeInput()
        {
            foreach (HeroClass h in Enum.GetValues(typeof(HeroClass)))
            {
                Assert.AreEqual(0.0, HeroRoster.Returns(h, 0.0));
                Assert.AreEqual(0.0, HeroRoster.Returns(h, -50.0));
                Assert.AreEqual(0.0, HeroRoster.BlockConverts(h, 0.0));
                Assert.AreEqual(0.0, HeroRoster.BlockConverts(h, -50.0));
            }
        }

        [Test]
        public void EveryHeroKeepsItsLegsUnderTheHipLine()
        {
            // LOAD-BEARING, and the reason the silhouette is numbers rather than a mesh.
            // CrowdInstanced swings everything below y = 0.30 about that line to make the
            // march cycle. A hero shorter than the hip line would walk from the waist.
            foreach (HeroProfile p in HeroRoster.All())
            {
                Assert.Greater(p.Silhouette.Height, HeroSilhouette.HipLine * 2f,
                    $"{p.Name} is too short to have legs below the hip line");
                Assert.Greater(p.Silhouette.Shoulders, 0f, $"{p.Name} has no width");
                Assert.Greater(p.Silhouette.Haft, 0f, $"{p.Name} carries nothing");
            }
        }

        [Test]
        public void HeroesAreTellableApartAtTenPixels()
        {
            // At 26 m a unit is about ten pixels tall, so only the outline reads. Two heroes
            // whose height AND width AND stoop all match would be the same figure in two
            // colours, which is exactly the complaint this feature answers.
            HeroProfile[] all = HeroRoster.All();
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                {
                    HeroSilhouette a = all[i].Silhouette, b = all[j].Silhouette;
                    float difference = Math.Abs(a.Height - b.Height)
                                       + Math.Abs(a.Shoulders - b.Shoulders) * 2f
                                       + Math.Abs(a.Stoop - b.Stoop) * 0.02f
                                       + Math.Abs(a.Haft - b.Haft)
                                       + Math.Abs(a.Companions - b.Companions) * 0.5f;
                    Assert.Greater(difference, 0.20f,
                        $"{all[i].Name} and {all[j].Name} have nearly the same outline");
                }
        }

        [Test]
        public void EveryHeroPaintsItsArmyDifferently()
        {
            // The army is the thing on screen, so its colour is most of what a hero looks
            // like. Two heroes sharing an army tint would be indistinguishable in play.
            HeroProfile[] all = HeroRoster.All();
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                {
                    float d = Math.Abs(all[i].Army.R - all[j].Army.R)
                              + Math.Abs(all[i].Army.G - all[j].Army.G)
                              + Math.Abs(all[i].Army.B - all[j].Army.B);
                    Assert.Greater(d, 0.25f,
                        $"{all[i].Name} and {all[j].Name} field the same colour army");
                }
        }

        [Test]
        public void ASavedIdIsReadBackSafely()
        {
            Assert.AreEqual(HeroClass.Ashcaller, HeroRoster.FromSaved(1));
            Assert.AreEqual(HeroRoster.Default, HeroRoster.FromSaved(-1), "a corrupt id must not throw");
            Assert.AreEqual(HeroRoster.Default, HeroRoster.FromSaved(99));
            Assert.AreEqual(HeroRoster.Default, HeroRoster.For((HeroClass)77).Id);
        }

        [Test]
        public void AnExistingSaveKeepsTheHeroItHasBeenPlaying()
        {
            // A veteran must not be sent to a character screen and invited to change who they
            // have been for twenty rounds. Same reasoning as the v2 -> v3 tutorial migration.
            var old = new PlayerProfile { SchemaVersion = 6 };
            SaveMigrator.Migrate(old);

            Assert.AreEqual(SaveMigrator.CurrentVersion, old.SchemaVersion);
            Assert.IsTrue(old.HeroChosen, "an existing save was sent back to the choice screen");
            Assert.AreEqual(HeroRoster.Default, HeroRoster.FromSaved(old.HeroId),
                "an existing save changed character during a migration");
        }

        [Test]
        public void ABrandNewProfileHasNotChosenYet()
        {
            // The other half: "chose the Warden" and "was never asked" both read as HeroId 0,
            // which is why HeroChosen exists at all.
            var fresh = new PlayerProfile();
            Assert.IsFalse(fresh.HeroChosen);
        }

        private static float Value(HeroProfile hero, string statId)
        {
            float total = 0f;
            foreach (StatModifier m in hero.Stats)
                if (m.StatId == statId) total += m.Value;
            return total;
        }

        /// <summary>
        /// The select screen's stat line has to name every modifier a hero actually carries.
        ///
        /// This is the one place the table and the UI can drift apart silently: adding a
        /// fourth modifier to a hero and forgetting the screen would ship a character whose
        /// advertised block is wrong, and nothing would fail.
        /// </summary>
        [Test]
        public void TheStatLineNamesEveryModifierAHeroHas()
        {
            foreach (HeroClass h in Enum.GetValues(typeof(HeroClass)))
            {
                string line = HeroRoster.StatLine(h);
                Assert.That(line, Is.Not.Empty, $"{h} prints no stats at all");
                foreach (StatModifier m in HeroRoster.For(h).Stats)
                {
                    string name = StatFormat.DisplayName(m.StatId);
                    Assert.That(line, Does.Contain(name),
                        $"{h}'s stat line omits {m.StatId}");
                }
            }
        }

        /// <summary>
        /// A hero's stats compose as MODIFIERS, which means StatSheet has to resolve every id
        /// in the table. An id nobody else uses — a typo, or a stat that was renamed — would
        /// resolve to nothing and the hero would quietly have two stats instead of three.
        /// </summary>
        [Test]
        public void EveryHeroStatResolvesThroughTheSheet()
        {
            var baseStats = new Dictionary<string, float>();
            foreach (HeroClass h in Enum.GetValues(typeof(HeroClass)))
                foreach (StatModifier m in HeroRoster.For(h).Stats)
                    baseStats[m.StatId] = 0f;

            foreach (HeroClass h in Enum.GetValues(typeof(HeroClass)))
            {
                StatSheet sheet = StatSheet.Resolve(baseStats, HeroRoster.For(h).Stats);
                foreach (StatModifier m in HeroRoster.For(h).Stats)
                {
                    Assert.That(sheet.Get(m.StatId), Is.EqualTo(m.Value).Within(1e-4f),
                        $"{h}'s {m.StatId} did not survive resolution");
                }
            }
        }

        /// <summary>
        /// The Warden converts a block; the Revenant refunds a loss. Neither may ever be
        /// worth MORE than the thing it is answering, or blocking becomes a way to farm and
        /// the best play is to stand in front of the biggest ambush on the road.
        /// </summary>
        [Test]
        public void AnAnswerIsNeverWorthMoreThanTheThreat()
        {
            foreach (double threat in new[] { 0.5, 1.0, 37.0, 4100.0, 9.2e9 })
            {
                Assert.That(HeroRoster.BlockConverts(HeroClass.Warden, threat),
                    Is.LessThan(threat), $"a block on {threat} paid at least as much back");
                Assert.That(HeroRoster.Returns(HeroClass.Revenant, threat),
                    Is.LessThan(threat), $"a loss of {threat} refunded at least as much");
            }
        }

        private static int CountWhere(Func<HeroClass, bool> predicate)
        {
            int n = 0;
            foreach (HeroClass h in Enum.GetValues(typeof(HeroClass)))
                if (predicate(h)) n++;
            return n;
        }
    }
}
