using System;
using System.IO;
using UnityEngine;

public class InitializerPlugin : IPlugin
{
    public InitializerPlugin() { }

    public string Name => "InitializerPlugin";

    public void initialize()
    {

    }

    public void start(GameObject root)
    {
        LabelComponent label = root.AddComponent<LabelComponent>();
        label.setText("Mod-API initialized!");
        label.setTop(10);
    }
}
