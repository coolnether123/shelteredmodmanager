Mod Settings Format (Coolnether123)
===================================

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
- default.json contains all defaults
- user.json includes only keys that differ from defaults
- Effective settings = defaults overridden by user values

Mod API Usage
-------------

using UnityEngine;

public class MyPlugin : IPlugin {
  public string Name => "MyPlugin";
  public string Version => "1.0.0";

  public void initialize() {}
  public void start(GameObject root) {
    var settings = ModSettings.ForThisAssembly(); // (Coolnether123)
    int maxCount = settings.GetInt("maxCount", 5);
    bool enabled = settings.GetBool("enabled", true);

    // Change and persist:
    settings.SetInt("maxCount", 20);
    settings.SaveUser();
  }
}

