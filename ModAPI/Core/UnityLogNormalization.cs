using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Delegate that can translate noisy Unity log entries into ModAPI log level, source, and once-key metadata.
    /// Return false when the normalizer does not own the message.
    /// </summary>
    public delegate bool UnityLogNormalizer(
        string condition,
        string stackTrace,
        LogType type,
        out UnityLogNormalization normalization);

    /// <summary>
    /// Normalized representation of a Unity log entry before it is written through ModAPI logging.
    /// </summary>
    public sealed class UnityLogNormalization
    {
        public MMLog.LogLevel Level = MMLog.LogLevel.Info;
        public string Source = "UnityLog";
        public string Message = string.Empty;
        public string OnceKey = string.Empty;
        public bool Suppress;
    }

    /// <summary>
    /// Registry for Unity log normalizers.
    /// Game integrations register normalizers to downgrade expected engine noise or attach better sources.
    /// </summary>
    public static class UnityLogNormalizationRegistry
    {
        private static readonly object Sync = new object();
        private static readonly List<UnityLogNormalizer> Normalizers = new List<UnityLogNormalizer>();

        public static void Register(UnityLogNormalizer normalizer)
        {
            if (normalizer == null) return;

            lock (Sync)
            {
                if (!Normalizers.Contains(normalizer))
                    Normalizers.Add(normalizer);
            }
        }

        internal static bool TryNormalize(
            string condition,
            string stackTrace,
            LogType type,
            out UnityLogNormalization normalization)
        {
            UnityLogNormalizer[] snapshot;
            normalization = null;

            lock (Sync)
            {
                snapshot = Normalizers.ToArray();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    if (snapshot[i](condition, stackTrace, type, out normalization) && normalization != null)
                        return true;
                }
                catch
                {
                }
            }

            return false;
        }
    }
}
