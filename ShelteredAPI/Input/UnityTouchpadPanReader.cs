using UnityEngine;

namespace ShelteredAPI.Input
{
    internal static class UnityTouchpadPanReader
    {
        private const float DiagonalAssistRatio = 0.7f;
        private const float DiagonalActivationThreshold = 0.08f;
        private const float DiagonalAssistFloor = 0.55f;

        public static float ReadHorizontalPan(bool raw, params string[] fallbackAxisNames)
        {
            float strongest = ReadCurrentTouchpadPanVector().x;
            strongest = UnityLegacyAxisReader.PickStronger(strongest, UnityLegacyAxisReader.ReadStrongest(raw, fallbackAxisNames));
            return UnityLegacyAxisReader.IsSignificant(strongest) ? strongest : 0f;
        }

        public static float ReadVerticalPan(bool raw, params string[] fallbackAxisNames)
        {
            float strongest = ReadCurrentTouchpadPanVector().y;
            strongest = UnityLegacyAxisReader.PickStronger(strongest, UnityLegacyAxisReader.ReadStrongest(raw, fallbackAxisNames));
            return UnityLegacyAxisReader.IsSignificant(strongest) ? strongest : 0f;
        }

        public static bool TryReadCurrentPanVector(out Vector2 pan)
        {
            pan = ReadCurrentTouchpadPanVector();
            return UnityLegacyAxisReader.IsSignificant(pan.x) || UnityLegacyAxisReader.IsSignificant(pan.y);
        }

        public static bool TryReadCurrentMapPanVector(out Vector2 pan)
        {
            pan = ReadCurrentPanVector(true);
            return UnityLegacyAxisReader.IsSignificant(pan.x) || UnityLegacyAxisReader.IsSignificant(pan.y);
        }

        private static Vector2 ReadCurrentTouchpadPanVector()
        {
            return ReadCurrentPanVector(false);
        }

        private static Vector2 ReadCurrentPanVector(bool includeMapWheelLikePan)
        {
            UnityScrollGestureSample sample = UnityIndirectScrollClassifier.GetCurrentSample();
            if (sample.Kind != UnityScrollGestureKind.Indirect
                && (!includeMapWheelLikePan || !UnityIndirectScrollClassifier.IsMapPanGesture(sample)))
                return Vector2.zero;

            Vector2 pan = sample.Delta * ShelteredInputTuning.TouchpadMovementSpeed;
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
