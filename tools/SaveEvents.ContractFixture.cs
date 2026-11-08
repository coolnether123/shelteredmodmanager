namespace ModAPI.Core {
    internal class ModEntry { public string Id = "fixture"; }
    internal static class ModRegistry { internal static bool TryGetModByAssembly(Assembly assembly, out ModEntry entry) { entry = null; return false; } }
    internal static class MMLog {
        internal static void WarnOnce(string key, string message) { }
        internal static void WriteError(string message) { }
    }
}
namespace ShelteredAPI.Saves {
    public class SaveEntry { public int absoluteSlot = 4; public string name; public string updatedAt; public long fileSize; public uint crc32; public object saveInfo; }
    public class SlotReservation { }
    public class SaveOverwriteOptions { public string name; }
    public delegate void SaveEvent(SaveEntry entry);
    public delegate void LoadEvent(SaveEntry entry);
    public delegate void PageChangedEvent(int page);
    public delegate void ReservationChangedEvent(int slot, SlotReservation reservation);
    internal static class NameSanitizer { internal static string SanitizeName(string name) { return name; } }
    internal static class SaveBackupService { internal static bool BackupCustomEntryBeforeOverwrite(SaveEntry entry) { return true; } }
    public sealed partial class SaveEventsContractFixture {
        public static int Count;
        private bool writeSuccess = true;
        private SaveEntry GetSave(string id) { return new SaveEntry(); }
        private bool TryCreateTransientEntryFromId(string id, SaveOverwriteOptions options, out SaveEntry entry) { entry = null; return false; }
        private bool TryWriteEntryFile(int slot, byte[] bytes, out long size, out uint crc) { size = bytes.Length; crc = 1; return writeSuccess; }
        private static void TryUpdateEntryInfo(SaveEntry entry, byte[] bytes) { }
        private void UpdateSlotManifest(int slot, object info) { }
        private void InvalidateCache() { }
        private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
        public static void Verify() {
            int notifications = 0;
            SaveEvent after = delegate { notifications++; };
            Events.OnAfterSave += after;
            var owner = new SaveEventsContractFixture();
            owner.OverwriteSave("save", null, new byte[1]);
            Check(notifications == 1, "A successful XML write must publish AfterSave once");
            owner.writeSuccess = false;
            owner.OverwriteSave("save", null, new byte[1]);
            Check(notifications == 1, "A failed XML write must not publish AfterSave");
            owner.OverwriteSave("save", null, null);
            Check(notifications == 1, "A metadata-only call must not publish an XML save event");
            Events.OnAfterSave -= after;
            Action fail = delegate { throw new InvalidOperationException("subscriber failure"); };
            int delivered = 0;
            Events.OnBeforeSave += delegate { fail(); };
            Events.OnBeforeSave += delegate { delivered++; };
            Events.OnAfterSave += delegate { fail(); };
            Events.OnAfterSave += delegate { delivered++; };
            Events.OnBeforeLoad += delegate { fail(); };
            Events.OnBeforeLoad += delegate { delivered++; };
            Events.OnAfterLoad += delegate { fail(); };
            Events.OnAfterLoad += delegate { delivered++; };
            Events.OnPageChanged += delegate { fail(); };
            Events.OnPageChanged += delegate { delivered++; };
            Events.OnReservationChanged += delegate { fail(); };
            Events.OnReservationChanged += delegate { delivered++; };
            Events.RaiseBeforeSave(new SaveEntry()); Check(delivered == 1, "BeforeSave must isolate subscribers");
            Events.RaiseAfterSave(new SaveEntry()); Check(delivered == 2, "AfterSave must isolate subscribers");
            Events.RaiseBeforeLoad(new SaveEntry()); Check(delivered == 3, "BeforeLoad must isolate subscribers");
            Events.RaiseAfterLoad(new SaveEntry()); Check(delivered == 4, "AfterLoad must isolate subscribers");
            Events.RaisePageChanged(1); Check(delivered == 5, "PageChanged must isolate subscribers");
            Events.RaiseReservationChanged(1, new SlotReservation()); Check(delivered == 6, "ReservationChanged must isolate subscribers");
        }
    }
}
