using System;
using UnityEngine;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.UI.Compatibility;
using ShelteredAPI.UI.FieldManual.Textures;

namespace ShelteredAPI.Scenarios.Presentation.UiKit
{
    /// <summary>
    /// Process-pass cached GUI roles built exclusively from Phase 9 tokens.
    /// Renderers select semantic roles; no renderer owns token literals.
    /// </summary>
    internal sealed class ScenarioUiStyleSheet
    {
        private readonly ScenarioUiTheme _theme;
        private readonly ScenarioUiTextureCache _textures;

        public ScenarioUiStyleSheet(ScenarioUiTheme theme, ScenarioUiTextureCache textures)
        {
            _theme = theme ?? ScenarioUiTheme.Default();
            _textures = textures ?? new ScenarioUiTextureCache();
            Rebuild();
        }

        public ScenarioUiTheme Theme { get { return _theme; } }
        public ScenarioUiTextureCache Textures { get { return _textures; } }

        // Explicit material tiers.
        public GUIStyle Page { get; private set; }
        public GUIStyle Card { get; private set; }
        public GUIStyle Inset { get; private set; }
        public GUIStyle Chrome { get; private set; }
        public GUIStyle Viewport { get; private set; }

        // Compatibility surface roles.
        public GUIStyle PanelBase { get; private set; }
        public GUIStyle PanelRaised { get; private set; }
        public GUIStyle PanelInset { get; private set; }
        public GUIStyle Header { get; private set; }
        public GUIStyle Footer { get; private set; }
        public GUIStyle Status { get; private set; }
        public GUIStyle Section { get; private set; }
        public GUIStyle Menu { get; private set; }
        public GUIStyle Field { get; private set; }
        public GUIStyle SearchField { get; private set; }
        public GUIStyle Divider { get; private set; }

        // Semantic chips.
        public GUIStyle ChipNeutral { get; private set; }
        public GUIStyle ChipInformational { get; private set; }
        public GUIStyle ChipReady { get; private set; }
        public GUIStyle ChipWarning { get; private set; }
        public GUIStyle ChipError { get; private set; }
        public GUIStyle Pill { get; private set; }
        public GUIStyle PillEmphasized { get; private set; }
        public GUIStyle PillDanger { get; private set; }
        public GUIStyle PillSuccess { get; private set; }
        public GUIStyle PillWarning { get; private set; }

        // Text roles.
        public GUIStyle BrandTitleText { get; private set; }
        public GUIStyle TitleText { get; private set; }
        public GUIStyle HeaderTitleText { get; private set; }
        public GUIStyle HeaderSubtitleText { get; private set; }
        public GUIStyle SubtitleText { get; private set; }
        public GUIStyle SectionTitleText { get; private set; }
        public GUIStyle BodyText { get; private set; }
        public GUIStyle MutedText { get; private set; }
        public GUIStyle PaperTitleText { get; private set; }
        public GUIStyle PaperBodyText { get; private set; }
        public GUIStyle PaperMutedText { get; private set; }
        public GUIStyle EmptyStateText { get; private set; }
        public GUIStyle PillText { get; private set; }
        public GUIStyle BreadcrumbLinkText { get; private set; }
        public GUIStyle BreadcrumbCurrentText { get; private set; }

        // Controls and component states.
        public GUIStyle Button { get; private set; }
        public GUIStyle ButtonEmphasized { get; private set; }
        public GUIStyle ButtonActive { get; private set; }
        public GUIStyle ButtonDanger { get; private set; }
        public GUIStyle ButtonDisabled { get; private set; }
        public GUIStyle Tab { get; private set; }
        public GUIStyle TabActive { get; private set; }
        public GUIStyle TabDisabled { get; private set; }
        public GUIStyle NavigatorRow { get; private set; }
        public GUIStyle NavigatorRowHover { get; private set; }
        public GUIStyle NavigatorRowSelected { get; private set; }
        public GUIStyle NavigatorRowWarning { get; private set; }
        public GUIStyle CompactChoice { get; private set; }
        public GUIStyle CompactChoiceSelected { get; private set; }

