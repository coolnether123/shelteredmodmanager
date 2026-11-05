using ShelteredAPI.Content;

namespace ShelteredAPI.Characters.Models{
    /// <summary>
    /// Runtime handle for one attribute modifier applied to a character.
    /// Keep the handle if you need to remove that exact modifier later.
    /// </summary>
    public class AttributeModifier
    {
        public string AttributeName { get; internal set; }
        public float Value { get; internal set; }
        /// <summary>When true, Value is a factor rather than an additive adjustment. Defaults to false.</summary>
        public bool IsMultiplicative { get; set; }
        /// <summary>Optional lower bound after all WalkSpeed contributions have combined.</summary>
        public float? MinimumResult { get; set; }
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
