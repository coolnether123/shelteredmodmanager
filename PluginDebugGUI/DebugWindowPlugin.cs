using UnityEngine;

public class DebugWindowPlugin : IPlugin
{
    public DebugWindowPlugin() { }

    public string Name => "DebugWindowPlugin";

    public void initialize()
    {

    }

    public void start(GameObject root)
    {
        DebugWindowComponent label = root.AddComponent<DebugWindowComponent>();

    }
}
