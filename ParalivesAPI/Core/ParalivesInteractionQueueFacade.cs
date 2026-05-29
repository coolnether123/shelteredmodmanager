using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesInteractionQueueEntry
    {
        public ulong CharacterGuid { get; internal set; }

        public int QueueIndex { get; internal set; }

        public ulong InteractionInstanceGuid { get; internal set; }

        public ulong InteractionSettingGuid { get; internal set; }

        public int State { get; internal set; }

        public ulong OwnerCharacterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public int ItemInstanceId { get; internal set; }

        public int TargetItemInstanceId { get; internal set; }

        public int UsedItemInstanceId { get; internal set; }

        public int CreatedItemInstanceId { get; internal set; }

        public ulong LotGuid { get; internal set; }

        public ulong CurrentActionGuid { get; internal set; }

        public int CurrentActionState { get; internal set; }

        public int CurrentActionIndex { get; internal set; }

        public int CurrentActionCount { get; internal set; }

        public ulong SocialGroupGuid { get; internal set; }

        public ulong SocialGroupClusterGuid { get; internal set; }

        public ulong ParentInteractionInstanceGuid { get; internal set; }

        public ulong ChildInteractionInstanceGuid { get; internal set; }

        public ulong SkinGuid { get; internal set; }

        public bool IsFromAutonomy { get; internal set; }

        public bool IsCancelling { get; internal set; }
    }

    public sealed class ParalivesInteractionInjectionRequest
    {
        public ulong ActorCharacterGuid { get; set; }

        public ulong InteractionGuid { get; set; }

        public ulong TargetCharacterGuid { get; set; }

        public InjectionPriority Priority { get; set; }

        public TargetPositionOfInjectedInteraction TargetPosition { get; set; }

        public TargetOtherCharacterOfInjectedInteraction TargetOtherCharacter { get; set; }

        public bool InjectItemSlotAsTargetItem { get; set; }

        public bool InjectOtherCharacterAsTargetCharacter { get; set; }

        public bool IsIdleAutonomous { get; set; }

        public bool IsForcedAutonomous { get; set; }

        public ulong SkinGuid { get; set; }

        public ulong LotGuid { get; set; }

        public ParalivesInteractionInjectionRequest()
        {
            Priority = InjectionPriority.AtTheEndOfInteractionQueue;
            TargetPosition = TargetPositionOfInjectedInteraction.None;
            InjectItemSlotAsTargetItem = true;
            InjectOtherCharacterAsTargetCharacter = true;
        }
    }

    public sealed class ParalivesInteractionInjectionResult
    {
        public bool Queued { get; internal set; }

        public ulong ActorCharacterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public ulong InteractionGuid { get; internal set; }

        public int QueueCountBefore { get; internal set; }

        public int QueueCountAfter { get; internal set; }

        public string Message { get; internal set; }
    }

    public sealed class ParalivesInteractionQueueFacade
    {
        private readonly ParalivesCharacterFacade _characters;

        internal ParalivesInteractionQueueFacade(ParalivesCharacterFacade characters)
        {
            _characters = characters;
        }

        public ParalivesInteractionQueueEntry[] ReadQueue(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? ReadQueue(character)
                : new ParalivesInteractionQueueEntry[0];
        }

        public ParalivesInteractionQueueEntry[] ReadQueue(global::AssetCharacter character)
        {
            List<ParalivesInteractionQueueEntry> entries = new List<ParalivesInteractionQueueEntry>();
            if (character == null || character.Data == null || character.Data.CurrentInteractionsInQueue == null)
                return entries.ToArray();

            for (int i = 0; i < character.Data.CurrentInteractionsInQueue.Count; i++)
            {
                global::AssetCharacterDataInteraction interaction = character.Data.CurrentInteractionsInQueue[i];
                if (interaction != null)
                    entries.Add(CreateEntry(character.GUID, i, interaction));
            }

            return entries.ToArray();
        }

        public bool TryFindQueueIndex(ulong characterGuid, ulong interactionInstanceGuid, out int queueIndex)
        {
            queueIndex = -1;
            if (interactionInstanceGuid == 0UL)
                return false;

            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character)
                || character.Data == null
                || character.Data.CurrentInteractionsInQueue == null)
            {
                return false;
            }

            for (int i = 0; i < character.Data.CurrentInteractionsInQueue.Count; i++)
            {
                global::AssetCharacterDataInteraction interaction = character.Data.CurrentInteractionsInQueue[i];
                if (interaction != null && interaction.GUID == interactionInstanceGuid)
                {
                    queueIndex = i;
                    return true;
                }
            }

            return false;
        }

        public global::AssetCharacter[] FindCharactersWithInteraction(ulong interactionInstanceGuid)
        {
            List<global::AssetCharacter> matches = new List<global::AssetCharacter>();
            if (interactionInstanceGuid == 0UL)
                return matches.ToArray();

            global::AssetCharacter[] characters = _characters.GetAll();
            for (int i = 0; i < characters.Length; i++)
            {
                int queueIndex;
                if (characters[i] != null && TryFindQueueIndex(characters[i].GUID, interactionInstanceGuid, out queueIndex))
                    matches.Add(characters[i]);
            }

            return matches.ToArray();
        }

        public bool TryCancelInteraction(ulong characterGuid, ulong interactionInstanceGuid)
        {
            string message;
            return TryCancelInteraction(characterGuid, interactionInstanceGuid, out message);
        }

        public bool TryCancelInteraction(ulong characterGuid, ulong interactionInstanceGuid, out string message)
        {
            message = string.Empty;
            if (interactionInstanceGuid == 0UL)
            {
                message = "Interaction instance GUID is empty.";
                return false;
            }

            global::AssetCharacter character;
            global::AssetCharacterDataInteraction interaction;
            if (!TryGetQueuedInteraction(characterGuid, interactionInstanceGuid, out character, out interaction))
            {
                message = "Interaction was not found in the character queue.";
                return false;
            }

            try
            {
                global::InteractionManager.Instance.CancelInteraction(interaction);
                bool cancelled = interaction.State == global::AssetCharacterDataInteractionState.ToBeCanceled
                    || interaction.IsCancelling;
                character.IsSaveDirty = character.IsSaveDirty || cancelled;
                message = cancelled ? "Interaction is marked for cancellation." : "Interaction could not be cancelled.";
                return cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public bool TryCancelSocialGroupInteractions(ulong characterGuid, ulong socialGroupGuid)
        {
            if (characterGuid == 0UL || socialGroupGuid == 0UL)
                return false;

            try
            {
                global::InteractionManager.Instance.CancelInteractionsOfSocialGroup(characterGuid, socialGroupGuid);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ulong BuildQueueDigest(global::AssetCharacter character)
        {
            ParalivesInteractionQueueEntry[] entries = ReadQueue(character);
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < entries.Length; i++)
            {
                ParalivesInteractionQueueEntry entry = entries[i];
                hash = Add(hash, (ulong)entry.QueueIndex);
                hash = Add(hash, entry.InteractionInstanceGuid);
                hash = Add(hash, entry.InteractionSettingGuid);
                hash = Add(hash, (ulong)entry.State);
                hash = Add(hash, entry.CurrentActionGuid);
                hash = Add(hash, (ulong)entry.CurrentActionState);
                hash = Add(hash, entry.TargetCharacterGuid);
                hash = Add(hash, (ulong)(entry.ItemInstanceId + 1));
                hash = Add(hash, entry.LotGuid);
            }

            return hash == 0UL ? 1UL : hash;
        }

        public ulong BuildQueueDigest(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character) ? BuildQueueDigest(character) : 0UL;
        }

        public bool TryInjectInteraction(
            ParalivesInteractionInjectionRequest request,
            out ParalivesInteractionInjectionResult result)
        {
            result = new ParalivesInteractionInjectionResult
            {
                Message = string.Empty
            };

            if (request == null)
            {
                result.Message = "Request is required.";
                return false;
            }

            result.ActorCharacterGuid = request.ActorCharacterGuid;
            result.TargetCharacterGuid = request.TargetCharacterGuid;
            result.InteractionGuid = request.InteractionGuid;

            if (request.InteractionGuid == 0UL)
            {
                result.Message = "Interaction GUID is empty.";
                return false;
            }

            global::AssetCharacter actor;
            if (!_characters.TryGet(request.ActorCharacterGuid, out actor) || actor.Data == null)
            {
                result.Message = "Actor character was not found.";
                return false;
            }

            global::AssetCharacter target = null;
            if (request.TargetCharacterGuid != 0UL && !_characters.TryGet(request.TargetCharacterGuid, out target))
            {
                result.Message = "Target character was not found.";
                return false;
            }

            try
            {
                Interactions interactions = global::Settings.Get<Interactions>();
                if (interactions == null || interactions.GetInteractionByGUID(request.InteractionGuid) == null)
                {
                    result.Message = "Interaction is not registered.";
                    return false;
                }
            }
            catch
            {
                result.Message = "Interaction settings are not ready.";
                return false;
            }

            result.QueueCountBefore = actor.Data.CurrentInteractionsInQueue == null
                ? 0
                : actor.Data.CurrentInteractionsInQueue.Count;

            try
            {
                InteractionToInject injection = new InteractionToInject
                {
                    InjectedInteraction = request.InteractionGuid,
                    Priority = request.Priority,
                    TargetPosition = request.TargetPosition,
                    TargetOtherCharacter = request.TargetOtherCharacter,
                    InjectItemSlotAsTargetItem = request.InjectItemSlotAsTargetItem,
                    InjectOtherCharacterAsTargetCharacter = request.InjectOtherCharacterAsTargetCharacter
                };

                global::InteractionManager.Instance.InjectInteraction(
                    actor,
                    injection,
                    target,
                    request.IsIdleAutonomous,
                    request.IsForcedAutonomous,
                    request.SkinGuid,
                    request.LotGuid);

                result.QueueCountAfter = actor.Data.CurrentInteractionsInQueue == null
                    ? 0
                    : actor.Data.CurrentInteractionsInQueue.Count;
                result.Queued = result.QueueCountAfter > result.QueueCountBefore
                    || HasInteractionSetting(actor, request.InteractionGuid, result.QueueCountBefore);
                result.Message = result.Queued ? "Queued." : "No route or usable target was found.";
                return result.Queued;
            }
            catch (System.Exception ex)
            {
                result.QueueCountAfter = actor.Data.CurrentInteractionsInQueue == null
                    ? 0
                    : actor.Data.CurrentInteractionsInQueue.Count;
                result.Message = ex.Message;
                return false;
            }
        }

        private static ParalivesInteractionQueueEntry CreateEntry(
            ulong characterGuid,
            int queueIndex,
            global::AssetCharacterDataInteraction interaction)
        {
            global::CurrentAction currentAction = null;
            if (interaction.CurrentActionList != null
                && interaction.CurrentActionListIndex >= 0
                && interaction.CurrentActionListIndex < interaction.CurrentActionList.Count)
            {
                currentAction = interaction.CurrentActionList[interaction.CurrentActionListIndex];
            }

            return new ParalivesInteractionQueueEntry
            {
                CharacterGuid = characterGuid,
                QueueIndex = queueIndex,
                InteractionInstanceGuid = interaction.GUID,
                InteractionSettingGuid = interaction.InteractionSettingGUID,
                State = (int)interaction.State,
                OwnerCharacterGuid = interaction.OwnerCharacterGUID,
                TargetCharacterGuid = interaction.TargetCharacterGUID,
                ItemInstanceId = interaction.ItemInstanceID,
                TargetItemInstanceId = GetItemInstanceId(interaction.TargetItem),
                UsedItemInstanceId = GetItemInstanceId(interaction.UsedItem),
                CreatedItemInstanceId = interaction.ItemCreatedInstanceID,
                LotGuid = interaction.LotGUID,
                CurrentActionGuid = currentAction != null ? currentAction.ActionSettingGUID : 0UL,
                CurrentActionState = currentAction != null ? (int)currentAction.State : 0,
                CurrentActionIndex = interaction.CurrentActionListIndex,
                CurrentActionCount = interaction.CurrentActionList != null ? interaction.CurrentActionList.Count : 0,
                SocialGroupGuid = interaction.SocialGroupGUID,
                SocialGroupClusterGuid = interaction.SocialGroupClusterGUID,
                ParentInteractionInstanceGuid = interaction.ParentInteractionGUID,
                ChildInteractionInstanceGuid = interaction.ChildInteractionGUID,
                SkinGuid = interaction.SkinGUID,
                IsFromAutonomy = interaction.IsFromAnyAutonomy,
                IsCancelling = interaction.IsCancelling
            };
        }

        private static int GetItemInstanceId(global::ItemIDAndSlotOption item)
        {
            global::ItemIDAndSlot value;
            return item.IsSome(out value) ? value.ItemInstanceID : -1;
        }

        private static ulong Add(ulong hash, ulong value)
        {
            unchecked
            {
                for (int i = 0; i < 8; i++)
                {
                    hash ^= (byte)(value >> (i * 8));
                    hash *= 1099511628211UL;
                }
            }

            return hash;
        }

        private static bool HasInteractionSetting(
            global::AssetCharacter character,
            ulong interactionGuid,
            int startIndex)
        {
            if (character == null
                || character.Data == null
                || character.Data.CurrentInteractionsInQueue == null
                || interactionGuid == 0UL)
            {
                return false;
            }

            int start = startIndex < 0 ? 0 : startIndex;
            if (start >= character.Data.CurrentInteractionsInQueue.Count)
                start = 0;

            for (int i = start; i < character.Data.CurrentInteractionsInQueue.Count; i++)
            {
                global::AssetCharacterDataInteraction interaction = character.Data.CurrentInteractionsInQueue[i];
                if (interaction != null && interaction.InteractionSettingGUID == interactionGuid)
                    return true;
            }

            return false;
        }

        private bool TryGetQueuedInteraction(
            ulong characterGuid,
            ulong interactionInstanceGuid,
            out global::AssetCharacter character,
            out global::AssetCharacterDataInteraction interaction)
        {
            interaction = null;
            if (!_characters.TryGet(characterGuid, out character)
                || character.Data == null
                || character.Data.CurrentInteractionsInQueue == null)
            {
                return false;
            }

            for (int i = 0; i < character.Data.CurrentInteractionsInQueue.Count; i++)
            {
                global::AssetCharacterDataInteraction queued = character.Data.CurrentInteractionsInQueue[i];
                if (queued != null && queued.GUID == interactionInstanceGuid)
                {
                    interaction = queued;
                    return true;
                }
            }

            return false;
        }
    }
}
