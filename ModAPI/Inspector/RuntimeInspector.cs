using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ModAPI.Inspector
{
    // Simple in-game object explorer + inspector with click-to-select + bounds highlight
    public class RuntimeInspector : MonoBehaviour
    {
        private Rect _window = new Rect(20, 20, 800, 500);
        private bool _visible = false; // F9
        private bool _pickMode = false; // click to pick

        private Vector2 _scrollHierarchy;
        private Vector2 _scrollInspector;
        private Dictionary<Transform, bool> _expanded = new Dictionary<Transform, bool>();
        private List<Transform> _roots = new List<Transform>();
        private float _nextRefresh;
        private float _refreshInterval = 1.0f; // seconds

        private Transform _selected;

        private string _filter = string.Empty;

        private void Awake()
        {
            gameObject.name = "ModAPI.RuntimeInspector";
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
                _visible = !_visible;

            if (!_visible) return;

            if (Time.unscaledTime >= _nextRefresh)
            {
                _roots = HierarchyUtil.GetRootTransforms();
                _nextRefresh = Time.unscaledTime + _refreshInterval;
            }

            HandlePickInput();
        }

        private void HandlePickInput()
        {
            if (!_pickMode) return;
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUi() || IsOverInspectorWindow()) return;

                // Try 3D raycast first
                Camera cam = Camera.main;
                if (cam == null)
                {
                    var cams = Camera.allCameras;
                    if (cams != null && cams.Length > 0) cam = cams[0];
                }
                if (cam != null)
                {
                    var ray = cam.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit3D;
                    if (Physics.Raycast(ray, out hit3D, 10000f))
                    {
                        SetSelection(hit3D.collider != null ? hit3D.collider.transform : null);
                        return;
                    }
                }

                // Fallback: 2D
                try
                {
                    var wp = (Vector2)(cam != null ? cam.ScreenToWorldPoint(Input.mousePosition) : Input.mousePosition);
                    var hit2d = Physics2D.OverlapPoint(wp);
                    if (hit2d != null)
                    {
                        SetSelection(hit2d.transform);
                        return;
                    }
                }
                catch { }
            }
        }

        private bool IsPointerOverUi()
        {
            try
            {
                return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            }
            catch { return false; }
        }

        private bool IsOverInspectorWindow()
        {
            var mp = Input.mousePosition;
            var guiPos = new Vector2(mp.x, Screen.height - mp.y);
            return _window.Contains(guiPos);
        }

        private void SetSelection(Transform t)
        {
            _selected = t;
            BoundsHighlighter.Target = t;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            var prevColor = GUI.color;
            _window = GUI.Window(0x5151, _window, DrawWindow, "ModAPI Object Explorer (F9)");
            GUI.color = prevColor;
        }

        private void DrawWindow(int id)
        {
            // Header bar
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_pickMode ? "Pick: ON (click scene)" : "Pick: OFF", GUILayout.Width(160)))
                _pickMode = !_pickMode;

            GUILayout.Space(10);
            GUILayout.Label("Filter:", GUILayout.Width(40));
            _filter = GUILayout.TextField(_filter ?? string.Empty, GUILayout.MinWidth(120));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) _filter = string.Empty;

            GUILayout.FlexibleSpace();
            BoundsHighlighter.HighlightEnabled = GUILayout.Toggle(BoundsHighlighter.HighlightEnabled, "Highlight", GUILayout.Width(90));
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                _roots = HierarchyUtil.GetRootTransforms();
                GUI.FocusControl("");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            // Left: Hierarchy
            GUILayout.BeginVertical(GUILayout.Width(300));
            _scrollHierarchy = GUILayout.BeginScrollView(_scrollHierarchy, GUI.skin.box, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _roots.Count; i++)
            {
                var t = _roots[i];
                DrawTransformNode(t, 0);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Right: Inspector
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _scrollInspector = GUILayout.BeginScrollView(_scrollInspector, GUI.skin.box, GUILayout.ExpandHeight(true));

            if (_selected != null)
            {
                DrawInspector(_selected);
            }
            else
            {
                GUILayout.Label("No selection. Click a hierarchy item or enable Pick and click in the scene.");
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawTransformNode(Transform t, int depth)
        {
            if (t == null) return;
            if (!string.IsNullOrEmpty(_filter))
            {
                // basic filter: skip branches that don't contain text
                var name = t.name ?? string.Empty;
                if (name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // If any child matches, still show collapsed parent
                    bool show = false;
                    for (int i = 0; i < t.childCount; i++)
                    {
                        var c = t.GetChild(i);
                        if (c != null && (c.name ?? string.Empty).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            show = true; break;
                        }
                    }
                    if (!show) return;
                }
            }

            bool isExpanded = false;
            _expanded.TryGetValue(t, out isExpanded);

            GUILayout.BeginHorizontal();
            GUILayout.Space(12 * depth);

            string fold = t.childCount > 0 ? (isExpanded ? "▼" : "▶") : "·";
            if (t.childCount > 0)
            {
                if (GUILayout.Button(fold, GUILayout.Width(20)))
                    _expanded[t] = !isExpanded;
            }
            else
            {
                GUILayout.Label(fold, GUILayout.Width(20));
            }

            var style = (BoundsHighlighter.Target == t) ? HighlightStyle(GUI.skin.button) : GUI.skin.button;
            if (GUILayout.Button(FormatNodeLabel(t), style))
            {
                SetSelection(t);
            }
            GUILayout.EndHorizontal();

            if (isExpanded)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    DrawTransformNode(t.GetChild(i), depth + 1);
                }
            }
        }

        private GUIStyle _highlightCache;
        private GUIStyle HighlightStyle(GUIStyle baseStyle)
        {
            if (_highlightCache == null)
            {
                _highlightCache = new GUIStyle(baseStyle);
                _highlightCache.normal.textColor = new Color(1f, 0.9f, 0.2f, 1f);
            }
            return _highlightCache;
        }

        private string FormatNodeLabel(Transform t)
        {
            try
            {
                var go = t.gameObject;
                return string.Format("{0}  [{1}]", go.name, go.activeSelf ? "active" : "inactive");
            }
            catch { return t != null ? t.name : "(null)"; }
        }

        private void DrawInspector(Transform t)
        {
            var go = t.gameObject;
            GUILayout.Label("GameObject", GUI.skin.box);
            GUILayout.Label("Path: " + HierarchyUtil.GetTransformPath(t));
            GUILayout.Label("Name: " + go.name);
            GUILayout.Label("Tag: " + go.tag);
            GUILayout.Label("Layer: " + go.layer);
            GUILayout.Label("ActiveSelf: " + go.activeSelf + ", ActiveInHierarchy: " + go.activeInHierarchy);
            GUILayout.Space(6);

            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                GUILayout.Label(c.GetType().FullName, GUI.skin.box);
                DrawComponentFields(c);
                GUILayout.Space(4);
            }
        }

        private void DrawComponentFields(Component c)
        {
            var type = c.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            // Fields
            FieldInfo[] fields = new FieldInfo[0];
            PropertyInfo[] props = new PropertyInfo[0];
            try { fields = type.GetFields(flags); } catch (Exception ex) { MMLog.WarnOnce("RuntimeInspector.DrawComponentFields.GetFields", "Error getting fields: " + ex.Message); }
            try { props = type.GetProperties(flags); } catch (Exception ex) { MMLog.WarnOnce("RuntimeInspector.DrawComponentFields.GetProperties", "Error getting properties: " + ex.Message); }

            // Keep it readable, skip Unity's heavy internals
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (ShouldSkipMember(f)) continue;
                string val = SafeRead(() => f.GetValue(c));
                GUILayout.Label(f.Name + ": " + val);
            }

            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (!p.CanRead) continue;
                if (p.GetIndexParameters() != null && p.GetIndexParameters().Length > 0) continue; // skip indexers
                if (ShouldSkipMember(p)) continue;
                string val = SafeRead(() => p.GetValue(c, null));
                GUILayout.Label(p.Name + ": " + val);
            }
        }

        private bool ShouldSkipMember(MemberInfo mi)
        {
            var name = mi != null ? mi.Name : string.Empty;
            if (string.IsNullOrEmpty(name)) return true;
            // Skip very noisy Unity internals commonly present
            if (name == "rigidbody" || name == "camera" || name == "light") return true;
            return false;
        }

        private string SafeRead(Func<object> getter)
        {
            try
            {
                var v = getter != null ? getter() : null;
                if (v == null) return "null";
                // Prevent huge collections dumping
                var s = v.ToString();
                if (s == null) return "(toString null)";
                if (s.Length > 256) s = s.Substring(0, 256) + "…";
                return s;
            }
            catch (Exception ex)
            {
                return "<error: " + ex.GetType().Name + ">";
            }
        }
    }
}
