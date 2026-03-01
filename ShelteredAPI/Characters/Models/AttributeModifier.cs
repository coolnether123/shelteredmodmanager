namespace ModAPI.Characters
{
    public class AttributeModifier
    {
        public string AttributeName { get; internal set; }
        public float Value { get; internal set; }
        public float Duration { get; internal set; }
        public float TimeRemaining { get; internal set; }
        public string SourceModId { get; internal set; }
        public float TimeApplied { get; internal set; }

        public bool IsPermanent
        {
            get { return Duration <= 0f; }
        }

        public bool IsExpired
        {
            get { return !IsPermanent && TimeRemaining <= 0f; }
        }
    }
}
