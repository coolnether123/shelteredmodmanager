using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;

namespace ModAPI.Interactions
{
    /// <summary>
    /// Registry for adding new interactions to existing game objects.
    /// Interactions are injected automatically when objects are spawned.
    /// </summary>
    public static class InteractionRegistry
    {
        private struct InteractionEntry
        {
            public string Name;
            public Type InteractionType;
        }

        private static readonly Dictionary<ObjectManager.ObjectType, List<InteractionEntry>> _customInteractions 
            = new Dictionary<ObjectManager.ObjectType, List<InteractionEntry>>();

        /// <summary>
        /// Registers a new interaction for a specific object type.
        /// </summary>
        /// <typeparam name="TTarget">The type of Obj_Base to add this to (e.g. Obj_Bed)</typeparam>
        /// <param name="interactionName">Unique internal name for the interaction</param>
        /// <param name="interactionType">The class inheriting from Int_Base to instantiate</param>
        public static void Register<TTarget>(string interactionName, Type interactionType) where TTarget : Obj_Base
        {
            // We need to know the ObjectType enum value for TTarget.
            // Since we can't easily get it from TTarget without an instance, 
            // the user might have to provide it.
            // Alternatively, we can use reflection to Find the ObjectType from a dummy instance or mapping.
        }

        /// <summary>
        /// Registers a new interaction for a specific ObjectType.
        /// </summary>
        public static void Register(ObjectManager.ObjectType targetType, string interactionName, Type interactionType)
        {
            if (!_customInteractions.ContainsKey(targetType))
                _customInteractions[targetType] = new List<InteractionEntry>();

            _customInteractions[targetType].Add(new InteractionEntry { Name = interactionName, InteractionType = interactionType });
            ModLog.Debug($"Registered interaction '{interactionName}' for {targetType}");
        }

        internal static void InjectInteractions(Obj_Base obj)
        {
            if (obj == null) return;
            var type = obj.GetObjectType();
            if (_customInteractions.TryGetValue(type, out var entries))
            {
                foreach (var entry in entries)
                {
                    try
                    {
                        // Instantiate the interaction object (Int_Base)
                        var interaction = obj.gameObject.AddComponent(entry.InteractionType) as Int_Base;
                        if (interaction != null)
                        {
                            obj.RegisterInteraction(interaction, entry.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error($"Failed to inject interaction {entry.Name} into {obj.name}: {ex.Message}");
                    }
                }
            }
        }

        // Harmony patch to auto-inject on Awake
        [HarmonyPatch(typeof(Obj_Base), "Awake")]
        private static class Obj_Base_Awake_Patch
        {
            private static void Postfix(Obj_Base __instance)
            {
                InjectInteractions(__instance);
            }
        }
    }
}
