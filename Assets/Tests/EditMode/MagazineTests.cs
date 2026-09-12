using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The spell and the shield both run on one of these, so every property here is a
    /// property of both abilities.
    /// </summary>
    [TestFixture]
    public class MagazineTests
    {
        private static Magazine Ready(int capacity, float refill)
        {
            var m = new Magazine();
            m.Configure(capacity, refill);
            m.Refill();
            return m;
        }

        [Test]
        public void ASingleChargeMagazineIsExactlyTheOldCooldown()
        {
            // The safety property. A player who has bought no charge talents must get the
            // ability that shipped before magazines existed, to the frame.
            Magazine m = Ready(1, 6f);
            Assert.IsTrue(m.Ready);
            Assert.IsTrue(m.TrySpend());
            Assert.IsFalse(m.Ready);
            Assert.AreEqual(6f, m.RefillRemaining, 1e-5f);

            m.Tick(5.9f);
            Assert.IsFalse(m.Ready);
            m.Tick(0.2f);
            Assert.IsTrue(m.Ready);
            Assert.AreEqual(0f, m.RefillRemaining);
        }

        [Test]
        public void TheTimerRunsWheneverTheMagazineIsShort()
        {
            // The rule that makes a second charge worth a talent point rather than merely
            // convenient: spending one starts the clock, so the third charge is filling
            // while the player still has the second in hand.
            Magazine m = Ready(3, 4f);
            Assert.IsTrue(m.TrySpend());
            Assert.IsTrue(m.TrySpend());
            Assert.AreEqual(1, m.Charges);

            m.Tick(4f);
            Assert.AreEqual(2, m.Charges, "a charge must return while others are still held");
        }

        [Test]
        public void SpendingTheLastChargeDoesNotResetProgressTowardTheNext()
        {
            Magazine m = Ready(2, 10f);
            m.TrySpend();
            m.Tick(6f);
            float before = m.RefillRemaining;
            m.TrySpend();
            Assert.AreEqual(before, m.RefillRemaining, 1e-5f,
                "emptying the magazine must not throw away six seconds of waiting");
        }

        [Test]
        public void AnEmptyMagazineRefusesToSpendAndChangesNothing()
        {
            Magazine m = Ready(1, 3f);
            m.TrySpend();
            float before = m.RefillRemaining;
            Assert.IsFalse(m.TrySpend());
            Assert.AreEqual(before, m.RefillRemaining, 1e-6f);
            Assert.AreEqual(0, m.Charges);
        }

        [Test]
        public void WideningTheMagazineDoesNotHandOutTheNewCharges()
        {
            // Or equipping a charge talent mid-run would be a free cast, and a stat that
            // pays out on the frame it is applied is one players learn to toggle rather
            // than to build around.
            Magazine m = Ready(1, 5f);
            m.TrySpend();
            m.Configure(3, 5f);
            Assert.AreEqual(0, m.Charges, "widening a magazine must not fill it");
            Assert.AreEqual(3, m.Capacity);
        }

        [Test]
        public void NarrowingTheMagazineSpillsTheChargesItCanNoLongerHold()
        {
            Magazine m = Ready(4, 5f);
            m.Configure(2, 5f);
            Assert.AreEqual(2, m.Charges);
        }

        [Test]
        public void FillReadsAsAContinuousDialRatherThanJumpingAPipAtATime()
        {
            Magazine m = Ready(2, 4f);
            Assert.AreEqual(1f, m.Fill, 1e-5f);
            m.TrySpend();
            Assert.AreEqual(0.5f, m.Fill, 1e-5f);
            m.Tick(2f);
            Assert.AreEqual(0.75f, m.Fill, 1e-5f, "half a charge back is a quarter of a full dial");
            m.Tick(2f);
            Assert.AreEqual(1f, m.Fill, 1e-5f);
        }

        [Test]
        public void SeveralChargesCanLandInOnePathologicalFrame()
        {
            Magazine m = Ready(4, 1f);
            m.TrySpend(); m.TrySpend(); m.TrySpend();
            Assert.AreEqual(1, m.Charges);
            m.Tick(10f);
            Assert.AreEqual(4, m.Charges);
            Assert.AreEqual(0f, m.RefillRemaining);
        }

        [Test]
        public void ConfigureRefusesNonsenseRatherThanPropagatingIt()
        {
            var m = new Magazine();
            m.Configure(0, 0f);
            Assert.AreEqual(1, m.Capacity, "a magazine of zero would silently disable the ability");
            Assert.GreaterOrEqual(m.RefillSeconds, 0.1f,
                "a zero refill would make the charge infinite");
        }
    }
}
