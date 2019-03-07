using UnityEngine;

public class ConsolePlugin : IPlugin
{
    public ConsolePlugin() { }

    public string Name => "ConsolePlugin";
    public string Version => "0.0.1";


    public void initialize()
    {

    }

    public void start(GameObject root)
    {
        ConsoleWindowComponent consoleWindow = root.AddComponent<ConsoleWindowComponent>();
    }

}