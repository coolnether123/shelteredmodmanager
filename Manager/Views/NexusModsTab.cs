using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Manager.Controls;
using Manager.Core.Models;
using Manager.Core.Services;

namespace Manager.Views
{
    public delegate void NexusInstallCompletedHandler();

    /// <summary>
    /// Nexus browsing, update status, and install workflow tab.
    /// </summary>
    public class NexusModsTab : UserControl
    {
        private Panel _topPanel;
        private Label _statusLabel;
        private Label _managerUpdateLabel;
        private Label _installedSummaryLabel;

        private Panel _installedPanel;
        private Label _installedHeaderLabel;
        private ListBox _installedList;

        private Panel _actionsPanel;
        private Button _refreshButton;
        private Button _checkManagerButton;
        private Button _installSelectedButton;
        private Button _openPageButton;

        private Panel _latestPanel;
        private Label _latestHeaderLabel;
        private ListBox _latestList;

        private ModDetailsPanel _detailsPanel;

        private NexusModsService _nexusService;
        private NexusInstallService _installService = new NexusInstallService();
        private AppSettings _settings;
        private string _managerVersion = string.Empty;
        private int _latestRequestToken = 0;
        private DateTime _lastCheckedUtc = DateTime.MinValue;

        public event NexusInstallCompletedHandler InstallCompleted;

        private sealed class NexusListItem
        {
            public NexusRemoteMod RemoteMod;
            public ModItem LocalMod;
            public bool IsInstalledRow;

