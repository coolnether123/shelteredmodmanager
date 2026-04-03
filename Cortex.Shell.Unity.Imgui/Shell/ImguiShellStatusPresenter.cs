using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Presentation.Models;
using UnityEngine;

namespace Cortex.Shell.Unity.Imgui
{
    internal sealed class ImguiShellStatusPresenter
    {
        private readonly CortexShellState _state;
        private readonly Func<string, object, bool> _executeCommandFunc;

        public ImguiShellStatusPresenter(CortexShellState state, Func<string, object, bool> executeCommandFunc)
        {
            _state = state;
            _executeCommandFunc = executeCommandFunc;
        }

        public void DrawStatusStrip(WorkbenchPresentationSnapshot snapshot, GUIStyle sectionStyle, GUIStyle statusStyle, GUIStyle captionStyle)
        {
            GUILayout.BeginHorizontal(sectionStyle, GUILayout.Height(22f));
            DrawStatusItems(snapshot != null ? snapshot.LeftStatusItems : null, captionStyle);
            GUILayout.FlexibleSpace();

            var msg = string.IsNullOrEmpty(_state.StatusMessage) ? (snapshot != null ? snapshot.RendererSummary ?? "Ready" : "Ready") : _state.StatusMessage;
            GUILayout.Label(msg, statusStyle);
            GUILayout.FlexibleSpace();

            var themeId = snapshot != null ? snapshot.ActiveThemeId : ((_state.Settings != null ? _state.Settings.ThemeId : null) ?? "vs-dark");
            GUILayout.Label(themeId, captionStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(8f);

            DrawStatusItems(snapshot != null ? snapshot.RightStatusItems : null, captionStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawStatusItems(IList<StatusItemContribution> items, GUIStyle captionStyle)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var prevColor = GUI.contentColor;
                GUI.contentColor = RuntimeLogVisuals.GetAccentColor(string.IsNullOrEmpty(item.Severity) ? "Info" : item.Severity);
                var label = item.Text ?? item.ItemId ?? "Status";
                if (!string.IsNullOrEmpty(item.CommandId) && GUILayout.Button(label, GUILayout.Width(Mathf.Max(90f, label.Length * 7f))))
                {
                    _executeCommandFunc(item.CommandId, null);
                }
                else
                {
                    GUILayout.Label(label, captionStyle);
                }

                GUI.contentColor = prevColor;
                GUILayout.Space(6f);
            }
        }
    }
}
