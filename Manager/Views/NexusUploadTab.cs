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
    public class NexusUploadTab : UserControl
    {
        private enum PublishStage { Details, Files, Verify, Publish }

        private Label _statusLabel;
        private ComboBox _authorFilter;
        private Button _refreshOwnedButton;
        private ModListView _localList;
        private ListBox _ownedList;
        private Panel _stagePanel;
        private Button _detailsStageButton;
        private Button _filesStageButton;
        private Button _verifyStageButton;
        private Button _publishStageButton;
        private Panel _detailsPanel;
        private Panel _filesPanel;
        private Panel _verifyPanel;
        private Panel _publishPanel;
        private TextBox _nameBox;
        private TextBox _versionBox;
        private TextBox _gameDomainBox;
        private TextBox _nexusModIdBox;
        private TextBox _existingModFileIdBox;
        private ComboBox _fileCategoryBox;
        private TextBox _authorsBox;
        private TextBox _tagsBox;
        private TextBox _summaryBox;
        private TextBox _descriptionBox;
        private Label _packageLabel;
        private TextBox _validationBox;
        private Label _ownershipLabel;
        private Button _saveDraftButton;
        private Button _buildPackageButton;
        private Button _verifyButton;
        private Button _openPackageButton;
        private Button _openNexusButton;
        private Button _publishApiButton;
        private ToolTip _helpToolTip;

        private NexusModsService _nexusService;
        private AppSettings _settings;
        private NexusAccountStatus _accountStatus;
        private readonly NexusUploadDraftService _draftService = new NexusUploadDraftService();
        private readonly NexusUploadOwnershipService _ownershipService = new NexusUploadOwnershipService();
        private readonly NexusUploadPackageService _packageService = new NexusUploadPackageService();
        private readonly List<ModItem> _installedMods = new List<ModItem>();
        private readonly List<NexusRemoteMod> _ownedMods = new List<NexusRemoteMod>();
        private NexusUploadDraft _currentDraft;
        private NexusOwnershipVerification _currentOwnership;
        private ModItem _currentMod;
        private PublishStage _stage = PublishStage.Details;
        private bool _isDark;

        public event NexusActivityHandler NexusActivity;

        public NexusUploadTab()
        {
            InitializeComponent();
            WireEvents();
        }

        public void Initialize(NexusModsService nexusService, AppSettings settings)
        {
            _nexusService = nexusService;
            _settings = settings;
            RefreshStatus();
            RebuildAuthorFilter();
            RefreshLocalList();
        }

        public void SetAccountStatus(NexusAccountStatus status)
        {
            _accountStatus = status;
            RefreshStatus();
        }

        public void UpdateInstalledMods(List<ModItem> mods)
        {
            _installedMods.Clear();
            if (mods != null)
            {
                for (int i = 0; i < mods.Count; i++)
                    if (mods[i] != null)
                        _installedMods.Add(mods[i]);
            }

            RebuildAuthorFilter();
            RefreshLocalList();
        }

        public void ApplyTheme(bool isDark)
        {
            _isDark = isDark;
            BackColor = isDark ? Color.FromArgb(46, 48, 53) : SystemColors.Control;
            ForeColor = isDark ? Color.White : SystemColors.ControlText;
            _stagePanel.BackColor = BackColor;
            _localList.ApplyTheme(isDark);
            ApplyComboBoxTheme(_authorFilter);
            _ownedList.BackColor = isDark ? Color.FromArgb(38, 40, 44) : SystemColors.Window;
            _ownedList.ForeColor = isDark ? Color.White : SystemColors.WindowText;
            ApplyPanelTheme(_detailsPanel);
            ApplyPanelTheme(_filesPanel);
            ApplyPanelTheme(_verifyPanel);
            ApplyPanelTheme(_publishPanel);
            RefreshStage();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            var topPanel = new Panel();
            var leftPanel = new Panel();
            var rightPanel = new Panel();
            var authorLabel = new Label();
            var ownedLabel = new Label();

            _statusLabel = new Label();
            _authorFilter = new ComboBox();
            _refreshOwnedButton = new Button();
            _localList = new ModListView();
            _ownedList = new ListBox();
            _stagePanel = new Panel();
            _detailsStageButton = new Button();
            _filesStageButton = new Button();
            _verifyStageButton = new Button();
            _publishStageButton = new Button();
            _detailsPanel = new Panel();
            _filesPanel = new Panel();
            _verifyPanel = new Panel();
            _publishPanel = new Panel();
            _saveDraftButton = new Button();
            _buildPackageButton = new Button();
            _verifyButton = new Button();
            _openPackageButton = new Button();
            _openNexusButton = new Button();
            _publishApiButton = new Button();
            _helpToolTip = new ToolTip();

            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 54;
            topPanel.Padding = new Padding(12, 8, 12, 8);

            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _statusLabel.AutoEllipsis = true;
            topPanel.Controls.Add(_statusLabel);

            leftPanel.Dock = DockStyle.Left;
            leftPanel.Width = 380;
            leftPanel.Padding = new Padding(12);

            authorLabel.Text = "Author";
            authorLabel.Dock = DockStyle.Top;
            authorLabel.Height = 20;
            authorLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            _authorFilter.Dock = DockStyle.Top;
            _authorFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _authorFilter.Height = 28;
            ConfigureThemedComboBox(_authorFilter);

            _localList.Dock = DockStyle.Fill;
            _localList.Title = "Local Mods";
            _localList.ShowSearch = true;

            ownedLabel.Text = "Nexus Mods For This Author";
            ownedLabel.Dock = DockStyle.Bottom;
            ownedLabel.Height = 22;
            ownedLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            _ownedList.Dock = DockStyle.Bottom;
            _ownedList.Height = 122;
            _ownedList.Font = new Font("Segoe UI", 9f);

            _refreshOwnedButton.Text = "Check Nexus Ownership";
            _refreshOwnedButton.Dock = DockStyle.Bottom;
            _refreshOwnedButton.Height = 32;

            leftPanel.Controls.Add(_localList);
            leftPanel.Controls.Add(_refreshOwnedButton);
            leftPanel.Controls.Add(_ownedList);
            leftPanel.Controls.Add(ownedLabel);
            leftPanel.Controls.Add(_authorFilter);
            leftPanel.Controls.Add(authorLabel);

            rightPanel.Dock = DockStyle.Fill;
            rightPanel.Padding = new Padding(12);

            _stagePanel.Dock = DockStyle.Top;
            _stagePanel.Height = 44;
            var stageLayout = new TableLayoutPanel();
            stageLayout.Dock = DockStyle.Fill;
            stageLayout.ColumnCount = 4;
            stageLayout.RowCount = 1;
            stageLayout.Padding = new Padding(0, 2, 0, 4);
            for (int i = 0; i < 4; i++)
                stageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            ConfigureStageButton(_detailsStageButton, "1 Details", 0);
            ConfigureStageButton(_filesStageButton, "2 Files", 112);
            ConfigureStageButton(_verifyStageButton, "3 Verify", 224);
            ConfigureStageButton(_publishStageButton, "4 Publish", 336);
            stageLayout.Controls.Add(_detailsStageButton, 0, 0);
            stageLayout.Controls.Add(_filesStageButton, 1, 0);
            stageLayout.Controls.Add(_verifyStageButton, 2, 0);
            stageLayout.Controls.Add(_publishStageButton, 3, 0);
            _stagePanel.Controls.Add(stageLayout);

            ConfigureDetailsPanel();
            ConfigureFilesPanel();
            ConfigureVerifyPanel();
            ConfigurePublishPanel();

            rightPanel.Controls.Add(_publishPanel);
            rightPanel.Controls.Add(_verifyPanel);
            rightPanel.Controls.Add(_filesPanel);
            rightPanel.Controls.Add(_detailsPanel);
            rightPanel.Controls.Add(_stagePanel);

            Controls.Add(rightPanel);
            Controls.Add(leftPanel);
            Controls.Add(topPanel);
            Name = "NexusUploadTab";
            ConfigureToolTips();
            ResumeLayout(false);
            RefreshStage();
        }

        private void ConfigureDetailsPanel()
        {
            _detailsPanel.Dock = DockStyle.Fill;
            _detailsPanel.AutoScroll = false;
            _detailsPanel.Padding = new Padding(0, 4, 0, 0);

            var detailsLayout = new TableLayoutPanel();
            detailsLayout.Dock = DockStyle.Fill;
            detailsLayout.ColumnCount = 4;
            detailsLayout.RowCount = 6;
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84f));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));

            _nameBox = CreateTextBox();
            _versionBox = CreateTextBox();
            _gameDomainBox = CreateTextBox();
            _nexusModIdBox = CreateTextBox();
            _existingModFileIdBox = CreateTextBox();
            _fileCategoryBox = new ComboBox();
            _fileCategoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _fileCategoryBox.Items.Add("main");
            _fileCategoryBox.Items.Add("optional");
            _fileCategoryBox.Items.Add("miscellaneous");
            ConfigureThemedComboBox(_fileCategoryBox);
            _fileCategoryBox.DropDownHeight = 72;
            _authorsBox = CreateTextBox();
            _tagsBox = CreateTextBox();
            _summaryBox = CreateMultilineTextBox();
            _descriptionBox = CreateMultilineTextBox();

            AddLabeledControl(detailsLayout, "Name", _nameBox, 0, 0, 2);
            AddLabeledControl(detailsLayout, "Version", _versionBox, 2, 0, 1);
            AddLabeledControl(detailsLayout, "File Category", _fileCategoryBox, 3, 0, 1);
            AddLabeledControl(detailsLayout, "Game Domain", _gameDomainBox, 0, 1, 1);
            AddLabeledControl(detailsLayout, "Nexus Mod ID", _nexusModIdBox, 1, 1, 1);
            AddLabeledControl(detailsLayout, "Existing Mod File ID", _existingModFileIdBox, 2, 1, 1);
            AddLabeledControl(detailsLayout, "Authors", _authorsBox, 3, 1, 1);
            AddLabeledControl(detailsLayout, "Tags", _tagsBox, 0, 2, 4);
            AddLabeledControl(detailsLayout, "Summary", _summaryBox, 0, 3, 4);
            AddLabeledControl(detailsLayout, "Description", _descriptionBox, 0, 4, 4);

            _saveDraftButton.Text = "Save Draft";
            _saveDraftButton.Dock = DockStyle.Left;
            _saveDraftButton.Margin = new Padding(4, 5, 4, 4);
            _saveDraftButton.Size = new Size(120, 32);
            detailsLayout.Controls.Add(_saveDraftButton, 0, 5);
            detailsLayout.SetColumnSpan(_saveDraftButton, 4);

            _detailsPanel.Controls.Add(detailsLayout);
        }

        private void ConfigureFilesPanel()
        {
            _filesPanel.Dock = DockStyle.Fill;
            _filesPanel.Visible = false;

            var note = AddLabel(_filesPanel, "Build a Nexus-ready ZIP from the selected local mod folder.", 14, 14, 560, true);
            note.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

            _packageLabel = AddLabel(_filesPanel, "Package: not built", 14, 54, 640, false);
            _buildPackageButton.Text = "Build Package";
            _buildPackageButton.Location = new Point(14, 90);
            _buildPackageButton.Size = new Size(130, 32);
            _openPackageButton.Text = "Open Package Folder";
            _openPackageButton.Location = new Point(154, 90);
            _openPackageButton.Size = new Size(160, 32);
            _filesPanel.Controls.Add(_buildPackageButton);
            _filesPanel.Controls.Add(_openPackageButton);
        }

        private void ConfigureVerifyPanel()
        {
            _verifyPanel.Dock = DockStyle.Fill;
            _verifyPanel.Visible = false;

            _ownershipLabel = AddLabel(_verifyPanel, "Ownership: not checked", 14, 14, 640, true);
            _verifyButton.Text = "Run Verification";
            _verifyButton.Location = new Point(14, 46);
            _verifyButton.Size = new Size(130, 32);

            _validationBox = new TextBox();
            _validationBox.Location = new Point(14, 92);
            _validationBox.Size = new Size(640, 260);
            _validationBox.Multiline = true;
            _validationBox.ScrollBars = ScrollBars.None;
            _validationBox.ReadOnly = true;
            _verifyPanel.Controls.Add(_verifyButton);
            _verifyPanel.Controls.Add(_validationBox);
        }

        private void ConfigurePublishPanel()
        {
            _publishPanel.Dock = DockStyle.Fill;
            _publishPanel.Visible = false;

            AddLabel(_publishPanel, "Experimental Nexus publish tools can prepare a manual handoff package. Live API publishing should only be used after validation passes and you are ready to update the selected Nexus file.", 14, 14, 640, false);
            _openNexusButton.Text = "Open Nexus Upload Page";
            _openNexusButton.Location = new Point(14, 82);
            _openNexusButton.Size = new Size(180, 32);
            _publishApiButton.Text = "Publish via Nexus API";
            _publishApiButton.Location = new Point(204, 82);
            _publishApiButton.Size = new Size(170, 32);
            _publishPanel.Controls.Add(_openNexusButton);
            _publishPanel.Controls.Add(_publishApiButton);
        }

        private void WireEvents()
        {
            _localList.SelectionChanged += LocalList_SelectionChanged;
            _ownedList.SelectedIndexChanged += OwnedList_SelectedIndexChanged;
            _authorFilter.SelectedIndexChanged += delegate { RefreshLocalList(); };
            _refreshOwnedButton.Click += delegate { RefreshOwnedModsAsync(); };
            _saveDraftButton.Click += delegate { SaveCurrentDraft(); };
            _buildPackageButton.Click += delegate { BuildPackage(); };
            _verifyButton.Click += delegate { RunVerification(); };
            _openPackageButton.Click += delegate { OpenPackageFolder(); };
            _openNexusButton.Click += delegate { OpenNexusUploadPage(); };
            _publishApiButton.Click += delegate { PublishViaApiAsync(); };
            _detailsStageButton.Click += delegate { SwitchStage(PublishStage.Details); };
            _filesStageButton.Click += delegate { SwitchStage(PublishStage.Files); };
            _verifyStageButton.Click += delegate { SwitchStage(PublishStage.Verify); };
            _publishStageButton.Click += delegate { SwitchStage(PublishStage.Publish); };
        }

        private void LocalList_SelectionChanged(object sender, ModItem item)
        {
            _currentMod = item;
            _currentDraft = item != null ? _draftService.LoadOrCreate(item, _settings) : null;
            _currentOwnership = null;
            PopulateDraftFields();
            RunVerification();
        }

        private void OwnedList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_currentDraft == null || _ownedList.SelectedIndex < 0 || _ownedList.SelectedIndex >= _ownedMods.Count)
                return;

            NexusRemoteMod remote = _ownedMods[_ownedList.SelectedIndex];
            if (remote == null)
                return;

            _currentDraft.GameDomain = remote.GameDomain;
            _currentDraft.NexusModId = remote.ModId;
            _currentDraft.ExistingModFileId = string.Empty;
            if (!string.IsNullOrEmpty(remote.Name))
                _currentDraft.Name = remote.Name;
            if (!string.IsNullOrEmpty(remote.Version))
                _currentDraft.Version = remote.Version;
            if (!string.IsNullOrEmpty(remote.Summary))
                _currentDraft.Summary = remote.Summary;
            if (!string.IsNullOrEmpty(remote.Description))
                _currentDraft.Description = remote.Description;

            PopulateDraftFields();
            RunVerification();
            SwitchStage(PublishStage.Details);
        }

        private void RefreshOwnedModsAsync()
        {
            if (_nexusService == null || _settings == null)
                return;

            string author = GetSelectedAuthor();
            _refreshOwnedButton.Enabled = false;
            EmitActivity("Checking Nexus ownership for " + (!string.IsNullOrEmpty(author) ? author : "selected author") + ".");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                var found = _nexusService.GetModsForUploadOwnership(_accountStatus, _settings.NexusGameDomain, author, out error);

                if (IsDisposed || Disposing)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _refreshOwnedButton.Enabled = true;
                        _ownedMods.Clear();
                        _ownedList.Items.Clear();
                        if (found != null)
                        {
                            for (int i = 0; i < found.Count; i++)
                            {
                                _ownedMods.Add(found[i]);
                                _ownedList.Items.Add(FormatOwnedMod(found[i]));
                            }
                        }

                        if (!string.IsNullOrEmpty(error))
                            EmitActivity("Ownership check failed: " + error);
                        else
                            EmitActivity("Ownership check returned " + _ownedMods.Count + " Nexus mods.");
                        RunVerification();
                    });
                }
                catch { }
            });
        }

        private void SaveCurrentDraft()
        {
            if (_currentMod == null || _currentDraft == null)
                return;

            ReadDraftFields();
            string error;
            if (_draftService.Save(_currentMod, _currentDraft, out error))
                EmitActivity("Saved upload draft for " + _currentMod.DisplayName + ".");
            else
                EmitActivity(error);

            RunVerification();
        }

        private void BuildPackage()
        {
            if (_currentMod == null || _currentDraft == null)
                return;

            SaveCurrentDraft();
            string error;
            NexusUploadPackageResult result = _packageService.BuildPackage(_currentMod, _currentDraft, out error);
            if (result == null)
            {
                EmitActivity(error);
                _packageLabel.Text = "Package: failed";
                return;
            }

            _currentDraft.PackagePath = result.PackagePath;
            _draftService.Save(_currentMod, _currentDraft, out error);
            _packageLabel.Text = "Package: " + result.PackagePath + " (" + result.FileCount + " files, " + FormatBytes(result.SizeBytes) + ")";
            EmitActivity("Built Nexus upload package: " + result.PackagePath);
            RunVerification();
        }

        private void PublishViaApiAsync()
        {
            if (_currentMod == null || _currentDraft == null || _nexusService == null)
                return;

            SaveCurrentDraft();
            ReadDraftFields();

            if (_settings == null || !_settings.EnableNexusIntegration || !_settings.EnableExperimentalPublishTab)
            {
                EmitActivity("Publish blocked: enable Nexus integration and the experimental Publish tab first.");
                return;
            }

            _currentOwnership = _ownershipService.Verify(_currentMod, _currentDraft, _accountStatus, _ownedMods);
            NexusUploadValidationReport report = _draftService.Validate(_currentMod, _currentDraft, _currentOwnership);
            _validationBox.Text = FormatValidation(report);
            _ownershipLabel.Text = "Ownership: " + _currentOwnership.Summary;
            if (!report.CanPublish)
            {
                EmitActivity("Publish blocked: resolve validation errors before using Nexus API publish.");
                return;
            }

            if (!ConfirmApiPublish())
                return;

            _publishApiButton.Enabled = false;
            EmitActivity("Publishing through Nexus v3 API.");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                NexusUploadPublishResult result = _nexusService.PublishPackage(_currentDraft, out error);

                if (IsDisposed || Disposing)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _publishApiButton.Enabled = true;
                        if (!string.IsNullOrEmpty(error) || result == null)
                        {
                            EmitActivity("Publish failed: " + (!string.IsNullOrEmpty(error) ? error : "Unknown Nexus v3 publish error."));
                            return;
                        }

                        EmitActivity(result.Summary);
                        _validationBox.Text = result.Summary + Environment.NewLine + _validationBox.Text;
                    });
                }
                catch { }
            });
        }

        private bool ConfirmApiPublish()
        {
            string message =
                "Publish this package through the Nexus API?\n\n" +
                "Mod: " + (_currentDraft.Name ?? string.Empty) + "\n" +
                "Nexus mod ID: " + _currentDraft.NexusModId + "\n" +
                "Version: " + (_currentDraft.Version ?? string.Empty) + "\n" +
                "Package: " + (_currentDraft.PackagePath ?? string.Empty) + "\n\n" +
                "This experimental action may create or update a public Nexus file.";

            return MessageBox.Show(this, message, "Confirm Experimental Nexus Publish",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void RunVerification()
        {
            if (_currentMod == null || _currentDraft == null)
            {
                _ownershipLabel.Text = "Ownership: select a local mod";
                _validationBox.Text = string.Empty;
                return;
            }

            ReadDraftFields();
            _currentOwnership = _ownershipService.Verify(_currentMod, _currentDraft, _accountStatus, _ownedMods);
            _ownershipLabel.Text = "Ownership: " + _currentOwnership.Summary;

            NexusUploadValidationReport report = _draftService.Validate(_currentMod, _currentDraft, _currentOwnership);
            _validationBox.Text = FormatValidation(report);
        }

        private void OpenPackageFolder()
        {
            string path = _currentDraft != null ? _currentDraft.PackagePath : string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try { System.Diagnostics.Process.Start("explorer.exe", Path.GetDirectoryName(path)); }
            catch { }
        }

        private void OpenNexusUploadPage()
        {
            if (_currentDraft == null)
                return;

            string url = _packageService.BuildUploadPageUrl(_currentDraft);
            if (string.IsNullOrEmpty(url))
                return;

            try { System.Diagnostics.Process.Start(url); }
            catch { }
        }

        private void PopulateDraftFields()
        {
            NexusUploadDraft draft = _currentDraft;
            _nameBox.Text = draft != null ? draft.Name : string.Empty;
            _versionBox.Text = draft != null ? draft.Version : string.Empty;
            _gameDomainBox.Text = draft != null ? draft.GameDomain : string.Empty;
            _nexusModIdBox.Text = draft != null && draft.NexusModId > 0 ? draft.NexusModId.ToString() : string.Empty;
            _existingModFileIdBox.Text = draft != null ? draft.ExistingModFileId : string.Empty;
            _fileCategoryBox.SelectedItem = draft != null && !string.IsNullOrEmpty(draft.FileCategory) ? draft.FileCategory : "main";
            if (_fileCategoryBox.SelectedIndex < 0)
                _fileCategoryBox.SelectedItem = "main";
            _authorsBox.Text = draft != null ? draft.AuthorsText : string.Empty;
            _tagsBox.Text = draft != null ? draft.TagsText : string.Empty;
            _summaryBox.Text = draft != null ? draft.Summary : string.Empty;
            _descriptionBox.Text = draft != null ? draft.Description : string.Empty;
            _packageLabel.Text = draft != null && !string.IsNullOrEmpty(draft.PackagePath)
                ? "Package: " + draft.PackagePath
                : "Package: not built";
        }

        private void ReadDraftFields()
        {
            if (_currentDraft == null)
                return;

            int modId;
            _currentDraft.Name = _nameBox.Text.Trim();
            _currentDraft.Version = _versionBox.Text.Trim();
            _currentDraft.GameDomain = _gameDomainBox.Text.Trim().ToLowerInvariant();
            _currentDraft.NexusModId = int.TryParse(_nexusModIdBox.Text.Trim(), out modId) ? modId : 0;
            _currentDraft.ExistingModFileId = _existingModFileIdBox.Text.Trim();
            _currentDraft.FileCategory = Convert.ToString(_fileCategoryBox.SelectedItem);
            _currentDraft.AuthorsText = _authorsBox.Text.Trim();
            _currentDraft.TagsText = _tagsBox.Text.Trim();
            _currentDraft.Summary = _summaryBox.Text.Trim();
            _currentDraft.Description = _descriptionBox.Text.Trim();
        }

        private void RebuildAuthorFilter()
        {
            string previous = GetSelectedAuthor();
            var authors = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            authors["All Authors"] = string.Empty;

            for (int i = 0; i < _installedMods.Count; i++)
            {
                string[] modAuthors = _installedMods[i].Authors;
                if (modAuthors == null || modAuthors.Length == 0)
                    authors["Unknown"] = "Unknown";
                else
                {
                    for (int j = 0; j < modAuthors.Length; j++)
                    {
                        string author = modAuthors[j];
                        if (!string.IsNullOrEmpty(author))
                            authors[author] = author;
                    }
                }
            }

            _authorFilter.Items.Clear();
            foreach (KeyValuePair<string, string> author in authors)
                _authorFilter.Items.Add(author.Key);

            int index = 0;
            if (!string.IsNullOrEmpty(previous))
            {
                for (int i = 0; i < _authorFilter.Items.Count; i++)
                {
                    if (string.Equals(Convert.ToString(_authorFilter.Items[i]), previous, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (_authorFilter.Items.Count > 0)
                _authorFilter.SelectedIndex = index;
        }

        private void RefreshLocalList()
        {
            string author = GetSelectedAuthor();
            var filtered = new List<ModItem>();
            for (int i = 0; i < _installedMods.Count; i++)
            {
                ModItem mod = _installedMods[i];
                if (string.IsNullOrEmpty(author) || HasAuthor(mod, author))
                    filtered.Add(mod);
            }

            filtered.Sort(delegate (ModItem a, ModItem b)
            {
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            _localList.SetItems(filtered);
        }

        private string GetSelectedAuthor()
        {
            string value = _authorFilter.SelectedItem as string;
            if (string.IsNullOrEmpty(value) || value == "All Authors")
                return string.Empty;
            return value;
        }

        private void SwitchStage(PublishStage stage)
        {
            _stage = stage;
            RefreshStage();
        }

        private void RefreshStage()
        {
            _detailsPanel.Visible = _stage == PublishStage.Details;
            _filesPanel.Visible = _stage == PublishStage.Files;
            _verifyPanel.Visible = _stage == PublishStage.Verify;
            _publishPanel.Visible = _stage == PublishStage.Publish;
            ApplyStageButton(_detailsStageButton, _stage == PublishStage.Details);
            ApplyStageButton(_filesStageButton, _stage == PublishStage.Files);
            ApplyStageButton(_verifyStageButton, _stage == PublishStage.Verify);
            ApplyStageButton(_publishStageButton, _stage == PublishStage.Publish);
        }

        private void RefreshStatus()
        {
            string domain = _settings != null ? _settings.NexusGameDomain : string.Empty;
            string account = _accountStatus != null ? _accountStatus.GetMembershipLabel() : "not checked";
            _statusLabel.Text = "Nexus Publish: " + (string.IsNullOrEmpty(domain) ? "no game domain" : domain) + " | Account: " + account;
        }

        private void EmitActivity(string message)
        {
            if (NexusActivity != null && !string.IsNullOrEmpty(message))
                NexusActivity(message);
        }

        private static bool HasAuthor(ModItem mod, string author)
        {
            if (mod == null || string.IsNullOrEmpty(author))
                return false;
            if (mod.Authors == null || mod.Authors.Length == 0)
                return string.Equals(author, "Unknown", StringComparison.OrdinalIgnoreCase);
            for (int i = 0; i < mod.Authors.Length; i++)
                if (string.Equals(mod.Authors[i], author, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string FormatOwnedMod(NexusRemoteMod mod)
        {
            if (mod == null)
                return string.Empty;
            return mod.Name + " | " + mod.GameDomain + "/mods/" + mod.ModId + " | " + mod.Version;
        }

        private static string FormatValidation(NexusUploadValidationReport report)
        {
            if (report == null)
                return string.Empty;

            var sb = new StringBuilder();
            if (report.Errors.Count == 0 && report.Warnings.Count == 0)
                sb.AppendLine("Ready for Nexus handoff.");

            for (int i = 0; i < report.Errors.Count; i++)
                sb.AppendLine("Error: " + report.Errors[i]);
            for (int i = 0; i < report.Warnings.Count; i++)
                sb.AppendLine("Warning: " + report.Warnings[i]);
            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes > 1024 * 1024)
                return (bytes / (1024d * 1024d)).ToString("0.0") + " MB";
            if (bytes > 1024)
                return (bytes / 1024d).ToString("0.0") + " KB";
            return bytes + " bytes";
        }

        private static void ConfigureStageButton(Button button, string text, int x)
        {
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4, 2, 4, 2);
            button.FlatStyle = FlatStyle.Flat;
        }

        private void ApplyStageButton(Button button, bool selected)
        {
            button.BackColor = selected ? Color.FromArgb(0, 122, 204) : (_isDark ? Color.FromArgb(58, 60, 64) : SystemColors.Control);
            button.ForeColor = selected ? Color.White : (_isDark ? Color.White : SystemColors.ControlText);
        }

        private void ApplyPanelTheme(Control parent)
        {
            parent.BackColor = BackColor;
            parent.ForeColor = ForeColor;
            foreach (Control control in parent.Controls)
            {
                if (control is TextBox)
                {
                    control.BackColor = _isDark ? Color.FromArgb(38, 40, 44) : SystemColors.Window;
                    control.ForeColor = _isDark ? Color.White : SystemColors.WindowText;
                }
                else if (control is Button)
                {
                    control.BackColor = _isDark ? Color.FromArgb(58, 60, 64) : SystemColors.Control;
                    control.ForeColor = _isDark ? Color.White : SystemColors.ControlText;
                }
                else if (control is ComboBox)
                {
                    ApplyComboBoxTheme((ComboBox)control);
                }
                else
                {
                    control.ForeColor = ForeColor;
                }

                if (control.HasChildren)
                    ApplyPanelTheme(control);
            }
        }

        private void ConfigureThemedComboBox(ComboBox comboBox)
        {
            comboBox.Dock = DockStyle.Top;
            comboBox.Height = 24;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.DrawMode = DrawMode.OwnerDrawFixed;
            comboBox.ItemHeight = 22;
            comboBox.IntegralHeight = false;
            comboBox.DropDownHeight = 180;
            comboBox.DrawItem += ThemedComboBox_DrawItem;
        }

        private void ApplyComboBoxTheme(ComboBox comboBox)
        {
            if (comboBox == null)
                return;

            comboBox.BackColor = _isDark ? Color.FromArgb(38, 40, 44) : SystemColors.Window;
            comboBox.ForeColor = _isDark ? Color.White : SystemColors.WindowText;
            comboBox.Invalidate();
        }

        private void ThemedComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox == null)
                return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool editArea = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
            if (e.Index < 0)
                editArea = true;
            bool highlightItem = selected && comboBox.DroppedDown && !editArea;
            Color backColor = highlightItem
                ? Color.FromArgb(0, 122, 204)
                : (_isDark ? Color.FromArgb(38, 40, 44) : SystemColors.Window);
            Color foreColor = highlightItem
                ? Color.White
                : (_isDark ? Color.White : SystemColors.WindowText);

            using (var brush = new SolidBrush(backColor))
                e.Graphics.FillRectangle(brush, e.Bounds);

            string text = editArea ? comboBox.Text : Convert.ToString(comboBox.Items[e.Index]);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                e.Font != null ? e.Font : comboBox.Font,
                new Rectangle(e.Bounds.Left + 4, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height),
                foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static TextBox CreateTextBox()
        {
            var box = new TextBox();
            box.Dock = DockStyle.Top;
            box.Height = 24;
            return box;
        }

        private static TextBox CreateMultilineTextBox()
        {
            var box = new TextBox();
            box.Dock = DockStyle.Fill;
            box.Multiline = true;
            box.ScrollBars = ScrollBars.None;
            box.WordWrap = true;
            return box;
        }

        private static void AddLabeledControl(TableLayoutPanel parent, string labelText, Control editor, int column, int row, int columnSpan)
        {
            var container = new Panel();
            container.Dock = DockStyle.Fill;
            container.Margin = new Padding(4);

            var label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Top;
            label.Height = 18;
            label.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            editor.Margin = new Padding(0);
            container.Controls.Add(editor);
            container.Controls.Add(label);
            parent.Controls.Add(container, column, row);
            if (columnSpan > 1)
                parent.SetColumnSpan(container, columnSpan);
        }

        private void ConfigureToolTips()
        {
            _helpToolTip.AutoPopDelay = 12000;
            _helpToolTip.InitialDelay = 350;
            _helpToolTip.ReshowDelay = 200;
            _helpToolTip.ShowAlways = true;

            _helpToolTip.SetToolTip(_statusLabel, "Shows the Nexus game domain and whether the OAuth session is connected to an account.");
            _helpToolTip.SetToolTip(_authorFilter, "Filter local mods by author before checking Nexus ownership.");
            _helpToolTip.SetToolTip(_localList, "Pick the local mod whose Nexus draft you want to prepare.");
            _helpToolTip.SetToolTip(_refreshOwnedButton, "Loads Nexus mods owned by the selected author so the draft can be linked to the right Nexus page.");
            _helpToolTip.SetToolTip(_ownedList, "Selecting an owned Nexus mod copies its known Nexus details into the draft.");
            _helpToolTip.SetToolTip(_detailsStageButton, "Review metadata that will be saved into the upload draft.");
            _helpToolTip.SetToolTip(_filesStageButton, "Build or open the ZIP package prepared from the selected local mod.");
            _helpToolTip.SetToolTip(_verifyStageButton, "Check ownership, required fields, and package readiness before publishing.");
            _helpToolTip.SetToolTip(_publishStageButton, "Open Nexus handoff actions once the draft and package are ready.");
            _helpToolTip.SetToolTip(_nameBox, "Display name for the Nexus file draft.");
            _helpToolTip.SetToolTip(_versionBox, "Version string Nexus users will see for this upload.");
            _helpToolTip.SetToolTip(_fileCategoryBox, "Nexus file bucket: main for primary releases, optional for add-ons, miscellaneous for supporting files.");
            _helpToolTip.SetToolTip(_gameDomainBox, "Nexus game domain used in upload URLs. Sheltered uses 'sheltered'.");
            _helpToolTip.SetToolTip(_nexusModIdBox, "Numeric Nexus mod page ID. Pick an owned Nexus mod to fill this automatically.");
            _helpToolTip.SetToolTip(_existingModFileIdBox, "Optional Nexus v3 mod file ID. When set, publishing creates a new version of this file through POST /mod-files/{id}/versions.");
            _helpToolTip.SetToolTip(_authorsBox, "Comma-separated authors saved with the draft.");
            _helpToolTip.SetToolTip(_tagsBox, "Comma-separated tags saved with the draft.");
            _helpToolTip.SetToolTip(_summaryBox, "Short changelog-style summary for the upload.");
            _helpToolTip.SetToolTip(_descriptionBox, "Longer description or release notes for the Nexus file.");
            _helpToolTip.SetToolTip(_saveDraftButton, "Save these details to the mod's local Nexus upload draft.");
            _helpToolTip.SetToolTip(_buildPackageButton, "Create the Nexus-ready ZIP using the current draft and selected mod folder.");
            _helpToolTip.SetToolTip(_openPackageButton, "Open the folder containing the generated upload ZIP.");
            _helpToolTip.SetToolTip(_verifyButton, "Refresh validation results for ownership, metadata, and package state.");
            _helpToolTip.SetToolTip(_validationBox, "Shows publish blockers and warnings for the selected mod draft.");
            _helpToolTip.SetToolTip(_openNexusButton, "Open the Nexus upload page for manual handoff.");
            _helpToolTip.SetToolTip(_publishApiButton, "Attempt publishing through the configured Nexus API service.");
        }

        private static Label AddLabel(Control parent, string text, int x, int y, int width, bool autoEllipsis)
        {
            var label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, 38);
            label.AutoEllipsis = autoEllipsis;
            parent.Controls.Add(label);
            return label;
        }

    }
}
