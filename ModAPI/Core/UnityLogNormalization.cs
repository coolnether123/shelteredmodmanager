using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Core
{
    public delegate bool UnityLogNormalizer(
        string condition,
        string stackTrace,
        LogType type,
        out UnityLogNormalization normalization);

    public sealed class UnityLogNormalization
    {
        public MMLog.LogLevel Level = MMLog.LogLevel.Info;
        public string Source = "UnityLog";
        public string Message = string.Empty;
        public string OnceKey = string.Empty;
    }

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
