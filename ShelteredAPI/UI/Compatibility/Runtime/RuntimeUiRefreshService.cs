using UnityEngine;

namespace ShelteredAPI.UI.Internal
{
    internal static class RuntimeUiRefreshService
    {
        public static void Refresh(string panelId)
        {
            RuntimeUiPanelRecord record;
            if (!RuntimeUiRegistry.TryGet(panelId, out record) || record == null)
                return;

            UIRuntimeServiceHelper.Run("RuntimeUiRefreshService.Refresh", delegate
            {
                if (record.Root == null)
                {
                    record.RebindRequested = true;
                    Update(record);
                    return;
                }

                if (record.Refresh != null)
                    record.Refresh(record);

                if (record.RebindRequested)
                    Update(record);
            });
        }

        public static void UpdateAll()
        {
            RuntimeUiPanelRecord[] records = RuntimeUiRegistry.Snapshot();
            for (int i = 0; i < records.Length; i++)
                Update(records[i]);
        }

        private static void Update(RuntimeUiPanelRecord record)
        {
            if (record == null)
                return;

            UIRuntimeServiceHelper.Run("RuntimeUiRefreshService.Update", delegate
            {
                bool missingRoot = record.Root == null;
                if (missingRoot || record.RebindRequested)
                {
                    if (record.Root != null)
                        Object.Destroy(record.Root);
                    record.Root = null;

                    if (record.Build != null)
                        record.Build(record);

                    record.RebindRequested = false;
                }

                if (record.RefreshEveryFrame && record.Refresh != null)
                    record.Refresh(record);
            });
        }
    }
}
