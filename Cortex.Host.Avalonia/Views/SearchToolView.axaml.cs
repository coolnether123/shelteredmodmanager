using Avalonia.Controls;
using Avalonia.Interactivity;
using Cortex.Host.Avalonia.Models;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia.Views
{
    public partial class SearchToolView : UserControl
    {
        public SearchToolView()
        {
            InitializeComponent();
        }

        private MainWindowViewModel ViewModel
        {
            get { return DataContext as MainWindowViewModel; }
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
