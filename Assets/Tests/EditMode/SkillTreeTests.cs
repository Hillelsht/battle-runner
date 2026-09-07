using System.Collections.Generic;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Save;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The tree is design, not content, so its rules are pinned here rather than
    /// discovered on a phone. These run under plain <c>dotnet test</c> against the
    /// engine-free mirror as well as in Unity's Test Runner.
    /// </summary>
    [TestFixture]
    public class SkillTreeTests
    {
        private const int Plenty = 10_000;

        private static Dictionary<string, int> Ranks(params string[] ids)
        {
            var map = new Dictionary<string, int>();
            foreach (string id in ids) map[id] = map.TryGetValue(id, out int r) ? r + 1 : 1;
            return map;
        }

        /// <summary>Buys ranks in a branch, cheapest tier first, until the target is met.</summary>
        private static Dictionary<string, int> Invested(SkillBranch branch, int points)
        {
            var map = new Dictionary<string, int>();
            int spent = 0;
            foreach (SkillNode node in SkillTree.Branch(branch))
            {
                while (spent < points && SkillTree.CanRank(node.Id, map, Plenty))
                {
                    map[node.Id] = SkillTree.RankOf(map, node.Id) + 1;
                    spent++;
                }
                if (spent >= points) break;
            }
            return map;
        }

        // --- Shape -------------------------------------------------------------

        [Test]
        public void TreeIsDeepEnoughToOutlastTheOldOne()
        {
            // The old tree had NINE takeable points and emptied in three boss kills. The
            // whole point of this one is that it does not.
            Assert.Greater(SkillTree.TotalRanks(), 150,
                "the tree should carry well over a hundred point-spends");
            Assert.Greater(SkillTree.Nodes.Count, 40);
        }

        [Test]
        public void EveryBranchReachesTierSix()
        {
            foreach (SkillBranch branch in new[] { SkillBranch.Warlord, SkillBranch.Warden, SkillBranch.Zealot })
            {
                int deepest = 0;
                foreach (SkillNode node in SkillTree.Branch(branch))
                    if (node.Tier > deepest) deepest = node.Tier;
                Assert.AreEqual(SkillTree.MaxTier, deepest, $"{branch} should run the full depth");
            }
        }

        [Test]
        public void EveryBranchHasExactlyThreeMutuallyExclusiveKeystones()
        {
            foreach (SkillBranch branch in new[] { SkillBranch.Warlord, SkillBranch.Warden, SkillBranch.Zealot })
            {
                var keystones = new List<SkillNode>();
                foreach (SkillNode node in SkillTree.Branch(branch))
                    if (node.Kind == SkillKind.Keystone) keystones.Add(node);

                Assert.AreEqual(3, keystones.Count, $"{branch} keystones");
                foreach (SkillNode k in keystones)
                {
                    Assert.AreEqual(SkillTree.MaxTier, k.Tier);
                    Assert.IsNotNull(k.Excludes, $"{k.Id} must rule out its rivals");
                    // Each keystone names the other two, so taking one closes the path.
                    Assert.AreEqual(2, k.Excludes.Split('|').Length, $"{k.Id} exclusions");
                }
            }
        }

        [Test]
        public void EveryNodeIsReachableByInvestingInItsBranch()
        {
            foreach (SkillNode node in SkillTree.Nodes)
            {
                if (node.Branch == SkillBranch.Crossroads) continue;
                Dictionary<string, int> map = Invested(node.Branch, (node.Tier - 1) * SkillTree.TierUnlockCost);
                Assert.IsNull(SkillTree.BlockedReason(node.Id, map, Plenty),
                    $"{node.Id} should open once its tier is paid for");
            }
        }

        [Test]
        public void EveryNodeHasAtLeastOneModifierAndAName()
        {
            foreach (SkillNode node in SkillTree.Nodes)
            {
                Assert.IsNotEmpty(node.PerRank, $"{node.Id} does nothing");
                Assert.IsNotEmpty(node.DisplayName, $"{node.Id} has no name");
                Assert.IsNotEmpty(node.Description, $"{node.Id} has no description");
                Assert.GreaterOrEqual(node.MaxRanks, 1);
                foreach (StatModifier m in node.PerRank)
                    Assert.Contains(m.StatId, StatIds.All, $"{node.Id} modifies an unknown stat");
            }
        }

        [Test]
        public void OnlyStatsWithARealBaseTakePercentModifiers()
        {
            // StatSheet resolves final = (base + flat) * (1 + percent). Every fraction-valued
            // stat has a base of zero, so a Percent modifier on one multiplies nothing and
            // the node is silently inert — the exact bug that made two spell talents dead.
            foreach (SkillNode node in SkillTree.Nodes)
            foreach (StatModifier m in node.PerRank)
            {
                if (m.Kind != ModifierKind.Percent) continue;
                Assert.IsFalse(StatFormat.IsFraction(m.StatId),
                    $"{node.Id} grants Percent on {m.StatId}, whose base is 0 — it would do nothing");
            }
        }

        // --- Rules -------------------------------------------------------------

        [Test]
        public void TierGatesBlockUntilTheBranchIsPaidFor()
        {
            var empty = new Dictionary<string, int>();
            SkillNode deep = null;
            foreach (SkillNode node in SkillTree.Branch(SkillBranch.Warlord))
                if (node.Tier == 3) { deep = node; break; }

            Assert.IsNotNull(deep);
            Assert.IsNotNull(SkillTree.BlockedReason(deep.Id, empty, Plenty),
                "a tier-3 node must not open on an empty branch");
            Assert.IsNull(SkillTree.BlockedReason(deep.Id,
                Invested(SkillBranch.Warlord, 2 * SkillTree.TierUnlockCost), Plenty));
        }

        [Test]
        public void TierOneOpensImmediately()
        {
            var empty = new Dictionary<string, int>();
            foreach (SkillNode node in SkillTree.Nodes)
                if (node.Tier == 1 && node.Branch != SkillBranch.Crossroads)
                    Assert.IsTrue(SkillTree.CanRank(node.Id, empty, 1), $"{node.Id} should open the branch");
        }

        [Test]
        public void RanksStopAtTheirCeiling()
        {
            var map = new Dictionary<string, int>();
            SkillNode node = SkillTree.Find("wl_edge");
            for (int i = 0; i < node.MaxRanks; i++)
            {
                Assert.IsTrue(SkillTree.CanRank(node.Id, map, Plenty), $"rank {i + 1} should be buyable");
                map[node.Id] = i + 1;
            }
            Assert.AreEqual("Fully ranked", SkillTree.BlockedReason(node.Id, map, Plenty));
        }

        [Test]
        public void ModifiersScaleWithRank()
        {
            List<StatModifier> one = SkillTree.ModifiersFor(Ranks("wl_edge"));
            var five = SkillTree.ModifiersFor(new Dictionary<string, int> { ["wl_edge"] = 5 });
            Assert.AreEqual(1, one.Count);
            Assert.AreEqual(5, five.Count, "five ranks should contribute five times");
        }

        [Test]
        public void RanksBeyondTheCeilingAreIgnoredWhenResolving()
        {
            // A hand-edited save must not become a stat cheat.
            var absurd = new Dictionary<string, int> { ["wl_edge"] = 9999 };
            Assert.AreEqual(SkillTree.Find("wl_edge").MaxRanks,
                SkillTree.ModifiersFor(absurd).Count);
        }

        [Test]
        public void KeystonesRuleEachOtherOut()
        {
            var held = Invested(SkillBranch.Warlord, 5 * SkillTree.TierUnlockCost);
            held["wl_annihilation"] = 1;
            Assert.IsNotNull(SkillTree.BlockedReason("wl_headsman", held, Plenty));
            Assert.IsNotNull(SkillTree.BlockedReason("wl_tempest", held, Plenty));
        }

        [Test]
        public void CrossroadsNeedsBothBranches()
        {
            SkillNode hybrid = SkillTree.Find("cr_warpriest");
            Assert.IsNotNull(hybrid);
            int need = hybrid.Tier * SkillTree.TierUnlockCost;

            Dictionary<string, int> oneSide = Invested(SkillBranch.Warlord, need);
            Assert.IsNotNull(SkillTree.BlockedReason(hybrid.Id, oneSide, Plenty),
                "one branch alone must not open a hybrid");

            foreach (KeyValuePair<string, int> pair in Invested(SkillBranch.Zealot, need))
                oneSide[pair.Key] = pair.Value;
            Assert.IsNull(SkillTree.BlockedReason(hybrid.Id, oneSide, Plenty));
        }

        [Test]
        public void NoPointsMeansNoRank()
        {
            Assert.AreEqual("No points to spend",
                SkillTree.BlockedReason("wl_edge", new Dictionary<string, int>(), 0));
        }

        [Test]
        public void UnknownNodesAreRefusedRatherThanCrashing()
        {
            Assert.IsNotNull(SkillTree.BlockedReason("not_a_node", new Dictionary<string, int>(), Plenty));
            Assert.IsNull(SkillTree.Find("not_a_node"));
            Assert.AreEqual(0, SkillTree.PointsSpent(Ranks("not_a_node")));
            Assert.IsEmpty(SkillTree.ModifiersFor(Ranks("not_a_node")));
        }

        [Test]
        public void NullsAreTolerated()
        {
            Assert.IsEmpty(SkillTree.ModifiersFor(null));
            Assert.AreEqual(0, SkillTree.PointsSpent(null));
            Assert.AreEqual(0, SkillTree.PointsIn(null, SkillBranch.Warlord));
            Assert.AreEqual(0, SkillTree.RankOf(null, "wl_edge"));
            Assert.IsFalse(SkillTree.AnyKeystoneTaken(null));
        }

        // --- Unlearning --------------------------------------------------------

        [Test]
        public void ALeafRankCanAlwaysBeHandedBack()
        {
            var map = Ranks("wl_edge");
            Assert.IsNull(SkillTree.UnlearnBlockedReason("wl_edge", map));
            Assert.IsTrue(SkillTree.CanUnlearn("wl_edge", map));
        }

        [Test]
        public void UnlearningIsBlockedWhenItWouldStrandADeeperNode()
        {
            Dictionary<string, int> deep = Invested(SkillBranch.Warlord, 2 * SkillTree.TierUnlockCost);
            SkillNode tier3 = null;
            foreach (SkillNode node in SkillTree.Branch(SkillBranch.Warlord))
                if (node.Tier == 3) { tier3 = node; break; }
            deep[tier3.Id] = 1;

            // Something in tier 1 or 2 now props up the tier-3 node, and the refusal names it.
            string firstId = null;
            foreach (SkillNode node in SkillTree.Branch(SkillBranch.Warlord))
                if (node.Tier == 1 && SkillTree.RankOf(deep, node.Id) > 0) { firstId = node.Id; break; }

            Assert.IsNotNull(firstId);
            Assert.AreEqual($"Unlearn {tier3.DisplayName} first",
                SkillTree.UnlearnBlockedReason(firstId, deep));
            Assert.IsNull(SkillTree.UnlearnBlockedReason(tier3.Id, deep),
                "the deepest node itself always comes off");
        }

        [Test]
        public void UnlearningNeverBlocksAcrossBranches()
        {
            var map = Invested(SkillBranch.Warlord, 3 * SkillTree.TierUnlockCost);
            map["wd_hide"] = 1;
            Assert.IsNull(SkillTree.UnlearnBlockedReason("wd_hide", map));
        }

        [Test]
        public void UnlearningRefusesWhatIsNotHeld()
        {
            Assert.AreEqual("Not learned",
                SkillTree.UnlearnBlockedReason("wl_edge", new Dictionary<string, int>()));
            Assert.AreEqual("Not learned", SkillTree.UnlearnBlockedReason("wl_edge", null));
            Assert.AreEqual("Unknown talent",
                SkillTree.UnlearnBlockedReason("not_a_node", Ranks("not_a_node")));
        }

        [Test]
        public void AFullyInvestedTreeCanBeUnwoundEntirely()
        {
            // Buy everything buyable, then hand it all back one rank at a time. If any
            // ordering could deadlock, this is where it shows up.
            var map = new Dictionary<string, int>();
            bool bought = true;
            while (bought)
            {
                bought = false;
                foreach (SkillNode node in SkillTree.Nodes)
                {
                    if (!SkillTree.CanRank(node.Id, map, Plenty)) continue;
                    map[node.Id] = SkillTree.RankOf(map, node.Id) + 1;
                    bought = true;
                }
            }

            Assert.Greater(SkillTree.PointsSpent(map), 100, "should have bought a lot");

            int guard = 0;
            while (SkillTree.PointsSpent(map) > 0 && guard++ < 10_000)
            {
                string next = null;
                foreach (KeyValuePair<string, int> pair in map)
                {
                    if (pair.Value <= 0) continue;
                    if (!SkillTree.CanUnlearn(pair.Key, map)) continue;
                    next = pair.Key;
                    break;
                }
                Assert.IsNotNull(next, "the tree deadlocked — no rank could be handed back");
                int rank = map[next];
                if (rank <= 1) map.Remove(next); else map[next] = rank - 1;
            }

            Assert.AreEqual(0, SkillTree.PointsSpent(map));
        }

        // --- Paragon -----------------------------------------------------------

        [Test]
        public void ParagonIsLockedUntilAKeystoneIsHeld()
        {
            var none = new Dictionary<string, int>();
            Assert.AreEqual("Reach a keystone first",
                Paragon.BlockedReason("pg_might", none, none, Plenty));

            var withKeystone = new Dictionary<string, int> { ["wl_annihilation"] = 1 };
            Assert.IsNull(Paragon.BlockedReason("pg_might", none, withKeystone, Plenty));
        }

        [Test]
        public void ParagonCostEscalatesButNeverStalls()
        {
            Assert.AreEqual(1, Paragon.NextRankCost(0));
            Assert.AreEqual(2, Paragon.NextRankCost(Paragon.CostStep));
            Assert.AreEqual(3, Paragon.NextRankCost(Paragon.CostStep * 2));
            for (int held = 0; held < 500; held++)
                Assert.GreaterOrEqual(Paragon.NextRankCost(held + 1), Paragon.NextRankCost(held),
                    "cost must never fall as ranks accumulate");
        }

        [Test]
        public void ParagonValueAlwaysGrowsAndAlwaysDiminishes()
        {
            Paragon.Track track = Paragon.Find("pg_might");
            Assert.IsNotNull(track);
            Assert.AreEqual(0f, Paragon.ValueAt(track, 0));

            float previousStep = float.MaxValue;
            for (int rank = 1; rank < 300; rank++)
            {
                float step = Paragon.ValueAt(track, rank) - Paragon.ValueAt(track, rank - 1);
                Assert.Greater(step, 0f, $"rank {rank} must still be worth something");
                Assert.LessOrEqual(step, previousStep + 1e-4f, $"rank {rank} must not out-earn rank {rank - 1}");
                previousStep = step;
            }
        }

        [Test]
        public void ParagonModifiersUseRealStats()
        {
            foreach (Paragon.Track track in Paragon.Tracks)
                Assert.Contains(track.StatId, StatIds.All, $"{track.Id} modifies an unknown stat");

            var held = new Dictionary<string, int> { ["pg_might"] = 3 };
            List<StatModifier> mods = Paragon.ModifiersFor(held);
            Assert.AreEqual(1, mods.Count, "a track contributes one summed modifier, not one per rank");
            Assert.Greater(mods[0].Value, 0f);
        }

        [Test]
        public void ParagonToleratesJunk()
        {
            Assert.IsEmpty(Paragon.ModifiersFor(null));
            Assert.AreEqual(0, Paragon.TotalRanks(null));
            Assert.AreEqual(0, Paragon.TotalRanks(new Dictionary<string, int> { ["nope"] = 4 }));
            Assert.IsNotNull(Paragon.BlockedReason("nope", null, null, Plenty));
        }

        // --- Save --------------------------------------------------------------

        [Test]
        public void V4SavesMigrateEveryTakenNodeToRankOne()
        {
            var profile = new PlayerProfile { SchemaVersion = 4 };
            profile.SkillNodes.Add("wl_edge");
            profile.SkillNodes.Add("wd_hide");

            PlayerProfile migrated = SaveMigrator.Migrate(profile);

            Assert.AreEqual(SaveMigrator.CurrentVersion, migrated.SchemaVersion);
            Assert.IsEmpty(migrated.SkillNodes, "the legacy list is drained");
            Dictionary<string, int> ranks = migrated.SkillRankMap();
            Assert.AreEqual(1, SkillTree.RankOf(ranks, "wl_edge"));
            Assert.AreEqual(1, SkillTree.RankOf(ranks, "wd_hide"));
        }

        [Test]
        public void V4NodesThatNoLongerExistAreRefundedRatherThanLost()
        {
            // The v5 table renamed most of the old ids. A rank pointing at nothing would be
            // a point the player paid for and can never see or reclaim.
            var profile = new PlayerProfile { SchemaVersion = 4 };
            profile.SkillNodes.Add("wl_annihilate");   // v4 id, gone in v5
            profile.SkillNodes.Add("wl_edge");         // survives

            PlayerProfile migrated = SaveMigrator.Migrate(profile);

            Assert.AreEqual(1, migrated.UnspentStatPoints, "the dead node's point comes back");
            Assert.AreEqual(1, SkillTree.RankOf(migrated.SkillRankMap(), "wl_edge"));
        }

        [Test]
        public void AFreshProfileIsEmptyAndUntouched()
        {
            var fresh = new PlayerProfile { SchemaVersion = SaveMigrator.CurrentVersion };
            Assert.IsNotNull(fresh.SkillRanks);
            Assert.IsEmpty(fresh.SkillRanks);
            Assert.AreEqual(0, SkillTree.PointsSpent(fresh.SkillRankMap()));
            Assert.AreEqual(0, Paragon.TotalRanks(fresh.ParagonRankMap()));
            Assert.IsTrue(SaveSlots.IsUntouched(fresh));
        }

        [Test]
        public void RankMapsRoundTripThroughTheSerialisedList()
        {
            var profile = new PlayerProfile();
            var map = new Dictionary<string, int> { ["wl_edge"] = 3, ["wd_hide"] = 5 };
            PlayerProfile.WriteMap(profile.SkillRanks, map);

            Dictionary<string, int> read = profile.SkillRankMap();
            Assert.AreEqual(3, read["wl_edge"]);
            Assert.AreEqual(5, read["wd_hide"]);
            Assert.AreEqual(8, SkillTree.PointsSpent(read));
        }

        [Test]
        public void DuplicateEntriesInAMangledSaveSumRatherThanVanish()
        {
            var profile = new PlayerProfile();
            profile.SkillRanks.Add(new RankEntry { Id = "wl_edge", Rank = 2 });
            profile.SkillRanks.Add(new RankEntry { Id = "wl_edge", Rank = 1 });
            Assert.AreEqual(3, profile.SkillRankMap()["wl_edge"]);
        }
    }
}
