using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;

namespace ModAPI.Interactions
{
    public enum InteractionInsertMode
    {
        Last,
        First,
        Index,
        Before,
        After,
        Priority,
        CustomIndex
    }

    public sealed class InteractionInsertContext
    {
        private readonly Obj_Base _targetObject;
        private readonly IList<string> _currentInteractions;
        private readonly string _interactionName;

        internal InteractionInsertContext(Obj_Base targetObject, IList<string> currentInteractions, string interactionName)
        {
            _targetObject = targetObject;
            _currentInteractions = currentInteractions;
            _interactionName = interactionName;
        }

        public Obj_Base TargetObject { get { return _targetObject; } }
        public IList<string> CurrentInteractions { get { return _currentInteractions; } }
        public string InteractionName { get { return _interactionName; } }
    }

    public sealed class InteractionTargetBuilder
    {
        private readonly ObjectManager.ObjectType _targetType;

        internal InteractionTargetBuilder(ObjectManager.ObjectType targetType)
        {
            _targetType = targetType;
        }

        public InteractionButtonBuilder Add<TInteraction>(string interactionName) where TInteraction : Int_Base
        {
            return Add(interactionName, typeof(TInteraction));
        }

        public InteractionButtonBuilder Add(string interactionName, Type interactionType)
        {
            return new InteractionButtonBuilder(this, _targetType, interactionName, interactionType);
        }

        public ObjectManager.ObjectType TargetType { get { return _targetType; } }
    }

    public sealed class InteractionButtonBuilder
    {
        private readonly InteractionTargetBuilder _parent;
        private readonly ObjectManager.ObjectType _targetType;
        private readonly string _interactionName;
        private readonly Type _interactionType;

        private InteractionInsertMode _mode = InteractionInsertMode.Last;
        private string _anchorInteraction;
        private int _index = -1;
        private int _priority = int.MaxValue;
        private Func<InteractionInsertContext, int> _customIndexResolver;
        private Func<Obj_Base, bool> _predicate;
        private Action<Obj_Base, Int_Base> _onInjected;
        private bool _debugEnabled;
        private string _debugKey;
        private int _debugMaxLogs = 40;

        internal InteractionButtonBuilder(
            InteractionTargetBuilder parent,
            ObjectManager.ObjectType targetType,
            string interactionName,
            Type interactionType)
        {
            _parent = parent;
            _targetType = targetType;
            _interactionName = interactionName;
            _interactionType = interactionType;
        }

        public InteractionButtonBuilder First()
        {
            _mode = InteractionInsertMode.First;
            return this;
        }

        public InteractionButtonBuilder Last()
        {
            _mode = InteractionInsertMode.Last;
            return this;
        }

        public InteractionButtonBuilder AtIndex(int index)
        {
            _mode = InteractionInsertMode.Index;
            _index = index;
            return this;
        }

        public InteractionButtonBuilder Before(string interactionType)
        {
            _mode = InteractionInsertMode.Before;
            _anchorInteraction = interactionType;
            return this;
        }

        public InteractionButtonBuilder After(string interactionType)
        {
            _mode = InteractionInsertMode.After;
            _anchorInteraction = interactionType;
            return this;
        }

        public InteractionButtonBuilder WithPriority(int priority)
        {
            _mode = InteractionInsertMode.Priority;
            _priority = priority;
            return this;
        }

        public InteractionButtonBuilder UsingIndexResolver(Func<InteractionInsertContext, int> resolver)
        {
            _mode = InteractionInsertMode.CustomIndex;
            _customIndexResolver = resolver;
            return this;
        }

        public InteractionButtonBuilder When(Func<Obj_Base, bool> predicate)
        {
            _predicate = predicate;
            return this;
        }

        public InteractionButtonBuilder OnInjected(Action<Obj_Base, Int_Base> callback)
        {
            _onInjected = callback;
            return this;
        }

        public InteractionButtonBuilder Debug(bool enabled)
        {
            _debugEnabled = enabled;
            if (!enabled) _debugKey = null;
            return this;
        }

        public InteractionButtonBuilder Debug(string debugKey, int maxLogs)
        {
            _debugEnabled = true;
            _debugKey = debugKey;
            _debugMaxLogs = maxLogs > 0 ? maxLogs : 40;
            return this;
        }

