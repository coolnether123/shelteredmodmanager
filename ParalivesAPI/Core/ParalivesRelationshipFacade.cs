using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesRelationshipLabelSnapshot
    {
        public ulong SourceCharacterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public ulong LabelGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public int Level { get; internal set; }

        public bool IsKnownLabel { get; internal set; }

        public bool IsHidden { get; internal set; }
    }

    public sealed class ParalivesRelationshipSnapshot
    {
        public ulong SourceCharacterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public string TargetDisplayName { get; internal set; }

        public float TimestampOfLastInteracted { get; internal set; }

        public ParalivesRelationshipLabelSnapshot[] Labels { get; internal set; }
    }

    public sealed class ParalivesRelationshipFacade
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesSettingsFacade _settings;

        public event System.Action<ParalivesRelationshipChangedEvent> RelationshipChanged;

        internal ParalivesRelationshipFacade(ParalivesCharacterFacade characters, ParalivesSettingsFacade settings)
        {
            _characters = characters;
            _settings = settings;
        }

        public ParalivesRelationshipSnapshot[] ReadRelationships(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? ReadRelationships(character)
                : new ParalivesRelationshipSnapshot[0];
        }

        public ParalivesRelationshipSnapshot[] ReadRelationships(global::AssetCharacter character)
        {
            List<ParalivesRelationshipSnapshot> snapshots = new List<ParalivesRelationshipSnapshot>();
            if (character == null || character.Data == null || character.Data.Relationships == null)
                return snapshots.ToArray();

            for (int i = 0; i < character.Data.Relationships.Count; i++)
            {
                global::AssetCharacterRelationshipData relationship = character.Data.Relationships[i];
                if (relationship != null && relationship.With != 0UL)
                    snapshots.Add(CreateSnapshot(character.GUID, relationship));
            }

            return snapshots.ToArray();
        }

        public ulong[] GetKnownCharacterGuids(ulong characterGuid)
        {
            List<ulong> guids = new List<ulong>();
            ParalivesRelationshipSnapshot[] relationships = ReadRelationships(characterGuid);
            for (int i = 0; i < relationships.Length; i++)
            {
                if (relationships[i].TargetCharacterGuid != 0UL && !guids.Contains(relationships[i].TargetCharacterGuid))
                    guids.Add(relationships[i].TargetCharacterGuid);
            }

            return guids.ToArray();
        }

        public ParalivesRelationshipLabelSnapshot[] GetLabelsBetween(ulong sourceGuid, ulong targetGuid)
        {
            List<ParalivesRelationshipLabelSnapshot> labels = new List<ParalivesRelationshipLabelSnapshot>();
            if (sourceGuid == 0UL || targetGuid == 0UL || sourceGuid == targetGuid)
                return labels.ToArray();

            try
            {
                List<global::AssetCharacterRelationshipLabelData> data =
                    global::RelationshipManager.Instance.GetLabelsBetweenCharacters(sourceGuid, targetGuid);
                if (data == null)
                    return labels.ToArray();

                for (int i = 0; i < data.Count; i++)
                {
                    if (data[i] != null)
                        labels.Add(CreateLabelSnapshot(sourceGuid, targetGuid, data[i]));
                }
            }
            catch
            {
            }

            return labels.ToArray();
        }

        public bool HasLabel(ulong sourceGuid, ulong targetGuid, ulong labelGuid)
        {
            return HasLabel(sourceGuid, targetGuid, labelGuid, false);
        }

        public bool HasLabel(ulong sourceGuid, ulong targetGuid, ulong labelGuid, bool useEquivalences)
        {
            if (sourceGuid == 0UL || targetGuid == 0UL || labelGuid == 0UL || sourceGuid == targetGuid)
                return false;

            try
            {
                return global::RelationshipManager.Instance.IsRelationshipLabelPresent(
                    sourceGuid,
                    targetGuid,
                    labelGuid,
                    useEquivalences);
            }
            catch
            {
                return false;
            }
        }

        public bool TryGetLabel(
            ulong sourceGuid,
            ulong targetGuid,
            ulong labelGuid,
            out ParalivesRelationshipLabelSnapshot label)
        {
            return TryGetLabel(sourceGuid, targetGuid, labelGuid, false, out label);
        }

        public bool TryGetLabel(
            ulong sourceGuid,
            ulong targetGuid,
            ulong labelGuid,
            bool useEquivalences,
            out ParalivesRelationshipLabelSnapshot label)
        {
            label = null;
            if (sourceGuid == 0UL || targetGuid == 0UL || labelGuid == 0UL || sourceGuid == targetGuid)
                return false;

            try
            {
                global::AssetCharacterRelationshipLabelData data =
                    global::RelationshipManager.Instance.GetRelationshipLabel(sourceGuid, targetGuid, labelGuid, useEquivalences);
                if (data == null)
                    return false;

                label = CreateLabelSnapshot(sourceGuid, targetGuid, data);
                return true;
            }
            catch
            {
                label = null;
                return false;
            }
        }

        public bool TryUnlockLabel(ulong sourceGuid, ulong targetGuid, ulong labelGuid)
        {
            return TryUnlockLabel(sourceGuid, targetGuid, labelGuid, false, false);
        }

        public bool TryUnlockLabel(
            ulong sourceGuid,
            ulong targetGuid,
            ulong labelGuid,
            bool canAddInverseLabels,
            bool canAddOtherRelativeLabels)
        {
            if (sourceGuid == 0UL || targetGuid == 0UL || labelGuid == 0UL || sourceGuid == targetGuid)
                return false;

            try
            {
                bool changed = global::RelationshipManager.Instance.UnlockLabel(
                    sourceGuid,
                    targetGuid,
                    labelGuid,
                    canAddInverseLabels,
                    canAddOtherRelativeLabels);
                MarkCharactersDirty(sourceGuid, targetGuid);
                return changed;
            }
            catch
            {
                return false;
            }
        }

        public bool TryRemoveLabel(ulong sourceGuid, ulong targetGuid, ulong labelGuid)
        {
            return TryRemoveLabel(sourceGuid, targetGuid, labelGuid, false);
        }

        public bool TryRemoveLabel(ulong sourceGuid, ulong targetGuid, ulong labelGuid, bool canRemoveSymmetricalLabel)
        {
            if (sourceGuid == 0UL || targetGuid == 0UL || labelGuid == 0UL || sourceGuid == targetGuid)
                return false;

            try
            {
                global::RelationshipManager.Instance.RemoveRelationshipLabel(
                    sourceGuid,
                    targetGuid,
                    labelGuid,
                    canRemoveSymmetricalLabel);
                MarkCharactersDirty(sourceGuid, targetGuid);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public int IncrementLabelLevel(ulong sourceGuid, ulong targetGuid, ulong labelGuid, int increment)
        {
            return IncrementLabelLevel(sourceGuid, targetGuid, labelGuid, increment, false);
        }

        public int IncrementLabelLevel(
            ulong sourceGuid,
            ulong targetGuid,
            ulong labelGuid,
            int increment,
            bool canLevelSymmetricalRelationship)
        {
            if (sourceGuid == 0UL || targetGuid == 0UL || labelGuid == 0UL || sourceGuid == targetGuid)
                return 0;

            try
            {
                int level = global::RelationshipManager.Instance.IncrementLabelLevel(
                    sourceGuid,
                    targetGuid,
                    labelGuid,
                    increment,
                    canLevelSymmetricalRelationship);
                MarkCharactersDirty(sourceGuid, targetGuid);
                return level;
            }
            catch
            {
                return 0;
            }
        }

        private ParalivesRelationshipSnapshot CreateSnapshot(
            ulong sourceGuid,
            global::AssetCharacterRelationshipData relationship)
        {
            List<ParalivesRelationshipLabelSnapshot> labels = new List<ParalivesRelationshipLabelSnapshot>();
            if (relationship.RelationshipLabelData != null)
            {
                for (int i = 0; i < relationship.RelationshipLabelData.Count; i++)
                {
                    global::AssetCharacterRelationshipLabelData label = relationship.RelationshipLabelData[i];
                    if (label != null)
                        labels.Add(CreateLabelSnapshot(sourceGuid, relationship.With, label));
                }
            }

            return new ParalivesRelationshipSnapshot
            {
                SourceCharacterGuid = sourceGuid,
                TargetCharacterGuid = relationship.With,
                TargetDisplayName = _characters.GetDisplayName(relationship.With),
                TimestampOfLastInteracted = relationship.TimestampOfLastInteracted,
                Labels = labels.ToArray()
            };
        }

        private ParalivesRelationshipLabelSnapshot CreateLabelSnapshot(
            ulong sourceGuid,
            ulong targetGuid,
            global::AssetCharacterRelationshipLabelData data)
        {
            RelationshipLabel setting;
            bool known = _settings.TryGetRelationshipLabel(data.LabelGUID, out setting);

            return new ParalivesRelationshipLabelSnapshot
            {
                SourceCharacterGuid = sourceGuid,
                TargetCharacterGuid = targetGuid,
                LabelGuid = data.LabelGUID,
                Level = data.Level,
                IsKnownLabel = known,
                DisplayName = known && setting != null ? (setting.DisplayName ?? string.Empty) : string.Empty,
                IsHidden = setting != null && setting.IsHidden
            };
        }

        private void MarkCharactersDirty(ulong sourceGuid, ulong targetGuid)
        {
            _characters.MarkSaveDirty(sourceGuid);
            _characters.MarkSaveDirty(targetGuid);
        }

        internal void PublishChanged(ParalivesRelationshipChangedEvent evt)
        {
            if (evt == null)
                return;

            System.Action<ParalivesRelationshipChangedEvent> handler = RelationshipChanged;
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
    }
}
