using System;

namespace BattleRunner.Meta.Services
{
    public enum IapProduct
    {
        KeysSmall = 0,
        KeysLarge = 1,
        StarterChest = 2
    }

    public interface IIapService
    {
        void Purchase(IapProduct product, Action<bool> onDone);
    }

    /// <summary>Instant-success stub; Unity IAP binds here post-MVP.</summary>
    public sealed class MockIapService : IIapService
    {
        public void Purchase(IapProduct product, Action<bool> onDone)
        {
            UnityEngine.Debug.Log($"[IAP:mock] Purchase: {product}");
            onDone?.Invoke(true);
        }
    }
}
