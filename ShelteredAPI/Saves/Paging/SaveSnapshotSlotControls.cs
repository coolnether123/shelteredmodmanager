using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Saves.Backups;
using ShelteredAPI.UI;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.Saves.Paging
{
    internal static class SaveSnapshotSlotControls
    {
        internal const float VerificationButtonX = -320f;
        internal const int VerificationButtonSize = 60;
        internal const int VerificationColliderSize = 70;
        private const int ButtonWidth = 84;
        private const int ButtonHeight = 60;
        private const int ColliderWidth = 92;
        private const int ColliderHeight = 70;
        private const int ControlGap = 8;
        private const float SnapshotButtonX =
            VerificationButtonX - ((ColliderWidth + VerificationColliderSize) / 2f) - ControlGap;
        private static readonly Color ButtonRestColor = new Color(0.3f, 0.25f, 0.2f, 0.9f);
        private static readonly Color ButtonHoverColor = new Color(0.43f, 0.35f, 0.27f, 0.98f);
        private static readonly Color LabelRestColor = Color.white;
        private static readonly Color LabelHoverColor = new Color(1f, 0.9f, 0.74f, 1f);

        private static readonly Dictionary<SaveSlotButton, GameObject> Buttons =
            new Dictionary<SaveSlotButton, GameObject>();
        private static string _lastDiagnosticsSignature;

        public static void UpdateButtons(SlotSelectionPanel panel)
        {
            if (panel == null || SaveSnapshotBrowserState.IsActive(panel))
            {
                HideAll();
                return;
            }

            UpdateButtons(panel, SlotSelectionSaveEntryResolver.Resolve(panel));
        }

        public static void UpdateButtons(SlotSelectionPanel panel, IList<SlotSelectionVisibleSave> visibleSaves)
        {
            HideAll();
            if (panel == null || SaveSnapshotBrowserState.IsActive(panel))
                return;

            Dictionary<string, int> snapshotCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (visibleSaves == null)
                return;

            string diagnostics = "page=" + PagingManager.GetPage(panel) + ", visible=" + visibleSaves.Count;
            int buttonsShown = 0;
            for (int i = 0; i < visibleSaves.Count; i++)
            {
                SlotSelectionVisibleSave visible = visibleSaves[i];
                string timelineKey;
                SaveManager.SaveType vanillaSaveType;
                if (!TryResolveTimeline(visible, out timelineKey, out vanillaSaveType))
                {
                    diagnostics += " | slot " + visible.DisplaySlotNumber + ": no timeline";
                    continue;
                }

                int snapshotCount;
                if (!snapshotCounts.TryGetValue(timelineKey, out snapshotCount))
                {
                    snapshotCount = SaveBackupService.CountSnapshots(timelineKey);
                    snapshotCounts[timelineKey] = snapshotCount;
                }

                diagnostics += " | slot " + visible.DisplaySlotNumber + ": timeline=" + timelineKey + ", snapshots=" + snapshotCount;

                if (snapshotCount <= 0)
                    continue;

                GameObject button = GetOrCreateButton(panel, visible.Button);
                button.SetActive(true);
                button.transform.localPosition = new Vector3(SnapshotButtonX, 0, -20);
                buttonsShown++;

                UILabel label = button.transform.Find("Label") != null
                    ? button.transform.Find("Label").GetComponent<UILabel>()
                    : null;
                if (label != null)
                    label.text = "ARCHIVE";

                SnapshotArchiveHoverVisual hover = button.GetComponent<SnapshotArchiveHoverVisual>();
                if (hover != null)
                    hover.SetHover(false);

                string capTimelineKey = timelineKey;
                SaveEntry capEntry = visible.Entry;
                bool capIsVanilla = vanillaSaveType != SaveManager.SaveType.Invalid;
                SaveManager.SaveType capVanillaType = vanillaSaveType;
                int capDisplaySlotNumber = visible.DisplaySlotNumber;
                SaveManager.SaveType capTransportSaveType = visible.TransportSaveType;
                int capTransportSlotNumber = visible.TransportSlotNumber;
                EventDelegate.Set(button.GetComponent<UIButton>().onClick, () =>
                {
                    SaveSnapshotBrowserState.Enter(
                        panel,
                        capTimelineKey,
                        capEntry,
                        capIsVanilla,
                        capVanillaType,
                        capDisplaySlotNumber,
                        capTransportSaveType,
                        capTransportSlotNumber);
                    panel.RefreshSaveSlotInfo();
                    PagingManager.Update(panel);
                });
            }

            diagnostics += " | buttonsShown=" + buttonsShown;
            LogDiagnosticsOnce(diagnostics);
        }

        private static bool TryResolveTimeline(SlotSelectionVisibleSave visible, out string timelineKey, out SaveManager.SaveType vanillaSaveType)
        {
            timelineKey = null;
            vanillaSaveType = SaveManager.SaveType.Invalid;
            if (visible == null)
                return false;

            if (visible.IsVanillaPage)
            {
                if (SaveBackupService.TryGetVanillaTimelineKey(
                    visible.TransportSlotNumber,
                    out timelineKey,
                    out vanillaSaveType)
                    && SaveBackupService.CountSnapshots(timelineKey) > 0)
                {
                    return true;
                }

                timelineKey = null;
                vanillaSaveType = SaveManager.SaveType.Invalid;

                if (visible.Entry != null
                    && IsSmmStoredEntry(visible)
                    && SaveBackupService.TryGetCustomTimelineKey(visible.Entry, out timelineKey))
                    return true;

                if (SaveBackupService.TryFindTimelineKey(
                    "CustomSlot",
                    visible.StorageScenarioId,
                    visible.DisplaySlotNumber,
                    out timelineKey))
                {
                    return true;
                }

                return false;
            }

            if (visible.Entry != null && SaveBackupService.TryGetCustomTimelineKey(visible.Entry, out timelineKey))
                return true;

            return SaveBackupService.TryFindTimelineKey(
                "CustomSlot",
                visible.StorageScenarioId,
                visible.DisplaySlotNumber,
                out timelineKey);
        }

        private static bool IsSmmStoredEntry(SlotSelectionVisibleSave visible)
        {
            if (visible == null || visible.Entry == null)
                return false;

            string scenarioId = string.IsNullOrEmpty(visible.StorageScenarioId) ? "Standard" : visible.StorageScenarioId;
            return System.IO.File.Exists(DirectoryProvider.EntryPath(scenarioId, visible.Entry.absoluteSlot, false));
        }

        private static GameObject GetOrCreateButton(SlotSelectionPanel panel, SaveSlotButton slotButton)
        {
            GameObject button;
            if (Buttons.TryGetValue(slotButton, out button) && button != null)
                return button;

            button = new GameObject("SnapshotBrowserBtn");
            button.transform.SetParent(slotButton.transform, false);
            button.layer = slotButton.gameObject.layer;

            UIPanel parentPanel = NGUITools.FindInParents<UIPanel>(slotButton.gameObject);
            int baseDepth = UIUtil.ComputeSafeDepth(parentPanel, 55);
            UIFontCache.SeedFromGameObject(panel.gameObject, "SaveSnapshotSlotControls");
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();

            UITexture background = button.AddComponent<UITexture>();
            background.mainTexture = UIUtil.WhiteTexture;
            background.width = ButtonWidth;
            background.height = ButtonHeight;
            background.depth = baseDepth;
            background.pivot = UIWidget.Pivot.Center;
            background.color = ButtonRestColor;

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(button.transform, false);
            labelGo.layer = button.layer;

            UILabel label = labelGo.AddComponent<UILabel>();
            label.bitmapFont = fonts.Bitmap;
            label.trueTypeFont = fonts.TTF;
            label.fontSize = 15;
            label.depth = baseDepth + 5;
            label.color = LabelRestColor;
            label.alignment = NGUIText.Alignment.Center;
            label.pivot = UIWidget.Pivot.Center;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            label.width = ButtonWidth - 8;
            label.height = ButtonHeight - 6;

            BoxCollider collider = button.AddComponent<BoxCollider>();
            collider.size = new Vector3(ColliderWidth, ColliderHeight, 1);

            UIButton uiButton = button.AddComponent<UIButton>();
            uiButton.tweenTarget = button;
            uiButton.defaultColor = ButtonRestColor;
            uiButton.hover = ButtonHoverColor;
            uiButton.pressed = ButtonHoverColor;
            uiButton.disabledColor = ButtonRestColor;

            SnapshotArchiveHoverVisual hover = button.AddComponent<SnapshotArchiveHoverVisual>();
            hover.Background = background;
            hover.Label = label;
            hover.RestColor = ButtonRestColor;
            hover.HoverColor = ButtonHoverColor;
            hover.LabelRestColor = LabelRestColor;
            hover.LabelHoverColor = LabelHoverColor;

            UIEventBindingRegistry.BindHover(slotButton.gameObject, "snapshot-archive-" + slotButton.GetInstanceID(), delegate(GameObject go, bool isOver)
            {
                if (hover != null)
                    hover.SetSlotHover(isOver);
            });
            UIEventBindingRegistry.BindHover(button, "snapshot-archive-button", delegate(GameObject go, bool isOver)
            {
                if (hover != null)
                    hover.SetButtonHover(isOver);
            });

            Buttons[slotButton] = button;
            return button;
        }

        private static void HideAll()
        {
            foreach (var pair in Buttons)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(false);
            }
        }

        private static void LogDiagnosticsOnce(string diagnostics)
        {
            if (string.Equals(_lastDiagnosticsSignature, diagnostics, StringComparison.Ordinal))
                return;

            _lastDiagnosticsSignature = diagnostics;
            MMLog.WriteInfo("[SnapshotButtons] " + diagnostics);
        }

    }

    internal sealed class SnapshotArchiveHoverVisual : MonoBehaviour
    {
        public UITexture Background;
        public UILabel Label;
        public Color RestColor;
        public Color HoverColor;
        public Color LabelRestColor;
        public Color LabelHoverColor;

        private bool _slotHover;
        private bool _buttonHover;

        public void SetHover(bool hovered)
        {
            _slotHover = hovered;
            _buttonHover = hovered;
            Apply();
        }

        public void SetSlotHover(bool hovered)
        {
            _slotHover = hovered;
            Apply();
        }

        public void SetButtonHover(bool hovered)
        {
            _buttonHover = hovered;
            Apply();
        }

        private void OnDisable()
        {
            _slotHover = false;
            _buttonHover = false;
            Apply();
        }

        private void Apply()
        {
            bool hovered = _slotHover || _buttonHover;
            if (Background != null)
                Background.color = hovered ? HoverColor : RestColor;
            if (Label != null)
                Label.color = hovered ? LabelHoverColor : LabelRestColor;
        }
    }
}
