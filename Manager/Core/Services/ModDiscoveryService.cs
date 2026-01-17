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
                    
                    // Skip reserved folder names
                    if (string.Equals(folderName, "disabled", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(folderName, "SMM", StringComparison.OrdinalIgnoreCase))
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
            if (string.IsNullOrEmpty(_installedModApiVersion))
                return;

            try
            {
                var assemblies = AssemblyVersionChecker.ScanModAssemblies(mod.RootPath);
                
                AssemblyVersionChecker.ModAssemblyVersion requirement = default(AssemblyVersionChecker.ModAssemblyVersion);
                foreach (var a in assemblies)
                {
                    if (!string.IsNullOrEmpty(a.ApiVersion))
                    {
                        requirement = a;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(requirement.ApiVersion))
                {
                    mod.RequiredModApiVersion = requirement.ApiVersion;
                    mod.IsModApiCompatible = AssemblyVersionChecker.IsCompatible(_installedModApiVersion, requirement.ApiVersion);

                    if (!mod.IsModApiCompatible)
                    {
                        mod.Status = ModStatus.VersionMismatch;
                        mod.StatusMessage = "Requires ModAPI " + requirement.ApiVersion + " (installed: " + _installedModApiVersion + ")";
                    }
                }
            }
            catch { }
        }
    }
}
