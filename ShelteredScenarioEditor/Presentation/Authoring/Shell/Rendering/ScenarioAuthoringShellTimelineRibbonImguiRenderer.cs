using System;
using UnityEngine;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Presentation.UiKit.Textures;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float TimelineRibbonLabelWidth = 142f;
        private const float TimelineRibbonZoomDuration = 0.15f;
        private float _timelineRibbonZoom;
        private float _timelineRibbonTargetZoom;
        private float _timelineRibbonZoomStart;
        private float _timelineRibbonZoomStartedAt;
        private float _timelineRibbonZoomAnchorDay = 1f;
        private float _timelineRibbonZoomAnchorPixel;
        private bool _timelineRibbonZoomAnimating;
        private float _timelineRibbonFirstVisibleDay = 1f;
        private bool _timelineRibbonDragCandidate;
        private bool _timelineRibbonDragging;
        private Vector2 _timelineRibbonDragStartMouse;
        private float _timelineRibbonDragStartDay;
        private int _timelineRibbonPendingDayClick = -1;
        private GUIStyle _timelineRibbonCaptionStyle;
        private GUIStyle _timelineRibbonTitleStyle;
        private GUIStyle _timelineRibbonDayStyle;
        private GUIStyle _timelineRibbonOverflowStyle;
        private GUIStyle _timelineRibbonEmptyStyle;
        private GUIStyle _timelineRibbonGlyphStyle;
        private GUIStyle _timelineRibbonChapterStyle;

        private void DrawWorkshopTimelineRibbon(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioDayTimelineRibbonViewModel ribbon = _snapshot != null && _snapshot.ShellViewModel != null
                ? _snapshot.ShellViewModel.TimelineRibbon
                : null;
            RegisterInteractiveRegion(rect);
            RegisterTourTarget("timeline:ribbon", rect);

            ScenarioUiAtlasSkin.DrawCornerCutShadow(rect, _uiContext.Styles.ShadowTexture);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, _uiContext.Styles.ChromeTexture);
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderHighlightTexture, _uiContext.Styles.BorderStrongTexture);

            EnsureTimelineRibbonStyles();
            if (ribbon == null || ribbon.EntryCount == 0)
            {
                Rect compactTitleRect = new Rect(rect.x + 10f, rect.y + 4f, TimelineRibbonLabelWidth - 18f, rect.height - 8f);
                GUI.Label(compactTitleRect, ShortenToFit(window != null ? window.Title ?? string.Empty : string.Empty, compactTitleRect.width, _timelineRibbonTitleStyle), _timelineRibbonTitleStyle);
                Rect compactMessageRect = new Rect(rect.x + TimelineRibbonLabelWidth, rect.y + 4f, Math.Max(80f, rect.width - TimelineRibbonLabelWidth - 8f), rect.height - 8f);
                GUI.Label(compactMessageRect, ribbon != null ? ribbon.EmptyMessage ?? string.Empty : "No scheduled events yet - add some in Timeline or Story", _timelineRibbonEmptyStyle);
                return;
            }

            Rect labelRect = new Rect(rect.x + 10f, rect.y + 8f, TimelineRibbonLabelWidth - 18f, rect.height - 16f);
            string workspaceTitle = window != null ? window.Title ?? string.Empty : string.Empty;
            float titleHeight = Mathf.Clamp(
                _timelineRibbonTitleStyle.CalcHeight(new GUIContent(workspaceTitle), labelRect.width) + 2f,
                24f,
                28f);
            GUI.Label(
                new Rect(labelRect.x, labelRect.y, labelRect.width, titleHeight),
                ShortenToFit(workspaceTitle, labelRect.width, _timelineRibbonTitleStyle),
                _timelineRibbonTitleStyle);
            GUI.Label(new Rect(labelRect.x, labelRect.y + titleHeight + 1f, labelRect.width, 16f), "DAY TIMELINE", _timelineRibbonCaptionStyle);

            Rect trackRect = new Rect(rect.x + TimelineRibbonLabelWidth, rect.y + 4f, Math.Max(80f, rect.width - TimelineRibbonLabelWidth - 8f), rect.height - 8f);
            float pixelsPerDay = ResolveTimelineRibbonPixelsPerDay(trackRect, ribbon);
            HandleTimelineRibbonInput(trackRect, ribbon, pixelsPerDay);
            AdvanceTimelineRibbonZoom(trackRect, ribbon);
            pixelsPerDay = ResolveTimelineRibbonPixelsPerDay(trackRect, ribbon);
            ClampTimelineRibbonPan(trackRect, ribbon, pixelsPerDay);
            UpdateTimelineRibbonViewport(trackRect, ribbon, pixelsPerDay);
            DrawTimelineRibbonDensity(trackRect, ribbon, pixelsPerDay);
            DrawTimelineRibbonAxis(trackRect, ribbon, pixelsPerDay);
            DrawTimelineRibbonMarkers(trackRect, ribbon, pixelsPerDay);
            ExecutePendingTimelineRibbonDayClick(ribbon);
        }

        private void EnsureTimelineRibbonStyles()
        {
            if (_timelineRibbonCaptionStyle != null && _timelineRibbonTitleStyle != null)
                return;

            _timelineRibbonTitleStyle = new GUIStyle(_smallTitleStyle);
            _timelineRibbonTitleStyle.wordWrap = false;
            _timelineRibbonTitleStyle.clipping = TextClipping.Clip;
            _timelineRibbonCaptionStyle = new GUIStyle(_uiContext.Styles.HeaderSubtitleText);
            _timelineRibbonCaptionStyle.fontSize = Math.Min(_timelineRibbonCaptionStyle.fontSize, 10);
            _timelineRibbonDayStyle = new GUIStyle(_uiContext.Styles.HeaderSubtitleText);
            _timelineRibbonDayStyle.alignment = TextAnchor.UpperCenter;
            _timelineRibbonDayStyle.fontSize = Math.Min(_timelineRibbonDayStyle.fontSize, 10);
            _timelineRibbonOverflowStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
            _timelineRibbonOverflowStyle.alignment = TextAnchor.MiddleCenter;
            _timelineRibbonOverflowStyle.fontSize = Math.Min(_timelineRibbonOverflowStyle.fontSize, 9);
            _timelineRibbonEmptyStyle = new GUIStyle(_uiContext.Styles.HeaderSubtitleText);
            _timelineRibbonEmptyStyle.alignment = TextAnchor.MiddleLeft;
            _timelineRibbonGlyphStyle = new GUIStyle(_uiContext.Styles.PaperMutedText);
            _timelineRibbonGlyphStyle.alignment = TextAnchor.MiddleCenter;
            _timelineRibbonGlyphStyle.fontStyle = FontStyle.Bold;
            _timelineRibbonGlyphStyle.fontSize = Math.Min(_timelineRibbonGlyphStyle.fontSize, 8);
            _timelineRibbonChapterStyle = new GUIStyle(_uiContext.Styles.HeaderSubtitleText);
            _timelineRibbonChapterStyle.alignment = TextAnchor.MiddleLeft;
            _timelineRibbonChapterStyle.fontStyle = FontStyle.Bold;
            _timelineRibbonChapterStyle.fontSize = Math.Min(_timelineRibbonChapterStyle.fontSize, 9);
            _timelineRibbonChapterStyle.clipping = TextClipping.Clip;
        }

        private float ResolveTimelineRibbonPixelsPerDay(Rect trackRect, ScenarioDayTimelineRibbonViewModel ribbon)
        {
            int dayCount = Math.Max(1, ribbon.LastDay - ribbon.FirstDay + 1);
            float overview = trackRect.width / dayCount;
            float closeUp = trackRect.width / Math.Min(7, dayCount);
            return Mathf.Lerp(overview, Math.Max(overview, closeUp), Mathf.Clamp01(_timelineRibbonZoom));
        }

        private void HandleTimelineRibbonInput(Rect trackRect, ScenarioDayTimelineRibbonViewModel ribbon, float pixelsPerDay)
        {
            Event current = Event.current;
            if (current == null)
                return;

            if (current.type == EventType.ScrollWheel && trackRect.Contains(current.mousePosition))
            {
                _timelineRibbonZoomAnchorPixel = current.mousePosition.x - trackRect.x;
                _timelineRibbonZoomAnchorDay = _timelineRibbonFirstVisibleDay
                    + (_timelineRibbonZoomAnchorPixel / Math.Max(1f, pixelsPerDay));
                _timelineRibbonZoomStart = _timelineRibbonZoom;
                _timelineRibbonTargetZoom = Mathf.Clamp01(_timelineRibbonTargetZoom - (current.delta.y * 0.10f));
                _timelineRibbonZoomStartedAt = Time.realtimeSinceStartup;
                _timelineRibbonZoomAnimating = true;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && trackRect.Contains(current.mousePosition))
            {
                _timelineRibbonPendingDayClick = -1;
                _timelineRibbonDragCandidate = true;
                _timelineRibbonDragging = false;
                _timelineRibbonDragStartMouse = current.mousePosition;
                _timelineRibbonDragStartDay = _timelineRibbonFirstVisibleDay;
                return;
            }

            if (_timelineRibbonDragCandidate && current.type == EventType.MouseDrag && current.button == 0)
            {
                if (_timelineRibbonDragging || Mathf.Abs(current.mousePosition.x - _timelineRibbonDragStartMouse.x) >= 3f)
                {
                    _timelineRibbonDragging = true;
                    _timelineRibbonFirstVisibleDay = _timelineRibbonDragStartDay
                        - ((current.mousePosition.x - _timelineRibbonDragStartMouse.x) / Math.Max(1f, pixelsPerDay));
                    ClampTimelineRibbonPan(trackRect, ribbon, pixelsPerDay);
                    current.Use();
                }
                return;
            }

            if (_timelineRibbonDragCandidate && current.rawType == EventType.MouseUp && current.button == 0)
            {
                bool wasDragging = _timelineRibbonDragging;
                _timelineRibbonDragCandidate = false;
                _timelineRibbonDragging = false;
                if (wasDragging)
                    current.Use();
                else
                {
                    float dayPosition = _timelineRibbonFirstVisibleDay
                        + ((current.mousePosition.x - trackRect.x) / Math.Max(1f, pixelsPerDay));
                    _timelineRibbonPendingDayClick = Mathf.Clamp(
                        Mathf.FloorToInt(dayPosition),
                        ribbon.FirstDay,
                        ribbon.LastDay);
                }
            }
        }

        private void ExecutePendingTimelineRibbonDayClick(ScenarioDayTimelineRibbonViewModel ribbon)
        {
            if (_timelineRibbonPendingDayClick < 0)
                return;
            int day = _timelineRibbonPendingDayClick;
            _timelineRibbonPendingDayClick = -1;
            Event current = Event.current;
            if (current == null || current.type == EventType.Used)
                return;
            ScenarioDayTimelineRibbonDayViewModel dayModel = GetTimelineRibbonDay(ribbon, day);
            ScenarioAuthoringInspectorAction action = dayModel != null ? dayModel.HoverAction : null;
            if (action == null || !action.Enabled)
                return;
            ExecuteInspectorAction(action);
            current.Use();
        }

        private void AdvanceTimelineRibbonZoom(Rect trackRect, ScenarioDayTimelineRibbonViewModel ribbon)
        {
            if (!_timelineRibbonZoomAnimating)
                return;
            float elapsed = Math.Max(0f, Time.realtimeSinceStartup - _timelineRibbonZoomStartedAt);
            float progress = Mathf.Clamp01(elapsed / TimelineRibbonZoomDuration);
            float eased = progress * progress * (3f - (2f * progress));
            _timelineRibbonZoom = Mathf.Lerp(_timelineRibbonZoomStart, _timelineRibbonTargetZoom, eased);
            float pixelsPerDay = ResolveTimelineRibbonPixelsPerDay(trackRect, ribbon);
            _timelineRibbonFirstVisibleDay = _timelineRibbonZoomAnchorDay
                - (_timelineRibbonZoomAnchorPixel / Math.Max(1f, pixelsPerDay));
            ClampTimelineRibbonPan(trackRect, ribbon, pixelsPerDay);
            if (progress >= 0.999f)
            {
                _timelineRibbonZoom = _timelineRibbonTargetZoom;
                _timelineRibbonZoomAnimating = false;
            }
        }

        private void ClampTimelineRibbonPan(Rect trackRect, ScenarioDayTimelineRibbonViewModel ribbon, float pixelsPerDay)
        {
            float visibleDays = trackRect.width / Math.Max(1f, pixelsPerDay);
            float maxFirstDay = Math.Max(ribbon.FirstDay, ribbon.LastDay - visibleDays + 1f);
            _timelineRibbonFirstVisibleDay = Mathf.Clamp(_timelineRibbonFirstVisibleDay, ribbon.FirstDay, maxFirstDay);
        }

        private void UpdateTimelineRibbonViewport(Rect trackRect, ScenarioDayTimelineRibbonViewModel ribbon, float pixelsPerDay)
        {
            if (ribbon == null)
                return;
            ribbon.Zoom = Mathf.Clamp01(_timelineRibbonZoom);
            ribbon.ZoomState = ribbon.Zoom <= 0.001f ? "overview" : ribbon.Zoom >= 0.999f ? "close_up" : "custom";
            ribbon.FirstVisibleDay = _timelineRibbonFirstVisibleDay;
            ribbon.LastVisibleDay = Math.Min(
                ribbon.LastDay,
                _timelineRibbonFirstVisibleDay + (trackRect.width / Math.Max(1f, pixelsPerDay)) - 1f);
        }

        private void DrawTimelineRibbonAxis(Rect trackRect, ScenarioDayTimelineRibbonViewModel ribbon, float pixelsPerDay)
        {
            float lineY = trackRect.y + 37f;
            GUI.DrawTexture(new Rect(trackRect.x, lineY, trackRect.width, 2f), _uiContext.Styles.BorderSubtleTexture);

            int interval = pixelsPerDay >= 38f ? 1 : pixelsPerDay >= 20f ? 2 : pixelsPerDay >= 10f ? 5 : 10;
            for (int day = ribbon.FirstDay; day <= ribbon.LastDay; day++)
            {
                if (day != ribbon.FirstDay && day != ribbon.LastDay && ((day - 1) % interval) != 0)
                    continue;
                float x = TimelineRibbonDayCenterX(trackRect, day, pixelsPerDay);
                if (x < trackRect.x - 12f || x > trackRect.xMax + 12f)
                    continue;
                GUI.DrawTexture(new Rect(x, lineY - 2f, 1f, 6f), _uiContext.Styles.BorderSubtleTexture);
                ScenarioDayTimelineRibbonDayViewModel dayModel = GetTimelineRibbonDay(ribbon, day);
                GUI.Label(new Rect(x - 18f, lineY + 5f, 36f, 14f), dayModel != null ? dayModel.Label : string.Empty, _timelineRibbonDayStyle);
            }
        }

        private void DrawTimelineRibbonDensity(Rect trackRect, ScenarioDayTimelineRibbonViewModel ribbon, float pixelsPerDay)
        {
            for (int day = ribbon.FirstDay; day <= ribbon.LastDay; day++)
            {
                float centerX = TimelineRibbonDayCenterX(trackRect, day, pixelsPerDay);
                Rect cellRect = new Rect(centerX - (pixelsPerDay * 0.5f) + 1f, trackRect.y + 1f, Math.Max(2f, pixelsPerDay - 2f), trackRect.height - 2f);
                if (cellRect.xMax < trackRect.x || cellRect.x > trackRect.xMax)
                    continue;
                cellRect.xMin = Math.Max(cellRect.xMin, trackRect.x);
                cellRect.xMax = Math.Min(cellRect.xMax, trackRect.xMax);
                ScenarioDayTimelineRibbonDayViewModel dayModel = GetTimelineRibbonDay(ribbon, day);
                int count = dayModel != null ? dayModel.MarkerCount : 0;
                float density = Mathf.Clamp01(count / 5f);
                if (count > 0)
                {
                    float railHeight = Mathf.Clamp(Mathf.Round(density * 4f), 1f, 4f);
                    GUI.DrawTexture(new Rect(cellRect.x, cellRect.yMax - railHeight, cellRect.width, railHeight), _uiContext.Styles.WorkspaceStoryTexture);
                }
                if (dayModel != null && dayModel.HoverAction != null)
                    RegisterRichHoverHelpSource(cellRect, dayModel.HoverAction);
            }
        }

        private void DrawTimelineRibbonMarkers(Rect trackRect, ScenarioDayTimelineRibbonViewModel ribbon, float pixelsPerDay)
        {
            ScenarioDayTimelineRibbonMarkerViewModel[] markers = ribbon.Markers;
            for (int index = 0; markers != null && index < markers.Length;)
            {
                ScenarioDayTimelineRibbonMarkerViewModel first = markers[index];
                if (first == null)
                {
                    index++;
                    continue;
                }

                int runEnd = index + 1;
                while (runEnd < markers.Length && markers[runEnd] != null && markers[runEnd].Day == first.Day)
                    runEnd++;
                float centerX = TimelineRibbonDayCenterX(trackRect, first.Day, pixelsPerDay);
                if (centerX >= trackRect.x - 18f && centerX <= trackRect.xMax + 18f)
                {
                    int visible = Math.Min(ScenarioDayTimelineRibbonViewModelBuilder.MaxVisibleMarkersPerDay, runEnd - index);
                    for (int markerIndex = 0; markerIndex < visible; markerIndex++)
                        DrawTimelineRibbonMarker(trackRect, centerX, markerIndex, markers[index + markerIndex]);
                    int overflow = (runEnd - index) - visible;
                    if (overflow > 0)
                        DrawTimelineRibbonOverflow(trackRect, centerX, GetTimelineRibbonDay(ribbon, first.Day));
                }
                index = runEnd;
            }
        }

        private void DrawTimelineRibbonMarker(Rect trackRect, float centerX, int lane, ScenarioDayTimelineRibbonMarkerViewModel marker)
        {
            if (marker == null || marker.Action == null)
                return;

            Rect markerRect = marker.IsChapter
                ? ResolveTimelineRibbonChapterRect(trackRect, centerX)
                : new Rect(centerX - 8f, trackRect.y + 5f + (lane * 8f), 16f, 16f);
            bool hovered = IsInteractiveHoverAllowed(markerRect);
            bool pressed = IsInteractiveMouseDownAllowed(markerRect);
            RegisterInteractiveRegion(markerRect);
            RegisterRichHoverHelpSource(markerRect, marker.Action);

            Color color = marker.IsChapter ? _uiContext.Styles.Theme.Palette.AccentGold : ResolveTimelineChipColor(marker.Domain);
            if (hovered)
                color = _uiContext.Styles.Theme.Palette.SemanticWarning;
            else if (pressed)
                color = _uiContext.Styles.Theme.Palette.ControlPressed;
            Color oldColor = GUI.color;
            GUI.color = color;
            if (marker.IsChapter)
            {
                GUI.DrawTexture(new Rect(markerRect.x + 2f, markerRect.y, 2f, markerRect.height), Texture2D.whiteTexture);
                ScenarioUiAtlasSkin.DrawCornerCutTexture(new Rect(markerRect.x + 4f, markerRect.y, markerRect.width - 4f, markerRect.height), Texture2D.whiteTexture);
            }
            else
            {
                ScenarioUiAtlasSkin.DrawCornerCutTexture(markerRect, Texture2D.whiteTexture);
            }
            GUI.color = oldColor;

            if (marker.IsChapter && markerRect.width > 24f)
            {
                GUI.Label(new Rect(markerRect.x + 9f, markerRect.y + 1f, markerRect.width - 12f, markerRect.height - 2f),
                    ShortenToFit(marker.Title ?? string.Empty, markerRect.width - 12f, _timelineRibbonChapterStyle),
                    _timelineRibbonChapterStyle);
            }
            else if (!marker.IsChapter)
            {
                DrawTimelineRibbonMarkerGlyph(new Rect(markerRect.x + 3f, markerRect.y + 3f, markerRect.width - 6f, markerRect.height - 6f), marker);
            }

            if (DrawPlainButton(markerRect, GUIContent.none, GUIStyle.none, marker.Action.Enabled))
            {
                ExecuteInspectorAction(marker.Action);
                if (Event.current != null)
                    Event.current.Use();
            }
        }

        private Rect ResolveTimelineRibbonChapterRect(
            Rect trackRect,
            float centerX)
        {
            float pixelsPerDay = ResolveTimelineRibbonPixelsPerDay(trackRect,
                _snapshot != null && _snapshot.ShellViewModel != null ? _snapshot.ShellViewModel.TimelineRibbon : null);
            float width = pixelsPerDay >= 72f ? Math.Min(112f, Math.Max(46f, pixelsPerDay - 8f)) : 18f;
            return new Rect(centerX - (width * 0.5f), trackRect.y + 3f, width, width > 24f ? 18f : 29f);
        }

        private void DrawTimelineRibbonMarkerGlyph(Rect rect, ScenarioDayTimelineRibbonMarkerViewModel marker)
        {
            string role = ResolveTimelineIconRole(marker != null ? marker.Domain : null);
            if (!string.IsNullOrEmpty(role) && ScenarioUiAtlasSkin.HasIcon(role) && ScenarioUiAtlasSkin.DrawIcon(rect, role))
                return;
            string glyph = marker != null && marker.Action != null ? marker.Action.IconText : string.Empty;
            GUI.Label(rect, ShortenToFit(glyph ?? string.Empty, rect.width, _timelineRibbonGlyphStyle), _timelineRibbonGlyphStyle);
        }

        private void DrawTimelineRibbonOverflow(
            Rect trackRect,
            float centerX,
            ScenarioDayTimelineRibbonDayViewModel day)
        {
            Rect overflowRect = new Rect(centerX + 7f, trackRect.y + 28f, 26f, 16f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(overflowRect, _uiContext.Styles.InsetTexture);
            GUI.Label(overflowRect, day != null ? day.OverflowLabel : string.Empty, _timelineRibbonOverflowStyle);
            if (day != null && day.HoverAction != null)
                RegisterRichHoverHelpSource(overflowRect, day.HoverAction);
        }

        private static ScenarioDayTimelineRibbonDayViewModel GetTimelineRibbonDay(
            ScenarioDayTimelineRibbonViewModel ribbon,
            int day)
        {
            int index = day - (ribbon != null ? ribbon.FirstDay : 1);
            return ribbon != null && ribbon.Days != null && index >= 0 && index < ribbon.Days.Length
                ? ribbon.Days[index]
                : null;
        }

        private float TimelineRibbonDayCenterX(Rect trackRect, int day, float pixelsPerDay)
        {
            return trackRect.x + ((day - _timelineRibbonFirstVisibleDay + 0.5f) * pixelsPerDay);
        }

        private void ResetTimelineRibbonInteraction()
        {
            _timelineRibbonZoom = 0f;
            _timelineRibbonTargetZoom = 0f;
            _timelineRibbonZoomStart = 0f;
            _timelineRibbonZoomAnimating = false;
            _timelineRibbonFirstVisibleDay = 1f;
            _timelineRibbonDragCandidate = false;
            _timelineRibbonDragging = false;
            _timelineRibbonPendingDayClick = -1;
            _timelineTrackZoom = 0f;
            _timelineTrackTargetZoom = 0f;
            _timelineTrackZoomStart = 0f;
            _timelineTrackZoomAnimating = false;
            _timelineTrackDragCandidate = false;
            _timelineTrackDragging = false;
        }
    }
}
