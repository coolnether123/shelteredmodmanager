using UnityEngine;

/**
* Author: benjaminfoo
* Maintainer: Coolnether123
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* This is the plugin definition for initializer plugin which simply prints "Modding-API active" on the screen.
* The purpose of this plugin is to signalize that the plugin-mechanism works as early as possible, as the console is not 
* available currently.
*/
public class InitializerPlugin : IModPlugin
{
    public InitializerPlugin() { }

    public void Initialize(IPluginContext ctx)
    {
        ctx.Log.Info("InitializerPlugin: Initialize called.");
    }

    public void Start(IPluginContext ctx)
    {
        ctx.Log.Info("InitializerPlugin: Start called.");

        LabelComponent label = ctx.PluginRoot.AddComponent<LabelComponent>();
        label.setTop(10);
        label.setText("Modding-API active");

        ctx.Log.Info("InitializerPlugin: LabelComponent added and text set.");
    }
}
