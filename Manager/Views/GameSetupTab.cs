using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Manager.Controls;
using Manager.Core.Models;

namespace Manager.Views
{
    /// <summary>
    /// Delegate for string path events
    /// </summary>
    public delegate void StringPathHandler(string path);

    /// <summary>
    /// Delegate for bool events
    /// </summary>
    public delegate void BoolEventHandler(bool value);

    /// <summary>
    /// Game Setup tab - handles game path configuration and launch.
    /// </summary>
    public class GameSetupTab : UserControl
    {
        // Path controls
        private TextBox _gamePathTextBox;
        private ActionButton _browseButton;
        private ActionButton _detectButton;
        private TextBox _modsPathTextBox;
        private ActionButton _openModsFolderButton;
        private ActionButton _openGameFolderButton;

        // Status
        private Label _statusLabel;
        private Label _modsCountLabel;
        private Label _modApiVersionLabel;

        // Launch buttons
        private ActionButton _launchButton;
        private ActionButton _launchVanillaButton;

        // Log viewer
        private Panel _logFrame;
        private RichTextBox _logTextBox;
        private ActionButton _clearLogButton;
        private ActionButton _loadGameLogButton;

        // Auto-load selection
        private ComboBox _autoLoadComboBox;
        private ActionButton _refreshSavesButton;
        private const int AUTO_LOAD_NONE = 0;
        private const int AUTO_LOAD_NEW_SAVE = -1;
        private const int MinimumPageHeight = 470;
        private const string InstallHelp =
            "Choose Sheltered.exe (Steam or GOG) or ShelteredWindows64_EOS.exe (Epic). You can also drop the file or its folder on the path box.";
        private TableLayoutPanel _root;
        private CardPanel _installCard;
        private CardPanel _playCard;

        private ToolTip _toolTip;

        // State
        private AppSettings _settings;
        private bool _isUpdating = false;
        private string _lastLoggedPath = null;

        /// <summary>
        /// Event raised when game path changes
        /// </summary>
        public event StringPathHandler GamePathChanged;

        /// <summary>
        /// Event raised when launch is requested (true = with mods, false = vanilla)
        /// </summary>
        public event BoolEventHandler LaunchRequested;

        /// <summary>
        /// Event raised when view game log is requested
        /// </summary>
        public event EventHandler ViewGameLogRequested;

        public GameSetupTab()
        {
            InitializeComponent();
            WireEvents();
        }

        /// <summary>Developer mode adds the shortcut to the game's own SMM log.</summary>
        public bool DeveloperMode
        {
            get { return _loadGameLogButton.Visible; }
            set { _loadGameLogButton.Visible = value; }
        }

        public void Initialize(AppSettings settings)
        {
            _settings = settings;
            UpdateFromSettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Padding = new Padding(16, 14, 16, 12);
            this.Font = ManagerTheme.UiFont(9f);
            ManagerTheme.SetRole(this, ThemeRole.Window);
            _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 400, ShowAlways = true };

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Installation card: game executable, derived mods folder, and folder shortcuts.
            CardPanel install = new CardPanel("Sheltered installation", InstallHelp);
            install.Dock = DockStyle.Fill;

