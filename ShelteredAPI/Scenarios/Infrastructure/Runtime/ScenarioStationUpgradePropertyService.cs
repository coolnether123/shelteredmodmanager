using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Infrastructure;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal static class ScenarioStationUpgradePropertyService
    {
        public const string UpgradePropertyPrefix = "upgrade.";
        public const string StatPropertyPrefix = "stat.";
        public const int ObjectLevelMin = 1;
        public const int ObjectLevelMax = 5;

        private const string StatFuelCapacity = "fuelCapacity";
        private const string StatPowerOutput = "powerOutput";
        private const string StatOutputRate = "outputRate";
        private const string StatWaterCapacity = "waterCapacity";
        private const string StatWaterGeneration = "waterGeneration";
        private const string StatOxygenMultiplier = "oxygenMultiplier";

        private static readonly FieldInfo ObjectLevelField = FindField(typeof(Obj_Base), "m_objectLevel");
        private static readonly FieldInfo GeneratorFuelField = FindField(typeof(Obj_Generator), "m_Fuel");
        private static readonly FieldInfo GeneratorFuelCapacityField = FindField(typeof(Obj_Generator), "m_FuelCapacity");
        private static readonly FieldInfo GeneratorFuelCapacityPerLevelField = FindField(typeof(Obj_Generator), "m_FuelCapacityPerLevel");
        private static readonly FieldInfo GeneratorPowerOutputField = FindField(typeof(Obj_Generator), "m_PowerOutput");
        private static readonly FieldInfo GeneratorPowerOutputPerLevelField = FindField(typeof(Obj_Generator), "m_PowerOutputPerLevel");
        private static readonly FieldInfo GeneratorOutputRateField = FindField(typeof(Obj_Generator), "m_OutputRate");
        private static readonly FieldInfo WaterTankCapacityField = FindField(typeof(Obj_WaterTank), "m_Capacity");
        private static readonly FieldInfo WaterTankGenerationField = FindField(typeof(Obj_WaterTank), "m_WaterGeneration");
        private static readonly FieldInfo WaterTankNextGenerationField = FindField(typeof(Obj_WaterTank), "m_NextWaterGenerationTime");
        private static readonly FieldInfo OxygenMultiplierField = FindField(typeof(Obj_OxygenFilter), "m_OxygenInMult");
        private static readonly FieldInfo OxygenMultiplierPerLevelField = FindField(typeof(Obj_OxygenFilter), "m_OxygenInMultPerLevel");

        public static bool IsStationObject(Obj_Base obj)
        {
            return obj is Obj_Generator
                || obj is Obj_OxygenFilter
                || obj is Obj_WaterFilter
                || obj is Obj_WaterTank
                || obj is Obj_Radio;
        }

        public static bool HasStationProperties(ObjectPlacement placement)
        {
            if (placement == null || placement.CustomProperties == null)
                return false;

            for (int i = 0; i < placement.CustomProperties.Count; i++)
            {
                ScenarioProperty property = placement.CustomProperties[i];
                if (property == null || string.IsNullOrEmpty(property.Key))
                    continue;

                if (string.Equals(property.Key, ScenarioPlacementDefinitions.PropertyLevel, StringComparison.OrdinalIgnoreCase)
                    || property.Key.StartsWith(UpgradePropertyPrefix, StringComparison.OrdinalIgnoreCase)
                    || property.Key.StartsWith(StatPropertyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Capture(Obj_Base obj, ObjectPlacement placement)
        {
            if (obj == null || placement == null || !IsStationObject(obj))
                return;

            ScenarioPropertyBag.Set(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyLevel, ClampInt(obj.objectLevel, ObjectLevelMin, ObjectLevelMax).ToString(CultureInfo.InvariantCulture));
            CaptureUpgradePaths(obj, placement);
            CaptureSafeStats(obj, placement);
        }

        public static void Apply(Obj_Base obj, ObjectPlacement placement, ScenarioApplyResult result)
        {
            string message;
            if (!SeamGuard.Run(
                "scenario.station-upgrade.apply",
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate { ApplyCore(obj, placement, result); },
                "Station upgrade projection unavailable - scenario still playable.",
                null,
                out message))
            {
                AddMessage(result, message);
            }
        }

        private static void ApplyCore(Obj_Base obj, ObjectPlacement placement, ScenarioApplyResult result)
        {
            if (obj == null || placement == null || !IsStationObject(obj) || !HasStationProperties(placement))
                return;

            int authoredLevel;
            if (ScenarioPropertyBag.TryGetInt(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyLevel, out authoredLevel))
                SetObjectLevel(obj, ClampInt(authoredLevel, ObjectLevelMin, ObjectLevelMax));

            ApplyUpgradePaths(obj, placement, result);
            ApplySafeStats(obj, placement, result);
        }

        public static ScenarioStationUpgradeSnapshot BuildSnapshot(Obj_Base obj, ObjectPlacement placement)
        {
            if (obj == null || !IsStationObject(obj))
                return null;

            ScenarioStationUpgradeSnapshot snapshot = new ScenarioStationUpgradeSnapshot();
            snapshot.ObjectType = obj.GetObjectType().ToString();
            snapshot.Level = ScenarioPropertyBag.GetInt(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyLevel, ClampInt(obj.objectLevel, ObjectLevelMin, ObjectLevelMax));
            snapshot.Level = ClampInt(snapshot.Level, ObjectLevelMin, ObjectLevelMax);

            UpgradeObject upgrade = obj.GetComponent<UpgradeObject>();
            if (upgrade != null)
            {
                List<UpgradeObject.PathEnum> paths = upgrade.GetPaths();
                for (int i = 0; paths != null && i < paths.Count; i++)
                {
                    UpgradeObject.PathEnum path = paths[i];
                    int current = Math.Max(0, upgrade.GetUpgradeLevel(path));
                    int authored = ScenarioPropertyBag.GetInt(placement != null ? placement.CustomProperties : null, UpgradeKey(path), current);
                    int max = Math.Max(0, upgrade.GetMaxUpgradeLevel(path));
                    snapshot.Paths.Add(new ScenarioStationUpgradePathSnapshot
                    {
                        Name = path.ToString(),
                        Level = ClampInt(authored, 0, max),
                        CurrentLevel = current,
                        MaxLevel = max
                    });
                }
            }

            AddStatSnapshots(obj, placement, snapshot.Stats);
            return snapshot;
        }

        public static bool TrySetObjectLevel(Obj_Base obj, ObjectPlacement placement, int delta, out string message)
        {
            message = null;
            if (obj == null || placement == null || !IsStationObject(obj))
            {
                message = "Select a station object before editing station upgrades.";
                return false;
            }

            int current = ScenarioPropertyBag.GetInt(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyLevel, ClampInt(obj.objectLevel, ObjectLevelMin, ObjectLevelMax));
            int next = ClampInt(current + delta, ObjectLevelMin, ObjectLevelMax);
            if (next == current)
            {
                message = "Station level is already at the vanilla bound.";
                return false;
            }

            ScenarioPropertyBag.Set(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyLevel, next.ToString(CultureInfo.InvariantCulture));
            SetObjectLevel(obj, next);
            message = "Station level set to " + next.ToString(CultureInfo.InvariantCulture) + ".";
            return true;
        }

        public static bool TrySetUpgradeLevel(Obj_Base obj, ObjectPlacement placement, string pathName, int delta, out string message)
        {
            message = null;
            if (obj == null || placement == null || string.IsNullOrEmpty(pathName))
            {
                message = "Select a station upgrade path before editing it.";
                return false;
            }

            UpgradeObject.PathEnum path;
            if (!TryParsePath(pathName, out path))
            {
                message = "Unknown station upgrade path: " + pathName + ".";
                return false;
            }

            UpgradeObject upgrade = obj.GetComponent<UpgradeObject>();
            if (upgrade == null || !upgrade.HasPath(path))
            {
                message = "Selected station does not support the " + path.ToString() + " upgrade path.";
                return false;
            }

            int max = Math.Max(0, upgrade.GetMaxUpgradeLevel(path));
            int current = ScenarioPropertyBag.GetInt(placement.CustomProperties, UpgradeKey(path), Math.Max(0, upgrade.GetUpgradeLevel(path)));
            int next = ClampInt(current + delta, 0, max);
            if (next == current)
            {
                message = path.ToString() + " is already at the vanilla bound.";
                return false;
            }

            ScenarioPropertyBag.Set(placement.CustomProperties, UpgradeKey(path), next.ToString(CultureInfo.InvariantCulture));
            ApplyUpgradeLevel(obj, upgrade, path, next, null);
            ApplySafeStats(obj, placement, null);
            message = path.ToString() + " upgrade set to " + next.ToString(CultureInfo.InvariantCulture) + "/" + max.ToString(CultureInfo.InvariantCulture) + ".";
            return true;
        }

        public static bool TrySetStat(Obj_Base obj, ObjectPlacement placement, string statName, float delta, out string message)
        {
            message = null;
            if (obj == null || placement == null || string.IsNullOrEmpty(statName))
            {
                message = "Select a station stat before editing it.";
                return false;
            }

            ScenarioStationStatSnapshot stat;
            if (!TryBuildStatSnapshot(obj, placement, statName, out stat))
            {
                message = "Selected station does not expose a safe " + statName + " override.";
                return false;
            }

            float next = ClampFloat(stat.Value + delta, stat.MinValue, stat.MaxValue);
            if (Math.Abs(next - stat.Value) < 0.0001f)
            {
                message = stat.Label + " is already at the safe bound.";
                return false;
            }

            ScenarioPropertyBag.Set(placement.CustomProperties, StatKey(stat.Name), FormatFloat(next));
            ApplySafeStats(obj, placement, null);
            message = stat.Label + " override set to " + FormatFloat(next) + ".";
            return true;
        }

        public static bool TryClearStat(Obj_Base obj, ObjectPlacement placement, string statName, out string message)
        {
            message = null;
            if (placement == null || string.IsNullOrEmpty(statName))
            {
                message = "Select a station stat before clearing it.";
                return false;
            }

            string key = StatKey(statName);
            bool removed = RemoveProperty(placement.CustomProperties, key);
            if (!removed)
            {
                message = "No " + statName + " override was set.";
                return false;
            }

            if (obj != null)
                ApplySafeStats(obj, placement, null);
            message = statName + " override cleared.";
            return true;
        }

        public static string UpgradeKey(UpgradeObject.PathEnum path)
        {
            return UpgradePropertyPrefix + path.ToString();
        }

        public static string StatKey(string statName)
        {
            return StatPropertyPrefix + statName;
        }

        private static void CaptureUpgradePaths(Obj_Base obj, ObjectPlacement placement)
        {
            UpgradeObject upgrade = obj.GetComponent<UpgradeObject>();
            if (upgrade == null)
                return;

            List<UpgradeObject.PathEnum> paths = upgrade.GetPaths();
            for (int i = 0; paths != null && i < paths.Count; i++)
            {
                UpgradeObject.PathEnum path = paths[i];
                int level = Math.Max(0, upgrade.GetUpgradeLevel(path));
                ScenarioPropertyBag.Set(placement.CustomProperties, UpgradeKey(path), level.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void CaptureSafeStats(Obj_Base obj, ObjectPlacement placement)
        {
            List<ScenarioStationStatSnapshot> stats = new List<ScenarioStationStatSnapshot>();
            AddStatSnapshots(obj, placement, stats);
            for (int i = 0; i < stats.Count; i++)
            {
                ScenarioStationStatSnapshot stat = stats[i];
                if (stat != null)
                    ScenarioPropertyBag.Set(placement.CustomProperties, StatKey(stat.Name), FormatFloat(stat.Value));
            }
        }

        private static void ApplyUpgradePaths(Obj_Base obj, ObjectPlacement placement, ScenarioApplyResult result)
        {
            UpgradeObject upgrade = obj.GetComponent<UpgradeObject>();
            if (upgrade == null)
                return;

            for (int i = 0; placement.CustomProperties != null && i < placement.CustomProperties.Count; i++)
            {
                ScenarioProperty property = placement.CustomProperties[i];
                if (property == null || string.IsNullOrEmpty(property.Key) || !property.Key.StartsWith(UpgradePropertyPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string pathName = property.Key.Substring(UpgradePropertyPrefix.Length);
                UpgradeObject.PathEnum path;
                int level;
                if (!TryParsePath(pathName, out path) || !int.TryParse(property.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
                {
                    AddMessage(result, "Invalid station upgrade property skipped: " + property.Key + ".");
                    continue;
                }

                ApplyUpgradeLevel(obj, upgrade, path, level, result);
            }
        }

        private static void ApplyUpgradeLevel(Obj_Base obj, UpgradeObject upgrade, UpgradeObject.PathEnum path, int authoredLevel, ScenarioApplyResult result)
        {
            if (upgrade == null || !upgrade.HasPath(path))
                return;

            int max = Math.Max(0, upgrade.GetMaxUpgradeLevel(path));
            int targetLevel = ClampInt(authoredLevel, 0, max);
            int currentLevel = Math.Max(0, upgrade.GetUpgradeLevel(path));
            if (upgrade.IsLocked(path))
                upgrade.UnlockPath(path);

            if (targetLevel > currentLevel)
            {
                upgrade.Upgrade(path, targetLevel);
            }
            else if (targetLevel < currentLevel)
            {
                upgrade.Downgrade(path, currentLevel - targetLevel);
            }
            else
            {
                upgrade.Upgrade(path, targetLevel);
            }
        }

        private static void ApplySafeStats(Obj_Base obj, ObjectPlacement placement, ScenarioApplyResult result)
        {
            if (obj is Obj_Generator)
            {
                Obj_Generator generator = (Obj_Generator)obj;
                ApplyGeneratorStat(generator, placement, StatFuelCapacity, result);
                ApplyGeneratorStat(generator, placement, StatPowerOutput, result);
                ApplyGeneratorStat(generator, placement, StatOutputRate, result);
                return;
            }

            if (obj is Obj_WaterTank)
            {
                ApplyWaterTankStat((Obj_WaterTank)obj, placement, StatWaterCapacity, result);
                ApplyWaterTankStat((Obj_WaterTank)obj, placement, StatWaterGeneration, result);
                return;
            }

            if (obj is Obj_OxygenFilter)
            {
                ApplyOxygenFilterStat((Obj_OxygenFilter)obj, placement, StatOxygenMultiplier, result);
            }
        }

        private static void ApplyGeneratorStat(Obj_Generator generator, ObjectPlacement placement, string statName, ScenarioApplyResult result)
        {
            float value;
            if (!ScenarioPropertyBag.TryGetFloat(placement.CustomProperties, StatKey(statName), out value))
                return;

            if (string.Equals(statName, StatFuelCapacity, StringComparison.OrdinalIgnoreCase))
            {
                value = ClampFloat(value, 1f, 10000f);
                int index = GetUpgradeIndex(generator, UpgradeObject.PathEnum.Capacity, GeneratorFuelCapacityPerLevelField);
                SetFloatArrayIndex(generator, GeneratorFuelCapacityPerLevelField, index, value);
                SetField(generator, GeneratorFuelCapacityField, value);
                if (GeneratorFuelField != null)
                {
                    float fuel = 0f;
                    string message;
                    SeamGuard.Try<float>(
                        "scenario.station-upgrade.field." + GeneratorFuelField.Name,
                        SeamRecoveryPolicy.DisableSeamAndDegrade,
                        delegate { return (float)GeneratorFuelField.GetValue(generator); },
                        0f,
                        "Station upgrade projection unavailable - scenario still playable.",
                        null,
                        out fuel,
                        out message);
                    if (fuel > value)
                        SetField(generator, GeneratorFuelField, value);
                }
                return;
            }

            if (string.Equals(statName, StatPowerOutput, StringComparison.OrdinalIgnoreCase))
            {
                int intValue = ClampInt((int)Math.Round(value), 1, 10000);
                int index = GetUpgradeIndex(generator, UpgradeObject.PathEnum.Efficiency, GeneratorPowerOutputPerLevelField);
                SetIntArrayIndex(generator, GeneratorPowerOutputPerLevelField, index, intValue);
                SetField(generator, GeneratorPowerOutputField, intValue);
                UpdatePowerFlow();
                return;
            }

            if (string.Equals(statName, StatOutputRate, StringComparison.OrdinalIgnoreCase))
            {
                float rate = ClampFloat(value, 0f, 1f);
                generator.OutputRate = rate;
                SetField(generator, GeneratorOutputRateField, rate);
                UpdatePowerFlow();
            }
        }

        private static void ApplyWaterTankStat(Obj_WaterTank tank, ObjectPlacement placement, string statName, ScenarioApplyResult result)
        {
            float value;
            if (!ScenarioPropertyBag.TryGetFloat(placement.CustomProperties, StatKey(statName), out value))
                return;

            if (string.Equals(statName, StatWaterCapacity, StringComparison.OrdinalIgnoreCase))
            {
                int capacity = ClampInt((int)Math.Round(value), 0, 10000);
                WaterManager water = WaterManager.Instance;
                if (water != null)
                    water.UnRegisterStorage(tank);
                SetField(tank, WaterTankCapacityField, capacity);
                if (water != null)
                    water.RegisterStorage(tank);
                return;
            }

            if (string.Equals(statName, StatWaterGeneration, StringComparison.OrdinalIgnoreCase))
            {
                float generation = ClampFloat(value, 0f, 1000f);
                SetField(tank, WaterTankGenerationField, generation);
                if (WaterTankNextGenerationField != null && generation > 0f)
                    SetField(tank, WaterTankNextGenerationField, Time.time + GameTime.RealSecondsPerDay / generation);
            }
        }

        private static void ApplyOxygenFilterStat(Obj_OxygenFilter filter, ObjectPlacement placement, string statName, ScenarioApplyResult result)
        {
            float value;
            if (!ScenarioPropertyBag.TryGetFloat(placement.CustomProperties, StatKey(statName), out value))
                return;

            float multiplier = ClampFloat(value, 0f, 10f);
            int index = GetUpgradeIndex(filter, UpgradeObject.PathEnum.Generation, OxygenMultiplierPerLevelField);
            SetFloatArrayIndex(filter, OxygenMultiplierPerLevelField, index, multiplier);
            SetField(filter, OxygenMultiplierField, multiplier);
        }

        private static void AddStatSnapshots(Obj_Base obj, ObjectPlacement placement, List<ScenarioStationStatSnapshot> stats)
        {
            if (stats == null || obj == null)
                return;

            if (obj is Obj_Generator)
            {
                Obj_Generator generator = (Obj_Generator)obj;
                stats.Add(BuildStat(placement, StatFuelCapacity, "Fuel Capacity", generator.FuelCapacity, 1f, 10000f, 10f, "Generator fuel storage read by Obj_Generator.Update."));
                stats.Add(BuildStat(placement, StatPowerOutput, "Power Output", generator.PowerOutput, 1f, 10000f, 25f, "Generator power output read by Obj_Generator.HowMuchPower."));
                stats.Add(BuildStat(placement, StatOutputRate, "Output Rate", generator.OutputRate, 0f, 1f, 0.1f, "Generator output throttle read by Obj_Generator.HowMuchPower."));
                return;
            }

            if (obj is Obj_WaterTank)
            {
                Obj_WaterTank tank = (Obj_WaterTank)obj;
                stats.Add(BuildStat(placement, StatWaterCapacity, "Water Capacity", tank.Capacity, 0f, 10000f, 10f, "WaterManager.RegisterStorage reads Obj_WaterTank.Capacity."));
                stats.Add(BuildStat(placement, StatWaterGeneration, "Water Generation", tank.WaterGeneration, 0f, 1000f, 1f, "Obj_WaterTank.Update reads WaterGeneration."));
                return;
            }

            if (obj is Obj_OxygenFilter)
            {
                Obj_OxygenFilter filter = (Obj_OxygenFilter)obj;
                stats.Add(BuildStat(placement, StatOxygenMultiplier, "Oxygen Multiplier", filter.OxygenInMult, 0f, 10f, 0.1f, "EnvironmentManager reads Obj_OxygenFilter.OxygenInMult."));
            }
        }

        private static bool TryBuildStatSnapshot(Obj_Base obj, ObjectPlacement placement, string statName, out ScenarioStationStatSnapshot stat)
        {
            stat = null;
            List<ScenarioStationStatSnapshot> stats = new List<ScenarioStationStatSnapshot>();
            AddStatSnapshots(obj, placement, stats);
            for (int i = 0; i < stats.Count; i++)
            {
                ScenarioStationStatSnapshot candidate = stats[i];
                if (candidate != null && string.Equals(candidate.Name, statName, StringComparison.OrdinalIgnoreCase))
                {
                    stat = candidate;
                    return true;
                }
            }

            return false;
        }

        private static ScenarioStationStatSnapshot BuildStat(ObjectPlacement placement, string name, string label, float fallback, float min, float max, float step, string detail)
        {
            float value = ScenarioPropertyBag.GetFloat(placement != null ? placement.CustomProperties : null, StatKey(name), fallback);
            return new ScenarioStationStatSnapshot
            {
                Name = name,
                Label = label,
                Value = ClampFloat(value, min, max),
                MinValue = min,
                MaxValue = max,
                Step = step,
                HasOverride = placement != null && !string.IsNullOrEmpty(ScenarioPropertyBag.GetString(placement.CustomProperties, StatKey(name))),
                Detail = detail
            };
        }

        private static int GetUpgradeIndex(Obj_Base obj, UpgradeObject.PathEnum path, FieldInfo arrayField)
        {
            int maxIndex = 0;
            if (arrayField != null)
            {
                Array array = null;
                string message;
                SeamGuard.Try<Array>(
                    "scenario.station-upgrade.array." + arrayField.Name,
                    SeamRecoveryPolicy.DisableSeamAndDegrade,
                    delegate { return arrayField.GetValue(obj) as Array; },
                    null,
                    "Station upgrade projection unavailable - scenario still playable.",
                    null,
                    out array,
                    out message);
                if (array != null && array.Length > 0)
                    maxIndex = array.Length - 1;
            }

            UpgradeObject upgrade = obj.GetComponent<UpgradeObject>();
            int level = upgrade != null ? upgrade.GetUpgradeLevel(path) : 0;
            return ClampInt(level, 0, maxIndex);
        }

        private static void SetObjectLevel(Obj_Base obj, int level)
        {
            SetField(obj, ObjectLevelField, level);
        }

        private static void SetFloatArrayIndex(object target, FieldInfo field, int index, float value)
        {
            if (target == null || field == null)
                return;

            float[] values = null;
            string message;
            SeamGuard.Try<float[]>(
                "scenario.station-upgrade.array." + field.Name,
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate { return field.GetValue(target) as float[]; },
                null,
                "Station upgrade projection unavailable - scenario still playable.",
                null,
                out values,
                out message);
            if (values == null || values.Length == 0)
                return;

            values[ClampInt(index, 0, values.Length - 1)] = value;
        }

        private static void SetIntArrayIndex(object target, FieldInfo field, int index, int value)
        {
            if (target == null || field == null)
                return;

            int[] values = null;
            string message;
            SeamGuard.Try<int[]>(
                "scenario.station-upgrade.array." + field.Name,
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate { return field.GetValue(target) as int[]; },
                null,
                "Station upgrade projection unavailable - scenario still playable.",
                null,
                out values,
                out message);
            if (values == null || values.Length == 0)
                return;

            values[ClampInt(index, 0, values.Length - 1)] = value;
        }

        private static void SetField(object target, FieldInfo field, object value)
        {
            if (target != null && field != null)
            {
                string message;
                SeamGuard.Run(
                    "scenario.station-upgrade.field." + field.Name,
                    SeamRecoveryPolicy.DisableSeamAndDegrade,
                    delegate { field.SetValue(target, value); },
                    "Station upgrade projection unavailable - scenario still playable.",
                    null,
                    out message);
            }
        }

        private static bool TryParsePath(string pathName, out UpgradeObject.PathEnum path)
        {
            path = UpgradeObject.PathEnum.Base;
            if (string.IsNullOrEmpty(pathName))
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(UpgradeObject.PathEnum), pathName, true);
                if (parsed == null || !Enum.IsDefined(typeof(UpgradeObject.PathEnum), parsed))
                    return false;

                path = (UpgradeObject.PathEnum)parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field;
            }

            return null;
        }

        private static bool RemoveProperty(List<ScenarioProperty> properties, string key)
        {
            for (int i = properties != null ? properties.Count - 1 : -1; i >= 0; i--)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    properties.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private static void UpdatePowerFlow()
        {
            if (PowerManager.Instance != null)
                PowerManager.Instance.UpdatePowerFlow();
        }

        private static void AddMessage(ScenarioApplyResult result, string message)
        {
            if (result != null)
                result.AddMessage(message);
            if (!string.IsNullOrEmpty(message))
                MMLog.WriteWarning("[ScenarioStationUpgradePropertyService] " + message);
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static float ClampFloat(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ScenarioStationUpgradeSnapshot
    {
        public ScenarioStationUpgradeSnapshot()
        {
            Paths = new List<ScenarioStationUpgradePathSnapshot>();
            Stats = new List<ScenarioStationStatSnapshot>();
        }

        public string ObjectType { get; set; }
        public int Level { get; set; }
        public List<ScenarioStationUpgradePathSnapshot> Paths { get; private set; }
        public List<ScenarioStationStatSnapshot> Stats { get; private set; }
    }

    internal sealed class ScenarioStationUpgradePathSnapshot
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public int CurrentLevel { get; set; }
        public int MaxLevel { get; set; }
    }

    internal sealed class ScenarioStationStatSnapshot
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public float Value { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public float Step { get; set; }
        public bool HasOverride { get; set; }
        public string Detail { get; set; }
    }
}
