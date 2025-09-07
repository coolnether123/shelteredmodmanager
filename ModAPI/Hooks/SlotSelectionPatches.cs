using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Saves;
using UnityEngine;

namespace ModAPI.Hooks
{
    internal static class PageState
    {
        private static readonly Dictionary<SlotSelectionPanel, int> _page = new Dictionary<SlotSelectionPanel, int>();
        private static readonly Dictionary<SlotSelectionPanel, string> _scenario = new Dictionary<SlotSelectionPanel, string>();
        private static readonly Dictionary<SlotSelectionPanel, UIElements> _ui = new Dictionary<SlotSelectionPanel, UIElements>();

        internal class UIElements { public GameObject prev; public GameObject next; public UILabel label; }

        public static int GetPage(SlotSelectionPanel p)
        { int v; if (_page.TryGetValue(p, out v)) return v; return 0; }
        public static void SetPage(SlotSelectionPanel p, int v) { _page[p] = Math.Max(0, v); }
        public static void Reset(SlotSelectionPanel p) { _page[p] = 0; }
        public static void SetScenario(SlotSelectionPanel p, string scenarioId) { _scenario[p] = scenarioId; }
        public static string GetScenario(SlotSelectionPanel p) { string s; return _scenario.TryGetValue(p, out s) ? s : "Standard"; }
        public static UIElements GetUI(SlotSelectionPanel p) { UIElements u; _ui.TryGetValue(p, out u); return u; }
        public static void SetUI(SlotSelectionPanel p, UIElements u) { _ui[p] = u; }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "OnShow")]
    internal static class SlotSelectionPanel_OnShow_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            // Reset page and infer scenario from reservations (first reserved wins); default to Standard
            PageState.Reset(__instance);
            string scenarioId = null;
            for (int i = 1; i <= 3; i++)
            {
                var r = SlotReservationManager.GetSlotReservation(i);
                if (r.usage == SaveSlotUsage.CustomScenario && !string.IsNullOrEmpty(r.scenarioId)) { scenarioId = r.scenarioId; break; }
            }
            if (string.IsNullOrEmpty(scenarioId)) scenarioId = "Standard";
            PageState.SetScenario(__instance, scenarioId);
            MMLog.WriteDebug($"OnShow: scenario={scenarioId}");

            // Add simple Prev/Next buttons + page label if not present
            if (PageState.GetUI(__instance) == null)
            {
                var ui = CreatePagingUI(__instance);
                PageState.SetUI(__instance, ui);
                MMLog.WriteDebug("Created paging UI elements");
            }
            UpdatePagingUI(__instance);
        }

        private static PageState.UIElements CreatePagingUI(SlotSelectionPanel panel)
        {
            var root = panel.gameObject.transform;
            var ui = new PageState.UIElements();

            // Find a UILabel template to inherit font/material settings so it actually renders
            UILabel template = panel.gameObject.GetComponentInChildren<UILabel>();

            // Prev button
            ui.prev = template != null ? GameObject.Instantiate(template.gameObject) as GameObject : new GameObject("CustomPrevButton");
            ui.prev.name = "CustomPrevButton";
            ui.prev.transform.parent = root;
            ui.prev.transform.localScale = Vector3.one;
            ui.prev.transform.localPosition = new Vector3(-280, -200, 0);
            ui.prev.layer = panel.gameObject.layer;
            var prevLabel = ui.prev.GetComponent<UILabel>();
            if (prevLabel == null) prevLabel = ui.prev.AddComponent<UILabel>();
            prevLabel.text = "< Prev";
            prevLabel.fontSize = 18;
            if (ui.prev.GetComponent<BoxCollider>() == null) ui.prev.AddComponent<BoxCollider>();
            try { NGUITools.AddWidgetCollider(ui.prev); } catch { }
            if (ui.prev.GetComponent<UIButton>() == null) ui.prev.AddComponent<UIButton>();
            var prevListener = UIEventListener.Get(ui.prev);
            prevListener.onClick = (go) => { MMLog.WriteDebug("Prev button clicked"); ChangePage(panel, -1); };

            // Next button
            ui.next = template != null ? GameObject.Instantiate(template.gameObject) as GameObject : new GameObject("CustomNextButton");
            ui.next.name = "CustomNextButton";
            ui.next.transform.parent = root;
            ui.next.transform.localScale = Vector3.one;
            ui.next.transform.localPosition = new Vector3(280, -200, 0);
            ui.next.layer = panel.gameObject.layer;
            var nextLabel = ui.next.GetComponent<UILabel>();
            if (nextLabel == null) nextLabel = ui.next.AddComponent<UILabel>();
            nextLabel.text = "Next >";
            nextLabel.fontSize = 18;
            if (ui.next.GetComponent<BoxCollider>() == null) ui.next.AddComponent<BoxCollider>();
            try { NGUITools.AddWidgetCollider(ui.next); } catch { }
            if (ui.next.GetComponent<UIButton>() == null) ui.next.AddComponent<UIButton>();
            var nextListener = UIEventListener.Get(ui.next);
            nextListener.onClick = (go) => { MMLog.WriteDebug("Next button clicked"); ChangePage(panel, +1); };

            // Page indicator label
            var pageObj = template != null ? GameObject.Instantiate(template.gameObject) as GameObject : new GameObject("CustomPageLabel");
            pageObj.name = "CustomPageLabel";
            pageObj.transform.parent = root;
            pageObj.transform.localScale = Vector3.one;
            pageObj.transform.localPosition = new Vector3(0, -200, 0);
            pageObj.layer = panel.gameObject.layer;
            ui.label = pageObj.GetComponent<UILabel>();
            if (ui.label == null) ui.label = pageObj.AddComponent<UILabel>();
            ui.label.text = "Page 1";
            ui.label.fontSize = 18;

            return ui;
        }

        private static void ChangePage(SlotSelectionPanel panel, int delta)
        {
            int p = PageState.GetPage(panel);
            string scenarioId = PageState.GetScenario(panel);
            int total = CustomSaveRegistry.CountSaves(scenarioId);
            if (delta > 0)
            {
                // Allow one extra "creation" page beyond the last filled page.
                bool canNext = (p == 0) || (total > p * 3);
                if (!canNext)
                {
                    MMLog.WriteDebug("ChangePage: blocked (no next page)");
                    return;
                }
            }
            if (delta < 0 && p <= 0)
            {
                MMLog.WriteDebug("ChangePage: blocked (at first page)");
                return;
            }
            int newPage = Math.Max(0, p + delta);
            if (newPage == p) return;
            PageState.SetPage(panel, newPage);
            Events.RaisePageChanged(newPage);
            panel.RefreshSaveSlotInfo();
            UpdatePagingUI(panel);
            MMLog.WriteDebug($"ChangePage: newPage={newPage}");
        }

        private static void UpdatePagingUI(SlotSelectionPanel panel)
        {
            var ui = PageState.GetUI(panel);
            if (ui == null) return;
            int p = PageState.GetPage(panel);
            string scenarioId = PageState.GetScenario(panel);
            int total = CustomSaveRegistry.CountSaves(scenarioId);
            // Allow moving forward from page 1 even with 0 entries (to create new),
            // and allow a single creation page beyond the last filled page.
            bool canPrev = p > 0;
            bool canNext = (p == 0) || (total > p * 3);

            var prevBtn = ui.prev != null ? ui.prev.GetComponent<UIButton>() : null;
            var nextBtn = ui.next != null ? ui.next.GetComponent<UIButton>() : null;
            if (prevBtn != null) prevBtn.isEnabled = canPrev;
            if (nextBtn != null) nextBtn.isEnabled = canNext;
            if (ui.prev != null) ui.prev.SetActive(true);
            if (ui.next != null) ui.next.SetActive(true);
            if (ui.label != null)
            {
                ui.label.text = "Page " + (p + 1).ToString();
            }
            MMLog.WriteDebug($"UpdatePagingUI: page={p} total={total} canPrev={canPrev} canNext={canNext}");
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "Update")]
    internal static class SlotSelectionPanel_Update_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            // Keyboard paging remains
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                PageState.SetPage(__instance, PageState.GetPage(__instance) + 1);
                Events.RaisePageChanged(PageState.GetPage(__instance));
                __instance.RefreshSaveSlotInfo();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                int p = PageState.GetPage(__instance);
                if (p > 0) PageState.SetPage(__instance, p - 1);
                Events.RaisePageChanged(PageState.GetPage(__instance));
                __instance.RefreshSaveSlotInfo();
            }
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSaveSlotInfo")]
    internal static class SlotSelectionPanel_RefreshSaveSlotInfo_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            try
            {
                // Sync UI state
                try
                {
                    var onShowType = typeof(SlotSelectionPanel_OnShow_Patch);
                    var updateUi = Traverse.Create(onShowType).Method("UpdatePagingUI", __instance);
                    updateUi.GetValue();
                }
                catch (Exception e)
                {
                    MMLog.Write("UpdatePagingUI invoke failed: " + e.ToString());
                }

                // Build virtual view for first 3 slots if reserved or if page > 0
                int page = PageState.GetPage(__instance);
                string scenarioId = PageState.GetScenario(__instance);
                MMLog.WriteDebug($"RefreshSaveSlotInfo: page={page} scenario={scenarioId}");

                for (int physical = 1; physical <= 3; physical++)
                {
                    var res = SlotReservationManager.GetSlotReservation(physical);
                    if (page > 0 || res.usage == SaveSlotUsage.CustomScenario)
                    {
                        var entry = CustomSaveRegistry.FindByPhysicalIndex(physical, page, scenarioId, 3);
                        var t = Traverse.Create(__instance);
                        var listObj = t.Field("m_slotInfo").GetValue();
                        var list = listObj as System.Collections.IList;
                        int idx = physical - 1;
                        if (list != null && idx >= 0 && idx < list.Count)
                        {
                            var slotInfo = list[idx];
                            var stateField = Traverse.Create(slotInfo).Field("m_state");
                            var famField = Traverse.Create(slotInfo).Field("m_familyName");
                            var dateField = Traverse.Create(slotInfo).Field("m_dateSaved");
                            var daysField = Traverse.Create(slotInfo).Field("m_daysSurvived");
                            var rainDiff = Traverse.Create(slotInfo).Field("m_rainDiff");
                            var resourceDiff = Traverse.Create(slotInfo).Field("m_resourceDiff");
                            var breachDiff = Traverse.Create(slotInfo).Field("m_breachDiff");
                            var factionDiff = Traverse.Create(slotInfo).Field("m_factionDiff");
                            var moodDiff = Traverse.Create(slotInfo).Field("m_moodDiff");
                            var mapSize = Traverse.Create(slotInfo).Field("m_mapSize");
                            var fog = Traverse.Create(slotInfo).Field("m_fog");
                            var diffSetting = Traverse.Create(slotInfo).Field("m_diffSetting");

                            if (entry != null)
                            {
                                stateField.SetValue(Enum.Parse(typeof(SlotSelectionPanel.SlotState), "Loaded"));
                                famField.SetValue(!string.IsNullOrEmpty(entry.saveInfo.familyName) ? entry.saveInfo.familyName : entry.name);
                                dateField.SetValue(entry.saveInfo.saveTime ?? entry.updatedAt);
                                daysField.SetValue(entry.saveInfo.daysSurvived);
                                rainDiff.SetValue(entry.saveInfo.rainDiff);
                                resourceDiff.SetValue(entry.saveInfo.resourceDiff);
                                breachDiff.SetValue(entry.saveInfo.breachDiff);
                                factionDiff.SetValue(entry.saveInfo.factionDiff);
                                moodDiff.SetValue(entry.saveInfo.moodDiff);
                                mapSize.SetValue(entry.saveInfo.mapSize);
                                fog.SetValue(entry.saveInfo.fog);
                                diffSetting.SetValue(entry.saveInfo.difficulty);
                            }
                            else
                            {
                                stateField.SetValue(Enum.Parse(typeof(SlotSelectionPanel.SlotState), "Empty"));
                                famField.SetValue(string.Empty);
                                dateField.SetValue(string.Empty);
                                daysField.SetValue(0);
                            }
                        }
                    }
                }

                // After injecting slotInfo, refresh labels to show our changes
                bool refreshed = false;
                try
                {
                    var refreshLabels = Traverse.Create(__instance).Method("RefreshSlotLabels");
                    refreshLabels.GetValue();
                    refreshed = true;
                }
                catch (Exception e)
                {
                    MMLog.Write("RefreshSlotLabels invoke failed: " + e.ToString());
                }

                if (!refreshed)
                {
                    // Fallback: update labels directly
                    ManualRefreshLabels(__instance);
                }
                MMLog.WriteDebug("RefreshSaveSlotInfo: labels refreshed");
            }
            catch (Exception ex)
            {
                MMLog.Write("RefreshSaveSlotInfo patch error: " + ex.ToString());
            }
        }

        private static void ManualRefreshLabels(SlotSelectionPanel panel)
        {
            try
            {
                int page = PageState.GetPage(panel);
                string scenarioId = PageState.GetScenario(panel);
                var t = Traverse.Create(panel);
                var buttonLabels = t.Field("m_slotButtonLabels").GetValue() as System.Collections.IList;
                var descLabels = t.Field("m_slotDescLabels").GetValue() as System.Collections.IList;
                var slotInfoList = t.Field("m_slotInfo").GetValue() as System.Collections.IList;
                if (buttonLabels == null || slotInfoList == null) return;
                for (int i = 0; i < buttonLabels.Count && i < 3; i++)
                {
                    var lab = buttonLabels[i] as UILabel; if (lab == null) continue;
                    int shownIndex = i + 1 + (page * 3);
                    var infoObj = slotInfoList[i];
                    string header = Localization.Get("Text.UI.Slot") + " " + shownIndex;
                    string text = header;
                    var state = Traverse.Create(infoObj).Field("m_state").GetValue();
                    string stateName = state != null ? state.ToString() : "Empty";
                    if (stateName == "Empty") text = header + ": " + Localization.Get("Text.UI.Empty");
                    else if (stateName == "Loaded")
                    {
                        string date = Traverse.Create(infoObj).Field("m_dateSaved").GetValue<string>();
                        text = header + ":\n" + (date ?? string.Empty);
                    }
                    lab.text = text;

                    if (descLabels != null && i < descLabels.Count)
                    {
                        var dlab = descLabels[i] as UILabel;
                        if (dlab != null)
                        {
                            string fam = Traverse.Create(infoObj).Field("m_familyName").GetValue<string>() ?? string.Empty;
                            int days = Traverse.Create(infoObj).Field("m_daysSurvived").GetValue<int>();
                            int diff = Traverse.Create(infoObj).Field("m_diffSetting").GetValue<int>();
                            string daysStr = days <= 1 ? Localization.Get("Text.UI.SurvivedOneDay") : Localization.Get("Text.UI.SurvivedXDays").Replace("$1", days.ToString());
                            string diffStr = Localization.Get("ui.difficulty.difficulty") + ": ";
                            switch (diff)
                            {
                                case 0: diffStr += Localization.Get("ui.difficulty.difficultyeasy"); break;
                                case 1: diffStr += Localization.Get("ui.difficulty.difficultynormal"); break;
                                case 2: diffStr += Localization.Get("ui.difficulty.difficultyhard"); break;
                                case 3: diffStr += Localization.Get("ui.difficulty.difficultyhardcore"); break;
                                case 4: diffStr += Localization.Get("ui.difficulty.difficultycustom"); break;
                                default: diffStr += Localization.Get("ui.difficulty.difficultynormal"); break;
                            }
                            dlab.text = (stateName == "Loaded") ? (Localization.Get("Text.UI.TheFamily").Replace("$1", fam) + ", " + daysStr + "\n" + diffStr) : string.Empty;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MMLog.Write("ManualRefreshLabels failed: " + e.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSlotLabels")]
    internal static class SlotSelectionPanel_RefreshSlotLabels_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            try
            {
                int page = PageState.GetPage(__instance);
                if (page <= 0) return;
                int offset = page * 3;
                var t = Traverse.Create(__instance);
                var labels = t.Field("m_slotButtonLabels").GetValue() as System.Collections.IList;
                if (labels == null) return;
                for (int i = 0; i < labels.Count && i < 3; i++)
                {
                    var lab = labels[i] as UILabel;
                    if (lab == null || string.IsNullOrEmpty(lab.text)) continue;
                    // Replace the first number we find with (i+1+offset)
                    string old = lab.text;
                    string updated = ReplaceFirstNumber(old, (i + 1 + offset).ToString());
                    if (updated != old) lab.text = updated;
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("RefreshSlotLabels patch error: " + ex.Message);
            }
        }

        private static string ReplaceFirstNumber(string s, string replacement)
        {
            int start = -1, len = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsDigit(s[i])) { start = i; break; }
            }
            if (start < 0) return s;
            for (int i = start; i < s.Length && char.IsDigit(s[i]); i++) len++;
            return s.Substring(0, start) + replacement + s.Substring(start + len);
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotChosen")]
    internal static class SlotSelectionPanel_OnSlotChosen_Patch
    {
        static bool Prefix(SlotSelectionPanel __instance)
        {
            try
            {
                int page = PageState.GetPage(__instance);
                string scenarioId = PageState.GetScenario(__instance);
                if (page <= 0)
                {
                    var t = Traverse.Create(__instance);
                    int chosen = t.Field("m_chosenSlot").GetValue<int>();
                    if (chosen < 0 || chosen > 2) return true; // let vanilla handle Surrounded/Stasis
                    var res = SlotReservationManager.GetSlotReservation(chosen + 1);
                    if (res.usage != SaveSlotUsage.CustomScenario) return true;
                }

                var t2 = Traverse.Create(__instance);
                int chosenSlot = t2.Field("m_chosenSlot").GetValue<int>();
                int physical = chosenSlot + 1; // 1..3
                var entry = CustomSaveRegistry.FindByPhysicalIndex(physical, page, scenarioId, 3);
                if (entry == null)
                {
                    // Empty: set up a new entry and map next save; let vanilla flow open customization
                    SaveManager.SaveType type = SaveManager.SaveType.Slot1;
                    if (physical == 2) type = SaveManager.SaveType.Slot2; else if (physical == 3) type = SaveManager.SaveType.Slot3;
                    var created = CustomSaveRegistry.CreateSave(scenarioId, new SaveCreateOptions { name = "Run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") });
                    var proxyNew = SaveManager.instance.platformSave as PlatformSaveProxy;
                    if (proxyNew != null && created != null)
                    {
                        proxyNew.SetNextSave(type, scenarioId, created.id);
                        MMLog.WriteDebug($"OnSlotChosen: created new entry id={created.id} for scenario={scenarioId} physical={physical}");
                    }
                    return true; // proceed with vanilla (customization)
                }

                // Set difficulty settings consistent with vanilla (guard if missing)
                DifficultyManager.StoreMenuDifficultySettings(
                    entry.saveInfo.rainDiff,
                    entry.saveInfo.resourceDiff,
                    entry.saveInfo.breachDiff,
                    entry.saveInfo.factionDiff,
                    entry.saveInfo.moodDiff,
                    entry.saveInfo.mapSize,
                    entry.saveInfo.fog);

                SaveManager.SaveType slotType = SaveManager.SaveType.Slot1;
                if (physical == 2) slotType = SaveManager.SaveType.Slot2;
                else if (physical == 3) slotType = SaveManager.SaveType.Slot3;

                var proxy = SaveManager.instance.platformSave as PlatformSaveProxy;
                if (proxy != null)
                {
                    proxy.SetNextLoad(slotType, scenarioId, entry.id);
                }
                SaveManager.instance.SetSlotToLoad(physical);
                MMLog.WriteDebug($"OnSlotChosen: loading entry id={entry.id} scenario={scenarioId} physical={physical}");
                return false;
            }
            catch (Exception ex)
            {
                MMLog.Write("OnSlotChosen patch error: " + ex.Message);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "OnDeleteMessageBox")]
    internal static class SlotSelectionPanel_OnDeleteMessageBox_Patch
    {
        static bool Prefix(SlotSelectionPanel __instance, int response)
        {
            try
            {
                if (response != 1) return true; // No pressed; let vanilla handle
                int page = PageState.GetPage(__instance);
                string scenarioId = PageState.GetScenario(__instance);
                var t = Traverse.Create(__instance);
                int selected = t.Field("m_selectedSlot").GetValue<int>();
                if (selected < 0 || selected > 2) return true; // let vanilla handle slots 4/5
                var res = SlotReservationManager.GetSlotReservation(selected + 1);
                if (page <= 0 && res.usage != SaveSlotUsage.CustomScenario) return true; // vanilla slot

                var entry = CustomSaveRegistry.FindByPhysicalIndex(selected + 1, page, scenarioId, 3);
                if (entry == null) return false; // nothing to delete; consume
                CustomSaveRegistry.DeleteSave(scenarioId, entry.id);
                t.Field("m_infoNeedsRefresh").SetValue(true);
                MMLog.WriteDebug($"OnDeleteMessageBox: deleted id={entry.id} scenario={scenarioId}");
                return false; // skip vanilla delete
            }
            catch (Exception ex)
            {
                MMLog.Write("OnDeleteMessageBox patch error: " + ex.Message);
                return true;
            }
        }
    }

    // Extra signal that the class exists and Harmony hooked it
    [HarmonyPatch(typeof(SlotSelectionPanel), "Start")]
    internal static class SlotSelectionPanel_Start_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            MMLog.WriteDebug("SlotSelectionPanel.Start postfix fired");
        }
    }
}
