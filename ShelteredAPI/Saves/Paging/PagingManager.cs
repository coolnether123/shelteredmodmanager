using System;
using System.Collections.Generic;
using ShelteredAPI.Saves;
using UnityEngine;
using ModAPI.Core;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Saves.Paging
{
    /// <summary>
    /// Manages the state and visual components of the save slot paging UI.
    /// This class is responsible for creating, updating, and handling clicks for the paging controls.
    /// </summary>
    internal static class PagingManager
    {
        private static readonly Dictionary<SlotSelectionPanel, int> _page = new Dictionary<SlotSelectionPanel, int>();
        private static readonly Dictionary<SlotSelectionPanel, UIElements> _ui = new Dictionary<SlotSelectionPanel, UIElements>();
        private static bool _suppressWelcomeDialogOnce;

        internal class UIElements { public GameObject prev; public GameObject next; public GameObject sort; public UILabel label; public GameObject archiveTitleRoot; public UILabel archiveTitle; }

        public static int GetPage(SlotSelectionPanel p) { int v; _page.TryGetValue(p, out v); return v; }
        private static void SetPage(SlotSelectionPanel p, int v) { _page[p] = Math.Max(0, v); }
        public static void SetPageDirect(SlotSelectionPanel p, int v) { SetPage(p, v); }
        public static void Reset(SlotSelectionPanel p) { _page[p] = 0; }
        public static void SuppressWelcomeDialogOnce() { _suppressWelcomeDialogOnce = true; }

        /// <summary>
        /// Ensures the paging UI is created and visible for a given panel.
        /// </summary>
        public static void Initialize(SlotSelectionPanel panel)
        {
            if (_ui.ContainsKey(panel))
            {
                Update(panel); // Ensure it's up-to-date if it already exists
                return;
            }

            var root = panel.gameObject.transform;
            var ui = new UIElements();
            UILabel template = panel.GetComponentInChildren<UILabel>();
            if (template == null) return;

            // Previous Button
            ui.prev = NGUITools.AddChild(root.gameObject, template.gameObject);
            ui.prev.name = "ModAPI_PrevButton";
            ui.prev.transform.localPosition = new Vector3(-280, -200, 0);
            var prevLabel = ui.prev.GetComponent<UILabel>();
            prevLabel.text = "< Prev";
            prevLabel.fontSize = 18;
            NGUITools.AddWidgetCollider(ui.prev);
            if (ui.prev.GetComponent<UIButton>() == null) ui.prev.AddComponent<UIButton>();
            UIEventListener.Get(ui.prev).onClick = (go) => ChangePage(panel, -1);

            // Next Button
            ui.next = NGUITools.AddChild(root.gameObject, template.gameObject);
            ui.next.name = "ModAPI_NextButton";
            ui.next.transform.localPosition = new Vector3(280, -200, 0);
            var nextLabel = ui.next.GetComponent<UILabel>();
            nextLabel.text = "Next >";
            nextLabel.fontSize = 18;
            NGUITools.AddWidgetCollider(ui.next);
            if (ui.next.GetComponent<UIButton>() == null) ui.next.AddComponent<UIButton>();
            UIEventListener.Get(ui.next).onClick = (go) => ChangePage(panel, +1);

            // Page Label
            var pageObj = NGUITools.AddChild(root.gameObject, template.gameObject);
            pageObj.name = "ModAPI_PageLabel";
            pageObj.transform.localPosition = new Vector3(0, -200, 0);
            ui.label = pageObj.GetComponent<UILabel>();

            var archiveTitleObj = NGUITools.AddChild(root.gameObject, template.gameObject);
            archiveTitleObj.name = "ModAPI_ArchiveTitle";
            archiveTitleObj.transform.localPosition = new Vector3(0, 430, 0);
            ui.archiveTitleRoot = archiveTitleObj;
            ui.archiveTitle = archiveTitleObj.GetComponent<UILabel>();
            if (ui.archiveTitle != null)
            {
                ui.archiveTitle.text = "Save Archive";
                ui.archiveTitle.fontSize = 34;
                ui.archiveTitle.width = 920;
                ui.archiveTitle.height = 58;
                ui.archiveTitle.alignment = NGUIText.Alignment.Center;
                ui.archiveTitle.pivot = UIWidget.Pivot.Center;
                ui.archiveTitle.overflowMethod = UILabel.Overflow.ShrinkContent;
            }

            // Sort Toggle (only shown while browsing snapshots)
            ui.sort = NGUITools.AddChild(root.gameObject, template.gameObject);
            ui.sort.name = "ModAPI_SnapshotSortButton";
            ui.sort.transform.localPosition = new Vector3(0, -235, 0);
            var sortLabel = ui.sort.GetComponent<UILabel>();
            sortLabel.text = "Newest first";
            sortLabel.fontSize = 16;
            NGUITools.AddWidgetCollider(ui.sort);
            if (ui.sort.GetComponent<UIButton>() == null) ui.sort.AddComponent<UIButton>();
            UIEventListener.Get(ui.sort).onClick = (go) => ToggleSnapshotSort(panel);

            _ui[panel] = ui;
            Update(panel);
        }

        /// <summary>
        /// Updates the state of the paging buttons and label text.
        /// </summary>
        public static void Update(SlotSelectionPanel panel)
        {
            UIElements ui;
            if (!_ui.TryGetValue(panel, out ui)) return;

            int p = GetPage(panel);
            SlotPagingScope scope = SlotPagingScopeResolver.Resolve(panel);
            SaveSnapshotBrowserSession snapshotSession;
            bool browsingSnapshots = SaveSnapshotBrowserState.TryGet(panel, out snapshotSession);

            bool canPrev = browsingSnapshots ? true : p > 0;

            int maxSlot = scope.GetMaxSlot();
            int lastSavePage = (maxSlot < scope.FirstExpandedSlot) ? 0 : (maxSlot - scope.FirstExpandedSlot) / 3 + 1;
            
            // Allow navigation if we are on the vanilla page (to go to first custom page)
            // or if we hasn't reached the page after the last save yet.
            bool canNext = browsingSnapshots ? p + 1 < snapshotSession.PageCount : (p == 0) || (p <= lastSavePage);

            var prevBtn = ui.prev?.GetComponent<UIButton>();
            var nextBtn = ui.next?.GetComponent<UIButton>();
            if (prevBtn != null) prevBtn.isEnabled = canPrev;
            if (nextBtn != null) nextBtn.isEnabled = canNext;
            if (ui.prev != null)
            {
                UILabel prevLabel = ui.prev.GetComponent<UILabel>();
                if (prevLabel != null) prevLabel.text = browsingSnapshots && p == 0 ? "< Saves" : "< Prev";
            }
            if (ui.label != null)
            {
                ui.label.text = browsingSnapshots
                    ? "Snapshots " + (p + 1) + "/" + snapshotSession.PageCount
                    : "Page " + (p + 1);
            }
            if (ui.sort != null)
            {
                ui.sort.SetActive(browsingSnapshots);
                UILabel sortLabel = ui.sort.GetComponent<UILabel>();
                if (sortLabel != null && browsingSnapshots)
                    sortLabel.text = snapshotSession.SortLabel;
            }
            if (ui.archiveTitleRoot != null)
            {
                ui.archiveTitleRoot.SetActive(browsingSnapshots);
                if (ui.archiveTitle != null && browsingSnapshots)
                    ui.archiveTitle.text = snapshotSession != null ? snapshotSession.ArchiveTitle : "Save Archive";
            }
        }

        /// <summary>
        /// Handles the logic for changing the current page.
        /// </summary>
        public static void ChangePage(SlotSelectionPanel panel, int delta)
        {
            try
            {
                int p = GetPage(panel);

                if (SaveSnapshotBrowserState.IsActive(panel))
                {
                    SaveSnapshotBrowserSession snapshotSession;
                    SaveSnapshotBrowserState.TryGet(panel, out snapshotSession);
                    if (delta < 0 && p <= 0)
                    {
                        SaveSnapshotBrowserState.Exit(panel);
                        panel.RefreshSaveSlotInfo();
                        Update(panel);
                        return;
                    }

                    int targetPage = Math.Max(0, p + delta);
                    int maxPage = snapshotSession != null ? snapshotSession.PageCount - 1 : 0;
                    if (targetPage > maxPage)
                        return;

                    SetPage(panel, targetPage);
                    panel.RefreshSaveSlotInfo();
                    Update(panel);
                    return;
                }

                if (delta < 0 && p <= 0) 
                {
                    MMLog.WriteDebug("[PagingManager] Ignored ChangePage - already at page 0.");
                    return; 
                }

                int newPage = Math.Max(0, p + delta);
                if (newPage == p) return;

                MMLog.WriteDebug(string.Format("[PagingManager] Changing page from {0} to {1}", p, newPage));
                SetPage(panel, newPage);
                
                try
                {
                    ShelteredAPI.Saves.Events.RaisePageChanged(newPage);
                }
                catch (Exception ex)
                {
                     MMLog.WriteError($"[PagingManager] Error raising PageChanged event: {ex}");
                }

                // Tutorial Check
                if (newPage == 1 && ModPrefs.GetInt("ModAPI_HasSeenCustomSavesHelp", 0) == 0)
                {
                    if (_suppressWelcomeDialogOnce)
                    {
                        MMLog.WriteDebug("[PagingManager] Suppressed custom saves welcome dialog for automated page change.");
                    }
                    else
                    {
                        ModPrefs.SetInt("ModAPI_HasSeenCustomSavesHelp", 1);
                        ModPrefs.Save();
                        CustomSavesWelcomeDialog.Show();
                    }
                }

                panel.RefreshSaveSlotInfo(); 
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[PagingManager] Critical error in ChangePage: {ex}");
            }
            finally
            {
                _suppressWelcomeDialogOnce = false;
                // ALWAYS update UI buttons state
                Update(panel);
            }
        }

        private static void ToggleSnapshotSort(SlotSelectionPanel panel)
        {
            SaveSnapshotBrowserState.ToggleSort(panel);
            panel.RefreshSaveSlotInfo();
            Update(panel);
        }
    }
}
