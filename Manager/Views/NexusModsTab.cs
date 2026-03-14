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
    public delegate void NexusActivityHandler(string message);

    public class NexusModsTab : UserControl
    {
        private enum NexusViewMode { Updates, Installed, Discover }
        private enum NexusItemState { UpdateAvailable, InstalledCurrent, InstalledUnlinked, Available }

        private sealed class NexusBrowserItem
        {
            public ModItem LocalMod;
            public NexusRemoteMod RemoteMod;
            public NexusItemState State;
            public string PrimaryText;
            public string SecondaryText;
            public string BadgeText;
        }

        private Panel _topPanel;
        private Label _connectionLabel;
        private Label _downloadLabel;
        private Label _summaryLabel;
        private Label _feedStatusLabel;
        private Label _managerStatusLabel;
        private Button _refreshButton;
        private Button _checkManagerButton;
        private Panel _leftPanel;
        private Panel _modePanel;
        private Button _updatesModeButton;
        private Button _installedModeButton;
        private Button _discoverModeButton;
        private Panel _listActionPanel;
        private Button _installSelectedButton;
        private Button _openPageButton;
        private ListBox _primaryList;
        private ModDetailsPanel _detailsPanel;

        private NexusModsService _nexusService;
        private NexusInstallService _installService = new NexusInstallService();
        private AppSettings _settings;
        private NexusAccountStatus _accountStatus;
        private string _managerVersion = string.Empty;
        private int _latestRequestToken;
        private DateTime _lastCheckedUtc = DateTime.MinValue;
        private static readonly TimeSpan LatestFeedCooldown = TimeSpan.FromMinutes(5);
        private List<NexusRemoteMod> _latestFeedCache = new List<NexusRemoteMod>();
        private DateTime _latestFeedCacheUtc = DateTime.MinValue;
        private string _latestFeedCacheDomain = string.Empty;
        private List<ModItem> _installedMods = new List<ModItem>();
        private Dictionary<string, ModItem> _installedByNexusKey = new Dictionary<string, ModItem>(StringComparer.OrdinalIgnoreCase);
        private List<NexusBrowserItem> _items = new List<NexusBrowserItem>();
        private NexusViewMode _currentMode = NexusViewMode.Installed;

        public event NexusInstallCompletedHandler InstallCompleted;
        public event NexusActivityHandler NexusActivity;

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
            RefreshHeaderText();
            RebuildPrimaryList();
            UpdateActionButtons();
        }

        public void SetAccountStatus(NexusAccountStatus status)
        {
            _accountStatus = status;
            RefreshHeaderText();
        }

        public void UpdateInstalledMods(List<ModItem> mods, int mappedMods, int updateCount, string errorMessage)
        {
            _installedMods = mods ?? new List<ModItem>();
            RebuildInstalledLookup();
            if (_currentMode == NexusViewMode.Updates && updateCount == 0)
                _currentMode = NexusViewMode.Installed;
            RefreshHeaderText(errorMessage);
            RebuildPrimaryList();
        }

        public void SetLastCheckedUtc(DateTime checkedUtc)
        {
            if (checkedUtc > DateTime.MinValue)
                _lastCheckedUtc = checkedUtc;
            RefreshHeaderText();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            _topPanel = new Panel();
            _connectionLabel = new Label();
            _downloadLabel = new Label();
            _summaryLabel = new Label();
            _feedStatusLabel = new Label();
            _managerStatusLabel = new Label();
            _refreshButton = new Button();
            _checkManagerButton = new Button();
            _leftPanel = new Panel();
            _modePanel = new Panel();
            _updatesModeButton = new Button();
            _installedModeButton = new Button();
            _discoverModeButton = new Button();
            _listActionPanel = new Panel();
            _installSelectedButton = new Button();
            _openPageButton = new Button();
            _primaryList = new ListBox();
            _detailsPanel = new ModDetailsPanel();

            _topPanel.Dock = DockStyle.Top;
            _topPanel.Height = 98;
            _topPanel.Padding = new Padding(12, 10, 12, 10);

            _connectionLabel.AutoSize = false;
            _connectionLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _connectionLabel.Location = new Point(12, 10);
            _connectionLabel.Size = new Size(700, 20);
            _connectionLabel.AutoEllipsis = true;
            _connectionLabel.Text = "Nexus: not connected";

            _downloadLabel.AutoSize = false;
            _downloadLabel.Font = new Font("Segoe UI", 8.75f);
            _downloadLabel.Location = new Point(12, 32);
            _downloadLabel.Size = new Size(700, 18);
            _downloadLabel.AutoEllipsis = true;

            _summaryLabel.AutoSize = false;
            _summaryLabel.Font = new Font("Segoe UI", 9f);
            _summaryLabel.Location = new Point(12, 56);
            _summaryLabel.Size = new Size(700, 18);
            _summaryLabel.AutoEllipsis = true;

            _feedStatusLabel.AutoSize = false;
            _feedStatusLabel.Font = new Font("Segoe UI", 9f);
            _feedStatusLabel.Location = new Point(12, 76);
            _feedStatusLabel.Size = new Size(220, 18);
            _feedStatusLabel.AutoEllipsis = true;
            _feedStatusLabel.Visible = false;

            _managerStatusLabel.AutoSize = false;
            _managerStatusLabel.Font = new Font("Segoe UI", 9f);
            _managerStatusLabel.Location = new Point(250, 76);
            _managerStatusLabel.Size = new Size(430, 18);
            _managerStatusLabel.AutoEllipsis = true;

            ConfigurePrimaryButton(_refreshButton, "Refresh");
            ConfigurePrimaryButton(_checkManagerButton, "Check SMM Update");
            _refreshButton.Size = new Size(120, 32);
            _checkManagerButton.Size = new Size(145, 32);
            _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _checkManagerButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _topPanel.Controls.Add(_connectionLabel);
            _topPanel.Controls.Add(_downloadLabel);
            _topPanel.Controls.Add(_summaryLabel);
            _topPanel.Controls.Add(_feedStatusLabel);
            _topPanel.Controls.Add(_managerStatusLabel);
            _topPanel.Controls.Add(_refreshButton);
            _topPanel.Controls.Add(_checkManagerButton);

            _leftPanel.Dock = DockStyle.Left;
            _leftPanel.Width = 470;
            _leftPanel.Padding = new Padding(12, 10, 12, 12);

            _modePanel.Dock = DockStyle.Top;
            _modePanel.Height = 42;

            ConfigureModeButton(_updatesModeButton, "Updates");
            ConfigureModeButton(_installedModeButton, "Installed");
            ConfigureModeButton(_discoverModeButton, "Discover");
            _updatesModeButton.Location = new Point(0, 0);
            _installedModeButton.Location = new Point(118, 0);
            _discoverModeButton.Location = new Point(236, 0);
            _modePanel.Controls.Add(_updatesModeButton);
            _modePanel.Controls.Add(_installedModeButton);
            _modePanel.Controls.Add(_discoverModeButton);

            _listActionPanel.Dock = DockStyle.Bottom;
            _listActionPanel.Height = 48;

            ConfigureSecondaryButton(_installSelectedButton, "Install from Nexus");
            ConfigureSecondaryButton(_openPageButton, "Open Nexus Page");
            _installSelectedButton.Location = new Point(0, 8);
            _installSelectedButton.Size = new Size(170, 32);
            _openPageButton.Location = new Point(180, 8);
            _openPageButton.Size = new Size(150, 32);
            _listActionPanel.Controls.Add(_installSelectedButton);
            _listActionPanel.Controls.Add(_openPageButton);

            _primaryList.Dock = DockStyle.Fill;
            _primaryList.BorderStyle = BorderStyle.FixedSingle;
            _primaryList.Font = new Font("Segoe UI", 9f);
            _primaryList.DrawMode = DrawMode.OwnerDrawFixed;
            _primaryList.ItemHeight = 38;

            _leftPanel.Controls.Add(_primaryList);
            _leftPanel.Controls.Add(_listActionPanel);
            _leftPanel.Controls.Add(_modePanel);

            _detailsPanel.Dock = DockStyle.Fill;
            _detailsPanel.Padding = new Padding(12);
            _detailsPanel.MinimumSize = new Size(340, 380);

            Controls.Add(_detailsPanel);
            Controls.Add(_leftPanel);
            Controls.Add(_topPanel);
            Name = "NexusModsTab";
            ResumeLayout(false);
            LayoutTopPanel();
        }

        private void WireEvents()
        {
            _refreshButton.Click += delegate { RefreshLatestModsAsync(true); };
            _checkManagerButton.Click += delegate { CheckManagerUpdateAsync(true); };
            _updatesModeButton.Click += delegate { SwitchMode(NexusViewMode.Updates); };
            _installedModeButton.Click += delegate { SwitchMode(NexusViewMode.Installed); };
            _discoverModeButton.Click += delegate { SwitchMode(NexusViewMode.Discover); };
            _installSelectedButton.Click += InstallSelectedButton_Click;
            _openPageButton.Click += OpenPageButton_Click;
            _primaryList.SelectedIndexChanged += PrimaryList_SelectedIndexChanged;
            _primaryList.DrawItem += PrimaryList_DrawItem;
            _detailsPanel.OpenFolderClicked += DetailsPanel_OpenFolderClicked;
            _detailsPanel.WebsiteClicked += DetailsPanel_WebsiteClicked;
            _topPanel.Resize += delegate { LayoutTopPanel(); };
        }

        public void ApplyTheme(bool isDark)
        {
            BackColor = isDark ? Color.FromArgb(46, 48, 53) : SystemColors.Control;
            _topPanel.BackColor = isDark ? Color.FromArgb(40, 42, 46) : SystemColors.ControlLight;
            _leftPanel.BackColor = BackColor;
            _modePanel.BackColor = BackColor;
            _listActionPanel.BackColor = BackColor;
            _primaryList.BackColor = isDark ? Color.FromArgb(32, 34, 38) : SystemColors.Window;
            _primaryList.ForeColor = isDark ? Color.WhiteSmoke : SystemColors.WindowText;
            _connectionLabel.ForeColor = isDark ? Color.White : SystemColors.ControlText;
            _downloadLabel.ForeColor = isDark ? Color.Gainsboro : SystemColors.ControlText;
            _summaryLabel.ForeColor = isDark ? Color.Gainsboro : SystemColors.ControlText;
            _feedStatusLabel.ForeColor = isDark ? Color.Gainsboro : SystemColors.ControlText;
            _managerStatusLabel.ForeColor = isDark ? Color.Gainsboro : SystemColors.ControlText;
            ApplyButtonTheme(_refreshButton, isDark, true);
            ApplyButtonTheme(_checkManagerButton, isDark, false);
            ApplyButtonTheme(_updatesModeButton, isDark, _currentMode == NexusViewMode.Updates);
            ApplyButtonTheme(_installedModeButton, isDark, _currentMode == NexusViewMode.Installed);
            ApplyButtonTheme(_discoverModeButton, isDark, _currentMode == NexusViewMode.Discover);
            ApplyButtonTheme(_installSelectedButton, isDark, true);
            ApplyButtonTheme(_openPageButton, isDark, false);
            _detailsPanel.ApplyTheme(isDark);
        }

        public void RefreshLatestModsAsync()
        {
            RefreshLatestModsAsync(false);
        }

        public void RefreshLatestModsAsync(bool forceRefresh)
        {
            if (_settings != null && !_settings.EnableNexusIntegration)
            {
                _feedStatusLabel.Text = "Discover: disabled";
                EmitActivity("Discover refresh skipped because Nexus features are disabled.");
                return;
            }
            if (_nexusService == null)
            {
                _feedStatusLabel.Text = "Discover: unavailable";
                return;
            }

            string domain = GetGameDomain();
            if (string.IsNullOrEmpty(domain))
            {
                _feedStatusLabel.Text = "Discover: no domain";
                return;
            }

            if (!forceRefresh && HasLatestFeedCache(domain))
            {
                _feedStatusLabel.Text = "Discover: " + _latestFeedCache.Count + " cached";
                RebuildPrimaryList();
                return;
            }

            _feedStatusLabel.Text = "Discover: loading...";
            _refreshButton.Enabled = false;
            int token = ++_latestRequestToken;

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                var latest = _nexusService.GetLatestMods(domain, 40, out error);
                if (IsDisposed || Disposing)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (token != _latestRequestToken)
                            return;
                        _refreshButton.Enabled = true;
                        if (!string.IsNullOrEmpty(error))
                        {
                            _feedStatusLabel.Text = "Discover: refresh failed";
                            EmitActivity("Discover refresh failed: " + error);
                            if (_currentMode == NexusViewMode.Discover)
                                _detailsPanel.ShowMod(CreateStatusMod("Nexus Discover Failed", error));
                            return;
                        }

                        _latestFeedCache = latest ?? new List<NexusRemoteMod>();
                        _latestFeedCacheUtc = DateTime.UtcNow;
                        _latestFeedCacheDomain = domain;
                        _feedStatusLabel.Text = "Discover: " + _latestFeedCache.Count + " cached";
                        EmitActivity("Discover refresh complete: loaded " + _latestFeedCache.Count + " mods.");
                        RebuildPrimaryList();
                    });
                }
                catch { }
            });
        }

        public void CheckManagerUpdateAsync(bool userInitiated)
        {
            if (_settings == null || _nexusService == null || !_settings.EnableNexusIntegration)
            {
                _managerStatusLabel.Text = "SMM: disabled";
                return;
            }
            if (_settings.ManagerNexusModId <= 0)
            {
                _managerStatusLabel.Text = "SMM: no manager ID";
                return;
            }

            string domain = GetGameDomain();
            if (string.IsNullOrEmpty(domain))
            {
                _managerStatusLabel.Text = "SMM: no domain";
                return;
            }

            _managerStatusLabel.Text = "SMM: checking...";
            _checkManagerButton.Enabled = false;

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                var remote = _nexusService.GetModByDomainAndId(domain, _settings.ManagerNexusModId, out error);
                if (IsDisposed || Disposing)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _checkManagerButton.Enabled = true;
                        if (!string.IsNullOrEmpty(error))
                        {
                            _managerStatusLabel.Text = "SMM: check failed";
                            EmitActivity("SMM update check failed: " + error);
                            return;
                        }
                        if (remote == null)
                        {
                            _managerStatusLabel.Text = "SMM: not found on Nexus";
                            return;
                        }

                        int comparison = NexusVersionComparer.CompareVersions(_managerVersion, remote.Version);
                        string localVersion = FormatVersion(_managerVersion);
                        string remoteVersion = FormatVersion(remote.Version);
                        if (comparison < 0)
                            _managerStatusLabel.Text = "SMM: update available (" + remoteVersion + ")";
                        else if (comparison == 0)
                            _managerStatusLabel.Text = "SMM: current (" + localVersion + ")";
                        else
                            _managerStatusLabel.Text = "SMM: local build newer than Nexus";
                    });
                }
                catch { }
            });
        }

        private void RebuildInstalledLookup()
        {
            _installedByNexusKey.Clear();
            for (int i = 0; i < _installedMods.Count; i++)
            {
                var mod = _installedMods[i];
                if (mod == null || !mod.HasNexusReference)
                    continue;
                _installedByNexusKey[BuildNexusKey(mod.NexusGameDomain, mod.NexusModId)] = mod;
            }
        }

        private void RebuildPrimaryList()
        {
            _items.Clear();
            switch (_currentMode)
            {
                case NexusViewMode.Updates:
                    BuildUpdateItems();
                    break;
                case NexusViewMode.Discover:
                    BuildDiscoverItems();
                    break;
                default:
                    BuildInstalledItems();
                    break;
            }

            _primaryList.BeginUpdate();
            _primaryList.Items.Clear();
            for (int i = 0; i < _items.Count; i++)
                _primaryList.Items.Add(_items[i]);
            _primaryList.EndUpdate();

            RefreshModeButtons();
            UpdateActionButtons();
            if (_items.Count == 0)
                ShowEmptyState();
        }

        private void BuildUpdateItems()
        {
            var source = new List<ModItem>();
            for (int i = 0; i < _installedMods.Count; i++)
            {
                var mod = _installedMods[i];
                if (mod != null && mod.HasUpdateAvailable)
                    source.Add(mod);
            }
            source.Sort(delegate (ModItem a, ModItem b) { return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase); });
            for (int i = 0; i < source.Count; i++)
                _items.Add(CreateInstalledItem(source[i], NexusItemState.UpdateAvailable));
        }

        private void BuildInstalledItems()
        {
            var source = new List<ModItem>(_installedMods);
            source.Sort(delegate (ModItem a, ModItem b)
            {
                int aScore = GetInstalledSortScore(a);
                int bScore = GetInstalledSortScore(b);
                if (aScore != bScore) return aScore.CompareTo(bScore);
                return string.Compare(a != null ? a.DisplayName : string.Empty, b != null ? b.DisplayName : string.Empty, StringComparison.OrdinalIgnoreCase);
            });
            for (int i = 0; i < source.Count; i++)
            {
                var mod = source[i];
                if (mod == null) continue;
                NexusItemState state = mod.HasUpdateAvailable ? NexusItemState.UpdateAvailable :
                    (mod.HasNexusReference ? NexusItemState.InstalledCurrent : NexusItemState.InstalledUnlinked);
                _items.Add(CreateInstalledItem(mod, state));
            }
        }

        private void BuildDiscoverItems()
        {
            for (int i = 0; i < _latestFeedCache.Count; i++)
            {
                var remote = _latestFeedCache[i];
                if (remote == null) continue;
                string key = BuildNexusKey(remote.GameDomain, remote.ModId);
                ModItem local;
                _installedByNexusKey.TryGetValue(key, out local);
                NexusItemState state = NexusItemState.Available;
                if (local != null)
                    state = local.HasUpdateAvailable ? NexusItemState.UpdateAvailable : NexusItemState.InstalledCurrent;
                _items.Add(CreateDiscoverItem(local, remote, state));
            }
        }

        private NexusBrowserItem CreateInstalledItem(ModItem mod, NexusItemState state)
        {
            var item = new NexusBrowserItem();
            item.LocalMod = mod;
            item.RemoteMod = BuildRemoteFromLocal(mod);
            item.State = state;
            item.PrimaryText = mod.DisplayName;
            item.SecondaryText = state == NexusItemState.UpdateAvailable
                ? FormatVersion(mod.Version) + " -> " + FormatVersion(mod.NexusRemoteVersion)
                : FormatVersion(mod.Version) + (mod.HasNexusReference ? " linked to Nexus" : " not linked");
            item.BadgeText = GetBadgeText(state);
            return item;
        }

        private NexusBrowserItem CreateDiscoverItem(ModItem local, NexusRemoteMod remote, NexusItemState state)
        {
            var item = new NexusBrowserItem();
            item.LocalMod = local;
            item.RemoteMod = remote;
            item.State = state;
            item.PrimaryText = remote.Name;
            item.SecondaryText = FormatVersion(remote.Version) + (local != null ? (" | installed as " + local.DisplayName) : " | not installed");
            item.BadgeText = GetBadgeText(state);
            return item;
        }

        private void ShowEmptyState()
        {
            if (_currentMode == NexusViewMode.Updates)
                _detailsPanel.ShowMod(CreateStatusMod("No Updates Found", "All linked installed mods appear current."));
            else if (_currentMode == NexusViewMode.Discover)
                _detailsPanel.ShowMod(CreateStatusMod("No Discover Results", "Refresh the Nexus feed to browse the latest releases."));
            else
                _detailsPanel.ShowMod(CreateStatusMod("No Installed Mods", "No installed mods are available to display in the Nexus view."));
        }

        private void SwitchMode(NexusViewMode mode)
        {
            _currentMode = mode;
            RebuildPrimaryList();
        }

        private void RefreshModeButtons()
        {
            bool dark = BackColor.R < 90 && BackColor.G < 90 && BackColor.B < 90;
            ApplyButtonTheme(_updatesModeButton, dark, _currentMode == NexusViewMode.Updates);
            ApplyButtonTheme(_installedModeButton, dark, _currentMode == NexusViewMode.Installed);
            ApplyButtonTheme(_discoverModeButton, dark, _currentMode == NexusViewMode.Discover);
        }

        private void PrimaryList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            if (item == null)
            {
                UpdateActionButtons();
                return;
            }

            if (item.LocalMod != null)
                _detailsPanel.ShowMod(BuildDetailsModFromInstalled(item.LocalMod));
            else
                _detailsPanel.ShowMod(BuildDetailsModFromRemote(item.RemoteMod));

            UpdateActionButtons();
        }

        private void PrimaryList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _items.Count)
                return;

            var item = _items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool dark = BackColor.R < 90 && BackColor.G < 90 && BackColor.B < 90;
            Color background = selected ? SystemColors.Highlight : _primaryList.BackColor;
            Color textColor = selected ? SystemColors.HighlightText : _primaryList.ForeColor;
            using (var bg = new SolidBrush(background))
                e.Graphics.FillRectangle(bg, e.Bounds);

            Rectangle badgeRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 7, 82, e.Bounds.Height - 14);
            using (var badge = new SolidBrush(GetStateColor(item.State, dark)))
                e.Graphics.FillRectangle(badge, badgeRect);
            using (var badgeText = new SolidBrush(Color.White))
                e.Graphics.DrawString(item.BadgeText, new Font(_primaryList.Font, FontStyle.Bold), badgeText, badgeRect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

            float textX = badgeRect.Right + 8;
            using (var primary = new SolidBrush(textColor))
                e.Graphics.DrawString(item.PrimaryText, _primaryList.Font, primary, new PointF(textX, e.Bounds.Y + 4));
            using (var secondary = new SolidBrush(selected ? SystemColors.HighlightText : (dark ? Color.Gainsboro : Color.DimGray)))
                e.Graphics.DrawString(item.SecondaryText, new Font(_primaryList.Font.FontFamily, 8.5f), secondary, new PointF(textX, e.Bounds.Y + 20));

            e.DrawFocusRectangle();
        }

        private void InstallSelectedButton_Click(object sender, EventArgs e)
        {
            if (_settings == null || _nexusService == null)
                return;
            if (!_settings.EnableNexusIntegration)
            {
                EmitActivity("Install skipped because Nexus features are disabled.");
                return;
            }
            if (!_settings.IsModsPathValid)
            {
                _detailsPanel.ShowMod(CreateStatusMod("Install Failed", "Mods folder is not configured."));
                return;
            }
            if (string.IsNullOrEmpty(_settings.NexusApiKey))
            {
                _detailsPanel.ShowMod(CreateStatusMod("Install Disabled", "Add a personal Nexus API key in Settings to attempt direct installs."));
                return;
            }

            NexusRemoteMod selected = GetSelectedRemoteMod();
            if (selected == null)
                return;

            _installSelectedButton.Enabled = false;
            EmitActivity("Installing from Nexus: " + selected.Name + ".");

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
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "No installable file was returned."));
                    return;
                }

                string downloadUrl = _nexusService.GetV1DownloadUrl(selected.GameDomain, selected.ModId, file.FileId, _settings.NexusApiKey, out error);
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(downloadUrl))
                {
                    FinishInstallWithError(RewriteDirectDownloadError(error));
                    return;
                }

                var result = _installService.DownloadAndInstall(downloadUrl, _settings.ModsPath, selected, file, out error);
                if (result == null || !string.IsNullOrEmpty(error))
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "Unknown install error."));
                    return;
                }

                if (IsDisposed || Disposing)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _installSelectedButton.Enabled = true;
                        EmitActivity("Install complete: " + selected.Name + " -> " + result.InstalledPath);
                        if (InstallCompleted != null)
                            InstallCompleted();
                    });
                }
                catch { }
            });
        }

        private void OpenPageButton_Click(object sender, EventArgs e)
        {
            string url = GetSelectedPageUrl();
            if (string.IsNullOrEmpty(url))
                return;
            try { System.Diagnostics.Process.Start(url); }
            catch { }
        }

        private void FinishInstallWithError(string message)
        {
            if (IsDisposed || Disposing)
                return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _installSelectedButton.Enabled = true;
                    EmitActivity(message);
                    _detailsPanel.ShowMod(CreateStatusMod("Install Failed", message));
                });
            }
            catch { }
        }

        private void DetailsPanel_OpenFolderClicked(object sender, string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", path);
            }
            catch { }
        }

        private void DetailsPanel_WebsiteClicked(object sender, string url)
        {
            try
            {
                if (!string.IsNullOrEmpty(url))
                    System.Diagnostics.Process.Start(url);
            }
            catch { }
        }

        private void UpdateActionButtons()
        {
            var item = GetSelectedItem();
            var remote = GetSelectedRemoteMod();
            string url = GetSelectedPageUrl();
            _openPageButton.Enabled = !string.IsNullOrEmpty(url);

            bool canInstall = remote != null && _settings != null && _settings.EnableNexusIntegration && _settings.IsModsPathValid && !string.IsNullOrEmpty(_settings.NexusApiKey);
            _installSelectedButton.Enabled = canInstall;
            _installSelectedButton.Text = "Install from Nexus";
            if (item != null && item.LocalMod != null && item.LocalMod.HasUpdateAvailable)
                _installSelectedButton.Text = "Update via Nexus";
            else if (item != null && item.LocalMod != null && item.LocalMod.HasNexusReference)
                _installSelectedButton.Text = "Reinstall from Nexus";
        }

        private void LayoutTopPanel()
        {
            bool showHelper = _downloadLabel.Visible;
            _topPanel.Height = showHelper ? 80 : 60;

            int rightMargin = 12;
            _checkManagerButton.Location = new Point(_topPanel.Width - _checkManagerButton.Width - rightMargin, 12);
            _refreshButton.Location = new Point(_checkManagerButton.Left - _refreshButton.Width - 12, 12);

            int contentWidth = _refreshButton.Left - 24;
            if (contentWidth < 200)
                contentWidth = 200;

            _connectionLabel.Width = contentWidth;
            _downloadLabel.Width = contentWidth;
            _summaryLabel.Width = contentWidth;

            _connectionLabel.Location = new Point(12, 10);
            _downloadLabel.Location = new Point(12, 32);
            _summaryLabel.Location = new Point(12, showHelper ? 56 : 32);
            _managerStatusLabel.Location = new Point(_summaryLabel.Right - 260, _summaryLabel.Top);
            _managerStatusLabel.Width = 260;
        }

        private void RefreshHeaderText(string errorMessage)
        {
            bool enabled = _settings == null || _settings.EnableNexusIntegration;
            if (_accountStatus == null)
            {
                _connectionLabel.Text = enabled ? "Nexus: not connected" : "Nexus: disabled";
                _downloadLabel.Text = enabled
                    ? "Direct install: add an API key in Settings."
                    : "Nexus features are disabled in Settings.";
            }
            else
            {
                _connectionLabel.Text = BuildConnectionText(_accountStatus);
                _downloadLabel.Text = BuildCapabilityText(_accountStatus);
            }

            if (!string.IsNullOrEmpty(errorMessage))
                _summaryLabel.Text = "Installed check failed";
            else
                _summaryLabel.Text = "Installed " + _installedMods.Count + "   Linked " + _installedByNexusKey.Count + "   Updates " + CountUpdates(_installedMods) + BuildLastCheckedSuffix();

            if (string.IsNullOrEmpty(_managerStatusLabel.Text))
                _managerStatusLabel.Text = "SMM: not checked";

            _downloadLabel.Visible = !string.IsNullOrEmpty(_downloadLabel.Text);
            LayoutTopPanel();
        }

        private void RefreshHeaderText()
        {
            RefreshHeaderText(null);
        }

        private string RewriteDirectDownloadError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return "Install failed: Nexus did not return a downloadable URL.";
            if (error.IndexOf("denied direct download", StringComparison.OrdinalIgnoreCase) < 0)
                return "Install failed: " + error;
            if (_accountStatus == null)
                return "Install failed: Nexus denied the direct download. This can happen because of account tier, file restrictions, or app approval.";

            string membership = _accountStatus.GetMembershipLabel();
            return "Install failed: Nexus denied the direct download. Connected account: " + membership + ". " + _accountStatus.DirectDownloadSummary;
        }

        private NexusBrowserItem GetSelectedItem()
        {
            return _primaryList.SelectedItem as NexusBrowserItem;
        }

        private NexusRemoteMod GetSelectedRemoteMod()
        {
            var item = GetSelectedItem();
            if (item == null)
                return null;
            if (item.RemoteMod != null)
                return item.RemoteMod;
            if (item.LocalMod != null && item.LocalMod.HasNexusReference)
                return BuildRemoteFromLocal(item.LocalMod);
            return null;
        }

        private string GetSelectedPageUrl()
        {
            var item = GetSelectedItem();
            if (item == null)
                return string.Empty;
            if (item.RemoteMod != null)
                return item.RemoteMod.GetPageUrl();
            if (item.LocalMod != null)
                return item.LocalMod.NexusPageUrl;
            return string.Empty;
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

        private static ModItem BuildDetailsModFromRemote(NexusRemoteMod mod)
        {
            var details = new ModItem(mod.ModId.ToString(), mod.Name ?? "Nexus Mod", string.Empty);
            details.Version = string.IsNullOrEmpty(mod.Version) ? "Unknown" : mod.Version;
            string author = string.IsNullOrEmpty(mod.Author) ? mod.UploaderName : mod.Author;
            details.Authors = !string.IsNullOrEmpty(author) ? new string[] { author } : new string[0];
            details.Tags = new string[0];
            details.NexusGameDomain = mod.GameDomain ?? string.Empty;
            details.NexusModId = mod.ModId;
            details.NexusPageUrl = mod.GetPageUrl();
            details.NexusRemoteVersion = mod.Version ?? string.Empty;
            details.NexusRemoteUpdatedAtUtc = mod.UpdatedAtUtc;
            details.Website = string.Empty;
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
            sb.AppendLine(!string.IsNullOrEmpty(local.Description) ? local.Description.Trim() : "No description provided.");
            if (!local.HasNexusReference)
            {
                sb.AppendLine();
                sb.AppendLine("Nexus: Not linked");
                sb.AppendLine("Tip: installing through the Manager writes About/Nexus.json automatically.");
            }
            details.Description = sb.ToString().Trim();
            return details;
        }

        private static ModItem CreateStatusMod(string title, string message)
        {
            var mod = new ModItem("nexus.status", title, string.Empty);
            mod.Authors = new string[0];
            mod.Tags = new string[0];
            mod.Description = message ?? string.Empty;
            return mod;
        }

        private static int GetInstalledSortScore(ModItem mod)
        {
            if (mod == null) return 100;
            if (mod.HasUpdateAvailable) return 0;
            if (mod.HasNexusReference) return 1;
            return 2;
        }

        private static int CountUpdates(List<ModItem> mods)
        {
            int count = 0;
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i] != null && mods[i].HasUpdateAvailable)
                    count++;
            }
            return count;
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
            return "   Checked " + _lastCheckedUtc.ToLocalTime().ToString("h:mm tt");
        }

        private bool HasLatestFeedCache(string domain)
        {
            return _latestFeedCache.Count > 0 &&
                _latestFeedCacheUtc > DateTime.MinValue &&
                string.Equals(_latestFeedCacheDomain, domain, StringComparison.OrdinalIgnoreCase) &&
                (DateTime.UtcNow - _latestFeedCacheUtc) < LatestFeedCooldown;
        }

        private void EmitActivity(string message)
        {
            if (!string.IsNullOrEmpty(message) && NexusActivity != null)
                NexusActivity(message);
        }

        private static string BuildNexusKey(string gameDomain, int modId)
        {
            return (gameDomain ?? string.Empty).Trim().ToLowerInvariant() + ":" + modId;
        }

        private static string FormatVersion(string version)
        {
            string value = (version ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value))
                return "Unknown";
            return value.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? value : "v" + value;
        }

        private static string BuildConnectionText(NexusAccountStatus status)
        {
            if (status == null)
                return "Nexus: not connected";

            if (!status.IsConfigured)
                return "Nexus: no API key";

            if (!status.IsConnected)
                return string.Equals(status.Summary, "Checking Nexus account...", StringComparison.OrdinalIgnoreCase)
                    ? "Nexus: checking account..."
                    : "Nexus: could not verify account";

            string membership = status.GetMembershipLabel();
            if (!string.IsNullOrEmpty(status.UserName))
                return "Nexus: " + status.UserName + " (" + membership + ")";

            return "Nexus: connected (" + membership + ")";
        }

        private static string BuildCapabilityText(NexusAccountStatus status)
        {
            if (status == null)
                return "Direct install: add an API key in Settings.";

            if (!status.IsConfigured)
                return "Direct install: add an API key in Settings.";

            if (!status.IsConnected)
                return string.Equals(status.Summary, "Checking Nexus account...", StringComparison.OrdinalIgnoreCase)
                    ? "Direct install: checking account capability..."
                    : "Direct install: account status unavailable.";

            switch (status.DirectDownloadAvailability)
            {
                case NexusDirectDownloadAvailability.Available:
                    return string.Empty;
                case NexusDirectDownloadAvailability.Limited:
                    return "Direct install: limited by Nexus policy";
                case NexusDirectDownloadAvailability.Unavailable:
                    return "Direct install: unavailable";
                default:
                    return "Direct install: capability unknown";
            }
        }

        private static string GetBadgeText(NexusItemState state)
        {
            switch (state)
            {
                case NexusItemState.UpdateAvailable: return "UPDATE";
                case NexusItemState.InstalledCurrent: return "INSTALLED";
                case NexusItemState.InstalledUnlinked: return "UNLINKED";
                default: return "AVAILABLE";
            }
        }

        private static Color GetStateColor(NexusItemState state, bool dark)
        {
            switch (state)
            {
                case NexusItemState.UpdateAvailable: return dark ? Color.FromArgb(0, 122, 204) : Color.RoyalBlue;
                case NexusItemState.InstalledCurrent: return dark ? Color.FromArgb(0, 153, 96) : Color.SeaGreen;
                case NexusItemState.InstalledUnlinked: return dark ? Color.FromArgb(120, 120, 120) : Color.Gray;
                default: return dark ? Color.FromArgb(150, 90, 0) : Color.Peru;
            }
        }

        private static void ConfigurePrimaryButton(Button button, string text)
        {
            button.Text = text;
            button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            button.FlatStyle = FlatStyle.Flat;
        }

        private static void ConfigureSecondaryButton(Button button, string text)
        {
            button.Text = text;
            button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            button.FlatStyle = FlatStyle.Flat;
        }

        private static void ConfigureModeButton(Button button, string text)
        {
            button.Text = text;
            button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            button.FlatStyle = FlatStyle.Flat;
            button.Size = new Size(108, 30);
        }

        private static void ApplyButtonTheme(Button button, bool isDark, bool active)
        {
            if (button == null)
                return;
            button.UseVisualStyleBackColor = false;
            if (isDark)
            {
                if (active)
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
                button.BackColor = active ? Color.FromArgb(225, 235, 248) : SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatAppearance.BorderColor = SystemColors.ControlDark;
            }
        }

    }
}
