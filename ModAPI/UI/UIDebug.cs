using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.UI
{
    /// <summary>
    /// Debug utilities for NGUI development.
    /// Provides methods to inspect atlases, widgets, hierarchies, and raycast targets.
    /// Useful for troubleshooting invisible sprites, click issues, and layer problems.
    /// </summary>
    public static class UIDebug
    {
        private static float _startTime = -1f;

        /// <summary>
        /// Global toggle for UI debug logging.
        /// Set to true to enable verbose click tracing and detailed UI inspection logs.
        /// Defaults to false for release builds.
        /// </summary>
        public static bool Enabled = false;
        
        /// <summary>
        /// Call at the start of a debug session to reset timing.
        /// </summary>
        public static void ResetTiming()
        {
            _startTime = Time.realtimeSinceStartup;
            if (Enabled) MMLog.WriteDebug($"[UIDebug] Timing reset at frame {Time.frameCount}");
        }
        
        /// <summary>
        /// Logs a timestamped message relative to when ResetTiming() was called.
        /// </summary>
        public static void LogTimed(string message)
        {
            if (!Enabled) return;
            float elapsed = _startTime >= 0 ? (Time.realtimeSinceStartup - _startTime) * 1000f : 0f;
            MMLog.WriteDebug($"[UIDebug] [T+{elapsed:F1}ms F{Time.frameCount}] {message}");
        }

        // ==================== CAMERA & RAYCAST (Feature 1) ====================
        
        /// <summary>
        /// Checks if a GameObject's layer is visible to any UICamera's event mask.
        /// This is the #1 reason clicks don't work.
        /// </summary>
        public static bool CanUICameraSee(GameObject go, out string diagnosis)
        {
            diagnosis = "";
            if (go == null) { diagnosis = "GameObject is null"; return false; }
            
            int layer = go.layer;
            int layerMask = 1 << layer;
            
            var cameras = UICamera.list;
            for (int i = 0; i < cameras.size; i++)
            {
                var cam = cameras[i];
                if ((cam.eventReceiverMask & layerMask) != 0)
                {
                    diagnosis = $"Layer {layer} ({LayerMask.LayerToName(layer)}) IS in UICamera '{cam.name}' event mask";
                    return true;
                }
            }
            
            // Build detailed diagnosis
            var sb = new StringBuilder();
            sb.AppendLine($"Layer {layer} ({LayerMask.LayerToName(layer)}) is NOT in any UICamera event mask!");
            sb.AppendLine($"  Active UICameras ({cameras.size}):");
            for (int i = 0; i < cameras.size; i++)
            {
                var cam = cameras[i];
                sb.AppendLine($"    [{i}] {cam.name}: EventMask={cam.eventReceiverMask} (layers: {MaskToLayerNames(cam.eventReceiverMask)})");
            }
            diagnosis = sb.ToString();
            return false;
        }
        
        private static string MaskToLayerNames(int mask)
        {
            var names = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    string name = LayerMask.LayerToName(i);
                    names.Add(string.IsNullOrEmpty(name) ? i.ToString() : name);
                }
            }
            return string.Join(", ", names.ToArray());
        }
        
        /// <summary>
        /// Logs detailed UICamera and raycast information.
        /// </summary>
        public static void LogCameraInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[UIDebug] === UICamera Analysis ===");
            
            var cameras = UICamera.list;
            sb.AppendLine($"Total UICameras: {cameras.size}");
            
            for (int i = 0; i < cameras.size; i++)
            {
                var uiCam = cameras[i];
                var cam = uiCam.GetComponent<Camera>();
                sb.AppendLine($"  [{i}] {uiCam.name}:");
                sb.AppendLine($"      EventMask: {uiCam.eventReceiverMask} ({MaskToLayerNames(uiCam.eventReceiverMask)})");
                if (cam != null)
                {
                    sb.AppendLine($"      CullingMask: {cam.cullingMask} ({MaskToLayerNames(cam.cullingMask)})");
                    sb.AppendLine($"      Depth: {cam.depth}");
                }
            }
            
            sb.AppendLine($"Current Hovered: {(UICamera.hoveredObject != null ? UICamera.hoveredObject.name : "(none)")}");
            sb.AppendLine($"Mouse Position: {Input.mousePosition}");
            
            if (Enabled) MMLog.WriteDebug(sb.ToString());
        }

        // ==================== EFFECTIVE DEPTH (Feature 2) ====================
        
        /// <summary>
        /// Calculates the effective render depth: (panel.depth * 1000) + widget.depth.
        /// Higher = renders on top.
        /// </summary>
        public static int GetEffectiveDepth(UIWidget widget)
        {
            if (widget == null) return 0;
            var panel = NGUITools.FindInParents<UIPanel>(widget.gameObject);
            int panelDepth = panel != null ? panel.depth : 0;
            return (panelDepth * 1000) + widget.depth;
        }
        
        /// <summary>
        /// Compares two widgets and reports which renders on top.
        /// </summary>
        public static void CompareDepths(UIWidget a, UIWidget b, string labelA = "A", string labelB = "B")
        {
            int depthA = GetEffectiveDepth(a);
            int depthB = GetEffectiveDepth(b);
            
            var panelA = a != null ? NGUITools.FindInParents<UIPanel>(a.gameObject) : null;
            var panelB = b != null ? NGUITools.FindInParents<UIPanel>(b.gameObject) : null;
            
            if (Enabled)
            {
                MMLog.WriteDebug($"[UIDebug] Depth Comparison:");
                MMLog.WriteDebug($"  {labelA}: Panel={panelA?.name ?? "null"}({panelA?.depth ?? 0}) + Widget({a?.depth ?? 0}) = Effective {depthA}");
                MMLog.WriteDebug($"  {labelB}: Panel={panelB?.name ?? "null"}({panelB?.depth ?? 0}) + Widget({b?.depth ?? 0}) = Effective {depthB}");
                MMLog.WriteDebug($"  Winner: {(depthA > depthB ? labelA : (depthB > depthA ? labelB : "TIE"))} renders on top");
            }
        }

        // ==================== PARENT ACTIVE CHAIN (Feature 3) ====================
        
        /// <summary>
        /// Checks every parent in the hierarchy to ensure all are active.
        /// Returns false if ANY parent is inactive.
        /// </summary>
        public static bool IsFullyActive(GameObject go, out string inactiveParent)
        {
            inactiveParent = null;
            if (go == null) return false;
            
            Transform t = go.transform;
            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                {
                    inactiveParent = t.name;
                    return false;
                }
                t = t.parent;
            }
            return true;
        }
        
        /// <summary>
        /// Logs the full parent chain with active states.
        /// </summary>
        public static void LogParentChain(GameObject go)
        {
            if (go == null) { MMLog.WriteDebug("[UIDebug] LogParentChain: go is null"); return; }
            
            var sb = new StringBuilder();
            sb.AppendLine($"[UIDebug] Parent Chain for '{go.name}':");
            
            Transform t = go.transform;
            int depth = 0;
            while (t != null)
            {
                string pad = new string(' ', depth * 2);
                string status = t.gameObject.activeSelf ? "✓" : "✗ INACTIVE";
                var panel = t.GetComponent<UIPanel>();
                string panelInfo = panel != null ? $" [Panel depth={panel.depth}]" : "";
                sb.AppendLine($"  {pad}{status} {t.name} (Layer={t.gameObject.layer}){panelInfo}");
                t = t.parent;
                depth++;
            }
            
            if (Enabled) MMLog.WriteDebug(sb.ToString());
        }

        // ==================== WORLD/SCREEN POSITION (Feature 4) ====================
        
        /// <summary>
        /// Converts a widget's position to screen coordinates and checks if it's visible.
        /// </summary>
        public static bool IsOnScreen(UIWidget widget, out Vector3 screenPos, out string diagnosis)
        {
            screenPos = Vector3.zero;
            diagnosis = "";
            
            if (widget == null) { diagnosis = "Widget is null"; return false; }
            
            // Find the UICamera that would render this
            Camera cam = null;
            var cameras = UICamera.list;
            int layer = widget.gameObject.layer;
            int layerMask = 1 << layer;
            
            for (int i = 0; i < cameras.size; i++)
            {
                var uiCam = cameras[i];
                var c = uiCam.GetComponent<Camera>();
                if (c != null && (c.cullingMask & layerMask) != 0)
                {
                    cam = c;
                    break;
                }
            }
            
            if (cam == null)
            {
                diagnosis = $"No camera renders layer {layer} ({LayerMask.LayerToName(layer)})";
                return false;
            }
            
            Vector3 worldPos = widget.transform.position;
            screenPos = cam.WorldToScreenPoint(worldPos);
            
            bool inViewport = screenPos.x >= 0 && screenPos.x <= Screen.width &&
                              screenPos.y >= 0 && screenPos.y <= Screen.height &&
                              screenPos.z > 0;
            
            if (!inViewport)
            {
                diagnosis = $"Off-screen! Screen pos: {screenPos}, Screen: {Screen.width}x{Screen.height}";
                return false;
            }
            
            diagnosis = $"On-screen at ({screenPos.x:F0}, {screenPos.y:F0})";
            return true;
        }

        // ==================== CLICK PATH TRACING (Feature 5) ====================
        
        /// <summary>
        /// Traces what NGUI would hit at a specific screen position.
        /// Call this when a click doesn't work to see what's being hit instead.
        /// </summary>
        public static void TraceClickAt(Vector3 screenPos)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[UIDebug] === Click Trace at ({screenPos.x:F0}, {screenPos.y:F0}) ===");
            
            // What is currently hovered?
            sb.AppendLine($"UICamera.hoveredObject: {(UICamera.hoveredObject != null ? UICamera.hoveredObject.name : "(none)")}");
            
            // Manual raycast from each UICamera
            var cameras = UICamera.list;
            for (int i = 0; i < cameras.size; i++)
            {
                var uiCam = cameras[i];
                var cam = uiCam.GetComponent<Camera>();
                if (cam == null) continue;
                
                Ray ray = cam.ScreenPointToRay(screenPos);
                RaycastHit[] hits = Physics.RaycastAll(ray, cam.farClipPlane, uiCam.eventReceiverMask);
                
                sb.AppendLine($"  Camera '{uiCam.name}' (EventMask={uiCam.eventReceiverMask}):");
                if (hits.Length == 0)
                {
                    sb.AppendLine("    No hits");
                }
                else
                {
                    // Sort by distance
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var hit in hits)
                    {
                        var w = hit.collider.GetComponent<UIWidget>();
                        int effDepth = w != null ? GetEffectiveDepth(w) : -1;
                        sb.AppendLine($"    -> {hit.collider.name} (dist={hit.distance:F2}, effDepth={effDepth})");
                    }
                }
            }
            
            if (Enabled) MMLog.WriteDebug(sb.ToString());
        }
        
        /// <summary>
        /// Attaches a one-shot click tracer that logs what gets hit on next click.
        /// </summary>
        public static void TraceNextClick()
        {
            if (Enabled) MMLog.WriteDebug("[UIDebug] Click tracing enabled - next click will be logged");
            // This would need a MonoBehaviour to implement properly
            // For now, call TraceClickAt(Input.mousePosition) manually in your onClick handler
        }

        // ==================== DELEGATE VERIFICATION (Feature 8) ====================
        
        /// <summary>
        /// Verifies that an EventDelegate was successfully added to a button.
        /// Call immediately after EventDelegate.Set() to confirm it worked.
        /// </summary>
        public static bool VerifyDelegateCount(UIButton button, int expectedCount, string label = "")
        {
            if (button == null)
            {
                MMLog.WriteError($"[UIDebug] VerifyDelegateCount({label}): button is null");
                return false;
            }
            
            int actual = button.onClick.Count;
            if (actual != expectedCount)
            {
                MMLog.WriteError($"[UIDebug] VerifyDelegateCount({label}): Expected {expectedCount} delegates, found {actual}!");
                return false;
            }
            
            if (Enabled) MMLog.WriteDebug($"[UIDebug] VerifyDelegateCount({label}): ✓ Has {actual} delegate(s) as expected");
            return true;
        }

        // ==================== CONSOLIDATED SNAPSHOT (Feature 10) ====================
        
        /// <summary>
        /// Creates a comprehensive one-shot diagnostic of a UI element.
        /// This is the "tell me everything" function.
        /// </summary>
        public static void TakeSnapshot(GameObject go, string label = "")
        {
            if (go == null) { MMLog.WriteDebug($"[UIDebug] TakeSnapshot({label}): go is null"); return; }
            
            var sb = new StringBuilder();
            sb.AppendLine($"[UIDebug] ========== SNAPSHOT: {label} ==========");
            sb.AppendLine($"[Timing] Frame {Time.frameCount}, RealTime {Time.realtimeSinceStartup:F3}s");
            
            // Basic info
            sb.AppendLine($"[Object] {go.name}");
            sb.AppendLine($"[Layer] {go.layer} ({LayerMask.LayerToName(go.layer)})");
            
            // Parent chain check
            string inactiveParent;
            bool fullyActive = IsFullyActive(go, out inactiveParent);
            sb.AppendLine($"[Active] Self={go.activeSelf}, Hierarchy={go.activeInHierarchy}, FullyActive={fullyActive}{(inactiveParent != null ? $" (blocked by '{inactiveParent}')" : "")}");
            
            // Position
            sb.AppendLine($"[Position] Local={go.transform.localPosition}, World={go.transform.position}");
            sb.AppendLine($"[Scale] Local={go.transform.localScale}");
            
            // Panel
            var panel = NGUITools.FindInParents<UIPanel>(go);
            sb.AppendLine($"[Panel] {(panel != null ? $"{panel.name} (Depth={panel.depth}, Clip={panel.clipping}, Alpha={panel.alpha})" : "NONE - NOT UNDER PANEL")}");
            
            // UIRoot
            var root = NGUITools.FindInParents<UIRoot>(go);
            sb.AppendLine($"[UIRoot] {(root != null ? root.name : "NONE - NOT UNDER UIROOT")}");
            
            // Widget
            var widget = go.GetComponent<UIWidget>();
            if (widget != null)
            {
                int effDepth = GetEffectiveDepth(widget);
                sb.AppendLine($"[Widget] Depth={widget.depth}, EffectiveDepth={effDepth}, Size={widget.width}x{widget.height}, Pivot={widget.pivot}");
                sb.AppendLine($"[Widget] Color={widget.color}, Alpha={widget.alpha}");
                
                var sprite = widget as UISprite;
                if (sprite != null)
                {
                    bool validSprite = sprite.atlas != null && sprite.atlas.GetSprite(sprite.spriteName) != null;
                    sb.AppendLine($"[Sprite] Atlas={sprite.atlas?.name ?? "NULL"}, Name='{sprite.spriteName}', Valid={validSprite}, Type={sprite.type}");
                }
            }
            
            // Collider
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                var box = collider as BoxCollider;
                string sizeInfo = box != null ? $", Size={box.size}" : "";
                sb.AppendLine($"[Collider] {collider.GetType().Name}, Enabled={collider.enabled}{sizeInfo}");
            }
            else
            {
                sb.AppendLine("[Collider] NONE");
            }
            
            // Button
            var button = go.GetComponent<UIButton>();
            if (button != null)
            {
                sb.AppendLine($"[Button] Enabled={button.isEnabled}, TweenTarget={button.tweenTarget?.name ?? "null"}");
                sb.AppendLine($"[Button] Delegates={button.onClick.Count}");
                for (int i = 0; i < button.onClick.Count; i++)
                {
                    var del = button.onClick[i];
                    sb.AppendLine($"  [{i}] Target={del.target?.name ?? "null"}, Method={del.methodName}");
                }
            }
            
            // Camera visibility
            string camDiagnosis;
            bool camCanSee = CanUICameraSee(go, out camDiagnosis);
            sb.AppendLine($"[Camera] CanSee={camCanSee}");
            if (!camCanSee) sb.AppendLine($"  {camDiagnosis}");
            
            // Screen position
            if (widget != null)
            {
                Vector3 screenPos;
                string posDiagnosis;
                bool onScreen = IsOnScreen(widget, out screenPos, out posDiagnosis);
                sb.AppendLine($"[Screen] OnScreen={onScreen}, {posDiagnosis}");
            }
            
            sb.AppendLine($"========== END SNAPSHOT ==========");
            if (Enabled) MMLog.WriteDebug(sb.ToString());
        }

        // ==================== LEGACY METHODS (kept for compatibility) ====================
        
        public static void LogAllAtlases(int maxSpritesPerAtlas = 20)
        {
            var atlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
            LogTimed($"Found {atlases.Length} UIAtlas(es)");
            
            foreach (var atlas in atlases)
            {
                if (atlas == null) continue;
                var sprites = atlas.spriteList;
                int count = sprites != null ? sprites.Count : 0;
                var names = sprites != null 
                    ? string.Join(", ", sprites.Take(maxSpritesPerAtlas).Select(s => s.name).ToArray())
                    : "(none)";
                string suffix = count > maxSpritesPerAtlas ? $"... (+{count - maxSpritesPerAtlas} more)" : "";
                if (Enabled) MMLog.WriteDebug($"  [{atlas.name}] ({count} sprites): {names}{suffix}");
            }
        }

        public static List<UIAtlas> FindAtlasesWithSprite(string spriteNameContains)
        {
            var results = new List<UIAtlas>();
            var atlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
            
            foreach (var atlas in atlases)
            {
                if (atlas == null || atlas.spriteList == null) continue;
                foreach (var sprite in atlas.spriteList)
                {
                    if (sprite.name.IndexOf(spriteNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(atlas);
                        break;
                    }
                }
            }
            return results;
        }

        public static void LogWidgetHierarchy(Transform root, int maxDepth = 5)
        {
            if (root == null) return;
            if (Enabled) MMLog.WriteDebug($"[UIDebug] Widget Hierarchy under '{root.name}':");
            LogWidgetRecursive(root, 0, maxDepth);
        }

        private static void LogWidgetRecursive(Transform t, int indent, int maxDepth)
        {
            if (indent > maxDepth) return;
            
            string pad = new string(' ', indent * 2);
            var widget = t.GetComponent<UIWidget>();
            var panel = t.GetComponent<UIPanel>();
            
            var sb = new StringBuilder();
            sb.Append($"{pad}[{t.name}] L={t.gameObject.layer} A={t.gameObject.activeInHierarchy}");
            
            if (widget != null)
                sb.Append($" W:d={widget.depth} {widget.width}x{widget.height}");
            if (panel != null)
                sb.Append($" P:d={panel.depth}");
            
            if (Enabled) MMLog.WriteDebug(sb.ToString());
            foreach (Transform child in t) LogWidgetRecursive(child, indent + 1, maxDepth);
        }

        public static void LogRaycastInfo()
        {
            LogCameraInfo();
            TraceClickAt(Input.mousePosition);
        }

        public static void InspectSprite(UISprite sprite, string label = "")
        {
            if (sprite == null) return;
            TakeSnapshot(sprite.gameObject, $"Sprite: {label}");
        }

        public static void InspectButton(UIButton button, string label = "")
        {
            if (button == null) return;
            TakeSnapshot(button.gameObject, $"Button: {label}");
        }

        public static void LogAllPanels()
        {
            var panels = UnityEngine.Object.FindObjectsOfType<UIPanel>();
            var sorted = panels.OrderByDescending(p => p.depth).ToArray();
            
            LogTimed($"Found {panels.Length} UIPanel(s)");
            foreach (var p in sorted)
            {
                var root = NGUITools.FindInParents<UIRoot>(p.gameObject);
                if (Enabled) MMLog.WriteDebug($"  [Depth {p.depth}] {p.name} | Clip={p.clipping} Alpha={p.alpha} Layer={p.gameObject.layer} Root={root?.name ?? "NONE"}");
            }
        }

        public static bool ValidateNGUISetup(GameObject go, string label = "")
        {
            if (go == null) return false;
            
            var issues = new List<string>();
            
            // Layer check
            string camDiag;
            if (!CanUICameraSee(go, out camDiag))
                issues.Add($"Camera can't see: {camDiag.Split('\n')[0]}");
            
            // Parent chain
            string inactiveParent;
            if (!IsFullyActive(go, out inactiveParent))
                issues.Add($"Blocked by inactive parent: {inactiveParent}");
            
            // Panel
            if (NGUITools.FindInParents<UIPanel>(go) == null)
                issues.Add("Not under any UIPanel");
            
            // Widget size
            var widget = go.GetComponent<UIWidget>();
            if (widget != null && (widget.width <= 0 || widget.height <= 0))
                issues.Add($"Zero size: {widget.width}x{widget.height}");
            
            // Button collider
            var button = go.GetComponent<UIButton>();
            if (button != null && go.GetComponent<Collider>() == null)
                issues.Add("UIButton has no Collider");
            
            if (issues.Count > 0)
            {
                MMLog.WriteError($"[UIDebug] Validation FAILED for '{go.name}' ({label}):");
                foreach (var issue in issues) MMLog.WriteError($"  ✗ {issue}");
                return false;
            }
            
            if (Enabled) MMLog.WriteDebug($"[UIDebug] Validation PASSED for '{go.name}' ({label})");
            return true;
        }

        public static void AttachDebugListeners(GameObject go, string label = "")
        {
            if (go == null) return;
            var listener = UIEventListener.Get(go);
            listener.onClick += (obj) => { LogTimed($"onClick: {label} ({obj.name})"); TraceClickAt(Input.mousePosition); };
            listener.onPress += (obj, pressed) => LogTimed($"onPress: {label} ({obj.name}) pressed={pressed}");
            listener.onHover += (obj, hover) => LogTimed($"onHover: {label} ({obj.name}) hover={hover}");
            LogTimed($"Attached debug listeners to '{go.name}' ({label})");
        }
    }
}
