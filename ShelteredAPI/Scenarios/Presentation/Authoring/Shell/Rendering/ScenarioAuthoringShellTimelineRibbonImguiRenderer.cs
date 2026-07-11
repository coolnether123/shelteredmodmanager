using System;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float TimelineRibbonLabelWidth = 142f;
        private float _timelineRibbonZoom;
        private float _timelineRibbonFirstVisibleDay = 1f;
        private bool _timelineRibbonDragCandidate;
        private bool _timelineRibbonDragging;
        private Vector2 _timelineRibbonDragStartMouse;
        private float _timelineRibbonDragStartDay;
        private GUIStyle _timelineRibbonCaptionStyle;
        private GUIStyle _timelineRibbonDayStyle;
        private GUIStyle _timelineRibbonOverflowStyle;
        private GUIStyle _timelineRibbonEmptyStyle;

        private void DrawWorkshopTimelineRibbon(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioDayTimelineRibbonViewModel ribbon = _snapshot != null && _snapshot.ShellViewModel != null
                ? _snapshot.ShellViewModel.TimelineRibbon
                : null;
            RegisterInteractiveRegion(rect);
            RegisterTourTarget("timeline:ribbon", rect);

            Color oldColor = GUI.color;
            GUI.color = new Color(0.74f, 0.66f, 0.51f, 0.24f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);

            Rect labelRect = new Rect(rect.x + 10f, rect.y + 8f, TimelineRibbonLabelWidth - 18f, rect.height - 16f);
            GUI.Label(new Rect(labelRect.x, labelRect.y, labelRect.width, 21f), window != null ? window.Title ?? string.Empty : string.Empty, _smallTitleStyle);
            EnsureTimelineRibbonStyles();
            GUI.Label(new Rect(labelRect.x, labelRect.y + 23f, labelRect.width, 16f), "DAY TIMELINE", _timelineRibbonCaptionStyle);

            Rect trackRect = new Rect(rect.x + TimelineRibbonLabelWidth, rect.y + 4f, Math.Max(80f, rect.width - TimelineRibbonLabelWidth - 8f), rect.height - 8f);
            if (ribbon == null || ribbon.EntryCount == 0)
            {
                GUI.Label(trackRect, ribbon != null ? ribbon.EmptyMessage ?? string.Empty : "No scheduled events yet - add some in Timeline or Story", _timelineRibbonEmptyStyle);
                return;
            }

            float pixelsPerDay = ResolveTimelineRibbonPixelsPerDay(trackRect, ribbon);
            HandleTimelineRibbonInput(trackRect, ribbon, pixelsPerDay);
            pixelsPerDay = ResolveTimelineRibbonPixelsPerDay(trackRect, ribbon);
            ClampTimelineRibbonPan(trackRect, ribbon, pixelsPerDay);
            UpdateTimelineRibbonViewport(trackRect, ribbon, pixelsPerDay);
            DrawTimelineRibbonAxis(trackRect, ribbon, pixelsPerDay);
            DrawTimelineRibbonMarkers(trackRect, ribbon, pixelsPerDay);
        }

        private void EnsureTimelineRibbonStyles()
        {
            if (_timelineRibbonCaptionStyle != null)
                return;

            _timelineRibbonCaptionStyle = new GUIStyle(_mutedTextStyle);
            _timelineRibbonCaptionStyle.fontSize = Math.Min(_timelineRibbonCaptionStyle.fontSize, 10);
            _timelineRibbonDayStyle = new GUIStyle(_mutedTextStyle);
            _timelineRibbonDayStyle.alignment = TextAnchor.UpperCenter;
            _timelineRibbonDayStyle.fontSize = Math.Min(_timelineRibbonDayStyle.fontSize, 10);
            _timelineRibbonOverflowStyle = new GUIStyle(_mutedTextStyle);
            _timelineRibbonOverflowStyle.alignment = TextAnchor.MiddleCenter;
            _timelineRibbonOverflowStyle.fontSize = Math.Min(_timelineRibbonOverflowStyle.fontSize, 9);
            _timelineRibbonEmptyStyle = new GUIStyle(_mutedTextStyle);
            _timelineRibbonEmptyStyle.alignment = TextAnchor.MiddleLeft;
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
                float pointerDay = _timelineRibbonFirstVisibleDay + ((current.mousePosition.x - trackRect.x) / Math.Max(1f, pixelsPerDay));
                _timelineRibbonZoom = Mathf.Clamp01(_timelineRibbonZoom - (current.delta.y * 0.08f));
                float nextPixelsPerDay = ResolveTimelineRibbonPixelsPerDay(trackRect, ribbon);
                _timelineRibbonFirstVisibleDay = pointerDay - ((current.mousePosition.x - trackRect.x) / Math.Max(1f, nextPixelsPerDay));
                ClampTimelineRibbonPan(trackRect, ribbon, nextPixelsPerDay);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && trackRect.Contains(current.mousePosition))
            {
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
            Color oldColor = GUI.color;
            float lineY = trackRect.y + 37f;
            GUI.color = new Color(0.34f, 0.27f, 0.18f, 0.58f);
            GUI.DrawTexture(new Rect(trackRect.x, lineY, trackRect.width, 2f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            int interval = pixelsPerDay >= 38f ? 1 : pixelsPerDay >= 20f ? 2 : pixelsPerDay >= 10f ? 5 : 10;
            for (int day = ribbon.FirstDay; day <= ribbon.LastDay; day++)
            {
                if (day != ribbon.FirstDay && day != ribbon.LastDay && ((day - 1) % interval) != 0)
                    continue;
                float x = TimelineRibbonDayCenterX(trackRect, day, pixelsPerDay);
                if (x < trackRect.x - 12f || x > trackRect.xMax + 12f)
                    continue;
                GUI.color = new Color(0.34f, 0.27f, 0.18f, 0.58f);
                GUI.DrawTexture(new Rect(x, lineY - 2f, 1f, 6f), Texture2D.whiteTexture);
                GUI.color = oldColor;
                ScenarioDayTimelineRibbonDayViewModel dayModel = GetTimelineRibbonDay(ribbon, day);
                GUI.Label(new Rect(x - 18f, lineY + 5f, 36f, 14f), dayModel != null ? dayModel.Label : string.Empty, _timelineRibbonDayStyle);
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
                ? new Rect(centerX - 9f, trackRect.y + 3f, 18f, 29f)
                : new Rect(centerX - 6f, trackRect.y + 5f + (lane * 8f), 12f, 12f);
            bool hovered = IsInteractiveHoverAllowed(markerRect);
            bool pressed = IsInteractiveMouseDownAllowed(markerRect);
            RegisterInteractiveRegion(markerRect);
            RegisterRichHoverHelpSource(markerRect, marker.Action);

            Color color = marker.IsChapter ? new Color(0.66f, 0.43f, 0.17f, 1f) : ResolveTimelineChipColor(marker.Domain);
            float intensity = hovered ? 1.18f : pressed ? 0.9f : 1f;
            Color oldColor = GUI.color;
            GUI.color = new Color(color.r * intensity, color.g * intensity, color.b * intensity, marker.IsChapter ? 0.96f : 0.88f);
            if (marker.IsChapter)
            {
                GUI.DrawTexture(new Rect(markerRect.x + 2f, markerRect.y, 2f, markerRect.height), Texture2D.whiteTexture);
                ScenarioUiAtlasSkin.DrawCornerCutTexture(new Rect(markerRect.x + 4f, markerRect.y, markerRect.width - 4f, 16f), Texture2D.whiteTexture);
            }
            else
            {
                ScenarioUiAtlasSkin.DrawCornerCutTexture(markerRect, Texture2D.whiteTexture);
            }
            GUI.color = oldColor;

            if (DrawPlainButton(markerRect, GUIContent.none, GUIStyle.none, marker.Action.Enabled))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(marker.Action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }
        }

        private void DrawTimelineRibbonOverflow(
            Rect trackRect,
            float centerX,
            ScenarioDayTimelineRibbonDayViewModel day)
        {
            Rect overflowRect = new Rect(centerX + 7f, trackRect.y + 28f, 26f, 16f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.42f, 0.34f, 0.24f, 0.72f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(overflowRect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(overflowRect, day != null ? day.OverflowLabel : string.Empty, _timelineRibbonOverflowStyle);
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
            _timelineRibbonFirstVisibleDay = 1f;
            _timelineRibbonDragCandidate = false;
            _timelineRibbonDragging = false;
        }
    }
}
