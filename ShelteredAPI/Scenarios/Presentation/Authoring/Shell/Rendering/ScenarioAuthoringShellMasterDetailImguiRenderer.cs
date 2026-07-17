using System;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal struct ScenarioAuthoringWorkspaceRenderPlan
    {
        public bool Wide;
        public bool ShowsNavigator;
        public bool ShowsDocument;
        public Rect SubtabRect;
        public Rect NavigatorRect;
        public Rect DocumentRect;
        public string NavigatorScrollOwnerId;
        public string DocumentScrollOwnerId;

        public int VisibleScrollOwnerCount
        {
            get { return (ShowsNavigator ? 1 : 0) + (ShowsDocument ? 1 : 0); }
        }
    }

    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float WorkspaceSubtabHeight = 36f;
        private const float WorkspacePanePadding = 12f;
        private const float WorkspaceRowIndent = 16f;
        private const float WorkspaceRowGap = 4f;
        private Texture2D _activeWorkspaceRailTexture;

        private Rect DrawWorkspaceBody(
            Rect bodyRect,
            ScenarioAuthoringShellWindowViewModel window)
        {
            if (window == null || window.WorkspaceBody == null)
                return bodyRect;

            ScenarioAuthoringWorkspaceViewModel workspace = window.WorkspaceBody;
            _activeWorkspaceRailTexture = ResolveWorkspaceRailTexture(workspace.Id);
            string subtabId = ResolveWorkspaceSubtabId(workspace);
            bool defaultDocumentPane = workspace.Document != null;
            bool narrowDocumentPane = ScenarioAuthoringRendererInteractionState.Instance.GetWorkspaceNarrowPane(
                workspace.Id,
                subtabId,
                defaultDocumentPane);
            ScenarioAuthoringWorkspaceRenderPlan plan = BuildWorkspaceRenderPlan(
                bodyRect,
                window,
                narrowDocumentPane);

            if (plan.SubtabRect.height > 0f)
                DrawWorkspaceSubtabs(plan.SubtabRect, workspace, subtabId);

            if (plan.ShowsNavigator)
                DrawWorkspaceNavigator(plan.NavigatorRect, plan.NavigatorScrollOwnerId, workspace, subtabId);
            if (plan.ShowsDocument)
                DrawWorkspaceDocument(plan.DocumentRect, plan.DocumentScrollOwnerId, workspace, subtabId, !plan.Wide);

            _activeWorkspaceRailTexture = null;
            return bodyRect;
        }

        internal static ScenarioAuthoringWorkspaceRenderPlan BuildWorkspaceRenderPlan(
            Rect bodyRect,
            ScenarioAuthoringShellWindowViewModel window,
            bool narrowDocumentPane)
        {
            ScenarioAuthoringWorkspaceRenderPlan plan = new ScenarioAuthoringWorkspaceRenderPlan();
            ScenarioAuthoringWorkspaceViewModel workspace = window != null ? window.WorkspaceBody : null;
            if (workspace == null || bodyRect.width <= 0f || bodyRect.height <= 0f)
                return plan;

            string subtabId = ResolveWorkspaceSubtabId(workspace);
            bool hasSubtabs = workspace.Subtabs != null && workspace.Subtabs.Length > 0;
            float panesY = bodyRect.y;
            if (hasSubtabs)
            {
                plan.SubtabRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, WorkspaceSubtabHeight);
                panesY = plan.SubtabRect.yMax + 8f;
            }

            Rect panesRect = new Rect(
                bodyRect.x,
                panesY,
                bodyRect.width,
                Math.Max(0f, bodyRect.yMax - panesY));
            string ownerRoot = "workspace." + ResolveWorkspaceOwnerToken(window, workspace) + "." + (subtabId ?? "default");
            plan.NavigatorScrollOwnerId = ownerRoot + ".navigator";
            plan.DocumentScrollOwnerId = ownerRoot + ".document";

            bool canSplit = workspace.LayoutKind == ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument
                && workspace.Navigator != null
                && workspace.Document != null
                && bodyRect.width >= ScenarioAuthoringShellLayout.MasterDetailWideMinWidth
                && bodyRect.height >= ScenarioAuthoringShellLayout.MasterDetailWideMinHeight;
            if (canSplit)
            {
                float navigatorWidth = Mathf.Clamp(
                    bodyRect.width * 0.27f,
                    ScenarioAuthoringShellLayout.MasterDetailNavigatorMinWidth,
                    ScenarioAuthoringShellLayout.MasterDetailNavigatorMaxWidth);
                plan.Wide = true;
                plan.ShowsNavigator = true;
                plan.ShowsDocument = true;
                plan.NavigatorRect = new Rect(panesRect.x, panesRect.y, navigatorWidth, panesRect.height);
                plan.DocumentRect = new Rect(
                    plan.NavigatorRect.xMax + ScenarioAuthoringShellLayout.MasterDetailPaneGutter,
                    panesRect.y,
                    Math.Max(240f, panesRect.width - navigatorWidth - ScenarioAuthoringShellLayout.MasterDetailPaneGutter),
                    panesRect.height);
                return plan;
            }

            bool documentOnly = workspace.LayoutKind == ScenarioAuthoringWorkspaceLayoutKind.DocumentOnly
                || workspace.Navigator == null;
            bool showDocument = documentOnly || (narrowDocumentPane && workspace.Document != null);
            plan.ShowsDocument = showDocument;
            plan.ShowsNavigator = !showDocument && workspace.Navigator != null;
            if (plan.ShowsDocument)
                plan.DocumentRect = panesRect;
            if (plan.ShowsNavigator)
                plan.NavigatorRect = panesRect;
            return plan;
        }

        private void DrawWorkspaceSubtabs(
            Rect rect,
            ScenarioAuthoringWorkspaceViewModel workspace,
            string activeSubtabId)
        {
            DrawChromePanel(rect, _uiContext.Styles.Page);
            ScenarioAuthoringWorkspaceSubtabViewModel[] subtabs = workspace != null ? workspace.Subtabs : null;
            int count = subtabs != null ? subtabs.Length : 0;
            if (count == 0)
                return;

            float gap = 4f;
            float innerWidth = Math.Max(80f, rect.width - (WorkspacePanePadding * 2f));
            float cellWidth = Math.Max(96f, (innerWidth - (gap * (count - 1))) / count);
            float x = rect.x + WorkspacePanePadding;
            for (int i = 0; i < count; i++)
            {
                ScenarioAuthoringWorkspaceSubtabViewModel subtab = subtabs[i];
                if (subtab == null)
                    continue;

                float remainingWidth = rect.xMax - WorkspacePanePadding - x;
                if (remainingWidth <= 0f)
                    break;
                Rect cellRect = new Rect(x, rect.y, Math.Min(cellWidth, remainingWidth), 36f);
                float chipWidth = MeasureStatusChipsWidth(subtab.StatusChips, 2, cellRect.width * 0.34f);
                float chipGap = chipWidth > 0f ? 4f : 0f;
                Rect tabRect = new Rect(cellRect.x, cellRect.y, Math.Max(24f, cellRect.width - chipWidth - chipGap), cellRect.height);
                bool selected = subtab.Selected || string.Equals(subtab.Id, activeSubtabId, StringComparison.Ordinal);
                ScenarioAuthoringInspectorAction action = subtab.SelectAction;
                string label = subtab.Label ?? string.Empty;
                bool enabled = action != null && action.Enabled;
                if (DrawPlainButton(
                    tabRect,
                    new GUIContent(label, action != null ? action.Hint ?? action.Detail ?? string.Empty : string.Empty),
                    enabled ? (selected ? _activeTabStyle : _tabStyle) : _uiContext.Styles.TabDisabled,
                    enabled))
                {
                    ExecuteWorkspaceAction(action);
                }

                if (selected)
                    GUI.DrawTexture(new Rect(tabRect.x, tabRect.yMax - 4f, tabRect.width, 4f), ResolveWorkspaceRailTexture(workspace != null ? workspace.Id : null));

                if (chipWidth > 0f)
                    DrawStatusChipRun(tabRect.xMax + chipGap, cellRect, subtab.StatusChips, 2, chipWidth);
                x += cellWidth + gap;
                if (x >= rect.xMax - WorkspacePanePadding)
                    break;
            }
        }

        private void DrawWorkspaceNavigator(
            Rect rect,
            string scrollOwnerId,
            ScenarioAuthoringWorkspaceViewModel workspace,
            string subtabId)
        {
            DrawChromePanel(rect, _uiContext.Styles.Page);
            ScenarioAuthoringNavigatorViewModel navigator = workspace != null ? workspace.Navigator : null;
            if (navigator == null)
                return;

            Rect inner = InsetWorkspaceRect(rect, WorkspacePanePadding);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 22f), "Navigator", _smallTitleStyle);
            Rect searchRect = new Rect(inner.x, inner.y + 26f, inner.width, 30f);
            DrawWorkspaceSearch(searchRect, workspace, subtabId, navigator);
            Rect viewport = new Rect(inner.x, searchRect.yMax + 8f, inner.width, Math.Max(60f, inner.yMax - searchRect.yMax - 8f));

            GUILayout.BeginArea(viewport);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, viewport.width - 18f);
            Vector2 scroll = GetWindowScrollPosition(scrollOwnerId);
            RegisterScrollRegion(scrollOwnerId, viewport);
            scroll = BeginMeasuredScrollView(scroll, viewport);
            int groupCount = navigator.Groups != null ? navigator.Groups.Length : 0;
            for (int i = 0; i < groupCount; i++)
            {
                ScenarioAuthoringNavigatorGroupViewModel group = navigator.Groups[i];
                if (group == null)
                    continue;
                DrawNavigatorGroupHeader(group);
                if (group.Expanded)
                {
                    for (int rowIndex = 0; group.Rows != null && rowIndex < group.Rows.Length; rowIndex++)
                        DrawNavigatorRow(group.Rows[rowIndex], 0, workspace != null ? workspace.Id : null);
                }
                GUILayout.Space(12f);
            }
            if (groupCount == 0)
                GUILayout.Label(string.IsNullOrEmpty(navigator.EmptyMessage) ? "Nothing to show yet." : navigator.EmptyMessage, _uiContext.Styles.MutedText);
            GUILayout.Space(12f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition(scrollOwnerId, scroll);
        }

        private void DrawWorkspaceSearch(
            Rect rect,
            ScenarioAuthoringWorkspaceViewModel workspace,
            string subtabId,
            ScenarioAuthoringNavigatorViewModel navigator)
        {
            string currentValue = navigator != null ? navigator.SearchText ?? string.Empty : string.Empty;
            string controlName = !string.IsNullOrEmpty(navigator != null ? navigator.SearchControlId : null)
                ? navigator.SearchControlId
                : "workspace_search." + (workspace != null ? workspace.Id ?? string.Empty : string.Empty) + "." + (subtabId ?? string.Empty);
            bool topmost = IsInteractiveVisualTopmost(rect);
            RegisterInteractiveRegion(rect);
            string nextValue = currentValue;
            if (topmost)
            {
                GUI.SetNextControlName(controlName);
                nextValue = GUI.TextField(rect, currentValue, _uiContext.Styles.SearchField);
            }
            else
            {
                GUI.Box(rect, currentValue, _uiContext.Styles.SearchField);
            }

            if (!string.Equals(nextValue, currentValue, StringComparison.Ordinal))
            {
                string actionId = ScenarioAuthoringWorkspaceViewModelFactory.BuildWorkspaceActionId(
                    ScenarioAuthoringActionIds.ActionRendererWorkspaceSearchSetPrefix,
                    workspace != null ? workspace.Id : null,
                    subtabId,
                    nextValue);
                ScenarioAuthoringBackendService.Instance.ExecuteAction(actionId);
            }
            DrawSearchPlaceholder(rect, nextValue, string.IsNullOrEmpty(navigator.SearchPlaceholder) ? "Search" : navigator.SearchPlaceholder);
            bool focused = topmost && string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            _workspaceSearchFocused = _workspaceSearchFocused || focused;
            if (focused)
                DrawFieldFocusBorder(rect);
        }

        private void DrawNavigatorGroupHeader(ScenarioAuthoringNavigatorGroupViewModel group)
        {
            Rect rowRect = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true), GUILayout.Height(34f));
            float createWidth = group.CreateAction != null
                ? Mathf.Clamp(MeasureButtonWidth(group.CreateAction, false, 18f), 30f, Math.Max(30f, rowRect.width * 0.34f))
                : 0f;
            float chipsWidth = MeasureStatusChipsWidth(group.StatusChips, 2, rowRect.width * 0.34f);
            float rightWidth = createWidth
                + (createWidth > 0f ? 5f : 0f)
                + chipsWidth
                + (chipsWidth > 0f ? 4f : 0f);
            Rect toggleRect = new Rect(rowRect.x, rowRect.y, Math.Max(58f, rowRect.width - rightWidth), rowRect.height);
            string label = group.Label ?? string.Empty;
            ScenarioAuthoringInspectorAction toggle = group.ToggleAction;
            if (toggle != null && DrawPlainButton(
                toggleRect,
                new GUIContent(label, toggle.Hint ?? toggle.Detail ?? string.Empty),
                toggle.Enabled ? (group.Expanded ? _activeButtonStyle : _buttonStyle) : _uiContext.Styles.ButtonDisabled,
                toggle.Enabled))
            {
                ExecuteWorkspaceAction(toggle);
            }
            else if (toggle == null)
            {
                GUI.Box(toggleRect, label, _buttonStyle);
            }

            float x = toggleRect.xMax + (chipsWidth > 0f ? 4f : 0f);
            x = DrawStatusChipRun(x, rowRect, group.StatusChips, 2, chipsWidth);
            if (group.CreateAction != null)
            {
                Rect createRect = new Rect(rowRect.xMax - createWidth, rowRect.y + 2f, createWidth, rowRect.height - 4f);
                DrawButton(createRect, group.CreateAction, false);
            }
        }

        private void DrawNavigatorRow(ScenarioAuthoringNavigatorRowViewModel row, int depth, string workspaceId)
        {
            if (row == null)
                return;

            float estimatedWidth = Math.Max(80f, GetSectionContentWidth());
            float indent = Math.Min(WorkspaceRowIndent * Math.Max(0, depth), estimatedWidth * 0.30f);
            bool hasSubtitle = !string.IsNullOrEmpty(row.Subtitle);
            float estimatedRowWidth = Math.Max(80f, estimatedWidth - indent);
            float estimatedToggleWidth = row.Children != null && row.Children.Length > 0 && row.ToggleAction != null ? 28f : 0f;
            float estimatedChipsWidth = MeasureStatusChipsWidth(row.StatusChips, 2, estimatedRowWidth * 0.36f);
            float estimatedGaps = (estimatedToggleWidth > 0f ? 4f : 0f) + (estimatedChipsWidth > 0f ? 4f : 0f);
            float estimatedTextWidth = Math.Max(20f, estimatedRowWidth - estimatedToggleWidth - estimatedChipsWidth - estimatedGaps - 18f);
            GUIStyle estimatedTitleStyle = row.Selected ? _uiContext.Styles.PaperTitleText : _uiContext.Styles.PaperBodyText;
            float titleHeight = estimatedTitleStyle != null
                ? Math.Max(20f, estimatedTitleStyle.CalcHeight(new GUIContent(row.Title ?? string.Empty), estimatedTextWidth))
                : 20f;
            float subtitleHeight = hasSubtitle && _uiContext.Styles.PaperMutedText != null
                ? Math.Max(20f, _uiContext.Styles.PaperMutedText.CalcHeight(new GUIContent(row.Subtitle), estimatedTextWidth))
                : 0f;
            float height = Math.Max(hasSubtitle ? 60f : 40f, 16f + titleHeight + (hasSubtitle ? subtitleHeight + 4f : 0f));
            Rect allocated = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            Rect rowRect = new Rect(allocated.x + indent, allocated.y, Math.Max(80f, allocated.width - indent), allocated.height);
            float toggleWidth = row.Children != null && row.Children.Length > 0 && row.ToggleAction != null ? 28f : 0f;
            float chipsWidth = MeasureStatusChipsWidth(row.StatusChips, 2, rowRect.width * 0.36f);
            float controlGaps = (toggleWidth > 0f ? 4f : 0f) + (chipsWidth > 0f ? 4f : 0f);
            float selectWidth = Math.Max(48f, rowRect.width - toggleWidth - chipsWidth - controlGaps);
            Rect selectRect = new Rect(rowRect.x, rowRect.y, selectWidth, rowRect.height);
            ScenarioAuthoringInspectorAction select = row.SelectAction;
            bool enabled = select != null && select.Enabled;
            bool warning = false;
            for (int statusIndex = 0; row.StatusChips != null && statusIndex < row.StatusChips.Length; statusIndex++)
            {
                ScenarioAuthoringStatusChipViewModel status = row.StatusChips[statusIndex];
                if (status != null && status.Tone == ScenarioAuthoringStatusTone.Warning)
                {
                    warning = true;
                    break;
                }
            }
            GUIStyle rowStyle = warning
                ? _uiContext.Styles.NavigatorRowWarning
                : (row.Selected ? _uiContext.Styles.NavigatorRowSelected : _uiContext.Styles.NavigatorRow);
            if (DrawPlainButton(
                selectRect,
                GUIContent.none,
                enabled ? rowStyle : _uiContext.Styles.ButtonDisabled,
                enabled))
                ExecuteWorkspaceAction(select);

            bool rowHovered = enabled && IsInteractiveHoverAllowed(selectRect);
            Texture2D rowBorder = warning
                ? _uiContext.Styles.SemanticWarningStrongTexture
                : (rowHovered ? _uiContext.Styles.BorderStrongTexture : _uiContext.Styles.BorderSubtleTexture);
            ScenarioUiAtlasSkin.DrawCornerCutBorder(selectRect, rowBorder, rowBorder);

            if (row.Selected)
                GUI.DrawTexture(new Rect(selectRect.x, selectRect.y, 4f, selectRect.height), ResolveWorkspaceRailTexture(workspaceId));
            if (row.Selected)
            {
                ScenarioUiAtlasSkin.DrawCornerCutBorder(selectRect, _uiContext.Styles.FocusBorderTexture, _uiContext.Styles.FocusBorderTexture);
                ScenarioUiAtlasSkin.DrawCornerCutBorder(new Rect(selectRect.x + 1f, selectRect.y + 1f, Math.Max(0f, selectRect.width - 2f), Math.Max(0f, selectRect.height - 2f)), _uiContext.Styles.FocusBorderTexture, _uiContext.Styles.FocusBorderTexture);
            }

            float labelX = 10f;
            GUI.BeginGroup(selectRect);
            GUI.Label(
                new Rect(labelX, hasSubtitle ? 6f : 7f, Math.Max(20f, selectRect.width - 18f), titleHeight),
                row.Title ?? string.Empty,
                row.Selected ? _uiContext.Styles.PaperTitleText : _uiContext.Styles.PaperBodyText);
            if (hasSubtitle)
                GUI.Label(new Rect(labelX, 8f + titleHeight, Math.Max(20f, selectRect.width - 18f), subtitleHeight), row.Subtitle, _uiContext.Styles.PaperMutedText);
            GUI.EndGroup();

            float x = selectRect.xMax + (toggleWidth > 0f || chipsWidth > 0f ? 4f : 0f);
            if (toggleWidth > 0f)
            {
                Rect toggleRect = new Rect(x, rowRect.y + 4f, toggleWidth, rowRect.height - 8f);
                ScenarioAuthoringInspectorAction toggle = row.ToggleAction;
                if (DrawPlainButton(
                    toggleRect,
                    new GUIContent(row.Expanded ? "v" : ">", toggle.Hint ?? toggle.Detail ?? string.Empty),
                    toggle.Enabled ? (row.Expanded ? _activeButtonStyle : _buttonStyle) : _uiContext.Styles.ButtonDisabled,
                    toggle.Enabled))
                {
                    ExecuteWorkspaceAction(toggle);
                }
                x = toggleRect.xMax + (chipsWidth > 0f ? 4f : 0f);
            }
            DrawStatusChipRun(x, rowRect, row.StatusChips, 2, chipsWidth);
            GUILayout.Space(WorkspaceRowGap);

            if (row.Expanded)
            {
                for (int i = 0; row.Children != null && i < row.Children.Length; i++)
                    DrawNavigatorRow(row.Children[i], depth + 1, workspaceId);
            }
        }

        private void DrawStatusChip(Rect rect, ScenarioAuthoringStatusChipViewModel chip)
        {
            if (chip == null || rect.width <= 0f || rect.height <= 0f)
                return;

            GUIStyle style = ResolveStatusChipStyle(chip.Tone);
            string label = chip.Text ?? string.Empty;
            float availableTextWidth = Math.Max(0f, rect.width - (style.padding != null ? style.padding.left + style.padding.right : 0f));
            if (style.CalcSize(new GUIContent(label)).x > availableTextWidth)
            {
                const string tail = "...";
                int length = label.Length;
                while (length > 0 && style.CalcSize(new GUIContent(label.Substring(0, length) + tail)).x > availableTextWidth)
                    length--;
                label = length > 0 ? label.Substring(0, length) + tail : tail;
            }
            ScenarioAuthoringInspectorAction action = chip.Action;
            if (action != null)
            {
                if (!string.IsNullOrEmpty(action.Id))
                    RegisterTourTarget("action:" + action.Id, rect);
                if (DrawPlainButton(
                    rect,
                    new GUIContent(label, action.Enabled ? action.Hint ?? action.Detail ?? string.Empty : action.DisabledReason ?? string.Empty),
                    action.Enabled ? style : _uiContext.Styles.ButtonDisabled,
                    action.Enabled))
                {
                    ExecuteWorkspaceAction(action);
                }
                return;
            }

            GUI.Box(rect, label, style);
        }

        private void DrawWorkspaceDocument(
            Rect rect,
            string scrollOwnerId,
            ScenarioAuthoringWorkspaceViewModel workspace,
            string subtabId,
            bool narrow)
        {
            DrawChromePanel(rect, _uiContext.Styles.Page);
            ScenarioAuthoringWorkspaceDocumentViewModel document = workspace != null ? workspace.Document : null;
            Rect inner = InsetWorkspaceRect(rect, WorkspacePanePadding);
            if (workspace != null && workspace.LayoutKind == ScenarioAuthoringWorkspaceLayoutKind.DocumentOnly)
            {
                float documentWidth = Math.Min(inner.width, ResolveLogicalPixelCap(760f));
                inner = new Rect(inner.x + ((inner.width - documentWidth) * 0.5f), inner.y, documentWidth, inner.height);
            }
            if (document == null)
            {
                GUI.Box(inner, GUIContent.none, _uiContext.Styles.Inset);
                ScenarioUiWidgets.DrawEmptyState(inner, "Select an item to open its document.", _uiContext.Styles);
                return;
            }

            float y = inner.y;
            if (narrow
                && workspace.LayoutKind == ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument
                && workspace.Navigator != null
                && document.BackAction != null)
            {
                float backWidth = Mathf.Clamp(MeasureButtonWidth(document.BackAction, false, 22f), 112f, Math.Max(112f, inner.width));
                float backHeight = _buttonStyle != null
                    ? Math.Max(32f, _buttonStyle.CalcHeight(new GUIContent(document.BackAction.Label ?? "< Back"), backWidth))
                    : 32f;
                Rect backRect = new Rect(inner.x, y, Math.Min(backWidth, inner.width), backHeight);
                DrawWorkspaceBack(backRect, document.BackAction);
                y = backRect.yMax + 8f;
            }

            if (document.Breadcrumbs != null && document.Breadcrumbs.Length > 0)
            {
                Rect breadcrumbRect = new Rect(inner.x, y, inner.width, 24f);
                DrawWorkspaceBreadcrumb(breadcrumbRect, document.Breadcrumbs);
                y = breadcrumbRect.yMax + 4f;
            }

            GUI.Label(new Rect(inner.x, y, inner.width, 28f), document.Title ?? "Document", _smallTitleStyle);
            y += 29f;
            if (!string.IsNullOrEmpty(document.Subtitle))
            {
                GUI.Label(new Rect(inner.x, y, inner.width, 20f), document.Subtitle, _uiContext.Styles.MutedText);
                y += 22f;
            }
            if (document.StatusChips != null && document.StatusChips.Length > 0)
            {
                Rect chipsRow = new Rect(inner.x, y, inner.width, 22f);
                DrawStatusChipRun(chipsRow.x, chipsRow, document.StatusChips, 5, chipsRow.width);
                y = chipsRow.yMax + 8f;
            }
            if (document.HeaderActions != null && document.HeaderActions.Length > 0)
            {
                float actionX = inner.x;
                for (int i = 0; i < document.HeaderActions.Length; i++)
                {
                    ScenarioAuthoringInspectorAction action = document.HeaderActions[i];
                    if (action == null)
                        continue;
                    float width = Mathf.Clamp(MeasureButtonWidth(action, false, 16f), 72f, Math.Min(240f, Math.Max(72f, inner.width)));
                    if (actionX > inner.x && actionX + width > inner.xMax)
                    {
                        actionX = inner.x;
                        y += 40f;
                    }
                    Rect actionRect = new Rect(actionX, y, Math.Min(width, inner.xMax - actionX), 32f);
                    DrawButton(actionRect, action, false);
                    actionX = actionRect.xMax + 8f;
                }
                y += 40f;
            }

            Rect viewport = new Rect(inner.x, y + 3f, inner.width, Math.Max(60f, inner.yMax - y - 3f));
            GUILayout.BeginArea(viewport);
            float previousContentWidth = _activeContentWidth;
            _activeContentWidth = Math.Max(120f, viewport.width - 18f);
            Vector2 scroll = GetWindowScrollPosition(scrollOwnerId);
            RegisterScrollRegion(scrollOwnerId, viewport);
            scroll = BeginMeasuredScrollView(scroll, viewport);
            for (int pass = 0; pass < 2; pass++)
            {
                bool advancedPass = pass == 1;
                bool drewAdvancedHeading = false;
                for (int i = 0; document.Sections != null && i < document.Sections.Length; i++)
                {
                    ScenarioAuthoringInspectorSection section = document.Sections[i];
                    if (section == null || section.IsAdvanced != advancedPass)
                        continue;

                    if (advancedPass && !drewAdvancedHeading)
                    {
                        GUILayout.Space(16f);
                        GUILayout.Label("Advanced", _uiContext.Styles.MutedText);
                        drewAdvancedHeading = true;
                    }
                    if (section.StatusChips != null && section.StatusChips.Length > 0)
                    {
                        Rect statusRect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true), GUILayout.Height(22f));
                        DrawStatusChipRun(statusRect.x, statusRect, section.StatusChips, 5, statusRect.width);
                        GUILayout.Space(4f);
                    }

                    if (advancedPass)
                        GUILayout.BeginVertical(_uiContext.Styles.Inset, GUILayout.Width(Math.Min(ResolveLogicalPixelCap(760f), GetSectionContentWidth())));
                    DrawSection(section);
                    if (advancedPass)
                        GUILayout.EndVertical();
                    GUILayout.Space(12f);
                }
            }
            if (document.Sections == null || document.Sections.Length == 0)
                GUILayout.Label("This document has no editable sections yet.", _uiContext.Styles.MutedText);
            GUILayout.Space(12f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            _activeContentWidth = previousContentWidth;
            SetWindowScrollPosition(scrollOwnerId, scroll);
        }

        private void DrawWorkspaceBreadcrumb(Rect rect, ScenarioAuthoringBreadcrumbViewModel[] breadcrumbs)
        {
            float x = rect.x;
            for (int i = 0; breadcrumbs != null && i < breadcrumbs.Length; i++)
            {
                ScenarioAuthoringBreadcrumbViewModel breadcrumb = breadcrumbs[i];
                if (breadcrumb == null)
                    continue;
                if (x > rect.x)
                {
                    GUI.Label(new Rect(x, rect.y + 2f, 16f, rect.height), ">", _uiContext.Styles.BreadcrumbLinkText);
                    x += 16f;
                }
                bool tail = i == breadcrumbs.Length - 1;
                float remaining = Math.Max(0f, rect.xMax - x);
                float laterReserve = tail ? 0f : 42f + (16f * Math.Max(0, breadcrumbs.Length - i - 1));
                float width = tail
                    ? remaining
                    : Math.Min(
                        ScenarioUiMeasuredLabel.Width(breadcrumb.Label ?? string.Empty, _uiContext.Styles.BreadcrumbLinkText, 0f),
                        Math.Max(42f, remaining - laterReserve));
                Rect crumbRect = new Rect(x, rect.y, Math.Min(width, rect.xMax - x), rect.height);
                ScenarioAuthoringInspectorAction action = breadcrumb.Action;
                string label = tail
                    ? FitBreadcrumbTail(breadcrumb.Label, crumbRect.width, _uiContext.Styles.BreadcrumbCurrentText)
                    : breadcrumb.Label ?? string.Empty;
                string tooltip = !string.Equals(label, breadcrumb.Label ?? string.Empty, StringComparison.Ordinal)
                    ? breadcrumb.Label ?? string.Empty
                    : (action != null ? action.Hint ?? action.Detail ?? string.Empty : string.Empty);
                if (action != null && DrawPlainButton(crumbRect, new GUIContent(string.Empty, tooltip), GUIStyle.none, action.Enabled))
                {
                    ExecuteWorkspaceAction(action);
                }
                GUI.Label(crumbRect, new GUIContent(label, tooltip), tail ? _uiContext.Styles.BreadcrumbCurrentText : _uiContext.Styles.BreadcrumbLinkText);
                x = crumbRect.xMax;
                if (x >= rect.xMax)
                    break;
            }
        }

        private static string FitBreadcrumbTail(string value, float width, GUIStyle style)
        {
            string label = value ?? string.Empty;
            if (style == null || width <= 0f || style.CalcSize(new GUIContent(label)).x <= width)
                return label;

            const string ellipsis = "...";
            for (int length = label.Length - 1; length >= 0; length--)
            {
                string candidate = label.Substring(0, length).TrimEnd() + ellipsis;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                    return candidate;
            }
            return string.Empty;
        }

        private void DrawWorkspaceBack(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return;
            string label = string.IsNullOrEmpty(action.Label) ? "< Back" : action.Label;
            if (DrawPlainButton(
                rect,
                new GUIContent(label, action.Hint ?? action.Detail ?? string.Empty),
                action.Enabled ? _buttonStyle : _uiContext.Styles.ButtonDisabled,
                action.Enabled))
            {
                ExecuteWorkspaceAction(action);
            }
        }

        private void DrawCompactChoice(ScenarioAuthoringCompactChoiceViewModel choice)
        {
            if (choice == null)
                return;

            string heading = choice.Label ?? string.Empty;
            if (!string.IsNullOrEmpty(choice.CurrentLabel))
                heading = string.IsNullOrEmpty(heading) ? choice.CurrentLabel : heading + "  -  " + choice.CurrentLabel;
            if (!string.IsNullOrEmpty(heading))
                GUILayout.Label(heading, _mutedTextStyle);

            ScenarioAuthoringCompactChoiceOptionViewModel[] options = choice.Options;
            int count = options != null ? options.Length : 0;
            if (count == 0)
                return;

            float availableWidth = Math.Min(ResolveLogicalPixelCap(720f), GetSectionContentWidth());
            float gap = 8f;
            int requested = choice.ColumnCount > 0 ? choice.ColumnCount : 4;
            int capacity = Math.Max(1, Mathf.FloorToInt((availableWidth + 8f) / 120f));
            int columns = Math.Min(requested, Math.Min(capacity, count));
            float optionWidth = Mathf.Clamp((availableWidth - (gap * (columns - 1))) / columns, 112f, 176f);
            for (int row = 0; row < count; row += columns)
            {
                GUILayout.BeginHorizontal();
                int rowCount = Math.Min(columns, count - row);
                for (int column = 0; column < rowCount; column++)
                {
                    ScenarioAuthoringCompactChoiceOptionViewModel option = options[row + column];
                    if (option == null)
                        continue;
                    Rect optionRect = GUILayoutUtility.GetRect(optionWidth, 36f, GUILayout.Width(optionWidth), GUILayout.Height(36f));
                    ScenarioAuthoringInspectorAction action = option.Action;
                    bool enabled = action != null && action.Enabled;
                    GUIStyle style = enabled
                        ? (option.Selected ? _uiContext.Styles.CompactChoiceSelected : _uiContext.Styles.CompactChoice)
                        : _uiContext.Styles.ButtonDisabled;
                    if (DrawPlainButton(
                        optionRect,
                        new GUIContent(option.Label ?? string.Empty, action != null ? action.Hint ?? action.Detail ?? string.Empty : string.Empty),
                        style,
                        enabled))
                    {
                        ExecuteWorkspaceAction(action);
                    }
                    Texture2D optionBorder = option.Selected
                        ? _uiContext.Styles.SemanticInfoStrongTexture
                        : (enabled && IsInteractiveHoverAllowed(optionRect) ? _uiContext.Styles.BorderStrongTexture : _uiContext.Styles.BorderSubtleTexture);
                    ScenarioUiAtlasSkin.DrawCornerCutBorder(optionRect, optionBorder, optionBorder);
                    if (option.Selected)
                    {
                        ScenarioUiAtlasSkin.DrawCornerCutBorder(new Rect(optionRect.x + 1f, optionRect.y + 1f, Math.Max(0f, optionRect.width - 2f), Math.Max(0f, optionRect.height - 2f)), optionBorder, optionBorder);
                        GUI.DrawTexture(new Rect(optionRect.x, optionRect.yMax - 4f, optionRect.width, 4f), _activeWorkspaceRailTexture ?? _uiContext.Styles.WorkspaceStoryTexture);
                    }
                    if (column < rowCount - 1)
                        GUILayout.Space(gap);
                }
                GUILayout.EndHorizontal();
                if (row + columns < count)
                    GUILayout.Space(gap);
            }
        }

        private float DrawStatusChipRun(
            float x,
            Rect rowRect,
            ScenarioAuthoringStatusChipViewModel[] chips,
            int maximum,
            float availableWidth)
        {
            int count = Math.Min(maximum, chips != null ? chips.Length : 0);
            float remaining = Math.Max(0f, availableWidth);
            int remainingChipCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (chips[i] != null)
                    remainingChipCount++;
            }
            for (int i = 0; i < count && remaining > 24f; i++)
            {
                ScenarioAuthoringStatusChipViewModel chip = chips[i];
                if (chip == null)
                    continue;
                remainingChipCount--;
                float laterChipReserve = remainingChipCount > 0 ? (remainingChipCount * 28f) + (remainingChipCount * 4f) : 0f;
                float width = Math.Min(MeasureStatusChipWidth(chip), Math.Max(28f, remaining - laterChipReserve));
                Rect chipRect = new Rect(x, rowRect.y + ((rowRect.height - 20f) * 0.5f), width, 20f);
                DrawStatusChip(chipRect, chip);
                x = chipRect.xMax + 4f;
                remaining -= width + 4f;
            }
            return x;
        }

        private float MeasureStatusChipsWidth(
            ScenarioAuthoringStatusChipViewModel[] chips,
            int maximum,
            float maximumWidth)
        {
            int count = Math.Min(maximum, chips != null ? chips.Length : 0);
            float width = 0f;
            for (int i = 0; i < count; i++)
            {
                if (chips[i] == null)
                    continue;
                width += MeasureStatusChipWidth(chips[i]) + (width > 0f ? 4f : 0f);
            }
            return Math.Min(width, Math.Max(0f, maximumWidth));
        }

        private float MeasureStatusChipWidth(ScenarioAuthoringStatusChipViewModel chip)
        {
            if (chip == null)
                return 0f;
            GUIStyle style = ResolveStatusChipStyle(chip.Tone);
            return Mathf.Clamp(ScenarioUiMeasuredLabel.Width(chip.Text ?? string.Empty, style, 12f), 28f, 280f);
        }

        private GUIStyle ResolveStatusChipStyle(ScenarioAuthoringStatusTone tone)
        {
            switch (tone)
            {
                case ScenarioAuthoringStatusTone.Informational:
                    return _uiContext.Styles.ChipInformational;
                case ScenarioAuthoringStatusTone.Ready:
                    return _uiContext.Styles.ChipReady;
                case ScenarioAuthoringStatusTone.Warning:
                    return _uiContext.Styles.ChipWarning;
                case ScenarioAuthoringStatusTone.Error:
                    return _uiContext.Styles.ChipError;
                default:
                    return _uiContext.Styles.ChipNeutral;
            }
        }

        private Texture2D ResolveWorkspaceRailTexture(string workspaceId)
        {
            string id = (workspaceId ?? string.Empty).ToLowerInvariant();
            if (id.IndexOf("cast", StringComparison.Ordinal) >= 0)
                return _uiContext.Styles.WorkspaceCastTexture;
            if (id.IndexOf("suppl", StringComparison.Ordinal) >= 0)
                return _uiContext.Styles.WorkspaceSuppliesTexture;
            if (id.IndexOf("map", StringComparison.Ordinal) >= 0 || id.IndexOf("world", StringComparison.Ordinal) >= 0)
                return _uiContext.Styles.WorkspaceMapTexture;
            if (id.IndexOf("test", StringComparison.Ordinal) >= 0)
                return _uiContext.Styles.WorkspaceTestTexture;
            if (id.IndexOf("publish", StringComparison.Ordinal) >= 0)
                return _uiContext.Styles.WorkspacePublishTexture;
            return _uiContext.Styles.WorkspaceStoryTexture;
        }

        private static string ResolveWorkspaceSubtabId(ScenarioAuthoringWorkspaceViewModel workspace)
        {
            if (workspace == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(workspace.ActiveSubtabId))
                return workspace.ActiveSubtabId;
            for (int i = 0; workspace.Subtabs != null && i < workspace.Subtabs.Length; i++)
            {
                ScenarioAuthoringWorkspaceSubtabViewModel subtab = workspace.Subtabs[i];
                if (subtab != null && subtab.Selected && !string.IsNullOrEmpty(subtab.Id))
                    return subtab.Id;
            }
            return workspace.Subtabs != null && workspace.Subtabs.Length > 0 && workspace.Subtabs[0] != null
                ? workspace.Subtabs[0].Id ?? string.Empty
                : string.Empty;
        }

        private static string ResolveWorkspaceOwnerToken(
            ScenarioAuthoringShellWindowViewModel window,
            ScenarioAuthoringWorkspaceViewModel workspace)
        {
            if (window != null && !string.IsNullOrEmpty(window.Id))
                return window.Id;
            if (workspace != null && !string.IsNullOrEmpty(workspace.Id))
                return workspace.Id;
            return "page";
        }

        private static Rect InsetWorkspaceRect(Rect rect, float inset)
        {
            return new Rect(
                rect.x + inset,
                rect.y + inset,
                Math.Max(0f, rect.width - (inset * 2f)),
                Math.Max(0f, rect.height - (inset * 2f)));
        }

        private static void ExecuteWorkspaceAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || !action.Enabled || string.IsNullOrEmpty(action.Id))
                return;
            ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
            if (Event.current != null)
                Event.current.Use();
        }
    }
}
