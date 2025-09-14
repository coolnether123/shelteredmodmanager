How to Develop a Harmony Patch | Coolnether123
==============================

Harmony Reference
- This project references Lib.Harmony 2.4.1
- You can either add Harmony to your own plugin, or depend on harmony being present. For less bloat please depend on it. Add to the mod about "dependsOn": ["com.harmony.0harmony"]

Quick Start
1) Ensure your plugin DLL references Harmony (either via NuGet or by referencing the `0Harmony.dll` shipped with the manager)
2) Create a patch class in your plugin assembly:

   using HarmonyLib;

   [HarmonyPatch(typeof(SomeGameType), "MethodName")] 
   public static class SomeGameType_MethodName_Patch {
     public static void Prefix(/* original args, ref bool __runOriginal, etc. */) {
       // run before original
     }

     public static void Postfix(/* original args, return value, etc. */) {
       // run after original
     }
   }

3) In your plugin `Start`, create/apply your Harmony instance:

   public void Start(IPluginContext ctx) {
     var harmony = new Harmony("com.yourname.myplugin");
     harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
     ctx.Log.Info("Harmony patches applied.");
   }

Tips
- Use unique Harmony IDs (reverse domain style) to avoid conflicts
- Use `Traverse` in Harmony for private field/property access when needed
- Keep patches focused; log with `MMLog.Write("...")` during development
- Harmony is powerful, but in v0.7 you don’t always need it:
  - Use Harmony patches when you want to change game logic.
  - Use UIUtil & context helpers to simply add UI elements.
  
- Read https://harmony.pardeike.net/ for more information
