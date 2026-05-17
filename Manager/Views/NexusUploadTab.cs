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

            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 54;
            topPanel.Padding = new Padding(12, 8, 12, 8);

            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _statusLabel.AutoEllipsis = true;
            topPanel.Controls.Add(_statusLabel);

            leftPanel.Dock = DockStyle.Left;
            leftPanel.Width = 430;
            leftPanel.Padding = new Padding(12);

            authorLabel.Text = "Author";
            authorLabel.Dock = DockStyle.Top;
            authorLabel.Height = 20;
            authorLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            _authorFilter.Dock = DockStyle.Top;
            _authorFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _authorFilter.Height = 28;

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
            _stagePanel.Height = 42;
            ConfigureStageButton(_detailsStageButton, "1 Details", 0);
            ConfigureStageButton(_filesStageButton, "2 Files", 112);
            ConfigureStageButton(_verifyStageButton, "3 Verify", 224);
            ConfigureStageButton(_publishStageButton, "4 Publish", 336);
            _stagePanel.Controls.Add(_detailsStageButton);
            _stagePanel.Controls.Add(_filesStageButton);
            _stagePanel.Controls.Add(_verifyStageButton);
            _stagePanel.Controls.Add(_publishStageButton);

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
            ResumeLayout(false);
            RefreshStage();
        }

        private void ConfigureDetailsPanel()
        {
            _detailsPanel.Dock = DockStyle.Fill;
            _detailsPanel.AutoScroll = true;

            _nameBox = AddTextField(_detailsPanel, "Name", 14, 10, 360);
            _versionBox = AddTextField(_detailsPanel, "Version", 14, 62, 180);
            _gameDomainBox = AddTextField(_detailsPanel, "Game Domain", 210, 62, 150);
            _nexusModIdBox = AddTextField(_detailsPanel, "Nexus Mod ID", 380, 62, 120);
            _authorsBox = AddTextField(_detailsPanel, "Authors", 14, 114, 486);
            _tagsBox = AddTextField(_detailsPanel, "Tags", 14, 166, 486);
            _summaryBox = AddMultilineField(_detailsPanel, "Summary", 14, 218, 486, 72);
            _descriptionBox = AddMultilineField(_detailsPanel, "Description", 14, 316, 486, 150);

            _saveDraftButton.Text = "Save Draft";
            _saveDraftButton.Location = new Point(14, 482);
            _saveDraftButton.Size = new Size(120, 32);
            _detailsPanel.Controls.Add(_saveDraftButton);
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
            _validationBox.ScrollBars = ScrollBars.Vertical;
            _validationBox.ReadOnly = true;
            _verifyPanel.Controls.Add(_verifyButton);
            _verifyPanel.Controls.Add(_validationBox);
        }

        private void ConfigurePublishPanel()
        {
            _publishPanel.Dock = DockStyle.Fill;
            _publishPanel.Visible = false;

            AddLabel(_publishPanel, "Nexus does not expose a documented mod-file publish mutation in the bundled API reference. Use this handoff after the draft is saved, ownership is checked, and the package is built.", 14, 14, 640, false);
            _openNexusButton.Text = "Open Nexus Upload Page";
            _openNexusButton.Location = new Point(14, 82);
            _openNexusButton.Size = new Size(180, 32);
        }

        private void WireEvents()
        {
            _localList.SelectionChanged += LocalList_SelectionChanged;
            _authorFilter.SelectedIndexChanged += delegate { RefreshLocalList(); };
            _refreshOwnedButton.Click += delegate { RefreshOwnedModsAsync(); };
            _saveDraftButton.Click += delegate { SaveCurrentDraft(); };
            _buildPackageButton.Click += delegate { BuildPackage(); };
            _verifyButton.Click += delegate { RunVerification(); };
            _openPackageButton.Click += delegate { OpenPackageFolder(); };
            _openNexusButton.Click += delegate { OpenNexusUploadPage(); };
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
            button.Location = new Point(x, 4);
            button.Size = new Size(104, 32);
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
                else
                {
                    control.ForeColor = ForeColor;
                }
            }
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

        private static TextBox AddTextField(Control parent, string label, int x, int y, int width)
        {
            AddFieldLabel(parent, label, x, y);
            var box = new TextBox();
            box.Location = new Point(x, y + 20);
            box.Size = new Size(width, 24);
            parent.Controls.Add(box);
            return box;
        }

        private static TextBox AddMultilineField(Control parent, string label, int x, int y, int width, int height)
        {
            AddFieldLabel(parent, label, x, y);
            var box = new TextBox();
            box.Location = new Point(x, y + 20);
            box.Size = new Size(width, height);
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Vertical;
            parent.Controls.Add(box);
            return box;
        }

        private static void AddFieldLabel(Control parent, string text, int x, int y)
        {
            var label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(160, 18);
            label.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            parent.Controls.Add(label);
        }
    }
}
