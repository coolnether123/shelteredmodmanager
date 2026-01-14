using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Saves;
using ModAPI.UI;

namespace ModAPI.Hooks.Paging
{
    /// <summary>
    /// Modal window showing save verification details.
    /// Displays active mods vs. save mods with status indicators.
    /// </summary>
    internal class SaveDetailsWindow : MonoBehaviour
    {
        private static GameObject _instance;
        private static Texture2D _whiteTexture;
        
        // Colors for status indicators
        private static readonly Color COLOR_MATCH = new Color(0.3f, 0.9f, 0.3f);
        private static readonly Color COLOR_VERSION_DIFF = new Color(0.9f, 0.9f, 0.2f);
        private static readonly Color COLOR_MISSING = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color COLOR_HEADER = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color COLOR_TEXT = Color.white;
        private static readonly Color COLOR_SUBTEXT = new Color(0.7f, 0.7f, 0.7f);
        
        // Layout constants for the modal window - adjusted for clipboard aesthetic
        private const int WINDOW_WIDTH = 900;
        private const int WINDOW_HEIGHT = 860;
        private const int COLUMN_WIDTH = 400;
        private const int ROW_HEIGHT = 42;
        
        public static void Show(SaveEntry entry, SlotManifest manifest, SaveVerification.VerificationState state, bool isAttemptingLoad, Action onLoadAnyway = null)
        {
            UIDebug.LogTimed("SaveDetailsWindow.Show called");
            if (_instance != null) Destroy(_instance);

            var panel = UIUtil.EnsureOverlayPanel("ModAPI_SaveDetailsWindow", 10000);
            if (panel == null) 
            {
                MMLog.WriteError("[SaveDetailsWindow] Failed to create overlay panel!");
                return;
            }
            
            // Create shared white texture
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(2, 2);
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        _whiteTexture.SetPixel(x, y, Color.white);
                _whiteTexture.Apply();
            }

            var root = new GameObject("SaveDetailsWindow");
            root.transform.SetParent(panel.transform, false);
            root.layer = panel.gameObject.layer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            
            _instance = root;

            // Get font
            UIFont uiFont = null;
            Font ttfFont = null;
            var sampleLabel = UnityEngine.Object.FindObjectOfType<UILabel>();
            if (sampleLabel != null)
            {
                uiFont = sampleLabel.bitmapFont;
                ttfFont = sampleLabel.trueTypeFont;
            }
            if (uiFont == null && ttfFont == null)
                ttfFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Create the window
            var win = root.AddComponent<SaveDetailsWindow>();
            win.BuildFullUI(root.transform, entry, manifest, state, isAttemptingLoad, onLoadAnyway, uiFont, ttfFont);
        }

        private void BuildFullUI(Transform root, SaveEntry entry, SlotManifest manifest, 
            SaveVerification.VerificationState state, bool isAttemptingLoad, Action onLoadAnyway,
            UIFont uiFont, Font ttfFont)
        {
            int layer = root.gameObject.layer;
            
            // === DARK OVERLAY ===
            CreateTexturedBox(root, "DarkOverlay", Vector3.zero, 3000, 3000, 
                new Color(0f, 0f, 0f, 0.85f), 0, true);
            
            // === CLONE CLIPBOARD VISUALS ===
            // Widened root for cloning if needed
            bool clipboardFound = CloneClipboardVisuals(root);
            
            if (!clipboardFound)
            {
                // Fallback to textured box if clipboard visual not found
                CreateTexturedBox(root, "WindowBackground", Vector3.zero, 
                    WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.98f), 10, false);
                CreateTexturedBox(root, "WindowBorder", Vector3.zero, 
                    WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);
            }

            // Text color based on background (Darker if clipboard found)
            Color primaryTextColor = clipboardFound ? new Color(0.15f, 0.1f, 0.05f) : COLOR_TEXT;
            Color secondaryTextColor = clipboardFound ? new Color(0.3f, 0.25f, 0.2f) : COLOR_SUBTEXT;
            Color titleColor = clipboardFound ? new Color(0.2f, 0.15f, 0.1f) : COLOR_HEADER;
            
            // === HEADER ===
            var titleLabel = CreateLabel(root, "Title", "MOD VERIFICATION",
                new Vector3(0, WINDOW_HEIGHT/2 - 70, 0), 32, titleColor, uiFont, ttfFont, 100);
            titleLabel.alignment = NGUIText.Alignment.Center;
            