        // Cached textures for direct painters.
        public Texture2D PageTexture { get; private set; }
        public Texture2D CardTexture { get; private set; }
        public Texture2D CardHoverTexture { get; private set; }
        public Texture2D CardSelectedTexture { get; private set; }
        public Texture2D InsetTexture { get; private set; }
        public Texture2D ChromeTexture { get; private set; }
        public Texture2D ShadowTexture { get; private set; }
        public Texture2D FocusBorderTexture { get; private set; }
        public Texture2D BorderHighlightTexture { get; private set; }
        public Texture2D SemanticReadyStrongTexture { get; private set; }
        public Texture2D SemanticWarningStrongTexture { get; private set; }
        public Texture2D SemanticErrorStrongTexture { get; private set; }
        public Texture2D SemanticInfoStrongTexture { get; private set; }
        public Texture2D WorkspaceStoryTexture { get; private set; }
        public Texture2D WorkspaceCastTexture { get; private set; }
        public Texture2D WorkspaceSuppliesTexture { get; private set; }
        public Texture2D WorkspaceMapTexture { get; private set; }
        public Texture2D WorkspaceTestTexture { get; private set; }
        public Texture2D WorkspacePublishTexture { get; private set; }

        public Texture2D PanelTexture { get; private set; }
        public Texture2D PanelRaisedTexture { get; private set; }
        public Texture2D PanelInsetTexture { get; private set; }
        public Texture2D BorderStrongTexture { get; private set; }
        public Texture2D BorderSubtleTexture { get; private set; }
        public Texture2D AccentActiveTexture { get; private set; }
        public Texture2D AccentHoverTexture { get; private set; }
        public Texture2D DangerTexture { get; private set; }
        public Texture2D DisabledTexture { get; private set; }
        public Texture2D ViewportTexture { get; private set; }
        public Texture2D BevelLightTexture { get; private set; }
        public Texture2D BevelDarkTexture { get; private set; }

