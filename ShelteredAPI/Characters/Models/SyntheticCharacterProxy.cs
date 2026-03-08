using System;

namespace ModAPI.Characters
{
    public class SyntheticCharacterProxy : ICharacterProxy
    {
        public int UniqueId { get; private set; }
        public string Name
        {
            get
            {
                string first = Data != null ? Data.FirstName : string.Empty;
                string last = Data != null ? Data.LastName : string.Empty;
                return (first + " " + last).Trim();
            }
        }
        public CharacterState State { get; internal set; }
        public CharacterLocation Location
        {
            get
            {
                if (State == CharacterState.InEncounter || State == CharacterState.SyntheticInEncounter) return CharacterLocation.Shelter;
                if (State == CharacterState.SyntheticAbsent
                    || State == CharacterState.TemporarilyAbsent
                    || State == CharacterState.SyntheticIdle)
                    return CharacterLocation.Away;
                return CharacterLocation.Away;
            }
        }
        public FamilyMember UnderlyingMember { get { return null; } }
        public NpcVisitor UnderlyingNpc { get { return null; } }
        public bool IsActive { get; private set; }
        public bool IsPersistent { get { return Data != null && Data.IsPersistent; } }
        public bool IsLoadedOnShelterEntry { get; internal set; }
        public string PersistenceKey { get { return Data != null ? Data.PersistenceKey : string.Empty; } }
        public CharacterSource Source { get { return CharacterSource.Synthetic; } }
        public string SourceMod { get { return Data != null ? Data.SourceMod : string.Empty; } }

        public ICharacterEffects Effects { get; internal set; }
        public ICharacterAttributes Attributes { get; internal set; }
        public ICharacterData Data { get; internal set; }

        public event Action<ICharacterProxy> OnUnregistered;

        internal SyntheticCharacterProxy(int id)
        {
            UniqueId = id;
            State = CharacterState.SyntheticAbsent;
            IsActive = true;
            IsLoadedOnShelterEntry = false;
        }

        internal void Unregister()
        {
            if (!IsActive) return;
            IsActive = false;
            var evt = OnUnregistered;
            if (evt != null) evt(this);
        }
    }
}
