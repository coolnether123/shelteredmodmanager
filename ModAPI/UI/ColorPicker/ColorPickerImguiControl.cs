using System;
using ModAPI.Core;
using UnityEngine;
using ModAPI.UI;

namespace ModAPI.UI.ColorPicker
{
    /// <summary>
    /// Optional Unity IMGUI style hooks for <see cref="ColorPickerImguiControl"/>.
    /// Callers can supply game-native GUIStyle instances without creating a game-specific dependency in ModAPI.
    /// </summary>
    public sealed class ColorPickerImguiStyle
    {
        public GUIStyle Label;
        public GUIStyle Field;
        public GUIStyle Button;
        public GUIStyle SmallButton;
        public GUIStyle SwatchLabel;
        public Color InvalidFieldTint = new Color(1f, 0.45f, 0.45f, 1f);
        public Color HandleColor = Color.white;
        public Color HandleBorderColor = Color.black;
    }

    /// <summary>
    /// Unity 5.3 IMGUI renderer for ModAPI color picker sessions.
    /// Draw this inside a top-level popup/window owned by the caller; it blocks same-frame click fallthrough through <see cref="UIFlowGuard"/>.
    /// </summary>
    public sealed class ColorPickerImguiControl : IDisposable
    {
        private readonly ColorPickerSession _session;
        private Texture2D _svTexture;
        private Texture2D _hueTexture;
        private Texture2D _alphaTexture;
        private Texture2D _checkerTexture;
        private float _cachedHue = -1f;
        private float _cachedSvAlpha = -1f;
        private float _cachedSaturation = -1f;
        private int _cachedSvTextureSize = -1;
        private int _cachedHueTextureWidth = -1;
        private int _cachedHueTextureHeight = -1;
        private int _cachedAlphaTextureWidth = -1;
        private int _cachedAlphaTextureHeight = -1;
        private ModColor _cachedAlphaColor;
        private string _focusedTextFieldId;

        private const float FixedGap = 8f;
        private const float HueBarHeight = 18f;
        private const float AlphaBarHeight = 18f;
        private const float MinimumSaturationValueSize = 120f;
        private const float MinimumDrawableSaturationValueSize = 40f;
        private const float MinimumContentWidth = 496f;
        private const float FieldLabelWidth = 36f;
        private const float FieldGap = 4f;
        private const float UnitFieldWidth = 42f;
        private const float HexFieldWidth = 116f;
        private const float SwatchLabelWidth = 54f;

        private static readonly ModColor[] BasicSwatches = new[]
        {
            new ModColor(0f, 0f, 0f, 1f),
            new ModColor(1f, 1f, 1f, 1f),
            new ModColor(0.5f, 0.5f, 0.5f, 1f),
            new ModColor(1f, 0f, 0f, 1f),
            new ModColor(0f, 1f, 0f, 1f),
            new ModColor(0f, 0f, 1f, 1f),
            new ModColor(1f, 1f, 0f, 1f),
            new ModColor(0f, 1f, 1f, 1f),
            new ModColor(1f, 0f, 1f, 1f)
        };

        public ColorPickerImguiControl(ColorPickerSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            _session = session;
            Style = new ColorPickerImguiStyle();
            PickerSize = 220f;
            SliderWidth = 16f;
            Margin = 8f;
            FieldHeight = 26f;
            ButtonHeight = 28f;
            PreviewSize = 42f;
            SwatchSize = 18f;
        }

        public ColorPickerImguiStyle Style { get; private set; }
        public float PickerSize { get; set; }
        public float SliderWidth { get; set; }
        public float Margin { get; set; }
        public float FieldHeight { get; set; }
        public float ButtonHeight { get; set; }
        public float PreviewSize { get; set; }
        public float SwatchSize { get; set; }
        public bool ConsumedInputThisFrame { get; private set; }

