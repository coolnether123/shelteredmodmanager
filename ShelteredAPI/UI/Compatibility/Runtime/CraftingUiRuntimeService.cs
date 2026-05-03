using System;
using UnityEngine;

namespace ShelteredAPI.UI.Internal
{
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
            RuntimeUiPanelRecord record = new RuntimeUiPanelRecord
            {
                PanelId = request.PanelId,
                OwnerId = request.OwnerId,
                Kind = "Crafting",
                Request = request,
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
            CraftingUiRequest request = record.Request as CraftingUiRequest;
            if (request == null)
                return;

            GameObject root = RuntimeUiPanelService.EnsurePanelRoot(record);
            if (root == null)
                return;

            RuntimeWidgetUtil.DestroyChildren(root);
            UIPanel panel = root.GetComponent<UIPanel>();
            int depth = panel != null ? panel.depth + 1 : RuntimeUiPanelService.AssignDepth();
            RuntimePanelChrome.Create(root, string.IsNullOrEmpty(request.Title) ? "Crafting" : request.Title, depth, delegate
            {
                RuntimeUiRegistry.Close(record.PanelId);
            });

            GameObject listRoot = RuntimeWidgetUtil.CreateChild(root, "RecipeList", Vector3.zero);
            RuntimeRecipeList.Build(listRoot, request.Recipes, request.IsAvailable, request.OnCraft, depth + 20);
        }

        private static void Refresh(RuntimeUiPanelRecord record)
        {
            record.RebindRequested = true;
        }

        private static void Close(RuntimeUiPanelRecord record)
        {
            CraftingUiRequest request = record.Request as CraftingUiRequest;
            if (request != null && request.OnClosed != null)
                request.OnClosed();
        }
    }
}
