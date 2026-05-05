using ShelteredAPI.UI.Compatibility;
using UnityEngine;
using ShelteredAPI.Content;
namespace ShelteredAPI.UI.Internal.Runtime{
    internal static class RuntimeUiPanelService
    {
        private const string OverlayName = "ShelteredRuntimeUI_Overlay";

        public static GameObject EnsurePanelRoot(RuntimeUiPanelRecord record)
        {
            if (record == null)
                return null;

            EnsureDriver();

            if (record.Root != null)
                return record.Root;

            UIPanel overlay = UIUtil.EnsureOverlayPanel(OverlayName, 50000);
            if (overlay == null)
                return null;

            GameObject root = new GameObject("RuntimeUI_" + record.PanelId);
            root.transform.SetParent(overlay.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            root.layer = overlay.gameObject.layer;
            NGUITools.SetLayer(root, overlay.gameObject.layer);

            UIPanel panel = root.AddComponent<UIPanel>();
            panel.depth = AssignDepth();
            panel.clipping = UIDrawCall.Clipping.None;
            panel.alpha = 1f;

            record.Root = root;
            return root;
        }

        public static int AssignDepth()
        {
            int depth = 50000;
            UIPanel[] panels = UnityEngine.Object.FindObjectsOfType<UIPanel>();
            if (panels != null)
            {
                for (int i = 0; i < panels.Length; i++)
                {
                    UIPanel panel = panels[i];
                    if (panel != null && panel.depth >= depth)
                        depth = panel.depth + 25;
                }
            }
            return depth;
        }

        private static void EnsureDriver()
        {
            UIPanel overlay = UIUtil.EnsureOverlayPanel(OverlayName, 50000);
            if (overlay == null)
                return;

            RuntimeUiDriver driver = overlay.gameObject.GetComponent<RuntimeUiDriver>();
            if (driver == null)
                overlay.gameObject.AddComponent<RuntimeUiDriver>();
        }
    }

    internal sealed class RuntimeUiDriver : MonoBehaviour
    {
        private void Update()
        {
            RuntimeUiRefreshService.UpdateAll();
        }

        private void OnDestroy()
        {
            RuntimeUiRegistry.RequestRebindAll();
        }
    }
}
