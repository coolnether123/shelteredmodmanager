using System;
using System.Globalization;
using System.IO;
using ModAPI.Util;

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
                    data = DeserializeSeedData(json);
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

                string json = SerializeSeedData(data);
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

        private static string SerializeSeedData(SeedData data)
        {
            ManualJsonObject root = new ManualJsonObject();
            root.Set("version", ManualJsonValue.Number(data.version));
            root.Set("masterSeed", ManualJsonValue.Number(data.masterSeed));
            root.Set("mode", ManualJsonValue.Number(data.mode));
            root.Set("masterState", ManualJsonValue.String(FormatUInt64(data.masterState)));
            root.Set("stepCount", ManualJsonValue.String(FormatUInt64(data.stepCount)));
            root.Set("isDeterministic", ManualJsonValue.Boolean(data.isDeterministic));
            root.Set("streamNames", ManualJsonValue.Array(SerializeStrings(data.streamNames)));
            root.Set("streamStates", ManualJsonValue.Array(SerializeUInt64Array(data.streamStates)));
            root.Set("streamSteps", ManualJsonValue.Array(SerializeUInt64Array(data.streamSteps)));
            return ManualJson.Serialize(root, true);
        }

        private static SeedData DeserializeSeedData(string json)
        {
            ManualJsonObject root;
            string error;
            if (!ManualJson.TryParseObject(json, out root, out error))
                throw new FormatException(error ?? "seed.json root was not an object.");

            SeedData data = new SeedData();
            data.version = root.GetInt("version", data.version);
            data.masterSeed = root.GetInt("masterSeed", data.masterSeed);
            data.mode = root.GetInt("mode", data.mode);
            data.masterState = ReadUInt64(root.Get("masterState"), data.masterState);
            data.stepCount = ReadUInt64(root.Get("stepCount"), data.stepCount);
            data.isDeterministic = root.GetBool("isDeterministic", data.isDeterministic);
            data.streamNames = ReadStringArray(root.GetArray("streamNames"));
            data.streamStates = ReadUInt64Array(root.GetArray("streamStates"));
            data.streamSteps = ReadUInt64Array(root.GetArray("streamSteps"));
            return data;
        }

        private static ManualJsonArray SerializeStrings(string[] values)
        {
            ManualJsonArray array = new ManualJsonArray();
            if (values == null)
                return array;

            for (int i = 0; i < values.Length; i++)
                array.Add(ManualJsonValue.String(values[i]));
            return array;
        }

        private static ManualJsonArray SerializeUInt64Array(ulong[] values)
        {
            ManualJsonArray array = new ManualJsonArray();
            if (values == null)
                return array;

            for (int i = 0; i < values.Length; i++)
                array.Add(ManualJsonValue.String(FormatUInt64(values[i])));
            return array;
        }

        private static string[] ReadStringArray(ManualJsonArray array)
        {
            if (array == null)
                return null;

            string[] values = new string[array.Items.Count];
            for (int i = 0; i < array.Items.Count; i++)
            {
                ManualJsonValue value = array.Items[i];
                values[i] = value != null && value.Type == ManualJsonValueType.String ? value.StringValue : string.Empty;
            }
            return values;
        }

        private static ulong[] ReadUInt64Array(ManualJsonArray array)
        {
            if (array == null)
                return null;

            ulong[] values = new ulong[array.Items.Count];
            for (int i = 0; i < array.Items.Count; i++)
                values[i] = ReadUInt64(array.Items[i], 0ul);
            return values;
        }

        private static ulong ReadUInt64(ManualJsonValue value, ulong fallback)
        {
            if (value == null)
                return fallback;

            string text = null;
            if (value.Type == ManualJsonValueType.String)
                text = value.StringValue;
            else if (value.Type == ManualJsonValueType.Number)
                text = value.NumberText;

            ulong parsed;
            return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static string FormatUInt64(ulong value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
