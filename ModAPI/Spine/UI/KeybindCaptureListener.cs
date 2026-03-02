using System;
using UnityEngine;

namespace ModAPI.Spine.UI
{
    /// <summary>
    /// Captures the next pressed key and reports it through callbacks.
    /// </summary>
    public class KeybindCaptureListener : MonoBehaviour
    {
        private static readonly KeyCode[] AllCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        private float _captureStartedAt;

        public UILabel ValueLabel;
        public Action<KeyCode> OnCaptured;
        public Action OnCanceled;
        public bool IsCapturing { get; private set; }

        public void StartCapture()
        {
            IsCapturing = true;
            _captureStartedAt = Time.realtimeSinceStartup;
            if (ValueLabel != null) ValueLabel.text = "PRESS KEY...";
        }

        private void Update()
        {
            if (!IsCapturing) return;

            // Avoid instantly re-capturing the click/keypress that opened capture.
            if ((Time.realtimeSinceStartup - _captureStartedAt) < 0.05f) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                IsCapturing = false;
                if (OnCanceled != null) OnCanceled();
                return;
            }

            for (int i = 0; i < AllCodes.Length; i++)
            {
                KeyCode code = AllCodes[i];
                if (code == KeyCode.None || code == KeyCode.Escape) continue;

                if (UnityEngine.Input.GetKeyDown(code))
                {
                    IsCapturing = false;
                    if (OnCaptured != null) OnCaptured(code);
                    return;
                }
            }
        }
    }
}
