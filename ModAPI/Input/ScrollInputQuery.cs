namespace ModAPI.InputServices
{
    /// <summary>
    /// Describes a vertical scroll request without coupling callers to Unity input APIs.
    /// </summary>
    public struct ScrollInputQuery
    {
        public float MinUiX;
        public float MaxUiX;
        public bool RestrictPointerToRange;
        public bool Raw;

        public ScrollInputQuery(float minUiX, float maxUiX, bool restrictPointerToRange, bool raw)
        {
            MinUiX = minUiX;
            MaxUiX = maxUiX;
            RestrictPointerToRange = restrictPointerToRange;
            Raw = raw;
        }

        public static ScrollInputQuery ForUiRange(float minUiX, float maxUiX)
        {
            return new ScrollInputQuery(minUiX, maxUiX, true, false);
        }

        public static ScrollInputQuery Anywhere(bool raw)
        {
            return new ScrollInputQuery(float.NegativeInfinity, float.PositiveInfinity, false, raw);
        }
    }
}
