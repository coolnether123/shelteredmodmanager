using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesNeedSnapshot
    {
        public ulong CharacterGuid { get; internal set; }

        public ulong NeedGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public bool IsKnownNeed { get; internal set; }

        public bool IsActive { get; internal set; }

        public bool IsWarning { get; internal set; }

        public bool IsCritical { get; internal set; }

        public bool IsFatal { get; internal set; }

        public bool IsRelieving { get; internal set; }

        public float Value { get; internal set; }

        public float SavedValue { get; internal set; }

        public float MaxValue { get; internal set; }

        public float DecayValue { get; internal set; }

        public bool IsMasked { get; internal set; }

        public int MaskingType { get; internal set; }

        public ulong MaskingStatusEffectGuid { get; internal set; }
    }

    public sealed class ParalivesNeedsFacade
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesSettingsFacade _settings;

        public event System.Action<ParalivesNeedChangedEvent> NeedChanged;

        internal ParalivesNeedsFacade(ParalivesCharacterFacade characters, ParalivesSettingsFacade settings)
        {
            _characters = characters;
            _settings = settings;
        }

        public ParalivesNeedSnapshot[] ReadNeeds(ulong characterGuid)
        {
            return ReadNeeds(characterGuid, true);
        }

        public ParalivesNeedSnapshot[] ReadNeeds(ulong characterGuid, bool includeConfiguredNeeds)
        {
            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character) || character.Data == null)
                return new ParalivesNeedSnapshot[0];

            List<ParalivesNeedSnapshot> snapshots = new List<ParalivesNeedSnapshot>();
            HashSet<ulong> seen = new HashSet<ulong>();

            if (character.Data.NeedSaveData != null)
            {
                for (int i = 0; i < character.Data.NeedSaveData.Count; i++)
                {
                    global::AssetCharacterNeedSaveData data = character.Data.NeedSaveData[i];
                    if (data == null || data.NeedGUID == 0UL || seen.Contains(data.NeedGUID))
                        continue;

                    snapshots.Add(ReadNeed(character, data.NeedGUID, data));
                    seen.Add(data.NeedGUID);
                }
            }

            if (includeConfiguredNeeds)
            {
                Needs needs;
                if (_settings.TryGet<Needs>(out needs) && needs.AllNeeds != null)
                {
                    for (int i = 0; i < needs.AllNeeds.Length; i++)
                    {
                        Need need = needs.AllNeeds[i];
                        if (need == null || need.GUID == 0UL || seen.Contains(need.GUID))
                            continue;

                        snapshots.Add(ReadNeed(character, need.GUID, null));
                        seen.Add(need.GUID);
                    }
                }
            }

            return snapshots.ToArray();
        }

        public bool TryReadNeed(ulong characterGuid, ulong needGuid, out ParalivesNeedSnapshot snapshot)
        {
            snapshot = null;
            if (needGuid == 0UL)
                return false;

            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character) || character.Data == null)
                return false;

            snapshot = ReadNeed(character, needGuid, FindSavedNeed(character, needGuid));
            return snapshot != null && snapshot.IsKnownNeed;
        }

        public bool TrySetNeedValue(ulong characterGuid, ulong needGuid, float value)
        {
            global::AssetCharacter character;
            if (needGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::NeedManager.Instance.SetNeedToValue(needGuid, character, value, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryChangeNeedValue(ulong characterGuid, ulong needGuid, float amount)
        {
            global::AssetCharacter character;
            if (needGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::NeedManager.Instance.ChangeNeedByValue(needGuid, character, amount);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private ParalivesNeedSnapshot ReadNeed(
            global::AssetCharacter character,
            ulong needGuid,
            global::AssetCharacterNeedSaveData saved)
        {
            Need need = null;
            Needs needs;
            if (_settings.TryGet<Needs>(out needs))
                need = needs.GetNeedByGUID(needGuid);

            ParalivesNeedSnapshot snapshot = new ParalivesNeedSnapshot
            {
                CharacterGuid = character.GUID,
                NeedGuid = needGuid,
                DisplayName = need == null ? string.Empty : (need.DisplayName ?? string.Empty),
                IsKnownNeed = need != null,
                SavedValue = saved == null ? (need == null ? 0f : need.DefaultValue) : saved.Value,
                DecayValue = saved == null ? 0f : saved.DecayValue
            };

            try
            {
                if (saved != null)
                {
                    snapshot.IsActive = global::NeedManager.Instance.IsNeedActive(needGuid, character);
                    snapshot.Value = global::NeedManager.Instance.GetNeedValue(needGuid, character);
                    snapshot.MaxValue = global::NeedManager.Instance.GetNeedCap(needGuid, character);
                    snapshot.IsWarning = global::NeedManager.Instance.IsNeedWarning(needGuid, character);
                    snapshot.IsCritical = global::NeedManager.Instance.IsNeedCritical(needGuid, character);
                    snapshot.IsFatal = global::NeedManager.Instance.IsNeedFatal(needGuid, character);
                    snapshot.IsRelieving = global::NeedManager.Instance.IsRelieving(needGuid, character);

                    var uiValue = global::NeedManager.Instance.GetNeedValueForUI(needGuid, character);
                    snapshot.IsMasked = uiValue.Item2;
                    snapshot.MaskingType = (int)uiValue.Item3;
                    snapshot.MaskingStatusEffectGuid = uiValue.Item4;
                }
                else
                {
                    FillUnsavedNeedSnapshot(character, need, snapshot);
                }
            }
            catch
            {
            }

            return snapshot;
        }

        internal void PublishChanged(ParalivesNeedChangedEvent evt)
        {
            if (evt == null)
                return;

            System.Action<ParalivesNeedChangedEvent> handler = NeedChanged;
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

        private static void FillUnsavedNeedSnapshot(
            global::AssetCharacter character,
            Need need,
            ParalivesNeedSnapshot snapshot)
        {
            if (need == null || snapshot == null)
                return;

            snapshot.IsActive = need.ActivatedByDefault;
            if (!snapshot.IsActive)
            {
                try
                {
                    snapshot.IsActive = global::StatusEffectManager.Instance
                        .GetStatusEffectLogicsAndDurationsByEffectType(
                            StatusEffectEffectTypes.ActivateNeed,
                            need.GUID,
                            character)
                        .Count > 0;
                }
                catch
                {
                }
            }

            snapshot.MaxValue = need.MaxCap;
            snapshot.Value = snapshot.IsActive
                ? (character != null && !character.IsInHousehold ? 10f : need.DefaultValue)
                : 0f;

            try
            {
                if (character != null
                    && character.Data != null
                    && character.Data.MaskedFromStatusEffects != null
                    && character.Data.MaskedFromStatusEffects.ContainsKey(need.GUID))
                {
                    var mask = character.Data.MaskedFromStatusEffects[need.GUID];
                    snapshot.IsMasked = true;
                    snapshot.MaskingType = (int)mask.Item1;
                    snapshot.MaskingStatusEffectGuid = mask.Item2;
                    snapshot.Value = snapshot.MaxValue;
                }
            }
            catch
            {
            }

            Needs needs = null;
            try
            {
                needs = global::Settings.Get<Needs>();
            }
            catch
            {
            }

            int warning = needs == null ? 4 : needs.WarningTreshold;
            int critical = needs == null ? 2 : needs.CriticalTreshold;
            snapshot.IsWarning = snapshot.IsActive && snapshot.Value <= warning;
            snapshot.IsCritical = snapshot.IsActive && snapshot.Value <= critical;
            snapshot.IsFatal = snapshot.IsActive && snapshot.Value <= 0f;
        }

        private static global::AssetCharacterNeedSaveData FindSavedNeed(global::AssetCharacter character, ulong needGuid)
        {
            if (character == null || character.Data == null || character.Data.NeedSaveData == null)
                return null;

            for (int i = 0; i < character.Data.NeedSaveData.Count; i++)
            {
                global::AssetCharacterNeedSaveData data = character.Data.NeedSaveData[i];
                if (data != null && data.NeedGUID == needGuid)
                    return data;
            }

            return null;
        }
    }
}
