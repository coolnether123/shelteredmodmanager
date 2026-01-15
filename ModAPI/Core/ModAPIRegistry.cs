using System;
using System.Collections.Generic;

namespace ModAPI.Core
{
    /// <summary>
    /// Service discovery registry allowing mods to publish and consume shared APIs.
    /// 
    /// Example:
    ///   Mod A publishes an API:
    ///     ModAPIRegistry.RegisterAPI("com.modA.CraftingAPI", new MyCraftingAPI());
    ///   
    ///   Mod B consumes it:
    ///     var api = ModAPIRegistry.GetAPI&lt;IMyCraftingAPI&gt;("com.modA.CraftingAPI");
    ///     if (api != null) api.RegisterRecipe(myRecipe);
    /// 
    /// Best Practice: 
    ///   - Use interfaces for API contracts
    ///   - Use reverse-domain naming for API names
    ///   - Document your API in your mod's About.json
    /// </summary>
    public static class ModAPIRegistry
    {
        private static readonly Dictionary<string, object> _apis 
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        
        private static readonly Dictionary<string, string> _apiProviders 
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        private static readonly object _lock = new object();
        
        /// <summary>
        /// Register an API implementation that other mods can consume.
        /// </summary>
        /// <typeparam name="T">API interface or class type</typeparam>
        /// <param name="apiName">Unique API name (use reverse-domain notation)</param>
        /// <param name="implementation">API implementation instance</param>
        /// <param name="providerModId">Optional mod ID that provides this API</param>
        /// <returns>True if registered successfully, false if API name already exists</returns>
        public static bool RegisterAPI<T>(string apiName, T implementation, string providerModId = null) where T : class
        {
            if (string.IsNullOrEmpty(apiName))
            {
                MMLog.WriteWarning("[ModAPIRegistry] Cannot register API with null/empty name");
                return false;
            }
            
            if (implementation == null)
            {
                MMLog.WriteWarning($"[ModAPIRegistry] Cannot register null implementation for API: {apiName}");
                return false;
            }
            
            lock (_lock)
            {
                if (_apis.ContainsKey(apiName))
                {
                    MMLog.WriteWarning($"[ModAPIRegistry] API already registered: {apiName}. " +
                              $"Provider: {_apiProviders.GetValueOrDefault(apiName, "unknown")}");
                    return false;
                }
                
                _apis[apiName] = implementation;
                _apiProviders[apiName] = providerModId ?? "unknown";
                
                MMLog.WriteInfo($"[ModAPIRegistry] Registered API: {apiName} (Provider: {_apiProviders[apiName]}, Type: {typeof(T).Name})");
                return true;
            }
        }
        
        /// <summary>
        /// Get a registered API implementation.
        /// </summary>
        /// <typeparam name="T">API interface or class type</typeparam>
        /// <param name="apiName">API name to retrieve</param>
        /// <returns>API implementation or null if not found or type mismatch</returns>
        public static T GetAPI<T>(string apiName) where T : class
        {
            if (string.IsNullOrEmpty(apiName))
                return null;
            
            object api;
            lock (_lock)
            {
                if (!_apis.TryGetValue(apiName, out api) || api == null)
                {
                    MMLog.WriteDebug($"[ModAPIRegistry] API not found: {apiName}");
                    return null;
                }
            }
            
            T typedApi = api as T;
            if (typedApi == null)
            {
                MMLog.WriteWarning($"[ModAPIRegistry] Type mismatch for API {apiName}. " +
                          $"Expected {typeof(T).Name}, got {api.GetType().Name}");
            }
            else
            {
                MMLog.WriteDebug($"[ModAPIRegistry] Retrieved API: {apiName} as {typeof(T).Name}");
            }
            
            return typedApi;
        }
        
        /// <summary>
        /// Try to get a registered API implementation.
        /// </summary>
        /// <typeparam name="T">API interface or class type</typeparam>
        /// <param name="apiName">API name to retrieve</param>
        /// <param name="api">Output API implementation</param>
        /// <returns>True if API was found and type matches</returns>
        public static bool TryGetAPI<T>(string apiName, out T api) where T : class
        {
            api = GetAPI<T>(apiName);
            return api != null;
        }
        
        /// <summary>
        /// Check if an API is registered.
        /// </summary>
        /// <param name="apiName">API name to check</param>
        /// <returns>True if the API is registered</returns>
        public static bool IsAPIRegistered(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
                return false;
            
            lock (_lock)
            {
                return _apis.ContainsKey(apiName);
            }
        }
        
        /// <summary>
        /// Unregister an API. Only the provider mod should call this.
        /// </summary>
        /// <param name="apiName">API name to unregister</param>
        /// <param name="providerModId">Optional verification - only unregister if provider matches</param>
        /// <returns>True if unregistered successfully</returns>
        public static bool UnregisterAPI(string apiName, string providerModId = null)
        {
            if (string.IsNullOrEmpty(apiName))
                return false;
            
            lock (_lock)
            {
                if (!_apis.ContainsKey(apiName))
                    return false;
                
                // Verify provider if specified
                if (!string.IsNullOrEmpty(providerModId))
                {
                    string registeredProvider = _apiProviders.GetValueOrDefault(apiName, null);
                    if (registeredProvider != providerModId)
                    {
                        MMLog.WriteWarning($"[ModAPIRegistry] Cannot unregister API {apiName}. " +
                                  $"Provider mismatch: requested={providerModId}, registered={registeredProvider}");
                        return false;
                    }
                }
                
                _apis.Remove(apiName);
                _apiProviders.Remove(apiName);
                
                MMLog.WriteInfo($"[ModAPIRegistry] Unregistered API: {apiName}");
                return true;
            }
        }
        
        /// <summary>
        /// Get all registered API names.
        /// </summary>
        /// <returns>List of registered API names</returns>
        public static List<string> GetRegisteredAPIs()
        {
            lock (_lock)
            {
                return new List<string>(_apis.Keys);
            }
        }
        
        /// <summary>
        /// Get diagnostic information about registered APIs.
        /// </summary>
        /// <returns>Dictionary of API names to APIInfo objects</returns>
        public static Dictionary<string, APIInfo> GetAPIDiagnostics()
        {
            var diagnostics = new Dictionary<string, APIInfo>();
            
            lock (_lock)
            {
                foreach (var kvp in _apis)
                {
                    string provider = _apiProviders.GetValueOrDefault(kvp.Key, "unknown");
                    string type = kvp.Value?.GetType().Name ?? "null";
                    diagnostics[kvp.Key] = new APIInfo(provider, type);
                }
            }
            
            return diagnostics;
        }
        
        /// <summary>
        /// Diagnostic information about a registered API.
        /// </summary>
        public class APIInfo
        {
            public string Provider { get; private set; }
            public string Type { get; private set; }
            
            public APIInfo(string provider, string type)
            {
                Provider = provider;
                Type = type;
            }
        }
        
        /// <summary>
        /// Clear all registered APIs.
        /// Only use during shutdown or testing.
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                int count = _apis.Count;
                _apis.Clear();
                _apiProviders.Clear();
                MMLog.WriteDebug($"[ModAPIRegistry] Cleared all {count} registered APIs");
            }
        }
    }
    
    /// <summary>
    /// Extension methods for Dictionary to provide GetValueOrDefault
    /// (in case this project targets .NET Framework without it)
    /// </summary>
    internal static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary, 
            TKey key, 
            TValue defaultValue = default(TValue))
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }
    }
}
