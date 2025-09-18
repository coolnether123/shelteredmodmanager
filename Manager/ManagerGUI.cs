using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Web.Script.Serialization;
using ModAPI;
using ModAPI.Core;


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
            }
            catch { } // Ignore exceptions during discovery
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
            settings["GameBitness"] = Program.GameBitness; // Save the detected bitness

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
                string iniPath = Path.Combine(gameDir, "doorstop_config.ini");

                // Ensure SMM directory exists (target assembly lives here)
                string smmDir = Path.Combine(gameDir, "SMM");
                Directory.CreateDirectory(smmDir);

                // Doorstop v4 config (General section)
                var ini = new List<string>();
                ini.Add("# Auto-generated by Sheltered Mod Manager");
                ini.Add("[General]");
                ini.Add("enabled=true");
                ini.Add("target_assembly=SMM\\Doorstop.dll");
                ini.Add("redirect_output_log=true");
                ini.Add("");
                ini.Add("[UnityMono]");
                ini.Add("dll_search_path_override=");
                ini.Add("debug_enabled=false");
                ini.Add("debug_address=127.0.0.1:10000");
                ini.Add("debug_suspend=false");
                File.WriteAllLines(iniPath, ini.ToArray());

                // Copy correct bitness winhttp.dll next to the exe (best-effort)
                bool currentIs64Bit = DetectIsExe64Bit(uiGamePath.Text);
                CopyWinhttpForGame(gameDir, currentIs64Bit);
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
    }
}