            // Subtitle with family name and days
            string familyName = entry?.saveInfo?.familyName;
            if (string.IsNullOrEmpty(familyName)) familyName = "Unknown";
            int days = entry?.saveInfo?.daysSurvived ?? 0;
            var subtitleLabel = CreateLabel(root, "Subtitle", $"Family: \"{familyName}\" - Day {days}",
                new Vector3(0, WINDOW_HEIGHT/2 - 105, 0), 22, secondaryTextColor, uiFont, ttfFont, 100);
            subtitleLabel.alignment = NGUIText.Alignment.Center;
            
            // === CLOSE [X] BUTTON ===
            // Positioned according to new width
            var closeBtn = CreateButton(root, "CloseBtn", "CLOSE", 
                new Vector3(WINDOW_WIDTH/2 - 80, WINDOW_HEIGHT/2 - 60, 0), 
                20, primaryTextColor, uiFont, ttfFont, 100, 40, () => Close());
            
            // === ANALYZE MODS ===
            var activeMods = PluginManager.LoadedMods;
            var savedMods = manifest?.lastLoadedMods ?? new LoadedModInfo[0];
            var discovered = ModDiscovery.DiscoverAllMods();
            var comparison = BuildModComparison(activeMods, savedMods);
            
            // === COLUMN HEADERS ===
            int headerY = WINDOW_HEIGHT/2 - 160;
            
            CreateLabel(root, "ActiveHeader", "ACTIVE",
                new Vector3(-WINDOW_WIDTH/4, headerY, 0), 24, titleColor, uiFont, ttfFont, 100)
                .alignment = NGUIText.Alignment.Center;
            
            CreateLabel(root, "SavedHeader", "SAVE FILE",
                new Vector3(WINDOW_WIDTH/4, headerY, 0), 24, titleColor, uiFont, ttfFont, 100)
                .alignment = NGUIText.Alignment.Center;
            
            // Divider line
            CreateTexturedBox(root, "HeaderDivider", new Vector3(0, headerY - 25, 0),
                WINDOW_WIDTH - 200, 2, new Color(0, 0, 0, 0.2f), 50, false);
            
            // === MOD LISTS ===
            int listStartY = headerY - 55;


            int rowIndex = 0;
            
            // Active mods column (left)
            foreach (var mod in activeMods)
            {
                int y = listStartY - (rowIndex * ROW_HEIGHT);
                var status = comparison.Find(c => c.activeId == mod.Id);
                ModCompareStatus compareStatus = status?.status ?? ModCompareStatus.Match;
                Color color = GetStatusColor(compareStatus);
                
                string iconPrefix = "✓";
                string suffix = "";
                
                switch (compareStatus)
                {
                    case ModCompareStatus.Extra: iconPrefix = "~"; suffix = " [NEW]"; break;
                    case ModCompareStatus.VersionDiff: iconPrefix = "~"; break; 
                }
                
                var nameLabel = CreateLabel(root, $"ActiveMod_{rowIndex}", $"{iconPrefix} {mod.Name}{suffix}",
                    new Vector3(-WINDOW_WIDTH/4, y, 0), 18, color, uiFont, ttfFont, 100);
                nameLabel.alignment = NGUIText.Alignment.Center;
                
                var verLabel = CreateLabel(root, $"ActiveVer_{rowIndex}", $"   v{mod.Version}",
                    new Vector3(-WINDOW_WIDTH/4, y - 18, 0), 14, clipboardFound ? new Color(0.4f, 0.4f, 0.4f) : COLOR_SUBTEXT, uiFont, ttfFont, 100);
                verLabel.alignment = NGUIText.Alignment.Center;
                rowIndex++;
            }
            
            // Saved mods column (right)
            rowIndex = 0;
            var warnings = new List<string>();
            
