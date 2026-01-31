using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Debugging
{
    /// <summary>
    /// Watches fields and properties for changes, logging only when values differ.
    /// Thread-safe and exception-safe.
    /// </summary>
    public class SmartWatcher : MonoBehaviour
    {
        #region Singleton
        
        private static SmartWatcher _instance;
        public static SmartWatcher Instance
        {
            get
            {
                // Unity objects can become 'null' in C# while the native reference is still cached. 
                // Explicitly clearing the instance prevents stale references from persisting 
                // across scene reloads.
                if (_instance == null || _instance.gameObject == null)
                {
                    _instance = null; // Clear stale ref
                    var go = new GameObject("[ModAPI] SmartWatcher");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SmartWatcher>();
                }
                return _instance;
            }
        }
        
        #endregion

        #region Watch Entry
        
        private static readonly object ERROR_SENTINEL = new object();

        private class WatchEntry
        {
            public WeakReference Instance;
            public MemberInfo Member;  // FieldInfo or PropertyInfo
            public Func<object, object> Getter; // Caching the GetValue method as a delegate avoids the overhead 
                                                // of reflection (MemberInfo.Invoke) in every poll cycle, 
                                                // reducing the performance impact of active watches.
            public object LastValue;
            public string Name;
            public string ModName;  // For log context
            public Action<object, object> OnChanged;  // Optional callback (oldValue, newValue)
            public int ErrorCount;  // Track consecutive errors

            // Unity's custom == operator correctly identifies destroyed GameObjects. 
            // WeakReference.IsAlive only tracks GC status, so we must manually check 
            // if the target has been destroyed in-engine.
            public bool IsAlive 
            {
                get
                {
                    if (!Instance.IsAlive) return false;
                    var target = Instance.Target;
                    if (target is UnityEngine.Object uo && uo == null) return false;
                    return target != null;
                }
            }
        }
        
        #endregion

        #region Thread-Safe Watch List
        
        // Simple lock for Unity's single-threaded environment. ReaderWriterLockSlim adds unnecessary
        // overhead (~150ns per acquisition) for a debugging tool that only runs on the main thread.
        // Double-buffering pattern ensures Watch() calls from mod initialization are safely queued.
        private readonly object _watchLock = new object();
        
        private List<WatchEntry> _watches = new List<WatchEntry>();
        private List<WatchEntry> _pendingAdds = new List<WatchEntry>();
        private List<WatchEntry> _pendingRemoves = new List<WatchEntry>();
        
        #endregion

        #region Configuration
        
        private int _basePollInterval = 5;  // Base frames between polls
        private int _pollJitter = 0;        // Randomized jitter to prevent frame-aligned CPU spikes.
        private const int MAX_CONSECUTIVE_ERRORS = 5;  // Auto-remove after this many errors
        
        // Epsilon thresholds for float comparison to avoid logging physics jitter
        private const float FLOAT_EPSILON = 0.0001f;
        private const double DOUBLE_EPSILON = 0.00001;
        
        public int PollInterval
        {
            get => _basePollInterval;
            set => _basePollInterval = Mathf.Max(1, value);
        }
        
        #endregion

        #region Public API

        /// <summary>
        /// Watch a field or property on an object.
        /// ⚠️ WARNING: Property getters must NOT call Watch/UnWatch on this watcher or modify the watch list.
        /// </summary>
        /// <param name="target">Object instance to watch.</param>
        /// <param name="memberName">Name of field or property.</param>
        public void Watch(object target, string memberName)
        {
            if (target == null) return;
            
            var type = target.GetType();
            MemberInfo member = FindMember(type, memberName);
            
            if (member == null)
            {
                MMLog.WriteWarning($"[SmartWatcher] Member '{memberName}' not found on {type.Name}");
                return;
            }

            var entry = new WatchEntry
            {
                Instance = new WeakReference(target),
                Member = member,
                Getter = CreateGetter(member),
                LastValue = SafeGetValue(member, target),
                Name = $"{type.Name}.{memberName}",
                ModName = Assembly.GetCallingAssembly().GetName().Name,
                ErrorCount = 0
            };

            lock (_watchLock)
            {
                _pendingAdds.Add(entry);
            }
            
            MMLog.WriteDebug($"[SmartWatcher:{entry.ModName}] Now watching {entry.Name}");
        }

        /// <summary>
        /// Watch with type checking and optional callback.
        /// ⚠️ WARNING: Callbacks must NOT call Watch/UnWatch on this watcher or modify the watch list.
        /// </summary>
        /// <typeparam name="T">Expected type of the member.</typeparam>
        /// <param name="target">Object instance to watch.</param>
        /// <param name="memberName">Name of field or property.</param>
        /// <param name="onChanged">Optional callback when value changes (receives oldVal, newVal).</param>
        public void Watch<T>(object target, string memberName, Action<T, T> onChanged = null)
        {
            if (target == null) return;
            
            var type = target.GetType();
            MemberInfo member = FindMember(type, memberName);
            
            if (member == null)
            {
                MMLog.WriteWarning($"[SmartWatcher] Member '{memberName}' not found on {type.Name}");
                return;
            }

            // IsAssignableFrom allows watching a derived type but providing a base type callback, 
            // ensuring type-safe variance in change handlers.
            Type memberType = GetMemberType(member);
            if (!typeof(T).IsAssignableFrom(memberType) && memberType != typeof(T))
            {
                MMLog.WriteWarning($"[SmartWatcher] Member '{memberName}' is {memberType.Name}, not compatible with {typeof(T).Name}");
                return;
            }

            var entry = new WatchEntry
            {
                Instance = new WeakReference(target),
                Member = member,
                Getter = CreateGetter(member),
                LastValue = SafeGetValue(member, target),
                Name = $"{type.Name}.{memberName}",
                ModName = Assembly.GetCallingAssembly().GetName().Name,
                // Providing the previous value allows the callback to perform delta calculations 
                // (e.g., health lost vs health gained) without maintaining its own state.
                OnChanged = onChanged != null 
                    ? (old, newVal) => onChanged((T)old, (T)newVal) 
                    : (Action<object, object>)null,
                ErrorCount = 0
            };

            lock (_watchLock)
            {
                _pendingAdds.Add(entry);
            }
            
            MMLog.WriteDebug($"[SmartWatcher:{entry.ModName}] Now watching {entry.Name} as {typeof(T).Name}");
        }

        /// <summary>Stop watching a specific member. Note: O(n) search.</summary>
        public void UnWatch(object target, string memberName)
        {
            lock (_watchLock)
            {
                foreach (var w in _watches)
                {
                    if (w.IsAlive && w.Instance.Target == target && w.Member.Name == memberName)
                    {
                        _pendingRemoves.Add(w);
                    }
                }
            }
        }

        /// <summary>Stop watching all members on a target. Note: O(n) search.</summary>
        public void UnWatchAll(object target)
        {
            lock (_watchLock)
            {
                foreach (var w in _watches)
                {
                    if (w.IsAlive && w.Instance.Target == target)
                    {
                        _pendingRemoves.Add(w);
                    }
                }
            }
        }

        /// <summary>Print current watcher statistics.</summary>
        public void PrintStats()
        {
            lock (_watchLock)
            {
                int total = _watches.Count;
                int alive = _watches.Count(w => w.IsAlive);
                int dead = total - alive;
                int pending = _pendingAdds.Count;
                
                MMLog.WriteInfo($"[SmartWatcher] Stats:");
                MMLog.WriteInfo($"  Active watches: {alive}");
                MMLog.WriteInfo($"  Dead (pending cleanup): {dead}");
                MMLog.WriteInfo($"  Pending adds: {pending}");
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>Find a field or property by name. Game objects often expose data 
        /// through properties rather than public fields. Searching both ensures 
        /// compatibility with standard Unity component patterns.</summary>
        private MemberInfo FindMember(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            
            // Try property first (more common in Unity)
            var prop = type.GetProperty(name, flags);
            if (prop != null) return prop;
            
            // Then try field
            var field = type.GetField(name, flags);
            return field;
        }

        private Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo field) return field.FieldType;
            if (member is PropertyInfo prop) return prop.PropertyType;
            return typeof(object);
        }

        private Func<object, object> CreateGetter(MemberInfo member)
        {
            if (member is FieldInfo field) return obj => field.GetValue(obj);
            if (member is PropertyInfo prop) return obj => prop.GetValue(obj, null);
            return obj => null;
        }

        /// <summary>Safely get value with exception handling. Safeguards the update loop from 
        /// crashing if a watched member throws an exception. Consecutive errors trigger 
        /// automatic removal to prevent log spam.</summary>
        private object SafeGetValue(MemberInfo member, object target)
        {
            try
            {
                if (member is FieldInfo field)
                    return field.GetValue(target);
                
                if (member is PropertyInfo prop)
                    return prop.GetValue(target, null);
                
                return null;
            }
            catch (Exception)
            {
                // Using a unique object sentinel for errors prevents type comparison 
                // issues that occur when using string markers like "[ERROR]".
                return ERROR_SENTINEL;
            }
        }

        private void Update()
        {
            // Adding a randomized jitter prevents multiple watchers from polling on the same frame, 
            // which would cause predictable CPU spikes. Periodic reset ensures jitter doesn't 
            // synchronize over time.
            if (Time.frameCount % 100 == 0)
            {
                _pollJitter = UnityEngine.Random.Range(0, _basePollInterval);
            }
            
            if ((Time.frameCount + _pollJitter) % _basePollInterval != 0) return;

            // Process pending adds/removes (thread-safe)
            lock (_watchLock)
            {
                if (_pendingAdds.Count > 0)
                {
                    _watches.AddRange(_pendingAdds);
                    _pendingAdds.Clear();
                }
                
                if (_pendingRemoves.Count > 0)
                {
                    foreach (var w in _pendingRemoves)
                    {
                        _watches.Remove(w);
                    }
                    _pendingRemoves.Clear();
                }
            }

            // Check values (reverse iteration for safe removal)
            for (int i = _watches.Count - 1; i >= 0; i--)
            {
                var w = _watches[i];
                
                // Clean up dead references (handles both GC and Unity destruction)
                if (!w.IsAlive)
                {
                    _watches.RemoveAt(i);
                    continue;
                }

                // Reference types held in LastValue (especially Unity objects) can 
                // prevent the garbage collector from reclaiming memory. Periodically 
                // clearing these prevents memory leaks.
                if (w.LastValue is UnityEngine.Object uo && uo == null)
                {
                    w.LastValue = null;
                }

                // Exception-safe value retrieval ensures that a single failing watcher 
                // does not crash the entire update loop.
                object currentVal;
                try
                {
                    // Caching the GetValue method as a delegate avoids the overhead 
                    // of reflection in every poll cycle.
                    currentVal = w.Getter(w.Instance.Target);
                    
                    // Check for error sentinel; if the getter returned the sentinel, 
                    // increment the error counter for potential auto-removal.
                    if (currentVal == ERROR_SENTINEL)
                    {
                        w.ErrorCount++;
                        if (w.ErrorCount >= MAX_CONSECUTIVE_ERRORS)
                        {
                            MMLog.WriteWarning($"[SmartWatcher] Removing {w.Name} after {MAX_CONSECUTIVE_ERRORS} consecutive errors");
                            _watches.RemoveAt(i);
                        }
                        continue;
                    }
                    
                    w.ErrorCount = 0;  // Reset on success
                }
                catch (Exception ex)
                {
                    w.ErrorCount++;
                    if (w.ErrorCount >= MAX_CONSECUTIVE_ERRORS)
                    {
                        MMLog.WriteWarning($"[SmartWatcher] Removing {w.Name}: {ex.Message}");
                        _watches.RemoveAt(i);
                    }
                    continue;
                }

                // Check for significant changes (uses epsilon for float/Vector types to filter physics jitter)
                Type memberType = GetMemberType(w.Member);
                if (IsSignificantChange(w.LastValue, currentVal, memberType))
                {
                    object oldValue = w.LastValue;
                    w.LastValue = currentVal;
                    
                    MMLog.WriteInfo($"[SmartWatch:{w.ModName}] {w.Name}: {FormatValue(oldValue)} -> {FormatValue(currentVal)}");
                    
                    // Invoke callback if present
                    try
                    {
                        w.OnChanged?.Invoke(oldValue, currentVal);
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning($"[SmartWatcher] Callback error for {w.Name}: {ex.Message}");
                    }
                }
            }
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is string s) return $"\"{s}\"";
            if (value is bool b) return b.ToString().ToLower();
            return value.ToString();
        }

        /// <summary>
        /// Check if a value change is significant enough to log.
        /// Uses epsilon thresholds for float/Vector types to filter out Unity physics jitter.
        /// </summary>
        private bool IsSignificantChange(object oldVal, object newVal, Type type)
        {
            if (oldVal == null && newVal == null) return false;
            if (oldVal == null || newVal == null) return true; // Null <-> Value is significant
            
            if (type == typeof(float))
            {
                float a = (float)oldVal;
                float b = (float)newVal;
                return Math.Abs(a - b) > FLOAT_EPSILON;
            }
            
            if (type == typeof(double))
            {
                double a = (double)oldVal;
                double b = (double)newVal;
                return Math.Abs(a - b) > DOUBLE_EPSILON;
            }
            
            if (type == typeof(Vector2))
            {
                Vector2 a = (Vector2)oldVal;
                Vector2 b = (Vector2)newVal;
                return (a - b).sqrMagnitude > (FLOAT_EPSILON * FLOAT_EPSILON);
            }
            
            if (type == typeof(Vector3))
            {
                Vector3 a = (Vector3)oldVal;
                Vector3 b = (Vector3)newVal;
                return (a - b).sqrMagnitude > (FLOAT_EPSILON * FLOAT_EPSILON);
            }
            
            // Default: exact equality
            return !oldVal.Equals(newVal);
        }

        #endregion
    }
}
