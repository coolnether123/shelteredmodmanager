using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class SceneGraphWindowPlugin : IPlugin
{
    public SceneGraphWindowPlugin() { }

    public string Name => "SceneGraphWindow-Plugin";

    public void initialize()
    {

    }

    public void start(GameObject root)
    {
        SceneGraphWindowComponent label = root.AddComponent<SceneGraphWindowComponent>();

    }
}

