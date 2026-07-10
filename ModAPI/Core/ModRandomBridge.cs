using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Random = UnityEngine.Random;

namespace ModAPI.Core
{
    /// <summary>Compatibility facade used by game RNG call-site redirects.</summary>
    /// <remarks>The volatile gate is intentionally the only work performed before a draw.</remarks>
    public static class ModRandomBridge
    {
        private const string MiscDomain = "misc";
        private static readonly string[] ScenarioDomains = new string[]
        {
            "map", "characters", "encounters", "weather", "visits", "combat", "items", MiscDomain
        };
        private static volatile bool _scenarioFixedSeedActive;
        private static int _scenarioFixedSeed;
        private static readonly object DomainLock = new object();
        private static readonly Dictionary<string, ModRandomStream> DomainStreams = new Dictionary<string, ModRandomStream>(StringComparer.Ordinal);
        private static readonly MethodInfo UnityInitState = typeof(Random).GetMethod("InitState", new System.Type[] { typeof(int) });

        public static bool ScenarioFixedSeedActive { get { return _scenarioFixedSeedActive; } }

        /// <summary>Enables deterministic gameplay draws for a fixed-seed custom scenario.</summary>
        public static void SetScenarioFixedSeedActive(bool active)
        {
            lock (DomainLock)
            {
                if (active)
                {
                    // ScenarioSeedPolicy resets ModRandom to the scenario seed immediately before
                    // enabling the gate. Capture it here so later vanilla InitState calls cannot
                    // replace the scenario-owned root with the map component's volatile seed.
                    _scenarioFixedSeed = ModRandom.CurrentSeed;
                    _scenarioFixedSeedActive = true;
                    ResetAllDomainsLocked("gate-active");
                }
                else
                {
                    _scenarioFixedSeedActive = false;
                    DomainStreams.Clear();
                }
            }
            MMLog.WriteInfo("[ModRandomBridge] RNG mode=" + (active ? "scenario-fixed" : "unity-pass-through") + ".");
        }

        public static int Range(int minInclusive, int maxExclusive)
        {
            return _scenarioFixedSeedActive ? ModRandom.Range(minInclusive, maxExclusive) : Random.Range(minInclusive, maxExclusive);
        }

        // Internal overloads are transpiler targets. They preserve the established public facade
        // while isolating patched declaring-type batches from unrelated draw-order changes.
        internal static int Range(int minInclusive, int maxExclusive, string domainName)
        {
            if (!_scenarioFixedSeedActive) return Random.Range(minInclusive, maxExclusive);
            lock (DomainLock)
            {
                return GetDomainStreamLocked(domainName, "first-int-draw").Range(minInclusive, maxExclusive);
            }
        }

        public static float Range(float minInclusive, float maxInclusive)
        {
            return _scenarioFixedSeedActive ? ModRandom.Range(minInclusive, maxInclusive) : Random.Range(minInclusive, maxInclusive);
        }

        internal static float Range(float minInclusive, float maxInclusive, string domainName)
        {
            if (!_scenarioFixedSeedActive) return Random.Range(minInclusive, maxInclusive);
            lock (DomainLock)
            {
                return GetDomainStreamLocked(domainName, "first-float-draw").Range(minInclusive, maxInclusive);
            }
        }

        /// <summary>Unity-compatible value contract: inclusive range [0, 1].</summary>
        public static float Value()
        {
            return _scenarioFixedSeedActive ? ModRandom.Range(0f, 1f) : Random.value;
        }

        internal static float Value(string domainName)
        {
            if (!_scenarioFixedSeedActive) return Random.value;
            lock (DomainLock)
            {
                return GetDomainStreamLocked(domainName, "first-value-draw").Value();
            }
        }

