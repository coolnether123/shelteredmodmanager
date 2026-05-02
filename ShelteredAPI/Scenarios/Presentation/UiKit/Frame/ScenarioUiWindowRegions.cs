using UnityEngine;

namespace ShelteredAPI.Scenarios.UiKit.Frame
{
    /// <summary>
    /// Output of <see cref="IScenarioUiWindowFrame.Build"/>. Exposes the rects a
    /// caller needs to fill (header, body, footer) without leaking the frame's
    /// internal padding or divider strategy. Callers paint widgets into
    /// <see cref="Body"/> and the kit's widgets/layout helpers.
    /// </summary>
    internal struct ScenarioUiWindowRegions
    {
        public Rect Outer;
        public Rect Header;
        public Rect Body;
        public Rect Footer;

        /// <summary>True when a footer was reserved (height &gt; 0).</summary>
        public bool HasFooter { get { return Footer.height > 0f; } }
    }
}
