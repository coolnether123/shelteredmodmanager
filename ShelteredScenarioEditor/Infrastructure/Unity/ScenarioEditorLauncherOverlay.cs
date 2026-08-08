using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;

namespace ShelteredScenarioEditor.Infrastructure.Unity
{
    /// <summary>Editor-owned launcher kept separate from the installed-scenario browser.</summary>
    internal sealed class ScenarioEditorLauncherOverlay : MonoBehaviour
    {
        private const int WindowId = 19450731;
        private Rect _window = new Rect(0f, 0f, 620f, 520f);
        private Vector2 _scroll;
        private bool _open;
        private string _status;
        private ScenarioInfo[] _drafts = new ScenarioInfo[0];
        private GUIStyle _bodyLabelStyle;
        private GUIStyle _draftLabelStyle;

        private void OnGUI()
        {
            ScenarioSelectionPanel panel = UnityEngine.Object.FindObjectOfType(typeof(ScenarioSelectionPanel)) as ScenarioSelectionPanel;
            if (panel == null || !panel.gameObject.activeInHierarchy)
            {
                _open = false;
                return;
            }

            if (!_open)
            {
                float launchWidth = Mathf.Min(272f, Mathf.Max(220f, Screen.width - 24f));
                Rect launch = new Rect(Mathf.Max(12f, Screen.width - launchWidth - 20f), 20f, launchWidth, 44f);
                if (GUI.Button(launch, "CUSTOM SCENARIO EDITOR"))
                {
                    _open = true;
                    _status = null;
                    _drafts = ScenarioAuthoringDraftRepository.Instance.ListAll() ?? new ScenarioInfo[0];
                    _window.width = Mathf.Min(620f, Mathf.Max(280f, Screen.width - 20f));
                    _window.height = Mathf.Min(520f, Mathf.Max(320f, Screen.height - 20f));
                    _window.x = Mathf.Max(10f, (Screen.width - _window.width) * 0.5f);
                    _window.y = Mathf.Max(10f, (Screen.height - _window.height) * 0.5f);
                }
                return;
            }

            _window.width = Mathf.Min(_window.width, Mathf.Max(280f, Screen.width - 20f));
            _window.height = Mathf.Min(_window.height, Mathf.Max(320f, Screen.height - 20f));
            _window.x = Mathf.Clamp(_window.x, 10f, Mathf.Max(10f, Screen.width - _window.width - 10f));
            _window.y = Mathf.Clamp(_window.y, 10f, Mathf.Max(10f, Screen.height - _window.height - 10f));
            _window = GUI.ModalWindow(WindowId, _window, DrawWindow, "Custom Scenario Editor");
        }

        private void DrawWindow(int id)
        {
            if (_bodyLabelStyle == null)
            {
                _bodyLabelStyle = new GUIStyle(GUI.skin.label);
                _bodyLabelStyle.wordWrap = true;
            }
            if (_draftLabelStyle == null)
            {
                _draftLabelStyle = new GUIStyle(GUI.skin.label);
                _draftLabelStyle.wordWrap = false;
                _draftLabelStyle.clipping = TextClipping.Clip;
            }

            GUILayout.Space(8f);
            GUILayout.Label(
                "Editor drafts are stored separately from vanilla and installed scenario saves.",
                _bodyLabelStyle,
                GUILayout.MinHeight(34f));
            GUILayout.Space(8f);

            if (GUILayout.Button("CREATE NEW SCENARIO", GUILayout.Height(42f)))
                CreateAndLaunch();

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Existing editor drafts", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("REFRESH", GUILayout.Width(90f)))
                _drafts = ScenarioAuthoringDraftRepository.Instance.ListAll() ?? new ScenarioInfo[0];
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(120f, _window.height - 218f)));
            if (_drafts.Length == 0)
            {
                GUILayout.Label("No editor drafts yet.");
            }
            else
            {
                for (int i = 0; i < _drafts.Length; i++)
                {
                    ScenarioInfo draft = _drafts[i];
                    if (draft == null) continue;
                    GUILayout.BeginHorizontal();
                    string draftName = string.IsNullOrEmpty(draft.DisplayName) ? draft.Id : draft.DisplayName;
                    GUILayout.Label(
                        new GUIContent(draftName ?? string.Empty, draftName ?? string.Empty),
                        _draftLabelStyle,
                        GUILayout.ExpandWidth(true),
                        GUILayout.Height(30f));
                    if (GUILayout.Button("OPEN", GUILayout.Width(90f), GUILayout.Height(30f)))
                        OpenAndLaunch(draft.Id);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_status))
                GUILayout.Label(_status, _bodyLabelStyle, GUILayout.MinHeight(34f), GUILayout.MaxHeight(52f));
            if (GUILayout.Button("CLOSE", GUILayout.Height(32f)))
                _open = false;

            GUI.DragWindow(new Rect(0f, 0f, _window.width, 28f));
        }

        private void CreateAndLaunch()
        {
            ScenarioAuthoringSession session = ScenarioAuthoringBootstrapService.Instance.QueueNewDraft(
                ScenarioBaseGameMode.Survival,
                SaveManager.SaveType.Slot1,
                true);
            Launch(session, "new scenario draft");
        }

        private void OpenAndLaunch(string draftId)
        {
            ScenarioAuthoringSession session = ScenarioAuthoringBootstrapService.Instance.QueueExistingDraft(
                draftId,
                SaveManager.SaveType.Slot1);
            Launch(session, "scenario draft '" + (draftId ?? string.Empty) + "'");
        }

        private void Launch(ScenarioAuthoringSession session, string label)
        {
            if (session == null || session.StartupSave == null)
            {
                _status = "The editor could not prepare that draft.";
                return;
            }

            string message;
            ScenarioDefinition definition;
            try
            {
                definition = new ScenarioEditorDefinitionSerializer().Load(session.ScenarioFilePath);
            }
            catch (Exception ex)
            {
                _status = "The editor could not read that draft: " + ex.Message;
                MMLog.WriteWarning("[ScenarioEditorLauncher] " + _status);
                return;
            }

            bool launched = ShelteredScenarioRuntime.TryLaunchScenarioWorld(
                new ScenarioWorldLaunchRequest
                {
                    StorageScenarioId = session.StorageScenarioId,
                    StartupSave = session.StartupSave,
                    SaveType = session.LaunchSaveType,
                    TargetLabel = label,
                    BaseGameMode = session.BaseMode,
                    Definition = definition
                },
                out message);
            _status = launched ? "Launching editor world..." : (message ?? "The editor world could not launch.");
            if (launched) _open = false;
            else MMLog.WriteWarning("[ScenarioEditorLauncher] " + _status);
        }
    }
}
