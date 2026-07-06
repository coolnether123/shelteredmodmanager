using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Animation;
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private void DrawTooltipOverlayCore(float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            string tip = _animations.ResolveTooltip(GUI.tooltip);
            float alpha = _animations.GetTooltipAlpha(tip);
            if (string.IsNullOrEmpty(tip) || alpha <= 0.001f)
                return;

            GUIStyle tipStyle = _mutedTextStyle;
            if (tipStyle == null)
                return;
            tipStyle.wordWrap = true;
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            float maxWidth = 320f;
            Vector2 size = tipStyle.CalcSize(new GUIContent(tip));
            float width = Math.Min(maxWidth, size.x + 18f);
            float height = tipStyle.CalcHeight(new GUIContent(tip), width - 14f) + 10f;
            bool topChromeHover = mouse.y <= TopBarHeight + 4f;
            float x = topChromeHover
                ? mouse.x - (width * 0.35f)
                : Math.Min(scaledWidth - width - 6f, mouse.x + 16f);
            float y = topChromeHover
                ? TopBarHeight + 8f
                : Math.Min(scaledHeight - height - 6f, mouse.y + 20f);
            if (x < 6f) x = 6f;
            if (y < 6f) y = 6f;
            Rect tipRect = ClampAwayFromHud(new Rect(x, y, width, height), scaledWidth, scaledHeight, hudReserveRect);
            using (ScenarioUiGuiScope.Apply(alpha, tipRect, 1f))
            {
                DrawChromePanel(tipRect, _uiContext.Styles.Menu);
                GUI.Label(new Rect(tipRect.x + 7f, tipRect.y + 5f, tipRect.width - 14f, tipRect.height - 10f), tip, tipStyle);
            }
        }

    }
}
