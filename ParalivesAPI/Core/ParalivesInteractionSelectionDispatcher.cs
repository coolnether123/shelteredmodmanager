using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using Setting;

namespace ParalivesAPI.Core
{
    public delegate void ParalivesInteractionSelectedEventHandler(ParalivesInteractionSelectedEvent selectedEvent);

    public sealed class ParalivesInteractionSelectionDispatcher
    {
        private static readonly FieldInfo UiInteractionsListField =
            AccessTools.Field(typeof(UIInteractionsListItem), "_uiInteractionsList");

        private static readonly FieldInfo RootObjectField =
            AccessTools.Field(typeof(UIInteractionsListItem), "_rootObject");

        private readonly object _sync = new object();
        private ParalivesInteractionSelectedEventHandler _interactionSelected;

        public event ParalivesInteractionSelectedEventHandler InteractionSelected
        {
            add
            {
                lock (_sync)
                    _interactionSelected += value;
            }
            remove
            {
                lock (_sync)
                    _interactionSelected -= value;
            }
        }

        internal void Raise(UIInteractionsListItem item, ulong skinGuid)
        {
            ParalivesInteractionSelectedEventHandler handler;
            lock (_sync)
                handler = _interactionSelected;

            if (handler == null || item == null || item.InteractionGroupItem == null)
                return;
            if (item.InteractionGroupItem.Type != InteractionItemType.Interaction)
                return;

            ParalivesInteractionSelectedEvent selectedEvent = CreateEvent(item, skinGuid);
            if (selectedEvent == null)
                return;

            Delegate[] subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                ParalivesInteractionSelectedEventHandler subscriber = subscribers[i] as ParalivesInteractionSelectedEventHandler;
                if (subscriber == null)
                    continue;

                try
                {
                    subscriber(selectedEvent);
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce(
                        "ParalivesInteractionSelectionDispatcher.Handler." + i,
                        "Paralives interaction selection handler failed: " + ex.Message);
                }
            }
        }

        private static ParalivesInteractionSelectedEvent CreateEvent(UIInteractionsListItem item, ulong skinGuid)
        {
            UIInteractionsList list = GetPrivateValue<UIInteractionsList>(UiInteractionsListField, item);
            UIInteractions ui = list != null ? list.UIInteractions : null;
            ItemObjectRoot rootObject = GetPrivateValue<ItemObjectRoot>(RootObjectField, item);

            int playerIndex = ui != null ? ui.PlayerOwnerIndex : -1;
            Player player = ui != null && ui.PlayerOwner != null ? ui.PlayerOwner.Player : null;
            ulong actorGuid = player != null ? player.GetSelectedCharacterGUID() : 0UL;
            ulong clickedCharacterGuid = ui != null ? ui.ClickedCharacterGUID : 0UL;
            int itemInstanceId = ui != null ? ui.ClickedItemInstanceID : -1;
            ulong lotGuid = ui != null ? ui.ClickedLotGUID : 0UL;

            if (rootObject != null)
            {
                if (itemInstanceId == -1)
                    itemInstanceId = rootObject.InstanceID;
                if (lotGuid == 0UL)
                    lotGuid = rootObject.LotPlacedOnGUID;
            }

            NormalizeItemAndLot(list, ui, ref itemInstanceId, ref lotGuid);

            return new ParalivesInteractionSelectedEvent
            {
                PlayerIndex = playerIndex,
                ActorCharacterGuid = actorGuid,
                TargetCharacterGuid = clickedCharacterGuid,
                InteractionSettingGuid = item.InteractionGroupItem.Interaction,
                InteractionGroupGuid = list != null && list.InteractionGroup != null ? list.InteractionGroup.GUID : 0UL,
                RootInteractionGroupGuid = ui != null && ui.CurrentRootInteractionGroup != null ? ui.CurrentRootInteractionGroup.GUID : 0UL,
                ItemInstanceId = itemInstanceId,
                LotGuid = lotGuid,
                SkinGuid = skinGuid
            };
        }

        private static void NormalizeItemAndLot(UIInteractionsList list, UIInteractions ui, ref int itemInstanceId, ref ulong lotGuid)
        {
            try
            {
                ItemObjectRoot item = ItemManager.Instance != null
                    ? ItemManager.Instance.GetItemByInstanceID(itemInstanceId)
                    : null;

                if (item == null)
                    return;

                if (item.IsImpostorForLot != 0UL)
                    lotGuid = item.IsImpostorForLot;

                Interactions interactions = Settings.Get<Interactions>();
                ulong floorGroupGuid = interactions != null ? interactions.FloorInteractions : 0UL;
                bool isFloorInteraction = floorGroupGuid != 0UL
                    && ((list != null && list.InteractionGroup != null && list.InteractionGroup.GUID == floorGroupGuid)
                        || (ui != null && ui.CurrentRootInteractionGroup != null && ui.CurrentRootInteractionGroup.GUID == floorGroupGuid));

                if (isFloorInteraction && item.PathfindingImpact == PathfindingImpact.CanBeWalkedOn)
                    itemInstanceId = -1;
            }
            catch
            {
            }
        }

        private static T GetPrivateValue<T>(FieldInfo field, object instance) where T : class
        {
            if (field == null || instance == null)
                return null;

            try
            {
                return field.GetValue(instance) as T;
            }
            catch
            {
                return null;
            }
        }
    }
}
