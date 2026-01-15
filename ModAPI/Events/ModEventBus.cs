using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Events
{
    /// <summary>
    /// Lightweight pub/sub event bus for inter-mod communication.
    /// Allows mods to publish typed events that other mods can subscribe to.
    /// 
    /// Usage:
    ///   Publishing: ModEventBus.Publish("com.mymod.ItemDiscovered", itemData);
    ///   Subscribing: ModEventBus.Subscribe&lt;ItemData&gt;("com.mymod.ItemDiscovered", OnItemFound);
    /// 
    /// Best Practice: Use reverse-domain naming for event names:
    ///   "com.authorname.modname.EventName"
    /// </summary>
    public static class ModEventBus
    {
        private static readonly Dictionary<string, Delegate> _subscribers 
            = new Dictionary<string, Delegate>(StringComparer.OrdinalIgnoreCase);
        
        private static readonly object _lock = new object();
        
        /// <summary>
        /// Publish a typed event to all subscribers.
        /// </summary>
        /// <typeparam name="T">Event data type</typeparam>
        /// <param name="eventName">Event name (use reverse-domain notation)</param>
        /// <param name="data">Event data to pass to subscribers</param>
        public static void Publish<T>(string eventName, T data)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                MMLog.WriteWarning("[ModEventBus] Cannot publish event with null/empty name");
                return;
            }
            
            Delegate handler;
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(eventName, out handler) || handler == null)
                {
                    MMLog.WriteDebug($"[ModEventBus] No subscribers for event: {eventName}");
                    return;
                }
            }
            
            // Invoke outside lock to prevent deadlocks
            try
            {
                var typedHandler = handler as Action<T>;
                if (typedHandler != null)
                {
                    MMLog.WriteDebug($"[ModEventBus] Publishing event: {eventName}");
                    typedHandler.Invoke(data);
                }
                else
                {
                    MMLog.WriteWarning($"[ModEventBus] Type mismatch for event {eventName}. Expected Action<{typeof(T).Name}>");
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce($"ModEventBus.{eventName}.Error", 
                    $"[ModEventBus] Handler error for {eventName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Subscribe to a named event.
        /// </summary>
        /// <typeparam name="T">Event data type</typeparam>
        /// <param name="eventName">Event name to subscribe to</param>
        /// <param name="handler">Handler to invoke when event is published</param>
        public static void Subscribe<T>(string eventName, Action<T> handler)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                MMLog.WriteWarning("[ModEventBus] Cannot subscribe to event with null/empty name");
                return;
            }
            
            if (handler == null)
            {
                MMLog.WriteWarning($"[ModEventBus] Cannot subscribe null handler to event: {eventName}");
                return;
            }
            
            lock (_lock)
            {
                if (_subscribers.ContainsKey(eventName))
                {
                    _subscribers[eventName] = Delegate.Combine(_subscribers[eventName], handler);
                    MMLog.WriteDebug($"[ModEventBus] Added subscriber to existing event: {eventName}");
                }
                else
                {
                    _subscribers[eventName] = handler;
                    MMLog.WriteDebug($"[ModEventBus] Created new event: {eventName}");
                }
            }
        }
        
        /// <summary>
        /// Unsubscribe from an event.
        /// </summary>
        /// <typeparam name="T">Event data type</typeparam>
        /// <param name="eventName">Event name to unsubscribe from</param>
        /// <param name="handler">Handler to remove</param>
        public static void Unsubscribe<T>(string eventName, Action<T> handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null)
                return;
            
            lock (_lock)
            {
                if (_subscribers.ContainsKey(eventName))
                {
                    _subscribers[eventName] = Delegate.Remove(_subscribers[eventName], handler);
                    
                    // Clean up if no subscribers remain
                    if (_subscribers[eventName] == null)
                    {
                        _subscribers.Remove(eventName);
                        MMLog.WriteDebug($"[ModEventBus] Removed last subscriber and cleaned up event: {eventName}");
                    }
                    else
                    {
                        MMLog.WriteDebug($"[ModEventBus] Removed subscriber from event: {eventName}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if an event has any subscribers.
        /// </summary>
        /// <param name="eventName">Event name to check</param>
        /// <returns>True if the event has at least one subscriber</returns>
        public static bool HasSubscribers(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return false;
            
            lock (_lock)
            {
                return _subscribers.ContainsKey(eventName) && _subscribers[eventName] != null;
            }
        }
        
        /// <summary>
        /// Get count of subscribers for an event.
        /// </summary>
        /// <param name="eventName">Event name</param>
        /// <returns>Number of subscribers</returns>
        public static int GetSubscriberCount(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return 0;
            
            lock (_lock)
            {
                if (_subscribers.TryGetValue(eventName, out var handler) && handler != null)
                {
                    return handler.GetInvocationList().Length;
                }
            }
            
            return 0;
        }
        
        /// <summary>
        /// Clear all subscriptions for an event.
        /// Use with caution - this will remove ALL subscribers.
        /// </summary>
        /// <param name="eventName">Event name to clear</param>
        public static void ClearEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return;
            
            lock (_lock)
            {
                if (_subscribers.Remove(eventName))
                {
                    MMLog.WriteDebug($"[ModEventBus] Cleared all subscribers for event: {eventName}");
                }
            }
        }
        
        /// <summary>
        /// Clear ALL event subscriptions.
        /// Only use during shutdown or testing.
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                int count = _subscribers.Count;
                _subscribers.Clear();
                MMLog.WriteDebug($"[ModEventBus] Cleared all {count} events and their subscribers");
            }
        }
        
        /// <summary>
        /// Get diagnostic information about registered events.
        /// </summary>
        /// <returns>Dictionary of event names and subscriber counts</returns>
        public static Dictionary<string, int> GetEventDiagnostics()
        {
            var diagnostics = new Dictionary<string, int>();
            
            lock (_lock)
            {
                foreach (var kvp in _subscribers)
                {
                    int count = kvp.Value != null ? kvp.Value.GetInvocationList().Length : 0;
                    diagnostics[kvp.Key] = count;
                }
            }
            
            return diagnostics;
        }
    }
}