        public Vector2 MinimumSize
        {
            get { return new Vector2(MinimumContentWidth + (Margin * 2f), CalculateRequiredHeight(MinimumSaturationValueSize)); }
        }

        public Vector2 PreferredSize
        {
            get
            {
                return new Vector2(MinimumSize.x, CalculateRequiredHeight(Math.Max(MinimumSaturationValueSize, PickerSize)));
            }
        }

        public void Draw(Rect rect)
        {
            ConsumedInputThisFrame = false;
            Event evt = Event.current;
            if (evt != null && rect.Contains(evt.mousePosition))
            {
                ConsumedInputThisFrame = true;
                UIFlowGuard.BlockSlotClicksForFrames(1);
            }

            string focused = GUI.GetNameOfFocusedControl();
            _session.SyncTextFields(IsColorPickerTextField(focused) ? focused : null, false);

            ColorPickerLayout layout = BuildLayout(rect);

            EnsureTextures(layout);
            DrawPickerArea(layout.SaturationValueRect, layout.HueRect, layout.AlphaRect);
            DrawPreviewRow(layout.PreviewRect);
            DrawNumericFieldGrid(layout);
            DrawSwatches(layout.BasicsSwatchRect, layout.PinnedSwatchRect, layout.RecentSwatchRect);
            DrawButtons(layout.ButtonRect);
#if DEBUG
            GuardNoLayoutIntersections(layout);
#endif
            HandleKeyboard();
            CommitTextFieldFocusChange(GUI.GetNameOfFocusedControl());

            if (evt != null && ConsumedInputThisFrame && ShouldUseEvent(evt))
                evt.Use();
        }

        public void Dispose()
        {
            DestroyTexture(ref _svTexture);
            DestroyTexture(ref _hueTexture);
            DestroyTexture(ref _alphaTexture);
            DestroyTexture(ref _checkerTexture);
        }

        private void DrawPickerArea(Rect pickerRect, Rect hueRect, Rect alphaRect)
        {
            GUI.DrawTexture(pickerRect, CheckerTexture);
            GUI.DrawTexture(pickerRect, _svTexture);
            GUI.DrawTexture(hueRect, _hueTexture);
            GUI.DrawTexture(alphaRect, CheckerTexture);
            GUI.DrawTexture(alphaRect, _alphaTexture);

            DrawHandle(new Rect(
                pickerRect.x + (_session.Hsv.S * pickerRect.width) - 4f,
                pickerRect.y + ((1f - _session.Hsv.V) * pickerRect.height) - 4f,
                8f,
                8f));
            DrawHandle(new Rect(hueRect.x + (_session.Hsv.H * hueRect.width) - 3f, hueRect.y - 2f, 6f, hueRect.height + 4f));
            DrawHandle(new Rect(alphaRect.x + (_session.CurrentColor.A * alphaRect.width) - 3f, alphaRect.y - 2f, 6f, alphaRect.height + 4f));

            HandlePickerDrag(pickerRect, hueRect, alphaRect);
        }

        private void DrawPreviewRow(Rect rect)
        {
            Rect newPreview = new Rect(rect.x, rect.y, PreviewSize, PreviewSize);
            Rect oldPreview = new Rect(newPreview.xMax + FixedGap, rect.y, PreviewSize, PreviewSize);
            DrawColorPreview(newPreview, _session.CurrentColor);
            DrawColorPreview(oldPreview, _session.OldColor);
            if (GUI.Button(oldPreview, GUIContent.none, GUIStyle.none))
                _session.RestoreOldColor();
            GUI.Label(new Rect(oldPreview.xMax + FixedGap, rect.y + 1f, Math.Max(1f, rect.xMax - oldPreview.xMax - FixedGap), 18f), "New / Old", LabelStyle);
        }

