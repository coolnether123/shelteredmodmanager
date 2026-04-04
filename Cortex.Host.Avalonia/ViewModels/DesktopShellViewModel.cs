using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Cortex.Host.Avalonia.Models;

namespace Cortex.Host.Avalonia.ViewModels
{
    internal sealed class DesktopShellViewModel : ViewModelBase
    {
        public DesktopShellViewModel(MainWindowViewModel workbench)
        {
            Workbench = workbench;
            SurfaceToggles = new ObservableCollection<DesktopWorkbenchSurfaceToggleViewModel>();
            if (Workbench != null)
            {
                Workbench.PropertyChanged += Workbench_PropertyChanged;
            }
        }

        public MainWindowViewModel Workbench { get; }
        public ObservableCollection<DesktopWorkbenchSurfaceToggleViewModel> SurfaceToggles { get; }

        public string LayoutModeSummary { get; private set; } = "Using runtime layout preset.";

        public string ShellStatusMessage
        {
            get { return LayoutModeSummary + " | " + (Workbench != null ? Workbench.StatusMessage : string.Empty); }
        }

        public string ActiveWorkbenchLayoutPresetId
        {
            get { return Workbench != null ? Workbench.ActiveWorkbenchLayoutPresetId : string.Empty; }
        }

        public void ApplyShellState(DesktopShellState shellState, IReadOnlyList<DesktopWorkbenchSurfaceDefinition> surfaces)
        {
            LayoutModeSummary = shellState != null && shellState.UseSavedLayout
                ? "Using saved Dock layout."
                : "Using runtime layout preset.";

            var surfaceStateLookup = new Dictionary<string, DesktopShellSurfaceState>(StringComparer.OrdinalIgnoreCase);
            if (shellState != null && shellState.SurfaceStates != null)
            {
                foreach (var surfaceState in shellState.SurfaceStates)
                {
                    if (surfaceState != null && !string.IsNullOrEmpty(surfaceState.SurfaceId))
                    {
                        surfaceStateLookup[surfaceState.SurfaceId] = surfaceState;
                    }
                }
            }

            SurfaceToggles.Clear();
            foreach (var surface in surfaces ?? Array.Empty<DesktopWorkbenchSurfaceDefinition>())
            {
                DesktopShellSurfaceState surfaceState;
                surfaceStateLookup.TryGetValue(surface.SurfaceId, out surfaceState);

                SurfaceToggles.Add(new DesktopWorkbenchSurfaceToggleViewModel
                {
                    SurfaceId = surface.SurfaceId,
                    Title = surface.Title,
                    CanHide = !surface.IsRequired,
                    IsVisible = surfaceState != null ? surfaceState.IsVisible : surface.DefaultVisible
                });
            }

            RaisePropertyChanged(nameof(LayoutModeSummary));
            RaisePropertyChanged(nameof(ShellStatusMessage));
        }

        private void Workbench_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.StatusMessage), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(MainWindowViewModel.ActiveWorkbenchLayoutPresetId), StringComparison.Ordinal))
            {
                RaisePropertyChanged(nameof(ShellStatusMessage));
                RaisePropertyChanged(nameof(ActiveWorkbenchLayoutPresetId));
            }
        }
    }
}
