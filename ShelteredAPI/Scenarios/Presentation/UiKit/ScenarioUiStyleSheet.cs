using UnityEngine;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
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
        public GUIStyle Divider { get; private set; }

        // Pill / badge surfaces
        public GUIStyle Pill { get; private set; }
        public GUIStyle PillEmphasized { get; private set; }
        public GUIStyle PillDanger { get; private set; }

        // Text styles
        public GUIStyle BrandTitleText { get; private set; }
        public GUIStyle TitleText { get; private set; }
        public GUIStyle SubtitleText { get; private set; }
        public GUIStyle SectionTitleText { get; private set; }
        public GUIStyle BodyText { get; private set; }
        public GUIStyle MutedText { get; private set; }
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

            int padXs = Mathf.RoundToInt(metrics.PaddingXs);
            int padSm = Mathf.RoundToInt(metrics.PaddingSm);
            int padMd = Mathf.RoundToInt(metrics.PaddingMd);
            int pillPadX = Mathf.RoundToInt(metrics.PillPaddingX);

            PanelBase    = BuildBox(PanelTexture, PanelBasePadding);
            PanelRaised  = BuildBox(PanelRaisedTexture, PanelBasePadding);
            PanelInset   = BuildBox(PanelInsetTexture, padMd);
            Header       = BuildBox(PanelRaisedTexture, padSm);
            Footer       = BuildBox(PanelRaisedTexture, padSm);
            Status       = BuildBox(PanelTexture, padSm);
            Section      = BuildBox(PanelRaisedTexture, padMd);
            Menu         = BuildBox(PanelRaisedTexture, padMd);
            Card         = BuildBox(PanelRaisedTexture, CardSurfacePadding);
            Field        = BuildField(PanelTexture, palette.TextBody, metrics, padXs);
            Divider      = BuildBox(BorderSubtleTexture, 0);

            Pill           = BuildPill(_textures.Get(palette.AccentMuted), palette.TextOnAccent, metrics, pillPadX);
            PillEmphasized = BuildPill(AccentActiveTexture, palette.TextOnAccent, metrics, pillPadX);
            PillDanger     = BuildPill(DangerTexture, palette.TextOnAccent, metrics, pillPadX);

            BrandTitleText   = BuildText(metrics.FontSizeBrand,    FontStyle.Bold,   palette.TextTitle);
            TitleText        = BuildText(metrics.FontSizeTitle,    FontStyle.Bold,   palette.TextSubtitle);
            SubtitleText     = BuildText(metrics.FontSizeSubtitle, FontStyle.Normal, palette.TextMuted);
            SectionTitleText = BuildText(metrics.FontSizeSection,  FontStyle.Bold,   palette.TextTitle);
            BodyText         = BuildText(metrics.FontSizeBody,     FontStyle.Normal, palette.TextBody);
            MutedText        = BuildText(metrics.FontSizeMuted,    FontStyle.Normal, palette.TextMuted);
            EmptyStateText   = BuildText(metrics.FontSizeMuted,    FontStyle.Normal, palette.TextMuted);
            EmptyStateText.alignment = TextAnchor.MiddleCenter;
            PillText         = BuildText(metrics.FontSizePill,     FontStyle.Bold,   palette.TextOnAccent);
            PillText.alignment = TextAnchor.MiddleCenter;

            // Buttons sit on raised surfaces; tabs sit directly on the base
            // panel so the active tab looks "settled into" the surrounding
            // chrome rather than floating above it.
            Button         = BuildButton(PanelRaisedTexture, AccentHoverTexture, PanelInsetTexture, palette.TextBody, metrics, padSm, padXs);
            ButtonActive   = BuildButton(AccentActiveTexture, AccentHoverTexture, BorderStrongTexture, palette.TextOnAccent, metrics, padSm, padXs);
            ButtonDanger   = BuildButton(DangerTexture,       AccentHoverTexture, BorderStrongTexture, palette.TextOnAccent, metrics, padSm, padXs);
            ButtonDisabled = BuildButton(DisabledTexture,     DisabledTexture,    DisabledTexture,     palette.TextDisabled, metrics, padSm, padXs);
            Tab            = BuildButton(PanelTexture,        AccentHoverTexture, BorderStrongTexture, palette.TextBody, metrics, padSm, padXs);
            TabActive      = BuildButton(AccentActiveTexture, AccentHoverTexture, BorderStrongTexture, palette.TextOnAccent, metrics, padSm, padXs);
            TabDisabled    = BuildButton(DisabledTexture,     DisabledTexture,    DisabledTexture,     palette.TextDisabled, metrics, padSm, padXs);
        }

        private static GUIStyle BuildBox(Texture2D background, int padding)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = background;
            style.border = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(padding, padding, padding, padding);
            style.margin = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static GUIStyle BuildField(Texture2D background, Color textColor, IScenarioUiMetrics metrics, int padding)
        {
            GUIStyle style = BuildBox(background, padding);
            style.normal.textColor = textColor;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = metrics.FontSizeMuted;
            return style;
        }

        private static GUIStyle BuildText(int size, FontStyle fontStyle, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.normal.textColor = color;
            style.wordWrap = true;
            return style;
        }

        private static GUIStyle BuildButton(Texture2D background, Texture2D hover, Texture2D active, Color textColor, IScenarioUiMetrics metrics, int padX, int padY)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
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
            style.border = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(padX, padX, padY, padY);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            return style;
        }

        private static GUIStyle BuildPill(Texture2D background, Color textColor, IScenarioUiMetrics metrics, int padX)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = background;
            style.normal.textColor = textColor;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = metrics.FontSizePill;
            style.fontStyle = FontStyle.Bold;
            style.border = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(padX, padX, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            return style;
        }
    }
}
