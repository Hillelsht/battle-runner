using System.Text;

namespace BattleRunner.Core.Save
{
    /// <summary>
    /// FNV-1a 64-bit over the serialized payload. Detects corruption and casual
    /// tampering — not a security boundary (server-side validation is post-MVP).
    /// </summary>
    public static class Checksum
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static string Compute(string payload)
        {
            ulong hash = OffsetBasis;
            byte[] bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= Prime;
            }
            return hash.ToString("x16");
        }

        public static bool Verify(string payload, string expected) =>
            Compute(payload) == expected;
    }
}
