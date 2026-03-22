using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using HarmonyLib;
using ModApiMMLog = ModAPI.Core.MMLog;

namespace Cortex.Platform.ModAPI.Runtime
{
    internal sealed class HarmonyRuntimeInspectionService : IHarmonyRuntimeInspectionService
    {
        public bool IsAvailable
        {
            get { return true; }
        }

        public HarmonyPatchSnapshot CaptureSnapshot()
        {
            var snapshot = new HarmonyPatchSnapshot();
            snapshot.GeneratedUtc = DateTime.UtcNow;
            WriteInfo("Runtime snapshot capture started.");

            try
            {
                var methods = new List<HarmonyMethodPatchSummary>();
                foreach (var method in Harmony.GetAllPatchedMethods())
                {
                    if (method == null)
                    {
                        continue;
                    }

                    methods.Add(BuildSummary(method));
                }

                methods.Sort(CompareSummaries);
                snapshot.Methods = methods.ToArray();
                snapshot.StatusMessage = "Captured " + methods.Count + " patched method(s).";
                WriteInfo("Runtime snapshot capture completed. PatchedMethods=" + methods.Count + ".");
            }
            catch (Exception ex)
            {
                snapshot.Methods = new HarmonyMethodPatchSummary[0];
                snapshot.StatusMessage = "Harmony snapshot failed: " + ex.Message;
                WriteWarning("Runtime snapshot capture failed. Error=" + ex.Message + ".");
            }

            return snapshot;
        }

        public HarmonyMethodPatchSummary Inspect(HarmonyPatchInspectionRequest request)
        {
            WriteInfo("Runtime inspect started. Assembly='" + (request != null ? request.AssemblyPath ?? string.Empty : string.Empty) +
                "', MetadataToken=0x" + (request != null ? request.MetadataToken : 0).ToString("X8") +
                ", Display='" + (request != null ? request.DisplayName ?? string.Empty : string.Empty) + "'.");
            var method = ResolveMethod(request);
            if (method == null)
            {
                WriteWarning("Runtime inspect could not resolve the requested method.");
                return BuildUnresolvedSummary(request);
            }

            var summary = BuildSummary(method);
            WriteInfo("Runtime inspect completed. Resolved='" +
                (summary != null ? summary.ResolvedMemberDisplayName ?? string.Empty : string.Empty) +
                "', TotalPatches=" + (summary != null && summary.Counts != null ? summary.Counts.TotalCount : 0) + ".");
            return summary;
        }

        private static void WriteInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            ModApiMMLog.WriteInfo("[Cortex.Harmony] " + message);
        }

