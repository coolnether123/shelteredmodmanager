using System;
using System.Collections.Generic;
using ModAPI.Saves;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Saves;

namespace ModAPI.Hooks.Paging
{
    /// <summary>
    /// Manages the state and visual components of the save slot paging UI.
    /// This class is responsible for creating, updating, and handling clicks for the paging controls.
    /// </summary>
    internal static class PagingManager
    {
        private static readonly Dictionary<SlotSelectionPanel, int> _page = new Dictionary<SlotSelectionPanel, int>();
        private static readonly Dictionary<SlotSelectionPanel, UIElements> _ui = new Dictionary<SlotSelectionPanel, UIElements>();

        internal class UIElements { public GameObject prev; public GameObject next; public UILabel label; }

        public static int GetPage(SlotSelectionPanel p) { int v; _page.TryGetValue(p, out v); return v; }
        private static void SetPage(SlotSelectionPanel p, int v) { _page[p] = Math.Max(0, v); }
        public static void Reset(SlotSelectionPanel p) { _page[p] = 0; }

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
            int totalExpanded = ExpandedVanillaSaves.Count();

            bool canPrev = p > 0;

            int maxSlot = ExpandedVanillaSaves.GetMaxSlot();
            int lastSavePage = (maxSlot < 4) ? 0 : (maxSlot - 4) / 3 + 1;
            
            // Allow navigation if we are on the vanilla page (to go to first custom page)
            // or if we hasn't reached the page after the last save yet.
            bool canNext = (p == 0) || (p <= lastSavePage);

            var prevBtn = ui.prev?.GetComponent<UIButton>();
            var nextBtn = ui.next?.GetComponent<UIButton>();
            if (prevBtn != null) prevBtn.isEnabled = canPrev;
            if (nextBtn != null) nextBtn.isEnabled = canNext;
            if (ui.label != null) ui.label.text = "Page " + (p + 1);
        }

        /// <summary>
        /// Handles the logic for changing the current page.
        /// </summary>
        public static void ChangePage(SlotSelectionPanel panel, int delta)
        {
            int p = GetPage(panel);

            if (delta < 0 && p <= 0) return; // Cannot go back from page 1

            int newPage = Math.Max(0, p + delta);
            if (newPage == p) return;

            SetPage(panel, newPage);
            ModAPI.Saves.Events.RaisePageChanged(newPage); // Triggers RefreshSaveSlotInfo patch which updates UI

            // Tutorial Check
            if (newPage == 1 && ModPrefs.GetInt("ModAPI_HasSeenCustomSavesHelp", 0) == 0)
            {
                ModPrefs.SetInt("ModAPI_HasSeenCustomSavesHelp", 1);
                ModPrefs.Save();
                MessageBox.Show(MessageBoxButtons.Okay_Button, "Welcome to Custom Saves!\n\nPage 2+ contains unlimited custom saves.\nUse arrows or keyboard to navigate pages.\n\nSaves stay in their slots permanently.\nIf gaps occur (from deleting saves), you'll be\nasked at game startup if you want to reorganize.\n\nSlots 1-3 are vanilla saves.");
            }

            panel.RefreshSaveSlotInfo(); // This will trigger our Postfix patch to update the UI
            Update(panel);
        }
    }
}