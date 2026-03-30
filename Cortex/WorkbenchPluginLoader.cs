using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Abstractions;
using GameModding.Shared.Serialization;

namespace Cortex
{
    internal sealed class WorkbenchPluginLoader
    {
        private const string ManifestFileName = "cortex.plugin.json";
        private readonly HashSet<string> _loadedAssemblyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IList<WorkbenchPluginLoadResult> LoadPlugins(
            CortexSettings settings,
            ICortexHostEnvironment hostEnvironment,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            IWorkbenchModuleRegistry moduleRegistry,
            IWorkbenchExtensionRegistry extensionRegistry,
            IWorkbenchRuntimeAccess runtimeAccess)
        {
            var results = new List<WorkbenchPluginLoadResult>();
            var descriptors = DiscoverPluginDescriptors(settings, hostEnvironment);
            for (var i = 0; i < descriptors.Count; i++)
            {
                LoadDescriptor(descriptors[i], commandRegistry, contributionRegistry, moduleRegistry, extensionRegistry, runtimeAccess, results);
            }

            return results;
        }

        private IList<WorkbenchPluginDescriptor> DiscoverPluginDescriptors(CortexSettings settings, ICortexHostEnvironment hostEnvironment)
        {
            var descriptors = new List<WorkbenchPluginDescriptor>();
            var seenAssemblyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var bundledRoots = SplitRoots(hostEnvironment != null ? hostEnvironment.BundledPluginSearchRoots : string.Empty);
            for (var i = 0; i < bundledRoots.Count; i++)
            {
                DiscoverInRoot(bundledRoots[i], descriptors, seenAssemblyPaths);
            }

            var explicitRoots = SplitRoots(settings != null ? settings.CortexPluginSearchRoots : string.Empty);
            for (var i = 0; i < explicitRoots.Count; i++)
            {
                DiscoverInRoot(explicitRoots[i], descriptors, seenAssemblyPaths);
            }

            var modsRoot = settings != null ? settings.ModsRootPath : string.Empty;
            if (!string.IsNullOrEmpty(modsRoot) && Directory.Exists(modsRoot))
            {
                string[] modDirectories;
                try
                {
                    modDirectories = Directory.GetDirectories(modsRoot);
                }
                catch
                {
                    modDirectories = new string[0];
                }

                for (var i = 0; i < modDirectories.Length; i++)
                {
                    var cortexRoot = Path.Combine(modDirectories[i], "Cortex");
                    DiscoverInRoot(cortexRoot, descriptors, seenAssemblyPaths);
                }
            }

            return descriptors;
        }

        private static List<string> SplitRoots(string rawRoots)
        {
            var roots = new List<string>();
            if (string.IsNullOrEmpty(rawRoots))
            {
                return roots;
            }

            var segments = rawRoots.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                try
                {
                    var root = Path.GetFullPath(segments[i].Trim());
                    if ((Directory.Exists(root) || File.Exists(root)) && !roots.Contains(root))
                    {
                        roots.Add(root);
                    }
                }
                catch
                {
                }
            }

            return roots;
        }

