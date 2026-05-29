using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesGoalObjectiveSnapshot
    {
        public ulong ObjectiveGuid { get; internal set; }

        public float Progress { get; internal set; }

        public global::AssetCharacterWantData WantData { get; internal set; }
    }

    public sealed class ParalivesGoalSnapshot
    {
        public ulong CharacterGuid { get; internal set; }

        public int Index { get; internal set; }

        public ulong GoalGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public bool IsKnownGoal { get; internal set; }

        public GoalType GoalType { get; internal set; }

        public ulong OfferedByGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public bool HasBeenRead { get; internal set; }

        public bool HasBeenCompleted { get; internal set; }

        public bool IsCompletedAndAllRewardsClaimed { get; internal set; }

        public bool IsTracked { get; internal set; }

        public bool IsCompleted { get; internal set; }

        public ParalivesGoalObjectiveSnapshot[] Objectives { get; internal set; }

        public ulong[] ClaimedRewardGuids { get; internal set; }
    }

    public sealed class ParalivesCurrentRequestSnapshot
    {
        public ulong RequestGuid { get; internal set; }

        public ulong RequesterGuid { get; internal set; }
    }

    public sealed class ParalivesGoalFacade
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesSettingsFacade _settings;

        public event System.Action<ParalivesGoalChangedEvent> GoalChanged;

        internal ParalivesGoalFacade(ParalivesCharacterFacade characters, ParalivesSettingsFacade settings)
        {
            _characters = characters;
            _settings = settings;
        }

        public ParalivesGoalSnapshot[] ReadGoals(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? ReadGoals(character)
                : new ParalivesGoalSnapshot[0];
        }

        public ParalivesGoalSnapshot[] ReadGoals(global::AssetCharacter character)
        {
            List<ParalivesGoalSnapshot> goals = new List<ParalivesGoalSnapshot>();
            if (character == null || character.Data == null || character.Data.GoalsSaveData == null)
                return goals.ToArray();

            for (int i = 0; i < character.Data.GoalsSaveData.Count; i++)
            {
                global::AssetCharacterGoalData data = character.Data.GoalsSaveData[i];
                if (data != null)
                    goals.Add(CreateSnapshot(character, i, data));
            }

            return goals.ToArray();
        }

        public ParalivesCurrentRequestSnapshot[] ReadCurrentRequests()
        {
            List<ParalivesCurrentRequestSnapshot> requests = new List<ParalivesCurrentRequestSnapshot>();
            try
            {
                for (int i = 0; i < global::GoalsManager.Instance.CurrentRequests.Count; i++)
                {
                    var request = global::GoalsManager.Instance.CurrentRequests[i];
                    requests.Add(new ParalivesCurrentRequestSnapshot
                    {
                        RequestGuid = request.Item1,
                        RequesterGuid = request.Item2
                    });
                }
            }
            catch
            {
            }

            return requests.ToArray();
        }

        public bool TryAddGoal(ulong characterGuid, ulong goalGuid)
        {
            return TryAddGoal(characterGuid, goalGuid, 0UL, 0UL);
        }

        public bool TryAddGoal(ulong characterGuid, ulong goalGuid, ulong requesterGuid, ulong targetGuid)
        {
            global::AssetCharacter character;
            if (goalGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::GoalsManager.Instance.AddGoalToCharacter(character, goalGuid, requesterGuid, targetGuid);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TrySetTracked(ulong characterGuid, ulong goalGuid, bool track)
        {
            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::GoalsManager.Instance.SetTrackedGoal(character, goalGuid, track);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryClaimReward(ulong characterGuid, ulong goalGuid, ulong rewardGuid)
        {
            global::AssetCharacter character;
            if (goalGuid == 0UL || rewardGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::GoalsManager.Instance.ClaimGoalReward(character, goalGuid, rewardGuid);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryCompleteWantObjective(ulong characterGuid, ulong goalGuid, ulong objectiveGuid)
        {
            global::AssetCharacter character;
            if (goalGuid == 0UL || objectiveGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::GoalsManager.Instance.CompleteWantInGoal(character, goalGuid, objectiveGuid);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryCancelRequestOrGoal(ulong characterGuid, ulong goalOrRequestGuid, ulong requesterGuid)
        {
            global::AssetCharacter character;
            if (goalOrRequestGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::GoalsManager.Instance.CancelRequestOrGoal(character, goalOrRequestGuid, requesterGuid);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryTurnInRequest(ulong characterGuid, ulong requestGuid, ulong requesterGuid)
        {
            global::AssetCharacter character;
            if (requestGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::GoalsManager.Instance.TurnInRequest(character, requestGuid, requesterGuid);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsCompleted(ulong characterGuid, ulong goalGuid)
        {
            global::AssetCharacter character;
            if (goalGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                return global::GoalsManager.Instance.IsGoalCompleted(character, goalGuid);
            }
            catch
            {
                return false;
            }
        }

        internal void PublishChanged(ParalivesGoalChangedEvent evt)
        {
            if (evt == null)
                return;

            System.Action<ParalivesGoalChangedEvent> handler = GoalChanged;
            if (handler == null)
                return;

            try
            {
                handler(evt);
            }
            catch
            {
            }
        }

        private ParalivesGoalSnapshot CreateSnapshot(
            global::AssetCharacter character,
            int index,
            global::AssetCharacterGoalData data)
        {
            Goals goalsSetting;
            Goal goal = null;
            if (_settings.TryGet<Goals>(out goalsSetting))
                goal = goalsSetting.GetGoalByGUID(data.GoalGUID);

            List<ParalivesGoalObjectiveSnapshot> objectives = new List<ParalivesGoalObjectiveSnapshot>();
            if (data.Objectives != null)
            {
                for (int i = 0; i < data.Objectives.Count; i++)
                {
                    global::AssetCharacterGoalObjectiveData objectiveData = data.Objectives[i];
                    if (objectiveData == null)
                        continue;

                    float progress = 0f;
                    try
                    {
                        GoalObjective objective = goalsSetting == null
                            ? null
                            : goalsSetting.GetGoalObjectiveByGUID(data.GoalGUID, objectiveData.GUID);
                        if (objective != null)
                            progress = global::GoalsManager.Instance.GetGoalObjectiveProgress(objective, objectiveData);
                    }
                    catch
                    {
                    }

                    objectives.Add(new ParalivesGoalObjectiveSnapshot
                    {
                        ObjectiveGuid = objectiveData.GUID,
                        Progress = progress,
                        WantData = objectiveData.WantData
                    });
                }
            }

            return new ParalivesGoalSnapshot
            {
                CharacterGuid = character.GUID,
                Index = index,
                GoalGuid = data.GoalGUID,
                DisplayName = goal == null ? string.Empty : (goal.DisplayName ?? string.Empty),
                IsKnownGoal = goal != null,
                GoalType = goal == null ? default(GoalType) : goal.GoalType,
                OfferedByGuid = data.OfferedBy,
                TargetCharacterGuid = data.TargetCharacter,
                HasBeenRead = data.HasBeenRead,
                HasBeenCompleted = data.HasBeenCompleted,
                IsCompletedAndAllRewardsClaimed = data.IsCompletedAndAllRewardsClaimed,
                IsTracked = character.Data != null && character.Data.TrackedGoal == data.GoalGUID,
                IsCompleted = IsCompleted(character.GUID, data.GoalGUID),
                Objectives = objectives.ToArray(),
                ClaimedRewardGuids = data.ClaimedRewards == null ? new ulong[0] : data.ClaimedRewards.ToArray()
            };
        }
    }
}
