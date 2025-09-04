using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/**
 * (Coolnether123) Result of the load order resolution, containing the sorted list and any errors.
 */
public class ResolutionResult
{
    public readonly List<ModEntry> Mods;
    public readonly List<string> MissingHardDependencies;
    public readonly HashSet<string> CycledModIds;

    public ResolutionResult(List<ModEntry> mods, List<string> errors, HashSet<string> cycledModIds)
    {
        Mods = mods;
        MissingHardDependencies = errors;
        CycledModIds = cycledModIds;
    }
}

public class ProcessedLoadOrderData
{
    public string[] Order;
    public Dictionary<string, LoadOrderResolver.ModStatusEntry> Mods;

    public ProcessedLoadOrderData()
    {
        Order = new string[0];
        Mods = new Dictionary<string, LoadOrderResolver.ModStatusEntry>(StringComparer.OrdinalIgnoreCase);
    }
}

/**
 * (Coolnether123) Resolves mod load order based on dependencies and user-defined priorities.
 * Uses Kahn's algorithm for topological sorting.
 */
public static class LoadOrderResolver
{
    // (Coolnether123) Structure of the loadorder.json file.
    [Serializable]
public class ModStatusEntry
{
    public bool enabled = true;
    public bool locked = false;
    public string notes;
}

    private static bool IsNullOrWhiteSpace(string s) { return s == null || s.Trim().Length == 0; }

    [Serializable]
public class ModStatusEntryKV
{
    public string id;
    public bool enabled = true;
    public bool locked = false;
    public string notes;
}

[Serializable]
public class LoadOrderFile
{
    public string[] order;
    public ModStatusEntryKV[] mods;
}

    // (Coolnether123) A dependency graph edge.
    private struct Edge
    {
        public readonly string To;
        public readonly bool IsHard;
        public Edge(string to, bool isHard) { To = to; IsHard = isHard; }
    }

    // (Coolnether123) A dependency constraint with versioning.
    private class ModConstraint
    {
        public readonly string ID;
        public readonly Version Version;
        public readonly string Operator;

        public ModConstraint(string id, Version version = null, string op = null)
        {
            ID = id;
            Version = version;
            Operator = op;
        }

        // (Coolnether123) Checks if a version satisfies the constraint.
        public bool IsSatisfiedBy(string versionString)
        {
            if (Version == null || string.IsNullOrEmpty(Operator)) return true; // No version constraint.
            if (string.IsNullOrEmpty(versionString)) return false; // Constraint needs a version, but mod has none.

            Version other;
            try { other = new Version(versionString); }
            catch { return false; } // Mod has an invalid version string.

            switch (Operator)
            {
                case ">=": return other >= Version;
                case "<=": return other <= Version;
                case ">": return other > Version;
                case "<": return other < Version;
                case "==": return other == Version;
                case "!=": return other != Version;
                default: return true; // Fallback.
            }
        }
    }

    // (Coolnether123) Case-insensitive comparer for mod IDs.
    private static readonly StringComparer ModIdComparer = StringComparer.OrdinalIgnoreCase;

    // (Coolnether123) Holds the resulting dependency graph.
    private class GraphBuilderResult
    {
        public readonly Dictionary<string, HashSet<Edge>> Adj = new Dictionary<string, HashSet<Edge>>(ModIdComparer);
        public readonly Dictionary<string, int> Indeg = new Dictionary<string, int>(ModIdComparer);
        public readonly List<string> Errors = new List<string>();
    }

    /**
     * (Coolnether123) Main entry point for resolving mod load order.
     */
    public static ResolutionResult Resolve(List<ModEntry> discovered, IEnumerable<string> userOrder)
    {
        Dictionary<string, ModEntry> byId;
        List<string> ids;
        Dictionary<string, int> priority;
        InitializeModData(discovered, userOrder, out byId, out ids, out priority);

        var graphResult = BuildDependencyGraph(discovered, byId);

        var sortResult = PerformTopologicalSort(ids, graphResult.Adj, graphResult.Indeg, priority);

        var resultMods = new List<ModEntry>(sortResult.SortedIds.Count);
        foreach (var id in sortResult.SortedIds)
            if (byId.ContainsKey(id)) resultMods.Add(byId[id]);

        return new ResolutionResult(resultMods, graphResult.Errors, sortResult.CycledIds);
    }

