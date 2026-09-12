using System;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// A pool of charges that refills on a timer — the spell and the shield both run on one.
    ///
    /// WHAT THIS REPLACED. Both abilities were a single boolean on a cooldown: you had the
    /// spell or you did not, and the only stat that touched it made the wait shorter. That
    /// is a fine mobile verb and a poor RPG one, because there is no build in it — nothing
    /// to spend a point on that changes HOW the ability is used rather than how often. The
    /// request was "a magazine of spells instead of one at a time with skill nodes to add
    /// charges and the skills to add the shield counts and the shield active time", and a
    /// magazine is what makes those three separate things to buy instead of one.
    ///
    /// EACH CHARGE REFILLS ON ITS OWN, AND THE TIMER RUNS WHENEVER THE MAGAZINE IS SHORT.
    /// That is the rule that makes extra charges worth points rather than merely convenient:
    /// with three charges the player may spend two on a dangerous chunk and still have the
    /// third when the next one arrives, because the refill did not wait for them to be empty.
    /// A magazine that only started refilling once spent would make the second and third
    /// charge strictly worse than the first, which is the failure mode this shape avoids.
    ///
    /// In Core because "how many casts do I have and when does the next one land" is the
    /// kind of arithmetic that is obvious until it is wrong at a phase boundary, and a test
    /// can hold it. Nothing here knows about Unity, input, or what a charge is spent ON.
    /// </summary>
    public sealed class Magazine
    {
        private int _charges;
        private float _refillRemaining;

        /// <summary>How many charges this magazine holds when full. Never below one.</summary>
        public int Capacity { get; private set; } = 1;

        /// <summary>Seconds one charge takes to come back. Never below a tenth of a second.</summary>
        public float RefillSeconds { get; private set; } = 1f;

        /// <summary>Charges available right now.</summary>
        public int Charges => _charges;

        /// <summary>Whether a charge can be spent.</summary>
        public bool Ready => _charges > 0;

        /// <summary>Seconds until the next charge lands; zero when the magazine is full.</summary>
        public float RefillRemaining => _charges >= Capacity ? 0f : Math.Max(0f, _refillRemaining);

        /// <summary>
        /// How full the magazine is, 0..1 — the HUD's dial.
        ///
        /// A partially refilled charge counts as its fraction, so the dial moves continuously
        /// instead of jumping a whole pip at a time. With one charge this is exactly the
        /// cooldown bar the HUD drew before magazines existed.
        /// </summary>
        public float Fill
        {
            get
            {
                if (Capacity <= 0) return 1f;
                if (_charges >= Capacity) return 1f;
                float partial = RefillSeconds <= 0f ? 1f : 1f - _refillRemaining / RefillSeconds;
                if (partial < 0f) partial = 0f;
                if (partial > 1f) partial = 1f;
                return (_charges + partial) / Capacity;
            }
        }

        /// <summary>
        /// Set the magazine's shape. Called when stats resolve, which is once per round.
        ///
        /// Raising the capacity does NOT hand out the new charges — it widens the magazine
        /// and leaves filling it to the timer. Otherwise equipping a charge talent mid-run
        /// would be a free cast, and a stat that pays out on the frame it is applied is a
        /// stat players learn to equip and unequip rather than to build around.
        /// </summary>
        public void Configure(int capacity, float refillSeconds)
        {
            Capacity = capacity < 1 ? 1 : capacity;
            RefillSeconds = refillSeconds < 0.1f ? 0.1f : refillSeconds;
            if (_charges > Capacity) _charges = Capacity;
            if (_refillRemaining > RefillSeconds) _refillRemaining = RefillSeconds;
        }

        /// <summary>Start a phase with a full magazine.</summary>
        public void Refill()
        {
            _charges = Capacity;
            _refillRemaining = 0f;
        }

        /// <summary>Spend one charge. False when there is none, and nothing changes.</summary>
        public bool TrySpend()
        {
            if (_charges <= 0) return false;
            _charges--;
            // A magazine that was FULL has no refill running, so spending is what starts the
            // clock. One that was already short keeps the timer it had: emptying the last
            // charge must not reset the progress made toward the next one.
            if (_charges == Capacity - 1) _refillRemaining = RefillSeconds;
            return true;
        }

        /// <summary>
        /// Advance the refill. Several charges can land in one tick, which matters only for
        /// a pathological frame but costs nothing to get right.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || _charges >= Capacity) return;
            _refillRemaining -= deltaSeconds;
            while (_refillRemaining <= 0f && _charges < Capacity)
            {
                _charges++;
                if (_charges >= Capacity)
                {
                    _refillRemaining = 0f;
                    return;
                }
                _refillRemaining += RefillSeconds;
            }
        }
    }
}
