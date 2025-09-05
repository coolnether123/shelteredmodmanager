Mod Settings Format
===================

Location per mod:

- Config/default.json — default values bundled with the mod
- Config/user.json — user overrides written by the loader when values change

File format (JSON, parsed by UnityEngine.JsonUtility on .NET 3.5):

{
  "entries": [
    { "key": "difficulty", "type": "string", "value": "Normal" },
    { "key": "maxCount",   "type": "int",    "value": "10" },
    { "key": "spawnRate",  "type": "float",  "value": "1.25" },
    { "key": "enabled",    "type": "bool",   "value": "true" }
  ]
}

Notes
-----
- Types supported: string, int, float, bool
- default.json contains all defaults for the mod
- user.json includes only keys that differ from defaults that the user assigns
- Effective settings = defaults overridden by user values

Using Settings in a Plugin
--------------------------

using UnityEngine;

public class MyPlugin : IModPlugin
{
  public void Initialize(IPluginContext ctx)
  {
    // optional: pre-load resources or register services
  }

  public void Start(IPluginContext ctx)
  {
    // Access this mod's settings via context
    var settings = ctx.Settings;
    int maxCount = settings.GetInt("maxCount", 5);
    bool enabled = settings.GetBool("enabled", true);

    // Modify and persist user overrides
    settings.SetInt("maxCount", 20);
    settings.SaveUser();

    // Attach behaviours under the per-plugin root
    ctx.PluginRoot.AddComponent<MyMonoBehaviour>();
    ctx.Log.Info($"MyPlugin started. enabled={enabled} maxCount={maxCount}");
  }
}
