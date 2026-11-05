using HarmonyLib;
using ModAPI.Harmony;

namespace ShelteredAPI.Events
{
    [PatchPolicy(PatchDomain.UI, "TradingPanelEvents",
        TargetBehavior = "Notify subscribers after trade calculations and selection labels.",
        FailureMode = "Trading extensions do not refresh.", RollbackStrategy = "Disable the UI patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(TradingPanel), "UpdateTrade")]
    internal static class TradeUpdatedPatch
    {
        private static void Postfix(TradingPanel __instance) { UIEvents.RaiseTradeUpdated(__instance); }
    }

    [PatchPolicy(PatchDomain.UI, "TradingPanelEvents",
        TargetBehavior = "Notify subscribers after selection labels.",
        FailureMode = "Selection extensions do not refresh.", RollbackStrategy = "Disable the UI patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(TradingPanel), "UpdateSelectionLabels")]
    internal static class TradeSelectionLabelsUpdatedPatch
    {
        private static void Postfix(TradingPanel __instance) { UIEvents.RaiseTradeSelectionLabelsUpdated(__instance); }
    }

    [PatchPolicy(PatchDomain.UI, "ExpeditionPanelEvents",
        TargetBehavior = "Notify subscribers after party panel setup.",
        FailureMode = "Party panel extensions do not initialize.", RollbackStrategy = "Disable the UI patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExpeditionMainPanelNew), "OnShow")]
    internal static class ExpeditionPanelShownPatch
    {
        private static void Postfix(ExpeditionMainPanelNew __instance) { UIEvents.RaiseExpeditionPanelShown(__instance); }
    }

    [PatchPolicy(PatchDomain.UI, "ExpeditionPanelEvents",
        TargetBehavior = "Notify subscribers after party panel frame updates.",
        FailureMode = "Party panel extensions stop updating.", RollbackStrategy = "Disable the UI patch domain.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExpeditionMainPanelNew), "Update")]
    internal static class ExpeditionPanelUpdatedPatch
    {
        private static void Postfix(ExpeditionMainPanelNew __instance) { UIEvents.RaiseExpeditionPanelUpdated(__instance); }
    }
}
