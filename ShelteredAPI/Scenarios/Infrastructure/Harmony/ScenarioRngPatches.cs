using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Scenarios.Infrastructure.Harmony
{
    /// <summary>Auditable first RNG batch. Missing Epic/Steam members are skipped, never fatal.</summary>
    internal static class ScenarioRngPatches
    {
        private static bool _installed;
        private static readonly string[] TierOneTypes = new string[]
        {
            "DiceRoll", "RandomStatGenerator", "CharacterMeshOptions", "EncounterGenerator",
            "WeatherManager", "NpcVisitManager", "FamilySpawner", "ExplorationParty",
            "CombatAI_Worm", "CombatAIAggressive", "CombatAIBear", "CombatAIDebug", "CombatAIDog",
            "CombatAIGeneric", "CombatAIMutant", "CombatAISurroundedBoss", "CombatAIWolf"
        };
        private static readonly MethodInfo RangeII = AccessTools.Method(typeof(UnityEngine.Random), "Range", new Type[] { typeof(int), typeof(int) });
        private static readonly MethodInfo RangeFF = AccessTools.Method(typeof(UnityEngine.Random), "Range", new Type[] { typeof(float), typeof(float) });
        private static readonly MethodInfo Value = AccessTools.PropertyGetter(typeof(UnityEngine.Random), "value");
        private static readonly MethodInfo BridgeII = AccessTools.Method(typeof(ModRandomBridge), "Range", new Type[] { typeof(int), typeof(int) });
        private static readonly MethodInfo BridgeFF = AccessTools.Method(typeof(ModRandomBridge), "Range", new Type[] { typeof(float), typeof(float) });
        private static readonly MethodInfo BridgeValue = AccessTools.Method(typeof(ModRandomBridge), "Value");

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("ShelteredModManager.ScenarioRngPatch");
            int patched = 0;
            for (int i = 0; i < TierOneTypes.Length; i++)
            {
                Type type = AccessTools.TypeByName(TierOneTypes[i]);
                if (type == null)
                {
                    MMLog.WriteWarning("[ScenarioRngPatch] SKIP type mismatch: " + TierOneTypes[i]);
                    continue;
                }

                MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                for (int j = 0; j < methods.Length; j++)
                {
                    MethodInfo target = methods[j];
                    if (target == null || target.IsAbstract || target.ContainsGenericParameters) continue;
                    try
                    {
                        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(ScenarioRngPatches), "RngTranspiler"));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning("[ScenarioRngPatch] SKIP method mismatch: " + type.FullName + "." + target.Name + " :: " + ex.Message);
                    }
                }
            }
            MMLog.WriteInfo("[ScenarioRngPatch] Installed first tier-1 batches; methods=" + patched + ".");
        }

        public static IEnumerable<CodeInstruction> RngTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator generator)
        {
            return FluentTranspiler.Execute(instructions, original, generator, FluentTranspiler.BuildProfile.Runtime, delegate(FluentTranspiler t)
            {
                t.ReplaceCalls(RangeII).Optional().WithCall(BridgeII, "RNG int Range redirect");
                t.ReplaceCalls(RangeFF).Optional().WithCall(BridgeFF, "RNG float Range redirect");
                t.ReplaceCalls(Value).Optional().WithCall(BridgeValue, "RNG value redirect");
            });
        }
    }
}
