using Cortex.Core.Abstractions;
using UnityEngine;

namespace Cortex.Modules.Runtime
{
    public sealed class RuntimeToolsModule
    {
        public void Draw(IRuntimeToolBridge toolBridge)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Legacy runtime tools remain available while Cortex absorbs the IDE workflow.");
            if (GUILayout.Button("Toggle Runtime Inspector (F9)", GUILayout.Width(260f)))
            {
                toolBridge.ToggleRuntimeInspector();
            }
            if (GUILayout.Button("Toggle Runtime IL Inspector (F10)", GUILayout.Width(260f)))
            {
                toolBridge.ToggleIlInspector();
            }
            if (GUILayout.Button("Toggle UI Debugger (F11)", GUILayout.Width(260f)))
            {
                toolBridge.ToggleUiDebugger();
            }
            if (GUILayout.Button("Toggle Runtime Debugger (F7)", GUILayout.Width(260f)))
            {
                toolBridge.ToggleRuntimeDebugger();
            }
            GUILayout.EndVertical();
        }
    }
}
