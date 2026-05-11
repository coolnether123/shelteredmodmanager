using System;
using System.Collections.Generic;
using ShelteredAPI.Content;
using UnityEngine;
using ShelteredAPI.UI.Internal.Runtime.Widgets;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime{
    internal static class ContainerUiRuntimeService
    {
        private static int _nextPanelId = 1;

        public static RuntimeUiHandle Open(ContainerUiRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            string panelId = NormalizePanelId(request);
            request.PanelId = panelId;

            RuntimeUiHandle handle = new RuntimeUiHandle(panelId);
            ContainerUiPanelState state = new ContainerUiPanelState(request, handle);

            RuntimeUiPanelRecord record = new RuntimeUiPanelRecord
            {
                PanelId = panelId,
                OwnerId = request.OwnerId,
                Kind = "Container",
                Request = state,
                RefreshEveryFrame = request.RefreshEveryFrame,
                Build = Build,
                Refresh = Refresh,
                Close = Close
            };

            RuntimeUiRegistry.Register(record);
            record.RebindRequested = true;
            RuntimeUiRefreshService.Refresh(panelId);
            return handle;
        }

        private static string NormalizePanelId(ContainerUiRequest request)
        {
            if (!string.IsNullOrEmpty(request.PanelId))
                return request.PanelId;

            string owner = string.IsNullOrEmpty(request.OwnerId) ? "anonymous" : request.OwnerId;
            return owner + ".container." + (_nextPanelId++);
        }

        private static void Build(RuntimeUiPanelRecord record)
        {
            ContainerUiPanelState state = record.Request as ContainerUiPanelState;
            if (state == null || state.Request == null)
                return;

            GameObject root = RuntimeUiPanelService.EnsurePanelRoot(record);
            if (root == null)
                return;

            RuntimeWidgetUtil.DestroyChildren(root);

            UIPanel panel = root.GetComponent<UIPanel>();
            int depth = panel != null ? panel.depth + 1 : RuntimeUiPanelService.AssignDepth();
            state.Layout = RuntimePanelChrome.Create(root, string.IsNullOrEmpty(state.Request.Title) ? "Container" : state.Request.Title, depth, delegate
            {
                RuntimeUiRegistry.Close(record.PanelId);
            }, state.Request.PanelOptions);

            RuntimeFilterTabs.Build(root, state.Request.Categories, state.SelectedCategory, delegate(ItemCategory? category)
            {
                state.SelectedCategory = category;
                RenderItems(state, depth + 10);
            }, depth + 4, state.Layout, GetStyle(state.Request));

            state.ListRoot = RuntimeWidgetUtil.CreateChild(root, "ItemList", Vector3.zero);
            state.ActionsRoot = RuntimeWidgetUtil.CreateChild(root, "Actions", Vector3.zero);
            Refresh(record);
        }

        private static void Refresh(RuntimeUiPanelRecord record)
        {
            ContainerUiPanelState state = record.Request as ContainerUiPanelState;
            if (state == null)
                return;

            UIPanel panel = record.Root != null ? record.Root.GetComponent<UIPanel>() : null;
            int depth = panel != null ? panel.depth + 20 : RuntimeUiPanelService.AssignDepth();
            RenderItems(state, depth);
            RenderFooterActions(state, panel != null ? panel.depth + 6 : depth + 6);

            if (state.Request.OnRefreshed != null)
                state.Request.OnRefreshed(state.Handle);
        }

        private static void RenderItems(ContainerUiPanelState state, int depth)
        {
            if (state == null || state.ListRoot == null)
                return;

            IList<ContainerUiItem> source = ResolveItems(state.Request);
            List<ContainerUiItem> filtered = new List<ContainerUiItem>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                ContainerUiItem item = source[i];
                if (item == null)
                    continue;
                if (!IsAllowedByCategory(state.Request, item, state.SelectedCategory))
                    continue;
                if (!IsAllowedById(state.Request, item))
                    continue;

                filtered.Add(item);
            }

            if (state.Request.SortComparison != null)
                filtered.Sort(state.Request.SortComparison);

            RuntimeItemList.Build(
                state.ListRoot,
                filtered,
                depth,
                new RuntimeItemListOptions
                {
                    EmptyText = state.Request.EmptyText,
                    TransferQuantity = state.Request.TransferQuantity > 0 ? state.Request.TransferQuantity : 1,
                    TransferDirection = state.Request.TransferDirection,
                    CanSelect = state.Request.CanSelect,
                    CanTransfer = state.Request.CanTransfer,
                    FormatCount = state.Request.FormatCount,
                    Layout = state.Layout,
                    Style = GetStyle(state.Request),
                    OnSelected = state.Request.OnItemSelected,
                    OnTransfer = delegate(ContainerUiTransferContext context)
                    {
                        if (state.Request.OnTransferRequested != null)
                            state.Request.OnTransferRequested(context);
                        if (state.Request.CloseOnTransfer)
                            state.Handle.Close();
                    }
                });
        }

        private static void RenderFooterActions(ContainerUiPanelState state, int depth)
        {
            if (state == null || state.ActionsRoot == null)
                return;

            RuntimeWidgetUtil.DestroyChildren(state.ActionsRoot);

            if (state.Request == null || state.Request.Actions == null)
                return;

            int count = Math.Min(state.Request.Actions.Count, 4);
            if (count <= 0)
                return;

            int totalWidth = count * 120 + (count - 1) * 10;
            int startX = -totalWidth / 2 + 60;
            for (int i = 0; i < count; i++)
            {
                ContainerUiAction action = state.Request.Actions[i];
                if (action == null || string.IsNullOrEmpty(action.Text))
                    continue;

                int x = startX + i * 130;
                bool enabled = action.IsEnabled == null || action.IsEnabled();
                ContainerUiAction captured = action;
                float y = state.Layout != null ? state.Layout.FooterY : -226f;
                RuntimeButton.Create(state.ActionsRoot, "Action_" + (!string.IsNullOrEmpty(action.Id) ? action.Id : i.ToString()), action.Text, 120, 32, new Vector3(x, y, 0f), depth, enabled, delegate
                {
                    if (captured.Execute != null)
                        captured.Execute(state.Handle);
                }, GetStyle(state.Request));
            }
        }

        private static RuntimePanelStyle GetStyle(ContainerUiRequest request)
        {
            return request != null && request.PanelOptions != null ? request.PanelOptions.Style : null;
        }

        private static IList<ContainerUiItem> ResolveItems(ContainerUiRequest request)
        {
            if (request == null)
                return new List<ContainerUiItem>();

            if (request.ItemSource != null)
            {
                IList<ContainerUiItem> refreshed = request.ItemSource();
                return refreshed ?? new List<ContainerUiItem>();
            }

            return request.Items ?? new List<ContainerUiItem>();
        }

        private static bool IsAllowedByCategory(ContainerUiRequest request, ContainerUiItem item, ItemCategory? selectedCategory)
        {
            if (selectedCategory.HasValue && !CategoriesMatch(selectedCategory.Value, item.Category))
                return false;

            if (request.Categories == null || request.Categories.Length == 0)
                return true;

            for (int i = 0; i < request.Categories.Length; i++)
            {
                if (CategoriesMatch(request.Categories[i], item.Category))
                    return true;
            }

            return false;
        }

        private static bool CategoriesMatch(ItemCategory filter, ItemCategory itemCategory)
        {
            if (filter == itemCategory)
                return true;

            return filter == ItemCategory.Food && itemCategory == ItemCategory.Meat;
        }

        private static bool IsAllowedById(ContainerUiRequest request, ContainerUiItem item)
        {
            if (request.AllowedItemIds == null || request.AllowedItemIds.Length == 0)
                return true;

            for (int i = 0; i < request.AllowedItemIds.Length; i++)
            {
                if (string.Equals(request.AllowedItemIds[i], item.ItemId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void Close(RuntimeUiPanelRecord record)
        {
            ContainerUiPanelState state = record.Request as ContainerUiPanelState;
            if (state == null || state.Request == null || state.Request.OnClosed == null)
                return;

            state.Request.OnClosed();
        }

        private sealed class ContainerUiPanelState
        {
            public readonly ContainerUiRequest Request;
            public readonly RuntimeUiHandle Handle;
            public ItemCategory? SelectedCategory;
            public GameObject ListRoot;
            public GameObject ActionsRoot;
            public RuntimePanelChromeLayout Layout;

            public ContainerUiPanelState(ContainerUiRequest request, RuntimeUiHandle handle)
            {
                Request = request;
                Handle = handle;
                SelectedCategory = request != null ? request.InitialCategory : null;
            }
        }
    }
}
