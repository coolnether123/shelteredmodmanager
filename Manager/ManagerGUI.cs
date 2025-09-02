using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Web.Script.Serialization; // JSON for Manager (Coolnether123)

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
            public string Id;              // manifest id or fallback
            public string RootPath;        // mod folder or dll folder
            public bool IsDirectory;       // true for folder-based mods
            public bool HasManifest;       // About/About.json exists
            public ModManifest Manifest;   // may be null
            public string PreviewPath;     // About/preview.png if exists
            public override string ToString() { return DisplayName; }
        }

        // Dynamic settings controls per key (Coolnether123)
        private readonly Dictionary<string, Control> _settingsControls = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        private ModListItem _selectedItem = null;
        private bool _orderDirty = false;

        public ManagerGUI()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            updateGamePath();
            updateAvailableMods();
        }

        /// <summary>
        /// Paths for enabled/disabled mods
        /// </summary>
        private string EnabledModsPath => Path.Combine(uiModsPath.Text, "enabled");
        private string DisabledModsPath => Path.Combine(uiModsPath.Text, "disabled");

        /// <summary>
        /// Refreshes the available and installed mods lists
        /// </summary>
        private void updateAvailableMods()
        {
            if (uiModsPath.Text.ToString().Trim().ToLower().Equals(DEFAULT_VALUE.ToLower())) return;

            // Ensure directories exist
            Directory.CreateDirectory(EnabledModsPath);
            Directory.CreateDirectory(DisabledModsPath);

            uiAvailbleModsListView.Items.Clear();
            uiInstalledModsListView.Items.Clear();

            // Populate Available Mods (from disabled folder)
            var disabledItems = DiscoverMods(DisabledModsPath);
            foreach (var it in disabledItems) uiAvailbleModsListView.Items.Add(it);

            // Populate Installed Mods (from enabled folder)
            var enabledItems = DiscoverMods(EnabledModsPath);
            var ordered = ApplySavedOrder(enabledItems);
            foreach (var it in ordered) uiInstalledModsListView.Items.Add(it);

            _orderDirty = false;
        }

        // Discover mods in a path (directories first, then legacy loose DLLs). (Coolnether123)
        private List<ModListItem> DiscoverMods(string root)
        {
            var list = new List<ModListItem>();
            try
            {
                if (!Directory.Exists(root)) return list;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    list.Add(BuildItemFromDirectory(dir));
                }
                foreach (var dll in Directory.GetFiles(root, "*.dll"))
                {
                    var name = Path.GetFileName(dll);
                    list.Add(new ModListItem
                    {
                        DisplayName = name,
                        Id = name.ToLowerInvariant(),
                        RootPath = Path.GetDirectoryName(dll),
                        IsDirectory = false,
                        HasManifest = false,
                        Manifest = null,
                        PreviewPath = null
                    });
                }
            }
            catch { }
            return list;
        }

        private ModListItem BuildItemFromDirectory(string dir)
        {
            var aboutDir = Path.Combine(dir, "About");
            var aboutJson = Path.Combine(aboutDir, "About.json");
            ModManifest manifest = null;
            string id = null, display = Path.GetFileName(dir);
            string preview = null;
            if (File.Exists(aboutJson))
            {
                try
                {
                    var text = File.ReadAllText(aboutJson);
                    manifest = new JavaScriptSerializer().Deserialize<ModManifest>(text);
                    if (manifest != null)
                    {
                        id = string.IsNullOrEmpty(manifest.id) ? null : manifest.id.Trim().ToLowerInvariant();
                        display = string.IsNullOrEmpty(manifest.name) ? display : manifest.name;
                    }
                }
                catch { }
                var prev = Path.Combine(aboutDir, "preview.png");
                if (File.Exists(prev)) preview = prev;
            }
            return new ModListItem
            {
                DisplayName = display,
                Id = id ?? display.ToLowerInvariant(),
                RootPath = dir,
                IsDirectory = true,
                HasManifest = File.Exists(aboutJson),
                Manifest = manifest,
                PreviewPath = preview
            };
        }

        private List<ModListItem> ApplySavedOrder(List<ModListItem> items)
        {
            try
            {
                var orderPath = Path.Combine(uiModsPath.Text, "loadorder.json");
                if (!File.Exists(orderPath)) return items;
                var text = File.ReadAllText(orderPath);
                var o = new JavaScriptSerializer().Deserialize<LocalOrder>(text);
                if (o == null || o.order == null) return items;
                var prio = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int p = 0; foreach (var id in o.order) if (!prio.ContainsKey(id)) prio[id] = p++;
                items.Sort(delegate (ModListItem a, ModListItem b)
                {
                    int pa = prio.ContainsKey(a.Id) ? prio[a.Id] : int.MaxValue;
                    int pb = prio.ContainsKey(b.Id) ? prio[b.Id] : int.MaxValue;
                    int c = pa.CompareTo(pb);
                    if (c != 0) return c;
                    return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
                });
                return items;
            }
            catch { return items; }
        }

        /// <summary>
        /// Loads the game path from the ini file
        /// </summary>
        private void updateGamePath()
        {
            try
            {
                string contents = File.ReadAllText(MOD_MANAGER_INI_FILE);
                uiGamePath.Text = contents;
                uiModsPath.Text = Path.Combine(Path.GetDirectoryName(contents), "mods");
            }
            catch
            {
                uiGamePath.Text = DEFAULT_VALUE;
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
                uiModsPath.Text = Path.Combine(Path.GetDirectoryName(fileDialog.FileName), "mods"); // (Coolnether123)
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
            System.Diagnostics.Process.Start(uiGamePath.Text);
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
            File.WriteAllText(MOD_MANAGER_INI_FILE, uiGamePath.Text);
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
            var item = uiAvailbleModsListView.SelectedItem as object;
            if (item == null) return;
            var mod = item as ManagerGUI.ModListItem;
            if (mod == null)
            {
                // Backward compat: enable a dll file name string
                string modFileName = uiAvailbleModsListView.SelectedItem.ToString();
                string sourcePath = Path.Combine(DisabledModsPath, modFileName);
                string destinationPath = Path.Combine(EnabledModsPath, modFileName);
                try { File.Move(sourcePath, destinationPath); updateAvailableMods(); } catch (Exception ex) { System.Windows.Forms.MessageBox.Show($"Error enabling mod: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error); }
                return;
            }
            try
            {
                if (mod.IsDirectory)
                {
                    var dest = Path.Combine(EnabledModsPath, Path.GetFileName(mod.RootPath));
                    Directory.Move(mod.RootPath, dest);
                }
                else
                {
                    var src = Path.Combine(DisabledModsPath, mod.DisplayName);
                    var dest = Path.Combine(EnabledModsPath, mod.DisplayName);
                    File.Move(src, dest);
                }
                updateAvailableMods();
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
            var item = uiInstalledModsListView.SelectedItem as object;
            if (item == null) return;
            var mod = item as ManagerGUI.ModListItem;
            if (mod == null)
            {
                // Backward compat: disable a dll file name string
                string modFileName = uiInstalledModsListView.SelectedItem.ToString();
                string sourcePath = Path.Combine(EnabledModsPath, modFileName);
                string destinationPath = Path.Combine(DisabledModsPath, modFileName);
                try { File.Move(sourcePath, destinationPath); updateAvailableMods(); } catch (Exception ex) { System.Windows.Forms.MessageBox.Show($"Error disabling mod: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error); }
                return;
            }
            try
            {
                if (mod.IsDirectory)
                {
                    var dest = Path.Combine(DisabledModsPath, Path.GetFileName(mod.RootPath));
                    Directory.Move(mod.RootPath, dest);
                }
                else
                {
                    var src = Path.Combine(EnabledModsPath, mod.DisplayName);
                    var dest = Path.Combine(DisabledModsPath, mod.DisplayName);
                    File.Move(src, dest);
                }
                updateAvailableMods();
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

        // Local order format (Coolnether123)
        [Serializable]
        private class LocalOrder { public string[] order; }

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
        }

        // Save load order to mods/loadorder.json (Coolnether123)
        private void btnSaveOrder_Click(object sender, EventArgs e)
        {
            try
            {
                var ids = new System.Collections.Generic.List<string>();
                foreach (var o in uiInstalledModsListView.Items)
                {
                    var it = o as ManagerGUI.ModListItem; if (it == null) continue; ids.Add(it.Id);
                }
                var lo = new LocalOrder { order = ids.ToArray() };
                var json = new JavaScriptSerializer().Serialize(lo);
                System.IO.File.WriteAllText(System.IO.Path.Combine(uiModsPath.Text, "loadorder.json"), json);
                _orderDirty = false;
                System.Windows.Forms.MessageBox.Show("Load order saved.", "Info", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to save load order: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                // labels created in Designer: lblName, lblVersion, lblAuthors, lblDescription, lblId, lblTags, lblWebsite
                if (lblName != null) lblName.Text = item != null ? item.DisplayName : string.Empty;
                if (lblId != null) lblId.Text = item != null ? item.Id : string.Empty;
                if (item != null && item.Manifest != null)
                {
                    if (lblVersion != null) lblVersion.Text = item.Manifest.version ?? "-";
                    if (lblAuthors != null) lblAuthors.Text = (item.Manifest.authors != null && item.Manifest.authors.Length > 0) ? string.Join(", ", item.Manifest.authors) : "-";
                    if (lblDescription != null) lblDescription.Text = item.Manifest.description ?? "";
                    if (lblTags != null) lblTags.Text = (item.Manifest.tags != null && item.Manifest.tags.Length > 0) ? string.Join(", ", item.Manifest.tags) : "-";
                    if (lblWebsite != null) lblWebsite.Text = item.Manifest.website ?? "-";
                }
                else
                {
                    if (lblVersion != null) lblVersion.Text = "-";
                    if (lblAuthors != null) lblAuthors.Text = "-";
                    if (lblTags != null) lblTags.Text = "-";
                    if (lblWebsite != null) lblWebsite.Text = "-";
                    if (lblDescription != null) lblDescription.Text = item != null && !item.HasManifest ? "Legacy mod (no About.json)" : string.Empty;
                }

                if (pbPreview != null)
                {
                    if (item != null && !string.IsNullOrEmpty(item.PreviewPath) && System.IO.File.Exists(item.PreviewPath))
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

                // Dev mode manifest presence
                if (chkDevMode != null && chkDevMode.Checked && item != null)
                {
                    var man = item.Manifest;
                    setCheck(chkHasId, man != null && !string.IsNullOrEmpty(man.id));
                    setCheck(chkHasName, man != null && !string.IsNullOrEmpty(man.name));
                    setCheck(chkHasVersion, man != null && !string.IsNullOrEmpty(man.version));
                    setCheck(chkHasAuthors, man != null && man.authors != null && man.authors.Length > 0);
                    setCheck(chkHasDescription, man != null && !string.IsNullOrEmpty(man.description));
                    setCheck(chkHasEntryType, man != null && !string.IsNullOrEmpty(man.entryType));
                    setCheck(chkHasDependsOn, man != null && man.dependsOn != null && man.dependsOn.Length > 0);
                    setCheck(chkHasLoadBefore, man != null && man.loadBefore != null && man.loadBefore.Length > 0);
                    setCheck(chkHasLoadAfter, man != null && man.loadAfter != null && man.loadAfter.Length > 0);
                    setCheck(chkHasTags, man != null && man.tags != null && man.tags.Length > 0);
                    setCheck(chkHasWebsite, man != null && !string.IsNullOrEmpty(man.website));
                }
            }
            catch { }
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
                        try { num.Value = (decimal)fval; } catch { num.Value = 0; }
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
            catch (Exception)
            {
                // ignore
            }
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
            v = false; return false;
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
            private readonly string _configDir;
            private readonly string _defaultPath;
            private readonly string _userPath;
            private readonly Dictionary<string, string> _types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, string> _defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, string> _user = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                catch { }
            }

            public IEnumerable<string> Keys()
            {
                var set = new HashSet<string>(_defaults.Keys, StringComparer.OrdinalIgnoreCase);
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
                }
                catch { }
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
            if (grpManifestInspector != null)
                grpManifestInspector.Visible = chkDevMode.Checked;
        }
    }
}
