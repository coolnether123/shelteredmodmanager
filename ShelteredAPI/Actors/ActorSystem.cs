using ModAPI.Actors;
using ShelteredAPI.Actors.Internal;
using ShelteredAPI.Characters;

using ShelteredAPI.Characters.Abstractions;
namespace ShelteredAPI.Actors
{
    /// <summary>
    /// Sheltered-backed actor facade for resolving game characters into ModAPI actor IDs.
    /// Use <see cref="Instance"/> for the full actor system and the helper methods for stable Sheltered character IDs.
    /// </summary>
    public static class ShelteredActors
    {
        private static ModAPI.Actors.IActorSystem _instance;

        /// <summary>
        /// Shared Sheltered actor system instance.
        /// </summary>
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

        /// <summary>
        /// Creates the actor ID used for a Sheltered family member.
        /// </summary>
        public static ActorId FamilyMemberActorId(int uniqueMemberId)
        {
            return new ActorId(ActorKind.Player, uniqueMemberId, string.Empty);
        }

        /// <summary>
        /// Creates the actor ID used for a Sheltered visitor.
        /// </summary>
        public static ActorId VisitorActorId(int uniqueVisitorId)
        {
            return new ActorId(ActorKind.Visitor, uniqueVisitorId, string.Empty);
        }

        /// <summary>
        /// Creates the actor ID used for a mod-created synthetic character.
        /// Include the source mod ID to avoid collisions between mods.
        /// </summary>
        public static ActorId SyntheticCharacterActorId(int uniqueCharacterId, string sourceModId)
        {
            return new ActorId(ActorKind.Synthetic, uniqueCharacterId, sourceModId ?? string.Empty);
        }

        /// <summary>
        /// Attempts to resolve a Sheltered character actor ID back to a character proxy.
        /// Returns false for non-character actor kinds.
        /// </summary>
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
