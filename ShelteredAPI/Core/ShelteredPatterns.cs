using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Specialized transpiler helpers for Sheltered-specific patterns.
    /// Moved to ShelteredAPI to preserve ModAPI's generic nature.
    /// </summary>
    public static class ShelteredPatterns
    {
        public static FluentTranspiler MatchManager(this FluentTranspiler t, Type managerType)
        {
            t.MatchCall(managerType, "get_instance");
            if (!t.HasMatch)
                t.MatchFieldLoad(managerType, "instance");
            return t;
        }

        public static FluentTranspiler MatchManagerSingleton(this FluentTranspiler t, Type managerType)
        {
            return t.MatchFieldLoad(managerType, "instance");
        }

        public static FluentTranspiler MatchUILocalization(this FluentTranspiler t, string key = null)
        {
             return t.MatchCall(typeof(Localization), "Get");
        }

        public static FluentTranspiler MatchBunkerLocation(this FluentTranspiler t)
        {
            return t.MatchManager(typeof(GameModeManager))
                    .MatchFieldLoad(typeof(GameModeManager), "m_bunkerPos");
        }
    }
}
