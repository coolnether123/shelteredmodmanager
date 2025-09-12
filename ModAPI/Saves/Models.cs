using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Saves
{
    [Serializable]
    public class SaveInfo
    {
        public int daysSurvived;
        public int difficulty; // 0-4 mirrors game diff setting
        public bool fog;
        public int mapSize;
        public int rainDiff = 1;
        public int resourceDiff = 1;
        public int breachDiff = 1;
        public int factionDiff = 1;
        public int moodDiff = 1;
        public string familyName = string.Empty;
        public string saveTime = string.Empty; // human-readable (for labels)
    }

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

    [Serializable]
    public class SaveManifest
    {
        public int version = 1;
        public SaveEntry[] entries = new SaveEntry[0];
    }

    public enum SaveSlotUsage
    {
        Standard = 0,
        CustomScenario = 1
    }

    [Serializable]
    public class SlotReservation
    {
        public int physicalSlot;                  // 1..3
        public SaveSlotUsage usage;
        public string scenarioId;                 // null when Standard
    }

    [Serializable]
    public class SlotReservationMap
    {
        public int version = 1;
        public SlotReservation[] reserved = new SlotReservation[0];
    }

    [Serializable]
    public class ScenarioDescriptor
    {
        public string id;
        public string displayName;
        public string description;
        public string version;
    }

    // Options
    public class SaveCreateOptions
    {
        public string name;
        public string extraJson;
        public int absoluteSlot;
    }

    public class SaveOverwriteOptions
    {
        public string name;
        public string extraJson;
    }

    public class LoadOptions
    {
        public bool showLoadingScreen = true;
    }

    public class StartOptions
    {
        public string name;
    }

    // Events
    public delegate void SaveEvent(SaveEntry entry);
    public delegate void LoadEvent(SaveEntry entry);
    public delegate void PageChangedEvent(int page);
    public delegate void ReservationChangedEvent(int physicalSlot, SlotReservation reservation);

    // Interfaces for mod authors
    public interface ICustomSaveParticipant
    {
        void OnSave(SaveData data, SaveEntry entry);
        void OnLoad(SaveData data, SaveEntry entry);
    }

    public interface ICustomSaveMetaProvider
    {
        CustomMeta GetMetaForCurrentRun();
    }

    [Serializable]
    public class CustomMeta
    {
        public string highScoreLine; // optional extra line
    }

    public interface ICustomScenarioHooks
    {
        void OnChosen();
        void OnSpawned();
        void OnContinue();
        void OnAbort();
    }
}

