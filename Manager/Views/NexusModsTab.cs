using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Manager.Core.Models;
using Manager.Core.Services;

namespace Manager.Views
{
    /// <summary>
    /// Nexus browsing and update visibility tab.
    /// </summary>
    public class NexusModsTab : UserControl
    {
        private Panel _topPanel;
        private Label _statusLabel;
        private Label _domainLabel;
        private TextBox _domainTextBox;
        private Button _refreshButton;
        private Button _checkManagerButton;
        private Label _managerUpdateLabel;
        private SplitContainer _split;
        private GroupBox _installedGroup;
        private GroupBox _latestGroup;
        private ListBox _installedList;
        private ListBox _latestList;
        private RichTextBox _detailsBox;
        private Button _openPageButton;
        private Label _installedSummaryLabel;

        private NexusModsService _nexusService;
        private AppSettings _settings;
        private string _managerVersion = string.Empty;
        private int _latestRequestToken = 0;
        private string _selectedUrl = string.Empty;

        private sealed class NexusListItem
        {
            public NexusRemoteMod Mod;
            public string Prefix;

            public override string ToString()
            {
                if (Mod == null) return Prefix ?? string.Empty;

                string version = string.IsNullOrEmpty(Mod.Version) ? "?" : Mod.Version;
                return (Prefix ?? string.Empty) + Mod.Name + "  (v" + version + ")";
            }
        }

        public NexusModsTab()
        {
            InitializeComponent();
            WireEvents();
        }

        public void Initialize(NexusModsService nexusService, AppSettings settings, string managerVersion)
        {
            _nexusService = nexusService;
            _settings = settings;
            _managerVersion = managerVersion ?? string.Empty;

            string domain = "sheltered";
            if (_settings != null && !string.IsNullOrEmpty(_settings.NexusGameDomain))
                domain = _settings.NexusGameDomain;

            _domainTextBox.Text = domain;
            _domainTextBox.Enabled = _settings == null || _settings.EnableNexusIntegration;
            _refreshButton.Enabled = _domainTextBox.Enabled;
            _checkManagerButton.Enabled = _domainTextBox.Enabled;
            _statusLabel.Text = _domainTextBox.Enabled
                ? "Nexus: Ready"
                : "Nexus: Disabled in Settings";
        }

        public void UpdateInstalledMods(List<ModItem> mods, int mappedMods, int updateCount, string errorMessage)
        {
            _installedList.BeginUpdate();
            _installedList.Items.Clear();

            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    if (!mod.HasUpdateAvailable) continue;

                    var remote = new NexusRemoteMod();
                    remote.GameDomain = mod.NexusGameDomain;
                    remote.ModId = mod.NexusModId;
                    remote.Name = mod.DisplayName;
                    remote.Version = mod.NexusRemoteVersion;

                    _installedList.Items.Add(new NexusListItem
                    {
                        Mod = remote,
                        Prefix = "Update: "
                    });
                }
            }

            _installedList.EndUpdate();

