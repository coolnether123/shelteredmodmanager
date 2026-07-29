using System;
using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.SaveFlow,
        "Paralives Save Lifecycle Hooks",
        TargetBehavior = "Publishes Paralives save load, save, and unload lifecycle events and drives save-scoped ModAPI persistence.",
        FailureMode = "Mods can still poll save state, but save-scoped lifecycle callbacks and automatic save-context routing are unavailable.",
        RollbackStrategy = "Disable mods depending on Paralives save lifecycle callbacks or unregister the Paralives save runtime adapter.",
        IsOptional = true,
        StartupTiming = PatchStartupTiming.BootCritical)]
    internal static class ParalivesSaveLifecycleHooksPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GameLoadingManager), "CreateRequest", new Type[] { typeof(ulong), typeof(Action), typeof(bool), typeof(bool) })]
        private static void CreateLoadRequestPrefix(ulong saveGameGUID, bool showTutorial, bool fromNewGame)
        {
            ParalivesGameLifecycleFacade.Current.PublishSaveLoading(saveGameGUID, showTutorial, fromNewGame);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GameLoadingManager), "UpdateRequest")]
        private static void LoadingUpdatePrefix(global::GameLoadingRequest request, out global::GameLoadingPhase __state)
        {
            __state = request != null ? request.CurrentPhase : global::GameLoadingPhase.IsDone;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::GameLoadingManager), "UpdateRequest")]
        private static void LoadingUpdatePostfix(global::GameLoadingRequest request, global::GameLoadingPhase __state)
        {
            if (request == null
                || request.SaveGameGUID == 0UL
                || request.OnlyLoadLot
                || __state != global::GameLoadingPhase.ShowGame
                || request.CurrentPhase != global::GameLoadingPhase.IsDone)
            {
                return;
            }

            ParalivesGameLifecycleFacade.Current.PublishSaveLoaded(request.SaveGameGUID, request.FromNewGame);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GameSavingManager), "CreateRequest", new Type[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
        private static void CreateSaveRequestPrefix(
            bool fromAutoSave,
            bool isCopyingSaveAsDefaultTown,
            bool shouldQuitAfterwards,
            bool shouldMainMenuAfterwards)
        {
            ParalivesGameLifecycleFacade.Current.PublishSaveSaving(
                fromAutoSave,
                isCopyingSaveAsDefaultTown,
                shouldQuitAfterwards,
                shouldMainMenuAfterwards);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::GameSavingManager), "UpdateRequest")]
        private static void SavingUpdatePrefix(global::GameSavingRequest request, out global::GameSavingPhase __state)
        {
            __state = request != null ? request.CurrentPhase : global::GameSavingPhase.Completed;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::GameSavingManager), "UpdateRequest")]
        private static void SavingUpdatePostfix(global::GameSavingRequest request, global::GameSavingPhase __state)
        {
            if (request == null
                || __state == global::GameSavingPhase.Completed
                || request.CurrentPhase != global::GameSavingPhase.Completed)
            {
                return;
            }

            ParalivesGameLifecycleFacade.Current.PublishSaveSaved(
                request.FromAutoSave,
                request.CopySaveAsDefaultTown,
                request.ShouldQuitAfterwards,
                request.ShouldMainMenuAfterwards);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::SavedGameManager), "UnloadCurrentGame")]
        private static void UnloadCurrentGamePrefix()
        {
            ParalivesGameLifecycleFacade.Current.PublishSaveUnloading();
        }
    }
}
