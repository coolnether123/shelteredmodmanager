using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Debugging
{
    /// <summary>
    /// Game-agnostic, persistent IMGUI feedback capture surface. A host supplies storage and optional context.
    /// </summary>
    public sealed class FeedbackOverlay : MonoBehaviour
    {
        private const string ScratchControlName = "ModAPI.FeedbackOverlay.Scratch";
        private const float ScratchSaveDelaySeconds = 1f;
        private const float ConfirmationSeconds = 2f;

        private FeedbackOverlayConfig _config;
        private FeedbackStorage _storage;
        private IFeedbackContextProvider _contextProvider;
        private Rect _windowRect;
        private string _scratch = string.Empty;
        private string _persistedScratch = string.Empty;
        private string _status = string.Empty;
        private float _scratchChangedAt;
        private float _statusExpiresAt;
        private bool _visible;
        private bool _focusScratch;
        private bool _focusWindow;
        private bool _consumePointerUntilMouseUp;
        private bool _captureInProgress;
        private GUIStyle _windowStyle;
        private GUIStyle _textAreaStyle;
        private GUIStyle _statusStyle;
        private Texture2D _parchmentTexture;

        /// <summary>Whether the overlay is currently accepting feedback input.</summary>
        public bool IsVisible
        {
            get { return _visible; }
        }

        /// <summary>Gets or changes the show/hide key after configuration.</summary>
        public KeyCode ToggleKey
        {
            get { return _config != null ? _config.ToggleKey : KeyCode.F4; }
            set
            {
                EnsureConfig();
                _config.ToggleKey = value;
            }
        }

        /// <summary>Resolved storage root, or null before configuration.</summary>
        public string StorageRootPath
        {
            get { return _storage != null ? _storage.RootPath : null; }
        }

        /// <summary>Optional host context provider captured at submission time.</summary>
        public IFeedbackContextProvider ContextProvider
        {
            get { return _contextProvider; }
            set { _contextProvider = value; }
        }

        /// <summary>Configures storage, loads the exact persisted scratch text, and registers host context.</summary>
        public void Configure(FeedbackOverlayConfig config, IFeedbackContextProvider contextProvider)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            FeedbackStorage storage = new FeedbackStorage(config.StorageRootPath);
            string scratch = storage.LoadScratch();
            _config = config;
            _storage = storage;
            _contextProvider = contextProvider;
            _scratch = scratch;
            _persistedScratch = scratch;
        }

        /// <summary>Shows the overlay and focuses its scratch editor.</summary>
        public void Show()
        {
            EnsureConfigured();
            _visible = true;
            _focusScratch = true;
            _focusWindow = true;
            NotifyVisibilityChanged();
        }

        /// <summary>Persists the scratch text and hides the overlay.</summary>
        public void Hide()
        {
            if (!_visible)
                return;

            SaveScratchNow();
            _visible = false;
            NotifyVisibilityChanged();
        }

        /// <summary>Toggles visibility, persisting scratch text when closing.</summary>
        public void Toggle()
        {
            if (_visible)
                Hide();
            else
                Show();
        }

        private void Awake()
        {
            _windowRect = BuildDefaultWindowRect();
        }

        private void Update()
        {
            if (!_visible)
                return;

            CapturePolledTextInput();

            if (!string.Equals(_scratch, _persistedScratch, StringComparison.Ordinal)
                && Time.realtimeSinceStartup - _scratchChangedAt >= ScratchSaveDelaySeconds)
            {
                SaveScratchNow();
            }
        }

        private void OnGUI()
        {
            Event current = Event.current;
            if (IsToggleKeyDown(current))
            {
                Toggle();
                current.Use();
            }

            // CaptureScreenshot records the completed frame. Suppress only the
            // overlay's rendering for the deferred capture frame while keeping
            // the host input gate active and the overlay logically open.
            if (_captureInProgress)
                return;

            if (!_visible || _storage == null)
            {
                ConsumeClosingPointerRelease(current);
                return;
            }

            EnsureStyles();
            GUI.depth = -32000;
            ConstrainWindowToScreen();
            if (IsPointerEvent(current) && !ContainsPointer(current))
            {
                if (current.type == EventType.MouseDown)
                {
                    _consumePointerUntilMouseUp = true;
                    Hide();
                }

                current.Use();
                return;
            }

            string title = string.IsNullOrEmpty(_config.WindowTitle) ? "Developer Feedback" : _config.WindowTitle;
            _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, title, _windowStyle);
            if (_focusWindow)
            {
                GUI.FocusWindow(GetInstanceID());
                _focusWindow = false;
            }

            if (current != null && IsKeyboardEvent(current))
                current.Use();
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(5f);
            GUILayout.Label("Notes autosave locally. Submit captures a clean frame and the recent runtime log.");
            GUILayout.Space(4f);

            GUI.SetNextControlName(ScratchControlName);
            GUIUtility.keyboardControl = 0;
            string updated = GUILayout.TextArea(
                _scratch ?? string.Empty,
                _textAreaStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            if (!string.Equals(updated, _scratch, StringComparison.Ordinal))
            {
                _scratch = updated;
                _scratchChangedAt = Time.realtimeSinceStartup;
            }

            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            GUI.enabled = !string.IsNullOrEmpty(_scratch);
            if (GUILayout.Button("Submit Entry", GUILayout.Height(28f)))
                SubmitFeedback(false);
            GUI.enabled = true;
            if (GUILayout.Button("Screenshot Only", GUILayout.Height(28f)))
                SubmitFeedback(true);
            if (GUILayout.Button("Close", GUILayout.Width(80f), GUILayout.Height(28f)))
                Hide();
            GUILayout.EndHorizontal();

            string footer = Time.realtimeSinceStartup < _statusExpiresAt
                ? _status
                : "Storage: " + _storage.RootPath;
            GUILayout.Label(footer, _statusStyle, GUILayout.Height(21f));

            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }

        private void SubmitFeedback(bool screenshotOnly)
        {
            if (_captureInProgress)
                return;

            DateTime timestamp = DateTime.Now;
            try
            {
                IList<KeyValuePair<string, string>> context = CaptureContext();
                string screenshotPath = _storage.ReserveScreenshotPath(timestamp);
                string submittedText = _scratch;
                string logExcerpt = _storage.ReadLogExcerpt(
                    _config.RuntimeLogPath,
                    _config.MaxLogExcerptLines,
                    _config.MaxLogExcerptBytes);
                _captureInProgress = true;
                StartCoroutine(CaptureFeedback(timestamp, submittedText, context, screenshotPath, logExcerpt, screenshotOnly));
            }
            catch (Exception ex)
            {
                _captureInProgress = false;
                SetStatus("Save failed: " + ex.Message);
                MMLog.WarnOnce("FeedbackOverlay.SubmitFeedback", "Feedback entry could not be saved: " + ex.Message);
            }
        }

        private IEnumerator CaptureFeedback(
            DateTime timestamp,
            string submittedText,
            IList<KeyValuePair<string, string>> context,
            string screenshotPath,
            string logExcerpt,
            bool screenshotOnly)
        {
            // The click frame already contains this IMGUI window. Wait for a
            // fresh frame in which OnGUI suppresses it, then capture that clean
            // frame at the end of rendering.
            yield return null;
            yield return new WaitForEndOfFrame();

            try
            {
                Application.CaptureScreenshot(screenshotPath);
                _storage.AppendEntry(timestamp, submittedText, context, screenshotPath, logExcerpt, screenshotOnly);

                if (!screenshotOnly && string.Equals(_scratch, submittedText, StringComparison.Ordinal))
                {
                    _scratch = string.Empty;
                    SaveScratchNow();
                }

                SetStatus("Saved.");
                _focusScratch = true;
            }
            catch (Exception ex)
            {
                SetStatus("Save failed: " + ex.Message);
                MMLog.WarnOnce("FeedbackOverlay.SubmitFeedback", "Feedback entry could not be saved: " + ex.Message);
            }
            finally
            {
                _captureInProgress = false;
            }
        }

        private IList<KeyValuePair<string, string>> CaptureContext()
        {
            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
            if (_contextProvider == null)
                return result;

            try
            {
                IEnumerable<KeyValuePair<string, string>> lines = _contextProvider.GetContextLines();
                if (lines != null)
                {
                    foreach (KeyValuePair<string, string> line in lines)
                        result.Add(line);
                }
            }
            catch (Exception ex)
            {
                result.Add(new KeyValuePair<string, string>("Context provider error", ex.Message));
                MMLog.WarnOnce("FeedbackOverlay.ContextProvider", "Feedback context provider failed: " + ex.Message);
            }

            return result;
        }

        private void SaveScratchNow()
        {
            if (_storage == null || string.Equals(_scratch, _persistedScratch, StringComparison.Ordinal))
                return;

            try
            {
                _storage.SaveScratch(_scratch);
                _persistedScratch = _scratch;
            }
            catch (Exception ex)
            {
                SetStatus("Scratch save failed: " + ex.Message);
                MMLog.WarnOnce("FeedbackOverlay.SaveScratch", "Feedback scratch could not be saved: " + ex.Message);
            }
        }

        private void CapturePolledTextInput()
        {
            string input = Input.inputString;
            if (string.IsNullOrEmpty(input))
                return;

            string value = _scratch ?? string.Empty;
            for (int i = 0; i < input.Length; i++)
            {
                char character = input[i];
                if (character == '\b')
                {
                    if (value.Length > 0)
                        value = value.Substring(0, value.Length - 1);
                }
                else if (character == '\r' || character == '\n' || !char.IsControl(character))
                {
                    value += character == '\r' ? '\n' : character;
                }
            }

            if (!string.Equals(value, _scratch, StringComparison.Ordinal))
            {
                _scratch = value;
                _scratchChangedAt = Time.realtimeSinceStartup;
            }
        }

        private void OnApplicationQuit()
        {
            SaveScratchNow();
        }

        private void OnDisable()
        {
            SaveScratchNow();
        }

        private void OnDestroy()
        {
            if (_parchmentTexture != null)
                Destroy(_parchmentTexture);
        }

        private void SetStatus(string message)
        {
            _status = message ?? string.Empty;
            _statusExpiresAt = Time.realtimeSinceStartup + ConfirmationSeconds;
        }

        private void NotifyVisibilityChanged()
        {
            if (_config == null || _config.OverlayVisibilityChanged == null)
                return;

            try
            {
                _config.OverlayVisibilityChanged(_visible);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("FeedbackOverlay.VisibilityChanged", "Feedback input gate failed: " + ex.Message);
            }
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
                return;

            _parchmentTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _parchmentTexture.SetPixel(0, 0, new Color(0.78f, 0.72f, 0.57f, 0.98f));
            _parchmentTexture.Apply();

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _parchmentTexture;
            _windowStyle.padding = new RectOffset(14, 14, 28, 12);
            _windowStyle.normal.textColor = new Color(0.12f, 0.1f, 0.07f, 1f);

            _textAreaStyle = new GUIStyle(GUI.skin.textArea);
            _textAreaStyle.wordWrap = true;
            _textAreaStyle.fontSize = 15;
            _textAreaStyle.padding = new RectOffset(9, 9, 8, 8);

            _statusStyle = new GUIStyle(GUI.skin.label);
            _statusStyle.fontSize = 11;
            _statusStyle.normal.textColor = new Color(0.18f, 0.14f, 0.08f, 1f);
            _statusStyle.clipping = TextClipping.Clip;
        }

        private void EnsureConfig()
        {
            if (_config == null)
                throw new InvalidOperationException("Configure the feedback overlay before changing its settings.");
        }

        private void EnsureConfigured()
        {
            if (_storage == null)
                throw new InvalidOperationException("Configure the feedback overlay before showing it.");
        }

        private Rect BuildDefaultWindowRect()
        {
            float width = Mathf.Min(760f, Mathf.Max(420f, Screen.width - 48f));
            float height = Mathf.Min(560f, Mathf.Max(320f, Screen.height - 48f));
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void ConstrainWindowToScreen()
        {
            _windowRect.width = Mathf.Min(_windowRect.width, Mathf.Max(320f, Screen.width));
            _windowRect.height = Mathf.Min(_windowRect.height, Mathf.Max(240f, Screen.height));
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
        }

        private static bool IsKeyboardEvent(Event current)
        {
            return current.type == EventType.KeyDown || current.type == EventType.KeyUp;
        }

        private bool IsToggleKeyDown(Event current)
        {
            return current != null
                && current.type == EventType.KeyDown
                && current.keyCode == ToggleKey;
        }

        private static bool IsConsumablePointerEvent(Event current)
        {
            return current.type == EventType.MouseDown
                || current.type == EventType.MouseUp
                || current.type == EventType.MouseDrag
                || current.type == EventType.ScrollWheel;
        }

        private static bool IsPointerEvent(Event current)
        {
            return current != null && IsConsumablePointerEvent(current);
        }

        private bool ContainsPointer(Event current)
        {
            return current != null && _windowRect.Contains(GUIUtility.GUIToScreenPoint(current.mousePosition));
        }

        private void ConsumeClosingPointerRelease(Event current)
        {
            if (!_consumePointerUntilMouseUp || current == null)
                return;

            if (current.type == EventType.MouseUp)
                _consumePointerUntilMouseUp = false;

            if (IsConsumablePointerEvent(current))
                current.Use();
        }
    }
}
