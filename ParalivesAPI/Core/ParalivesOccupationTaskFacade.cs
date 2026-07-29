using System;
using System.Collections.Generic;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesOccupationTaskDefinition
    {
        public ulong OccupationGuid { get; set; }

        public ulong TaskGuid { get; set; }

        public ulong SkillGuid { get; set; }

        public ulong CharacterTargetGuid { get; set; }

        public ulong BrainLogicGuid { get; set; }

        public ulong SkinGuid { get; set; }

        public ulong CatalogueGuid { get; set; }

        public bool DoesNotCount { get; set; }

        public bool MatchSkillGuid { get; set; }
    }

    public sealed class ParalivesOccupationTaskEntry
    {
        public ulong CharacterGuid { get; internal set; }

        public int TaskIndex { get; internal set; }

        public ulong TaskGuid { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public bool HasActiveOccupation { get; internal set; }

        public bool IsKnownOccupation { get; internal set; }

        public string DisplayName { get; internal set; }

        public string FullName { get; internal set; }

        public ulong BrainLogicGuid { get; internal set; }

        public float Timestamp { get; internal set; }

        public float Progress { get; internal set; }

        public float Goal { get; internal set; }

        public ulong CharacterTargetGuid { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public ulong SkinGuid { get; internal set; }

        public ulong CatalogueGuid { get; internal set; }

        public bool IsPinned { get; internal set; }

        public bool DoesNotCount { get; internal set; }

        public bool HasPlayedAppearAnimation { get; internal set; }

        public int Status { get; internal set; }

        public bool IsActive { get; internal set; }

        public float ClearTimestamp { get; internal set; }
    }

    public sealed class ParalivesOccupationTaskAssignmentResult
    {
        public bool Succeeded { get; internal set; }

        public bool WasCreated { get; internal set; }

        public bool WasRefreshed { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public ulong TaskGuid { get; internal set; }

        public int TaskIndex { get; internal set; }

        public ParalivesOccupationTaskEntry Task { get; internal set; }

        public string Message { get; internal set; }
    }

    public sealed class ParalivesOccupationTaskCompletionResult
    {
        public bool Succeeded { get; internal set; }

        public int MatchedCount { get; internal set; }

        public int CompletedCount { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public ulong TaskGuid { get; internal set; }

        public ParalivesOccupationTaskEntry Task { get; internal set; }

        public string Message { get; internal set; }
    }

    public sealed class ParalivesOccupationTaskFacade
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesOccupationFacade _occupations;
        private readonly ParalivesWantFacade _wants;

        internal ParalivesOccupationTaskFacade(
            ParalivesCharacterFacade characters,
            ParalivesOccupationFacade occupations,
            ParalivesWantFacade wants)
        {
            if (characters == null)
                throw new ArgumentNullException("characters");
            if (occupations == null)
                throw new ArgumentNullException("occupations");
            if (wants == null)
                throw new ArgumentNullException("wants");

            _characters = characters;
            _occupations = occupations;
            _wants = wants;
        }

        public ParalivesOccupationTaskEntry[] ReadActiveTasks(ulong characterGuid)
        {
            return ReadActiveTasksCore(characterGuid, 0UL, false);
        }

        public ParalivesOccupationTaskEntry[] ReadActiveTasks(ulong characterGuid, ulong occupationGuid)
        {
            if (occupationGuid == 0UL)
                return new ParalivesOccupationTaskEntry[0];

            return ReadActiveTasksCore(characterGuid, occupationGuid, true);
        }

        public ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid)
        {
            return AssignTask(characterGuid, occupationGuid, taskGuid, 0UL);
        }

        public ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid,
            ulong skillGuid)
        {
            return AssignTask(
                characterGuid,
                new ParalivesOccupationTaskDefinition
                {
                    OccupationGuid = occupationGuid,
                    TaskGuid = taskGuid,
                    SkillGuid = skillGuid
                });
        }

        public ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ParalivesOccupationTaskDefinition definition)
        {
            ParalivesOccupationTaskAssignmentResult result = CreateAssignmentResult(characterGuid, definition);
            if (definition == null)
            {
                result.Message = "Task definition is required.";
                return result;
            }

            if (!ValidateCharacter(characterGuid, result))
                return result;
            if (!ValidateTaskIdentity(definition.OccupationGuid, definition.TaskGuid, result))
                return result;

            string displayName;
            if (!_wants.TryGetWantDisplayName(definition.TaskGuid, out displayName))
            {
                result.Message = "Task backing want is not registered or settings are not ready.";
                return result;
            }

            ParalivesOccupationTaskEntry existing;
            bool hadMatchingTask = TryFindActiveTask(
                characterGuid,
                definition.OccupationGuid,
                definition.TaskGuid,
                definition.MatchSkillGuid,
                definition.SkillGuid,
                out existing);

            int taskIndex;
            bool assigned = _wants.CreateOrRefreshActiveWant(
                characterGuid,
                definition.TaskGuid,
                definition.OccupationGuid,
                definition.SkillGuid,
                definition.CharacterTargetGuid,
                definition.BrainLogicGuid,
                definition.SkinGuid,
                definition.CatalogueGuid,
                definition.DoesNotCount,
                definition.MatchSkillGuid,
                out taskIndex);

            if (!assigned)
            {
                result.Message = "Task could not be assigned.";
                return result;
            }

            result.Succeeded = true;
            result.WasCreated = !hadMatchingTask;
            result.WasRefreshed = hadMatchingTask;
            result.TaskIndex = taskIndex;
            result.Task = FindActiveTaskByIndex(characterGuid, taskIndex);
            result.Message = hadMatchingTask ? "Task refreshed." : "Task assigned.";
            return result;
        }

        public ParalivesOccupationTaskCompletionResult CompleteTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid)
        {
            return CompleteCore(
                characterGuid,
                occupationGuid,
                taskGuid,
                0UL,
                0UL,
                null,
                false);
        }

        public ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid)
        {
            return CompleteMatchingTask(characterGuid, occupationGuid, 0UL);
        }

        public ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong skillGuid)
        {
            return CompleteCore(
                characterGuid,
                occupationGuid,
                0UL,
                skillGuid,
                0UL,
                null,
                false);
        }

        public ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong skillGuid,
            ulong characterTargetGuid)
        {
            return CompleteCore(
                characterGuid,
                occupationGuid,
                0UL,
                skillGuid,
                characterTargetGuid,
                null,
                false);
        }

        public ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            Predicate<ParalivesOccupationTaskEntry> predicate)
        {
            return CompleteCore(
                characterGuid,
                occupationGuid,
                0UL,
                0UL,
                0UL,
                predicate,
                false);
        }

        public ParalivesOccupationTaskCompletionResult CompleteAllMatchingTasks(
            ulong characterGuid,
            ulong occupationGuid,
            Predicate<ParalivesOccupationTaskEntry> predicate)
        {
            return CompleteCore(
                characterGuid,
                occupationGuid,
                0UL,
                0UL,
                0UL,
                predicate,
                true);
        }

        private ParalivesOccupationTaskEntry[] ReadActiveTasksCore(
            ulong characterGuid,
            ulong occupationGuid,
            bool requireOccupation)
        {
            List<ParalivesOccupationTaskEntry> tasks = new List<ParalivesOccupationTaskEntry>();
            ParalivesWantEntry[] wants = _wants.ReadActiveWants(characterGuid);
            for (int i = 0; i < wants.Length; i++)
            {
                ParalivesWantEntry want = wants[i];
                if (want.OccupationGuid == 0UL)
                    continue;
                if (requireOccupation && want.OccupationGuid != occupationGuid)
                    continue;

                tasks.Add(CreateEntry(want));
            }

            return tasks.ToArray();
        }

        private ParalivesOccupationTaskCompletionResult CompleteCore(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid,
            ulong skillGuid,
            ulong characterTargetGuid,
            Predicate<ParalivesOccupationTaskEntry> predicate,
            bool completeAll)
        {
            ParalivesOccupationTaskCompletionResult result = CreateCompletionResult(
                characterGuid,
                occupationGuid,
                taskGuid);

            if (!ValidateCharacter(characterGuid, result))
                return result;
            if (!ValidateCompletionIdentity(occupationGuid, taskGuid, result))
                return result;

            ParalivesOccupationTaskEntry[] tasks = ReadActiveTasks(characterGuid, occupationGuid);
            for (int i = 0; i < tasks.Length; i++)
            {
                ParalivesOccupationTaskEntry task = tasks[i];
                if (taskGuid != 0UL && task.TaskGuid != taskGuid)
                    continue;
                if (skillGuid != 0UL && task.SkillGuid != skillGuid)
                    continue;
                if (characterTargetGuid != 0UL && task.CharacterTargetGuid != characterTargetGuid)
                    continue;
                if (predicate != null && !predicate(task))
                    continue;

                result.MatchedCount++;
                if (_wants.TryCompleteWant(characterGuid, task.TaskIndex))
                {
                    result.CompletedCount++;
                    if (result.Task == null)
                        result.Task = task;
                }

                if (!completeAll)
                    break;
            }

            result.Succeeded = result.CompletedCount > 0;
            if (result.Succeeded)
                result.Message = result.CompletedCount == 1 ? "Task completed." : "Tasks completed.";
            else if (result.MatchedCount > 0)
                result.Message = "Matching task could not be completed.";
            else
                result.Message = "No matching active occupation task was found.";

            return result;
        }

        private bool TryFindActiveTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid,
            bool matchSkillGuid,
            ulong skillGuid,
            out ParalivesOccupationTaskEntry task)
        {
            task = null;
            ParalivesOccupationTaskEntry[] tasks = ReadActiveTasks(characterGuid, occupationGuid);
            for (int i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].TaskGuid != taskGuid)
                    continue;
                if (matchSkillGuid && tasks[i].SkillGuid != skillGuid)
                    continue;

                task = tasks[i];
                return true;
            }

            return false;
        }

        private ParalivesOccupationTaskEntry FindActiveTaskByIndex(ulong characterGuid, int taskIndex)
        {
            ParalivesOccupationTaskEntry[] tasks = ReadActiveTasks(characterGuid);
            for (int i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].TaskIndex == taskIndex)
                    return tasks[i];
            }

            return null;
        }

        private ParalivesOccupationTaskEntry CreateEntry(ParalivesWantEntry want)
        {
            int occupationIndex = -1;
            global::AssetCharacterOccupationData occupationData;
            global::Setting.Occupation occupation;
            bool hasActiveOccupation = _occupations.TryFindActiveOccupation(
                want.CharacterGuid,
                want.OccupationGuid,
                out occupationIndex,
                out occupationData,
                out occupation);

            return new ParalivesOccupationTaskEntry
            {
                CharacterGuid = want.CharacterGuid,
                TaskIndex = want.Index,
                TaskGuid = want.WantGuid,
                OccupationGuid = want.OccupationGuid,
                OccupationIndex = occupationIndex,
                HasActiveOccupation = hasActiveOccupation,
                IsKnownOccupation = occupation != null,
                DisplayName = want.DisplayName ?? string.Empty,
                FullName = want.FullName ?? string.Empty,
                BrainLogicGuid = want.BrainLogicGuid,
                Timestamp = want.Timestamp,
                Progress = want.Progress,
                Goal = want.Goal,
                CharacterTargetGuid = want.CharacterTargetGuid,
                SkillGuid = want.SkillGuid,
                SkinGuid = want.SkinGuid,
                CatalogueGuid = want.CatalogueGuid,
                IsPinned = want.IsPinned,
                DoesNotCount = want.DoesNotCount,
                HasPlayedAppearAnimation = want.HasPlayedAppearAnimation,
                Status = (int)want.Status,
                IsActive = want.Status == global::AssetCharacterWantStatus.Active,
                ClearTimestamp = want.ClearTimestamp
            };
        }

        private ParalivesOccupationTaskAssignmentResult CreateAssignmentResult(
            ulong characterGuid,
            ParalivesOccupationTaskDefinition definition)
        {
            return new ParalivesOccupationTaskAssignmentResult
            {
                CharacterGuid = characterGuid,
                OccupationGuid = definition == null ? 0UL : definition.OccupationGuid,
                TaskGuid = definition == null ? 0UL : definition.TaskGuid,
                TaskIndex = -1,
                Message = string.Empty
            };
        }

        private static ParalivesOccupationTaskCompletionResult CreateCompletionResult(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid)
        {
            return new ParalivesOccupationTaskCompletionResult
            {
                CharacterGuid = characterGuid,
                OccupationGuid = occupationGuid,
                TaskGuid = taskGuid,
                Message = string.Empty
            };
        }

        private bool ValidateCharacter(ulong characterGuid, ParalivesOccupationTaskAssignmentResult result)
        {
            global::AssetCharacter character;
            if (_characters.TryGet(characterGuid, out character) && character != null && character.Data != null)
                return true;

            result.Message = "Character was not found or is not loaded.";
            return false;
        }

        private bool ValidateCharacter(ulong characterGuid, ParalivesOccupationTaskCompletionResult result)
        {
            global::AssetCharacter character;
            if (_characters.TryGet(characterGuid, out character) && character != null && character.Data != null)
                return true;

            result.Message = "Character was not found or is not loaded.";
            return false;
        }

        private bool ValidateTaskIdentity(
            ulong occupationGuid,
            ulong taskGuid,
            ParalivesOccupationTaskAssignmentResult result)
        {
            if (occupationGuid == 0UL)
            {
                result.Message = "Occupation GUID is required.";
                return false;
            }

            if (taskGuid == 0UL)
            {
                result.Message = "Task GUID is required.";
                return false;
            }

            return true;
        }

        private bool ValidateCompletionIdentity(
            ulong occupationGuid,
            ulong taskGuid,
            ParalivesOccupationTaskCompletionResult result)
        {
            if (occupationGuid == 0UL)
            {
                result.Message = "Occupation GUID is required.";
                return false;
            }

            if (taskGuid == 0UL)
                return true;

            string displayName;
            if (_wants.TryGetWantDisplayName(taskGuid, out displayName))
                return true;

            result.Message = "Task backing want is not registered or settings are not ready.";
            return false;
        }
    }
}
