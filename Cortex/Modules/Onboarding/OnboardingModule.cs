using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Onboarding
{
    public sealed class OnboardingModule
    {
        private const float InnerPadding = 18f;
        private const float HeaderHeight = 82f;
        private const float FooterHeight = 40f;
        private const int ThemesPerRow = 3;

        private sealed class OnboardingStepDefinition
        {
            public string StepId = string.Empty;
            public string Label = string.Empty;
            public string Title = string.Empty;
            public string Description = string.Empty;
            public bool IsScrollable;
            public Func<CortexOnboardingState, Vector2> GetScrollPosition = delegate { return Vector2.zero; };
            public Action<CortexOnboardingState, Vector2> SetScrollPosition = delegate { };
            public Action<CortexOnboardingState, CortexOnboardingCatalog, CortexOnboardingService> Render = delegate { };
        }

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

        public bool Draw(Rect modalRect, CortexOnboardingState onboardingState, CortexOnboardingCatalog catalog, CortexOnboardingService onboardingService, bool previewBackground)
        {
            if (onboardingState == null || catalog == null || onboardingService == null)
            {
                return false;
            }

            var steps = BuildSteps(onboardingState, catalog, onboardingService);
            if (steps.Count == 0)
            {
                return false;
            }

            onboardingState.ActiveStepIndex = Mathf.Clamp(onboardingState.ActiveStepIndex, 0, steps.Count - 1);

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
            DrawHeader(onboardingState, previewBackground, steps);
            GUILayout.EndArea();

            DrawStepBody(bodyRect, onboardingState, catalog, onboardingService, steps);

            GUILayout.BeginArea(footerRect);
            var finishRequested = DrawFooter(onboardingState, steps.Count);
            GUILayout.EndArea();
            return finishRequested;
        }

        private static List<OnboardingStepDefinition> BuildSteps(
            CortexOnboardingState onboardingState,
            CortexOnboardingCatalog catalog,
            CortexOnboardingService onboardingService)
        {
            var steps = new List<OnboardingStepDefinition>();
            steps.Add(new OnboardingStepDefinition
            {
                StepId = "profile",
                Label = "Profile",
                Title = "Choose your starting profile",
                Description = "Pick the default posture Cortex should start from.",
                IsScrollable = false,
                Render = delegate(CortexOnboardingState state, CortexOnboardingCatalog currentCatalog, CortexOnboardingService service)
                {
                    DrawProfileStep(state, currentCatalog, service);
                }
            });
            steps.Add(new OnboardingStepDefinition
            {
                StepId = "layout",
                Label = "Layout",
                Title = "Choose a layout style",
                Description = "Select the shell arrangement you want Cortex to apply after onboarding.",
                IsScrollable = false,
                Render = delegate(CortexOnboardingState state, CortexOnboardingCatalog currentCatalog, CortexOnboardingService service)
                {
                    DrawLayoutStep(state, currentCatalog, service);
                }
            });
            steps.Add(new OnboardingStepDefinition
            {
                StepId = "theme",
                Label = "Theme",
                Title = "Choose a theme",
                Description = "Keep it simple for v1 with built-in themes. Custom colors can layer on top later.",
                IsScrollable = true,
                GetScrollPosition = delegate(CortexOnboardingState state) { return state != null ? state.ThemeScroll : Vector2.zero; },
                SetScrollPosition = delegate(CortexOnboardingState state, Vector2 value)
                {
                    if (state != null)
                    {
                        state.ThemeScroll = value;
                    }
                },
                Render = delegate(CortexOnboardingState state, CortexOnboardingCatalog currentCatalog, CortexOnboardingService service)
                {
                    DrawThemeStep(state, currentCatalog, service);
                }
            });
            return steps;
        }

        private static void DrawHeader(CortexOnboardingState onboardingState, bool previewBackground, IList<OnboardingStepDefinition> steps)
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
                    : "Set your starting profile, layout, and theme. Defaults are already selected.",
                bodyStyle,
                GUILayout.Height(34f));
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            for (var i = 0; i < steps.Count; i++)
            {
                DrawStepChip((i + 1).ToString(), steps[i].Label, onboardingState.ActiveStepIndex == i, stepStyle);
                if (i < steps.Count - 1)
                {
                    GUILayout.Space(8f);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();
        }

        private static void DrawStepBody(
            Rect bodyRect,
            CortexOnboardingState onboardingState,
            CortexOnboardingCatalog catalog,
            CortexOnboardingService onboardingService,
            IList<OnboardingStepDefinition> steps)
        {
            var step = steps[onboardingState.ActiveStepIndex];
            GUILayout.BeginArea(bodyRect);
            DrawSectionIntro(step.Title, step.Description);
            GUILayout.Space(8f);
            if (step.IsScrollable)
            {
                var scrollPosition = GUILayout.BeginScrollView(step.GetScrollPosition(onboardingState), false, true, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                step.SetScrollPosition(onboardingState, scrollPosition);
                step.Render(onboardingState, catalog, onboardingService);
                GUILayout.EndScrollView();
            }
            else
            {
                step.Render(onboardingState, catalog, onboardingService);
            }

            GUILayout.EndArea();
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

        private static bool DrawFooter(CortexOnboardingState onboardingState, int totalSteps)
        {
            GUILayout.BeginHorizontal();
            onboardingState.KeepFocused = GUILayout.Toggle(
                onboardingState.KeepFocused,
                "Keep onboarding focused",
                CreateToggleStyle(),
                GUILayout.Width(220f),
                GUILayout.Height(26f));

            GUILayout.FlexibleSpace();

            GUI.enabled = onboardingState.ActiveStepIndex > 0;
            if (GUILayout.Button("Back", GUILayout.Width(96f), GUILayout.Height(28f)))
            {
                onboardingState.ActiveStepIndex = Mathf.Max(0, onboardingState.ActiveStepIndex - 1);
            }

            GUI.enabled = true;
            GUILayout.Space(8f);

            if (onboardingState.ActiveStepIndex < totalSteps - 1)
            {
                if (GUILayout.Button("Next", GUILayout.Width(120f), GUILayout.Height(28f)))
                {
                    onboardingState.ActiveStepIndex = Mathf.Min(totalSteps - 1, onboardingState.ActiveStepIndex + 1);
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
            DrawMiniPanel(rect, CortexIdeLayout.GetSurfaceColor());
            var labels = GetProfilePreviewTags(profile);
            for (var i = 0; i < labels.Length; i++)
            {
                var chipRect = new Rect(rect.x + 10f + (i * 86f), rect.y + 24f, 78f, 22f);
                DrawBadge(chipRect, labels[i]);
            }
        }

        private static void DrawLayoutPreview(Rect rect, OnboardingLayoutPresetContribution layoutPreset)
        {
            DrawMiniPanel(rect, CortexIdeLayout.GetBackgroundColor());
            var inner = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
            var hasLeft = !string.IsNullOrEmpty(layoutPreset.DefaultPrimarySideContainerId);
            var hasRight = !string.IsNullOrEmpty(layoutPreset.DefaultSecondarySideContainerId);
            var hasBottom = !string.IsNullOrEmpty(layoutPreset.DefaultPanelContainerId);
            var leftWidth = hasLeft ? inner.width * 0.22f : 0f;
            var rightWidth = hasRight ? inner.width * 0.18f : 0f;
            var bottomHeight = hasBottom ? inner.height * 0.22f : 0f;

            DrawBlock(new Rect(inner.x, inner.y, inner.width, 10f), CortexIdeLayout.Blend(CortexIdeLayout.GetHeaderColor(), Color.black, 0.18f));

            var centerRect = new Rect(
                inner.x + leftWidth + 4f,
                inner.y + 16f,
                inner.width - leftWidth - rightWidth - 8f,
                inner.height - bottomHeight - 20f);

            if (hasLeft)
            {
                DrawBlock(new Rect(inner.x + 4f, inner.y + 4f, leftWidth - 6f, inner.height - bottomHeight - 8f), CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetSurfaceColor(), 0.58f));
                DrawPreviewLabel(new Rect(inner.x + 8f, inner.y + 16f, leftWidth - 14f, 16f), layoutPreset.PreviewPrimaryLabel);
            }

            DrawBlock(centerRect, CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), Color.white, 0.04f));
            DrawPreviewLabel(new Rect(centerRect.x + 8f, centerRect.y + 8f, centerRect.width - 16f, 16f), GetPreviewLabel(layoutPreset.PreviewCenterLabel, "Editor"));

            if (hasRight)
            {
                DrawBlock(new Rect(centerRect.xMax + 4f, inner.y + 4f, rightWidth - 6f, inner.height - bottomHeight - 8f), CortexIdeLayout.Blend(CortexIdeLayout.GetHeaderColor(), CortexIdeLayout.GetSurfaceColor(), 0.48f));
                DrawPreviewLabel(new Rect(centerRect.xMax + 8f, inner.y + 16f, rightWidth - 14f, 16f), layoutPreset.PreviewSecondaryLabel);
            }

            if (hasBottom)
            {
                DrawBlock(new Rect(inner.x + 4f, centerRect.yMax + 4f, inner.width - 8f, bottomHeight - 6f), CortexIdeLayout.Blend(CortexIdeLayout.GetWarningColor(), CortexIdeLayout.GetHeaderColor(), 0.74f));
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

        private static void DrawStepChip(string index, string label, bool isActive, GUIStyle style)
        {
            var chipRect = GUILayoutUtility.GetRect(94f, 94f, 30f, 30f, GUILayout.Width(94f), GUILayout.Height(30f));
            DrawCardChrome(chipRect, isActive, false);
            GUI.Label(new Rect(chipRect.x + 10f, chipRect.y + 6f, chipRect.width - 20f, 18f), index + "  " + label, style);
        }

        private static void DrawCardChrome(Rect rect, bool isSelected, bool isHovered)
        {
            var fill = isSelected
                ? CortexIdeLayout.Blend(CortexIdeLayout.GetHeaderColor(), CortexIdeLayout.GetAccentColor(), 0.18f)
                : (isHovered
                    ? CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.4f)
                    : CortexIdeLayout.GetSurfaceColor());
            var border = isSelected ? CortexIdeLayout.GetAccentColor() : CortexIdeLayout.GetBorderColor();
            DrawBlock(rect, fill);
            DrawBorder(rect, border, isSelected ? 2f : 1f);
        }

        private static void DrawMiniPanel(Rect rect, Color backgroundColor)
        {
            DrawBlock(rect, backgroundColor);
            DrawBorder(rect, CortexIdeLayout.Blend(CortexIdeLayout.GetBorderColor(), Color.white, 0.06f), 1f);
        }

        private static void DrawBadge(Rect rect, string text)
        {
            DrawBlock(rect, CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetHeaderColor(), 0.3f));
            DrawBorder(rect, CortexIdeLayout.GetAccentColor(), 1f);
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
            style.normal.textColor = CortexIdeLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateSectionTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 18;
            style.normal.textColor = CortexIdeLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateBodyStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.normal.textColor = CortexIdeLayout.GetMutedTextColor();
            return style;
        }

        private static GUIStyle CreateCardTitleStyle(bool isSelected)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 16;
            style.normal.textColor = isSelected ? Color.white : CortexIdeLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateCardBodyStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.normal.textColor = CortexIdeLayout.GetMutedTextColor();
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
            style.normal.textColor = CortexIdeLayout.GetTextColor();
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
            style.normal.textColor = CortexIdeLayout.GetTextColor();
            style.onNormal.textColor = Color.white;
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
                Selection = CortexIdeLayout.Blend(accent, text, 0.28f),
                Gutter = CortexIdeLayout.Blend(background, surface, 0.52f),
                Editor = CortexIdeLayout.Blend(surface, text, 0.04f)
            };
        }

        private static Color ParseThemeColor(string colorValue, Color fallback)
        {
            return CortexIdeLayout.ParseColor(colorValue, fallback);
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
    }
}
