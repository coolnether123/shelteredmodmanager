using System;

namespace ModAPI.Scenarios
{
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
