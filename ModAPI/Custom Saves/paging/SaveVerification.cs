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
                    target = new SaveEntry { 
                        id = "vanilla_slot_" + absoluteSlot,
                        absoluteSlot = absoluteSlot,
                        name = "Slot " + absoluteSlot
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
                    var bgTexture = iconGO.AddComponent<UITexture>();
                    
                    // Create a simple 2x2 white texture
                    var whiteTex = new Texture2D(2, 2);
                    for (int x = 0; x < 2; x++)
                        for (int y = 0; y < 2; y++)
                            whiteTex.SetPixel(x, y, Color.white);
                    whiteTex.Apply();
                    
                    bgTexture.mainTexture = whiteTex;
                    bgTexture.depth = baseDepth;
                    bgTexture.width = 60;
                    bgTexture.height = 60;
                    bgTexture.pivot = UIWidget.Pivot.Center;
                    bgTexture.color = new Color(0.3f, 0.25f, 0.2f, 0.9f); // Dark brown, more opaque
                    
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

                // Sprite selection
                string spriteName = "Checkmark";
                if (sampleAtlas.GetSprite("Checkmark") != null) spriteName = "Checkmark";
                else if (sampleAtlas.GetSprite("Tick") != null) spriteName = "Tick";
                
                if (state == VerificationState.Missing)
                {
                    if (sampleAtlas.GetSprite("Cancel") != null) spriteName = "Cancel";
                    else if (sampleAtlas.GetSprite("Close") != null) spriteName = "Close";
                }

                Color color = state == VerificationState.Match ? Color.green : 
                             (state == VerificationState.Missing ? Color.red : Color.yellow);

                if (childSprite != null)
                {
                    childSprite.spriteName = spriteName;
                    childSprite.color = color;
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
