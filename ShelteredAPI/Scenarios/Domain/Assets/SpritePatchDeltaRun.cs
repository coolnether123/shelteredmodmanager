using System;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Horizontal run of pixels in a sprite patch.
    /// This compact shape keeps generated sprite edits readable and small in XML.
    /// </summary>
    public sealed class SpritePatchDeltaRun
    {
        public SpritePatchDeltaRun()
        {
            Length = 1;
            ColorHex = "00000000";
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Length { get; set; }
        public string ColorHex { get; set; }

        public bool IsValid()
        {
            return X >= 0
                && Y >= 0
                && Length > 0
                && !string.IsNullOrEmpty(ColorHex);
        }
    }
}