            if (!string.IsNullOrEmpty(errorMessage))
            {
                _installedSummaryLabel.Text = "Installed updates: check failed (" + errorMessage + ")";
            }
            else
            {
                _installedSummaryLabel.Text = "Installed updates: " + updateCount + " available across " + mappedMods + " linked mods";
            }
        }

        public void RefreshLatestModsAsync()
        {
            if (_settings != null && !_settings.EnableNexusIntegration)
            {
                _statusLabel.Text = "Nexus: Disabled in Settings";
                return;
            }

            if (_nexusService == null)
            {
                _statusLabel.Text = "Nexus: Service unavailable";
                return;
            }

            string domain = (_domainTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(domain))
            {
                _statusLabel.Text = "Nexus: Enter a game domain";
                return;
            }

            _statusLabel.Text = "Nexus: Loading latest mods...";
            _refreshButton.Enabled = false;
            int token = ++_latestRequestToken;

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                var latest = _nexusService.GetLatestMods(domain, 20, out error);

                if (IsDisposed || Disposing) return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (token != _latestRequestToken)
                            return;

                        _latestList.BeginUpdate();
                        _latestList.Items.Clear();
                        foreach (var mod in latest)
                        {
                            _latestList.Items.Add(new NexusListItem { Mod = mod });
                        }
                        _latestList.EndUpdate();

                        _refreshButton.Enabled = true;

                        if (!string.IsNullOrEmpty(error))
                        {
                            _statusLabel.Text = "Nexus: Load failed";
                            _detailsBox.Text = "Failed to load latest mods.\n\n" + error;
                        }
                        else
                        {
                            _statusLabel.Text = "Nexus: Loaded " + latest.Count + " latest mods for '" + domain + "'";
                            if (latest.Count == 0)
                                _detailsBox.Text = "No mods found for this domain.";
                        }
                    });
                }
                catch
                {
                    // UI gone; ignore.
                }
            });
        }

        public void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                BackColor = Color.FromArgb(52, 54, 58);
                _topPanel.BackColor = Color.FromArgb(42, 44, 48);
                _split.BackColor = BackColor;
                _split.Panel1.BackColor = BackColor;
                _split.Panel2.BackColor = BackColor;

                _statusLabel.ForeColor = Color.White;
                _domainLabel.ForeColor = Color.White;
                _managerUpdateLabel.ForeColor = Color.Gainsboro;
                _installedSummaryLabel.ForeColor = Color.Gainsboro;
                _installedSummaryLabel.BackColor = Color.FromArgb(48, 50, 55);

                _installedGroup.ForeColor = Color.White;
                _installedGroup.BackColor = Color.FromArgb(52, 54, 58);
                _latestGroup.ForeColor = Color.White;
                _latestGroup.BackColor = Color.FromArgb(52, 54, 58);

                _installedList.BackColor = Color.FromArgb(36, 38, 42);
                _installedList.ForeColor = Color.Gainsboro;
                _latestList.BackColor = Color.FromArgb(36, 38, 42);
                _latestList.ForeColor = Color.Gainsboro;
                _detailsBox.BackColor = Color.FromArgb(30, 32, 36);
                _detailsBox.ForeColor = Color.WhiteSmoke;
                _domainTextBox.BackColor = Color.FromArgb(36, 38, 42);
                _domainTextBox.ForeColor = Color.White;

                ApplyButtonTheme(_refreshButton, true, true);
                ApplyButtonTheme(_checkManagerButton, true, true);
                ApplyButtonTheme(_openPageButton, true, false);
            }
            else
            {
                BackColor = SystemColors.Control;
                _topPanel.BackColor = SystemColors.ControlLight;
                _split.BackColor = SystemColors.Control;
                _split.Panel1.BackColor = SystemColors.Control;
                _split.Panel2.BackColor = SystemColors.Control;

                _statusLabel.ForeColor = SystemColors.ControlText;
                _domainLabel.ForeColor = SystemColors.ControlText;
                _managerUpdateLabel.ForeColor = SystemColors.ControlText;
                _installedSummaryLabel.ForeColor = SystemColors.ControlText;
                _installedSummaryLabel.BackColor = SystemColors.Control;
                _installedGroup.ForeColor = SystemColors.ControlText;
                _installedGroup.BackColor = SystemColors.Control;
                _latestGroup.ForeColor = SystemColors.ControlText;
                _latestGroup.BackColor = SystemColors.Control;
                _installedList.BackColor = SystemColors.Window;
                _installedList.ForeColor = SystemColors.WindowText;
                _latestList.BackColor = SystemColors.Window;
                _latestList.ForeColor = SystemColors.WindowText;
                _detailsBox.BackColor = SystemColors.Window;
                _detailsBox.ForeColor = SystemColors.WindowText;
                _domainTextBox.BackColor = SystemColors.Window;
                _domainTextBox.ForeColor = SystemColors.WindowText;

                ApplyButtonTheme(_refreshButton, false, true);
                ApplyButtonTheme(_checkManagerButton, false, true);
                ApplyButtonTheme(_openPageButton, false, false);
            }
        }

        private static void ApplyButtonTheme(Button button, bool isDark, bool primary)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;

            if (isDark)
            {
                if (primary)
                {
                    button.BackColor = Color.FromArgb(0, 122, 204);
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Color.FromArgb(0, 92, 164);
                }
                else
                {
                    button.BackColor = Color.FromArgb(68, 72, 78);
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Color.FromArgb(90, 95, 102);
                }
            }
            else
            {
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatAppearance.BorderColor = SystemColors.ControlDark;
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            _topPanel = new Panel();
            _statusLabel = new Label();
            _domainLabel = new Label();
            _domainTextBox = new TextBox();
            _refreshButton = new Button();
            _checkManagerButton = new Button();
            _managerUpdateLabel = new Label();
            _split = new SplitContainer();
            _installedGroup = new GroupBox();
            _latestGroup = new GroupBox();
            _installedList = new ListBox();
            _latestList = new ListBox();
            _detailsBox = new RichTextBox();
            _openPageButton = new Button();
            _installedSummaryLabel = new Label();

            _topPanel.Dock = DockStyle.Top;
            _topPanel.Height = 95;
            _topPanel.Padding = new Padding(12, 10, 12, 10);

            _statusLabel.AutoSize = true;
            _statusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _statusLabel.Location = new Point(12, 10);
            _statusLabel.Text = "Nexus: Ready";

            _domainLabel.AutoSize = true;
            _domainLabel.Font = new Font("Segoe UI", 9f);
            _domainLabel.Location = new Point(12, 36);
            _domainLabel.Text = "Game Domain:";

            _domainTextBox.Font = new Font("Segoe UI", 9f);
            _domainTextBox.Location = new Point(110, 33);
            _domainTextBox.Width = 180;

            _refreshButton.Text = "Refresh Latest";
            _refreshButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _refreshButton.Location = new Point(305, 31);
            _refreshButton.Size = new Size(120, 28);

            _checkManagerButton.Text = "Check Manager";
            _checkManagerButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _checkManagerButton.Location = new Point(435, 31);
            _checkManagerButton.Size = new Size(125, 28);

            _managerUpdateLabel.AutoSize = true;
            _managerUpdateLabel.Font = new Font("Segoe UI", 9f);
            _managerUpdateLabel.Location = new Point(12, 64);
            _managerUpdateLabel.Text = "Manager update: not checked";

            _topPanel.Controls.Add(_statusLabel);
            _topPanel.Controls.Add(_domainLabel);
            _topPanel.Controls.Add(_domainTextBox);
            _topPanel.Controls.Add(_refreshButton);
            _topPanel.Controls.Add(_checkManagerButton);
            _topPanel.Controls.Add(_managerUpdateLabel);

            _split.Dock = DockStyle.Fill;
            _split.FixedPanel = FixedPanel.Panel1;
            _split.SplitterDistance = 390;

            _installedGroup.Dock = DockStyle.Top;
            _installedGroup.Height = 200;
            _installedGroup.Text = "Installed Mod Updates";
            _installedGroup.Padding = new Padding(8);

            _installedSummaryLabel.Dock = DockStyle.Top;
            _installedSummaryLabel.Height = 34;
            _installedSummaryLabel.Text = "Installed updates: waiting for mod scan";

            _installedList.Dock = DockStyle.Fill;
            _installedList.Font = new Font("Segoe UI", 9f);

            _installedGroup.Controls.Add(_installedList);
            _installedGroup.Controls.Add(_installedSummaryLabel);

            _latestGroup.Dock = DockStyle.Fill;
            _latestGroup.Text = "Latest Releases";
            _latestGroup.Padding = new Padding(8);

            _latestList.Dock = DockStyle.Fill;
            _latestList.Font = new Font("Segoe UI", 9f);
            _latestGroup.Controls.Add(_latestList);

            _split.Panel1.Controls.Add(_latestGroup);
            _split.Panel1.Controls.Add(_installedGroup);

            _openPageButton.Text = "Open Selected Mod Page";
            _openPageButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _openPageButton.Dock = DockStyle.Top;
            _openPageButton.Height = 34;

            _detailsBox.Dock = DockStyle.Fill;
            _detailsBox.Font = new Font("Segoe UI", 9f);
            _detailsBox.ReadOnly = true;
            _detailsBox.WordWrap = true;
            _detailsBox.Text = "Select a mod in either list to view details.";

            _split.Panel2.Controls.Add(_detailsBox);
            _split.Panel2.Controls.Add(_openPageButton);

            Controls.Add(_split);
            Controls.Add(_topPanel);
            Name = "NexusModsTab";
            Padding = new Padding(12);

            ResumeLayout(false);
        }

        private void WireEvents()
        {
            _refreshButton.Click += (s, e) => RefreshLatestModsAsync();
            _openPageButton.Click += OpenPageButton_Click;
            _latestList.SelectedIndexChanged += LatestList_SelectedIndexChanged;
            _installedList.SelectedIndexChanged += InstalledList_SelectedIndexChanged;
            _checkManagerButton.Click += CheckManagerButton_Click;
        }

        private void LatestList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = _latestList.SelectedItem as NexusListItem;
            if (item == null || item.Mod == null)
                return;

            ShowModDetails(item.Mod, "Latest release");
            _selectedUrl = item.Mod.GetPageUrl();
            _installedList.ClearSelected();
        }

        private void InstalledList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = _installedList.SelectedItem as NexusListItem;
            if (item == null || item.Mod == null)
                return;

            ShowModDetails(item.Mod, "Installed mod update");
            _selectedUrl = item.Mod.GetPageUrl();
            _latestList.ClearSelected();
        }

        private void ShowModDetails(NexusRemoteMod mod, string heading)
        {
            if (mod == null)
                return;

            var lines = new List<string>();
            lines.Add(heading);
            lines.Add(string.Empty);
            lines.Add("Name: " + (mod.Name ?? "Unknown"));
            lines.Add("Version: " + (mod.Version ?? "Unknown"));
            lines.Add("Game: " + (mod.GameDomain ?? "Unknown"));
            lines.Add("Mod ID: " + mod.ModId);
            lines.Add("Updated: " + (mod.UpdatedAtUtc.HasValue ? mod.UpdatedAtUtc.Value.ToString("u") : "Unknown"));
            lines.Add("Downloads: " + mod.Downloads);
            lines.Add("Endorsements: " + mod.Endorsements);
            lines.Add("Page: " + mod.GetPageUrl());
            lines.Add(string.Empty);
            lines.Add("Summary:");
            lines.Add(string.IsNullOrEmpty(mod.Summary) ? "No summary provided." : mod.Summary);

            _detailsBox.Text = string.Join(Environment.NewLine, lines.ToArray());
        }

        private void OpenPageButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedUrl))
                return;

            try
            {
                System.Diagnostics.Process.Start(_selectedUrl);
            }
            catch
            {
                // ignore
            }
        }

        private void CheckManagerButton_Click(object sender, EventArgs e)
        {
            if (_settings == null || _nexusService == null)
                return;
            if (!_settings.EnableNexusIntegration)
            {
                _managerUpdateLabel.Text = "Manager update: Nexus integration is disabled.";
                return;
            }

            if (_settings.ManagerNexusModId <= 0)
            {
                _managerUpdateLabel.Text = "Manager update: set Manager Mod ID in Settings.";
                return;
            }

            string domain = (_domainTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(domain))
            {
                _managerUpdateLabel.Text = "Manager update: game domain is required.";
                return;
            }

            _managerUpdateLabel.Text = "Manager update: checking...";
            _checkManagerButton.Enabled = false;

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                var remote = _nexusService.GetModByDomainAndId(domain, _settings.ManagerNexusModId, out error);

                if (IsDisposed || Disposing) return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _checkManagerButton.Enabled = true;

                        if (!string.IsNullOrEmpty(error))
                        {
                            _managerUpdateLabel.Text = "Manager update: check failed (" + error + ")";
                            return;
                        }

                        if (remote == null)
                        {
                            _managerUpdateLabel.Text = "Manager update: no Nexus entry found.";
                            return;
                        }

                        bool updateAvailable = NexusVersionComparer.IsRemoteNewer(_managerVersion, remote.Version);
                        if (updateAvailable)
                        {
                            _managerUpdateLabel.Text = "Manager update available: v" + remote.Version + " (current: v" + _managerVersion + ")";
                        }
                        else
                        {
                            _managerUpdateLabel.Text = "Manager is up to date (current: v" + _managerVersion + ")";
                        }
                    });
                }
                catch
                {
                    // UI already gone; ignore.
                }
            });
        }
    }
}
