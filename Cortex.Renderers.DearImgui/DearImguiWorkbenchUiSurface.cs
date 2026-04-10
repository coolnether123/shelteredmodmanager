using System;
using System.Collections.Generic;
using System.Text;
using Cortex.Plugins.Abstractions;
using Cortex.Renderers.DearImgui.Native;

namespace Cortex.Renderers.DearImgui
{
    public sealed class DearImguiWorkbenchUiSurface : IWorkbenchUiSurface
    {
        private readonly Dictionary<string, byte[]> _inputBuffers = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public string DrawSearchToolbar(string label, string draftQuery, float height, bool expandWidth)
        {
            var query = draftQuery ?? string.Empty;
            DearImguiNative.igTextUnformatted(label ?? "Search", IntPtr.Zero);
            DearImguiNative.igSameLine(0f, 12f);
            if (DrawInputText("##search." + (label ?? "search"), ref query, 1024))
            {
                draftQuery = query;
            }

            DearImguiNative.igSameLine(0f, 8f);
            if (DearImguiNative.igButton("Clear", new DearImguiNative.ImVec2(0f, 0f)))
            {
                query = string.Empty;
            }

            return query;
        }

        public bool DrawNavigationGroupHeader(string title, bool isActive, bool isExpanded)
        {
            return DearImguiNative.igSelectable_Bool((isExpanded ? "v " : "> ") + (title ?? "Group"), isActive, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f));
        }

        public bool DrawNavigationItem(string title, bool isSelected, float indent)
        {
            if (indent > 0f)
            {
                DearImguiNative.igIndent(indent);
            }

            var clicked = DearImguiNative.igSelectable_Bool(title ?? string.Empty, isSelected, DearImguiNative.ImGuiSelectableFlags.None, new DearImguiNative.ImVec2(0f, 0f));
            if (indent > 0f)
            {
                DearImguiNative.igUnindent(indent);
            }

            return clicked;
        }

        public void DrawCollapsedNavigationItem(string title, float indent)
        {
            if (indent > 0f)
            {
                DearImguiNative.igIndent(indent);
            }

            DearImguiNative.igTextUnformatted(title ?? string.Empty, IntPtr.Zero);
            if (indent > 0f)
            {
                DearImguiNative.igUnindent(indent);
            }
        }

        public void DrawSectionHeader(string title, string description)
        {
            DearImguiNative.igTextUnformatted(title ?? string.Empty, IntPtr.Zero);
            if (!string.IsNullOrEmpty(description))
            {
                DearImguiNative.igTextWrapped(description);
            }

            DearImguiNative.igSeparator();
        }

        public void DrawSectionPanel(string title, Action drawBody)
        {
            if (!string.IsNullOrEmpty(title))
            {
                DearImguiNative.igTextUnformatted(title, IntPtr.Zero);
            }

            if (drawBody != null)
            {
                drawBody();
            }

            DearImguiNative.igSeparator();
        }

        public void DrawPopupMenuPanel(float width, Action drawBody)
        {
            if (DearImguiNative.igBeginChild_Str("popup.panel", new DearImguiNative.ImVec2(width > 0f ? width : 220f, 0f), true, DearImguiNative.ImGuiWindowFlags.None))
            {
                if (drawBody != null)
                {
                    drawBody();
                }
            }

            DearImguiNative.igEndChild();
        }

        public void BeginPropertyRow()
        {
            DearImguiNative.igSeparator();
        }

        public void EndPropertyRow()
        {
        }

        public void DrawPropertyLabelColumn(string title, string description)
        {
            DearImguiNative.igTextUnformatted(title ?? string.Empty, IntPtr.Zero);
            if (!string.IsNullOrEmpty(description))
            {
                DearImguiNative.igTextWrapped(description);
            }
        }

        private bool DrawInputText(string id, ref string value, int capacity)
        {
            var key = id ?? string.Empty;
            var buffer = GetOrCreateBuffer(key, value, capacity);
            var changed = DearImguiNative.igInputText(key, buffer, (uint)buffer.Length, DearImguiNative.ImGuiInputTextFlags.None, IntPtr.Zero, IntPtr.Zero);
            if (changed)
            {
                value = DecodeBuffer(buffer);
            }
            else if (!string.Equals(value ?? string.Empty, DecodeBuffer(buffer), StringComparison.Ordinal))
            {
                CopyToBuffer(buffer, value);
            }

            return changed;
        }

        private byte[] GetOrCreateBuffer(string key, string value, int capacity)
        {
            byte[] buffer;
            if (!_inputBuffers.TryGetValue(key, out buffer) || buffer == null || buffer.Length < capacity)
            {
                buffer = new byte[Math.Max(32, capacity)];
                _inputBuffers[key] = buffer;
                CopyToBuffer(buffer, value);
            }

            return buffer;
        }

        private static void CopyToBuffer(byte[] buffer, string value)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            Array.Clear(buffer, 0, buffer.Length);
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            var count = Math.Min(bytes.Length, buffer.Length - 1);
            Array.Copy(bytes, buffer, count);
            buffer[count] = 0;
        }

        private static string DecodeBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return string.Empty;
            }

            var count = 0;
            while (count < buffer.Length && buffer[count] != 0)
            {
                count++;
            }

            return count > 0 ? Encoding.UTF8.GetString(buffer, 0, count) : string.Empty;
        }
    }
}
