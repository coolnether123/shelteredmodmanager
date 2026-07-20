using System;
using System.Collections.Generic;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private void DrawInventorySlotGridSection(ScenarioAuthoringInspectorSection section, bool compactInspector)
        {
            float availableWidth = GetSectionContentWidth();
            DrawInventorySlotSectionHeaderItems(section, availableWidth);

            ScenarioInventorySlotGridViewModel grid = section != null ? section.InventorySlotGrid : null;
            ScenarioInventorySlotViewModel[] slots = grid != null ? grid.Slots : null;
            if (slots == null || slots.Length == 0 || CountRenderableInventorySlots(slots) == 0)
            {
                GUILayout.Label(grid != null ? grid.EmptyMessage ?? string.Empty : string.Empty, _mutedTextStyle);
                return;
            }

            float gap = compactInspector ? 5f : 6f;
            bool timed = HasInventoryScheduleSlots(slots);
            bool readableSupplyCards = IsStartingSupplyGrid(section) && !timed;
            float minSlot = timed
                ? (compactInspector ? 86f : 96f)
                : (readableSupplyCards ? (compactInspector ? 78f : 88f) : (compactInspector ? 54f : 62f));
            float preferredSlot = timed
                ? (compactInspector ? 98f : 112f)
                : (readableSupplyCards ? (compactInspector ? 88f : 98f) : (compactInspector ? 64f : 74f));
            int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + gap) / (minSlot + gap)));
            float slotSize = Mathf.Min(preferredSlot, (availableWidth - (gap * (columns - 1))) / columns);
            float responsiveMinimum = readableSupplyCards
                ? Math.Min(minSlot, Math.Max(54f, availableWidth))
                : minSlot;
            float responsiveMaximum = readableSupplyCards
                ? Math.Min(preferredSlot, Math.Max(responsiveMinimum, availableWidth))
                : preferredSlot;
            slotSize = Mathf.Clamp(slotSize, responsiveMinimum, responsiveMaximum);
            float cellHeight = timed
                ? slotSize + 112f
                : slotSize + (readableSupplyCards ? 68f : 48f);

            int column = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < slots.Length; i++)
            {
                ScenarioInventorySlotViewModel slot = slots[i];
                if (slot == null)
                    continue;

                Rect cellRect = GUILayoutUtility.GetRect(slotSize, cellHeight, GUILayout.Width(slotSize), GUILayout.Height(cellHeight));
                DrawInventorySlotCell(
                    cellRect,
                    slot,
                    grid != null && grid.ReadOnly,
                    timed,
                    readableSupplyCards);
                column++;
                if (column >= columns && HasMoreInventorySlots(slots, i + 1))
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(gap);
                    GUILayout.BeginHorizontal();
                    column = 0;
                }
                else if (column < columns)
                {
                    GUILayout.Space(gap);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawInventorySlotSectionHeaderItems(ScenarioAuthoringInspectorSection section, float availableWidth)
        {
            if (section == null || section.Items == null)
                return;

            float rowWidth = 0f;
            bool drewAction = false;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action == null)
                    continue;

                float width = Math.Max(96f, MeasureButtonWidth(item.Action, false, 22f));
                width = Math.Min(width, availableWidth);
                if (rowWidth > 0f && rowWidth + width > availableWidth)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                    GUILayout.BeginHorizontal();
                    rowWidth = 0f;
                }

                Rect rect = GUILayoutUtility.GetRect(width, 28f, GUILayout.Width(width), GUILayout.Height(28f));
                DrawButton(rect, item.Action, false);
                GUILayout.Space(8f);
                rowWidth += width + 8f;
                drewAction = true;
            }
            GUILayout.EndHorizontal();
            if (drewAction)
                GUILayout.Space(8f);

            for (int i = 0; i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null || item.Action != null)
                    continue;

                DrawItem(item, false);
            }
        }

        private void DrawInventorySlotCell(
            Rect cellRect,
            ScenarioInventorySlotViewModel slot,
            bool gridReadOnly,
            bool reserveScheduleSpace,
            bool readableSupplyCard)
        {
            bool readOnly = gridReadOnly || (slot != null && slot.ReadOnly);
            float slotSize = cellRect.width;
            Rect slotRect = new Rect(cellRect.x, cellRect.y, slotSize, slotSize);
            ScenarioAuthoringInspectorAction primary = slot != null ? slot.PrimaryAction : null;
            bool clickable = !readOnly && primary != null && primary.Enabled && !string.IsNullOrEmpty(primary.Id);
            string tooltip = BuildInventorySlotTooltip(slot, readOnly);

            RegisterInteractiveRegion(slotRect);
            if (primary != null && !readOnly && RegisterRichHoverHelpSource(slotRect, primary))
                tooltip = string.Empty;

            GUIStyle style = _uiContext.Styles.Field;
            if (DrawPlainButton(slotRect, new GUIContent(string.Empty, tooltip), style, clickable))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(primary.Id);
                if (Event.current != null)
                    Event.current.Use();
            }

            bool hovered = clickable && IsInteractiveHoverAllowed(slotRect);
            bool pressed = clickable && IsInteractiveMouseDownAllowed(slotRect);
            DrawButtonAnimationOverlay(slotRect, primary != null ? primary.Id : null, clickable, hovered, pressed);
            DrawInventorySlotIcon(slotRect, slot);
            DrawInventorySlotBadge(slotRect, slot, readableSupplyCard);
            DrawInventoryQuantityBadge(slotRect, slot);
            if (slot != null && slot.Emphasized)
            {
                ScenarioUiAtlasSkin.DrawCornerCutBorder(slotRect, _uiContext.Styles.FocusBorderTexture, _uiContext.Styles.FocusBorderTexture);
                ScenarioUiAtlasSkin.DrawCornerCutBorder(new Rect(slotRect.x + 1f, slotRect.y + 1f, Math.Max(0f, slotRect.width - 2f), Math.Max(0f, slotRect.height - 2f)), _uiContext.Styles.FocusBorderTexture, _uiContext.Styles.FocusBorderTexture);
            }
            if (slot != null && slot.Empty && clickable)
                DrawInventoryEmptyAddGlyph(slotRect);
            if (!clickable && readOnly)
                DrawInventoryReadOnlyCorner(slotRect);
            ScenarioUiAtlasSkin.DrawCornerCutBorder(slotRect, _uiContext.Styles.BorderSubtleTexture, _uiContext.Styles.BorderSubtleTexture);

            float labelHeight = reserveScheduleSpace ? 32f : (readableSupplyCard ? 38f : 30f);
            Rect labelRect = new Rect(cellRect.x, slotRect.yMax + 3f, cellRect.width, labelHeight);
            GUIStyle labelStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
            labelStyle.wordWrap = true;
            labelStyle.clipping = TextClipping.Clip;
            labelStyle.alignment = TextAnchor.UpperLeft;
            GUI.Label(labelRect, slot != null ? slot.DisplayName ?? string.Empty : string.Empty, labelStyle);

            if (reserveScheduleSpace)
            {
                Rect scheduleRect = new Rect(cellRect.x, labelRect.yMax + 1f, cellRect.width, 15f);
                GUI.Label(scheduleRect, ShortenToFit(slot != null ? slot.ScheduleText : null, scheduleRect.width, _mutedTextStyle), _mutedTextStyle);
            }

            DrawInventorySlotControls(
                cellRect,
                slot,
                readOnly,
                reserveScheduleSpace,
                readableSupplyCard,
                labelRect.yMax);
        }

        private void DrawInventorySlotControls(
            Rect cellRect,
            ScenarioInventorySlotViewModel slot,
            bool readOnly,
            bool reserveScheduleSpace,
            bool readableSupplyCard,
            float labelBottom)
        {
            if (slot == null || slot.Empty || readOnly)
                return;

            float controlHeight = readableSupplyCard ? 22f : 18f;
            float quantityWidth = readableSupplyCard
                ? (cellRect.width >= 82f ? 24f : (cellRect.width >= 70f ? 18f : 15f))
                : 18f;
            float controlGap = readableSupplyCard
                ? (cellRect.width >= 82f ? 3f : (cellRect.width >= 70f ? 2f : 1f))
                : 2f;
            float removeWidth = readableSupplyCard
                ? Mathf.Clamp(cellRect.width - (quantityWidth * 2f) - (controlGap * 2f), 22f, 30f)
                : 20f;
            float y = readableSupplyCard
                ? labelBottom + 3f
                : cellRect.y + cellRect.width + (reserveScheduleSpace ? 51f : 32f);
            Rect minusRect = new Rect(cellRect.x, y, quantityWidth, controlHeight);
            Rect plusRect = new Rect(minusRect.xMax + controlGap, y, quantityWidth, controlHeight);
            Rect removeRect = new Rect(cellRect.xMax - removeWidth, y, removeWidth, controlHeight);
            if (slot.QuantityDecreaseAction != null)
                DrawButton(minusRect, slot.QuantityDecreaseAction, false);
            if (slot.QuantityIncreaseAction != null)
                DrawButton(plusRect, slot.QuantityIncreaseAction, false);
            if (slot.RemoveAction != null)
                DrawButton(removeRect, slot.RemoveAction, false);

            if (slot.KindAction == null && (slot.TimeActions == null || slot.TimeActions.Length == 0))
                return;

            if (slot.KindAction != null)
                DrawButton(new Rect(cellRect.x, y, Math.Min(42f, cellRect.width), controlHeight), slot.KindAction, false);

            y += 20f;
            float x = cellRect.x;
            for (int i = 0; slot.TimeActions != null && i < slot.TimeActions.Length; i++)
            {
                if (x + 22f > cellRect.xMax)
                {
                    x = cellRect.x;
                    y += 20f;
                    if (y + 18f > cellRect.yMax)
                        break;
                }

                DrawButton(new Rect(x, y, 20f, 18f), slot.TimeActions[i], false);
                x += 22f;
            }
        }

        private void DrawInventorySlotIcon(Rect slotRect, ScenarioInventorySlotViewModel slot)
        {
            Rect iconRect = new Rect(slotRect.x + 7f, slotRect.y + 9f, slotRect.width - 14f, slotRect.height - 18f);
            Sprite sprite = slot != null ? slot.PreviewSprite : null;
            if (sprite != null)
            {
                DrawSpritePreview(iconRect, sprite, false);
                return;
            }

            GUI.Box(iconRect, GUIContent.none, _uiContext.Styles.Field);
            string glyph = slot != null && slot.Empty ? "+" : "?";
            GUIStyle glyphStyle = new GUIStyle(_sectionTitleStyle);
            glyphStyle.alignment = TextAnchor.MiddleCenter;
            glyphStyle.clipping = TextClipping.Clip;
            GUI.Label(iconRect, glyph, glyphStyle);
        }

        private void DrawInventorySlotBadge(
            Rect slotRect,
            ScenarioInventorySlotViewModel slot,
            bool compactLongBadge)
        {
            if (slot == null || string.IsNullOrEmpty(slot.Badge))
                return;

            string badge = compactLongBadge
                ? ResolveSupplyCardBadge(slot.Badge, slotRect.width - 8f)
                : slot.Badge;
            float badgeWidth = Mathf.Min(slotRect.width - 8f, Math.Max(44f, MeasurePillWidth(badge, 18f)));
            Rect badgeRect = new Rect(slotRect.x + 4f, slotRect.y + 4f, badgeWidth, 17f);
            ScenarioUiPillEmphasis emphasis = ScenarioUiPillEmphasis.Default;
            if (StringContains(slot.Badge, "+") || StringContains(slot.Badge, "START"))
                emphasis = ScenarioUiPillEmphasis.Active;
            else if (StringContains(slot.Badge, "-"))
                emphasis = ScenarioUiPillEmphasis.Warning;
            ScenarioUiWidgets.DrawPill(badgeRect, badge, _uiContext.Styles, emphasis);
        }

        private string ResolveSupplyCardBadge(string badge, float availableWidth)
        {
            if (string.IsNullOrEmpty(badge) || MeasurePillWidth(badge, 18f) <= availableWidth)
                return badge;
            if (StringContains(badge, "EDITOR"))
                return "EDITOR";
            if (StringContains(badge, "START"))
                return "START";
            return ShortenToFit(badge, availableWidth - 12f, _mutedTextStyle);
        }

        private void DrawInventoryQuantityBadge(Rect slotRect, ScenarioInventorySlotViewModel slot)
        {
            if (slot == null || slot.Empty || string.IsNullOrEmpty(slot.QuantityText))
                return;

            float width = Mathf.Min(slotRect.width - 8f, Math.Max(32f, MeasurePillWidth(slot.QuantityText, 16f)));
            Rect qtyRect = new Rect(slotRect.xMax - width - 4f, slotRect.yMax - 21f, width, 17f);
            ScenarioUiWidgets.DrawPill(qtyRect, slot.QuantityText, _uiContext.Styles, ScenarioUiPillEmphasis.Default);
        }

        private float MeasurePillWidth(string label, float padding)
        {
            if (string.IsNullOrEmpty(label))
                return padding;

            GUIStyle style = _uiContext != null && _uiContext.Styles != null && _uiContext.Styles.Pill != null
                ? _uiContext.Styles.Pill
                : _mutedTextStyle;
            return (style != null ? style.CalcSize(new GUIContent(label)).x : 0f) + padding;
        }

        private void DrawInventoryEmptyAddGlyph(Rect slotRect)
        {
            Rect glyphRect = new Rect(slotRect.x + 10f, slotRect.y + 26f, slotRect.width - 20f, slotRect.height - 42f);
            GUIStyle glyphStyle = new GUIStyle(_sectionTitleStyle);
            glyphStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(glyphRect, "+", glyphStyle);
        }

        private void DrawInventoryReadOnlyCorner(Rect slotRect)
        {
            Rect cornerRect = new Rect(slotRect.xMax - 20f, slotRect.y + 4f, 16f, 16f);
            GUI.Label(cornerRect, "R", _mutedTextStyle);
        }

        private static string BuildInventorySlotTooltip(ScenarioInventorySlotViewModel slot, bool readOnly)
        {
            if (slot == null)
                return string.Empty;

            string title = slot.DisplayName ?? string.Empty;
            string detail = slot.Detail ?? string.Empty;
            if (readOnly)
                detail = string.IsNullOrEmpty(detail) ? "Reference only." : detail + " Reference only.";
            if (string.IsNullOrEmpty(detail))
                return title;
            if (string.IsNullOrEmpty(title))
                return detail;
            return title + ": " + detail;
        }

        private static int CountRenderableInventorySlots(ScenarioInventorySlotViewModel[] slots)
        {
            int count = 0;
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                if (slots[i] != null)
                    count++;
            }

            return count;
        }

        private static bool HasMoreInventorySlots(ScenarioInventorySlotViewModel[] slots, int startIndex)
        {
            for (int i = Math.Max(0, startIndex); slots != null && i < slots.Length; i++)
            {
                if (slots[i] != null)
                    return true;
            }

            return false;
        }

        private static bool HasInventoryScheduleSlots(ScenarioInventorySlotViewModel[] slots)
        {
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                if (slots[i] != null && (!string.IsNullOrEmpty(slots[i].ScheduleText) || slots[i].KindAction != null || (slots[i].TimeActions != null && slots[i].TimeActions.Length > 0)))
                    return true;
            }

            return false;
        }

        private static bool IsStartingSupplyGrid(ScenarioAuthoringInspectorSection section)
        {
            return section != null
                && string.Equals(section.Id, "authored_starting_items", StringComparison.OrdinalIgnoreCase);
        }
    }
}
