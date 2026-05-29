using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesMemorySnapshot
    {
        public ulong CharacterGuid { get; internal set; }

        public int Index { get; internal set; }

        public MemoryLogType MemoryLogType { get; internal set; }

        public global::MemoryData Data { get; internal set; }

        public float StartTime { get; internal set; }

        public float EndTime { get; internal set; }

        public float FlagTime { get; internal set; }

        public bool WasCancelled { get; internal set; }
    }

    public sealed class ParalivesMemoryFacade
    {
        private readonly ParalivesCharacterFacade _characters;

        public event System.Action<ParalivesMemoryChangedEvent> MemoryChanged;

        internal ParalivesMemoryFacade(ParalivesCharacterFacade characters)
        {
            _characters = characters;
        }

        public ParalivesMemorySnapshot[] ReadMemories(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? ReadMemories(character)
                : new ParalivesMemorySnapshot[0];
        }

        public ParalivesMemorySnapshot[] ReadMemories(global::AssetCharacter character)
        {
            List<ParalivesMemorySnapshot> memories = new List<ParalivesMemorySnapshot>();
            if (character == null || character.Data == null || character.Data.MemoryLogSaveData == null)
                return memories.ToArray();

            for (int i = 0; i < character.Data.MemoryLogSaveData.Count; i++)
            {
                global::AssetCharacterMemoryLogSaveData data = character.Data.MemoryLogSaveData[i];
                if (data != null)
                    memories.Add(CreateSnapshot(character.GUID, i, data));
            }

            return memories.ToArray();
        }

        public bool WriteMemory(ulong characterGuid, MemoryLogType memoryType, global::MemoryData data)
        {
            return WriteMemory(characterGuid, memoryType, data, false);
        }

        public bool WriteMemory(ulong characterGuid, MemoryLogType memoryType, global::MemoryData data, bool forceEvenPaused)
        {
            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::MemoryManager.Instance.WriteMemory(character, memoryType, data ?? new global::MemoryData(), forceEvenPaused);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool CancelMemory(ulong characterGuid, MemoryLogType memoryType, global::MemoryData data)
        {
            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::MemoryManager.Instance.SetMemoryAsCancelled(character, memoryType, data ?? new global::MemoryData());
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal void PublishChanged(ParalivesMemoryChangedEvent evt)
        {
            if (evt == null)
                return;

            System.Action<ParalivesMemoryChangedEvent> handler = MemoryChanged;
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

        internal ParalivesMemoryChangedEvent CreateEvent(
            ParalivesMemoryChangeType changeType,
            global::AssetCharacter character,
            global::AssetCharacterMemoryLogSaveData memory)
        {
            return new ParalivesMemoryChangedEvent
            {
                ChangeType = changeType,
                CharacterGuid = character == null ? 0UL : character.GUID,
                MemoryLogType = memory == null ? default(MemoryLogType) : memory.MemoryLogType,
                Data = memory == null ? null : memory.Data,
                StartTime = memory == null ? 0f : memory.StartTime,
                EndTime = memory == null ? 0f : memory.EndTime,
                WasCancelled = memory != null && memory.WasCancelled
            };
        }

        private static ParalivesMemorySnapshot CreateSnapshot(
            ulong characterGuid,
            int index,
            global::AssetCharacterMemoryLogSaveData data)
        {
            return new ParalivesMemorySnapshot
            {
                CharacterGuid = characterGuid,
                Index = index,
                MemoryLogType = data.MemoryLogType,
                Data = data.Data,
                StartTime = data.StartTime,
                EndTime = data.EndTime,
                FlagTime = data.FlagTime,
                WasCancelled = data.WasCancelled
            };
        }
    }
}
