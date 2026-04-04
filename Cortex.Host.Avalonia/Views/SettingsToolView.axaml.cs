using Avalonia.Controls;
using Avalonia.Interactivity;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia.Views
{
    public partial class SettingsToolView : UserControl
    {
        public SettingsToolView()
        {
            InitializeComponent();
        }

        private MainWindowViewModel ViewModel
        {
            get { return DataContext as MainWindowViewModel; }
        }

        private void SettingValueTextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedSettingValue = SettingValueTextBox.Text ?? string.Empty;
                ViewModel.CommitSelectedSettingValue();
            }
        }

        private void SaveSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.SaveSettings();
        }
    }
}
