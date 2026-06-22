using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Saves
{
    /// <summary>
    /// Vanilla save summary copied into custom save metadata.
    /// Fields are public for Unity JSON serialization.
    /// </summary>
    [Serializable]
    public class SaveInfo
    {
        public int daysSurvived;
        public int difficulty; // 0-4 mirrors game diff setting
        public bool fog;
        public int mapSize;
        public bool hasMapSizeMetadata;
        public int rainDiff = 1;
        public int resourceDiff = 1;
        public int breachDiff = 1;
        public int factionDiff = 1;
        public int moodDiff = 1;
        public string familyName = string.Empty;
        public string saveTime = string.Empty; 
    }

    /// <summary>
    /// Catalog entry for one custom save slot.
    /// This is the main read model shown by paging, verification, and load UI.
    /// </summary>
    [Serializable]
    public class SaveEntry
    {
        public string id;                    // GUID string
        public int absoluteSlot;             // 1-based slot number (1, 2, 3, 4, ...)
        public string name;                  // display name
        public string createdAt;             // ISO-8601
        public string updatedAt;             // ISO-8601
        public string gameVersion;           // game build version string
        public string modApiVersion;         // ModAPI version string
        public string scenarioId;            // owning scenario
        public string scenarioVersion;       // scenario version
        public long fileSize;                // bytes
        public uint crc32;                   // checksum of xml bytes
        public string previewPath;           // relative path to preview png
        public string extra;                 // optional freeform JSON (flat)
        public SaveInfo saveInfo = new SaveInfo();
    }

    /// <summary>
    /// Legacy global manifest shape kept only for reading old saves.
    /// New code should use directory-based slot discovery and <see cref="SlotManifest"/>.
    /// </summary>
    [Serializable]
    [Obsolete("No longer used. Save discovery is now directory-based.")]
    public class SaveManifest
    {
        public int version = 1;
        public SaveEntry[] entries = new SaveEntry[0];
    }

    /// <summary>
    /// Declares whether a physical slot belongs to normal saves or a custom scenario run.
    /// </summary>
    public enum SaveSlotUsage
    {
        Standard = 0,
        CustomScenario = 1
    }

    /// <summary>
    /// Reservation for one of the vanilla physical save slots.
    /// Custom scenarios use reservations to avoid colliding with standard saves.
    /// </summary>
    [Serializable]
    public class SlotReservation
    {
        public int physicalSlot;                  // 1..3
        public SaveSlotUsage usage;
        public string scenarioId;                 // null when Standard
    }

    /// <summary>
    /// Persisted map of physical slot reservations.
    /// </summary>
    [Serializable]
    public class SlotReservationMap
    {
        public int version = 1;
        public SlotReservation[] reserved = new SlotReservation[0];
    }

    /// <summary>
    /// Lightweight scenario metadata stored alongside scenario-owned saves.
    /// </summary>
    [Serializable]
    public class ScenarioDescriptor
    {
        public string id;
        public string displayName;
        public string description;
        public string version;
    }

    /// <summary>
    /// Options used when creating a new custom save entry.
    /// </summary>
    public class SaveCreateOptions
    {
        public string name;
        public string extraJson;
        public int absoluteSlot;
    }

    /// <summary>
    /// Options used when overwriting an existing custom save entry.
    /// </summary>
    public class SaveOverwriteOptions
    {
        public string name;
        public string extraJson;
    }

    /// <summary>
    /// Options used when loading a custom save.
    /// </summary>
    public class LoadOptions
    {
        public bool showLoadingScreen = true;
    }

    /// <summary>
    /// Options used when starting a new custom scenario run.
    /// </summary>
    public class StartOptions
    {
        public string name;
    }

    /// <summary>Raised for save-entry lifecycle notifications.</summary>
    public delegate void SaveEvent(SaveEntry entry);
    /// <summary>Raised for load-entry lifecycle notifications.</summary>
    public delegate void LoadEvent(SaveEntry entry);
    /// <summary>Raised when the custom save browser changes page.</summary>
    public delegate void PageChangedEvent(int page);
    /// <summary>Raised when a physical slot reservation changes.</summary>
    public delegate void ReservationChangedEvent(int physicalSlot, SlotReservation reservation);

    /// <summary>
    /// Optional participant hook for mods that need to write or restore data beside a custom save.
    /// </summary>
    public interface ICustomSaveParticipant
    {
        void OnSave(SaveData data, SaveEntry entry);
        void OnLoad(SaveData data, SaveEntry entry);
    }

    /// <summary>
    /// Optional provider for extra display metadata on the current custom save run.
    /// </summary>
    public interface ICustomSaveMetaProvider
    {
        CustomMeta GetMetaForCurrentRun();
    }

    /// <summary>
    /// Custom save display metadata supplied by mods.
    /// </summary>
    [Serializable]
    public class CustomMeta
    {
        public string highScoreLine; // optional extra line
    }

    /// <summary>
    /// Optional hooks for scenario-specific save and selection flow.
    /// </summary>
    public interface ICustomScenarioHooks
    {
        void OnChosen();
        void OnSpawned();
        void OnContinue();
        void OnAbort();
    }

    /// <summary>
    /// Mod compatibility record captured when a save was last loaded.
    /// </summary>
    [Serializable]
    public class LoadedModInfo
    {
        public string modId;
        public string version;
        public string requiredModApiVersion;
        public string requiredShelteredApiVersion;
        public string[] warnings = new string[0];
    }

    /// <summary>
    /// Per-slot manifest stored inside a self-contained save directory.
    /// </summary>
    [Serializable]
    public class SlotManifest
    {
        public int manifestVersion = 1;
        public string lastModified;
        public string family_name;
        public string saveScopeId;
        public string saveId;
        public string customScenarioId;
        public string source;
        public int sourceSlot;
        public uint sourceVanillaCrc32;
        public string sourceVanillaLastWriteUtc;
        public string modApiVersion;
        public string shelteredApiVersion;
        public string mapFactsStatus = "unknown";
        public bool hasMapSize;
        public int mapSize;
        public string runtimeMapFactsStatus = "unavailable";
        public int runtimeMapWidth;
        public int runtimeMapHeight;
        public string runtimeMapScaleFactor;
        public bool hasMapSeed;
        public int mapSeed;
        public string queueFactsStatus = "unavailable";
        public string queueSummary;
        public string restoreFactsStatus = "unknown";
        public string restoreLineageId;
        public LoadedModInfo[] lastLoadedMods = new LoadedModInfo[0];
    }
}

