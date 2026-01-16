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
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
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

            disabled.Sort(delegate(ModItem a, ModItem b)
            {
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            return disabled;
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
                        if (skipHarmony && errorMsg.ToLowerInvariant().Contains("harmony"))
                            continue;

                        var match = System.Text.RegularExpressions.Regex.Match(errorMsg, @"^Mod '([^']*)' has a missing hard dependency:");
                        if (match.Success)
                        {
                            validation.HardIssueModIds.Add(match.Groups[1].Value);
                            validation.MissingDependencies.Add(errorMsg);
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

        // Internal JSON model
        private class LoadOrderData
        {
            public string[] order { get; set; }
        }
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
        
        public bool HasIssues 
        { 
            get { return _hardIssueModIds.Count > 0 || _softIssueModIds.Count > 0; } 
        }
    }
}
