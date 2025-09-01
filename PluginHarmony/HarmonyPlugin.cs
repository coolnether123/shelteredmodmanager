using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

/**
* Author: benjaminfoo
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* This is the plugin definition for the harmony-plugin, which enables the usage of harmony within a mod-
*/
public class HarmonyPlugin : IPlugin
{
    public HarmonyPlugin() { }

    public string Name => "HarmonyPlugin";

    public string Version => "0.0.1";

    public void initialize()
    {

    }

    public void start(GameObject root)
    {

        var harmony = new Harmony("com.coolnether123.shelteredmodmanager");
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        LabelComponent label = root.AddComponent<LabelComponent>();
        label.setTop(30);
        if (harmony == null) return;
        label.setText(
            "Harmony-Plugin loaded: " + (harmony != null) + "\n"
            + "Harmony-Id: " + (harmony.Id) + "\n"
            + "Harmony.hasPatches: " + Harmony.HasAnyPatches(harmony.Id)
        );



        
    }

    [HarmonyPatch(typeof(CraftingPanel), "GetRecipes")]
    public static class CraftingPanel_GetRecipes_Patch
    {
        public static void Postfix(CraftingPanel __instance)
        {
            using (TextWriter tw = File.CreateText(@"harmony_works.log"))
            {
                tw.WriteLine("HarmonyPlugin and Patches initialized!");
                tw.Flush();
            }
        }
    }

    [HarmonyPatch(typeof(Obj_Base), "IsRelocationEnabled")]
    public static class Obj_Base_IsRelocationEnabled_Patch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }


    [HarmonyPatch(typeof(Obj_Integrity), "IsInteractionAllowed")]
    public static class Obj_Integrity_IsInteractionAllowed_Patch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }


    [HarmonyPatch(typeof(Obj_Base), "Update")]
    public static class Obj_Base_Update_Patch
    {
        public static bool Prefix(Obj_Base __instance)
        {
            Traverse.Create(__instance).Field("m_Movable").SetValue(true);
            return true;
        }
    }




}
