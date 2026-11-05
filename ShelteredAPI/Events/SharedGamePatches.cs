using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Events
{
    [PatchPolicy(PatchDomain.Events, "CharacterDeathEvents",
        TargetBehavior = "Report shelter and wasteland death after damage commits health.",
        FailureMode = "Non-encounter deaths are not reported.", RollbackStrategy = "Disable the Events patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(BaseCharacter), "Damage")]
    internal static class CharacterDamageEventPatch
    {
        private static void Prefix(BaseCharacter __instance, out bool __state) { __state = __instance.health > 0; }
        private static void Postfix(BaseCharacter __instance, bool __state, BaseCharacter.DamageType type, string extra_info, bool wasteland)
        {
            GameEvents.RaiseDeath(__instance, __state, type, extra_info, wasteland);
        }
    }

    [PatchPolicy(PatchDomain.Events, "CharacterDeathEvents",
        TargetBehavior = "Report fatal damage even when vanilla chooses unconsciousness.",
        FailureMode = "Queue extensions miss fatal damage.", RollbackStrategy = "Disable the Events patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(FamilyMember), "OnFatalDamageTaken")]
    internal static class CharacterFatalDamageEventPatch
    {
        private static void Prefix(FamilyMember __instance) { GameEvents.RaiseFatalDamageStarting(__instance); }
        private static void Postfix(FamilyMember __instance, bool __result) { GameEvents.RaiseFatalDamage(__instance, __result); }
    }

    [PatchPolicy(PatchDomain.Events, "CharacterDeathEvents",
        TargetBehavior = "Report encounter health transitions with the credited attacker.",
        FailureMode = "Combat death identities are unavailable.", RollbackStrategy = "Disable the Events patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(EncounterCharacter), "Health", MethodType.Setter)]
    internal static class EncounterDeathEventPatch
    {
        private static void Prefix(EncounterCharacter __instance, out bool __state) { __state = !__instance.isDead; }
        private static void Postfix(EncounterCharacter __instance, bool __state) { GameEvents.RaiseEncounterDeath(__instance, __state); }
    }

    [PatchPolicy(PatchDomain.Events, "CharacterDeathEvents",
        TargetBehavior = "Clear pending encounter death attribution after healing.",
        FailureMode = "A later death after revival may be skipped.", RollbackStrategy = "Disable the Events patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch]
    internal static class CharacterDeathHealingPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BaseCharacter), "Heal");
            yield return AccessTools.Method(typeof(BaseCharacter), "HealAfterUnconscious");
        }
        private static void Postfix(BaseCharacter __instance) { GameEvents.ClearEncounterDeath(__instance); }
    }

    [PatchPolicy(PatchDomain.Events, "ExpeditionTickEvent",
        TargetBehavior = "Notify subscribers after every expedition manager update.",
        FailureMode = "Expedition cleanup subscribers do not run.", RollbackStrategy = "Disable the Events patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExplorationManager), "UpdateManager")]
    internal static class ExpeditionTickEventPatch
    {
        private static void Postfix(ExplorationManager __instance) { GameEvents.RaiseExpeditionTick(__instance); }
    }

    [PatchPolicy(PatchDomain.Events, "ObjectWearEvent",
        TargetBehavior = "Adjust timed wear amount and next interval without intercepting fire or direct damage.",
        FailureMode = "Timed wear contributions are ignored.", RollbackStrategy = "Disable the Events patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(Obj_Integrity), "Update")]
    internal static class ObjectWearEventPatch
    {
        private static readonly FieldInfo Interval = AccessTools.Field(typeof(Obj_Integrity), "m_DegradeInterval");
        private static void Prefix(float ___m_DegradeInterval, out float? __state)
        {
            __state = GameEvents.HasWearSubscribers ? (float?)___m_DegradeInterval : null;
        }
        private static Exception Finalizer(ref float ___m_DegradeInterval, float? __state, Exception __exception)
        {
            if (__state.HasValue) ___m_DegradeInterval = __state.Value;
            return __exception;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            MethodInfo degrade = AccessTools.Method(typeof(Obj_Integrity), "Degrade");
            int matches = code.FindAll(x => x.Calls(degrade)).Count;
            if (matches != 1)
            {
                MMLog.WarnOnce("ObjectWearEvent.Shape", "Timed wear bridge expected one Degrade call; leaving Update unchanged.");
                return code;
            }
            foreach (CodeInstruction instruction in code)
                if (instruction.Calls(degrade))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(ObjectWearEventPatch), "ApplyTimedWear");
                }
            return code;
        }
        private static void ApplyTimedWear(Obj_Integrity target, int amount)
        {
            if (GameEvents.HasWearSubscribers)
            {
                float interval = (float)Interval.GetValue(target);
                ObjectDegradingEventArgs args = GameEvents.RaiseObjectDegrading(target, amount, interval);
                amount = Math.Max(0, args.WearAmount);
                if (args.Interval > 0f && !float.IsInfinity(args.Interval)) Interval.SetValue(target, args.Interval);
            }
            target.Degrade(amount);
        }
    }
}
