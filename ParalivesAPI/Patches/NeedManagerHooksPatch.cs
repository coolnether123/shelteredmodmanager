using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.Characters,
        "Paralives Need Hooks",
        TargetBehavior = "Publishes native need value changes through ParalivesAPI.Needs.",
        FailureMode = "Mods can still poll need state, but cannot react consistently to native need changes.",
        RollbackStrategy = "Unsubscribe need hooks or disable mods depending on reactive need updates.",
        IsOptional = true)]
    internal static class NeedManagerHooksPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::NeedManager), "SetNeedToValue")]
        private static void SetNeedPrefix(ulong needGUID, global::AssetCharacter character, ref float __state)
        {
            __state = ReadValue(needGUID, character);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::NeedManager), "SetNeedToValue")]
        private static void SetNeedPostfix(ulong needGUID, global::AssetCharacter character, float value, float __state)
        {
            Publish(ParalivesNeedChangeType.SetValue, needGUID, character, __state, value);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::NeedManager), "ChangeNeedByValue")]
        private static void ChangeNeedPrefix(ulong needGUID, global::AssetCharacter character, ref float __state)
        {
            __state = ReadValue(needGUID, character);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::NeedManager), "ChangeNeedByValue")]
        private static void ChangeNeedPostfix(ulong needGUID, global::AssetCharacter character, float amount, float __state)
        {
            Publish(ParalivesNeedChangeType.ChangedByValue, needGUID, character, __state, amount);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::NeedManager), "ReliefNeed")]
        private static void ReliefNeedPrefix(ulong needGUID, global::AssetCharacter character, ref float __state)
        {
            __state = ReadValue(needGUID, character);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::NeedManager), "ReliefNeed")]
        private static void ReliefNeedPostfix(ulong needGUID, global::AssetCharacter character, float __state)
        {
            Publish(ParalivesNeedChangeType.Relieved, needGUID, character, __state, 0f);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::NeedManager), "ForceReliefNeed")]
        private static void ForceReliefNeedPrefix(ulong needGUID, global::AssetCharacter character, ref float __state)
        {
            __state = ReadValue(needGUID, character);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::NeedManager), "ForceReliefNeed")]
        private static void ForceReliefNeedPostfix(ulong needGUID, global::AssetCharacter character, float __state)
        {
            Publish(ParalivesNeedChangeType.ForceRelieved, needGUID, character, __state, 0f);
        }

        private static float ReadValue(ulong needGuid, global::AssetCharacter character)
        {
            if (needGuid == 0UL || character == null)
                return 0f;

            try
            {
                return global::NeedManager.Instance.GetNeedValue(needGuid, character);
            }
            catch
            {
                return 0f;
            }
        }

        private static void Publish(
            ParalivesNeedChangeType changeType,
            ulong needGuid,
            global::AssetCharacter character,
            float previous,
            float requestedAmount)
        {
            if (needGuid == 0UL || character == null)
                return;

            float current = ReadValue(needGuid, character);
            ParalivesRuntimeInfo.Current.Needs.PublishChanged(new ParalivesNeedChangedEvent
            {
                ChangeType = changeType,
                CharacterGuid = character.GUID,
                NeedGuid = needGuid,
                PreviousValue = previous,
                CurrentValue = current,
                Amount = requestedAmount
            });
        }
    }
}
