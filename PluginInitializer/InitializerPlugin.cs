using System;
using System.IO;

public class InitializerPlugin : IPlugin
{
    public InitializerPlugin() { }

    public string Name => "Initializer";

    public void initialize()
    {
        Console.WriteLine("Executing initialize of Initializer ...");

        using (TextWriter tw = File.CreateText("plugin_loaded.log"))
        {
            tw.WriteLine("plugin initialized!");
            tw.Flush();
        }
    }

    public void start()
    {
    }
}
