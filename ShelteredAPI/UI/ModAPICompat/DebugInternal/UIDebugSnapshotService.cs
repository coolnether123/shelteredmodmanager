using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ModAPI.Internal.DebugUI
{
    using Snapshot = ModAPI.Harmony.TranspilerDebugger.Snapshot;

    internal static class UIDebugSnapshotService
    {
        public static List<Snapshot> GetVisibleSnapshots(
            IList<Snapshot> history,
            bool showCorePatches,
            bool showExternalPatches,
            bool sceneFilteredOnly,
            string historyMethodSearch,
            string activeSceneName,
            ICollection<string> sceneTypeHints)
        {
            if (history == null) return new List<Snapshot>();
            IEnumerable<Snapshot> query = history;

            query = query.Where(snapshot =>
            {
                var isCore = IsCoreSnapshot(snapshot);
                if (isCore && !showCorePatches) return false;
                if (!isCore && !showExternalPatches) return false;
                return true;
            });

            var hasMethodSearch = !string.IsNullOrEmpty(historyMethodSearch);
            if (sceneFilteredOnly && !hasMethodSearch)
            {
                query = query.Where(snapshot => IsSnapshotSceneRelevant(snapshot, activeSceneName, sceneTypeHints));
            }

            if (hasMethodSearch)
            {
                var search = historyMethodSearch.Trim();
                query = query.Where(snapshot =>
                {
                    if (snapshot == null) return false;
                    var methodId = BuildSnapshotMethodId(snapshot) ?? string.Empty;
                    var haystack = string.Join(" ", new[]
                    {
                        snapshot.ModId ?? string.Empty,
                        methodId,
                        snapshot.StepName ?? string.Empty,
                        snapshot.MethodName ?? string.Empty,
                        snapshot.PatchOrigin ?? string.Empty
                    });
                    return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                });
            }

            var filtered = query.ToList();
            if (filtered.Count <= 1) return filtered;

            var deduped = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
            for (var i = 0; i < filtered.Count; i++)
            {
                var snapshot = filtered[i];
                var key = BuildSnapshotDedupKey(snapshot);
                if (string.IsNullOrEmpty(key))
                {
                    key = "__index__" + i;
                }

                if (deduped.TryGetValue(key, out var existing))
                {
                    if (existing == null || snapshot.Timestamp > existing.Timestamp)
                    {
                        deduped[key] = snapshot;
                    }
                }
                else
                {
                    deduped[key] = snapshot;
                }
            }

            return deduped.Values.OrderBy(snapshot => snapshot.Timestamp).ToList();
        }

        public static List<MethodBase> FindRuntimeMethodMatches(string search, int maxMatches)
        {
            var result = new List<MethodBase>();
            if (string.IsNullOrEmpty(search)) return result;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var normalized = search.Trim();
            if (string.IsNullOrEmpty(normalized)) return result;

            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assemblies = new List<Assembly>(allAssemblies.Length);
            for (var i = 0; i < allAssemblies.Length; i++)
            {
                var assembly = allAssemblies[i];
                if (assembly != null) assemblies.Add(assembly);
            }

            assemblies.Sort((left, right) =>
            {
                var leftName = left != null ? (left.GetName().Name ?? string.Empty) : string.Empty;
                var rightName = right != null ? (right.GetName().Name ?? string.Empty) : string.Empty;
                var leftRank = string.Equals(leftName, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ? 0
                    : (leftName.IndexOf("ModAPI", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2);
                var rightRank = string.Equals(rightName, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ? 0
                    : (rightName.IndexOf("ModAPI", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2);
                var rankCompare = leftRank.CompareTo(rightRank);
                if (rankCompare != 0) return rankCompare;
                return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
            });

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Count && result.Count < maxMatches; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch (ReflectionTypeLoadException rtl)
                {
                    var partialTypes = new List<Type>();
                    if (rtl.Types != null)
                    {
                        for (var i = 0; i < rtl.Types.Length; i++)
                        {
                            var type = rtl.Types[i];
                            if (type != null) partialTypes.Add(type);
                        }
                    }

                    types = partialTypes.ToArray();
                }
                catch
                {
                    continue;
                }

                for (var typeIndex = 0; typeIndex < types.Length && result.Count < maxMatches; typeIndex++)
                {
                    var type = types[typeIndex];
                    if (type == null) continue;

                    var typeName = type.FullName ?? type.Name ?? string.Empty;
                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(flags);
                    }
                    catch
                    {
                        continue;
                    }

                    for (var methodIndex = 0; methodIndex < methods.Length && result.Count < maxMatches; methodIndex++)
                    {
                        var method = methods[methodIndex];
                        if (method == null) continue;

                        var hit =
                            typeName.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            method.Name.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!hit) continue;

                        var key = typeName + "::" + method.Name;
                        if (seen.Contains(key)) continue;
                        seen.Add(key);
                        result.Add(method);
                    }
                }
            }

            return result;
        }

        public static bool IsCoreSnapshot(Snapshot snapshot)
        {
            if (snapshot == null) return false;
            var mod = snapshot.ModId ?? string.Empty;
            if (string.Equals(mod, "ModAPI", StringComparison.OrdinalIgnoreCase)) return true;

            var origin = snapshot.PatchOrigin ?? string.Empty;
            if (origin.IndexOf("Owner:ModAPI", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (origin.IndexOf("CooperativePatcher|ModAPI|", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static bool IsSnapshotSceneRelevant(Snapshot snapshot, string activeSceneName, ICollection<string> sceneTypeHints)
        {
            if (snapshot == null) return false;
            if (sceneTypeHints == null || sceneTypeHints.Count == 0) return true;

            var probe = (snapshot.MethodName ?? string.Empty) + " " + (snapshot.StepName ?? string.Empty) + " " + (snapshot.PatchOrigin ?? string.Empty);
            if (string.IsNullOrEmpty(probe)) return false;
            if (!string.IsNullOrEmpty(activeSceneName) && probe.IndexOf(activeSceneName, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            foreach (var hint in sceneTypeHints)
            {
                if (string.IsNullOrEmpty(hint)) continue;
                if (probe.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        public static string BuildMethodDisplayName(MethodBase method, Snapshot fallback)
        {
            if (method != null && method.DeclaringType != null)
            {
                return method.DeclaringType.FullName + "." + method.Name;
            }

            if (fallback != null)
            {
                return BuildSnapshotMethodId(fallback);
            }

            return "<unresolved method>";
        }

        public static string BuildSnapshotMethodId(Snapshot snapshot)
        {
            if (snapshot == null) return "<unresolved method>";
            if (!string.IsNullOrEmpty(snapshot.MethodName)) return snapshot.MethodName;
            if (!string.IsNullOrEmpty(snapshot.StepName)) return snapshot.StepName;
            return "<unresolved method>";
        }

        public static string BuildSnapshotDisplayTitle(Snapshot snapshot)
        {
            var methodId = BuildSnapshotMethodId(snapshot);
            var lastDot = methodId.LastIndexOf('.');
            if (lastDot > 0 && lastDot < methodId.Length - 1)
            {
                var previousDot = methodId.LastIndexOf('.', lastDot - 1);
                if (previousDot >= 0 && previousDot < methodId.Length - 1)
                {
                    return methodId.Substring(previousDot + 1);
                }
            }

            return methodId;
        }

        public static bool IsSnapshotForMethod(Snapshot snapshot, MethodBase method)
        {
            if (snapshot == null || method == null) return false;

            var methodId = method.DeclaringType != null
                ? method.DeclaringType.FullName + "." + method.Name
                : method.Name;
            if (string.IsNullOrEmpty(methodId)) return false;

            if (!string.IsNullOrEmpty(snapshot.MethodName) &&
                string.Equals(snapshot.MethodName, methodId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(snapshot.StepName) &&
                string.Equals(snapshot.StepName, methodId, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        public static string SafeValue(object value)
        {
            if (value == null) return "null";
            var text = value.ToString() ?? string.Empty;
            return text.Length > 160 ? text.Substring(0, 157) + "..." : text;
        }

        public static Snapshot FindLatestSnapshotForMethod(MethodBase method, IList<Snapshot> history)
        {
            if (method == null || history == null) return null;

            for (var i = history.Count - 1; i >= 0; i--)
            {
                var snapshot = history[i];
                if (IsSnapshotForMethod(snapshot, method))
                {
                    return snapshot;
                }
            }

            return null;
        }

        public static MethodBase ResolveMethodFromSnapshot(Snapshot snapshot)
        {
            if (snapshot == null) return null;

            MethodBase method;
            if (TryResolveMethodIdentifier(snapshot.MethodName, snapshot.AssemblyName, out method))
            {
                return method;
            }

            if (TryResolveMethodIdentifier(snapshot.StepName, snapshot.AssemblyName, out method))
            {
                return method;
            }

            return null;
        }

        public static bool TryResolveMethodIdentifier(string methodIdentifier, string assemblyName, out MethodBase method)
        {
            method = null;
            if (string.IsNullOrEmpty(methodIdentifier)) return false;

            var normalized = methodIdentifier.Trim();
            var paren = normalized.IndexOf('(');
            if (paren > 0) normalized = normalized.Substring(0, paren);
            var lastDot = normalized.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= normalized.Length - 1) return false;

            var typeName = normalized.Substring(0, lastDot);
            var methodName = normalized.Substring(lastDot + 1);

            Type type = null;
            if (!string.IsNullOrEmpty(assemblyName))
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (var i = 0; i < assemblies.Length; i++)
                {
                    var assembly = assemblies[i];
                    if (!string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)) continue;
                    type = assembly.GetType(typeName, false);
                    if (type != null) break;
                }
            }

            if (type == null)
            {
                type = AccessTools.TypeByName(typeName);
            }

            if (type == null) return false;

            method = AccessTools.Method(type, methodName);
            if (method != null) return true;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var methods = type.GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == methodName)
                {
                    method = methods[i];
                    return true;
                }
            }

            return false;
        }

        private static string BuildSnapshotDedupKey(Snapshot snapshot)
        {
            if (snapshot == null) return string.Empty;

            return string.Join("|", new[]
            {
                snapshot.ModId ?? string.Empty,
                BuildSnapshotMethodId(snapshot) ?? string.Empty
            });
        }
    }
}
