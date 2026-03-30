using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed partial class HarmonyWorkflowController
    {
        private HarmonyMethodPatchSummary NormalizeSummary(IWorkbenchModuleRuntime runtime, HarmonyMethodPatchSummary summary)
        {
            if (summary == null)
            {
                return null;
            }

            summary.Counts = summary.Counts ?? new HarmonyPatchCounts();
            summary.Entries = summary.Entries ?? new HarmonyPatchEntry[0];
            summary.Order = summary.Order ?? new HarmonyPatchOrderExplanation[0];
            summary.Owners = summary.Owners ?? new string[0];
            summary.Target = summary.Target ?? new HarmonyPatchNavigationTarget();
            if (string.IsNullOrEmpty(summary.Target.AssemblyPath))
            {
                summary.Target.AssemblyPath = summary.AssemblyPath ?? string.Empty;
            }

            if (string.IsNullOrEmpty(summary.Target.DisplayName))
            {
                summary.Target.DisplayName = _displayService.BuildTargetDisplayName(summary);
            }

            var targetProject = FindProject(runtime, summary.AssemblyPath, summary.DocumentPath);
            if (targetProject != null)
            {
                summary.ProjectModId = targetProject.ModId ?? string.Empty;
                summary.ProjectSourceRootPath = targetProject.SourceRootPath ?? string.Empty;
            }

            var targetMod = FindLoadedMod(runtime, summary.AssemblyPath);
            if (targetMod != null)
            {
                summary.LoadedModId = targetMod.ModId ?? string.Empty;
                summary.LoadedModRootPath = targetMod.RootPath ?? string.Empty;
            }

            for (var i = 0; i < summary.Entries.Length; i++)
            {
                var entry = summary.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                entry.Before = entry.Before ?? new string[0];
                entry.After = entry.After ?? new string[0];
                if (string.IsNullOrEmpty(entry.OwnerDisplayName))
                {
                    entry.OwnerDisplayName = entry.OwnerId ?? string.Empty;
                }

                entry.OwnerAssociation = ResolveOwnerAssociation(runtime, entry);
                if (entry.OwnerAssociation != null && !string.IsNullOrEmpty(entry.OwnerAssociation.DisplayName))
                {
                    entry.OwnerDisplayName = entry.OwnerAssociation.DisplayName;
                }
            }

            if (summary.CapturedUtc == DateTime.MinValue)
            {
                summary.CapturedUtc = DateTime.UtcNow;
            }

            return summary;
        }

        private bool TryNavigate(IWorkbenchModuleRuntime runtime, HarmonyPatchNavigationTarget target, string successMessage, out string statusMessage)
        {
            statusMessage = "Could not open the requested Harmony target.";
            if (runtime == null || target == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(target.DocumentPath) && File.Exists(target.DocumentPath) && !IsDecompilerPath(target.DocumentPath))
            {
                var session = runtime.Documents != null ? runtime.Documents.Open(target.DocumentPath, target.Line > 0 ? target.Line : 1) : null;
                if (session != null)
                {
                    statusMessage = successMessage;
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(target.AssemblyPath) && target.MetadataToken > 0)
            {
                var response = runtime.Navigation != null
                    ? runtime.Navigation.RequestDecompilerSource(target.AssemblyPath, target.MetadataToken, DecompilerEntityKind.Method, false)
                    : null;
                if (response != null && runtime.Navigation.OpenDecompilerResult(response, target.Line > 0 ? target.Line : 1))
                {
                    statusMessage = successMessage;
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(target.CachePath) && File.Exists(target.CachePath))
            {
                var cacheSession = runtime.Documents != null ? runtime.Documents.Open(target.CachePath, target.Line > 0 ? target.Line : 1) : null;
                if (cacheSession != null)
                {
                    statusMessage = successMessage;
                    return true;
                }
            }

            return false;
        }

        private static string BuildSummaryKey(HarmonyPatchInspectionRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(request.AssemblyPath) && request.MetadataToken > 0)
            {
                return request.AssemblyPath + "|0x" + request.MetadataToken.ToString("X8");
            }

            return (request.AssemblyPath ?? string.Empty) + "|" +
                (request.DeclaringTypeName ?? string.Empty) + "|" +
                (request.MethodName ?? string.Empty) + "|" +
                (request.Signature ?? string.Empty);
        }

        private static CortexProjectDefinition FindProject(IWorkbenchModuleRuntime runtime, string assemblyPath, string documentPath)
        {
            var projects = runtime != null && runtime.Projects != null ? runtime.Projects.GetProjects() : null;
            if (projects == null)
            {
                return null;
            }

            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(documentPath) && PathStartsWith(documentPath, project.SourceRootPath))
                {
                    return project;
                }

                if (!string.IsNullOrEmpty(assemblyPath) &&
                    !string.IsNullOrEmpty(project.OutputAssemblyPath) &&
                    PathsEqual(assemblyPath, project.OutputAssemblyPath))
                {
                    return project;
                }
            }

            return null;
        }

        private static LoadedModInfo FindLoadedMod(IWorkbenchModuleRuntime runtime, string assemblyPath)
        {
            var mods = runtime != null && runtime.Projects != null ? runtime.Projects.GetLoadedMods() : null;
            if (mods == null || string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            for (var i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod != null && PathStartsWith(assemblyPath, mod.RootPath))
                {
                    return mod;
                }
            }

            return null;
        }

        private static void AddNamespacePrefixes(HashSet<string> namespaces, string assemblyPath, string declaringTypeName)
        {
            var normalizedType = NormalizeTypeName(declaringTypeName);
            var lastDot = normalizedType.LastIndexOf('.');
            if (lastDot <= 0)
            {
                return;
            }

            var namespacePath = normalizedType.Substring(0, lastDot);
            var segments = namespacePath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            for (var i = 0; i < segments.Length; i++)
            {
                current = string.IsNullOrEmpty(current) ? segments[i] : current + "." + segments[i];
                namespaces.Add((assemblyPath ?? string.Empty) + "|" + current);
            }
        }

        private static string NormalizeTypeName(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace('+', '.').Trim();
        }
    }
}
