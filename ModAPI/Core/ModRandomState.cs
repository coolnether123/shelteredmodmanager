using System;
using System.IO;
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

        public static void Load(object payload)
        {
            string filePath = GetSeedFilePath();
            if (string.IsNullOrEmpty(filePath)) return;
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
                    // Per-save stable seed mode: reuse the save's master seed on every load.
                    // This avoids re-rolling RNG identity between loads while still not restoring step history.
                    RandomnessMode mode = data.mode == (int)RandomnessMode.Legacy ? RandomnessMode.Legacy : RandomnessMode.XorShift;
                    ModRandom.ResetForSaveSeed(data.masterSeed, mode);
                    MMLog.WriteInfo(string.Format("[ModRandom] Session Started (Seed Reused): Seed {0}, Mode {1}.", data.masterSeed, mode));
                }
            }
            else
            {
                // No seed.json: Default behavior (Randomized on load)
                int newSeed = GenerateFreshSeed();
                ModRandom.ResetForSaveSeed(newSeed);
                MMLog.WriteInfo(string.Format("[ModRandom] Session Started (Randomized): New Seed {0}. (No seed.json found)", newSeed));
            }

            if (data != null && ModRandom.IsDeterministic)
            {
                // Exact-state restores do not reset; listeners must rebind to restored named streams.
                ModRandom.NotifySeedChanged();
            }
        }

        public static void Save(object payload)
        {
            try
            {
                string filePath = GetSeedFilePath();
                if (string.IsNullOrEmpty(filePath)) return;
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
                MMLog.WriteDebug(string.Format("[ModRandom] Saved seed.json. Deterministic={0}", data.isDeterministic));
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModRandom] Failed to save seed.json: " + ex.Message);
            }
        }

        private static string GetSeedFilePath()
        {
            string slotDir = SaveRuntimeAdapters.GetCurrentSlotPath();
            if (string.IsNullOrEmpty(slotDir)) return null;
            
            if (!Directory.Exists(slotDir)) Directory.CreateDirectory(slotDir);
            
            return Path.Combine(slotDir, "seed.json");
        }

        private static int GenerateFreshSeed()
        {
            return Environment.TickCount ^ Guid.NewGuid().GetHashCode();
        }
    }
}
