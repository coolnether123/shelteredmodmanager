using System;
using UnityEngine;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.UI.Compatibility;
namespace ShelteredAPI.Scenarios.Presentation.UiKit{
    /// <summary>
    /// Cached <see cref="GUIStyle"/>s for the scenario authoring UiKit. Built
    /// once from a <see cref="ScenarioUiTheme"/> and a shared
    /// <see cref="ScenarioUiTextureCache"/>. A new style sheet is constructed
    /// when the theme changes (e.g. opacity slider moves), so callers should
    /// hold an instance and replace it via <see cref="ScenarioUiKit"/>.
    ///
    /// Style roles are deliberately named for the renderer surfaces they map
    /// onto: <see cref="PanelBase"/>/<see cref="Header"/>/<see cref="Footer"/>
    /// for chrome, <see cref="Section"/>/<see cref="Card"/>/<see cref="Status"/>
    /// for content surfaces, <see cref="Tab"/>/<see cref="TabActive"/> and
    /// <see cref="Button"/>/<see cref="ButtonActive"/> for interactive elements.
    /// </summary>
    internal sealed class ScenarioUiStyleSheet
    {
        // Specific surface paddings sit between the generic spacing tokens
        // (Xs=4, Sm=6, Md=8, Lg=12) so they live as constants here rather
        // than bloating the metrics interface.
        private const int PanelBasePadding = 10;
        private const int CardSurfacePadding = 10;

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

        // Surface styles (boxes)
        public GUIStyle PanelBase { get; private set; }
        public GUIStyle PanelRaised { get; private set; }
        public GUIStyle PanelInset { get; private set; }
        public GUIStyle Header { get; private set; }
        public GUIStyle Footer { get; private set; }
        public GUIStyle Status { get; private set; }
        public GUIStyle Section { get; private set; }
        public GUIStyle Menu { get; private set; }
        public GUIStyle Card { get; private set; }
        public GUIStyle Field { get; private set; }
        public GUIStyle SearchField { get; private set; }
        public GUIStyle Divider { get; private set; }

        // Pill / badge surfaces
        public GUIStyle Pill { get; private set; }
        public GUIStyle PillEmphasized { get; private set; }
        public GUIStyle PillDanger { get; private set; }
        public GUIStyle PillSuccess { get; private set; }
        public GUIStyle PillWarning { get; private set; }

        // Text styles
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

        // Interactive styles
        public GUIStyle Button { get; private set; }
        public GUIStyle ButtonActive { get; private set; }
        public GUIStyle ButtonDanger { get; private set; }
        public GUIStyle ButtonDisabled { get; private set; }
        public GUIStyle Tab { get; private set; }
        public GUIStyle TabActive { get; private set; }
        public GUIStyle TabDisabled { get; private set; }

        // Direct texture handles for renderers that paint backgrounds via
        // GUI.DrawTexture without going through a GUIStyle.
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

        // Parchment bevel: a warm highlight for the top/left edges and a soft
        // shadow for the bottom/right edges, giving surfaces a lit, raised feel.
        public Texture2D BevelLightTexture { get; private set; }
        public Texture2D BevelDarkTexture { get; private set; }

