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
        private void DrawTooltipOverlayCore(float scaledWidth, float scaledHeight, Rect hudReserveRect, Rect contentRect)
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
                ? BuildTopChromeTooltipRect(mouse, width, height, scaledWidth, scaledHeight, hudReserveRect, contentRect)
                : BuildContentTooltipRect(mouse, width, height, scaledWidth, scaledHeight, hudReserveRect, contentRect);
            using (ScenarioUiGuiScope.Apply(alpha, tipRect, 1f))
            {
                DrawChromePanel(tipRect, _uiContext.Styles.Menu);
                GUI.Label(new Rect(tipRect.x + 7f, tipRect.y + 5f, tipRect.width - 14f, tipRect.height - 10f), tip, tipStyle);
            }
        }

        private Rect BuildTopChromeTooltipRect(Vector2 mouse, float width, float height, float scaledWidth, float scaledHeight, Rect hudReserveRect, Rect contentRect)
        {
            Rect bounds = BuildTooltipBounds(contentRect, scaledWidth, scaledHeight);
            Rect avoidRect = BuildTooltipAvoidanceRect(mouse, bounds, scaledWidth, true);
            return PlaceTooltipAroundAvoidance(avoidRect, width, height, bounds, scaledWidth, scaledHeight, hudReserveRect);
        }

        private Rect BuildContentTooltipRect(Vector2 mouse, float width, float height, float scaledWidth, float scaledHeight, Rect hudReserveRect, Rect contentRect)
        {
            Rect bounds = BuildTooltipBounds(contentRect, scaledWidth, scaledHeight);
            Rect avoidRect = BuildTooltipAvoidanceRect(mouse, bounds, scaledWidth, false);
            return PlaceTooltipAroundAvoidance(avoidRect, width, height, bounds, scaledWidth, scaledHeight, hudReserveRect);
        }

        private Rect PlaceTooltipAroundAvoidance(Rect avoidRect, float width, float height, Rect bounds, float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            const float gap = 10f;
            float centerX = avoidRect.x + (avoidRect.width * 0.5f);
            float centerY = avoidRect.y + (avoidRect.height * 0.5f);
            Rect[] candidates = new[]
            {
                new Rect(centerX - (width * 0.5f), avoidRect.yMax + gap, width, height),
                new Rect(centerX - (width * 0.5f), avoidRect.y - height - gap, width, height),
                new Rect(avoidRect.xMax + gap, centerY - (height * 0.5f), width, height),
                new Rect(avoidRect.x - width - gap, centerY - (height * 0.5f), width, height)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Rect clamped = ClampTooltipRect(candidates[i], bounds, scaledWidth, scaledHeight, hudReserveRect);
                if (!clamped.Overlaps(avoidRect))
                    return clamped;
            }

            Rect fallback = new Rect(centerX - (width * 0.5f), avoidRect.yMax + gap, width, height);
            Rect fallbackClamped = ClampTooltipRect(fallback, bounds, scaledWidth, scaledHeight, hudReserveRect);
            if (fallbackClamped.Overlaps(avoidRect))
            {
                float belowY = Math.Min(bounds.yMax - height, avoidRect.yMax + gap);
                float aboveY = Math.Max(bounds.y, avoidRect.y - height - gap);
                fallbackClamped.y = belowY + height <= bounds.yMax && belowY >= avoidRect.yMax
                    ? belowY
                    : aboveY;
                fallbackClamped.x = Mathf.Clamp(fallbackClamped.x, bounds.x, Math.Max(bounds.x, bounds.xMax - width));
            }
            return fallbackClamped;
        }

        private Rect BuildTooltipBounds(Rect contentRect, float scaledWidth, float scaledHeight)
        {
            Rect fallback = new Rect(Margin, TopBarHeight + Gutter, Math.Max(120f, scaledWidth - (Margin * 2f)), Math.Max(120f, scaledHeight - TopBarHeight - StatusHeight - (Gutter * 2f)));
            Rect bounds = contentRect.width > 0f && contentRect.height > 0f ? contentRect : fallback;
            return new Rect(
                bounds.x + 6f,
                bounds.y + 6f,
                Math.Max(120f, bounds.width - 12f),
                Math.Max(80f, bounds.height - 12f));
        }

        private Rect BuildTooltipAvoidanceRect(Vector2 mouse, Rect bounds, float scaledWidth, bool topChrome)
        {
            if (topChrome)
            {
                float topWidth = Mathf.Clamp(scaledWidth * 0.32f, 300f, 520f);
                return new Rect(
                    Mathf.Clamp(mouse.x - (topWidth * 0.5f), 0f, Math.Max(0f, scaledWidth - topWidth)),
                    0f,
                    topWidth,
                    TopBarHeight + 8f);
            }

            float width = Math.Min(Math.Max(520f, bounds.width * 0.58f), Math.Max(220f, bounds.width));
            float height = 168f;
            return new Rect(
                Mathf.Clamp(mouse.x - (width * 0.5f), bounds.x, Math.Max(bounds.x, bounds.xMax - width)),
                Mathf.Clamp(mouse.y - 56f, bounds.y, Math.Max(bounds.y, bounds.yMax - height)),
                width,
                height);
        }

        private Rect ClampTooltipRect(Rect rect, Rect bounds, float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            rect.x = Mathf.Clamp(rect.x, bounds.x, Math.Max(bounds.x, bounds.xMax - rect.width));
            rect.y = Mathf.Clamp(rect.y, bounds.y, Math.Max(bounds.y, bounds.yMax - rect.height));
            if (rect.Overlaps(hudReserveRect))
            {
                float shiftedLeft = hudReserveRect.x - rect.width - Gutter;
                if (shiftedLeft >= bounds.x)
                    rect.x = shiftedLeft;
                else
                    rect.y = Math.Min(bounds.yMax - rect.height, hudReserveRect.yMax + Gutter);
            }
            rect.x = Mathf.Clamp(rect.x, bounds.x, Math.Max(bounds.x, bounds.xMax - rect.width));
            rect.y = Mathf.Clamp(rect.y, bounds.y, Math.Max(bounds.y, bounds.yMax - rect.height));
            return rect;
        }

    }
}
