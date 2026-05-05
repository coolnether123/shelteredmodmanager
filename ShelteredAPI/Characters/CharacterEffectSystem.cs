using ShelteredAPI.Characters.Internal;


using ShelteredAPI.Characters.Abstractions;
namespace ShelteredAPI.Characters
{
    internal static class CharacterEffectSystem
    {
        private static ICharacterEffectSystem _instance;

        internal static ICharacterEffectSystem Instance
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