        private void Rebuild()
        {
            IScenarioUiPalette palette = _theme.Palette;
            IScenarioUiMetrics metrics = _theme.Metrics;

            // Texture cache: panel base uses the user's opacity verbatim;
            // raised and active surfaces nudge alpha up so layering reads
            // even when opacity is dialled down.
            PanelTexture        = _textures.Get(_theme.WithPanelOpacity(palette.PanelBase));
            PanelRaisedTexture  = _textures.Get(_theme.WithRaisedOpacity(palette.PanelRaised));
            PanelInsetTexture   = _textures.Get(_theme.WithPanelOpacity(palette.PanelInset));
            BorderStrongTexture = _textures.Get(palette.BorderStrong);
            BorderSubtleTexture = _textures.Get(palette.BorderSubtle);
            AccentActiveTexture = _textures.Get(_theme.WithActiveOpacity(palette.AccentActive));
            AccentHoverTexture  = _textures.Get(_theme.WithRaisedOpacity(palette.AccentHover));
            DangerTexture       = _textures.Get(palette.AccentDanger);
            DisabledTexture     = _textures.Get(_theme.WithPanelOpacity(palette.DisabledSurface));
            ViewportTexture     = _textures.Get(palette.Viewport);
            BevelLightTexture   = _textures.Get(new Color(0.98f, 0.93f, 0.80f, 0.30f));
            BevelDarkTexture    = _textures.Get(new Color(0.10f, 0.06f, 0.03f, 0.38f));

            Texture2D panelCorner       = _textures.GetCornerCut(_theme.WithPanelOpacity(palette.PanelBase));
            Texture2D panelRaisedCorner = _textures.GetCornerCut(_theme.WithRaisedOpacity(palette.PanelRaised));
            Texture2D panelInsetCorner  = _textures.GetCornerCut(_theme.WithPanelOpacity(palette.PanelInset));
            Texture2D accentActiveCorner = _textures.GetCornerCut(_theme.WithActiveOpacity(palette.AccentActive));
            Texture2D accentHoverCorner = _textures.GetCornerCut(_theme.WithRaisedOpacity(palette.AccentHover));
            Texture2D dangerCorner       = _textures.GetCornerCut(palette.AccentDanger);
            Texture2D disabledCorner     = _textures.GetCornerCut(_theme.WithPanelOpacity(palette.DisabledSurface));
            Texture2D accentSuccessCorner = _textures.GetCornerCut(palette.AccentSuccess);
            Texture2D accentWarningCorner = _textures.GetCornerCut(palette.AccentWarning);
            Texture2D accentNeutralCorner = _textures.GetCornerCut(palette.AccentNeutral);
            Texture2D borderStrongCorner = _textures.GetCornerCut(palette.BorderStrong);

            int padXs = Mathf.RoundToInt(metrics.PaddingXs);
            int padSm = Mathf.RoundToInt(metrics.PaddingSm);
            int padMd = Mathf.RoundToInt(metrics.PaddingMd);
            int pillPadX = Mathf.RoundToInt(metrics.PillPaddingX);

            PanelBase    = BuildBox(panelCorner, PanelBasePadding);
            PanelRaised  = BuildBox(panelRaisedCorner, PanelBasePadding);
            PanelInset   = BuildBox(panelInsetCorner, padMd);
            Header       = BuildBox(panelRaisedCorner, padSm);
            Footer       = BuildBox(panelRaisedCorner, padSm);
            Status       = BuildBox(panelCorner, padSm);
            Section      = BuildBox(panelRaisedCorner, padMd);
            Menu         = BuildBox(panelRaisedCorner, padMd);
            Card         = BuildBox(accentNeutralCorner, CardSurfacePadding);
            Field        = BuildField(panelInsetCorner, accentHoverCorner, palette.TextBody, metrics, padSm, padXs);
            SearchField  = BuildField(accentNeutralCorner, accentActiveCorner, palette.TextOnLight, metrics, padMd, padXs);
            Divider      = BuildBox(BorderSubtleTexture, 0);

            Pill           = BuildPill(accentNeutralCorner, palette.TextOnLight, metrics, pillPadX);
            PillEmphasized = BuildPill(accentActiveCorner, palette.TextOnAccent, metrics, pillPadX);
            PillDanger     = BuildPill(dangerCorner, palette.TextOnAccent, metrics, pillPadX);
            PillSuccess    = BuildPill(accentSuccessCorner, palette.TextOnAccent, metrics, pillPadX);
            PillWarning    = BuildPill(accentWarningCorner, palette.TextOnLight, metrics, pillPadX);

            BrandTitleText   = BuildText(metrics.FontSizeBrand,    FontStyle.Bold,   palette.TextTitle);
            TitleText        = BuildText(metrics.FontSizeTitle,    FontStyle.Bold,   palette.TextSubtitle);
            HeaderTitleText  = BuildText(metrics.FontSizeTitle,    FontStyle.Bold,   new Color(0.13f, 0.08f, 0.04f, 1f));
            HeaderSubtitleText = BuildText(metrics.FontSizeSubtitle, FontStyle.Normal, new Color(0.22f, 0.15f, 0.08f, 1f));
            SubtitleText     = BuildText(metrics.FontSizeSubtitle, FontStyle.Normal, palette.TextMuted);
            SectionTitleText = BuildText(metrics.FontSizeSection,  FontStyle.Bold,   palette.TextTitle);
            BodyText         = BuildText(metrics.FontSizeBody,     FontStyle.Normal, palette.TextBody);
            MutedText        = BuildText(metrics.FontSizeMuted,    FontStyle.Normal, palette.TextMuted);
            PaperTitleText   = BuildText(metrics.FontSizeSection,  FontStyle.Bold,   palette.TextOnLight);
            PaperBodyText    = BuildText(metrics.FontSizeBody,     FontStyle.Normal, palette.TextOnLight);
            PaperMutedText   = BuildText(metrics.FontSizeMuted,    FontStyle.Normal, new Color(0.34f, 0.27f, 0.18f, 1f));
            EmptyStateText   = BuildText(metrics.FontSizeMuted,    FontStyle.Normal, palette.TextMuted);
            EmptyStateText.alignment = TextAnchor.MiddleCenter;
            PillText         = BuildText(metrics.FontSizePill,     FontStyle.Bold,   palette.TextOnAccent);
            PillText.alignment = TextAnchor.MiddleCenter;

            // Buttons sit on raised surfaces; tabs sit directly on the base
            // panel so the active tab looks "settled into" the surrounding
            // chrome rather than floating above it.
            Button         = BuildButton(panelRaisedCorner, accentHoverCorner, panelInsetCorner, palette.TextBody, metrics, padSm, padXs);
            ButtonActive   = BuildButton(accentActiveCorner, accentHoverCorner, borderStrongCorner, palette.TextOnAccent, metrics, padSm, padXs);
            ButtonDanger   = BuildButton(dangerCorner,       accentHoverCorner, borderStrongCorner, palette.TextOnAccent, metrics, padSm, padXs);
            ButtonDisabled = BuildButton(disabledCorner,     disabledCorner,    disabledCorner,     palette.TextDisabled, metrics, padSm, padXs);
            Tab            = BuildButton(panelCorner,        accentHoverCorner, borderStrongCorner, palette.TextBody, metrics, padSm, padXs);
            TabActive      = BuildButton(panelInsetCorner,   accentHoverCorner, borderStrongCorner, palette.TextTitle, metrics, padSm, padXs);
            TabDisabled    = BuildButton(disabledCorner,     disabledCorner,    disabledCorner,     palette.TextDisabled, metrics, padSm, padXs);
        }

