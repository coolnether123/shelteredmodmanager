using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Backups;
using ShelteredAPI.Saves.Runtime;
using Smm.FirstRun;

namespace ShelteredAPI.Core
{
    internal static class FirstRunSaveGuard
    {
        internal static void Initialize()
        {
            Exception error;
            if (PreUpgradeSaveBackup.TryPrepare(ModApiPaths.GameRoot, out error)) return;

            MMLog.WriteError("The pre-2.0 save backup failed: " + error);
            // This protection is required even when the optional SaveFlow patches are disabled.
            var harmony = new HarmonyLib.Harmony("ShelteredModManager.FirstRunSaveGuard");
            var prefix = new HarmonyMethod(AccessTools.Method(typeof(FirstRunSaveGuard), nameof(BlockSave)))
            {
                priority = Priority.First
            };
            Patch(harmony, prefix, typeof(SaveManager),
                "StartSave", "SaveToCurrentSlot", "SaveGlobalData", "DeleteSlot", "DeleteCurrentSlot");
            Patch(harmony, prefix, typeof(SaveManager).Assembly.GetType("PlatformSave_PC"), "PlatformSave", "PlatformDelete");
            Patch(harmony, prefix, typeof(PlatformSaveProxy), "PlatformSave", "PlatformDelete");
            Patch(harmony, prefix, typeof(SaveRegistryCore),
                "CreateSave", "OverwriteSave", "DeleteSave", "CondenseSlots", "TryWriteEntryFile", "TryWriteSlotManifest");
            Patch(harmony, prefix, typeof(SaveBackupRepository), "RestoreSnapshot");
        }

        private static void Patch(HarmonyLib.Harmony harmony, HarmonyMethod prefix, Type type, params string[] names)
        {
            if (type == null)
            {
                MMLog.WriteError("The pre-2.0 save guard could not find the PC save implementation.");
                return;
            }
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
            {
                if (Array.IndexOf(names, method.Name) < 0) continue;
                try { harmony.Patch(method, prefix: prefix); }
                catch (Exception error) { MMLog.WriteError("Could not guard " + type.Name + "." + method.Name + ": " + error); }
            }
        }

        private static bool BlockSave()
        {
            const string message = "Sheltered could not verify the pre-2.0 backup, so saving is blocked. Close Sheltered and open Mod Manager to retry the backup.";
            // A save can be attempted before the game's message box is ready.
            try { MessageBox.Show(MessageBoxButtons.Okay_Button, message, null, null, null, false); }
            catch (Exception error) { MMLog.WriteError(message + " " + error.Message); }
            return false;
        }
    }
}
