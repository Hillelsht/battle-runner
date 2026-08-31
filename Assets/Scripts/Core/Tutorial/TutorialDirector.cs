using System;

namespace BattleRunner.Core.Tutorial
{
    /// <summary>The four things a new player cannot discover on their own.</summary>
    public enum TutorialStep
    {
        Steer = 0,
        Gate = 1,
        Spell = 2,
        Shield = 3
    }

    /// <summary>Something the player did that can satisfy a step.</summary>
    public enum TutorialSignal
    {
        LaneChanged,
        GatePassed,
        SpellCast,
        ShieldRaised
    }

    /// <summary>
    /// Decides which coaching prompt is on screen and whether the run is held for it.
    ///
    /// Engine-free on purpose: a tutorial that can strand a player is worse than none, and
    /// the only way to prove it cannot is to run every path in a test. The gameplay layer
    /// owns presentation and arming; this owns the sequence, the hold, and the timeouts.
    ///
    /// Why it is needed, measured against the shipped content: a player who never steers
    /// takes only the lane-0 gates on level 1 and is reduced to zero force by the enemy
    /// pack at 16.7 s, which fires the resurrect prompt. A first run that ends in death
    /// inside twenty seconds is the retention cliff this exists to remove.
    /// </summary>
    public sealed class TutorialDirector
    {
        /// <summary>Longest any single step may hold the run before it gives up and moves on.</summary>
        public const float StepTimeoutSeconds = 6f;

        private readonly struct StepSpec
        {
            public readonly TutorialSignal Satisfies;
            public readonly bool HoldsRun;
            public readonly string Headline;
            public readonly string Detail;

            public StepSpec(TutorialSignal satisfies, bool holdsRun, string headline, string detail)
            {
                Satisfies = satisfies;
                HoldsRun = holdsRun;
                Headline = headline;
                Detail = detail;
            }
        }

        // Copy uses the same ASCII vocabulary as the HUD ("SHIELD v" / "SPELL ^") rather
        // than arrow glyphs: the built-in LegacyRuntime font is the only font in the build
        // and geometric-shape codepoints are not guaranteed in it.
        //
        // Both flick prompts say "lift your thumb" on purpose. GestureClassifier is one
        // gesture per contact by design -- a touch that has classified as LaneDrag can
        // never emit a flick -- so a player steering with their thumb held down physically
        // cannot cast, and would read an un-worded prompt as broken input.
        private static readonly StepSpec[] Specs =
        {
            new StepSpec(TutorialSignal.LaneChanged, true,
                "DRAG TO STEER", "Slide your thumb left or right"),
            new StepSpec(TutorialSignal.GatePassed, false,
                "TAKE THE BIGGER GATE", "Blue adds, gold multiplies, red takes"),
            new StepSpec(TutorialSignal.SpellCast, true,
                "FLICK UP TO CAST", "Lift your thumb, then swipe up fast"),
            new StepSpec(TutorialSignal.ShieldRaised, true,
                "FLICK DOWN TO BLOCK", "Lift your thumb, then swipe down fast")
        };

        private int _completedMask;
        private int _activeIndex = -1;
        private float _elapsed;

        public TutorialDirector(int completedMask = 0) => _completedMask = completedMask;

        /// <summary>Bitmask of finished steps, for persisting on the profile.</summary>
        public int CompletedMask => _completedMask;

        /// <summary>True once every step is done, however it was resolved.</summary>
        public bool IsComplete => _completedMask == (1 << Specs.Length) - 1;

        public bool HasPrompt => _activeIndex >= 0;

        /// <summary>The step currently on screen. Only valid while <see cref="HasPrompt"/>.</summary>
        public TutorialStep ActiveStep => (TutorialStep)_activeIndex;

        public string Headline => _activeIndex >= 0 ? Specs[_activeIndex].Headline : string.Empty;
        public string Detail => _activeIndex >= 0 ? Specs[_activeIndex].Detail : string.Empty;

        /// <summary>
        /// Whether the run should stand still for the current prompt. The runner state already
        /// halts on a flag for the resurrect prompt, so this reuses that path instead of
        /// touching Time.timeScale, which would also freeze UI and any ad SDK.
        /// </summary>
        public bool HoldsRun => _activeIndex >= 0 && Specs[_activeIndex].HoldsRun;

        /// <summary>Fraction of this step's patience already spent, for a countdown ring or bar.</summary>
        public float TimeoutProgress =>
            _activeIndex < 0 ? 0f : Math.Min(1f, _elapsed / StepTimeoutSeconds);

        public bool IsDone(TutorialStep step) => (_completedMask & (1 << (int)step)) != 0;

        /// <summary>
        /// Put a step on screen, if it has not already been taught and nothing else is showing.
        /// The gameplay layer calls this when the situation the step teaches actually arises.
        /// Returns true when the prompt was raised.
        /// </summary>
        public bool TryArm(TutorialStep step)
        {
            if (_activeIndex >= 0 || IsDone(step)) return false;
            _activeIndex = (int)step;
            _elapsed = 0f;
            return true;
        }

        /// <summary>The player did something. Completes the active step if it matches.</summary>
        public void Observe(TutorialSignal signal)
        {
            if (_activeIndex < 0) return;
            if (Specs[_activeIndex].Satisfies != signal) return;
            Complete();
        }

        /// <summary>
        /// Advances the active prompt's patience. On expiry the step is marked taught and
        /// dismissed anyway: a player who cannot or will not perform the gesture must still
        /// be able to finish the run, so no step can ever hold the game indefinitely.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_activeIndex < 0) return;
            if (deltaTime > 0f) _elapsed += deltaTime;
            if (_elapsed >= StepTimeoutSeconds) Complete();
        }

        /// <summary>Dismiss whatever is showing without crediting it — used when a run ends early.</summary>
        public void Dismiss() => _activeIndex = -1;

        /// <summary>Mark every step taught, for a "skip tutorial" affordance.</summary>
        public void SkipAll()
        {
            _completedMask = (1 << Specs.Length) - 1;
            _activeIndex = -1;
        }

        private void Complete()
        {
            _completedMask |= 1 << _activeIndex;
            _activeIndex = -1;
            _elapsed = 0f;
        }
    }
}
