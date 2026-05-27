using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace Manager
{
    /// <summary>
    /// Reads ModAPI/ShelteredAPI assembly references used by installed mods.
    /// </summary>
    public static class AssemblyVersionChecker
    {
        public struct ModAssemblyVersion
        {
            public string DllName;
            public string ApiName;
            public string ApiVersion;
        }

        /// <summary>
        /// Gets the version of ModAPI.dll from the SMM folder.
        /// </summary>
        /// <param name="smmPath">Path to the SMM folder containing ModAPI.dll</param>
        /// <returns>Version string (e.g., "1.0.0.0") or null if not found</returns>
        public static string GetInstalledModApiVersion(string smmPath)
        {
            return GetInstalledApiVersion(smmPath, "ModAPI");
        }

        /// <summary>
        /// Gets the version of a known API assembly from the SMM folder.
        /// </summary>
        public static string GetInstalledApiVersion(string smmPath, string apiName)
        {
            try
            {
                string apiPath = FindInstalledApiAssemblyPath(smmPath, apiName);
                if (string.IsNullOrEmpty(apiPath)) return null;

                // Use FileVersionInfo as it's more stable on Windows 7 than ReflectionOnlyLoad
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(apiPath);
                
                // FileVersion tracks assembly compatibility. ProductVersion may use
                // informational labels such as "v0.1" that are not reference versions.
                string v = versionInfo.FileVersion;
                if (string.IsNullOrEmpty(v)) v = versionInfo.ProductVersion;
                return v;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssemblyVersionChecker] Error reading ModAPI version: {ex.Message}");
                return null;
            }
        }

        public static Dictionary<string, string> GetInstalledApiVersions(string smmPath)
        {
            return GetInstalledApiVersions(smmPath, GetDefaultKnownApiAssemblies());
        }

        public static Dictionary<string, string> GetInstalledApiVersions(string smmPath, IEnumerable<string> apiNames)
        {
            var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (apiNames == null)
                apiNames = GetDefaultKnownApiAssemblies();

            foreach (string apiName in apiNames)
                AddInstalledApiVersion(versions, smmPath, apiName);

            return versions;
        }

        /// <summary>
        /// Gets the ModAPI/ShelteredAPI version that a mod's DLL was compiled against.
        /// </summary>
        /// <param name="modDllPath">Path to the mod's assembly DLL</param>
        /// <returns>Version string or null if no ModAPI reference found</returns>
        public static string GetModRequiredApiVersion(string modDllPath)
        {
            try
            {
                var references = GetModApiReferences(modDllPath);
                foreach (var reference in references)
                {
                    if (!string.IsNullOrEmpty(reference.ApiVersion))
                        return reference.ApiVersion;
                }

                return null; // No ModAPI reference found
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssemblyVersionChecker] Error reading mod API version from {Path.GetFileName(modDllPath)}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets every ModAPI/ShelteredAPI assembly reference used by a mod DLL.
        /// </summary>
        public static List<ModAssemblyVersion> GetModApiReferences(string modDllPath)
        {
            return GetModApiReferences(modDllPath, GetDefaultKnownApiAssemblies());
        }

        public static List<ModAssemblyVersion> GetModApiReferences(string modDllPath, IEnumerable<string> knownApiAssemblies)
        {
            var results = new List<ModAssemblyVersion>();
            HashSet<string> knownApis = ToKnownApiSet(knownApiAssemblies);

            try
            {
                if (!File.Exists(modDllPath))
                    return results;

                string dllName = Path.GetFileName(modDllPath);

                // Load assembly bytes to avoid file locking
                byte[] assemblyBytes = File.ReadAllBytes(modDllPath);
                var assembly = Assembly.ReflectionOnlyLoad(assemblyBytes);
                var references = assembly.GetReferencedAssemblies();

                foreach (var reference in references)
                {
                    if (!IsKnownApiAssembly(reference.Name, knownApis))
                        continue;

                    results.Add(new ModAssemblyVersion
                    {
                        DllName = dllName,
                        ApiName = reference.Name,
                        ApiVersion = reference.Version != null ? reference.Version.ToString() : null
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssemblyVersionChecker] Error reading API references from {Path.GetFileName(modDllPath)}: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Checks if a mod's required ModAPI version is compatible with the installed version.
        /// </summary>
        /// <param name="installedVersion">Installed ModAPI version (e.g., "1.0.0.0")</param>
        /// <param name="requiredVersion">Version the mod was compiled against</param>
        /// <returns>True if the installed API is the same version or newer, false otherwise</returns>
        public static bool IsCompatible(string installedVersion, string requiredVersion)
        {
            if (string.IsNullOrEmpty(installedVersion) || string.IsNullOrEmpty(requiredVersion))
            {
                return false;
            }

            try
            {
                var installed = new Version(installedVersion);
                var required = new Version(requiredVersion);

                return installed.CompareTo(required) >= 0;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Scans a mod directory's Assemblies folder and returns version info for all DLLs.
        /// </summary>
        /// <param name="modPath">Root path of the mod</param>
        /// <returns>List of ModAssemblyVersion structs</returns>
        public static List<ModAssemblyVersion> ScanModAssemblies(string modPath)
        {
            return ScanModAssemblies(modPath, GetDefaultKnownApiAssemblies());
        }

        public static List<ModAssemblyVersion> ScanModAssemblies(string modPath, IEnumerable<string> knownApiAssemblies)
        {
            var results = new List<ModAssemblyVersion>();
            HashSet<string> knownApis = ToKnownApiSet(knownApiAssemblies);

            try
            {
                string assembliesPath = Path.Combine(modPath, "Assemblies");
                
                if (!Directory.Exists(assembliesPath))
                {
                    return results;
                }

                var dllPaths = Directory.GetFiles(assembliesPath, "*.dll", SearchOption.AllDirectories);
                Array.Sort(dllPaths, StringComparer.OrdinalIgnoreCase);

                foreach (var dllPath in dllPaths)
                {
                    // Skip known framework/dependency DLLs
                    string fileName = Path.GetFileName(dllPath);
                    if (fileName.Equals("0Harmony.dll", StringComparison.OrdinalIgnoreCase) ||
                        IsKnownApiAssemblyFile(fileName, knownApis))
                    {
                        continue;
                    }

                    var references = GetModApiReferences(dllPath, knownApis);
                    if (references.Count == 0)
                    {
                        results.Add(new ModAssemblyVersion { DllName = fileName, ApiName = string.Empty, ApiVersion = string.Empty });
                        continue;
                    }

                    for (int i = 0; i < references.Count; i++)
                        results.Add(references[i]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssemblyVersionChecker] Error scanning assemblies in {modPath}: {ex.Message}");
            }

            return results;
        }

        private static void AddInstalledApiVersion(Dictionary<string, string> versions, string smmPath, string apiName)
        {
            string version = GetInstalledApiVersion(smmPath, apiName);
            if (!string.IsNullOrEmpty(version))
                versions[apiName] = version;
        }

        private static string FindInstalledApiAssemblyPath(string smmPath, string apiName)
        {
            if (string.IsNullOrEmpty(smmPath) || string.IsNullOrEmpty(apiName))
                return null;

            string dllName = apiName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? apiName
                : apiName + ".dll";

            string[] candidates = new string[]
            {
                Path.Combine(smmPath, dllName),
                Path.Combine(Path.Combine(smmPath, "bin"), dllName)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                    return candidates[i];
            }

            return null;
        }

        private static IEnumerable<string> GetDefaultKnownApiAssemblies()
        {
            return new string[] { "ModAPI", "ShelteredAPI", "ModAPI.Networking" };
        }

        private static HashSet<string> ToKnownApiSet(IEnumerable<string> knownApiAssemblies)
        {
            HashSet<string> knownApis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (knownApiAssemblies != null)
            {
                foreach (string api in knownApiAssemblies)
                {
                    if (!string.IsNullOrEmpty(api))
                        knownApis.Add(api);
                }
            }

            if (knownApis.Count == 0)
            {
                foreach (string api in GetDefaultKnownApiAssemblies())
                    knownApis.Add(api);
            }

            return knownApis;
        }

        private static bool IsKnownApiAssemblyFile(string fileName, HashSet<string> knownApis)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string assemblyName = Path.GetFileNameWithoutExtension(fileName);
            return IsKnownApiAssembly(assemblyName, knownApis);
        }

        private static bool IsKnownApiAssembly(string assemblyName, HashSet<string> knownApis)
        {
            return !string.IsNullOrEmpty(assemblyName) && knownApis != null && knownApis.Contains(assemblyName);
        }
    }
}
