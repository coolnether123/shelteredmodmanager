using System;
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
        private ModColor _cachedAlphaColor;
        private string _focusedTextFieldId;

        public ColorPickerImguiControl(ColorPickerSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            _session = session;
            Style = new ColorPickerImguiStyle();
            PickerSize = 220f;
            SliderWidth = 16f;
            Margin = 6f;
            FieldHeight = 22f;
            ButtonHeight = 28f;
            PreviewSize = 56f;
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

        public Vector2 PreferredSize
        {
            get
            {
                float rightWidth = PreviewSize * 2f + Margin;
                rightWidth = Math.Max(rightWidth, 188f);
                float width = PickerSize + Margin + SliderWidth + Margin + SliderWidth + Margin + rightWidth;
                float rightHeight = PreviewSize
                    + Margin
                    + (SwatchSize * 3f)
                    + Margin
                    + (FieldHeight * 3f)
                    + (Margin * 2f)
                    + ButtonHeight
                    + Margin
                    + ButtonHeight;
                return new Vector2(width, Math.Max(PickerSize, rightHeight));
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

            Rect pickerRect = new Rect(rect.x, rect.y, PickerSize, PickerSize);
            Rect hueRect = new Rect(pickerRect.xMax + Margin, rect.y, SliderWidth, PickerSize);
            Rect alphaRect = new Rect(hueRect.xMax + Margin, rect.y, SliderWidth, PickerSize);
            Rect rightRect = new Rect(alphaRect.xMax + Margin, rect.y, rect.xMax - alphaRect.xMax - Margin, rect.height);

            EnsureTextures();
            DrawPickerArea(pickerRect, hueRect, alphaRect);
            DrawRightColumn(rightRect);
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
            DrawHandle(new Rect(hueRect.x - 2f, hueRect.y + ((1f - _session.Hsv.H) * hueRect.height) - 3f, hueRect.width + 4f, 6f));
            DrawHandle(new Rect(alphaRect.x - 2f, alphaRect.y + ((1f - _session.CurrentColor.A) * alphaRect.height) - 3f, alphaRect.width + 4f, 6f));

            HandlePickerDrag(pickerRect, hueRect, alphaRect);
        }

        private void DrawRightColumn(Rect rect)
        {
            Rect newPreview = new Rect(rect.x, rect.y, PreviewSize, PreviewSize);
            Rect oldPreview = new Rect(newPreview.xMax + Margin, rect.y, PreviewSize, PreviewSize);
            DrawColorPreview(newPreview, _session.CurrentColor);
            DrawColorPreview(oldPreview, _session.OldColor);
            if (GUI.Button(oldPreview, GUIContent.none, GUIStyle.none))
                _session.RestoreOldColor();

            Rect swatches = new Rect(rect.x, newPreview.yMax + Margin, rect.width, SwatchSize * 3f);
            DrawSwatches(swatches);

            Rect hsv = new Rect(rect.x, swatches.yMax + Margin, rect.width, FieldHeight);
            Rect rgb = new Rect(rect.x, hsv.yMax + Margin, rect.width, FieldHeight);
            Rect hex = new Rect(rect.x, rgb.yMax + Margin, rect.width, FieldHeight);
            DrawFieldRow(hsv, "HSV", _session.HueField, _session.SaturationField, _session.ValueField, _session.AlphaHsvField);
            DrawFieldRow(rgb, "RGB", _session.RedField, _session.GreenField, _session.BlueField, _session.AlphaRgbField);
            DrawHexRow(hex);
            CommitTextFieldFocusChange(GUI.GetNameOfFocusedControl());

            Rect applyRect = new Rect(rect.x, hex.yMax + Margin, (rect.width - Margin) * 0.5f, ButtonHeight);
            Rect cancelRect = new Rect(applyRect.xMax + Margin, applyRect.y, applyRect.width, ButtonHeight);
            Rect okRect = new Rect(rect.x, applyRect.yMax + Margin, rect.width, ButtonHeight);

            if (GUI.Button(applyRect, "Apply", ButtonStyle))
                _session.Apply();
            if (GUI.Button(cancelRect, "Cancel", ButtonStyle))
                _session.Cancel();
            if (GUI.Button(okRect, "OK", ButtonStyle))
                _session.Ok();
        }

        private void DrawSwatches(Rect rect)
        {
            float x = rect.x;
            float y = rect.y;
            ModColor[] pinned = _session.Palette.PinnedColors;
            ModColor[] recent = _session.Palette.RecentColors;

            for (int i = 0; i < pinned.Length; i++)
                DrawSwatch(ref x, ref y, rect, pinned[i], true);

            if (pinned.Length > 0 && recent.Length > 0)
            {
                x = rect.x;
                y += SwatchSize + 2f;
            }

            for (int i = 0; i < recent.Length; i++)
                DrawSwatch(ref x, ref y, rect, recent[i], false);
        }

        private void DrawSwatch(ref float x, ref float y, Rect bounds, ModColor color, bool pinned)
        {
            if (y + SwatchSize > bounds.yMax)
                return;

            Rect swatch = new Rect(x, y, SwatchSize, SwatchSize);
            DrawColorPreview(swatch, color);
            if (pinned)
                GUI.Label(swatch, "*", Style.SwatchLabel ?? GUI.skin.label);

            Event evt = Event.current;
            if (evt != null && swatch.Contains(evt.mousePosition) && evt.type == EventType.MouseDown)
            {
                if (evt.button == 1)
                    _session.TogglePinned(color);
                else
                    _session.SelectSwatch(color);
                evt.Use();
            }

            x += SwatchSize + 2f;
            if (x + SwatchSize > bounds.xMax)
            {
                x = bounds.x;
                y += SwatchSize + 2f;
            }
        }

        private void DrawFieldRow(Rect rect, string label, ColorPickerTextField a, ColorPickerTextField b, ColorPickerTextField c, ColorPickerTextField d)
        {
            float labelWidth = 32f;
            GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label, LabelStyle);

            float fieldWidth = Math.Max(36f, (rect.width - labelWidth) / 4f);
            DrawTextField(new Rect(rect.x + labelWidth, rect.y, fieldWidth, rect.height), a);
            DrawTextField(new Rect(rect.x + labelWidth + fieldWidth, rect.y, fieldWidth, rect.height), b);
            DrawTextField(new Rect(rect.x + labelWidth + (fieldWidth * 2f), rect.y, fieldWidth, rect.height), c);
            DrawTextField(new Rect(rect.x + labelWidth + (fieldWidth * 3f), rect.y, fieldWidth, rect.height), d);
        }

        private void DrawHexRow(Rect rect)
        {
            float labelWidth = 32f;
            GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), "HEX", LabelStyle);
            DrawTextField(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height), _session.HexField);
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
                    _session.SetHue(_session.Hsv.H - (evt.delta.y / PickerSize));
                    evt.Use();
                }
                else if (alphaRect.Contains(evt.mousePosition))
                {
                    _session.SetAlpha(_session.CurrentColor.A - (evt.delta.y / PickerSize));
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
                float h = 1f - ColorPickerMath.Clamp01((evt.mousePosition.y - hueRect.y) / hueRect.height);
                _session.SetHue(h);
                evt.Use();
            }
            else if (_session.ActiveControl == ColorPickerActiveControl.Alpha)
            {
                float a = 1f - ColorPickerMath.Clamp01((evt.mousePosition.y - alphaRect.y) / alphaRect.height);
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

        private void EnsureTextures()
        {
            if (_hueTexture == null)
                _hueTexture = CreateHueTexture(1, (int)PickerSize);

            if (_svTexture == null
                || Math.Abs(_cachedHue - _session.Hsv.H) > 0.0001f
                || Math.Abs(_cachedSvAlpha - _session.CurrentColor.A) > 0.0001f)
            {
                DestroyTexture(ref _svTexture);
                _svTexture = CreateSaturationValueTexture((int)PickerSize, (int)PickerSize, _session.Hsv.H, _session.CurrentColor.A);
                _cachedHue = _session.Hsv.H;
                _cachedSvAlpha = _session.CurrentColor.A;
            }

            if (_alphaTexture == null
                || Math.Abs(_cachedSaturation - _session.Hsv.S) > 0.0001f
                || !_cachedAlphaColor.NearlyEquals(_session.CurrentColor))
            {
                DestroyTexture(ref _alphaTexture);
                _alphaTexture = CreateAlphaTexture(1, (int)PickerSize, _session.CurrentColor);
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
                float h = height <= 1 ? 0f : y / (float)(height - 1);
                Color color = ToUnityColor(ColorPickerMath.HsvToRgb(h, 1f, 1f, 1f));
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, color);
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateAlphaTexture(int width, int height, ModColor color)
        {
            Texture2D texture = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                float alpha = height <= 1 ? 0f : y / (float)(height - 1);
                Color unity = new Color(color.R, color.G, color.B, alpha);
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, unity);
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

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
                return;

            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
