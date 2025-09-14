How to Develop a Plugin | Coolnether123
=======================

Prerequisites
- Visual Studio 2017+ or JetBrains Rider
- Clone this repo and build once so `ModAPI.dll` is available

Steps
1) Create a new C# Class Library project targeting .NET Framework 3.5

2) Add a project reference to 
- `ModAPI.dll` from `Dist/SMM`
- `Assembly-CSharp.dll` from `Windows64_EOS_Data\Managed`
- Also if needed `UnityEngine.dll` from `Windows64_EOS_Data\Managed`

3) Write your plugin code:

// HelloWorldPlugin.cs - A simple example plugin.
using UnityEngine;

public class HelloWorldPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx)
    {
        // one-time setup; read settings, register services, etc.
    }

    public void Start(IPluginContext ctx)
    {
        // Attach a simple behaviour under the per-plugin root.
        ctx.PluginRoot.AddComponent<HelloWorldComponent>();
        ctx.Log.Info("Hello World Plugin started.");
    }
}

// Unity MonoBehaviour that renders a label over the screen
public class HelloWorldComponent : MonoBehaviour
{
    void OnGUI()
    {
        GUI.color = Color.white;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.UpperCenter
        };
        GUI.Label(new Rect(0, 10, Screen.width, 50),
            "Hello, World! The plugin is working!", style);
    }
}


4) Implement `IModPlugin` and use the folder-based mod layout:

   Sheltered/mods/disabled/MyPlugin/
     About/About.json
     Assemblies/MyPlugin.dll   <-- Build your project here
     Config/default.json       (optional; see documentation/SETTINGS.md)

   Minimal About.json:
   {
     "id": "com.yourname.myplugin",
     "name": "My Plugin",
     "version": "1.0.0",
     "authors": ["You"],
     "description": "What it does"
   }


5) Enable your mod in the Manager GUI

Using Settings
- Access via the plugin context: `var s = ctx.Settings;`
- Read: `s.GetInt("maxCount", 5)` / `s.GetBool("enabled", true)` etc.
- Write: `s.SetInt("maxCount", 10); s.SaveUser();`

Tips
- Avoid blocking the main thread during `Start`; use coroutines if needed (`ctx.StartCoroutine` or `ctx.RunNextFrame`)
- Use `ctx.Log.Info("...")` for simple file logging, or advanced MMLog levels for diagnostics.
- Reference Unity game assemblies from your local install if you build against them
