using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Web.Script.Serialization;
using ModAPI;


/**
 * Author: benjaminfoo
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

        // Model for mod items shown in lists (Coolnether123)
        private class ModListItem
        {
            public string DisplayName;
            public string Id;              // about id or fallback
            public string RootPath;        // mod folder or dll folder
            public bool IsDirectory;       // true for folder-based mods
            public bool HasAbout;       // About/About.json exists
            public ModAbout About;   // may be null
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
            
            LoadSettings(); // Load all settings from INI
            updateAvailableMods();

            // Apply the initial theme to all controls
            ThemeManager.IsDarkMode = darkModeToggle.Checked;
            ThemeManager.ApplyTheme(this);
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

        // Convert UI items to ModAPI ModEntry list for resolver
        private List<ModEntry> ToModEntries(List<ModListItem> items)
        {
            var list = new List<ModEntry>();
            foreach (var it in items)
            {
                try
                {
                    var aboutPath = Path.Combine(it.RootPath ?? string.Empty, "About\\About.json");
                    list.Add(new ModEntry
                    {
                        Id = (it.Id ?? string.Empty).Trim().ToLowerInvariant(),
                        Name = it.DisplayName,
                        Version = it.About != null ? it.About.version : null,
                        RootPath = it.RootPath,
                        AboutPath = File.Exists(aboutPath) ? aboutPath : null,
                        AssembliesPath = Path.Combine(it.RootPath ?? string.Empty, "Assemblies"),
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
            } catch { } // Ignore exceptions during discovery
            return list;
        }

        private ModListItem BuildItemFromDirectory(string dir)
        {
            var aboutDir = Path.Combine(dir, "About");
            var aboutJson = Path.Combine(aboutDir, "About.json");
            ModAbout about = null;
            string id = null, display = Path.GetFileName(dir);
            string preview = null;
            if (File.Exists(aboutJson))
            {
                try
                {
                    var text = File.ReadAllText(aboutJson);
                    about = new JavaScriptSerializer().Deserialize<ModAbout>(text);
                    if (about != null)
                    {
                        id = string.IsNullOrEmpty(about.id) ? null : about.id.Trim().ToLowerInvariant();
                        display = string.IsNullOrEmpty(about.name) ? display : about.name;
                    }
                }
                catch { } // Ignore exceptions during JSON deserialization
                var prev = Path.Combine(aboutDir, "preview.png");
                if (File.Exists(prev)) preview = prev;
            }
            return new ModListItem
            {
                DisplayName = display,
                Id = id ?? display.ToLowerInvariant(),
                RootPath = dir,
                IsDirectory = true,
                HasAbout = File.Exists(aboutJson),
                About = about,
                PreviewPath = preview,
                IsEnabled = false
            };
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
            if (!File.Exists(MOD_MANAGER_INI_FILE)) return settings;

            try
            {
                foreach (var line in File.ReadAllLines(MOD_MANAGER_INI_FILE))
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
                System.Windows.Forms.MessageBox.Show($"Error reading settings from {MOD_MANAGER_INI_FILE}: {ex.Message}", "Settings Error", System.Windows.Forms.MessageBoxButtons.OK,
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
                File.WriteAllLines(MOD_MANAGER_INI_FILE, lines.ToArray());
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

            // --- ADD THIS SECTION ---
            if (grpDevSettings != null) // Add null check for the parent group box
            {
                // chkDevMode is part of grpSettings, so it should be initialized earlier.
                // However, it's safer to check it if it's being accessed within this block.
                if (chkDevMode != null)
                {
                    settings["DevMode"] = chkDevMode.Checked.ToString();
                }

                if (cmbLogLevel != null && cmbLogLevel.SelectedItem != null)
                {
                    settings["LogLevel"] = cmbLogLevel.SelectedItem.ToString();
                }
                
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
            // -----------------------

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
            // --- ADD THESE ---
            string devMode = "false";
            string logLevel = "Info"; // Default
            string logCategories = "General,Loader,Plugin,Assembly"; // Default
            string ignoreOrderChecks = "false";
            // -----------------

            settings.TryGetValue("GamePath", out gamePath);
            settings.TryGetValue("DarkMode", out darkMode);
            // --- ADD THESE ---
            settings.TryGetValue("DevMode", out devMode);
            settings.TryGetValue("LogLevel", out logLevel);
            settings.TryGetValue("LogCategories", out logCategories);
            settings.TryGetValue("IgnoreOrderChecks", out ignoreOrderChecks);
            // -----------------

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
            
            // --- ADD THIS SECTION ---
            bool isDev;
            if (bool.TryParse(devMode, out isDev))
            {
                chkDevMode.Checked = isDev;
            }

            // Load LogLevel
            cmbLogLevel.Items.AddRange((string[])Enum.GetNames(typeof(MMLog.LogLevel))); // Add this line
            int logLevelIndex = cmbLogLevel.FindStringExact(logLevel);
            cmbLogLevel.SelectedIndex = logLevelIndex >= 0 ? logLevelIndex : cmbLogLevel.FindStringExact("Info");

            // Load LogCategories
            clbLogCategories.Items.AddRange((string[])Enum.GetNames(typeof(MMLog.LogCategory))); // Add this line
            var enabledCategories = new HashSet<string>((logCategories ?? "").Split(','), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < clbLogCategories.Items.Count; i++)
            {
                clbLogCategories.SetItemChecked(i, enabledCategories.Contains(clbLogCategories.Items[i].ToString()));
            }
            // -----------------------

            // Ignore order checks toggle visibility/state
            bool ignore;
            if (chkIgnoreOrderChecks != null && bool.TryParse(ignoreOrderChecks, out ignore))
            {
                chkIgnoreOrderChecks.Checked = ignore;
                chkIgnoreOrderChecks.Enabled = chkDevMode.Checked;
            }

            uiLaunchButton.Enabled = File.Exists(uiGamePath.Text);
            uiOpenGameDir.Enabled = File.Exists(uiGamePath.Text);
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
            }
        }

        /// <summary>
        /// Open UnityDoorstop link
        /// </summary>
        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(doorstopLink.Text);
        }

        /// <summary>
        /// Open Sheltered Steam link
        /// </summary>
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(shelteredLink.Text);
        }

        /// <summary>
        /// Open Harmony link
        /// </summary>
        private void linkLabel1_LinkClicked_1(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(harmonyLink.Text);
        }

        /// <summary>
        /// Launch the game
        /// </summary>
        private void onLaunchClicked(object sender, EventArgs e)
        {
            // Save any pending settings changes before launch
            SaveSettings();

            try
            {
                // Read settings (dev mode + order checks)
                var settings = ReadIniSettings();
                string devMode, logLevel, logCategories, ignoreOrderChecks;
                settings.TryGetValue("DevMode", out devMode);
                settings.TryGetValue("LogLevel", out logLevel);
                settings.TryGetValue("LogCategories", out logCategories);
                settings.TryGetValue("IgnoreOrderChecks", out ignoreOrderChecks);

                bool isDev = false; bool.TryParse(devMode ?? "false", out isDev);
                bool bypass = false; if (isDev) bool.TryParse(ignoreOrderChecks ?? "false", out bypass);

                if (!bypass)
                {
                    // Evaluate current order for issues
                    var currentOrder = ReadOrderFromFile(uiModsPath.Text) ?? new string[0];
                    var enabledItems = uiInstalledModsListView.Items.Cast<ManagerGUI.ModListItem>().ToList();
                    var discovered = ToModEntries(enabledItems);
                    var eval = LoadOrderResolver.Evaluate(discovered, currentOrder);
                    var hasIssues = (eval.HardIssues != null && eval.HardIssues.Count > 0)
                                  || (eval.SoftIssues != null && eval.SoftIssues.Count > 0)
                                  || (eval.MissingHardDependencies != null && eval.MissingHardDependencies.Count > 0);
                    if (hasIssues)
                    {
                        var harmonyMissing = eval.MissingHardDependencies.Any(depMsg =>
                            System.Text.RegularExpressions.Regex.IsMatch(depMsg, "missing hard dependency: '0Harmony'", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                            System.Text.RegularExpressions.Regex.IsMatch(depMsg, "missing hard dependency: 'Lib.Harmony'", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                        );

                        var msg = "Mod order has dependency issues.\n\n" +
                                  (eval.HardIssues.Count > 0 ? ("Hard issues: " + string.Join(", ", eval.HardIssues.ToArray()) + "\n") : "") +
                                  (eval.SoftIssues.Count > 0 ? ("Soft issues: " + string.Join(", ", eval.SoftIssues.ToArray()) + "\n") : "") +
                                  (eval.MissingHardDependencies.Count > 0 ? ("Missing deps: " + string.Join(" | ", eval.MissingHardDependencies.ToArray()) + "\n") : "");

                        if (harmonyMissing)
                        {
                            msg += "\nNote: If '0Harmony' is listed as a missing dependency, ModAPI will attempt to load it automatically regardless of its position in the load order.\n";
                        }

                        msg += "\nYes = Load as is\nNo = Auto sort and load\nCancel = Abort";

                        var choice = System.Windows.Forms.MessageBox.Show(msg, "Load Order Warning", System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Warning);
                        if (choice == DialogResult.Cancel)
                        {
                            try { this.Close(); } catch { }
                            return; // abort and close
                        }
                        if (choice == DialogResult.No)
                        {
                            try { WriteOrderToFile(uiModsPath.Text, eval.SortedIds ?? new List<string>()); }
                            catch { }
                            updateAvailableMods();
                        }
                        // Yes => proceed as is
                    }
                }

                // Write mod_manager.ini into the game directory so ModAPI can read logging settings
                try
                {
                    var gameDir = Path.GetDirectoryName(uiGamePath.Text);
                    var iniPath = System.IO.Path.Combine(System.IO.Path.Combine(gameDir, "SMM"), "mod_manager.ini");
                    var lines = new List<string>();
                    lines.Add("# Generated by Manager at launch to handle user settings");
                    lines.Add("DevMode=" + (isDev ? "true" : "false"));
                    lines.Add("LogLevel=" + (logLevel ?? "Info"));
                    lines.Add("LogCategories=" + (logCategories ?? "General,Loader,Plugin,Assembly"));
                    System.IO.File.WriteAllLines(iniPath, lines.ToArray());
                }
                catch { }

                var startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = uiGamePath.Text;
                startInfo.WorkingDirectory = Path.GetDirectoryName(uiGamePath.Text);
                startInfo.UseShellExecute = false;
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to launch Sheltered: {ex.Message}", "Launch Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// When the game path text changes, update ini and buttons
        /// </summary>
        private void uiGamePath_TextChanged_1(object sender, EventArgs e)
        {
            if (uiGamePath.Text.Length == 0) return;
            if (uiGamePath.Text.ToString().Trim().ToLower().Equals(DEFAULT_VALUE.ToLower())) return;

            uiLaunchButton.Enabled = File.Exists(uiGamePath.Text);
            uiOpenGameDir.Enabled = File.Exists(uiGamePath.Text);
            SaveSettings(); // Save setting on change
        }

        private void uiInstaledLabel_Click(object sender, EventArgs e) { }

        private void uiInstalledModsListView_SelectedIndexChanged(object sender, EventArgs e) {
            var item = uiInstalledModsListView.SelectedItem as ManagerGUI.ModListItem;
            if (item == null) return;
            _selectedItem = item;
            UpdateDetailsPanel(item);
            RebuildSettingsPanel(item);
        }

        private void tabPage1_Click(object sender, EventArgs e) { }

        /// <summary>
        /// Enable mod (move from disabled to enabled)
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            var selected = uiAvailbleModsListView.SelectedItems.Cast<ManagerGUI.ModListItem>().ToList();
            if (selected == null || selected.Count == 0) return;

            try
            {
                if (_orderDirty) SaveCurrentOrderToDisk();
                var modsRoot = uiModsPath.Text;
                var order = new List<string>(ReadOrderFromFile(modsRoot) ?? new string[0]);
                foreach (var item in selected)
                {
                    if (!order.Any(x => string.Equals(x, item.Id, StringComparison.OrdinalIgnoreCase)))
                        order.Add(item.Id);
                }
                WriteOrderToFile(modsRoot, order);

                updateAvailableMods(); // Refresh UI
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error enabling mod: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Disable mod (move from enabled to disabled)
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            var selected = uiInstalledModsListView.SelectedItems.Cast<ManagerGUI.ModListItem>().ToList();
            if (selected == null || selected.Count == 0) return;

            try
            {
                if (_orderDirty) SaveCurrentOrderToDisk();
                var modsRoot = uiModsPath.Text;
                var order = new List<string>(ReadOrderFromFile(modsRoot) ?? new string[0]);
                foreach (var item in selected)
                {
                    order.RemoveAll(x => string.Equals(x, item.Id, StringComparison.OrdinalIgnoreCase));
                }
                WriteOrderToFile(modsRoot, order);
                
                updateAvailableMods(); // Refresh UI
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error disabling mod: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Double-click available mod to enable
        /// </summary>
        private void uiAvailbleModsListView_DoubleClick(object sender, EventArgs e)
        {
            button1_Click(sender, e);
        }

        /// <summary>
        /// Double-click installed mod to disable
        /// </summary>
        private void uiInstalledModsListView_DoubleClick(object sender, EventArgs e)
        {
            button2_Click(sender, e);
        }

        /// <summary>
        /// Open game directory in Explorer
        /// </summary>
        private void uiOpenGameDir_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Path.GetDirectoryName(uiGamePath.Text));
        }

        

        // Move selected enabled mod up (Coolnether123)
        private void btnMoveUpEnabled_Click(object sender, EventArgs e)
        {
            var lb = uiInstalledModsListView;
            int i = lb.SelectedIndex;
            if (i <= 0) return;
            var item = lb.Items[i];
            lb.Items.RemoveAt(i);
            lb.Items.Insert(i - 1, item);
            lb.SelectedIndex = i - 1;
            _orderDirty = true;
            SaveCurrentOrderToDisk();
        }

        // Move selected enabled mod down (Coolnether123)
        private void btnMoveDownEnabled_Click(object sender, EventArgs e)
        {
            var lb = uiInstalledModsListView;
            int i = lb.SelectedIndex;
            if (i < 0 || i >= lb.Items.Count - 1) return;
            var item = lb.Items[i];
            lb.Items.RemoveAt(i);
            lb.Items.Insert(i + 1, item);
            lb.SelectedIndex = i + 1;
            _orderDirty = true;
            SaveCurrentOrderToDisk();
        }

        // Auto sort enabled mods and save to mods/loadorder.json
        private void btnSaveOrder_Click(object sender, EventArgs e)
        {
            try
            {
                var modsRoot = uiModsPath.Text;
                var currentOrder = ReadOrderFromFile(modsRoot) ?? new string[0];
                var enabledItems = uiInstalledModsListView.Items.Cast<ManagerGUI.ModListItem>().ToList();
                var discovered = ToModEntries(enabledItems);
                var eval = LoadOrderResolver.Evaluate(discovered, currentOrder);
                WriteOrderToFile(modsRoot, eval.SortedIds ?? new List<string>());
                _orderDirty = false;
                updateAvailableMods();
                System.Windows.Forms.MessageBox.Show("Mods auto-sorted.", "Info", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to auto sort: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void SaveCurrentOrderToDisk()
        {
            try
            {
                var modsRoot = uiModsPath.Text;
                var currentUiOrder = uiInstalledModsListView.Items.Cast<ManagerGUI.ModListItem>().Select(item => item.Id).ToList();
                WriteOrderToFile(modsRoot, currentUiOrder);
                _orderDirty = false;

                // Store selected item ID before refreshing
                string selectedModId = null;
                if (uiInstalledModsListView.SelectedItem is ManagerGUI.ModListItem selectedItem)
                {
                    selectedModId = selectedItem.Id;
                }

                updateAvailableMods();

                // Re-select the mod after refreshing
                if (selectedModId != null)
                {
                    foreach (ManagerGUI.ModListItem item in uiInstalledModsListView.Items)
                    {
                        if (item.Id == selectedModId)
                        {
                            uiInstalledModsListView.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to save current order: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        // Local JSON helpers (no Unity dependency)
        private class SimpleLoadOrder { public string[] order; }
        private static bool IsNullOrWhiteSpaceCompat(string s) { return s == null || s.Trim().Length == 0; }
        private string[] ReadOrderFromFile(string modsRoot)
        {
            try
            {
                var path = Path.Combine(modsRoot, "loadorder.json");
                if (!File.Exists(path)) return new string[0];
                var json = File.ReadAllText(path);
                var obj = new JavaScriptSerializer().Deserialize<SimpleLoadOrder>(json);
                var raw = (obj != null && obj.order != null) ? obj.order : new string[0];
                var list = new List<string>();
                foreach (var s in raw)
                {
                    if (IsNullOrWhiteSpaceCompat(s)) continue;
                    var id = s.Trim().ToLowerInvariant();
                    if (!list.Contains(id)) list.Add(id);
                }
                return list.ToArray();
            }
            catch { return new string[0]; }
        }

        private void WriteOrderToFile(string modsRoot, IEnumerable<string> order)
        {
            try
            {
                var unique = new List<string>();
                foreach (var s in (order ?? new string[0]))
                {
                    if (IsNullOrWhiteSpaceCompat(s)) continue;
                    var id = s.Trim().ToLowerInvariant();
                    if (!unique.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase))) unique.Add(id);
                }
                var json = new JavaScriptSerializer().Serialize(new { order = unique.ToArray() });
                var path = Path.Combine(modsRoot, "loadorder.json");
                Directory.CreateDirectory(modsRoot);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to write load order: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        // Also update details when selecting in available list (Coolnether123)
        private void uiAvailbleModsListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = uiAvailbleModsListView.SelectedItem as ManagerGUI.ModListItem;
            if (item == null) return;
            _selectedItem = item;
            UpdateDetailsPanel(item);
            RebuildSettingsPanel(item);
        }

        // Build the right-side details view (Coolnether123)
        private void UpdateDetailsPanel(ManagerGUI.ModListItem item)
        {
            try
            {
                if (item == null)
                {
                    // Clear all fields if no item is selected
                    if (lblName != null) lblName.Text = string.Empty;
                    if (lblId != null) lblId.Text = string.Empty;
                    if (lblVersion != null) lblVersion.Text = "-";
                    if (lblAuthors != null) lblAuthors.Text = "-";
                    if (lblDependsOn != null) lblDependsOn.Text = "";
                    if (rtbDescription != null) rtbDescription.Text = string.Empty;
                    if (lblTags != null) lblTags.Text = "-";
                    if (lblWebsite != null) lblWebsite.Text = "-";
                    if (pbPreview != null) pbPreview.Image = null;
                    return;
                }

                bool devMode = chkDevMode != null && chkDevMode.Checked;

                // Basic info (always available)
                if (lblName != null) lblName.Text = item.DisplayName;
                if (lblId != null) lblId.Text = item.Id;

                // About-dependent info
                var about = item.About;

                if (lblVersion != null) {
                    if (about != null && !string.IsNullOrEmpty(about.version)) lblVersion.Text = about.version;
                    else if (devMode) lblVersion.Text = "Add '\"version\": \"1.0.0\"'";
                    else lblVersion.Text = "-";
                }

                if (lblAuthors != null) {
                    if (about != null && about.authors != null && about.authors.Length > 0) lblAuthors.Text = string.Join(", ", about.authors);
                    else if (devMode) lblAuthors.Text = "Add '\"authors\": [\"YourName\"]'";
                    else lblAuthors.Text = "-";
                }

                if (lblDependsOn != null)
                {
                    if (about != null && about.dependsOn != null && about.dependsOn.Length > 0)
                    {
                        lblDependsOn.Text = "Depends On: " + string.Join(", ", about.dependsOn);
                    }
                    else
                    {
                        lblDependsOn.Text = "";
                    }
                }

                if (rtbDescription != null) {
                    if (about != null && !string.IsNullOrEmpty(about.description)) rtbDescription.Text = about.description;
                    else if (devMode && !item.HasAbout) rtbDescription.Text = "Create 'About/About.json' file for this mod.";
                    else if (devMode) rtbDescription.Text = "Add '\"description\": \"Your description.\"'";
                    else if (!item.HasAbout) rtbDescription.Text = "Legacy mod (no About.json)";
                    else rtbDescription.Text = "";
                }

                if (lblTags != null) {
                    if (about != null && about.tags != null && about.tags.Length > 0) lblTags.Text = string.Join(", ", about.tags);
                    else if (devMode) lblTags.Text = "Add '\"tags\": [\"QoL\", \"UI\"]'";
                    else lblTags.Text = "-";
                }

                if (lblWebsite != null) {
                    if (about != null && !string.IsNullOrEmpty(about.website)) lblWebsite.Text = about.website;
                    else if (devMode) lblWebsite.Text = "Add '\"website\": \"https://your.link.com\"'";
                    else lblWebsite.Text = "-";
                }


                if (pbPreview != null)
                {
                    if (!string.IsNullOrEmpty(item.PreviewPath) && System.IO.File.Exists(item.PreviewPath))
                    {
                        using (var img = Image.FromFile(item.PreviewPath))
                        {
                            pbPreview.Image = new Bitmap(img);
                        }
                    }
                    else
                    {
                        pbPreview.Image = null;
                    }
                }

                // Dev mode about presence inspector
                if (grpAboutInspector != null)
                {
                    grpAboutInspector.Visible = devMode;
                    if(devMode)
                    {
                        setCheck(chkHasId, about != null && !string.IsNullOrEmpty(about.id));
                        setCheck(chkHasName, about != null && !string.IsNullOrEmpty(about.name));
                        setCheck(chkHasVersion, about != null && !string.IsNullOrEmpty(about.version));
                        setCheck(chkHasAuthors, about != null && about.authors != null && about.authors.Length > 0);
                        setCheck(chkHasDescription, about != null && !string.IsNullOrEmpty(about.description));
                        setCheck(chkHasEntryType, about != null && !string.IsNullOrEmpty(about.entryType));
                        setCheck(chkHasDependsOn, about != null && about.dependsOn != null && about.dependsOn.Length > 0);
                        setCheck(chkHasLoadBefore, about != null && about.loadBefore != null && about.loadBefore.Length > 0);
                        setCheck(chkHasLoadAfter, about != null && about.loadAfter != null && about.loadAfter.Length > 0);
                        setCheck(chkHasTags, about != null && about.tags != null && about.tags.Length > 0);
                        setCheck(chkHasWebsite, about != null && !string.IsNullOrEmpty(about.website));
                    }
                }
            }
            catch { } // Ignore exceptions during details update
        }

        private void setCheck(CheckBox cb, bool on)
        {
            if (cb != null) cb.Checked = on;
        }

        // Build the dynamic settings UI from Config/default.json + user.json (Coolnether123)
        private void RebuildSettingsPanel(ManagerGUI.ModListItem item)
        {
            if (panelSettings == null) return;
            panelSettings.SuspendLayout();
            panelSettings.Controls.Clear();
            _settingsControls.Clear();
            if (item == null) { panelSettings.ResumeLayout(); return; }

            try
            {
                var ms = new LocalSettings(item.RootPath);
                int y = 5;
                foreach (var key in ms.Keys().OrderBy(k => k))
                {
                    var lbl = new Label { Left = 5, Top = y + 3, Width = 140, Text = key };
                    Control input = null;
                    string sval = ms.GetString(key, null);
                    int ival; float fval; bool bval;
                    if (TryBool(sval, out bval))
                    {
                        var cb = new CheckBox { Left = 150, Top = y, Width = 200, Checked = bval };
                        input = cb;
                    }
                    else if (int.TryParse(sval, out ival))
                    {
                        var num = new NumericUpDown { Left = 150, Top = y, Width = 100, Minimum = -100000, Maximum = 100000, Value = ival, DecimalPlaces = 0, Increment = 1 };
                        input = num;
                    }
                    else if (float.TryParse(sval, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fval))
                    {
                        var num = new NumericUpDown { Left = 150, Top = y, Width = 100, Minimum = -100000, Maximum = 100000, DecimalPlaces = 2 };
                        try { num.Value = (decimal)fval; } catch { num.Value = 0; } // Handle potential conversion errors
                        input = num;
                    }
                    else
                    {
                        var tb = new TextBox { Left = 150, Top = y, Width = 200, Text = sval ?? string.Empty };
                        input = tb;
                    }
                    panelSettings.Controls.Add(lbl);
                    panelSettings.Controls.Add(input);
                    _settingsControls[key] = input;
                    y += 28;
                }

                panelSettings.Tag = ms; // stash
            }
            catch (Exception) { /* Ignore exceptions during settings panel rebuild */ }
            finally
            {
                panelSettings.ResumeLayout();
            }
        }

        private static bool TryBool(string raw, out bool v)
        {
            if (bool.TryParse(raw, out v)) return true;
            if (raw == null) { v = false; return false; }
            var s = raw.Trim().ToLowerInvariant();
            if (s == "1" || s == "yes" || s == "y" || s == "on") { v = true; return true; }
            if (s == "0" || s == "no" || s == "n" || s == "off") { v = false; return true; }
            v = false;
            return false;
        }

        private void btnApplySettings_Click(object sender, EventArgs e)
        {
            var ms = panelSettings.Tag as LocalSettings;
            if (ms == null) return;
            foreach (var kv in _settingsControls)
            {
                var key = kv.Key; var ctrl = kv.Value;
                if (ctrl is CheckBox)
                {
                    ms.SetBool(key, ((CheckBox)ctrl).Checked);
                }
                else if (ctrl is NumericUpDown)
                {
                    var num = (NumericUpDown)ctrl;
                    if (num.DecimalPlaces == 0) ms.SetInt(key, (int)num.Value);
                    else ms.SetFloat(key, (float)num.Value);
                }
                else if (ctrl is TextBox)
                {
                    ms.SetString(key, ((TextBox)ctrl).Text);
                }
            }
            ms.SaveUser();
            System.Windows.Forms.MessageBox.Show("Settings saved.", "Info", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
        }

        private void btnResetSettings_Click(object sender, EventArgs e)
        {
            var ms = panelSettings.Tag as LocalSettings;
            if (ms == null) return;
            ms.ResetUser();
            var currentItem = _selectedItem;
            RebuildSettingsPanel(currentItem);
        }

        // Local settings class for Manager (avoid UnityEngine dependency). (Coolnether123)
        private class LocalSettings
        {
            private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

            private readonly string _configDir;
            private readonly string _defaultPath;
            private readonly string _userPath;
            private readonly Dictionary<string, string> _types = new Dictionary<string, string>(KeyComparer);
            private readonly Dictionary<string, string> _defaults = new Dictionary<string, string>(KeyComparer);
            private readonly Dictionary<string, string> _user = new Dictionary<string, string>(KeyComparer);

            public LocalSettings(string rootPath)
            {
                _configDir = Path.Combine(rootPath ?? string.Empty, "Config");
                _defaultPath = Path.Combine(_configDir, "default.json");
                _userPath = Path.Combine(_configDir, "user.json");
                Reload();
            }

            private void Reload()
            {
                _types.Clear(); _defaults.Clear(); _user.Clear();
                LoadInto(_defaultPath, _defaults);
                LoadInto(_userPath, _user);
            }

            private class LocalEntry { public string key; public string type; public string value; }
            private class LocalFile { public LocalEntry[] entries; }

            private void LoadInto(string path, Dictionary<string, string> target)
            {
                try
                {
                    if (!File.Exists(path)) return;
                    var json = File.ReadAllText(path);
                    var file = new JavaScriptSerializer().Deserialize<LocalFile>(json);
                    if (file == null || file.entries == null) return;
                    foreach (var e in file.entries)
                    {
                        if (e == null || string.IsNullOrEmpty(e.key)) continue;
                        if (!string.IsNullOrEmpty(e.type)) _types[e.key] = e.type;
                        target[e.key] = e.value;
                    }
                }
                catch { } // Ignore exceptions during file loading
            }

            public IEnumerable<string> Keys()
            {
                var set = new HashSet<string>(_defaults.Keys, KeyComparer);
                foreach (var k in _user.Keys) set.Add(k);
                return set;
            }

            public string GetString(string key, string fallback)
            {
                string v; if (_user.TryGetValue(key, out v)) return v; if (_defaults.TryGetValue(key, out v)) return v; return fallback;
            }
            public int GetInt(string key, int fallback)
            {
                int v; var s = GetString(key, null); return int.TryParse(s, out v) ? v : fallback;
            }
            public float GetFloat(string key, float fallback)
            {
                float v; var s = GetString(key, null); return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
            }
            public bool GetBool(string key, bool fallback)
            {
                bool v; var s = GetString(key, null); return TryBool(s, out v) ? v : fallback;
            }

            public void SetString(string key, string value)
            {
                ApplySet(key, "string", value);
            }
            public void SetInt(string key, int value)
            {
                ApplySet(key, "int", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            public void SetFloat(string key, float value)
            {
                ApplySet(key, "float", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            public void SetBool(string key, bool value)
            {
                ApplySet(key, "bool", value ? "true" : "false");
            }

            private void ApplySet(string key, string type, string raw)
            {
                _types[key] = type;
                string def;
                if (_defaults.TryGetValue(key, out def) && string.Equals((def ?? string.Empty), (raw ?? string.Empty), StringComparison.Ordinal))
                {
                    _user.Remove(key);
                }
                else
                {
                    _user[key] = raw;
                }
            }

            public void SaveUser()
            {
                try
                {
                    Directory.CreateDirectory(_configDir);
                    var entries = new List<LocalEntry>();
                    foreach (var kv in _user)
                    {
                        string t = _types.ContainsKey(kv.Key) ? _types[kv.Key] : Infer(_defaults.ContainsKey(kv.Key) ? _defaults[kv.Key] : kv.Value);
                        entries.Add(new LocalEntry { key = kv.Key, type = t, value = kv.Value });
                    }
                    var file = new LocalFile { entries = entries.ToArray() };
                    var json = new JavaScriptSerializer().Serialize(file);
                    File.WriteAllText(_userPath, json);

                    // Also write INI mirror alongside user.json
                    try
                    {
                        var iniPath = Path.Combine(_configDir, "user.ini");
                        using (var sw = new StreamWriter(iniPath, false))
                        {
                            sw.WriteLine("[Settings]");
                            foreach (var kv in _user)
                            {
                                var val = kv.Value ?? string.Empty;
                                val = val.Replace('\r', ' ').Replace('\n', ' ');
                                sw.WriteLine(kv.Key + "=" + val);
                            }
                        }
                    }
                    catch { }
                }
                catch { } // Ignore exceptions during user settings save
            }

            public void ResetUser()
            {
                _user.Clear(); SaveUser();
            }

            private static string Infer(string raw)
            {
                if (string.IsNullOrEmpty(raw)) return "string";
                int i; float f; bool b;
                if (int.TryParse(raw, out i)) return "int";
                if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) return "float";
                if (bool.TryParse(raw, out b)) return "bool";
                return "string";
            }
        }

        private void chkDevMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateDetailsPanel(_selectedItem);
            if (grpAboutInspector != null)
                grpAboutInspector.Visible = chkDevMode.Checked;
            
            // --- ADD THIS ---
            if (grpDevSettings != null)
                grpDevSettings.Visible = chkDevMode.Checked;
            // ----------------
            if (chkIgnoreOrderChecks != null)
                chkIgnoreOrderChecks.Enabled = chkDevMode.Checked;
        }
    }
}
