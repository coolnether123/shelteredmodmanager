using System.Collections.Generic;

namespace ModAPI.Characters
{
    public class EffectInstance
    {
        public ICharacterEffect Effect { get; internal set; }
        public float TimeApplied { get; internal set; }
        public float Duration { get; internal set; }
        public float TimeRemaining { get; internal set; }
        public string SourceModId { get; internal set; }
        public int StackCount { get; internal set; }
        public Dictionary<string, object> CustomData { get; internal set; }

        public bool IsPermanent
        {
            get { return Duration <= 0f; }
        }

        public bool IsExpired
        {
            get { return !IsPermanent && TimeRemaining <= 0f; }
        }

        internal EffectInstance()
        {
            CustomData = new Dictionary<string, object>();
            StackCount = 1;
        }
    }
}