        private void DrawNumericFieldGrid(ColorPickerLayout layout)
        {
            DrawHexRow(layout.HexFieldRect);
            DrawFieldRow(layout.RgbFieldRect, "RGB", _session.RedField, _session.GreenField, _session.BlueField, _session.AlphaRgbField);
            DrawFieldRow(layout.HsvFieldRect, "HSV", _session.HueField, _session.SaturationField, _session.ValueField, _session.AlphaHsvField);
            CommitTextFieldFocusChange(GUI.GetNameOfFocusedControl());
        }

        private void DrawButtons(Rect rect)
        {
            float buttonWidth = Math.Max(1f, (rect.width - (FixedGap * 2f)) / 3f);
            Rect applyRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            Rect okRect = new Rect(applyRect.xMax + FixedGap, rect.y, buttonWidth, rect.height);
            Rect cancelRect = new Rect(okRect.xMax + FixedGap, rect.y, Math.Max(1f, rect.xMax - okRect.xMax - FixedGap), rect.height);

            if (GUI.Button(applyRect, "Apply", ButtonStyle))
                _session.Apply();
            if (GUI.Button(okRect, "OK", ButtonStyle))
                _session.Ok();
            if (GUI.Button(cancelRect, "Cancel", ButtonStyle))
                _session.Cancel();
        }

        private void DrawSwatches(Rect basicsRect, Rect pinnedRect, Rect recentRect)
        {
            DrawSwatchRow(basicsRect, "Basics", BasicSwatches, true);
            DrawSwatchRow(pinnedRect, "Pinned", _session.Palette.PinnedColors, true);
            DrawSwatchRow(recentRect, "Recent", _session.Palette.RecentColors, true);
        }

        private void DrawSwatchRow(Rect rect, string label, ModColor[] colors, bool allowPinToggle)
        {
            GUI.Label(new Rect(rect.x, rect.y + 1f, SwatchLabelWidth, rect.height), label, LabelStyle);
            Rect bounds = new Rect(rect.x + SwatchLabelWidth, rect.y, Math.Max(0f, rect.width - SwatchLabelWidth), rect.height);
            float x = bounds.x;
            float y = bounds.y;

            for (int i = 0; colors != null && i < colors.Length; i++)
                DrawSwatch(ref x, ref y, bounds, colors[i], _session.Palette.IsPinned(colors[i]), allowPinToggle);
        }

        private void DrawSwatch(ref float x, ref float y, Rect bounds, ModColor color, bool pinned, bool allowPinToggle)
        {
            if (y + SwatchSize > bounds.yMax || x + SwatchSize > bounds.xMax)
                return;

            Rect swatch = new Rect(x, y, SwatchSize, SwatchSize);
            DrawColorPreview(swatch, color);
            if (pinned)
                GUI.Label(swatch, "*", Style.SwatchLabel ?? GUI.skin.label);

            Event evt = Event.current;
            if (evt != null && swatch.Contains(evt.mousePosition) && evt.type == EventType.MouseDown)
            {
                if (evt.button == 1 && allowPinToggle)
                    _session.TogglePinned(color);
                else
                    _session.SelectSwatch(color);
                evt.Use();
            }

            x += SwatchSize + 3f;
        }

        private void DrawFieldRow(Rect rect, string label, ColorPickerTextField a, ColorPickerTextField b, ColorPickerTextField c, ColorPickerTextField d)
        {
            GUI.Label(new Rect(rect.x, rect.y + 3f, FieldLabelWidth, rect.height - 3f), label, LabelStyle);

            float availableWidth = Math.Max(4f, rect.width - FieldLabelWidth - (FieldGap * 3f));
            float fieldWidth = Math.Min(UnitFieldWidth, availableWidth / 4f);
            float x = rect.x + FieldLabelWidth;
            DrawTextField(new Rect(x, rect.y, fieldWidth, rect.height), a);
            x += fieldWidth + FieldGap;
            DrawTextField(new Rect(x, rect.y, fieldWidth, rect.height), b);
            x += fieldWidth + FieldGap;
            DrawTextField(new Rect(x, rect.y, fieldWidth, rect.height), c);
            x += fieldWidth + FieldGap;
            DrawTextField(new Rect(x, rect.y, fieldWidth, rect.height), d);
        }

