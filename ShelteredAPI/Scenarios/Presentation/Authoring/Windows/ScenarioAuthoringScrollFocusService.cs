using System;
using System.Collections.Generic;
using UnityEngine;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Windows{
    internal sealed class ScenarioAuthoringScrollFocusService
    {
        private readonly List<ScrollRegion> _frameRegions = new List<ScrollRegion>();
        private readonly List<ScrollRegion> _previousFrameRegions = new List<ScrollRegion>();
        private string _frameFocusedOwnerId;

        public string FocusedOwnerId { get; private set; }
        public bool PointerOverScrollableRegion { get; private set; }

        public void BeginFrame(Vector2 pointerPosition)
        {
            _frameFocusedOwnerId = ResolveTopmostOwner(_previousFrameRegions, pointerPosition);
            _frameRegions.Clear();
            FocusedOwnerId = null;
            PointerOverScrollableRegion = !string.IsNullOrEmpty(_frameFocusedOwnerId);
        }

        public void BeginFrame()
        {
            BeginFrame(Vector2.zero);
        }

        public void RegisterRegion(string ownerId, Rect rect)
        {
            if (string.IsNullOrEmpty(ownerId) || rect.width <= 0f || rect.height <= 0f)
                return;

            _frameRegions.Add(new ScrollRegion
            {
                OwnerId = ownerId,
                Rect = rect
            });
        }

        public bool ShouldDeferScrollToFocusedRegion(string ownerId, Rect rect, Event evt, Vector2 pointerPosition)
        {
            if (evt == null || evt.type != EventType.ScrollWheel || string.IsNullOrEmpty(ownerId))
                return false;
            if (!rect.Contains(pointerPosition))
                return false;
            if (string.IsNullOrEmpty(_frameFocusedOwnerId))
                return false;

            return !string.Equals(_frameFocusedOwnerId, ownerId, StringComparison.Ordinal);
        }

        public bool ConsumeScrollWheelIfNotFocused(string ownerId, Rect rect, Event evt, Vector2 pointerPosition)
        {
            if (!ShouldDeferScrollToFocusedRegion(ownerId, rect, evt, pointerPosition))
                return false;

            evt.Use();
            return true;
        }

        public void CompleteFrame(Vector2 pointerPosition)
        {
            FocusedOwnerId = ResolveTopmostOwner(_frameRegions, pointerPosition);
            PointerOverScrollableRegion = !string.IsNullOrEmpty(FocusedOwnerId);
            _previousFrameRegions.Clear();
            _previousFrameRegions.AddRange(_frameRegions);
        }

        public bool ConsumeScrollWheelIfFocused(Event evt)
        {
            if (evt == null || evt.type != EventType.ScrollWheel || !PointerOverScrollableRegion)
                return false;

            evt.Use();
            return true;
        }

        private static string ResolveTopmostOwner(List<ScrollRegion> regions, Vector2 pointerPosition)
        {
            for (int i = regions.Count - 1; i >= 0; i--)
            {
                ScrollRegion region = regions[i];
                if (region.Rect.Contains(pointerPosition))
                    return region.OwnerId;
            }

            return null;
        }

        private struct ScrollRegion
        {
            public string OwnerId;
            public Rect Rect;
        }
    }
}
