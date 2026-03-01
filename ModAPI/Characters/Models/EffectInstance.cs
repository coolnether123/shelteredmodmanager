using System.Collections.Generic;

namespace ModAPI.Characters
{
    public class EffectInstance
    {
        public ICharacterEffect Effect { get; set; }
        public float TimeApplied { get; set; }
        public float Duration { get; set; }
        public float TimeRemaining { get; set; }
        public string SourceModId { get; set; }
        public int StackCount { get; set; }
        public Dictionary<string, object> CustomData { get; set; }

        public bool IsPermanent
        {
            get { return Duration <= 0f; }
        }

        public bool IsExpired
        {
            get { return !IsPermanent && TimeRemaining <= 0f; }
        }

        public EffectInstance()
        {
            CustomData = new Dictionary<string, object>();
            StackCount = 1;
        }
    }
}
