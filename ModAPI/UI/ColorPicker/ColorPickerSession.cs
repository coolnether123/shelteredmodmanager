using System;

namespace ModAPI.UI.ColorPicker
{
    public enum ColorPickerCommitKind
    {
        Apply = 0,
        Ok = 1,
        Cancel = 2,
        AutoApply = 3
    }

    public enum ColorPickerActiveControl
    {
        None = 0,
        SaturationValue = 1,
        Hue = 2,
        Alpha = 3
    }

    public delegate void ColorPickerCommitCallback(ModColor color, ColorPickerCommitKind commitKind);

    /// <summary>
    /// Optional callbacks and behavior flags for a color picker session.
    /// </summary>
    public sealed class ColorPickerOptions
    {
        public bool AutoApply { get; set; }
        public ColorPickerCommitCallback OnCommit { get; set; }
        public Action OnCancel { get; set; }
        public Action OnClosed { get; set; }
    }

    /// <summary>
    /// Presentation-neutral color picker state. Renderers should mutate this object instead of owning color logic.
    /// Example: create a session with <c>ColorPickerPaletteStore.LoadDefault()</c>, draw it from an IMGUI popup,
    /// then call <c>Ok()</c> or <c>Cancel()</c> from the hosting window.
    /// </summary>
    public sealed class ColorPickerSession
    {
        public const string HueFieldId = "ModAPI.ColorPicker.Hue";
        public const string SaturationFieldId = "ModAPI.ColorPicker.Saturation";
        public const string ValueFieldId = "ModAPI.ColorPicker.Value";
        public const string AlphaHsvFieldId = "ModAPI.ColorPicker.AlphaHsv";
        public const string RedFieldId = "ModAPI.ColorPicker.Red";
        public const string GreenFieldId = "ModAPI.ColorPicker.Green";
        public const string BlueFieldId = "ModAPI.ColorPicker.Blue";
        public const string AlphaRgbFieldId = "ModAPI.ColorPicker.AlphaRgb";
        public const string HexFieldId = "ModAPI.ColorPicker.Hex";

        private readonly ColorPickerOptions _options;
        private readonly string _palettePath;

        public ColorPickerSession(ModColor initialColor)
            : this(initialColor, ColorPickerPaletteStore.LoadDefault(), new ColorPickerOptions(), ColorPickerPaletteStore.DefaultPalettePath)
        {
        }

        public ColorPickerSession(ModColor initialColor, ColorPickerPalette palette, ColorPickerOptions options, string palettePath)
        {
            OldColor = initialColor;
            AppliedColor = initialColor;
            CurrentColor = initialColor;
            Hsv = initialColor.ToHsv();
            Palette = palette ?? new ColorPickerPalette();
            _options = options ?? new ColorPickerOptions();
            _palettePath = palettePath;

            HueField = NewUnitField(HueFieldId);
            SaturationField = NewUnitField(SaturationFieldId);
            ValueField = NewUnitField(ValueFieldId);
            AlphaHsvField = NewUnitField(AlphaHsvFieldId);
            RedField = NewUnitField(RedFieldId);
            GreenField = NewUnitField(GreenFieldId);
            BlueField = NewUnitField(BlueFieldId);
            AlphaRgbField = NewUnitField(AlphaRgbFieldId);
            HexField = new ColorPickerTextField(HexFieldId, initialColor.ToHexRgba(), IsHex);

            SyncTextFields(null, true);
        }

        public ModColor OldColor { get; private set; }
        public ModColor AppliedColor { get; private set; }
        public ModColor CurrentColor { get; private set; }
        public ModHsvColor Hsv { get; private set; }
        public ColorPickerPalette Palette { get; private set; }
        public ColorPickerActiveControl ActiveControl { get; set; }
        public bool WantsClose { get; private set; }
        public bool Accepted { get; private set; }

        public ColorPickerTextField HueField { get; private set; }
        public ColorPickerTextField SaturationField { get; private set; }
        public ColorPickerTextField ValueField { get; private set; }
        public ColorPickerTextField AlphaHsvField { get; private set; }
        public ColorPickerTextField RedField { get; private set; }
        public ColorPickerTextField GreenField { get; private set; }
        public ColorPickerTextField BlueField { get; private set; }
        public ColorPickerTextField AlphaRgbField { get; private set; }
        public ColorPickerTextField HexField { get; private set; }

        public void SetRgb(float r, float g, float b, float a)
        {
            SetColor(new ModColor(r, g, b, a), null, false);
        }

        public void SetHsv(float h, float s, float v, float a)
        {
            Hsv = new ModHsvColor(h, s, v, a);
            SetColor(Hsv.ToRgb(), null, true);
        }

        public void SetHex(string hex)
        {
            ModColor color;
            if (!ModColor.TryParseHex(hex, out color))
                return;

            SetColor(color, HexFieldId, false);
        }

        public void SetSaturationValue(float saturation, float value)
        {
            SetHsv(Hsv.H, saturation, value, Hsv.A);
        }

        public void SetHue(float hue)
        {
            SetHsv(hue, Hsv.S, Hsv.V, Hsv.A);
        }

        public void SetAlpha(float alpha)
        {
            SetHsv(Hsv.H, Hsv.S, Hsv.V, alpha);
        }

        public void RestoreOldColor()
        {
            SetColor(OldColor, null, false);
        }

        public void SelectSwatch(ModColor color)
        {
            SetColor(color, null, false);
        }

