using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Rendering.Models;
using UnityEngine;
using Cortex.Services.Onboarding;
using Cortex.Shell.Unity.Imgui;

namespace Cortex.Modules.Onboarding
{
    public sealed class OnboardingModule
    {
        private const float InnerPadding = 18f;
        private const float HeaderHeight = 82f;
        private const float FooterHeight = 40f;
        private const int ThemesPerRow = 3;
        private const float WorkspaceFieldHeight = 26f;
        private readonly CortexOnboardingFlowService _flowService = new CortexOnboardingFlowService();
        private readonly CortexOnboardingProjectSetupService _projectSetupService = new CortexOnboardingProjectSetupService();

        private struct ThemePreviewPalette
        {
            public Color Background;
            public Color Surface;
            public Color Header;
            public Color Border;
            public Color Accent;
            public Color Text;
            public Color Muted;
            public Color Warning;
            public Color Error;
            public Color Selection;
            public Color Gutter;
            public Color Editor;
        }

        public bool Draw(
            Rect modalRect,
            CortexOnboardingState onboardingState,
            CortexOnboardingCatalog catalog,
            CortexOnboardingService onboardingService,
            IPathInteractionService pathInteractionService,
            bool previewBackground)
        {
            if (onboardingState == null || catalog == null || onboardingService == null)
            {
                return false;
            }

            var flow = _flowService.BuildFlow(onboardingState, catalog, onboardingService);
            if (flow.Steps.Count == 0)
            {
                return false;
            }

            _flowService.ClampActiveStepIndex(onboardingState, flow);

            var innerRect = new Rect(
                modalRect.x + InnerPadding,
                modalRect.y + InnerPadding,
                modalRect.width - (InnerPadding * 2f),
                modalRect.height - (InnerPadding * 2f));
            var headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, HeaderHeight);
            var footerRect = new Rect(innerRect.x, innerRect.yMax - FooterHeight, innerRect.width, FooterHeight);
            var bodyRect = new Rect(
                innerRect.x,
                headerRect.yMax + 8f,
                innerRect.width,
                Mathf.Max(0f, footerRect.y - headerRect.yMax - 16f));

            GUILayout.BeginArea(headerRect);
            DrawHeader(onboardingState, previewBackground, flow);
            GUILayout.EndArea();

            DrawStepBody(bodyRect, onboardingState, catalog, onboardingService, pathInteractionService, flow);

            GUILayout.BeginArea(footerRect);
            var finishRequested = DrawFooter(onboardingState, flow);
            GUILayout.EndArea();
            return finishRequested;
        }