    /**
     * (Coolnether123) Extracts a unique ID for a given mod.
     */
    private static string GetModId(ModEntry m)
    {
        // (Coolnether123) ID fallback order: About.id -> ModEntry.Id -> Name -> RootPath.
        var id = (m.About != null && !string.IsNullOrEmpty(m.About.id)) ? m.About.id.Trim().ToLowerInvariant() : (m.Id ?? m.Name ?? m.RootPath);
        // (Coolnether123) Generate GUID if no ID exists.
        if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
        return id;
    }

    /**
     * (Coolnether123) Prepares data structures for sorting, mapping mods by ID and priority.
     */
    private static void InitializeModData(List<ModEntry> discovered, IEnumerable<string> userOrder, out Dictionary<string, ModEntry> byId, out List<string> ids, out Dictionary<string, int> priority)
    {
        byId = new Dictionary<string, ModEntry>(ModIdComparer);
        ids = new List<string>();
        foreach (var m in discovered)
        {
            var id = GetModId(m);
            if (!byId.ContainsKey(id))
            {
                byId[id] = m;
                ids.Add(id);
            }
            else
            {
                // (Coolnether123) Warn on duplicate mod ID; keep first entry.
                var existing = byId[id];
                MMLog.Write($"Warning: Duplicate mod ID '{id}' detected. Keeping '{existing.RootPath}', ignoring '{m.RootPath}'.");
            }
        }

        // (Coolnether123) Assign priority from user order.
        priority = new Dictionary<string, int>(ModIdComparer);
        int p = 0;
        foreach (var id in userOrder)
            if (!priority.ContainsKey(id)) priority[id] = p++;
        
        // (Coolnether123) Alphabetically sort remaining mods for stable priority.
        var remainingIds = new List<string>();
        foreach (var id in ids)
            if (!priority.ContainsKey(id)) remainingIds.Add(id);
        remainingIds.Sort(StringComparer.OrdinalIgnoreCase);

        // (Coolnether123) Assign priority to remaining mods.
        foreach (var id in remainingIds)
            if (!priority.ContainsKey(id)) priority[id] = p++;
    }

    /**
     * (Coolnether123) Constructs the dependency graph from all discovered mods.
     */
    private static GraphBuilderResult BuildDependencyGraph(List<ModEntry> discovered, Dictionary<string, ModEntry> byId)
    {
        var result = new GraphBuilderResult();
        var adj = result.Adj;
        var indeg = result.Indeg;
        var ids = byId.Keys;

        foreach (var id in ids)
        {
            adj[id] = new HashSet<Edge>();
            indeg[id] = 0;
        }

        // (Coolnether123) Helper to add a dependency edge.
        Action<string, string, bool> addEdge = (from, to, isHard) =>
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || ModIdComparer.Equals(from, to)) return;
            if (!adj.ContainsKey(from) || !adj.ContainsKey(to)) return;

