using UnityEngine;

/**
* Author: benjaminfoo
* Maintainer: Coolnether123
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* This is the plugin definition for the console-plugin, which displays an ingame-console.
*/
public class ConsolePlugin : IModPlugin
{
    public ConsolePlugin() { }

    public void Initialize(IPluginContext ctx)
    {
        // No-op; reserved for future console command registration
    }

    public void Start(IPluginContext ctx)
    {
        ConsoleWindowComponent consoleWindow = ctx.PluginRoot.AddComponent<ConsoleWindowComponent>();
        ctx.Log.Info("ConsolePlugin: ConsoleWindowComponent attached.");
    }
}
