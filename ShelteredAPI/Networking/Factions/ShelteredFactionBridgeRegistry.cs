using System;

namespace ShelteredAPI.Networking.Factions
{
    public static class ShelteredFactionBridgeRegistry
    {
        private static readonly object Sync = new object();
        private static IShelteredMultiplayerFactionBridge _bridge = new NullShelteredFactionBridge();

        public static IShelteredMultiplayerFactionBridge Current
        {
            get
            {
                lock (Sync)
                {
                    return _bridge;
                }
            }
        }

        public static void Register(IShelteredMultiplayerFactionBridge bridge)
        {
            lock (Sync)
            {
                _bridge = bridge ?? new NullShelteredFactionBridge();
            }
        }

        public static void Reset()
        {
            Register(null);
        }
    }
}
