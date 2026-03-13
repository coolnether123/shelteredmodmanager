using System;
using UnityEngine;

namespace ShelteredAPI.Input
{
    internal static class UnityLegacyAxisReader
    {
        private const float AxisEpsilon = 0.0001f;

        public static float ReadStrongest(bool raw, params string[] axisNames)
        {
            float strongest = 0f;
            if (axisNames == null)
                return strongest;

            for (int i = 0; i < axisNames.Length; i++)
                strongest = PickStronger(strongest, SafeReadAxis(axisNames[i], raw));

            return IsSignificant(strongest) ? strongest : 0f;
        }

        public static float PickStronger(float current, float candidate)
        {
            return Mathf.Abs(candidate) > Mathf.Abs(current) ? candidate : current;
        }

        public static bool IsSignificant(float value)
        {
            return Mathf.Abs(value) > AxisEpsilon;
        }

        private static float SafeReadAxis(string axisName, bool raw)
        {
            if (string.IsNullOrEmpty(axisName))
                return 0f;

            try
            {
                return raw ? UnityEngine.Input.GetAxisRaw(axisName) : UnityEngine.Input.GetAxis(axisName);
            }
            catch (Exception)
            {
                return 0f;
            }
        }
    }
}
