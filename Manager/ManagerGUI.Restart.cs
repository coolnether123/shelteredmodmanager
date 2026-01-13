using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Text;

namespace Manager
{
    public partial class ManagerGUI
    {
        /// <summary>
        /// Provides diagnostic logging for the restart request handler.
        /// </summary>
        private class RestartDiagnostics
        {
            private static readonly string LogPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)), "mod_manager.log");

            public static void Log(string msg)
            {
                try { File.AppendAllText(LogPath, $"[{DateTime.Now}] [RestartHandler] {msg}\r\n"); } catch { }
            }

            public static void LogRestartRequest(RestartRequest req, string path, bool isValidJson, bool exists)
            {
                Log($"Checking restart.json at: {path}");
                Log($"File exists: {exists}");
                
                if (exists)
                {
                    Log($"Valid JSON: {isValidJson}");
                    if (req != null)
                    {
                        Log($"Action: {req.Action}");
                        Log($"LoadFromManifest: {req.LoadFromManifest}");
                    }
                }
            }

            public static void LogManifestParse(ManagerSlotManifest manifest, string path, bool success)
            {
                Log($"Parsing manifest at: {path}");
                Log($"Success: {success}");
                if (success && manifest != null && manifest.lastLoadedMods != null)
                {
                    Log($"lastLoadedMods count: {manifest.lastLoadedMods.Length}");
                    for(int i=0; i < Math.Min(5, manifest.lastLoadedMods.Length); i++)
                    {
                        var m = manifest.lastLoadedMods[i];
                        Log($"[{i}] {m.modId} v{m.version}");
                    }
                    if (manifest.lastLoadedMods.Length > 5) Log($"... and {manifest.lastLoadedMods.Length - 5} more.");
                }
            }

            public static void LogLoadOrderDiff(List<string> oldOrder, List<string> newOrder)
            {
                Log("Applying new load order from save.");
                
                var oldSet = new HashSet<string>(oldOrder, StringComparer.OrdinalIgnoreCase);
                var newSet = new HashSet<string>(newOrder, StringComparer.OrdinalIgnoreCase);

                var added = newOrder.Where(m => !oldSet.Contains(m)).ToList();
                var removed = oldOrder.Where(m => !newSet.Contains(m)).ToList();

                if (added.Count > 0) Log($"Enabled/Added: {string.Join(", ", added.ToArray())}");
                if (removed.Count > 0) Log($"Disabled/Removed: {string.Join(", ", removed.ToArray())}");
                
                Log($"New order length: {newOrder.Count}");
            }

            public static void LogPreFlightCheck(OrderEvaluation evaluation, bool safe)
            {
                Log("Pre-Flight Diagnostic:");
                Log($"Missing Deps: {evaluation.MissingHardDependencies.Count}");
                foreach(var m in evaluation.MissingHardDependencies) Log($"  - MISSING: {m}");
                
                Log($"Cycles: {evaluation.CycledModIds.Count}");
                foreach(var c in evaluation.CycledModIds) Log($"  - CYCLE: {c}");

                Log($"Safe To Launch: {safe}");
            }

            public static void LogCleanup(string path, bool success)
            {
                Log($"Deleting restart file: {path}");
                Log($"Cleanup success: {success}");
            }
        }

        /// <summary>
        /// Defines the structure of a restart request received from the game.
        /// </summary>
        private class RestartRequest
        {
            public string Action;
            public string LoadFromManifest;
        }

        private class ManagerSlotManifest
        {
            public ManagerLoadedModInfo[] lastLoadedMods;
        }

        private class ManagerLoadedModInfo
        {
            public string modId;
            public string version;
        }

        private void CheckAndHandleRestartRequest()
        {
            try
            {
                // Look for restart.json in SMM/Bin
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                // Try SMM/Bin relative to manager
                var restartPath = Path.Combine(Path.Combine(Path.Combine(baseDir, "SMM"), "Bin"), "restart.json");
                
                // If not found, try searching in game directory if we know it
                if (!File.Exists(restartPath) && !string.IsNullOrEmpty(uiGamePath.Text) && File.Exists(uiGamePath.Text))
                {
                    var gameSmmBin = Path.Combine(Path.Combine(Path.Combine(Path.GetDirectoryName(uiGamePath.Text), "SMM"), "Bin"), "restart.json");
                    if (File.Exists(gameSmmBin)) restartPath = gameSmmBin;
                }

                if (!File.Exists(restartPath)) return;

                // Found restart request
                RestartRequest req = null;
                string json = null;
                try 
                {
                    json = File.ReadAllText(restartPath);
                    req = new JavaScriptSerializer().Deserialize<RestartRequest>(json);
                    RestartDiagnostics.LogRestartRequest(req, restartPath, true, true);
                }
                catch (Exception ex)
                {
                    RestartDiagnostics.Log($"Failed to parse restart.json: {ex.Message}");
                    RestartDiagnostics.LogRestartRequest(null, restartPath, false, true);
                    return;
                }

                if (req != null && string.Equals(req.Action, "Restart", StringComparison.OrdinalIgnoreCase) 
                    && !string.IsNullOrEmpty(req.LoadFromManifest))
                {
                    if (!File.Exists(req.LoadFromManifest))
                    {
                        RestartDiagnostics.Log($"Manifest not found at: {req.LoadFromManifest}");
                        return;
                    }

                    // 1. Read Manifest
                    ManagerSlotManifest manifest = null;
                    try
                    {
                        var manifestJson = File.ReadAllText(req.LoadFromManifest);
                        manifest = new JavaScriptSerializer().Deserialize<ManagerSlotManifest>(manifestJson);
                        RestartDiagnostics.LogManifestParse(manifest, req.LoadFromManifest, true);
                    }
                    catch (Exception ex)
                    {
                        RestartDiagnostics.Log($"Failed to parse manifest: {ex.Message}");
                        RestartDiagnostics.LogManifestParse(null, req.LoadFromManifest, false);
                        return;
                    }

                    if (manifest != null && manifest.lastLoadedMods != null)
                    {
                        // Capture old order for diff
                        var oldOrder = ReadOrderFromFile(uiModsPath.Text).ToList();

                        // 2. Extract Mod IDs
                        var newOrder = new List<string>();
                        foreach(var m in manifest.lastLoadedMods)
                        {
                            if (!string.IsNullOrEmpty(m.modId)) newOrder.Add(m.modId);
                        }

                        // 3. Log Diff and Write Load Order
                        RestartDiagnostics.LogLoadOrderDiff(oldOrder, newOrder);
                        
                        if (!string.IsNullOrEmpty(uiModsPath.Text))
                        {
                            WriteOrderToFile(uiModsPath.Text, newOrder);
                            updateAvailableMods(); // Refresh UI to reflect new order
                        }

                        // 4. Validate (Pre-flight for logic issues)
                        bool safeToLaunch = true;
                        
                        // Check dependencies
                        var allDiscovered = DiscoverModsFromRoot(uiModsPath.Text);
                        var enabledSet = new HashSet<string>(newOrder, StringComparer.OrdinalIgnoreCase);
                        var enabledMods = allDiscovered.Where(m => enabledSet.Contains(m.Id)).ToList();
                        var modInfos = ToModEntries(enabledMods);
                        var eval = LoadOrderResolver.Evaluate(modInfos, newOrder);

                        RestartDiagnostics.LogPreFlightCheck(eval, !eval.MissingHardDependencies.Any() && !eval.CycledModIds.Any());

                        if (eval.MissingHardDependencies.Any() || eval.CycledModIds.Any())
                        {
                            safeToLaunch = false;
                            MessageBox.Show("The save's mod list has dependency issues (missing mods or cycles).\n\nPlease review the load order before launching.", 
                                "Restart Interrupted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        // 5. Delete restart file
                        bool msgCleanup = false;
                        try 
                        { 
                            File.Delete(restartPath); 
                            msgCleanup = true;
                        } 
                        catch (Exception ex)
                        {
                            RestartDiagnostics.Log($"Failed to delete restart file: {ex.Message}");
                        }
                        RestartDiagnostics.LogCleanup(restartPath, msgCleanup);

                        // 6. Launch if safe
                        if (safeToLaunch)
                        {
                            RestartDiagnostics.Log("ModAPI Restart: Launching game...");
                            // Trigger launch
                            onLaunchClicked(this, EventArgs.Empty);
                        }
                        else
                        {
                            RestartDiagnostics.Log("ModAPI Restart: Aborted due to safety check.");
                        }
                    }
                }
                else
                {
                    RestartDiagnostics.Log($"Invalid restart request action or path. Action={req?.Action}");
                    // Invalid request, just cleanup
                    try { File.Delete(restartPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                // Last ditch log
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mod_manager.log"), $"[CRITICAL] Restart Logic Crash: {ex}\r\n"); } catch { }
                MessageBox.Show("Failed to process restart request: " + ex.Message);
            }
        }
    }
}
