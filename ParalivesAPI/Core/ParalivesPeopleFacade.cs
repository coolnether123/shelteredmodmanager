using System.Collections.Generic;
using Setting;
using UnityEngine;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesPersonActivitySnapshot
    {
        public ulong CharacterGuid { get; internal set; }

        public bool HasQueuedInteractions { get; internal set; }

        public int QueueCount { get; internal set; }

        public int QueueIndex { get; internal set; }

        public ulong InteractionInstanceGuid { get; internal set; }

        public ulong InteractionSettingGuid { get; internal set; }

        public int InteractionState { get; internal set; }

        public ulong CurrentActionGuid { get; internal set; }

        public int CurrentActionState { get; internal set; }

        public int CurrentActionIndex { get; internal set; }

        public int CurrentActionCount { get; internal set; }

        public ulong OwnerCharacterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public ulong LotGuid { get; internal set; }

        public int ItemInstanceId { get; internal set; }

        public int TargetItemInstanceId { get; internal set; }

        public int UsedItemInstanceId { get; internal set; }

        public bool IsFromAutonomy { get; internal set; }

        public bool IsCancelling { get; internal set; }
    }

    public sealed class ParalivesPersonSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public string ShortName { get; internal set; }

        public float Age { get; internal set; }

        public ulong GenderGuid { get; internal set; }

        public ulong SpeciesGuid { get; internal set; }

        public bool IsSelected { get; internal set; }

        public int SelectingPlayerIndex { get; internal set; }

        public bool IsInCurrentHousehold { get; internal set; }

        public bool IsInHousehold { get; internal set; }

        public ulong HouseholdGuid { get; internal set; }

        public ulong[] HouseholdCharacterGuids { get; internal set; }

        public bool IsDead { get; internal set; }

        public bool IsTakenAway { get; internal set; }

        public bool IsDeadOrTakenAway { get; internal set; }

        public bool IsUnselectable { get; internal set; }

        public bool IsAvailableForGameplay { get; internal set; }

        public bool IsVisualLoaded { get; internal set; }

        public bool IsVisibleInWorld { get; internal set; }

        public bool IsDummy { get; internal set; }

        public bool DoNotLoadVisual { get; internal set; }

        public bool IsAtDaycare { get; internal set; }

        public ulong CurrentRabbitHoleGuid { get; internal set; }

        public ulong CurrentLotGuid { get; internal set; }

        public Vector3 Position { get; internal set; }

        public Quaternion Rotation { get; internal set; }

        public ulong LifeStageGuid { get; internal set; }

        public string LifeStageDisplayName { get; internal set; }

        public int CurrentOccupationIndex { get; internal set; }

        public int OccupationIndexToGoTo { get; internal set; }

        public int CurrentlyAffectedOccupationIndex { get; internal set; }

        public int[] ActiveOccupationIndexes { get; internal set; }

        public int[] ActiveSchoolIndexes { get; internal set; }

        public ulong[] CharacterRequirementsMet { get; internal set; }

        public ulong QueueDigest { get; internal set; }

        public ParalivesPersonActivitySnapshot Activity { get; internal set; }
    }

    public sealed class ParalivesPeopleFacade
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesPlayerFacade _players;
        private readonly ParalivesInteractionQueueFacade _queues;
        private readonly ParalivesOccupationFacade _occupations;

        internal ParalivesPeopleFacade(
            ParalivesCharacterFacade characters,
            ParalivesPlayerFacade players,
            ParalivesInteractionQueueFacade queues,
            ParalivesOccupationFacade occupations)
        {
            _characters = characters;
            _players = players;
            _queues = queues;
            _occupations = occupations;
        }

        public ParalivesPersonSnapshot Read(ulong characterGuid)
        {
            ParalivesPersonSnapshot snapshot;
            return TryRead(characterGuid, out snapshot) ? snapshot : CreateMissing(characterGuid);
        }

        public bool TryRead(ulong characterGuid, out ParalivesPersonSnapshot snapshot)
        {
            snapshot = CreateMissing(characterGuid);

            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character) || character.Data == null)
                return false;

            snapshot.Exists = true;
            snapshot.CharacterGuid = character.GUID;
            snapshot.DisplayName = _characters.GetDisplayName(character);
            snapshot.ShortName = character.Data.ShortName ?? string.Empty;
            snapshot.Age = character.Data.Age;
            snapshot.GenderGuid = character.Data.Gender;
            snapshot.SpeciesGuid = character.Data.CurrentSpeciesGUID;
            snapshot.SelectingPlayerIndex = FindSelectingPlayerIndex(character.GUID);
            snapshot.IsSelected = snapshot.SelectingPlayerIndex >= 0;
            snapshot.IsInCurrentHousehold = _characters.IsInCurrentHousehold(character.GUID);
            snapshot.IsInHousehold = character.IsInHousehold;
            snapshot.HouseholdGuid = character.Household == null ? 0UL : character.Household.GUID;
            snapshot.HouseholdCharacterGuids = _characters.GetHouseholdCharacterGuids(character.GUID);
            snapshot.IsDead = character.Data.IsDead;
            snapshot.IsTakenAway = character.Data.TakenAwayBySocialServices;
            snapshot.IsDeadOrTakenAway = character.Data.IsDeadOrTakenAway;
            snapshot.IsUnselectable = character.Data.IsUnselectable;
            snapshot.IsAvailableForGameplay = _characters.IsAvailableForGameplay(character);
            snapshot.IsVisualLoaded = character.IsVisualLoaded;
            snapshot.IsVisibleInWorld = character.IsVisibleInWorld;
            snapshot.IsDummy = character.IsDummy;
            snapshot.DoNotLoadVisual = character.DoNotLoadVisual;
            snapshot.IsAtDaycare = character.Data.IsAtDaycare;
            snapshot.CurrentRabbitHoleGuid = character.Data.CurrentRabbitHole;
            snapshot.Position = character.Data.Position;
            snapshot.Rotation = character.Data.Rotation;
            snapshot.CurrentOccupationIndex = character.Data.CurrentOccupationIndex;
            snapshot.OccupationIndexToGoTo = character.Data.OccupationIndexToGoTo;
            snapshot.CurrentlyAffectedOccupationIndex = character.Data.CurrentlyAffectedOccupationIndex;
            snapshot.ActiveOccupationIndexes = _occupations.GetActiveOccupationIndexes(character);
            snapshot.ActiveSchoolIndexes = _occupations.GetActiveSchoolIndexes(character);
            snapshot.CharacterRequirementsMet = character.CharacterRequirementsMet == null
                ? new ulong[0]
                : character.CharacterRequirementsMet.ToArray();
            snapshot.QueueDigest = _queues.BuildQueueDigest(character);
            snapshot.Activity = ReadActivity(character.GUID);

            ulong lotGuid;
            snapshot.CurrentLotGuid = _characters.TryGetCurrentLotGuid(character.GUID, out lotGuid) ? lotGuid : 0UL;

            LifeStage lifeStage;
            if (_characters.TryGetLifeStage(character.GUID, out lifeStage))
            {
                snapshot.LifeStageGuid = lifeStage.GUID;
                snapshot.LifeStageDisplayName = lifeStage.DisplayName ?? string.Empty;
            }

            return true;
        }

        public ParalivesPersonSnapshot[] ReadCurrentHousehold()
        {
            ulong[] guids = _characters.GetCurrentHouseholdCharacterGuids();
            List<ParalivesPersonSnapshot> snapshots = new List<ParalivesPersonSnapshot>();
            for (int i = 0; i < guids.Length; i++)
            {
                ParalivesPersonSnapshot snapshot;
                if (TryRead(guids[i], out snapshot))
                    snapshots.Add(snapshot);
            }

            return snapshots.ToArray();
        }

        public ParalivesPersonActivitySnapshot ReadActivity(ulong characterGuid)
        {
            ParalivesInteractionQueueEntry[] queue = _queues.ReadQueue(characterGuid);
            ParalivesPersonActivitySnapshot snapshot = new ParalivesPersonActivitySnapshot
            {
                CharacterGuid = characterGuid,
                QueueCount = queue.Length,
                QueueIndex = -1,
                HasQueuedInteractions = queue.Length > 0
            };

            ParalivesInteractionQueueEntry current = PickCurrent(queue);
            if (current == null)
                return snapshot;

            snapshot.QueueIndex = current.QueueIndex;
            snapshot.InteractionInstanceGuid = current.InteractionInstanceGuid;
            snapshot.InteractionSettingGuid = current.InteractionSettingGuid;
            snapshot.InteractionState = current.State;
            snapshot.CurrentActionGuid = current.CurrentActionGuid;
            snapshot.CurrentActionState = current.CurrentActionState;
            snapshot.CurrentActionIndex = current.CurrentActionIndex;
            snapshot.CurrentActionCount = current.CurrentActionCount;
            snapshot.OwnerCharacterGuid = current.OwnerCharacterGuid;
            snapshot.TargetCharacterGuid = current.TargetCharacterGuid;
            snapshot.LotGuid = current.LotGuid;
            snapshot.ItemInstanceId = current.ItemInstanceId;
            snapshot.TargetItemInstanceId = current.TargetItemInstanceId;
            snapshot.UsedItemInstanceId = current.UsedItemInstanceId;
            snapshot.IsFromAutonomy = current.IsFromAutonomy;
            snapshot.IsCancelling = current.IsCancelling;
            return snapshot;
        }

        private static ParalivesPersonSnapshot CreateMissing(ulong characterGuid)
        {
            return new ParalivesPersonSnapshot
            {
                CharacterGuid = characterGuid,
                DisplayName = string.Empty,
                ShortName = string.Empty,
                SelectingPlayerIndex = -1,
                HouseholdCharacterGuids = new ulong[0],
                ActiveOccupationIndexes = new int[0],
                ActiveSchoolIndexes = new int[0],
                CharacterRequirementsMet = new ulong[0],
                Activity = new ParalivesPersonActivitySnapshot
                {
                    CharacterGuid = characterGuid,
                    QueueIndex = -1
                }
            };
        }

        private int FindSelectingPlayerIndex(ulong characterGuid)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                ulong[] selected = _players.GetSelectedCharacterGuids(i);
                for (int j = 0; j < selected.Length; j++)
                {
                    if (selected[j] == characterGuid)
                        return i;
                }
            }

            return -1;
        }

        private static ParalivesInteractionQueueEntry PickCurrent(ParalivesInteractionQueueEntry[] queue)
        {
            if (queue == null || queue.Length == 0)
                return null;

            int running = (int)global::AssetCharacterDataInteractionState.Running;
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] != null && queue[i].State == running)
                    return queue[i];
            }

            int toBeDeleted = (int)global::AssetCharacterDataInteractionState.ToBeDeleted;
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] != null && queue[i].State != toBeDeleted)
                    return queue[i];
            }

            return queue[0];
        }
    }
}
