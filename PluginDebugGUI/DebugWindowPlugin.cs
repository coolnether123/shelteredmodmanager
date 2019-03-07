using UnityEngine;

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