        private void Rebuild()
        {
            IScenarioUiPalette palette = _theme.Palette;
            IScenarioUiMetrics m = _theme.Metrics;

            IScenarioUiPalette p = palette;

            PageTexture = _textures.Get(p.SurfacePage);
            CardTexture = _textures.Get(p.SurfaceCard);
            CardHoverTexture = _textures.Get(p.SurfaceCardHover);
            CardSelectedTexture = _textures.Get(p.SurfaceCardSelected);
            InsetTexture = _textures.Get(p.SurfaceInset);
            ChromeTexture = _textures.Get(p.SurfaceChrome);
            ShadowTexture = _textures.Get(p.DepthShadow);
            FocusBorderTexture = _textures.Get(p.BorderFocus);
            BorderHighlightTexture = _textures.Get(p.BorderHighlight);
            BorderStrongTexture = _textures.Get(p.BorderStrong);
            BorderSubtleTexture = _textures.Get(p.BorderDefault);
            SemanticReadyStrongTexture = _textures.Get(p.SemanticReadyStrong);
            SemanticWarningStrongTexture = _textures.Get(p.SemanticWarningStrong);
            SemanticErrorStrongTexture = _textures.Get(p.SemanticErrorStrong);
            SemanticInfoStrongTexture = _textures.Get(p.SemanticInfoStrong);
            WorkspaceStoryTexture = _textures.Get(p.WorkspaceStory);
            WorkspaceCastTexture = _textures.Get(p.WorkspaceCast);
            WorkspaceSuppliesTexture = _textures.Get(p.WorkspaceSupplies);
            WorkspaceMapTexture = _textures.Get(p.WorkspaceMap);
            WorkspaceTestTexture = _textures.Get(p.WorkspaceTest);
            WorkspacePublishTexture = _textures.Get(p.WorkspacePublish);

            PanelTexture = PageTexture;
            PanelRaisedTexture = CardTexture;
            PanelInsetTexture = InsetTexture;
            AccentActiveTexture = _textures.Get(p.AccentGold);
            AccentHoverTexture = CardHoverTexture;
            DangerTexture = _textures.Get(p.SemanticErrorStrong);
            DisabledTexture = _textures.Get(p.SurfaceDisabled);
            ViewportTexture = _textures.Get(p.SurfaceViewport);
            BevelLightTexture = BorderHighlightTexture;
            BevelDarkTexture = BorderStrongTexture;

            Texture2D page = ProceduralTextureLibrary.MaterialSurface(MaterialSurfaceTier.Page, ScenarioUiAtlasSkin.CornerTextureSize, ScenarioUiAtlasSkin.CornerTextureSize, ScenarioUiAtlasSkin.CornerRadiusPixels, p.SurfacePage);
            Texture2D card = ProceduralTextureLibrary.MaterialSurface(MaterialSurfaceTier.RaisedCard, ScenarioUiAtlasSkin.CornerTextureSize, ScenarioUiAtlasSkin.CornerTextureSize, ScenarioUiAtlasSkin.CornerRadiusPixels, p.SurfaceCard);
            Texture2D cardHover = _textures.GetCornerCut(p.SurfaceCardHover);
            Texture2D cardSelected = _textures.GetCornerCut(p.SurfaceCardSelected);
            Texture2D inset = ProceduralTextureLibrary.MaterialSurface(MaterialSurfaceTier.RecessedInset, ScenarioUiAtlasSkin.CornerTextureSize, ScenarioUiAtlasSkin.CornerTextureSize, ScenarioUiAtlasSkin.CornerRadiusPixels, p.SurfaceInset);
            Texture2D chrome = ProceduralTextureLibrary.MaterialSurface(MaterialSurfaceTier.RaisedCard, ScenarioUiAtlasSkin.CornerTextureSize, ScenarioUiAtlasSkin.CornerTextureSize, ScenarioUiAtlasSkin.CornerRadiusPixels, p.SurfaceChrome);
            Texture2D pressed = _textures.GetCornerCut(p.ControlPressed);
            Texture2D disabled = _textures.GetCornerCut(p.SurfaceDisabled);
            Texture2D gold = _textures.GetCornerCut(p.AccentGold);
            Texture2D warning = _textures.GetCornerCut(p.SemanticWarning);
            Texture2D warningStrong = _textures.GetCornerCut(p.SemanticWarningStrong);
            Texture2D error = _textures.GetCornerCut(p.SemanticError);
            Texture2D errorStrong = _textures.GetCornerCut(p.SemanticErrorStrong);
            Texture2D info = _textures.GetCornerCut(p.SemanticInfo);
            Texture2D infoStrong = _textures.GetCornerCut(p.SemanticInfoStrong);
            Texture2D ready = _textures.GetCornerCut(p.SemanticReady);
            Texture2D neutral = _textures.GetCornerCut(p.SurfaceDisabled);
            Texture2D viewport = _textures.GetCornerCut(p.SurfaceViewport);
            // Contract-compatible aliases: Phase 9 keeps search fill unchanged
            // on focus because the distinct focused surface is a 2px ring.
            Texture2D accentNeutralCorner = inset;
            Texture2D accentActiveCorner = inset;

            int cardPadding = Mathf.RoundToInt(m.CardPadding);
            int insetPadding = Mathf.RoundToInt(m.InsetPadding);
            int padX = Mathf.RoundToInt(m.PaddingSm);
            int padY = Mathf.RoundToInt(m.PaddingXs);
            int chipPad = Mathf.RoundToInt(m.PillPaddingX);

            Page = BuildBox(page, cardPadding);
            Card = BuildBox(card, cardPadding);
            Inset = BuildBox(inset, insetPadding);
            Chrome = BuildBox(chrome, cardPadding);
            Viewport = BuildBox(viewport, insetPadding);

            PanelBase = Page;
            PanelRaised = Card;
            PanelInset = Inset;
            Header = BuildBox(chrome, padX);
            Footer = BuildBox(chrome, padX);
            Status = BuildBox(chrome, padX);
            Section = Card;
            Menu = BuildBox(chrome, insetPadding);
            Field = BuildField(inset, p.TextPrimary, m, padX, padY);
            SearchField = BuildField(accentNeutralCorner, accentActiveCorner, palette.TextOnLight, m, padX, padY);
            Divider = BuildBox(BorderSubtleTexture, 0);

            ChipNeutral = BuildPill(neutral, p.TextPrimary, m, chipPad);
            ChipInformational = BuildPill(info, p.TextPrimary, m, chipPad);
            ChipReady = BuildPill(ready, p.TextPrimary, m, chipPad);
            ChipWarning = BuildPill(warning, p.TextPrimary, m, chipPad);
            ChipError = BuildPill(error, p.TextPrimary, m, chipPad);
            Pill = ChipNeutral;
            PillEmphasized = ChipInformational;
            PillDanger = ChipError;
            PillSuccess = ChipReady;
            PillWarning = ChipWarning;

            BrandTitleText = BuildText(m.FontSizeBrand, FontStyle.Bold, p.AccentGold);
            TitleText = BuildText(m.FontSizeTitle, FontStyle.Bold, p.TextInverse);
            HeaderTitleText = BuildText(m.FontSizeTitle, FontStyle.Bold, p.TextInverse);
            HeaderSubtitleText = BuildText(m.FontSizeSubtitle, FontStyle.Normal, p.TextInverseMuted);
            SubtitleText = BuildText(m.FontSizeSubtitle, FontStyle.Normal, p.TextInverseMuted);
            SectionTitleText = BuildText(m.FontSizeSection, FontStyle.Bold, p.TextInverse);
            BodyText = BuildText(m.FontSizeBody, FontStyle.Normal, p.TextInverse);
            MutedText = BuildText(m.FontSizeMuted, FontStyle.Normal, p.TextInverseMuted);
            PaperTitleText = BuildText(m.FontSizeSection, FontStyle.Bold, p.TextPrimary);
            PaperBodyText = BuildText(m.FontSizeBody, FontStyle.Normal, p.TextPrimary);
            PaperMutedText = BuildText(m.FontSizeMuted, FontStyle.Normal, p.TextSecondary);
            EmptyStateText = BuildText(m.FontSizeMuted, FontStyle.Normal, p.TextSecondary);
            EmptyStateText.alignment = TextAnchor.MiddleCenter;
            PillText = BuildText(m.FontSizePill, FontStyle.Bold, p.TextPrimary);
            PillText.alignment = TextAnchor.MiddleCenter;
            BreadcrumbLinkText = BuildText(m.FontSizeBody, FontStyle.Normal, p.TextInverseMuted);
            BreadcrumbCurrentText = BuildText(m.FontSizeBody, FontStyle.Bold, p.AccentGold);

            Button = BuildButton(chrome, gold, pressed, p.TextInverse, p.TextInverse, p.TextInverse, m, padX, padY);
            ButtonEmphasized = BuildButton(gold, warning, pressed, p.TextInverse, p.TextPrimary, p.TextInverse, m, padX, padY);
            ButtonActive = ButtonEmphasized;
            ButtonDanger = BuildButton(error, errorStrong, errorStrong, p.TextPrimary, p.TextInverse, p.TextInverse, m, padX, padY);
            ButtonDisabled = BuildButton(disabled, disabled, disabled, p.TextDisabled, p.TextDisabled, p.TextDisabled, m, padX, padY);
            Tab = BuildButton(chrome, pressed, pressed, p.TextInverse, p.TextInverse, p.TextInverse, m, padX, padY);
            TabActive = BuildButton(gold, pressed, pressed, p.TextInverse, p.TextInverse, p.TextInverse, m, padX, padY);
            TabDisabled = ButtonDisabled;

            NavigatorRow = BuildButton(card, cardHover, cardSelected, p.TextPrimary, p.TextPrimary, p.TextPrimary, m, padX, padY);
            NavigatorRowHover = BuildButton(cardHover, cardHover, cardSelected, p.TextPrimary, p.TextPrimary, p.TextPrimary, m, padX, padY);
            NavigatorRowSelected = BuildButton(cardSelected, cardHover, cardSelected, p.TextPrimary, p.TextPrimary, p.TextPrimary, m, padX, padY);
            NavigatorRowWarning = BuildButton(warning, warningStrong, warning, p.TextPrimary, p.TextPrimary, p.TextPrimary, m, padX, padY);
            NavigatorRow.fixedHeight = 0f;
            NavigatorRowHover.fixedHeight = 0f;
            NavigatorRowSelected.fixedHeight = 0f;
            NavigatorRowWarning.fixedHeight = 0f;
            CompactChoice = BuildButton(card, cardHover, cardSelected, p.TextPrimary, p.TextPrimary, p.TextPrimary, m, padX, padY);
            CompactChoiceSelected = BuildButton(info, infoStrong, info, p.TextPrimary, p.TextPrimary, p.TextPrimary, m, padX, padY);
        }

