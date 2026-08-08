using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed class ScenarioAuthoringTourTargetRegistry
    {
        private readonly Dictionary<string, Rect> _targets = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);

        public void ClearFrame()
        {
            _targets.Clear();
        }

        public void Register(string targetId, Rect rect)
        {
            if (string.IsNullOrEmpty(targetId) || rect.width <= 0f || rect.height <= 0f)
                return;

            _targets[targetId] = rect;
        }

        public bool TryGet(string targetId, out Rect rect)
        {
            rect = new Rect(0f, 0f, 0f, 0f);
            return !string.IsNullOrEmpty(targetId) && _targets.TryGetValue(targetId, out rect);
        }
    }
}
