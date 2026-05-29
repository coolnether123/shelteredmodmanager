namespace ParalivesAPI.Core
{
    public sealed class ParalivesLocalizationEntry
    {
        public ParalivesLocalizationEntry(string key, string value)
            : this(key, value, null, false, 0UL)
        {
        }

        public ParalivesLocalizationEntry(string key, string value, string infoForTranslators, bool doNotTranslate, ulong guid)
        {
            Key = key;
            Value = value;
            InfoForTranslators = infoForTranslators;
            DoNotTranslate = doNotTranslate;
            Guid = guid;
        }

        public string Key { get; private set; }

        public string Value { get; private set; }

        public string InfoForTranslators { get; private set; }

        public bool DoNotTranslate { get; private set; }

        public ulong Guid { get; private set; }
    }
}
