using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Saves;
using ModAPI.UI;
using ModAPI.Hooks;
using HarmonyLib;

namespace ModAPI.Hooks.Paging
{
    internal static class SaveVerification
    {
        // Key icons by the button INSTANCE to handle page swapping correctly
        private static Dictionary<SaveSlotButton, GameObject> _slotIcons = new Dictionary<SaveSlotButton, GameObject>();
        
        // Colors for status indicators (brighter for dark brown background)
        private static readonly Color COLOR_MATCH = new Color(0.3f, 1.0f, 0.3f);
        private static readonly Color COLOR_VERSION_DIFF = new Color(1.0f, 1.0f, 0.2f);
        private static readonly Color COLOR_MISSING = new Color(1.0f, 0.3f, 0.3f);
        private static readonly Color COLOR_UNKNOWN = new Color(1.0f, 0.6f, 0.2f);

        private static UIFont _cachedUIFont;
        private static Font _cachedTTFFont;

        public static void UpdateIcons(SlotSelectionPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            var buttons = panel.GetComponentsInChildren<SaveSlotButton>(true);
            int page = PagingManager.GetPage(panel);

            // Reset all icons for known buttons
            foreach(var kv in _slotIcons) 
            {
                if(kv.Value != null) kv.Value.SetActive(false);
            }

            // Find an atlas that has the sprites we need
            UIAtlas sampleAtlas = null;
            string[] desiredSprites = { "Checkmark", "Tick", "Cancel", "Close" };
            var allAtlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
            
            foreach(var a in allAtlases)
            {
                if (a.spriteList == null || a.spriteList.Count == 0) continue;
                foreach(var spriteName in desiredSprites)
                {
                    if (a.GetSprite(spriteName) != null)
                    {
                        sampleAtlas = a;
                        break;
                    }
                }
                if (sampleAtlas != null) break;
            }

            if (sampleAtlas == null) return;

            // Sort buttons by visual Y position to ensure top-to-bottom mapping
            var sortedButtons = buttons.Where(b => b != null && b.gameObject.activeInHierarchy)
                                       .OrderByDescending(b => b.transform.localPosition.y)
                                       .ToList();

            for (int i = 0; i < sortedButtons.Count; i++)
            {
                var btn = sortedButtons[i];
                int uiSlotIndex = i; // Visual index 0, 1, 2

                // Calculate absolute slot
                int absoluteSlot;
                if (page == 0) absoluteSlot = uiSlotIndex + 1;
                else absoluteSlot = 4 + (page - 1) * 3 + uiSlotIndex;

                // Find save entry
                var all = ExpandedVanillaSaves.List();
                SaveEntry target = null;
                foreach(var e in all) 
                {
                    if (e.absoluteSlot == absoluteSlot) { target = e; break; }
                }

                if (target == null && absoluteSlot <= 3)
                {
                    // For vanilla slots 1-3, try to read save info
                    var saveInfo = SaveRegistryCore.ReadVanillaSaveInfo(absoluteSlot);
                    
                    // Only create entry if save file actually exists
                    if (saveInfo != null)
                    {
                        target = new SaveEntry { 
                            id = "vanilla_slot_" + absoluteSlot,
                            absoluteSlot = absoluteSlot,
                            name = "Slot " + absoluteSlot,
                            saveInfo = saveInfo
                        };
                    }
                }

                // Skip icon creation if no save exists
                if (target == null)
                {
                    // Hide icon if it was previously created for this button
                    if (_slotIcons.ContainsKey(btn))
                    {
                        _slotIcons[btn].SetActive(false);
                    }
                    continue;
                }

                // Create/Get Icon GameObject
                GameObject iconGO;
                if (!_slotIcons.ContainsKey(btn))
                {
                    // Create container
                    iconGO = new GameObject("VerificationBtn_" + absoluteSlot);
                    iconGO.transform.SetParent(btn.transform, false);
                    iconGO.layer = btn.gameObject.layer;
                    
                    // Get Safe Depth
                    var parentPanel = NGUITools.FindInParents<UIPanel>(btn.gameObject);
                    int baseDepth = UIUtil.ComputeSafeDepth(parentPanel, 50);
                    
                    // Background: Create a UITexture with a white pixel texture
                    // This is the ONLY way to get a colored box in NGUI without sprites
                    var bgTex = iconGO.AddComponent<UITexture>();
                    
                    // Create a simple 2x2 white texture
                    var whiteTex = new Texture2D(2, 2);
                    for (int x = 0; x < 2; x++)
                        for (int y = 0; y < 2; y++)
                            whiteTex.SetPixel(x, y, Color.white);
                    whiteTex.Apply();
                    
                    bgTex.mainTexture = whiteTex;
                    bgTex.depth = baseDepth;
                    bgTex.width = 60;
                    bgTex.height = 60;
                    bgTex.pivot = UIWidget.Pivot.Center;
                    bgTex.color = new Color(0.3f, 0.25f, 0.2f, 0.9f); // Dark brown, more opaque
                    
                    // Icon sprite (checkmark/X) as child
                    var iconChild = new GameObject("Icon");
                    iconChild.transform.SetParent(iconGO.transform, false);
                    iconChild.layer = iconGO.layer;
                    var iconSprite = iconChild.AddComponent<UISprite>();
                    iconSprite.atlas = sampleAtlas; // The one we found with Checkmark/Tick
                    iconSprite.depth = baseDepth + 5; 
                    iconSprite.width = 36;
                    iconSprite.height = 36;
                    iconSprite.pivot = UIWidget.Pivot.Center;
                    
                    // Collider and button on the parent container
                    var col = iconGO.AddComponent<BoxCollider>();
                    col.size = new Vector3(70, 70, 1);
                    var uiBtn = iconGO.AddComponent<UIButton>();
                    uiBtn.tweenTarget = iconGO; 
                    
                    // Initial setup of cached fonts if missing
                    if (_cachedUIFont == null && _cachedTTFFont == null)
                    {
                        var label = panel.GetComponentInChildren<UILabel>();
                        if (label != null)
                        {
                            _cachedUIFont = label.bitmapFont;
                            _cachedTTFFont = label.trueTypeFont;
                        }
                    }
                    
                    _slotIcons[btn] = iconGO;
                }
                else
                {
                    iconGO = _slotIcons[btn];
                }

                iconGO.SetActive(true);
                // local Z: -20 to win raycast against slot button
                iconGO.transform.localPosition = new Vector3(-320, 0, -20);
                
                var iconChildObj = iconGO.transform.Find("Icon");
                UISprite childSprite = iconChildObj != null ? iconChildObj.GetComponent<UISprite>() : null;
                var currentBgSprite = iconGO.GetComponent<UISprite>();
                var iconButton = iconGO.GetComponent<UIButton>();
                var bgTexture = iconGO.GetComponent<UITexture>();
                
                // Get Manifest and State
                var slotRoot = DirectoryProvider.SlotRoot("Standard", absoluteSlot, false); 
                var manPath = Path.Combine(slotRoot, "manifest.json");
                
                VerificationState state = VerificationState.Match;
                SlotManifest manifest = null;

                if (File.Exists(manPath))
                {
                    try 
                    { 
                        string json = File.ReadAllText(manPath);
                        manifest = ModAPI.Saves.SaveRegistryCore.DeserializeSlotManifest(json);
                        state = Verify(manifest);
                    } 
                    catch (Exception ex)
                    {
                        state = VerificationState.Unknown;
                        MMLog.WriteError($"[SaveVerification] Failed to deserialize manifest for slot {absoluteSlot}: {ex.Message}");
                    }
                }
                else if (absoluteSlot > 3)
                {
                    state = VerificationState.Unknown;
                }

                // Sprite selection - ensure we have a label for the character icons
                UILabel childLabel = iconChildObj != null ? iconChildObj.GetComponent<UILabel>() : null;
                if (childLabel == null && iconChildObj != null)
                {
                    childLabel = iconChildObj.gameObject.AddComponent<UILabel>();
                    if (childSprite != null) childSprite.enabled = false;
                    
                    childLabel.bitmapFont = _cachedUIFont;
                    childLabel.trueTypeFont = _cachedTTFFont;
                    childLabel.fontSize = 48; // Larger font for better visibility
                    childLabel.pivot = UIWidget.Pivot.Center;
                    childLabel.alignment = NGUIText.Alignment.Center; // Center text horizontally
                    // Use depth relative to background - retrieve baseDepth from background texture
                    var parentBgTex = iconChildObj.parent.GetComponent<UITexture>();
                    int labelDepth = (parentBgTex != null ? parentBgTex.depth : 100) + 10;
                    childLabel.depth = labelDepth;
                    childLabel.overflowMethod = UILabel.Overflow.ResizeFreely; // Let it size naturally
                    childLabel.width = 60; // Match parent box size
                    childLabel.height = 60;
                }

                if (childLabel != null)
                {
                    // Ensure fonts are still valid if they were null during creation
                    if (childLabel.bitmapFont == null && childLabel.trueTypeFont == null)
                    {
                        childLabel.bitmapFont = _cachedUIFont;
                        childLabel.trueTypeFont = _cachedTTFFont;
                    }
                }

                // Determine icon and color based on state
                string iconPrefix = "✓";
                Color iconColor = COLOR_MATCH;
                float yOffset = 0f;
                
                switch (state)
                {
                    case VerificationState.Match:
                        iconPrefix = "✓";
                        iconColor = COLOR_MATCH;
                        yOffset = 0f;
                        break;
                    case VerificationState.VersionMismatch:
                        iconPrefix = "~";
                        iconColor = COLOR_VERSION_DIFF;
                        yOffset = -20f;
                        break;
                    case VerificationState.Warning:
                        iconPrefix = "~";
                        iconColor = COLOR_VERSION_DIFF;
                        yOffset = -20f;
                        break;
                    case VerificationState.Missing:
                        iconPrefix = "✗";
                        iconColor = COLOR_MISSING;
                        yOffset = 0f;
                        break;
                }
                
                if (state == VerificationState.Unknown)
                {
                    iconPrefix = "?";
                    iconColor = COLOR_UNKNOWN;
                    yOffset = 0f;
                }

                if (childLabel != null)
                {
                    childLabel.text = iconPrefix;
                    childLabel.color = iconColor;
                }
                
                // Ensure Icon child is vertically centered based on character
                if (iconChildObj != null)
                {
                    iconChildObj.localPosition = new Vector3(0, yOffset, 0);
                }

                var capTarget = target;
                var capManifest = manifest;
                var capState = state;
                int capSlot = absoluteSlot;
                
                EventDelegate.Set(iconButton.onClick, () => {
                    SaveDetailsWindow.Show(capTarget, capManifest, capState, false, () => {
                        int virtualSlot = (capSlot <= 3) ? capSlot : ((capSlot - 4) % 3) + 1;
                        var virtualSaveType = (SaveManager.SaveType)virtualSlot;

                        if (capSlot <= 3)
                        {
                            // For vanilla saves, ensure we bypass the anti-mod-mismatch loading block
                            ModAPI.Core.SaveProtectionPatches.LoadGamePatch._forceLoad = true;
                        }
                        else
                        {
                            // For custom saves, set the redirect target
                            PlatformSaveProxy.SetNextLoad(virtualSaveType, "Standard", capTarget.id);
                            
                            // Transfer difficulty settings from the manifest/save info
                            if (capTarget.saveInfo != null)
                            {
                                DifficultyManager.StoreMenuDifficultySettings(
                                    capTarget.saveInfo.rainDiff, 
                                    capTarget.saveInfo.resourceDiff, 
                                    capTarget.saveInfo.breachDiff, 
                                    capTarget.saveInfo.factionDiff, 
                                    capTarget.saveInfo.moodDiff, 
                                    capTarget.saveInfo.mapSize, 
                                    capTarget.saveInfo.fog);
                            }
                        }

                        // Try to show the game's loading graphic
                        try {
                            var t = Traverse.Create(panel);
                            var loadingGraphic = t.Field("m_loadingGraphic").GetValue<GameObject>();
                            if (loadingGraphic != null) loadingGraphic.SetActive(true);
                        } catch { }

                        // Start the actual load
                        SaveManager.instance.SetSlotToLoad(virtualSlot);
                    });
                });
            }
        }