        private static void WriteWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            ModApiMMLog.WriteWarning("[Cortex.Harmony] " + message);
        }

        private static HarmonyMethodPatchSummary BuildSummary(MethodBase method)
        {
            var summary = new HarmonyMethodPatchSummary();
            summary.CapturedUtc = DateTime.UtcNow;
            summary.DeclaringType = method.DeclaringType != null ? method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty : string.Empty;
            summary.MethodName = method.Name ?? string.Empty;
            summary.Signature = BuildMethodSignature(method);
            summary.AssemblyPath = GetAssemblyPath(method);
            summary.DocumentPath = string.Empty;
            summary.CachePath = string.Empty;
            summary.ResolvedMemberDisplayName = BuildResolvedDisplayName(method);
            summary.Target = BuildNavigationTarget(method, string.Empty, string.Empty);

            var patches = Harmony.GetPatchInfo(method);
            var entries = new List<HarmonyPatchEntry>();
            var counts = new HarmonyPatchCounts();
            AddEntries(entries, counts, patches != null ? patches.Prefixes : null, HarmonyPatchKind.Prefix);
            AddEntries(entries, counts, patches != null ? patches.Postfixes : null, HarmonyPatchKind.Postfix);
            AddEntries(entries, counts, patches != null ? patches.Transpilers : null, HarmonyPatchKind.Transpiler);
            AddEntries(entries, counts, patches != null ? patches.Finalizers : null, HarmonyPatchKind.Finalizer);
            AddEntries(entries, counts, patches != null ? patches.InnerPrefixes : null, HarmonyPatchKind.InnerPrefix);
            AddEntries(entries, counts, patches != null ? patches.InnerPostfixes : null, HarmonyPatchKind.InnerPostfix);

            summary.Counts = counts;
            summary.IsPatched = counts.TotalCount > 0;
            summary.Entries = entries.ToArray();
            summary.Owners = CollectOwners(entries);
            summary.OwnerCount = summary.Owners.Length;
            summary.ConflictHint = BuildConflictHint(summary);
            summary.Order = BuildOrder(entries);
            return summary;
        }

        private static HarmonyMethodPatchSummary BuildUnresolvedSummary(HarmonyPatchInspectionRequest request)
        {
            var summary = new HarmonyMethodPatchSummary();
            summary.CapturedUtc = DateTime.UtcNow;
            summary.DeclaringType = request != null ? request.DeclaringTypeName ?? string.Empty : string.Empty;
            summary.MethodName = request != null ? request.MethodName ?? string.Empty : string.Empty;
            summary.Signature = request != null ? request.Signature ?? string.Empty : string.Empty;
            summary.AssemblyPath = request != null ? request.AssemblyPath ?? string.Empty : string.Empty;
            summary.DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty;
            summary.CachePath = request != null ? request.CachePath ?? string.Empty : string.Empty;
            summary.ResolvedMemberDisplayName = request != null ? request.DisplayName ?? string.Empty : string.Empty;
            summary.Target = new HarmonyPatchNavigationTarget
            {
                AssemblyPath = summary.AssemblyPath,
                MetadataToken = request != null ? request.MetadataToken : 0,
                DocumentPath = summary.DocumentPath,
                CachePath = summary.CachePath,
                DeclaringTypeName = summary.DeclaringType,
                MethodName = summary.MethodName,
                Signature = summary.Signature,
                DisplayName = summary.ResolvedMemberDisplayName,
                Line = 1,
                Column = 1,
                IsDecompilerTarget = !string.IsNullOrEmpty(summary.CachePath)
            };
            summary.Counts = new HarmonyPatchCounts();
            summary.Entries = new HarmonyPatchEntry[0];
            summary.Owners = new string[0];
            summary.OwnerCount = 0;
            summary.ConflictHint = "Method could not be resolved from the current runtime.";
            summary.Order = new HarmonyPatchOrderExplanation[0];
            summary.IsPatched = false;
            return summary;
        }

        private static MethodBase ResolveMethod(HarmonyPatchInspectionRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.AssemblyPath))
            {
                return null;
            }

            var assembly = ResolveAssembly(request.AssemblyPath);
            if (assembly == null)
            {
                return null;
            }

            if (request.MetadataToken > 0)
            {
                var byToken = ResolveMethodByToken(assembly, request.MetadataToken);
                if (byToken != null)
                {
                    return byToken;
                }
            }

            if (string.IsNullOrEmpty(request.DeclaringTypeName) || string.IsNullOrEmpty(request.MethodName))
            {
                return null;
            }

            var type = ResolveType(assembly, request.DeclaringTypeName);
            if (type == null)
            {
                return null;
            }

            return ResolveMethodByName(type, request.MethodName);
        }

        private static Assembly ResolveAssembly(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                try
                {
                    if (string.Equals(loadedAssemblies[i].Location, assemblyPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return loadedAssemblies[i];
                    }
                }
                catch
                {
                }
            }

            try
            {
                return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
            }
            catch
            {
                return null;
            }
        }

        private static MethodBase ResolveMethodByToken(Assembly assembly, int metadataToken)
        {
            if (assembly == null || metadataToken <= 0)
            {
                return null;
            }

            try
            {
                return assembly.ManifestModule.ResolveMethod(metadataToken);
            }
            catch
            {
                var modules = assembly.GetModules();
                for (var i = 0; i < modules.Length; i++)
                {
                    try
                    {
                        var method = modules[i].ResolveMethod(metadataToken);
                        if (method != null)
                        {
                            return method;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static Type ResolveType(Assembly assembly, string declaringTypeName)
        {
            if (assembly == null || string.IsNullOrEmpty(declaringTypeName))
            {
                return null;
            }

            var normalized = declaringTypeName.Replace('+', '.');
            try
            {
                var direct = assembly.GetType(normalized, false);
                if (direct != null)
                {
                    return direct;
                }

                var types = assembly.GetTypes();
                for (var i = 0; i < types.Length; i++)
                {
                    var candidate = types[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    var name = (candidate.FullName ?? candidate.Name ?? string.Empty).Replace('+', '.');
                    if (string.Equals(name, normalized, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static MethodBase ResolveMethodByName(Type type, string methodName)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var methods = type.GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                if (string.Equals(methods[i].Name, methodName, StringComparison.Ordinal))
                {
                    return methods[i];
                }
            }

            if (string.Equals(methodName, ".ctor", StringComparison.Ordinal) ||
                string.Equals(methodName, type.Name, StringComparison.Ordinal))
            {
                var constructors = type.GetConstructors(flags);
                if (constructors.Length > 0)
                {
                    return constructors[0];
                }
            }

            if (string.Equals(methodName, ".cctor", StringComparison.Ordinal))
            {
                return type.TypeInitializer;
            }

            return null;
        }

        private static void AddEntries(
            List<HarmonyPatchEntry> entries,
            HarmonyPatchCounts counts,
            IList<Patch> patches,
            HarmonyPatchKind patchKind)
        {
            if (entries == null || counts == null || patches == null || patches.Count == 0)
            {
                return;
            }

            for (var i = 0; i < patches.Count; i++)
            {
                var patch = patches[i];
                if (patch == null)
                {
                    continue;
                }

                entries.Add(BuildPatchEntry(patch, patchKind));
                counts.TotalCount++;
                switch (patchKind)
                {
                    case HarmonyPatchKind.Prefix:
                        counts.PrefixCount++;
                        break;
                    case HarmonyPatchKind.Postfix:
                        counts.PostfixCount++;
                        break;
                    case HarmonyPatchKind.Transpiler:
                        counts.TranspilerCount++;
                        break;
                    case HarmonyPatchKind.Finalizer:
                        counts.FinalizerCount++;
                        break;
                    case HarmonyPatchKind.InnerPrefix:
                        counts.InnerPrefixCount++;
                        break;
                    case HarmonyPatchKind.InnerPostfix:
                        counts.InnerPostfixCount++;
                        break;
                }
            }
        }

        private static HarmonyPatchEntry BuildPatchEntry(Patch patch, HarmonyPatchKind patchKind)
        {
            MethodInfo patchMethod = null;
            string declaringTypeName = string.Empty;
            string methodName = string.Empty;
            string signature = string.Empty;
            string assemblyPath = string.Empty;
            int metadataToken = 0;

            try
            {
                patchMethod = patch.PatchMethod;
                if (patchMethod != null)
                {
                    declaringTypeName = patchMethod.DeclaringType != null ? patchMethod.DeclaringType.FullName ?? patchMethod.DeclaringType.Name ?? string.Empty : string.Empty;
                    methodName = patchMethod.Name ?? string.Empty;
                    signature = BuildMethodSignature(patchMethod);
                    assemblyPath = GetAssemblyPath(patchMethod);
                    metadataToken = patchMethod.MetadataToken;
                }
            }
            catch
            {
            }

            return new HarmonyPatchEntry
            {
                PatchKind = patchKind,
                OwnerId = patch.owner ?? string.Empty,
                OwnerDisplayName = patch.owner ?? string.Empty,
                PatchMethodDeclaringType = declaringTypeName,
                PatchMethodName = methodName,
                PatchMethodSignature = signature,
                Priority = patch.priority,
                Before = patch.before ?? new string[0],
                After = patch.after ?? new string[0],
                Index = patch.index,
                AssemblyPath = assemblyPath,
                MetadataToken = metadataToken,
                NavigationTarget = BuildNavigationTarget(patchMethod, string.Empty, string.Empty)
            };
        }

        private static HarmonyPatchOrderExplanation[] BuildOrder(List<HarmonyPatchEntry> entries)
        {
            var results = new List<HarmonyPatchOrderExplanation>();
            AddOrderGroup(results, entries, HarmonyPatchKind.Prefix);
            AddOrderGroup(results, entries, HarmonyPatchKind.Postfix);
            AddOrderGroup(results, entries, HarmonyPatchKind.Transpiler);
            AddOrderGroup(results, entries, HarmonyPatchKind.Finalizer);
            return results.ToArray();
        }

        private static void AddOrderGroup(List<HarmonyPatchOrderExplanation> results, List<HarmonyPatchEntry> entries, HarmonyPatchKind patchKind)
        {
            var filtered = new List<HarmonyPatchEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].PatchKind == patchKind)
                {
                    filtered.Add(entries[i]);
                }
            }

            if (filtered.Count == 0)
            {
                return;
            }

            filtered.Sort(CompareEntries);
            var items = new HarmonyPatchOrderExplanationItem[filtered.Count];
            for (var i = 0; i < filtered.Count; i++)
            {
                items[i] = new HarmonyPatchOrderExplanationItem
                {
                    Position = i + 1,
                    OwnerId = filtered[i].OwnerId,
                    PatchMethodName = filtered[i].PatchMethodName,
                    Priority = filtered[i].Priority,
                    Index = filtered[i].Index,
                    Before = filtered[i].Before ?? new string[0],
                    After = filtered[i].After ?? new string[0],
                    Explanation = BuildOrderExplanationText(filtered[i])
                };
            }

            results.Add(new HarmonyPatchOrderExplanation
            {
                PatchKind = patchKind,
                Disclaimer = "Sorted by Harmony priority and registration index. before/after constraints are shown separately and may still affect effective runtime order.",
                Items = items
            });
        }

        private static string BuildOrderExplanationText(HarmonyPatchEntry entry)
        {
            var text = "Priority " + entry.Priority + ", Index " + entry.Index + ".";
            if (entry.Before != null && entry.Before.Length > 0)
            {
                text += " Before: " + string.Join(", ", entry.Before) + ".";
            }

            if (entry.After != null && entry.After.Length > 0)
            {
                text += " After: " + string.Join(", ", entry.After) + ".";
            }

            return text;
        }

        private static int CompareEntries(HarmonyPatchEntry left, HarmonyPatchEntry right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var priorityOrder = right.Priority.CompareTo(left.Priority);
            if (priorityOrder != 0)
            {
                return priorityOrder;
            }

            var indexOrder = left.Index.CompareTo(right.Index);
            if (indexOrder != 0)
            {
                return indexOrder;
            }

            return string.Compare(left.PatchMethodName, right.PatchMethodName, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareSummaries(HarmonyMethodPatchSummary left, HarmonyMethodPatchSummary right)
        {
            var leftName = left != null ? left.ResolvedMemberDisplayName ?? left.MethodName ?? string.Empty : string.Empty;
            var rightName = right != null ? right.ResolvedMemberDisplayName ?? right.MethodName ?? string.Empty : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] CollectOwners(List<HarmonyPatchEntry> entries)
        {
            var owners = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (entries == null)
            {
                return owners.ToArray();
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var owner = entries[i] != null ? entries[i].OwnerId ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(owner) || !seen.Add(owner))
                {
                    continue;
                }

                owners.Add(owner);
            }

            return owners.ToArray();
        }

        private static string BuildConflictHint(HarmonyMethodPatchSummary summary)
        {
            if (summary == null || summary.Counts == null)
            {
                return string.Empty;
            }

            if (summary.Counts.TotalCount >= 6 || summary.OwnerCount >= 3)
            {
                return "High patch crowding. Review priorities and before/after constraints carefully.";
            }

            if (summary.OwnerCount > 1)
            {
                return "Patched by multiple owners. Order depends on Harmony priority plus before/after constraints.";
            }

            if (summary.Counts.TranspilerCount > 0 || summary.Counts.FinalizerCount > 0)
            {
                return "Advanced patch types are present. Runtime behavior may depend on IL transforms or exception flow.";
            }

            if (summary.Counts.TotalCount > 0)
            {
                return "Patched. Review priority and before/after metadata before assuming load order.";
            }

            return "No active Harmony patches were found for this method.";
        }

        private static HarmonyPatchNavigationTarget BuildNavigationTarget(MethodBase method, string documentPath, string cachePath)
        {
            if (method == null)
            {
                return new HarmonyPatchNavigationTarget
                {
                    DocumentPath = documentPath ?? string.Empty,
                    CachePath = cachePath ?? string.Empty,
                    Line = 1,
                    Column = 1,
                    IsDecompilerTarget = !string.IsNullOrEmpty(cachePath)
                };
            }

            return new HarmonyPatchNavigationTarget
            {
                AssemblyPath = GetAssemblyPath(method),
                MetadataToken = method.MetadataToken,
                DocumentPath = documentPath ?? string.Empty,
                CachePath = cachePath ?? string.Empty,
                DeclaringTypeName = method.DeclaringType != null ? method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty : string.Empty,
                MethodName = method.Name ?? string.Empty,
                Signature = BuildMethodSignature(method),
                DisplayName = BuildResolvedDisplayName(method),
                Line = 1,
                Column = 1,
                IsDecompilerTarget = !string.IsNullOrEmpty(cachePath)
            };
        }

        private static string BuildResolvedDisplayName(MethodBase method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var declaringType = method.DeclaringType != null ? method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty : string.Empty;
            return declaringType + "." + BuildMethodSignature(method);
        }

        private static string BuildMethodSignature(MethodBase method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var parameters = method.GetParameters();
            var parts = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                var typeName = parameterType != null ? parameterType.Name ?? string.Empty : "object";
                parts[i] = typeName + " " + (parameters[i].Name ?? ("arg" + i));
            }

            var prefix = string.Empty;
            var methodInfo = method as MethodInfo;
            if (methodInfo != null && methodInfo.ReturnType != null)
            {
                prefix = methodInfo.ReturnType.Name + " ";
            }

            return prefix + (method.Name ?? string.Empty) + "(" + string.Join(", ", parts) + ")";
        }

        private static string GetAssemblyPath(MethodBase method)
        {
            try
            {
                return method != null && method.DeclaringType != null && method.DeclaringType.Assembly != null
                    ? method.DeclaringType.Assembly.Location ?? string.Empty
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
