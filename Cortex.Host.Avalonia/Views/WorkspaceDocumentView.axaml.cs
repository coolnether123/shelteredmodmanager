using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Cortex.Host.Avalonia.Models;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia.Views
{
    public partial class WorkspaceDocumentView : UserControl
    {
        public WorkspaceDocumentView()
        {
            InitializeComponent();
        }

        private MainWindowViewModel ViewModel
        {
            get { return DataContext as MainWindowViewModel; }
        }

        private async void BrowseWorkspaceRoot_OnClick(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || topLevel.StorageProvider == null || ViewModel == null)
            {
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Choose workspace root"
            });
            var folder = folders.FirstOrDefault();
            if (folder == null)
            {
                return;
            }

            var path = folder.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                WorkspaceRootTextBox.Text = path;
                ViewModel.SetWorkspaceRoot(path);
            }
        }

        private void AnalyzeWorkspace_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SetWorkspaceRoot(WorkspaceRootTextBox.Text ?? string.Empty);
                ViewModel.AnalyzeWorkspaceRoot();
            }
        }

        private void ImportWorkspace_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SetWorkspaceRoot(WorkspaceRootTextBox.Text ?? string.Empty);
                ViewModel.ImportWorkspaceProjects();
            }
        }

        private void WorkspaceTreeView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = WorkspaceTreeView.SelectedItem as WorkspaceFileNodeViewModel;
            if (ViewModel != null && selected != null && !selected.IsDirectory)
            {
                ViewModel.LoadFilePreview(selected.FullPath);
            }
        }

        private void RunSearch_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UpdateWorkbenchSearch();
            }
        }

        private void SearchResultsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = SearchResultsListBox.SelectedItem as SearchMatchItemViewModel;
            if (ViewModel != null && selected != null)
            {
                ViewModel.OpenSearchResult(selected);
            }
        }
    }
}
