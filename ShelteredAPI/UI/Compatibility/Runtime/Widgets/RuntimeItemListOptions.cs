using System;

namespace ShelteredAPI.UI.Internal
{
    internal sealed class RuntimeItemListOptions
    {
        public string EmptyText;
        public int TransferQuantity;
        public ContainerUiTransferDirection TransferDirection;
        public Func<ContainerUiItem, bool> CanSelect;
        public Func<ContainerUiItem, bool> CanTransfer;
        public Func<ContainerUiItem, string> FormatCount;
        public Action<ContainerUiItem> OnSelected;
        public Action<ContainerUiTransferContext> OnTransfer;
    }
}
