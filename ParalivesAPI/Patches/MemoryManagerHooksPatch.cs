using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;
using Setting;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.Characters,
        "Paralives Memory Hooks",
        TargetBehavior = "Publishes memory write, cancel, and brain-logic execution events through ParalivesAPI.Memories.",
        FailureMode = "Mods can still read/write memories, but cannot react predictably to native memory lifecycle changes.",
        RollbackStrategy = "Unsubscribe memory hooks or disable mods depending on memory lifecycle callbacks.",
        IsOptional = true)]
    internal static class MemoryManagerHooksPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::MemoryManager), "WriteMemory")]
        private static void WriteMemoryPrefix(
            global::AssetCharacter character,
            MemoryLogType memoryType,
            global::MemoryData memoryData,
            out MemoryState __state)
        {
            __state = Capture(character, memoryType, memoryData);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::MemoryManager), "WriteMemory")]
        private static void WriteMemoryPostfix(
            global::AssetCharacter character,
            MemoryLogType memoryType,
            global::MemoryData memoryData,
            MemoryState __state)
        {
            global::AssetCharacterMemoryLogSaveData memory = FindLatestMatchingMemory(character, memoryType, memoryData);
            if (memory == null || !HasChanged(__state, memory))
                return;

            ParalivesRuntimeInfo.Current.Memories.PublishChanged(
                ParalivesRuntimeInfo.Current.Memories.CreateEvent(
                    ParalivesMemoryChangeType.Written,
                    character,
                    memory));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::MemoryManager), "SetMemoryAsCancelled")]
        private static void SetMemoryAsCancelledPrefix(
            global::AssetCharacter character,
            MemoryLogType memoryType,
            global::MemoryData memoryData,
            out MemoryState __state)
        {
            __state = Capture(character, memoryType, memoryData);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::MemoryManager), "SetMemoryAsCancelled")]
        private static void SetMemoryAsCancelledPostfix(
            global::AssetCharacter character,
            MemoryLogType memoryType,
            global::MemoryData memoryData,
            MemoryState __state)
        {
            global::AssetCharacterMemoryLogSaveData memory = FindLatestMatchingMemory(character, memoryType, memoryData);
            if (memory == null || !memory.WasCancelled || (__state.HadMemory && __state.WasCancelled))
                return;

            ParalivesRuntimeInfo.Current.Memories.PublishChanged(
                ParalivesRuntimeInfo.Current.Memories.CreateEvent(
                    ParalivesMemoryChangeType.Cancelled,
                    character,
                    memory));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::MemoryManager), "ExecuteBrainLogic")]
        private static void ExecuteBrainLogicPostfix(
            global::AssetCharacter character,
            global::AssetCharacterMemoryLogSaveData memory,
            MemoryLogTrigger trigger,
            bool inHousehold)
        {
            ParalivesMemoryChangedEvent evt = ParalivesRuntimeInfo.Current.Memories.CreateEvent(
                ParalivesMemoryChangeType.BrainLogicExecuted,
                character,
                memory);
            evt.MemoryLogTrigger = trigger;
            evt.InHousehold = inHousehold;
            ParalivesRuntimeInfo.Current.Memories.PublishChanged(evt);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::MemoryManager), "ExecuteBrainLogicAction")]
        private static void ExecuteBrainLogicActionPostfix(
            global::AssetCharacter character,
            global::AssetCharacterMemoryLogSaveData memory,
            MemoryLogTriggerWithCancelAndComplete trigger,
            bool inHousehold)
        {
            ParalivesMemoryChangedEvent evt = ParalivesRuntimeInfo.Current.Memories.CreateEvent(
                ParalivesMemoryChangeType.BrainLogicActionExecuted,
                character,
                memory);
            evt.MemoryLogActionTrigger = trigger;
            evt.InHousehold = inHousehold;
            ParalivesRuntimeInfo.Current.Memories.PublishChanged(evt);
        }

        private static MemoryState Capture(
            global::AssetCharacter character,
            MemoryLogType memoryType,
            global::MemoryData memoryData)
        {
            global::AssetCharacterMemoryLogSaveData memory = FindLatestMatchingMemory(character, memoryType, memoryData);
            int count = character != null
                && character.Data != null
                && character.Data.MemoryLogSaveData != null
                    ? character.Data.MemoryLogSaveData.Count
                    : 0;

            return new MemoryState
            {
                Count = count,
                HadMemory = memory != null,
                StartTime = memory == null ? 0f : memory.StartTime,
                EndTime = memory == null ? 0f : memory.EndTime,
                FlagTime = memory == null ? 0f : memory.FlagTime,
                WasCancelled = memory != null && memory.WasCancelled
            };
        }

        private static bool HasChanged(MemoryState state, global::AssetCharacterMemoryLogSaveData memory)
        {
            if (memory == null)
                return false;
            if (!state.HadMemory)
                return true;
            if (state.Count == 0)
                return true;

            return state.StartTime != memory.StartTime
                || state.EndTime != memory.EndTime
                || state.FlagTime != memory.FlagTime
                || state.WasCancelled != memory.WasCancelled;
        }

        private static global::AssetCharacterMemoryLogSaveData FindLatestMatchingMemory(
            global::AssetCharacter character,
            MemoryLogType memoryType,
            global::MemoryData memoryData)
        {
            if (character == null || character.Data == null || character.Data.MemoryLogSaveData == null)
                return null;

            for (int i = character.Data.MemoryLogSaveData.Count - 1; i >= 0; i--)
            {
                global::AssetCharacterMemoryLogSaveData memory = character.Data.MemoryLogSaveData[i];
                if (memory == null || memory.MemoryLogType != memoryType)
                    continue;

                if (IsSameMemory(memory, memoryData))
                    return memory;
            }

            return null;
        }

        private static bool IsSameMemory(global::AssetCharacterMemoryLogSaveData memory, global::MemoryData memoryData)
        {
            try
            {
                return global::MemoryManager.Instance
                    .GetMemoryLogLogic(memory.MemoryLogType)
                    .IsSameMemory(memory.Data, memoryData);
            }
            catch
            {
                return ReferenceEquals(memory.Data, memoryData);
            }
        }

        private struct MemoryState
        {
            public int Count;

            public bool HadMemory;

            public float StartTime;

            public float EndTime;

            public float FlagTime;

            public bool WasCancelled;
        }
    }
}
