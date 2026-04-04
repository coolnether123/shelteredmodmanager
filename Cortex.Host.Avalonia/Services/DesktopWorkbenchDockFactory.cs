using Dock.Model.Core;
using Dock.Model.Controls;
using Dock.Model.Mvvm;
using Cortex.Host.Avalonia.ViewModels;
using Cortex.Host.Avalonia.Views;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopWorkbenchDockFactory : Factory
    {
        private const string FocusLayoutId = "cortex.onboarding.layout.focus";
        private readonly MainWindowViewModel _viewModel;
        private readonly string _layoutPresetId;

        public DesktopWorkbenchDockFactory(MainWindowViewModel viewModel, string layoutPresetId)
        {
            _viewModel = viewModel;
            _layoutPresetId = layoutPresetId ?? string.Empty;
        }

        public override IRootDock CreateLayout()
        {
            var onboardingTool = CreateTool();
            onboardingTool.Id = "cortex.tool.onboarding";
            onboardingTool.Title = "Onboarding";
            onboardingTool.CanClose = false;
            onboardingTool.CanFloat = false;
            onboardingTool.CanPin = false;
            onboardingTool.Context = new OnboardingToolView { DataContext = _viewModel };

            var workspaceDocument = CreateDocument();
            workspaceDocument.Id = "cortex.document.workspace";
            workspaceDocument.Title = "Workspace";
            workspaceDocument.CanClose = false;
            workspaceDocument.CanFloat = false;
            workspaceDocument.CanPin = false;
            workspaceDocument.Context = new WorkspaceDocumentView { DataContext = _viewModel };

            var settingsTool = CreateTool();
            settingsTool.Id = "cortex.tool.settings";
            settingsTool.Title = "Settings";
            settingsTool.CanClose = false;
            settingsTool.CanFloat = false;
            settingsTool.CanPin = false;
            settingsTool.Context = new SettingsToolView { DataContext = _viewModel };

            var leftProportion = IsFocusLayout ? 0.20 : 0.24;
            var centerProportion = IsFocusLayout ? 0.58 : 0.46;
            var rightProportion = 1.0 - leftProportion - centerProportion;

            var onboardingDock = CreateToolDock();
            onboardingDock.Id = "cortex.dock.onboarding";
            onboardingDock.Title = "Onboarding";
            onboardingDock.Proportion = leftProportion;
            onboardingDock.Alignment = Alignment.Left;
            onboardingDock.VisibleDockables = CreateList<IDockable>(onboardingTool);
            onboardingDock.ActiveDockable = onboardingTool;
            onboardingDock.DefaultDockable = onboardingTool;

            var workspaceDock = CreateDocumentDock();
            workspaceDock.Id = "cortex.dock.workspace";
            workspaceDock.Title = "Workspace";
            workspaceDock.Proportion = centerProportion;
            workspaceDock.CanCreateDocument = false;
            workspaceDock.CanCloseLastDockable = false;
            workspaceDock.VisibleDockables = CreateList<IDockable>(workspaceDocument);
            workspaceDock.ActiveDockable = workspaceDocument;
            workspaceDock.DefaultDockable = workspaceDocument;

            var settingsDock = CreateToolDock();
            settingsDock.Id = "cortex.dock.settings";
            settingsDock.Title = "Settings";
            settingsDock.Proportion = rightProportion;
            settingsDock.Alignment = Alignment.Right;
            settingsDock.VisibleDockables = CreateList<IDockable>(settingsTool);
            settingsDock.ActiveDockable = settingsTool;
            settingsDock.DefaultDockable = settingsTool;

            var workbenchDock = CreateProportionalDock();
            workbenchDock.Id = "cortex.dock.workbench";
            workbenchDock.Title = "Workbench";
            workbenchDock.Orientation = Orientation.Horizontal;
            workbenchDock.VisibleDockables = CreateList<IDockable>(onboardingDock, workspaceDock, settingsDock);
            workbenchDock.ActiveDockable = workspaceDock;
            workbenchDock.DefaultDockable = workspaceDock;

            var root = CreateRootDock();
            root.Id = "cortex.root";
            root.Title = "Cortex Desktop Workbench";
            root.VisibleDockables = CreateList<IDockable>(workbenchDock);
            root.ActiveDockable = workbenchDock;
            root.DefaultDockable = workbenchDock;
            return root;
        }

        private bool IsFocusLayout
        {
            get { return string.Equals(_layoutPresetId, FocusLayoutId, System.StringComparison.OrdinalIgnoreCase); }
        }
    }
}
