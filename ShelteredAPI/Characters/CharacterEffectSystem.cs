using ModAPI.Characters.Internal;

namespace ModAPI.Characters
{
    public static class CharacterEffectSystem
    {
        private static ICharacterEffectSystem _instance;

        public static ICharacterEffectSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CharacterEffectSystemImpl();
                }
                return _instance;
            }
        }

        internal static CharacterEffectSystemImpl InternalInstance
        {
            get { return (CharacterEffectSystemImpl)Instance; }
        }
    }
}
