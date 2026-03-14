using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Spine.UI
{
    /// <summary>
    /// Captures the next pressed key and reports it through callbacks.
    /// </summary>
    public class KeybindCaptureListener : MonoBehaviour
    {
        public enum CaptureState
        {
            Idle = 0,
            Listening = 1,
            Cancelled = 2,
            Confirmed = 3
        }

        private static readonly KeyCode[] AllCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        private static readonly List<KeybindCaptureListener> ActiveListeners = new List<KeybindCaptureListener>();
        private static int _lastEscapeConsumedFrame = -1;

        private float _captureStartedAt;
        private CaptureState _state = CaptureState.Idle;
        private string _lastRenderedText = string.Empty;

        public UILabel ValueLabel;
        public Action<KeyCode> OnCaptured;
        public Action OnCanceled;
        public Func<string> DisplayTextProvider;

        public bool IsCapturing { get { return _state == CaptureState.Listening; } }
        public CaptureState State { get { return _state; } }

        public static bool HasActiveCapture()
        {
            for (int i = 0; i < ActiveListeners.Count; i++)
            {
                var listener = ActiveListeners[i];
                if (listener != null && listener.IsCapturing) return true;
            }
            return false;
        }

        public static bool ShouldBlockEscapeClose()
        {
            if (HasActiveCapture()) return true;
            return Time.frameCount == _lastEscapeConsumedFrame;
        }

        private void OnEnable()
        {
            if (!ActiveListeners.Contains(this))
                ActiveListeners.Add(this);
        }

        private void OnDisable()
        {
            ActiveListeners.Remove(this);
        }

        public void StartCapture()
        {
            CancelOtherActiveCaptures();
            _state = CaptureState.Listening;
            _captureStartedAt = Time.realtimeSinceStartup;
            if (ValueLabel != null) ValueLabel.text = "PRESS KEY...";
            MMLog.WriteInfo("[KeybindCaptureListener] Capture started on " + gameObject.name + ".");
        }

        private void Update()
        {
            if (!IsCapturing)
            {
                RefreshDisplayFromProvider();
                return;
            }

            // Avoid instantly re-capturing the click/keypress that opened capture.
            if ((Time.realtimeSinceStartup - _captureStartedAt) < 0.05f) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                CancelCapture(true);
                return;
            }

            for (int i = 0; i < AllCodes.Length; i++)
            {
                KeyCode code = AllCodes[i];
                if (code == KeyCode.None || code == KeyCode.Escape) continue;

                if (UnityEngine.Input.GetKeyDown(code))
                {
                    _state = CaptureState.Confirmed;
                    MMLog.WriteInfo("[KeybindCaptureListener] Captured key " + code + " on " + gameObject.name + ".");
                    if (OnCaptured != null) OnCaptured(code);
                    return;
                }
            }
        }

        private void RefreshDisplayFromProvider()
        {
            if (DisplayTextProvider == null || ValueLabel == null) return;

            string text = DisplayTextProvider();
            if (text == null) text = string.Empty;
            if (text == _lastRenderedText) return;

            _lastRenderedText = text;
            ValueLabel.text = text;
        }

        private void CancelOtherActiveCaptures()
        {
            for (int i = 0; i < ActiveListeners.Count; i++)
            {
                var listener = ActiveListeners[i];
                if (listener == null || listener == this || !listener.IsCapturing) continue;
                listener.CancelCapture(false);
            }
        }

        private void CancelCapture(bool consumeEscape)
        {
            _state = CaptureState.Cancelled;
            if (consumeEscape)
                _lastEscapeConsumedFrame = Time.frameCount;

            MMLog.WriteInfo("[KeybindCaptureListener] Capture cancelled on " + gameObject.name + ".");
            if (OnCanceled != null) OnCanceled();
        }
    }
}
