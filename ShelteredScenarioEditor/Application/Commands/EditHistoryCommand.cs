namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class EditHistoryAutomationIds
    {
        public const string Undo = "editor.edit_history.undo";
        public const string Redo = "editor.edit_history.redo";
    }

    internal enum EditHistoryCommandKind
    {
        Undo,
        Redo
    }

    internal sealed class EditHistoryCommand : ScenarioAuthoringCommand
    {
        private static readonly EditHistoryCommand UndoCommand =
            new EditHistoryCommand(EditHistoryCommandKind.Undo, EditHistoryAutomationIds.Undo);
        private static readonly EditHistoryCommand RedoCommand =
            new EditHistoryCommand(EditHistoryCommandKind.Redo, EditHistoryAutomationIds.Redo);

        private EditHistoryCommand(EditHistoryCommandKind kind, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
        }

        public EditHistoryCommandKind Kind { get; private set; }
        public static EditHistoryCommand Undo { get { return UndoCommand; } }
        public static EditHistoryCommand Redo { get { return RedoCommand; } }
    }
}
