using System;
using ShelteredAPI.Content;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
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
