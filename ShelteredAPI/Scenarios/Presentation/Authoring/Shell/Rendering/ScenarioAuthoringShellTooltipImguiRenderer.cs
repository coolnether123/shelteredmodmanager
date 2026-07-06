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
            Rect tipRect = topChromeHover
                ? BuildTopChromeTooltipRect(mouse, width, height, scaledWidth, scaledHeight, hudReserveRect)
                : BuildContentTooltipRect(mouse, width, height, scaledWidth, scaledHeight, hudReserveRect);
            using (ScenarioUiGuiScope.Apply(alpha, tipRect, 1f))
            {
                DrawChromePanel(tipRect, _uiContext.Styles.Menu);
                GUI.Label(new Rect(tipRect.x + 7f, tipRect.y + 5f, tipRect.width - 14f, tipRect.height - 10f), tip, tipStyle);
            }
        }

        private Rect BuildTopChromeTooltipRect(Vector2 mouse, float width, float height, float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            float x = mouse.x - (width * 0.35f);
            float y = TopBarHeight + 8f;
            return ClampTooltipRect(new Rect(x, y, width, height), scaledWidth, scaledHeight, hudReserveRect);
        }

        private Rect BuildContentTooltipRect(Vector2 mouse, float width, float height, float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            const float gap = 16f;
            Rect[] candidates = new[]
            {
                new Rect(mouse.x + gap, mouse.y - height - gap, width, height),
                new Rect(mouse.x - width - gap, mouse.y - height - gap, width, height),
                new Rect(mouse.x + gap, mouse.y + gap, width, height),
                new Rect(mouse.x - width - gap, mouse.y + gap, width, height)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Rect clamped = ClampTooltipRect(candidates[i], scaledWidth, scaledHeight, hudReserveRect);
                if (!clamped.Contains(mouse))
                    return clamped;
            }

            return ClampTooltipRect(new Rect(mouse.x + gap, mouse.y + gap, width, height), scaledWidth, scaledHeight, hudReserveRect);
        }

        private Rect ClampTooltipRect(Rect rect, float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            rect.x = Mathf.Clamp(rect.x, 6f, Math.Max(6f, scaledWidth - rect.width - 6f));
            rect.y = Mathf.Clamp(rect.y, 6f, Math.Max(6f, scaledHeight - rect.height - 6f));
            return ClampAwayFromHud(rect, scaledWidth, scaledHeight, hudReserveRect);
        }

    }
}