        public bool TogglePinned(ModColor color)
        {
            bool changed = Palette.TogglePin(color);
            if (changed)
                SavePalette();
            return changed;
        }

        public void Apply()
        {
            Commit(ColorPickerCommitKind.Apply, false);
        }

        public void Ok()
        {
            Commit(ColorPickerCommitKind.Ok, true);
        }

        public void Cancel()
        {
            CurrentColor = AppliedColor;
            Hsv = CurrentColor.ToHsv();
            SyncTextFields(null, true);
            Accepted = false;
            WantsClose = true;
            if (_options.OnCancel != null)
                _options.OnCancel();
            if (_options.OnCommit != null)
                _options.OnCommit(AppliedColor, ColorPickerCommitKind.Cancel);
            if (_options.OnClosed != null)
                _options.OnClosed();
        }

        public bool TryCommitField(string fieldId)
        {
            ColorPickerTextField field = GetField(fieldId);
            if (field == null || !field.TryCommit())
                return false;

            float value;
            if (fieldId == HexFieldId)
            {
                SetHex(field.Text);
                return true;
            }

            if (!ColorPickerMath.TryParseUnitFloat(field.Text, out value))
                return false;

            if (fieldId == HueFieldId) SetHsv(value, Hsv.S, Hsv.V, Hsv.A);
            else if (fieldId == SaturationFieldId) SetHsv(Hsv.H, value, Hsv.V, Hsv.A);
            else if (fieldId == ValueFieldId) SetHsv(Hsv.H, Hsv.S, value, Hsv.A);
            else if (fieldId == AlphaHsvFieldId || fieldId == AlphaRgbFieldId) SetHsv(Hsv.H, Hsv.S, Hsv.V, value);
            else if (fieldId == RedFieldId) SetRgb(value, CurrentColor.G, CurrentColor.B, CurrentColor.A);
            else if (fieldId == GreenFieldId) SetRgb(CurrentColor.R, value, CurrentColor.B, CurrentColor.A);
            else if (fieldId == BlueFieldId) SetRgb(CurrentColor.R, CurrentColor.G, value, CurrentColor.A);
            else return false;

            return true;
        }

        public ColorPickerTextField GetField(string fieldId)
        {
            if (fieldId == HueFieldId) return HueField;
            if (fieldId == SaturationFieldId) return SaturationField;
            if (fieldId == ValueFieldId) return ValueField;
            if (fieldId == AlphaHsvFieldId) return AlphaHsvField;
            if (fieldId == RedFieldId) return RedField;
            if (fieldId == GreenFieldId) return GreenField;
            if (fieldId == BlueFieldId) return BlueField;
            if (fieldId == AlphaRgbFieldId) return AlphaRgbField;
            if (fieldId == HexFieldId) return HexField;
            return null;
        }

        public void SyncTextFields(string activeFieldId, bool force)
        {
            SetFieldText(HueField, ColorPickerMath.FormatUnitFloat(Hsv.H), activeFieldId, force);
            SetFieldText(SaturationField, ColorPickerMath.FormatUnitFloat(Hsv.S), activeFieldId, force);
            SetFieldText(ValueField, ColorPickerMath.FormatUnitFloat(Hsv.V), activeFieldId, force);
            SetFieldText(AlphaHsvField, ColorPickerMath.FormatUnitFloat(CurrentColor.A), activeFieldId, force);
            SetFieldText(RedField, ColorPickerMath.FormatUnitFloat(CurrentColor.R), activeFieldId, force);
            SetFieldText(GreenField, ColorPickerMath.FormatUnitFloat(CurrentColor.G), activeFieldId, force);
            SetFieldText(BlueField, ColorPickerMath.FormatUnitFloat(CurrentColor.B), activeFieldId, force);
            SetFieldText(AlphaRgbField, ColorPickerMath.FormatUnitFloat(CurrentColor.A), activeFieldId, force);
            SetFieldText(HexField, CurrentColor.ToHexRgba(), activeFieldId, force);
        }

        private void SetColor(ModColor color, string activeFieldId, bool hsvAlreadyUpdated)
        {
            CurrentColor = color;
            if (!hsvAlreadyUpdated)
                Hsv = color.ToHsv();

            SyncTextFields(activeFieldId, false);

            if (_options.AutoApply)
                Commit(ColorPickerCommitKind.AutoApply, false);
        }

        private void Commit(ColorPickerCommitKind kind, bool close)
        {
            AppliedColor = CurrentColor;
            Palette.AddRecent(CurrentColor);
            SavePalette();

            if (_options.OnCommit != null)
                _options.OnCommit(CurrentColor, kind);

            if (close)
            {
                Accepted = true;
                WantsClose = true;
                if (_options.OnClosed != null)
                    _options.OnClosed();
            }
        }

        private void SavePalette()
        {
            if (string.IsNullOrEmpty(_palettePath))
                return;

            ColorPickerPaletteStore.Save(_palettePath, Palette);
        }

        private static void SetFieldText(ColorPickerTextField field, string value, string activeFieldId, bool force)
        {
            if (field == null)
                return;

            field.SetCommittedText(value, force || field.Id != activeFieldId);
        }

        private static ColorPickerTextField NewUnitField(string id)
        {
            return new ColorPickerTextField(id, "0", IsUnitFloat);
        }

        private static bool IsUnitFloat(string text)
        {
            float value;
            return ColorPickerMath.TryParseUnitFloat(text, out value);
        }

        private static bool IsHex(string text)
        {
            ModColor color;
            return ModColor.TryParseHex(text, out color);
        }
    }
}
