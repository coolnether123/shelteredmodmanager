using UnityEngine;
using System;

/**
* Author: benjaminfoo
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* This is the plugin definition for the console-plugin, which displays an ingame-console
*/
public class ConsolePlugin : IPlugin
{
    public ConsolePlugin() { }

    public string Name => "ConsolePlugin";
    public string Version => "0.0.1";


    public void initialize()
    {
        MMLog.Write("ConsolePlugin: initialize called.");
    }

    public void start(GameObject root)
    {
        MMLog.Write("ConsolePlugin: Attempting to add ConsoleWindowComponent to GameObject.");
        try
        {
            ConsoleWindowComponent consoleWindow = root.AddComponent<ConsoleWindowComponent>();
            MMLog.Write("ConsolePlugin: ConsoleWindowComponent added successfully.");
        }
        catch (Exception ex)
        {
            MMLog.Write(string.Format("ConsolePlugin: ERROR adding ConsoleWindowComponent: {0}", ex.Message));
        }
    }

}