            foreach (var saved in savedMods)
            {
                int y = listStartY - (rowIndex * ROW_HEIGHT);
                // LoadedModInfo has .modId, .version (camelCase)
                var status = comparison.Find(c => c.savedId == saved.modId);
                ModCompareStatus compareStatus = status?.status ?? ModCompareStatus.Match;
                Color color = GetStatusColor(compareStatus);
                
                string icon = "✓";
                string suffix = "";
                
                switch (compareStatus)
                {
                    case ModCompareStatus.Missing: icon = "✗"; suffix = " [MISSING]"; break;
                    case ModCompareStatus.VersionDiff: icon = "~"; suffix = " [VER DIFF]"; break;
                }
                
                var diskMod = discovered.Find(d => d.Id.Equals(saved.modId, StringComparison.OrdinalIgnoreCase));
                string displayName = diskMod != null ? diskMod.Name : saved.modId;

                var savedName = CreateLabel(root, $"SavedMod_{rowIndex}", $"{icon} {displayName}{suffix}",
                    new Vector3(WINDOW_WIDTH/4, y, 0), 18, color, uiFont, ttfFont, 100);
                savedName.alignment = NGUIText.Alignment.Center;
                
                string verText = compareStatus == ModCompareStatus.VersionDiff && status != null
                    ? $"   (save: v{saved.version}, active: v{status.activeVersion})"
                    : $"   v{saved.version}";
                var savedVer = CreateLabel(root, $"SavedVer_{rowIndex}", verText,
                    new Vector3(WINDOW_WIDTH/4, y - 18, 0), 14, clipboardFound ? new Color(0.4f, 0.4f, 0.4f) : COLOR_SUBTEXT, uiFont, ttfFont, 100);
                savedVer.alignment = NGUIText.Alignment.Center;
                rowIndex++;
            }
            
            // === WARNINGS SECTION ===
            int warningY = -WINDOW_HEIGHT/2 + 180;
            
            if (warnings.Count > 0)
            {
                CreateLabel(root, "WarningHeader", "⚠️ WARNINGS FROM MISSING MODS:",
                    new Vector3(-WINDOW_WIDTH/2 + 40, warningY, 0), 18, COLOR_MISSING, uiFont, ttfFont, 100);
                
                CreateTexturedBox(root, "WarningBg", new Vector3(0, warningY - 50, 0),
                    WINDOW_WIDTH - 60, 80, new Color(0.3f, 0.1f, 0.1f, 0.8f), 40, false);
                
                string warningText = string.Join("\n", warnings.Take(3).ToArray());
                if (warnings.Count > 3) warningText += $"\n... and {warnings.Count - 3} more";
                
                var warnLabel = CreateLabel(root, "WarningText", warningText,
                    new Vector3(-WINDOW_WIDTH/2 + 50, warningY - 25, 0), 14, COLOR_TEXT, uiFont, ttfFont, 100);
                warnLabel.overflowMethod = UILabel.Overflow.ClampContent;
                warnLabel.width = WINDOW_WIDTH - 100;
            }
            
            // === BUTTON ROW ===
            // Move buttons lower due to taller clipboard
            int buttonY = -WINDOW_HEIGHT/2 + 100;
            
            // Determine button states
            bool hasMissing = comparison.Any(c => c.status == ModCompareStatus.Missing);
            bool hasVersionDiff = comparison.Any(c => c.status == ModCompareStatus.VersionDiff);
            bool hasExtra = comparison.Any(c => c.status == ModCompareStatus.Extra);
            bool allMatch = !hasMissing && !hasVersionDiff && !hasExtra;
            
            bool canReload = !allMatch;
            if (hasMissing)
            {
                var missingEntries = comparison.Where(c => c.status == ModCompareStatus.Missing);
                bool allMissingAvailable = true;
                foreach(var m in missingEntries)
                {
                    if (m.savedId != null && !discovered.Exists(d => d.Id.Equals(m.savedId, StringComparison.OrdinalIgnoreCase)))
                    {
                        allMissingAvailable = false;
                        break;
                    }
                }
                if (!allMissingAvailable) canReload = false;
            }
            
            // RELOAD WITH SAVE MODS button
            Color requestedBrown = new Color(113f/255f, 82f/255f, 62f/255f);
            int absoluteSlot = entry.absoluteSlot; 
            
            var reloadBtn = CreateButton(root, "ReloadBtn", "AUTO-LOAD MODS",
                new Vector3(-WINDOW_WIDTH/4, buttonY, 0), 18, Color.white, uiFont, ttfFont, 240, 45,
                canReload ? (Action)(() => CreateRestartRequest(absoluteSlot, manifest)) : null);
            
