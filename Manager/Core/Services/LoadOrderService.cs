using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Delegate for order changed events
    /// </summary>
    public delegate void OrderChangedHandler(string[] order);

    /// <summary>
    /// Manages mod load order - reading, writing, and validating order.
    /// Single responsibility: Load order management only.
    /// </summary>
    public class LoadOrderService
    {
        private const string ORDER_FILENAME = "loadorder.json";

        /// <summary>
        /// Event raised when load order changes
        /// </summary>
        public event OrderChangedHandler OrderChanged;

        /// <summary>
        /// Read the current load order from file
        /// </summary>
        public string[] ReadOrder(string modsPath)
        {
            try
            {
                string orderPath = Path.Combine(modsPath, ORDER_FILENAME);
                if (!File.Exists(orderPath))
                    return new string[0];

                string json = File.ReadAllText(orderPath);
                var serializer = new JavaScriptSerializer();
                var data = serializer.Deserialize<LoadOrderData>(json);

                if (data != null && data.order != null)
                    return data.order;
                return new string[0];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error reading load order: " + ex.Message);
                return new string[0];
            }
        }

        /// <summary>
        /// Save the load order to file
        /// </summary>
        public void SaveOrder(string modsPath, IEnumerable<string> modIds)
        {
            try
            {
                string orderPath = Path.Combine(modsPath, ORDER_FILENAME);
                
                var orderList = new List<string>(modIds);
                var data = new LoadOrderData();
                data.order = orderList.ToArray();
                
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(data);
                
                File.WriteAllText(orderPath, json);
                
                if (OrderChanged != null)
                    OrderChanged(data.order);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error saving load order: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Get enabled mods (those in load order) from all discovered mods
        /// </summary>
        public List<ModItem> GetEnabledMods(IEnumerable<ModItem> allMods, string modsPath)
        {
            var order = ReadOrder(modsPath);
            var orderSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in order)
                orderSet.Add(id);
            
            var enabled = new List<ModItem>();
            foreach (var m in allMods)
            {
                if (orderSet.Contains(m.Id))
                    enabled.Add(m);
            }
            
            // Sort by order position
            var orderIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < order.Length; i++)
            {
                orderIndex[order[i]] = i;
            }

            enabled.Sort(delegate(ModItem a, ModItem b)
            {
                int posA = orderIndex.ContainsKey(a.Id) ? orderIndex[a.Id] : int.MaxValue;
                int posB = orderIndex.ContainsKey(b.Id) ? orderIndex[b.Id] : int.MaxValue;
                int cmp = posA.CompareTo(posB);
                if (cmp != 0) return cmp;
                return CompareByNameThenIdentity(a, b);
            });

            foreach (var mod in enabled)
                mod.IsEnabled = true;

            return enabled;
        }

        /// <summary>
        /// Get disabled mods (those NOT in load order)
        /// </summary>
        public List<ModItem> GetDisabledMods(IEnumerable<ModItem> allMods, string modsPath)
        {
            var order = ReadOrder(modsPath);
            var orderSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in order)
                orderSet.Add(id);
            
            var disabled = new List<ModItem>();
            foreach (var m in allMods)
            {
                if (!orderSet.Contains(m.Id))
                {
                    m.IsEnabled = false;
                    disabled.Add(m);
                }
            }

            disabled.Sort(CompareByNameThenIdentity);

            return disabled;
        }

        /// <summary>
        /// Name order with ID and folder as tie-breaks. List.Sort is not stable, so
        /// without them two mods with the same name could swap between runs.
        /// </summary>
        private static int CompareByNameThenIdentity(ModItem a, ModItem b)
        {
            int cmp = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;
            cmp = string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;
            return string.Compare(a.RootPath, b.RootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Enable a mod by adding it to the load order
        /// </summary>
        public void EnableMod(string modsPath, string modId)
        {
            var order = new List<string>(ReadOrder(modsPath));
            
            bool found = false;
            foreach (var id in order)
            {
                if (string.Equals(id, modId, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                order.Add(modId);
                SaveOrder(modsPath, order);
            }
        }

        /// <summary>
        /// Disable a mod by removing it from the load order
        /// </summary>
        public void DisableMod(string modsPath, string modId)
        {
            var order = new List<string>(ReadOrder(modsPath));
            
            for (int i = order.Count - 1; i >= 0; i--)
            {
                if (string.Equals(order[i], modId, StringComparison.OrdinalIgnoreCase))
                {
                    order.RemoveAt(i);
                }
            }
            
            SaveOrder(modsPath, order);
        }

        /// <summary>
        /// Move a mod up in the load order
        /// </summary>
        public void MoveUp(string modsPath, string modId)
        {
            var order = new List<string>(ReadOrder(modsPath));
            
            int index = -1;
            for (int i = 0; i < order.Count; i++)
            {
                if (string.Equals(order[i], modId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            
            if (index > 0)
            {
                var temp = order[index - 1];
                order[index - 1] = order[index];
                order[index] = temp;
                SaveOrder(modsPath, order);
            }
        }

        /// <summary>
        /// Move a mod down in the load order
        /// </summary>
        public void MoveDown(string modsPath, string modId)
        {
            var order = new List<string>(ReadOrder(modsPath));
            
            int index = -1;
            for (int i = 0; i < order.Count; i++)
            {
                if (string.Equals(order[i], modId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            
            if (index >= 0 && index < order.Count - 1)
            {
                var temp = order[index + 1];
                order[index + 1] = order[index];
                order[index] = temp;
                SaveOrder(modsPath, order);
            }
        }

        /// <summary>
        /// Validate load order and return issues
        /// </summary>
        public LoadOrderValidation ValidateOrder(IEnumerable<ModItem> enabledMods, string modsPath, bool skipHarmony)
        {
            var validation = new LoadOrderValidation();
            var order = ReadOrder(modsPath);
            
            try
            {
                var modInfos = new List<ModTypes.ModInfo>();
                foreach (var m in enabledMods)
                {
                    var info = new ModTypes.ModInfo();
                    info.Id = m.Id;
                    info.Name = m.DisplayName;
                    info.RootPath = m.RootPath;
                    
                    var about = new ModTypes.ModAboutInfo();
                    about.id = m.Id;
                    about.name = m.DisplayName;
                    about.version = m.Version;
                    about.dependsOn = m.DependsOn;
                    about.loadAfter = m.LoadAfter;
                    about.loadBefore = m.LoadBefore;
                    info.About = about;
                    
                    modInfos.Add(info);
                }

                var evalResult = LoadOrderResolver.Evaluate(modInfos, order);
                
                if (evalResult.HardIssues != null)
                {
                    foreach (var id in evalResult.HardIssues)
                        validation.HardIssueModIds.Add(id);
                }
                
                if (evalResult.SoftIssues != null)
                {
                    foreach (var id in evalResult.SoftIssues)
                        validation.SoftIssueModIds.Add(id);
                }

                // Process missing dependencies
                if (evalResult.MissingHardDependencies != null)
                {
                    foreach (var errorMsg in evalResult.MissingHardDependencies)
                    {
                        string dependencyId = ExtractDependencyId(errorMsg);
                        if (IsBundledDependency(dependencyId))
                            continue;
                        if (skipHarmony && errorMsg.ToLowerInvariant().Contains("harmony"))
                            continue;

                        var match = System.Text.RegularExpressions.Regex.Match(errorMsg, @"^Mod '([^']*)'");
                        if (match.Success)
                        {
                            validation.HardIssueModIds.Add(match.Groups[1].Value);
                            validation.MissingDependencies.Add(errorMsg);
                            var problem = new DependencyProblem();
                            problem.ModId = match.Groups[1].Value;
                            problem.DependencyId = dependencyId;
                            problem.NeedsNewerVersion = errorMsg.IndexOf(" requires dependency ", StringComparison.Ordinal) >= 0;
                            validation.DependencyProblems.Add(problem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error validating order: " + ex.Message);
            }

            return validation;
        }

        /// <summary>
        /// Returns the active mods (given in their current load order) in an
        /// order that meets every dependsOn / loadAfter / loadBefore rule
        /// between them, keeping the current order wherever the rules allow.
        /// A mod that others need takes the earliest position of the mods that
        /// need it, so a fix moves the dependency up instead of pushing
        /// unrelated mods around. A dependency loop is broken at its earliest
        /// mod. The result depends only on the input order and the rules, so
        /// the same setup always gives the same order, and fixing a fixed
        /// order changes nothing.
        /// </summary>
        public static List<string> FixOrder(IList<ModItem> active)
        {
            List<string> ids = new List<string>();
            Dictionary<string, int> position = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ModItem mod in active ?? new ModItem[0])
            {
                if (mod == null || string.IsNullOrEmpty(mod.Id) || position.ContainsKey(mod.Id))
                    continue;
                position[mod.Id] = ids.Count;
                ids.Add(mod.Id);
            }

            // before[a] holds every mod that must load after a.
            Dictionary<string, List<string>> before = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> waiting = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in ids)
            {
                before[id] = new List<string>();
                waiting[id] = 0;
            }
            foreach (ModItem mod in active ?? new ModItem[0])
            {
                if (mod == null || string.IsNullOrEmpty(mod.Id))
                    continue;
                foreach (string rule in mod.DependsOn ?? new string[0])
                    AddOrderRule(before, waiting, RuleId(rule), mod.Id);
                foreach (string rule in mod.LoadAfter ?? new string[0])
                    AddOrderRule(before, waiting, RuleId(rule), mod.Id);
                foreach (string rule in mod.LoadBefore ?? new string[0])
                    AddOrderRule(before, waiting, mod.Id, RuleId(rule));
            }

            // A mod inherits the earliest position of anything that must load after it.
            Dictionary<string, int> rank = new Dictionary<string, int>(position, StringComparer.OrdinalIgnoreCase);
            for (int pass = 0; pass < ids.Count; pass++)
            {
                bool changed = false;
                foreach (string id in ids)
                {
                    foreach (string later in before[id])
                    {
                        if (rank[later] < rank[id])
                        {
                            rank[id] = rank[later];
                            changed = true;
                        }
                    }
                }
                if (!changed)
                    break;
            }

            Comparison<string> earliest = delegate(string a, string b)
            {
                int byRank = rank[a].CompareTo(rank[b]);
                return byRank != 0 ? byRank : position[a].CompareTo(position[b]);
            };
            List<string> result = new List<string>(ids.Count);
            HashSet<string> placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> ready = new List<string>();
            foreach (string id in ids)
                if (waiting[id] == 0)
                    ready.Add(id);
            while (result.Count < ids.Count)
            {
                if (ready.Count == 0)
                {
                    // Only a loop is left: treat its earliest mod as ready.
                    string forced = null;
                    foreach (string id in ids)
                        if (!placed.Contains(id) && (forced == null || earliest(id, forced) < 0))
                            forced = id;
                    waiting[forced] = 0;
                    ready.Add(forced);
                }
                ready.Sort(earliest);
                string next = ready[0];
                ready.RemoveAt(0);
                result.Add(next);
                placed.Add(next);
                foreach (string later in before[next])
                {
                    if (placed.Contains(later))
                        continue;
                    waiting[later]--;
                    if (waiting[later] == 0)
                        ready.Add(later);
                }
            }
            return result;
        }

        private static void AddOrderRule(Dictionary<string, List<string>> before, Dictionary<string, int> waiting, string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second) ||
                string.Equals(first, second, StringComparison.OrdinalIgnoreCase) ||
                !before.ContainsKey(first) || !before.ContainsKey(second))
                return;
            foreach (string existing in before[first])
                if (string.Equals(existing, second, StringComparison.OrdinalIgnoreCase))
                    return;
            before[first].Add(second);
            waiting[second]++;
        }

        /// <summary>The mod ID at the start of a rule such as "coolnether123.lifespan >= 1.2".</summary>
        private static string RuleId(string rule)
        {
            var match = System.Text.RegularExpressions.Regex.Match(rule ?? string.Empty, @"^\s*([A-Za-z0-9_.-]+)");
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        // SMM ships Harmony (SMM\bin\0Harmony.dll) and shares it with every mod,
        // so a declared dependency on the Harmony library is always satisfied.
        private static readonly HashSet<string> BundledDependencyIds = new HashSet<string>(
            new string[]
            {
                "harmony", "0harmony", "harmonylib", "lib.harmony", "com.harmony",
                "com.harmony.0harmony", "brrainz.harmony", "pardeike.harmony"
            },
            StringComparer.OrdinalIgnoreCase);

        /// <summary>True for dependencies SMM always provides, such as the Harmony library.</summary>
        public static bool IsBundledDependency(string dependencyId)
        {
            return !string.IsNullOrEmpty(dependencyId) && BundledDependencyIds.Contains(dependencyId.Trim());
        }

        private static string ExtractDependencyId(string message)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                message ?? string.Empty,
                @"(?:missing hard dependency: '|requires dependency ')([^']+)'");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // Internal JSON model
        private class LoadOrderData
        {
            public string[] order { get; set; }
        }
    }

    /// <summary>
    /// One mod's unmet dependency: missing, turned off, or too old.
    /// </summary>
    public class DependencyProblem
    {
        public string ModId { get; set; }
        public string DependencyId { get; set; }
        public bool NeedsNewerVersion { get; set; }
    }

    /// <summary>
    /// Results of load order validation
    /// </summary>
    public class LoadOrderValidation
    {
        private HashSet<string> _hardIssueModIds;
        private HashSet<string> _softIssueModIds;
        private List<string> _missingDependencies;

        public LoadOrderValidation()
        {
            _hardIssueModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _softIssueModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _missingDependencies = new List<string>();
        }

        public HashSet<string> HardIssueModIds 
        { 
            get { return _hardIssueModIds; } 
            set { _hardIssueModIds = value; } 
        }
        
        public HashSet<string> SoftIssueModIds 
        { 
            get { return _softIssueModIds; } 
            set { _softIssueModIds = value; } 
        }
        
        public List<string> MissingDependencies
        {
            get { return _missingDependencies; }
            set { _missingDependencies = value; }
        }

        private readonly List<DependencyProblem> _dependencyProblems = new List<DependencyProblem>();

        /// <summary>Each unmet dependency, for offering a fix.</summary>
        public List<DependencyProblem> DependencyProblems
        {
            get { return _dependencyProblems; }
        }

        public bool HasIssues 
        { 
            get { return _hardIssueModIds.Count > 0 || _softIssueModIds.Count > 0; }
        }
    }
}
