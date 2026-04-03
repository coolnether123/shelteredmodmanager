namespace Cortex.Services.Editor.Presentation
{
    internal sealed class EditorSearchShortcutInput
    {
        public bool Control;
        public bool Alt;
        public bool Shift;
        public string KeyCode = string.Empty;
        public bool HasFocusedControl;
        public string FocusedControlName = string.Empty;
    }

    internal sealed class EditorCodeAreaPresentation
    {
        public bool UsesUnifiedSourceSurface;
        public bool IsEditingEnabled;
    }

    internal sealed class EditorStatusBarPresentation
    {
        public bool CanToggleEditMode;
        public bool IsEditing;
        public bool IsDirty;
        public bool CanSaveAll;
        public int Line;
        public int Column;
        public int LineCount;
        public string LanguageStatusLabel = string.Empty;
        public string CompletionStatusLabel = string.Empty;
        public string EditModeTooltip = string.Empty;
    }

    internal sealed class EditorHoverRefreshPlan
    {
        public bool ShouldInvalidateSurfaces;
        public string HoverKey = string.Empty;
    }
}
