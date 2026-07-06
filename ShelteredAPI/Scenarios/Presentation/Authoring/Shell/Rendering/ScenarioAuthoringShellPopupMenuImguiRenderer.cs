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
        private void DrawContextMenuCore(Rect rect, ScenarioAuthoringContextMenuModel menu)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            GUILayout.Label(menu.Title ?? "Context", _sectionTitleStyle);
            if (!string.IsNullOrEmpty(menu.Detail))
                GUILayout.Label(menu.Detail, _mutedTextStyle);
            GUILayout.Space(4f);
            for (int i = 0; menu.Actions != null && i < menu.Actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = menu.Actions[i];
                if (action == null)
                    continue;

                Rect buttonRect = GUILayoutUtility.GetRect(rect.width - 24f, 24f, GUILayout.Height(24f));
                DrawButton(buttonRect, action, false);
                if (!action.Enabled && !string.IsNullOrEmpty(action.DisabledReason))
                {
                    float reasonHeight = MeasureDisabledReasonHeight(action.DisabledReason, rect.width - 24f);
                    Rect reasonRect = GUILayoutUtility.GetRect(rect.width - 24f, reasonHeight, GUILayout.Height(reasonHeight));
                    GUI.Label(reasonRect, action.DisabledReason, _mutedTextStyle);
                }
            }
            GUILayout.EndArea();
        }

        private Rect BuildPopupRectCore(ScenarioAuthoringContextMenuModel menu, float width, float height, Rect hudReserveRect)
        {
            float rectWidth = 220f;
            float rectHeight = 54f + ((menu.Actions != null ? menu.Actions.Length : 0) * 28f);
            for (int i = 0; menu.Actions != null && i < menu.Actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = menu.Actions[i];
                if (action != null && !action.Enabled && !string.IsNullOrEmpty(action.DisabledReason))
                    rectHeight += MeasureDisabledReasonHeight(action.DisabledReason, rectWidth - 24f) + 2f;
            }
            if (menu.CenterOnScreen)
                return ScenarioAuthoringShellLayout.BuildCenteredPopupRect(width, height, rectWidth, rectHeight, hudReserveRect);

            Rect rect = new Rect(
                Mathf.Clamp(menu.AnchorX + 16f, Margin, width - rectWidth - Margin),
                Mathf.Clamp(menu.AnchorY + 16f, Margin, height - rectHeight - Margin),
                rectWidth,
                rectHeight);
            return ClampAwayFromHud(rect, width, height, hudReserveRect);
        }

        private float MeasureDisabledReasonHeight(string reason, float width)
        {
            if (string.IsNullOrEmpty(reason))
                return 0f;

            GUIStyle style = _mutedTextStyle ?? GUI.skin.label;
            float measured = style.CalcHeight(new GUIContent(reason), Mathf.Max(80f, width));
            return Mathf.Clamp(measured + 2f, 16f, 64f);
        }

        private Rect BuildWindowMenuRectCore(Rect buttonRect, ScenarioAuthoringInspectorAction[] actions, float width, float height, Rect hudReserveRect)
        {
            float rectWidth = 220f;
            for (int i = 0; actions != null && i < actions.Length; i++)
                rectWidth = Math.Max(rectWidth, MeasureButtonWidth(actions[i], false, 26f) + 24f);

            rectWidth = Mathf.Clamp(rectWidth, 220f, 320f);
            float rectHeight = 16f + ((actions != null ? actions.Length : 0) * 28f);
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
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                Rect buttonRect = GUILayoutUtility.GetRect(rect.width - 24f, 24f, GUILayout.Height(24f));
                DrawButton(buttonRect, action, false);
            }
            GUILayout.EndArea();
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
            string tooltip = action.Enabled
                ? (action.Hint ?? action.Detail ?? string.Empty)
                : (!string.IsNullOrEmpty(action.DisabledReason) ? action.DisabledReason : (!string.IsNullOrEmpty(action.Detail) ? action.Detail : (action.Hint ?? string.Empty)));
            GUIStyle style = tab
                ? (!action.Enabled ? _uiContext.Styles.TabDisabled : (action.Emphasized ? _activeTabStyle : _tabStyle))
                : (!action.Enabled ? _uiContext.Styles.ButtonDisabled : (action.Emphasized ? _activeButtonStyle : _buttonStyle));
            bool nativeButton = ScenarioUiAtlasSkin.DrawButton(visualRect, action.Emphasized, action.Enabled, pressed, tab);
            GUIStyle drawStyle = nativeButton ? ResolveContentButtonStyle(action, tab) : style;
            GUIContent content = new GUIContent(ShortenToFit(action.Label ?? string.Empty, rect.width - 10f, drawStyle), tooltip);

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
                GUI.DrawTexture(overlayRect, _uiContext.Styles.AccentHoverTexture != null ? _uiContext.Styles.AccentHoverTexture : Texture2D.whiteTexture);
            }

            if (press > 0.001f)
            {
                GUI.color = new Color(0.718f, 0.639f, 0.482f, 0.34f * press);
                GUI.DrawTexture(overlayRect, Texture2D.whiteTexture);
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
            GUI.DrawTexture(InsetButtonOverlayRect(rect), Texture2D.whiteTexture);
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