        public enum VerificationState { Match, VersionMismatch, Warning, Missing, Unknown }
        public enum ModCompareStatus { Match, VersionDiff, Extra, Missing }

        public sealed class ModCompareEntry
        {
            public string activeId;
            public string activeVersion;
            public string savedId;
            public string savedVersion;
            public ModCompareStatus status;
        }

        public static VerificationState Verify(SlotManifest manifest)
        {
            return Verify(manifest, true);
        }

        public static VerificationState VerifyRequired(SlotManifest manifest)
        {
            return Verify(manifest, false);
        }

        public static VerificationState Verify(SlotManifest manifest, bool includeExtraMods)
        {
            if (manifest == null) return VerificationState.Unknown; 
            if (manifest.lastLoadedMods == null) return VerificationState.Unknown;

            var comparison = BuildModComparison(PluginManager.LoadedMods, manifest.lastLoadedMods, includeExtraMods);
            if (comparison.Any(c => c.status == ModCompareStatus.Missing))
                return VerificationState.Missing;

            if (comparison.Any(c => c.status == ModCompareStatus.VersionDiff))
                return VerificationState.VersionMismatch;

            if (includeExtraMods && comparison.Any(c => c.status == ModCompareStatus.Extra))
                return VerificationState.Warning;

            return VerificationState.Match;
        }

