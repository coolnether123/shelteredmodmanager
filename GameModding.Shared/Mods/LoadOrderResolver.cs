using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameModding.Shared.Mods
{
    public sealed class ResolutionResult
    {
        public readonly List<ModInfo> Mods;
        public readonly List<string> MissingHardDependencies;
        public readonly HashSet<string> CycledModIds;

        public ResolutionResult(List<ModInfo> mods, List<string> errors, HashSet<string> cycledModIds)
        {
            Mods = mods;
            MissingHardDependencies = errors;
            CycledModIds = cycledModIds;
        }
    }

    public sealed class OrderEvaluation
    {
        public readonly List<string> EnabledOrder;
        public readonly List<string> SortedIds;
        public readonly HashSet<string> HardIssues;
        public readonly HashSet<string> SoftIssues;
        public readonly List<string> MissingHardDependencies;
        public readonly HashSet<string> CycledModIds;

        public OrderEvaluation(
            List<string> enabledOrder,
            List<string> sortedIds,
            HashSet<string> hardIssues,
            HashSet<string> softIssues,
            List<string> missingHardDependencies,
            HashSet<string> cycled)
        {
            EnabledOrder = enabledOrder;
            SortedIds = sortedIds;
            HardIssues = hardIssues;
            SoftIssues = softIssues;
            MissingHardDependencies = missingHardDependencies;
            CycledModIds = cycled;
        }
    }

    public static class LoadOrderResolver
    {
        public sealed class ModStatusEntry
        {
            public bool enabled = true;
            public bool locked = false;
            public string notes;
        }

        [Serializable]
        public sealed class ModStatusEntryKV
        {
            public string id;
            public bool enabled = true;
            public bool locked = false;
            public string notes;
        }

        [Serializable]
        public sealed class LoadOrderStateFile
        {
            public string[] order;
            public ModStatusEntryKV[] mods;
        }

        public sealed class ProcessedLoadOrderData
        {
            public string[] Order;
            public Dictionary<string, ModStatusEntry> Mods;

            public ProcessedLoadOrderData()
            {
                Order = new string[0];
                Mods = new Dictionary<string, ModStatusEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private struct Edge
        {
            public readonly string To;
            public readonly bool IsHard;

            public Edge(string to, bool isHard)
            {
                To = to;
                IsHard = isHard;
            }
        }

        private sealed class EdgeComparer : IEqualityComparer<Edge>
        {
            public bool Equals(Edge x, Edge y)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(x.To, y.To) && x.IsHard == y.IsHard;
            }

            public int GetHashCode(Edge obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.To ?? string.Empty) ^ (obj.IsHard ? 1 : 0);
            }
        }

        private sealed class ModConstraint
        {
            public readonly string ID;
            public readonly Version Version;
            public readonly string Operator;

            public ModConstraint(string id, Version version, string op)
            {
                ID = id;
                Version = version;
                Operator = op;
            }

            public bool IsSatisfiedBy(string versionString)
            {
                if (Version == null || string.IsNullOrEmpty(Operator))
                {
                    return true;
                }

                if (string.IsNullOrEmpty(versionString))
                {
                    return false;
                }

                Version other;
                try
                {
                    other = new Version(versionString);
                }
                catch
                {
                    return false;
                }

                switch (Operator)
                {
                    case ">=": return other >= Version;
                    case "<=": return other <= Version;
                    case ">": return other > Version;
                    case "<": return other < Version;
                    case "==": return other == Version;
                    case "!=": return other != Version;
                    default: return true;
                }
            }
        }

        private sealed class GraphBuilderResult
        {
            public readonly Dictionary<string, HashSet<Edge>> Adj = new Dictionary<string, HashSet<Edge>>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> Indeg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Errors = new List<string>();
        }

        private sealed class SortResult
        {
            public readonly List<string> SortedIds;
            public readonly HashSet<string> CycledIds;

            public SortResult(List<string> sorted, HashSet<string> cycled)
            {
                SortedIds = sorted;
                CycledIds = cycled;
            }
        }

        public static ResolutionResult Resolve(List<ModInfo> discovered, IEnumerable<string> userOrder)
        {
            Dictionary<string, ModInfo> byId;
            List<string> ids;
            Dictionary<string, int> priority;
            InitializeModData(discovered, userOrder, out byId, out ids, out priority);

            var graphResult = BuildDependencyGraph(discovered, byId);
            var sortResult = PerformTopologicalSort(ids, graphResult.Adj, graphResult.Indeg, priority);

            var resultMods = new List<ModInfo>(sortResult.SortedIds.Count);
            foreach (var id in sortResult.SortedIds)
            {
                if (byId.ContainsKey(id))
                {
                    resultMods.Add(byId[id]);
                }
            }

            return new ResolutionResult(resultMods, graphResult.Errors, sortResult.CycledIds);
        }

        public static OrderEvaluation Evaluate(List<ModInfo> discovered, IEnumerable<string> userOrder)
        {
            if (discovered == null)
            {
                discovered = new List<ModInfo>();
            }

            if (userOrder == null)
            {
                userOrder = new string[0];
            }

            Dictionary<string, ModInfo> byId;
            List<string> ids;
            Dictionary<string, int> priority;
            InitializeModData(discovered, userOrder, out byId, out ids, out priority);

            var graphResult = BuildDependencyGraph(discovered, byId);
            var sortResult = PerformTopologicalSort(ids, graphResult.Adj, graphResult.Indeg, priority);

            var enabledOrder = new List<string>();
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var pos = 0;
            foreach (var id in userOrder)
            {
                var normalized = NormalizeId(id);
                if (!byId.ContainsKey(normalized))
                {
                    continue;
                }

                if (!index.ContainsKey(normalized))
                {
                    index[normalized] = pos++;
                    enabledOrder.Add(normalized);
                }
            }

            var hardIssues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var softIssues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in graphResult.Adj)
            {
                foreach (var edge in kv.Value)
                {
                    int fromIndex;
                    int toIndex;
                    if (!index.TryGetValue(kv.Key, out fromIndex) || !index.TryGetValue(edge.To, out toIndex))
                    {
                        continue;
                    }

                    if (toIndex < fromIndex)
                    {
                        if (edge.IsHard)
                        {
                            hardIssues.Add(edge.To);
                        }
                        else
                        {
                            softIssues.Add(edge.To);
                        }
                    }
                }
            }

            return new OrderEvaluation(enabledOrder, sortResult.SortedIds, hardIssues, softIssues, graphResult.Errors, sortResult.CycledIds);
        }

        private static void InitializeModData(List<ModInfo> discovered, IEnumerable<string> userOrder, out Dictionary<string, ModInfo> byId, out List<string> ids, out Dictionary<string, int> priority)
        {
            byId = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
            ids = new List<string>();
            foreach (var mod in discovered)
            {
                var id = GetModId(mod);
                if (!byId.ContainsKey(id))
                {
                    byId[id] = mod;
                    ids.Add(id);
                }
            }

            priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var p = 0;
            foreach (var id in userOrder)
            {
                var normalized = NormalizeId(id);
                if (!priority.ContainsKey(normalized))
                {
                    priority[normalized] = p++;
                }
            }

            var remainingIds = new List<string>();
            foreach (var id in ids)
            {
                if (!priority.ContainsKey(id))
                {
                    remainingIds.Add(id);
                }
            }

            remainingIds.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var id in remainingIds)
            {
                if (!priority.ContainsKey(id))
                {
                    priority[id] = p++;
                }
            }
        }

        private static GraphBuilderResult BuildDependencyGraph(List<ModInfo> discovered, Dictionary<string, ModInfo> byId)
        {
            var result = new GraphBuilderResult();
            var edgeComparer = new EdgeComparer();

            foreach (var id in byId.Keys)
            {
                result.Adj[id] = new HashSet<Edge>(edgeComparer);
                result.Indeg[id] = 0;
            }

            Action<string, string, bool> addEdge = delegate(string from, string to, bool isHard)
            {
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!result.Adj.ContainsKey(from) || !result.Adj.ContainsKey(to))
                {
                    return;
                }

                var edge = new Edge(to, isHard);
                if (result.Adj[from].Add(edge))
                {
                    result.Indeg[to]++;
                }
            };

            foreach (var mod in discovered)
            {
                var modId = GetModId(mod);
                if (!result.Adj.ContainsKey(modId) || mod.About == null)
                {
                    continue;
                }

                if (mod.About.dependsOn != null)
                {
                    foreach (var dep in mod.About.dependsOn)
                    {
                        var constraint = ParseConstraint(dep);
                        if (constraint == null)
                        {
                            continue;
                        }

                        if (!byId.ContainsKey(constraint.ID))
                        {
                            result.Errors.Add("Mod '" + modId + "' has a missing hard dependency: '" + constraint.ID + "'.");
                            continue;
                        }

                        var depMod = byId[constraint.ID];
                        if (constraint.IsSatisfiedBy(depMod.About != null ? depMod.About.version : null))
                        {
                            addEdge(constraint.ID, modId, true);
                        }
                        else
                        {
                            result.Errors.Add("Mod '" + modId + "' requires dependency '" + constraint.ID + "' version " + constraint.Operator + constraint.Version + ", but found version " + ((depMod.About != null ? depMod.About.version : null) ?? "none") + ".");
                        }
                    }
                }

                if (mod.About.loadBefore != null)
                {
                    foreach (var item in mod.About.loadBefore)
                    {
                        var constraint = ParseConstraint(item);
                        if (constraint == null || !byId.ContainsKey(constraint.ID))
                        {
                            continue;
                        }

                        var beforeMod = byId[constraint.ID];
                        if (constraint.IsSatisfiedBy(beforeMod.About != null ? beforeMod.About.version : null))
                        {
                            addEdge(modId, constraint.ID, false);
                        }
                    }
                }

                if (mod.About.loadAfter != null)
                {
                    foreach (var item in mod.About.loadAfter)
                    {
                        var constraint = ParseConstraint(item);
                        if (constraint == null || !byId.ContainsKey(constraint.ID))
                        {
                            continue;
                        }

                        var afterMod = byId[constraint.ID];
                        if (constraint.IsSatisfiedBy(afterMod.About != null ? afterMod.About.version : null))
                        {
                            addEdge(constraint.ID, modId, false);
                        }
                    }
                }
            }

            return result;
        }

        private static SortResult PerformTopologicalSort(List<string> ids, Dictionary<string, HashSet<Edge>> adj, Dictionary<string, int> indeg, Dictionary<string, int> priority)
        {
            var comparer = new PriorityComparer(priority);
            var heap = new List<string>();
            foreach (var kv in indeg)
            {
                if (kv.Value == 0)
                {
                    heap.Add(kv.Key);
                }
            }

            heap.Sort(comparer);
            var sortedIds = new List<string>();

            while (heap.Count > 0)
            {
                var id = heap[0];
                heap.RemoveAt(0);
                sortedIds.Add(id);

                foreach (var edge in adj[id].OrderBy(x => x.To, StringComparer.OrdinalIgnoreCase))
                {
                    indeg[edge.To]--;
                    if (indeg[edge.To] == 0)
                    {
                        var index = heap.BinarySearch(edge.To, comparer);
                        if (index < 0)
                        {
                            index = ~index;
                        }

                        heap.Insert(index, edge.To);
                    }
                }
            }

            var cycled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sortedIds.Count < ids.Count)
            {
                var remaining = new HashSet<string>(ids.Where(delegate(string id) { return !sortedIds.Contains(id); }), StringComparer.OrdinalIgnoreCase);
                var tempIndeg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in remaining)
                {
                    tempIndeg[id] = 0;
                }

                foreach (var id in remaining)
                {
                    foreach (var edge in adj[id])
                    {
                        if (edge.IsHard && remaining.Contains(edge.To))
                        {
                            tempIndeg[edge.To]++;
                        }
                    }
                }

                var tempHeap = new List<string>();
                foreach (var kv in tempIndeg)
                {
                    if (kv.Value == 0)
                    {
                        tempHeap.Add(kv.Key);
                    }
                }

                tempHeap.Sort(comparer);
                var resolved = new List<string>();
                while (tempHeap.Count > 0)
                {
                    var id = tempHeap[0];
                    tempHeap.RemoveAt(0);
                    resolved.Add(id);
                    foreach (var edge in adj[id])
                    {
                        if (edge.IsHard && remaining.Contains(edge.To))
                        {
                            tempIndeg[edge.To]--;
                            if (tempIndeg[edge.To] == 0)
                            {
                                var index = tempHeap.BinarySearch(edge.To, comparer);
                                if (index < 0)
                                {
                                    index = ~index;
                                }

                                tempHeap.Insert(index, edge.To);
                            }
                        }
                    }
                }

                sortedIds.AddRange(resolved);

                if (resolved.Count < remaining.Count)
                {
                    foreach (var id in remaining)
                    {
                        if (!resolved.Contains(id))
                        {
                            cycled.Add(id);
                        }
                    }

                    var unresolved = new List<string>(cycled);
                    unresolved.Sort(comparer);
                    sortedIds.AddRange(unresolved);
                }
            }

            return new SortResult(sortedIds, cycled);
        }

        private static ModConstraint ParseConstraint(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            raw = raw.Trim();
            var match = Regex.Match(raw, "^\\s*([a-zA-Z0-9_.-]+)\\s*(>=|<=|>|<|==|!=)?\\s*([0-9.]+)?\\s*$");
            if (!match.Success)
            {
                return new ModConstraint(NormalizeId(raw), null, null);
            }

            var id = NormalizeId(match.Groups[1].Value);
            var op = match.Groups[2].Value;
            var versionText = match.Groups[3].Value;

            Version version = null;
            if (!string.IsNullOrEmpty(versionText))
            {
                try
                {
                    version = new Version(versionText);
                }
                catch
                {
                    version = null;
                }
            }

            if ((!string.IsNullOrEmpty(op) && version == null) || (string.IsNullOrEmpty(op) && version != null))
            {
                return new ModConstraint(id, null, null);
            }

            return new ModConstraint(id, version, op);
        }

        private static string GetModId(ModInfo mod)
        {
            var id = mod != null && mod.About != null && !string.IsNullOrEmpty(mod.About.id)
                ? mod.About.id.Trim().ToLowerInvariant()
                : (mod != null ? mod.Id ?? mod.Name ?? mod.RootPath : null);

            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
            }

            return id;
        }

        private static string NormalizeId(string id)
        {
            return (id ?? string.Empty).Trim().ToLowerInvariant();
        }

        private sealed class PriorityComparer : IComparer<string>
        {
            private readonly Dictionary<string, int> _priority;

            public PriorityComparer(Dictionary<string, int> priority)
            {
                _priority = priority;
            }

            public int Compare(string x, string y)
            {
                var px = _priority.ContainsKey(x) ? _priority[x] : int.MaxValue;
                var py = _priority.ContainsKey(y) ? _priority[y] : int.MaxValue;
                var compare = px.CompareTo(py);
                if (compare != 0)
                {
                    return compare;
                }

                return StringComparer.OrdinalIgnoreCase.Compare(x, y);
            }
        }
    }
}
