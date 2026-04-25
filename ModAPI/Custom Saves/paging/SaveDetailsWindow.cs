using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using ModAPI.Core;
using ModAPI.InputServices;
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

        public sealed class VerificationWindowOptions
        {
            public string Title = "MOD VERIFICATION";
            public string Subtitle;
            public string SavedColumnHeader = "SAVE FILE";
            public string MatchLoadButtonText = "LOAD SAVE";
            public string MismatchLoadButtonText = "LOAD ANYWAY";
            public string BlockedLoadButtonText = "LOAD BLOCKED";
            public string UnknownLoadButtonText = "LOAD BLOCKED";
            public string MatchLoadHint = "(Mods match)";
            public string MismatchLoadHint = "(Override warnings)";
            public string BlockedLoadHint = "(Resolve required mod issues)";
            public string UnknownBlockedHint = "(Metadata unavailable)";
            public string MatchStatusText = "Mods match - safe to play";
            public string UnknownBlockedStatusText = "Save metadata missing - load blocked";
            public string UnknownRecoverStatusText = "Save metadata missing - you can regenerate it from the current mods";
            public bool IncludeExtraMods = true;
            public bool AllowMismatchLoad = true;
            public bool AllowUnknownRecovery = true;
            public bool AllowAutoLoad = true;
        }
        
        // Colors for status indicators
        private static readonly Color COLOR_MATCH = new Color(0.3f, 0.9f, 0.3f);
        private static readonly Color COLOR_VERSION_DIFF = new Color(0.9f, 0.9f, 0.2f);
        private static readonly Color COLOR_MISSING = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color COLOR_HEADER = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color COLOR_TEXT = Color.white;
        private static readonly Color COLOR_SUBTEXT = new Color(0.7f, 0.7f, 0.7f);
        
        // Layout constants for the modal window
        private const int WINDOW_WIDTH = 900;
        private const int WINDOW_HEIGHT = 860;
        private const int COLUMN_WIDTH = 400;
        private const int ROW_HEIGHT = 42;
        
        public static void Show(SaveEntry entry, SlotManifest manifest, SaveVerification.VerificationState state, bool isAttemptingLoad, Action onLoadAnyway = null, Action onCancel = null)
        {
            Show(entry, manifest, state, isAttemptingLoad, onLoadAnyway, onCancel, null);
        }

        public static void ShowScenario(string scenarioName, SlotManifest manifest, SaveVerification.VerificationState state, Action onStart, Action onCancel = null)
        {
            VerificationWindowOptions options = new VerificationWindowOptions
            {
                Title = "SCENARIO MOD VERIFICATION",
                Subtitle = "Scenario: \"" + (scenarioName ?? "Unknown") + "\"",
                SavedColumnHeader = "REQUIRED BY SCENARIO",
                MatchLoadButtonText = "START SCENARIO",
                MismatchLoadButtonText = "START BLOCKED",
                BlockedLoadButtonText = "START BLOCKED",
                UnknownLoadButtonText = "START BLOCKED",
                MatchLoadHint = "(Required mods match)",
                MismatchLoadHint = "(Required mods must match)",
                BlockedLoadHint = "(Install or enable required mods)",
                UnknownBlockedHint = "(Scenario dependency metadata unavailable)",
                MatchStatusText = "Required mods match - scenario can start",
                UnknownBlockedStatusText = "Scenario dependency metadata missing - start blocked",
                IncludeExtraMods = false,
                AllowMismatchLoad = false,
                AllowUnknownRecovery = false,
                AllowAutoLoad = false
            };

            SaveEntry entry = new SaveEntry
            {
                name = scenarioName ?? "Custom Scenario",
                saveInfo = new SaveInfo { familyName = scenarioName ?? "Custom Scenario" }
            };

            Show(entry, manifest, state, true, onStart, onCancel, options);
        }

        private static void Show(SaveEntry entry, SlotManifest manifest, SaveVerification.VerificationState state, bool isAttemptingLoad, Action onLoadAnyway, Action onCancel, VerificationWindowOptions options)
        {
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
            win.BuildFullUI(root.transform, entry, manifest, state, isAttemptingLoad, onLoadAnyway, onCancel, uiFont, ttfFont, options ?? new VerificationWindowOptions());
        }

        private void BuildFullUI(Transform root, SaveEntry entry, SlotManifest manifest, 
            SaveVerification.VerificationState state, bool isAttemptingLoad, Action onLoadAnyway, Action onCancel,
            UIFont uiFont, Font ttfFont, VerificationWindowOptions options)
        {
            _onCancel = onCancel;

            int layer = root.gameObject.layer;
            
            // === DARK OVERLAY ===
            CreateTexturedBox(root, "DarkOverlay", Vector3.zero, 3000, 3000, 
                new Color(0f, 0f, 0f, 0.85f), 0, true);
            
            // === WINDOW BACKGROUND ===
            CreateTexturedBox(root, "WindowBackground", Vector3.zero, 
                WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.98f), 10, false);
            CreateTexturedBox(root, "WindowBorder", Vector3.zero, 
                WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);

            // Text colors
            Color primaryTextColor = COLOR_TEXT;
            Color secondaryTextColor = COLOR_SUBTEXT;
            Color titleColor = COLOR_HEADER;
            
            // === HEADER ===
            var titleLabel = CreateLabel(root, "Title", options.Title ?? "MOD VERIFICATION",
                new Vector3(0, WINDOW_HEIGHT/2 - 70, 0), 32, titleColor, uiFont, ttfFont, 100);
            titleLabel.alignment = NGUIText.Alignment.Center;
            
            // Subtitle with family name and days
            string subtitleText = options.Subtitle;
            if (string.IsNullOrEmpty(subtitleText))
            {
                string familyName = entry?.saveInfo?.familyName;
                if (string.IsNullOrEmpty(familyName)) familyName = "Unknown";
                int days = entry?.saveInfo?.daysSurvived ?? 0;
                subtitleText = $"Family: \"{familyName}\" - Day {days}";
            }
            var subtitleLabel = CreateLabel(root, "Subtitle", subtitleText,
                new Vector3(0, WINDOW_HEIGHT/2 - 105, 0), 22, secondaryTextColor, uiFont, ttfFont, 100);
            subtitleLabel.alignment = NGUIText.Alignment.Center;
            
            // === CLOSE [X] BUTTON ===
            // Positioned according to new width
            var closeBtn = CreateButton(root, "CloseBtn", "CLOSE", 
                new Vector3(WINDOW_WIDTH/2 - 80, WINDOW_HEIGHT/2 - 60, 0), 
                20, primaryTextColor, uiFont, ttfFont, 100, 40, () => {
                    onCancel?.Invoke();
                    Close();
                });
            
            // === ANALYZE MODS ===
            var activeMods = PluginManager.LoadedMods;
            var savedMods = manifest?.lastLoadedMods ?? new LoadedModInfo[0];
            var discovered = ModDiscovery.DiscoverAllMods();
            var comparison = SaveVerification.BuildModComparison(activeMods, savedMods, options.IncludeExtraMods);
            
            // === COLUMN HEADERS ===
            int headerY = WINDOW_HEIGHT/2 - 160;
            
            CreateLabel(root, "ActiveHeader", "ACTIVE",
                new Vector3(-WINDOW_WIDTH/4, headerY, 0), 24, titleColor, uiFont, ttfFont, 100)
                .alignment = NGUIText.Alignment.Center;
            
            CreateLabel(root, "SavedHeader", options.SavedColumnHeader ?? "SAVE FILE",
                new Vector3(WINDOW_WIDTH/4, headerY, 0), 24, titleColor, uiFont, ttfFont, 100)
                .alignment = NGUIText.Alignment.Center;
            
            // Divider line
            CreateTexturedBox(root, "HeaderDivider", new Vector3(0, headerY - 25, 0),
                WINDOW_WIDTH - 200, 2, new Color(0, 0, 0, 0.2f), 50, false);
            
            // === MOD LISTS (SCROLLABLE) ===
            int listStartY = headerY - 55;
            int listAreaHeight = 320; // Available height for mod list (before buttons/warnings)
            int maxVisibleRows = listAreaHeight / ROW_HEIGHT;
            
            // Create container for scroll content
            var modListContainer = new GameObject("ModListContainer");
            modListContainer.transform.SetParent(root, false);
            modListContainer.layer = root.gameObject.layer;
            modListContainer.transform.localPosition = Vector3.zero;
            
            // Track all mod row GameObjects for scrolling
            var modRowObjects = new List<GameObject>();
            int totalRows = Math.Max(activeMods.Count, savedMods.Length);
            
            int rowIndex = 0;
            
            // Active mods column (left)
            foreach (var mod in activeMods)
            {
                int y = listStartY - (rowIndex * ROW_HEIGHT);
                var status = comparison.Find(c => c.activeId == mod.Id);
                SaveVerification.ModCompareStatus compareStatus = status?.status ?? SaveVerification.ModCompareStatus.Match;
                Color color = GetStatusColor(compareStatus);
                
                string iconPrefix = "✓";
                string suffix = "";
                
                switch (compareStatus)
                {
                    case SaveVerification.ModCompareStatus.Extra: iconPrefix = "~"; suffix = " [NEW]"; break;
                    case SaveVerification.ModCompareStatus.VersionDiff: iconPrefix = "~"; break;
                }
                
                // Create a row container to group name and version labels
                var rowGO = new GameObject($"ActiveRow_{rowIndex}");
                rowGO.transform.SetParent(modListContainer.transform, false);
                rowGO.layer = modListContainer.layer;
                rowGO.transform.localPosition = new Vector3(-WINDOW_WIDTH/4, y, 0);
                
                var nameLabel = CreateLabel(rowGO.transform, $"Name", $"{iconPrefix} {mod.Name}{suffix}",
                    Vector3.zero, 18, color, uiFont, ttfFont, 100);
                nameLabel.alignment = NGUIText.Alignment.Center;
                
                var verLabel = CreateLabel(rowGO.transform, $"Version", $"   v{mod.Version}",
                    new Vector3(0, -18, 0), 14, COLOR_SUBTEXT, uiFont, ttfFont, 100);
                verLabel.alignment = NGUIText.Alignment.Center;
                
                modRowObjects.Add(rowGO);
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
                SaveVerification.ModCompareStatus compareStatus = status?.status ?? SaveVerification.ModCompareStatus.Match;
                Color color = GetStatusColor(compareStatus);
                
                string icon = "✓";
                string suffix = "";
                
                switch (compareStatus)
                {
                    case SaveVerification.ModCompareStatus.Missing:
                        icon = "✗"; 
                        suffix = " [MISSING]";
                        break;
                    case SaveVerification.ModCompareStatus.VersionDiff: icon = "~"; suffix = " [VER DIFF]"; break;
                }
                
                var diskMod = discovered.Find(d => d.Id.Equals(saved.modId, StringComparison.OrdinalIgnoreCase));
                string displayName = diskMod != null ? diskMod.Name : saved.modId;

                // Create a row container
                var rowGO = new GameObject($"SavedRow_{rowIndex}");
                rowGO.transform.SetParent(modListContainer.transform, false);
                rowGO.layer = modListContainer.layer;
                rowGO.transform.localPosition = new Vector3(WINDOW_WIDTH/4, y, 0);

                var savedName = CreateLabel(rowGO.transform, $"Name", $"{icon} {displayName}{suffix}",
                    Vector3.zero, 18, color, uiFont, ttfFont, 100);
                savedName.alignment = NGUIText.Alignment.Center;
                
                // Collect warnings from missing mods using the display name
                if (compareStatus == SaveVerification.ModCompareStatus.Missing && saved.warnings != null && saved.warnings.Length > 0)
                {
                    foreach (var w in saved.warnings)
                    {
                        if (!string.IsNullOrEmpty(w)) warnings.Add($"[{displayName}] {w}");
                    }
                }
                
                string verText = compareStatus == SaveVerification.ModCompareStatus.VersionDiff && status != null
                    ? $"   (required: v{saved.version}, active: v{status.activeVersion})"
                    : (string.IsNullOrEmpty(saved.version) ? "   any version" : $"   v{saved.version}");
                var savedVer = CreateLabel(rowGO.transform, $"Version", verText,
                    new Vector3(0, -18, 0), 14, COLOR_SUBTEXT, uiFont, ttfFont, 100);
                savedVer.alignment = NGUIText.Alignment.Center;
                
                modRowObjects.Add(rowGO);
                rowIndex++;
            }
            
            // Add scroll helper if content exceeds visible area
            if (totalRows > maxVisibleRows)
            {
                var scrollHelper = modListContainer.AddComponent<ModListScrollHelper>();
                scrollHelper.Initialize(modRowObjects, listStartY, ROW_HEIGHT, 
                    listStartY - listAreaHeight, listStartY, 
                    -WINDOW_WIDTH/2, WINDOW_WIDTH/2); // Full width for scroll input
            }
            
            // === WARNINGS SECTION ===
            int warningY = -WINDOW_HEIGHT/2 + 250; // Raised from 180
            
            if (warnings.Count > 0)
            {
                var warnHeader = CreateLabel(root, "WarningHeader", "⚠️ WARNINGS FROM MISSING MODS:",
                    new Vector3(0, warningY, 0), 18, COLOR_MISSING, uiFont, ttfFont, 100);
                warnHeader.alignment = NGUIText.Alignment.Center;
                
                // Red background box
                int boxHeight = 100;
                int boxWidth = WINDOW_WIDTH - 60;
                CreateTexturedBox(root, "WarningBg", new Vector3(0, warningY - 60, 0),
                    boxWidth, boxHeight, new Color(0.3f, 0.1f, 0.1f, 0.8f), 40, true);
                
                // Scrollable container with clipping
                var warnClippedGO = new GameObject("WarningClippedArea");
                warnClippedGO.transform.SetParent(root, false);
                warnClippedGO.layer = root.gameObject.layer;
                // Move closer to camera (-50) to ensure it renders on top of the red box
                warnClippedGO.transform.localPosition = new Vector3(0, warningY - 60, -50);
                
                var warnPanel = warnClippedGO.AddComponent<UIPanel>();
                warnPanel.depth = 10100; // higher than main panel
                warnPanel.clipping = UIDrawCall.Clipping.SoftClip;
                warnPanel.baseClipRegion = new Vector4(0f, 0f, boxWidth, boxHeight);
                
                // Construct the warning label within the scrollable area
                string warningText = string.Join("\n", warnings.ToArray());
                // Local Z -1 to be slightly in front of the panel just in case
                var warnLabel = CreateLabel(warnClippedGO.transform, "WarningText", warningText,
                    new Vector3(0, 0, -1), 15, COLOR_TEXT, uiFont, ttfFont, 120);
                
                warnLabel.alignment = NGUIText.Alignment.Center;
                warnLabel.overflowMethod = UILabel.Overflow.ResizeHeight;
                warnLabel.width = boxWidth - 40;
                warnLabel.pivot = UIWidget.Pivot.Center; // Vertical center by default
                warnLabel.ProcessText();
                warnLabel.MarkAsChanged();
                
                // Add scroll helper for the label
                var scrollHelper = warnClippedGO.AddComponent<WarningScrollHelper>();
                scrollHelper.Initialize(warnLabel, boxHeight);
            }
            
            // === BUTTON ROW ===
            int buttonY = -WINDOW_HEIGHT/2 + 100;
            
            // Determine button states
            bool hasMissing = comparison.Any(c => c.status == SaveVerification.ModCompareStatus.Missing);
            bool hasVersionDiff = comparison.Any(c => c.status == SaveVerification.ModCompareStatus.VersionDiff);
            bool hasExtra = comparison.Any(c => c.status == SaveVerification.ModCompareStatus.Extra);
            bool hasUnknownState = state == SaveVerification.VerificationState.Unknown;
            bool allMatch = !hasMissing && !hasVersionDiff && !hasExtra && !hasUnknownState;
            
            bool canReload = options.AllowAutoLoad && !allMatch && !hasUnknownState;
            if (canReload && hasMissing)
            {
                var missingEntries = comparison.Where(c => c.status == SaveVerification.ModCompareStatus.Missing);
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
            int absoluteSlot = entry?.absoluteSlot ?? 0; 
            bool canRecoverUnknown = options.AllowUnknownRecovery && hasUnknownState && onLoadAnyway != null && absoluteSlot > 0;
             
            var reloadBtn = CreateButton(root, "ReloadBtn", "AUTO-LOAD MODS",
                new Vector3(-WINDOW_WIDTH/4, buttonY, 0), 18, Color.white, uiFont, ttfFont, 240, 45,
                canReload ? (Action)(() => CreateRestartRequest(absoluteSlot, manifest, entry)) : null);
            
            // Apply requested color to background
            var reloadTex = reloadBtn.GetComponent<UITexture>();
            if (reloadTex != null) reloadTex.color = canReload ? requestedBrown : new Color(0.25f, 0.2f, 0.15f, 1f);

            if (!canReload)
            {
                string reason = hasUnknownState ? options.UnknownBlockedHint :
                                allMatch ? "(Already matching)" : 
                                (hasVersionDiff || hasExtra) && !hasMissing ? "(Minor differences)" :
                                "(Some mods missing)";
                CreateLabel(root, "ReloadHint", reason,
                    new Vector3(-WINDOW_WIDTH/4, buttonY - 30, 0), 12, secondaryTextColor, uiFont, ttfFont, 100)
                    .alignment = NGUIText.Alignment.Center;
            }
            
            // LOAD button - always shown
            // Text changes based on whether mods match:
            // - "LOAD SAVE" when mods match (not an "anyway" situation)
            // - "LOAD ANYWAY" when mods differ (user is overriding warnings)
            Action loadButtonAction = null;
            bool mismatchBlocked = !options.AllowMismatchLoad && (hasMissing || hasVersionDiff || hasExtra);
            if (hasUnknownState)
            {
                if (canRecoverUnknown)
                {
                    loadButtonAction = () =>
                    {
                        if (TryRecoverManifestFromCurrentMods(absoluteSlot, entry))
                        {
                            onLoadAnyway?.Invoke();
                            Close();
                        }
                    };
                }
            }
            else if (onLoadAnyway != null && !mismatchBlocked)
            {
                loadButtonAction = () =>
                {
                    onLoadAnyway();
                    Close();
                };
            }

            bool canLoad = loadButtonAction != null;
            string loadButtonText = hasUnknownState
                ? (canRecoverUnknown ? "RECOVER & LOAD" : options.UnknownLoadButtonText)
                : (allMatch ? options.MatchLoadButtonText : (mismatchBlocked ? options.BlockedLoadButtonText : options.MismatchLoadButtonText));
            string loadHintText = hasUnknownState
                ? (canRecoverUnknown ? "(Rebuild manifest from current mods)" : options.UnknownBlockedHint)
                : (allMatch ? options.MatchLoadHint : (mismatchBlocked ? options.BlockedLoadHint : options.MismatchLoadHint));
             
            var loadBtn = CreateButton(root, "LoadBtn", loadButtonText,
                new Vector3(WINDOW_WIDTH/4, buttonY, 0), 18, Color.white, uiFont, ttfFont, 200, 45,
                loadButtonAction);

            var loadTex = loadBtn.GetComponent<UITexture>();
            if (loadTex != null) loadTex.color = canLoad ? requestedBrown : new Color(0.25f, 0.2f, 0.15f, 1f);
            
            CreateLabel(root, "LoadHint", loadHintText,
                new Vector3(WINDOW_WIDTH/4, buttonY - 30, 0), 12, secondaryTextColor, uiFont, ttfFont, 100)
                .alignment = NGUIText.Alignment.Center;
            
            // === STATUS LINE ===
            string statusText = allMatch ? "✓ Mods match - safe to play" :
                               hasMissing && hasVersionDiff ? $"⚠ {comparison.Count(c => c.status == SaveVerification.ModCompareStatus.Missing)} missing, {comparison.Count(c => c.status == SaveVerification.ModCompareStatus.VersionDiff)} version diff" :
                               hasMissing ? $"⚠ {comparison.Count(c => c.status == SaveVerification.ModCompareStatus.Missing)} mod(s) missing" :
                               hasExtra && !hasMissing ? $"⚠ {comparison.Count(c => c.status == SaveVerification.ModCompareStatus.Extra)} extra mod(s) active" :
                               $"~ {comparison.Count(c => c.status == SaveVerification.ModCompareStatus.VersionDiff)} version difference(s)";
            
            if (hasUnknownState)
            {
                statusText = canRecoverUnknown
                    ? "? Save metadata missing - you can regenerate it from the current mods"
                    : "? Save metadata missing - load blocked";
            }
            if (allMatch)
                statusText = options.MatchStatusText;
            if (hasUnknownState)
                statusText = canRecoverUnknown ? "? " + options.UnknownRecoverStatusText : "? " + options.UnknownBlockedStatusText;
            if (mismatchBlocked && !hasUnknownState)
                statusText = "Scenario cannot start: " + statusText;

            Color statusColor = hasUnknownState ? COLOR_MISSING : (allMatch ? COLOR_MATCH : (hasMissing ? COLOR_MISSING : COLOR_VERSION_DIFF));
            var statusLabel = CreateLabel(root, "Status", statusText,
                new Vector3(0, -WINDOW_HEIGHT/2 + 20, 0), 16, statusColor, uiFont, ttfFont, 100);
            statusLabel.alignment = NGUIText.Alignment.Center;
        }
        private Action _onCancel;

        private void SetOnCancel(Action onCancel)
        {
            _onCancel = onCancel;
        }

        private void Update()
        {
            // Handle Escape to close this window WITHOUT propagating to underlying panels
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _onCancel?.Invoke();
                Close();
                // Consume input to prevent underlying panels from receiving Escape
                Input.ResetInputAxes();
            }
        }
        
        private Color GetStatusColor(SaveVerification.ModCompareStatus status)
        {
            switch (status)
            {
                case SaveVerification.ModCompareStatus.Match: return new Color(0.2f, 0.6f, 0.2f); // Darker green for paper
                case SaveVerification.ModCompareStatus.VersionDiff: return new Color(0.6f, 0.5f, 0f); // Darker yellow/brown
                case SaveVerification.ModCompareStatus.Extra: return new Color(0.6f, 0.5f, 0f);
                case SaveVerification.ModCompareStatus.Missing: return new Color(0.7f, 0.2f, 0.2f); // Darker red
                default: return Color.black;
            }
        }
        
        // === UI HELPER METHODS ===
        
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

        private bool TryRecoverManifestFromCurrentMods(int slotNumber, SaveEntry entry)
        {
            if (slotNumber <= 0)
            {
                MMLog.WriteError("[SaveDetailsWindow] Cannot recover manifest for invalid slot number: " + slotNumber);
                return false;
            }

            try
            {
                var manifest = SaveRegistryCore.CreateCurrentManifestSnapshot(entry != null ? entry.saveInfo : null);
                string manifestPath;
                string error;
                if (!SaveRegistryCore.TryWriteSlotManifest("Standard", slotNumber, manifest, out manifestPath, out error))
                {
                    MMLog.WriteError("[SaveDetailsWindow] Failed to recover manifest from current mods: " + error);
                    return false;
                }

                MMLog.Write("[SaveDetailsWindow] Recovered manifest from current mods at: " + manifestPath);
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SaveDetailsWindow] Unexpected error recovering manifest: " + ex);
                return false;
            }
        }
        
        // === RESTART REQUEST LOGIC ===
        
        [Serializable]
        private class RestartRequest
        {
            public string Action;
            public string LoadFromManifest;
            public bool RequireExactManifest;
        }

        private void CreateRestartRequest(int slotNumber, SlotManifest manifest, SaveEntry entry)
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
                // Determine path to the slot manifest: mods/ModAPI/Saves/Standard/Slot_X/manifest.json
                var modsRoot = Path.Combine(gameRoot, "mods");
                var slotDir = Path.Combine(Path.Combine(Path.Combine(modsRoot, "ModAPI"), "Saves"), Path.Combine("Standard", $"Slot_{slotNumber}"));
                var manifestPath = Path.Combine(slotDir, "manifest.json");

                MMLog.WriteDebug($"[SaveDetailsWindow] Checking for manifest at: {manifestPath}");
                
                if (!File.Exists(manifestPath))
                {
                    if (manifest == null || manifest.lastLoadedMods == null)
                    {
                        MMLog.WriteError("[SaveDetailsWindow] Manifest missing and no trusted manifest payload is available. Restart request aborted.");
                        return;
                    }

                    manifest.lastModified = DateTime.UtcNow.ToString("o");
                    if (string.IsNullOrEmpty(manifest.family_name))
                        manifest.family_name = entry != null && entry.saveInfo != null ? entry.saveInfo.familyName : "Unknown";

                    string writeError;
                    if (!SaveRegistryCore.TryWriteSlotManifest("Standard", slotNumber, manifest, out manifestPath, out writeError))
                    {
                        MMLog.WriteError("[SaveDetailsWindow] Failed to persist trusted manifest for restart: " + writeError);
                        return;
                    }

                    MMLog.WriteDebug($"[SaveDetailsWindow] Restored manifest for restart at: {manifestPath}");
                }
                else
                {
                    MMLog.WriteDebug($"[SaveDetailsWindow] Manifest detected at: {manifestPath}");
                }

                var req = new RestartRequest
                {
                    Action = "Restart",
                    LoadFromManifest = manifestPath,
                    RequireExactManifest = true
                };
                
                string json = JsonUtility.ToJson(req, true);
                
                // Write to SMM/Bin/restart.json
                string restartPath = Path.Combine(smmBin, "restart.json");
                
                // Ensure directory exists
                Directory.CreateDirectory(smmBin);
                
                File.WriteAllText(restartPath, json);
                MMLog.Write($"[SaveDetailsWindow] Application restart requested for automated mod loading. Request saved to: {restartPath}");
                MMLog.WriteDebug($"[SaveDetailsWindow] Restart Payload: {json}");

                Application.Quit();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed to create restart request: " + ex);
                // Show error to user? For now just log.
            }
        }

    }
    
    /// <summary>
    /// Helper component for scrolling the mod comparison list in SaveDetailsWindow.
    /// Moves all row GameObjects together on the Y axis when the user scrolls.
    /// </summary>
    internal class ModListScrollHelper : MonoBehaviour
    {
        private List<GameObject> _items = new List<GameObject>();
        private Dictionary<GameObject, float> _originalYPositions = new Dictionary<GameObject, float>();
        private float _scrollOffset = 0f;
        private float _minOffset;  // Minimum scroll offset (0 = at top)
        private float _maxOffset;  // Maximum scroll offset (positive = scrolled down)
        private float _rowHeight;
        private float _minX;
        private float _maxX;
        
        /// <summary>
        /// Initialize the scroll helper with row items and bounds.
        /// </summary>
        public void Initialize(List<GameObject> items, float startY, float rowHeight, 
            float minY, float maxY, float minX, float maxX)
        {
            _items = items;
            _rowHeight = rowHeight;
            _minX = minX;
            _maxX = maxX;
            
            // Store original Y positions for each item
            foreach (var item in items)
            {
                if (item != null)
                {
                    _originalYPositions[item] = item.transform.localPosition.y;
                }
            }
            
            // Calculate scroll limits based on content vs visible area
            float visibleHeight = maxY - minY;
            float contentHeight = items.Count * rowHeight;
            
            _minOffset = 0f;
            _maxOffset = Mathf.Max(0f, contentHeight - visibleHeight);
            
            MMLog.WriteDebug($"[ModListScrollHelper] Initialized with {items.Count} items. Max scroll offset: {_maxOffset}");
        }
        
        void Update()
        {
            if (_items == null || _items.Count == 0) return;
            if (_maxOffset <= 0) return; // No scrolling needed
            
            float scroll;
            if (!ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.ForUiRange(_minX, _maxX), out scroll))
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.PageUp))
                    scroll = 1f;
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.PageDown))
                    scroll = -1f;
            }
            if (scroll == 0f) return;
            
            // Update scroll offset (negative scroll = move content up)
            float scrollSpeed = _rowHeight; 
            _scrollOffset -= scroll * scrollSpeed;
            _scrollOffset = Mathf.Clamp(_scrollOffset, _minOffset, _maxOffset);
            
            // Apply offset to all items using their original positions
            foreach (var item in _items)
            {
                if (item == null || !_originalYPositions.ContainsKey(item)) continue;
                
                float originalY = _originalYPositions[item];
                var pos = item.transform.localPosition;
                
                // Offset the item Y position by the scroll amount
                item.transform.localPosition = new Vector3(pos.x, originalY - _scrollOffset, pos.z);
            }
        }
    }
    
    /// <summary>
    /// Helper for scrolling a single long UILabel within a clipped area.
    /// </summary>
    internal class WarningScrollHelper : MonoBehaviour
    {
        private UILabel _label;
        private float _clipHeight;
        private float _scrollY = 0f;
        
        public void Initialize(UILabel label, float clipHeight)
        {
            _label = label;
            _clipHeight = clipHeight;
            _scrollY = 0f;
        }
        
        void Update()
        {
            if (_label == null) return;
            
            float contentHeight = _label.height;
            if (contentHeight <= _clipHeight) return;
            
            float scroll;
            if (!ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.ForUiRange(-400f, 400f), out scroll))
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.PageUp))
                    scroll = 1f;
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.PageDown))
                    scroll = -1f;
            }
            if (scroll == 0f) return;
            
            float scrollSpeed = 30f;
            _scrollY -= scroll * scrollSpeed;
            
            // Limit scroll based on label height vs clip area (Vertical centering logic)
            float maxScroll = (contentHeight - _clipHeight) / 2f;
            _scrollY = Mathf.Clamp(_scrollY, -maxScroll, maxScroll);
            
            _label.transform.localPosition = new Vector3(0, _scrollY, -1);
        }
    }
}
