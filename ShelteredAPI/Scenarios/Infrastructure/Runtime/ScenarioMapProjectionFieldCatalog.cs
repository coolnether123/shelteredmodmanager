using System;
using System.Reflection;

using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime
{
    internal sealed class ScenarioMapProjectionField
    {
        public string Group { get; set; }
        public string Field { get; set; }
        public bool AppliesInGame { get; set; }
        internal ScenarioMapEncounterProjectionAction Apply { get; set; }

        public string StatusText
        {
            get { return AppliesInGame ? "Applies in game" : "Saved with the scenario; not yet applied in game"; }
        }
    }

    internal delegate bool ScenarioMapEncounterProjectionAction(
        MapRegion region,
        MapEncounterTableDefinition table,
        FieldInfo factionChanceField);

    /// <summary>
    /// The executable projection map for encounter authoring. Runtime application and
    /// editor honesty labels consume these same descriptors, preventing a parallel UI list.
    /// </summary>
    internal static class ScenarioMapProjectionFieldCatalog
    {
        private static readonly ScenarioMapProjectionField[] EncounterFields =
        {
            Projected("Encounter chances", "OpenGroundChance", delegate(MapRegion region, MapEncounterTableDefinition table, FieldInfo ignored)
            {
                if (table.OpenGroundChance < 0) return false;
                region.chanceOfOpenGroundEncounter = ClampPercent(table.OpenGroundChance);
                return true;
            }),
            Projected("Encounter chances", "SearchNpcRevealChance", delegate(MapRegion region, MapEncounterTableDefinition table, FieldInfo ignored)
            {
                if (table.SearchNpcRevealChance < 0) return false;
                region.chanceThatSearchRevealsNpcs = ClampPercent(table.SearchNpcRevealChance);
                return true;
            }),
            Projected("Encounter chances", "AnimalEncounterChance", delegate(MapRegion region, MapEncounterTableDefinition table, FieldInfo ignored)
            {
                if (table.AnimalEncounterChance < 0) return false;
                region.chanceThatEncounterIsAnimal = ClampPercent(table.AnimalEncounterChance);
                return true;
            }),
            Projected("Encounter chances", "FactionEncounterChance", delegate(MapRegion region, MapEncounterTableDefinition table, FieldInfo factionChanceField)
            {
                if (table.FactionEncounterChance < 0 || factionChanceField == null) return false;
                factionChanceField.SetValue(region, ClampPercent(table.FactionEncounterChance));
                return true;
            }),
            StoredOnly("Encounter outcomes", "Entries"),
            StoredOnly("Encounter custom data", "Properties")
        };

        public static ScenarioMapProjectionField[] GetEncounterFields()
        {
            ScenarioMapProjectionField[] copy = new ScenarioMapProjectionField[EncounterFields.Length];
            Array.Copy(EncounterFields, copy, EncounterFields.Length);
            return copy;
        }

        public static int ApplyEncounterFields(MapRegion region, MapEncounterTableDefinition table, FieldInfo factionChanceField)
        {
            if (region == null || table == null)
                return 0;
            int applied = 0;
            for (int i = 0; i < EncounterFields.Length; i++)
            {
                ScenarioMapProjectionField descriptor = EncounterFields[i];
                if (descriptor.AppliesInGame && descriptor.Apply != null && descriptor.Apply(region, table, factionChanceField))
                    applied++;
            }
            return applied;
        }

        public static bool IsSynchronized()
        {
            for (int i = 0; i < EncounterFields.Length; i++)
            {
                ScenarioMapProjectionField descriptor = EncounterFields[i];
                if (descriptor == null || string.IsNullOrEmpty(descriptor.Group) || string.IsNullOrEmpty(descriptor.Field))
                    return false;
                if (descriptor.AppliesInGame != (descriptor.Apply != null))
                    return false;
            }
            return true;
        }

        private static ScenarioMapProjectionField Projected(string group, string field, ScenarioMapEncounterProjectionAction apply)
        {
            return new ScenarioMapProjectionField { Group = group, Field = field, AppliesInGame = true, Apply = apply };
        }

        private static ScenarioMapProjectionField StoredOnly(string group, string field)
        {
            return new ScenarioMapProjectionField { Group = group, Field = field, AppliesInGame = false, Apply = null };
        }

        private static int ClampPercent(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }
}
