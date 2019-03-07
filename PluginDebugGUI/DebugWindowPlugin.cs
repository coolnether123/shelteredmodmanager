using UnityEngine;

/**
* Author: benjaminfoo
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* This is the plugin definition for the debug-window-plugin, which instantiates the DebugWindowComponent
*/
public class DebugWindowPlugin : IPlugin
{
    public DebugWindowPlugin() { }

    public string Name => "DebugWindowPlugin";
    public string Version => "0.0.1";

    public void initialize()
    {

    }

    public void start(GameObject root)
    {
        DebugWindowComponent debugWindow = root.AddComponent<DebugWindowComponent>();
    }
}
