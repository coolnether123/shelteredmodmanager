using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Dock.Model.Core;
using Dock.Model.Controls;
using Cortex.Host.Avalonia.Services;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia
{
    public partial class MainWindow : Window
    {
        private readonly DesktopWorkbenchCompositionService _workbenchCompositionService;
        private DesktopWorkbenchDockFactory _dockFactory;
        private DesktopShellViewModel _viewModel;
        private bool _synchronizingSurfaceState;

        public MainWindow()
            : this(null, null)
        {
        }

        internal MainWindow(DesktopShellViewModel viewModel, DesktopWorkbenchCompositionService workbenchCompositionService)
        {
            _viewModel = viewModel;
            _workbenchCompositionService = workbenchCompositionService;
            InitializeComponent();
            DataContext = viewModel;
            Opened += MainWindow_OnOpened;
            Closed += MainWindow_OnClosed;
            SubscribeToSurfaceToggles();
        }

        private void MainWindow_OnOpened(object sender, EventArgs e)
        {
            ApplyWorkbenchLayout();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(DesktopShellViewModel.ActiveWorkbenchLayoutPresetId), StringComparison.Ordinal) &&
                !_workbenchCompositionService.CurrentShellState.UseSavedLayout)
            {
                ApplyWorkbenchLayout();
            }
        }

        private DesktopShellViewModel ViewModel
        {
            get { return _viewModel; }
        }

        private MainWindowViewModel WorkbenchViewModel
        {
            get { return _viewModel != null ? _viewModel.Workbench : null; }
        }

        private void SubscribeToSurfaceToggles()
        {
            if (_viewModel == null)
            {
                return;
            }

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            foreach (var surfaceToggle in _viewModel.SurfaceToggles)
            {
                surfaceToggle.PropertyChanged += SurfaceToggle_PropertyChanged;
            }
        }

        private void UnsubscribeFromSurfaceToggles()
        {
            if (_viewModel == null)
            {
                return;
            }

            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            foreach (var surfaceToggle in _viewModel.SurfaceToggles)
            {
                surfaceToggle.PropertyChanged -= SurfaceToggle_PropertyChanged;
            }
        }

        private void SurfaceToggle_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_synchronizingSurfaceState || !string.Equals(e.PropertyName, nameof(DesktopWorkbenchSurfaceToggleViewModel.IsVisible), StringComparison.Ordinal))
            {
                return;
            }

            var surfaceToggle = sender as DesktopWorkbenchSurfaceToggleViewModel;
            if (surfaceToggle == null || WorkbenchViewModel == null)
            {
                return;
            }

            if (!_workbenchCompositionService.SetSurfaceVisibility(
                surfaceToggle.SurfaceId,
                surfaceToggle.IsVisible,
                WorkbenchViewModel.ActiveWorkbenchLayoutPresetId))
            {
                _synchronizingSurfaceState = true;
                surfaceToggle.IsVisible = true;
                _synchronizingSurfaceState = false;
                return;
            }

            RefreshShellState();
            ApplyWorkbenchLayout();
        }

        private void RefreshShellState()
        {
            if (_viewModel == null)
            {
                return;
            }

            _synchronizingSurfaceState = true;
            UnsubscribeFromSurfaceToggles();
            _viewModel.ApplyShellState(
                _workbenchCompositionService.CurrentShellState,
                _workbenchCompositionService.SurfaceRegistry.Definitions);
            SubscribeToSurfaceToggles();
            _synchronizingSurfaceState = false;
        }

        private void ApplyWorkbenchLayout()
        {
            if (ViewModel == null || WorkbenchViewModel == null)
            {
                return;
            }

            var composition = _workbenchCompositionService.ComposeLayout(
                ViewModel,
                WorkbenchViewModel.ActiveWorkbenchLayoutPresetId);
            RefreshShellState();
            _dockFactory = composition.Factory;
            WorkbenchDockControl.Factory = _dockFactory;
            WorkbenchDockControl.Layout = composition.RootDock;
        }

        private void SaveLayout_OnClick(object sender, RoutedEventArgs e)
        {
            var rootDock = WorkbenchDockControl.Layout as IRootDock;
            if (rootDock == null || WorkbenchViewModel == null)
            {
                return;
            }

            _workbenchCompositionService.SaveLayout(rootDock, WorkbenchViewModel.ActiveWorkbenchLayoutPresetId);
            RefreshShellState();
        }

        private void ResetLayout_OnClick(object sender, RoutedEventArgs e)
        {
            if (WorkbenchViewModel == null)
            {
                return;
            }

            _workbenchCompositionService.ResetLayout(WorkbenchViewModel.ActiveWorkbenchLayoutPresetId);
            ApplyWorkbenchLayout();
        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            var rootDock = WorkbenchDockControl.Layout as IRootDock;
            if (rootDock != null && WorkbenchViewModel != null && _workbenchCompositionService.CurrentShellState.UseSavedLayout)
            {
                _workbenchCompositionService.SaveLayout(rootDock, WorkbenchViewModel.ActiveWorkbenchLayoutPresetId);
            }

            UnsubscribeFromSurfaceToggles();
        }
    }
}