            if (adj[from].Add(new Edge(to, isHard))) indeg[to]++;
        };

        foreach (var m in discovered)
        {
            var mid = GetModId(m);
            if (string.IsNullOrEmpty(mid) || !adj.ContainsKey(mid)) continue;

            var man = m.About;
            if (man == null) continue;

            // (Coolnether123) Hard dependencies: 'dependsOn'.
            if (man.dependsOn != null)
            {
                foreach (var dep in man.dependsOn)
                {
                    var constraint = ParseConstraint(dep);
                    if (constraint == null) continue;

                    if (!byId.ContainsKey(constraint.ID))
                    {
                        result.Errors.Add($"Mod '{mid}' has a missing hard dependency: '{constraint.ID}'.");
                        continue;
                    }

                    var depMod = byId[constraint.ID];
                    if (constraint.IsSatisfiedBy(depMod.About?.version))
                    {
                        addEdge(constraint.ID, mid, true);
                    }
                    else
                    {
                        result.Errors.Add($"Mod '{mid}' requires dependency '{constraint.ID}' version {constraint.Operator}{constraint.Version}, but found version {depMod.About?.version ?? "none"}.");
                    }
                }
            }
            // (Coolnether123) Soft dependencies: 'loadBefore'.
            if (man.loadBefore != null)
            {
                foreach (var b in man.loadBefore)
                {
                    var constraint = ParseConstraint(b);
                    if (constraint == null || !byId.ContainsKey(constraint.ID)) continue;
                    
                    var depMod = byId[constraint.ID];
                    if (constraint.IsSatisfiedBy(depMod.About?.version))
                    {
                        addEdge(mid, constraint.ID, false);
                    }
                }
            }
            // (Coolnether123) Soft dependencies: 'loadAfter'.
            if (man.loadAfter != null)
            {
                foreach (var a in man.loadAfter)
                {
                    var constraint = ParseConstraint(a);
                    if (constraint == null || !byId.ContainsKey(constraint.ID)) continue;

                    var depMod = byId[constraint.ID];
                    if (constraint.IsSatisfiedBy(depMod.About?.version))
                    {
                        addEdge(constraint.ID, mid, false);
                    }
                }
            }
        }
        return result;
    }

    /**
     * (Coolnether123) Parses a dependency string like "id >= 1.2.3" into a ModConstraint.
     */
    private static ModConstraint ParseConstraint(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;

        raw = raw.Trim();
        // (Coolnether123) Regex for 'id [op] [version]' format.
        var match = Regex.Match(raw, @"^\s*([a-zA-Z0-9_.-]+)\s*(>=|<=|>|<|==|!=)?\s*([0-9.]+)?\s*$");

        if (!match.Success)
        {
            // (Coolnether123) Fallback for simple ID-only constraints.
            return new ModConstraint(NormId(raw));
        }

        var id = NormId(match.Groups[1].Value);
        var op = match.Groups[2].Value;
        var versionStr = match.Groups[3].Value;

        Version version = null;
        if (!string.IsNullOrEmpty(versionStr))
        {
            try 
            { 
                version = new Version(versionStr); 
            }
            catch (Exception e) {
                MMLog.Write($"Warning: Could not parse version '{versionStr}' in dependency string '{raw}'. Ignoring version constraint. Error: {e.Message}");
            }
        }
        
        // (Coolnether123) Warn on ambiguous constraints (e.g. 'id >=').
        if (!string.IsNullOrEmpty(op) && version == null || string.IsNullOrEmpty(op) && version != null) {
            MMLog.Write($"Warning: Ambiguous version constraint in '{raw}'. Treating as simple dependency on '{id}'.");
            return new ModConstraint(id);
        }
        
        return new ModConstraint(id, version, op);
    }

    // (Coolnether123) Holds the result of a topological sort.
    private class SortResult
    {
        public readonly List<string> SortedIds;
        public readonly HashSet<string> CycledIds;
        public SortResult(List<string> sorted, HashSet<string> cycled) { SortedIds = sorted; CycledIds = cycled; }
    }

    /**
     * (Coolnether123) Performs a topological sort on the dependency graph.
     */
    private static SortResult PerformTopologicalSort(List<string> ids, Dictionary<string, HashSet<Edge>> adj, Dictionary<string, int> indeg, Dictionary<string, int> priority)
    {
        // (Coolnether123) Priority queue for nodes with no incoming dependencies.
        var heap = new List<string>();
        var priorityComparer = new PriorityComparer(priority, ModIdComparer);
        foreach (var kv in indeg)
            if (kv.Value == 0) heap.Add(kv.Key);
        heap.Sort(priorityComparer);

        var resultIds = new List<string>();
        var idx = 0;
        while (heap.Count > 0)
        {
            var u = heap[0];
            heap.RemoveAt(0);
            resultIds.Add(u);
            foreach (var edge in adj[u].OrderBy(e => e.To, ModIdComparer)) // (Coolnether123) OrderBy helps keep sort stable
            {
                indeg[edge.To]--;
                if (indeg[edge.To] == 0)
                {
                    // (Coolnether123) Insert into sorted heap.
                    int pos = heap.BinarySearch(edge.To, priorityComparer);
                    if (pos < 0) pos = ~pos;
                    heap.Insert(pos, edge.To);
                }
            }
            idx++;
            // (Coolnether123) Safety break against infinite loops.
            if (idx > 10000) break; 
        }

        var cycled = new HashSet<string>(ModIdComparer);
        // (Coolnether123) Cycle detected: attempt to resolve.
        if (resultIds.Count < ids.Count)
        {
            var remainingNodes = new HashSet<string>(ModIdComparer);
            foreach (var id in ids) if (!resultIds.Contains(id)) remainingNodes.Add(id);

            // (Coolnether123) Cycle resolution: break soft dependencies first.
            var tempIndeg = new Dictionary<string, int>(ModIdComparer);
            foreach (var id in remainingNodes) tempIndeg[id] = 0;

            foreach (var u in remainingNodes)
            {
                foreach (var edge in adj[u])
                {
                    if (edge.IsHard && remainingNodes.Contains(edge.To))
                    {
                        tempIndeg[edge.To]++;
                    }
                }
            }

            var tempHeap = new List<string>();
            foreach (var kv in tempIndeg) if (kv.Value == 0) tempHeap.Add(kv.Key);
            tempHeap.Sort(priorityComparer);

            var resolvedFromCycle = new List<string>();
            while (tempHeap.Count > 0)
            {
                var u = tempHeap[0];
                tempHeap.RemoveAt(0);
                resolvedFromCycle.Add(u);
                foreach (var edge in adj[u])
                {
                    if (edge.IsHard && remainingNodes.Contains(edge.To))
                    {
                        tempIndeg[edge.To]--;
                        if (tempIndeg[edge.To] == 0)
                        {
                            int pos = tempHeap.BinarySearch(edge.To, priorityComparer);
                            if (pos < 0) pos = ~pos;
                            tempHeap.Insert(pos, edge.To);
                        }
                    }
                }
            }
            resultIds.AddRange(resolvedFromCycle);

            // (Coolnether123) Unresolvable hard cycle: add remaining nodes by priority.
            if (resolvedFromCycle.Count < remainingNodes.Count)
            {
                MMLog.Write("Unresolvable load order cycle detected.");
                foreach (var id in remainingNodes)
                {
                    if (!resolvedFromCycle.Contains(id)) cycled.Add(id);
                }
                var trulyUnresolvable = new List<string>(cycled);
                trulyUnresolvable.Sort(priorityComparer);
                resultIds.AddRange(trulyUnresolvable);
            }
        }

        return new SortResult(resultIds, cycled);
    }

    /**
     * (Coolnether123) Reads the user-defined load order from 'loadorder.json'.
     */
    public static ProcessedLoadOrderData ReadLoadOrderFile(string modsRoot)
    {
        var processedData = new ProcessedLoadOrderData();
        var lof = new LoadOrderFile { order = new string[0], mods = new ModStatusEntryKV[0] };
        try
        {
            var path = Path.Combine(modsRoot, "loadorder.json");
            if (!File.Exists(path)) return processedData;

            var text = File.ReadAllText(path);
            lof = JsonUtility.FromJson<LoadOrderFile>(text) ?? lof;

            processedData.Order = (lof.order ?? new string[0])
                .Select(s => IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            if (lof.mods != null)
            {
                foreach (var kvp in lof.mods)
                {
                    if (!IsNullOrWhiteSpace(kvp.id) && kvp != null)
                    {
                        processedData.Mods[NormId(kvp.id)] = new ModStatusEntry { enabled = kvp.enabled, locked = kvp.locked, notes = kvp.notes };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MMLog.Write("Failed to read loadorder.json: " + ex.Message);
        }
        return processedData;
    }

    private static string NormId(string s) => (s ?? "").Trim().ToLowerInvariant();
}

/**
 * (Coolnether123) Sorts strings based on a priority dictionary.
 */
internal class PriorityComparer : IComparer<string>
{
    private readonly Dictionary<string, int> _prio;
    private readonly StringComparer _comparer;

    public PriorityComparer(Dictionary<string, int> prio, StringComparer comparer)
    {
        _prio = prio;
        _comparer = comparer;
    }

    public int Compare(string x, string y)
    {
        int px = _prio.ContainsKey(x) ? _prio[x] : int.MaxValue;
        int py = _prio.ContainsKey(y) ? _prio[y] : int.MaxValue;
        int c = px.CompareTo(py);
        if (c != 0) return c;
        // (Coolnether123) Tie-break with alphabetical sort.
        return _comparer.Compare(x, y);
    }
}