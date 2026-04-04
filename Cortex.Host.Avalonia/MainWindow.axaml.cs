using System;
using System.ComponentModel;
using Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;
using Cortex.Host.Avalonia.Services;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia
{
    public partial class MainWindow : Window
    {
        private DesktopWorkbenchDockFactory _dockFactory;
        private MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            Opened += MainWindow_OnOpened;
            DataContextChanged += MainWindow_DataContextChanged;
        }

        private MainWindowViewModel ViewModel
        {
            get { return DataContext as MainWindowViewModel; }
        }

        private void MainWindow_OnOpened(object sender, EventArgs e)
        {
            RebuildWorkbenchLayout();
        }

        private void MainWindow_DataContextChanged(object sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = ViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.ActiveWorkbenchLayoutPresetId), StringComparison.Ordinal))
            {
                RebuildWorkbenchLayout();
            }
        }

        public void RebuildWorkbenchLayout()
        {
            if (ViewModel == null)
            {
                return;
            }

            _dockFactory = new DesktopWorkbenchDockFactory(ViewModel, ViewModel.ActiveWorkbenchLayoutPresetId);
            IRootDock layout = _dockFactory.CreateLayout();
            _dockFactory.InitLayout(layout);
            WorkbenchDockControl.Factory = _dockFactory;
            WorkbenchDockControl.Layout = layout;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel = null;
            }

            base.OnClosed(e);
        }
    }
}
