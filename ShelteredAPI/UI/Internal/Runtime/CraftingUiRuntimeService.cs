using System;
using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.UI.Internal.Runtime.Widgets;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime{
    internal static class CraftingUiRuntimeService
    {
        private static int _nextPanelId = 1;

        public static RuntimeUiHandle Open(CraftingUiRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            if (string.IsNullOrEmpty(request.PanelId))
            {
                string owner = string.IsNullOrEmpty(request.OwnerId) ? "anonymous" : request.OwnerId;
                request.PanelId = owner + ".crafting." + (_nextPanelId++);
            }

            RuntimeUiHandle handle = new RuntimeUiHandle(request.PanelId);
            CraftingUiPanelState state = new CraftingUiPanelState(request, handle);
            RuntimeUiPanelRecord record = new RuntimeUiPanelRecord
            {
                PanelId = request.PanelId,
                OwnerId = request.OwnerId,
                Kind = "Crafting",
                Request = state,
                RefreshEveryFrame = request.RefreshEveryFrame,
                Build = Build,
                Refresh = Refresh,
                Close = Close
            };

            RuntimeUiRegistry.Register(record);
            record.RebindRequested = true;
            RuntimeUiRefreshService.Refresh(request.PanelId);
            return handle;
        }

        private static void Build(RuntimeUiPanelRecord record)
        {
            CraftingUiPanelState state = record.Request as CraftingUiPanelState;
            if (state == null || state.Request == null)
                return;

            GameObject root = RuntimeUiPanelService.EnsurePanelRoot(record);
            if (root == null)
                return;

            RuntimeWidgetUtil.DestroyChildren(root);
            UIPanel panel = root.GetComponent<UIPanel>();
            int depth = panel != null ? panel.depth + 1 : RuntimeUiPanelService.AssignDepth();
            state.Layout = RuntimePanelChrome.Create(root, string.IsNullOrEmpty(state.Request.Title) ? "Crafting" : state.Request.Title, depth, delegate
            {
                RuntimeUiRegistry.Close(record.PanelId);
            }, state.Request.PanelOptions);

            state.ListRoot = RuntimeWidgetUtil.CreateChild(root, "RecipeList", Vector3.zero);
            RenderRecipes(state, depth + 20);
        }

        private static void Refresh(RuntimeUiPanelRecord record)
        {
            CraftingUiPanelState state = record.Request as CraftingUiPanelState;
            if (state == null || state.Request == null)
                return;

            if (state.ListRoot == null)
            {
                record.RebindRequested = true;
                return;
            }

            UIPanel panel = record.Root != null ? record.Root.GetComponent<UIPanel>() : null;
            int depth = panel != null ? panel.depth + 20 : RuntimeUiPanelService.AssignDepth();
            RenderRecipes(state, depth);
            if (state.Request.OnRefreshed != null)
                state.Request.OnRefreshed(state.Handle);
        }

        private static void Close(RuntimeUiPanelRecord record)
        {
            CraftingUiPanelState state = record.Request as CraftingUiPanelState;
            CraftingUiRequest request = state != null ? state.Request : record.Request as CraftingUiRequest;
            if (request != null && request.OnClosed != null)
                request.OnClosed();
        }

        private static void RenderRecipes(CraftingUiPanelState state, int depth)
        {
            if (state == null || state.ListRoot == null || state.Request == null)
                return;

            RuntimeRecipeList.Build(
                state.ListRoot,
                ResolveRecipes(state.Request),
                state.Request.IsAvailable,
                delegate(CraftingUiRecipe recipe)
                {
                    if (state.Request.OnCraftRequested != null)
                        state.Request.OnCraftRequested(new CraftingUiCraftContext(recipe, state.Handle));
                    if (state.Request.OnCraft != null)
                        state.Request.OnCraft(recipe);
                },
                depth,
                new RuntimeRecipeListOptions
                {
                    EmptyText = state.Request.EmptyText,
                    CraftButtonText = state.Request.CraftButtonText,
                    GetUnavailableReason = state.Request.GetUnavailableReason,
                    Layout = state.Layout,
                    Style = GetStyle(state.Request)
                });
        }

        private static IList<CraftingUiRecipe> ResolveRecipes(CraftingUiRequest request)
        {
            if (request == null)
                return new List<CraftingUiRecipe>();

            if (request.RecipeSource != null)
            {
                IList<CraftingUiRecipe> refreshed = request.RecipeSource();
                return refreshed ?? new List<CraftingUiRecipe>();
            }

            return request.Recipes ?? new List<CraftingUiRecipe>();
        }

        private static RuntimePanelStyle GetStyle(CraftingUiRequest request)
        {
            return request != null && request.PanelOptions != null ? request.PanelOptions.Style : null;
        }

        private sealed class CraftingUiPanelState
        {
            public readonly CraftingUiRequest Request;
            public readonly RuntimeUiHandle Handle;
            public GameObject ListRoot;
            public RuntimePanelChromeLayout Layout;

            public CraftingUiPanelState(CraftingUiRequest request, RuntimeUiHandle handle)
            {
                Request = request;
                Handle = handle;
            }
        }
    }
}
