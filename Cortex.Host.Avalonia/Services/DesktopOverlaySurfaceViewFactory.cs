using Avalonia.Controls;
using Cortex.Host.Avalonia.ViewModels;
using Cortex.Host.Avalonia.Views;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopOverlaySurfaceViewFactory
    {
        private readonly MainWindowViewModel _workbenchViewModel;
        private readonly DesktopShellViewModel _shellViewModel;

        public DesktopOverlaySurfaceViewFactory(MainWindowViewModel workbenchViewModel, DesktopShellViewModel shellViewModel)
        {
            _workbenchViewModel = workbenchViewModel;
            _shellViewModel = shellViewModel;
        }

        public Control Create(string contentViewId)
        {
            switch (contentViewId)
            {
                case DesktopWorkbenchSurfaceRegistry.OnboardingSurfaceId:
                    return new OnboardingToolView { DataContext = _workbenchViewModel };
                case DesktopWorkbenchSurfaceRegistry.WorkspaceSurfaceId:
                    return new WorkspaceDocumentView { DataContext = _workbenchViewModel };
                case DesktopWorkbenchSurfaceRegistry.EditorSurfaceId:
                    return new EditorDocumentView { DataContext = _workbenchViewModel };
                case DesktopWorkbenchSurfaceRegistry.SettingsSurfaceId:
                    return new SettingsToolView { DataContext = _workbenchViewModel };
                case DesktopWorkbenchSurfaceRegistry.ReferenceSurfaceId:
                    return new ReferenceToolView { DataContext = _workbenchViewModel };
                case DesktopWorkbenchSurfaceRegistry.SearchSurfaceId:
                    return new SearchToolView { DataContext = _workbenchViewModel };
                case DesktopWorkbenchSurfaceRegistry.StatusSurfaceId:
                default:
                    return new StatusToolView { DataContext = _shellViewModel };
            }
        }
    }
}
