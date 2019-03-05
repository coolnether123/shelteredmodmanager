using System;
using System.IO;
using System.Windows.Forms;

namespace Manager
{
    public partial class ManagerGUI : Form
    {
        private OpenFileDialog fileDialog;

        public ManagerGUI()
        {
            InitializeComponent();

            fileDialog = new OpenFileDialog();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            updateLaunchButton();
            // update path
            string contents = File.ReadAllText("mod_manager.ini");
            if (contents != null)
            {
                uiGamePath.Text = contents;
            }
            else {
                uiGamePath.Text = "None";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
           
            fileDialog.RestoreDirectory = true;
            fileDialog.Title = "Locate Sheltered.exe ...";
            fileDialog.DefaultExt = "exe";
            fileDialog.Filter = "exe files (*.exe)|*.exe|All files (*.*)|*.*";

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                updateLaunchButton();
            }
        }

        private void updateLaunchButton() {
            string sourceFile = fileDialog.FileName;
            uiGamePath.Text = sourceFile;
        }

        private void button2_Click(object sender, EventArgs e)
        {
        }

        private void uiGamePath_TextChanged(object sender, EventArgs e)
        {
            if (uiGamePath.Text.Length == 0) return;
            button2.Enabled = File.Exists(uiGamePath.Text);
            File.WriteAllText("mod_manager.ini", uiGamePath.Text);
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(doorstopLink.Text);

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(shelteredLink.Text);
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(uiGamePath.Text);

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
    }
}
