using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.Characters,
        "Paralives Skill Hooks",
        TargetBehavior = "Publishes native skill level and experience changes through ParalivesAPI.Skills.",
        FailureMode = "Mods can still poll skill state, but cannot react predictably when native skill changes happen.",
        RollbackStrategy = "Unsubscribe skill hooks or disable mods depending on reactive skill updates.",
        IsOptional = true)]
    internal static class SkillManagerHooksPatch
    {
        private sealed class SkillState
        {
            public int Level;

            public float CurrentLevelExperience;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::SkillManager), "SetCharacterSkillLevel")]
        private static void SetLevelPrefix(global::AssetCharacter characterAsset, ulong skillGUID, ref SkillState __state)
        {
            __state = Capture(characterAsset, skillGUID);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::SkillManager), "SetCharacterSkillLevel")]
        private static void SetLevelPostfix(global::AssetCharacter characterAsset, ulong skillGUID, SkillState __state)
        {
            Publish(ParalivesSkillChangeType.LevelSet, characterAsset, skillGUID, __state, 0f);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::SkillManager), "IncrementCharacterSkillBurst")]
        private static void BurstPrefix(global::AssetCharacter characterAsset, ulong skillGUID, ref SkillState __state)
        {
            __state = Capture(characterAsset, skillGUID);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::SkillManager), "IncrementCharacterSkillBurst")]
        private static void BurstPostfix(
            global::AssetCharacter characterAsset,
            ulong skillGUID,
            System.ValueTuple<int, float, float> __result,
            SkillState __state)
        {
            Publish(ParalivesSkillChangeType.BurstExperience, characterAsset, skillGUID, __state, __result.Item3);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::SkillManager), "IncrementCharacterSkillOverTime")]
        private static void OverTimePrefix(global::AssetCharacter characterAsset, ulong skillGUID, ref SkillState __state)
        {
            __state = Capture(characterAsset, skillGUID);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::SkillManager), "IncrementCharacterSkillOverTime")]
        private static void OverTimePostfix(
            global::AssetCharacter characterAsset,
            ulong skillGUID,
            System.ValueTuple<int, float, float> __result,
            SkillState __state)
        {
            Publish(ParalivesSkillChangeType.OverTimeExperience, characterAsset, skillGUID, __state, __result.Item3);
        }

        private static SkillState Capture(global::AssetCharacter character, ulong skillGuid)
        {
            if (character == null || skillGuid == 0UL || global::SkillManager.Instance == null)
                return new SkillState();

            try
            {
                return new SkillState
                {
                    Level = global::SkillManager.Instance.GetCharacterSkillLevel(character, skillGuid),
                    CurrentLevelExperience = global::SkillManager.Instance.GetCharacterExperienceInCurrentLevel(character, skillGuid)
                };
            }
            catch
            {
                return new SkillState();
            }
        }

        private static void Publish(
            ParalivesSkillChangeType changeType,
            global::AssetCharacter character,
            ulong skillGuid,
            SkillState previous,
            float grantedExperience)
        {
            if (character == null || skillGuid == 0UL)
                return;

            int currentLevel = 0;
            try
            {
                currentLevel = global::SkillManager.Instance.GetCharacterSkillLevel(character, skillGuid);
            }
            catch
            {
            }

            ParalivesRuntimeInfo.Current.Skills.PublishChanged(new ParalivesSkillChangedEvent
            {
                ChangeType = changeType,
                CharacterGuid = character.GUID,
                SkillGuid = skillGuid,
                PreviousLevel = previous == null ? 0 : previous.Level,
                CurrentLevel = currentLevel,
                PreviousCurrentLevelExperience = previous == null ? 0f : previous.CurrentLevelExperience,
                GrantedExperience = grantedExperience
            });
        }
    }
}