            _gamePathTextBox = UiFactory.TextField();
            _gamePathTextBox.AccessibleName = "Sheltered executable path";
            _gamePathTextBox.AllowDrop = true;
            _gamePathTextBox.Margin = new Padding(0, 3, 8, 3);
            _gamePathTextBox.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            _gamePathTextBox.DragDrop += (s, e) =>
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) _gamePathTextBox.Text = files[0];
            };
            _browseButton = UiFactory.Button("Browse...", ActionButtonKind.Secondary, null);
            _detectButton = UiFactory.Button("Auto-detect", ActionButtonKind.Quiet, null);
            _browseButton.Margin = new Padding(0, 0, 6, 0);
            _detectButton.Margin = new Padding(0);
            _toolTip.SetToolTip(_detectButton, "Search the usual Steam, GOG, and Epic install folders");
            install.AddBody(Row(null, UiFactory.Framed(_gamePathTextBox), _browseButton, _detectButton));

            _statusLabel = UiFactory.Text("Game not found.", ThemeRole.Danger, 9f, true);
            _statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _statusLabel.Margin = new Padding(0, 4, 0, 10);
            install.AddBody(_statusLabel);

            Label modsCaption = UiFactory.Text("Mods folder", ThemeRole.Default, 9f, true);
            modsCaption.Anchor = AnchorStyles.Left;
            modsCaption.Margin = new Padding(0, 0, 10, 0);
            _modsPathTextBox = UiFactory.TextField();
            _modsPathTextBox.ReadOnly = true;
            _modsPathTextBox.AccessibleName = "Mods folder path";
            _modsPathTextBox.Margin = new Padding(0, 3, 8, 3);
            _openModsFolderButton = UiFactory.Button("Open mods folder", ActionButtonKind.Secondary, null);
            _openGameFolderButton = UiFactory.Button("Open game folder", ActionButtonKind.Quiet, null);
            _openModsFolderButton.Margin = new Padding(0, 0, 6, 0);
            _openGameFolderButton.Margin = new Padding(0);
            install.AddBody(Row(modsCaption, UiFactory.Framed(_modsPathTextBox), _openModsFolderButton, _openGameFolderButton));

            // Play card: launch actions and auto-load choice.
            CardPanel play = new CardPanel("Play", null);
            play.Dock = DockStyle.Fill;
            play.Margin = new Padding(0, 0, 0, 12);
            _launchButton = UiFactory.Button("Launch Sheltered (modded)", ActionButtonKind.Primary, null);
            _launchButton.Font = ManagerTheme.Semibold(10.5f);
            _launchButton.MinimumSize = new Size(0, 40);
            _launchButton.Padding = new Padding(20, 0, 20, 0);
            _launchButton.Enabled = false;
            _launchVanillaButton = UiFactory.Button("Launch vanilla (no mods)", ActionButtonKind.Secondary, null);
            _launchVanillaButton.MinimumSize = new Size(0, 40);
            _launchVanillaButton.Padding = new Padding(16, 0, 16, 0);
            _launchVanillaButton.Enabled = false;

            _modsCountLabel = UiFactory.Text("Active mods: 0", ThemeRole.Default, 9f, true);
            _modApiVersionLabel = UiFactory.Text(string.Empty, ThemeRole.Warning, 9f, false);
            _modApiVersionLabel.Visible = false;
            _modsCountLabel.AutoEllipsis = true;
            _modApiVersionLabel.AutoEllipsis = true;
            TableLayoutPanel launchInfo = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(12, 0, 0, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            launchInfo.Controls.Add(_modsCountLabel, 0, 0);
            launchInfo.Controls.Add(_modApiVersionLabel, 0, 1);
            _launchButton.Margin = new Padding(0, 0, 8, 0);
            _launchVanillaButton.Margin = new Padding(0);
            TableLayoutPanel launchRow = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top
            };
            launchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            launchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            launchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            launchRow.Controls.Add(_launchButton, 0, 0);
            launchRow.Controls.Add(_launchVanillaButton, 1, 0);
            launchRow.Controls.Add(launchInfo, 2, 0);
            launchRow.Margin = new Padding(0, 0, 0, 10);
            play.AddBody(launchRow);

            Label autoLoadLabel = UiFactory.Text("Auto-load on start", ThemeRole.Default, 9f, false);
            autoLoadLabel.Anchor = AnchorStyles.Left;
            autoLoadLabel.Margin = new Padding(0, 0, 10, 0);
            _autoLoadComboBox = UiFactory.ChoiceField(null);
            _autoLoadComboBox.AccessibleName = "Auto-load on start";
            _autoLoadComboBox.Width = 360;
            _autoLoadComboBox.Anchor = AnchorStyles.Left;
            _autoLoadComboBox.Margin = new Padding(0, 0, 6, 0);
            // Keep 10 actual save slots visible before scroll starts.
            // The list also includes "(None)" + "(New Save)" as extra items.
            _autoLoadComboBox.MaxDropDownItems = 12;
            _refreshSavesButton = UiFactory.Button("Refresh saves", ActionButtonKind.Quiet, null);
            _refreshSavesButton.Margin = new Padding(0);
            _refreshSavesButton.Anchor = AnchorStyles.Left;
            _toolTip.SetToolTip(_refreshSavesButton, "Read the save slots from the game folder again");
            TableLayoutPanel autoLoadRow = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            autoLoadRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            autoLoadRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            autoLoadRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            autoLoadRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            autoLoadRow.Controls.Add(autoLoadLabel, 0, 0);
            autoLoadRow.Controls.Add(_autoLoadComboBox, 1, 0);
            autoLoadRow.Controls.Add(_refreshSavesButton, 2, 0);
            play.AddBody(autoLoadRow);

            // Activity log card fills the remaining height.
            CardPanel log = new CardPanel(null, null);
            log.FillsHeight = true;
            log.Dock = DockStyle.Fill;
            log.Margin = new Padding(0);
            TableLayoutPanel logHeader = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 10)
            };
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            // Styled like a card title (tan serif caps over a rule) with the log actions beside it.
            Label logTitle = UiFactory.Text("ACTIVITY LOG", ThemeRole.Accent, 9.5f, false);
            logTitle.Font = ManagerTheme.Display(9.5f);
            logTitle.AccessibleName = "Activity log";
            logTitle.Anchor = AnchorStyles.Left;
            logTitle.Margin = new Padding(0);
            logHeader.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen rule = new Pen(ManagerTheme.Current.Border))
                    e.Graphics.DrawLine(rule, 0, logHeader.Height - 1, logHeader.Width, logHeader.Height - 1);
            };
            _clearLogButton = UiFactory.Button("Clear", ActionButtonKind.Quiet, null);
            _loadGameLogButton = UiFactory.Button("View game log", ActionButtonKind.Quiet, null);
            _toolTip.SetToolTip(_loadGameLogButton, "Show SMM/mod_manager.log from the game folder");
            _loadGameLogButton.Margin = new Padding(0, 0, 4, 4);
            _clearLogButton.Margin = new Padding(0, 0, 0, 4);
            logHeader.Controls.Add(logTitle, 0, 0);
            logHeader.Controls.Add(_loadGameLogButton, 1, 0);
            logHeader.Controls.Add(_clearLogButton, 2, 0);
            log.AddBody(logHeader);

            _logTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = ManagerTheme.UiFont(9f),
                ReadOnly = true,
                Multiline = true,
                WordWrap = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                AccessibleName = "Activity log"
            };
            _logFrame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), MinimumSize = new Size(0, 60) };
            _logFrame.Controls.Add(_logTextBox);
            log.AddBody(_logFrame);

            root.Controls.Add(install, 0, 0);
            root.Controls.Add(play, 0, 1);
            root.Controls.Add(log, 0, 2);
            install.Margin = new Padding(0, 0, 0, 12);
            log.TabIndex = 2;
            _root = root;
            _installCard = install;
            _playCard = play;

            // Below MinimumPageHeight the page scrolls instead of squeezing the activity log away.
            Panel host = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            root.Dock = DockStyle.Top;
            root.Height = MinimumPageHeight;
            host.Controls.Add(root);
            host.Resize += delegate
            {
                // Toggling AutoScroll clears a scrollbar left over from a smaller size.
                host.AutoScroll = host.Height < MinimumPageHeight;
                root.Height = Math.Max(host.ClientSize.Height, MinimumPageHeight);
            };
            this.Controls.Add(host);
            this.ResumeLayout();
        }

        // One row: optional leading label, a stretching field, then trailing buttons.
        private static TableLayoutPanel Row(Control leading, Control fill, params Control[] trailing)
        {
            int first = leading == null ? 0 : 1;
            TableLayoutPanel row = new TableLayoutPanel
            {
                ColumnCount = first + 1 + trailing.Length,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            if (leading != null)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.Controls.Add(leading, 0, 0);
            }
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.Controls.Add(fill, first, 0);
            for (int i = 0; i < trailing.Length; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.Controls.Add(trailing[i], first + 1 + i, 0);
            }
            return row;
        }

        private void WireEvents()
        {
            _browseButton.Click += BrowseButton_Click;
            _detectButton.Click += DetectGameButton_Click;
            _openModsFolderButton.Click += OpenModsFolderButton_Click;
            _openGameFolderButton.Click += OpenGameFolderButton_Click;
            _launchButton.Click += LaunchButton_Click;
            _launchVanillaButton.Click += LaunchVanillaButton_Click;
            _gamePathTextBox.TextChanged += GamePathTextBox_TextChanged;
            _clearLogButton.Click += ClearLogButton_Click;
            _loadGameLogButton.Click += LoadGameLogButton_Click;
            _autoLoadComboBox.SelectedIndexChanged += AutoLoadComboBox_SelectedIndexChanged;
            _refreshSavesButton.Click += (s, e) => RefreshSavesList();
            this.Load += GameSetupTab_Load;
        }

        private void AutoLoadComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdating || _settings == null) return;

            var selected = _autoLoadComboBox.SelectedItem as SaveSlotItem;
            if (selected != null)
            {
                _settings.AutoLoadSaveSlot = selected.Slot;
                if (selected.Slot == AUTO_LOAD_NEW_SAVE)
                    Log("Auto-load set to start a new save in the first free slot.");
                else
                    Log("Auto-load slot set to: " + selected.ToString());
            }
        }

        private class SaveSlotItem
        {
            public int Slot { get; set; }
            public string Description { get; set; }
            public override string ToString() => Description;
        }

        public void RefreshSavesList()
        {
            if (_settings == null || !_settings.IsGamePathValid)
            {
                _autoLoadComboBox.Items.Clear();
                _autoLoadComboBox.Items.Add(new SaveSlotItem { Slot = AUTO_LOAD_NONE, Description = "Open the main menu" });
                _autoLoadComboBox.Items.Add(new SaveSlotItem { Slot = AUTO_LOAD_NEW_SAVE, Description = "Start a new save in the first free slot" });
                _autoLoadComboBox.SelectedIndex = 0;
                return;
            }

            _isUpdating = true;
            try
            {
                int currentSelection = _settings.AutoLoadSaveSlot;
                _autoLoadComboBox.Items.Clear();
                _autoLoadComboBox.Items.Add(new SaveSlotItem { Slot = AUTO_LOAD_NONE, Description = "Open the main menu" });
                int selectIndex = 0;
                if (currentSelection == AUTO_LOAD_NONE) selectIndex = 0;

                int newSaveIndex = _autoLoadComboBox.Items.Add(
                    new SaveSlotItem { Slot = AUTO_LOAD_NEW_SAVE, Description = "Start a new save in the first free slot" }
                );
                if (currentSelection == AUTO_LOAD_NEW_SAVE) selectIndex = newSaveIndex;

                var discovery = new Manager.Core.Services.SaveDiscoveryService();
                var saves = discovery.DiscoverSaves(_settings.GamePath);
                foreach (var save in saves)
                {
                    var item = new SaveSlotItem { Slot = save.AbsoluteSlot, Description = save.ToString() };
                    int idx = _autoLoadComboBox.Items.Add(item);
                    if (save.AbsoluteSlot == currentSelection) selectIndex = idx;
                }

                _autoLoadComboBox.SelectedIndex = selectIndex;
            }
            catch (Exception ex)
            {
                Log("Error discovering saves: " + ex.Message);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void GameSetupTab_Load(object sender, EventArgs e)
        {
            RefreshSavesList();
        }

        private void LoadGameLogButton_Click(object sender, EventArgs e)
        {
            if (ViewGameLogRequested != null)
                ViewGameLogRequested(this, EventArgs.Empty);
        }

        private void ClearLogButton_Click(object sender, EventArgs e)
        {
            _logTextBox.Clear();
        }

        /// <summary>
        /// Add a message to the activity log
        /// </summary>
        public void Log(string message)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new MethodInvoker(delegate { Log(message); }));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            bool atBottom = _logTextBox.SelectionStart == _logTextBox.TextLength;
            _logTextBox.AppendText("[" + timestamp + "] " + message + "\n");

            if (atBottom)
                _logTextBox.ScrollToCaret();
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            BrowseForGame();
        }

        private void BrowseForGame()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Locate Sheltered.exe";
                dialog.Filter = "Sheltered Executable|Sheltered.exe;ShelteredWindows64_EOS.exe|All Executables|*.exe";
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _gamePathTextBox.Text = dialog.FileName;
                    Log("Game path set via browser: " + dialog.FileName);
                }
            }
        }

        private void DetectGameButton_Click(object sender, EventArgs e)
        {
            Log("Searching for Sheltered installation...");
            var settingsService = new Manager.Core.Services.SettingsService();
            string detected = settingsService.Load().GamePath; // This triggers auto-detect if current is invalid

            if (!string.IsNullOrEmpty(detected) && File.Exists(detected))
            {
                _gamePathTextBox.Text = detected;
                Log("Game found at: " + detected);
            }
            else
            {
                MessageBox.Show("Could not find Sheltered automatically. Please use 'Browse' to locate Sheltered.exe.",
                    "Detection Failed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void GamePathTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_isUpdating) return;

            string path = _gamePathTextBox.Text;
            if (path != null) path = path.Trim();

            if (string.IsNullOrEmpty(path))
            {
                _modsPathTextBox.Text = string.Empty;
                if (_settings != null) _settings.GamePath = string.Empty;
                UpdateStatus(false);
                return;
            }

            // If a directory was provided, try to find the executable within it
            if (Directory.Exists(path) && !File.Exists(path))
            {
                string[] possibleExes = { "Sheltered.exe", "ShelteredWindows64_EOS.exe" };
                foreach (var exe in possibleExes)
                {
                    string fullPath = Path.Combine(path, exe);
                    if (File.Exists(fullPath))
                    {
                        path = fullPath;
                        _isUpdating = true;
                        _gamePathTextBox.Text = path;
                        _isUpdating = false;
                        break;
                    }
                }
            }

            if (File.Exists(path))
            {
                string gameDir = Path.GetDirectoryName(path);
                string modsPath = Path.Combine(gameDir, "mods");

                _modsPathTextBox.Text = modsPath;

                if (_settings != null)
                {
                    _settings.GamePath = path;
                    _settings.ModsPath = modsPath;
                }

                UpdateStatus(true);
                RefreshSavesList();
                if (GamePathChanged != null)
                    GamePathChanged(path);
            }
            else
            {
                _modsPathTextBox.Text = string.Empty;
                UpdateStatus(false);
            }
        }

        private void OpenModsFolderButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_modsPathTextBox.Text))
            {
                try
                {
                    if (!Directory.Exists(_modsPathTextBox.Text))
                        Directory.CreateDirectory(_modsPathTextBox.Text);

                    System.Diagnostics.Process.Start("explorer.exe", _modsPathTextBox.Text);
                    Log("Opened mods folder");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to open folder: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenGameFolderButton_Click(object sender, EventArgs e)
        {
            if (_settings == null) return;

            if (!_settings.IsGamePathValid)
            {
                BrowseForGame();
                return;
            }

            string path = _gamePathTextBox.Text;
            if (string.IsNullOrEmpty(path))
            {
                _settings.GamePath = string.Empty;
                BrowseForGame();
                return;
            }

            try
            {
                string dir = null;
                if (File.Exists(path))
                    dir = Path.GetDirectoryName(path);
                else if (Directory.Exists(path))
                    dir = path;

                if (dir != null)
                {
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                    Log("Opened game folder: " + dir);
                }
                else
                {
                    Log("Cannot open folder: Path is invalid.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open folder: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LaunchButton_Click(object sender, EventArgs e)
        {
            if (LaunchRequested != null)
                LaunchRequested(true);
        }

        private void LaunchVanillaButton_Click(object sender, EventArgs e)
        {
            if (LaunchRequested != null)
                LaunchRequested(false);
        }

        public void UpdateFromSettings()
        {
            if (_settings == null) return;

            _isUpdating = true;
            try
            {
                _gamePathTextBox.Text = _settings.GamePath ?? string.Empty;
                _modsPathTextBox.Text = _settings.ModsPath ?? string.Empty;
                // Show the end of long paths (the executable name) instead of a selected block.
                _gamePathTextBox.Select(_gamePathTextBox.TextLength, 0);
                _gamePathTextBox.ScrollToCaret();
                _modsPathTextBox.Select(_modsPathTextBox.TextLength, 0);
                _modsPathTextBox.ScrollToCaret();
                _toolTip.SetToolTip(_gamePathTextBox, _gamePathTextBox.Text);
                _toolTip.SetToolTip(_modsPathTextBox, _modsPathTextBox.Text);
            }
            finally
            {
                _isUpdating = false;
            }

            UpdateStatus(_settings.IsGamePathValid);

            if (_settings.IsGamePathValid && _settings.GamePath != _lastLoggedPath)
            {
                Log("Loaded configuration - game found");
                _lastLoggedPath = _settings.GamePath;
            }

            RefreshSavesList();
        }

        /// <summary>
        /// Update the status display
        /// </summary>
        public void UpdateStatus(bool isValid, int enabledModCount, string modApiVersion)
        {
            if (isValid)
            {
                _statusLabel.Text = "Game found. Mods load from the folder below.";
                ManagerTheme.SetRole(_statusLabel, ThemeRole.Success);
                _launchButton.Enabled = true;
                _launchVanillaButton.Enabled = true;
                _openGameFolderButton.Text = "Open game folder";
                _modsCountLabel.Text = enabledModCount == 0
                    ? "No mods active. Plain Sheltered will start."
                    : (enabledModCount == 1 ? "1 mod will load" : enabledModCount + " mods will load");
            }
            else
            {
                _statusLabel.Text = "Game not found. Choose Browse and select Sheltered.exe or ShelteredWindows64_EOS.exe.";
                ManagerTheme.SetRole(_statusLabel, ThemeRole.Danger);
                _launchButton.Enabled = false;
                _launchVanillaButton.Enabled = false;
                _openGameFolderButton.Text = "Set game path (.exe)";
                _modsCountLabel.Text = "Set the game path to launch";
            }

            // Loader versions only matter when something is missing; otherwise they sit in a tooltip.
            bool loaderMissing = isValid && (modApiVersion ?? string.Empty).IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0;
            _modApiVersionLabel.Text = loaderMissing ? "Mod loader not detected in the game folder." : string.Empty;
            _modApiVersionLabel.Visible = loaderMissing;
            ManagerTheme.SetRole(_modApiVersionLabel, ThemeRole.Warning);
            _toolTip.SetToolTip(_modsCountLabel, "Mod loader: " + (modApiVersion ?? "unknown"));

            // Once the game is found, Play leads and the setup instructions step aside.
            if (_root != null)
            {
                _root.SuspendLayout();
                _root.SetRow(_playCard, isValid ? 0 : 1);
                _root.SetRow(_installCard, isValid ? 1 : 0);
                _playCard.TabIndex = isValid ? 0 : 1;
                _installCard.TabIndex = isValid ? 1 : 0;
                _installCard.Description = isValid ? null : InstallHelp;
                _root.ResumeLayout(true);
            }
        }

        /// <summary>
        /// Update status with defaults
        /// </summary>
        public void UpdateStatus(bool isValid)
        {
            UpdateStatus(isValid, 0, null);
        }

        /// <summary>
        /// Apply theme
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            ThemePalette palette = ManagerTheme.Current;
            ManagerTheme.Apply(this);
            _logFrame.BackColor = palette.Border;
            _logTextBox.BackColor = palette.Input;
            _logTextBox.ForeColor = palette.Text;
            ManagerTheme.SetRole(_statusLabel, ManagerTheme.GetRole(_statusLabel));
        }
    }
}