        /// <summary>
        /// Redirect target for the map generator's global Unity RNG reset. A fixed scenario resets
        /// only its owned stream; vanilla retains the original global-state behaviour.
        /// </summary>
        public static void InitScenarioState(int seed)
        {
            if (_scenarioFixedSeedActive)
            {
                lock (DomainLock)
                {
                    ResetDomainLocked("map", "map-init;ignored-vanilla-seed=" + seed);
                }
                return;
            }

            // Compile against older Unity profiles too; the catalogued call exists only on
            // game builds exposing this member, which ScenarioRngPatches verifies first.
            if (UnityInitState != null)
                UnityInitState.Invoke(null, new object[] { seed });
        }

        public static Vector2 InsideUnitCircle()
        {
            return _scenarioFixedSeedActive ? SampleInsideUnitCircle() : Random.insideUnitCircle;
        }

        public static Vector3 InsideUnitSphere()
        {
            return _scenarioFixedSeedActive ? SampleInsideUnitSphere() : Random.insideUnitSphere;
        }

        public static Vector3 OnUnitSphere()
        {
            return _scenarioFixedSeedActive ? SampleOnUnitSphere() : Random.onUnitSphere;
        }

        public static Quaternion Rotation()
        {
            if (!_scenarioFixedSeedActive) return Random.rotation;
            Quaternion value = new Quaternion(Value(), Value(), Value(), Value());
            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            return magnitude > 0f ? new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude) : new Quaternion(0f, 0f, 0f, 1f);
        }

        private static Vector2 SampleInsideUnitCircle()
        {
            Vector2 point;
            do { point = new Vector2(Range(-1f, 1f), Range(-1f, 1f)); } while (point.sqrMagnitude > 1f);
            return point;
        }

        private static Vector3 SampleInsideUnitSphere()
        {
            Vector3 point;
            do { point = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)); } while (point.sqrMagnitude > 1f);
            return point;
        }

        private static Vector3 SampleOnUnitSphere()
        {
            Vector3 point;
            do { point = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)); } while (point.sqrMagnitude < 0.000001f || point.sqrMagnitude > 1f);
            return point.normalized;
        }

        private static void ResetAllDomainsLocked(string origin)
        {
            DomainStreams.Clear();
            for (int i = 0; i < ScenarioDomains.Length; i++)
            {
                ResetDomainLocked(ScenarioDomains[i], origin);
            }
        }

        private static void ResetDomainLocked(string domainName, string origin)
        {
            string domain = NormalizeDomain(domainName);
            int domainSeed = DeriveDomainSeed(_scenarioFixedSeed, domain);
            DomainStreams[domain] = new ModRandomStream(domainSeed);
            MMLog.WriteInfo("[ModRandomBridge] Domain reset domain=" + domain
                + " scenarioSeed=" + _scenarioFixedSeed
                + " domainSeed=" + domainSeed
                + " origin=" + origin + ".");
        }

        private static ModRandomStream GetDomainStreamLocked(string domainName, string origin)
        {
            string domain = NormalizeDomain(domainName);
            ModRandomStream stream;
            if (!DomainStreams.TryGetValue(domain, out stream))
            {
                ResetDomainLocked(domain, origin);
                stream = DomainStreams[domain];
            }
            return stream;
        }

        private static string NormalizeDomain(string domainName)
        {
            if (string.IsNullOrEmpty(domainName)) return MiscDomain;
            for (int i = 0; i < ScenarioDomains.Length; i++)
            {
                if (string.Equals(ScenarioDomains[i], domainName, StringComparison.Ordinal))
                    return ScenarioDomains[i];
            }
            return MiscDomain;
        }

        private static int DeriveDomainSeed(int scenarioSeed, string domainName)
        {
            unchecked
            {
                // Stable FNV-1a composition. Do not use string.GetHashCode(), whose contract does
                // not promise cross-runtime stability.
                uint hash = 2166136261u;
                hash ^= (uint)scenarioSeed;
                hash *= 16777619u;
                for (int i = 0; i < domainName.Length; i++)
                {
                    hash ^= domainName[i];
                    hash *= 16777619u;
                }
                int seed = (int)hash;
                return seed == 0 ? 1 : seed;
            }
        }
    }
}
