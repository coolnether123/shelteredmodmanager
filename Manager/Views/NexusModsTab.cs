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
        private enum NexusInstallFileChoice { Auto, Stable, Prerelease, Cancel }
        private enum InstallWorkLeaseResult { Acquired, Busy, StaleBatch, AlreadyOwnedByBatch }

        private sealed class NexusBrowserItem
        {
            public ModItem LocalMod;
            public NexusRemoteMod RemoteMod;
            public NexusItemState State;
            public string PrimaryText;
            public string SecondaryText;
            public string BadgeText;
        }

        private enum NexusAccountAction { None, OpenSettings, SignIn, Retry, AccountSettings }

        // Account card: connection state and the one action that moves it forward.
        private CardPanel _accountCard;
        private Label _accountDot;
        private Label _connectionLabel;
        private Label _downloadLabel;
        private ActionButton _accountButton;
        private NexusAccountAction _accountAction;
        private bool _signInAvailable = true;
        private bool _installedSyncReceived;

        // Toolbar and summary.
        private ActionButton _updatesModeButton;
        private ActionButton _installedModeButton;
        private ActionButton _discoverModeButton;
        private SearchBox _searchBox;
        private string _filterText = string.Empty;
        private ActionButton _refreshButton;
        private ActionButton _checkManagerButton;
        private Label _summaryLabel;
        private Label _feedStatusLabel;
        private Label _managerStatusLabel;

        // Browser list and details.
        private Panel _listFrame;
        private Label _listEmptyLabel;
        private ActionButton _installSelectedButton;
        private ActionButton _openPageButton;
        private ActionButton _updateAllButton;

        // Update all and list installs run one Nexus install at a time. Premium
        // accounts download directly; others authorize each file on the website
        // and the queue moves on once that authorized install finishes.
        private readonly Queue<BatchInstall> _batchQueue = new Queue<BatchInstall>();
        private readonly object _installWorkSync = new object();
        private Guid _activeInstallWorkToken;
        private BatchInstall _activeInstallWorkBatch;
        private BatchInstall _batchCurrent;
        private int _batchTotal;
        private int _batchSucceeded;
        private readonly List<string> _batchFailures = new List<string>();
        private bool _batchAuthorizationExplained;
        private bool _batchPreparing;

        private sealed class BatchInstall
        {
            public NexusRemoteMod Remote;
            public ModItem Local;
            public NexusInstallFileChoice Choice;
            public string Name;
            public int AuthorizedFileId;
        }
        private ListBox _primaryList;
        private ModDetailsPanel _detailsPanel;
        private ToolTip _toolTip;

        public event EventHandler SignInRequested;
        public event EventHandler OpenSettingsRequested;
        public event EventHandler AccountRefreshRequested;

        private NexusModsService _nexusService;
        private NexusInstallService _installService = new NexusInstallService();
        private ManagerSelfUpdateService _managerUpdateService = new ManagerSelfUpdateService();
        private AppSettings _settings;
        private NexusAccountStatus _accountStatus;
        private string _managerVersion = string.Empty;
        private string _managerUpdatePromptedVersion = string.Empty;
        private string _pendingManagerUpdateDomain = string.Empty;
        private int _pendingManagerUpdateFileId;
        private string _pendingManagerUpdateVersion = string.Empty;
        private ManagerUpdateArchiveHashExpectation _pendingManagerUpdateHashExpectation;
        private DateTime _pendingManagerUpdateExpiresUtc = DateTime.MinValue;
        private bool _managerUpdateInProgress;
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

        /// <summary>Raised when an Update all or list install queue has finished.</summary>
        public event EventHandler BatchInstallFinished;

        public NexusModsTab()
        {
            InitializeComponent();
            WireEvents();
        }

        /// <summary>Developer mode shows technical details such as mod IDs.</summary>
        public bool DeveloperMode
        {
            get { return _detailsPanel.ShowTechnicalDetails; }
            set { _detailsPanel.ShowTechnicalDetails = value; }
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
            _installedSyncReceived = true;
            _installedMods = mods ?? new List<ModItem>();
            RebuildInstalledLookup();
            if (_currentMode == NexusViewMode.Updates && updateCount == 0)
                _currentMode = NexusViewMode.Installed;
            RefreshHeaderText(errorMessage);
            RebuildPrimaryList();
            UpdateActionButtons();
        }

        public void SetLastCheckedUtc(DateTime checkedUtc)
        {
            if (checkedUtc > DateTime.MinValue)
                _lastCheckedUtc = checkedUtc;
            RefreshHeaderText();
        }

        public void HandleNxmLinkAsync(string rawLink)
        {
            NexusNxmLink link;
            string parseError;
            if (!NexusNxmLink.TryParse(rawLink, out link, out parseError))
            {
                FinishInstallWithError("Nexus authorization failed: " + parseError, null);
                return;
            }
            AppSettings settings = _settings;
            NexusModsService nexusService = _nexusService;
            NexusAccountStatus accountStatus = _accountStatus;
            if (settings == null || nexusService == null || !settings.EnableNexusIntegration)
            {
                FinishInstallWithError("Nexus authorization failed: Nexus features are disabled or unavailable.", null);
                return;
            }
            if (!settings.HasNexusOAuthSession)
            {
                FinishInstallWithError("Sign in to Nexus before installing mods directly.", null);
                return;
            }
            string gameDomain = GetGameDomain();
            if (!string.Equals(link.GameDomain, gameDomain, StringComparison.OrdinalIgnoreCase))
            {
                FinishInstallWithError("Nexus authorization failed: This Manager only handles downloads for " + gameDomain + ".", null);
                return;
            }
            if (link.UserId > 0 && accountStatus != null && accountStatus.UserId > 0 && link.UserId != accountStatus.UserId)
            {
                FinishInstallWithError("Nexus authorization failed: The browser download belongs to a different Nexus account than the configured credential.", null);
                return;
            }
            if (link.ModId == settings.ManagerNexusModId)
            {
                HandleAuthorizedManagerUpdate(link);
                return;
            }
            if (!settings.IsModsPathValid)
            {
                FinishInstallWithError("Nexus authorization failed: The Mods folder is not configured.", null);
                return;
            }

            // Only the matching queued item owns this website response. A late or
            // unrelated nxm link is a standalone result, never the current item.
            BatchInstall batch = GetCurrentBatch();
            if (batch == null || batch.Remote == null || batch.Remote.ModId != link.ModId ||
                !string.Equals(batch.Remote.GameDomain, link.GameDomain, StringComparison.OrdinalIgnoreCase) ||
                batch.AuthorizedFileId <= 0 || batch.AuthorizedFileId != link.FileId)
                batch = null;
            else
                batch.AuthorizedFileId = 0;

            ModItem localMod;
            _installedByNexusKey.TryGetValue(BuildNexusKey(link.GameDomain, link.ModId), out localMod);
            NexusInstallTargetContext targetContext = NexusInstallTargetContext.FromInstalledMod(localMod);
            string modsPath = settings.ModsPath;
            NexusInstallService installService = _installService;
            _installSelectedButton.Enabled = false;
            EmitActivity("Nexus website authorization received. Resolving the requested file.");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                NexusRemoteMod mod = nexusService.GetModByDomainAndId(link.GameDomain, link.ModId, out error);
                if (!string.IsNullOrEmpty(error) || mod == null)
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "Could not resolve mod metadata."), batch);
                    return;
                }

                List<NexusRemoteModFile> files = nexusService.GetModFiles(link.GameDomain, link.ModId, out error);
                NexusRemoteModFile requestedFile = null;
                for (int i = 0; files != null && i < files.Count; i++)
                {
                    if (files[i] != null && files[i].FileId == link.FileId)
                    {
                        requestedFile = files[i];
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(error) || requestedFile == null)
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "Nexus did not return the authorized file."), batch);
                    return;
                }

                string downloadUrl = nexusService.GetDownloadUrlWithAuthorization(
                    link.GameDomain,
                    link.ModId,
                    link.FileId,
                    link.DownloadKey,
                    link.Expires,
                    out error);
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(downloadUrl))
                {
                    FinishInstallWithError(RewriteDirectDownloadError(error), batch);
                    return;
                }

                InstallWorkLeaseResult leaseResult;
                NexusInstallResult result = DownloadAndInstallWithLease(
                    installService,
                    downloadUrl,
                    modsPath,
                    mod,
                    requestedFile,
                    targetContext,
                    batch,
                    out error,
                    out leaseResult);
                if (leaseResult == InstallWorkLeaseResult.StaleBatch ||
                    leaseResult == InstallWorkLeaseResult.AlreadyOwnedByBatch)
                    return;
                if (leaseResult == InstallWorkLeaseResult.Busy)
                {
                    FinishInstallWithError("A Nexus install is already in progress.", batch);
                    return;
                }
                if (result == null || !string.IsNullOrEmpty(error))
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "Unknown install error."), batch);
                    return;
                }

                FinishInstallSuccess(mod, result, batch);
            });
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Padding = new Padding(16, 14, 16, 12);
            Font = ManagerTheme.UiFont(9f);
            _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 400, ShowAlways = true };

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Account card: who is connected, what they can do, and the next step.
            _accountCard = new CardPanel(null, null);
            _accountCard.Dock = DockStyle.Fill;
            _accountCard.Padding = new Padding(16, 10, 12, 10);
            _accountCard.Margin = new Padding(0, 0, 0, 12);
            TableLayoutPanel account = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            account.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            account.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            account.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _accountDot = UiFactory.Text("■", ThemeRole.Muted, 10f, false);
            _accountDot.Margin = new Padding(0, 0, 8, 0);
            _accountDot.AccessibleRole = AccessibleRole.Graphic;
            _connectionLabel = UiFactory.Text("Nexus: not connected", ThemeRole.Heading, 10.5f, true);
            _connectionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _connectionLabel.Margin = new Padding(0);
            _downloadLabel = UiFactory.Help(string.Empty);
            _downloadLabel.Margin = new Padding(0, 2, 0, 0);
            _accountButton = UiFactory.Button("Sign in to Nexus", ActionButtonKind.Primary, AccountButton_Click);
            _accountButton.Anchor = AnchorStyles.Right;
            _accountButton.Margin = new Padding(12, 0, 0, 0);
            account.Controls.Add(_accountDot, 0, 0);
            account.Controls.Add(_connectionLabel, 1, 0);
            account.Controls.Add(_downloadLabel, 1, 1);
            account.Controls.Add(_accountButton, 2, 0);
            account.SetRowSpan(_accountButton, 2);
            _accountCard.AddBody(account);

            // Toolbar: view switch, filter, and refresh actions.
            TableLayoutPanel toolbar = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 6)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _updatesModeButton = ModeButton("Updates", "Show installed mods with a newer version on Nexus");
            _installedModeButton = ModeButton("Installed", "Show every installed mod and its Nexus link");
            _discoverModeButton = ModeButton("Discover", "Browse the latest Sheltered mods on Nexus");
            FlowLayoutPanel modes = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0, 0, 12, 0),
                Anchor = AnchorStyles.Left
            };
            modes.Controls.Add(_updatesModeButton);
            modes.Controls.Add(_installedModeButton);
            modes.Controls.Add(_discoverModeButton);
            _searchBox = new SearchBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Placeholder = "Filter by name...",
                Margin = new Padding(0, 0, 12, 0),
                MinimumSize = new Size(140, 30),
                Height = 30
            };
            _searchBox.SetFieldName("Filter Nexus mods");
            _refreshButton = UiFactory.Button("Refresh", ActionButtonKind.Secondary, null);
            _refreshButton.Margin = new Padding(0, 0, 6, 0);
            _refreshButton.Anchor = AnchorStyles.Right;
            _toolTip.SetToolTip(_refreshButton, "Reload the Discover feed from Nexus");
            _checkManagerButton = UiFactory.Button("Check SMM update", ActionButtonKind.Quiet, null);
            _checkManagerButton.Margin = new Padding(0);
            _checkManagerButton.Anchor = AnchorStyles.Right;
            _toolTip.SetToolTip(_checkManagerButton, "Check Nexus for a newer Sheltered Mod Manager");
            toolbar.Controls.Add(modes, 0, 0);
            toolbar.Controls.Add(_searchBox, 1, 0);
            toolbar.Controls.Add(_refreshButton, 2, 0);
            toolbar.Controls.Add(_checkManagerButton, 3, 0);

            // Summary line: counts on the left, feed and manager status on the right.
            TableLayoutPanel summary = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8)
            };
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _summaryLabel = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                Margin = new Padding(0),
                Font = ManagerTheme.UiFont(9f)
            };
            _feedStatusLabel = new Label { Visible = false, UseMnemonic = false };
            _managerStatusLabel = UiFactory.Text(string.Empty, ThemeRole.Muted, 9f, false);
            _managerStatusLabel.Anchor = AnchorStyles.Right;
            _managerStatusLabel.Margin = new Padding(12, 0, 0, 0);
            ManagerTheme.SetRole(_summaryLabel, ThemeRole.Muted);
            summary.Controls.Add(_summaryLabel, 0, 0);
            summary.Controls.Add(_managerStatusLabel, 1, 0);

            // Browser: list with its actions, and the details of the selection.
            TableLayoutPanel browser = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
            browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
            browser.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel listColumn = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 16, 0)
            };
            listColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            listColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            listColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _primaryList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = ManagerTheme.UiFont(9.5f),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 46,
                IntegralHeight = false,
                AccessibleName = "Nexus mods"
            };
            _listEmptyLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(20),
                UseMnemonic = false,
                Visible = false,
                Font = ManagerTheme.UiFont(9.5f)
            };
            ManagerTheme.SetRole(_listEmptyLabel, ThemeRole.Muted);
            _listFrame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), Margin = new Padding(0, 0, 0, 10) };
            _listFrame.Controls.Add(_primaryList);
            _listFrame.Controls.Add(_listEmptyLabel);
            _listEmptyLabel.BringToFront();
            _installSelectedButton = UiFactory.Button("Install from Nexus", ActionButtonKind.Primary, null);
            _openPageButton = UiFactory.Button("Open Nexus page", ActionButtonKind.Secondary, null);
            _updateAllButton = UiFactory.Button("Update all", ActionButtonKind.Secondary, delegate
            {
                if (IsBatchRunning)
                    StopBatch("Stopped updating.");
                else
                    StartUpdateAll();
            });
            _updateAllButton.Visible = false;
            _toolTip.SetToolTip(_updateAllButton, "Install every available update, one after another");
            FlowLayoutPanel listActions = UiFactory.ButtonRow(_installSelectedButton, _updateAllButton, _openPageButton);
            listColumn.Controls.Add(_listFrame, 0, 0);
            listColumn.Controls.Add(listActions, 0, 1);

            _detailsPanel = new ModDetailsPanel();
            _detailsPanel.Dock = DockStyle.Fill;
            _detailsPanel.Padding = new Padding(0);
            _detailsPanel.Margin = new Padding(0);
            _detailsPanel.MinimumSize = Size.Empty;
            _detailsPanel.ShowNexusActionButtons = true;

            browser.Controls.Add(listColumn, 0, 0);
            browser.Controls.Add(_detailsPanel, 1, 0);

            root.Controls.Add(_accountCard, 0, 0);
            root.Controls.Add(toolbar, 0, 1);
            root.Controls.Add(summary, 0, 2);
            root.Controls.Add(browser, 0, 3);
            Controls.Add(root);
            Controls.Add(_feedStatusLabel);
            Name = "NexusModsTab";
            ResumeLayout(false);
        }

        private ActionButton ModeButton(string text, string tip)
        {
            ActionButton button = UiFactory.Button(text, ActionButtonKind.Secondary, null);
            button.IsToggle = true;
            button.Margin = new Padding(0, 0, 4, 0);
            button.MinimumSize = new Size(84, 30);
            _toolTip.SetToolTip(button, tip);
            return button;
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
            _primaryList.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && _installSelectedButton.Enabled)
                {
                    _installSelectedButton.PerformClick();
                    e.Handled = true;
                }
            };
            _searchBox.SearchChanged += delegate(object sender, string text)
            {
                _filterText = (text ?? string.Empty).Trim();
                RebuildPrimaryList();
            };
            _detailsPanel.OpenFolderClicked += DetailsPanel_OpenFolderClicked;
            _detailsPanel.WebsiteClicked += DetailsPanel_WebsiteClicked;
            _detailsPanel.NexusUpdateRequested += DetailsPanel_NexusUpdateRequested;
            _detailsPanel.NexusStableRequested += DetailsPanel_NexusStableRequested;
            _feedStatusLabel.TextChanged += delegate
            {
                RefreshModeButtons();
                UpdateListEmptyState();
                if (_items.Count == 0 && _currentMode == NexusViewMode.Discover)
                    ShowEmptyState();
            };
        }

        /// <summary>
        /// Enables the in-tab sign-in shortcut. Sign-in itself always runs the
        /// shared OAuth flow owned by the main window.
        /// </summary>
        public void SetSignInAvailable(bool available)
        {
            _signInAvailable = available;
            RefreshHeaderText();
        }

        /// <summary>Switches to the Updates view, for example from the header status link.</summary>
        public void ShowUpdates()
        {
            SwitchMode(NexusViewMode.Updates);
        }

        private void AccountButton_Click(object sender, EventArgs e)
        {
            EventHandler handler = null;
            switch (_accountAction)
            {
                case NexusAccountAction.SignIn: handler = SignInRequested; break;
                case NexusAccountAction.Retry: handler = AccountRefreshRequested; break;
                case NexusAccountAction.OpenSettings:
                case NexusAccountAction.AccountSettings: handler = OpenSettingsRequested; break;
            }
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public void ApplyTheme(bool isDark)
        {
            ThemePalette palette = ManagerTheme.Current;
            BackColor = palette.Window;
            ManagerTheme.Apply(this);
            _listFrame.BackColor = palette.Border;
            _listEmptyLabel.BackColor = palette.Input;
            _primaryList.BackColor = palette.Input;
            _primaryList.ForeColor = palette.Text;
            _searchBox.ApplyTheme(isDark);
            _detailsPanel.ApplyTheme(isDark);
            RefreshHeaderText();
            RefreshModeButtons();
            _primaryList.Invalidate();
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
            if (forceRefresh)
                _nexusService.ClearCachedResponses();

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
                                _detailsPanel.ShowMessage("Could not load Discover", error);
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
            if (userInitiated)
                _nexusService.ClearCachedResponses();

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
                        string remoteChannel = FormatReleaseChannelSuffix(remote.Version);
                        string localChannel = FormatReleaseChannelSuffix(_managerVersion);
                        if (comparison < 0)
                        {
                            _managerStatusLabel.Text = "SMM: update available (" + remoteVersion + remoteChannel + ")";
                            PromptForManagerUpdate(remote, userInitiated);
                        }
                        else if (comparison == 0)
                            _managerStatusLabel.Text = "SMM: current (" + localVersion + localChannel + ")";
                        else
                            _managerStatusLabel.Text = "SMM: local build newer than Nexus (" + localVersion + localChannel + ")";
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

            if (!string.IsNullOrEmpty(_filterText))
            {
                string filter = _filterText;
                _items = _items.FindAll(delegate(NexusBrowserItem item)
                {
                    return ContainsIgnoreCase(item.PrimaryText, filter) || ContainsIgnoreCase(item.SecondaryText, filter);
                });
            }

            _primaryList.BeginUpdate();
            _primaryList.Items.Clear();
            for (int i = 0; i < _items.Count; i++)
                _primaryList.Items.Add(_items[i]);
            _primaryList.EndUpdate();

            RefreshModeButtons();
            UpdateActionButtons();
            UpdateListEmptyState();
            if (_items.Count == 0)
                ShowEmptyState();
            else if (_primaryList.SelectedIndex < 0)
                _detailsPanel.ShowMessage("Nothing selected", "Select a mod to see its details and Nexus status.");
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
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
                ? FormatVersionWithChannel(mod.Version) + "  →  " + FormatVersionWithChannel(mod.NexusRemoteVersion)
                : FormatVersionWithChannel(mod.Version) + (mod.HasNexusReference ? " linked to Nexus" : " not linked");
            if (HasApiIssue(mod))
                item.SecondaryText += " | " + GetApiIssueText(mod);
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
            item.SecondaryText = FormatVersionWithChannel(remote.Version) + (local != null ? (" | installed as " + local.DisplayName) : " | not installed");
            item.BadgeText = GetBadgeText(state);
            return item;
        }

        private void ShowEmptyState()
        {
            string title;
            string message;
            DescribeEmptyList(out title, out message);
            _detailsPanel.ShowMessage(title, message);
        }

        private void DescribeEmptyList(out string title, out string message)
        {
            bool enabled = _settings == null || _settings.EnableNexusIntegration;
            if (!string.IsNullOrEmpty(_filterText))
            {
                title = "No matches";
                message = "No mods in this view match \"" + _filterText + "\". Clear the filter to see everything.";
                return;
            }
            if (_currentMode == NexusViewMode.Updates)
            {
                title = "No updates found";
                message = enabled
                    ? "Every installed mod that is linked to Nexus is up to date."
                    : "Turn on Nexus features in Settings to check installed mods for updates.";
                return;
            }
            if (_currentMode == NexusViewMode.Discover)
            {
                string feed = _feedStatusLabel.Text ?? string.Empty;
                title = "Nothing to show yet";
                if (!enabled)
                    message = "Turn on Nexus features in Settings to browse the latest Sheltered mods.";
                else if (feed.IndexOf("loading", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    title = "Loading";
                    message = "Getting the latest Sheltered mods from Nexus...";
                }
                else if (feed.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    title = "Could not load Discover";
                    message = "Nexus did not answer. Check your connection, then choose Refresh.";
                }
                else
                    message = "Choose Refresh to load the latest Sheltered mods from Nexus.";
                return;
            }
            if (!_installedSyncReceived)
            {
                title = "Checking installed mods";
                message = "Reading the mods folder and matching mods to Nexus...";
                return;
            }
            title = "No installed mods";
            message = "No mods were found in the mods folder. Install mods from Discover, or copy them into the mods folder.";
        }

        private void UpdateListEmptyState()
        {
            bool empty = _items.Count == 0;
            if (empty)
            {
                string title;
                string message;
                DescribeEmptyList(out title, out message);
                _listEmptyLabel.Text = title + "\r\n" + message;
            }
            _listEmptyLabel.Visible = empty;
        }

        private void SwitchMode(NexusViewMode mode)
        {
            _currentMode = mode;
            RebuildPrimaryList();
            RefreshHeaderText();
        }

        private void RefreshModeButtons()
        {
            int updates = CountUpdates(_installedMods);
            _updatesModeButton.Text = updates > 0 ? "Updates (" + updates + ")" : "Updates";
            _installedModeButton.Text = "Installed (" + _installedMods.Count + ")";
            _discoverModeButton.Text = _latestFeedCache.Count > 0 ? "Discover (" + _latestFeedCache.Count + ")" : "Discover";
            _updatesModeButton.Toggled = _currentMode == NexusViewMode.Updates;
            _installedModeButton.Toggled = _currentMode == NexusViewMode.Installed;
            _discoverModeButton.Toggled = _currentMode == NexusViewMode.Discover;
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

            ThemePalette palette = ManagerTheme.Current;
            var item = _items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = selected ? palette.Selection : palette.Input;
            Color textColor = selected ? palette.SelectionText : palette.Text;
            Color secondaryColor = selected ? palette.SelectionText : palette.TextMuted;
            using (var bg = new SolidBrush(background))
                e.Graphics.FillRectangle(bg, e.Bounds);

            // State badge: coloured pill plus text, so the state never depends on colour alone.
            Color badgeFill;
            Color badgeText;
            GetBadgeColors(item.State, palette, out badgeFill, out badgeText);
            Rectangle badgeRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + (e.Bounds.Height - 20) / 2, 84, 20);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = ManagerTheme.RoundedRectangle(badgeRect, 4))
            using (var badge = new SolidBrush(badgeFill))
                e.Graphics.FillPath(badge, path);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
            TextRenderer.DrawText(e.Graphics, item.BadgeText, ManagerTheme.Semibold(7.75f), badgeRect, badgeText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            int textX = badgeRect.Right + 12;
            int textWidth = Math.Max(10, e.Bounds.Right - textX - 8);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, item.PrimaryText, ManagerTheme.Semibold(9.5f),
                new Rectangle(textX, e.Bounds.Y + 5, textWidth, 20), textColor, flags);
            TextRenderer.DrawText(e.Graphics, item.SecondaryText, ManagerTheme.UiFont(8.5f),
                new Rectangle(textX, e.Bounds.Y + 24, textWidth, 17), secondaryColor, flags);

            using (Pen divider = new Pen(palette.Border))
                e.Graphics.DrawLine(divider, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus && _primaryList.Focused)
            {
                using (Pen focus = new Pen(palette.AccentText, 2f))
                    e.Graphics.DrawRectangle(focus, e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 3, e.Bounds.Height - 3);
            }
        }

        private void InstallSelectedButton_Click(object sender, EventArgs e)
        {
            if (IsBatchBusy())
                return;
            // Signed-out users are routed to the shared OAuth sign-in instead of a dead end.
            if (_settings != null && _settings.EnableNexusIntegration && !_settings.HasNexusOAuthSession)
            {
                if (_signInAvailable && SignInRequested != null)
                    SignInRequested(this, EventArgs.Empty);
                return;
            }
            InstallSelectedRemote(NexusInstallFileChoice.Auto);
        }

        private void DetailsPanel_NexusUpdateRequested(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            ModItem local = item != null ? item.LocalMod : null;
            NexusInstallFileChoice choice = NexusInstallFileChoice.Stable;
            if (local != null && NexusReleaseClassifier.IsPrerelease(local.NexusRemoteVersion))
                choice = NexusInstallFileChoice.Prerelease;
            InstallSelectedRemote(choice);
        }

        private void DetailsPanel_NexusStableRequested(object sender, EventArgs e)
        {
            InstallSelectedRemote(NexusInstallFileChoice.Stable);
        }

        private void InstallSelectedRemote(NexusInstallFileChoice requestedChoice)
        {
            if (IsBatchBusy())
                return;
            NexusRemoteMod selected = GetSelectedRemoteMod();
            if (selected == null)
                return;
            var selectedItem = GetSelectedItem();
            InstallRemote(selected, selectedItem != null ? selectedItem.LocalMod : null, requestedChoice);
        }

        private void InstallRemote(NexusRemoteMod selected, ModItem localMod, NexusInstallFileChoice requestedChoice)
        {
            BatchInstall batch = GetCurrentBatch();
            AppSettings settings = _settings;
            NexusModsService nexusService = _nexusService;
            if (settings == null || nexusService == null)
                return;
            if (!settings.EnableNexusIntegration)
            {
                EmitActivity("Install skipped because Nexus features are disabled.");
                return;
            }
            if (!settings.IsModsPathValid)
            {
                _detailsPanel.ShowMessage("Install failed", "The mods folder is not set. Choose the game in Game setup first.");
                return;
            }
            if (!settings.HasNexusOAuthSession)
            {
                _detailsPanel.ShowMessage("Sign in to install", "Sign in to Nexus to install mods directly.");
                return;
            }

            string modsPath = settings.ModsPath;
            bool includePrereleaseFiles = settings.IncludeNexusPrereleaseFiles;
            NexusAccountStatus accountStatus = _accountStatus;
            NexusInstallService installService = _installService;
            NexusInstallTargetContext targetContext = NexusInstallTargetContext.FromInstalledMod(localMod);

            _installSelectedButton.Enabled = false;
            EmitActivity("Installing from Nexus: " + selected.Name + ".");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                int gameId = selected.GameId;
                if (gameId <= 0)
                {
                    var refreshed = nexusService.GetModByDomainAndId(selected.GameDomain, selected.ModId, out error);
                    if (!string.IsNullOrEmpty(error) || refreshed == null)
                    {
                        FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "Could not resolve mod metadata."), batch);
                        return;
                    }
                    selected = refreshed;
                    gameId = selected.GameId;
                }

                var files = nexusService.GetModFiles(selected.GameDomain, selected.ModId, out error);
                if (!string.IsNullOrEmpty(error) || files == null || files.Count == 0)
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "No installable file was returned."), batch);
                    return;
                }

                var stableFile = nexusService.SelectPreferredInstallFile(files, false);
                var prereleaseFile = includePrereleaseFiles ? nexusService.SelectPreferredPrereleaseInstallFile(files) : null;
                NexusInstallFileChoice resolvedChoice = requestedChoice;
                NexusRemoteModFile file = SelectInstallFile(selected, stableFile, prereleaseFile, ref resolvedChoice);
                if (resolvedChoice == NexusInstallFileChoice.Cancel)
                {
                    FinishInstallWithError("Install canceled.", batch);
                    return;
                }

                if (file == null)
                {
                    FinishInstallWithError(BuildMissingFileMessage(resolvedChoice, includePrereleaseFiles), batch);
                    return;
                }

                if (accountStatus == null ||
                    accountStatus.DirectDownloadAvailability != NexusDirectDownloadAvailability.Available)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (batch != null && !ReferenceEquals(batch, _batchCurrent))
                                return;

                            string explanation =
                                "Nexus requires a short-lived website authorization for non-premium downloads." + Environment.NewLine + Environment.NewLine +
                                "Sheltered Mod Manager will temporarily receive the next nxm:// link, then immediately restore your previous Nexus link handler. The temporary registration also expires automatically after 15 minutes." + Environment.NewLine + Environment.NewLine +
                                (batch != null
                                    ? "Nexus asks you to click Mod Manager Download once for each file. SMM moves on to the next mod after each download."
                                    : "Continue to the selected file on Nexus Mods?");
                            // A queued install explains the website step once, not once per mod.
                            bool explained = batch != null && _batchAuthorizationExplained;
                            if (!explained && MessageBox.Show(this, explanation, "Authorize Nexus download", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                            {
                                UpdateActionButtons();
                                EmitActivity("Install canceled before Nexus website authorization.");
                                if (batch != null && ReferenceEquals(batch, _batchCurrent))
                                    StopBatch("Stopped: the Nexus website step was canceled.");
                                return;
                            }
                            if (batch != null && ReferenceEquals(batch, _batchCurrent))
                                _batchAuthorizationExplained = true;

                            string registrationError;
                            if (!NexusProtocolHandlerService.BeginTemporaryCapture(Application.ExecutablePath, out registrationError))
                            {
                                UpdateActionButtons();
                                FinishInstallWithError(registrationError, batch);
                                return;
                            }

                            if (batch != null && ReferenceEquals(batch, _batchCurrent))
                                batch.AuthorizedFileId = file.FileId;

                            string authorizationPage = selected.GetPageUrl() + "?tab=files&file_id=" + file.FileId;
                            try
                            {
                                System.Diagnostics.Process.Start(authorizationPage);
                                UpdateActionButtons();
                                EmitActivity("Nexus authorization opened. Click Mod Manager Download on the website; the previous nxm handler will be restored automatically.");
                                _detailsPanel.ShowMessage("Waiting for Nexus", "Click Mod Manager Download for the selected file in your browser. SMM will continue automatically when Nexus returns the authorized link.");
                            }
                            catch (Exception ex)
                            {
                                if (batch != null && ReferenceEquals(batch, _batchCurrent))
                                    batch.AuthorizedFileId = 0;
                                NexusProtocolHandlerService.RestorePreviousHandler();
                                UpdateActionButtons();
                                FinishInstallWithError("Could not open the Nexus authorization page: " + ex.Message, batch);
                            }
                        });
                    }
                    catch
                    {
                        FinishInstallWithError("Could not start Nexus website authorization.", batch);
                    }
                    return;
                }

                string downloadUrl = nexusService.GetDownloadUrl(selected.GameDomain, selected.ModId, file.FileId, out error);
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(downloadUrl))
                {
                    FinishInstallWithError(RewriteDirectDownloadError(error), batch);
                    return;
                }

                InstallWorkLeaseResult leaseResult;
                var result = DownloadAndInstallWithLease(
                    installService,
                    downloadUrl,
                    modsPath,
                    selected,
                    file,
                    targetContext,
                    batch,
                    out error,
                    out leaseResult);
                if (leaseResult == InstallWorkLeaseResult.StaleBatch ||
                    leaseResult == InstallWorkLeaseResult.AlreadyOwnedByBatch)
                    return;
                if (leaseResult == InstallWorkLeaseResult.Busy)
                {
                    FinishInstallWithError("A Nexus install is already in progress.", batch);
                    return;
                }
                if (result == null || !string.IsNullOrEmpty(error))
                {
                    FinishInstallWithError("Install failed: " + (!string.IsNullOrEmpty(error) ? error : "Unknown install error."), batch);
                    return;
                }

                FinishInstallSuccess(selected, result, batch);
            });
        }

        private NexusInstallResult DownloadAndInstallWithLease(
            NexusInstallService installService,
            string downloadUrl,
            string modsPath,
            NexusRemoteMod mod,
            NexusRemoteModFile file,
            NexusInstallTargetContext targetContext,
            BatchInstall batch,
            out string errorMessage,
            out InstallWorkLeaseResult leaseResult)
        {
            Guid leaseToken;
            leaseResult = TryAcquireInstallWorkLease(batch, out leaseToken);
            errorMessage = null;
            if (leaseResult != InstallWorkLeaseResult.Acquired)
                return null;

            try
            {
                return installService.DownloadAndInstall(
                    downloadUrl,
                    modsPath,
                    mod,
                    file,
                    targetContext,
                    out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = !string.IsNullOrEmpty(ex.Message) ? ex.Message : "Unknown install error.";
                return null;
            }
            finally
            {
                ReleaseInstallWorkLease(leaseToken);
            }
        }

        private InstallWorkLeaseResult TryAcquireInstallWorkLease(BatchInstall batch, out Guid leaseToken)
        {
            leaseToken = Guid.Empty;
            lock (_installWorkSync)
            {
                if (batch != null && !ReferenceEquals(batch, _batchCurrent))
                    return InstallWorkLeaseResult.StaleBatch;

                // A standalone or unrelated browser response cannot enter while a
                // queued item owns the Nexus flow, even before that item downloads.
                if (batch == null && (_batchCurrent != null || _batchQueue.Count > 0))
                    return InstallWorkLeaseResult.Busy;

                if (_activeInstallWorkToken != Guid.Empty)
                {
                    if (batch != null && ReferenceEquals(batch, _activeInstallWorkBatch))
                        return InstallWorkLeaseResult.AlreadyOwnedByBatch;
                    return InstallWorkLeaseResult.Busy;
                }

                do
                {
                    leaseToken = Guid.NewGuid();
                } while (leaseToken == Guid.Empty);
                _activeInstallWorkToken = leaseToken;
                _activeInstallWorkBatch = batch;
            }

            QueueInstallWorkActionUpdate();
            return InstallWorkLeaseResult.Acquired;
        }

        private void ReleaseInstallWorkLease(Guid leaseToken)
        {
            bool released = false;
            lock (_installWorkSync)
            {
                if (leaseToken != Guid.Empty && leaseToken == _activeInstallWorkToken)
                {
                    _activeInstallWorkToken = Guid.Empty;
                    _activeInstallWorkBatch = null;
                    released = true;
                }
            }

            if (released)
                QueueInstallWorkActionUpdate();
        }

        private BatchInstall GetCurrentBatch()
        {
            lock (_installWorkSync)
                return _batchCurrent;
        }

        private bool IsInstallWorkActive
        {
            get
            {
                lock (_installWorkSync)
                    return _activeInstallWorkToken != Guid.Empty;
            }
        }

        private void QueueInstallWorkActionUpdate()
        {
            if (IsDisposed || Disposing)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed && !Disposing)
                        UpdateActionButtons();
                });
            }
            catch { }
        }

        // ------------------------------------------------------------ Update all and batch installs

        public bool IsBatchRunning
        {
            get
            {
                lock (_installWorkSync)
                    return _batchCurrent != null || _batchQueue.Count > 0;
            }
        }

        /// <summary>True while a batch, install worker, or Update all check is active; says so to the player.</summary>
        private bool IsBatchBusy()
        {
            if (!IsBatchRunning && !_batchPreparing && !IsInstallWorkActive)
                return false;
            _detailsPanel.ShowMessage("Already installing", "Wait for the current installs to finish, then try again.");
            return true;
        }

        /// <summary>
        /// Installs every available update. Shows what will change first, using
        /// each file's changelog from Nexus, then updates the mods one at a time.
        /// </summary>
        public void StartUpdateAll()
        {
            if (IsBatchBusy())
                return;
            if (!CanStartBatch())
                return;

            List<ModItem> updates = new List<ModItem>();
            foreach (ModItem mod in _installedMods)
                if (mod != null && mod.HasUpdateAvailable && mod.HasNexusReference)
                    updates.Add(mod);
            SwitchMode(NexusViewMode.Updates);
            if (updates.Count == 0)
            {
                _detailsPanel.ShowMessage("Everything is up to date", "None of your Nexus mods has a newer version.");
                return;
            }

            _updateAllButton.Enabled = false;
            _updateAllButton.Text = "Checking...";
            bool includePrerelease = _settings.IncludeNexusPrereleaseFiles;
            // Holds off other batches while the files are checked, so none can start and then be replaced.
            _batchPreparing = true;
            UpdateActionButtons();
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<BatchInstall> planned = new List<BatchInstall>();
                List<string> lines = new List<string>();
                string failure = null;
                try
                {
                    PlanUpdates(updates, includePrerelease, planned, lines);
                }
                catch (Exception ex)
                {
                    // An exception here would otherwise end the whole app (it runs on a worker thread).
                    failure = ex.Message;
                }

                if (IsDisposed || Disposing)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            if (failure != null)
                            {
                                _detailsPanel.ShowMessage("Couldn't check for updates", failure);
                                return;
                            }
                            using (ManagerDialog dialog = new ManagerDialog("Update all",
                                planned.Count == 1 ? "Update 1 mod?" : "Update " + planned.Count + " mods?",
                                "Each mod is backed up before it's replaced, so a failed update puts the old version back."))
                            {
                                dialog.AddLines(lines);
                                dialog.AddButton(planned.Count == 1 ? "Update" : "Update all", DialogResult.OK, ActionButtonKind.Primary);
                                dialog.AddButton("Cancel", DialogResult.Cancel, ActionButtonKind.Quiet);
                                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                                    return;
                            }
                            if (!StartBatch(planned))
                                _detailsPanel.ShowMessage("Already installing", "Wait for the current Nexus install to finish, then try again.");
                        }
                        finally
                        {
                            _batchPreparing = false;
                            UpdateActionButtons();
                        }
                    });
                }
                catch
                {
                    // The tab closed while checking; nothing to show.
                }
            });
        }

        /// <summary>Finds the file each update would install and a line describing it (runs on a worker thread).</summary>
        private void PlanUpdates(List<ModItem> updates, bool includePrerelease, List<BatchInstall> planned, List<string> lines)
        {
            foreach (ModItem mod in updates)
            {
                BatchInstall install = new BatchInstall();
                install.Local = mod;
                install.Remote = BuildRemoteFromLocal(mod);
                install.Name = mod.DisplayName;
                bool prerelease = NexusReleaseClassifier.IsPrerelease(mod.NexusRemoteVersion) && includePrerelease;
                install.Choice = prerelease ? NexusInstallFileChoice.Prerelease : NexusInstallFileChoice.Stable;

                string error;
                List<NexusRemoteModFile> files = _nexusService.GetModFiles(mod.NexusGameDomain, mod.NexusModId, out error);
                NexusRemoteModFile file = files == null ? null : (prerelease
                    ? _nexusService.SelectPreferredPrereleaseInstallFile(files)
                    : _nexusService.SelectPreferredInstallFile(files, false));
                string line = mod.DisplayName + "   " + FormatVersionWithChannel(mod.Version) + "  →  " +
                    FormatVersionWithChannel(file != null && !string.IsNullOrEmpty(file.Version) ? file.Version : mod.NexusRemoteVersion);
                string changes = SummarizeChangelog(file != null ? file.Changelog : null);
                if (!string.IsNullOrEmpty(changes))
                    line += Environment.NewLine + "      " + changes;
                lines.Add(line);
                planned.Add(install);
            }
        }

        /// <summary>
        /// Installs mods by Nexus reference, for example the missing mods of an
        /// imported list. Returns false when no install started, so the caller
        /// knows no <see cref="BatchInstallFinished"/> will follow for this request.
        /// </summary>
        public bool InstallReferences(List<NexusModReference> references)
        {
            if (references == null || references.Count == 0 || IsBatchBusy() || !CanStartBatch())
                return false;
            List<BatchInstall> planned = new List<BatchInstall>();
            foreach (NexusModReference reference in references)
            {
                if (reference == null || !reference.IsValid ||
                    !string.Equals(reference.GameDomain, GetGameDomain(), StringComparison.OrdinalIgnoreCase))
                    continue;
                BatchInstall install = new BatchInstall();
                install.Remote = new NexusRemoteMod { GameDomain = reference.GameDomain, ModId = reference.ModId, Name = "Nexus mod " + reference.ModId };
                ModItem local;
                _installedByNexusKey.TryGetValue(BuildNexusKey(reference.GameDomain, reference.ModId), out local);
                install.Local = local;
                install.Choice = NexusInstallFileChoice.Stable;
                install.Name = install.Remote.Name;
                planned.Add(install);
            }
            if (planned.Count == 0)
                return false;
            if (StartBatch(planned))
                return true;

            _detailsPanel.ShowMessage("Already installing", "Wait for the current Nexus install to finish, then try again.");
            return false;
        }

        private bool CanStartBatch()
        {
            if (_settings == null || _nexusService == null || !_settings.EnableNexusIntegration)
            {
                _detailsPanel.ShowMessage("Nexus is off", "Turn on Nexus features in Settings to install from Nexus.");
                return false;
            }
            if (!_settings.IsModsPathValid)
            {
                _detailsPanel.ShowMessage("Set up the game first", "Choose the game in Game setup so SMM knows where your mods folder is.");
                return false;
            }
            if (!_settings.HasNexusOAuthSession)
            {
                _detailsPanel.ShowMessage("Sign in to install", "Sign in to Nexus to install mods directly.");
                if (_signInAvailable && SignInRequested != null)
                    SignInRequested(this, EventArgs.Empty);
                return false;
            }
            return true;
        }

        private bool StartBatch(List<BatchInstall> planned)
        {
            // Reserve the queue in the same gate used by install workers, so a
            // manual/NXM worker cannot enter between the busy check and queue start.
            bool busy;
            lock (_installWorkSync)
            {
                busy = _activeInstallWorkToken != Guid.Empty || _batchCurrent != null || _batchQueue.Count > 0;
                if (!busy)
                {
                    _batchQueue.Clear();
                    foreach (BatchInstall install in planned)
                        _batchQueue.Enqueue(install);
                    _batchTotal = planned.Count;
                    _batchSucceeded = 0;
                    _batchFailures.Clear();
                    _batchAuthorizationExplained = false;
                }
            }
            if (busy)
                return false;

            EmitActivity("Installing " + _batchTotal + " mod" + (_batchTotal == 1 ? string.Empty : "s") + " from Nexus.");
            RunNextBatchInstall();
            return true;
        }

        private void RunNextBatchInstall()
        {
            BatchInstall batch;
            lock (_installWorkSync)
            {
                if (_batchQueue.Count == 0)
                {
                    _batchCurrent = null;
                    batch = null;
                }
                else
                {
                    batch = _batchQueue.Dequeue();
                    _batchCurrent = batch;
                }
            }
            if (batch == null)
            {
                FinishBatch();
                return;
            }
            int number = _batchTotal - _batchQueue.Count;
            UpdateActionButtons();
            _updateAllButton.Visible = true;
            _updateAllButton.Text = "Stop (" + number + " of " + _batchTotal + ")";
            _updateAllButton.Enabled = true;
            _detailsPanel.ShowMessage("Installing " + number + " of " + _batchTotal, batch.Name);
            InstallRemote(batch.Remote, batch.Local, batch.Choice);
        }

        private void OnBatchInstallFinished(BatchInstall batch, bool succeeded, string name, string error)
        {
            lock (_installWorkSync)
            {
                if (batch == null || !ReferenceEquals(batch, _batchCurrent))
                    return;
                _batchCurrent = null;
            }
            if (succeeded)
                _batchSucceeded++;
            else
                _batchFailures.Add((name ?? batch.Name) + ": " + (error ?? "failed"));
            RunNextBatchInstall();
        }

        private void StopBatch(string message)
        {
            lock (_installWorkSync)
            {
                _batchQueue.Clear();
                _batchCurrent = null;
            }
            FinishBatch();
            EmitActivity(message);
        }

        private void FinishBatch()
        {
            lock (_installWorkSync)
                _batchCurrent = null;
            string summary = _batchSucceeded + " of " + _batchTotal + " installed.";
            if (_batchFailures.Count > 0)
                summary += " " + string.Join(" ", _batchFailures.ToArray());
            EmitActivity("Nexus installs finished: " + summary);
            _detailsPanel.ShowMessage(_batchFailures.Count == 0 ? "All done" : "Finished with problems", summary);
            UpdateActionButtons();
            if (BatchInstallFinished != null)
                BatchInstallFinished(this, EventArgs.Empty);
        }

        private int CountInstalledUpdates()
        {
            int count = 0;
            foreach (ModItem mod in _installedMods)
                if (mod != null && mod.HasUpdateAvailable && mod.HasNexusReference)
                    count++;
            return count;
        }

        /// <summary>The first meaningful line of a Nexus changelog, as plain text.</summary>
        private static string SummarizeChangelog(string changelog)
        {
            if (string.IsNullOrEmpty(changelog))
                return null;
            string text = System.Text.RegularExpressions.Regex.Replace(changelog, "<br\\s*/?>|</p>|</li>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>|\\[/?[a-zA-Z*][^\\]]*\\]", string.Empty);
            text = text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
            foreach (string raw in text.Split('\n', '\r'))
            {
                string line = raw.Trim().TrimStart('-', '*', '•', ' ');
                if (line.Length == 0)
                    continue;
                return line.Length > 110 ? line.Substring(0, 107) + "..." : line;
            }
            return null;
        }

        private void PromptForManagerUpdate(NexusRemoteMod remote, bool userInitiated)
        {
            if (remote == null)
                return;

            string version = FormatVersion(remote.Version);
            if (!userInitiated &&
                string.Equals(_managerUpdatePromptedVersion, version, StringComparison.OrdinalIgnoreCase))
                return;

            _managerUpdatePromptedVersion = version;
            string message =
                "Sheltered Mod Manager " + version + " is available." + Environment.NewLine + Environment.NewLine +
                "Yes installs it automatically." + Environment.NewLine +
                "No lets you choose a downloaded ZIP." + Environment.NewLine +
                "Cancel closes this message." + Environment.NewLine + Environment.NewLine +
                "Either installation method keeps your settings and a rollback copy. The manager restarts after installation.";
            DialogResult choice = MessageBox.Show(this, message, "SMM update available",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
            if (choice == DialogResult.Yes)
                BeginManagerUpdate(remote);
            else if (choice == DialogResult.No)
                BeginManualManagerUpdate(remote);
        }

        private void BeginManagerUpdate(NexusRemoteMod remote)
        {
            if (remote == null || _settings == null || _nexusService == null)
                return;
            if (_managerUpdateInProgress)
            {
                EmitActivity("A manager update is already in progress.");
                return;
            }
            _managerUpdateInProgress = true;
            if (!_settings.HasNexusOAuthSession)
            {
                _managerUpdateInProgress = false;
                BeginManualManagerUpdate(remote);
                return;
            }
            string gameProcessName = !string.IsNullOrEmpty(_settings.GamePath)
                ? Path.GetFileNameWithoutExtension(_settings.GamePath)
                : "Sheltered";
            if (IsProcessRunning(gameProcessName))
            {
                FinishManagerUpdateError("Close Sheltered before updating the manager. The game may be using files that must be replaced.");
                return;
            }

            _checkManagerButton.Enabled = false;
            _managerStatusLabel.Text = "SMM: resolving update file...";
            EmitActivity("Resolving the Sheltered Mod Manager update package.");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                List<NexusRemoteModFile> files = _nexusService.GetModFiles(
                    remote.GameDomain, remote.ModId, out error);
                if (!string.IsNullOrEmpty(error) || files == null || files.Count == 0)
                {
                    FinishManagerUpdateError(!string.IsNullOrEmpty(error)
                        ? error
                        : "Nexus did not return a manager update file.");
                    return;
                }

                bool allowPrerelease = _settings.IncludeNexusPrereleaseFiles;
                NexusRemoteModFile file = NexusReleaseClassifier.IsPrerelease(remote.Version) && allowPrerelease
                    ? _nexusService.SelectPreferredPrereleaseInstallFile(files)
                    : _nexusService.SelectPreferredInstallFile(files, false);
                if (file == null)
                {
                    FinishManagerUpdateError("No eligible manager update file was found for the selected release channel.");
                    return;
                }

                if (_accountStatus != null &&
                    _accountStatus.DirectDownloadAvailability == NexusDirectDownloadAvailability.Limited)
                {
                    BeginManagerWebsiteAuthorization(remote, file);
                    return;
                }

                string downloadUrl = _nexusService.GetDownloadUrl(
                    remote.GameDomain, remote.ModId, file.FileId, out error);
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(downloadUrl))
                {
                    FinishManagerUpdateError(RewriteDirectDownloadError(error));
                    return;
                }

                DownloadAndApplyManagerUpdate(
                    downloadUrl,
                    file.Version ?? remote.Version,
                    BuildManagerUpdateHashExpectation(file, remote));
            });
        }

        private void BeginManagerWebsiteAuthorization(NexusRemoteMod remote, NexusRemoteModFile file)
        {
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _pendingManagerUpdateDomain = remote.GameDomain ?? string.Empty;
                    _pendingManagerUpdateFileId = file.FileId;
                    _pendingManagerUpdateVersion = file.Version ?? remote.Version ?? string.Empty;
                    _pendingManagerUpdateHashExpectation = BuildManagerUpdateHashExpectation(file, remote);
                    _pendingManagerUpdateExpiresUtc = DateTime.UtcNow.AddMinutes(15);

                    string registrationError;
                    if (!NexusProtocolHandlerService.BeginTemporaryCapture(Application.ExecutablePath, out registrationError))
                    {
                        ClearPendingManagerUpdate();
                        FinishManagerUpdateError(registrationError);
                        return;
                    }

                    string authorizationPage = remote.GetPageUrl() + "?tab=files&file_id=" + file.FileId;
                    try
                    {
                        System.Diagnostics.Process.Start(authorizationPage);
                        _checkManagerButton.Enabled = false;
                        _managerStatusLabel.Text = "SMM: waiting for Nexus authorization...";
                        EmitActivity("Click Mod Manager Download on Nexus. SMM will continue the manager update automatically.");
                    }
                    catch (Exception ex)
                    {
                        NexusProtocolHandlerService.RestorePreviousHandler();
                        ClearPendingManagerUpdate();
                        FinishManagerUpdateError("Could not open the Nexus authorization page: " + ex.Message);
                    }
                });
            }
            catch
            {
                FinishManagerUpdateError("Could not start Nexus website authorization.");
            }
        }

        private void HandleAuthorizedManagerUpdate(NexusNxmLink link)
        {
            bool matchesPending =
                _pendingManagerUpdateFileId > 0 &&
                _pendingManagerUpdateExpiresUtc > DateTime.UtcNow &&
                string.Equals(link.GameDomain, _pendingManagerUpdateDomain, StringComparison.OrdinalIgnoreCase) &&
                link.FileId == _pendingManagerUpdateFileId;
            if (!matchesPending)
            {
                FinishManagerUpdateError("The Nexus manager download does not match the pending update request. Start the update from Check SMM Update and try again.");
                return;
            }

            string expectedVersion = _pendingManagerUpdateVersion;
            ManagerUpdateArchiveHashExpectation hashExpectation = _pendingManagerUpdateHashExpectation;
            ClearPendingManagerUpdate();
            _checkManagerButton.Enabled = false;
            _managerStatusLabel.Text = "SMM: authorization received...";
            EmitActivity("Nexus authorization received for the manager update.");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                List<NexusRemoteModFile> files = _nexusService.GetModFiles(
                    link.GameDomain, link.ModId, out error);
                NexusRemoteModFile requestedFile = null;
                for (int i = 0; files != null && i < files.Count; i++)
                {
                    if (files[i] != null && files[i].FileId == link.FileId)
                    {
                        requestedFile = files[i];
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(error) || requestedFile == null)
                {
                    FinishManagerUpdateError(!string.IsNullOrEmpty(error)
                        ? error
                        : "Nexus did not return the authorized manager file.");
                    return;
                }

                string downloadUrl = _nexusService.GetDownloadUrlWithAuthorization(
                    link.GameDomain, link.ModId, link.FileId,
                    link.DownloadKey, link.Expires, out error);
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(downloadUrl))
                {
                    FinishManagerUpdateError(RewriteDirectDownloadError(error));
                    return;
                }

                DownloadAndApplyManagerUpdate(
                    downloadUrl,
                    !string.IsNullOrEmpty(expectedVersion) ? expectedVersion : requestedFile.Version,
                    hashExpectation);
            });
        }

        private void ClearPendingManagerUpdate()
        {
            _pendingManagerUpdateDomain = string.Empty;
            _pendingManagerUpdateFileId = 0;
            _pendingManagerUpdateVersion = string.Empty;
            _pendingManagerUpdateHashExpectation = null;
            _pendingManagerUpdateExpiresUtc = DateTime.MinValue;
        }

        private static bool IsProcessRunning(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;
            try
            {
                System.Diagnostics.Process[] processes =
                    System.Diagnostics.Process.GetProcessesByName(processName);
                bool running = processes != null && processes.Length > 0;
                for (int i = 0; processes != null && i < processes.Length; i++)
                    processes[i].Dispose();
                return running;
            }
            catch { return false; }
        }

        private static ManagerUpdateArchiveHashExpectation BuildManagerUpdateHashExpectation(
            NexusRemoteModFile file,
            NexusRemoteMod remote)
        {
            var metadata = new StringBuilder();
            string[] metadataParts =
            {
                file != null ? file.Description : null,
                file != null ? file.Changelog : null,
                remote != null ? remote.Summary : null,
                remote != null ? remote.Description : null
            };
            for (int i = 0; i < metadataParts.Length; i++)
            {
                if (string.IsNullOrEmpty(metadataParts[i]))
                    continue;
                if (metadata.Length > 0)
                    metadata.AppendLine();
                metadata.Append(metadataParts[i]);
            }

            return new ManagerUpdateArchiveHashExpectation
            {
                ExpectedMd5 = file != null ? file.Md5 : string.Empty,
                ReleaseMetadata = metadata.ToString()
            };
        }

        private void DownloadAndApplyManagerUpdate(
            string downloadUrl,
            string expectedVersion,
            ManagerUpdateArchiveHashExpectation hashExpectation)
        {
            SetManagerUpdateStatus("SMM: downloading update...");
            string error;
            ManagerUpdateStage stage = _managerUpdateService.DownloadAndStage(
                downloadUrl, expectedVersion, hashExpectation, out error);
            if (stage == null || !string.IsNullOrEmpty(error))
            {
                FinishManagerUpdateError(!string.IsNullOrEmpty(error)
                    ? error
                    : "The manager update could not be staged.");
                return;
            }
            LaunchStagedManagerUpdate(stage);
        }

        private void LaunchStagedManagerUpdate(ManagerUpdateStage stage)
        {
            string gameProcessName = _settings != null && !string.IsNullOrEmpty(_settings.GamePath)
                ? Path.GetFileNameWithoutExtension(_settings.GamePath)
                : "Sheltered";
            if (IsProcessRunning(gameProcessName))
            {
                FinishManagerUpdateError("Close Sheltered before applying the manager update.");
                return;
            }

            string error;
            if (!_managerUpdateService.LaunchUpdater(stage, out error))
            {
                FinishManagerUpdateError(error);
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _managerStatusLabel.Text = "SMM: restarting into " + stage.Version + "...";
                    EmitActivity("Manager update verified. Restarting SMM.");
                    Application.Exit();
                });
            }
            catch { }
        }

        private void BeginManualManagerUpdate(NexusRemoteMod remote)
        {
            if (remote == null)
                return;
            if (_managerUpdateInProgress)
            {
                EmitActivity("A manager update is already in progress.");
                return;
            }
            _managerUpdateInProgress = true;
            _checkManagerButton.Enabled = false;

            try { System.Diagnostics.Process.Start(remote.GetPageUrl() + "?tab=files"); }
            catch { }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select downloaded Sheltered Mod Manager update";
                dialog.Filter = "ZIP packages (*.zip)|*.zip|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    _managerUpdateInProgress = false;
                    _checkManagerButton.Enabled = true;
                    _managerStatusLabel.Text = "SMM: update available (" + FormatVersion(remote.Version) + ")";
                    EmitActivity("Manual manager update canceled.");
                    return;
                }

                string packagePath = dialog.FileName;
                _managerStatusLabel.Text = "SMM: validating downloaded package...";
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string error;
                    ManagerUpdateStage stage = _managerUpdateService.StageLocalPackage(
                        packagePath,
                        remote.Version,
                        out error);
                    if (stage == null || !string.IsNullOrEmpty(error))
                    {
                        FinishManagerUpdateError(!string.IsNullOrEmpty(error)
                            ? error
                            : "The downloaded manager package could not be staged.");
                        return;
                    }
                    LaunchStagedManagerUpdate(stage);
                });
            }
        }

        private void SetManagerUpdateStatus(string status)
        {
            if (IsDisposed || Disposing)
                return;
            try
            {
                BeginInvoke((MethodInvoker)delegate { _managerStatusLabel.Text = status; });
            }
            catch { }
        }

        private void FinishManagerUpdateError(string message)
        {
            if (IsDisposed || Disposing)
                return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _checkManagerButton.Enabled = true;
                    _managerUpdateInProgress = false;
                    _managerStatusLabel.Text = "SMM: update failed";
                    EmitActivity("SMM update failed: " + (message ?? "Unknown error."));
                    MessageBox.Show(this, message ?? "The manager update failed.",
                        "SMM Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            catch { }
        }

        private void FinishInstallSuccess(NexusRemoteMod mod, NexusInstallResult result, BatchInstall batch)
        {
            if (IsDisposed || Disposing)
                return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    UpdateActionButtons();
                    EmitActivity("Install complete: " + mod.Name + " -> " + result.InstalledPath);
                    if (!string.IsNullOrEmpty(result.VerificationSummary))
                        EmitActivity("Install verified: " + result.VerificationSummary);
                    if (InstallCompleted != null)
                        InstallCompleted();
                    OnBatchInstallFinished(batch, true, mod.Name, null);
                });
            }
            catch { }
        }

        private NexusRemoteModFile SelectInstallFile(
            NexusRemoteMod selected,
            NexusRemoteModFile stableFile,
            NexusRemoteModFile prereleaseFile,
            ref NexusInstallFileChoice requestedChoice)
        {
            if (requestedChoice == NexusInstallFileChoice.Stable)
                return stableFile;

            if (requestedChoice == NexusInstallFileChoice.Prerelease)
                return prereleaseFile;

            if (prereleaseFile != null &&
                (stableFile == null || prereleaseFile.FileId != stableFile.FileId))
            {
                requestedChoice = PromptForPrereleaseInstall(selected, prereleaseFile, stableFile);
                if (requestedChoice == NexusInstallFileChoice.Cancel)
                    return null;

                return requestedChoice == NexusInstallFileChoice.Prerelease ? prereleaseFile : stableFile;
            }

            return stableFile ?? prereleaseFile;
        }

        private static string BuildMissingFileMessage(NexusInstallFileChoice requestedChoice, bool includePrereleaseFiles)
        {
            if (requestedChoice == NexusInstallFileChoice.Prerelease)
                return includePrereleaseFiles
                    ? "Install failed: No active beta/prerelease file was returned."
                    : "Install failed: Enable Nexus beta/prerelease files in Developer Options to install prerelease files.";

            if (requestedChoice == NexusInstallFileChoice.Stable)
                return "Install failed: No active stable file was returned.";

            return includePrereleaseFiles
                ? "Install failed: No active Nexus file was returned."
                : "Install failed: No active stable file was returned. Enable Nexus beta/prerelease files in Settings to install prerelease files.";
        }

        private NexusInstallFileChoice PromptForPrereleaseInstall(NexusRemoteMod mod, NexusRemoteModFile prereleaseFile, NexusRemoteModFile stableFile)
        {
            if (IsDisposed || Disposing)
                return NexusInstallFileChoice.Cancel;

            if (InvokeRequired)
            {
                NexusInstallFileChoice choice = NexusInstallFileChoice.Cancel;
                try
                {
                    Invoke((MethodInvoker)delegate
                    {
                        choice = PromptForPrereleaseInstall(mod, prereleaseFile, stableFile);
                    });
                }
                catch
                {
                    return NexusInstallFileChoice.Cancel;
                }
                return choice;
            }

            string channel = NexusReleaseClassifier.GetDisplayLabel(NexusReleaseClassifier.ClassifyFile(prereleaseFile));
            string modName = mod != null && !string.IsNullOrEmpty(mod.Name) ? mod.Name : "this mod";
            string stableText = stableFile != null
                ? "Stable file: " + FormatNexusFileLabel(stableFile)
                : "No stable file was found.";

            string message =
                "Nexus has a " + channel + " file for " + modName + "." + Environment.NewLine + Environment.NewLine +
                "Prerelease file: " + FormatNexusFileLabel(prereleaseFile) + Environment.NewLine +
                stableText + Environment.NewLine + Environment.NewLine;

            if (stableFile != null)
            {
                message += "Choose Yes to install the " + channel + " file, No to install the stable file, or Cancel to stop.";
                DialogResult result = MessageBox.Show(this, message, "Install Nexus prerelease?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    return NexusInstallFileChoice.Prerelease;
                if (result == DialogResult.No)
                    return NexusInstallFileChoice.Stable;
                return NexusInstallFileChoice.Cancel;
            }

            message += "Choose Yes to install the " + channel + " file, or No to stop.";
            DialogResult prereleaseOnlyResult = MessageBox.Show(this, message, "Install Nexus prerelease?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return prereleaseOnlyResult == DialogResult.Yes ? NexusInstallFileChoice.Prerelease : NexusInstallFileChoice.Cancel;
        }

        private void OpenPageButton_Click(object sender, EventArgs e)
        {
            string url = GetSelectedPageUrl();
            if (string.IsNullOrEmpty(url))
                return;
            try { System.Diagnostics.Process.Start(url); }
            catch { }
        }

        private void FinishInstallWithError(string message, BatchInstall batch)
        {
            if (IsDisposed || Disposing)
                return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    bool isCurrentBatch = false;
                    if (batch != null)
                    {
                        lock (_installWorkSync)
                            isCurrentBatch = ReferenceEquals(batch, _batchCurrent);
                    }
                    bool showInstallError = batch != null
                        ? isCurrentBatch
                        : !IsBatchRunning && !_batchPreparing && !IsInstallWorkActive;

                    UpdateActionButtons();
                    EmitActivity(message);
                    if (showInstallError)
                        _detailsPanel.ShowMessage("Install failed", message);
                    OnBatchInstallFinished(batch, false, null, message);
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

            bool nexusOn = _settings != null && _settings.EnableNexusIntegration;
            bool batchRunning = IsBatchRunning;
            bool installWorkBusy = IsInstallWorkActive;
            bool batchBusy = batchRunning || _batchPreparing || installWorkBusy;
            bool canInstall = remote != null && nexusOn && _settings.IsModsPathValid && !batchBusy;
            _detailsPanel.ShowNexusActionButtons = !batchBusy;
            if (batchRunning)
            {
                _updateAllButton.Visible = true;
                _updateAllButton.Enabled = true;
            }
            else if (!_batchPreparing)
            {
                int updates = CountInstalledUpdates();
                _updateAllButton.Visible = updates > 0 && nexusOn;
                _updateAllButton.Text = updates == 1 ? "Update 1 mod" : "Update all (" + updates + ")";
                _updateAllButton.Enabled = !installWorkBusy;
            }
            else
                _updateAllButton.Enabled = false;
            _installSelectedButton.Enabled = canInstall;
            _installSelectedButton.Text = "Install from Nexus";
            if (item != null && item.LocalMod != null && item.LocalMod.HasUpdateAvailable)
                _installSelectedButton.Text = "Update via Nexus";
            else if (item != null && item.LocalMod != null && item.LocalMod.HasNexusReference)
                _installSelectedButton.Text = "Reinstall from Nexus";

            // Installing needs an OAuth session; offer sign-in rather than a button that fails.
            if (canInstall && !_settings.HasNexusOAuthSession)
            {
                _installSelectedButton.Text = "Sign in to install";
                _installSelectedButton.Enabled = _signInAvailable;
            }
            _toolTip.SetToolTip(_installSelectedButton, batchBusy
                ? "Wait for the current installs to finish"
                : !nexusOn
                ? "Turn on Nexus features in Settings to install from Nexus"
                : (_settings != null && !_settings.IsModsPathValid
                    ? "Set the game path in Game setup first"
                    : (remote == null ? "Select a mod that is on Nexus" : "Download and install the selected file (Enter)")));
        }

        private void RefreshHeaderText(string errorMessage)
        {
            if (_connectionLabel == null)
                return;
            bool enabled = _settings == null || _settings.EnableNexusIntegration;
            string title;
            string detail;
            ThemeRole dot;
            NexusAccountAction action;

            if (!enabled)
            {
                title = "Nexus features are off";
                detail = "Turn them on in Settings to browse Sheltered mods, check for updates, and install from Nexus.";
                dot = ThemeRole.Muted;
                action = NexusAccountAction.OpenSettings;
            }
            else if (_accountStatus == null || !_accountStatus.IsConfigured)
            {
                title = "Not signed in, browsing only";
                detail = _signInAvailable
                    ? "You can browse and check for updates. Sign in with your Nexus account to install mods directly."
                    : "You can browse and check for updates. Nexus sign-in is not available in this build.";
                dot = ThemeRole.Warning;
                action = _signInAvailable ? NexusAccountAction.SignIn : NexusAccountAction.None;
            }
            else if (!_accountStatus.IsConnected &&
                string.Equals(_accountStatus.Summary, "Checking Nexus account...", StringComparison.OrdinalIgnoreCase))
            {
                title = "Checking your Nexus account...";
                detail = "Confirming the session and what this account can download.";
                dot = ThemeRole.Muted;
                action = NexusAccountAction.None;
            }
            else if (!_accountStatus.IsConnected)
            {
                title = "Could not verify your Nexus account";
                detail = !string.IsNullOrEmpty(_accountStatus.ErrorMessage)
                    ? _accountStatus.ErrorMessage
                    : "Nexus did not confirm the session. Check your connection and try again.";
                dot = ThemeRole.Danger;
                action = NexusAccountAction.Retry;
            }
            else
            {
                string membership = _accountStatus.GetMembershipLabel();
                title = string.IsNullOrEmpty(_accountStatus.UserName)
                    ? "Signed in (" + membership + ")"
                    : "Signed in as " + _accountStatus.UserName + " (" + membership + ")";
                detail = BuildCapabilityText(_accountStatus);
                if (string.IsNullOrEmpty(detail))
                    detail = "Direct installs are available for this account.";
                dot = ThemeRole.Success;
                action = NexusAccountAction.AccountSettings;
            }

            _connectionLabel.Text = title;
            _downloadLabel.Text = detail;
            _accountDot.AccessibleName = title;
            ManagerTheme.SetRole(_accountDot, dot);
            _accountAction = action;
            switch (action)
            {
                case NexusAccountAction.SignIn:
                    _accountButton.Text = "Sign in to Nexus";
                    _accountButton.Kind = ActionButtonKind.Primary;
                    _toolTip.SetToolTip(_accountButton, "Sign in securely through Nexus in your browser");
                    break;
                case NexusAccountAction.Retry:
                    _accountButton.Text = "Try again";
                    _accountButton.Kind = ActionButtonKind.Secondary;
                    _toolTip.SetToolTip(_accountButton, "Check the Nexus session again");
                    break;
                case NexusAccountAction.OpenSettings:
                    _accountButton.Text = "Open Settings";
                    _accountButton.Kind = ActionButtonKind.Primary;
                    _toolTip.SetToolTip(_accountButton, "Turn on Nexus features in Settings");
                    break;
                case NexusAccountAction.AccountSettings:
                    _accountButton.Text = "Account settings";
                    _accountButton.Kind = ActionButtonKind.Quiet;
                    _toolTip.SetToolTip(_accountButton, "Manage the Nexus session and download options in Settings");
                    break;
            }
            _accountButton.Visible = action != NexusAccountAction.None;

            if (!enabled)
                _summaryLabel.Text = _installedMods.Count + " installed";
            else if (!string.IsNullOrEmpty(errorMessage))
                _summaryLabel.Text = "Update check failed: " + errorMessage;
            else
                _summaryLabel.Text = _installedMods.Count + " installed   ·   " + _installedByNexusKey.Count + " linked to Nexus   ·   " +
                    FormatCountText(CountUpdates(_installedMods), "update") + "   ·   " +
                    FormatCountText(CountApiIssues(_installedMods), "compatibility warning") + BuildLastCheckedSuffix();
            ManagerTheme.SetRole(_summaryLabel, string.IsNullOrEmpty(errorMessage) ? ThemeRole.Muted : ThemeRole.Danger);

            if (string.IsNullOrEmpty(_managerStatusLabel.Text))
                _managerStatusLabel.Text = "SMM: not checked";
            if (!enabled)
            {
                _refreshButton.Enabled = false;
                _checkManagerButton.Enabled = false;
            }
            else
            {
                // Background work disables these while it runs; only undo the "features off" state here.
                if ((_feedStatusLabel.Text ?? string.Empty).IndexOf("loading", StringComparison.OrdinalIgnoreCase) < 0)
                    _refreshButton.Enabled = true;
                if (!_managerUpdateInProgress && _managerStatusLabel.Text != "SMM: checking...")
                    _checkManagerButton.Enabled = true;
            }
            RefreshModeButtons();
            UpdateActionButtons();
            UpdateListEmptyState();
        }

        private static string FormatCountText(int count, string singular)
        {
            return count + " " + singular + (count == 1 ? string.Empty : "s");
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
            details.DeclaredModApiVersion = local.DeclaredModApiVersion;
            details.DeclaredShelteredApiVersion = local.DeclaredShelteredApiVersion;
            details.ApiCompatibility = local.ApiCompatibility;
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

        private static int GetInstalledSortScore(ModItem mod)
        {
            if (mod == null) return 100;
            if (mod.HasUpdateAvailable) return 0;
            if (HasApiIssue(mod)) return 1;
            if (mod.HasNexusReference) return 2;
            return 3;
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

        private static int CountApiIssues(List<ModItem> mods)
        {
            int count = 0;
            for (int i = 0; i < mods.Count; i++)
            {
                if (HasApiIssue(mods[i]))
                    count++;
            }
            return count;
        }

        private static bool HasApiIssue(ModItem mod)
        {
            return mod != null &&
                mod.ApiCompatibility != null &&
                mod.ApiCompatibility.Severity >= ApiCompatibilitySeverity.Warning;
        }

        private static string GetApiIssueText(ModItem mod)
        {
            if (mod == null || mod.ApiCompatibility == null)
                return "compatibility issue";

            if (mod.ApiCompatibility.Severity == ApiCompatibilitySeverity.Error)
                return "needs mod update";

            return "compatibility warning";
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

        private static string FormatVersionWithChannel(string version)
        {
            return FormatVersion(version) + FormatReleaseChannelSuffix(version);
        }

        private static string FormatNexusFileLabel(NexusRemoteModFile file)
        {
            if (file == null)
                return "Unknown file";

            string name = !string.IsNullOrEmpty(file.Name) ? file.Name : ("File #" + file.FileId);
            string version = !string.IsNullOrEmpty(file.Version) ? FormatVersion(file.Version) : "Unknown version";
            string category = !string.IsNullOrEmpty(file.Category) ? file.Category : "uncategorized";
            return name + " (" + version + ", " + category + ")";
        }

        private static string FormatReleaseChannelSuffix(string version)
        {
            NexusReleaseChannel channel = NexusReleaseClassifier.ClassifyVersion(version);
            if (channel == NexusReleaseChannel.Stable)
                return string.Empty;

            return " " + NexusReleaseClassifier.GetDisplayLabel(channel);
        }

        private static string BuildConnectionText(NexusAccountStatus status)
        {
            if (status == null)
                return "Nexus: not connected";

            if (!status.IsConfigured)
                return "Nexus: browse only (not signed in)";

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
                return "Sign in to Nexus to install mods directly.";

            if (!status.IsConfigured)
                return "Sign in to Nexus to install mods directly.";

            if (!status.IsConnected)
                return string.Equals(status.Summary, "Checking Nexus account...", StringComparison.OrdinalIgnoreCase)
                    ? "Checking whether this account can download mods..."
                    : "Could not check whether this account can install mods.";

            switch (status.DirectDownloadAvailability)
            {
                case NexusDirectDownloadAvailability.Available:
                    return string.Empty;
                case NexusDirectDownloadAvailability.Limited:
                    return "Direct install: Nexus website authorization required";
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

        private static void GetBadgeColors(NexusItemState state, ThemePalette palette, out Color fill, out Color text)
        {
            Color onStatus = palette.IsDark ? palette.Chrome : Color.White;
            switch (state)
            {
                case NexusItemState.UpdateAvailable:
                    fill = palette.Accent;
                    text = palette.OnAccent;
                    break;
                case NexusItemState.InstalledCurrent:
                    fill = palette.Success;
                    text = onStatus;
                    break;
                case NexusItemState.InstalledUnlinked:
                    fill = palette.SurfaceHover;
                    text = palette.Text;
                    break;
                default:
                    fill = palette.Warning;
                    text = onStatus;
                    break;
            }
        }
    }
}
