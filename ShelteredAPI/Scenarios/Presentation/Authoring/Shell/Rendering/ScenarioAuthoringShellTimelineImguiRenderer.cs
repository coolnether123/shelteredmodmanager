using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const string TimelineTrackSectionId = "timeline_workshop_track";
        private const string TimelineDayMetadataPrefix = "timeline-day|";
        private const string TimelineChipMetadataPrefix = "timeline-chip|";
        private const float TimelineTrackZoomDuration = 0.15f;
        private ScenarioDayTimelineRibbonViewModel _timelineTrackCachedRevisionToken;
        private TimelineDayInfo[] _timelineTrackCachedDays;
        private TimelineChipInfo[] _timelineTrackCachedChips;
        private float _timelineTrackMeasuredDayWidth;
        private int _timelineTrackMaxLanes;
        private float _timelineTrackZoom;
        private float _timelineTrackTargetZoom;
        private float _timelineTrackZoomStart;
        private float _timelineTrackZoomStartedAt;
        private float _timelineTrackZoomAnchorDay;
        private float _timelineTrackZoomAnchorPixel;
        private bool _timelineTrackZoomAnimating;
        private bool _timelineTrackDragCandidate;
        private bool _timelineTrackDragging;
        private Vector2 _timelineTrackDragStartMouse;
        private float _timelineTrackDragStartScroll;
        private GUIStyle _timelineTrackLabelStyle;
        private GUIStyle _timelineTrackGlyphStyle;

        private bool IsPacingSection(ScenarioAuthoringInspectorSection section)
        {
            return section != null
                && string.Equals(section.Id, ScenarioPacingAuthoringSectionBuilder.SectionId, StringComparison.OrdinalIgnoreCase);
        }

        private void DrawPacingSection(ScenarioAuthoringInspectorSection section)
        {
            ScenarioAuthoringInspectorItem readingItem = null;
            ScenarioAuthoringInspectorItem densityItem = null;
            List<ScenarioAuthoringInspectorItem> callouts = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item == null)
                    continue;
                if (string.Equals(item.Label, "Reading", StringComparison.OrdinalIgnoreCase))
                    readingItem = item;
                else if (string.Equals(item.Label, ScenarioPacingAuthoringSectionBuilder.DensityLabel, StringComparison.OrdinalIgnoreCase))
                    densityItem = item;
                else if (item.Kind == ScenarioAuthoringInspectorItemKind.Text)
                    callouts.Add(item);
            }

            string[] densityParts = densityItem != null && !string.IsNullOrEmpty(densityItem.Value)
                ? densityItem.Value.Split(',')
                : new string[0];
            int[] counts = new int[densityParts.Length];
            int maxCount = 1;
            for (int i = 0; i < densityParts.Length; i++)
            {
                int count;
                counts[i] = int.TryParse(densityParts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) ? Math.Max(0, count) : 0;
                maxCount = Math.Max(maxCount, counts[i]);
            }

            float width = GetSectionContentWidth();
            float calloutHeight = callouts.Count * 22f;
            float height = 102f + calloutHeight + (densityItem != null && !string.IsNullOrEmpty(densityItem.Detail) ? 18f : 0f);
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            Color oldColor = GUI.color;
            GUI.color = new Color(0.79f, 0.70f, 0.53f, 0.32f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);

            string reading = readingItem != null ? readingItem.Value : "No authored pacing data yet.";
            Rect readingRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 20f);
            string fittedReading;
            string readingTooltip;
            ScenarioUiMeasuredLabel.PreserveLabelWithOverflowTooltip(reading, readingRect.width, _textStyle, out fittedReading, out readingTooltip);
            GUI.Label(readingRect, new GUIContent(fittedReading, readingTooltip), _textStyle);

            Rect chartRect = new Rect(rect.x + 12f, rect.y + 33f, rect.width - 24f, 48f);
            if (counts.Length > 0)
            {
                float gap = counts.Length > 20 ? 2f : 3f;
                float barWidth = Math.Max(2f, (chartRect.width - (gap * (counts.Length - 1))) / counts.Length);
                for (int i = 0; i < counts.Length; i++)
                {
                    float ratio = counts[i] > 0 ? (float)counts[i] / (float)maxCount : 0f;
                    float barHeight = counts[i] > 0 ? Math.Max(4f, 34f * ratio) : 2f;
                    Rect barRect = new Rect(chartRect.x + (i * (barWidth + gap)), chartRect.yMax - 12f - barHeight, barWidth, barHeight);
                    GUI.color = counts[i] > 0
                        ? new Color(0.58f, 0.38f, 0.15f, 0.88f)
                        : new Color(0.43f, 0.35f, 0.25f, 0.28f);
                    GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                    GUI.color = oldColor;
                    if (i == 0 || (i + 1) % 5 == 0 || i == counts.Length - 1)
                    {
                        GUIStyle dayStyle = new GUIStyle(_mutedTextStyle);
                        dayStyle.alignment = TextAnchor.UpperCenter;
                        dayStyle.fontSize = Math.Min(dayStyle.fontSize, 10);
                        GUI.Label(new Rect(barRect.x - 3f, chartRect.yMax - 11f, barRect.width + 6f, 14f), (i + 1).ToString(CultureInfo.InvariantCulture), dayStyle);
                    }
                }
            }

            float y = rect.y + 83f;
            if (densityItem != null && !string.IsNullOrEmpty(densityItem.Detail))
            {
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 17f), densityItem.Detail, _mutedTextStyle);
                y += 18f;
            }
            for (int i = 0; i < callouts.Count; i++)
            {
                GUIStyle calloutStyle = new GUIStyle(callouts[i].Emphasized ? _textStyle : _mutedTextStyle);
                calloutStyle.fontStyle = callouts[i].Emphasized ? FontStyle.Bold : FontStyle.Normal;
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 20f), callouts[i].Value ?? string.Empty, calloutStyle);
                y += 22f;
            }
        }

        private bool IsTimelineTrackSection(ScenarioAuthoringInspectorSection section)
        {
            return section != null
                && string.Equals(section.Id, TimelineTrackSectionId, StringComparison.OrdinalIgnoreCase);
        }

        private void DrawTimelineTrackSection(ScenarioAuthoringInspectorSection section)
        {
            EnsureTimelineTrackCache(section);
            TimelineDayInfo[] days = _timelineTrackCachedDays;
            TimelineChipInfo[] chips = _timelineTrackCachedChips;

            GUIStyle instructionStyle = new GUIStyle(_mutedTextStyle);
            instructionStyle.wordWrap = true;
            GUILayout.Label(
                "Each column is one in-game day. Event cards show when something happens; select a card to edit it.",
                instructionStyle);
            GUILayout.Space(5f);
            DrawTimelineAddFlow(section, chips.Length == 0);

            float availableWidth = GetSectionContentWidth();
            const int daysPerPage = 7;
            int weekCount = Math.Max(1, (days.Length + daysPerPage - 1) / daysPerPage);
            int maxLanes = Math.Max(1, _timelineTrackMaxLanes);
            float weekHeight = Mathf.Clamp(62f + (maxLanes * 50f), 174f, 330f);
            float trackHeight = (weekHeight * weekCount) + (8f * Math.Max(0, weekCount - 1));
            Rect viewportRect = GUILayoutUtility.GetRect(availableWidth, trackHeight, GUILayout.ExpandWidth(true), GUILayout.Height(trackHeight));
            DrawTimelineTrackViewport(viewportRect, days, chips, weekHeight);
            GUILayout.Space(5f);
            GUILayout.Label(
                chips.Length == 0
                    ? "The calendar is empty. Choose an event type above to schedule the first change."
                    : "Color key: story, weather, supplies, survivors, journal, and world events.",
                _mutedTextStyle);
        }

        private void EnsureTimelineTrackCache(ScenarioAuthoringInspectorSection section)
        {
            ScenarioDayTimelineRibbonViewModel revisionToken = _snapshot != null && _snapshot.ShellViewModel != null
                ? _snapshot.ShellViewModel.TimelineRibbon
                : null;
            if (object.ReferenceEquals(_timelineTrackCachedRevisionToken, revisionToken)
                && _timelineTrackCachedDays != null
                && _timelineTrackCachedChips != null)
            {
                RebindTimelineTrackActions(section);
                return;
            }
            _timelineTrackCachedRevisionToken = revisionToken;
            _timelineTrackCachedDays = BuildTimelineDayInfos(section);
            _timelineTrackCachedChips = BuildTimelineChipInfos(section);
            if (_timelineTrackLabelStyle == null)
            {
                _timelineTrackLabelStyle = new GUIStyle(_mutedTextStyle);
                _timelineTrackLabelStyle.alignment = TextAnchor.MiddleLeft;
                _timelineTrackGlyphStyle = new GUIStyle(_mutedTextStyle);
                _timelineTrackGlyphStyle.alignment = TextAnchor.MiddleCenter;
                _timelineTrackGlyphStyle.fontStyle = FontStyle.Bold;
                _timelineTrackGlyphStyle.clipping = TextClipping.Clip;
            }
            _timelineTrackMeasuredDayWidth = MeasureTimelineDayWidth(_timelineTrackCachedDays, _timelineTrackCachedChips, 0f);
            _timelineTrackMaxLanes = MaxTimelineChipLaneCount(_timelineTrackCachedDays, _timelineTrackCachedChips);
        }

        private void RebindTimelineTrackActions(ScenarioAuthoringInspectorSection section)
        {
            int dayIndex = 0;
            int chipIndex = 0;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = section.Items[i] != null ? section.Items[i].Action : null;
                string metadata = action != null ? action.DisabledReason : null;
                if (metadata != null && metadata.StartsWith(TimelineDayMetadataPrefix, StringComparison.Ordinal))
                {
                    if (dayIndex < _timelineTrackCachedDays.Length && _timelineTrackCachedDays[dayIndex] != null)
                        _timelineTrackCachedDays[dayIndex].Action = action;
                    dayIndex++;
                }
                else if (metadata != null && metadata.StartsWith(TimelineChipMetadataPrefix, StringComparison.Ordinal))
                {
                    if (chipIndex < _timelineTrackCachedChips.Length && _timelineTrackCachedChips[chipIndex] != null)
                        _timelineTrackCachedChips[chipIndex].Action = action;
                    chipIndex++;
                }
            }
        }

        private void DrawTimelineAddFlow(ScenarioAuthoringInspectorSection section, bool empty)
        {
            GUILayout.Label(empty ? "Add the first event" : "Add to calendar", _uiContext.Styles.PaperTitleText);
            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            float used = 0f;
            float limit = GetSectionContentWidth();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                ScenarioAuthoringInspectorAction action = item != null ? item.Action : null;
                if (!IsTimelineAddAction(action))
                    continue;

                float width = Math.Max(88f, MeasureButtonWidth(action, false, 20f));
                if (used + width + 4f > limit && used > 90f)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    used = 0f;
                }

                Rect rect = GUILayoutUtility.GetRect(width, 30f, GUILayout.Width(width), GUILayout.Height(30f));
                DrawButton(rect, action, false);
                GUILayout.Space(4f);
                used += width + 4f;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        private void DrawTimelineTrackViewport(
            Rect viewportRect,
            TimelineDayInfo[] days,
            TimelineChipInfo[] chips,
            float weekHeight)
        {
            if (days == null || days.Length == 0)
                return;

            RegisterInteractiveRegion(viewportRect);
            Color oldColor = GUI.color;
            GUI.color = _uiContext.Styles.Theme.Palette.SurfacePage;
            ScenarioUiAtlasSkin.DrawCornerCutTexture(viewportRect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(
                viewportRect,
                _uiContext.Styles.BorderStrongTexture,
                _uiContext.Styles.BorderSubtleTexture);

            const int daysPerWeek = 7;
            float fittedDayWidth = viewportRect.width / daysPerWeek;
            for (int dayIndex = 0; dayIndex < days.Length; dayIndex++)
            {
                int weekIndex = dayIndex / daysPerWeek;
                int columnIndex = dayIndex % daysPerWeek;
                TimelineDayInfo day = days[dayIndex];
                Rect dayRect = new Rect(
                    viewportRect.x + (columnIndex * fittedDayWidth) + 3f,
                    viewportRect.y + (weekIndex * (weekHeight + 8f)) + 4f,
                    fittedDayWidth - 6f,
                    weekHeight - 8f);
                DrawTimelineDayColumn(dayRect, day, Math.Max(1, _timelineTrackMaxLanes), fittedDayWidth);
                DrawTimelineChipsForDay(dayRect, day, chips);
            }
        }

        private Vector2 HandleTimelineTrackInput(
            Rect viewportRect,
            TimelineDayInfo[] days,
            TimelineChipInfo[] chips,
            Vector2 scroll)
        {
            float closeDayWidth = Math.Max(_timelineTrackMeasuredDayWidth, viewportRect.width >= 720f ? 168f : 144f);
            float overviewDayWidth = Math.Max(54f, viewportRect.width / Math.Max(1, days.Length));
            float dayWidth = Mathf.Lerp(overviewDayWidth, Math.Max(overviewDayWidth, closeDayWidth), _timelineTrackZoom);
            Event current = Event.current;
            if (current != null && current.type == EventType.ScrollWheel && viewportRect.Contains(current.mousePosition))
            {
                _timelineTrackZoomAnchorPixel = current.mousePosition.x - viewportRect.x;
                _timelineTrackZoomAnchorDay = (scroll.x + _timelineTrackZoomAnchorPixel) / Math.Max(1f, dayWidth);
                _timelineTrackZoomStart = _timelineTrackZoom;
                _timelineTrackTargetZoom = Mathf.Clamp01(_timelineTrackTargetZoom - (current.delta.y * 0.10f));
                _timelineTrackZoomStartedAt = Time.realtimeSinceStartup;
                _timelineTrackZoomAnimating = true;
                current.Use();
            }

            if (_timelineTrackZoomAnimating)
            {
                float progress = Mathf.Clamp01((Time.realtimeSinceStartup - _timelineTrackZoomStartedAt) / TimelineTrackZoomDuration);
                float eased = progress * progress * (3f - (2f * progress));
                _timelineTrackZoom = Mathf.Lerp(_timelineTrackZoomStart, _timelineTrackTargetZoom, eased);
                dayWidth = Mathf.Lerp(overviewDayWidth, Math.Max(overviewDayWidth, closeDayWidth), _timelineTrackZoom);
                scroll.x = (_timelineTrackZoomAnchorDay * dayWidth) - _timelineTrackZoomAnchorPixel;
                if (progress >= 0.999f)
                {
                    _timelineTrackZoom = _timelineTrackTargetZoom;
                    _timelineTrackZoomAnimating = false;
                }
            }

            if (current != null && current.type == EventType.MouseDown && current.button == 0 && viewportRect.Contains(current.mousePosition))
            {
                _timelineTrackDragCandidate = true;
                _timelineTrackDragging = false;
                _timelineTrackDragStartMouse = current.mousePosition;
                _timelineTrackDragStartScroll = scroll.x;
            }
            else if (current != null && _timelineTrackDragCandidate && current.type == EventType.MouseDrag && current.button == 0)
            {
                if (_timelineTrackDragging || Mathf.Abs(current.mousePosition.x - _timelineTrackDragStartMouse.x) >= 3f)
                {
                    _timelineTrackDragging = true;
                    scroll.x = _timelineTrackDragStartScroll - (current.mousePosition.x - _timelineTrackDragStartMouse.x);
                    current.Use();
                }
            }
            else if (current != null && _timelineTrackDragCandidate && current.rawType == EventType.MouseUp && current.button == 0)
            {
                bool wasDragging = _timelineTrackDragging;
                _timelineTrackDragCandidate = false;
                _timelineTrackDragging = false;
                if (wasDragging)
                    current.Use();
            }
            return scroll;
        }

        private float MeasureTimelineDayWidth(TimelineDayInfo[] days, TimelineChipInfo[] chips, float minimum)
        {
            float width = minimum;
            for (int i = 0; days != null && i < days.Length; i++)
                width = Math.Max(width, ScenarioUiMeasuredLabel.Width(days[i] != null ? days[i].Baseline : null, _mutedTextStyle, 22f));
            for (int i = 0; chips != null && i < chips.Length; i++)
                width = Math.Max(width, ScenarioUiMeasuredLabel.Width(chips[i] != null && chips[i].Action != null ? chips[i].Action.Label : null, _textStyle, 66f));
            return Math.Min(320f, width);
        }

        private void DrawTimelineAxis(Rect canvasRect, TimelineDayInfo[] days, TimelineChipInfo[] chips, float dayWidth)
        {
            Rect rulerRect = new Rect(canvasRect.x, canvasRect.y, canvasRect.width, 42f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.28f, 0.22f, 0.16f, 0.52f);
            GUI.DrawTexture(new Rect(rulerRect.x, rulerRect.y + rulerRect.height - 5f, rulerRect.width, 2f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            int maxDensity = 1;
            for (int i = 0; days != null && i < days.Length; i++)
                maxDensity = Math.Max(maxDensity, days[i] != null ? days[i].Count : 0);
            for (int i = 0; days != null && i < days.Length; i++)
            {
                TimelineDayInfo day = days[i];
                Rect dayRect = new Rect(canvasRect.x + (i * dayWidth), canvasRect.y, dayWidth - 6f, canvasRect.height - 2f);
                DrawTimelineDayColumn(dayRect, day, maxDensity, dayWidth);
                DrawTimelineChipsForDay(dayRect, day, chips);
            }
        }

        private void DrawTimelineDayColumn(Rect rect, TimelineDayInfo day, int maxDensity, float dayWidth)
        {
            Color oldColor = GUI.color;
            float density = day != null ? Mathf.Clamp01((float)day.Count / Math.Max(1, maxDensity)) : 0f;
            GUI.color = day != null && day.Count > 0
                ? new Color(0.76f, 0.64f, 0.44f, 0.22f + (density * 0.24f))
                : new Color(0.64f, 0.58f, 0.48f, 0.15f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderSubtleTexture, _uiContext.Styles.BorderStrongTexture);

            string label = day != null
                ? (dayWidth >= 82f ? "Day " : "") + day.Day.ToString(CultureInfo.InvariantCulture)
                : "Day";
            GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 18f), label, _smallTitleStyle);
            if (day != null && day.Action != null)
                RegisterRichHoverHelpSource(rect, day.Action);
            string baseline = day != null ? day.Baseline : null;
            if (!string.IsNullOrEmpty(baseline))
            {
                int repeatedDay = baseline.IndexOf(" / day ", StringComparison.OrdinalIgnoreCase);
                if (repeatedDay > 0)
                    baseline = baseline.Substring(0, repeatedDay);
                if (string.Equals(baseline, "LightSand", StringComparison.OrdinalIgnoreCase))
                    baseline = "Light sand";
                string fitted;
                string tooltip;
                ScenarioUiMeasuredLabel.PreserveLabelWithOverflowTooltip(baseline, rect.width - 16f, _mutedTextStyle, out fitted, out tooltip);
                GUI.Label(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, 16f), new GUIContent(fitted, tooltip), _mutedTextStyle);
            }
        }

        private void DrawTimelineChipsForDay(Rect dayRect, TimelineDayInfo day, TimelineChipInfo[] chips)
        {
            int lane = 0;
            for (int i = 0; chips != null && i < chips.Length; i++)
            {
                TimelineChipInfo chip = chips[i];
                if (chip == null || day == null || chip.Day != day.Day)
                    continue;

                Rect chipRect = new Rect(dayRect.x + 8f, dayRect.y + 48f + (lane * 50f), Math.Max(84f, dayRect.width - 16f), 44f);
                DrawTimelineChip(chipRect, chip);
                lane++;
            }
        }

        private void DrawTimelineChip(Rect rect, TimelineChipInfo chip)
        {
            if (chip == null || chip.Action == null)
                return;

            bool hovered = chip.Action.Enabled && IsInteractiveHoverAllowed(rect);
            bool pressed = chip.Action.Enabled && IsInteractiveMouseDownAllowed(rect);
            RegisterInteractiveRegion(rect);
            if (!string.IsNullOrEmpty(chip.Action.Id))
                RegisterTourTarget("action:" + chip.Action.Id, rect);
            RegisterRichHoverHelpSource(rect, chip.Action);

            DrawTimelineChipSurface(rect, chip, hovered, pressed);
            if (DrawPlainButton(rect, GUIContent.none, GUIStyle.none, chip.Action.Enabled))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(chip.Action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }
        }

        private void DrawTimelineChipSurface(Rect rect, TimelineChipInfo chip, bool hovered, bool pressed)
        {
            Color oldColor = GUI.color;
            Color color = ResolveTimelineChipColor(chip.Domain);
            float value = hovered ? 1.12f : 1f;
            if (pressed)
                value = 0.92f;
            GUI.color = new Color(color.r * value, color.g * value, color.b * value, chip.Action.Emphasized ? 0.92f : 0.78f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, chip.Action.Emphasized ? _uiContext.Styles.BorderStrongTexture : _uiContext.Styles.BorderSubtleTexture, _uiContext.Styles.BorderSubtleTexture);

            bool showStatusBadge = !string.IsNullOrEmpty(chip.Action.Badge)
                && !string.Equals(chip.Status, "Pending", StringComparison.OrdinalIgnoreCase);
            Rect labelRect = new Rect(rect.x + 7f, rect.y + 16f, rect.width - (showStatusBadge ? 40f : 14f), 25f);
            GUIStyle calendarLabelStyle = new GUIStyle(_buttonStyle);
            calendarLabelStyle.alignment = TextAnchor.UpperLeft;
            calendarLabelStyle.padding = new RectOffset();
            calendarLabelStyle.wordWrap = true;
            calendarLabelStyle.clipping = TextClipping.Clip;
            calendarLabelStyle.normal.background = null;
            calendarLabelStyle.hover.background = null;
            calendarLabelStyle.active.background = null;
            calendarLabelStyle.focused.background = null;

            string fullLabel = chip.Action.Label ?? string.Empty;
            int scheduleSeparator = fullLabel.IndexOf(" - ", StringComparison.Ordinal);
            string calendarLabel = scheduleSeparator >= 0 && scheduleSeparator + 3 < fullLabel.Length
                ? fullLabel.Substring(scheduleSeparator + 3)
                : fullLabel;
            if (calendarLabel.StartsWith("Story ", StringComparison.OrdinalIgnoreCase))
                calendarLabel = calendarLabel.Substring("Story ".Length);
            calendarLabel = calendarLabel.Replace("stage change -> ", "Go to ");
            calendarLabel = calendarLabel.Replace("unanswered -> ", "No answer \u2192 ");
            GUI.Label(labelRect, new GUIContent(calendarLabel, fullLabel), calendarLabelStyle);
            if (!string.IsNullOrEmpty(chip.Time))
            {
                GUIStyle timeStyle = new GUIStyle(calendarLabelStyle);
                timeStyle.fontSize = Math.Max(9, timeStyle.fontSize - 2);
                timeStyle.wordWrap = false;
                GUI.Label(new Rect(labelRect.x, rect.y + 2f, labelRect.width, 14f), chip.Time, timeStyle);
            }
            if (showStatusBadge)
                ScenarioUiWidgets.DrawPill(new Rect(rect.xMax - 28f, rect.y + 13f, 23f, 18f), chip.Action.Badge, _uiContext.Styles, ResolveTimelineStatusEmphasis(chip.Status));
        }

        private void DrawTimelineChipGlyph(Rect rect, TimelineChipInfo chip)
        {
            string role = ResolveTimelineIconRole(chip != null ? chip.Domain : null);
            if (!string.IsNullOrEmpty(role) && ScenarioUiAtlasSkin.HasIcon(role) && ScenarioUiAtlasSkin.DrawIcon(rect, role))
                return;

            GUI.Label(rect, ShortenToFit(chip.Action.IconText ?? string.Empty, rect.width, _timelineTrackGlyphStyle), _timelineTrackGlyphStyle);
        }

        private void DrawTimelineEmptyState(ScenarioAuthoringInspectorSection section)
        {
            Rect rect = GUILayoutUtility.GetRect(GetSectionContentWidth(), 110f, GUILayout.ExpandWidth(true), GUILayout.Height(110f));
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Card);
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 24f), "Nothing scheduled yet", _sectionTitleStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 38f, rect.width - 28f, 20f), "Add events to build your day track, or open Story for beats.", _textStyle);

            float x = rect.x + 14f;
            float y = rect.y + 66f;
            int drawn = 0;
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = section.Items[i] != null ? section.Items[i].Action : null;
                if (!IsTimelineAddAction(action))
                    continue;

                float width = Math.Max(90f, MeasureButtonWidth(action, false, 20f));
                if (x + width > rect.xMax - 14f && drawn > 0)
                {
                    x = rect.x + 14f;
                    y += 30f;
                }
                DrawButton(new Rect(x, y, width, 26f), action, false);
                x += width + 6f;
                drawn++;
            }
        }

        private TimelineDayInfo[] BuildTimelineDayInfos(ScenarioAuthoringInspectorSection section)
        {
            List<TimelineDayInfo> result = new List<TimelineDayInfo>();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = section.Items[i] != null ? section.Items[i].Action : null;
                string metadata = action != null ? action.DisabledReason : null;
                if (metadata == null || !metadata.StartsWith(TimelineDayMetadataPrefix, StringComparison.Ordinal))
                    continue;

                string[] parts = metadata.Split('|');
                int day;
                int count;
                TimelineDayInfo info = new TimelineDayInfo();
                info.Action = action;
                info.Day = parts.Length > 1 && int.TryParse(parts[1], out day) ? day : result.Count + 1;
                info.Baseline = parts.Length > 2 ? parts[2] : string.Empty;
                info.Count = parts.Length > 3 && int.TryParse(parts[3], out count) ? count : 0;
                result.Add(info);
            }

            return result.ToArray();
        }

        private TimelineChipInfo[] BuildTimelineChipInfos(ScenarioAuthoringInspectorSection section)
        {
            List<TimelineChipInfo> result = new List<TimelineChipInfo>();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = section.Items[i] != null ? section.Items[i].Action : null;
                string metadata = action != null ? action.DisabledReason : null;
                if (metadata == null || !metadata.StartsWith(TimelineChipMetadataPrefix, StringComparison.Ordinal))
                    continue;

                string[] parts = metadata.Split('|');
                int day;
                TimelineChipInfo info = new TimelineChipInfo();
                info.Action = action;
                info.Day = parts.Length > 1 && int.TryParse(parts[1], out day) ? day : 1;
                info.Domain = parts.Length > 2 ? parts[2] : "change";
                info.Time = parts.Length > 3 ? parts[3] : string.Empty;
                info.Status = parts.Length > 4 ? parts[4] : string.Empty;
                result.Add(info);
            }

            return result.ToArray();
        }

        private static bool IsTimelineAddAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id))
                return false;
            return string.Equals(action.Id, ScenarioAuthoringActionIds.ActionWeatherScheduleAdd, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionScheduledActionAdd, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionWorldEventAdd, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionJournalEntryAdd, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionTriggerAddScheduled, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionFutureSurvivorAdd, StringComparison.Ordinal)
                || action.Id.StartsWith(ScenarioAuthoringActionIds.ActionTimelinePresetPrefix, StringComparison.Ordinal)
                || action.Id.StartsWith(ScenarioAuthoringActionIds.ActionStageSelectPrefix, StringComparison.Ordinal);
        }

        private static int MaxTimelineChipLaneCount(TimelineDayInfo[] days, TimelineChipInfo[] chips)
        {
            int max = 0;
            for (int i = 0; days != null && i < days.Length; i++)
            {
                int count = 0;
                for (int c = 0; chips != null && c < chips.Length; c++)
                    if (chips[c] != null && days[i] != null && chips[c].Day == days[i].Day)
                        count++;
                max = Math.Max(max, count);
            }

            return max;
        }

        private Color ResolveTimelineChipColor(string domain)
        {
            if (string.Equals(domain, "weather", StringComparison.OrdinalIgnoreCase))
                return _uiContext.Styles.Theme.Palette.SemanticInfoStrong;
            if (string.Equals(domain, "inventory", StringComparison.OrdinalIgnoreCase))
                return _uiContext.Styles.Theme.Palette.WorkspaceSupplies;
            if (string.Equals(domain, "arrival", StringComparison.OrdinalIgnoreCase))
                return _uiContext.Styles.Theme.Palette.SemanticReadyStrong;
            if (string.Equals(domain, "trigger", StringComparison.OrdinalIgnoreCase))
                return _uiContext.Styles.Theme.Palette.SemanticWarningStrong;
            if (string.Equals(domain, "story", StringComparison.OrdinalIgnoreCase))
                return _uiContext.Styles.Theme.Palette.WorkspaceStory;
            if (string.Equals(domain, "journal", StringComparison.OrdinalIgnoreCase))
                return _uiContext.Styles.Theme.Palette.WorkspaceTest;
            if (string.Equals(domain, "world_event", StringComparison.OrdinalIgnoreCase))
                return _uiContext.Styles.Theme.Palette.WorkspaceMap;
            return _uiContext.Styles.Theme.Palette.BorderDefault;
        }

        private static string ResolveTimelineIconRole(string domain)
        {
            if (string.Equals(domain, "inventory", StringComparison.OrdinalIgnoreCase))
                return "home_inventory";
            if (string.Equals(domain, "arrival", StringComparison.OrdinalIgnoreCase))
                return "home_people";
            if (string.Equals(domain, "story", StringComparison.OrdinalIgnoreCase))
                return "home_publish";
            if (string.Equals(domain, "world_event", StringComparison.OrdinalIgnoreCase))
                return "home_events";
            return "home_events";
        }

        private static ScenarioUiPillEmphasis ResolveTimelineStatusEmphasis(string status)
        {
            if (string.Equals(status, "Fired", StringComparison.OrdinalIgnoreCase))
                return ScenarioUiPillEmphasis.Success;
            if (string.Equals(status, "Blocked", StringComparison.OrdinalIgnoreCase))
                return ScenarioUiPillEmphasis.Warning;
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                return ScenarioUiPillEmphasis.Danger;
            return ScenarioUiPillEmphasis.Default;
        }

        private sealed class TimelineDayInfo
        {
            public ScenarioAuthoringInspectorAction Action;
            public int Day;
            public string Baseline;
            public int Count;
        }

        private sealed class TimelineChipInfo
        {
            public ScenarioAuthoringInspectorAction Action;
            public int Day;
            public string Domain;
            public string Time;
            public string Status;
        }
    }
}
