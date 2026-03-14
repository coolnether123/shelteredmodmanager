using ModAPI.InputServices;
using UnityEngine;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Unity-backed scroll input source used by Sheltered runtime integrations.
    /// </summary>
    internal sealed class UnityScrollInputSource : IScrollInputSource
    {
        private static readonly string[] ScrollAxisNames =
        {
            "Mouse ScrollWheel",
            "PC_MouseScroll"
        };

        public static readonly UnityScrollInputSource Instance = new UnityScrollInputSource();

        private UnityScrollInputSource()
        {
        }

        public bool TryGetVerticalScroll(ScrollInputQuery query, out float scroll)
        {
            scroll = ReadWheelLikeScroll(query.Raw);
            if (UnityLegacyAxisReader.IsSignificant(scroll))
            {
                if (!query.RestrictPointerToRange || IsPointerWithinXRange(query.MinUiX, query.MaxUiX))
                    return true;

                scroll = 0f;
            }

            return UnityTouchDragTracker.TryGetVerticalScroll(query.MinUiX, query.MaxUiX, out scroll);
        }

        public bool IsIndirectScrollActive()
        {
            return UnityIndirectScrollClassifier.IsIndirectScrollActive();
        }

        private static float ReadWheelLikeScroll(bool raw)
        {
            float strongest = 0f;

            strongest = UnityLegacyAxisReader.PickStronger(strongest, UnityEngine.Input.mouseScrollDelta.y);

            for (int i = 0; i < ScrollAxisNames.Length; i++)
            {
                strongest = UnityLegacyAxisReader.PickStronger(
                    strongest,
                    UnityLegacyAxisReader.ReadStrongest(raw, ScrollAxisNames[i]));

                if (!raw)
                {
                    strongest = UnityLegacyAxisReader.PickStronger(
                        strongest,
                        UnityLegacyAxisReader.ReadStrongest(true, ScrollAxisNames[i]));
                }
            }

            if (!UnityLegacyAxisReader.IsSignificant(strongest))
                return 0f;

            return strongest * ShelteredInputTuning.MouseScrollSpeed;
        }

        private static bool IsPointerWithinXRange(float minUiX, float maxUiX)
        {
            Vector3 pointerPosition = UnityEngine.Input.mousePosition;
            float uiX = pointerPosition.x - (Screen.width * 0.5f);
            return uiX >= minUiX && uiX <= maxUiX;
        }
    }
}
