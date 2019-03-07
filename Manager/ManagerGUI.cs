using System;
using System.IO;
using System.Windows.Forms;

/**
 * Author: benjaminfoo
 * See: https://github.com/benjaminfoo/shelteredmodmanager
 * 
 * This class contains the userinterface for the mod-manager. 
 */
namespace Manager
{
    public partial class ManagerGUI : Form
    {
        public static string DEFAULT_VALUE = "None";
        public static string MOD_MANAGER_INI_FILE = "mod_manager.ini";

        // the value which this gui operate son
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

        private void updateAvailableMods()
        {
            if (uiModsPath.Text.ToString().Trim().ToLower().Equals(DEFAULT_VALUE.ToLower())) return;

            uiAvailbleModsListView.Items.Clear();
            var items = Directory.GetFiles(uiModsPath.Text, "*.dll");
            foreach (var item in items) {
                uiAvailbleModsListView.Items.Add(item);
            }

        }

        private void updateGamePath()
        {
            try
            {
                string contents = File.ReadAllText(MOD_MANAGER_INI_FILE);
                uiGamePath.Text = contents;
                // TODO: the following line is obviously a joke
                uiModsPath.Text = contents.Replace("\\Sheltered.exe", "") + "\\" + "mods\\";
            }
            catch {
                uiGamePath.Text = DEFAULT_VALUE;
            }


            uiLaunchButton.Enabled = File.Exists(uiGamePath.Text);
            uiOpenGameDir.Enabled = File.Exists(uiGamePath.Text);
        }

        private void onLocate(object sender, EventArgs e)
        {
           
            fileDialog.RestoreDirectory = true;
            fileDialog.Title = "Locate Sheltered.exe ...";
            fileDialog.DefaultExt = "exe";
            fileDialog.Filter = "exe files (*.exe)|*.exe|All files (*.*)|*.*";

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                uiGamePath.Text = fileDialog.FileName;
                uiModsPath.Text = fileDialog.FileName.Replace("\\Sheltered.exe", "") + "\\" + "manager_mods\\";
            }

        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(doorstopLink.Text);

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(shelteredLink.Text);
        }


        private void onLaunchClicked(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(uiGamePath.Text);

        }

        private void uiGamePath_TextChanged_1(object sender, EventArgs e)
        {
            if (uiGamePath.Text.Length == 0) return;
            if (uiGamePath.Text.ToString().Trim().ToLower().Equals(DEFAULT_VALUE.ToLower())) return;

            uiLaunchButton.Enabled = File.Exists(uiGamePath.Text);
            uiOpenGameDir.Enabled = File.Exists(uiGamePath.Text);
            File.WriteAllText(MOD_MANAGER_INI_FILE, uiGamePath.Text);
        }

        private void uiInstaledLabel_Click(object sender, EventArgs e)
        {

        }

        private void uiInstalledModsListView_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        private void uiOpenGameDir_Click(object sender, EventArgs e)
        {
            // start explorer, in game-directory
            System.Diagnostics.Process.Start(uiGamePath.Text.Replace("Sheltered.exe", ""));
        }

        private void linkLabel1_LinkClicked_1(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(harmonyLink.Text);
        }
    }
}
