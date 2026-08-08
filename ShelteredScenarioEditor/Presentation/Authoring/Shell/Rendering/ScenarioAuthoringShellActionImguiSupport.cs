using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
using ShelteredScenarioEditor.Presentation.UiKit;
using ShelteredScenarioEditor.Presentation.UiKit.Frame;
using ShelteredScenarioEditor.Presentation.UiKit.Textures;
using ShelteredScenarioEditor.Presentation.UiKit.Theme;
using ShelteredScenarioEditor.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float WindowChromeGlyphMaxExtent = 34f;

        private bool ExecuteInspectorAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || !action.Enabled)
                return false;
            return action.Command != null
                && _backend != null
                && _backend.ExecuteCommand(action.Command);
        }

        private bool ExecuteInspectorTextAction(ScenarioAuthoringInspectorAction action, string value)
        {
            if (action == null || !action.Enabled)
                return false;

            IScenarioTextValueCommand textCommand = action.Command as IScenarioTextValueCommand;
            return textCommand != null
                && _backend != null
                && _backend.ExecuteCommand(textCommand.WithTextValue(value));
        }

        private bool ExecuteRendererCommand(RendererInteractionCommand command)
        {
            return _backend != null && _backend.ExecuteCommand(command);
        }

        // Fixed grain seed for chrome bands (top bar, docks, menus) so the
        // leather tooth stays stable frame to frame.
        private const int ChromeGrainSeed = 5;

        private static bool IsWindowMenuAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellToggleWindowMenu, StringComparison.Ordinal);
        }

        private ScenarioAuthoringInspectorAction[] GetHeaderActions(ScenarioAuthoringInspectorAction[] actions, bool chromeOnly)
        {
            if (actions == null || actions.Length == 0)
                return new ScenarioAuthoringInspectorAction[0];

            List<ScenarioAuthoringInspectorAction> filtered = new List<ScenarioAuthoringInspectorAction>();
            for (int i = 0; i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                bool isChrome = IsWindowHeaderChromeAction(action);
                if (isChrome == chromeOnly)
                    filtered.Add(action);
            }

            return filtered.ToArray();
        }

        private static bool IsWindowHeaderChromeAction(ScenarioAuthoringInspectorAction action)
        {
            return IsWindowHeaderCollapseAction(action) || IsWindowHeaderCloseAction(action);
        }

        private static bool IsWindowHeaderChromeGlyphAction(ScenarioAuthoringInspectorAction action, Rect rect)
        {
            return IsWindowHeaderChromeAction(action)
                && rect.width > 0f
                && rect.height > 0f
                && rect.width <= WindowChromeGlyphMaxExtent
                && rect.height <= WindowChromeGlyphMaxExtent;
        }

        private static bool IsWindowHeaderCollapseAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && !string.IsNullOrEmpty(action.Id)
                && action.Id.StartsWith(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix, StringComparison.Ordinal)
                && IsWindowHeaderCollapseLabel(action.Label);
        }

        private static bool IsWindowHeaderCloseAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id) || !string.Equals((action.Label ?? string.Empty).Trim(), "x", StringComparison.OrdinalIgnoreCase))
                return false;

            return action.Id.StartsWith(ScenarioAuthoringActionIds.ActionWindowTogglePrefix, StringComparison.Ordinal)
                || (action.Command is SpriteSwapCommand && ((SpriteSwapCommand)action.Command).Kind == SpriteSwapCommandKind.CancelPicker)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellCloseHelp, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellCloseSettings, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionFocusedEditorCancel, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioBaseModeAuthoringActions.ActionSwitchCancel, StringComparison.Ordinal);
        }

        private static bool IsWindowHeaderCollapseLabel(string label)
        {
            string normalized = (label ?? string.Empty).Trim();
            return string.Equals(normalized, "_", StringComparison.Ordinal)
                || string.Equals(normalized, "-", StringComparison.Ordinal);
        }

        private float MeasureButtonWidth(ScenarioAuthoringInspectorAction action, bool tab, float extraPadding)
        {
            GUIStyle style = tab
                ? (action != null && action.Emphasized ? _activeTabStyle : _tabStyle)
                : (action != null && action.Emphasized ? _activeButtonStyle : _buttonStyle);
            return ScenarioUiMeasuredLabel.Width(action != null ? action.Label ?? string.Empty : string.Empty, style, extraPadding);
        }

        private void RegisterScrollRegion(string ownerId, Rect rect)
        {
            if (_inputCapture != null)
                _inputCapture.RegisterScrollRect(ownerId, rect);
        }

        private void RegisterInteractiveRegion(Rect rect)
        {
            if (_inputCapture != null)
                _inputCapture.RegisterInteractiveRect(rect);
        }

        private void RegisterTourTarget(string targetId, Rect rect)
        {
            if (string.IsNullOrEmpty(targetId) || rect.width <= 0f || rect.height <= 0f)
                return;

            if (_tourTargets != null)
                _tourTargets.Register(targetId, ToAbsoluteGuiRect(rect));
        }

        private Rect ToAbsoluteGuiRect(Rect rect)
        {
            float scale = _activeUiScale > 0.001f ? _activeUiScale : 1f;
            Vector2 origin = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            Vector2 opposite = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.yMax));
            return new Rect(origin.x / scale, origin.y / scale, (opposite.x - origin.x) / scale, (opposite.y - origin.y) / scale);
        }

        private void DrawChromePanel(Rect rect, GUIStyle style)
        {
            if (_uiContext == null || _uiContext.Styles == null)
            {
                GUI.Box(rect, GUIContent.none, style);
                return;
            }

            ScenarioUiStyleSheet styles = _uiContext.Styles;
            bool page = style == styles.Page || style == styles.PanelBase;
            bool inset = style == styles.Inset || style == styles.PanelInset || style == styles.Field;
            bool card = style == styles.Card || style == styles.Section || style == styles.PanelRaised;
            bool chrome = style == styles.Chrome || style == _headerStyle || style == _statusStyle || style == styles.Menu;
            bool drewNative = style == _headerStyle
                ? ScenarioUiAtlasSkin.DrawHeader(rect)
                : (style == _statusStyle ? ScenarioUiAtlasSkin.DrawStatus(rect) : ScenarioUiAtlasSkin.DrawPanel(rect));
            if (!drewNative)
            {
                if (card || chrome)
                    ScenarioUiAtlasSkin.DrawCornerCutShadow(rect, styles.ShadowTexture);
                GUI.Box(rect, GUIContent.none, style ?? _rootPanelStyle);
                ScenarioUiParchment.PaintFace(
                    rect,
                    styles.Textures,
                    Color.clear,
                    ChromeGrainSeed,
                    page ? 0.018f : (inset ? 0.015f : 0.035f),
                    page || inset ? 0.10f : 0.45f,
                    page ? null : (inset ? styles.BorderStrongTexture : styles.BorderHighlightTexture),
                    page ? null : (inset ? styles.BorderHighlightTexture : styles.BorderStrongTexture));
                if (page)
                    ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, styles.BorderStrongTexture, styles.BorderStrongTexture);
            }
        }
    }
}
