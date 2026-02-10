using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Debugging;
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
            DrawSourceLineList(SplitLines(activeSource), _sourceSingleViewMode != SourceSingleViewMode.Decompiled);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSnapshotSourceDiffContent(float x, float y, float width, float height)
        {
            EnsureSourceLineStyle();
            var originalSource = string.IsNullOrEmpty(_sourceText) ? "// Source is not loaded for this method yet." : _sourceText;
            var patchedView = _showSourceOverlayComments ? _patchedSourceText : _patchedSourceRewrittenText;
            var patchedSource = string.IsNullOrEmpty(patchedView) ? "// Patched preview is not available yet." : patchedView;
            var alignedRows = BuildAlignedSourceDiffRows(originalSource, patchedSource);
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
                    GUILayout.Label("<size=10>" + EscapeRichText(_sourceRegexSummaries[i]) + "</size>");
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
                GUILayout.Label(FormatSourceLineForDisplay(lines[i], patched), _sourceLineStyle);
            }
        }

        private void DrawSourceLineList(IList<string> lines, bool patched)
        {
            if (lines == null || lines.Count == 0)
            {
                GUILayout.Label(FormatSourceLineForDisplay(string.Empty, patched), _sourceLineStyle);
                return;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) line = string.Empty;
                if (line.Length == 0) line = " ";
                var display = FormatSourceLineForDisplay(line, patched);
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
            GUILayout.Label(EscapeRichText(_sourceLineInspectText));
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

        private SourceDiffAlignedRows BuildAlignedSourceDiffRows(string originalSource, string patchedSource)
        {
            var left = SplitLines(originalSource);
            var rightAll = SplitLines(patchedSource);

            var rightReal = new List<string>(rightAll.Count);
            var rightSynthetic = new Dictionary<int, List<string>>();
            var seenReal = 0;

            for (var i = 0; i < rightAll.Count; i++)
            {
                var line = rightAll[i] ?? string.Empty;
                if (IsSyntheticPatchedOverlayLine(line))
                {
                    if (!rightSynthetic.TryGetValue(seenReal, out var list))
                    {
                        list = new List<string>();
                        rightSynthetic[seenReal] = list;
                    }
                    list.Add(line);
                    continue;
                }

                rightReal.Add(line);
                seenReal++;
            }

            var alignedLeft = new List<string>(Math.Max(left.Count, rightReal.Count));
            var alignedRight = new List<string>(Math.Max(left.Count, rightReal.Count));

            // LCS align original source against non-synthetic patched source lines.
            var m = left.Count;
            var n = rightReal.Count;
            var lcs = new int[m + 1, n + 1];

            for (var i = 1; i <= m; i++)
            {
                for (var j = 1; j <= n; j++)
                {
                    if (string.Equals(left[i - 1], rightReal[j - 1], StringComparison.Ordinal))
                    {
                        lcs[i, j] = lcs[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
                    }
                }
            }

            var rowStack = new Stack<SourceAlignRow>();
            var x = m;
            var y = n;

            while (x > 0 && y > 0)
            {
                if (string.Equals(left[x - 1], rightReal[y - 1], StringComparison.Ordinal))
                {
                    rowStack.Push(new SourceAlignRow { Left = left[x - 1], Right = rightReal[y - 1] });
                    x--;
                    y--;
                }
                else if (lcs[x - 1, y] >= lcs[x, y - 1])
                {
                    rowStack.Push(new SourceAlignRow { Left = left[x - 1], Right = string.Empty });
                    x--;
                }
                else
                {
                    rowStack.Push(new SourceAlignRow { Left = string.Empty, Right = rightReal[y - 1] });
                    y--;
                }
            }

            while (x > 0)
            {
                rowStack.Push(new SourceAlignRow { Left = left[x - 1], Right = string.Empty });
                x--;
            }

            while (y > 0)
            {
                rowStack.Push(new SourceAlignRow { Left = string.Empty, Right = rightReal[y - 1] });
                y--;
            }

            var realIndex = 0;
            var injectedSynthetic = new HashSet<int>();
            while (rowStack.Count > 0)
            {
                if (!injectedSynthetic.Contains(realIndex) && rightSynthetic.TryGetValue(realIndex, out var syntheticBefore))
                {
                    for (var s = 0; s < syntheticBefore.Count; s++)
                    {
                        AddSyntheticOverlayRow(syntheticBefore[s], alignedLeft, alignedRight);
                    }
                    injectedSynthetic.Add(realIndex);
                }

                var row = rowStack.Pop();
                alignedLeft.Add(row.Left ?? string.Empty);
                alignedRight.Add(row.Right ?? string.Empty);

                if (!string.IsNullOrEmpty(row.Right))
                {
                    realIndex++;
                }
            }

            if (!injectedSynthetic.Contains(realIndex) && rightSynthetic.TryGetValue(realIndex, out var trailingSynthetic))
            {
                for (var s = 0; s < trailingSynthetic.Count; s++)
                {
                    AddSyntheticOverlayRow(trailingSynthetic[s], alignedLeft, alignedRight);
                }
            }

            return new SourceDiffAlignedRows { LeftLines = alignedLeft, RightLines = alignedRight };
        }

        private static void AddSyntheticOverlayRow(string syntheticLine, List<string> alignedLeft, List<string> alignedRight)
        {
            var line = syntheticLine ?? string.Empty;
            var trimmed = line.TrimStart();

            // Keep IL delta lines spatially mirrored:
            // removed lines belong to left pane, added lines belong to right pane.
            if (trimmed.StartsWith("//   -", StringComparison.Ordinal))
            {
                alignedLeft.Add(line);
                alignedRight.Add(string.Empty);
                return;
            }

            if (trimmed.StartsWith("//   +", StringComparison.Ordinal))
            {
                alignedLeft.Add(string.Empty);
                alignedRight.Add(line);
                return;
            }

            alignedLeft.Add(string.Empty);
            alignedRight.Add(line);
        }

        private static List<string> SplitLines(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Split('\n').ToList();
        }

        private static bool IsSyntheticPatchedOverlayLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;

            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("//", StringComparison.Ordinal)) return false;

            return
                line.IndexOf("TRANSPILE INJECTION PREVIEW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.StartsWith("// This shows likely runtime-injected operations", StringComparison.Ordinal) ||
                trimmed.StartsWith("// [Regex Rewrite]", StringComparison.Ordinal) ||
                trimmed.StartsWith("// Hunk", StringComparison.Ordinal) ||
                trimmed.StartsWith("//   +", StringComparison.Ordinal) ||
                trimmed.StartsWith("//   -", StringComparison.Ordinal) ||
                trimmed.StartsWith("// ... ", StringComparison.Ordinal) ||
                line.IndexOf("END INJECTION PREVIEW", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class SourceDiffAlignedRows
        {
            public List<string> LeftLines = new List<string>();
            public List<string> RightLines = new List<string>();
        }

        private sealed class SourceAlignRow
        {
            public string Left;
            public string Right;
        }

        private string FormatSourceLineForDisplay(string raw, bool patched)
        {
            var line = raw ?? string.Empty;
            var trimmed = line.TrimStart();
            var escaped = EscapeRichText(line);

            if (!patched)
            {
                return "<color=#D8D8D8>" + escaped + "</color>";
            }

            if (trimmed.StartsWith("//   +", StringComparison.Ordinal))
            {
                return "<color=#7CFC00>" + escaped + "</color>";
            }

            if (trimmed.StartsWith("//   -", StringComparison.Ordinal))
            {
                return "<color=#FF8A8A>" + escaped + "</color>";
            }

            if (trimmed.StartsWith("// [Regex Rewrite]", StringComparison.Ordinal) || line.IndexOf("[REGEX_PATCH]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "<color=#F6D365>" + escaped + "</color>";
            }

            if (line.IndexOf("TRANSPILE INJECTION PREVIEW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.StartsWith("// Hunk", StringComparison.Ordinal))
            {
                return "<color=#8ED6FF>" + escaped + "</color>";
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                return "<color=#B0B0B0>" + escaped + "</color>";
            }

            return "<color=#EDEDED>" + escaped + "</color>";
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
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
            var history = ModAPI.Harmony.TranspilerDebugger.History;
            if (history == null) return new List<ModAPI.Harmony.TranspilerDebugger.Snapshot>();
            IEnumerable<ModAPI.Harmony.TranspilerDebugger.Snapshot> query = history;

            query = query.Where(s =>
            {
                var isCore = IsCoreSnapshot(s);
                if (isCore && !_showCorePatches) return false;
                if (!isCore && !_showExternalPatches) return false;
                return true;
            });

            var hasMethodSearch = !string.IsNullOrEmpty(_historyMethodSearch);
            if (_sceneFilteredOnly && !hasMethodSearch)
            {
                query = query.Where(IsSnapshotSceneRelevant);
            }

            if (hasMethodSearch)
            {
                var search = _historyMethodSearch.Trim();
                query = query.Where(s =>
                {
                    if (s == null) return false;
                    var methodId = BuildSnapshotMethodId(s) ?? string.Empty;
                    var haystack = string.Join(" ", new[]
                    {
                        s.ModId ?? string.Empty,
                        methodId,
                        s.StepName ?? string.Empty,
                        s.MethodName ?? string.Empty,
                        s.PatchOrigin ?? string.Empty
                    });
                    return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                });
            }

            var filtered = query.ToList();
            if (filtered.Count <= 1) return filtered;

            // Some patches can be recorded twice in the same startup tick due to multi-stage patch flows.
            // Collapse identical per-method snapshots occurring in the same second.
            var deduped = new Dictionary<string, ModAPI.Harmony.TranspilerDebugger.Snapshot>(StringComparer.Ordinal);
            for (var i = 0; i < filtered.Count; i++)
            {
                var snap = filtered[i];
                var key = BuildSnapshotDedupKey(snap);
                if (string.IsNullOrEmpty(key))
                {
                    key = "__index__" + i;
                }

                if (deduped.TryGetValue(key, out var existing))
                {
                    if (existing == null || snap.Timestamp > existing.Timestamp)
                    {
                        deduped[key] = snap;
                    }
                }
                else
                {
                    deduped[key] = snap;
                }
            }

            return deduped.Values.OrderBy(s => s.Timestamp).ToList();
        }

        private static string BuildSnapshotDedupKey(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            if (snap == null) return string.Empty;

            var methodId = BuildSnapshotMethodId(snap);

            return string.Join("|", new[]
            {
                snap.ModId ?? string.Empty,
                methodId ?? string.Empty,
            });
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

            const int maxMatches = 40;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var normalized = search.Trim();
            if (string.IsNullOrEmpty(normalized)) return;

            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assemblies = new List<Assembly>(allAssemblies.Length);
            for (var i = 0; i < allAssemblies.Length; i++)
            {
                var asm = allAssemblies[i];
                if (asm == null) continue;
                assemblies.Add(asm);
            }

            assemblies.Sort((a, b) =>
            {
                var an = a != null ? (a.GetName().Name ?? string.Empty) : string.Empty;
                var bn = b != null ? (b.GetName().Name ?? string.Empty) : string.Empty;
                var ar = string.Equals(an, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ? 0
                    : (an.IndexOf("ModAPI", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2);
                var br = string.Equals(bn, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ? 0
                    : (bn.IndexOf("ModAPI", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2);
                var byRank = ar.CompareTo(br);
                if (byRank != 0) return byRank;
                return string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
            });

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var a = 0; a < assemblies.Count && _runtimeMethodMatches.Count < maxMatches; a++)
            {
                Type[] types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (ReflectionTypeLoadException rtl)
                {
                    var tmp = new List<Type>();
                    if (rtl.Types != null)
                    {
                        for (var i = 0; i < rtl.Types.Length; i++)
                        {
                            var t = rtl.Types[i];
                            if (t != null) tmp.Add(t);
                        }
                    }
                    types = tmp.ToArray();
                }
                catch
                {
                    continue;
                }

                for (var t = 0; t < types.Length && _runtimeMethodMatches.Count < maxMatches; t++)
                {
                    var type = types[t];
                    if (type == null) continue;
                    var typeName = type.FullName ?? type.Name ?? string.Empty;
                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(flags);
                    }
                    catch
                    {
                        continue;
                    }

                    for (var m = 0; m < methods.Length && _runtimeMethodMatches.Count < maxMatches; m++)
                    {
                        var method = methods[m];
                        if (method == null) continue;
                        var hit =
                            typeName.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            method.Name.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!hit) continue;

                        var key = typeName + "::" + method.Name;
                        if (seen.Contains(key)) continue;
                        seen.Add(key);
                        _runtimeMethodMatches.Add(method);
                    }
                }
            }
        }

        private static bool IsCoreSnapshot(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            if (snap == null) return false;
            var mod = snap.ModId ?? string.Empty;
            if (string.Equals(mod, "ModAPI", StringComparison.OrdinalIgnoreCase)) return true;

            var origin = snap.PatchOrigin ?? string.Empty;
            if (origin.IndexOf("Owner:ModAPI", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (origin.IndexOf("CooperativePatcher|ModAPI|", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private bool IsSnapshotSceneRelevant(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            if (snap == null) return false;
            if (_sceneTypeHints.Count == 0) return true;

            var probe = (snap.MethodName ?? string.Empty) + " " + (snap.StepName ?? string.Empty) + " " + (snap.PatchOrigin ?? string.Empty);
            if (string.IsNullOrEmpty(probe)) return false;
            if (!string.IsNullOrEmpty(_activeSceneName) && probe.IndexOf(_activeSceneName, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            foreach (var hint in _sceneTypeHints)
            {
                if (string.IsNullOrEmpty(hint)) continue;
                if (probe.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
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
            if (method != null && method.DeclaringType != null)
            {
                return method.DeclaringType.FullName + "." + method.Name;
            }

            if (fallback != null)
            {
                return BuildSnapshotMethodId(fallback);
            }

            return "<unresolved method>";
        }

        private static string BuildSnapshotMethodId(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            if (snap == null) return "<unresolved method>";
            if (!string.IsNullOrEmpty(snap.MethodName)) return snap.MethodName;
            if (!string.IsNullOrEmpty(snap.StepName)) return snap.StepName;
            return "<unresolved method>";
        }

        private static string BuildSnapshotDisplayTitle(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            var methodId = BuildSnapshotMethodId(snap);
            var lastDot = methodId.LastIndexOf('.');
            if (lastDot > 0 && lastDot < methodId.Length - 1)
            {
                var previousDot = methodId.LastIndexOf('.', lastDot - 1);
                if (previousDot >= 0 && previousDot < methodId.Length - 1)
                {
                    return methodId.Substring(previousDot + 1);
                }
            }

            return methodId;
        }

        private static bool IsSnapshotForMethod(ModAPI.Harmony.TranspilerDebugger.Snapshot snap, MethodBase method)
        {
            if (snap == null || method == null) return false;

            var methodId = method.DeclaringType != null
                ? method.DeclaringType.FullName + "." + method.Name
                : method.Name;
            if (string.IsNullOrEmpty(methodId)) return false;

            if (!string.IsNullOrEmpty(snap.MethodName) &&
                string.Equals(snap.MethodName, methodId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(snap.StepName) &&
                string.Equals(snap.StepName, methodId, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static string SafeValue(object value)
        {
            if (value == null) return "null";
            var text = value.ToString() ?? string.Empty;
            if (text.Length > 160)
            {
                return text.Substring(0, 157) + "...";
            }

            return text;
        }

        private ModAPI.Harmony.TranspilerDebugger.Snapshot FindLatestSnapshotForMethod(MethodBase method)
        {
            if (method == null) return null;

            var history = ModAPI.Harmony.TranspilerDebugger.History;
            if (history == null) return null;

            for (var i = history.Count - 1; i >= 0; i--)
            {
                var snap = history[i];
                if (IsSnapshotForMethod(snap, method))
                {
                    return snap;
                }
            }

            return null;
        }

        private string BuildPatchedSourcePreview(string vanillaSource, ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            _sourceRegexReplaceCount = 0;
            _sourceRegexSummaries.Clear();
            _patchedSourceRewrittenText = string.Empty;

            if (string.IsNullOrEmpty(vanillaSource))
            {
                _patchedSourceRewrittenText = "// Patched preview unavailable: vanilla source is empty.";
                return "// Patched preview unavailable: vanilla source is empty.";
            }

            if (snap == null)
            {
                _patchedSourceRewrittenText = vanillaSource;
                return vanillaSource;
            }

            var hunks = BuildSourcePreviewHunks(snap);
            if (hunks.Count == 0)
            {
                _patchedSourceRewrittenText = vanillaSource;
                return vanillaSource + "\n\n// [Transpiler Injection Preview] No IL additions/removals were detected.";
            }

            var rewritten = ApplyRegexSourceRewrites(vanillaSource, hunks, snap, out var rewriteSummaries, out var rewriteCount);
            _patchedSourceRewrittenText = rewritten;
            _sourceRegexReplaceCount = rewriteCount;
            if (rewriteSummaries != null && rewriteSummaries.Count > 0)
            {
                _sourceRegexSummaries.AddRange(rewriteSummaries);
            }

            var normalized = rewritten.Replace("\r\n", "\n");
            var lines = normalized.Split('\n').ToList();
            var methodName = _selectedMethod != null ? _selectedMethod.Name : ExtractMethodNameFromSelectedId(_selectedMethodId);
            var insertLine = FindBestOverlayInsertLine(lines, hunks, methodName);
            var indent = GuessIndentation(lines, insertLine);
            var overlay = RenderSourcePreviewOverlay(hunks, indent, _sourceRegexSummaries, _sourceRegexReplaceCount);

            lines.InsertRange(insertLine, overlay);
            return string.Join("\n", lines.ToArray());
        }

        private string ApplyRegexSourceRewrites(string source, List<SourcePreviewHunk> hunks, ModAPI.Harmony.TranspilerDebugger.Snapshot snap, out List<string> summaries, out int replaceCount)
        {
            summaries = new List<string>();
            replaceCount = 0;
            if (string.IsNullOrEmpty(source) || hunks == null || hunks.Count == 0)
            {
                return source;
            }

            var methodName = _selectedMethod != null ? _selectedMethod.Name : ExtractMethodNameFromSelectedId(_selectedMethodId);
            int scopeStart;
            int scopeLength;
            var hasScopedBody = TryFindMethodBodySpan(source, methodName, out scopeStart, out scopeLength);
            var scopePrefix = hasScopedBody ? source.Substring(0, scopeStart) : string.Empty;
            var scope = hasScopedBody ? source.Substring(scopeStart, scopeLength) : source;
            var scopeSuffix = hasScopedBody ? source.Substring(scopeStart + scopeLength) : string.Empty;
            var rewrittenScope = scope;

            var anyCandidate = false;
            var appliedPairSet = new HashSet<string>(StringComparer.Ordinal);
            var pairRewriteOrdinals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var h = 0; h < hunks.Count; h++)
            {
                if (replaceCount >= 8) break;

                var hunk = hunks[h];
                var addedExpressions = ExtractAddedExpressionsFromHunk(hunk);
                var removedNames = ExtractRemovedSourceTokensFromHunk(hunk);

                // Even if nothing was removed in IL, we might be inserting before/after a symbolic name.
                if (addedExpressions.Count == 0) continue;

                if (removedNames.Count == 0 && hunk.StartIndexBefore < snap.BeforeInstructions.Count)
                {
                    // Peek ahead up to 5 instructions to find a symbolic anchor for the insertion.
                    for (int i = hunk.StartIndexBefore; i < Math.Min(hunk.StartIndexBefore + 5, snap.BeforeInstructions.Count); i++)
                    {
                        var tokens = ExtractTokensFromILLine(snap.BeforeInstructions[i]);
                        foreach (var t in tokens)
                        {
                            if (!removedNames.Contains(t) && !IsHighRiskRemovedToken(t)) 
                                removedNames.Add(t);
                        }
                        if (removedNames.Count > 0) break;
                    }
                }

                anyCandidate = true;
                for (var a = 0; a < addedExpressions.Count; a++)
                {
                    if (replaceCount >= 8) break;
                    var replacementExpr = addedExpressions[a];
                    if (string.IsNullOrEmpty(replacementExpr)) continue;

                    for (var r = 0; r < removedNames.Count; r++)
                    {
                        if (replaceCount >= 8) break;

                        var token = removedNames[r];
                        if (string.IsNullOrEmpty(token)) continue;

                        var pairKey = token + "->" + replacementExpr;
                        if (appliedPairSet.Contains(pairKey)) continue;
                        int ordinalCursor;
                        if (!pairRewriteOrdinals.TryGetValue(pairKey, out ordinalCursor))
                        {
                            ordinalCursor = 0;
                        }

                        var before = replaceCount;
                        if (string.Equals(token, "__VECTOR2_ZERO_ZERO__", StringComparison.Ordinal) ||
                            string.Equals(token, "__VECTOR2_CTOR__", StringComparison.Ordinal))
                        {
                            // Handles both exact zero ctor and generic ctor-only removal cases.
                            var vector2LiteralPattern = string.Equals(token, "__VECTOR2_ZERO_ZERO__", StringComparison.Ordinal)
                                ? @"new\s+Vector2\s*\(\s*0(?:\.0+)?f?\s*,\s*0(?:\.0+)?f?\s*\)"
                                : @"new\s+Vector2\s*\(\s*[^,\)]+?\s*,\s*[^,\)]+?\s*\)";
                            rewrittenScope = TryApplyUniqueRegexRewrite(
                                rewrittenScope,
                                vector2LiteralPattern,
                                replacementExpr + " /* [REGEX_PATCH] */",
                                "Hunk " + (h + 1) + " literal new Vector2(...) -> " + replacementExpr,
                                summaries,
                                ref replaceCount,
                                ref ordinalCursor,
                                true);
                        }
                        else if (string.Equals(token, "__GRIDREF_HALF_COORDS__", StringComparison.Ordinal))
                        {
                            // FindClearSpace-style center-point construction:
                            // new GridRef(width / 2, height / 2) -> <replacementExpr>
                            
                            // Improved pattern to handle:
                            // - Optional 'this.' qualification
                            // - Optional '(float)' or '(int)' casts
                            // - 'width' or 'm_width'
                            // - Division by 2.0f, 2f, 2, or bitshift >> 1
                            var gridRefHalfPattern =
                                @"new\s+(?:[\w\.]+\.)*GridRef\s*\(\s*" +
                                // Arg 1: width/2
                                @"(?:[\w\.]+\.)*(?:m_)?width\s*(?:/[^,]+|>>\s*1)\s*," +
                                // Arg 2: height/2
                                @"\s*(?:[\w\.]+\.)*(?:m_)?height\s*(?:/[^)]+|>>\s*1)\s*\)";

                            rewrittenScope = TryApplyUniqueRegexRewrite(
                                rewrittenScope,
                                gridRefHalfPattern,
                                replacementExpr, // Removed redundant comment suffix here, TryApply adds one if needed
                                "Hunk " + (h + 1) + " literal new GridRef(width/2,height/2) -> " + replacementExpr,
                                summaries,
                                ref replaceCount,
                                ref ordinalCursor,
                                true);

                            if (replaceCount == before)
                            {
                                // Broader fallback when decompiler renders equivalent center math differently.
                                // We still keep occurrence-based matching to avoid global rewrites.
                                var anyGridRefCtorPattern = @"new\s+(?:[A-Za-z_]\w*\.)*GridRef\s*\((?:[^()]|\([^()]*\))*\)";
                                rewrittenScope = TryApplyUniqueRegexRewrite(
                                    rewrittenScope,
                                    anyGridRefCtorPattern,
                                    replacementExpr + " /* [REGEX_PATCH] */",
                                    "Hunk " + (h + 1) + " fallback new GridRef(...) -> " + replacementExpr,
                                    summaries,
                                    ref replaceCount,
                                    ref ordinalCursor,
                                    true);
                            }
                        }
                        else if (token.StartsWith("__CTOR__", StringComparison.Ordinal))
                        {
                            var ctorTypePattern = BuildConstructorLiteralPattern(token);
                            if (!string.IsNullOrEmpty(ctorTypePattern))
                            {
                                rewrittenScope = TryApplyUniqueRegexRewrite(
                                    rewrittenScope,
                                    ctorTypePattern,
                                    replacementExpr + " /* [REGEX_PATCH] */",
                                    "Hunk " + (h + 1) + " ctor literal " + token + " -> " + replacementExpr,
                                    summaries,
                                    ref replaceCount,
                                    ref ordinalCursor,
                                    true);
                            }
                        }
                        else if (Regex.IsMatch(token, @"^[A-Za-z_]\w*$"))
                        {
                            if (IsHighRiskRemovedToken(token))
                            {
                                summaries.Add("[Regex Rewrite] High-risk token left unchanged: " + token + " (Hunk " + (h + 1) + ")");
                                continue;
                            }

                            // If we have removals, we replace. If we only have additions, we anchor.
                            bool isReplacement = hunk.Removed != null && hunk.Removed.Count > 0;

                            // Prefer replacing/anchoring entire object/property chains where possible.
                            var chainPattern = @"\b[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*\.\s*" + Regex.Escape(token) + @"\b";
                            rewrittenScope = TryApplyUniqueRegexRewrite(
                                rewrittenScope,
                                chainPattern,
                                replacementExpr + (isReplacement ? " /* [REGEX_PATCH] */" : " /* [INSERT_PATCH] */"),
                                (isReplacement ? "Hunk " : "Anchor Hunk ") + (h + 1) + " chain ." + token + " -> " + replacementExpr,
                                summaries,
                                ref replaceCount,
                                ref ordinalCursor,
                                true,
                                !isReplacement); // anchorOnly if not replacing

                            if (replaceCount == before)
                            {
                                // Fallback: unique standalone symbol replacement.
                                var symbolPattern = @"\b" + Regex.Escape(token) + @"\b";
                                rewrittenScope = TryApplyUniqueRegexRewrite(
                                    rewrittenScope,
                                    symbolPattern,
                                    replacementExpr + " /* [REGEX_PATCH] */",
                                    "Hunk " + (h + 1) + " symbol " + token + " -> " + replacementExpr,
                                    summaries,
                                    ref replaceCount,
                                    ref ordinalCursor,
                                    false);
                            }
                        }
                        else
                        {
                            // Phrase literal fallback for non-symbol tokens.
                            var literalPattern = Regex.Escape(token);
                            rewrittenScope = TryApplyUniqueRegexRewrite(
                                rewrittenScope,
                                literalPattern,
                                replacementExpr + " /* [REGEX_PATCH] */",
                                "Hunk " + (h + 1) + " literal " + token + " -> " + replacementExpr,
                                summaries,
                                ref replaceCount,
                                ref ordinalCursor,
                                true);
                        }

                        if (replaceCount > before)
                        {
                            appliedPairSet.Add(pairKey);
                        }

                        pairRewriteOrdinals[pairKey] = ordinalCursor;
                    }
                }
            }

            if (!anyCandidate)
            {
                summaries.Add("[Regex Rewrite] No usable IL hunk pairs found (added expression + removed token).");
            }
            else if (replaceCount == 0)
            {
                summaries.Add("[Regex Rewrite] 0 applied (patterns were ambiguous or absent in method body).");
            }

            return hasScopedBody ? scopePrefix + rewrittenScope + scopeSuffix : rewrittenScope;
        }

        private static string TryApplyUniqueRegexRewrite(
            string source,
            string pattern,
            string replacement,
            string description,
            List<string> summaries,
            ref int replaceCount,
            ref int ordinalCursor,
            bool allowOrdinalFallback,
            bool anchorOnly = false)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(pattern)) return source;

            var regex = new Regex(pattern, RegexOptions.Multiline);
            var matches = regex.Matches(source);
            var validMatches = new List<Match>();
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (!match.Success) continue;
                if (IsInsideCommentLine(source, match.Index)) continue;
                validMatches.Add(match);
            }

            if (validMatches.Count == 1)
            {
                replaceCount++;
                summaries.Add("[Regex Rewrite] Applied: " + description);
                var m = validMatches[0];
                ordinalCursor++;
                
                if (anchorOnly)
                {
                    // For pure insertions, we keep the anchor and insert our code before it.
                    return source.Insert(m.Index, replacement + "\n" + GuessIndentationForAt(source, m.Index));
                }
                
                return source.Substring(0, m.Index) + replacement + source.Substring(m.Index + m.Length);
            }

            if (validMatches.Count > 1)
            {
                // Controlled fallback: for low-ambiguity rewrite candidates, prefer a deterministic
                // first-match replacement over dropping the rewrite entirely.
                // This is especially useful for repeated literals in methods with multiple similar branches.
                var allowBestGuess =
                    description.IndexOf("literal", StringComparison.OrdinalIgnoreCase) >= 0;

                if (allowOrdinalFallback)
                {
                    if (ordinalCursor >= 0 && ordinalCursor < validMatches.Count)
                    {
                        var m = validMatches[ordinalCursor];
                        var lineNumber = 1;
                        for (var i = 0; i < m.Index && i < source.Length; i++)
                        {
                            if (source[i] == '\n') lineNumber++;
                        }

                        replaceCount++;
                        summaries.Add("[Regex Rewrite] Applied by occurrence (" + (ordinalCursor + 1) + "/" + validMatches.Count + ", @line " + lineNumber + "): " + description);
                        ordinalCursor++;
                        return source.Substring(0, m.Index) + replacement + source.Substring(m.Index + m.Length);
                    }
                }

                if (allowBestGuess && validMatches.Count <= 4)
                {
                    var m = validMatches[0];
                    var lineNumber = 1;
                    for (var i = 0; i < m.Index && i < source.Length; i++)
                    {
                        if (source[i] == '\n') lineNumber++;
                    }

                    replaceCount++;
                    summaries.Add("[Regex Rewrite] Best-guess applied (" + validMatches.Count + " matches, chose first @line " + lineNumber + "): " + description);
                    ordinalCursor++;
                    return source.Substring(0, m.Index) + replacement + source.Substring(m.Index + m.Length);
                }

                summaries.Add("[Regex Rewrite] Ambiguous pattern (" + validMatches.Count + " matches), left unchanged: " + description);
            }
            else if (matches.Count > 0 && validMatches.Count == 0)
            {
                summaries.Add("[Regex Rewrite] Comment-only match, left unchanged: " + description);
            }

            return source;
        }

        private static bool IsHighRiskRemovedToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return true;

            // These are common structural identifiers in decompiled output.
            // Rewriting them tends to corrupt control/null-check semantics.
            return
                string.Equals(token, "instance", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "current", StringComparison.OrdinalIgnoreCase);
        }

        private static int FindBestOverlayInsertLine(List<string> lines, List<SourcePreviewHunk> hunks, string methodName)
        {
            if (lines == null || lines.Count == 0)
            {
                return 0;
            }

            var methodStartLine = 0;
            var methodEndLine = lines.Count - 1;
            TryGetMethodBodyLineRange(lines, methodName, out methodStartLine, out methodEndLine);

            // Highest-confidence anchor: a concrete regex rewrite mark in patched source.
            for (var i = methodStartLine; i <= methodEndLine && i < lines.Count; i++)
            {
                var line = lines[i] ?? string.Empty;
                if (line.IndexOf("[REGEX_PATCH]", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            // Fallback anchor: known Vector2 zero ctor pattern used by CreateMap patch.
            if (hunks != null && HasVector2ZeroCtorHunk(hunks))
            {
                var vector2Zero = new Regex(@"new\s+Vector2\s*\(\s*0(?:\.0+)?f?\s*,\s*0(?:\.0+)?f?\s*\)", RegexOptions.Multiline);
                for (var i = methodStartLine; i <= methodEndLine && i < lines.Count; i++)
                {
                    var line = lines[i] ?? string.Empty;
                    if (vector2Zero.IsMatch(line))
                    {
                        return i;
                    }
                }
            }

            // Last fallback: selected method body start.
            var methodBodyInsert = FindMethodBodyInsertLine(lines, methodName);
            if (methodBodyInsert >= 0 && methodBodyInsert <= lines.Count)
            {
                return methodBodyInsert;
            }

            return FindMethodBodyInsertLine(lines);
        }

        private static bool HasVector2ZeroCtorHunk(List<SourcePreviewHunk> hunks)
        {
            if (hunks == null) return false;
            for (var h = 0; h < hunks.Count; h++)
            {
                var removed = hunks[h] != null ? hunks[h].Removed : null;
                if (removed == null || removed.Count == 0) continue;

                var hasCtor = false;
                var zeroLoads = 0;
                for (var i = 0; i < removed.Count; i++)
                {
                    var line = removed[i] ?? string.Empty;
                    if (line.IndexOf("Vector2::.ctor", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasCtor = true;
                    }

                    if (line.IndexOf("ldc.r4 0", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        zeroLoads++;
                    }
                }

                if (hasCtor && zeroLoads >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideCommentLine(string source, int index)
        {
            if (string.IsNullOrEmpty(source) || index < 0 || index >= source.Length) return false;

            var lineStart = source.LastIndexOf('\n', index);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var lineEnd = source.IndexOf('\n', index);
            if (lineEnd < 0) lineEnd = source.Length;

            var line = source.Substring(lineStart, lineEnd - lineStart);
            return line.TrimStart().StartsWith("//", StringComparison.Ordinal);
        }

        private static List<string> ExtractAddedExpressionsFromHunks(List<SourcePreviewHunk> hunks)
        {
            var result = new List<string>();
            if (hunks == null) return result;

            for (var h = 0; h < hunks.Count; h++)
            {
                var added = hunks[h].Added;
                if (added == null) continue;
                for (var i = 0; i < added.Count; i++)
                {
                    var expression = BuildSourceExpressionFromILLine(added[i]);
                    if (!string.IsNullOrEmpty(expression) && !result.Contains(expression))
                    {
                        result.Add(expression);
                    }
                }
            }

            return result;
        }

        private static List<string> ExtractAddedExpressionsFromHunk(SourcePreviewHunk hunk)
        {
            var result = new List<string>();
            if (hunk == null || hunk.Added == null) return result;

            for (var i = 0; i < hunk.Added.Count; i++)
            {
                var expression = BuildSourceExpressionFromILLine(hunk.Added[i]);
                if (!string.IsNullOrEmpty(expression) && !result.Contains(expression))
                {
                    result.Add(expression);
                }
            }

            return result;
        }

        private static List<string> ExtractRemovedSourceTokensFromHunks(List<SourcePreviewHunk> hunks)
        {
            var tokens = new List<string>();
            if (hunks == null) return tokens;

            for (var h = 0; h < hunks.Count; h++)
            {
                var removed = hunks[h].Removed;
                if (removed == null) continue;
                for (var i = 0; i < removed.Count; i++)
                {
                    var line = removed[i] ?? string.Empty;

                    var getterMatches = Regex.Matches(line, @"::get_([A-Za-z_]\w*)\(");
                    for (var g = 0; g < getterMatches.Count; g++)
                    {
                        var token = getterMatches[g].Groups[1].Value;
                        if (!string.IsNullOrEmpty(token) && !tokens.Contains(token))
                        {
                            tokens.Add(token);
                        }
                    }

                    var callMatches = Regex.Matches(line, @"::([A-Za-z_]\w*)\(");
                    for (var c = 0; c < callMatches.Count; c++)
                    {
                        var token = callMatches[c].Groups[1].Value;
                        if (string.IsNullOrEmpty(token)) continue;
                        if (token.StartsWith("get_", StringComparison.Ordinal) || token.StartsWith("set_", StringComparison.Ordinal)) continue;
                        if (!tokens.Contains(token))
                        {
                            tokens.Add(token);
                        }
                    }
                }
            }

            return tokens;
        }

        private static List<string> ExtractRemovedSourceTokensFromHunk(SourcePreviewHunk hunk)
        {
            var tokens = new List<string>();
            if (hunk == null || hunk.Removed == null) return tokens;

            // Capture literal constructor patterns used by Vector2-based transpiles.
            var hasVector2Ctor = false;
            var vector2ZeroLoads = 0;
            var hasGridRefCtor = false;
            var widthFieldLoads = 0;
            var heightFieldLoads = 0;
            var intDivOps = 0;
            var removedCtorTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < hunk.Removed.Count; i++)
            {
                var line = hunk.Removed[i] ?? string.Empty;

                if (line.IndexOf("Vector2::.ctor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasVector2Ctor = true;
                }
                if (line.IndexOf("ldc.r4 0", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    vector2ZeroLoads++;
                }
                if (line.IndexOf("GridRef::.ctor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasGridRefCtor = true;
                }
                TryCollectCtorToken(line, removedCtorTypes);
                if (line.IndexOf("::width", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    widthFieldLoads++;
                }
                if (line.IndexOf("::height", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    heightFieldLoads++;
                }
                if (line.IndexOf(" div", StringComparison.OrdinalIgnoreCase) >= 0 || line.StartsWith("div", StringComparison.OrdinalIgnoreCase))
                {
                    intDivOps++;
                }

                var getterMatches = Regex.Matches(line, @"::get_([A-Za-z_]\w*)\(");
                for (var g = 0; g < getterMatches.Count; g++)
                {
                    var token = getterMatches[g].Groups[1].Value;
                    if (!string.IsNullOrEmpty(token) && !tokens.Contains(token))
                    {
                        tokens.Add(token);
                    }
                }

                var callMatches = Regex.Matches(line, @"::([A-Za-z_]\w*)\(");
                for (var c = 0; c < callMatches.Count; c++)
                {
                    var token = callMatches[c].Groups[1].Value;
                    if (string.IsNullOrEmpty(token)) continue;
                    if (token.StartsWith("get_", StringComparison.Ordinal) || token.StartsWith("set_", StringComparison.Ordinal)) continue;
                    if (!tokens.Contains(token))
                    {
                        tokens.Add(token);
                    }
                }
            }

            if (hasVector2Ctor && !tokens.Contains("__VECTOR2_CTOR__"))
            {
                tokens.Add("__VECTOR2_CTOR__");
            }

            if (hasVector2Ctor && vector2ZeroLoads >= 2 && !tokens.Contains("__VECTOR2_ZERO_ZERO__"))
            {
                tokens.Add("__VECTOR2_ZERO_ZERO__");
            }

            if (hasGridRefCtor && widthFieldLoads > 0 && heightFieldLoads > 0 && intDivOps >= 2 && !tokens.Contains("__GRIDREF_HALF_COORDS__"))
            {
                tokens.Add("__GRIDREF_HALF_COORDS__");
            }

            foreach (var ctorType in removedCtorTypes)
            {
                var ctorToken = "__CTOR__" + ctorType;
                if (!tokens.Contains(ctorToken))
                {
                    tokens.Add(ctorToken);
                }
            }

            return tokens;
        }

        private static void TryCollectCtorToken(string ilLine, HashSet<string> ctorTypes)
        {
            if (string.IsNullOrEmpty(ilLine) || ctorTypes == null) return;

            // Example IL:
            // newobj System.Void ExpeditionMap/GridRef::.ctor(System.Int32 x, System.Int32 y)
            var match = Regex.Match(ilLine, @"newobj\s+System\.Void\s+([^\s:]+)::\.ctor", RegexOptions.IgnoreCase);
            if (!match.Success) return;

            var rawType = match.Groups[1].Value ?? string.Empty;
            if (string.IsNullOrEmpty(rawType)) return;

            var normalized = rawType.Replace("/", ".").Replace("+", ".");
            var tick = normalized.IndexOf('`');
            if (tick > 0) normalized = normalized.Substring(0, tick);

            var shortType = normalized;
            var lastDot = normalized.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < normalized.Length - 1)
            {
                shortType = normalized.Substring(lastDot + 1);
            }

            if (string.IsNullOrEmpty(shortType)) return;
            ctorTypes.Add(shortType);
        }

        private static string BuildConstructorLiteralPattern(string ctorToken)
        {
            if (string.IsNullOrEmpty(ctorToken) || !ctorToken.StartsWith("__CTOR__", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var typeName = ctorToken.Substring("__CTOR__".Length);
            if (string.IsNullOrEmpty(typeName)) return string.Empty;

            // Generic constructor-source anchor:
            // new TypeName(...)
            return @"new\s+" + Regex.Escape(typeName) + @"\s*\((?:[^()]|\([^()]*\))*\)";
        }

        private static string ExtractMethodNameFromSelectedId(string methodId)
        {
            if (string.IsNullOrEmpty(methodId)) return string.Empty;
            var normalized = methodId.Trim();
            var paren = normalized.IndexOf('(');
            if (paren > 0) normalized = normalized.Substring(0, paren);
            var dot = normalized.LastIndexOf('.');
            if (dot >= 0 && dot < normalized.Length - 1)
            {
                return normalized.Substring(dot + 1);
            }

            return normalized;
        }

        private static bool TryFindMethodBodySpan(string source, string methodName, out int bodyStart, out int bodyLength)
        {
            bodyStart = 0;
            bodyLength = 0;
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(methodName)) return false;

            // Prefer declaration-like signatures to avoid matching method call sites.
            var declarationPattern = @"^\s*(?:public|private|protected|internal)\s+[^=\r\n;]*\b" + Regex.Escape(methodName) + @"\s*\(";
            var signature = new Regex(declarationPattern, RegexOptions.Multiline);
            var sigMatch = signature.Match(source);
            if (!sigMatch.Success)
            {
                // Fallback to broader search only when declaration scan fails.
                signature = new Regex(@"\b" + Regex.Escape(methodName) + @"\s*\(", RegexOptions.Multiline);
                sigMatch = signature.Match(source);
                if (!sigMatch.Success) return false;
            }

            var open = source.IndexOf('{', sigMatch.Index);
            if (open < 0) return false;

            var depth = 0;
            for (var i = open; i < source.Length; i++)
            {
                var ch = source[i];
                if (ch == '{') depth++;
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        bodyStart = open + 1;
                        bodyLength = i - bodyStart;
                        return bodyLength > 0;
                    }
                }
            }

            return false;
        }

        private static int FindMethodBodyInsertLine(List<string> lines, string methodName)
        {
            if (lines == null || lines.Count == 0 || string.IsNullOrEmpty(methodName))
            {
                return -1;
            }

            var declarationRegex = new Regex(@"^\s*(?:public|private|protected|internal)\s+[^=\r\n;]*\b" + Regex.Escape(methodName) + @"\s*\(");
            var depth = 0;
            var inMethod = false;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i] ?? string.Empty;
                if (!inMethod)
                {
                    if (!declarationRegex.IsMatch(line))
                    {
                        continue;
                    }
                }

                for (var c = 0; c < line.Length; c++)
                {
                    var ch = line[c];
                    if (ch == '{')
                    {
                        depth++;
                        if (!inMethod)
                        {
                            inMethod = true;
                            return Math.Min(i + 1, lines.Count);
                        }
                    }
                    else if (ch == '}')
                    {
                        if (depth > 0) depth--;
                    }
                }
            }

            return -1;
        }

        private static bool TryGetMethodBodyLineRange(List<string> lines, string methodName, out int startLine, out int endLine)
        {
            startLine = 0;
            endLine = (lines != null && lines.Count > 0) ? lines.Count - 1 : 0;
            if (lines == null || lines.Count == 0 || string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            var declarationRegex = new Regex(@"^\s*(?:public|private|protected|internal)\s+[^=\r\n;]*\b" + Regex.Escape(methodName) + @"\s*\(");
            var depth = 0;
            var declarationSeen = false;
            var bodyStarted = false;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i] ?? string.Empty;
                if (!declarationSeen)
                {
                    if (!declarationRegex.IsMatch(line))
                    {
                        continue;
                    }
                    declarationSeen = true;
                }

                for (var c = 0; c < line.Length; c++)
                {
                    var ch = line[c];
                    if (ch == '{')
                    {
                        depth++;
                        if (!bodyStarted)
                        {
                            bodyStarted = true;
                            startLine = Math.Min(i + 1, lines.Count - 1);
                        }
                    }
                    else if (ch == '}')
                    {
                        if (depth > 0) depth--;
                        if (bodyStarted && depth == 0)
                        {
                            endLine = Math.Max(startLine, i - 1);
                            return true;
                        }
                    }
                }
            }

            if (bodyStarted)
            {
                endLine = lines.Count - 1;
                return true;
            }

            return false;
        }

        private static string GuessIndentationForAt(string source, int index)
        {
            if (string.IsNullOrEmpty(source) || index < 0 || index >= source.Length) return "    ";
            var lineStart = source.LastIndexOf('\n', index);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var lineEnd = source.IndexOf('\n', lineStart);
            if (lineEnd < 0) lineEnd = source.Length;
            
            var fullLine = source.Substring(lineStart, lineEnd - lineStart);
            var trimmed = fullLine.TrimStart();
            return fullLine.Substring(0, fullLine.Length - trimmed.Length);
        }

        private static List<string> ExtractTokensFromILLine(string line)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(line)) return tokens;

            var getterMatches = Regex.Matches(line, @"::get_([A-Za-z_]\w*)\(");
            for (var g = 0; g < getterMatches.Count; g++)
            {
                var token = getterMatches[g].Groups[1].Value;
                if (!string.IsNullOrEmpty(token) && !tokens.Contains(token)) tokens.Add(token);
            }

            var callMatches = Regex.Matches(line, @"::([A-Za-z_]\w*)\(");
            for (var c = 0; c < callMatches.Count; c++)
            {
                var token = callMatches[c].Groups[1].Value;
                if (string.IsNullOrEmpty(token)) continue;
                if (token.StartsWith("get_", StringComparison.Ordinal) || token.StartsWith("set_", StringComparison.Ordinal)) continue;
                if (!tokens.Contains(token)) tokens.Add(token);
            }
            
            var fieldMatches = Regex.Matches(line, @"::([A-Za-z_]\w*)\b");
            for (var f = 0; f < fieldMatches.Count; f++)
            {
                var token = fieldMatches[f].Groups[1].Value;
                if (string.IsNullOrEmpty(token)) continue;
                if (token == ".ctor" || token == ".cctor") continue;
                if (!tokens.Contains(token)) tokens.Add(token);
            }

            return tokens;
        }

        private static string BuildSourceExpressionFromILLine(string ilLine)
        {
            if (string.IsNullOrEmpty(ilLine)) return string.Empty;

            var line = ilLine.Trim();
            var match = Regex.Match(line, @"::([A-Za-z_]\w*)\(");
            if (!match.Success) return string.Empty;

            var methodName = match.Groups[1].Value;
            var typeEnd = line.IndexOf("::", StringComparison.Ordinal);
            if (typeEnd < 0) return string.Empty;

            // We need the token right before "::" (the type name).
            var left = line.Substring(0, typeEnd).Trim();
            var space = left.LastIndexOf(' ');
            var typeName = space >= 0 ? left.Substring(space + 1).Trim() : left;
            if (string.IsNullOrEmpty(typeName)) return string.Empty;

            if (methodName.StartsWith("get_", StringComparison.Ordinal))
            {
                return typeName + "." + methodName.Substring(4);
            }

            return typeName + "." + methodName + "()";
        }

        private List<SourcePreviewHunk> BuildSourcePreviewHunks(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            if (snap != null && snap.PatchEdits != null && snap.PatchEdits.Count > 0)
            {
                var manifestHunks = BuildSourcePreviewHunksFromPatchEdits(snap.PatchEdits);
                if (manifestHunks.Count > 0)
                {
                    return manifestHunks;
                }
            }

            var diff = _currentDiff ?? ComputeDiff(snap.BeforeInstructions, snap.Instructions);
            var hunks = new List<SourcePreviewHunk>();
            SourcePreviewHunk current = null;

            for (var i = 0; i < diff.Count; i++)
            {
                var line = diff[i];
                var isRemoved = line != null && line.LeftMarker == "-" && !string.IsNullOrEmpty(line.LeftContent);
                var isAdded = line != null && line.RightMarker == "+" && !string.IsNullOrEmpty(line.RightContent);
                var changed = isRemoved || isAdded;
                if (!changed)
                {
                    if (current != null && (current.Removed.Count > 0 || current.Added.Count > 0))
                    {
                        hunks.Add(current);
                    }
                    current = null;
                    continue;
                }

                if (current == null)
                {
                    current = new SourcePreviewHunk { StartIndexBefore = line.LeftIndex };
                }

                if (isRemoved)
                {
                    current.Removed.Add(FormatInstructionForSourcePreview(line.LeftContent));
                }

                if (isAdded)
                {
                    current.Added.Add(FormatInstructionForSourcePreview(line.RightContent));
                }
            }

            if (current != null && (current.Removed.Count > 0 || current.Added.Count > 0))
            {
                hunks.Add(current);
            }

            return hunks;
        }

        private static List<SourcePreviewHunk> BuildSourcePreviewHunksFromPatchEdits(IList<ModAPI.Harmony.TranspilerDebugger.PatchEdit> patchEdits)
        {
            var hunks = new List<SourcePreviewHunk>();
            if (patchEdits == null) return hunks;

            for (var i = 0; i < patchEdits.Count; i++)
            {
                var edit = patchEdits[i];
                if (edit == null) continue;

                var removed = edit.RemovedInstructions ?? new List<string>();
                var added = edit.AddedInstructions ?? new List<string>();
                if (removed.Count == 0 && added.Count == 0) continue;

                var hunk = new SourcePreviewHunk { StartIndexBefore = edit.StartIndexBefore };
                for (var r = 0; r < removed.Count; r++)
                {
                    hunk.Removed.Add(FormatInstructionForSourcePreview(removed[r]));
                }
                for (var a = 0; a < added.Count; a++)
                {
                    hunk.Added.Add(FormatInstructionForSourcePreview(added[a]));
                }

                if (hunk.Removed.Count > 0 || hunk.Added.Count > 0)
                {
                    hunks.Add(hunk);
                }
            }

            return hunks;
        }

        private static int FindMethodBodyInsertLine(List<string> lines)
        {
            if (lines == null || lines.Count == 0) return 0;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) continue;
                if (line.IndexOf("{", StringComparison.Ordinal) >= 0)
                {
                    return Math.Min(i + 1, lines.Count);
                }
            }

            return lines.Count;
        }

        private static string GuessIndentation(List<string> lines, int insertLine)
        {
            if (lines == null || lines.Count == 0) return "    ";
            var probeStart = Math.Max(0, insertLine - 1);
            var probeEnd = Math.Min(lines.Count - 1, insertLine + 3);

            for (var i = probeStart; i <= probeEnd; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                var trimmed = line.TrimStart();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("}", StringComparison.Ordinal)) continue;

                var indentLen = line.Length - trimmed.Length;
                if (indentLen > 0)
                {
                    return line.Substring(0, indentLen);
                }
            }

            return "    ";
        }

        private static List<string> RenderSourcePreviewOverlay(List<SourcePreviewHunk> hunks, string indent, List<string> regexSummaries, int regexRewriteCount)
        {
            var lines = new List<string>();
            if (hunks == null || hunks.Count == 0) return lines;

            lines.Add(indent + "// === TRANSPILE INJECTION PREVIEW (estimated from IL diff) ===");
            lines.Add(indent + "// This shows likely runtime-injected operations next to original source.");
            if (regexRewriteCount > 0)
            {
                lines.Add(indent + "// [Regex Rewrite] " + regexRewriteCount + " source replacements applied.");
            }
            else
            {
                lines.Add(indent + "// [Regex Rewrite] 0 source replacements applied.");
            }
            if (regexSummaries != null && regexSummaries.Count > 0)
            {
                var shown = Math.Min(4, regexSummaries.Count);
                for (var i = 0; i < shown; i++)
                {
                    lines.Add(indent + "// " + regexSummaries[i]);
                }
                if (regexSummaries.Count > shown)
                {
                    lines.Add(indent + "// ... " + (regexSummaries.Count - shown) + " more rewrite notes");
                }
            }

            const int maxHunks = 8;
            const int maxLinesPerSide = 6;
            var displayedHunks = Math.Min(maxHunks, hunks.Count);
            for (var h = 0; h < displayedHunks; h++)
            {
                var hunk = hunks[h];
                lines.Add(indent + "// Hunk " + (h + 1) + ":");

                var removedCount = hunk.Removed != null ? hunk.Removed.Count : 0;
                var addedCount = hunk.Added != null ? hunk.Added.Count : 0;
                if (removedCount == 0 && addedCount == 0)
                {
                    lines.Add(indent + "//   (no delta lines)");
                    continue;
                }

                if (removedCount > 0)
                {
                    var removedShown = Math.Min(maxLinesPerSide, removedCount);
                    for (var i = 0; i < removedShown; i++)
                    {
                        lines.Add(indent + "//   - " + hunk.Removed[i]);
                    }
                    if (removedCount > removedShown)
                    {
                        lines.Add(indent + "//   - ... " + (removedCount - removedShown) + " more removed IL lines");
                    }
                }

                if (addedCount > 0)
                {
                    var addedShown = Math.Min(maxLinesPerSide, addedCount);
                    for (var i = 0; i < addedShown; i++)
                    {
                        lines.Add(indent + "//   + " + hunk.Added[i]);
                    }
                    if (addedCount > addedShown)
                    {
                        lines.Add(indent + "//   + ... " + (addedCount - addedShown) + " more added IL lines");
                    }
                }
            }

            if (hunks.Count > displayedHunks)
            {
                lines.Add(indent + "// ... " + (hunks.Count - displayedHunks) + " more IL diff hunks omitted");
            }

            lines.Add(indent + "// === END INJECTION PREVIEW ===");
            lines.Add(string.Empty);
            return lines;
        }

        private static string FormatInstructionForSourcePreview(string ilLine)
        {
            if (string.IsNullOrEmpty(ilLine)) return string.Empty;

            var text = ilLine.Trim();
            if (text.Length > 170)
            {
                text = text.Substring(0, 167) + "...";
            }

            return text;
        }

        private List<DiffLine> _currentDiff;

        private class DiffLine
        {
            public string LeftContent;   // Original
            public string RightContent;  // Patched
            public int LeftIndex;        // Index in original list
            public int RightIndex;       // Index in patched list
            public string LeftMarker;    // "-" or empty
            public string RightMarker;   // "+" or empty
            public bool IsMatch;         // True if contents match
        }

        private sealed class SourcePreviewHunk
        {
            public List<string> Removed = new List<string>();
            public List<string> Added = new List<string>();
            public int StartIndexBefore;
        }

        private void OnSnapshotSelected(ModAPI.Harmony.TranspilerDebugger.Snapshot snap)
        {
            if (snap == null) return;
            _selectedMethod = ResolveMethodFromSnapshot(snap);
            _selectedMethodId = BuildMethodDisplayName(_selectedMethod, snap);
            _sourceStatus = string.Empty;
            
            // Compute Diff
            _currentDiff = ComputeDiff(snap.BeforeInstructions, snap.Instructions);

            if (_preferSourceDiffDefault && !_isLiveMode)
            {
                _snapshotViewMode = SnapshotViewMode.SourceDiff;
            }

            if (_snapshotViewMode == SnapshotViewMode.Source || _snapshotViewMode == SnapshotViewMode.SourceDiff)
            {
                LoadSourceForSelectedSnapshot();
            }
        }

        private List<DiffLine> ComputeDiff(List<string> before, List<string> after)
        {
            // Simple LCS-based diff for strings
            // This allows us to align the "Before" (Left) and "After" (Right) scroll views visually.
            
            var A = before ?? new List<string>();
            var B = after ?? new List<string>();
            int m = A.Count;
            int n = B.Count;

            int[,] C = new int[m + 1, n + 1];

            for (int i = 0; i <= m; i++)
            {
                for (int j = 0; j <= n; j++)
                {
                    if (i == 0 || j == 0)
                        C[i, j] = 0;
                    else if (A[i - 1] == B[j - 1])
                        C[i, j] = C[i - 1, j - 1] + 1;
                    else
                        C[i, j] = Math.Max(C[i - 1, j], C[i, j - 1]);
                }
            }

            var result = new List<DiffLine>();
            int x = m, y = n;
            
            // Backtrack
            var stack = new Stack<DiffLine>();
            
            while (x > 0 && y > 0)
            {
                if (A[x - 1] == B[y - 1])
                {
                    stack.Push(new DiffLine 
                    { 
                        LeftContent = A[x - 1], RightContent = B[y - 1],
                        LeftIndex = x - 1, RightIndex = y - 1,
                        LeftMarker = " ", RightMarker = " ", IsMatch = true
                    });
                    x--; y--;
                }
                else if (C[x - 1, y] >= C[x, y - 1])
                {
                    stack.Push(new DiffLine 
                    { 
                        LeftContent = A[x - 1], RightContent = null,
                        LeftIndex = x - 1, RightIndex = -1,
                        LeftMarker = "-", RightMarker = " ", IsMatch = false
                    });
                    x--;
                }
                else
                {
                    stack.Push(new DiffLine 
                    { 
                        LeftContent = null, RightContent = B[y - 1],
                        LeftIndex = -1, RightIndex = y - 1,
                        LeftMarker = " ", RightMarker = "+", IsMatch = false
                    });
                    y--;
                }
            }

            while (x > 0)
            {
                stack.Push(new DiffLine 
                { 
                    LeftContent = A[x - 1], RightContent = null,
                    LeftIndex = x - 1, RightIndex = -1,
                    LeftMarker = "-", RightMarker = " ", IsMatch = false
                });
                x--;
            }

            while (y > 0)
            {
                stack.Push(new DiffLine 
                { 
                    LeftContent = null, RightContent = B[y - 1],
                    LeftIndex = -1, RightIndex = y - 1,
                    LeftMarker = " ", RightMarker = "+", IsMatch = false
                });
                y--;
            }

            return stack.ToList();
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
            if (snap == null) return null;

            MethodBase method;
            if (TryResolveMethodIdentifier(snap.MethodName, snap.AssemblyName, out method))
            {
                return method;
            }

            if (TryResolveMethodIdentifier(snap.StepName, snap.AssemblyName, out method))
            {
                return method;
            }

            return null;
        }

        private static bool TryResolveMethodIdentifier(string methodIdentifier, string assemblyName, out MethodBase method)
        {
            method = null;
            if (string.IsNullOrEmpty(methodIdentifier)) return false;

            var normalized = methodIdentifier.Trim();
            var paren = normalized.IndexOf('(');
            if (paren > 0) normalized = normalized.Substring(0, paren);
            var lastDot = normalized.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= normalized.Length - 1) return false;

            var typeName = normalized.Substring(0, lastDot);
            var methodName = normalized.Substring(lastDot + 1);

            Type type = null;
            if (!string.IsNullOrEmpty(assemblyName))
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (var i = 0; i < assemblies.Length; i++)
                {
                    var asm = assemblies[i];
                    if (!string.Equals(asm.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)) continue;
                    type = asm.GetType(typeName, false);
                    if (type != null) break;
                }
            }

            if (type == null)
            {
                type = AccessTools.TypeByName(typeName);
            }

            if (type == null) return false;

            method = AccessTools.Method(type, methodName);
            if (method != null) return true;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var methods = type.GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == methodName)
                {
                    method = methods[i];
                    return true;
                }
            }

            return false;
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
                GUILayout.Label("<color=" + color + ">[" + e.Timestamp.ToString("HH:mm:ss.fff") + "] [" + e.Level.ToString().ToUpper() + "] [" + EscapeRichText(src) + "] " + EscapeRichText(msg) + "</color>");
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
                GUILayout.Label("<size=10>" + EscapeRichText(_liveSourceStatus) + "</size>");
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

            var sourceLines = SplitLines(string.IsNullOrEmpty(_liveSourceText) ? "// Source unavailable." : _liveSourceText);
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

                var display = linePrefix + FormatSourceLineForDisplay(displayLine, false);
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

