using System;

namespace ModAPI.Core
{
    /// <summary>
    /// Opaque save context supplied by the active game runtime.
    /// ModAPI uses only the neutral path/index fields; host-specific descriptors stay opaque.
    /// </summary>
    public interface IModSaveContext
    {
        string SlotPath { get; }
        int SlotIndex { get; }
        string SaveScopeId { get; }
        string SaveId { get; }
        object HostSaveDescriptor { get; }
    }

    public sealed class ModSaveContext : IModSaveContext
    {
        public ModSaveContext(string slotPath, int slotIndex, string saveScopeId, string saveId, object hostSaveDescriptor)
        {
            SlotPath = slotPath;
            SlotIndex = slotIndex;
            SaveScopeId = saveScopeId;
            SaveId = saveId;
            HostSaveDescriptor = hostSaveDescriptor;
        }

        public string SlotPath { get; private set; }
        public int SlotIndex { get; private set; }
        public string SaveScopeId { get; private set; }
        public string SaveId { get; private set; }
        public object HostSaveDescriptor { get; private set; }
    }

    /// <summary>
    /// Neutral port for save-runtime questions that ModAPI framework services need to ask.
    /// Concrete game assemblies own slot routing, host descriptors, and manager integration.
    /// </summary>
    public interface ISaveRuntimeAdapter
    {
        string GetCurrentSlotPath();
        int ActiveSlotIndex { get; }
        IModSaveContext GetCurrentSaveContext();
        void EnsureRuntimeReady();
        void ResetRuntimeState();
        string GetQuitHeartbeatDetail();
    }

    internal static class SaveRuntimeAdapters
    {
        private static readonly ISaveRuntimeAdapter NullAdapter = new NullSaveRuntimeAdapter();

        internal static string GetCurrentSlotPath()
        {
            try { return Current.GetCurrentSlotPath(); }
            catch (Exception ex)
            {
                MMLog.WarnOnce("SaveRuntimeAdapters.GetCurrentSlotPath", "Save runtime adapter failed: " + ex.Message);
                return null;
            }
        }

        internal static int GetActiveSlotIndex()
        {
            try { return Current.ActiveSlotIndex; }
            catch (Exception ex)
            {
                MMLog.WarnOnce("SaveRuntimeAdapters.GetActiveSlotIndex", "Save runtime adapter failed: " + ex.Message);
                return -1;
            }
        }

        internal static IModSaveContext GetCurrentSaveContext()
        {
            try { return Current.GetCurrentSaveContext(); }
            catch (Exception ex)
            {
                MMLog.WarnOnce("SaveRuntimeAdapters.GetCurrentSaveContext", "Save runtime adapter failed: " + ex.Message);
                return null;
            }
        }

        internal static void EnsureRuntimeReady()
        {
            try { Current.EnsureRuntimeReady(); }
            catch (Exception ex)
            {
                MMLog.WarnOnce("SaveRuntimeAdapters.EnsureRuntimeReady", "Save runtime adapter failed: " + ex.Message);
            }
        }

        internal static void ResetRuntimeState()
        {
            try { Current.ResetRuntimeState(); }
            catch (Exception ex)
            {
                MMLog.WarnOnce("SaveRuntimeAdapters.ResetRuntimeState", "Save runtime adapter failed: " + ex.Message);
            }
        }

        internal static string GetQuitHeartbeatDetail()
        {
            try { return Current.GetQuitHeartbeatDetail(); }
            catch (Exception ex)
            {
                return "save runtime heartbeat failed: " + ex.Message;
            }
        }

        private static ISaveRuntimeAdapter Current
        {
            get
            {
                if (!ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.SaveRuntime))
                    return NullAdapter;

                ISaveRuntimeAdapter adapter = ModAPIRegistry.GetAPI<ISaveRuntimeAdapter>(GameRuntimeApiIds.SaveRuntime);
                return adapter ?? NullAdapter;
            }
        }

        private sealed class NullSaveRuntimeAdapter : ISaveRuntimeAdapter
        {
            public string GetCurrentSlotPath() { return null; }
            public int ActiveSlotIndex { get { return -1; } }
            public IModSaveContext GetCurrentSaveContext() { return null; }
            public void EnsureRuntimeReady() { }
            public void ResetRuntimeState() { }
            public string GetQuitHeartbeatDetail() { return "save runtime unavailable"; }
        }
    }
}
