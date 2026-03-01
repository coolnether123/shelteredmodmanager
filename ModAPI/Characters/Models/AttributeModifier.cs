namespace ModAPI.Characters
{
    public class AttributeModifier
    {
        public string AttributeName { get; set; }
        public float Value { get; set; }
        public float Duration { get; set; }
        public float TimeRemaining { get; set; }
        public string SourceModId { get; set; }
        public float TimeApplied { get; set; }

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
