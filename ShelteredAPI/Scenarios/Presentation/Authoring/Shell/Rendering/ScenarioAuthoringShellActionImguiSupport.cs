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
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float WindowChromeGlyphMaxExtent = 34f;

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
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellCloseHelp, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellCloseSettings, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionFocusedEditorCancel, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioStoryFocusedEditorActions.ActionCancel, StringComparison.Ordinal)
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
            try
            {
                ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
                if (inputCapture != null)
                    inputCapture.RegisterScrollRect(ownerId, rect);
            }
            catch
            {
            }
        }

        private void RegisterInteractiveRegion(Rect rect)
        {
            try
            {
                ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
                if (inputCapture != null)
                    inputCapture.RegisterInteractiveRect(rect);
            }
            catch
            {
            }
        }

        private void RegisterTourTarget(string targetId, Rect rect)
        {
            if (string.IsNullOrEmpty(targetId) || rect.width <= 0f || rect.height <= 0f)
                return;

            ScenarioAuthoringTourTargetRegistry registry = ScenarioCompositionRoot.Resolve<ScenarioAuthoringTourTargetRegistry>();
            if (registry != null)
                registry.Register(targetId, ToAbsoluteGuiRect(rect));
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

            bool drewNative = style == _headerStyle
                ? ScenarioUiAtlasSkin.DrawHeader(rect)
                : (style == _statusStyle ? ScenarioUiAtlasSkin.DrawStatus(rect) : ScenarioUiAtlasSkin.DrawPanel(rect));
            if (!drewNative)
            {
                ScenarioUiAtlasSkin.DrawCornerCutShadow(rect);
                GUI.Box(rect, GUIContent.none, style ?? _rootPanelStyle);
                ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);
            }
        }
    }
}