        public InteractionTargetBuilder Register()
        {
            InteractionRegistry.RegisterInternal(
                _targetType,
                _interactionName,
                _interactionType,
                _mode,
                _anchorInteraction,
                _index,
                _priority,
                _customIndexResolver,
                _predicate,
                _onInjected,
                _debugEnabled,
                _debugKey,
                _debugMaxLogs);

            return _parent;
        }
    }

    /// <summary>
    /// Registry for adding new interactions (interaction-menu buttons) to existing game objects.
    /// Interactions are injected automatically when objects are spawned, with fluent placement controls.
    /// </summary>
    [Obsolete("Compatibility API retained in ModAPI 1.3. Planned to move to ShelteredAPI in a future major release.", false)]
    public static class InteractionRegistry
    {
        private struct InteractionEntry
        {
            public string Name;
            public Type InteractionType;
            public InteractionInsertMode InsertMode;
            public string AnchorInteraction;
            public int InsertIndex;
            public int PriorityValue;
            public Func<InteractionInsertContext, int> CustomIndexResolver;
            public Func<Obj_Base, bool> Predicate;
            public Action<Obj_Base, Int_Base> OnInjected;
            public bool DebugEnabled;
            public string DebugKey;
            public int DebugMaxLogs;
            public int Order;
        }

        private static readonly Dictionary<ObjectManager.ObjectType, List<InteractionEntry>> _customInteractions
            = new Dictionary<ObjectManager.ObjectType, List<InteractionEntry>>();
        private static readonly Dictionary<string, int> _debugLogCounts = new Dictionary<string, int>();
        private static readonly object _sync = new object();
        private static int _nextOrder = 1;

        public static InteractionTargetBuilder For(ObjectManager.ObjectType targetType)
        {
            return new InteractionTargetBuilder(targetType);
        }

        public static InteractionTargetBuilder On(ObjectManager.ObjectType targetType)
        {
            return For(targetType);
        }

        public static InteractionButtonBuilder AddButton<TInteraction>(ObjectManager.ObjectType targetType, string interactionName)
            where TInteraction : Int_Base
        {
            return For(targetType).Add<TInteraction>(interactionName);
        }

        /// <summary>
        /// Registers a new interaction for a specific object type.
        /// </summary>
        /// <typeparam name="TTarget">The type of Obj_Base to add this to (e.g. Obj_Bed)</typeparam>
        /// <param name="interactionName">Unique internal name for the interaction</param>
        /// <param name="interactionType">The class inheriting from Int_Base to instantiate</param>
        public static void Register<TTarget>(string interactionName, Type interactionType) where TTarget : Obj_Base
        {
            MMLog.WarnOnce(
                "InteractionRegistry.RegisterGeneric.Unsupported",
                "[InteractionRegistry] Register<TTarget> is deprecated and cannot infer ObjectType. Use InteractionRegistry.For(ObjectType)...Register().");
        }

        /// <summary>
        /// Registers a new interaction for a specific ObjectType.
        /// </summary>
        public static void Register(ObjectManager.ObjectType targetType, string interactionName, Type interactionType)
        {
            RegisterInternal(
                targetType,
                interactionName,
                interactionType,
                InteractionInsertMode.Last,
                null,
                -1,
                int.MaxValue,
                null,
                null,
                null,
                false,
                null,
                0);
        }

        internal static void RegisterInternal(
            ObjectManager.ObjectType targetType,
            string interactionName,
            Type interactionType,
            InteractionInsertMode insertMode,
            string anchorInteraction,
            int insertIndex,
            int priorityValue,
            Func<InteractionInsertContext, int> customIndexResolver,
            Func<Obj_Base, bool> predicate,
            Action<Obj_Base, Int_Base> onInjected,
            bool debugEnabled,
            string debugKey,
            int debugMaxLogs)
        {
            if (string.IsNullOrEmpty(interactionName))
                throw new ArgumentException("interactionName cannot be null or empty.", "interactionName");
            if (interactionType == null)
                throw new ArgumentNullException("interactionType");
            if (!typeof(Int_Base).IsAssignableFrom(interactionType))
                throw new ArgumentException("interactionType must derive from Int_Base.", "interactionType");

            lock (_sync)
            {
                List<InteractionEntry> entries;
                if (!_customInteractions.TryGetValue(targetType, out entries))
                {
                    entries = new List<InteractionEntry>();
                    _customInteractions[targetType] = entries;
                }

                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    if (StringEquals(entries[i].Name, interactionName))
                        entries.RemoveAt(i);
                }

                entries.Add(new InteractionEntry
                {
                    Name = interactionName,
                    InteractionType = interactionType,
                    InsertMode = insertMode,
                    AnchorInteraction = anchorInteraction,
                    InsertIndex = insertIndex,
                    PriorityValue = priorityValue,
                    CustomIndexResolver = customIndexResolver,
                    Predicate = predicate,
                    OnInjected = onInjected,
                    DebugEnabled = debugEnabled,
                    DebugKey = debugKey,
                    DebugMaxLogs = debugMaxLogs > 0 ? debugMaxLogs : 40,
                    Order = _nextOrder++
                });
            }

