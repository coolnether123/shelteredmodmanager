using System;
using ModAPI.Core;

namespace ShelteredAPI.Networking
{
    /// <summary>
    /// Sheltered multiplayer bridge from a neutral network session id to ModRandom's master seed.
    /// </summary>
    internal static class ShelteredMultiplayerSessionSeed
    {
        private const string SeedScope = "ShelteredAPI.Multiplayer.SessionSeed:";
        private const string LogSource = "ShelteredAPI.Multiplayer.SessionSeed";

        private static readonly object _sync = new object();
        private static string _lastSessionId = string.Empty;
        private static int _lastMasterSeed;

        public static string LastSessionId
        {
            get
            {
                lock (_sync)
                {
                    return _lastSessionId;
                }
            }
        }

        public static int LastMasterSeed
        {
            get
            {
                lock (_sync)
                {
                    return _lastMasterSeed;
                }
            }
        }

        public static bool HasApplied
        {
            get { return LastSessionId.Length > 0; }
        }

        public static int DeriveMasterSeed(string sessionId)
        {
            string normalized = NormalizeSessionId(sessionId);
            if (normalized.Length == 0)
                throw new ArgumentException("Session id is required.", "sessionId");

            return ModRandom.DeriveStableSeed(SeedScope + normalized);
        }

        public static bool TryApply(string sessionId, out int masterSeed, out string error)
        {
            masterSeed = 0;
            error = string.Empty;

            string normalized = NormalizeSessionId(sessionId);
            if (normalized.Length == 0)
            {
                error = "Cannot apply multiplayer seed without a session id.";
                return false;
            }

            try
            {
                masterSeed = DeriveMasterSeed(normalized);

                // Multiplayer session identity owns the master seed; local save step history should not override it.
                ModRandom.IsDeterministic = false;
                ModRandom.InitializeAndNotify(masterSeed);

                lock (_sync)
                {
                    _lastSessionId = normalized;
                    _lastMasterSeed = masterSeed;
                }

                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Applied ModRandom master seed " + masterSeed + " from session '" + normalized + "'.");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                MMLog.WriteWithSource(MMLog.LogLevel.Error, MMLog.LogCategory.Network, LogSource,
                    "Failed to apply session seed: " + ex.Message);
                return false;
            }
        }

        private static string NormalizeSessionId(string sessionId)
        {
            return (sessionId ?? string.Empty).Trim();
        }
    }
}
