using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/**
 * Resolves final mod load order using user order plus about constraints.
 * Constraints considered: dependsOn (hard), loadBefore (soft), loadAfter (soft).
 * Author: Coolnether123
 */
public static class LoadOrderResolver
{
    [Serializable]
    private class LoadOrderFile { public string[] order; }

    public static List<ModEntry> Resolve(List<ModEntry> discovered, string modsRoot)
    {
        var byId = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
        var ids = new List<string>();
        foreach (var m in discovered)
        {
            var id = (m.About != null && !string.IsNullOrEmpty(m.About.id)) ? m.About.id.Trim().ToLowerInvariant() : (m.Id ?? m.Name ?? m.RootPath);
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (!byId.ContainsKey(id))
            {
                byId[id] = m;
                ids.Add(id);
            }
        }

        var userOrder = ReadUserOrder(modsRoot);
        var priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int p = 0;
        foreach (var id in userOrder)
            if (!priority.ContainsKey(id)) priority[id] = p++;
        foreach (var id in ids)
            if (!priority.ContainsKey(id)) priority[id] = p++;

        // Build graph
        var adj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var indeg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            adj[id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            indeg[id] = 0;
        }

        Action<string, string, string> addEdge = (a, b, kind) =>
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a.Equals(b, StringComparison.OrdinalIgnoreCase)) return;
            if (!adj.ContainsKey(a) || !adj.ContainsKey(b)) return;
            if (adj[a].Add(b)) indeg[b]++;
        };

        foreach (var m in discovered)
        {
            var mid = (m.About != null && !string.IsNullOrEmpty(m.About.id)) ? m.About.id.Trim().ToLowerInvariant() : (m.Id ?? m.Name ?? m.RootPath);
            if (string.IsNullOrEmpty(mid) || !adj.ContainsKey(mid)) continue;

            var man = m.About;
            if (man == null) continue;
            if (man.dependsOn != null)
            {
                foreach (var dep in man.dependsOn)
                {
                    var depId = ParseId(dep);
                    if (!string.IsNullOrEmpty(depId)) addEdge(depId, mid, "dependsOn");
                }
            }
            if (man.loadBefore != null)
            {
                foreach (var b in man.loadBefore)
                {
                    var bid = ParseId(b);
                    addEdge(mid, bid, "loadBefore");
                }
            }
            if (man.loadAfter != null)
            {
                foreach (var a in man.loadAfter)
                {
                    var aid = ParseId(a);
                    addEdge(aid, mid, "loadAfter");
                }
            }
        }

        // Kahn's algorithm with user priority
        var heap = new List<string>();
        foreach (var kv in indeg)
            if (kv.Value == 0) heap.Add(kv.Key);
        heap.Sort(new PriorityComparer(priority));

        var resultIds = new List<string>();
        var idx = 0;
        while (heap.Count > 0)
        {
            var u = heap[0];
            heap.RemoveAt(0);
            resultIds.Add(u);
            foreach (var v in adj[u])
            {
                indeg[v]--;
                if (indeg[v] == 0)
                {
                    // Insert keeping priority order (Coolnether123)
                    int pos = heap.BinarySearch(v, new PriorityComparer(priority));
                    if (pos < 0) pos = ~pos;
                    heap.Insert(pos, v);
                }
            }
            idx++;
            if (idx > 10000) break; // safety
        }

        // If cycles remain, append remaining by priority and log
        if (resultIds.Count < ids.Count)
        {
            MMLog.Write("Load order contains cycles; applying best-effort order.");
            var remaining = new List<string>();
            foreach (var id in ids)
                if (!resultIds.Contains(id)) remaining.Add(id);
            remaining.Sort(new PriorityComparer(priority));
            resultIds.AddRange(remaining);
        }

        var result = new List<ModEntry>(resultIds.Count);
        foreach (var id in resultIds)
            if (byId.ContainsKey(id)) result.Add(byId[id]);
        return result;
    }

    public static List<string> ReadUserOrder(string modsRoot)
    {
        var list = new List<string>();
        try
        {
            var path = Path.Combine(modsRoot, "loadorder.json");
            if (!File.Exists(path)) return list;
            var text = File.ReadAllText(path);
            var obj = JsonUtility.FromJson<LoadOrderFile>(text);
            if (obj != null && obj.order != null)
            {
                foreach (var id in obj.order)
                {
                    if (!string.IsNullOrEmpty(id)) list.Add(id.Trim().ToLowerInvariant());
                }
            }
        }
        catch (Exception ex)
        {
            MMLog.Write("Failed to read loadorder.json: " + ex.Message);
        }
        return list;
    }

    private static string ParseId(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var s = raw.Trim().ToLowerInvariant();
        // Strip simple version suffixes like id>=1.2.3
        int cut = s.IndexOfAny(new char[] { '>', '<', '=', '!' });
        if (cut > 0) s = s.Substring(0, cut).Trim();
        return s;
    }
}

// Custom comparer for .NET 3.5 (no Comparer.Create) (Coolnether123)
internal class PriorityComparer : System.Collections.Generic.IComparer<string>
{
    private readonly System.Collections.Generic.Dictionary<string, int> _prio;
    public PriorityComparer(System.Collections.Generic.Dictionary<string, int> prio) { _prio = prio; }
    public int Compare(string x, string y)
    {
        int px = _prio.ContainsKey(x) ? _prio[x] : int.MaxValue;
        int py = _prio.ContainsKey(y) ? _prio[y] : int.MaxValue;
        int c = px.CompareTo(py);
        if (c != 0) return c;
        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
