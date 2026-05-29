using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesCharacterFacade
    {
        private readonly ParalivesPlayerFacade _players;

        internal ParalivesCharacterFacade(ParalivesPlayerFacade players)
        {
            _players = players;
        }

        public bool TryGet(ulong characterGuid, out global::AssetCharacter character)
        {
            character = null;
            if (characterGuid == 0UL)
                return false;

            try
            {
                if (global::CharacterManager.Instance != null)
                    character = global::CharacterManager.Instance.GetCharacterByGUID(characterGuid);
                if (character == null && global::AssetManager.Instance != null)
                    character = global::AssetManager.Instance.GetCharacter(characterGuid);
            }
            catch
            {
                character = null;
            }

            return character != null;
        }

        public global::AssetCharacter GetOrNull(ulong characterGuid)
        {
            global::AssetCharacter character;
            return TryGet(characterGuid, out character) ? character : null;
        }

        public global::AssetCharacter[] GetAll()
        {
            if (global::CharacterManager.Instance == null || global::CharacterManager.Instance.Characters == null)
                return new global::AssetCharacter[0];

            return global::CharacterManager.Instance.Characters.ToArray();
        }

        public bool TryGetSelected(int playerIndex, out global::AssetCharacter character)
        {
            character = null;
            ulong characterGuid = _players.GetSelectedCharacterGuid(playerIndex);
            return TryGet(characterGuid, out character);
        }

        public bool Select(ulong characterGuid, int playerIndex)
        {
            return Select(characterGuid, playerIndex, true);
        }

        public bool Select(ulong characterGuid, int playerIndex, bool force)
        {
            if (characterGuid == 0UL || global::CharacterManager.Instance == null)
                return false;

            try
            {
                global::CharacterManager.Instance.SelectCharacter(characterGuid, playerIndex, force);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetDisplayName(global::AssetCharacter character)
        {
            if (character == null || character.Data == null)
                return string.Empty;

            string fullName = character.Data.FullName;
            return string.IsNullOrWhiteSpace(fullName) ? character.GUID.ToString() : fullName.Trim();
        }

        public string GetDisplayName(ulong characterGuid)
        {
            global::AssetCharacter character;
            return TryGet(characterGuid, out character) ? GetDisplayName(character) : string.Empty;
        }

        public bool TryGetLifeStage(ulong characterGuid, out LifeStage lifeStage)
        {
            lifeStage = null;
            global::AssetCharacter character;
            if (!TryGet(characterGuid, out character))
                return false;

            try
            {
                lifeStage = global::LifeStageManager.Instance != null
                    ? global::LifeStageManager.Instance.GetCurrentLifeStageOfCharacter(character)
                    : null;
            }
            catch
            {
                lifeStage = null;
            }

            return lifeStage != null;
        }

        public bool TryGetCurrentLotGuid(ulong characterGuid, out ulong lotGuid)
        {
            lotGuid = 0UL;
            global::AssetCharacter character;
            if (!TryGet(characterGuid, out character))
                return false;

            try
            {
                if (global::CharacterManager.Instance == null)
                    return false;

                lotGuid = global::CharacterManager.Instance.GetLotForCharacter(character);
                return lotGuid != 0UL;
            }
            catch
            {
                lotGuid = 0UL;
                return false;
            }
        }

        public ulong[] GetCurrentHouseholdCharacterGuids()
        {
            try
            {
                if (global::HouseholdManager.Instance == null)
                    return new ulong[0];

                List<ulong> guids = global::HouseholdManager.Instance.GetCharactersInCurrentHousehold();
                return guids == null ? new ulong[0] : guids.ToArray();
            }
            catch
            {
                return new ulong[0];
            }
        }

        public global::AssetCharacter[] GetCurrentHouseholdCharacters()
        {
            ulong[] guids = GetCurrentHouseholdCharacterGuids();
            List<global::AssetCharacter> characters = new List<global::AssetCharacter>();
            for (int i = 0; i < guids.Length; i++)
            {
                global::AssetCharacter character;
                if (TryGet(guids[i], out character))
                    characters.Add(character);
            }

            return characters.ToArray();
        }

        public bool IsInCurrentHousehold(ulong characterGuid)
        {
            if (characterGuid == 0UL)
                return false;

            ulong[] household = GetCurrentHouseholdCharacterGuids();
            for (int i = 0; i < household.Length; i++)
            {
                if (household[i] == characterGuid)
                    return true;
            }

            return false;
        }

        public ulong[] GetHouseholdCharacterGuids(ulong characterGuid)
        {
            global::AssetCharacter character;
            if (!TryGet(characterGuid, out character))
                return new ulong[0];

            try
            {
                if (character.Household != null
                    && character.Household.Data != null
                    && character.Household.Data.Characters != null)
                {
                    return character.Household.Data.Characters.ToArray();
                }
            }
            catch
            {
            }

            return IsInCurrentHousehold(characterGuid)
                ? GetCurrentHouseholdCharacterGuids()
                : new ulong[0];
        }

        public bool IsDeadOrTakenAway(ulong characterGuid)
        {
            global::AssetCharacter character;
            return TryGet(characterGuid, out character)
                && character.Data != null
                && character.Data.IsDeadOrTakenAway;
        }

        public bool IsAvailableForGameplay(ulong characterGuid)
        {
            global::AssetCharacter character;
            return TryGet(characterGuid, out character) && IsAvailableForGameplay(character);
        }

        public bool IsAvailableForGameplay(global::AssetCharacter character)
        {
            if (character == null || character.Data == null)
                return false;

            return !character.Data.IsDeadOrTakenAway
                && !character.Data.IsUnselectable
                && !character.IsDummy
                && !character.DoNotLoadVisual;
        }

        public bool HasCharacterRequirement(ulong characterGuid, ulong requirementGuid)
        {
            if (characterGuid == 0UL)
                return false;
            if (requirementGuid == 0UL)
                return true;

            try
            {
                return global::CharacterManager.Instance != null
                    && global::CharacterManager.Instance.CharacterHasCharacterRequirement(characterGuid, requirementGuid);
            }
            catch
            {
                return false;
            }
        }

        public bool IsLifeStageNamedAny(ulong characterGuid, params string[] names)
        {
            LifeStage lifeStage;
            if (!TryGetLifeStage(characterGuid, out lifeStage) || names == null)
                return false;

            string displayName = lifeStage.DisplayName ?? string.Empty;
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (!string.IsNullOrEmpty(name)
                    && displayName.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsTeenOrOlder(ulong characterGuid)
        {
            return IsLifeStageNamedAny(characterGuid, "teen", "adult", "elder");
        }

        public void MarkSaveDirty(global::AssetCharacter character)
        {
            if (character != null)
                character.IsSaveDirty = true;
        }

        public bool MarkSaveDirty(ulong characterGuid)
        {
            global::AssetCharacter character;
            if (!TryGet(characterGuid, out character))
                return false;

            character.IsSaveDirty = true;
            return true;
        }
    }
}
