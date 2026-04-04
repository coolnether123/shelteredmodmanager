using Avalonia.Controls;
using Cortex.Host.Avalonia.ViewModels;
using Cortex.Shell.Shared.Models;

namespace Cortex.Host.Avalonia.Views
{
    public partial class EditorDocumentView : UserControl
    {
        public EditorDocumentView()
        {
            InitializeComponent();
        }

        private MainWindowViewModel ViewModel
        {
            get { return DataContext as MainWindowViewModel; }
        }

        private void OpenDocumentsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = OpenDocumentsListBox.SelectedItem as EditorDocumentSummaryModel;
            if (ViewModel != null && selected != null)
            {
                ViewModel.LoadFilePreview(selected.FilePath);
            }
        }
    }
}
