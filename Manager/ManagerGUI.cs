using System;
using System.IO;
using System.Windows.Forms;

/**
 * Author: benjaminfoo
 * See: https://github.com/benjaminfoo/shelteredmodmanager
 * 
 * This class contains the user interface for the mod-manager. 
 */
namespace Manager
{
    public partial class ManagerGUI : Form
    {
        public static string DEFAULT_VALUE = "None";
        public static string MOD_MANAGER_INI_FILE = "mod_manager.ini";

        // the value which this gui operates on
        public string currentGameDirectoryPath = DEFAULT_VALUE;

        private OpenFileDialog fileDialog = new OpenFileDialog();

        public ManagerGUI()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            updateGamePath();
            updateAvailableMods();
        }

        /// <summary>
        /// Paths for enabled/disabled mods
        /// </summary>
        private string EnabledModsPath => Path.Combine(uiModsPath.Text, "enabled");
        private string DisabledModsPath => Path.Combine(uiModsPath.Text, "disabled");

        /// <summary>
        /// Refreshes the available and installed mods lists
        /// </summary>
        private void updateAvailableMods()
        {
            if (uiModsPath.Text.ToString().Trim().ToLower().Equals(DEFAULT_VALUE.ToLower())) return;

            // Ensure directories exist
            Directory.CreateDirectory(EnabledModsPath);
            Directory.CreateDirectory(DisabledModsPath);

            uiAvailbleModsListView.Items.Clear();
            uiInstalledModsListView.Items.Clear();

            // Populate Available Mods (from disabled folder)
            var disabledItems = Directory.GetFiles(DisabledModsPath, "*.dll");
            foreach (var item in disabledItems)
            {
                uiAvailbleModsListView.Items.Add(Path.GetFileName(item));
            }

            // Populate Installed Mods (from enabled folder)
            var enabledItems = Directory.GetFiles(EnabledModsPath, "*.dll");
            foreach (var item in enabledItems)
            {
                uiInstalledModsListView.Items.Add(Path.GetFileName(item));
            }
        }

        /// <summary>
        /// Loads the game path from the ini file
        /// </summary>
        private void updateGamePath()
        {
            try
            {
                string contents = File.ReadAllText(MOD_MANAGER_INI_FILE);
                uiGamePath.Text = contents;
                uiModsPath.Text = Path.Combine(Path.GetDirectoryName(contents), "mods");
            }
            catch
            {
                uiGamePath.Text = DEFAULT_VALUE;
            }

            uiLaunchButton.Enabled = File.Exists(uiGamePath.Text);
            uiOpenGameDir.Enabled = File.Exists(uiGamePath.Text);
        }

        /// <summary>
        /// Locate the game exe
        /// </summary>
        private void onLocate(object sender, EventArgs e)
        {
            fileDialog.RestoreDirectory = true;
            fileDialog.Title = "Locate Sheltered.exe ...";
            fileDialog.DefaultExt = "exe";
            fileDialog.Filter = "exe files (*.exe)|*.exe|All files (*.*)|*.*";

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                uiGamePath.Text = fileDialog.FileName;
                uiModsPath.Text = Path.Combine(Path.GetDirectoryName(fileDialog.FileName), "manager_mods");
            }
        }

        /// <summary>
        /// Open UnityDoorstop link
        /// </summary>
        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(doorstopLink.Text);
        }

        /// <summary>
        /// Open Sheltered Steam link
        /// </summary>
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(shelteredLink.Text);
        }

        /// <summary>
        /// Open Harmony link
        /// </summary>
        private void linkLabel1_LinkClicked_1(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(harmonyLink.Text);
        }

        /// <summary>
        /// Launch the game
        /// </summary>
        private void onLaunchClicked(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(uiGamePath.Text);
        }

        /// <summary>
        /// When the game path text changes, update ini and buttons
        /// </summary>
        private void uiGamePath_TextChanged_1(object sender, EventArgs e)
        {
            if (uiGamePath.Text.Length == 0) return;
            if (uiGamePath.Text.ToString().Trim().ToLower().Equals(DEFAULT_VALUE.ToLower())) return;

            uiLaunchButton.Enabled = File.Exists(uiGamePath.Text);
            uiOpenGameDir.Enabled = File.Exists(uiGamePath.Text);
            File.WriteAllText(MOD_MANAGER_INI_FILE, uiGamePath.Text);
        }

        private void uiInstaledLabel_Click(object sender, EventArgs e) { }

        private void uiInstalledModsListView_SelectedIndexChanged(object sender, EventArgs e) { }

        private void tabPage1_Click(object sender, EventArgs e) { }

        /// <summary>
        /// Enable mod (move from disabled to enabled)
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            if (uiAvailbleModsListView.SelectedItem == null) return;

            string modFileName = uiAvailbleModsListView.SelectedItem.ToString();
            string sourcePath = Path.Combine(DisabledModsPath, modFileName);
            string destinationPath = Path.Combine(EnabledModsPath, modFileName);

            try
            {
                File.Move(sourcePath, destinationPath);
                updateAvailableMods(); // Refresh lists
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error enabling mod: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Disable mod (move from enabled to disabled)
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            if (uiInstalledModsListView.SelectedItem == null) return;

            string modFileName = uiInstalledModsListView.SelectedItem.ToString();
            string sourcePath = Path.Combine(EnabledModsPath, modFileName);
            string destinationPath = Path.Combine(DisabledModsPath, modFileName);

            try
            {
                File.Move(sourcePath, destinationPath);
                updateAvailableMods(); // Refresh lists
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error disabling mod: {ex.Message}",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Double-click available mod to enable
        /// </summary>
        private void uiAvailbleModsListView_DoubleClick(object sender, EventArgs e)
        {
            button1_Click(sender, e);
        }

        /// <summary>
        /// Double-click installed mod to disable
        /// </summary>
        private void uiInstalledModsListView_DoubleClick(object sender, EventArgs e)
        {
            button2_Click(sender, e);
        }

        /// <summary>
        /// Open game directory in Explorer
        /// </summary>
        private void uiOpenGameDir_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Path.GetDirectoryName(uiGamePath.Text));
        }
    }
}