            ModLog.Debug(string.Format("Registered interaction '{0}' for {1} with mode {2}", interactionName, targetType, insertMode));
        }

        internal static void InjectInteractions(Obj_Base obj)
        {
            if (obj == null) return;
            var type = obj.GetObjectType();
            List<InteractionEntry> entries;

            lock (_sync)
            {
                if (!_customInteractions.TryGetValue(type, out entries) || entries == null || entries.Count == 0)
                    return;
                entries = new List<InteractionEntry>(entries);
            }

            entries.Sort(delegate(InteractionEntry x, InteractionEntry y)
            {
                return x.Order.CompareTo(y.Order);
            });

            foreach (var entry in entries)
            {
                try
                {
                    Trace(entry, "inject:start obj=" + SafeObjectName(obj) + ", type=" + type);

                    if (entry.Predicate != null && !entry.Predicate(obj))
                    {
                        Trace(entry, "inject:skip predicate returned false");
                        continue;
                    }

                    if (HasInteraction(obj, entry.Name))
                    {
                        Trace(entry, "inject:skip interaction already exists");
                        continue;
                    }

                    var interaction = obj.gameObject.AddComponent(entry.InteractionType) as Int_Base;
                    if (interaction != null)
                    {
                        obj.RegisterInteraction(interaction, entry.Name);
                        Trace(entry, "inject:registered successfully");
                        if (entry.OnInjected != null)
                        {
                            entry.OnInjected(obj, interaction);
                            Trace(entry, "inject:onInjected callback completed");
                        }
                    }
                    else
                    {
                        Trace(entry, "inject:failed component creation returned null");
                    }
                }
                catch (Exception ex)
                {
                    Trace(entry, "inject:error " + ex.Message);
                    ModLog.Error(string.Format("Failed to inject interaction {0} into {1}: {2}", entry.Name, obj.name, ex.Message));
                }
            }
        }

        private static bool HasInteraction(Obj_Base obj, string interactionName)
        {
            if (obj == null || string.IsNullOrEmpty(interactionName))
                return false;

            try
            {
                var list = obj.GetAllInteractions();
                if (list == null) return false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (StringEquals(list[i], interactionName))
                        return true;
                }
            }
            catch { }

