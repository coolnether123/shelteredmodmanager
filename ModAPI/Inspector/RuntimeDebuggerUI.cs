using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ModAPI.Inspector
{
    public class RuntimeDebuggerUI : MonoBehaviour
    {
        private bool _active;
        private Rect _windowRect = new Rect(60, 60, 1040, 700);
        private readonly RuntimeVariableEditor _variableEditor = new RuntimeVariableEditor();

        private MethodBase _selectedMethod;
        private string _typeName = string.Empty;
        private string _methodName = string.Empty;
        private bool _showIL;
        private bool _losslessTrace;
        private string _sourceText = string.Empty;
        private string _ilText = string.Empty;
        private string _statusText = "Ready";
        private string _tooltipText = "";
        private Vector2 _tooltipPos;
        private GUIStyle _tooltipStyle;

        private bool _hasStartPoint;
        private int _startLine = -1;
        private int _startOffset = -1;
        private int _selectedSourceLine = -1;

        private readonly HashSet<string> _watchedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _lockedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Vector2 _scrollLeft;
        private Vector2 _scrollRight;
        private Vector2 _scrollSnapshot;
        private Vector2 _scrollSnapshotSource;
        private Vector2 _scrollSnapshotVars;
        private Vector2 _scrollBuild; // Added this based on DrawBuildMode usage
        private bool _autoScroll = true;
        private int _lastScrollLine = -1;
        private float _lineHeight = 16f;

        private GUIStyle _monoTextArea;
        private GUIStyle _lineButtonStyle;
        private Font _monoFont;

        private string _snapshotInfo = string.Empty;
        private readonly List<TraceFrame> _snapshotFrames = new List<TraceFrame>();
        private TraceFrame _snapshotFrame;
        private int _snapshotIndex;
        private MethodBase _snapshotMethod;
        private string _snapshotSource = string.Empty;
        private int _snapshotSourceLine = -1;
        private string _snapshotVarsText = string.Empty;

        private VisualTranspilerBuilder.PatchConfiguration _patchConfig = new VisualTranspilerBuilder.PatchConfiguration();
        private string _buildPreview = string.Empty;
        private string _buildPatchCode = string.Empty;

        private enum DebugMode { Live, Snapshot, Build }
        private DebugMode _mode = DebugMode.Live;

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12)) _active = !_active;
            if (_active && Input.GetKeyDown(KeyCode.Escape)) _active = false;
            _variableEditor.ProcessPendingEdits();
        }

        public void OnGUI()
        {
            if (!_active) return;
            EnsureStyles();
            _windowRect = GUI.Window(9999, _windowRect, DrawWindow, "Runtime Debugger v2.0 (F12)");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_mode == DebugMode.Live, "Live", "button", GUILayout.Width(120))) _mode = DebugMode.Live;
            if (GUILayout.Toggle(_mode == DebugMode.Snapshot, "Snapshot", "button", GUILayout.Width(120))) _mode = DebugMode.Snapshot;
            if (GUILayout.Toggle(_mode == DebugMode.Build, "Build Patch", "button", GUILayout.Width(120))) _mode = DebugMode.Build;
            GUILayout.FlexibleSpace();
            GUILayout.Label("F12/Esc to close");
            if (GUILayout.Button("Close", GUILayout.Width(80))) _active = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (_mode == DebugMode.Live && ModAPI.Debugging.LiveDebugger.IsAttached)
            {
                DrawLiveMode();
            }
            else if (_mode == DebugMode.Snapshot)
            {
                DrawSnapshotMode();
            }
            else if (_mode == DebugMode.Build)
            {
                DrawBuildMode();
            }

            if (!string.IsNullOrEmpty(_tooltipText))
            {
                DrawTooltip();
            }

            GUI.DragWindow(); // This was originally here, keeping it.
        }

        private void DrawTooltip()
        {
            if (_tooltipStyle == null)
            {
                _tooltipStyle = new GUIStyle(GUI.skin.box);
                _tooltipStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.95f));
                _tooltipStyle.normal.textColor = Color.white;
                _tooltipStyle.alignment = TextAnchor.UpperLeft;
                _tooltipStyle.wordWrap = true;
                _tooltipStyle.padding = new RectOffset(8, 8, 8, 8);
            }

            var content = new GUIContent(_tooltipText);
            var size = _tooltipStyle.CalcSize(content);
            size.x = Math.Min(size.x, 400); // Max width
            var height = _tooltipStyle.CalcHeight(content, size.x);
            
            var rect = new Rect(_tooltipPos.x + 15, _tooltipPos.y + 15, size.x, height);
            
            // Keep on screen
            if (rect.xMax > Screen.width) rect.x -= rect.width + 20;
            if (rect.yMax > Screen.height) rect.y -= rect.height + 20;

            GUI.Label(rect, content, _tooltipStyle);
            
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.Layout)
            {
                _tooltipText = ""; // Clear for next frame
            }
        }

        // Helper method for MakeTex, assuming it's needed and not already present
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void DrawLiveMode()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            DrawMethodSelector();
            DrawCaptureControls();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(_windowRect.width * 0.62f));
            DrawSourceOrILPanel();
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawVariablePanel();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawMethodSelector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Type", GUILayout.Width(40));
            _typeName = GUILayout.TextField(_typeName, GUILayout.Width(300));
            GUILayout.Label("Method", GUILayout.Width(55));
            _methodName = GUILayout.TextField(_methodName, GUILayout.Width(240));
            if (GUILayout.Button("Resolve", GUILayout.Width(90))) ResolveMethod();
            if (GUILayout.Button("Use Last Transpiled", GUILayout.Width(160))) UseLastTranspiledMethod();
            GUILayout.EndHorizontal();

            if (_selectedMethod != null) GUILayout.Label("Selected: " + _selectedMethod.DeclaringType.FullName + "." + _selectedMethod.Name);
            GUILayout.Label("Cache: " + SourceCacheManager.CacheRootPath);
            GUILayout.Label("Decompiler: " + SourceCacheManager.ResolveDecompilerPath());
            if (!string.IsNullOrEmpty(SourceCacheManager.LastError)) GUILayout.Label("Decompiler Error: " + SourceCacheManager.LastError);
            if (!string.IsNullOrEmpty(_statusText)) GUILayout.Label("Status: " + _statusText);
        }

        private void DrawCaptureControls()
        {
            GUILayout.BeginHorizontal();
            if (_selectedMethod == null || ExecutionTracer.Instance == null)
            {
                GUILayout.Label(_selectedMethod == null ? "Select a method to start tracing." : "ExecutionTracer not initialized.");
                GUILayout.EndHorizontal();
                return;
            }

            var tracer = ExecutionTracer.Instance;
            var offset = _startOffset >= 0 ? _startOffset : 0;

            if (GUILayout.Button("Start Snapshot", GUILayout.Width(120)))
            {
                tracer.BeginSnapshot(_selectedMethod, offset);
                _hasStartPoint = true;
                _statusText = "Snapshot started at IL_" + offset.ToString("X4");
                MMLog.WriteInfo("[RuntimeDebuggerUI] Start snapshot at IL_" + offset.ToString("X4"));
            }

            if (GUILayout.Button("End Snapshot", GUILayout.Width(120)))
            {
                tracer.EndSnapshot(offset);
                _hasStartPoint = false;
                _startLine = -1;
                _startOffset = -1;
                _statusText = "Snapshot ended.";
            }

            var newLossless = GUILayout.Toggle(_losslessTrace, "Lossless Trace", "button", GUILayout.Width(130));
            if (newLossless != _losslessTrace)
            {
                _losslessTrace = newLossless;
                tracer.SetLosslessTrace(_losslessTrace);
            }

            if (GUILayout.Toggle(!_showIL, "Source View", "button", GUILayout.Width(120))) _showIL = false;
            if (GUILayout.Toggle(_showIL, "IL View", "button", GUILayout.Width(120))) _showIL = true;
            GUILayout.FlexibleSpace();
            GUILayout.Label("Click source line numbers for Point A / Point B.");
            GUILayout.EndHorizontal();
        }

        private void DrawSourceOrILPanel()
        {
            GUILayout.Label(_showIL ? "<b>IL</b>" : "<b>Source</b>");
            if (_showIL)
            {
                _scrollLeft = GUILayout.BeginScrollView(_scrollLeft, GUILayout.Height(540));
                GUILayout.TextArea(string.IsNullOrEmpty(_ilText) ? "<No IL loaded>" : _ilText, _monoTextArea, GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
                return;
            }

            var source = string.IsNullOrEmpty(_sourceText) ? "No source loaded." : _sourceText;
            var lines = source.Replace("\r\n", "\n").Split('\n');
            _scrollLeft = GUILayout.BeginScrollView(_scrollLeft, GUILayout.Height(540));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(70));
            for (var i = 0; i < lines.Length; i++)
            {
                var lineNo = i + 1;
                var prev = GUI.contentColor;
                if (_snapshotSourceLine == lineNo) GUI.contentColor = Color.green;
                else if (_startLine == lineNo) GUI.contentColor = new Color(0.95f, 0.8f, 0.25f);
                else if (_selectedSourceLine == lineNo) GUI.contentColor = Color.cyan;
                if (GUILayout.Button(lineNo.ToString("D4"), _lineButtonStyle, GUILayout.Width(64))) OnSourceLineClicked(lineNo);
                GUI.contentColor = prev;
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.TextArea(source, _monoTextArea, GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void OnSourceLineClicked(int lineNo)
        {
            _selectedSourceLine = lineNo;
            if (_mode != DebugMode.Live || _selectedMethod == null || ExecutionTracer.Instance == null) return;

            var ilOffset = FindNearestILOffset(_selectedMethod, lineNo, out _selectedSourceLine);
            if (ilOffset < 0)
            {
                _statusText = "No IL mapping found near line " + lineNo;
                return;
            }

            var tracer = ExecutionTracer.Instance;
            if (!_hasStartPoint || !tracer.IsCapturing)
            {
                _hasStartPoint = true;
                _startLine = _selectedSourceLine;
                _startOffset = ilOffset;
                tracer.BeginSnapshot(_selectedMethod, ilOffset);
                _statusText = "Point A set at line " + _selectedSourceLine + " (IL_" + ilOffset.ToString("X4") + ")";
                MMLog.WriteInfo("[RuntimeDebuggerUI] Point A line " + _selectedSourceLine + " -> IL_" + ilOffset.ToString("X4"));
            }
            else
            {
                tracer.EndSnapshot(ilOffset);
                _statusText = "Point B set at line " + _selectedSourceLine + " (IL_" + ilOffset.ToString("X4") + ")";
                MMLog.WriteInfo("[RuntimeDebuggerUI] Point B line " + _selectedSourceLine + " -> IL_" + ilOffset.ToString("X4"));
                _hasStartPoint = false;
                _startLine = -1;
                _startOffset = -1;
            }
        }

        private int FindNearestILOffset(MethodBase method, int requestedLine, out int actualLine)
        {
            actualLine = requestedLine;
            var offset = SourceCacheManager.MapSourceLineToILOffset(method, requestedLine);
            if (offset >= 0) return offset;

            // Search nearby lines (up to 5 lines ahead)
            for (var i = 1; i <= 5; i++)
            {
                offset = SourceCacheManager.MapSourceLineToILOffset(method, requestedLine + i);
                if (offset >= 0) { actualLine = requestedLine + i; return offset; }
            }
            return -1;
        }

        private void DrawVariablePanel()
        {
            GUILayout.Label("<b>Variables</b>");
            _scrollRight = GUILayout.BeginScrollView(_scrollRight, GUILayout.Height(540));
            var tracer = ExecutionTracer.Instance;
            if (tracer == null || tracer.CurrentSnapshot == null || tracer.CurrentSnapshot.Frames == null || tracer.CurrentSnapshot.Frames.Count == 0)
            {
                GUILayout.Label("No frames captured.");
                GUILayout.EndScrollView();
                return;
            }

            var latest = tracer.CurrentSnapshot.Frames[tracer.CurrentSnapshot.Frames.Count - 1];
            GUILayout.Label("Method: " + latest.MethodName);
            GUILayout.Label("Time: " + latest.Timestamp.ToString("HH:mm:ss.fff"));
            GUILayout.Label("Exec: " + latest.ExecutionTimeMs.ToString("F3") + " ms");
            GUILayout.Label("Memory: " + (latest.MemoryUsage / 1024L / 1024L) + " MB");
            if (latest.WasModified && latest.ModifiedVariables != null && latest.ModifiedVariables.Count > 0) GUILayout.Label("Locked Changes: " + string.Join(", ", latest.ModifiedVariables.ToArray()));
            GUILayout.Label("Writer Busy: " + tracer.IsWriterBusy + " | Lossless: " + tracer.LosslessTrace);

            GUILayout.Space(8);
            GUILayout.Label("<b>Arguments</b>");
            DrawVariableMap(latest.Variables, latest);
            GUILayout.Space(8);
            GUILayout.Label("<b>Fields</b>");
            DrawVariableMap(latest.Fields, latest);
            GUILayout.Space(8);
            GUILayout.Label("<b>Statics</b>");
            DrawVariableMap(latest.Statics, latest);
            GUILayout.EndScrollView();
        }

        private void DrawVariableMap(Dictionary<string, object> map, TraceFrame frame)
        {
            if (map == null) return;
            foreach (var kv in map)
            {
                var key = kv.Key;
                var valStr = kv.Value != null ? kv.Value.ToString() : "null";
                var isWatched = _watchedVariables.Contains(key);
                var locked = _lockedVariables.Contains(key); // Keep locked for now, as it's used later in the original code
                var wasModified = frame != null && frame.ModifiedVariables != null && frame.ModifiedVariables.Contains(key);

                GUILayout.BeginHorizontal();
                
                var labelColor = isWatched ? "yellow" : "white";
                if (wasModified) labelColor = "orange";

                if (GUILayout.Button(string.Format("<color={0}>{1}</color>", labelColor, key), _lineButtonStyle, GUILayout.Width(170)))
                {
                    if (isWatched) _watchedVariables.Remove(key);
                    else _watchedVariables.Add(key);
                    MMLog.WriteDebug("[RuntimeDebuggerUI] " + (isWatched ? "Unwatched " : "Watched ") + key); // Added logging back
                }

                // Tooltip logic for key
                if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    _tooltipText = string.Format("<b>{0}</b>\n<i>Click to {1} variable</i>", key, isWatched ? "unwatch" : "watch");
                    _tooltipPos = Event.current.mousePosition;
                }

                var valDisplay = valStr;
                if (valDisplay.Length > 40) valDisplay = valDisplay.Substring(0, 37) + "...";
                GUILayout.Label(valDisplay);

                // Tooltip logic for value
                if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    _tooltipText = string.Format("<b>Value:</b>\n{0}", valStr);
                    _tooltipPos = Event.current.mousePosition;
                }

                // Re-adding the lock button as it was not explicitly removed by the diff,
                // and the diff only showed replacement of the TextField part.
                // If the intent was to remove it, this would need further instruction.
                if (GUILayout.Button(locked ? "Unlock" : "Lock", GUILayout.Width(70)))
                {
                    if (locked) _lockedVariables.Remove(key); else _lockedVariables.Add(key);
                    if (ExecutionTracer.Instance != null) ExecutionTracer.Instance.SetLockedVariables(_lockedVariables.ToArray());
                    MMLog.WriteInfo("[RuntimeDebuggerUI] " + (locked ? "Unlocked " : "Locked ") + key);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void ScrollToActiveLine(ref Vector2 scrollPos, int lineNo, float viewHeight)
        {
            if (lineNo <= 0) return;
            var targetY = (lineNo - 1) * _lineHeight;
            // Center it if possible
            scrollPos.y = targetY - (viewHeight / 2) + (_lineHeight / 2);
            if (scrollPos.y < 0) scrollPos.y = 0;
        }

        private void DrawSnapshotMode()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            var dir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModAPI"), "Snapshots");
            _scrollSnapshot = GUILayout.BeginScrollView(_scrollSnapshot, GUILayout.Height(200));
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "snapshot_*.bin");
                Array.Sort(files);
                Array.Reverse(files);
                for (var i = 0; i < files.Length; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Path.GetFileName(files[i]), GUILayout.Width(340));
                    if (GUILayout.Button("Inspect", GUILayout.Width(80))) _snapshotInfo = LoadSnapshot(files[i]);
                    GUILayout.EndHorizontal();
                }
            }
            else GUILayout.Label("No snapshot directory found.");
            GUILayout.EndScrollView();

            GUILayout.TextArea(_snapshotInfo, GUILayout.Height(80));
            if (_snapshotFrames.Count > 0)
            {
                var max = Mathf.Max(0, _snapshotFrames.Count - 1);
                var slide = GUILayout.HorizontalSlider(_snapshotIndex, 0f, max, GUILayout.Width(_windowRect.width - 120));
                var idx = Mathf.Clamp(Mathf.RoundToInt(slide), 0, max);
                if (idx != _snapshotIndex) SetSnapshotFrame(idx);
                GUILayout.Label("Frame " + (_snapshotIndex + 1) + "/" + _snapshotFrames.Count + " | " + (_snapshotFrame != null ? _snapshotFrame.Timestamp.ToString("HH:mm:ss.ffff") : string.Empty));

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(_windowRect.width * 0.62f));
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-Scroll Source");
                var viewH = 320;
                if (_autoScroll && _snapshotSourceLine != _lastScrollLine)
                {
                    ScrollToActiveLine(ref _scrollSnapshotSource, _snapshotSourceLine, viewH);
                    _lastScrollLine = _snapshotSourceLine;
                }
                _scrollSnapshotSource = GUILayout.BeginScrollView(_scrollSnapshotSource, GUILayout.Height(viewH));
                GUILayout.TextArea(BuildSnapshotSourceView(), _monoTextArea, GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                _scrollSnapshotVars = GUILayout.BeginScrollView(_scrollSnapshotVars, GUILayout.Height(320));
                GUILayout.TextArea(_snapshotVarsText, _monoTextArea, GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawBuildMode()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            if (_selectedMethod == null) { GUILayout.Label("Select a method in Live mode first."); GUILayout.EndVertical(); return; }
            if (_patchConfig.TargetType == null) { _patchConfig.TargetType = _selectedMethod.DeclaringType; _patchConfig.TargetMethod = _selectedMethod.Name; _patchConfig.Action = VisualTranspilerBuilder.PatchAction.InsertBefore; _patchConfig.TargetILOffset = _startOffset >= 0 ? _startOffset : 0; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Action", GUILayout.Width(50));
            GUILayout.Label(_patchConfig.Action.ToString(), GUILayout.Width(180));
            if (GUILayout.Button("Next Action", GUILayout.Width(120))) CycleAction();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Anchor Method", GUILayout.Width(100));
            _patchConfig.AnchorMethod = GUILayout.TextField(_patchConfig.AnchorMethod ?? string.Empty, GUILayout.Width(260));
            GUILayout.Label("Injection Method", GUILayout.Width(110));
            _patchConfig.InjectionMethod = GUILayout.TextField(_patchConfig.InjectionMethod ?? string.Empty, GUILayout.Width(260));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target ILOffset", GUILayout.Width(100));
            var text = GUILayout.TextField(_patchConfig.TargetILOffset.ToString(), GUILayout.Width(120));
            int parsed; if (int.TryParse(text, out parsed)) _patchConfig.TargetILOffset = parsed;
            if (GUILayout.Button("Generate", GUILayout.Width(120)))
            {
                _patchConfig.TargetType = _selectedMethod.DeclaringType; _patchConfig.TargetMethod = _selectedMethod.Name;
                _buildPatchCode = VisualTranspilerBuilder.GeneratePatchCode(_selectedMethod, _patchConfig);
                _buildPreview = VisualTranspilerBuilder.GenerateCSharpPreview(_selectedMethod, _patchConfig, _sourceText);
                MMLog.WriteDebug("[RuntimeDebuggerUI] Generated patch preview for " + _selectedMethod.Name);
            }
            GUILayout.EndHorizontal();
            _scrollBuild = GUILayout.BeginScrollView(_scrollBuild, GUILayout.Height(500));
            GUILayout.Label("<b>C# Preview</b>");
            GUILayout.TextArea(_buildPreview, _monoTextArea, GUILayout.Height(230));
            GUILayout.Label("<b>Generated Patch Code</b>");
            GUILayout.TextArea(_buildPatchCode, _monoTextArea, GUILayout.Height(240));
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void ResolveMethod()
        {
            _selectedMethod = null;
            if (string.IsNullOrEmpty(_typeName) || string.IsNullOrEmpty(_methodName)) { _statusText = "Type and Method are required."; return; }
            if (TryResolveMethod(_typeName, _methodName, out _selectedMethod))
            {
                _statusText = "Resolved: " + _selectedMethod.DeclaringType.FullName + "." + _selectedMethod.Name;
                _hasStartPoint = false; _startLine = -1; _startOffset = -1;
                _sourceText = SourceCacheManager.GetSource(_selectedMethod);
                _ilText = BuildILText(_selectedMethod);
                return;
            }
            _statusText = "Could not resolve method: " + _typeName + "." + _methodName;
            _sourceText = "// " + _statusText; _ilText = "<Method not resolved>";
            MMLog.WriteWarning("[RuntimeDebuggerUI] " + _statusText);
        }

        private bool TryResolveMethod(string typeName, string methodName, out MethodBase method)
        {
            method = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                Type resolved = null;
                try { var types = assemblies[i].GetTypes(); for (var t = 0; t < types.Length; t++) if (types[t].FullName == typeName || types[t].Name == typeName) { resolved = types[t]; break; } }
                catch { continue; }
                if (resolved == null) continue;
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var methods = resolved.GetMethods(flags);
                for (var m = 0; m < methods.Length; m++) if (methods[m].Name == methodName) { method = methods[m]; return true; }
            }
            return false;
        }

        private void UseLastTranspiledMethod()
        {
            var history = ModAPI.Harmony.TranspilerDebugger.History;
            if (history == null || history.Count == 0) { _statusText = "No transpiler history available yet."; return; }
            var snap = history[history.Count - 1];
            var id = !string.IsNullOrEmpty(snap.MethodName) ? snap.MethodName : snap.StepName;
            if (string.IsNullOrEmpty(id)) { _statusText = "No method identifier in last history entry."; return; }
            var lastDot = id.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= id.Length - 1) { _statusText = "Could not parse method identifier: " + id; return; }
            _typeName = id.Substring(0, lastDot); _methodName = id.Substring(lastDot + 1);
            ResolveMethod();
        }

        private string LoadSnapshot(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    var magic = new string(br.ReadChars(4));
                    var version = br.ReadUInt16();
                    var startTicks = br.ReadInt64();
                    var startOffset = br.ReadInt32();
                    var endOffset = br.ReadInt32();
                    var frameCount = br.ReadInt32();
                    var methodName = br.ReadString();

                    _snapshotFrames.Clear();
                    for (var i = 0; i < frameCount; i++)
                    {
                        _snapshotFrames.Add(TraceFrame.Read(br, version));
                    }

                    ResolveSnapshotMethod(methodName);
                    _snapshotSource = _snapshotMethod != null ? SourceCacheManager.GetSource(_snapshotMethod) : ("// Could not resolve method: " + methodName);
                    if (_snapshotFrames.Count > 0) SetSnapshotFrame(_snapshotFrames.Count - 1);
                    MMLog.WriteInfo("[RuntimeDebuggerUI] Loaded snapshot " + Path.GetFileName(path) + ", version=" + version + ", frames=" + frameCount);
                    return "File: " + Path.GetFileName(path) + "\nMagic: " + magic + "\nVersion: " + version + "\nStartTimeUtc: " + new DateTime(startTicks, DateTimeKind.Utc).ToString("u") + "\nMethod: " + methodName + "\nStartOffset: " + startOffset + "\nEndOffset: " + endOffset + "\nFrames: " + frameCount;
                }
            }
            catch (Exception ex) { MMLog.WriteError("[RuntimeDebuggerUI] Snapshot read failed: " + ex.Message); return "Failed to read snapshot: " + ex.Message; }
        }

        private void ResolveSnapshotMethod(string methodName)
        {
            _snapshotMethod = null;
            if (string.IsNullOrEmpty(methodName)) return;
            var lastDot = methodName.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= methodName.Length - 1) return;
            MethodBase resolved;
            if (TryResolveMethod(methodName.Substring(0, lastDot), methodName.Substring(lastDot + 1), out resolved)) _snapshotMethod = resolved;
        }

        private void SetSnapshotFrame(int index)
        {
            if (_snapshotFrames.Count == 0) { _snapshotFrame = null; _snapshotIndex = 0; _snapshotVarsText = string.Empty; _snapshotSourceLine = -1; return; }
            _snapshotIndex = Mathf.Clamp(index, 0, _snapshotFrames.Count - 1);
            _snapshotFrame = _snapshotFrames[_snapshotIndex];
            _snapshotSourceLine = _snapshotMethod != null ? SourceCacheManager.MapILToSourceLine(_snapshotMethod, _snapshotFrame.ILOffset) : -1;
            _snapshotVarsText = BuildFrameDump(_snapshotFrame);
        }

        private string BuildSnapshotSourceView()
        {
            if (string.IsNullOrEmpty(_snapshotSource) || _snapshotSourceLine <= 0) return _snapshotSource;
            var lines = _snapshotSource.Replace("\r\n", "\n").Split('\n');
            var sw = new StringWriter();
            for (var i = 0; i < lines.Length; i++) { sw.Write(i + 1 == _snapshotSourceLine ? "▶ " : "  "); sw.Write((i + 1).ToString("D4")); sw.Write(" "); sw.WriteLine(lines[i]); }
            return sw.ToString();
        }

        private static string BuildFrameDump(TraceFrame frame)
        {
            if (frame == null) return "No frame selected.";
            var sw = new StringWriter();
            sw.WriteLine("Timestamp: " + frame.Timestamp.ToString("u"));
            sw.WriteLine("Method: " + frame.MethodName);
            sw.WriteLine("ILOffset: IL_" + frame.ILOffset.ToString("X4"));
            sw.WriteLine("DurationMs: " + frame.ExecutionTimeMs.ToString("F3"));
            sw.WriteLine("MemoryMB: " + (frame.MemoryUsage / 1024L / 1024L));
            sw.WriteLine("WasModified: " + frame.WasModified);
            if (frame.ModifiedVariables != null && frame.ModifiedVariables.Count > 0) sw.WriteLine("ModifiedVariables: " + string.Join(", ", frame.ModifiedVariables.ToArray()));
            sw.WriteLine(); sw.WriteLine("[Variables]");
            if (frame.Variables != null) foreach (var kv in frame.Variables) sw.WriteLine(kv.Key + " = " + (kv.Value != null ? kv.Value.ToString() : "null"));
            sw.WriteLine(); sw.WriteLine("[Fields]");
            if (frame.Fields != null) foreach (var kv in frame.Fields) sw.WriteLine(kv.Key + " = " + (kv.Value != null ? kv.Value.ToString() : "null"));
            sw.WriteLine(); sw.WriteLine("[Statics]");
            if (frame.Statics != null) foreach (var kv in frame.Statics) sw.WriteLine(kv.Key + " = " + (kv.Value != null ? kv.Value.ToString() : "null"));
            return sw.ToString();
        }


        private static string BuildILText(MethodBase method)
        {
            try
            {
                var body = method.GetMethodBody();
                if (body == null) return "<No IL body available>";
                var bytes = body.GetILAsByteArray();
                if (bytes == null || bytes.Length == 0) return "<Empty IL body>";
                var sw = new StringWriter();
                for (var i = 0; i < bytes.Length; i++) { sw.Write(i.ToString("X4")); sw.Write(": "); sw.Write(bytes[i].ToString("X2")); sw.WriteLine(); }
                return sw.ToString();
            }
            catch (Exception ex) { return "<Failed to read IL: " + ex.Message + ">"; }
        }

        private void CycleAction()
        {
            var values = (VisualTranspilerBuilder.PatchAction[])Enum.GetValues(typeof(VisualTranspilerBuilder.PatchAction));
            var index = Array.IndexOf(values, _patchConfig.Action); if (index < 0) index = 0; index = (index + 1) % values.Length; _patchConfig.Action = values[index];
        }

        private void EnsureStyles()
        {
            if (_monoTextArea == null) { _monoTextArea = new GUIStyle(GUI.skin.textArea); _monoTextArea.wordWrap = false; }
            if (_lineButtonStyle == null) { _lineButtonStyle = new GUIStyle(GUI.skin.button); _lineButtonStyle.alignment = TextAnchor.MiddleRight; }
            if (_monoFont == null) { try { _monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Courier" }, 13); } catch { _monoFont = null; } }
            if (_monoFont != null) { 
                _monoTextArea.font = _monoFont; 
                _lineButtonStyle.font = _monoFont;
                if (_lineHeight <= 16f) _lineHeight = _monoTextArea.lineHeight > 0 ? _monoTextArea.lineHeight : 16f;
            }
        }
    }
}