            public override string ToString()
            {
                if (IsInstalledRow)
                {
                    if (LocalMod == null) return "Unknown";

                    if (!LocalMod.HasNexusReference)
                        return "[UNLINKED] " + LocalMod.DisplayName;

                    if (LocalMod.HasUpdateAvailable)
                        return "[UPDATE] " + LocalMod.DisplayName + "  (" + (LocalMod.Version ?? "?") + " -> " + (LocalMod.NexusRemoteVersion ?? "?") + ")";

                    return "[OK] " + LocalMod.DisplayName + "  (" + (LocalMod.Version ?? "?") + ")";
                }

                if (RemoteMod == null) return "Unknown";
                string version = (RemoteMod.Version ?? "?").Trim();
                if (!version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    version = "v" + version;
                return RemoteMod.Name + "  (" + version + ")";
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
            _detailsPanel.InstalledModApiVersion = _settings != null ? _settings.InstalledModApiVersion : null;

            bool enabled = _settings == null || _settings.EnableNexusIntegration;
            _refreshButton.Enabled = enabled;
            _checkManagerButton.Enabled = enabled;
            _installSelectedButton.Enabled = enabled;

            if (!enabled)
            {
                _statusLabel.Text = "Nexus: Disabled in Settings";
            }
            else
            {
                string domain = GetGameDomain();
                _statusLabel.Text = "Nexus: Ready (" + domain + ")";
            }

            AdjustListHeights();
        }

        public void UpdateInstalledMods(List<ModItem> mods, int mappedMods, int updateCount, string errorMessage)
        {
            _installedList.BeginUpdate();
            _installedList.Items.Clear();

            var source = mods ?? new List<ModItem>();
            source.Sort(delegate (ModItem a, ModItem b)
            {
                int aScore = GetInstalledSortScore(a);
                int bScore = GetInstalledSortScore(b);
                if (aScore != bScore) return aScore.CompareTo(bScore);
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var mod in source)
            {
                _installedList.Items.Add(new NexusListItem
                {
                    IsInstalledRow = true,
                    LocalMod = mod,
                    RemoteMod = BuildRemoteFromLocal(mod)
                });
            }

            _installedList.EndUpdate();
            AdjustListHeights();

            int totalMods = source.Count;
            if (!string.IsNullOrEmpty(errorMessage))
                _installedSummaryLabel.Text = "Installed: link check failed (" + errorMessage + ")" + BuildLastCheckedSuffix();
            else
                _installedSummaryLabel.Text = "Installed: " + totalMods + " mods, " + mappedMods + " linked, " + updateCount + " updates" + BuildLastCheckedSuffix();
        }

        public void SetLastCheckedUtc(DateTime checkedUtc)
        {
            if (checkedUtc <= DateTime.MinValue)
                return;

            _lastCheckedUtc = checkedUtc;
            // Preserve current summary text, append/update timestamp on next update pass.
            if (string.IsNullOrEmpty(_installedSummaryLabel.Text))
                _installedSummaryLabel.Text = "Installed: waiting for mod scan" + BuildLastCheckedSuffix();
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

            string domain = GetGameDomain();
            if (string.IsNullOrEmpty(domain))
            {
                _statusLabel.Text = "Nexus: Game domain not configured";
                return;
            }

            _statusLabel.Text = "Nexus: Loading latest releases...";
            _refreshButton.Enabled = false;
            int token = ++_latestRequestToken;

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                var latest = _nexusService.GetLatestMods(domain, 50, out error);

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
                            _latestList.Items.Add(new NexusListItem
                            {
                                IsInstalledRow = false,
                                RemoteMod = mod
                            });
                        }
                        _latestList.EndUpdate();
                        AdjustListHeights();

                        _refreshButton.Enabled = true;

                        if (!string.IsNullOrEmpty(error))
                        {
                            _statusLabel.Text = "Nexus: Load failed";
                            _detailsPanel.ShowMod(CreateStatusMod("Nexus Load Failed", error));
                        }
                        else
                        {
                            _statusLabel.Text = "Nexus: Loaded " + latest.Count + " latest mods (" + domain + ")";
                            if (latest.Count == 0)
                                _detailsPanel.ShowMod(CreateStatusMod("No Nexus Results", "No mods were returned for this game domain."));
                        }
                    });
                }
                catch { }
            });
        }

        public void CheckManagerUpdateAsync(bool userInitiated)
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

            string domain = GetGameDomain();
            if (string.IsNullOrEmpty(domain))
            {
                _managerUpdateLabel.Text = "Manager update: game domain is missing.";
                return;
            }

            _managerUpdateLabel.Text = userInitiated
                ? "Manager update: checking..."
                : "Manager update: auto-checking...";
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
                            _managerUpdateLabel.Text = "Manager update: Nexus entry not found.";
                            return;
                        }

                        int comparison = NexusVersionComparer.CompareVersions(_managerVersion, remote.Version);
                        if (comparison < 0)
                        {
                            _managerUpdateLabel.Text = "Manager update available: v" + remote.Version + " (current: v" + _managerVersion + ")";
                        }
                        else if (comparison == 0)
                        {
                            _managerUpdateLabel.Text = "Manager is up to date (v" + _managerVersion + ")";
                        }
                        else
                        {
                            _managerUpdateLabel.Text = "Local build v" + _managerVersion + " is newer than Nexus v" + remote.Version;
                        }
                    });
                }
                catch { }
            });
        }

        public void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                BackColor = Color.FromArgb(46, 48, 53);
                _topPanel.BackColor = Color.FromArgb(40, 42, 46);
                _installedPanel.BackColor = Color.FromArgb(46, 48, 53);
                _actionsPanel.BackColor = Color.FromArgb(46, 48, 53);
                _latestPanel.BackColor = Color.FromArgb(46, 48, 53);

                _statusLabel.ForeColor = Color.White;
                _managerUpdateLabel.ForeColor = Color.Gainsboro;
                _installedSummaryLabel.ForeColor = Color.Gainsboro;
                _installedHeaderLabel.ForeColor = Color.White;
                _latestHeaderLabel.ForeColor = Color.White;

                _installedList.BackColor = Color.FromArgb(32, 34, 38);
                _installedList.ForeColor = Color.WhiteSmoke;
                _latestList.BackColor = Color.FromArgb(32, 34, 38);
                _latestList.ForeColor = Color.WhiteSmoke;

                ApplyButtonTheme(_refreshButton, true, true);
                ApplyButtonTheme(_checkManagerButton, true, true);
                ApplyButtonTheme(_installSelectedButton, true, true);
                ApplyButtonTheme(_openPageButton, true, false);
            }
            else
            {
                BackColor = SystemColors.Control;
                _topPanel.BackColor = SystemColors.ControlLight;
                _installedPanel.BackColor = SystemColors.Control;
                _actionsPanel.BackColor = SystemColors.Control;
                _latestPanel.BackColor = SystemColors.Control;

                _statusLabel.ForeColor = SystemColors.ControlText;
                _managerUpdateLabel.ForeColor = SystemColors.ControlText;
                _installedSummaryLabel.ForeColor = SystemColors.ControlText;
                _installedHeaderLabel.ForeColor = SystemColors.ControlText;
                _latestHeaderLabel.ForeColor = SystemColors.ControlText;

                _installedList.BackColor = SystemColors.Window;
                _installedList.ForeColor = SystemColors.WindowText;
                _latestList.BackColor = SystemColors.Window;
                _latestList.ForeColor = SystemColors.WindowText;

                ApplyButtonTheme(_refreshButton, false, true);
                ApplyButtonTheme(_checkManagerButton, false, true);
                ApplyButtonTheme(_installSelectedButton, false, true);
                ApplyButtonTheme(_openPageButton, false, false);
            }

            _detailsPanel.ApplyTheme(isDark);
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            _topPanel = new Panel();
            _statusLabel = new Label();
            _managerUpdateLabel = new Label();
            _installedSummaryLabel = new Label();

            _installedPanel = new Panel();
            _installedHeaderLabel = new Label();
            _installedList = new ListBox();

            _actionsPanel = new Panel();
            _refreshButton = new Button();
            _checkManagerButton = new Button();
            _installSelectedButton = new Button();
            _openPageButton = new Button();

            _latestPanel = new Panel();
            _latestHeaderLabel = new Label();
            _latestList = new ListBox();

            _detailsPanel = new ModDetailsPanel();

            _topPanel.Dock = DockStyle.Top;
            _topPanel.Height = 80;
            _topPanel.Padding = new Padding(12, 8, 12, 8);

            _statusLabel.AutoSize = true;
            _statusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _statusLabel.Location = new Point(12, 10);
            _statusLabel.Text = "Nexus: Ready";

            _managerUpdateLabel.AutoSize = true;
            _managerUpdateLabel.Font = new Font("Segoe UI", 9f);
            _managerUpdateLabel.Location = new Point(12, 31);
            _managerUpdateLabel.Text = "Manager update: not checked";

            _installedSummaryLabel.AutoSize = true;
            _installedSummaryLabel.Font = new Font("Segoe UI", 9f);
            _installedSummaryLabel.Location = new Point(12, 52);
            _installedSummaryLabel.Text = "Installed: waiting for mod scan";

            _topPanel.Controls.Add(_statusLabel);
            _topPanel.Controls.Add(_managerUpdateLabel);
            _topPanel.Controls.Add(_installedSummaryLabel);

            _installedPanel.Dock = DockStyle.Left;
            _installedPanel.Width = 320;
            _installedPanel.Padding = new Padding(12, 10, 8, 12);

            _installedHeaderLabel.Dock = DockStyle.Top;
            _installedHeaderLabel.Height = 28;
            _installedHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            _installedHeaderLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _installedHeaderLabel.Text = "Installed Mods";

            _installedList.Dock = DockStyle.Top;
            _installedList.Font = new Font("Segoe UI", 9f);
            _installedList.BorderStyle = BorderStyle.FixedSingle;
            _installedList.Height = 320;
            _installedList.IntegralHeight = false;

            _installedPanel.Controls.Add(_installedList);
            _installedPanel.Controls.Add(_installedHeaderLabel);

            _actionsPanel.Dock = DockStyle.Left;
            _actionsPanel.Width = 170;
            _actionsPanel.Padding = new Padding(10, 16, 10, 16);

            _refreshButton.Text = "Refresh Feed";
            _refreshButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _refreshButton.Size = new Size(145, 34);
            _refreshButton.Location = new Point(12, 20);

            _checkManagerButton.Text = "Check Manager";
            _checkManagerButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _checkManagerButton.Size = new Size(145, 34);
            _checkManagerButton.Location = new Point(12, 64);

            _installSelectedButton.Text = "Install Selected";
            _installSelectedButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _installSelectedButton.Size = new Size(145, 34);
            _installSelectedButton.Location = new Point(12, 108);

            _openPageButton.Text = "Open Page";
            _openPageButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _openPageButton.Size = new Size(145, 34);
            _openPageButton.Location = new Point(12, 152);

            _actionsPanel.Controls.Add(_refreshButton);
            _actionsPanel.Controls.Add(_checkManagerButton);
            _actionsPanel.Controls.Add(_installSelectedButton);
            _actionsPanel.Controls.Add(_openPageButton);

            _latestPanel.Dock = DockStyle.Left;
            _latestPanel.Width = 430;
            _latestPanel.Padding = new Padding(8, 10, 8, 12);

            _latestHeaderLabel.Dock = DockStyle.Top;
            _latestHeaderLabel.Height = 28;
            _latestHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            _latestHeaderLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _latestHeaderLabel.Text = "Latest On Nexus";

            _latestList.Dock = DockStyle.Top;
            _latestList.Font = new Font("Segoe UI", 9f);
            _latestList.BorderStyle = BorderStyle.FixedSingle;
            _latestList.Height = 320;
            _latestList.IntegralHeight = false;

            _latestPanel.Controls.Add(_latestList);
            _latestPanel.Controls.Add(_latestHeaderLabel);

            _detailsPanel.Dock = DockStyle.Fill;
            _detailsPanel.Padding = new Padding(12);
            _detailsPanel.MinimumSize = new Size(300, 380);

            Controls.Add(_detailsPanel);
            Controls.Add(_latestPanel);
            Controls.Add(_actionsPanel);
            Controls.Add(_installedPanel);
            Controls.Add(_topPanel);
            Name = "NexusModsTab";
            Padding = new Padding(0);

            ResumeLayout(false);
        }

        private void WireEvents()
        {
            _refreshButton.Click += delegate { RefreshLatestModsAsync(); };
            _checkManagerButton.Click += delegate { CheckManagerUpdateAsync(true); };
            _installSelectedButton.Click += InstallSelectedButton_Click;
            _openPageButton.Click += OpenPageButton_Click;
            _installedList.SelectedIndexChanged += InstalledList_SelectedIndexChanged;
            _latestList.SelectedIndexChanged += LatestList_SelectedIndexChanged;
            _detailsPanel.OpenFolderClicked += DetailsPanel_OpenFolderClicked;
            _detailsPanel.WebsiteClicked += DetailsPanel_WebsiteClicked;
            SizeChanged += delegate { AdjustListHeights(); };
        }

        private void DetailsPanel_OpenFolderClicked(object sender, string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    return;

                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            catch { }
        }

        private void DetailsPanel_WebsiteClicked(object sender, string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                    return;

                System.Diagnostics.Process.Start(url);
            }
            catch { }
        }

        private void InstalledList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = _installedList.SelectedItem as NexusListItem;
            if (item == null) return;

            _latestList.ClearSelected();
            ShowInstalledDetails(item);
        }

        private void LatestList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = _latestList.SelectedItem as NexusListItem;
            if (item == null) return;

            _installedList.ClearSelected();
            ShowRemoteDetails(item.RemoteMod, "Latest Nexus release");
        }

        private void InstallSelectedButton_Click(object sender, EventArgs e)
        {
            if (_settings == null || _nexusService == null)
                return;

            if (!_settings.EnableNexusIntegration)
            {
                _statusLabel.Text = "Nexus: Disabled in Settings";
                return;
            }

            if (!_settings.IsModsPathValid)
            {
                _statusLabel.Text = "Install failed: Mods path is not valid.";
                return;
            }

            if (string.IsNullOrEmpty(_settings.NexusApiKey))
            {
                _statusLabel.Text = "Install failed: Set Nexus API key in Settings for direct downloads.";
                return;
            }

            NexusRemoteMod selected = GetSelectedRemoteMod();
            if (selected == null)
            {
                _statusLabel.Text = "Install failed: Select a Nexus mod first.";
                return;
            }

            _installSelectedButton.Enabled = false;
            _statusLabel.Text = "Installing '" + selected.Name + "'...";

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;

                int gameId = selected.GameId;
                if (gameId <= 0)
                {
                    var refreshed = _nexusService.GetModByDomainAndId(selected.GameDomain, selected.ModId, out error);
                    if (!string.IsNullOrEmpty(error) || refreshed == null)
                    {
                        FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "Could not resolve mod metadata."));
                        return;
                    }
                    selected = refreshed;
                    gameId = selected.GameId;
                }

                var file = _nexusService.GetPreferredInstallFile(gameId, selected.ModId, out error);
                if (!string.IsNullOrEmpty(error) || file == null)
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "No installable file found."));
                    return;
                }

                string downloadUrl = _nexusService.GetV1DownloadUrl(selected.GameDomain, selected.ModId, file.FileId, _settings.NexusApiKey, out error);
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(downloadUrl))
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "No downloadable URL returned."));
                    return;
                }

                var result = _installService.DownloadAndInstall(downloadUrl, _settings.ModsPath, selected, file, out error);
                if (result == null || !string.IsNullOrEmpty(error))
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "Unknown install error."));
                    return;
                }

                if (IsDisposed || Disposing) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _installSelectedButton.Enabled = true;
                        _statusLabel.Text = "Installed '" + selected.Name + "' to " + result.InstalledPath;
                        if (InstallCompleted != null)
                            InstallCompleted();
                    });
                }
                catch { }
            });
        }

        private void FinishInstallWithError(string message)
        {
            if (IsDisposed || Disposing) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _installSelectedButton.Enabled = true;
                    _statusLabel.Text = message;
                });
            }
            catch { }
        }

        private void OpenPageButton_Click(object sender, EventArgs e)
        {
            string url = GetSelectedPageUrl();
            if (string.IsNullOrEmpty(url))
                return;

            try { System.Diagnostics.Process.Start(url); }
            catch { }
        }

        private string GetSelectedPageUrl()
        {
            var installed = _installedList.SelectedItem as NexusListItem;
            if (installed != null)
            {
                if (installed.RemoteMod != null)
                {
                    string url = installed.RemoteMod.GetPageUrl();
                    if (!string.IsNullOrEmpty(url)) return url;
                }

                if (installed.LocalMod != null && !string.IsNullOrEmpty(installed.LocalMod.NexusPageUrl))
                    return installed.LocalMod.NexusPageUrl;
            }

            var latest = _latestList.SelectedItem as NexusListItem;
            if (latest != null && latest.RemoteMod != null)
                return latest.RemoteMod.GetPageUrl();

            return string.Empty;
        }

        private NexusRemoteMod GetSelectedRemoteMod()
        {
            var latest = _latestList.SelectedItem as NexusListItem;
            if (latest != null && latest.RemoteMod != null)
                return latest.RemoteMod;

            var installed = _installedList.SelectedItem as NexusListItem;
            if (installed != null)
            {
                if (installed.RemoteMod != null)
                    return installed.RemoteMod;

                var local = installed.LocalMod;
                if (local != null && local.HasNexusReference)
                {
                    var remote = new NexusRemoteMod();
                    remote.GameDomain = local.NexusGameDomain;
                    remote.ModId = local.NexusModId;
                    remote.Name = local.DisplayName;
                    remote.Version = local.NexusRemoteVersion;
                    return remote;
                }
            }

            return null;
        }

        private void ShowInstalledDetails(NexusListItem item)
        {
            var local = item.LocalMod;
            if (local == null)
            {
                _detailsPanel.ShowMod(CreateStatusMod("No Mod Selected", "No installed mod selected."));
                return;
            }

            _detailsPanel.ShowMod(BuildDetailsModFromInstalled(local));
        }

        private void ShowRemoteDetails(NexusRemoteMod mod, string heading)
        {
            if (mod == null)
            {
                _detailsPanel.ShowMod(CreateStatusMod("No Nexus Mod", "No Nexus mod selected."));
                return;
            }

            _detailsPanel.ShowMod(BuildDetailsModFromRemote(mod, heading));
        }

        private NexusRemoteMod BuildRemoteFromLocal(ModItem local)
        {
            if (local == null || !local.HasNexusReference)
                return null;

            var remote = new NexusRemoteMod();
            remote.GameDomain = local.NexusGameDomain;
            remote.ModId = local.NexusModId;
            remote.Name = local.DisplayName;
            remote.Version = local.NexusRemoteVersion;
            remote.Summary = local.NexusRemoteSummary;
            remote.UpdatedAtUtc = local.NexusRemoteUpdatedAtUtc;
            return remote;
        }

        private static ModItem BuildDetailsModFromRemote(NexusRemoteMod mod, string heading)
        {
            var details = new ModItem(mod.ModId.ToString(), mod.Name ?? "Nexus Mod", string.Empty);
            details.Version = string.IsNullOrEmpty(mod.Version) ? "Unknown" : mod.Version;

            string author = string.IsNullOrEmpty(mod.Author) ? mod.UploaderName : mod.Author;
            if (!string.IsNullOrEmpty(author))
                details.Authors = new string[] { author };
            else
                details.Authors = new string[0];

            // Nexus release card does not expose meaningful tag taxonomy for our UI.
            details.Tags = new string[0];

            details.NexusGameDomain = mod.GameDomain ?? string.Empty;
            details.NexusModId = mod.ModId;
            details.NexusPageUrl = mod.GetPageUrl();
            details.NexusRemoteVersion = mod.Version ?? string.Empty;
            details.NexusRemoteUpdatedAtUtc = mod.UpdatedAtUtc;
            details.Website = details.NexusPageUrl;
            details.Description = string.IsNullOrEmpty(mod.Summary) ? "No description provided." : mod.Summary.Trim();

            return details;
        }

        private static ModItem BuildDetailsModFromInstalled(ModItem local)
        {
            var details = new ModItem(local.Id ?? "unknown", local.DisplayName ?? "Unknown Mod", local.RootPath ?? string.Empty);
            details.Version = local.Version;
            details.Authors = local.Authors ?? new string[0];
            details.Tags = local.Tags ?? new string[0];
            details.Website = local.Website ?? string.Empty;
            details.PreviewPath = local.PreviewPath ?? string.Empty;
            details.RequiredModApiVersion = local.RequiredModApiVersion;
            details.IsModApiCompatible = local.IsModApiCompatible;
            details.Status = local.Status;
            details.StatusMessage = local.StatusMessage;

            details.NexusGameDomain = local.NexusGameDomain;
            details.NexusModId = local.NexusModId;
            details.NexusPageUrl = local.NexusPageUrl;
            details.NexusRemoteVersion = local.NexusRemoteVersion;
            details.NexusRemoteUpdatedAtUtc = local.NexusRemoteUpdatedAtUtc;
            details.NexusRemoteSummary = local.NexusRemoteSummary;
            details.HasUpdateAvailable = local.HasUpdateAvailable;

            if (local.DependsOn != null) details.DependsOn = local.DependsOn;
            if (local.LoadAfter != null) details.LoadAfter = local.LoadAfter;
            if (local.LoadBefore != null) details.LoadBefore = local.LoadBefore;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(local.Description))
                sb.AppendLine(local.Description.Trim());
            else
                sb.AppendLine("No description provided.");

            if (!local.HasNexusReference)
            {
                sb.AppendLine();
                sb.AppendLine("Nexus: Not linked");
                sb.AppendLine("Tip: installing via Manager writes About/Nexus.json automatically.");
            }

            details.Description = sb.ToString().Trim();
            return details;
        }

        private static ModItem CreateStatusMod(string title, string message)
        {
            var mod = new ModItem("nexus.status", title, string.Empty);
            mod.Version = string.Empty;
            mod.Authors = new string[0];
            mod.Description = message ?? string.Empty;
            mod.Tags = new string[0];
            return mod;
        }

        private static int GetInstalledSortScore(ModItem mod)
        {
            if (mod == null) return 100;
            if (mod.HasUpdateAvailable) return 0;
            if (mod.HasNexusReference) return 1;
            return 2;
        }

        private string GetGameDomain()
        {
            if (_settings == null || string.IsNullOrEmpty(_settings.NexusGameDomain))
                return "sheltered";
            return _settings.NexusGameDomain.Trim().ToLowerInvariant();
        }

        private string BuildLastCheckedSuffix()
        {
            if (_lastCheckedUtc <= DateTime.MinValue)
                return string.Empty;
            return " | Last checked: " + _lastCheckedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void AdjustListHeights()
        {
            if (_installedList == null || _latestList == null || _topPanel == null)
                return;

            int available = Height - _topPanel.Height - 24;
            if (available <= 0)
                return;

            int target = (int)(available * 0.58f);
            if (target < 220) target = 220;
            if (target > 420) target = 420;

            int installedMax = Math.Max(180, _installedPanel.Height - _installedHeaderLabel.Height - 16);
            int latestMax = Math.Max(180, _latestPanel.Height - _latestHeaderLabel.Height - 16);

            _installedList.Height = Math.Min(target, installedMax);
            _latestList.Height = Math.Min(target, latestMax);
        }

        private static void ApplyButtonTheme(Button button, bool isDark, bool primary)
        {
            if (button == null) return;

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
                    button.BackColor = Color.FromArgb(74, 78, 84);
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Color.FromArgb(96, 101, 108);
                }
            }
            else
            {
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatAppearance.BorderColor = SystemColors.ControlDark;
            }
        }
    }
}
