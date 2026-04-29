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
        private readonly ApiCompatibilityService _apiCompatibilityService;

        public ModDiscoveryService(string installedModApiVersion)
        {
            var installedVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(installedModApiVersion))
                installedVersions["ModAPI"] = installedModApiVersion;
            _apiCompatibilityService = new ApiCompatibilityService(installedVersions);
        }

        public ModDiscoveryService(Dictionary<string, string> installedApiVersions)
        {
            _apiCompatibilityService = new ApiCompatibilityService(installedApiVersions);
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
                var report = _apiCompatibilityService.Evaluate(assemblies, mod.DeclaredModApiVersion, mod.DeclaredShelteredApiVersion);
                mod.ApiCompatibility = report;

                if (report.Requirements.Count > 0)
                {
                    mod.RequiredModApiVersion = report.RequirementSummary;
                    mod.IsModApiCompatible = report.IsCompatible;

                    if (report.Severity == ApiCompatibilitySeverity.Error)
                    {
                        mod.Status = ModStatus.VersionMismatch;
                        mod.StatusMessage = report.Summary;
                    }
                    else if (report.Severity == ApiCompatibilitySeverity.Warning && mod.Status == ModStatus.Ok)
                    {
                        mod.Status = ModStatus.Warning;
                        mod.StatusMessage = report.Summary;
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

    }
}
