using BattleRunner.Core.Save;
using BattleRunner.Core.Tutorial;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class TutorialDirectorTests
    {
        private static readonly TutorialStep[] AllSteps =
        {
            TutorialStep.Steer, TutorialStep.Gate, TutorialStep.Spell, TutorialStep.Shield
        };

        [Test]
        public void FreshPlayer_HasNothingTaught()
        {
            var director = new TutorialDirector();
            foreach (TutorialStep step in AllSteps) Assert.IsFalse(director.IsDone(step));
            Assert.IsFalse(director.IsComplete);
            Assert.IsFalse(director.HasPrompt);
            Assert.IsFalse(director.HoldsRun, "nothing is armed, so nothing may hold the run");
        }

        [Test]
        public void ArmedStep_ShowsItsPrompt()
        {
            var director = new TutorialDirector();
            Assert.IsTrue(director.TryArm(TutorialStep.Steer));
            Assert.IsTrue(director.HasPrompt);
            Assert.AreEqual(TutorialStep.Steer, director.ActiveStep);
            Assert.IsNotEmpty(director.Headline);
            Assert.IsNotEmpty(director.Detail);
        }

        [Test]
        public void OnlyOnePromptAtATime()
        {
            var director = new TutorialDirector();
            Assert.IsTrue(director.TryArm(TutorialStep.Steer));
            Assert.IsFalse(director.TryArm(TutorialStep.Spell), "a second prompt must not stack");
            Assert.AreEqual(TutorialStep.Steer, director.ActiveStep);
        }

        [Test]
        public void MatchingSignal_CompletesTheStep()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);
            director.Observe(TutorialSignal.LaneChanged);

            Assert.IsTrue(director.IsDone(TutorialStep.Steer));
            Assert.IsFalse(director.HasPrompt);
            Assert.IsFalse(director.HoldsRun);
        }

        [Test]
        public void WrongSignal_DoesNotCompleteTheStep()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);
            director.Observe(TutorialSignal.SpellCast);

            Assert.IsFalse(director.IsDone(TutorialStep.Steer));
            Assert.IsTrue(director.HasPrompt, "an unrelated action must not dismiss the lesson");
        }

        [Test]
        public void TaughtStep_NeverArmsAgain()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);
            director.Observe(TutorialSignal.LaneChanged);

            Assert.IsFalse(director.TryArm(TutorialStep.Steer));
            Assert.IsFalse(director.HasPrompt);
        }

        // --- The property that matters most: it can never strand a player ------------

        [Test]
        public void EveryStep_ReleasesTheRunOnTimeout()
        {
            foreach (TutorialStep step in AllSteps)
            {
                var director = new TutorialDirector();
                director.TryArm(step);

                // A player who does nothing at all, ticked well past the deadline.
                for (int frame = 0; frame < 600; frame++) director.Tick(1f / 60f);

                Assert.IsFalse(director.HoldsRun, $"{step} still held the run after 10 s");
                Assert.IsFalse(director.HasPrompt, $"{step} was still on screen after 10 s");
                Assert.IsTrue(director.IsDone(step), $"{step} must not be offered again");
            }
        }

        [Test]
        public void APlayerWhoIgnoresEverything_FinishesTheTutorial()
        {
            var director = new TutorialDirector();
            for (int pass = 0; pass < AllSteps.Length; pass++)
            {
                foreach (TutorialStep step in AllSteps) director.TryArm(step);
                for (int frame = 0; frame < 600; frame++) director.Tick(1f / 60f);
            }

            Assert.IsTrue(director.IsComplete, "the tutorial must end even if nothing is ever performed");
            Assert.IsFalse(director.HoldsRun);
        }

        [Test]
        public void HoldNeverOutlastsTheTimeout()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);
            Assert.IsTrue(director.HoldsRun);

            float held = 0f;
            while (director.HoldsRun && held < 60f)
            {
                director.Tick(1f / 60f);
                held += 1f / 60f;
            }

            Assert.Less(held, TutorialDirector.StepTimeoutSeconds + 0.05f,
                "the run may never be held longer than one step's patience");
        }

        [Test]
        public void ZeroAndNegativeDeltaTime_CannotStallTheTimeout()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);

            // A paused or clock-skewed frame must not rewind the deadline.
            for (int i = 0; i < 100; i++) director.Tick(0f);
            for (int i = 0; i < 100; i++) director.Tick(-1f);
            Assert.IsTrue(director.HasPrompt, "no time passed, so the prompt stands");

            for (int frame = 0; frame < 600; frame++) director.Tick(1f / 60f);
            Assert.IsFalse(director.HasPrompt);
        }

        [Test]
        public void GateStep_NeverHoldsTheRun()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Gate);
            Assert.IsTrue(director.HasPrompt);
            Assert.IsFalse(director.HoldsRun, "naming what already happened must not stop the road");
        }

        [Test]
        public void TimeoutProgress_RunsZeroToOne()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);
            Assert.AreEqual(0f, director.TimeoutProgress, 1e-4f);

            director.Tick(TutorialDirector.StepTimeoutSeconds * 0.5f);
            Assert.AreEqual(0.5f, director.TimeoutProgress, 0.02f);
        }

        // --- Persistence round-trip ---------------------------------------------------

        [Test]
        public void CompletedMask_RoundTripsThroughAProfile()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);
            director.Observe(TutorialSignal.LaneChanged);
            director.TryArm(TutorialStep.Spell);
            director.Observe(TutorialSignal.SpellCast);

            var profile = new PlayerProfile { TutorialMask = director.CompletedMask };
            var resumed = new TutorialDirector(profile.TutorialMask);

            Assert.IsTrue(resumed.IsDone(TutorialStep.Steer));
            Assert.IsTrue(resumed.IsDone(TutorialStep.Spell));
            Assert.IsFalse(resumed.IsDone(TutorialStep.Gate));
            Assert.IsFalse(resumed.IsDone(TutorialStep.Shield));
            Assert.IsFalse(resumed.TryArm(TutorialStep.Steer), "a resumed run must not re-teach");
        }

        [Test]
        public void SkipAll_EndsTheTutorialImmediately()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);
            director.SkipAll();

            Assert.IsTrue(director.IsComplete);
            Assert.IsFalse(director.HasPrompt);
            Assert.IsFalse(director.HoldsRun);
        }

        [Test]
        public void Dismiss_ClearsThePromptWithoutTeaching()
        {
            var director = new TutorialDirector();
            director.TryArm(TutorialStep.Steer);
            director.Dismiss();

            Assert.IsFalse(director.HasPrompt);
            Assert.IsFalse(director.IsDone(TutorialStep.Steer), "an abandoned run re-offers the lesson");
            Assert.IsTrue(director.TryArm(TutorialStep.Steer));
        }

        // --- Migration ----------------------------------------------------------------

        [Test]
        public void ExistingSave_IsNotCoachedAgain()
        {
            // Someone who already played gets a v2 save with no TutorialMask field, which
            // deserializes to 0. Coaching them would be wrong.
            var old = SaveMigrator.Migrate(new PlayerProfile { SchemaVersion = 2 });

            Assert.AreEqual(SaveMigrator.CurrentVersion, old.SchemaVersion);
            Assert.IsTrue(new TutorialDirector(old.TutorialMask).IsComplete,
                "a save that predates the tutorial belongs to a player who does not need it");
        }

        [Test]
        public void BrandNewProfile_StillGetsTheTutorial()
        {
            // FileSaveService stamps a fresh profile at the current schema precisely so no
            // migration runs. If one did, the v2->v3 step would mark the tutorial taught and
            // silently skip it for every new player.
            var fresh = new PlayerProfile { SchemaVersion = SaveMigrator.CurrentVersion };
            SaveMigrator.Migrate(fresh);

            Assert.AreEqual(0, fresh.TutorialMask);
            Assert.IsFalse(new TutorialDirector(fresh.TutorialMask).IsComplete);
        }

        [Test]
        public void NewGame_WipesEverythingAndReArmsTheTutorial()
        {
            // What the menu's NEW GAME does. Stamped at the current schema so no migration
            // runs -- the v2->v3 step marks the tutorial taught, which would defeat the point.
            var wiped = new PlayerProfile { SchemaVersion = SaveMigrator.CurrentVersion };

            Assert.AreEqual(0, wiped.TutorialMask);
            Assert.AreEqual(0, wiped.CurrentLevelIndex);
            Assert.AreEqual(0, wiped.UnspentStatPoints);
            Assert.IsEmpty(wiped.Inventory);
            Assert.IsEmpty(wiped.Equipped);
            Assert.IsFalse(new TutorialDirector(wiped.TutorialMask).IsComplete,
                "a wiped save must get the tutorial again");
        }

        [Test]
        public void AncientV1Save_MigratesStraightThroughToTaught()
        {
            var ancient = SaveMigrator.Migrate(new PlayerProfile { SchemaVersion = 1 });

            Assert.AreEqual(SaveMigrator.CurrentVersion, ancient.SchemaVersion);
            Assert.IsTrue(new TutorialDirector(ancient.TutorialMask).IsComplete);
        }
    }
}