        private static GUIStyle BuildBox(Texture2D background, int padding)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = background;
            style.border = BuildCornerBorder();
            style.padding = new RectOffset(padding, padding, padding, padding);
            style.margin = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static GUIStyle BuildField(Texture2D background, Texture2D focusedBackground, Color textColor, IScenarioUiMetrics metrics, int padX, int padY)
        {
            GUIStyle style = new GUIStyle(GUI.skin.textField);
            style.font = ResolveRuntimeFont();
            style.normal.background = background;
            style.hover.background = focusedBackground;
            style.focused.background = focusedBackground;
            style.active.background = focusedBackground;
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.focused.textColor = textColor;
            style.active.textColor = textColor;
            style.alignment = TextAnchor.MiddleLeft;
            style.fontSize = metrics.FontSizeBody;
            style.border = BuildCornerBorder();
            style.padding = new RectOffset(padX, padX, padY + 1, padY + 2);
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
            style.wordWrap = true;
            style.clipping = TextClipping.Overflow;
            style.padding = new RectOffset(0, 0, 1, 3);
            return style;
        }

        private static GUIStyle BuildButton(Texture2D background, Texture2D hover, Texture2D active, Color textColor, IScenarioUiMetrics metrics, int padX, int padY)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.font = ResolveRuntimeFont();
            style.normal.background = background;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = hover;
            style.onNormal.background = active;
            style.onHover.background = hover;
            style.onActive.background = active;
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = metrics.FontSizeBody;
            style.border = BuildCornerBorder();
            style.padding = new RectOffset(padX, padX, padY + 1, padY + 3);
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
            style.border = BuildCornerBorder();
            style.padding = new RectOffset(padX, padX, 1, 2);
            style.margin = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static RectOffset BuildCornerBorder()
        {
            int radius = ScenarioUiAtlasSkin.CornerRadiusPixels;
            return new RectOffset(radius, radius, radius, radius);
        }

        private static Font ResolveRuntimeFont()
        {
            UIFontCache.RefreshIfMissing();
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();
            return fonts.TTF;
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
