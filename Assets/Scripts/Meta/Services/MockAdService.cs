using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRunner.Meta.Services
{
    /// <summary>Simulates a rewarded ad: short delay, then the reward is granted.</summary>
    public sealed class MockAdService : IAdService
    {
        private sealed class Pending
        {
            public float Remaining;
            public Action<bool> OnDone;
        }

        private readonly List<Pending> _pending = new List<Pending>();
        private readonly float _fakeAdSeconds;

        public MockAdService(float fakeAdSeconds = 1.2f) => _fakeAdSeconds = fakeAdSeconds;

        public bool IsRewardedReady(AdPlacement placement) => true;

        public void ShowRewarded(AdPlacement placement, Action<bool> onDone)
        {
            Debug.Log($"[Ads:mock] Rewarded requested: {placement}");
            _pending.Add(new Pending { Remaining = _fakeAdSeconds, OnDone = onDone });
        }

        public void Tick(float deltaTime)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                _pending[i].Remaining -= deltaTime;
                if (_pending[i].Remaining > 0f) continue;
                Action<bool> done = _pending[i].OnDone;
                _pending.RemoveAt(i);
                done?.Invoke(true);
            }
        }
    }
}
