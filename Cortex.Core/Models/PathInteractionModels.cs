namespace Cortex.Core.Models
{
    public enum PathSelectionKind
    {
        Folder = 0,
        OpenFile = 1
    }

    public sealed class PathSelectionRequest
    {
        public PathSelectionKind SelectionKind = PathSelectionKind.Folder;
        public string Title = string.Empty;
        public string InitialPath = string.Empty;
        public string SuggestedFileName = string.Empty;
        public string Filter = string.Empty;
        public bool CheckPathExists = true;
        public bool RestoreDirectory = true;
    }

    public sealed class PathSelectionResult
    {
        public bool Succeeded;
        public string SelectedPath = string.Empty;
        public string ErrorMessage = string.Empty;
    }
}
