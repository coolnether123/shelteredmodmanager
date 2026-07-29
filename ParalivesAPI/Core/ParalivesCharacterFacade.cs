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
            Requirements = new ParalivesRequirementFacade(this);
        }

        public ParalivesRequirementFacade Requirements
        {
            get;
            private set;
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
            try
            {
                if (global::CharacterManager.Instance == null || global::CharacterManager.Instance.Characters == null)
                    return new global::AssetCharacter[0];

                return global::CharacterManager.Instance.Characters.ToArray();
            }
            catch
            {
                return new global::AssetCharacter[0];
            }
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

            try
            {
                string fullName = character.Data.FullName;
                return string.IsNullOrWhiteSpace(fullName) ? character.GUID.ToString() : fullName.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public string GetDisplayName(ulong characterGuid)
        {
            global::AssetCharacter character;
            return TryGet(characterGuid, out character) ? GetDisplayName(character) : string.Empty;
        }

        public ParalivesCharacterSnapshot ReadSnapshot(ulong characterGuid)
        {
            ParalivesCharacterSnapshot snapshot;
            return TryReadSnapshot(characterGuid, out snapshot) ? snapshot : CreateMissingSnapshot(characterGuid);
        }

        public bool TryReadSnapshot(ulong characterGuid, out ParalivesCharacterSnapshot snapshot)
        {
            snapshot = CreateMissingSnapshot(characterGuid);

            global::AssetCharacter character;
            if (!TryGet(characterGuid, out character) || character.Data == null)
                return false;

            try
            {
                snapshot.Exists = true;
                snapshot.CharacterGuid = character.GUID;
                snapshot.DisplayName = GetDisplayName(character);
                snapshot.ShortName = character.Data.ShortName ?? string.Empty;
                snapshot.HouseholdGuid = character.Household == null ? 0UL : character.Household.GUID;
                snapshot.HouseholdCharacterGuids = GetHouseholdCharacterGuids(character.GUID);
                snapshot.IsInHousehold = character.IsInHousehold;
                snapshot.IsInCurrentHousehold = IsInCurrentHousehold(character.GUID);
                snapshot.SelectedPlayerIndex = FindSelectedPlayerIndex(character.GUID);
                snapshot.IsSelected = snapshot.SelectedPlayerIndex >= 0;
                snapshot.IsDead = character.Data.IsDead;
                snapshot.IsTakenAway = character.Data.TakenAwayBySocialServices;
                snapshot.IsDeadOrTakenAway = character.Data.IsDeadOrTakenAway;
                snapshot.IsUnselectable = character.Data.IsUnselectable;
                snapshot.IsAvailableForGameplay = IsAvailableForGameplay(character);
                snapshot.IsVisualLoaded = character.IsVisualLoaded;
                snapshot.IsVisibleInWorld = character.IsVisibleInWorld;
                snapshot.IsDummy = character.IsDummy;
                snapshot.DoNotLoadVisual = character.DoNotLoadVisual;
                snapshot.CharacterRequirementsMet = character.CharacterRequirementsMet == null
                    ? new ulong[0]
                    : character.CharacterRequirementsMet.ToArray();

                ulong lotGuid;
                snapshot.CurrentLotGuid = TryGetCurrentLotGuid(character.GUID, out lotGuid) ? lotGuid : 0UL;

                LifeStage lifeStage;
                if (TryGetLifeStage(character.GUID, out lifeStage))
                {
                    snapshot.LifeStageGuid = lifeStage.GUID;
                    snapshot.LifeStageDisplayName = lifeStage.DisplayName ?? string.Empty;
                }

                return true;
            }
            catch
            {
                snapshot = CreateMissingSnapshot(characterGuid);
                return false;
            }
        }

        public ParalivesCharacterSnapshot[] ReadCurrentHouseholdSnapshots()
        {
            ulong[] guids = GetCurrentHouseholdCharacterGuids();
            List<ParalivesCharacterSnapshot> snapshots = new List<ParalivesCharacterSnapshot>();
            for (int i = 0; i < guids.Length; i++)
            {
                ParalivesCharacterSnapshot snapshot;
                if (TryReadSnapshot(guids[i], out snapshot))
                    snapshots.Add(snapshot);
            }

            return snapshots.ToArray();
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
            try
            {
                return TryGet(characterGuid, out character)
                    && character.Data != null
                    && character.Data.IsDeadOrTakenAway;
            }
            catch
            {
                return false;
            }
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

            try
            {
                return !character.Data.IsDeadOrTakenAway
                    && !character.Data.IsUnselectable
                    && !character.IsDummy
                    && !character.DoNotLoadVisual;
            }
            catch
            {
                return false;
            }
        }

        public bool HasCharacterRequirement(ulong characterGuid, ulong requirementGuid)
        {
            return Requirements.CharacterHasRequirement(characterGuid, requirementGuid);
        }

        public bool IsCurrentLifeStage(ulong characterGuid, ulong lifeStageGuid)
        {
            if (characterGuid == 0UL || lifeStageGuid == 0UL)
                return false;

            LifeStage lifeStage;
            return TryGetLifeStage(characterGuid, out lifeStage) && lifeStage.GUID == lifeStageGuid;
        }

        public bool IsCurrentLifeStageAny(ulong characterGuid, params ulong[] lifeStageGuids)
        {
            if (characterGuid == 0UL || lifeStageGuids == null || lifeStageGuids.Length == 0)
                return false;

            LifeStage lifeStage;
            if (!TryGetLifeStage(characterGuid, out lifeStage))
                return false;

            for (int i = 0; i < lifeStageGuids.Length; i++)
            {
                if (lifeStageGuids[i] != 0UL && lifeStageGuids[i] == lifeStage.GUID)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Compatibility helper for older callers. Prefer requirement GUIDs or life-stage GUID checks when available.
        /// </summary>
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

        /// <summary>
        /// Compatibility helper for older callers. Prefer CharacterHasRequirement with a game requirement GUID when available.
        /// </summary>
        public bool IsTeenOrOlder(ulong characterGuid)
        {
            return IsLifeStageNamedAny(characterGuid, "teen", "adult", "elder");
        }

        public void MarkSaveDirty(global::AssetCharacter character)
        {
            try
            {
                if (character != null)
                    character.IsSaveDirty = true;
            }
            catch
            {
            }
        }

        public bool MarkSaveDirty(ulong characterGuid)
        {
            global::AssetCharacter character;
            if (!TryGet(characterGuid, out character))
                return false;

            try
            {
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int FindSelectedPlayerIndex(ulong characterGuid)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                ulong[] selected = _players.GetSelectedCharacterGuids(i);
                for (int j = 0; j < selected.Length; j++)
                {
                    if (selected[j] == characterGuid)
                        return i;
                }
            }

            return -1;
        }

        private static ParalivesCharacterSnapshot CreateMissingSnapshot(ulong characterGuid)
        {
            return new ParalivesCharacterSnapshot
            {
                CharacterGuid = characterGuid,
                DisplayName = string.Empty,
                ShortName = string.Empty,
                SelectedPlayerIndex = -1,
                HouseholdCharacterGuids = new ulong[0],
                CharacterRequirementsMet = new ulong[0]
            };
        }
    }
}
