using System.Collections.Generic;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Save;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class SkillTreeTests
    {
        private const int Plenty = 99;

        private static List<string> Taken(params string[] ids) => new List<string>(ids);

        [Test]
        public void EveryNodeIsReachableFromNothing()
        {
            // A talent nobody can ever buy is dead content. Spend freely and prove each one
            // can be reached by some legal order.
            foreach (SkillNode node in SkillTree.Nodes)
            {
                var taken = new List<string>();
                foreach (SkillNode prerequisite in SkillTree.Branch(node.Branch))
                {
                    if (prerequisite.Id == node.Id) continue;
                    if (prerequisite.Tier >= node.Tier) continue;
                    // Never take the target's exclusive sibling: reachability means SOME
                    // legal order exists, and taking a node's rival is not one of them.
                    if (prerequisite.Id == node.Excludes) continue;
                    if (SkillTree.CanTake(prerequisite.Id, taken, Plenty)) taken.Add(prerequisite.Id);
                }
                Assert.IsNull(SkillTree.BlockedReason(node.Id, taken, Plenty),
                    $"{node.Id} is unreachable");
            }
        }

        [Test]
        public void TierOneNeedsNothing()
        {
            var empty = new List<string>();
            foreach (SkillNode node in SkillTree.Nodes)
                if (node.Tier == 1)
                    Assert.IsTrue(SkillTree.CanTake(node.Id, empty, 1), $"{node.Id} should open the branch");
        }

        [Test]
        public void TierTwoNeedsItsBranchOpened()
        {
            Assert.IsNotNull(SkillTree.BlockedReason("wl_cleave", new List<string>(), Plenty),
                "tier 2 must not be reachable from nothing");
            Assert.IsNull(SkillTree.BlockedReason("wl_cleave", Taken("wl_edge"), Plenty));
        }

        [Test]
        public void TierThreeNeedsTwoInTheBranch()
        {
            Assert.IsNotNull(SkillTree.BlockedReason("wl_annihilate", Taken("wl_edge"), Plenty),
                "the capstone must cost a real commitment");
            Assert.IsNull(SkillTree.BlockedReason("wl_annihilate", Taken("wl_edge", "wl_cleave"), Plenty));
        }

        [Test]
        public void ExclusiveChoicesLockEachOtherOut()
        {
            // The whole point of the tier-2 pair: you cannot have both, so a build means something.
            foreach (SkillNode node in SkillTree.Nodes)
            {
                if (node.Excludes == null) continue;

                SkillNode sibling = SkillTree.Find(node.Excludes);
                Assert.IsNotNull(sibling, $"{node.Id} excludes a node that does not exist");
                Assert.AreEqual(node.Branch, sibling.Branch, "exclusivity must stay inside a branch");
                Assert.AreEqual(node.Tier, sibling.Tier, "exclusivity must stay inside a tier");
                Assert.AreEqual(node.Id, sibling.Excludes, "exclusion must be mutual");

                var opened = new List<string>();
                foreach (SkillNode t1 in SkillTree.Branch(node.Branch))
                    if (t1.Tier == 1) opened.Add(t1.Id);
                opened.Add(node.Id);

                StringAssert.Contains("rules it out",
                    SkillTree.BlockedReason(sibling.Id, opened, Plenty) ?? string.Empty);
            }
        }

        [Test]
        public void NoPointsMeansNoTalents()
        {
            Assert.AreEqual("No points to spend",
                SkillTree.BlockedReason("wl_edge", new List<string>(), 0));
        }

        [Test]
        public void ATalentCannotBeBoughtTwice()
        {
            Assert.AreEqual("Already learned",
                SkillTree.BlockedReason("wl_edge", Taken("wl_edge"), Plenty));
        }

        [Test]
        public void UnknownTalentIsRejected()
        {
            Assert.IsNotNull(SkillTree.BlockedReason("not_a_node", new List<string>(), Plenty));
            Assert.IsNull(SkillTree.Find("not_a_node"));
            Assert.AreEqual(0, SkillTree.PointsSpent(Taken("not_a_node")),
                "junk in a save must not consume points");
        }

        // --- What the tree is FOR -----------------------------------------------------

        [Test]
        public void EveryBranchPaysOffSomewhere()
        {
            foreach (SkillBranch branch in new[] { SkillBranch.Warlord, SkillBranch.Warden, SkillBranch.Zealot })
            {
                List<SkillNode> nodes = SkillTree.Branch(branch);
                Assert.AreEqual(4, nodes.Count, $"{branch} should be 1 + 2 exclusive + 1 capstone");
                foreach (SkillNode node in nodes)
                    Assert.IsNotEmpty(node.Modifiers, $"{node.Id} grants nothing");
            }
        }

        [Test]
        public void TheZealotBranchAffectsTheRunNotTheBoss()
        {
            // The reason the tree exists: the old three stats all only mattered during the
            // boss fight, so nothing a player bought changed the run itself.
            var runAxes = new HashSet<string>
            {
                StatIds.GateYield, StatIds.RunSpeed, StatIds.EnemyResist, StatIds.Fortune
            };

            bool touchesTheRun = false;
            foreach (SkillNode node in SkillTree.Branch(SkillBranch.Zealot))
                foreach (StatModifier m in node.Modifiers)
                    if (runAxes.Contains(m.StatId)) touchesTheRun = true;

            Assert.IsTrue(touchesTheRun, "the Zealot path must change how the run itself plays");
        }

        [Test]
        public void ModifiersAccumulateAcrossTakenNodes()
        {
            List<StatModifier> mods = SkillTree.ModifiersFor(Taken("zl_avarice", "zl_zeal"));
            float gateYield = 0f;
            foreach (StatModifier m in mods)
                if (m.StatId == StatIds.GateYield) gateYield += m.Value;

            Assert.AreEqual(0.20f, gateYield, 1e-4f, "0.12 from Avarice plus 0.08 from Zeal");
        }

        [Test]
        public void PointsSpentCountsOnlyRealNodes()
        {
            Assert.AreEqual(0, SkillTree.PointsSpent(new List<string>()));
            Assert.AreEqual(2, SkillTree.PointsSpent(Taken("wl_edge", "wd_hide")));
            Assert.AreEqual(2, SkillTree.PointsSpent(Taken("wl_edge", "wd_hide", "junk")));
        }

        [Test]
        public void ACompleteBranchCostsFourPointsAndLocksOneChoice()
        {
            var taken = new List<string>();
            foreach (SkillNode node in SkillTree.Branch(SkillBranch.Zealot))
                if (SkillTree.CanTake(node.Id, taken, Plenty)) taken.Add(node.Id);

            Assert.AreEqual(3, taken.Count, "tier 2 offers two but grants one");
            Assert.AreEqual(3, SkillTree.PointsSpent(taken));
        }

        [Test]
        public void NullAndEmptyInputsAreSafe()
        {
            Assert.IsEmpty(SkillTree.ModifiersFor(null));
            Assert.AreEqual(0, SkillTree.PointsSpent(null));
            Assert.AreEqual(0, SkillTree.CountInBranch(null, SkillBranch.Warlord));
        }

        // --- Migration ------------------------------------------------------------------

        [Test]
        public void OldStatPointsAreRefundedAsAFreeRespec()
        {
            // v3 saves spent points on three flat stats that no longer exist. There is no
            // faithful mapping onto talents, so the migration hands the points back.
            var old = new PlayerProfile { SchemaVersion = 3, UnspentStatPoints = 1 };
            old.StatPoints.Add(new StatSpend { StatId = StatIds.Damage, Points = 4 });
            old.StatPoints.Add(new StatSpend { StatId = StatIds.Health, Points = 2 });

            PlayerProfile migrated = SaveMigrator.Migrate(old);

            Assert.AreEqual(SaveMigrator.CurrentVersion, migrated.SchemaVersion);
            Assert.AreEqual(7, migrated.UnspentStatPoints, "1 unspent + 4 + 2 refunded");
            Assert.IsEmpty(migrated.StatPoints);
            Assert.IsNotNull(migrated.SkillNodes);
            Assert.IsEmpty(migrated.SkillNodes);
        }

        [Test]
        public void AFreshProfileHasAnEmptyTree()
        {
            var fresh = new PlayerProfile { SchemaVersion = SaveMigrator.CurrentVersion };
            SaveMigrator.Migrate(fresh);

            Assert.IsNotNull(fresh.SkillNodes);
            Assert.IsEmpty(fresh.SkillNodes);
            Assert.AreEqual(0, SkillTree.PointsSpent(fresh.SkillNodes));
        }
    }
}
