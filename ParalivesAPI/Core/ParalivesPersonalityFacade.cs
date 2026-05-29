using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesPersonalityTraitSnapshot
    {
        public ulong CharacterGuid { get; internal set; }

        public ulong TraitGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public bool IsKnownTrait { get; internal set; }

        public bool Enabled { get; internal set; }

        public int Points { get; internal set; }

        public ulong CategoryGuid { get; internal set; }

        public ulong[] Evolutions { get; internal set; }
    }

    public sealed class ParalivesPersonalityFacade
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesSettingsFacade _settings;

        internal ParalivesPersonalityFacade(ParalivesCharacterFacade characters, ParalivesSettingsFacade settings)
        {
            _characters = characters;
            _settings = settings;
        }

        public ParalivesPersonalityTraitSnapshot[] ReadTraits(ulong characterGuid)
        {
            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character)
                || character.Data == null
                || character.Data.Personality == null
                || character.Data.Personality.PersonalityTraits == null)
            {
                return new ParalivesPersonalityTraitSnapshot[0];
            }

            List<ParalivesPersonalityTraitSnapshot> traits = new List<ParalivesPersonalityTraitSnapshot>();
            for (int i = 0; i < character.Data.Personality.PersonalityTraits.Count; i++)
            {
                PersonalityTraitSaveData data = character.Data.Personality.PersonalityTraits[i];
                if (data != null && data.PersonalityTrait != 0UL)
                    traits.Add(CreateSnapshot(character.GUID, data));
            }

            return traits.ToArray();
        }

        public bool TryGetTraitDisplayName(ulong traitGuid, out string displayName)
        {
            displayName = string.Empty;
            PersonalityTrait trait;
            if (!_settings.TryGetPersonalityTrait(traitGuid, out trait))
                return false;

            displayName = trait.DisplayName ?? string.Empty;
            return true;
        }

        public bool HasTrait(ulong characterGuid, ulong traitGuid)
        {
            if (traitGuid == 0UL)
                return false;

            ParalivesPersonalityTraitSnapshot[] traits = ReadTraits(characterGuid);
            for (int i = 0; i < traits.Length; i++)
            {
                if (traits[i].TraitGuid == traitGuid)
                    return true;
            }

            return false;
        }

        private ParalivesPersonalityTraitSnapshot CreateSnapshot(ulong characterGuid, PersonalityTraitSaveData data)
        {
            PersonalityTrait trait;
            bool known = _settings.TryGetPersonalityTrait(data.PersonalityTrait, out trait);

            List<ulong> evolutions = new List<ulong>();
            if (data.Evolutions != null)
            {
                for (int i = 0; i < data.Evolutions.Length; i++)
                    evolutions.Add(data.Evolutions[i]);
            }

            return new ParalivesPersonalityTraitSnapshot
            {
                CharacterGuid = characterGuid,
                TraitGuid = data.PersonalityTrait,
                IsKnownTrait = known,
                DisplayName = known && trait != null ? (trait.DisplayName ?? string.Empty) : string.Empty,
                Enabled = trait == null || trait.Enabled,
                Points = data.Points,
                CategoryGuid = trait == null ? 0UL : trait.PersonalityTraitCategory,
                Evolutions = evolutions.ToArray()
            };
        }
    }
}
