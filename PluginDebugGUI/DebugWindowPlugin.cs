using UnityEngine;
using ModAPI.Core;

/**
 * Maintainer: coolnether123
 * 
 * Simple plugin that attaches the DebugWindowComponent using the new
 * IModPlugin lifecycle + context. This replaces the old IPlugin-based version.
 */
public class DebugWindowPlugin : IModPlugin //
{
    public void Initialize(IPluginContext ctx)
    {
        // no-op
    }

    public void Start(IPluginContext ctx)
    {
        // Attach the debug window UI under this plugin's parent
        var comp = ctx.PluginRoot.AddComponent<DebugWindowComponent>(); //
        // keep any default labels or setup here if you want
    }
}
