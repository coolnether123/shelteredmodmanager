using System;
using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesWantEntry
    {
        public ulong CharacterGuid { get; internal set; }

        public int Index { get; internal set; }

        public ulong WantGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public string FullName { get; internal set; }

        public ulong BrainLogicGuid { get; internal set; }

        public float Timestamp { get; internal set; }

        public float Progress { get; internal set; }

        public float Goal { get; internal set; }

        public ulong CharacterTargetGuid { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public ulong SkinGuid { get; internal set; }

        public ulong CatalogueGuid { get; internal set; }

        public bool IsPinned { get; internal set; }

        public bool DoesNotCount { get; internal set; }

        public bool HasPlayedAppearAnimation { get; internal set; }

        public global::AssetCharacterWantStatus Status { get; internal set; }

        public float ClearTimestamp { get; internal set; }
    }

    public sealed class ParalivesOfferedWantEntry
    {
        public ulong CharacterGuid { get; internal set; }

        public int Index { get; internal set; }

        public ulong WantGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public string FullName { get; internal set; }

        public ulong BrainLogicGuid { get; internal set; }

        public ulong EmotionGuid { get; internal set; }

        public ulong StatusEffectGuid { get; internal set; }

        public ulong NeedGuid { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public ulong SkinGuid { get; internal set; }

        public ulong CatalogueGuid { get; internal set; }

        public ulong OtherCharacterGuid { get; internal set; }
    }

    public sealed class ParalivesWantFacade
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesSettingsFacade _settings;

        public event Action<ParalivesWantChangedEvent> WantChanged;

        internal ParalivesWantFacade(ParalivesCharacterFacade characters, ParalivesSettingsFacade settings)
        {
            _characters = characters;
            _settings = settings;
        }

        public ParalivesWantEntry[] ReadWants(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? ReadWants(character)
                : new ParalivesWantEntry[0];
        }

        public ParalivesWantEntry[] ReadWants(global::AssetCharacter character)
        {
            List<ParalivesWantEntry> entries = new List<ParalivesWantEntry>();
            if (character == null || character.Data == null || character.Data.Wants == null)
                return entries.ToArray();

            for (int i = 0; i < character.Data.Wants.Count; i++)
            {
                global::AssetCharacterWantData want = character.Data.Wants[i];
                if (want != null)
                    entries.Add(CreateEntry(character, i, want));
            }

            return entries.ToArray();
        }

        public ParalivesWantEntry[] ReadActiveWants(ulong characterGuid)
        {
            List<ParalivesWantEntry> active = new List<ParalivesWantEntry>();
            ParalivesWantEntry[] entries = ReadWants(characterGuid);
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Status == global::AssetCharacterWantStatus.Active)
                    active.Add(entries[i]);
            }

            return active.ToArray();
        }

        public ParalivesOfferedWantEntry[] ReadOfferedWants(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? ReadOfferedWants(character)
                : new ParalivesOfferedWantEntry[0];
        }

        public ParalivesOfferedWantEntry[] ReadOfferedWants(global::AssetCharacter character)
        {
            List<ParalivesOfferedWantEntry> entries = new List<ParalivesOfferedWantEntry>();
            if (character == null || character.Data == null || character.Data.OfferedWants == null)
                return entries.ToArray();

            for (int i = 0; i < character.Data.OfferedWants.Count; i++)
            {
                global::AssetCharacterOfferedWant want = character.Data.OfferedWants[i];
                if (want != null)
                    entries.Add(CreateOfferedEntry(character, i, want));
            }

            return entries.ToArray();
        }

        public bool TryGetWantDisplayName(ulong wantGuid, out string displayName)
        {
            displayName = string.Empty;
            Want want;
            if (!_settings.TryGetWant(wantGuid, out want))
                return false;

            displayName = want.DisplayName ?? string.Empty;
            return true;
        }

        public bool TryGetWantFullName(ulong characterGuid, int wantIndex, out string fullName)
        {
            fullName = string.Empty;

            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character)
                || character.Data == null
                || character.Data.Wants == null
                || wantIndex < 0
                || wantIndex >= character.Data.Wants.Count)
            {
                return false;
            }

            global::AssetCharacterWantData data = character.Data.Wants[wantIndex];
            Want want;
            if (data == null || !_settings.TryGetWant(data.WantGUID, out want))
                return false;

            try
            {
                fullName = global::WantsManager.Instance.GetWantFullName(want, data) ?? string.Empty;
                return true;
            }
            catch
            {
                fullName = want.DisplayName ?? string.Empty;
                return fullName.Length > 0;
            }
        }

        public bool CreateOrRefreshActiveWant(
            ulong characterGuid,
            ulong wantGuid,
            ulong occupationGuid,
            ulong skillGuid)
        {
            int wantIndex;
            return CreateOrRefreshActiveWant(characterGuid, wantGuid, occupationGuid, skillGuid, out wantIndex);
        }

        public bool CreateOrRefreshActiveWant(
            ulong characterGuid,
            ulong wantGuid,
            ulong occupationGuid,
            ulong skillGuid,
            out int wantIndex)
        {
            return CreateOrRefreshActiveWant(
                characterGuid,
                wantGuid,
                occupationGuid,
                skillGuid,
                0UL,
                0UL,
                0UL,
                0UL,
                false,
                true,
                out wantIndex);
        }

        public bool CreateOrRefreshOccupationWant(
            ulong characterGuid,
            ulong wantGuid,
            ulong occupationGuid,
            ulong skillGuid,
            out int wantIndex)
        {
            return CreateOrRefreshActiveWant(
                characterGuid,
                wantGuid,
                occupationGuid,
                skillGuid,
                0UL,
                0UL,
                0UL,
                0UL,
                false,
                false,
                out wantIndex);
        }

        public bool CreateOrRefreshActiveWant(
            ulong characterGuid,
            ulong wantGuid,
            ulong occupationGuid,
            ulong skillGuid,
            ulong characterTargetGuid,
            ulong brainLogicGuid,
            ulong skinGuid,
            ulong catalogueGuid,
            bool doesNotCount,
            bool matchSkillGuid,
            out int wantIndex)
        {
            wantIndex = -1;
            if (wantGuid == 0UL)
                return false;

            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character) || character.Data == null)
                return false;

            if (character.Data.Wants == null)
                character.Data.Wants = new List<global::AssetCharacterWantData>();

            for (int i = 0; i < character.Data.Wants.Count; i++)
            {
                global::AssetCharacterWantData existing = character.Data.Wants[i];
                if (existing == null || existing.Status != global::AssetCharacterWantStatus.Active)
                    continue;

                if (existing.WantGUID != wantGuid || existing.OccupationGUID != occupationGuid)
                    continue;

                if (matchSkillGuid && existing.SkillGUID != skillGuid)
                    continue;

                existing.Timestamp = global::ParaTime.TotalMinutes;
                existing.SkillGUID = skillGuid;
                existing.CharacterTargetGUID = characterTargetGuid;
                existing.BrainLogicGUID = brainLogicGuid;
                existing.SkinGUID = skinGuid;
                existing.CatalogueGUID = catalogueGuid;
                existing.DoesNotCount = doesNotCount;
                character.IsSaveDirty = true;
                wantIndex = i;
                return true;
            }

            global::AssetCharacterWantData added = new global::AssetCharacterWantData
            {
                WantGUID = wantGuid,
                BrainLogicGUID = brainLogicGuid,
                CharacterTargetGUID = characterTargetGuid,
                Timestamp = global::ParaTime.TotalMinutes,
                OccupationGUID = occupationGuid,
                SkillGUID = skillGuid,
                SkinGUID = skinGuid,
                CatalogueGUID = catalogueGuid,
                DoesNotCount = doesNotCount,
                Status = global::AssetCharacterWantStatus.Active
            };
            character.Data.Wants.Add(added);
            character.IsSaveDirty = true;
            wantIndex = character.Data.Wants.Count - 1;
            return true;
        }

        public bool TryCompleteWant(ulong characterGuid, int wantIndex)
        {
            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character)
                || character.Data == null
                || character.Data.Wants == null
                || wantIndex < 0
                || wantIndex >= character.Data.Wants.Count)
            {
                return false;
            }

            try
            {
                global::WantsManager.Instance.CompleteWant(character, wantIndex);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public int CompleteMatchingWants(ulong characterGuid, Predicate<ParalivesWantEntry> predicate)
        {
            if (predicate == null)
                return 0;

            int completed = 0;
            ParalivesWantEntry[] wants = ReadWants(characterGuid);
            for (int i = 0; i < wants.Length; i++)
            {
                ParalivesWantEntry want = wants[i];
                if (want.Status == global::AssetCharacterWantStatus.Active
                    && predicate(want)
                    && TryCompleteWant(characterGuid, want.Index))
                {
                    completed++;
                }
            }

            return completed;
        }

        public bool TrySetStatus(ulong characterGuid, int wantIndex, global::AssetCharacterWantStatus status)
        {
            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character)
                || character.Data == null
                || character.Data.Wants == null
                || wantIndex < 0
                || wantIndex >= character.Data.Wants.Count)
            {
                return false;
            }

            try
            {
                global::WantsManager.Instance.SetWantStatus(character, character.Data.Wants[wantIndex], status);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private ParalivesWantEntry CreateEntry(global::AssetCharacter character, int index, global::AssetCharacterWantData want)
        {
            string displayName;
            TryGetWantDisplayName(want.WantGUID, out displayName);

            string fullName;
            if (!TryGetWantFullName(character.GUID, index, out fullName))
                fullName = displayName;

            return new ParalivesWantEntry
            {
                CharacterGuid = character.GUID,
                Index = index,
                WantGuid = want.WantGUID,
                DisplayName = displayName,
                FullName = fullName,
                BrainLogicGuid = want.BrainLogicGUID,
                Timestamp = want.Timestamp,
                Progress = want.Progress,
                Goal = want.Goal,
                CharacterTargetGuid = want.CharacterTargetGUID,
                OccupationGuid = want.OccupationGUID,
                SkillGuid = want.SkillGUID,
                SkinGuid = want.SkinGUID,
                CatalogueGuid = want.CatalogueGUID,
                IsPinned = want.IsPinned,
                DoesNotCount = want.DoesNotCount,
                HasPlayedAppearAnimation = want.HasPlayedAppearAnimation,
                Status = want.Status,
                ClearTimestamp = want.ClearTimestamp
            };
        }

        private ParalivesOfferedWantEntry CreateOfferedEntry(
            global::AssetCharacter character,
            int index,
            global::AssetCharacterOfferedWant want)
        {
            string displayName;
            TryGetWantDisplayName(want.WantGUID, out displayName);

            string fullName = displayName;
            Want setting;
            if (_settings.TryGetWant(want.WantGUID, out setting))
            {
                try
                {
                    fullName = global::WantsManager.Instance.GetWantFullName(setting, want) ?? displayName;
                }
                catch
                {
                }
            }

            return new ParalivesOfferedWantEntry
            {
                CharacterGuid = character.GUID,
                Index = index,
                WantGuid = want.WantGUID,
                DisplayName = displayName,
                FullName = fullName,
                BrainLogicGuid = want.BrainLogicGUID,
                EmotionGuid = want.EmotionGUID,
                StatusEffectGuid = want.StatusEffectGUID,
                NeedGuid = want.NeedGUID,
                SkillGuid = want.SkillGUID,
                SkinGuid = want.SkinGUID,
                CatalogueGuid = want.CatalogueGUID,
                OtherCharacterGuid = want.OtherCharacterGUID
            };
        }

        internal void PublishChanged(ParalivesWantChangedEvent evt)
        {
            if (evt == null)
                return;

            Action<ParalivesWantChangedEvent> handler = WantChanged;
            if (handler == null)
                return;

            try
            {
                handler(evt);
            }
            catch
            {
            }
        }

        internal int FindWantIndex(global::AssetCharacter character, global::AssetCharacterWantData wantData)
        {
            if (character == null || character.Data == null || character.Data.Wants == null || wantData == null)
                return -1;

            for (int i = 0; i < character.Data.Wants.Count; i++)
            {
                if (object.ReferenceEquals(character.Data.Wants[i], wantData))
                    return i;
            }

            return -1;
        }
    }
}
