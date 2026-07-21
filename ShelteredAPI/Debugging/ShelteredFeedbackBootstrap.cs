using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Debugging;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using UnityEngine;

namespace ShelteredAPI.Debugging
{
    internal static class ShelteredFeedbackBootstrap
    {
        public static void EnsureInstalled(GameObject runtimeRoot)
        {
            if (runtimeRoot == null)
                throw new ArgumentNullException("runtimeRoot");

            FeedbackOverlay overlay = runtimeRoot.GetComponent<FeedbackOverlay>();
            if (overlay != null)
                return;

            try
            {
                string installRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string smmRoot = Path.Combine(installRoot, "SMM");
                string storageRoot = Path.Combine(smmRoot, "Feedback");
                FeedbackOverlayConfig config = new FeedbackOverlayConfig(storageRoot)
                {
                    ToggleKey = KeyCode.F4,
                    WindowTitle = "Sheltered Developer Feedback",
                    RuntimeLogPath = Path.Combine(smmRoot, "mod_manager.log"),
                    OverlayVisibilityChanged = ShelteredFeedbackInputEnabler.SetOverlayVisible
                };

                overlay = runtimeRoot.AddComponent<FeedbackOverlay>();
                overlay.Configure(config, new ShelteredFeedbackContextProvider());
                MMLog.WriteInfo("[ShelteredFeedbackBootstrap] Feedback overlay installed on "
                    + runtimeRoot.name + ". Storage=" + storageRoot + ".");
            }
            catch (Exception ex)
            {
                if (overlay != null)
                    UnityEngine.Object.Destroy(overlay);
                MMLog.WarnOnce(
                    "ShelteredFeedbackBootstrap.Install",
                    "Sheltered feedback overlay could not be installed: " + ex.Message);
            }
        }
    }

    /// <summary>Bridges the reusable overlay's visibility to the Sheltered authoring input gate.</summary>
    internal static class ShelteredFeedbackInputEnabler
    {
        private static bool _overlayVisible;

        public static bool IsOverlayVisible
        {
            get { return _overlayVisible; }
        }

        public static void SetOverlayVisible(bool visible)
        {
            _overlayVisible = visible;
            ApplyAuthoringCapture();
        }

        internal static void ApplyAuthoringCapture()
        {
            if (!_overlayVisible)
                return;

            try
            {
                ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
                if (inputCapture != null)
                    inputCapture.SetKeyboardCaptured(true);
            }
            catch
            {
                // The feedback surface must remain available outside authoring.
            }
        }
    }

    internal sealed class ShelteredFeedbackContextProvider : IFeedbackContextProvider
    {
        public IEnumerable<KeyValuePair<string, string>> GetContextLines()
        {
            List<KeyValuePair<string, string>> lines = new List<KeyValuePair<string, string>>();
            AddSceneContext(lines);
            AddScenarioContext(lines);
            AddGameTimeContext(lines);
            return lines;
        }

        private static void AddSceneContext(ICollection<KeyValuePair<string, string>> lines)
        {
            try
            {
                lines.Add(Context("Scene", Application.loadedLevelName));
            }
            catch (Exception ex)
            {
                lines.Add(Context("Scene", "Unavailable (" + ex.Message + ")"));
            }
        }

        private static void AddScenarioContext(ICollection<KeyValuePair<string, string>> lines)
        {
            try
            {
                IScenarioAuthoringBackend backend = ScenarioCompositionRoot.Resolve<IScenarioAuthoringBackend>();
                ScenarioAuthoringState state = backend != null ? backend.CurrentState : null;
                bool active = state != null && state.IsActive;
                lines.Add(Context("Scenario editor active", active ? "Yes" : "No"));
                if (!active)
                    return;

                lines.Add(Context("Draft id", state.ActiveDraftId));
                lines.Add(Context("Workshop page", state.ActiveShellTab.ToString()));
            }
            catch (Exception ex)
            {
                lines.Add(Context("Scenario editor", "Unavailable (" + ex.Message + ")"));
            }
        }

        private static void AddGameTimeContext(ICollection<KeyValuePair<string, string>> lines)
        {
            try
            {
                lines.Add(Context(
                    "Game time",
                    "Day " + GameTime.Day + ", " + GameTime.Hour.ToString("00") + ":" + GameTime.Minute.ToString("00")));
            }
            catch (Exception ex)
            {
                lines.Add(Context("Game time", "Unavailable (" + ex.Message + ")"));
            }
        }

        private static KeyValuePair<string, string> Context(string key, string value)
        {
            return new KeyValuePair<string, string>(key, string.IsNullOrEmpty(value) ? "(none)" : value);
        }
    }
}
