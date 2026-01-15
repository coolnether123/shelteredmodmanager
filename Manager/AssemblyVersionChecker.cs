using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace Manager
{
    /// <summary>
    /// Checks ModAPI version compatibility between installed ModAPI.dll and mod assemblies.
    /// </summary>
    public static class AssemblyVersionChecker
    {
        public struct ModAssemblyVersion
        {
            public string DllName;
            public string ApiVersion;
        }

        /// <summary>
        /// Gets the version of ModAPI.dll from the SMM folder.
        /// </summary>
        /// <param name="smmPath">Path to the SMM folder containing ModAPI.dll</param>
        /// <returns>Version string (e.g., "1.0.0.0") or null if not found</returns>
        public static string GetInstalledModApiVersion(string smmPath)
        {
            try
            {
                string modApiPath = Path.Combine(smmPath, "ModAPI.dll");
                
                if (!File.Exists(modApiPath))
                {
                    return null;
                }

                // Use ReflectionOnlyLoadFrom to avoid executing code
                var assembly = Assembly.ReflectionOnlyLoadFrom(modApiPath);
                return assembly.GetName().Version?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssemblyVersionChecker] Error reading ModAPI version: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the ModAPI version that a mod's DLL was compiled against.
        /// </summary>
        /// <param name="modDllPath">Path to the mod's assembly DLL</param>
        /// <returns>Version string or null if no ModAPI reference found</returns>
        public static string GetModRequiredApiVersion(string modDllPath)
        {
            try
            {
                if (!File.Exists(modDllPath))
                {
                    return null;
                }

                // Use ReflectionOnlyLoadFrom to avoid executing code
                var assembly = Assembly.ReflectionOnlyLoadFrom(modDllPath);
                var references = assembly.GetReferencedAssemblies();

                // Find the ModAPI reference
                foreach (var reference in references)
                {
                    if (reference.Name == "ModAPI")
                    {
                        return reference.Version?.ToString();
                    }
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
        /// Checks if a mod's required ModAPI version is compatible with the installed version.
        /// </summary>
        /// <param name="installedVersion">Installed ModAPI version (e.g., "1.0.0.0")</param>
        /// <param name="requiredVersion">Version the mod was compiled against</param>
        /// <returns>True if compatible (exact match), false otherwise</returns>
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

                // For now, require exact major.minor match (1.0.x.x compatible with 1.0.y.z)
                return installed.Major == required.Major && installed.Minor == required.Minor;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Scans a mod directory's Assemblies folder and returns version info for all DLLs.
        /// </summary>
        /// <param name="modPath">Root path of the mod</param>
        /// <returns>List of ModAssemblyVersion structs</returns>
        public static List<ModAssemblyVersion> ScanModAssemblies(string modPath)
        {
            var results = new List<ModAssemblyVersion>();

            try
            {
                string assembliesPath = Path.Combine(modPath, "Assemblies");
                
                if (!Directory.Exists(assembliesPath))
                {
                    return results;
                }

                foreach (var dllPath in Directory.GetFiles(assembliesPath, "*.dll"))
                {
                    // Skip known framework/dependency DLLs
                    string fileName = Path.GetFileName(dllPath);
                    if (fileName.Equals("0Harmony.dll", StringComparison.OrdinalIgnoreCase) ||
                        fileName.Equals("ModAPI.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string apiVersion = GetModRequiredApiVersion(dllPath);
                    results.Add(new ModAssemblyVersion { DllName = fileName, ApiVersion = apiVersion });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssemblyVersionChecker] Error scanning assemblies in {modPath}: {ex.Message}");
            }

            return results;
        }
    }
}
