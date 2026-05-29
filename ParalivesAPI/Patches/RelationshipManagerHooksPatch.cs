using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.Characters,
        "Paralives Relationship Hooks",
        TargetBehavior = "Publishes native relationship label unlock/remove/level changes through ParalivesAPI.Relationships.",
        FailureMode = "Mods can still poll relationship state, but cannot react consistently to native relationship changes.",
        RollbackStrategy = "Unsubscribe relationship hooks or disable mods depending on reactive relationship updates.",
        IsOptional = true)]
    internal static class RelationshipManagerHooksPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::RelationshipManager), "UnlockLabel")]
        private static void UnlockLabelPostfix(
            ulong from,
            ulong to,
            ulong labelGUID,
            bool __result)
        {
            ParalivesRuntimeInfo.Current.Relationships.PublishChanged(new ParalivesRelationshipChangedEvent
            {
                ChangeType = ParalivesRelationshipChangeType.LabelUnlocked,
                SourceCharacterGuid = from,
                TargetCharacterGuid = to,
                LabelGuid = labelGUID,
                Level = __result ? 1 : 0,
                Changed = __result
            });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::RelationshipManager), "RemoveRelationshipLabel")]
        private static void RemoveLabelPrefix(
            ulong from,
            ulong to,
            ulong labelGUID,
            ref bool __state)
        {
            try
            {
                __state = global::RelationshipManager.Instance.IsRelationshipLabelPresent(from, to, labelGUID);
            }
            catch
            {
                __state = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::RelationshipManager), "RemoveRelationshipLabel")]
        private static void RemoveLabelPostfix(
            ulong from,
            ulong to,
            ulong labelGUID,
            bool __state)
        {
            if (!__state)
                return;

            ParalivesRuntimeInfo.Current.Relationships.PublishChanged(new ParalivesRelationshipChangedEvent
            {
                ChangeType = ParalivesRelationshipChangeType.LabelRemoved,
                SourceCharacterGuid = from,
                TargetCharacterGuid = to,
                LabelGuid = labelGUID,
                Changed = true
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::RelationshipManager), "IncrementLabelLevel")]
        private static void IncrementLabelPostfix(
            ulong from,
            ulong to,
            ulong labelGUID,
            int increment,
            int __result)
        {
            if (__result == 0)
                return;

            ParalivesRuntimeInfo.Current.Relationships.PublishChanged(new ParalivesRelationshipChangedEvent
            {
                ChangeType = ParalivesRelationshipChangeType.LabelLevelChanged,
                SourceCharacterGuid = from,
                TargetCharacterGuid = to,
                LabelGuid = labelGUID,
                Increment = increment,
                Level = __result,
                Changed = true
            });
        }
    }
}
