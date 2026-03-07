using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Debugging;
using ModAPI.Internal.DebugUI;
using ModAPI.Inspector;

namespace ModAPI.UI
{
    public class UIDebugInspector : MonoBehaviour
    {
        private bool _active = false;
        private GameObject _lastHover;
        
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                _showTranspilerLogs = !_showTranspilerLogs;
                _active = _showTranspilerLogs;
                MMLog.WriteInfo($"[UIDebug] Transpiler Inspector {(_showTranspilerLogs ? "Enabled" : "Disabled")}");
            }

            if (_active && !_showTranspilerLogs && Input.GetMouseButtonDown(0))
            {
                if (_lastHover != null)
                {
                    DumpWidgetInfo(_lastHover);
                }
            }
        }
        
        private bool _showTranspilerLogs = false;
        private bool _showModList = false;
        private string _lastLoggedHistorySearch = string.Empty;
        private string _lastLoggedIlSearch = string.Empty;
        private string _lastLoggedActiveScriptSearch = string.Empty;
        private bool _historySearchHadFocus = false;
        private bool _ilSearchHadFocus = false;
        private bool _activeScriptSearchHadFocus = false;
        private bool _followLiveSourceLine = true;
        private int _lastAutoFollowSourceLine = -1;

        public void OnGUI()
        {
            if (!_active) return;
            
            if (_showTranspilerLogs)
            {
                DrawTranspilerOverlay();
                return;
            }
            
            if (_showModList)
            {
                DrawModListOverlay();
                return;
            }

            var cam = UICamera.currentCamera;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            // Simple Raycast using NGUI logic would be best, but let's use UICamera.raycastGlobal
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, 1 << LayerMask.NameToLayer("UI")))
            {
                _lastHover = hit.collider.gameObject;
                DrawOverlay(_lastHover);
            }
            else
            {
                _lastHover = null;
            }
        }

        private ModAPI.Harmony.TranspilerDebugger.Snapshot _selectedSnapshot;
        private Vector2 _scrollPos;

        private bool _groupByMod = true;
        private bool _showCorePatches = true;
        private bool _showExternalPatches = true;
        private bool _showDetailWindow = true;
        private bool _showNavigatorSettings = false;
        private bool _showNavigatorSummary = true;
        private bool _preferSourceDiffDefault = true;
        private Dictionary<string, bool> _modFoldouts = new Dictionary<string, bool>();
        private string _ilSearchPattern = "";
        private string _historyMethodSearch = "";
        private readonly List<MethodBase> _runtimeMethodMatches = new List<MethodBase>();
        private string _runtimeMethodSearchLast = string.Empty;
        private float _nextRuntimeMethodSearchTime = 0f;
        private GUIStyle _navigatorHintStyle;

        private bool _isLiveMode = false;
        private Vector2 _scrollLive;
        private Vector2 _scrollLiveSource;
        private Vector2 _scrollAlerts;
        private string _liveSourceText = string.Empty;
        private string _liveSourceStatus = string.Empty;
        private string _liveSourceMethodId = string.Empty;

        private enum SnapshotViewMode
        {
            Diff,
            Source,
            SourceDiff,
            PatchedIL,
            VanillaIL
        }

        private enum SourceSingleViewMode
        {
            Decompiled,
            RegexRewritten,
            OverlayPreview
        }

        private SnapshotViewMode _snapshotViewMode = SnapshotViewMode.Diff;
        private SourceSingleViewMode _sourceSingleViewMode = SourceSingleViewMode.Decompiled;
        private bool _sceneFilteredOnly = false;
        private string _activeSceneName = string.Empty;
        private float _nextSceneScanTime = 0f;
        private readonly HashSet<string> _sceneTypeHints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private MethodBase _selectedMethod;
        private string _selectedMethodId = string.Empty;
        private string _sourceText = string.Empty;
        private string _patchedSourceText = string.Empty;
        private string _patchedSourceRewrittenText = string.Empty;
        private string _sourceStatus = string.Empty;
        private Vector2 _scrollSource;
        private Vector2 _scrollSourceDiffLeft;
        private Vector2 _scrollSourceDiffRight;
        private Vector2 _scrollPatchedIL;
        private bool _syncSourceDiffScroll = true;
        private int _sourceRegexReplaceCount = 0;
        private readonly List<string> _sourceRegexSummaries = new List<string>();
        private bool _showSourceOverlayComments = false;
        private bool _showProvenancePanel = false;
        private bool _showTimelinePanel = false;
        private bool _showRegexDiagnostics = false;
        private bool _showSourceInspectorPanel = true;
        private bool _focusSourceLayout = true;
        private GUIStyle _sourceLineStyle;
        private int _selectedSourceLineNumber = -1;
        private string _selectedSourceLineText = string.Empty;
        private string _sourceLineInspectText = "Double-click a source line to inspect current runtime values.";
        private Vector2 _scrollTimeline;
        private Vector2 _scrollRegexDiagnostics;
        private string _lastExportPath = string.Empty;
        private Vector2 _scrollVanillaIL;
        private Vector2 _scrollActiveScripts;
        private string _activeScriptSearch = string.Empty;
        private float _nextActiveScriptScanTime = 0f;
        private readonly List<ActiveScriptInfo> _activeScripts = new List<ActiveScriptInfo>();
        private int _activeScriptInstanceCount = 0;

        private sealed class ActiveScriptInfo
        {
            public string TypeName;
            public int Count;
        }
        
        private void DrawTranspilerOverlay()
        {
            _currentTooltip = "";
            RefreshSceneHintsIfNeeded();
            if (_isLiveMode)
            {
                RefreshActiveScriptsIfNeeded();
            }
            var visibleHistory = GetVisibleSnapshots();
            
            float w = Mathf.Clamp(Screen.width * 0.97f, 1180f, 2100f);
            float h = Mathf.Clamp(Screen.height * 0.95f, 820f, 1300f);
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;
            GUI.Box(new Rect(x, y, w, h), "Transpiler Inspector & Live Debugger (F11 Toggle)");

            const float outerPad = 10f;
            const float gutter = 8f;
            const float headerH = 36f;
            var headerY = y + 24f;

            GUILayout.BeginArea(new Rect(x + outerPad, headerY, w - (outerPad * 2f), headerH));
            GUILayout.BeginHorizontal(GUI.skin.box);
            if (DrawStateButton("Snapshot", !_isLiveMode, "Browse transpiler snapshots captured at patch time.", GUILayout.Width(95)))
            {
                _isLiveMode = false;
                LogUiStep("Mode switched", "Snapshot");
            }
            if (DrawStateButton("Live", _isLiveMode, "Attach to a method and inspect runtime frames while the game runs.", GUILayout.Width(80)))
            {
                _isLiveMode = true;
                LogUiStep("Mode switched", "Live");
            }

            GUILayout.Space(10f);
            GUILayout.Label(new GUIContent("Visible: " + visibleHistory.Count, "Snapshots currently visible after filters are applied."), GUILayout.Width(90));
            if (_selectedSnapshot != null)
            {
                GUILayout.Label(new GUIContent("Selected: " + BuildSnapshotDisplayTitle(_selectedSnapshot), "Method currently selected in the navigator."), GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.Label(new GUIContent("Selected: <none>", "Select a method from the navigator list."), GUILayout.ExpandWidth(true));
            }

            if (DrawStateButton("Detail Pane", _showDetailWindow, "Toggle the in-depth inspector window.", GUILayout.Width(105)))
            {
                _showDetailWindow = !_showDetailWindow;
                LogUiStep("Detail pane toggled", _showDetailWindow ? "Shown" : "Hidden");
            }
            if (DrawStateButton("Settings", _showNavigatorSettings, "Show or hide navigator options and defaults.", GUILayout.Width(90)))
            {
                _showNavigatorSettings = !_showNavigatorSettings;
                LogUiStep("Navigator settings toggled", _showNavigatorSettings ? "Shown" : "Hidden");
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            float bodyY = headerY + headerH + 6f;
            float bodyH = h - (bodyY - y) - outerPad;
            if (bodyH < 220f) bodyH = 220f;

            float navW = _showDetailWindow
                ? Mathf.Clamp(w * 0.30f, 320f, 480f)
                : (w - (outerPad * 2f));

            float navX = x + outerPad;
            DrawNavigatorPanel(navX, bodyY, navW, bodyH, visibleHistory);

            if (!_showDetailWindow)
            {
                return;
            }

            float detailX = navX + navW + gutter;
            float detailW = (x + w - outerPad) - detailX;
            if (detailW < 420f) detailW = 420f;

            if (!_isLiveMode)
            {
                DrawSnapshotDetail(detailX, bodyY, 0f, detailW, bodyH);
            }
            else
            {
                DrawLiveDebugger(detailX, bodyY, 0f, detailW, bodyH);
            }
        }

        private bool DrawStateButton(string text, bool active, string tooltip, params GUILayoutOption[] options)
        {
            var activeStyle = GUI.skin.FindStyle("button_on") ?? GUI.skin.button;
            var inactiveStyle = GUI.skin.FindStyle("button") ?? GUI.skin.button;
            var style = active ? activeStyle : inactiveStyle;
            return GUILayout.Button(new GUIContent(text, tooltip), style, options);
        }

        private void LogUiStep(string action, string detail = null)
        {
            if (string.IsNullOrEmpty(detail))
            {
                MMLog.WriteInfo("[UIDebugFlow] " + action);
            }
            else
            {
                MMLog.WriteInfo("[UIDebugFlow] " + action + " | " + detail);
            }
        }

        private void EnsureNavigatorHintStyle()
        {
            if (_navigatorHintStyle != null) return;
            _navigatorHintStyle = new GUIStyle(GUI.skin.box);
            _navigatorHintStyle.richText = true;
            _navigatorHintStyle.alignment = TextAnchor.MiddleLeft;
            _navigatorHintStyle.wordWrap = true;
            _navigatorHintStyle.fontSize = 11;
            _navigatorHintStyle.padding = new RectOffset(8, 8, 4, 4);
        }

        private void DrawNavigatorPanel(float x, float y, float w, float h, List<ModAPI.Harmony.TranspilerDebugger.Snapshot> history)
        {
            GUI.Box(new Rect(x, y, w, h), _isLiveMode ? "Runtime Navigator" : "Patch Navigator");
            var innerX = x + 8f;
            var innerY = y + 24f;
            var innerW = w - 16f;
            var innerH = h - 28f;

            GUILayout.BeginArea(new Rect(innerX, innerY, innerW, innerH));
            if (_showNavigatorSettings)
            {
                DrawNavigatorSettings();
            }
            DrawNavigatorSelectionSummary();

            GUILayout.Label(new GUIContent("<b>History</b>", "Select a transpiler step to inspect source, IL, provenance, and runtime behavior."));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUI.skin.box, GUILayout.ExpandHeight(true));
            if (history != null && history.Count > 0)
            {
                if (_groupByMod)
                {
                    var groups = history.GroupBy(s => s.ModId).OrderBy(g => g.Key);
                    foreach (var group in groups)
                    {
                        if (!_modFoldouts.ContainsKey(group.Key)) _modFoldouts[group.Key] = true;
                        var groupCore = group.All(IsCoreSnapshot);
                        var groupLabel = group.Key + (groupCore ? " (Core)" : "");

                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(new GUIContent(_modFoldouts[group.Key] ? "[-]" : "[+]", "Expand or collapse this mod group."), GUILayout.Width(25)))
                        {
                            _modFoldouts[group.Key] = !_modFoldouts[group.Key];
                        }
                        GUILayout.Label("<b>" + groupLabel + "</b>");
                        GUILayout.EndHorizontal();

                        if (_modFoldouts[group.Key])
                        {
                            foreach (var snap in group.OrderByDescending(s => s.Timestamp))
                            {
                                DrawSnapshotButton(snap);
                            }
                        }
                    }
                }
                else
                {
                    for (int i = history.Count - 1; i >= 0; i--)
                    {
                        DrawSnapshotButton(history[i]);
                    }
                }
            }
            else
            {
                GUILayout.Label("No snapshots recorded yet.");
            }

            if (_isLiveMode && !string.IsNullOrEmpty(_historyMethodSearch))
            {
                DrawRuntimeMethodMatches(_historyMethodSearch);
            }
            GUILayout.EndScrollView();

            DrawNavigatorTooltipFooter();
            GUILayout.EndArea();
        }

        private void DrawNavigatorSettings()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Navigator Settings</b>");

            GUILayout.BeginHorizontal();
            if (DrawStateButton("Group by Mod", _groupByMod, "Group history by mod owner for big-picture review.", GUILayout.Width(120)))
            {
                _groupByMod = true;
                LogUiStep("Grouping changed", "Group by Mod");
            }
            if (DrawStateButton("Chronological", !_groupByMod, "Show history in strict timestamp order.", GUILayout.Width(120)))
            {
                _groupByMod = false;
                LogUiStep("Grouping changed", "Chronological");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var oldShowCore = _showCorePatches;
            var oldShowMods = _showExternalPatches;
            var oldSceneOnly = _sceneFilteredOnly;
            _showCorePatches = GUILayout.Toggle(_showCorePatches, new GUIContent("Core", "Include ModAPI-owned patches in the list."), GUILayout.Width(60));
            _showExternalPatches = GUILayout.Toggle(_showExternalPatches, new GUIContent("Mods", "Include external mod patches in the list."), GUILayout.Width(60));
            _sceneFilteredOnly = GUILayout.Toggle(_sceneFilteredOnly, new GUIContent("Scene Only", "Restrict list to methods likely active in the current scene."), GUILayout.Width(100));
            if (oldShowCore != _showCorePatches) LogUiStep("Filter toggled", "Core=" + _showCorePatches);
            if (oldShowMods != _showExternalPatches) LogUiStep("Filter toggled", "Mods=" + _showExternalPatches);
            if (oldSceneOnly != _sceneFilteredOnly) LogUiStep("Filter toggled", "SceneOnly=" + _sceneFilteredOnly);
            GUILayout.Label(new GUIContent("Scene: " + (string.IsNullOrEmpty(_activeSceneName) ? "<unknown>" : _activeSceneName), "Current runtime scene detected by ModAPI."), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Find Method", "Filter left history cards by method/mod/origin text."), GUILayout.Width(72));
            GUI.SetNextControlName("HistoryMethodSearch");
            _historyMethodSearch = GUILayout.TextField(_historyMethodSearch, GUILayout.Width(140));
            HandleSearchCommitLogging("HistoryMethodSearch", ref _historySearchHadFocus, _historyMethodSearch, ref _lastLoggedHistorySearch, "Method search submitted");
            if (GUILayout.Button(new GUIContent("Clear", "Reset method filter."), GUILayout.Width(55)))
            {
                _historyMethodSearch = string.Empty;
                _lastLoggedHistorySearch = string.Empty;
                LogUiStep("Method search cleared");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Search IL", "Filter IL rows by text in IL-based views."), GUILayout.Width(62));
            GUI.SetNextControlName("IlSearchPattern");
            _ilSearchPattern = GUILayout.TextField(_ilSearchPattern, GUILayout.Width(150));
            HandleSearchCommitLogging("IlSearchPattern", ref _ilSearchHadFocus, _ilSearchPattern, ref _lastLoggedIlSearch, "IL search submitted");
            if (GUILayout.Button(new GUIContent("Clear", "Reset IL search filter."), GUILayout.Width(55)))
            {
                _ilSearchPattern = string.Empty;
                _lastLoggedIlSearch = string.Empty;
                LogUiStep("IL search cleared");
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var oldPreferSourceDiff = _preferSourceDiffDefault;
            _preferSourceDiffDefault = GUILayout.Toggle(
                _preferSourceDiffDefault,
                new GUIContent("Default to Source Diff on selection", "When enabled, selecting a snapshot opens Source Diff first for faster readability."));
            if (oldPreferSourceDiff != _preferSourceDiffDefault) LogUiStep("Preference changed", "DefaultSourceDiff=" + _preferSourceDiffDefault);
            var oldSourceInspector = _showSourceInspectorPanel;
            _showSourceInspectorPanel = GUILayout.Toggle(
                _showSourceInspectorPanel,
                new GUIContent("Show Source Line Inspector", "Keep runtime value inspector visible below source diff."));
            if (oldSourceInspector != _showSourceInspectorPanel) LogUiStep("Preference changed", "ShowSourceInspector=" + _showSourceInspectorPanel);
            var oldOverlayComments = _showSourceOverlayComments;
            _showSourceOverlayComments = GUILayout.Toggle(
                _showSourceOverlayComments,
                new GUIContent("Default Show Overlay Comments", "Render inline IL overlay comments in patched source view by default."));
            if (oldOverlayComments != _showSourceOverlayComments) LogUiStep("Preference changed", "ShowOverlayComments=" + _showSourceOverlayComments);
            var oldFocusSource = _focusSourceLayout;
            _focusSourceLayout = GUILayout.Toggle(
                _focusSourceLayout,
                new GUIContent("Focus Source Layout", "Prioritize source panes over provenance/timeline when in source-focused views."));
            if (oldFocusSource != _focusSourceLayout) LogUiStep("Preference changed", "FocusSourceLayout=" + _focusSourceLayout);

            if (_isLiveMode)
            {
                var attached = LiveDebugger.AttachedMethod;
                if (attached != null)
                {
                    GUILayout.Label(new GUIContent("<color=lime>[ATTACHED]</color> " + BuildMethodDisplayName(attached, null), "Method currently attached for runtime capture."));
                }
                else
                {
                    GUILayout.Label(new GUIContent("<color=red>[DISCONNECTED]</color> Select a method and attach from detail panel.", "No method is attached to live debugger."));
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawNavigatorSelectionSummary()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(_showNavigatorSummary ? "[-]" : "[+]", "Collapse or expand the selected method summary."), GUILayout.Width(25)))
            {
                _showNavigatorSummary = !_showNavigatorSummary;
                LogUiStep("Selection summary toggled", _showNavigatorSummary ? "Expanded" : "Collapsed");
            }
            GUILayout.Label(new GUIContent("<b>Selection Summary</b>", "High-level status and quick actions for the selected step."));
            GUILayout.EndHorizontal();

            if (_showNavigatorSummary)
            {
                if (_selectedSnapshot == null)
                {
                    if (_selectedMethod != null)
                    {
                        GUILayout.Label("Method: " + BuildMethodDisplayName(_selectedMethod, null));
                        GUILayout.Label("Owner: Runtime/Vanilla");
                        GUILayout.Label("Diff: n/a (no transpiler snapshot selected)");
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(new GUIContent("Live Attach", "Attach selected method to runtime debugger."), GUILayout.Width(100)))
                        {
                            TryAttachSelectedMethod();
                            _isLiveMode = true;
                            _showDetailWindow = true;
                            LogUiStep("Live attach requested", BuildMethodDisplayName(_selectedMethod, null));
                        }
                        if (GUILayout.Button(new GUIContent("Clear Method", "Clear current runtime method selection."), GUILayout.Width(110)))
                        {
                            LogUiStep("Selected runtime method cleared", _selectedMethodId);
                            _selectedMethod = null;
                            _selectedMethodId = string.Empty;
                        }
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.Label("No step selected.");
                    }
                }
                else
                {
                    GUILayout.Label("Step: " + BuildSnapshotDisplayTitle(_selectedSnapshot));
                    GUILayout.Label("Owner: " + (IsCoreSnapshot(_selectedSnapshot) ? "ModAPI Core" : "External Mod"));
                    GUILayout.Label("Diff: " + _selectedSnapshot.DiffSummary + " (+" + _selectedSnapshot.AddedCount + "/-" + _selectedSnapshot.RemovedCount + ")");
                    if (_selectedSnapshot.WarningCount > 0)
                    {
                        GUILayout.Label("<color=orange>Warnings: " + _selectedSnapshot.WarningCount + "</color>");
                    }

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent("Open Source Diff", "Jump detail pane to source-level before/after comparison."), GUILayout.Width(120)))
                    {
                        _isLiveMode = false;
                        _showDetailWindow = true;
                        _snapshotViewMode = SnapshotViewMode.SourceDiff;
                        LoadSourceForSelectedSnapshot();
                        LogUiStep("Quick action", "Open Source Diff");
                    }
                    if (GUILayout.Button(new GUIContent("Open IL Diff", "Jump detail pane to IL diff comparison."), GUILayout.Width(100)))
                    {
                        _isLiveMode = false;
                        _showDetailWindow = true;
                        _snapshotViewMode = SnapshotViewMode.Diff;
                        LogUiStep("Quick action", "Open IL Diff");
                    }
                    if (GUILayout.Button(new GUIContent("Live Attach", "Attach selected method to runtime debugger."), GUILayout.Width(90)))
                    {
                        TryAttachSelectedSnapshot();
                        _isLiveMode = true;
                        _showDetailWindow = true;
                        LogUiStep("Live attach requested", BuildSnapshotDisplayTitle(_selectedSnapshot));
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawNavigatorTooltipFooter()
        {
            EnsureNavigatorHintStyle();
            var tip = GUI.tooltip;
            if (string.IsNullOrEmpty(tip))
            {
                tip = "Hover any control to see what it does.";
            }
            GUILayout.Label(tip, _navigatorHintStyle, GUILayout.Height(38));
        }

        private void DrawSnapshotDetail(float x, float y, float topOffset, float w, float h)
        {
            GUI.Box(new Rect(x, y + topOffset, w, h), "Snapshot Inspector");
            if (_selectedSnapshot == null)
            {
                GUI.Label(new Rect(x + 10, y + topOffset + 50, w - 20, 100), "Select a snapshot to view.", GUI.skin.label);
                return;
            }

            float innerX = x + 10;
            float innerW = w - 20;
            
            // DYNAMIC HEADER HEIGHT: 
            // Calculate height based on warning count to avoid clipping the control buttons.
            float metadataH = 150f;
            if (_selectedSnapshot.Warnings != null && _selectedSnapshot.Warnings.Count > 0)
            {
                metadataH += (_selectedSnapshot.Warnings.Count * 18f) + 20f;
            }

            var sourceFocusedView = _snapshotViewMode == SnapshotViewMode.Source || _snapshotViewMode == SnapshotViewMode.SourceDiff;
            var effectiveShowProvenance = _showProvenancePanel && !(_focusSourceLayout && sourceFocusedView);
            var effectiveShowTimeline = _showTimelinePanel && !(_focusSourceLayout && sourceFocusedView);
            float provenanceH = effectiveShowProvenance ? 82f : 0f;
            float timelineH = effectiveShowTimeline ? 150f : 0f;
            float bottomInfoH = 140;

            GUILayout.BeginArea(new Rect(innerX, y + topOffset + 25, innerW, metadataH));
            GUILayout.Label($"<b>Step:</b> {BuildSnapshotDisplayTitle(_selectedSnapshot)}");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Mod:</b> {_selectedSnapshot.ModId}", GUILayout.Width(innerW * 0.28f));
            GUILayout.Label($"<b>Time:</b> {_selectedSnapshot.Timestamp:HH:mm:ss} ({_selectedSnapshot.DurationMs:F2}ms)", GUILayout.Width(innerW * 0.32f));
            GUILayout.Label($"<b>Diff:</b> <color=yellow>{_selectedSnapshot.DiffSummary}</color> (+{_selectedSnapshot.AddedCount}/-{_selectedSnapshot.RemovedCount})", GUILayout.Width(innerW * 0.24f));
            GUILayout.Label($"<b>Method:</b> {(string.IsNullOrEmpty(_selectedMethodId) ? "<unresolved>" : _selectedMethodId)}", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            
            if (_selectedSnapshot.Warnings != null && _selectedSnapshot.Warnings.Count > 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<b><color=orange>Validation Warnings ({_selectedSnapshot.Warnings.Count}):</color></b>");
                foreach (var warn in _selectedSnapshot.Warnings)
                {
                    GUILayout.Label($"<color=orange>• {warn}</color>");
                }
                GUILayout.EndVertical();
            }
            
            if (!string.IsNullOrEmpty(_selectedSnapshot.PatchOrigin))
            {
                GUILayout.Label($"<b>Patch Origin:</b> {_selectedSnapshot.PatchOrigin}");
            }
            GUILayout.Label("<b>Ownership:</b> " + (IsCoreSnapshot(_selectedSnapshot) ? "<color=#8ED6FF>ModAPI Core</color>" : "<color=#9CFF9C>External Mod</color>"));

            GUILayout.BeginHorizontal();
            if (DrawStateButton("Diff", _snapshotViewMode == SnapshotViewMode.Diff, "Show IL diff", GUILayout.Width(90)))
            {
                _snapshotViewMode = SnapshotViewMode.Diff;
                LogUiStep("View changed", "Diff");
            }
            if (DrawStateButton("Vanilla Source", _snapshotViewMode == SnapshotViewMode.Source, "Show vanilla source", GUILayout.Width(120)))
            {
                _snapshotViewMode = SnapshotViewMode.Source;
                LoadSourceForSelectedSnapshot();
                LogUiStep("View changed", "Vanilla Source");
            }
            if (DrawStateButton("Source Diff", _snapshotViewMode == SnapshotViewMode.SourceDiff, "Show source diff", GUILayout.Width(100)))
            {
                _snapshotViewMode = SnapshotViewMode.SourceDiff;
                LoadSourceForSelectedSnapshot();
                LogUiStep("View changed", "Source Diff");
            }
            if (DrawStateButton("Vanilla IL", _snapshotViewMode == SnapshotViewMode.VanillaIL, "Show vanilla IL", GUILayout.Width(100)))
            {
                _snapshotViewMode = SnapshotViewMode.VanillaIL;
                LogUiStep("View changed", "Vanilla IL");
            }
            if (DrawStateButton("Patched IL", _snapshotViewMode == SnapshotViewMode.PatchedIL, "Show patched IL", GUILayout.Width(100)))
            {
                _snapshotViewMode = SnapshotViewMode.PatchedIL;
                LogUiStep("View changed", "Patched IL");
            }

            GUILayout.Space(8);
            if (GUILayout.Button("DEBUG THIS METHOD LIVE", GUILayout.Width(210)))
            {
                if (_selectedMethod != null)
                {
                    LiveDebugger.Attach(_selectedMethod);
                    _isLiveMode = true;
                    LogUiStep("Live attach requested", BuildMethodDisplayName(_selectedMethod, _selectedSnapshot));
                }
                else
                {
                    _sourceStatus = "Cannot attach: snapshot method could not be resolved.";
                    LogUiStep("Live attach failed", _sourceStatus);
                }
            }
            if (GUILayout.Button("Reload Source", GUILayout.Width(120)))
            {
                LoadSourceForSelectedSnapshot(true);
                LogUiStep("Source reloaded", BuildSnapshotDisplayTitle(_selectedSnapshot));
            }
            if (GUILayout.Button("Export Report", GUILayout.Width(130)))
            {
                ExportSelectedSnapshotReport();
                LogUiStep("Report export requested", BuildSnapshotDisplayTitle(_selectedSnapshot));
            }
            var oldProvenance = _showProvenancePanel;
            var oldTimeline = _showTimelinePanel;
            var oldFocusLayout = _focusSourceLayout;
            _showProvenancePanel = GUILayout.Toggle(_showProvenancePanel, "Provenance", GUILayout.Width(100));
            _showTimelinePanel = GUILayout.Toggle(_showTimelinePanel, "Timeline", GUILayout.Width(90));
            _focusSourceLayout = GUILayout.Toggle(_focusSourceLayout, "Focus Source", GUILayout.Width(110));
            if (oldProvenance != _showProvenancePanel) LogUiStep("Panel toggled", "Provenance=" + _showProvenancePanel);
            if (oldTimeline != _showTimelinePanel) LogUiStep("Panel toggled", "Timeline=" + _showTimelinePanel);
            if (oldFocusLayout != _focusSourceLayout) LogUiStep("Panel toggled", "FocusSource=" + _focusSourceLayout);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_sourceStatus))
            {
                GUILayout.Label(_sourceStatus);
            }
            if (!string.IsNullOrEmpty(_lastExportPath))
            {
                GUILayout.Label("<size=10>Last export: " + _lastExportPath + "</size>");
            }

            GUILayout.EndArea();

            float contentY = y + topOffset + 25 + metadataH + 12;
            if (effectiveShowProvenance)
            {
                DrawPatchProvenancePanel(innerX, contentY, innerW, provenanceH);
                contentY += provenanceH + 6f;
            }
            if (effectiveShowTimeline)
            {
                DrawStepTimelinePanel(innerX, contentY, innerW, timelineH);
                contentY += timelineH + 6f;
            }

            var needsAnalysisPanel =
                _snapshotViewMode == SnapshotViewMode.Diff ||
                _snapshotViewMode == SnapshotViewMode.VanillaIL ||
                _snapshotViewMode == SnapshotViewMode.PatchedIL;
            var analysisReserved = needsAnalysisPanel ? bottomInfoH : 0f;
            var reserved = metadataH + analysisReserved + 54f + provenanceH + timelineH + (effectiveShowProvenance ? 6f : 0f) + (effectiveShowTimeline ? 6f : 0f);
            float contentH = h - reserved;
            if (contentH < 80) contentH = 80;

            switch (_snapshotViewMode)
            {
                case SnapshotViewMode.Diff:
                    DrawSnapshotDiffContent(innerX, contentY, innerW, contentH);
                    DrawInstructionAnalysisPanel(innerX, y + topOffset + h - bottomInfoH - 5, innerW, bottomInfoH);
                    break;
                case SnapshotViewMode.Source:
                    DrawSnapshotSourceContent(innerX, contentY, innerW, contentH);
                    break;
                case SnapshotViewMode.SourceDiff:
                    DrawSnapshotSourceDiffContent(innerX, contentY, innerW, contentH);
                    break;
                case SnapshotViewMode.VanillaIL:
                    DrawSnapshotVanillaILContent(innerX, contentY, innerW, contentH);
                    DrawInstructionAnalysisPanel(innerX, y + topOffset + h - bottomInfoH - 5, innerW, bottomInfoH);
                    break;
                case SnapshotViewMode.PatchedIL:
                    DrawSnapshotPatchedILContent(innerX, contentY, innerW, contentH);
                    DrawInstructionAnalysisPanel(innerX, y + topOffset + h - bottomInfoH - 5, innerW, bottomInfoH);
                    break;
            }
        }

        private void DrawInstructionLine(int index, string line, List<int> stacks, bool isDiffView, string marker = "")
        {
            if (string.IsNullOrEmpty(line))
            {
                 // Render an explicit spacer row to preserve before/after alignment.
                 GUILayout.BeginHorizontal();
                 GUILayout.Label("", GUILayout.Width(30));
                 GUILayout.Label("", GUILayout.Width(35));
                 GUILayout.Label("", GUILayout.ExpandWidth(true));
                 GUILayout.EndHorizontal();
                 return;
            }

            // Filter
             if (!string.IsNullOrEmpty(_ilSearchPattern) && line.IndexOf(_ilSearchPattern, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            GUILayout.BeginHorizontal();
            
            // Index
            GUI.contentColor = Color.gray;
            GUILayout.Label($"{index:D3}", GUILayout.Width(30));
            
            // Stack Depth
            int depth = (stacks != null && index < stacks.Count) ? stacks[index] : -1;
            string stackStr = depth >= 0 ? $"[{depth:D2}]" : "    ";
            string stackHint = depth >= 0 ? $"Eval Stack Depth: {depth}" : "Stack data unavailable";
            
            GUI.contentColor = depth >= 0 ? (depth > 0 ? Color.cyan : Color.gray) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            GUILayout.Label(new GUIContent(stackStr, stackHint), GUILayout.Width(35));
            
            // Marker
            if (isDiffView)
            {
                if (marker == "-") GUI.contentColor = new Color(1f, 0.4f, 0.4f);
                else if (marker == "+") GUI.contentColor = Color.green;
                else GUI.contentColor = Color.white;
                
                GUILayout.Label(marker, GUILayout.Width(15));
            }

            // Interaction
            GUI.contentColor = Color.white;
            if (line == _selectedLineContent) GUI.color = Color.yellow;
            else if (marker == "-") GUI.color = new Color(1f, 0.6f, 0.6f);
            else if (marker == "+") GUI.color = new Color(0.6f, 1f, 0.6f);
            
            if (GUILayout.Button(line, "label", GUILayout.ExpandWidth(true)))
            {
                _selectedLineContent = line;
                _selectedStackDepth = depth;
                string opName = line.Split(' ')[0].Trim();
                _selectedOpCodeName = opName;
                _selectedOpCodeDesc = ModAPI.Harmony.TranspilerDebugger.ExplainOpCode(opName);
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        private void DrawSnapshotDiffContent(float x, float y, float width, float height)
        {
            GUILayout.BeginArea(new Rect(x, y, width, height));
            
            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Original (Vanilla Before)</b>", GUILayout.Width(width / 2f - 10));
            GUILayout.Label("<b>Patched (Runtime After)</b>", GUILayout.Width(width / 2f - 10));
            GUILayout.EndHorizontal();

            _scrollBefore = GUILayout.BeginScrollView(_scrollBefore, GUI.skin.box);

            if (_currentDiff != null)
            {
                foreach (var line in _currentDiff)
                {
                    GUILayout.BeginHorizontal();
                    
                    // Left Column
                    GUILayout.BeginVertical(GUILayout.Width(width / 2f - 25)); // Adjust for scrollbar
                    DrawInstructionLine(line.LeftIndex, line.LeftContent, _selectedSnapshot.BeforeStackDepths, true, line.LeftMarker);
                    GUILayout.EndVertical();

                    // Divider
                    GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

                    // Right Column
                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    DrawInstructionLine(line.RightIndex, line.RightContent, _selectedSnapshot.StackDepths, true, line.RightMarker);
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                 GUILayout.Label("Select a snapshot to view diff.");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSnapshotPatchedILContent(float x, float y, float width, float height)
        {
            GUI.Label(new Rect(x, y - 20, width, 20), "<b>Patched IL (Current Runtime Code Path)</b>", GUI.skin.label);
            GUILayout.BeginArea(new Rect(x, y, width, height));
            _scrollPatchedIL = GUILayout.BeginScrollView(_scrollPatchedIL, GUI.skin.box);
            for (int i = 0; i < _selectedSnapshot.Instructions.Count; i++)
                DrawInstructionLine(i, _selectedSnapshot.Instructions[i], _selectedSnapshot.StackDepths, false);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSnapshotVanillaILContent(float x, float y, float width, float height)
        {
            GUI.Label(new Rect(x, y - 20, width, 20), "<b>Vanilla IL (Before Patch)</b>", GUI.skin.label);
            GUILayout.BeginArea(new Rect(x, y, width, height));
            _scrollVanillaIL = GUILayout.BeginScrollView(_scrollVanillaIL, GUI.skin.box);
            for (int i = 0; i < _selectedSnapshot.BeforeInstructions.Count; i++)
                DrawInstructionLine(i, _selectedSnapshot.BeforeInstructions[i], _selectedSnapshot.BeforeStackDepths, false);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSnapshotSourceContent(float x, float y, float width, float height)
        {
            EnsureSourceLineStyle();
            GUILayout.BeginArea(new Rect(x, y, width, height));

            GUILayout.Label("<b>Source View</b>  <size=10><i>Flip between raw decompiled source and transpiler-aware guess views.</i></size>");

            GUILayout.BeginHorizontal(GUI.skin.box);
            if (DrawStateButton("Decompiled", _sourceSingleViewMode == SourceSingleViewMode.Decompiled, "Raw decompiled method source from vanilla Assembly-CSharp.", GUILayout.Width(105)))
            {
                _sourceSingleViewMode = SourceSingleViewMode.Decompiled;
                LogUiStep("Source mode changed", "Decompiled");
            }
            if (DrawStateButton("Regex Guess", _sourceSingleViewMode == SourceSingleViewMode.RegexRewritten, "Best-effort source rewrite using IL hunk mappings and regex anchors.", GUILayout.Width(105)))
            {
                _sourceSingleViewMode = SourceSingleViewMode.RegexRewritten;
                LogUiStep("Source mode changed", "Regex Guess");
            }
            if (DrawStateButton("Overlay Guess", _sourceSingleViewMode == SourceSingleViewMode.OverlayPreview, "Regex guess plus inline IL overlay comments for injection context.", GUILayout.Width(110)))
            {
                _sourceSingleViewMode = SourceSingleViewMode.OverlayPreview;
                LogUiStep("Source mode changed", "Overlay Guess");
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var baseSource = string.IsNullOrEmpty(_sourceText) ? "// Source is not loaded for this method yet." : _sourceText;
            var regexGuess = string.IsNullOrEmpty(_patchedSourceRewrittenText) ? baseSource : _patchedSourceRewrittenText;
            var overlayGuess = string.IsNullOrEmpty(_patchedSourceText) ? regexGuess : _patchedSourceText;

            string activeSource;
            switch (_sourceSingleViewMode)
            {
                case SourceSingleViewMode.RegexRewritten:
                    activeSource = regexGuess;
                    break;
                case SourceSingleViewMode.OverlayPreview:
                    activeSource = overlayGuess;
                    break;
                default:
                    activeSource = baseSource;
                    break;
            }

            if (_sourceSingleViewMode != SourceSingleViewMode.Decompiled)
            {
                GUILayout.Label("<size=10>Regex rewrites: " + _sourceRegexReplaceCount + " applied</size>");
            }

            _scrollSource = GUILayout.BeginScrollView(_scrollSource, GUI.skin.box);
            DrawSourceLineList(UIDebugSourcePreviewService.SplitLines(activeSource), _sourceSingleViewMode != SourceSingleViewMode.Decompiled);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSnapshotSourceDiffContent(float x, float y, float width, float height)
        {
            EnsureSourceLineStyle();
            var originalSource = string.IsNullOrEmpty(_sourceText) ? "// Source is not loaded for this method yet." : _sourceText;
            var patchedView = _showSourceOverlayComments ? _patchedSourceText : _patchedSourceRewrittenText;
            var patchedSource = string.IsNullOrEmpty(patchedView) ? "// Patched preview is not available yet." : patchedView;
            var alignedRows = UIDebugSourcePreviewService.BuildAlignedSourceDiffRows(originalSource, patchedSource);
            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Label("<b>Source Diff (Estimated)</b>  <size=10><i>Patched view uses IL diff + regex-assisted source rewrite.</i></size>");

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("<color=#7CFC00>+ inserted IL</color>  <color=#FF8A8A>- removed IL</color>  <color=#F6D365>~ regex rewritten source</color>", GUILayout.ExpandWidth(true));
            _showSourceOverlayComments = GUILayout.Toggle(_showSourceOverlayComments, "Show Overlay", GUILayout.Width(110));
            _showRegexDiagnostics = GUILayout.Toggle(_showRegexDiagnostics, "Regex Notes", GUILayout.Width(105));
            _showSourceInspectorPanel = GUILayout.Toggle(_showSourceInspectorPanel, "Line Inspector", GUILayout.Width(115));
            _syncSourceDiffScroll = GUILayout.Toggle(_syncSourceDiffScroll, "Sync Scroll", GUILayout.Width(100));
            GUILayout.EndHorizontal();
            if (_sourceRegexReplaceCount > 0 || _sourceRegexSummaries.Count > 0)
            {
                GUILayout.Label("<b>Regex Rewrites:</b> " + _sourceRegexReplaceCount + " applied");
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(width / 2f - 8f));
            GUILayout.Label("<b>Original Source</b>");
            _scrollSourceDiffLeft = GUILayout.BeginScrollView(_scrollSourceDiffLeft, GUI.skin.box);
            DrawSourceLineList(alignedRows.LeftLines, false);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(width / 2f - 8f));
            GUILayout.Label(_showSourceOverlayComments
                ? "<b>Patched Source (With Injected Overlay)</b>"
                : "<b>Patched Source (Rewritten View)</b>");
            if (_syncSourceDiffScroll)
            {
                _scrollSourceDiffRight.x = _scrollSourceDiffLeft.x;
                _scrollSourceDiffRight.y = _scrollSourceDiffLeft.y;
            }
            _scrollSourceDiffRight = GUILayout.BeginScrollView(_scrollSourceDiffRight, GUI.skin.box);
            DrawSourceLineList(alignedRows.RightLines, true);
            GUILayout.EndScrollView();
            if (_syncSourceDiffScroll)
            {
                _scrollSourceDiffLeft.x = _scrollSourceDiffRight.x;
                _scrollSourceDiffLeft.y = _scrollSourceDiffRight.y;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (_showSourceInspectorPanel)
            {
                DrawSourceInspectorFooter();
            }

            if (_showRegexDiagnostics && _sourceRegexSummaries.Count > 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                _scrollRegexDiagnostics = GUILayout.BeginScrollView(_scrollRegexDiagnostics, GUILayout.Height(90));
                for (var i = 0; i < _sourceRegexSummaries.Count; i++)
                {
                    GUILayout.Label("<size=10>" + UIDebugSourcePreviewService.EscapeRichText(_sourceRegexSummaries[i]) + "</size>");
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        private void EnsureSourceLineStyle()
        {
            if (_sourceLineStyle != null) return;

            _sourceLineStyle = new GUIStyle(GUI.skin.label);
            _sourceLineStyle.richText = true;
            _sourceLineStyle.wordWrap = false;
            _sourceLineStyle.alignment = TextAnchor.UpperLeft;
            _sourceLineStyle.fontSize = 12;
            _sourceLineStyle.padding = new RectOffset(4, 4, 0, 0);
        }

        private void DrawSourceTextBlock(string text, bool patched)
        {
            var normalized = (text ?? string.Empty).Replace("\r\n", "\n");
            var lines = normalized.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                GUILayout.Label(UIDebugSourcePreviewService.FormatSourceLineForDisplay(lines[i], patched), _sourceLineStyle);
            }
        }

        private void DrawSourceLineList(IList<string> lines, bool patched)
        {
            if (lines == null || lines.Count == 0)
            {
                GUILayout.Label(UIDebugSourcePreviewService.FormatSourceLineForDisplay(string.Empty, patched), _sourceLineStyle);
                return;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) line = string.Empty;
                if (line.Length == 0) line = " ";
                var display = UIDebugSourcePreviewService.FormatSourceLineForDisplay(line, patched);
                if (GUILayout.Button(display, _sourceLineStyle, GUILayout.ExpandWidth(true)))
                {
                    var isDouble = Event.current != null && Event.current.clickCount >= 2;
                    OnSourceLineClicked(i + 1, line, isDouble);
                }
            }
        }

        private void OnSourceLineClicked(int displayLineNumber, string line, bool isDoubleClick)
        {
            _selectedSourceLineNumber = displayLineNumber;
            _selectedSourceLineText = line ?? string.Empty;
            if (!isDoubleClick) return;
            InspectSelectedSourceLine();
        }

        private void InspectSelectedSourceLine()
        {
            var selected = (_selectedSourceLineText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(selected))
            {
                _sourceLineInspectText = "Selected line is empty.";
                return;
            }

            var frame = GetLatestRuntimeFrame();
            if (frame == null)
            {
                _sourceLineInspectText = "No live runtime frame available. Attach Live Debugger and run the method, then double-click again.";
                return;
            }

            var identifiers = Regex.Matches(selected, @"\b[A-Za-z_]\w*\b")
                .Cast<Match>()
                .Select(m => m.Value)
                .Distinct(StringComparer.Ordinal)
                .Where(id => !IsSourceKeyword(id))
                .Take(8)
                .ToList();

            var rows = new List<string>();
            for (var i = 0; i < identifiers.Count; i++)
            {
                var id = identifiers[i];
                if (TryResolveRuntimeValue(frame, id, out var valueText))
                {
                    rows.Add(id + " = " + valueText);
                }
            }

            var mappedIl = -1;
            if (_selectedMethod != null && _selectedSourceLineNumber > 0)
            {
                mappedIl = SourceCacheManager.MapSourceLineToILOffset(_selectedMethod, _selectedSourceLineNumber);
            }

            var sb = new StringBuilder();
            sb.AppendLine("Line " + _selectedSourceLineNumber + ": " + selected);
            if (mappedIl >= 0) sb.AppendLine("Nearest IL: IL_" + mappedIl.ToString("X4"));
            if (rows.Count == 0)
            {
                sb.AppendLine("No direct runtime values found for identifiers on this line.");
            }
            else
            {
                sb.AppendLine("Runtime values:");
                for (var i = 0; i < rows.Count; i++) sb.AppendLine("- " + rows[i]);
            }

            _sourceLineInspectText = sb.ToString().TrimEnd();
        }

        private ModAPI.Debugging.ExecutionFrame GetLatestRuntimeFrame()
        {
            lock ((object)LiveDebugger.RecentFrames)
            {
                if (LiveDebugger.RecentFrames.Count == 0) return null;
                return LiveDebugger.RecentFrames.Last();
            }
        }

        private static bool TryResolveRuntimeValue(ModAPI.Debugging.ExecutionFrame frame, string identifier, out string valueText)
        {
            valueText = string.Empty;
            if (frame == null || string.IsNullOrEmpty(identifier)) return false;

            object value;
            if (frame.Parameters != null && frame.Parameters.TryGetValue(identifier, out value))
            {
                valueText = SafeValue(value);
                return true;
            }
            if (frame.Fields != null && frame.Fields.TryGetValue(identifier, out value))
            {
                valueText = SafeValue(value);
                return true;
            }
            if (frame.Fields != null && frame.Fields.TryGetValue("m_" + identifier, out value))
            {
                valueText = SafeValue(value);
                return true;
            }
            if (frame.Statics != null && frame.Statics.TryGetValue(identifier, out value))
            {
                valueText = SafeValue(value);
                return true;
            }

            return false;
        }

        private static bool IsSourceKeyword(string token)
        {
            switch (token)
            {
                case "if":
                case "else":
                case "for":
                case "while":
                case "return":
                case "new":
                case "this":
                case "null":
                case "true":
                case "false":
                case "public":
                case "private":
                case "protected":
                case "internal":
                case "static":
                case "void":
                    return true;
                default:
                    return false;
            }
        }

        private void DrawSourceInspectorFooter()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Source Line Inspector</b>  <size=10>(double-click a source line)</size>");
            GUILayout.Label(UIDebugSourcePreviewService.EscapeRichText(_sourceLineInspectText));
            GUILayout.EndVertical();
        }

        private void DrawPatchProvenancePanel(float x, float y, float width, float height)
        {
            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Patch Provenance</b>");
            var owner = IsCoreSnapshot(_selectedSnapshot) ? "ModAPI Core" : "External Mod";
            GUILayout.Label("Owner: " + owner + " | Mod ID: " + (_selectedSnapshot.ModId ?? "Unknown"));
            GUILayout.Label("Method: " + BuildSnapshotMethodId(_selectedSnapshot));
            GUILayout.Label("Origin: " + (string.IsNullOrEmpty(_selectedSnapshot.PatchOrigin) ? "<unspecified>" : _selectedSnapshot.PatchOrigin));
            var patchEditCount = _selectedSnapshot.PatchEdits != null ? _selectedSnapshot.PatchEdits.Count : 0;
            GUILayout.Label("Recorded patch operations: " + patchEditCount + " | Warnings: " + _selectedSnapshot.WarningCount);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawStepTimelinePanel(float x, float y, float width, float height)
        {
            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Step Timeline (Source + IL + Values)</b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh Timeline", GUILayout.Width(130)))
            {
                _scrollTimeline = Vector2.zero;
            }
            GUILayout.EndHorizontal();

            if (_selectedMethod == null)
            {
                GUILayout.Label("No method is selected.");
                GUILayout.EndVertical();
                GUILayout.EndArea();
                return;
            }

            if (LiveDebugger.AttachedMethod != _selectedMethod)
            {
                GUILayout.Label("Attach live debugger to this method to capture an accurate timeline.");
                GUILayout.EndVertical();
                GUILayout.EndArea();
                return;
            }

            var frames = GetRecentFramesForDisplay();
            if (frames.Count == 0)
            {
                GUILayout.Label("No live frames captured. Attach live debugger and execute the selected method.");
                GUILayout.EndVertical();
                GUILayout.EndArea();
                return;
            }

            _scrollTimeline = GUILayout.BeginScrollView(_scrollTimeline, GUILayout.Height(height - 42));
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var il = frame.CurrentILIndex;
                var ilLabel = il >= 0 ? "IL_" + il.ToString("X4") : "IL_----";
                var sourceLine = (_selectedMethod != null && il >= 0) ? SourceCacheManager.MapILToSourceLine(_selectedMethod, il) : -1;
                var sourceLabel = sourceLine > 0 ? ("L" + sourceLine) : "L?";
                var values = BuildFrameValuePreview(frame);
                GUILayout.Label(frame.Timestamp.ToString("HH:mm:ss.fff") + " | " + ilLabel + " | " + sourceLabel + " | " + values);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private List<ModAPI.Debugging.ExecutionFrame> GetRecentFramesForDisplay()
        {
            lock ((object)LiveDebugger.RecentFrames)
            {
                return LiveDebugger.RecentFrames
                    .Where(f => f != null)
                    .Take(50)
                    .ToList();
            }
        }

        private static string BuildFrameValuePreview(ModAPI.Debugging.ExecutionFrame frame)
        {
            if (frame == null) return "<no frame>";
            var parts = new List<string>();
            if (frame.Parameters != null)
            {
                foreach (var kv in frame.Parameters.Take(2))
                {
                    parts.Add(kv.Key + "=" + SafeValue(kv.Value));
                }
            }
            if (frame.Fields != null)
            {
                foreach (var kv in frame.Fields.Take(2))
                {
                    parts.Add(kv.Key + "=" + SafeValue(kv.Value));
                }
            }
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "<no values>";
        }

        private void ExportSelectedSnapshotReport()
        {
            if (_selectedSnapshot == null)
            {
                _sourceStatus = "Export failed: no snapshot selected.";
                return;
            }

            try
            {
                var dir = Path.Combine(Path.Combine(Path.Combine("Mods", "ModAPI"), "Logs"), "InspectorReports");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var methodName = BuildSnapshotDisplayTitle(_selectedSnapshot);
                var safeMethod = SanitizeFileName(methodName);
                var safeMod = SanitizeFileName(_selectedSnapshot.ModId ?? "Unknown");
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var path = Path.Combine(dir, safeMod + "__" + safeMethod + "__" + stamp + ".txt");
                var report = BuildSnapshotReportText(_selectedSnapshot);
                File.WriteAllText(path, report);
                _lastExportPath = path;
                _sourceStatus = "Exported report to " + path;
            }
            catch (Exception ex)
            {
                _sourceStatus = "Export failed: " + ex.Message;
            }
        }

        private string BuildSnapshotReportText(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Snapshot Debug Report ===");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Owner: " + (IsCoreSnapshot(snap) ? "ModAPI Core" : "External Mod"));
            sb.AppendLine("Mod: " + (snap.ModId ?? "Unknown"));
            sb.AppendLine("Method: " + BuildSnapshotMethodId(snap));
            sb.AppendLine("Step: " + (snap.StepName ?? string.Empty));
            sb.AppendLine("Timestamp: " + snap.Timestamp.ToString("O"));
            sb.AppendLine("Diff: " + snap.DiffSummary + " (+" + snap.AddedCount + "/-" + snap.RemovedCount + ")");
            sb.AppendLine("Warnings: " + snap.WarningCount);
            sb.AppendLine("PatchOrigin: " + (string.IsNullOrEmpty(snap.PatchOrigin) ? "<unspecified>" : snap.PatchOrigin));
            sb.AppendLine();

            sb.AppendLine("=== Patch Operations ===");
            if (snap.PatchEdits == null || snap.PatchEdits.Count == 0)
            {
                sb.AppendLine("(none)");
            }
            else
            {
                for (var i = 0; i < snap.PatchEdits.Count; i++)
                {
                    var e = snap.PatchEdits[i];
                    sb.AppendLine("#" + (i + 1) + " " + (e.Kind ?? string.Empty) + " | confidence=" + (e.Confidence ?? string.Empty));
                    sb.AppendLine("  before: idx=" + e.StartIndexBefore + " removed=" + e.RemovedCount);
                    sb.AppendLine("  after : idx=" + e.StartIndexAfter + " added=" + e.AddedCount);
                    if (!string.IsNullOrEmpty(e.Note)) sb.AppendLine("  note  : " + e.Note);
                }
            }
            sb.AppendLine();

            sb.AppendLine("=== Source (Vanilla) ===");
            sb.AppendLine(_sourceText ?? string.Empty);
            sb.AppendLine();
            sb.AppendLine("=== Source (Rewritten) ===");
            sb.AppendLine(_patchedSourceRewrittenText ?? string.Empty);
            sb.AppendLine();
            sb.AppendLine("=== Source (Overlay) ===");
            sb.AppendLine(_patchedSourceText ?? string.Empty);
            sb.AppendLine();

            sb.AppendLine("=== IL (Before) ===");
            if (snap.BeforeInstructions != null)
            {
                for (var i = 0; i < snap.BeforeInstructions.Count; i++)
                {
                    sb.AppendLine(i.ToString("D4") + ": " + snap.BeforeInstructions[i]);
                }
            }
            sb.AppendLine();
            sb.AppendLine("=== IL (After) ===");
            if (snap.Instructions != null)
            {
                for (var i = 0; i < snap.Instructions.Count; i++)
                {
                    sb.AppendLine(i.ToString("D4") + ": " + snap.Instructions[i]);
                }
            }
            sb.AppendLine();

            sb.AppendLine("=== Recent Runtime Timeline ===");
            var frames = GetRecentFramesForDisplay();
            for (var i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                var il = f.CurrentILIndex;
                var sourceLine = (_selectedMethod != null && il >= 0) ? SourceCacheManager.MapILToSourceLine(_selectedMethod, il) : -1;
                sb.AppendLine(f.Timestamp.ToString("HH:mm:ss.fff") + " | IL_" + (il >= 0 ? il.ToString("X4") : "----") + " | L" + (sourceLine > 0 ? sourceLine.ToString() : "?") + " | " + BuildFrameValuePreview(f));
            }
            return sb.ToString();
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }

        private void DrawInstructionAnalysisPanel(float x, float y, float width, float height)
        {
            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Label("<b>Selected Instruction Analysis:</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            if (!string.IsNullOrEmpty(_selectedLineContent))
            {
                GUILayout.Label($"<color=cyan><b>OP:</b> {_selectedOpCodeName}</color> | <b>Stack Depth:</b> {(_selectedStackDepth >= 0 ? _selectedStackDepth.ToString() : "Unknown")}");
                GUILayout.Label($"<b>Instruction Guide:</b> {_selectedOpCodeDesc}");
                GUILayout.Label($"<b>Full IL:</b> {_selectedLineContent}");
                GUILayout.Space(5);
                GUILayout.Label("<size=10><i>Stack depth is the number of eval stack entries before this opcode executes.</i></size>");
            }
            else
            {
                GUILayout.Label("<i>Click an IL instruction line to inspect its stack behavior and semantics.</i>");
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void RefreshSceneHintsIfNeeded()
        {
            if (Time.realtimeSinceStartup < _nextSceneScanTime) return;
            _nextSceneScanTime = Time.realtimeSinceStartup + 2f;
            _activeSceneName = ModAPI.SceneUtil.GetCurrentSceneName() ?? string.Empty;

            _sceneTypeHints.Clear();
            var roots = UnityEngine.Object.FindObjectsOfType<Transform>();
            for (var i = 0; i < roots.Length; i++)
            {
                var t = roots[i];
                if (t == null || t.parent != null || !t.gameObject.activeInHierarchy) continue;
                _sceneTypeHints.Add(t.name);

                var comps = t.GetComponentsInChildren<Component>(true);
                for (var c = 0; c < comps.Length; c++)
                {
                    var comp = comps[c];
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        _sceneTypeHints.Add(typeName);
                        if (_sceneTypeHints.Count > 250) return;
                    }
                }
            }
        }

        private List<ModAPI.Harmony.TranspilerDebugger.Snapshot> GetVisibleSnapshots()
        {
            return UIDebugSnapshotService.GetVisibleSnapshots(
                ModAPI.Harmony.TranspilerDebugger.History,
                _showCorePatches,
                _showExternalPatches,
                _sceneFilteredOnly,
                _historyMethodSearch,
                _activeSceneName,
                _sceneTypeHints);
        }

        private void DrawRuntimeMethodMatches(string search)
        {
            if (_leftButtonStyle == null)
            {
                _leftButtonStyle = new GUIStyle(GUI.skin.button);
                _leftButtonStyle.alignment = TextAnchor.MiddleLeft;
                _leftButtonStyle.padding.left = 10;
                _leftButtonStyle.wordWrap = true;
                _leftButtonStyle.fontSize = 11;
                _leftButtonStyle.richText = true;
            }

            RefreshRuntimeMethodMatches(search);
            GUILayout.Space(6f);
            GUILayout.Label("<b>Game Method Matches (non-snapshot)</b>");
            GUILayout.Label("<size=10>Use these when no transpiler snapshot exists for the method you need.</size>");

            if (_runtimeMethodMatches.Count == 0)
            {
                GUILayout.Label("No game methods match the filter.");
                return;
            }

            for (var i = 0; i < _runtimeMethodMatches.Count; i++)
            {
                var method = _runtimeMethodMatches[i];
                if (method == null) continue;
                var declaring = method.DeclaringType != null ? method.DeclaringType.FullName : "<unknown>";
                var label = declaring + "." + method.Name;
                if (GUILayout.Button(new GUIContent(label, "Select this method for live attach / source inspection."), _leftButtonStyle, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
                {
                    _selectedMethod = method;
                    _selectedMethodId = BuildMethodDisplayName(method, null);
                    _selectedSnapshot = FindLatestSnapshotForMethod(method);
                    LogUiStep("Runtime method selected", _selectedMethodId);
                    if (_selectedSnapshot != null)
                    {
                        OnSnapshotSelected(_selectedSnapshot);
                    }
                    else
                    {
                        _sourceStatus = "Selected runtime method without transpiler snapshot: " + _selectedMethodId;
                    }
                }
            }
        }

        private void RefreshRuntimeMethodMatches(string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                _runtimeMethodMatches.Clear();
                _runtimeMethodSearchLast = string.Empty;
                return;
            }

            if (string.Equals(_runtimeMethodSearchLast, search, StringComparison.OrdinalIgnoreCase) &&
                Time.realtimeSinceStartup < _nextRuntimeMethodSearchTime)
            {
                return;
            }

            _runtimeMethodSearchLast = search;
            _nextRuntimeMethodSearchTime = Time.realtimeSinceStartup + 1.0f;
            _runtimeMethodMatches.Clear();
            _runtimeMethodMatches.AddRange(UIDebugSnapshotService.FindRuntimeMethodMatches(search, 40));
        }

        private static bool IsCoreSnapshot(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            return UIDebugSnapshotService.IsCoreSnapshot(snap);
        }

        private bool IsSnapshotSceneRelevant(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            return UIDebugSnapshotService.IsSnapshotSceneRelevant(snap, _activeSceneName, _sceneTypeHints);
        }

        private void RefreshActiveScriptsIfNeeded()
        {
            if (Time.realtimeSinceStartup < _nextActiveScriptScanTime) return;
            _nextActiveScriptScanTime = Time.realtimeSinceStartup + 2f;

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var activeInstances = 0;
            var scripts = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            for (var i = 0; i < scripts.Length; i++)
            {
                var script = scripts[i];
                if (script == null || !script.isActiveAndEnabled) continue;

                activeInstances++;
                var type = script.GetType();
                var typeName = type != null ? type.FullName : null;
                if (string.IsNullOrEmpty(typeName)) typeName = "<unknown script>";
                map[typeName] = map.TryGetValue(typeName, out var count) ? count + 1 : 1;
            }

            _activeScriptInstanceCount = activeInstances;
            _activeScripts.Clear();
            foreach (var kvp in map.OrderByDescending(k => k.Value).ThenBy(k => k.Key))
            {
                _activeScripts.Add(new ActiveScriptInfo
                {
                    TypeName = kvp.Key,
                    Count = kvp.Value
                });
            }
        }

        private static string BuildMethodDisplayName(MethodBase method, ModAPI.Harmony.TranspilerDebugger.Snapshot fallback)
        {
            return UIDebugSnapshotService.BuildMethodDisplayName(method, fallback);
        }

        private static string BuildSnapshotMethodId(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            return UIDebugSnapshotService.BuildSnapshotMethodId(snap);
        }

        private static string BuildSnapshotDisplayTitle(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            return UIDebugSnapshotService.BuildSnapshotDisplayTitle(snap);
        }

        private static bool IsSnapshotForMethod(ModAPI.Harmony.TranspilerDebugger.Snapshot snap, MethodBase method)
        {
            return UIDebugSnapshotService.IsSnapshotForMethod(snap, method);
        }

        private static string SafeValue(object value)
        {
            return UIDebugSnapshotService.SafeValue(value);
        }

        private ModAPI.Harmony.TranspilerDebugger.Snapshot FindLatestSnapshotForMethod(MethodBase method)
        {
            return UIDebugSnapshotService.FindLatestSnapshotForMethod(method, ModAPI.Harmony.TranspilerDebugger.History);
        }

        private string BuildPatchedSourcePreview(string vanillaSource, ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            var preview = UIDebugSourcePreviewService.BuildPatchedSourcePreview(
                vanillaSource,
                snap,
                _selectedMethod,
                _selectedMethodId,
                _currentDiff);

            _patchedSourceRewrittenText = preview.PatchedSourceRewrittenText;
            _sourceRegexReplaceCount = preview.RegexReplaceCount;
            _sourceRegexSummaries.Clear();
            _sourceRegexSummaries.AddRange(preview.RegexSummaries);
            return preview.PatchedSourceText;
        }



        private List<UIDebugDiffLine> _currentDiff;

        private void OnSnapshotSelected(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            if (snap == null) return;
            _selectedMethod = ResolveMethodFromSnapshot(snap);
            _selectedMethodId = BuildMethodDisplayName(_selectedMethod, snap);
            _sourceStatus = string.Empty;
            
            // Compute Diff
            _currentDiff = UIDebugSourcePreviewService.ComputeDiff(snap.BeforeInstructions, snap.Instructions);

            if (_preferSourceDiffDefault && !_isLiveMode)
            {
                _snapshotViewMode = SnapshotViewMode.SourceDiff;
            }

            if (_snapshotViewMode == SnapshotViewMode.Source || _snapshotViewMode == SnapshotViewMode.SourceDiff)
            {
                LoadSourceForSelectedSnapshot();
            }
        }

        private void LoadSourceForSelectedSnapshot(bool forceReload = false)
        {
            if (_selectedMethod == null)
            {
                _sourceText = "// Method resolution failed for this snapshot.";
                _patchedSourceText = _sourceText;
                _patchedSourceRewrittenText = _sourceText;
                _sourceStatus = _sourceText;
                return;
            }

            if (forceReload)
            {
                try
                {
                    var cachePath = SourceCacheManager.GetCachePath(_selectedMethod);
                    var mapPath = SourceCacheManager.GetMapPath(_selectedMethod);
                    if (File.Exists(cachePath)) File.Delete(cachePath);
                    if (File.Exists(mapPath)) File.Delete(mapPath);
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[UIDebugInspector] Failed to clear cache: " + ex.Message);
                }
            }

            _sourceText = SourceCacheManager.GetSource(_selectedMethod);
            _patchedSourceText = BuildPatchedSourcePreview(_sourceText, _selectedSnapshot);
            if (string.IsNullOrEmpty(_patchedSourceRewrittenText))
            {
                _patchedSourceRewrittenText = _sourceText;
            }
            if (!string.IsNullOrEmpty(SourceCacheManager.LastError))
            {
                _sourceStatus = SourceCacheManager.LastError;
            }
            else
            {
                _sourceStatus = "Loaded source for " + _selectedMethodId;
            }
        }

        private MethodBase ResolveMethodFromSnapshot(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            return UIDebugSnapshotService.ResolveMethodFromSnapshot(snap);
        }

        private void DrawLiveDebugger(float x, float y, float topOffset, float w, float h)
        {
            GUI.Box(new Rect(x, y + topOffset, w, h), "Live Runtime Inspector");
            var attached = LiveDebugger.AttachedMethod;
            var innerX = x + 10;
            var innerW = w - 20;
            var headerY = y + topOffset + 25;

            GUILayout.BeginArea(new Rect(innerX, headerY, innerW, 52));
            GUILayout.BeginHorizontal();
            if (attached != null)
            {
                GUILayout.Label("<b>Attached Method:</b> " + BuildMethodDisplayName(attached, null), GUILayout.Width(innerW * 0.66f));
                var trace = LiveDebugger.GetTrace(attached);
                if (trace != null)
                {
                    GUILayout.Label("Hits: " + trace.HitCount + " | Avg: " + trace.AverageDuration.ToString("F2") + "ms", GUILayout.Width(180));
                }
                if (GUILayout.Button("Detach", GUILayout.Width(90)))
                {
                    LiveDebugger.Detach();
                    attached = null;
                    LogUiStep("Live debugger detached");
                }
            }
            else
            {
                GUILayout.Label("<b>No method attached.</b> Select a snapshot or runtime method and attach.", GUILayout.Width(innerW * 0.66f));
                GUI.enabled = _selectedMethod != null;
                if (GUILayout.Button("Attach Selected Method", GUILayout.Width(190)))
                {
                    TryAttachSelectedMethod();
                    attached = LiveDebugger.AttachedMethod;
                    LogUiStep("Attach selected method clicked", _selectedMethodId);
                }
                GUI.enabled = true;
            }

            if (GUILayout.Button("Refresh Scripts", GUILayout.Width(130)))
            {
                _nextActiveScriptScanTime = 0f;
                RefreshActiveScriptsIfNeeded();
                LogUiStep("Active scripts refreshed");
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            var splitY = y + topOffset + 80;
            var splitH = h - 100;
            var varW = 350f;
            var rightW = innerW - varW - 10f;
            var codeH = splitH * 0.56f;
            var scriptH = splitH - codeH - 8f;
            var alertsH = Mathf.Max(130f, splitH * 0.32f);
            var framesH = splitH - alertsH - 8f;
            if (framesH < 150f) framesH = 150f;

            DrawLiveFramesPanel(new Rect(innerX, splitY, varW, framesH));
            DrawRuntimeAlertsPanel(new Rect(innerX, splitY + framesH + 8f, varW, alertsH));
            DrawLiveCodePanel(new Rect(innerX + varW + 10f, splitY, rightW, codeH), attached);
            DrawActiveScriptsPanel(new Rect(innerX + varW + 10f, splitY + codeH + 8f, rightW, scriptH));
        }

        private void DrawLiveFramesPanel(Rect rect)
        {
            GUI.Box(rect, "Live Call Feed");
            GUILayout.BeginArea(new Rect(rect.x + 5, rect.y + 25, rect.width - 10, rect.height - 30));

            List<ExecutionFrame> frames;
            lock ((object)LiveDebugger.RecentFrames)
            {
                frames = LiveDebugger.RecentFrames.ToList();
            }

            GUILayout.Label("Buffered Frames: " + frames.Count + "/" + LiveDebugger.MaxFrames);
            if (frames.Count == 0)
            {
                GUILayout.Label("Waiting for execution...");
                GUILayout.EndArea();
                return;
            }

            var latest = frames[frames.Count - 1];
            GUILayout.Label("<b>Last Call:</b> " + latest.Timestamp.ToString("HH:mm:ss.fff") + " (" + latest.DurationMs.ToString("F2") + "ms)");
            GUILayout.Label("<b>Current IL Index:</b> " + latest.CurrentILIndex);
            GUILayout.Space(6);

            _scrollLive = GUILayout.BeginScrollView(_scrollLive);
            GUILayout.Label("<b>Arguments</b>");
            if (latest.Parameters != null && latest.Parameters.Count > 0)
            {
                foreach (var kvp in latest.Parameters)
                {
                    GUILayout.Label("  " + kvp.Key + " = " + SafeValue(kvp.Value));
                }
            }
            else
            {
                GUILayout.Label("  (none)");
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>Fields (Instance)</b>");
            if (latest.Fields != null && latest.Fields.Count > 0)
            {
                foreach (var kvp in latest.Fields)
                {
                    GUILayout.Label("  " + kvp.Key + " = " + SafeValue(kvp.Value));
                }
            }
            else
            {
                GUILayout.Label("  (none)");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRuntimeAlertsPanel(Rect rect)
        {
            GUI.Box(rect, "Runtime Alerts (Warnings/Errors)");
            GUILayout.BeginArea(new Rect(rect.x + 5, rect.y + 25, rect.width - 10, rect.height - 30));

            var entries = MMLog.GetRecentEntries(MMLog.LogLevel.Warning, 40);
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("No warnings or errors captured in current session.");
                GUILayout.EndArea();
                return;
            }

            var warningCount = 0;
            var errorCount = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                if (e.Level == MMLog.LogLevel.Warning) warningCount++;
                if (e.Level >= MMLog.LogLevel.Error) errorCount++;
            }
            GUILayout.Label("Recent: " + warningCount + " warning(s), " + errorCount + " error(s)/fatal");

            _scrollAlerts = GUILayout.BeginScrollView(_scrollAlerts, GUI.skin.box);
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                var color = e.Level >= MMLog.LogLevel.Error ? "#FF8A8A" : "#F6D365";
                var src = string.IsNullOrEmpty(e.Source) ? "Unknown" : e.Source;
                var msg = string.IsNullOrEmpty(e.Message) ? "<empty>" : e.Message;
                if (msg.Length > 240) msg = msg.Substring(0, 240) + "...";
                GUILayout.Label("<color=" + color + ">[" + e.Timestamp.ToString("HH:mm:ss.fff") + "] [" + e.Level.ToString().ToUpper() + "] [" + UIDebugSourcePreviewService.EscapeRichText(src) + "] " + UIDebugSourcePreviewService.EscapeRichText(msg) + "</color>");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawLiveCodePanel(Rect rect, MethodBase attached)
        {
            GUI.Box(rect, "Live Execution Trace");
            GUILayout.BeginArea(new Rect(rect.x + 5, rect.y + 25, rect.width - 10, rect.height - 30));
            var previewMethod = attached ?? _selectedMethod;
            GUILayout.Label("<i>Source is decompiled on demand for the selected/attached method.</i>");

            if (previewMethod == null)
            {
                GUILayout.Label("Select a method from the navigator to preview source. Attach when you want live frames.");
                GUILayout.EndArea();
                return;
            }

            if (attached == null)
            {
                GUILayout.Label("<size=10><color=#F6D365>Preview mode:</color> no live debugger attached yet.</size>");
            }

            var matchedSnapshot = _selectedSnapshot != null && IsSnapshotForMethod(_selectedSnapshot, previewMethod)
                ? _selectedSnapshot
                : FindLatestSnapshotForMethod(previewMethod);

            GUILayout.BeginHorizontal();
            if (matchedSnapshot != null)
            {
                GUILayout.Label("<b>Snapshot:</b> " + BuildSnapshotDisplayTitle(matchedSnapshot), GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.Label("<b>Snapshot:</b> None for attached method", GUILayout.ExpandWidth(true));
            }

            if (GUILayout.Button("Reload Live Source", GUILayout.Width(150)))
            {
                EnsureLiveSourceLoaded(previewMethod, true);
                LogUiStep("Live source reloaded", BuildMethodDisplayName(previewMethod, null));
            }

            if (matchedSnapshot != null && GUILayout.Button("View Vanilla Source", GUILayout.Width(150)))
            {
                _selectedSnapshot = matchedSnapshot;
                OnSnapshotSelected(matchedSnapshot);
                _snapshotViewMode = SnapshotViewMode.SourceDiff;
                LoadSourceForSelectedSnapshot();
                _isLiveMode = false;
            }
            GUILayout.EndHorizontal();

            EnsureSourceLineStyle();
            EnsureLiveSourceLoaded(previewMethod, false);

            var currentIlIndex = -1;
            if (attached != null)
            {
                lock ((object)LiveDebugger.RecentFrames)
                {
                    if (LiveDebugger.RecentFrames.Count > 0)
                    {
                        currentIlIndex = LiveDebugger.RecentFrames.Last().CurrentILIndex;
                    }
                }
            }

            var currentSourceLine = currentIlIndex >= 0 ? SourceCacheManager.MapILToSourceLine(previewMethod, currentIlIndex) : -1;
            AutoFollowLiveSourceLine(currentSourceLine);
            if (!string.IsNullOrEmpty(_liveSourceStatus))
            {
                GUILayout.Label("<size=10>" + UIDebugSourcePreviewService.EscapeRichText(_liveSourceStatus) + "</size>");
            }
            if (currentIlIndex >= 0)
            {
                var sourceText = currentSourceLine > 0 ? currentSourceLine.ToString() : "unknown";
                GUILayout.Label("<size=10><b>Live Position:</b> IL_" + currentIlIndex.ToString("X4") + " -> Source line " + sourceText + "</size>");
            }
            else
            {
                GUILayout.Label("<size=10><b>Live Position:</b> not available until attached frames are captured.</size>");
            }

            _followLiveSourceLine = GUILayout.Toggle(_followLiveSourceLine, "Follow live line");
            GUILayout.Label("<size=10><b>Markers:</b> <color=#7CFC00>&gt;</color> latest live line, <color=#F6D365>*</color> selected line. If it stays fixed, recent frames are reporting the same IL or no new frames.</size>");

            var sourceLines = UIDebugSourcePreviewService.SplitLines(string.IsNullOrEmpty(_liveSourceText) ? "// Source unavailable." : _liveSourceText);
            _scrollLiveSource = GUILayout.BeginScrollView(_scrollLiveSource, GUI.skin.box, GUILayout.Height(Mathf.Max(140f, rect.height * 0.60f)));
            DrawLiveSourceLineList(sourceLines, currentSourceLine);
            GUILayout.EndScrollView();

            if (matchedSnapshot != null)
            {
                GUILayout.Label("<b>Patched IL Snapshot</b>");
                _scrollAfter = GUILayout.BeginScrollView(_scrollAfter, GUI.skin.box);
                for (var i = 0; i < matchedSnapshot.Instructions.Count; i++)
                {
                    DrawInstructionLine(i, matchedSnapshot.Instructions[i], matchedSnapshot.StackDepths, false);
                }
                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("No transpiler snapshot was recorded for this method. Live source and runtime values are still available.");
            }

            GUILayout.EndArea();
        }

        private void EnsureLiveSourceLoaded(MethodBase attached, bool forceReload)
        {
            if (attached == null)
            {
                _liveSourceMethodId = string.Empty;
                _liveSourceText = string.Empty;
                _liveSourceStatus = "No attached method.";
                return;
            }

            var methodId = BuildMethodDisplayName(attached, null);
            if (!forceReload &&
                string.Equals(_liveSourceMethodId, methodId, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(_liveSourceText))
            {
                return;
            }

            _liveSourceMethodId = methodId;
            _liveSourceText = SourceCacheManager.GetSource(attached);
            _liveSourceStatus = string.IsNullOrEmpty(_liveSourceText)
                ? "Live source unavailable for " + methodId
                : "Loaded live source for " + methodId;
        }

        private void DrawLiveSourceLineList(IList<string> lines, int currentSourceLine)
        {
            if (lines == null || lines.Count == 0)
            {
                GUILayout.Label("<color=#B0B0B0>0001</color> <color=#D8D8D8>// Source unavailable.</color>", _sourceLineStyle);
                return;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i] ?? string.Empty;
                var displayLine = line.Length == 0 ? " " : line;
                var lineNumber = i + 1;
                var linePrefix = "<color=#8A8A8A>" + lineNumber.ToString("D4") + "</color> ";

                if (lineNumber == currentSourceLine)
                {
                    linePrefix = "<color=#7CFC00>&gt;</color> " + linePrefix;
                }
                else if (lineNumber == _selectedSourceLineNumber)
                {
                    linePrefix = "<color=#F6D365>*</color> " + linePrefix;
                }
                else
                {
                    linePrefix = "  " + linePrefix;
                }

                var display = linePrefix + UIDebugSourcePreviewService.FormatSourceLineForDisplay(displayLine, false);
                if (GUILayout.Button(display, _sourceLineStyle, GUILayout.ExpandWidth(true)))
                {
                    var isDouble = Event.current != null && Event.current.clickCount >= 2;
                    OnSourceLineClicked(lineNumber, line, isDouble);
                }
            }
        }

        private void HandleSearchCommitLogging(
            string controlName,
            ref bool hadFocus,
            string currentText,
            ref string lastLoggedValue,
            string action)
        {
            var focusedControl = GUI.GetNameOfFocusedControl();
            var isFocused = string.Equals(focusedControl, controlName, StringComparison.Ordinal);
            var e = Event.current;
            if (isFocused &&
                e != null &&
                e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                GUI.FocusControl(string.Empty);
                isFocused = false;
            }

            if (isFocused)
            {
                hadFocus = true;
                return;
            }

            if (!hadFocus) return;

            hadFocus = false;
            var normalized = (currentText ?? string.Empty).Trim();
            if (string.Equals(lastLoggedValue, normalized, StringComparison.OrdinalIgnoreCase)) return;
            lastLoggedValue = normalized;
            LogUiStep(action, string.IsNullOrEmpty(normalized) ? "<empty>" : normalized);
        }

        private void AutoFollowLiveSourceLine(int currentSourceLine)
        {
            if (!_followLiveSourceLine || currentSourceLine <= 0)
            {
                if (currentSourceLine <= 0)
                {
                    _lastAutoFollowSourceLine = -1;
                }
                return;
            }

            if (currentSourceLine == _lastAutoFollowSourceLine) return;
            _lastAutoFollowSourceLine = currentSourceLine;
            const float lineHeight = 20f;
            var targetY = Mathf.Max(0f, (currentSourceLine - 4) * lineHeight);
            _scrollLiveSource.y = targetY;
        }

        private void DrawActiveScriptsPanel(Rect rect)
        {
            GUI.Box(rect, "Active Scripts In Scene");
            GUILayout.BeginArea(new Rect(rect.x + 5, rect.y + 25, rect.width - 10, rect.height - 30));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Count: " + _activeScriptInstanceCount + " instances / " + _activeScripts.Count + " script types", GUILayout.ExpandWidth(true));
            GUI.SetNextControlName("ActiveScriptSearch");
            _activeScriptSearch = GUILayout.TextField(_activeScriptSearch, GUILayout.Width(180));
            HandleSearchCommitLogging("ActiveScriptSearch", ref _activeScriptSearchHadFocus, _activeScriptSearch, ref _lastLoggedActiveScriptSearch, "Active script search submitted");
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                _activeScriptSearch = string.Empty;
                _lastLoggedActiveScriptSearch = string.Empty;
                LogUiStep("Active script search cleared");
            }
            GUILayout.EndHorizontal();

            _scrollActiveScripts = GUILayout.BeginScrollView(_scrollActiveScripts);
            var hasSearch = !string.IsNullOrEmpty(_activeScriptSearch);
            var displayed = 0;
            for (var i = 0; i < _activeScripts.Count; i++)
            {
                var entry = _activeScripts[i];
                if (entry == null || string.IsNullOrEmpty(entry.TypeName)) continue;
                if (hasSearch && entry.TypeName.IndexOf(_activeScriptSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;

                displayed++;
                GUILayout.Label(entry.TypeName + "  (x" + entry.Count + ")");
            }

            if (displayed == 0)
            {
                GUILayout.Label(hasSearch ? "No scripts match the filter." : "No active scripts found for the current scene.");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void TryAttachSelectedSnapshot()
        {
            if (_selectedSnapshot == null)
            {
                return;
            }

            try
            {
                if (_selectedMethod == null || !IsSnapshotForMethod(_selectedSnapshot, _selectedMethod))
                {
                    _selectedMethod = ResolveMethodFromSnapshot(_selectedSnapshot);
                }

                if (_selectedMethod != null)
                {
                    LiveDebugger.Attach(_selectedMethod);
                    _selectedMethodId = BuildMethodDisplayName(_selectedMethod, _selectedSnapshot);
                    LogUiStep("Live debugger attached", _selectedMethodId);
                }
                else
                {
                    MMLog.WriteError("[UIDebugInspector] Attach failed: method could not be resolved from snapshot.");
                    LogUiStep("Attach failed", "Snapshot method unresolved");
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[UIDebugInspector] Attach failed: " + ex.Message);
            }
        }

        private void TryAttachSelectedMethod()
        {
            try
            {
                if (_selectedMethod == null && _selectedSnapshot != null)
                {
                    _selectedMethod = ResolveMethodFromSnapshot(_selectedSnapshot);
                }

                if (_selectedMethod != null)
                {
                    LiveDebugger.Attach(_selectedMethod);
                    _selectedMethodId = BuildMethodDisplayName(_selectedMethod, _selectedSnapshot);
                    LogUiStep("Live debugger attached", _selectedMethodId);
                    return;
                }

                MMLog.WriteError("[UIDebugInspector] Attach failed: no selected method.");
                LogUiStep("Attach failed", "No selected method");
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[UIDebugInspector] Attach failed: " + ex.Message);
                LogUiStep("Attach exception", ex.Message);
            }
        }

        private Vector2 _scrollBefore;
        private Vector2 _scrollAfter;
        private string _selectedLineContent;
        private string _selectedOpCodeName;
        private string _selectedOpCodeDesc;
        private int _selectedStackDepth;

        private void DrawInstructionLine(int index, string line, List<int> stacks, bool isBefore)
        {
            // Filter
             if (!string.IsNullOrEmpty(_ilSearchPattern) && line.IndexOf(_ilSearchPattern, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            GUILayout.BeginHorizontal();
            
            // Index
            GUI.contentColor = Color.gray;
            GUILayout.Label($"{index:D3}", GUILayout.Width(30));
            
            // Stack Depth
            int depth = (stacks != null && index < stacks.Count) ? stacks[index] : -1;
            string stackStr = depth >= 0 ? $"[{depth:D2}]" : "[N/A]";
            string stackHint = depth >= 0 ? $"Eval Stack Depth: {depth}" : "Stack data unavailable (Analysis failed or skipped)";
            
            GUI.contentColor = depth >= 0 ? (depth > 0 ? Color.cyan : Color.gray) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            GUILayout.Label(new GUIContent(stackStr, stackHint), GUILayout.Width(35));
            
            // Interaction
            GUI.contentColor = Color.white;
            if (line == _selectedLineContent) GUI.color = Color.yellow;
            
            if (GUILayout.Button(line, "label", GUILayout.ExpandWidth(true)))
            {
                _selectedLineContent = line;
                _selectedStackDepth = depth;
                string opName = line.Split(' ')[0].Trim();
                _selectedOpCodeName = opName;
                _selectedOpCodeDesc = ModAPI.Harmony.TranspilerDebugger.ExplainOpCode(opName);
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        private ModAPI.Harmony.TranspilerDebugger.Snapshot GetPreviousSnapshot(ModAPI.Harmony.TranspilerDebugger.Snapshot current)
        {
            var history = ModAPI.Harmony.TranspilerDebugger.History;
            int idx = history.IndexOf(current);
            if (idx > 0) return history[idx - 1];
            return null;
        }

        private GUIStyle _leftButtonStyle;
        private void DrawSnapshotButton(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            if (_leftButtonStyle == null)
            {
                _leftButtonStyle = new GUIStyle(GUI.skin.button);
                _leftButtonStyle.alignment = TextAnchor.MiddleLeft;
                _leftButtonStyle.padding.left = 10;
                _leftButtonStyle.wordWrap = true;
                _leftButtonStyle.fontSize = 11;
                _leftButtonStyle.richText = true;
            }

            var status = snap.WarningCount > 0
                ? $"Warn: <color=orange>{snap.WarningCount}</color>"
                : "<color=lime>OK</color>";
            var ownerTag = IsCoreSnapshot(snap) ? "<color=#8ED6FF>[CORE]</color>" : "<color=#9CFF9C>[MOD]</color>";
            var methodName = BuildSnapshotDisplayTitle(snap);
            var diffSummary = "Delta " + snap.DiffSummary + " (+" + snap.AddedCount + "/-" + snap.RemovedCount + ")";
            var summary = ownerTag + " " + methodName + "\n" + snap.Timestamp.ToString("HH:mm:ss") + " | " + status + " | " + diffSummary;
            var tooltip = "Open " + methodName + " details.\nMethod: " + BuildSnapshotMethodId(snap);
            
            if (_selectedSnapshot == snap) GUI.backgroundColor = Color.cyan;
            
            if (GUILayout.Button(new GUIContent(summary, tooltip), _leftButtonStyle, GUILayout.Height(54), GUILayout.ExpandWidth(true)))
            {
                _selectedSnapshot = snap;
                _detailsScrollPos = Vector2.zero;
                OnSnapshotSelected(snap);
                LogUiStep("Snapshot selected", BuildSnapshotDisplayTitle(snap) + " | Mod=" + (snap.ModId ?? "Unknown"));
            }
            
            GUI.backgroundColor = Color.white;
        }
        
        private Vector2 _detailsScrollPos;
        private string _currentTooltip;

        private void DrawOverlay(GameObject go)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"GO: {go.name}");
            sb.AppendLine($"Layer: {LayerMask.LayerToName(go.layer)}");
            sb.AppendLine($"Pos: {go.transform.position} | Local: {go.transform.localPosition}");
            sb.AppendLine($"Scale: {go.transform.localScale}");
            
            var widget = go.GetComponent<UIWidget>();
            if (widget != null)
            {
                sb.AppendLine($"Widget: {widget.GetType().Name}");
                sb.AppendLine($"Depth: {widget.depth}");
                sb.AppendLine($"Alpha: {widget.alpha}");
                sb.AppendLine($"Color: {widget.color}");
                sb.AppendLine($"Dims: {widget.width}x{widget.height}");
                sb.AppendLine($"Pivot: {widget.pivot}");
            }

            var label = go.GetComponent<UILabel>();
            if (label != null)
            {
                sb.AppendLine($"-- LABEL --");
                sb.AppendLine($"Text: '{label.text}'");
                sb.AppendLine($"FontSize: {label.fontSize}");
                sb.AppendLine($"FontType: {(label.bitmapFont != null ? "Bitmap " + label.bitmapFont.name : "TTF " + (label.trueTypeFont?.name ?? "null"))}");
                sb.AppendLine($"Overflow: {label.overflowMethod}");
                sb.AppendLine($"Effect: {label.effectStyle}");
            }

            var panel = NGUITools.FindInParents<UIPanel>(go);
            if (panel != null)
            {
                sb.AppendLine($"-- PANEL --");
                sb.AppendLine($"Panel: {panel.name} Depth: {panel.depth}");
                sb.AppendLine($"RenderQueue: {panel.startingRenderQueue}");
            }

            // Draw Box
            Vector2 mouse = Event.current.mousePosition;
            float w = 300, h = 300;
            float x = mouse.x + 15;
            float y = mouse.y + 15;
            if (x + w > Screen.width) x = mouse.x - w - 15;
            if (y + h > Screen.height) y = mouse.y - h - 15;

            GUI.Box(new Rect(x, y, w, h), "UI Inspector (F10 to Toggle)");
            
            if (go == null)
            {
                // NO HOVER: Show Mod List Summary
                var plugins = ModAPI.Core.PluginManager.getInstance().GetPlugins();
                string modSummary = $"<b>Active Mods ({plugins.Count()}):</b>\n";
                foreach (var p in plugins)
                {
                    ModAPI.Core.ModEntry entry;
                    string mId = ModAPI.Core.ModRegistry.TryGetModByAssembly(p.GetType().Assembly, out entry) ? entry.Id : p.GetType().Name;
                    modSummary += $"• {mId}\n";
                }
                GUI.Label(new Rect(x + 5, y + 25, w - 10, h - 30), modSummary);
            }
            else
            {
                GUI.Label(new Rect(x + 5, y + 25, w - 10, h - 30), sb.ToString());
            }
        }

        private void DumpWidgetInfo(GameObject go)
        {
            MMLog.WriteInfo("--------------------------------------------------");
            MMLog.WriteInfo($"[UIDebug] Inspecting '{go.name}'");
            MMLog.WriteInfo($"  Path: {GetPath(go.transform)}");
            
            var widget = go.GetComponent<UIWidget>();
            if (widget != null)
            {
                MMLog.WriteInfo($"  [UIWidget] Type={widget.GetType().Name} Depth={widget.depth} Alpha={widget.alpha} Color={widget.color}");
                MMLog.WriteInfo($"  Dimensions={widget.width}x{widget.height} Pivot={widget.pivot}");
            }

            var label = go.GetComponent<UILabel>();
            if (label != null)
            {
                string fName = label.bitmapFont != null ? $"Bitmap({label.bitmapFont.name})" : $"TTF({label.trueTypeFont?.name})";
                MMLog.WriteInfo($"  [UILabel] Text='{label.text}' Size={label.fontSize} Font={fName}");
                MMLog.WriteInfo($"  Overflow={label.overflowMethod} MultiLine={label.multiLine}");
                MMLog.WriteInfo($"  PrintedSize={label.printedSize}");
            }
            
            var sprite = go.GetComponent<UISprite>();
            if (sprite != null)
            {
                MMLog.WriteInfo($"  [UISprite] SpriteName={sprite.spriteName} Atlas={sprite.atlas?.name}");
            }

            MMLog.WriteInfo($"  Global Scale: {go.transform.lossyScale}");
            MMLog.WriteInfo("--------------------------------------------------");
        }

        private void DrawModListOverlay()
        {
            float w = 600, h = 500;
            float x = (Screen.width - w) / 2;
            float y = (Screen.height - h) / 2;
            
            GUI.Box(new Rect(x, y, w, h), "Active Mod Plugins");
            
            GUILayout.BeginArea(new Rect(x + 10, y + 25, w - 20, h - 40));
            
            if (GUILayout.Button("Close / Back to Inspector", GUILayout.Height(30))) _showModList = false;
            
            GUILayout.Space(10);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            
            var plugins = ModAPI.Core.PluginManager.getInstance().GetPlugins().ToList();
            foreach (var plugin in plugins)
            {
                ModAPI.Core.ModEntry entry;
                string mId = ModAPI.Core.ModRegistry.TryGetModByAssembly(plugin.GetType().Assembly, out entry) ? entry.Id : plugin.GetType().Name;

                GUILayout.BeginVertical("box");
                GUILayout.Label($"<b>{mId}</b>", "largeLabel");
                GUILayout.Label($"Type: {plugin.GetType().FullName}");
                
                GUILayout.EndVertical();
                GUILayout.Space(5);
            }
            
            if (plugins.Count == 0) GUILayout.Label("No plugins registered via PluginManager.");
            
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string GetPath(Transform t)
        {
            return t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
        }
    }
}

