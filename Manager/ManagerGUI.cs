using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Web.Script.Serialization;
using Manager;

/**
 * Author: benjaminfoo
 * Maintainer: Coolnether123
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
        private static readonly string ManagerLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mod_manager.log");

        // Model for mod items shown in lists (Coolnether123)
        private class ModListItem
        {
            public string DisplayName;
            public string Id;              // about id or fallback
            public string RootPath;        // mod folder or dll folder
            public bool IsDirectory;       // true for folder-based mods
            public bool HasAbout;       // About/About.json exists
            public ModTypes.ModAboutInfo About;   // may be null
            public string PreviewPath;     // About/preview.png if exists
            public bool IsEnabled;         // Enabled/disabled status
            public override string ToString() { return DisplayName; }
        }

        private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

        // Dynamic settings controls per key (Coolnether123)
        private readonly Dictionary<string, Control> _settingsControls = new Dictionary<string, Control>(NameComparer);
        private ModListItem _selectedItem = null;
        private bool _orderDirty = false;

        // Dependency evaluation result for color coding
        private HashSet<string> _hardIssueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _softIssueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CheckBox darkModeToggle;



        public ManagerGUI()
        {
            InitializeComponent();

            // Create the dark mode checkbox
            darkModeToggle = new CheckBox();
            darkModeToggle.Text = "Dark Mode";
            darkModeToggle.AutoSize = true;
            darkModeToggle.CheckedChanged += new EventHandler(darkModeToggle_CheckedChanged);
        }

        private void darkModeToggle_CheckedChanged(object sender, EventArgs e)
        {
            ThemeManager.IsDarkMode = darkModeToggle.Checked;
            ThemeManager.ApplyTheme(this);
            SaveSettings(); // Save setting on change
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Position and add the dark mode checkbox
            darkModeToggle.Location = new Point(12, this.ClientSize.Height - darkModeToggle.Height - 12);
            darkModeToggle.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.Controls.Add(darkModeToggle);
            darkModeToggle.BringToFront(); // Ensure it's visible

            // Allow multi-select for batch enable/disable
            try { uiAvailbleModsListView.SelectionMode = SelectionMode.MultiExtended; } catch { }
            try { uiInstalledModsListView.SelectionMode = SelectionMode.MultiExtended; } catch { }
            // Owner-draw to color code misordered mods
            try { uiInstalledModsListView.DrawMode = DrawMode.OwnerDrawFixed; uiInstalledModsListView.DrawItem += uiInstalledModsListView_DrawItem; } catch { }
            try
            {
                uiAvailbleModsListView.SelectedIndexChanged += OnModSelectionChanged;
                uiInstalledModsListView.SelectedIndexChanged += OnModSelectionChanged;
            }
            catch { }

            LoadSettings(); // Load all settings from INI
            updateAvailableMods();

            // Apply the initial theme to all controls
            ThemeManager.IsDarkMode = darkModeToggle.Checked;
            ThemeManager.ApplyTheme(this);

            UpdateModDetails(null);
        }

        /// <summary>
        /// Paths for enabled/disabled mods
        /// </summary>


        /// <summary>
        /// Refreshes the available and installed mods lists
        /// </summary>
        private void updateAvailableMods()
        {
            if (uiModsPath.Text.ToString().Trim().ToLower().Equals(DEFAULT_VALUE.ToLower())) return;

            uiAvailbleModsListView.Items.Clear();
            uiInstalledModsListView.Items.Clear();
            UpdateModDetails(null);

            // Discover all mods
            var allMods = DiscoverModsFromRoot(uiModsPath.Text);

            // Read load order file (Manager-local JSON reader, order-only)
            var currentOrder = ReadOrderFromFile(uiModsPath.Text);

            // Separate enabled and disabled mods based on presence in loadorder `order`.
            var enabledMods = new List<ModListItem>();
            var disabledMods = new List<ModListItem>();

            var enabledSet = new HashSet<string>(currentOrder ?? new string[0], StringComparer.OrdinalIgnoreCase);
            foreach (var mod in allMods)
            {
                if (enabledSet.Contains(mod.Id)) enabledMods.Add(mod); else disabledMods.Add(mod);
            }

            // Populate Available Mods (disabled)
            uiAvailbleModsListView.BeginUpdate();
            uiInstalledModsListView.BeginUpdate();
            try
            {
                foreach (var it in disabledMods) uiAvailbleModsListView.Items.Add(it);
            }
            finally { uiAvailbleModsListView.EndUpdate(); }

            // Populate Installed Mods (enabled) and apply saved order
            var ordered = ApplySavedOrder(enabledMods, currentOrder);
            try
            {
                foreach (var it in ordered) uiInstalledModsListView.Items.Add(it);
            }
            finally { uiInstalledModsListView.EndUpdate(); }

            // Evaluate to update color coding for issues
            try
            {
                var discovered = ToModEntries(ordered);
                var eval = LoadOrderResolver.Evaluate(discovered, currentOrder ?? new string[0]);
                _hardIssueIds = new HashSet<string>(eval.HardIssues ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
                _softIssueIds = new HashSet<string>(eval.SoftIssues ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);

                // Add mods with missing hard dependencies to hard issues for red coloring
                foreach (var errorMsg in eval.MissingHardDependencies)
                {
                    if (ManagerSettings.SkipHarmonyDependencyCheck && errorMsg.ToLowerInvariant().Contains("harmony"))
                    {
                        continue;
                    }
                        

                        var match = System.Text.RegularExpressions.Regex.Match(errorMsg, "^Mod '([^']*)' has a missing hard dependency:");
                    if (match.Success)
                    {
                        _hardIssueIds.Add(match.Groups[1].Value);
                    }
                }
            }
            catch { _hardIssueIds.Clear(); _softIssueIds.Clear(); }

            _orderDirty = false;
            try { uiInstalledModsListView.Invalidate(); } catch { }
        }

        // Convert UI items to lightweight mod info for resolver
        private List<ModTypes.ModInfo> ToModEntries(List<ModListItem> items)
        {
            var list = new List<ModTypes.ModInfo>();
            foreach (var it in items)
            {
                try
                {
                    var aboutPath = Path.Combine(it.RootPath ?? string.Empty, "About\\About.json");
                    list.Add(new ModTypes.ModInfo
                    {
                        Id = (it.Id ?? string.Empty).Trim().ToLowerInvariant(),
                        Name = it.DisplayName,
                        RootPath = it.RootPath,
                        About = it.About
                    });
                }
                catch { }
            }
            return list;
        }

        // Owner-draw for installed list to show dependency issues (red/yellow)
        private void uiInstalledModsListView_DrawItem(object sender, DrawItemEventArgs e)
        {
            try
            {
                e.DrawBackground();
                if (e.Index < 0 || e.Index >= uiInstalledModsListView.Items.Count) return;
                var item = uiInstalledModsListView.Items[e.Index] as ManagerGUI.ModListItem;
                var text = item != null ? item.ToString() : string.Empty;

                var fore = e.ForeColor;
                if (item != null)
                {
                    if (_hardIssueIds.Contains(item.Id)) fore = Color.Red;
                    else if (_softIssueIds.Contains(item.Id)) fore = Color.Goldenrod;
                }

                using (var b = new SolidBrush(fore))
                {
                    e.Graphics.DrawString(text, e.Font, b, e.Bounds);
                }
                e.DrawFocusRectangle();
            }
            catch { }
        }

        // Discover mods in a path (directories first, then legacy loose DLLs). (Coolnether123)
        private List<ModListItem> DiscoverModsFromRoot(string root)
        {
            var list = new List<ModListItem>();
            try
            {
                if (!Directory.Exists(root)) return list;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var name = Path.GetFileName(dir);
                    if (string.Equals(name, "disabled", StringComparison.OrdinalIgnoreCase)) continue; // Skip disabled folder

                    list.Add(BuildItemFromDirectory(dir));
                }
                // No longer discovering loose DLLs directly in the root, as they should be in mod folders.
                // If there's a need for loose DLLs, they should be handled by a specific mod entry.
            }
            catch { } // Ignore exceptions during discovery
            return list;
        }

        private ModListItem BuildItemFromDirectory(string dir)
        {
            ModTypes.ModAboutInfo about;
            string normalizedId, display, preview;
            var hasAbout = ModAboutReader.TryLoad(dir, out about, out normalizedId, out display, out preview);

            var fallbackDisplay = Path.GetFileName(dir);
            var finalDisplay = !string.IsNullOrEmpty(display) ? display : fallbackDisplay;
            var finalId = !string.IsNullOrEmpty(normalizedId) ? normalizedId : finalDisplay.ToLowerInvariant();

            return new ModListItem
            {
                DisplayName = finalDisplay,
                Id = finalId,
                RootPath = dir,
                IsDirectory = true,
                HasAbout = hasAbout,
                About = about,
                PreviewPath = preview,
                IsEnabled = false
            };
        }

        private void OnModSelectionChanged(object sender, EventArgs e)
        {
            try
            {
                var listBox = sender as ListBox;
                var item = listBox != null ? listBox.SelectedItem as ModListItem : null;
                if (item == null)
                {
                    // If nothing selected in the sender, try the other list for single-selection convenience.
                    if (listBox == uiAvailbleModsListView && uiInstalledModsListView.SelectedItem != null)
                        item = uiInstalledModsListView.SelectedItem as ModListItem;
                    else if (listBox == uiInstalledModsListView && uiAvailbleModsListView.SelectedItem != null)
                        item = uiAvailbleModsListView.SelectedItem as ModListItem;
                }
                UpdateModDetails(item);
            }
            catch { UpdateModDetails(null); }
        }

        private void UpdateModDetails(ModListItem item)
        {
            if (item == null)
            {
                ClearModDetails();
                return;
            }

            var about = item.About;
            lblName.Text = about != null && !string.IsNullOrEmpty(about.name) ? about.name : item.DisplayName;
            lblId.Text = about != null && !string.IsNullOrEmpty(about.id) ? about.id : item.Id;
            lblVersion.Text = about != null && !string.IsNullOrEmpty(about.version) ? about.version : "Unknown";

            if (about != null && about.authors != null && about.authors.Length > 0)
                lblAuthors.Text = string.Join(", ", about.authors);
            else
                lblAuthors.Text = "Unknown";

            if (about != null && about.tags != null && about.tags.Length > 0)
                lblTags.Text = string.Join(", ", about.tags);
            else
                lblTags.Text = "None";

            if (about != null && about.dependsOn != null && about.dependsOn.Length > 0)
                lblDependsOn.Text = string.Join(", ", about.dependsOn);
            else
                lblDependsOn.Text = "None";

            lblWebsite.Text = about != null && !string.IsNullOrEmpty(about.website) ? about.website : "None";
            rtbDescription.Text = about != null && !string.IsNullOrEmpty(about.description) ? about.description : "No description provided.";

            try
            {
                if (!string.IsNullOrEmpty(item.PreviewPath) && File.Exists(item.PreviewPath))
                {
                    pbPreview.ImageLocation = item.PreviewPath;
                    pbPreview.Visible = true;
                }
                else
                {
                    pbPreview.Image = null;
                    pbPreview.ImageLocation = null;
                    pbPreview.Visible = false;
                }
            }
            catch { }
        }

        private void ClearModDetails()
        {
            lblName.Text = "Name: -";
            lblId.Text = "ID: -";
            lblVersion.Text = "Version: -";
            lblAuthors.Text = "Authors: -";
            lblTags.Text = "Tags: -";
            lblDependsOn.Text = "Depends On: -";
            lblWebsite.Text = "Website: -";
            rtbDescription.Text = string.Empty;
            try { pbPreview.Image = null; pbPreview.ImageLocation = null; pbPreview.Visible = false; } catch { }
        }

        private List<ModListItem> ApplySavedOrder(List<ModListItem> items, string[] order)
        {
            try
            {
                if (order == null) return items;
                var prio = new Dictionary<string, int>(NameComparer);
                int p = 0; foreach (var id in order) if (!prio.ContainsKey(id)) prio[id] = p++;
                items.Sort(delegate (ModListItem a, ModListItem b)
                {
                    int pa = prio.ContainsKey(a.Id) ? prio[a.Id] : int.MaxValue;
                    int pb = prio.ContainsKey(b.Id) ? prio[b.Id] : int.MaxValue;
                    int c = pa.CompareTo(pb);
                    if (c != 0) return c;
                    return NameComparer.Compare(a.DisplayName, b.DisplayName);
                });
                return items;
            }
            catch { return items; } // Ignore exceptions during order loading
        }

        // INI Settings Helpers
        private Dictionary<string, string> ReadIniSettings()
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string iniPath = GetModManagerIniPath(); // Use helper for path
            if (!File.Exists(iniPath)) return settings;

            try
            {
                foreach (var line in File.ReadAllLines(iniPath))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        settings[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error reading settings from {MOD_MANAGER_INI_FILE}: {ex.Message}", "Settings Error", (System.Windows.Forms.MessageBoxButtons)0,
 System.Windows.Forms.MessageBoxIcon.Error);
            }
            return settings;
        }

        private void WriteIniSettings(Dictionary<string, string> settings)
        {
            try
            {
                var lines = new List<string>();
                foreach (var kvp in settings)
                {
                    lines.Add($"{kvp.Key}={kvp.Value}");
                }
                string iniPath = GetModManagerIniPath(); // Use helper for path
                File.WriteAllLines(iniPath, lines.ToArray());
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error saving settings to {MOD_MANAGER_INI_FILE}: {ex.Message}", "Settings Error", System.Windows.Forms.MessageBoxButtons.OK,
  System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void SaveSettings()
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            settings["GamePath"] = uiGamePath.Text;
            settings["DarkMode"] = darkModeToggle.Checked.ToString();
            settings["GameBitness"] = Program.GameBitness; // Save the detected bitness
            settings["SkipHarmonyDependencyCheck"] = ManagerSettings.SkipHarmonyDependencyCheck.ToString(); // Save new setting

            if (grpDevSettings != null) // Add null check for the parent group box
            {
                // chkDevMode is part of grpSettings, so it should be initialized earlier.
                // However, it's safer to check it if it's being accessed within this block.
                if (chkDevMode != null)
                {
                    settings["DevMode"] = chkDevMode.Checked.ToString();
                }

                settings["LogLevel"] = (chkVerboseLogging != null && chkVerboseLogging.Checked) ? "Debug" : "Info";

                if (clbLogCategories != null) // Check clbLogCategories itself
                {
                    var checkedCategories = clbLogCategories.CheckedItems.Cast<string>().ToArray();
                    settings["LogCategories"] = string.Join(",", checkedCategories);
                }

                if (chkIgnoreOrderChecks != null)
                {
                    settings["IgnoreOrderChecks"] = chkIgnoreOrderChecks.Checked.ToString();
                }
            }

            WriteIniSettings(settings);
        }

        /// <summary>
        /// Loads settings from the ini file and applies them
        /// </summary>
        private void LoadSettings()
        {
            var settings = ReadIniSettings();
            string gamePath = null;
            string darkMode = "false";

            string devMode = "false";
            string logLevel = "Info"; // Default
            string logCategories = "General,Loader,Plugin,Assembly"; // Default
            string ignoreOrderChecks = "false";
                        string gameBitness = null; // New setting
                        string skipHarmonyDependencyCheck = "false"; // New setting
            
                        settings.TryGetValue("GamePath", out gamePath);
            
                        if (string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath))
                        {
                            try
                            {
                                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                var exeDirInfo = new DirectoryInfo(exeDir);
                                DirectoryInfo parentDir = Directory.GetParent(exeDir);
            
                                string gameDir = null;
                                if (exeDirInfo.Name.Equals("sheltered", StringComparison.OrdinalIgnoreCase))
                                {
                                    gameDir = exeDir;
                                }
                                else if (parentDir != null && parentDir.Name.Equals("sheltered", StringComparison.OrdinalIgnoreCase))
                                {
                                    gameDir = parentDir.FullName;
                                }
            
                                if (gameDir != null)
                                {
                                    string[] possibleExeNames = { "Sheltered.exe", "ShelteredWindows64_EOS.exe" };
                                    string foundGameExe = null;
                                    foreach (var exeName in possibleExeNames)
                                    {
                                        string potentialPath = Path.Combine(gameDir, exeName);
                                        if (File.Exists(potentialPath))
                                        {
                                            foundGameExe = potentialPath;
                                            break;
                                        }
                                    }
            
                                    if (foundGameExe != null)
                                    {
                                        gamePath = foundGameExe;
                                        settings["GamePath"] = gamePath;
                                        WriteIniSettings(settings); // Persist auto-detected path
                                    }
                                }
                            }
                            catch { /* Ignore errors during auto-detection */ }
                        }
            
                        settings.TryGetValue("DarkMode", out darkMode);
            
                        settings.TryGetValue("DevMode", out devMode);
                        settings.TryGetValue("LogLevel", out logLevel);
                        settings.TryGetValue("LogCategories", out logCategories);
                        settings.TryGetValue("IgnoreOrderChecks", out ignoreOrderChecks);
                        settings.TryGetValue("GameBitness", out gameBitness); // Read new setting
                        settings.TryGetValue("SkipHarmonyDependencyCheck", out skipHarmonyDependencyCheck); // Read new setting
            
                        if (!string.IsNullOrEmpty(gamePath) && File.Exists(gamePath))
                        {
                            uiGamePath.Text = gamePath;
                            uiModsPath.Text = Path.Combine(Path.GetDirectoryName(gamePath), "mods");
                            Program.GameRootPath = gamePath;
                        }
                        else
                        {
                            uiGamePath.Text = DEFAULT_VALUE;
                            Program.GameRootPath = null;
                        }
            
                        bool isDark;
                        if (bool.TryParse(darkMode, out isDark))
                        {
                            darkModeToggle.Checked = isDark;
                        }
            
                        bool isDev;
                        if (bool.TryParse(devMode, out isDev))
                        {
                            chkDevMode.Checked = isDev;
                        }
            
                        // Load Verbose toggle based on LogLevel
                        chkVerboseLogging.Checked = (logLevel != null && logLevel.Equals("Debug", StringComparison.OrdinalIgnoreCase));
            
                        // Load LogCategories (hard-coded to avoid runtime dependency on ModAPI/MMLog)
                        clbLogCategories.Items.AddRange(new object[] {
                            "General","Loader","Plugin","Assembly","Dependency","Configuration",
                            "Performance","Memory","Scene","UI","Network","IO"
                        });
                        var enabledCategories = new HashSet<string>((logCategories ?? "").Split(','), StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < clbLogCategories.Items.Count; i++)
                        {
                            clbLogCategories.SetItemChecked(i, enabledCategories.Contains(clbLogCategories.Items[i].ToString()));
                        }
                        clbLogCategories.Enabled = chkVerboseLogging != null && chkVerboseLogging.Checked;
            
                        // Ignore order checks toggle visibility/state
                        bool ignore;
                        if (chkIgnoreOrderChecks != null && bool.TryParse(ignoreOrderChecks, out ignore))
                        {
                            chkIgnoreOrderChecks.Checked = ignore;
                            chkIgnoreOrderChecks.Enabled = chkDevMode.Checked;
                        }
            
                        // Set Program.GameBitness from loaded setting
                        Program.GameBitness = gameBitness;
            
                        // Set ManagerSettings.SkipHarmonyDependencyCheck
                        bool isSkipHarmonyDependencyCheck;
                        if (bool.TryParse(skipHarmonyDependencyCheck, out isSkipHarmonyDependencyCheck))
                        {
                            ManagerSettings.SkipHarmonyDependencyCheck = isSkipHarmonyDependencyCheck;
                        }
            
                        uiLaunchButton.Enabled = File.Exists(uiGamePath.Text);
                        uiOpenGameDir.Enabled = File.Exists(uiGamePath.Text);
                    }
            
                    /// <summary>
                    /// Helper to get the absolute path to mod_manager.ini
                    /// </summary>
                    private string GetModManagerIniPath()
                    {
                        string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        return Path.Combine(exeDir, MOD_MANAGER_INI_FILE);
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
                            uiModsPath.Text = Path.Combine(Path.GetDirectoryName(fileDialog.FileName), "mods");
                            Program.GameRootPath = fileDialog.FileName;
                            SaveSettings(); // Save setting on change
                            try { SetupDoorstop(); } catch { }
            
                            // Immediately refresh the mod lists based on the newly selected game/mods path
                            try { updateAvailableMods(); } catch { }
                        }
                    }
                    private void btnApplySettings_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void btnResetSettings_Click(object sender, EventArgs e)
        {
            LoadSettings();
        }

        private void chkDevMode_CheckedChanged(object sender, EventArgs e)
        {
            // Show/hide dev-only settings
            grpDevSettings.Visible = chkDevMode.Checked;
            if (chkIgnoreOrderChecks != null) chkIgnoreOrderChecks.Enabled = chkDevMode.Checked;
            SaveSettings();
        }

        private void chkVerboseLogging_CheckedChanged(object sender, EventArgs e)
        {
            // Enable/disable log category selection
            if (clbLogCategories != null) clbLogCategories.Enabled = chkVerboseLogging.Checked;
            SaveSettings();
        }

        /// <summary>
        /// Writes/updates doorstop_config.ini alongside the game exe to ensure injection.
        /// </summary>
        private void SetupDoorstop()
        {
            try
            {
                if (string.IsNullOrEmpty(uiGamePath.Text) || !File.Exists(uiGamePath.Text)) return;
                string gameDir = Path.GetDirectoryName(uiGamePath.Text);

                // --- FIX 1: COPY THE LOADER FILES ---
                // Copy the entire SMM directory (containing Doorstop.dll, ModAPI.dll, etc.)
                // from the Manager's location to the game's directory.
                var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string managerSmmDir = Path.Combine(baseDir, "SMM");
                string gameSmmDir = Path.Combine(gameDir, "SMM");

                // If Manager.exe itself lives in an SMM folder, use that folder directly.
                if (!Directory.Exists(managerSmmDir))
                {
                    var baseName = Path.GetFileName(baseDir);
                    if (!string.IsNullOrEmpty(baseName) && baseName.Equals("SMM", StringComparison.OrdinalIgnoreCase))
                    {
                        managerSmmDir = baseDir;
                    }
                    else
                    {
                        var parent = Directory.GetParent(baseDir);
                        var sibling = parent != null ? Path.Combine(parent.FullName, "SMM") : null;
                        if (!string.IsNullOrEmpty(sibling) && Directory.Exists(sibling))
                        {
                            managerSmmDir = sibling;
                        }
                    }
                }

                bool samePath = string.Equals(
                    Path.GetFullPath(managerSmmDir ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(gameSmmDir ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);

                if (Directory.Exists(managerSmmDir) && !samePath)
                {
                    SafeLog($"Copying SMM from '{managerSmmDir}' to '{gameSmmDir}'.");
                    CopyDirectory(managerSmmDir, gameSmmDir, true);
                }
                else if (!Directory.Exists(managerSmmDir))
                {
                    SafeLog($"SMM source folder not found near manager (looked for '{managerSmmDir}').");
                    System.Windows.Forms.MessageBox.Show("Could not find the 'SMM' directory alongside the manager. Continuing without copying SMM files; mod loading may fail.",
                        "Configuration Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                }
                else
                {
                    SafeLog("Manager is running from the target SMM folder; skipping copy.");
                }

                // Ensure critical files exist in game SMM\bin
                try
                {
                    var gameBinDir = Path.Combine(gameSmmDir, "bin");
                    var managerBinDir = Path.Combine(managerSmmDir, "bin");
                    Directory.CreateDirectory(gameBinDir);
                    var sourceDoorstop = Path.Combine(managerBinDir, "Doorstop.dll");
                    var targetDoorstop = Path.Combine(gameBinDir, "Doorstop.dll");
                    if (File.Exists(sourceDoorstop))
                    {
                        File.Copy(sourceDoorstop, targetDoorstop, true);
                        SafeLog($"Copied Doorstop to '{targetDoorstop}'.");
                    }
                    else
                    {
                        SafeLog($"Doorstop.dll missing at '{sourceDoorstop}'.");
                    }

                    var sourceModApi = Path.Combine(managerSmmDir, "ModAPI.dll");
                    var targetModApi = Path.Combine(gameSmmDir, "ModAPI.dll");
                    if (File.Exists(sourceModApi))
                    {
                        try
                        {
                            File.Copy(sourceModApi, targetModApi, true);
                            SafeLog($"Copied ModAPI to '{targetModApi}'.");
                        }
                        catch (Exception ex)
                        {
                            SafeLog($"ModAPI copy failed (likely locked) from '{sourceModApi}' to '{targetModApi}': {ex.Message}");
                        }
                    }
                    else
                    {
                        SafeLog($"ModAPI.dll missing at '{sourceModApi}'.");
                    }
                }
                catch (Exception ex)
                {
                    SafeLog("Error ensuring critical files in SMM/bin: " + ex.Message);
                }

                // --- FIX 2: CORRECTLY CONSTRUCT AND USE THE DLL SEARCH PATH ---
                var searchPaths = new List<string>();
                // The root mods folder is a good fallback.
                searchPaths.Add(uiModsPath.Text);

                try
                {
                    // Add the 'Assemblies' folder for each enabled mod.
                    var enabledModIds = new HashSet<string>(ReadOrderFromFile(uiModsPath.Text) ?? new string[0], StringComparer.OrdinalIgnoreCase);
                    var allDiscoveredMods = DiscoverModsFromRoot(uiModsPath.Text);

                    foreach (var mod in allDiscoveredMods)
                    {
                        if (enabledModIds.Contains(mod.Id))
                        {
                            string assembliesPath = Path.Combine(mod.RootPath, "Assemblies");
                            if (Directory.Exists(assembliesPath))
                            {
                                searchPaths.Add(assembliesPath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Could not build detailed assembly search paths: " + ex.Message,
                        "Warning", (System.Windows.Forms.MessageBoxButtons)1, System.Windows.Forms.MessageBoxIcon.Warning);
                }

                // Create a list of relative paths and join them with semicolons.
                var relativePaths = searchPaths
                    .Distinct(StringComparer.OrdinalIgnoreCase) // Avoid duplicates
                    .Select(p => GetRelativePath(gameDir, p))
                    .ToList();

                string dllSearchPath = string.Join(";", relativePaths.ToArray());

                // --- CONFIGURE DOORSTOP.INI ---
                string iniPath = Path.Combine(gameDir, "doorstop_config.ini");
                var ini = new List<string>();
                ini.Add("# Auto-generated by Sheltered Mod Manager");
                ini.Add("[General]");
                ini.Add("enabled=true");
                // IMPORTANT: The target assembly must exist at this path inside the game directory.
                // The file copy logic above ensures this. This path assumes your project structure is:
                // Manager.exe
                // SMM/
                //   bin/
                //     Doorstop.dll
                //   ModAPI.dll
                //   ...
                ini.Add("target_assembly=SMM\\bin\\Doorstop.dll");
                ini.Add("redirect_output_log=true");
                ini.Add("");
                ini.Add("[UnityMono]");
                // Use the correctly built search path here.
                ini.Add("dll_search_path_override=" + dllSearchPath);
                ini.Add("debug_enabled=false");
                ini.Add("debug_address=127.0.0.1:10000");
                ini.Add("debug_suspend=false");
                File.WriteAllLines(iniPath, ini.ToArray());

                // Copy correct bitness winhttp.dll next to the exe
                bool currentIs64Bit = DetectIsExe64Bit(uiGamePath.Text);
                CopyWinhttpForGame(gameDir, currentIs64Bit);

                // Copy mods from project to game directory (this part seems fine)
                try
                {
                    string projectModsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods");
                    string gameModsPath = Path.Combine(gameDir, "mods");
                    if (Directory.Exists(projectModsPath))
                    {
                        CopyDirectory(projectModsPath, gameModsPath, true);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to copy mods to game directory: " + ex.Message, "Mod Sync Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Failed to configure Doorstop: " + ex.Message,
                    "Configuration Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        // Returns true if the specified exe is 64-bit (PE machine AMD64), false if 32-bit (I386)
        private bool DetectIsExe64Bit(string exePath)
        {
            FileStream fs = null; BinaryReader br = null;
            try
            {
                fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                br = new BinaryReader(fs);
                fs.Seek(0x3C, SeekOrigin.Begin);
                int peOffset = br.ReadInt32();
                fs.Seek(peOffset, SeekOrigin.Begin);
                uint peSignature = br.ReadUInt32(); // 'PE\0\0'
                if (peSignature != 0x00004550) return false;
                ushort machine = br.ReadUInt16();
                // 0x8664 = AMD64, 0x014c = I386
                return machine == 0x8664;
            }
            catch { return false; }
            finally { try { if (br != null) br.Close(); } catch { } try { if (fs != null) fs.Close(); } catch { } }
        }

        // Copies the appropriate winhttp.dll (x86/x64) from packaged locations into the game directory
        private void CopyWinhttpForGame(string gameDir, bool is64)
        {
            try
            {
                string target = Path.Combine(gameDir, "winhttp.dll");
                // Always copy to ensure correct bitness and version

                string baseDir = AppDomain.CurrentDomain.BaseDirectory; // Manager base
                List<string> search = new List<string>();

                // Prefer within game SMM\Doorstop\x64|x32 structure
                string smmDir = Path.Combine(gameDir, "SMM");
                string doorstopDir = Path.Combine(smmDir, "Doorstop");
                string dsX64 = Path.Combine(doorstopDir, "x64");
                string dsX32 = Path.Combine(doorstopDir, "x32");
                if (is64)
                {
                    search.Add(Path.Combine(dsX64, "winhttp.dll"));
                }
                else
                {
                    search.Add(Path.Combine(dsX32, "winhttp.dll"));
                }

                // Also check alongside Manager in x64/x32/x86 directories
                if (is64)
                {
                    string baseX64 = Path.Combine(baseDir, "x64");
                    search.Add(Path.Combine(baseX64, "winhttp.dll"));
                    search.Add(Path.Combine(baseDir, "x64\\winhttp.dll"));
                }
                else
                {
                    string baseX32 = Path.Combine(baseDir, "x32");
                    string baseX86 = Path.Combine(baseDir, "x86");
                    search.Add(Path.Combine(baseX32, "winhttp.dll"));
                    search.Add(Path.Combine(baseX86, "winhttp.dll"));
                    search.Add(Path.Combine(baseDir, "x32\\winhttp.dll"));
                    search.Add(Path.Combine(baseDir, "x86\\winhttp.dll"));
                }
                // Generic fallbacks
                search.Add(Path.Combine(baseDir, "winhttp.dll"));
                search.Add(Path.Combine(baseDir, "libs\\winhttp.dll"));

                foreach (string c in search)
                {
                    if (File.Exists(c)) { File.Copy(c, target, true); return; }
                }
            }
            catch { }
        }

        // Helper to get a relative path from one absolute path to another
        private string GetRelativePath(string relativeTo, string path)
        {
            if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                relativeTo += Path.DirectorySeparatorChar;
            }
            Uri uri1 = new Uri(relativeTo);
            Uri uri2 = new Uri(path);

            Uri relativeUri = uri1.MakeRelativeUri(uri2);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        // Helper to recursively copy a directory (skips Manager.exe; logs/ignores locked files)
        private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Name.Equals("Manager.exe", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(targetFilePath, true);
                }
                catch (Exception ex)
                {
                    // Ignore locked files (e.g., ModAPI.dll in use) and continue copying others.
                    SafeLog($"CopyDirectory: skipped '{file.FullName}': {ex.Message}");
                }
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        private void SafeLog(string message)
        {
            try
            {
                File.AppendAllText(ManagerLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