        private void DrawHexRow(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y + 3f, FieldLabelWidth, rect.height - 3f), "HEX", LabelStyle);
            float width = Math.Min(HexFieldWidth, Math.Max(1f, rect.width - FieldLabelWidth));
            DrawTextField(new Rect(rect.x + FieldLabelWidth, rect.y, width, rect.height), _session.HexField);
        }

        private void DrawTextField(Rect rect, ColorPickerTextField field)
        {
            if (field == null)
                return;

            Color oldColor = GUI.color;
            if (!field.IsValid)
                GUI.color = Style.InvalidFieldTint;

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
                GUI.FocusControl(field.Id);
            GUI.SetNextControlName(field.Id);
            string next = GUI.TextField(rect, field.Text, FieldStyle);
            GUI.color = oldColor;

            bool focused = GUI.GetNameOfFocusedControl() == field.Id;
            if (focused)
                field.BeginEdit();

            if (next != field.Text)
            {
                field.BeginEdit();
                field.SetDraft(next);
            }
        }

        private void HandlePickerDrag(Rect pickerRect, Rect hueRect, Rect alphaRect)
        {
            Event evt = Event.current;
            if (evt == null)
                return;

            if (evt.type == EventType.MouseUp)
            {
                _session.ActiveControl = ColorPickerActiveControl.None;
                return;
            }

            if (evt.type == EventType.ScrollWheel)
            {
                if (hueRect.Contains(evt.mousePosition))
                {
                    _session.SetHue(_session.Hsv.H - (evt.delta.y / Math.Max(1f, hueRect.width)));
                    evt.Use();
                }
                else if (alphaRect.Contains(evt.mousePosition))
                {
                    _session.SetAlpha(_session.CurrentColor.A - (evt.delta.y / Math.Max(1f, alphaRect.width)));
                    evt.Use();
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (pickerRect.Contains(evt.mousePosition)) _session.ActiveControl = ColorPickerActiveControl.SaturationValue;
                else if (hueRect.Contains(evt.mousePosition)) _session.ActiveControl = ColorPickerActiveControl.Hue;
                else if (alphaRect.Contains(evt.mousePosition)) _session.ActiveControl = ColorPickerActiveControl.Alpha;
            }

            if (evt.type != EventType.MouseDown && evt.type != EventType.MouseDrag)
                return;

            if (_session.ActiveControl == ColorPickerActiveControl.SaturationValue)
            {
                float s = ColorPickerMath.Clamp01((evt.mousePosition.x - pickerRect.x) / pickerRect.width);
                float v = 1f - ColorPickerMath.Clamp01((evt.mousePosition.y - pickerRect.y) / pickerRect.height);
                _session.SetSaturationValue(s, v);
                evt.Use();
            }
            else if (_session.ActiveControl == ColorPickerActiveControl.Hue)
            {
                float h = ColorPickerMath.Clamp01((evt.mousePosition.x - hueRect.x) / hueRect.width);
                _session.SetHue(h);
                evt.Use();
            }
            else if (_session.ActiveControl == ColorPickerActiveControl.Alpha)
            {
                float a = ColorPickerMath.Clamp01((evt.mousePosition.x - alphaRect.x) / alphaRect.width);
                _session.SetAlpha(a);
                evt.Use();
            }
        }

        private void HandleKeyboard()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown)
                return;

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                string focused = GUI.GetNameOfFocusedControl();
                if (!string.IsNullOrEmpty(focused))
                {
                    _session.TryCommitField(focused);
                    GUI.FocusControl(null);
                }
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                _session.Cancel();
                evt.Use();
            }
        }

        private void CommitTextFieldFocusChange(string focused)
        {
            string focusedFieldId = IsColorPickerTextField(focused) ? focused : null;
            if (!string.Equals(_focusedTextFieldId, focusedFieldId, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(_focusedTextFieldId))
                {
                    _session.TryCommitField(_focusedTextFieldId);
                    ColorPickerTextField previous = _session.GetField(_focusedTextFieldId);
                    if (previous != null)
                        previous.EndEdit();
                }

                _focusedTextFieldId = focusedFieldId;
            }

            if (!string.IsNullOrEmpty(_focusedTextFieldId))
            {
                ColorPickerTextField current = _session.GetField(_focusedTextFieldId);
                if (current != null)
                    current.BeginEdit();
            }
        }

        private static bool IsColorPickerTextField(string controlName)
        {
            return string.Equals(controlName, ColorPickerSession.HueFieldId, StringComparison.Ordinal)
                || string.Equals(controlName, ColorPickerSession.SaturationFieldId, StringComparison.Ordinal)
                || string.Equals(controlName, ColorPickerSession.ValueFieldId, StringComparison.Ordinal)
                || string.Equals(controlName, ColorPickerSession.AlphaHsvFieldId, StringComparison.Ordinal)
                || string.Equals(controlName, ColorPickerSession.RedFieldId, StringComparison.Ordinal)
                || string.Equals(controlName, ColorPickerSession.GreenFieldId, StringComparison.Ordinal)
                || string.Equals(controlName, ColorPickerSession.BlueFieldId, StringComparison.Ordinal)
                || string.Equals(controlName, ColorPickerSession.AlphaRgbFieldId, StringComparison.Ordinal)
                || string.Equals(controlName, ColorPickerSession.HexFieldId, StringComparison.Ordinal);
        }

        private void DrawColorPreview(Rect rect, ModColor color)
        {
            GUI.DrawTexture(rect, CheckerTexture);
            Color old = GUI.color;
            GUI.color = ToUnityColor(color);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Box(rect, GUIContent.none);
        }

        private void DrawHandle(Rect rect)
        {
            Color old = GUI.color;
            GUI.color = Style.HandleColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Style.HandleBorderColor;
            GUI.Box(rect, GUIContent.none);
            GUI.color = old;
        }

        private void EnsureTextures(ColorPickerLayout layout)
        {
            int svSize = Math.Max(1, (int)Math.Round(layout.SaturationValueRect.width));
            int hueWidth = Math.Max(1, (int)Math.Round(layout.HueRect.width));
            int hueHeight = Math.Max(1, (int)Math.Round(layout.HueRect.height));
            int alphaWidth = Math.Max(1, (int)Math.Round(layout.AlphaRect.width));
            int alphaHeight = Math.Max(1, (int)Math.Round(layout.AlphaRect.height));

            if (_hueTexture == null || _cachedHueTextureWidth != hueWidth || _cachedHueTextureHeight != hueHeight)
            {
                DestroyTexture(ref _hueTexture);
                _hueTexture = CreateHueTexture(hueWidth, hueHeight);
                _cachedHueTextureWidth = hueWidth;
                _cachedHueTextureHeight = hueHeight;
            }

            if (_svTexture == null
                || _cachedSvTextureSize != svSize
                || Math.Abs(_cachedHue - _session.Hsv.H) > 0.0001f
                || Math.Abs(_cachedSvAlpha - _session.CurrentColor.A) > 0.0001f)
            {
                DestroyTexture(ref _svTexture);
                _svTexture = CreateSaturationValueTexture(svSize, svSize, _session.Hsv.H, _session.CurrentColor.A);
                _cachedSvTextureSize = svSize;
                _cachedHue = _session.Hsv.H;
                _cachedSvAlpha = _session.CurrentColor.A;
            }

            if (_alphaTexture == null
                || _cachedAlphaTextureWidth != alphaWidth
                || _cachedAlphaTextureHeight != alphaHeight
                || Math.Abs(_cachedSaturation - _session.Hsv.S) > 0.0001f
                || !_cachedAlphaColor.NearlyEquals(_session.CurrentColor))
            {
                DestroyTexture(ref _alphaTexture);
                _alphaTexture = CreateAlphaTexture(alphaWidth, alphaHeight, _session.CurrentColor);
                _cachedAlphaTextureWidth = alphaWidth;
                _cachedAlphaTextureHeight = alphaHeight;
                _cachedSaturation = _session.Hsv.S;
                _cachedAlphaColor = _session.CurrentColor;
            }
        }

        private Texture2D CheckerTexture
        {
            get
            {
                if (_checkerTexture == null)
                    _checkerTexture = CreateCheckerTexture(16, 16);
                return _checkerTexture;
            }
        }

        private static Texture2D CreateSaturationValueTexture(int width, int height, float hue, float alpha)
        {
            Texture2D texture = new Texture2D(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float s = width <= 1 ? 0f : x / (float)(width - 1);
                    float v = height <= 1 ? 0f : y / (float)(height - 1);
                    texture.SetPixel(x, y, ToUnityColor(ColorPickerMath.HsvToRgb(hue, s, v, alpha)));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateHueTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = width <= 1 ? 0f : x / (float)(width - 1);
                    Color color = ToUnityColor(ColorPickerMath.HsvToRgb(h, 1f, 1f, 1f));
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateAlphaTexture(int width, int height, ModColor color)
        {
            Texture2D texture = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = width <= 1 ? 0f : x / (float)(width - 1);
                    Color unity = new Color(color.R, color.G, color.B, alpha);
                    texture.SetPixel(x, y, unity);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateCheckerTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            Color a = Color.white;
            Color b = new Color(0.78f, 0.78f, 0.78f, 1f);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool odd = ((x / 4) + (y / 4)) % 2 == 1;
                    texture.SetPixel(x, y, odd ? a : b);
                }
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            return texture;
        }

        private GUIStyle LabelStyle
        {
            get { return Style.Label ?? GUI.skin.label; }
        }

        private GUIStyle FieldStyle
        {
            get { return Style.Field ?? GUI.skin.textField; }
        }

        private GUIStyle ButtonStyle
        {
            get { return Style.Button ?? GUI.skin.button; }
        }

        private static Color ToUnityColor(ModColor color)
        {
            return new Color(color.R, color.G, color.B, color.A);
        }

        private static bool ShouldUseEvent(Event evt)
        {
            return evt.type == EventType.MouseDown
                || evt.type == EventType.MouseDrag
                || evt.type == EventType.ScrollWheel;
        }

        private ColorPickerLayout BuildLayout(Rect rect)
        {
            Rect content = new Rect(
                rect.x + Margin,
                rect.y + Margin,
                Math.Max(1f, rect.width - (Margin * 2f)),
                Math.Max(1f, rect.height - (Margin * 2f)));

            float fixedHeight = HueBarHeight
                + AlphaBarHeight
                + PreviewSize
                + NumericGridHeight
                + SwatchRowsHeight
                + ButtonHeight
                + (FixedGap * 6f);
            float availableSvSize = content.height - fixedHeight;
            float svSize = Math.Min(Math.Max(MinimumDrawableSaturationValueSize, availableSvSize), Math.Min(PickerSize, content.width));
            if (availableSvSize >= MinimumSaturationValueSize)
                svSize = Math.Min(Math.Min(PickerSize, availableSvSize), content.width);

            float y = content.y;
            ColorPickerLayout layout = new ColorPickerLayout();
            layout.HueRect = new Rect(content.x, y, content.width, HueBarHeight);
            y += HueBarHeight + FixedGap;

            layout.SaturationValueRect = new Rect(content.x + Math.Max(0f, (content.width - svSize) * 0.5f), y, svSize, svSize);
            y += svSize + FixedGap;

            layout.AlphaRect = new Rect(content.x, y, content.width, AlphaBarHeight);
            y += AlphaBarHeight + FixedGap;

            layout.PreviewRect = new Rect(content.x, y, content.width, PreviewSize);
            y += PreviewSize + FixedGap;

            float columnWidth = Math.Max(1f, (content.width - FixedGap) * 0.5f);
            layout.HexFieldRect = new Rect(content.x, y, columnWidth, FieldHeight);
            layout.RgbFieldRect = new Rect(content.x + columnWidth + FixedGap, y, columnWidth, FieldHeight);
            y += FieldHeight + FixedGap;
            layout.HsvFieldRect = new Rect(content.x, y, columnWidth, FieldHeight);
            y += FieldHeight + FixedGap;

            layout.BasicsSwatchRect = new Rect(content.x, y, content.width, SwatchSize);
            y += SwatchSize + FixedGap;
            layout.PinnedSwatchRect = new Rect(content.x, y, content.width, SwatchSize);
            y += SwatchSize + FixedGap;
            layout.RecentSwatchRect = new Rect(content.x, y, content.width, SwatchSize);
            y += SwatchSize + FixedGap;

            layout.ButtonRect = new Rect(content.x, y, content.width, ButtonHeight);
            return layout;
        }

        private float NumericGridHeight
        {
            get { return (FieldHeight * 2f) + FixedGap; }
        }

        private float SwatchRowsHeight
        {
            get { return (SwatchSize * 3f) + (FixedGap * 2f); }
        }

        private float CalculateRequiredHeight(float saturationValueSize)
        {
            return (Margin * 2f)
                + HueBarHeight
                + saturationValueSize
                + AlphaBarHeight
                + PreviewSize
                + NumericGridHeight
                + SwatchRowsHeight
                + ButtonHeight
                + (FixedGap * 6f);
        }

#if DEBUG
        private static void GuardNoLayoutIntersections(ColorPickerLayout layout)
        {
            string[] names = new[]
            {
                "hue",
                "sv",
                "alpha",
                "preview",
                "hex",
                "rgb",
                "hsv",
                "basics",
                "pinned",
                "recent",
                "buttons"
            };
            Rect[] rects = new[]
            {
                layout.HueRect,
                layout.SaturationValueRect,
                layout.AlphaRect,
                layout.PreviewRect,
                layout.HexFieldRect,
                layout.RgbFieldRect,
                layout.HsvFieldRect,
                layout.BasicsSwatchRect,
                layout.PinnedSwatchRect,
                layout.RecentSwatchRect,
                layout.ButtonRect
            };

            for (int i = 0; i < rects.Length; i++)
            {
                for (int j = i + 1; j < rects.Length; j++)
                {
                    if (Intersects(rects[i], rects[j]))
                    {
                        MMLog.WarnOnce(
                            "ColorPicker.LayoutIntersection." + names[i] + "." + names[j],
                            "[ColorPicker] Layout rects intersect: " + names[i] + "=" + FormatRect(rects[i]) + " " + names[j] + "=" + FormatRect(rects[j]));
                    }
                }
            }
        }

        private static bool Intersects(Rect a, Rect b)
        {
            return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        }

        private static string FormatRect(Rect rect)
        {
            return "(" + rect.x.ToString("0.##") + "," + rect.y.ToString("0.##") + "," + rect.width.ToString("0.##") + "," + rect.height.ToString("0.##") + ")";
        }
#endif

        private struct ColorPickerLayout
        {
            public Rect HueRect;
            public Rect SaturationValueRect;
            public Rect AlphaRect;
            public Rect PreviewRect;
            public Rect HexFieldRect;
            public Rect RgbFieldRect;
            public Rect HsvFieldRect;
            public Rect BasicsSwatchRect;
            public Rect PinnedSwatchRect;
            public Rect RecentSwatchRect;
            public Rect ButtonRect;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
                return;

            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
