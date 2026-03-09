using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using UnityEngine;

namespace Cortex.Modules.Runtime
{
    public sealed class RuntimeToolsModule
    {
        public void Draw(IRuntimeToolBridge toolBridge, CortexShellState state)
        {
            var tools = toolBridge != null ? toolBridge.GetTools() : new List<RuntimeToolStatus>();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Legacy runtime tools remain available while Cortex absorbs the IDE workflow.");
            if (tools.Count == 0)
            {
                GUILayout.Label("No runtime tools are registered for this host.");
                GUILayout.EndVertical();
                return;
            }

            for (var i = 0; i < tools.Count; i++)
            {
                DrawTool(toolBridge, state, tools[i]);
            }
            GUILayout.EndVertical();
        }

        private static void DrawTool(IRuntimeToolBridge toolBridge, CortexShellState state, RuntimeToolStatus tool)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(tool.DisplayName + (string.IsNullOrEmpty(tool.ShortcutHint) ? string.Empty : " (" + tool.ShortcutHint + ")"));
            GUILayout.Label(tool.Description ?? string.Empty);

            if (!tool.IsAvailable)
            {
                GUILayout.Label(string.IsNullOrEmpty(tool.UnavailableReason) ? "This tool is unavailable in the current host." : tool.UnavailableReason);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label("Status: " + (tool.IsActive ? "Active" : "Inactive"));
            if (GUILayout.Button(tool.IsActive ? "Disable" : "Enable", GUILayout.Width(120f)))
            {
                string statusMessage;
                if (toolBridge != null && toolBridge.Execute(tool.ToolId, out statusMessage))
                {
                    state.StatusMessage = statusMessage;
                }
                else
                {
                    state.StatusMessage = statusMessage;
                }
            }
            GUILayout.EndVertical();
        }
    }
}