        private static GUIStyle BuildBox(Texture2D background, int padding)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = background;
            style.normal.textColor = Color.white;
            style.border = BuildCornerBorder();
            style.padding = new RectOffset(padding, padding, padding, padding);
            style.margin = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static GUIStyle BuildField(Texture2D background, Color textColor, IScenarioUiMetrics metrics, int padX, int padY)
        {
            return BuildField(background, background, textColor, metrics, padX, padY);
        }

        private static GUIStyle BuildField(Texture2D background, Texture2D focusedBackground, Color textColor, IScenarioUiMetrics metrics, int padX, int padY)
        {
            GUIStyle style = new GUIStyle(GUI.skin.textField);
            style.font = ResolveRuntimeFont();
            style.normal.background = background;
            style.hover.background = background;
            style.focused.background = focusedBackground;
            style.active.background = focusedBackground;
            ApplyTextColor(style, textColor, textColor, textColor);
            style.alignment = TextAnchor.MiddleLeft;
            style.fontSize = metrics.FontSizeBody;
            style.fixedHeight = metrics.ButtonHeight;
            style.border = BuildCornerBorder();
            style.padding = new RectOffset(padX, padX, padY, padY);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.clipping = TextClipping.Clip;
            return style;
        }

        private static GUIStyle BuildText(int size, FontStyle fontStyle, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.font = ResolveRuntimeFont();
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.wordWrap = true;
            style.clipping = TextClipping.Overflow;
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static GUIStyle BuildButton(Texture2D normal, Texture2D hover, Texture2D active, Color normalText, Color hoverText, Color activeText, IScenarioUiMetrics metrics, int padX, int padY)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.font = ResolveRuntimeFont();
            style.normal.background = normal;
            style.hover.background = hover;
            style.focused.background = normal;
            style.active.background = active;
            style.onNormal.background = active;
            style.onHover.background = hover;
            style.onFocused.background = active;
            style.onActive.background = active;
            ApplyTextColor(style, normalText, hoverText, activeText);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = metrics.FontSizeBody;
            style.fixedHeight = metrics.ButtonHeight;
            style.border = BuildCornerBorder();
            style.padding = new RectOffset(padX, padX, padY, padY);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            return style;
        }