        public static List<ModCompareEntry> BuildModComparison(List<ModEntry> active, LoadedModInfo[] saved, bool includeExtraMods)
        {
            var result = new List<ModCompareEntry>();
            if (active == null)
                active = new List<ModEntry>();
            if (saved == null)
                saved = new LoadedModInfo[0];

            foreach (var s in saved)
            {
                if (s == null || string.IsNullOrEmpty(s.modId))
                    continue;

                var entry = new ModCompareEntry { savedId = s.modId, savedVersion = s.version };
                var match = active.Find(a => a != null && string.Equals(a.Id, s.modId, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    entry.status = ModCompareStatus.Missing;
                }
                else
                {
                    entry.activeId = match.Id;
                    entry.activeVersion = match.Version;
                    entry.status = HasVersionMismatch(match.Version, s.version)
                        ? ModCompareStatus.VersionDiff
                        : ModCompareStatus.Match;
                }

                result.Add(entry);
            }

            if (includeExtraMods)
            {
                foreach (var a in active)
                {
                    if (a == null || string.IsNullOrEmpty(a.Id))
                        continue;

                    if (!saved.Any(s => s != null && string.Equals(s.modId, a.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(new ModCompareEntry {
                            activeId = a.Id,
                            activeVersion = a.Version,
                            status = ModCompareStatus.Extra
                        });
                    }
                }
            }

            return result;
        }

        private static bool HasVersionMismatch(string activeVersion, string requiredVersion)
        {
            if (string.IsNullOrEmpty(requiredVersion))
                return false;

            return !string.Equals(activeVersion ?? string.Empty, requiredVersion, StringComparison.OrdinalIgnoreCase);
        }
    }
}
