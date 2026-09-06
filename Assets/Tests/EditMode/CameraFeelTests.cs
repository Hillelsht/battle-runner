using BattleRunner.Core.Feel;
using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class CameraFeelTests
    {
        [Test]
        public void ScaleIsTheRatioNotTheDifference()
        {
            // The whole reason this lives in Core. Force is unbounded, so scaling on the
            // DIFFERENCE would make a late-game gate shake the screen off its hinges
            // while the identical early-game moment did nothing.
            CameraFeel small = CameraFeel.ForGate(GateOp.Multiply, 10, 20);
            CameraFeel large = CameraFeel.ForGate(GateOp.Multiply, 1000, 2000);
            Assert.AreEqual(small.Trauma, large.Trauma, 1e-5f, "a doubling is a doubling");
            Assert.AreEqual(small.KickY, large.KickY, 1e-5f);
        }

        [Test]
        public void BiggerSwingsHitHarderAndSaturate()
        {
            float x2 = CameraFeel.ForGate(GateOp.Multiply, 100, 200).Trauma;
            float x4 = CameraFeel.ForGate(GateOp.Multiply, 100, 400).Trauma;
            float x64 = CameraFeel.ForGate(GateOp.Multiply, 100, 6400).Trauma;
            Assert.Less(x2, x4, "two octaves must beat one");
            Assert.AreEqual(x4, x64, 1e-5f, "saturates at two octaves so nothing runs away");
        }

        [Test]
        public void GainKicksUpAndLossKicksDown()
        {
            Assert.Greater(CameraFeel.ForGate(GateOp.Add, 50, 80).KickY, 0f);
            Assert.Less(CameraFeel.ForGate(GateOp.Subtract, 80, 50).KickY, 0f);
            Assert.Less(CameraFeel.ForLoss(80, 40).KickY, 0f);
        }

        [Test]
        public void EveryImpulseStaysInsideTheShakeBudget()
        {
            // Trauma is clamped to 1 by the rig, but a single event that already saturates
            // it means every event feels identical. Nothing may exceed 0.85 on its own.
            var probes = new[]
            {
                CameraFeel.ForGate(GateOp.Multiply, 1, 1_000_000),
                CameraFeel.ForGate(GateOp.Subtract, 1_000_000, 1),
                CameraFeel.ForLoss(1_000_000, 1),
                CameraFeel.ForBossStrike(1_000_000, 1, blocked: false),
                CameraFeel.BossDefeated, CameraFeel.Revive, CameraFeel.Blocked,
            };
            foreach (CameraFeel f in probes)
            {
                Assert.GreaterOrEqual(f.Trauma, 0f);
                Assert.LessOrEqual(f.Trauma, 0.85f, "one event must not saturate the pool");
                Assert.LessOrEqual(System.Math.Abs(f.KickY), 0.25f, "kick must stay small");
            }
        }

        [Test]
        public void DegenerateCountsDoNotThrowOrProduceNonsense()
        {
            // A crowd wiped to zero, or a gate resolving from zero, must not divide by it.
            foreach (CameraFeel f in new[]
            {
                CameraFeel.ForGate(GateOp.Add, 0, 10),
                CameraFeel.ForGate(GateOp.Subtract, 10, 0),
                CameraFeel.ForLoss(0, 0),
                CameraFeel.ForBossStrike(0, 0, blocked: false),
            })
            {
                Assert.IsFalse(float.IsNaN(f.Trauma) || float.IsInfinity(f.Trauma));
                Assert.IsFalse(float.IsNaN(f.KickY) || float.IsInfinity(f.KickY));
                Assert.GreaterOrEqual(f.Trauma, 0f);
            }
        }

        [Test]
        public void ABlockedBlowIsLighterThanOneThatLands()
        {
            CameraFeel blocked = CameraFeel.ForBossStrike(100, 60, blocked: true);
            CameraFeel landed = CameraFeel.ForBossStrike(100, 60, blocked: false);
            Assert.Less(blocked.Trauma, landed.Trauma, "blocking must feel better than not");
            Assert.AreEqual(0f, blocked.KickY, 1e-5f, "a blocked blow does not rock the frame");
        }

        [Test]
        public void AMultiplyGateOutpunchesAnAddOfTheSameRatio()
        {
            Assert.Greater(CameraFeel.ForGate(GateOp.Multiply, 100, 200).Trauma,
                           CameraFeel.ForGate(GateOp.Add, 100, 200).Trauma);
        }
    }
}
