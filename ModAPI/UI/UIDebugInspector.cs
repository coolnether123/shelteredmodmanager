using System;
using System.Text;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.UI
{
    public class UIDebugInspector : MonoBehaviour
    {
        private bool _active = true;
        private GameObject _lastHover;
        
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _active = !_active;
                MMLog.WriteInfo($"[UIDebug] Inspector {(_active ? "Enabled" : "Disabled")}");
            }

            if (_active && Input.GetMouseButtonDown(0))
            {
                if (_lastHover != null)
                {
                    DumpWidgetInfo(_lastHover);
                }
            }
        }

        public void OnGUI()
        {
            if (!_active) return;

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
            GUI.Label(new Rect(x + 5, y + 25, w - 10, h - 30), sb.ToString());
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

        private string GetPath(Transform t)
        {
            return t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
        }
    }
}