            return false;
        }

        private static void ReorderInteractions(Obj_Base obj, List<string> interactions)
        {
            if (obj == null || interactions == null || interactions.Count == 0)
                return;

            List<InteractionEntry> entries;
            lock (_sync)
            {
                ObjectManager.ObjectType objectType = obj.GetObjectType();
                if (!_customInteractions.TryGetValue(objectType, out entries) || entries == null || entries.Count == 0)
                    return;
                entries = new List<InteractionEntry>(entries);
            }

            entries.Sort(delegate(InteractionEntry x, InteractionEntry y)
            {
                return x.Order.CompareTo(y.Order);
            });

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                int currentIndex = IndexOfInteraction(interactions, entry.Name);
                if (currentIndex < 0)
                {
                    Trace(entry, "reorder:interaction not currently present in menu list");
                    continue;
                }

                interactions.RemoveAt(currentIndex);

                int targetIndex = ResolveInsertionIndex(obj, interactions, entry);
                if (targetIndex < 0) targetIndex = 0;
                if (targetIndex > interactions.Count) targetIndex = interactions.Count;

                interactions.Insert(targetIndex, entry.Name);
                Trace(entry, "reorder:moved to index=" + targetIndex + ", mode=" + entry.InsertMode);
            }
        }

        private static int ResolveInsertionIndex(Obj_Base obj, IList<string> interactions, InteractionEntry entry)
        {
            switch (entry.InsertMode)
            {
                case InteractionInsertMode.First:
                    return 0;

                case InteractionInsertMode.Last:
                    return interactions.Count;

                case InteractionInsertMode.Index:
                    return entry.InsertIndex;

                case InteractionInsertMode.Before:
                {
                    int anchor = IndexOfInteraction(interactions, entry.AnchorInteraction);
                    return anchor >= 0 ? anchor : interactions.Count;
                }

                case InteractionInsertMode.After:
                {
                    int anchor = IndexOfInteraction(interactions, entry.AnchorInteraction);
                    return anchor >= 0 ? (anchor + 1) : interactions.Count;
                }

                case InteractionInsertMode.Priority:
                    return ResolvePriorityIndex(obj, interactions, entry.PriorityValue);

                case InteractionInsertMode.CustomIndex:
                {
                    if (entry.CustomIndexResolver == null) return interactions.Count;
                    try
                    {
                        var ctx = new InteractionInsertContext(obj, interactions, entry.Name);
                        return entry.CustomIndexResolver(ctx);
                    }
                    catch (Exception ex)
                    {
                        Trace(entry, "reorder:custom resolver threw " + ex.Message + ", defaulting to end");
                        MMLog.WarnOnce(
                            "InteractionRegistry.CustomIndexResolverError",
                            "[InteractionRegistry] Custom index resolver error: " + ex.Message);
                        return interactions.Count;
                    }
                }
            }

            return interactions.Count;
        }

        private static int ResolvePriorityIndex(Obj_Base obj, IList<string> interactions, int priorityValue)
        {
            for (int i = 0; i < interactions.Count; i++)
            {
                int p = GetInteractionPriority(obj, interactions[i]);
                if (p > priorityValue)
                    return i;
            }
            return interactions.Count;
        }

        private static int GetInteractionPriority(Obj_Base obj, string interactionName)
        {
            if (obj == null || string.IsNullOrEmpty(interactionName))
                return int.MaxValue;

            try
            {
                var map = Safe.GetField<Dictionary<string, Int_Base>>(obj, "interactions");
                if (map != null)
                {
                    Int_Base interaction;
                    if (map.TryGetValue(interactionName, out interaction) && interaction != null)
                        return interaction.GetInteractionPriority();
                }
            }
            catch { }

            return int.MaxValue;
        }

        private static int IndexOfInteraction(IList<string> interactions, string interactionName)
        {
            if (interactions == null || string.IsNullOrEmpty(interactionName))
                return -1;

            for (int i = 0; i < interactions.Count; i++)
            {
                if (StringEquals(interactions[i], interactionName))
                    return i;
            }

            return -1;
        }

        private static bool StringEquals(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static void Trace(InteractionEntry entry, string message)
        {
            if (!entry.DebugEnabled) return;

            string key = string.IsNullOrEmpty(entry.DebugKey) ? entry.Name : entry.DebugKey;
            if (string.IsNullOrEmpty(key)) key = "interaction_debug";

            int limit = entry.DebugMaxLogs > 0 ? entry.DebugMaxLogs : 40;
            bool logThis = false;
            bool reachedLimit = false;

            lock (_sync)
            {
                int count;
                _debugLogCounts.TryGetValue(key, out count);
                if (count < limit)
                {
                    count++;
                    _debugLogCounts[key] = count;
                    logThis = true;
                    reachedLimit = count == limit;
                }
            }

            if (!logThis) return;

            MMLog.WriteDebug("[InteractionRegistry DEBUG][" + key + "] " + message + (reachedLimit ? " (log limit reached)" : string.Empty));
        }

        private static string SafeObjectName(Obj_Base obj)
        {
            if (obj == null) return "(null)";
            try
            {
                return string.IsNullOrEmpty(obj.name) ? obj.GetType().Name : obj.name;
            }
            catch
            {
                return "(unknown)";
            }
        }

        [HarmonyPatch(typeof(Obj_Base), "Awake")]
        private static class Obj_Base_Awake_Patch
        {
            private static void Postfix(Obj_Base __instance)
            {
                InjectInteractions(__instance);
            }
        }

        [HarmonyPatch(typeof(Obj_Base), "GetAllInteractions")]
        private static class Obj_Base_GetAllInteractions_Patch
        {
            private static void Postfix(Obj_Base __instance, ref List<string> __result)
            {
                ReorderInteractions(__instance, __result);
            }
        }

        [HarmonyPatch(typeof(Obj_Base), "GetPlayerInteractions")]
        private static class Obj_Base_GetPlayerInteractions_Patch
        {
            private static void Postfix(Obj_Base __instance, ref List<string> __result)
            {
                ReorderInteractions(__instance, __result);
            }
        }
    }
}