        private static void DrawHeader(CortexOnboardingState onboardingState, bool previewBackground, CortexOnboardingFlowModel flow)
        {
            var titleStyle = CreateTitleStyle();
            var bodyStyle = CreateBodyStyle();
            var stepStyle = CreateStepStyle();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("Welcome to Cortex", titleStyle, GUILayout.Height(34f));
            GUILayout.Label(
                previewBackground
                    ? "Preview the shell behind this overlay. Cortex stays blocked until you finish."
                    : (flow.Steps.Count > 3
                        ? "Set your starting profile, link your active content, then choose a layout and theme. Defaults are already selected."
                        : "Set your starting profile, layout, and theme. Defaults are already selected."),
                bodyStyle,
                GUILayout.Height(34f));
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            for (var i = 0; i < flow.Steps.Count; i++)
            {
                DrawStepChip((i + 1).ToString(), flow.Steps[i].Label, onboardingState.ActiveStepIndex == i, stepStyle);
                if (i < flow.Steps.Count - 1)
                {
                    GUILayout.Space(8f);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();
        }

        private void DrawStepBody(
            Rect bodyRect,
            CortexOnboardingState onboardingState,
            CortexOnboardingCatalog catalog,
            CortexOnboardingService onboardingService,
            IPathInteractionService pathInteractionService,
            CortexOnboardingFlowModel flow)
        {
            var step = flow != null ? flow.ActiveStep : null;
            if (step == null)
            {
                return;
            }

            GUILayout.BeginArea(bodyRect);
            DrawSectionIntro(step.Title, step.Description);
            GUILayout.Space(8f);
            if (step.IsScrollable)
            {
                var scrollPosition = GUILayout.BeginScrollView(GetStepScrollPosition(onboardingState, step.StepKind), false, true, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                SetStepScrollPosition(onboardingState, step.StepKind, scrollPosition);
                DrawStepContent(step.StepKind, onboardingState, catalog, onboardingService, pathInteractionService);
                GUILayout.EndScrollView();
            }
            else
            {
                DrawStepContent(step.StepKind, onboardingState, catalog, onboardingService, pathInteractionService);
            }

            GUILayout.EndArea();
        }

        private void DrawStepContent(
            CortexOnboardingStepKind stepKind,
            CortexOnboardingState onboardingState,
            CortexOnboardingCatalog catalog,
            CortexOnboardingService onboardingService,
            IPathInteractionService pathInteractionService)
        {
            switch (stepKind)
            {
                case CortexOnboardingStepKind.Profile:
                    DrawProfileStep(onboardingState, catalog, onboardingService);
                    return;
                case CortexOnboardingStepKind.Projects:
                    DrawModProjectStep(onboardingState, pathInteractionService);
                    return;
                case CortexOnboardingStepKind.Layout:
                    DrawLayoutStep(onboardingState, catalog, onboardingService);
                    return;
                case CortexOnboardingStepKind.Theme:
                    DrawThemeStep(onboardingState, catalog, onboardingService);
                    return;
            }
        }

        private static Vector2 GetStepScrollPosition(CortexOnboardingState onboardingState, CortexOnboardingStepKind stepKind)
        {
            if (onboardingState == null)
            {
                return Vector2.zero;
            }

            switch (stepKind)
            {
                case CortexOnboardingStepKind.Projects:
                    return ToVector2(onboardingState.ModProjectScroll);
                case CortexOnboardingStepKind.Theme:
                    return ToVector2(onboardingState.ThemeScroll);
                default:
                    return Vector2.zero;
            }
        }

        private static void SetStepScrollPosition(CortexOnboardingState onboardingState, CortexOnboardingStepKind stepKind, Vector2 value)
        {
            if (onboardingState == null)
            {
                return;
            }

            switch (stepKind)
            {
                case CortexOnboardingStepKind.Projects:
                    onboardingState.ModProjectScroll = ToRenderPoint(value);
                    break;
                case CortexOnboardingStepKind.Theme:
                    onboardingState.ThemeScroll = ToRenderPoint(value);
                    break;
            }
        }

        private static Vector2 ToVector2(RenderPoint point)
        {
            return new Vector2(point.X, point.Y);
        }

        private static RenderPoint ToRenderPoint(Vector2 point)
        {
            return new RenderPoint(point.x, point.y);
        }

        private static void DrawProfileStep(CortexOnboardingState onboardingState, CortexOnboardingCatalog catalog, CortexOnboardingService onboardingService)
        {
            GUILayout.BeginHorizontal();
            for (var i = 0; i < catalog.Profiles.Count; i++)
            {
                var profile = catalog.Profiles[i];
                if (profile == null)
                {
                    continue;
                }

                if (DrawOptionCard(
                    profile.DisplayName,
                    profile.Description,
                    profile.IsDefault ? "Default" : string.Empty,
                    string.Equals(onboardingState.SelectedProfileId, profile.ProfileId, StringComparison.OrdinalIgnoreCase),
                    214f,
                    delegate(Rect previewRect) { DrawProfilePreview(previewRect, profile); }))
                {
                    onboardingService.SelectProfile(onboardingState, catalog, profile.ProfileId);
                }

                if (i < catalog.Profiles.Count - 1)
                {
                    GUILayout.Space(12f);
                }
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawLayoutStep(CortexOnboardingState onboardingState, CortexOnboardingCatalog catalog, CortexOnboardingService onboardingService)
        {
            GUILayout.BeginHorizontal();
            for (var i = 0; i < catalog.LayoutPresets.Count; i++)
            {
                var layoutPreset = catalog.LayoutPresets[i];
                if (layoutPreset == null)
                {
                    continue;
                }

                if (DrawOptionCard(
                    layoutPreset.DisplayName,
                    layoutPreset.Description,
                    layoutPreset.IsDefault ? "Default" : string.Empty,
                    string.Equals(onboardingState.SelectedLayoutPresetId, layoutPreset.LayoutPresetId, StringComparison.OrdinalIgnoreCase),
                    228f,
                    delegate(Rect previewRect) { DrawLayoutPreview(previewRect, layoutPreset); }))
                {
                    onboardingService.SelectLayoutPreset(onboardingState, catalog, layoutPreset.LayoutPresetId);
                }

                if (i < catalog.LayoutPresets.Count - 1)
                {
                    GUILayout.Space(12f);
                }
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawThemeStep(CortexOnboardingState onboardingState, CortexOnboardingCatalog catalog, CortexOnboardingService onboardingService)
        {
            for (var rowStart = 0; rowStart < catalog.Themes.Count; rowStart += ThemesPerRow)
            {
                GUILayout.BeginHorizontal();
                var rowEnd = Mathf.Min(rowStart + ThemesPerRow, catalog.Themes.Count);
                for (var i = rowStart; i < rowEnd; i++)
                {
                    var theme = catalog.Themes[i];
                    if (theme == null)
                    {
                        continue;
                    }

                    if (DrawOptionCard(
                        theme.DisplayName,
                        theme.Description,
                        theme.IsDefault ? "Default" : string.Empty,
                        string.Equals(onboardingState.SelectedThemeId, theme.ThemeId, StringComparison.OrdinalIgnoreCase),
                        208f,
                        delegate(Rect previewRect) { DrawThemePreview(previewRect, theme); }))
                    {
                        onboardingService.SelectTheme(onboardingState, theme.ThemeId);
                    }

                    if (i < rowEnd - 1)
                    {
                        GUILayout.Space(12f);
                    }
                }

                GUILayout.EndHorizontal();
                if (rowEnd < catalog.Themes.Count)
                {
                    GUILayout.Space(12f);
                }
            }

            GUILayout.Space(4f);
        }

        private void DrawModProjectStep(CortexOnboardingState onboardingState, IPathInteractionService pathInteractionService)
        {
            if (onboardingState == null)
            {
                return;
            }

            DrawWorkspaceRootCard(onboardingState, pathInteractionService);

            GUILayout.Space(10f);

            if (onboardingState.ModProjectDrafts.Count == 0)
            {
                DrawEmptyModProjectCard();
                return;
            }

            DrawModProjectSummary(onboardingState);
            GUILayout.Space(10f);

            for (var i = 0; i < onboardingState.ModProjectDrafts.Count; i++)
            {
                DrawModProjectCard(onboardingState, onboardingState.ModProjectDrafts[i], pathInteractionService);
                if (i < onboardingState.ModProjectDrafts.Count - 1)
                {
                    GUILayout.Space(10f);
                }
            }
        }

        private bool DrawFooter(CortexOnboardingState onboardingState, CortexOnboardingFlowModel flow)
        {
            GUILayout.BeginHorizontal();
            onboardingState.KeepFocused = GUILayout.Toggle(
                onboardingState.KeepFocused,
                "Keep onboarding focused",
                CreateToggleStyle(),
                GUILayout.Width(220f),
                GUILayout.Height(26f));

            GUILayout.FlexibleSpace();

            GUI.enabled = _flowService.CanMovePrevious(flow);
            if (GUILayout.Button("Back", GUILayout.Width(96f), GUILayout.Height(28f)))
            {
                _flowService.MovePrevious(onboardingState, flow);
            }

            GUI.enabled = true;
            GUILayout.Space(8f);

            if (!_flowService.IsFinalStep(flow))
            {
                if (GUILayout.Button("Next", GUILayout.Width(120f), GUILayout.Height(28f)))
                {
                    _flowService.MoveNext(onboardingState, flow);
                }

                GUILayout.EndHorizontal();
                return false;
            }

            var finishRequested = GUILayout.Button("Finish", GUILayout.Width(120f), GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            return finishRequested;
        }

        private static bool DrawOptionCard(string title, string description, string badgeText, bool isSelected, float height, Action<Rect> drawPreview)
        {
            var rect = GUILayoutUtility.GetRect(0f, 10000f, height, height, GUILayout.ExpandWidth(true), GUILayout.MinHeight(height));
            var current = Event.current;
            var isHovered = current != null && rect.Contains(current.mousePosition);
            var clicked = current != null &&
                current.type == EventType.MouseDown &&
                current.button == 0 &&
                rect.Contains(current.mousePosition);

            if (clicked)
            {
                current.Use();
            }

            DrawCardChrome(rect, isSelected, isHovered);
            if (!string.IsNullOrEmpty(badgeText))
            {
                DrawBadge(new Rect(rect.xMax - 92f, rect.y + 12f, 74f, 20f), badgeText);
            }

            var previewRect = new Rect(rect.x + 14f, rect.y + 14f, rect.width - 28f, 74f);
            if (drawPreview != null)
            {
                drawPreview(previewRect);
            }

            GUI.Label(new Rect(rect.x + 14f, rect.y + 100f, rect.width - 28f, 24f), title, CreateCardTitleStyle(isSelected));
            GUI.Label(new Rect(rect.x + 14f, rect.y + 126f, rect.width - 28f, rect.height - 138f), description, CreateCardBodyStyle());
            return clicked;
        }

        private static void DrawSectionIntro(string title, string description)
        {
            GUILayout.Label(title, CreateSectionTitleStyle(), GUILayout.Height(24f));
            GUILayout.Label(description, CreateBodyStyle(), GUILayout.Height(24f));
        }

        private static void DrawProfilePreview(Rect rect, OnboardingProfileContribution profile)
        {
            DrawMiniPanel(rect, ImguiWorkbenchLayout.GetSurfaceColor());
            var labels = GetProfilePreviewTags(profile);
            for (var i = 0; i < labels.Length; i++)
            {
                var chipRect = new Rect(rect.x + 10f + (i * 86f), rect.y + 24f, 78f, 22f);
                DrawBadge(chipRect, labels[i]);
            }
        }

        private static void DrawLayoutPreview(Rect rect, OnboardingLayoutPresetContribution layoutPreset)
        {
            DrawMiniPanel(rect, ImguiWorkbenchLayout.GetBackgroundColor());
            var inner = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
            var hasLeft = !string.IsNullOrEmpty(layoutPreset.DefaultPrimarySideContainerId);
            var hasRight = !string.IsNullOrEmpty(layoutPreset.DefaultSecondarySideContainerId);
            var hasBottom = !string.IsNullOrEmpty(layoutPreset.DefaultPanelContainerId);
            var leftWidth = hasLeft ? inner.width * 0.22f : 0f;
            var rightWidth = hasRight ? inner.width * 0.18f : 0f;
            var bottomHeight = hasBottom ? inner.height * 0.22f : 0f;

            DrawBlock(new Rect(inner.x, inner.y, inner.width, 10f), ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetHeaderColor(), Color.black, 0.18f));

            var centerRect = new Rect(
                inner.x + leftWidth + 4f,
                inner.y + 16f,
                inner.width - leftWidth - rightWidth - 8f,
                inner.height - bottomHeight - 20f);

            if (hasLeft)
            {
                DrawBlock(new Rect(inner.x + 4f, inner.y + 4f, leftWidth - 6f, inner.height - bottomHeight - 8f), ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetSurfaceColor(), 0.58f));
                DrawPreviewLabel(new Rect(inner.x + 8f, inner.y + 16f, leftWidth - 14f, 16f), layoutPreset.PreviewPrimaryLabel);
            }

            DrawBlock(centerRect, ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), Color.white, 0.04f));
            DrawPreviewLabel(new Rect(centerRect.x + 8f, centerRect.y + 8f, centerRect.width - 16f, 16f), GetPreviewLabel(layoutPreset.PreviewCenterLabel, "Editor"));

            if (hasRight)
            {
                DrawBlock(new Rect(centerRect.xMax + 4f, inner.y + 4f, rightWidth - 6f, inner.height - bottomHeight - 8f), ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetHeaderColor(), ImguiWorkbenchLayout.GetSurfaceColor(), 0.48f));
                DrawPreviewLabel(new Rect(centerRect.xMax + 8f, inner.y + 16f, rightWidth - 14f, 16f), layoutPreset.PreviewSecondaryLabel);
            }

            if (hasBottom)
            {
                DrawBlock(new Rect(inner.x + 4f, centerRect.yMax + 4f, inner.width - 8f, bottomHeight - 6f), ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetWarningColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.74f));
                DrawPreviewLabel(new Rect(inner.x + 10f, centerRect.yMax + 8f, inner.width - 20f, 16f), layoutPreset.PreviewPanelLabel);
            }
        }

        private static void DrawThemePreview(Rect rect, ThemeContribution theme)
        {
            var palette = BuildThemePreviewPalette(theme);
            DrawMiniPanel(rect, palette.Background);

            var topBarRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 12f);
            var gutterRect = new Rect(rect.x + 8f, rect.y + 24f, 26f, rect.height - 32f);
            var railRect = new Rect(rect.x + 38f, rect.y + 24f, 34f, rect.height - 32f);
            var editorRect = new Rect(rect.x + 76f, rect.y + 24f, rect.width - 84f, rect.height - 32f);
            var tabRect = new Rect(editorRect.x + 8f, topBarRect.y + 2f, Mathf.Min(64f, editorRect.width * 0.42f), 8f);

            DrawBlock(topBarRect, palette.Header);
            DrawBlock(gutterRect, palette.Gutter);
            DrawBlock(railRect, palette.Surface);
            DrawBlock(editorRect, palette.Editor);
            DrawBlock(tabRect, palette.Accent);
            DrawBlock(new Rect(topBarRect.xMax - 18f, topBarRect.y + 2f, 10f, 8f), palette.Selection);
            DrawThemePreviewCodeLine(new Rect(editorRect.x + 8f, editorRect.y + 8f, editorRect.width * 0.52f, 5f), palette.Muted);
            DrawThemePreviewCodeLine(new Rect(editorRect.x + 8f, editorRect.y + 18f, editorRect.width * 0.72f, 5f), palette.Accent);
            DrawThemePreviewCodeLine(new Rect(editorRect.x + 16f, editorRect.y + 28f, editorRect.width * 0.44f, 5f), palette.Text);
            DrawThemePreviewCodeLine(new Rect(editorRect.x + 24f, editorRect.y + 38f, editorRect.width * 0.48f, 5f), palette.Warning);
            DrawThemePreviewCodeLine(new Rect(editorRect.x + 32f, editorRect.y + 48f, editorRect.width * 0.38f, 5f), palette.Error);
            DrawThemePreviewCodeLine(new Rect(editorRect.x + 12f, editorRect.y + 58f, editorRect.width * 0.58f, 5f), palette.Selection);
            DrawBorder(editorRect, palette.Border, 1f);
        }

        private static void DrawThemePreviewCodeLine(Rect rect, Color color)
        {
            DrawBlock(rect, color);
        }

        private static void DrawWorkspaceRootCard(CortexOnboardingState onboardingState, IPathInteractionService pathInteractionService)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Workspace Root", CreateSectionTitleStyle(), GUILayout.Height(22f));
            GUILayout.Label(
                "Set the broad workspace root Cortex should scan for project discovery. Leave it empty if you only want to map specific source trees here.",
                CreateBodyStyle());
            GUILayout.Space(6f);
            onboardingState.SelectedWorkspaceRootPath = DrawPathEditor(
                "onboarding.workspaceRoot",
                "Workspace Root",
                onboardingState.SelectedWorkspaceRootPath,
                pathInteractionService,
                new PathSelectionRequest
                {
                    SelectionKind = PathSelectionKind.Folder,
                    Title = "Select workspace root",
                    InitialPath = onboardingState.SelectedWorkspaceRootPath
                });
            GUILayout.EndVertical();
        }

