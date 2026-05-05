using System;
using System.Collections.Generic;
using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
namespace ShelteredAPI.UI.Runtime{
    /// <summary>
    /// Stable handle returned by runtime UI entry points. Mod authors should use
    /// this instead of holding Unity or NGUI objects.
    /// </summary>
    public sealed class RuntimeUiHandle : IDisposable
    {
        private readonly string _panelId;

        internal RuntimeUiHandle(string panelId)
        {
            _panelId = panelId;
        }

        public string PanelId { get { return _panelId; } }

        public bool IsOpen
        {
            get { return ShelteredRuntimeUI.IsOpen(_panelId); }
        }

        public void Refresh()
        {
            ShelteredRuntimeUI.Refresh(_panelId);
        }

        public void Close()
        {
            ShelteredRuntimeUI.Close(_panelId);
        }

        public void Dispose()
        {
            Close();
        }
    }

    /// <summary>
    /// Request for a mod-owned container/item list panel.
    /// </summary>
    public sealed class ContainerUiRequest
    {
        public ContainerUiRequest()
        {
            TransferQuantity = 1;
            TransferDirection = ContainerUiTransferDirection.OutOfContainer;
        }

        public string PanelId { get; set; }
        public string Title { get; set; }
        public string OwnerId { get; set; }
        public IList<ContainerUiItem> Items { get; set; }
        public Func<IList<ContainerUiItem>> ItemSource { get; set; }
        public ItemCategory[] Categories { get; set; }
        public ItemCategory? InitialCategory { get; set; }
        public string[] AllowedItemIds { get; set; }
        public string EmptyText { get; set; }
        public int TransferQuantity { get; set; }
        public ContainerUiTransferDirection TransferDirection { get; set; }
        public bool CloseOnTransfer { get; set; }
        public bool RefreshEveryFrame { get; set; }
        public Obj_Base AttachedObject { get; set; }
        public Comparison<ContainerUiItem> SortComparison { get; set; }
        public Func<ContainerUiItem, bool> CanSelect { get; set; }
        public Func<ContainerUiItem, bool> CanTransfer { get; set; }
        public Func<ContainerUiItem, string> FormatCount { get; set; }
        public IList<ContainerUiAction> Actions { get; set; }
        public Action<ContainerUiItem> OnItemSelected { get; set; }
        public Action<ContainerUiTransferContext> OnTransferRequested { get; set; }
        public Action<RuntimeUiHandle> OnRefreshed { get; set; }
        public Action OnClosed { get; set; }
    }

    /// <summary>
    /// Footer command for a runtime container panel.
    /// </summary>
    public sealed class ContainerUiAction
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public Func<bool> IsEnabled { get; set; }
        public Action<RuntimeUiHandle> Execute { get; set; }
    }

    /// <summary>
    /// DTO for an item row shown in a runtime container panel.
    /// </summary>
    public sealed class ContainerUiItem
    {
        public ContainerUiItem()
        {
        }

        public ContainerUiItem(string itemId, string displayName, ItemCategory category, int count)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Category = category;
            Count = count;
        }

        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Subtitle { get; set; }
        public ItemCategory Category { get; set; }
        public int Count { get; set; }
        public string CountText { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsTransferEnabled { get; set; }
        public object Tag { get; set; }
    }

    /// <summary>
    /// Direction requested by a runtime container transfer action.
    /// </summary>
    public enum ContainerUiTransferDirection
    {
        IntoContainer,
        OutOfContainer
    }

    /// <summary>
    /// Transfer request passed to container UI callbacks.
    /// The UI has not applied inventory changes; handlers own the actual transfer behavior.
    /// </summary>
    public sealed class ContainerUiTransferContext
    {
        internal ContainerUiTransferContext(ContainerUiItem item, int quantity, ContainerUiTransferDirection direction)
        {
            Item = item;
            Quantity = quantity;
            Direction = direction;
        }

        public ContainerUiItem Item { get; private set; }
        public int Quantity { get; private set; }
        public ContainerUiTransferDirection Direction { get; private set; }
    }

    /// <summary>
    /// Registers a runtime UI entry point on an interactable object type.
    /// </summary>
    public sealed class ObjectPanelRegistration
    {
        public ObjectPanelRegistration()
        {
            ObjectType = ObjectManager.ObjectType.Undefined;
        }

        public string ObjectId { get; set; }
        public ObjectManager.ObjectType ObjectType { get; set; }
        public string InteractionId { get; set; }
        public string InteractionText { get; set; }
        public int Priority { get; set; }
        public Func<ObjectPanelContext, bool> CanOpen { get; set; }
        public Func<ObjectPanelContext, RuntimeUiHandle> Open { get; set; }
    }

    /// <summary>
    /// Runtime context passed when a registered object panel is opened.
    /// Use the target object and selected family member immediately; do not cache them across scene changes.
    /// </summary>
    public sealed class ObjectPanelContext
    {
        internal ObjectPanelContext(string objectId, Obj_Base targetObject, FamilyMember selectedMember)
        {
            ObjectId = objectId;
            TargetObject = targetObject;
            SelectedMember = selectedMember;
        }

        public string ObjectId { get; private set; }
        public Obj_Base TargetObject { get; private set; }
        public FamilyMember SelectedMember { get; private set; }
    }

    /// <summary>
    /// Request shape reserved for custom crafting panels that reuse the runtime widget system.
    /// </summary>
    public sealed class CraftingUiRequest
    {
        public string PanelId { get; set; }
        public string Title { get; set; }
        public string OwnerId { get; set; }
        public IList<CraftingUiRecipe> Recipes { get; set; }
        public Func<CraftingUiRecipe, bool> IsAvailable { get; set; }
        public Action<CraftingUiRecipe> OnCraft { get; set; }
        public Action OnClosed { get; set; }
    }

    /// <summary>
    /// Recipe row shown by a custom crafting UI.
    /// Item IDs are mod-facing content IDs, not raw game item enum names.
    /// </summary>
    public sealed class CraftingUiRecipe
    {
        public string RecipeId { get; set; }
        public string DisplayName { get; set; }
        public string OutputItemId { get; set; }
        public int OutputCount { get; set; }
        public IList<CraftingUiIngredient> Ingredients { get; set; }
        public object Tag { get; set; }
    }

    /// <summary>
    /// Required item and quantity for a custom crafting recipe.
    /// </summary>
    public sealed class CraftingUiIngredient
    {
        public string ItemId { get; set; }
        public int Count { get; set; }
    }
}
