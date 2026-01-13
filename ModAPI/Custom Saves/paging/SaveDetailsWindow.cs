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
    internal class SaveDetailsWindow : MonoBehaviour
    {
        private static GameObject _instance;

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
            
            UIDebug.LogTimed($"Overlay panel created: {panel.name}, Depth={panel.depth}");

            var root = new GameObject("WindowContent");
            root.transform.SetParent(panel.transform, false);
            root.layer = panel.gameObject.layer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;

            // Dark background using UITexture with generated white texture
            var bgObj = new GameObject("BackgroundOverlay");
            bgObj.transform.SetParent(root.transform, false);
            bgObj.layer = root.layer;
            
            var bgTexture = bgObj.AddComponent<UITexture>();
            
            // Create 2x2 white texture for tinting
            var whiteTex = new Texture2D(2, 2);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    whiteTex.SetPixel(x, y, Color.white);
            whiteTex.Apply();
            
            bgTexture.mainTexture = whiteTex;
            bgTexture.depth = 0; 
            bgTexture.width = 3000;
            bgTexture.height = 3000;
            bgTexture.color = new Color(0f, 0f, 0f, 0.90f);
            
            UIDebug.LogTimed("Background created using UITexture with generated white texture");
            
            // Click blocker
            var blocker = bgObj.AddComponent<BoxCollider>();
            blocker.size = new Vector3(3000, 3000, 1);
            
            // Get font - MUST have a font or crash
            UIFont uiFont = null;
            Font ttfFont = null;
            
            var sampleLabel = UnityEngine.Object.FindObjectOfType<UILabel>();
            if (sampleLabel != null)
            {
                uiFont = sampleLabel.bitmapFont;
                ttfFont = sampleLabel.trueTypeFont;
            }
            
            // Guaranteed fallback
            if (uiFont == null && ttfFont == null)
            {
                ttfFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                UIDebug.LogTimed("Using Arial.ttf fallback font");
            }
            else
            {
                UIDebug.LogTimed($"Using font: UIFont={uiFont?.name ?? "null"}, TTF={ttfFont?.name ?? "null"}");
            }
            
            // Simple visible test label using NGUI directly
            var testLabelGO = new GameObject("TestLabel");
            testLabelGO.transform.SetParent(root.transform, false);
            testLabelGO.layer = root.layer;
            testLabelGO.transform.localPosition = new Vector3(0, 100, 0);
            
            var testLabel = testLabelGO.AddComponent<UILabel>();
            testLabel.text = $"SAVE DETAILS: {entry?.name ?? "Unknown"}\nState: {state}\nClick below to close";
            testLabel.fontSize = 32;
            testLabel.depth = 150;
            testLabel.color = Color.white;
            testLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            testLabel.alignment = NGUIText.Alignment.Center;
            testLabel.bitmapFont = uiFont;
            testLabel.trueTypeFont = ttfFont;

            // Close button (simple text button for now)
            var closeBtnGO = new GameObject("CloseButton");
            closeBtnGO.transform.SetParent(root.transform, false);
            closeBtnGO.layer = root.layer;
            closeBtnGO.transform.localPosition = new Vector3(0, -100, 0);
            
            var closeLabel = closeBtnGO.AddComponent<UILabel>();
            closeLabel.text = "[ CLOSE ]";
            closeLabel.fontSize = 28;
            closeLabel.depth = 151;
            closeLabel.color = Color.yellow;
            closeLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            closeLabel.alignment = NGUIText.Alignment.Center;
            closeLabel.bitmapFont = uiFont;
            closeLabel.trueTypeFont = ttfFont;
            
            var closeCol = closeBtnGO.AddComponent<BoxCollider>();
            closeCol.size = new Vector3(200, 50, 1);
            
            var closeBtn = closeBtnGO.AddComponent<UIButton>();
            closeBtn.tweenTarget = closeBtnGO;
            
            EventDelegate.Set(closeBtn.onClick, () => {
                UIDebug.LogTimed("Close button clicked");
                Destroy(root);
            });
            
            UIDebug.VerifyDelegateCount(closeBtn, 1, "CloseButton");
            
            _instance = root;
            
            // Full diagnostics
            UIDebug.LogTimed("Window created - running diagnostics...");
            UIDebug.LogCameraInfo();
            UIDebug.TakeSnapshot(root, "SaveDetailsWindow Root");
            UIDebug.TakeSnapshot(closeBtnGO, "SaveDetailsWindow CloseButton");
            UIDebug.LogAllPanels();
        }

        private void BuildUI(Transform root, SaveEntry entry, SlotManifest manifest, SaveVerification.VerificationState state, bool isAttemptingLoad, Action onLoadAnyway)
        {
            // ... (keep previous UI building code) ...
            
            // Title
            var title = UIHelper.CreateLabel(root, "Save Verification: " + entry.name, 32, TextAnchor.UpperCenter);
            title.transform.localPosition = new Vector3(0, 260, 0);

            // Columns
            // Active Mods
            // ... (keep previous Active Mods code) ...
            var lblActive = UIHelper.CreateLabel(root, "Active Mods", 24, TextAnchor.UpperCenter);
            lblActive.transform.localPosition = new Vector3(-200, 220, 0);

            var listActive = UIHelper.CreateLabel(root, "", 18, TextAnchor.UpperLeft);
            listActive.transform.localPosition = new Vector3(-350, 190, 0);
            listActive.width = 300;
            listActive.height = 400;
            
            var mods = PluginManager.LoadedMods;
            string activeText = "";
            foreach(var m in mods) activeText += $"{m.Name} (v{m.Version})\n";
            listActive.text = activeText;

            // Saved Mods
            // ... (keep previous Saved Mods code) ...
            var lblSaved = UIHelper.CreateLabel(root, "Last Loaded Mods", 24, TextAnchor.UpperCenter);
            lblSaved.transform.localPosition = new Vector3(200, 220, 0);

            var listSaved = UIHelper.CreateLabel(root, "", 18, TextAnchor.UpperLeft);
            listSaved.transform.localPosition = new Vector3(50, 190, 0);
            listSaved.width = 300;
            listSaved.height = 400;

            string savedText = "";
            var warnings = new List<string>();

            if (manifest != null && manifest.lastLoadedMods != null)
            {
                foreach(var m in manifest.lastLoadedMods)
                {
                    savedText += $"{m.modId} (v{m.version})\n";
                    // Check presence
                    var activeMod = mods.Find(x => x.Id == m.modId);
                    if (activeMod == null)
                    {
                        // Missing
                        savedText += $"  [MISSING]\n";
                        if (m.warnings != null) warnings.AddRange(m.warnings);
                    }
                    else if (activeMod.Version != m.version)
                    {
                        savedText += $"  [VER DIFF]\n";
                    }
                }
            }
            listSaved.text = savedText;


            // Warnings
            if (warnings.Count > 0)
            {
                var warnLabel = UIHelper.CreateLabel(root, "WARNINGS:", 22, TextAnchor.UpperLeft);
                warnLabel.color = Color.red;
                warnLabel.transform.localPosition = new Vector3(-350, -100, 0);
                
                string wText = string.Join("\n", warnings.ToArray());
                var warnContent = UIHelper.CreateLabel(root, wText, 18, TextAnchor.UpperLeft);
                warnContent.color = Color.yellow;
                warnContent.transform.localPosition = new Vector3(-350, -130, 0);
            }

            // Buttons
            // Reload & Restart
            bool safeToReload = true;
            bool anyIdMismatch = false; // Initialize here
            var discovered = ModDiscovery.DiscoverAllMods(); // Move this up

            if (manifest != null && manifest.lastLoadedMods != null)
            {
                // 1. Check if all saved mods are installed
                foreach(var m in manifest.lastLoadedMods)
                {
                    if (!discovered.Exists(d => string.Equals(d.Id, m.modId, StringComparison.OrdinalIgnoreCase)))
                    {
                        safeToReload = false; // Missing from disk
                        break;
                    }
                }

                // 2. Check for ID mismatch vs Active
                if (safeToReload)
                {
                    var active = PluginManager.LoadedMods; // Define active here

                    // If counts differ, mismatch (active has extra or fewer)
                    if (manifest.lastLoadedMods.Length != active.Count) 
                        anyIdMismatch = true;
                    else
                    {
                        // Same count, check if all saved mods are active (Set equality)
                        foreach(var m in manifest.lastLoadedMods)
                        {
                            if (!active.Exists(a => string.Equals(a.Id, m.modId, StringComparison.OrdinalIgnoreCase)))
                            {
                                anyIdMismatch = true;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                // No manifest -> Vanilla save. 
                if (PluginManager.LoadedMods.Count > 0) anyIdMismatch = true;
            }
            
            bool enableReload = safeToReload && anyIdMismatch;

            var btnReloadObj = UIFactory.CreateIconButton(root, "Button", new Vector3(-150, -250, 0), 1f, () => {
                if (!enableReload) return;
                ApplyLoadOrderAndRestart(manifest);
            });
            UIHelper.CreateLabel(btnReloadObj.transform, "Reload & Restart", 20, TextAnchor.MiddleCenter);
            if (!enableReload) btnReloadObj.GetComponent<UIButton>().isEnabled = false;

            // Load Anyway
            if (isAttemptingLoad)
            {
                var btnLoadObj = UIFactory.CreateIconButton(root, "Button", new Vector3(150, -250, 0), 1f, () => {
                   if (onLoadAnyway != null) onLoadAnyway();
                   Destroy(root);
                });
                UIHelper.CreateLabel(btnLoadObj.transform, "Load Anyway", 20, TextAnchor.MiddleCenter);
            }

            // Close (X)
            var btnCloseObj = UIFactory.CreateIconButton(root, "Cancel", new Vector3(350, 260, 0), 0.8f, () => Destroy(root));
        }

        private void ApplyLoadOrderAndRestart(SlotManifest manifest)
        {
            // 1. Construct new load order
            var newOrder = new SimpleLoadOrder { order = new string[0] };
            if (manifest != null && manifest.lastLoadedMods != null)
            {
                newOrder.order = manifest.lastLoadedMods.Select(m => m.modId).ToArray();
            }

            // 2. Write loadorder.json
            try
            {
                 // Assuming mods/loadorder.json location is standard.
                 // PluginManager uses _modsRoot but it's private. 
                 // We can derive it.
                 var modsRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "mods");
                 var path = Path.Combine(modsRoot, "loadorder.json");
                 File.WriteAllText(path, JsonUtility.ToJson(newOrder, true));
                 MMLog.Write("[SaveDetailsWindow] Updated loadorder.json to match save.");
                 
                 // 3. Write restart.flag
                 var flagPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "restart.flag");
                 File.WriteAllText(flagPath, "restart");
                 MMLog.Write("[SaveDetailsWindow] Created restart.flag.");
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed to update load order or create restart flag: " + ex);
            }

            // 4. Quit
            Application.Quit();
        }

        private class SimpleLoadOrder { public string[] order; }
    }
}