        private static void DrawEmptyModProjectCard()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("No active content is available right now.", CreateSectionTitleStyle(), GUILayout.Height(22f));
            GUILayout.Label(
                "Finish onboarding now and link projects later from the Projects or Settings surfaces when active content is available.",
                CreateBodyStyle());
            GUILayout.EndVertical();
        }

        private static void DrawModProjectSummary(CortexOnboardingState onboardingState)
        {
            var ownedCount = 0;
            for (var i = 0; i < onboardingState.ModProjectDrafts.Count; i++)
            {
                if (onboardingState.ModProjectDrafts[i] != null && onboardingState.ModProjectDrafts[i].IsOwnedByUser)
                {
                    ownedCount++;
                }
            }

            GUILayout.BeginHorizontal();
            DrawStatPill("Active Items", onboardingState.ModProjectDrafts.Count.ToString());
            GUILayout.Space(8f);
            DrawStatPill("Owned By You", ownedCount.ToString());
            GUILayout.EndHorizontal();
        }

        private void DrawModProjectCard(
            CortexOnboardingState onboardingState,
            CortexOnboardingModProjectDraft draft,
            IPathInteractionService pathInteractionService)
        {
            if (onboardingState == null || draft == null)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(ResolveDraftDisplayName(draft), CreateSectionTitleStyle(), GUILayout.Height(22f));
            GUILayout.Label("Mod ID  " + (draft.ModId ?? string.Empty), CreateMetaStyle(), GUILayout.Height(18f));
            GUILayout.EndVertical();
            if (draft.HasExistingMapping)
            {
                DrawInlineBadge("Mapped", ImguiWorkbenchLayout.GetAccentColor(), 74f);
                GUILayout.Space(6f);
            }

            if (DrawOwnershipToggleButton(draft.IsOwnedByUser))
            {
                _projectSetupService.ToggleOwnership(draft);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            DrawMetadataRow("Runtime Content Root", string.IsNullOrEmpty(draft.RootPath) ? "Unknown" : draft.RootPath);
            if (!draft.IsOwnedByUser)
            {
                GUILayout.Space(6f);
                GUILayout.Label(
                    "Leave this compact if the mod is active but you do not want Cortex mapping it to local source during onboarding.",
                    CreateHintStyle());
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Space(10f);
            DrawHorizontalRule(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetBorderColor(), ImguiWorkbenchLayout.GetAccentColor(), 0.22f), 1f);
            GUILayout.Space(8f);
            GUILayout.Label("Source Setup", CreateSubsectionTitleStyle(), GUILayout.Height(20f));
            GUILayout.Label(
                "Point Cortex at the editable source root for this mod. It will infer the project file and create the mapping on finish.",
                CreateBodyStyle());
            GUILayout.Space(6f);
            draft.SourceRootPath = DrawPathEditor(
                "onboarding.mod." + ResolveDraftFieldKey(draft) + ".sourceRoot",
                "Source Root",
                draft.SourceRootPath,
                pathInteractionService,
                new PathSelectionRequest
                {
                    SelectionKind = PathSelectionKind.Folder,
                    Title = "Select source root",
                    InitialPath = !string.IsNullOrEmpty(draft.SourceRootPath)
                        ? draft.SourceRootPath
                        : (!string.IsNullOrEmpty(onboardingState.SelectedWorkspaceRootPath)
                            ? onboardingState.SelectedWorkspaceRootPath
                            : draft.RootPath)
                });
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var previousEnabled = GUI.enabled;
            GUI.enabled = !string.IsNullOrEmpty(onboardingState.SelectedWorkspaceRootPath);
            if (GUILayout.Button("Use Workspace Root", GUILayout.Width(150f), GUILayout.Height(24f)))
            {
                _projectSetupService.UseSelectedWorkspaceRoot(onboardingState, draft);
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            if (string.IsNullOrEmpty(draft.SourceRootPath))
            {
                GUILayout.Space(6f);
                GUILayout.Label("Set a source root for this mod before finishing onboarding.", CreateWarningStyle());
            }

            GUILayout.EndVertical();
        }

        private static void DrawStepChip(string index, string label, bool isActive, GUIStyle style)
        {
            var chipRect = GUILayoutUtility.GetRect(94f, 94f, 30f, 30f, GUILayout.Width(94f), GUILayout.Height(30f));
            DrawCardChrome(chipRect, isActive, false);
            GUI.Label(new Rect(chipRect.x + 10f, chipRect.y + 6f, chipRect.width - 20f, 18f), index + "  " + label, style);
        }

        private static void DrawCardChrome(Rect rect, bool isSelected, bool isHovered)
        {
            var fill = isSelected
                ? ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetHeaderColor(), ImguiWorkbenchLayout.GetAccentColor(), 0.18f)
                : (isHovered
                    ? ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.4f)
                    : ImguiWorkbenchLayout.GetSurfaceColor());
            var border = isSelected ? ImguiWorkbenchLayout.GetAccentColor() : ImguiWorkbenchLayout.GetBorderColor();
            DrawBlock(rect, fill);
            DrawBorder(rect, border, isSelected ? 2f : 1f);
        }

        private static void DrawMiniPanel(Rect rect, Color backgroundColor)
        {
            DrawBlock(rect, backgroundColor);
            DrawBorder(rect, ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetBorderColor(), Color.white, 0.06f), 1f);
        }

        private static void DrawBadge(Rect rect, string text)
        {
            DrawBlock(rect, ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.3f));
            DrawBorder(rect, ImguiWorkbenchLayout.GetAccentColor(), 1f);
            GUI.Label(rect, text, CreateBadgeStyle());
        }

        private static void DrawPreviewLabel(Rect rect, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            GUI.Label(rect, text, CreatePreviewLabelStyle(TextAnchor.UpperLeft));
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawBlock(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawBlock(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawBlock(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawBlock(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void DrawBlock(Rect rect, Color color)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;
        }

        private static GUIStyle CreateTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 26;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateSectionTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 18;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateBodyStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.normal.textColor = ImguiWorkbenchLayout.GetMutedTextColor();
            return style;
        }

        private static GUIStyle CreateCardTitleStyle(bool isSelected)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 16;
            style.normal.textColor = isSelected ? Color.white : ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateCardBodyStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.normal.textColor = ImguiWorkbenchLayout.GetMutedTextColor();
            return style;
        }

        private static GUIStyle CreateBadgeStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle CreateStepStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreatePreviewLabelStyle(TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = alignment;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle CreateToggleStyle()
        {
            var style = new GUIStyle(GUI.skin.toggle);
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            style.onNormal.textColor = Color.white;
            return style;
        }

        private static GUIStyle CreateSubsectionTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 14;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateMetaStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 11;
            style.normal.textColor = ImguiWorkbenchLayout.GetMutedTextColor();
            return style;
        }

        private static GUIStyle CreateMetaLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 11;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetMutedTextColor(), ImguiWorkbenchLayout.GetTextColor(), 0.18f);
            return style;
        }

        private static GUIStyle CreatePathPreviewStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateHintStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.fontSize = 11;
            style.normal.textColor = ImguiWorkbenchLayout.GetMutedTextColor();
            return style;
        }

        private static GUIStyle CreateWarningStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.fontSize = 11;
            style.normal.textColor = ImguiWorkbenchLayout.GetWarningColor();
            return style;
        }

        private static GUIStyle CreateStatStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 20;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static ThemePreviewPalette BuildThemePreviewPalette(ThemeContribution theme)
        {
            var background = ParseThemeColor(theme != null ? theme.BackgroundColor : string.Empty, new Color(0.08f, 0.08f, 0.1f, 1f));
            var surface = ParseThemeColor(theme != null ? theme.SurfaceColor : string.Empty, new Color(0.14f, 0.14f, 0.18f, 1f));
            var header = ParseThemeColor(theme != null ? theme.HeaderColor : string.Empty, new Color(0.18f, 0.18f, 0.22f, 1f));
            var border = ParseThemeColor(theme != null ? theme.BorderColor : string.Empty, new Color(0.28f, 0.3f, 0.34f, 1f));
            var accent = ParseThemeColor(theme != null ? theme.AccentColor : string.Empty, new Color(0.35f, 0.62f, 1f, 1f));
            var text = ParseThemeColor(theme != null ? theme.TextColor : string.Empty, new Color(0.88f, 0.9f, 0.94f, 1f));
            var muted = ParseThemeColor(theme != null ? theme.MutedTextColor : string.Empty, new Color(0.62f, 0.66f, 0.74f, 1f));
            var warning = ParseThemeColor(theme != null ? theme.WarningColor : string.Empty, new Color(0.95f, 0.78f, 0.36f, 1f));
            var error = ParseThemeColor(theme != null ? theme.ErrorColor : string.Empty, new Color(0.9f, 0.42f, 0.42f, 1f));

            return new ThemePreviewPalette
            {
                Background = background,
                Surface = surface,
                Header = header,
                Border = border,
                Accent = accent,
                Text = text,
                Muted = muted,
                Warning = warning,
                Error = error,
                Selection = ImguiWorkbenchLayout.Blend(accent, text, 0.28f),
                Gutter = ImguiWorkbenchLayout.Blend(background, surface, 0.52f),
                Editor = ImguiWorkbenchLayout.Blend(surface, text, 0.04f)
            };
        }

        private static Color ParseThemeColor(string colorValue, Color fallback)
        {
            return ImguiWorkbenchLayout.ParseColor(colorValue, fallback);
        }

        private static string[] GetProfilePreviewTags(OnboardingProfileContribution profile)
        {
            if (profile != null && profile.PreviewTags != null && profile.PreviewTags.Length > 0)
            {
                return profile.PreviewTags;
            }

            return new[] { "Code", "Build", "Navigate" };
        }

        private static string GetPreviewLabel(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static bool DrawOwnershipToggleButton(bool isOwnedByUser)
        {
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.backgroundColor = isOwnedByUser
                ? ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.28f)
                : ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.78f);
            GUI.contentColor = isOwnedByUser ? Color.white : ImguiWorkbenchLayout.GetTextColor();
            var clicked = GUILayout.Button(
                isOwnedByUser ? "Maintained By Me" : "Mark As Mine",
                GUILayout.Width(138f),
                GUILayout.Height(24f));
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            return clicked;
        }

        private static void DrawInlineBadge(string text, Color accentColor, float width)
        {
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.backgroundColor = ImguiWorkbenchLayout.Blend(accentColor, ImguiWorkbenchLayout.GetHeaderColor(), 0.25f);
            GUI.contentColor = Color.white;
            GUILayout.Label(text, GUI.skin.box, GUILayout.Width(width), GUILayout.Height(22f));
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
        }

        private static void DrawMetadataRow(string label, string value)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinHeight(52f));
            GUILayout.Label(label ?? string.Empty, CreateMetaLabelStyle(), GUILayout.Height(18f));
            GUILayout.Label(value ?? string.Empty, CreatePathPreviewStyle());
            GUILayout.EndVertical();
        }

        private static string DrawPathEditor(
            string fieldId,
            string label,
            string value,
            IPathInteractionService pathInteractionService,
            PathSelectionRequest browseRequest)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(label ?? string.Empty, CreateMetaLabelStyle(), GUILayout.Height(18f));
            var nextValue = CortexPathField.DrawValueEditor(
                fieldId,
                value ?? string.Empty,
                pathInteractionService,
                new CortexPathFieldOptions
                {
                    AllowBrowse = true,
                    AllowOpen = true,
                    AllowPaste = true,
                    AllowClear = true,
                    BrowseRequest = browseRequest
                },
                GUILayout.Height(WorkspaceFieldHeight),
                GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
            return nextValue;
        }

        private static void DrawStatPill(string label, string value)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(126f), GUILayout.Height(48f));
            GUILayout.Label(label ?? string.Empty, CreateMetaLabelStyle(), GUILayout.Height(16f));
            GUILayout.Label(value ?? string.Empty, CreateStatStyle(), GUILayout.Height(22f));
            GUILayout.EndVertical();
        }

        private static void DrawHorizontalRule(Color color, float height)
        {
            var rect = GUILayoutUtility.GetRect(0f, 0f, height, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            DrawBlock(rect, color);
        }

        private static string DrawPathField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            var nextValue = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return nextValue;
        }

        private static string ResolveDraftDisplayName(CortexOnboardingModProjectDraft draft)
        {
            return string.IsNullOrEmpty(draft != null ? draft.DisplayName : string.Empty)
                ? (draft != null ? draft.ModId ?? string.Empty : string.Empty)
                : draft.DisplayName;
        }

        private static string ResolveDraftFieldKey(CortexOnboardingModProjectDraft draft)
        {
            var rawKey = draft != null && !string.IsNullOrEmpty(draft.ModId)
                ? draft.ModId
                : ResolveDraftDisplayName(draft);
            return string.IsNullOrEmpty(rawKey) ? "unknown" : rawKey.Replace(' ', '_');
        }
    }
}
