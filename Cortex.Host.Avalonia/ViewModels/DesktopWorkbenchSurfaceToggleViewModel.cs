namespace Cortex.Host.Avalonia.ViewModels
{
    internal sealed class DesktopWorkbenchSurfaceToggleViewModel : ViewModelBase
    {
        private bool _isVisible;

        public string SurfaceId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool CanHide { get; set; } = true;

        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                if (_isVisible == value)
                {
                    return;
                }

                _isVisible = value;
                RaisePropertyChanged();
            }
        }
    }
}
