using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringScrollFocusService
    {
        private readonly List<ScrollRegion> _frameRegions = new List<ScrollRegion>();

        public string FocusedOwnerId { get; private set; }
        public bool PointerOverScrollableRegion { get; private set; }

        public void BeginFrame()
        {
            _frameRegions.Clear();
            PointerOverScrollableRegion = false;
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

        public void CompleteFrame(Vector2 pointerPosition)
        {
            PointerOverScrollableRegion = false;
            for (int i = 0; i < _frameRegions.Count; i++)
            {
                ScrollRegion region = _frameRegions[i];
                if (region.Rect.Contains(pointerPosition))
                {
                    FocusedOwnerId = region.OwnerId;
                    PointerOverScrollableRegion = true;
                    return;
                }
            }
        }

        private struct ScrollRegion
        {
            public string OwnerId;
            public Rect Rect;
        }
    }
}