            // Apply requested color to background
            var reloadTex = reloadBtn.GetComponent<UITexture>();
            if (reloadTex != null) reloadTex.color = canReload ? requestedBrown : new Color(0.25f, 0.2f, 0.15f, 1f);

            if (!canReload)
            {
                string reason = allMatch ? "(Already matching)" : 
                                (hasVersionDiff || hasExtra) && !hasMissing ? "(Minor differences)" :
                                "(Some mods missing)";
                CreateLabel(root, "ReloadHint", reason,
                    new Vector3(-WINDOW_WIDTH/4, buttonY - 30, 0), 12, secondaryTextColor, uiFont, ttfFont, 100)
                    .alignment = NGUIText.Alignment.Center;
            }
            
            // LOAD ANYWAY button
            if (isAttemptingLoad)
            {
                var loadBtn = CreateButton(root, "LoadAnywayBtn", "LOAD ANYWAY",
                    new Vector3(WINDOW_WIDTH/4, buttonY, 0), 18, Color.white, uiFont, ttfFont, 200, 45,
                    () => {
                        onLoadAnyway?.Invoke();
                        Close();
                    });

                var loadTex = loadBtn.GetComponent<UITexture>();
                if (loadTex != null) loadTex.color = requestedBrown;
                
                CreateLabel(root, "LoadHint", "(Manual override)",
                    new Vector3(WINDOW_WIDTH/4, buttonY - 30, 0), 12, secondaryTextColor, uiFont, ttfFont, 100)
                    .alignment = NGUIText.Alignment.Center;
            }
            
            // === STATUS LINE ===
            string statusText = allMatch ? "✓ Mods match - safe to play" :
                               hasMissing && hasVersionDiff ? $"⚠ {comparison.Count(c => c.status == ModCompareStatus.Missing)} missing, {comparison.Count(c => c.status == ModCompareStatus.VersionDiff)} version diff" :
                               hasMissing ? $"⚠ {comparison.Count(c => c.status == ModCompareStatus.Missing)} mod(s) missing" :
                               hasExtra && !hasMissing ? $"⚠ {comparison.Count(c => c.status == ModCompareStatus.Extra)} extra mod(s) active" :
                               $"~ {comparison.Count(c => c.status == ModCompareStatus.VersionDiff)} version difference(s)";
            
            Color statusColor = allMatch ? COLOR_MATCH : (hasMissing ? COLOR_MISSING : COLOR_VERSION_DIFF);
            var statusLabel = CreateLabel(root, "Status", statusText,
                new Vector3(0, -WINDOW_HEIGHT/2 + 20, 0), 16, statusColor, uiFont, ttfFont, 100);
            statusLabel.alignment = NGUIText.Alignment.Center;
            
