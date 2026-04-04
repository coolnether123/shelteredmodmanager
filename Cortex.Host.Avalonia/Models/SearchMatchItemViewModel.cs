namespace Cortex.Host.Avalonia.Models
{
    internal sealed class SearchMatchItemViewModel
    {
        public int ResultIndex { get; set; } = -1;
        public string DisplayText { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
