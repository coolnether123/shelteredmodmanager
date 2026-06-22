using System;
using System.IO;
using ModAPI.Core;
using ShelteredAPI.Saves.Runtime;

namespace ShelteredAPI.Saves.Backups
{
    internal static class SaveBackupSidecarCapture
    {
        internal static void EnsureVanillaSidecar(VanillaSaveRoute route, SaveInfo saveInfo)
        {
            try
            {
                string slotRoot = DirectoryProvider.SlotRoot(route.StorageScenarioId, route.AbsoluteSlot, true);
                string manifestPath = Path.Combine(slotRoot, "manifest.json");
                if (File.Exists(manifestPath))
                    return;

                SlotManifest manifest = SaveManifestFacts.CaptureCurrent(saveInfo);
                string writtenPath;
                string error;
                if (!SaveRegistryCore.TryWriteSlotManifest(route.StorageScenarioId, route.AbsoluteSlot, manifest, out writtenPath, out error))
                {
                    MMLog.WriteWarning("[SaveBackup] Vanilla sidecar manifest was not captured for "
                        + route.SaveType + ": " + error);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveBackup] Vanilla sidecar capture skipped for "
                    + route.SaveType + ": " + ex.Message);
            }
        }
    }
}
