using ModAPI.Actors;
using ShelteredAPI.Actors.Internal;
using ShelteredAPI.Characters;

namespace ShelteredAPI.Actors
{
    public static class ShelteredActors
    {
        private static ModAPI.Actors.IActorSystem _instance;

        public static ModAPI.Actors.IActorSystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ActorSystemImpl();
                return _instance;
            }
        }

        internal static ActorSystemImpl InternalInstance
        {
            get { return (ActorSystemImpl)Instance; }
        }

        public static ActorId FamilyMemberActorId(int uniqueMemberId)
        {
            return new ActorId(ActorKind.Player, uniqueMemberId, string.Empty);
        }

        public static ActorId VisitorActorId(int uniqueVisitorId)
        {
            return new ActorId(ActorKind.Visitor, uniqueVisitorId, string.Empty);
        }

        public static ActorId SyntheticCharacterActorId(int uniqueCharacterId, string sourceModId)
        {
            return new ActorId(ActorKind.Synthetic, uniqueCharacterId, sourceModId ?? string.Empty);
        }

        public static bool TryGetCharacter(ActorId actorId, out ICharacterProxy character)
        {
            character = null;
            if (actorId == null)
                return false;

            if (actorId.Kind != ActorKind.Player
                && actorId.Kind != ActorKind.Visitor
                && actorId.Kind != ActorKind.Synthetic)
                return false;

            character = ShelteredCharacters.GetByUniqueId(actorId.LocalId);
            return character != null;
        }
    }
}
