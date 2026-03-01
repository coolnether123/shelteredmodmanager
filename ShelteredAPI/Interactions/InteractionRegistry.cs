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