            UIDebug.LogTimed($"Window built. Active={activeMods.Count}, Saved={savedMods.Length}, Comparison={comparison.Count}");
        }
        
        // === COMPARISON LOGIC ===
        
        private enum ModCompareStatus { Match, VersionDiff, Extra, Missing }
        
        private class ModCompareEntry
        {
            public string activeId;
            public string activeVersion;
            public string savedId;
            public string savedVersion;
            public ModCompareStatus status;
        }
        
        private List<ModCompareEntry> BuildModComparison(List<ModEntry> active, LoadedModInfo[] saved)
        {
            var result = new List<ModCompareEntry>();
            
            // 1. Check saved mods against active (Missing/Match/VersionDiff)
            foreach (var s in saved)
            {
                var entry = new ModCompareEntry { savedId = s.modId, savedVersion = s.version };
                var match = active.Find(a => a.Id.Equals(s.modId, StringComparison.OrdinalIgnoreCase));
                
                if (match == null)
                {
                    entry.status = ModCompareStatus.Missing;
                }
                else
                {
                    entry.activeId = match.Id;
                    entry.activeVersion = match.Version;
                    entry.status = match.Version == s.version ? ModCompareStatus.Match : ModCompareStatus.VersionDiff;
                }
                
                result.Add(entry);
            }

            // 2. Check active mods for extras
            foreach (var a in active)
            {
                if (!saved.Any(s => string.Equals(s.modId, a.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new ModCompareEntry {
                        activeId = a.Id,
                        activeVersion = a.Version,
                        status = ModCompareStatus.Extra
                    });
                }
            }
            
            return result;
        }
        
        private void Update()
        {
            // Handle Escape to close this window WITHOUT propagating to underlying panels
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                // Mark input as consumed to prevent underlying panels from also receiving Escape
                Input.ResetInputAxes();
            }
        }
        
        private Color GetStatusColor(ModCompareStatus status)
        {
            switch (status)
            {
                case ModCompareStatus.Match: return new Color(0.2f, 0.6f, 0.2f); // Darker green for paper
                case ModCompareStatus.VersionDiff: return new Color(0.6f, 0.5f, 0f); // Darker yellow/brown
                case ModCompareStatus.Extra: return new Color(0.6f, 0.5f, 0f);
                case ModCompareStatus.Missing: return new Color(0.7f, 0.2f, 0.2f); // Darker red
                default: return Color.black;
            }
        }
        
        // === CLONING HELPERS ===
        
        private static GameObject _clipboardTemplate;

        private bool CloneClipboardVisuals(Transform root)
        {
            // Use cached template if available
            if (_clipboardTemplate != null)
            {
                CloneSpecificVisual(root, _clipboardTemplate);
                return true;
            }
            
            try
            {
                MMLog.Write("[ModAPI] Searching for clipboard visuals...");
                
                GameObject templateGroup = null;
                
                // First-time search: Broad search for any clipboard-like objects or paper sprites
                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allGos)
                {
                    string lowName = go.name.ToLower();
                    if (lowName.Contains("bgfamily") || lowName.Equals("paper") || lowName.Contains("journalpanel"))
                    {
                        templateGroup = go;
                        break;
                    }
                }
                
                if (templateGroup == null)
                {
                    // Fallback search for any UISprite with "Paper" in name
                    var allSprites = Resources.FindObjectsOfTypeAll<UISprite>();
                    foreach (var s in allSprites)
                    {
                        if (s.name.ToLower().Contains("paper"))
                        {
                            _clipboardTemplate = s.gameObject; // Cache it
                            CloneSpecificVisual(root, s.gameObject);
                            return true;
                        }
                    }
                    MMLog.Write("[ModAPI] No clipboard template found. Using fallback.");
                    return false;
                }

                MMLog.Write("[ModAPI] Cloning visuals from: " + templateGroup.name);
                
                // If it's just a single object (like a sprite GO), clone it directly
                if (templateGroup.GetComponent<UISprite>() != null || templateGroup.GetComponent<UITexture>() != null)
                {
                    _clipboardTemplate = templateGroup; // Cache it
                    CloneSpecificVisual(root, templateGroup);
                    return true;
                }

                bool foundAny = false;
                foreach (Transform child in templateGroup.transform)
                {
                    string name = child.name.ToLower();
                    if (name.Contains("bg") || name.Contains("paper") || name.Contains("visual"))
                    {
                        var sprite = child.GetComponent<UISprite>();
                        var tex = child.GetComponent<UITexture>();
                        if (sprite != null || tex != null)
                        {
                            if (!foundAny) _clipboardTemplate = child.gameObject; // Cache first found
                            CloneSpecificVisual(root, child.gameObject);
                            foundAny = true;
                        }
                    }
                }
                return foundAny;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SaveDetailsWindow] Error cloning clipboard visuals: " + ex.Message);
                return false;
            }
        }

        private void CloneSpecificVisual(Transform root, GameObject original)
        {
            var clone = (GameObject)UnityEngine.Object.Instantiate(original);
            clone.transform.SetParent(root, false);
            clone.name = "Cloned_" + original.name;
            clone.transform.localPosition = Vector3.zero;
            
            // Rescale to fit our 900x860 window. 
            // Most game clipboards are designed for 1080p, let's assume ~800px base height.
            float scaleFactor = (float)WINDOW_HEIGHT / 800f;
            clone.transform.localScale = original.transform.localScale * scaleFactor;
            
            // Force reasonable dimensions if it's a sprite but has weird scale
            var sprite = clone.GetComponent<UISprite>();
            if (sprite != null)
            {
                sprite.width = WINDOW_WIDTH;
                sprite.height = WINDOW_HEIGHT;
            }
            
            clone.transform.localRotation = original.transform.localRotation;
            clone.layer = root.gameObject.layer;

            foreach (var b in clone.GetComponentsInChildren<UIButton>(true)) UnityEngine.Object.Destroy(b);
            foreach (var l in clone.GetComponentsInChildren<UILabel>(true)) UnityEngine.Object.Destroy(l.gameObject);
            foreach (var c in clone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);

            var widgets = clone.GetComponentsInChildren<UIWidget>(true);
            foreach (var w in widgets)
            {
                w.gameObject.layer = root.gameObject.layer;
                w.depth = 10;
            }
            clone.SetActive(true);
        }
        
        private GameObject CreateTexturedBox(Transform parent, string name, Vector3 pos, int w, int h, Color color, int depth, bool addCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;
            
            var tex = go.AddComponent<UITexture>();
            tex.mainTexture = _whiteTexture;
            tex.width = w;
            tex.height = h;
            tex.depth = depth;
            tex.color = color;
            
            if (addCollider)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(w, h, 1);
            }
            
            return go;
        }
        
        private UILabel CreateLabel(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;
            
            var label = go.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.depth = depth;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.bitmapFont = uiFont;
            label.trueTypeFont = ttfFont;
            
            return label;
        }
        
        private GameObject CreateButton(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int w, int h, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = pos;
            
            // Background
            var bg = go.AddComponent<UITexture>();
            bg.mainTexture = _whiteTexture;
            bg.width = w;
            bg.height = h;
            bg.depth = 100;
            bg.color = new Color(0.44f, 0.32f, 0.24f, 1f); // RGB: 113, 82, 62 default
            
            // Label (child)
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.layer = go.layer;
            
            var label = labelGo.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.depth = 101;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.alignment = NGUIText.Alignment.Center;
            label.bitmapFont = uiFont;
            label.trueTypeFont = ttfFont;
            
            // Collider
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(w, h, 1);
            
            // Button
            var btn = go.AddComponent<UIButton>();
            btn.tweenTarget = go;
            
            if (onClick != null)
            {
                EventDelegate.Set(btn.onClick, () => onClick());
            }
            else
            {
                btn.isEnabled = false;
                bg.color = new Color(0.15f, 0.12f, 0.1f, 1f);
                label.color = COLOR_SUBTEXT;
            }
            
            return go;
        }
        
        private void Close()
        {
            if (_instance != null)
            {
                Destroy(_instance);
                _instance = null;
            }
        }
        
        // === RESTART REQUEST LOGIC ===
        
        [Serializable]
        private class RestartRequest
        {
            public string Action;
            public string LoadFromManifest;
        }

        private void CreateRestartRequest(int slotNumber, SlotManifest manifest)
        {
            try
            {
                // We don't overwrite loadorder.json. 
                // We create a restart.json file in SMM/Bin for the Manager to handle.
                
                // Assuming standard path: Sheltered/SMM/Bin/restart.json
                var gameRoot = Directory.GetParent(Application.dataPath).FullName;
                var smmBin = Path.Combine(Path.Combine(gameRoot, "SMM"), "Bin");

                if (!Directory.Exists(smmBin))
                {
                    // Fallback try - maybe SMM is elsewhere?
                    // But usually mod tools are in root.
                }
                
                // Construct path to manifest
                // Typically: mods/ModAPI/Saves/Standard/Slot_X/manifest.json
                var modsRoot = Path.Combine(gameRoot, "mods");
                var slotDir = Path.Combine(Path.Combine(Path.Combine(modsRoot, "ModAPI"), "Saves"), Path.Combine("Standard", $"Slot_{slotNumber}"));
                var manifestPath = Path.Combine(slotDir, "manifest.json");

                var req = new RestartRequest
                {
                    Action = "Restart",
                    LoadFromManifest = manifestPath
                };
                
                string json = JsonUtility.ToJson(req, true);
                
                // Write to SMM/Bin/restart.json
                string restartPath = Path.Combine(smmBin, "restart.json");
                
                // Ensure directory exists
                Directory.CreateDirectory(smmBin);
                
                File.WriteAllText(restartPath, json);
                MMLog.Write($"[SaveDetailsWindow] Created restart request at: {restartPath}");
                MMLog.Write($"[SaveDetailsWindow] Payload: {json}");

                Application.Quit();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed to create restart request: " + ex);
                // Show error to user? For now just log.
            }
        }

    }
}