        private void DiscoverInRoot(
            string rootPath,
            IList<WorkbenchPluginDescriptor> descriptors,
            HashSet<string> seenAssemblyPaths)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return;
            }

            try
            {
                var normalizedRoot = Path.GetFullPath(rootPath);
                if (File.Exists(normalizedRoot))
                {
                    if (string.Equals(Path.GetFileName(normalizedRoot), ManifestFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        TryAddManifestDescriptor(normalizedRoot, descriptors, seenAssemblyPaths);
                    }

                    return;
                }

                if (!Directory.Exists(normalizedRoot))
                {
                    return;
                }

                var rootManifest = Path.Combine(normalizedRoot, ManifestFileName);
                TryAddManifestDescriptor(rootManifest, descriptors, seenAssemblyPaths);
                DiscoverInChildDirectories(normalizedRoot, descriptors, seenAssemblyPaths);

                var pluginsDirectory = Path.Combine(normalizedRoot, "Plugins");
                if (Directory.Exists(pluginsDirectory))
                {
                    var pluginManifest = Path.Combine(pluginsDirectory, ManifestFileName);
                    TryAddManifestDescriptor(pluginManifest, descriptors, seenAssemblyPaths);
                }
            }
            catch
            {
            }
        }

        private void DiscoverInChildDirectories(
            string rootPath,
            IList<WorkbenchPluginDescriptor> descriptors,
            HashSet<string> seenAssemblyPaths)
        {
            string[] childDirectories;
            try
            {
                childDirectories = Directory.GetDirectories(rootPath);
            }
            catch
            {
                childDirectories = new string[0];
            }

            for (var i = 0; i < childDirectories.Length; i++)
            {
                var childManifest = Path.Combine(childDirectories[i], ManifestFileName);
                TryAddManifestDescriptor(childManifest, descriptors, seenAssemblyPaths);
            }
        }

        private void TryAddManifestDescriptor(
            string manifestPath,
            IList<WorkbenchPluginDescriptor> descriptors,
            HashSet<string> seenAssemblyPaths)
        {
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                return;
            }

            try
            {
                var manifest = ManualJson.Deserialize<CortexPluginManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || manifest.Enabled == false || string.IsNullOrEmpty(manifest.AssemblyPath))
                {
                    return;
                }

                var baseDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                var assemblyPath = manifest.AssemblyPath;
                if (!Path.IsPathRooted(assemblyPath))
                {
                    assemblyPath = Path.Combine(baseDirectory, assemblyPath);
                }

                TryAddAssemblyDescriptor(assemblyPath, manifest.PluginId, manifest.EntryTypeName, descriptors, seenAssemblyPaths);
            }
            catch
            {
            }
        }

        private void TryAddAssemblyDescriptor(
            string assemblyPath,
            string declaredPluginId,
            string entryTypeName,
            IList<WorkbenchPluginDescriptor> descriptors,
            HashSet<string> seenAssemblyPaths)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(assemblyPath);
                if (!File.Exists(fullPath) || seenAssemblyPaths.Contains(fullPath))
                {
                    return;
                }

                seenAssemblyPaths.Add(fullPath);
                descriptors.Add(new WorkbenchPluginDescriptor
                {
                    AssemblyPath = fullPath,
                    DeclaredPluginId = declaredPluginId ?? string.Empty,
                    EntryTypeName = entryTypeName ?? string.Empty
                });
            }
            catch
            {
            }
        }

        private void LoadDescriptor(
            WorkbenchPluginDescriptor descriptor,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            IWorkbenchModuleRegistry moduleRegistry,
            IWorkbenchExtensionRegistry extensionRegistry,
            IWorkbenchRuntimeAccess runtimeAccess,
            IList<WorkbenchPluginLoadResult> results)
        {
            if (descriptor == null || string.IsNullOrEmpty(descriptor.AssemblyPath))
            {
                return;
            }

            if (_loadedAssemblyPaths.Contains(descriptor.AssemblyPath))
            {
                results.Add(new WorkbenchPluginLoadResult
                {
                    AssemblyPath = descriptor.AssemblyPath,
                    DisplayName = Path.GetFileNameWithoutExtension(descriptor.AssemblyPath),
                    Loaded = false,
                    StatusMessage = "Already loaded " + descriptor.AssemblyPath + "."
                });
                return;
            }

            try
            {
                var assembly = Assembly.LoadFrom(descriptor.AssemblyPath);
                var pluginTypes = assembly.GetTypes();
                var loadedAny = false;
                var pluginContext = new WorkbenchPluginContext(commandRegistry, contributionRegistry, moduleRegistry, extensionRegistry, runtimeAccess);

                for (var i = 0; i < pluginTypes.Length; i++)
                {
                    var type = pluginTypes[i];
                    if (type == null || type.IsAbstract)
                    {
                        continue;
                    }

                    if (!typeof(IWorkbenchPluginContributor).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(descriptor.EntryTypeName) &&
                        !string.Equals(type.FullName, descriptor.EntryTypeName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var instance = Activator.CreateInstance(type);
                    var pluginId = descriptor.DeclaredPluginId;
                    var displayName = type.Name;

                    var contextPlugin = instance as IWorkbenchPluginContributor;
                    if (contextPlugin == null)
                    {
                        continue;
                    }

                    contextPlugin.Register(pluginContext);
                    pluginId = string.IsNullOrEmpty(contextPlugin.PluginId) ? pluginId : contextPlugin.PluginId;
                    displayName = string.IsNullOrEmpty(contextPlugin.DisplayName) ? displayName : contextPlugin.DisplayName;

                    loadedAny = true;
                    results.Add(new WorkbenchPluginLoadResult
                    {
                        AssemblyPath = descriptor.AssemblyPath,
                        PluginId = pluginId,
                        DisplayName = displayName,
                        Loaded = true,
                        StatusMessage = "Loaded."
                    });
                }

                _loadedAssemblyPaths.Add(descriptor.AssemblyPath);
                if (!loadedAny)
                {
                    results.Add(new WorkbenchPluginLoadResult
                    {
                        AssemblyPath = descriptor.AssemblyPath,
                        PluginId = descriptor.DeclaredPluginId,
                        DisplayName = Path.GetFileNameWithoutExtension(descriptor.AssemblyPath),
                        Loaded = false,
                        StatusMessage = "No workbench plugin entry points were found in " + descriptor.AssemblyPath + "."
                    });
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                results.Add(new WorkbenchPluginLoadResult
                {
                    AssemblyPath = descriptor.AssemblyPath,
                    PluginId = descriptor.DeclaredPluginId,
                    DisplayName = Path.GetFileNameWithoutExtension(descriptor.AssemblyPath),
                    Loaded = false,
                    StatusMessage = BuildReflectionLoadError(ex)
                });
            }
            catch (Exception ex)
            {
                results.Add(new WorkbenchPluginLoadResult
                {
                    AssemblyPath = descriptor.AssemblyPath,
                    PluginId = descriptor.DeclaredPluginId,
                    DisplayName = Path.GetFileNameWithoutExtension(descriptor.AssemblyPath),
                    Loaded = false,
                    StatusMessage = ex.Message
                });
            }
        }

        private static string BuildReflectionLoadError(ReflectionTypeLoadException exception)
        {
            if (exception == null || exception.LoaderExceptions == null || exception.LoaderExceptions.Length == 0)
            {
                return "Cortex plugin type load failed.";
            }

            return exception.LoaderExceptions[0] != null && !string.IsNullOrEmpty(exception.LoaderExceptions[0].Message)
                ? exception.LoaderExceptions[0].Message
                : exception.Message;
        }

        private sealed class CortexPluginManifest
        {
            public string PluginId;
            public string DisplayName;
            public string AssemblyPath;
            public string EntryTypeName;
            public bool Enabled = true;
        }
    }

    internal sealed class WorkbenchPluginDescriptor
    {
        public string AssemblyPath;
        public string DeclaredPluginId;
        public string EntryTypeName;
    }

    internal sealed class WorkbenchPluginLoadResult
    {
        public string AssemblyPath;
        public string PluginId;
        public string DisplayName;
        public bool Loaded;
        public string StatusMessage;
    }
}
