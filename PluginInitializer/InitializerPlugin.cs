using UnityEngine;

/**
* Author: benjaminfoo
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* This is the plugin definition for initializer plugin which simply prints "Modding-API active" on the screen.
* The purpose of this plugin is to signalize that the plugin-mechanism works as early as possible, as the console is not 
* available currently.
*/
public class InitializerPlugin : IPlugin
{
    public InitializerPlugin() { }

    public string Name => "InitializerPlugin";
    public string Version => "0.0.1";


    public void initialize()
    {

    }

    public void start(GameObject root)
    {
        LabelComponent label = root.AddComponent<LabelComponent>();
        label.setTop(10);
        label.setText("Modding-API active");

    }
}
