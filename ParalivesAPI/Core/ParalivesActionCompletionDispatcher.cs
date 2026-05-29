using System;
using ModAPI.Core;
using Setting;

namespace ParalivesAPI.Core
{
    public delegate void ParalivesActionCompletedEventHandler(ParalivesActionCompletedEvent completedEvent);

    public sealed class ParalivesActionCompletionDispatcher
    {
        private readonly object _sync = new object();
        private ParalivesActionCompletedEventHandler _actionCompleted;

        public event ParalivesActionCompletedEventHandler ActionCompleted
        {
            add
            {
                lock (_sync)
                    _actionCompleted += value;
            }
            remove
            {
                lock (_sync)
                    _actionCompleted -= value;
            }
        }

        internal void Raise(
            AssetCharacter characterAsset,
            AssetCharacterDataInteraction interaction,
            CurrentAction currentAction,
            ActionUnit actionUnit)
        {
            ParalivesActionCompletedEventHandler handler;
            lock (_sync)
                handler = _actionCompleted;

            if (handler == null)
                return;

            ParalivesActionCompletedEvent completedEvent = CreateEvent(characterAsset, interaction, currentAction, actionUnit);
            Delegate[] subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                ParalivesActionCompletedEventHandler subscriber = subscribers[i] as ParalivesActionCompletedEventHandler;
                if (subscriber == null)
                    continue;

                try
                {
                    subscriber(completedEvent);
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce(
                        "ParalivesActionCompletionDispatcher.Handler." + i,
                        "Paralives action completion handler failed: " + ex.Message);
                }
            }
        }

        private static ParalivesActionCompletedEvent CreateEvent(
            AssetCharacter characterAsset,
            AssetCharacterDataInteraction interaction,
            CurrentAction currentAction,
            ActionUnit actionUnit)
        {
            InteractionRequirementsCheckData requirements = interaction != null
                ? interaction.InteractionRequirementsCheckData
                : default(InteractionRequirementsCheckData);

            ulong actorGuid = characterAsset != null ? characterAsset.GUID : requirements.CharacterGUID;
            ulong targetGuid = interaction != null ? interaction.TargetCharacterGUID : 0UL;
            ulong otherGuid = requirements.OtherCharactersGUID != 0UL ? requirements.OtherCharactersGUID : targetGuid;
            ulong actionSettingGuid = currentAction != null ? currentAction.ActionSettingGUID : 0UL;
            int actionIndex = interaction != null ? interaction.CurrentActionListIndex : -1;
            int actionCount = interaction != null && interaction.CurrentActionList != null ? interaction.CurrentActionList.Count : 0;
            float completedAt = ParaTime.TotalMinutes;
            float startedAt = currentAction != null ? currentAction.StartParaTimeMinutes : 0f;

            return new ParalivesActionCompletedEvent
            {
                ActorCharacterGuid = actorGuid,
                PlayerIndex = FindSelectingPlayerIndex(actorGuid),
                OwnerCharacterGuid = interaction != null && interaction.OwnerCharacterGUID != 0UL
                    ? interaction.OwnerCharacterGUID
                    : requirements.OwnerCharacterGUID,
                TargetCharacterGuid = targetGuid,
                OtherCharacterGuid = otherGuid,
                InteractionInstanceGuid = interaction != null ? interaction.GUID : 0UL,
                InteractionSettingGuid = interaction != null ? interaction.InteractionSettingGUID : 0UL,
                ActionSettingGuid = actionSettingGuid,
                ActionUnitGuid = actionUnit != null ? actionUnit.GUID : actionSettingGuid,
                ActionIndex = actionIndex,
                ActionCount = actionCount,
                IsFinalActionInInteraction = actionCount > 0 && actionIndex == actionCount - 1,
                StartedAtParaTimeMinutes = startedAt,
                CompletedAtParaTimeMinutes = completedAt,
                ElapsedParaTimeMinutes = startedAt > 0f && completedAt >= startedAt ? completedAt - startedAt : 0f,
                ItemInstanceId = interaction != null ? interaction.ItemInstanceID : requirements.ItemInstanceID,
                TargetItemInstanceId = interaction != null ? GetItemInstanceId(interaction.TargetItem) : -1,
                UsedItemInstanceId = interaction != null ? GetItemInstanceId(interaction.UsedItem) : -1,
                CreatedItemInstanceId = interaction != null ? interaction.ItemCreatedInstanceID : -1,
                LotGuid = interaction != null && interaction.LotGUID != 0UL ? interaction.LotGUID : requirements.LotGUID,
                SocialGroupGuid = interaction != null ? interaction.SocialGroupGUID : 0UL,
                SocialGroupClusterGuid = interaction != null ? interaction.SocialGroupClusterGUID : 0UL,
                ParentInteractionInstanceGuid = interaction != null ? interaction.ParentInteractionGUID : 0UL,
                ChildInteractionInstanceGuid = interaction != null ? interaction.ChildInteractionGUID : 0UL,
                SkinGuid = interaction != null ? interaction.SkinGUID : 0UL,
                IsFromAutonomy = interaction != null && interaction.IsFromAnyAutonomy,
                IsCancelling = interaction != null && interaction.IsCancelling,
                HasSuccessBeenDetermined = interaction != null && interaction.HasSuccessBeenDetermined,
                IsSuccess = interaction != null && interaction.IsSuccess
            };
        }

        private static int GetItemInstanceId(ItemIDAndSlotOption item)
        {
            ItemIDAndSlot value;
            return item.IsSome(out value) ? value.ItemInstanceID : -1;
        }

        private static int FindSelectingPlayerIndex(ulong characterGuid)
        {
            if (characterGuid == 0UL)
                return -1;

            try
            {
                if (PlayerManager.Instance == null || PlayerManager.Instance.Players == null)
                    return -1;

                for (int i = 0; i < PlayerManager.Instance.Players.Count; i++)
                {
                    Player player = PlayerManager.Instance.Players[i];
                    if (player != null && player.SelectedCharactersGUID != null && player.SelectedCharactersGUID.Contains(characterGuid))
                        return player.PlayerIndex;
                }
            }
            catch
            {
                return -1;
            }

            return -1;
        }
    }
}
