using System;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Unifies wheel, touchpad-like wheel axes, and touch drag into one vertical scroll signal.
    /// </summary>
    public static class ScrollInputBridge
    {
        private const float AxisEpsilon = 0.0001f;

        private static readonly string[] ScrollAxisNames =
        {
            "Mouse ScrollWheel",
            "PC_MouseScroll"
        };

        public static bool TryGetVerticalScroll(float minUiX, float maxUiX, out float scroll)
        {
            return TryGetVerticalScrollInternal(minUiX, maxUiX, true, false, out scroll);
        }

        public static bool TryGetVerticalScrollAnywhere(out float scroll)
        {
            return TryGetVerticalScrollInternal(float.NegativeInfinity, float.PositiveInfinity, false, false, out scroll);
        }

        public static bool TryGetVerticalScrollAnywhereRaw(out float scroll)
        {
            return TryGetVerticalScrollInternal(float.NegativeInfinity, float.PositiveInfinity, false, true, out scroll);
        }

        private static bool TryGetVerticalScrollInternal(float minUiX, float maxUiX, bool restrictPointerToRange, bool raw, out float scroll)
        {
            scroll = ReadWheelLikeScroll(raw);
            if (Mathf.Abs(scroll) > AxisEpsilon)
            {
                if (!restrictPointerToRange || IsPointerWithinXRange(minUiX, maxUiX))
                    return true;

                scroll = 0f;
            }

            return TouchInputBridge.TryGetTouchScroll(minUiX, maxUiX, out scroll);
        }

        private static float ReadWheelLikeScroll(bool raw)
        {
            float strongest = 0f;

            strongest = PickStronger(strongest, Input.mouseScrollDelta.y);

            for (int i = 0; i < ScrollAxisNames.Length; i++)
            {
                string axisName = ScrollAxisNames[i];
                strongest = PickStronger(strongest, SafeReadAxis(axisName, raw));

                if (!raw)
                    strongest = PickStronger(strongest, SafeReadAxis(axisName, true));
            }

            return Mathf.Abs(strongest) > AxisEpsilon ? strongest : 0f;
        }

        private static float SafeReadAxis(string axisName, bool raw)
        {
            if (string.IsNullOrEmpty(axisName))
                return 0f;

            try
            {
                return raw ? Input.GetAxisRaw(axisName) : Input.GetAxis(axisName);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        private static float PickStronger(float current, float candidate)
        {
            return Mathf.Abs(candidate) > Mathf.Abs(current) ? candidate : current;
        }

        private static bool IsPointerWithinXRange(float minUiX, float maxUiX)
        {
            Vector3 pointerPosition = Input.mousePosition;
            float uiX = pointerPosition.x - (Screen.width * 0.5f);
            return uiX >= minUiX && uiX <= maxUiX;
        }
    }
}
