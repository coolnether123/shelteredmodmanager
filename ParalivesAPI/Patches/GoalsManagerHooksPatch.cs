using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.Characters,
        "Paralives Goal Hooks",
        TargetBehavior = "Publishes goal add, tracking, reward, objective, cancel, and request turn-in events through ParalivesAPI.Goals.",
        FailureMode = "Mods can still read/mutate goals, but cannot react predictably to native goal lifecycle changes.",
        RollbackStrategy = "Unsubscribe goal hooks or disable mods depending on goal lifecycle callbacks.",
        IsOptional = true)]
    internal static class GoalsManagerHooksPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GoalsManager), "AddGoalToCharacter")]
        private static void AddGoalToCharacterPrefix(global::AssetCharacter character, out int __state)
        {
            __state = GetGoalCount(character);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::GoalsManager), "AddGoalToCharacter")]
        private static void AddGoalToCharacterPostfix(
            global::AssetCharacter character,
            ulong goalGUID,
            ulong requesterGUID,
            ulong targetGUID,
            int __state)
        {
            if (character == null || GetGoalCount(character) <= __state || FindGoalData(character, goalGUID, requesterGUID) == null)
                return;

            ParalivesRuntimeInfo.Current.Goals.PublishChanged(new ParalivesGoalChangedEvent
            {
                ChangeType = ParalivesGoalChangeType.Added,
                CharacterGuid = character.GUID,
                GoalGuid = goalGUID,
                RequesterGuid = requesterGUID,
                TargetCharacterGuid = targetGUID
            });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GoalsManager), "SetTrackedGoal")]
        private static void SetTrackedGoalPrefix(global::AssetCharacter character, out ulong __state)
        {
            __state = character == null || character.Data == null ? 0UL : character.Data.TrackedGoal;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::GoalsManager), "SetTrackedGoal")]
        private static void SetTrackedGoalPostfix(
            global::AssetCharacter character,
            ulong goalGUID,
            bool track,
            ulong __state)
        {
            ulong trackedGoal = character == null || character.Data == null ? 0UL : character.Data.TrackedGoal;
            if (character == null || trackedGoal == __state)
                return;

            ParalivesRuntimeInfo.Current.Goals.PublishChanged(new ParalivesGoalChangedEvent
            {
                ChangeType = ParalivesGoalChangeType.Tracked,
                CharacterGuid = character.GUID,
                GoalGuid = goalGUID,
                Track = track
            });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GoalsManager), "ClaimGoalReward")]
        private static void ClaimGoalRewardPrefix(
            global::AssetCharacter character,
            ulong goalGUID,
            ulong rewardGUID,
            out bool __state)
        {
            __state = IsRewardClaimed(character, goalGUID, rewardGUID);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::GoalsManager), "ClaimGoalReward")]
        private static void ClaimGoalRewardPostfix(
            global::AssetCharacter character,
            ulong goalGUID,
            ulong rewardGUID,
            bool __state)
        {
            if (character == null || __state || !IsRewardClaimed(character, goalGUID, rewardGUID))
                return;

            ParalivesRuntimeInfo.Current.Goals.PublishChanged(new ParalivesGoalChangedEvent
            {
                ChangeType = ParalivesGoalChangeType.RewardClaimed,
                CharacterGuid = character.GUID,
                GoalGuid = goalGUID,
                RewardGuid = rewardGUID
            });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GoalsManager), "CompleteWantInGoal")]
        private static void CompleteWantInGoalPrefix(
            global::AssetCharacter character,
            ulong goalGUID,
            ulong objectiveGUID,
            out bool __state)
        {
            __state = IsObjectiveComplete(character, goalGUID, objectiveGUID);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::GoalsManager), "CompleteWantInGoal")]
        private static void CompleteWantInGoalPostfix(
            global::AssetCharacter character,
            ulong goalGUID,
            ulong objectiveGUID,
            bool __state)
        {
            if (character == null || __state || !IsObjectiveComplete(character, goalGUID, objectiveGUID))
                return;

            ParalivesRuntimeInfo.Current.Goals.PublishChanged(new ParalivesGoalChangedEvent
            {
                ChangeType = ParalivesGoalChangeType.WantObjectiveCompleted,
                CharacterGuid = character.GUID,
                GoalGuid = goalGUID,
                ObjectiveGuid = objectiveGUID
            });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GoalsManager), "CancelRequestOrGoal")]
        private static void CancelRequestOrGoalPrefix(
            global::AssetCharacter character,
            ulong goalOrRequestGUID,
            ulong requesterGUID,
            out bool __state)
        {
            __state = FindGoalData(character, goalOrRequestGUID, requesterGUID) != null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::GoalsManager), "CancelRequestOrGoal")]
        private static void CancelRequestOrGoalPostfix(
            global::AssetCharacter character,
            ulong goalOrRequestGUID,
            ulong requesterGUID,
            bool __state)
        {
            if (character == null || !__state || FindGoalData(character, goalOrRequestGUID, requesterGUID) != null)
                return;

            ParalivesRuntimeInfo.Current.Goals.PublishChanged(new ParalivesGoalChangedEvent
            {
                ChangeType = ParalivesGoalChangeType.Cancelled,
                CharacterGuid = character.GUID,
                GoalGuid = goalOrRequestGUID,
                RequesterGuid = requesterGUID
            });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GoalsManager), "TurnInRequest")]
        private static void TurnInRequestPrefix(
            global::AssetCharacter character,
            ulong requestGUID,
            ulong requesterGUID,
            out bool __state)
        {
            __state = FindGoalData(character, requestGUID, requesterGUID) != null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::GoalsManager), "TurnInRequest")]
        private static void TurnInRequestPostfix(
            global::AssetCharacter character,
            ulong requestGUID,
            ulong requesterGUID,
            bool __state)
        {
            if (character == null || !__state || FindGoalData(character, requestGUID, requesterGUID) != null)
                return;

            ParalivesRuntimeInfo.Current.Goals.PublishChanged(new ParalivesGoalChangedEvent
            {
                ChangeType = ParalivesGoalChangeType.RequestTurnedIn,
                CharacterGuid = character.GUID,
                GoalGuid = requestGUID,
                RequesterGuid = requesterGUID
            });
        }

        private static int GetGoalCount(global::AssetCharacter character)
        {
            return character != null
                && character.Data != null
                && character.Data.GoalsSaveData != null
                    ? character.Data.GoalsSaveData.Count
                    : 0;
        }

        private static global::AssetCharacterGoalData FindGoalData(
            global::AssetCharacter character,
            ulong goalGuid,
            ulong requesterGuid)
        {
            if (character == null || character.Data == null || character.Data.GoalsSaveData == null)
                return null;

            for (int i = character.Data.GoalsSaveData.Count - 1; i >= 0; i--)
            {
                global::AssetCharacterGoalData data = character.Data.GoalsSaveData[i];
                if (data == null || data.GoalGUID != goalGuid)
                    continue;

                if (requesterGuid == 0UL || data.OfferedBy == requesterGuid)
                    return data;
            }

            return null;
        }

        private static bool IsRewardClaimed(global::AssetCharacter character, ulong goalGuid, ulong rewardGuid)
        {
            global::AssetCharacterGoalData data = FindGoalData(character, goalGuid, 0UL);
            return data != null && data.ClaimedRewards != null && data.ClaimedRewards.Contains(rewardGuid);
        }

        private static bool IsObjectiveComplete(global::AssetCharacter character, ulong goalGuid, ulong objectiveGuid)
        {
            global::AssetCharacterGoalData data = FindGoalData(character, goalGuid, 0UL);
            if (data == null || data.Objectives == null)
                return false;

            for (int i = 0; i < data.Objectives.Count; i++)
            {
                global::AssetCharacterGoalObjectiveData objective = data.Objectives[i];
                if (objective != null
                    && objective.GUID == objectiveGuid
                    && objective.WantData != null
                    && objective.WantData.Status == global::AssetCharacterWantStatus.Completed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
