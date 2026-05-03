using System;
using ShelteredAPI.UI.Internal;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Public facade for mod-owned runtime UI. This API intentionally exposes
    /// DTOs and callbacks rather than NGUI objects.
    /// </summary>
    public static class ShelteredRuntimeUI
    {
        public static RuntimeUiHandle OpenContainer(ContainerUiRequest request)
        {
            return ContainerUiRuntimeService.Open(request);
        }

        public static RuntimeUiHandle OpenCrafting(CraftingUiRequest request)
        {
            return CraftingUiRuntimeService.Open(request);
        }

        public static IDisposable RegisterObjectPanel(ObjectPanelRegistration registration)
        {
            return RuntimeObjectPanelRegistry.Register(registration);
        }

        public static bool IsOpen(string panelId)
        {
            return RuntimeUiRegistry.Contains(panelId);
        }

        public static void Refresh(string panelId)
        {
            RuntimeUiRefreshService.Refresh(panelId);
        }

        public static void Close(string panelId)
        {
            RuntimeUiRegistry.Close(panelId);
        }

        public static void CloseOwner(string ownerId)
        {
            RuntimeUiRegistry.CloseOwner(ownerId);
        }

        public static void CloseAll()
        {
            RuntimeUiRegistry.CloseAll();
        }
    }
}
