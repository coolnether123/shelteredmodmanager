using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Discovers mods from the mods folder and creates ModItem objects.
    /// Single responsibility: Mod discovery and parsing only.
    /// </summary>
    public class ModDiscoveryService
    {
        private readonly string _installedModApiVersion;

        public ModDiscoveryService(string installedModApiVersion)
        {
            _installedModApiVersion = installedModApiVersion;
        }

        /// <summary>
        /// Discover all mods from a root directory
        /// </summary>
        public List<ModItem> DiscoverMods(string modsRootPath)
        {
            var mods = new List<ModItem>();

            if (string.IsNullOrEmpty(modsRootPath) || !Directory.Exists(modsRootPath))
                return mods;

            try
            {
                foreach (var dir in Directory.GetDirectories(modsRootPath))
                {
                    var folderName = Path.GetFileName(dir);

                    // Skip reserved/internal directories.
                    if (IsReservedFolderName(folderName))
                        continue;

                    var mod = DiscoverMod(dir);
                    if (mod != null)
                    {
                        mods.Add(mod);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error discovering mods: " + ex.Message);
            }

            return mods;
        }

        /// <summary>
        /// Discover a single mod from a directory
        /// </summary>
        public ModItem DiscoverMod(string modPath)
        {
            try
            {
                ModTypes.ModAboutInfo about;
                string normalizedId, displayName, previewPath;
                
                bool hasAbout = ModAboutReader.TryLoad(modPath, out about, out normalizedId, out displayName, out previewPath);

                ModItem mod;
                if (hasAbout)
                {
                    mod = ModItem.FromAbout(about, modPath, previewPath);
                }
                else
                {
                    mod = CreateFallbackMod(modPath);
                }

                // Check ModAPI compatibility
                CheckModApiCompatibility(mod);

                return mod;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error discovering mod at " + modPath + ": " + ex.Message);
                return CreateFallbackMod(modPath);
            }
        }

        private ModItem CreateFallbackMod(string modPath)
        {
            var folderName = Path.GetFileName(modPath) ?? "Unknown";
            var mod = new ModItem(
                folderName.ToLowerInvariant(),
                folderName,
                modPath
            );
            mod.Status = ModStatus.Warning;
            mod.StatusMessage = "Missing About.json";
            return mod;
        }

        private void CheckModApiCompatibility(ModItem mod)
        {
            try
            {
                var assemblies = AssemblyVersionChecker.ScanModAssemblies(mod.RootPath);

                var apiVersions = new List<string>();
                foreach (var a in assemblies)
                {
                    if (string.IsNullOrEmpty(a.ApiVersion))
                        continue;

                    bool exists = false;
                    for (int i = 0; i < apiVersions.Count; i++)
                    {
                        if (string.Equals(apiVersions[i], a.ApiVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                        apiVersions.Add(a.ApiVersion);
                }

                if (apiVersions.Count > 0)
                {
                    string requirement = SelectPreferredRequirement(apiVersions, _installedModApiVersion);
                    mod.RequiredModApiVersion = requirement;

                    if (!string.IsNullOrEmpty(_installedModApiVersion))
                    {
                        bool isCompatible = true;
                        for (int i = 0; i < apiVersions.Count; i++)
                        {
                            if (!AssemblyVersionChecker.IsCompatible(_installedModApiVersion, apiVersions[i]))
                            {
                                isCompatible = false;
                                break;
                            }
                        }

                        mod.IsModApiCompatible = isCompatible;
                        if (!isCompatible)
                        {
                            mod.Status = ModStatus.VersionMismatch;
                            mod.StatusMessage = "Requires ModAPI " + requirement + " (installed: " + _installedModApiVersion + ")";
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(mod.RequiredModApiVersion) && !string.IsNullOrEmpty(_installedModApiVersion))
                {
                    // Fallback for mods that declare required API in About.json but do not expose
                    // a readable ModAPI/ShelteredAPI assembly reference.
                    bool isCompatible = AssemblyVersionChecker.IsCompatible(_installedModApiVersion, mod.RequiredModApiVersion);
                    mod.IsModApiCompatible = isCompatible;
                    if (!isCompatible)
                    {
                        mod.Status = ModStatus.VersionMismatch;
                        mod.StatusMessage = "Requires ModAPI " + mod.RequiredModApiVersion + " (installed: " + _installedModApiVersion + ")";
                    }
                }
            }
            catch { }
        }

        private static bool IsReservedFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return true;

            if (string.Equals(folderName, "disabled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderName, "SMM", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderName, "ModAPI", StringComparison.OrdinalIgnoreCase))
                return true;

            // Manager internal working directories should never appear as mods.
            return folderName.StartsWith("_smm_", StringComparison.OrdinalIgnoreCase);
        }

        private static string SelectPreferredRequirement(List<string> apiVersions, string installedModApiVersion)
        {
            if (apiVersions == null || apiVersions.Count == 0)
                return string.Empty;

            if (!string.IsNullOrEmpty(installedModApiVersion))
            {
                for (int i = 0; i < apiVersions.Count; i++)
                {
                    if (!AssemblyVersionChecker.IsCompatible(installedModApiVersion, apiVersions[i]))
                        return apiVersions[i];
                }
            }

            // Fallback: choose highest declared API version for stable display.
            string selected = apiVersions[0];
            for (int i = 1; i < apiVersions.Count; i++)
            {
                if (CompareVersionStrings(apiVersions[i], selected) > 0)
                    selected = apiVersions[i];
            }

            return selected;
        }

        private static int CompareVersionStrings(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
                return string.IsNullOrEmpty(right) ? 0 : -1;
            if (string.IsNullOrEmpty(right))
                return 1;

            try
            {
                return new Version(left).CompareTo(new Version(right));
            }
            catch
            {
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
