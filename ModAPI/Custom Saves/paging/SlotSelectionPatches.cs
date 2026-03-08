using HarmonyLib;
using ModAPI.Harmony;

namespace ModAPI.Hooks
{
    /// <summary>
    /// This static class holds state that must persist across scene loads,
    /// specifically the intended "slot in use" for the SaveManager.
    /// </summary>
    internal static class SaveManagerContext
    {
        public static SaveManager.SaveType IntendedSlotInUse = SaveManager.SaveType.Invalid;
    }

    [PatchPolicy(PatchDomain.SaveFlow, "SlotSelectionPagingInit",
        TargetBehavior = "Paged custom-save initialization when the slot selection panel opens",
        FailureMode = "Paged save UI does not initialize correctly.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the slot selection init patch.")]
    [HarmonyPatch(typeof(SlotSelectionPanel), "OnShow")]
    internal static class SlotSelectionPanel_OnShow_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            SlotSelectionPatchCoordinator.Initialize(__instance);
        }
    }

    [PatchPolicy(PatchDomain.SaveFlow, "SlotSelectionRefreshTakeover",
        TargetBehavior = "Custom-save page takeover for slot info rendering",
        FailureMode = "Custom saves render incorrectly or page state drifts from save metadata.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the slot info refresh takeover.")]
    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSaveSlotInfo")]
    internal static class SlotSelectionPanel_RefreshSaveSlotInfo_Patch
    {
        static bool Prefix(SlotSelectionPanel __instance)
        {
            return SlotSelectionPatchCoordinator.RefreshSaveSlotInfoPrefix(__instance);
        }

        static void Postfix(SlotSelectionPanel __instance)
        {
            SlotSelectionPatchCoordinator.RefreshSaveSlotInfoPostfix(__instance);
        }
    }

    [PatchPolicy(PatchDomain.SaveFlow, "SlotSelectionLabelRewrite",
        TargetBehavior = "Visible slot label rewrite for paged custom saves",
        FailureMode = "Paged custom slots show misleading slot numbers.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the slot label rewrite patch.")]
    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSlotLabels")]
    internal static class SlotSelectionPanel_RefreshSlotLabels_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            SlotSelectionPatchCoordinator.RefreshSlotLabelsPostfix(__instance);
        }
    }

    [PatchPolicy(PatchDomain.SaveFlow, "SlotSelectionLoadSaveRouting",
        TargetBehavior = "Custom-save create/load/delete routing from slot selection",
        FailureMode = "Loads, deletes, or new games can target the wrong slot or skip verification.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the slot chosen routing patch.")]
    [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotChosen")]
    internal static class SlotSelectionPanel_OnSlotChosen_Patch
    {
        static bool Prefix(SlotSelectionPanel __instance)
        {
            return SlotSelectionPatchCoordinator.OnSlotChosenPrefix(__instance);
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnDeleteMessageBox")]
        internal static class SlotSelectionPanel_OnDeleteMessageBox_Patch
        {
            static bool Prefix(SlotSelectionPanel __instance, int response)
            {
                return SlotSelectionPatchCoordinator.OnDeleteMessageBoxPrefix(__instance, response);
            }
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "Update")]
        internal static class SlotSelectionPanel_Update_Patch
        {
            static void Postfix(SlotSelectionPanel __instance)
            {
                SlotSelectionPatchCoordinator.UpdatePostfix(__instance);
            }
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "Start")]
        internal static class SlotSelectionPanel_Start_Patch
        {
            static void Postfix()
            {
            }
        }
    }
}
