using UnityEngine;

namespace ShelteredAPI.Input
{
    internal static class UnityTouchpadPanReader
    {
        private const float TouchpadPanSensitivity = 2f;
        private const float DiagonalAssistRatio = 0.7f;
        private const float DiagonalActivationThreshold = 0.08f;
        private const float DiagonalAssistFloor = 0.55f;

        public static float ReadHorizontalPan(bool raw, params string[] fallbackAxisNames)
        {
            float strongest = ReadTouchpadPanVector().x;
            strongest = UnityLegacyAxisReader.PickStronger(strongest, UnityLegacyAxisReader.ReadStrongest(raw, fallbackAxisNames));
            return UnityLegacyAxisReader.IsSignificant(strongest) ? strongest : 0f;
        }

        public static float ReadVerticalPan(bool raw, params string[] fallbackAxisNames)
        {
            float strongest = ReadTouchpadPanVector().y;
            strongest = UnityLegacyAxisReader.PickStronger(strongest, UnityLegacyAxisReader.ReadStrongest(raw, fallbackAxisNames));
            return UnityLegacyAxisReader.IsSignificant(strongest) ? strongest : 0f;
        }

        private static Vector2 ReadTouchpadPanVector()
        {
            Vector2 pan = UnityEngine.Input.mouseScrollDelta * TouchpadPanSensitivity;
            pan.x = Mathf.Clamp(pan.x, -1f, 1f);
            pan.y = Mathf.Clamp(pan.y, -1f, 1f);

            float absX = Mathf.Abs(pan.x);
            float absY = Mathf.Abs(pan.y);
            if (absX >= DiagonalActivationThreshold && absY >= DiagonalActivationThreshold)
            {
                float assistedMinimum = Mathf.Max(Mathf.Max(absX, absY) * DiagonalAssistRatio, DiagonalAssistFloor);
                if (absX < assistedMinimum)
                    pan.x = Mathf.Sign(pan.x) * assistedMinimum;
                if (absY < assistedMinimum)
                    pan.y = Mathf.Sign(pan.y) * assistedMinimum;
            }

            return pan;
        }
    }
}
