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
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float PopupMenuRowHeight = 32f;
        private const float PopupMenuPadding = 8f;

        private void DrawContextMenuCore(Rect rect, ScenarioAuthoringContextMenuModel menu)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
            GUILayout.BeginArea(new Rect(rect.x + PopupMenuPadding, rect.y + PopupMenuPadding, rect.width - (PopupMenuPadding * 2f), rect.height - (PopupMenuPadding * 2f)));
            GUILayout.Label(menu.Title ?? "Context", _sectionTitleStyle);
            if (!string.IsNullOrEmpty(menu.Detail))
                GUILayout.Label(menu.Detail, _mutedTextStyle);
            GUILayout.Space(4f);
            for (int i = 0; menu.Actions != null && i < menu.Actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = menu.Actions[i];
                if (action == null)
                    continue;

                Rect buttonRect = GUILayoutUtility.GetRect(rect.width - 24f, PopupMenuRowHeight, GUILayout.Height(PopupMenuRowHeight));
                DrawMenuActionRow(buttonRect, action);
            }
            GUILayout.EndArea();
        }

        private Rect BuildPopupRectCore(ScenarioAuthoringContextMenuModel menu, float width, float height, Rect hudReserveRect)
        {
            float rectWidth = Math.Max(220f, ScenarioUiMeasuredLabel.Width(menu != null ? menu.Title : null, _sectionTitleStyle, 24f));
            for (int i = 0; menu.Actions != null && i < menu.Actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = menu.Actions[i];
                if (action != null)
                    rectWidth = Math.Max(rectWidth, MeasureButtonWidth(action, false, 32f) + 24f);
            }
            rectWidth = Mathf.Clamp(rectWidth, 220f, 360f);
            float rectHeight = 44f + (!string.IsNullOrEmpty(menu.Detail) ? 18f : 0f) + ((menu.Actions != null ? menu.Actions.Length : 0) * PopupMenuRowHeight);
            if (menu.CenterOnScreen)
                return ScenarioAuthoringShellLayout.BuildCenteredPopupRect(width, height, rectWidth, rectHeight, hudReserveRect);

            Rect rect = new Rect(
                Mathf.Clamp(menu.AnchorX + 16f, Margin, width - rectWidth - Margin),
                Mathf.Clamp(menu.AnchorY + 16f, Margin, height - rectHeight - Margin),
                rectWidth,
                rectHeight);
            return ClampAwayFromHud(rect, width, height, hudReserveRect);
        }

        private Rect BuildWindowMenuRectCore(Rect buttonRect, ScenarioAuthoringInspectorAction[] actions, float width, float height, Rect hudReserveRect)
        {
            // TODO(centralize): Window menu is still a panel-management popup from the
            // multi-window shell. Fold these choices into central workspace navigation.
            float rectWidth = 220f;
            for (int i = 0; actions != null && i < actions.Length; i++)
                rectWidth = Math.Max(rectWidth, MeasureMenuActionWidth(actions[i]) + 24f);

            rectWidth = Mathf.Clamp(rectWidth, 220f, 320f);
            float rectHeight = 16f + ((actions != null ? actions.Length : 0) * PopupMenuRowHeight);
            float rectX = buttonRect.width > 0f
                ? buttonRect.xMax - rectWidth
                : buttonRect.x;
            Rect rect = new Rect(
                Mathf.Clamp(rectX, Margin, width - rectWidth - Margin),
                Mathf.Clamp(buttonRect.yMax, Margin, height - rectHeight - Margin),
                rectWidth,
                rectHeight);
            return ClampAwayFromHud(rect, width, height, hudReserveRect);
        }

        private void DrawWindowMenuCore(Rect rect, ScenarioAuthoringInspectorAction[] actions)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
            GUILayout.BeginArea(new Rect(rect.x + PopupMenuPadding, rect.y + PopupMenuPadding, rect.width - (PopupMenuPadding * 2f), rect.height - (PopupMenuPadding * 2f)));
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                Rect buttonRect = GUILayoutUtility.GetRect(rect.width - 24f, PopupMenuRowHeight, GUILayout.Height(PopupMenuRowHeight));
                DrawMenuActionRow(buttonRect, action);
            }
            GUILayout.EndArea();
        }

        private float MeasureMenuActionWidth(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return 0f;

            if (IsMenuGroupAction(action))
                return ScenarioUiMeasuredLabel.Width(action.Label, _sectionTitleStyle, 24f);

            return MeasureButtonWidth(action, false, 32f);
        }

        private void DrawMenuActionRow(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return;

            if (IsMenuGroupAction(action))
            {
                GUI.Label(new Rect(rect.x + 4f, rect.y + 7f, rect.width - 8f, rect.height - 8f), action.Label ?? string.Empty, _sectionTitleStyle);
                return;
            }

            DrawButton(rect, action, false);
        }

        private static bool IsMenuGroupAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && string.Equals(action.Badge, "GROUP", StringComparison.OrdinalIgnoreCase);
        }

        private void DrawButton(Rect rect, ScenarioAuthoringInspectorAction action, bool tab)
        {
            if (action == null)
                return;

            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            bool manualHighlightEnabled = _scaledWindowDrawDepth == 0;
            bool hovered = manualHighlightEnabled && rect.Contains(mouse);
            bool pressed = hovered && Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            float press = action.Enabled ? _animations.GetButtonPressForAction(action.Id, pressed) : 0f;
            Rect visualRect = press > 0.001f
                ? new Rect(rect.x + press, rect.y - press, rect.width, rect.height)
                : rect;
            if (!string.IsNullOrEmpty(action.Id))
                RegisterTourTarget("action:" + action.Id, visualRect);
            string tooltip = action.Enabled
                ? (action.Hint ?? action.Detail ?? string.Empty)
                : (!string.IsNullOrEmpty(action.DisabledReason) ? action.DisabledReason : (!string.IsNullOrEmpty(action.Detail) ? action.Detail : (action.Hint ?? string.Empty)));
            GUIStyle style = tab
                ? (!action.Enabled ? _uiContext.Styles.TabDisabled : (action.Emphasized ? _activeTabStyle : _tabStyle))
                : (!action.Enabled ? _uiContext.Styles.ButtonDisabled : (action.Emphasized ? _activeButtonStyle : _buttonStyle));
            bool nativeButton = ScenarioUiAtlasSkin.DrawButton(visualRect, action.Emphasized, action.Enabled, pressed, tab);
            GUIStyle drawStyle = nativeButton ? ResolveContentButtonStyle(action, tab) : style;
            GUIContent content = new GUIContent(
                ScenarioUiMeasuredLabel.FitLabelWithEllipsis(action.Label ?? string.Empty, ResolveButtonContentWidth(rect, drawStyle, tab), drawStyle),
                tooltip);

            if (IsWindowMenuAction(action))
            {
                if (GUI.Button(visualRect, content, drawStyle) && action.Enabled)
                {
                    _windowMenuOpen = !_windowMenuOpen;
                    if (_snapshot != null && _snapshot.State != null)
                        _snapshot.State.WindowMenuOpen = _windowMenuOpen;
                    if (Event.current != null)
                        Event.current.Use();
                }
                DrawButtonAnimationOverlay(visualRect, action.Id, action.Enabled, hovered, pressed);
                return;
            }

            if (GUI.Button(visualRect, content, drawStyle) && action.Enabled)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }

            DrawButtonAnimationOverlay(visualRect, action.Id, action.Enabled, hovered, pressed);
            DrawActionPulseOverlay(visualRect, action);
        }

        private GUIStyle ResolveContentButtonStyle(ScenarioAuthoringInspectorAction action, bool tab)
        {
            if (tab)
                return !action.Enabled ? _disabledTabContentStyle : (action.Emphasized ? _activeTabContentStyle : _tabContentStyle);

            return !action.Enabled ? _disabledButtonContentStyle : (action.Emphasized ? _activeButtonContentStyle : _buttonContentStyle);
        }

        private static float ResolveButtonContentWidth(Rect rect, GUIStyle style, bool tab)
        {
            float stylePadding = style != null && style.padding != null
                ? style.padding.left + style.padding.right
                : 0f;
            float minimumPadding = tab ? 28f : 20f;
            return Math.Max(0f, rect.width - Math.Max(stylePadding, minimumPadding));
        }

        private void DrawButtonAnimationOverlay(Rect rect, string actionId, bool enabled, bool hovered, bool pressed)
        {
            if (!enabled || string.IsNullOrEmpty(actionId) || _uiContext == null || _uiContext.Styles == null)
                return;

            float hover = _animations.GetButtonHover(actionId, hovered);
            float press = _animations.GetButtonPressForAction(actionId, pressed);
            if (hover <= 0.001f && press <= 0.001f)
                return;

            Color oldColor = GUI.color;
            Rect overlayRect = InsetButtonOverlayRect(rect);
            if (hover > 0.001f)
            {
                GUI.color = new Color(0.882f, 0.784f, 0.588f, 0.28f * hover);
                ScenarioUiAtlasSkin.DrawCornerCutTexture(overlayRect, _uiContext.Styles.AccentHoverTexture != null ? _uiContext.Styles.AccentHoverTexture : Texture2D.whiteTexture);
            }

            if (press > 0.001f)
            {
                GUI.color = new Color(0.718f, 0.639f, 0.482f, 0.34f * press);
                ScenarioUiAtlasSkin.DrawCornerCutTexture(overlayRect, Texture2D.whiteTexture);
            }

            GUI.color = oldColor;
        }

        private void DrawActionPulseOverlay(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id) || _uiContext == null || _uiContext.Styles == null)
                return;

            bool pulseAction = string.Equals(action.Id, ScenarioAuthoringActionIds.ActionSave, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionPlaytest, StringComparison.Ordinal);
            if (!pulseAction)
                return;

            string signature = action.Id + ":" + (action.Label ?? string.Empty) + ":" + action.Emphasized;
            float pulse = _animations.GetPulseProgress("action.pulse." + action.Id, signature, 0.42f, ScenarioUiEasing.EaseOut);
            if (pulse <= 0.001f)
                return;

            Color oldColor = GUI.color;
            GUI.color = new Color(0.94f, 0.80f, 0.52f, 0.26f * pulse);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(InsetButtonOverlayRect(rect), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static Rect InsetButtonOverlayRect(Rect rect)
        {
            const float inset = 3f;
            if (rect.width <= inset * 2f || rect.height <= inset * 2f)
                return rect;

            return new Rect(rect.x + inset, rect.y + inset, rect.width - (inset * 2f), rect.height - (inset * 2f));
        }
    }
}
