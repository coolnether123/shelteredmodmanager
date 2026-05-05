using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Events
{
    /// <summary>
    /// In-process pub/sub event bus for inter-mod communication.
    /// Use reverse-domain event names, such as <c>com.author.mod.ItemDiscovered</c>, so unrelated mods do not collide.
    /// </summary>
    public static class ModEventBus
    {
        private static readonly Dictionary<string, Delegate> _subscribers 
            = new Dictionary<string, Delegate>(StringComparer.OrdinalIgnoreCase);
        
        private static readonly object _lock = new object();
        
        /// <summary>
        /// Publishes a typed event to all current subscribers.
        /// Handlers run synchronously on the caller's thread, so publish from the Unity main thread when handlers may touch game objects.
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
        /// Subscribes a handler to a named typed event.
        /// The handler type must match the type used by publishers for the same event name.
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
        /// Removes a previously subscribed handler from a named event.
        /// Call this during mod shutdown when the subscriber owns runtime objects.
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
        /// Returns true when an event currently has one or more subscribers.
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
        /// Returns the current subscriber count for an event.
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
        /// Clears all subscriptions for one event name.
        /// Use only for owner-controlled events, shutdown, or tests because this removes other mods' handlers too.
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
        /// Clears every event subscription in the process.
        /// Only use during shutdown or tests.
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
        /// Returns event names and subscriber counts for diagnostics.
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
