using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>Compatibility facade used by game RNG call-site redirects.</summary>
    /// <remarks>The volatile gate is intentionally the only work performed before a draw.</remarks>
    public static class ModRandomBridge
    {
        private static volatile bool _scenarioFixedSeedActive;

        public static bool ScenarioFixedSeedActive { get { return _scenarioFixedSeedActive; } }

        /// <summary>Enables deterministic gameplay draws for a fixed-seed custom scenario.</summary>
        public static void SetScenarioFixedSeedActive(bool active)
        {
            _scenarioFixedSeedActive = active;
            MMLog.WriteInfo("[ModRandomBridge] RNG mode=" + (active ? "scenario-fixed" : "unity-pass-through") + ".");
        }

        public static int Range(int minInclusive, int maxExclusive)
        {
            return _scenarioFixedSeedActive ? ModRandom.Range(minInclusive, maxExclusive) : Random.Range(minInclusive, maxExclusive);
        }

        public static float Range(float minInclusive, float maxInclusive)
        {
            return _scenarioFixedSeedActive ? ModRandom.Range(minInclusive, maxInclusive) : Random.Range(minInclusive, maxInclusive);
        }

        /// <summary>Unity-compatible value contract: inclusive range [0, 1].</summary>
        public static float Value()
        {
            return _scenarioFixedSeedActive ? ModRandom.Range(0f, 1f) : Random.value;
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
    }
}
