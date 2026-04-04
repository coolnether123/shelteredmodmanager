using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia.Views
{
    public partial class OnboardingToolView : UserControl
    {
        public OnboardingToolView()
        {
            InitializeComponent();
        }

        private MainWindowViewModel ViewModel
        {
            get { return DataContext as MainWindowViewModel; }
        }

        private async void BrowseOnboardingWorkspaceRoot_OnClick(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || topLevel.StorageProvider == null || ViewModel == null)
            {
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Choose onboarding workspace root"
            });
            var folder = folders.FirstOrDefault();
            if (folder == null)
            {
                return;
            }

            var path = folder.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                OnboardingWorkspaceRootTextBox.Text = path;
                ViewModel.SetOnboardingWorkspaceRoot(path);
            }
        }

        private void ApplyOnboarding_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.SetOnboardingWorkspaceRoot(OnboardingWorkspaceRootTextBox.Text ?? string.Empty);
            ViewModel.ApplyOnboarding();
        }
    }
}