        private static GUIStyle BuildPill(Texture2D background, Color textColor, IScenarioUiMetrics metrics, int padX)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.font = ResolveRuntimeFont();
            style.normal.background = background;
            style.normal.textColor = textColor;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = metrics.FontSizePill;
            style.fontStyle = FontStyle.Bold;
            style.fixedHeight = metrics.PillHeight;
            style.border = BuildCornerBorder();
            style.padding = new RectOffset(padX, padX, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static void ApplyBackground(GUIStyle style, Texture2D background)
        {
            style.normal.background = background;
            style.hover.background = background;
            style.focused.background = background;
            style.active.background = background;
        }

        private static void ApplyTextColor(GUIStyle style, Color normal, Color hover, Color active)
        {
            style.normal.textColor = normal;
            style.hover.textColor = hover;
            style.focused.textColor = normal;
            style.active.textColor = active;
            style.onNormal.textColor = active;
            style.onHover.textColor = hover;
            style.onFocused.textColor = active;
            style.onActive.textColor = active;
        }

        private static RectOffset BuildCornerBorder()
        {
            int radius = ScenarioUiAtlasSkin.CornerRadiusPixels;
            return new RectOffset(radius, radius, radius, radius);
        }

        private static Font ResolveRuntimeFont()
        {
            UIFontCache.RefreshIfMissing();
            return UIFontCache.GetFonts().TTF;
        }
    }

    internal static class ScenarioUiMeasuredLabel
    {
        public static float Width(string label, GUIStyle style, float extraPadding)
        {
            if (style == null)
                style = GUI.skin.label;
            Vector2 size = style.CalcSize(new GUIContent(label ?? string.Empty));
            return size.x + Math.Max(0f, extraPadding) + ResolveHorizontalPadding(style);
        }

        public static bool PreserveLabelWithOverflowTooltip(string label, float maxWidth, GUIStyle style, out string fitted, out string tooltip)
        {
            string safeLabel = label ?? string.Empty;
            fitted = safeLabel;
            bool overflows = style != null && style.CalcSize(new GUIContent(safeLabel)).x > maxWidth;
            tooltip = overflows ? safeLabel : string.Empty;
            return overflows;
        }

        private static float ResolveHorizontalPadding(GUIStyle style)
        {
            return style != null && style.padding != null ? style.padding.left + style.padding.right : 0f;
        }
    }
}
