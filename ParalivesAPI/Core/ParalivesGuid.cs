using System;
using System.Text;

namespace ParalivesAPI.Core
{
    public static class ParalivesGuid
    {
        private const ulong FnvaOffset = 14695981039346656037UL;
        private const ulong FnvaPrime = 1099511628211UL;

        public static ulong FromStableName(string stableName)
        {
            return FromStableName("ParalivesAPI", stableName);
        }

        public static ulong FromStableName(string namespaceId, string stableName)
        {
            if (string.IsNullOrEmpty(namespaceId))
                throw new ArgumentException("A namespace is required for deterministic GUID generation.", "namespaceId");
            if (string.IsNullOrEmpty(stableName))
                throw new ArgumentException("A stable name is required for deterministic GUID generation.", "stableName");

            string input = namespaceId + ":" + stableName;
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            ulong hash = FnvaOffset;

            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= FnvaPrime;
            }

            return hash == 0UL ? FnvaOffset : hash;
        }
    }
}
