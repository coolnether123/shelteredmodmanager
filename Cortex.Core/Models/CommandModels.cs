namespace Cortex.Core.Models
{
    public delegate void CommandHandler(CommandExecutionContext context);
    public delegate bool CommandEnablement(CommandExecutionContext context);

    public sealed class CommandDefinition
    {
        public string CommandId;
        public string DisplayName;
        public string Category;
        public string IconId;
        public string Description;
        public string DefaultGesture;
        public int SortOrder;
        public bool ShowInPalette;
        public bool IsGlobal;
    }

    public sealed class CommandExecutionContext
    {
        public string ActiveContainerId;
        public string ActiveDocumentId;
        public string FocusedRegionId;
        public object Parameter;
    }

    public sealed class CommandHandlerRegistration
    {
        public string CommandId;
        public CommandHandler Handler;
        public CommandEnablement CanExecute;
    }

    public sealed class CommandBinding
    {
        public string CommandId;
        public string Gesture;
    }
}
