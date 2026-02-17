using System;
using System.IO;
using ModAPI.Saves;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Handles the persistent storage of RNG state in 'seed.json'.
    /// </summary>
    internal static class ModRandomState
    {
        [Serializable]
        private class SeedData
        {
            public int version = 2;
            public int masterSeed;
            public int mode;
            public ulong masterState;
            public ulong stepCount;
            public bool isDeterministic;
            public string[] streamNames;
            public ulong[] streamStates;
            public ulong[] streamSteps;
        }

        public static void Load(SaveEntry entry)
        {
            if (entry == null) return;

            string filePath = GetSeedFilePath(entry);
            SeedData data = null;

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    data = JsonUtility.FromJson<SeedData>(json);
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("[ModRandom] Failed to read seed.json: " + ex.Message);
                }
            }

            if (data != null)
            {
                // Apply the saved deterministic flag
                ModRandom.IsDeterministic = data.isDeterministic;

                if (ModRandom.IsDeterministic)
                {
                    // Deterministic Mode: restore exact RNG state and stream states.
                    ModRandomStateSnapshot snapshot = new ModRandomStateSnapshot();
                    snapshot.MasterSeed = data.masterSeed;
                    snapshot.Mode = data.mode == (int)RandomnessMode.Legacy ? RandomnessMode.Legacy : RandomnessMode.XorShift;
                    snapshot.MasterState = data.masterState;
                    snapshot.StepCount = data.stepCount;
                    snapshot.StreamNames = data.streamNames;
                    snapshot.StreamStates = data.streamStates;
                    snapshot.StreamSteps = data.streamSteps;
                    ModRandom.RestoreSnapshot(snapshot);
                    MMLog.WriteInfo(string.Format("[ModRandom] Session Restored (Deterministic): Seed {0}, Step {1}, Mode {2}", data.masterSeed, data.stepCount, snapshot.Mode));
                }
                else
                {
                    // Random Mode: Even if we have a file, if it says not deterministic, we generate a new seed
                    int newSeed = GenerateFreshSeed();
                    ModRandom.Initialize(newSeed);
                    MMLog.WriteInfo(string.Format("[ModRandom] Session Started (Randomized): New Seed {0}. (File existed but deterministic=false)", newSeed));
                }
            }
            else
            {
                // No seed.json: Default behavior (Randomized on load)
                int newSeed = GenerateFreshSeed();
                ModRandom.Initialize(newSeed);
                MMLog.WriteInfo(string.Format("[ModRandom] Session Started (Randomized): New Seed {0}. (No seed.json found)", newSeed));
            }

            // Notify mods that the seed is ready/changed
            ModRandom.NotifySeedChanged();
        }

        public static void Save(SaveEntry entry)
        {
            if (entry == null) return;

            try
            {
                string filePath = GetSeedFilePath(entry);
                ModRandomStateSnapshot snapshot = ModRandom.CreateSnapshot();
                var data = new SeedData();
                data.masterSeed = snapshot.MasterSeed;
                data.mode = (int)snapshot.Mode;
                data.masterState = snapshot.MasterState;
                data.stepCount = snapshot.StepCount;
                data.isDeterministic = ModRandom.IsDeterministic;
                data.streamNames = snapshot.StreamNames;
                data.streamStates = snapshot.StreamStates;
                data.streamSteps = snapshot.StreamSteps;

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(filePath, json);
                MMLog.WriteDebug(string.Format("[ModRandom] Saved seed.json to {0}. Deterministic={1}", entry.id, data.isDeterministic));
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModRandom] Failed to save seed.json: " + ex.Message);
            }
        }

        private static string GetSeedFilePath(SaveEntry entry)
        {
            // We use the same directory logic as SaveSystemImpl
            string scenario = string.IsNullOrEmpty(entry.scenarioId) ? "Standard" : entry.scenarioId;
            string slotDir = DirectoryProvider.SlotRoot(scenario, entry.absoluteSlot, false);
            
            if (!Directory.Exists(slotDir)) Directory.CreateDirectory(slotDir);
            
            return Path.Combine(slotDir, "seed.json");
        }

        private static int GenerateFreshSeed()
        {
            return Environment.TickCount ^ Guid.NewGuid().GetHashCode();
        }
    }
}
