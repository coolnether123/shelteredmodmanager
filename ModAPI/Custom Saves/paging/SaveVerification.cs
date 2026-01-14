using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Saves;
using ModAPI.UI;

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

        private static UIFont _cachedUIFont;
        private static Font _cachedTTFFont;

        public static void UpdateIcons(SlotSelectionPanel panel)
        {
            UIDebug.ResetTiming();
            UIDebug.LogTimed("UpdateIcons called");
            
            if (panel == null)
            {
                MMLog.WriteDebug("[SaveVerification] Panel is null, returning");
                return;
            }

            var buttons = panel.GetComponentsInChildren<SaveSlotButton>(true);
            int page = PagingManager.GetPage(panel);

            MMLog.WriteDebug($"[SaveVerification] Found {buttons.Length} buttons, page = {page}");

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
                    // For vanilla slots 1-3, create entry and read family name from save file
                    target = new SaveEntry { 
                        id = "vanilla_slot_" + absoluteSlot,
                        absoluteSlot = absoluteSlot,
                        name = "Slot " + absoluteSlot,
                        saveInfo = SaveRegistryCore.ReadVanillaSaveInfo(absoluteSlot)
                    };
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
                    
                    MMLog.WriteDebug($"[SaveVerification] Created UITexture background with white texture");
                    
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

                if (target == null) 
                {
                    iconGO.SetActive(false);
                    continue;
                }

                iconGO.SetActive(true);
                // local Z: -20 to win raycast against slot button
                iconGO.transform.localPosition = new Vector3(-320, 0, -20);
                
                var iconChildObj = iconGO.transform.Find("Icon");
                UISprite childSprite = iconChildObj != null ? iconChildObj.GetComponent<UISprite>() : null;
                var currentBgSprite = iconGO.GetComponent<UISprite>();
                var iconButton = iconGO.GetComponent<UIButton>();
                var bgTexture = iconGO.GetComponent<UITexture>();
                
                // DEBUG: Log button visual state
                MMLog.WriteDebug($"[SaveVerification] Slot {absoluteSlot}: Button State = {iconButton.state}, IsEnabled = {iconButton.isEnabled}");
                if (bgTexture != null)
                {
                    MMLog.WriteDebug($"[SaveVerification] Slot {absoluteSlot}: BG Color = {bgTexture.color}, Alpha = {bgTexture.alpha}");
                    MMLog.WriteDebug($"[SaveVerification] Slot {absoluteSlot}: Widget Depth = {bgTexture.depth}, Panel Depth = {NGUITools.FindInParents<UIPanel>(iconGO)?.depth}");
                }
                
                // Check for tweens
                var tweenAlpha = iconGO.GetComponent<TweenAlpha>();
                if (tweenAlpha != null)
                {
                    MMLog.WriteDebug($"[SaveVerification] Slot {absoluteSlot}: Has TweenAlpha! from={tweenAlpha.from}, to={tweenAlpha.to}, value={tweenAlpha.value}");
                }
                
                // Get Manifest and State
                var slotRoot = DirectoryProvider.SlotRoot("Standard", absoluteSlot); 
                var manPath = Path.Combine(slotRoot, "manifest.json");
                
                VerificationState state = VerificationState.Match;
                SlotManifest manifest = null;

                if (File.Exists(manPath))
                {
                    try { 
                        manifest = ModAPI.Saves.SaveRegistryCore.DeserializeSlotManifest(File.ReadAllText(manPath)); 
                        state = Verify(manifest);
                    } catch { }
                }

                // Sprite selection - ensure we have a label for the character icons
                UILabel childLabel = iconChildObj != null ? iconChildObj.GetComponent<UILabel>() : null;
                if (childLabel == null && iconChildObj != null)
                {
                    childLabel = iconChildObj.gameObject.AddComponent<UILabel>();
                    if (childSprite != null) childSprite.enabled = false;
                    
                    childLabel.bitmapFont = _cachedUIFont;
                    childLabel.trueTypeFont = _cachedTTFFont;
                    childLabel.fontSize = 32;
                    childLabel.pivot = UIWidget.Pivot.Center;
                    // Use depth relative to background - retrieve baseDepth from background texture
                    var parentBgTex = iconChildObj.parent.GetComponent<UITexture>();
                    int labelDepth = (parentBgTex != null ? parentBgTex.depth : 100) + 10;
                    childLabel.depth = labelDepth;
                    childLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
                    childLabel.width = 50;
                    childLabel.height = 50;
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
                
                switch (state)
                {
                    case VerificationState.Match:
                        iconPrefix = "✓";
                        iconColor = COLOR_MATCH;
                        break;
                    case VerificationState.VersionMismatch:
                        iconPrefix = "~";
                        iconColor = COLOR_VERSION_DIFF;
                        break;
                    case VerificationState.Warning:
                        iconPrefix = "~";
                        iconColor = COLOR_VERSION_DIFF;
                        break;
                    case VerificationState.Missing:
                        iconPrefix = "✗";
                        iconColor = COLOR_MISSING;
                        break;
                }
                
                if (childLabel != null)
                {
                    childLabel.text = iconPrefix;
                    childLabel.color = iconColor;
                }
                
                // Debug: Full snapshot on first button only
                if (i == 0)
                {
                    UIDebug.TakeSnapshot(iconGO, $"VerificationBtn Slot {absoluteSlot}");
                }

                var capTarget = target;
                var capManifest = manifest;
                var capState = state;
                int capSlot = absoluteSlot;
                
                EventDelegate.Set(iconButton.onClick, () => {
                    UIDebug.LogTimed($"Icon clicked for slot {capSlot}");
                    UIDebug.TraceClickAt(Input.mousePosition);
                    SaveDetailsWindow.Show(capTarget, capManifest, capState, false, null);
                });
                
                // Verify the delegate was added
                UIDebug.VerifyDelegateCount(iconButton, 1, $"VerificationBtn Slot {absoluteSlot}");
            }
            
            UIDebug.LogTimed("UpdateIcons complete");
        }

        public enum VerificationState { Match, VersionMismatch, Warning, Missing }

        public static VerificationState Verify(SlotManifest manifest)
        {
            if (manifest == null) return VerificationState.Match; 
            if (manifest.lastLoadedMods == null) return VerificationState.Match;

            var activeMods = PluginManager.LoadedMods;
            
            // 1. Check for Missing Mods (Critical - Red)
            foreach(var savedMod in manifest.lastLoadedMods)
            {
                 var activeMod = activeMods.Find(m => string.Equals(m.Id, savedMod.modId, StringComparison.OrdinalIgnoreCase));
                 if (activeMod == null) return VerificationState.Missing; 
            }

            // 2. Check for Version Mismatches (Warning - Yellow)
            bool versionMismatch = false;
            foreach(var savedMod in manifest.lastLoadedMods)
            {
                 var activeMod = activeMods.Find(m => string.Equals(m.Id, savedMod.modId, StringComparison.OrdinalIgnoreCase));
                 if (activeMod != null && activeMod.Version != savedMod.version)
                 {
                     versionMismatch = true;
                 }
            }

            // 3. Check for Extra Mods (Warning - Yellow)
            // If user has more mods active than the save intended
            bool extraMods = false;
            foreach (var activeMod in activeMods)
            {
                if (!manifest.lastLoadedMods.Any(m => string.Equals(m.modId, activeMod.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    extraMods = true;
                }
            }

            if (versionMismatch) return VerificationState.VersionMismatch;
            if (extraMods) return VerificationState.Warning;

            return VerificationState.Match;
        }
    }
}
