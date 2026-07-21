using System;

namespace ModAPI.UI.ColorPicker
{
    /// <summary>
    /// Deferred-commit text field model for picker numeric and hex inputs.
    /// Invalid draft text is retained for the user, but it does not mutate the picker state.
    /// </summary>
    public sealed class ColorPickerTextField
    {
        private readonly Func<string, bool> _validator;
        private string _text;
        private string _committedText;

        public ColorPickerTextField(string id, string initialText, Func<string, bool> validator)
        {
            Id = id ?? string.Empty;
            _validator = validator;
            _text = initialText ?? string.Empty;
            _committedText = _text;
            IsValid = Validate(_text);
        }

        public string Id { get; private set; }
        public bool IsEditing { get; private set; }
        public bool IsValid { get; private set; }
        public string LastError { get; private set; }

        public string Text
        {
            get { return _text; }
        }

        public string CommittedText
        {
            get { return _committedText; }
        }

        public void BeginEdit()
        {
            IsEditing = true;
        }

        public void EndEdit()
        {
            IsEditing = false;
        }

        public void SetDraft(string text)
        {
            _text = text ?? string.Empty;
            IsValid = Validate(_text);
        }

        public bool TryCommit()
        {
            IsValid = Validate(_text);
            if (!IsValid)
                return false;

            _committedText = _text;
            return true;
        }

        public void SetCommittedText(string text, bool force)
        {
            if (IsEditing && !force)
                return;

            _text = text ?? string.Empty;
            _committedText = _text;
            IsValid = Validate(_text);
        }

        private bool Validate(string text)
        {
            if (_validator == null || _validator(text))
            {
                LastError = string.Empty;
                return true;
            }

            LastError = "Invalid color value.";
            return false;
        }
    }
}
