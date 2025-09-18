using HarmonyLib;
using UnityEngine;
using ModAPI.Core;
using ModAPI.UI;

/// <summary>
/// Example plugin that adds a simple cyan label to the Main Menu
/// 
/// This shows the full lifecycle of a ModAPI plugin:
///  - Initialize() is called before the game starts (read settings here)
///  - Start() is called once Unity is running (safe to patch here)
///  - Harmony is used to patch into game methods (here: MainMenu.OnShow)
///  - UIUtil helper is used to safely create an NGUI UILabel
/// 
/// Why is Harmony used at all?
///   - Sheltered doesn't add menu panels until runtime
///   - The menu GameObjects exist but are inactive during scene load
///   - Harmony lets us “hook” the panel’s OnShow() so we run code right when
///     the game activates it, ensuring the label is visible.
/// 
/// </summary>
public class MyMenuPlugin : IModPlugin
{
    private IModLogger _log;

    /// <summary>
    /// Called very early, before Unity scene is running.
    /// Use this to read settings/config values.
    /// </summary>
    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("MyMenuPlugin Initializing...");
    }

    /// <summary>
    /// Called once Unity is ready and safe to run coroutines/patches.
    /// Creates a Harmony instance and patch all targets in this assembly.
    /// </summary>
    public void Start(IPluginContext ctx)
    {
        _log.Info("MyMenuPlugin Starting...");
        var h = new Harmony("com.plugin.mymenu"); // unique ID for this patch set
        h.PatchAll(typeof(MyMenuPlugin).Assembly);
        _log.Info("Harmony patches for MyMenuPlugin applied.");
    }
}

/// <summary>
/// Patch handler for when the MainMenu panel is shown.
/// Note: Sheltered has two menu variants: MainMenu and MainMenuX.
/// To be safe, you can patch both. Just MainMenu works here though.
/// </summary>
[HarmonyPatch(typeof(MainMenu), "OnShow")]
public static class MainMenu_OnShow_Patch
{
    /// <summary>
    /// Postfix is called after the real OnShow() runs.
    /// At this point the panel is active and safe to add children to.
    /// </summary>
    static void Postfix(MainMenu __instance)
    {
        // Always null-check the instance; Harmony will pass null if the patch misfires
        if (__instance == null || __instance.gameObject == null) return;

        // Our own marker component prevents us from adding the label twice
        if (__instance.GetComponent<MyMenuLabelMarker>() != null) return;

        __instance.gameObject.AddComponent<MyMenuLabelMarker>();

        // Build label options:
        // - Cyan text
        // - Anchored top-right with a 10px inset
        // - Slight outline effect so it stands out
        // - Relative depth +50 to ensure it renders above most default widgets
        UIPanel used;
        var opts = new UIUtil.UILabelOptions
        {
            text = "MyMenuPlugin is active!",
            color = Color.cyan,
            fontSize = 22,
            alignment = NGUIText.Alignment.Right,
            effect = UILabel.Effect.Outline,
            effectColor = new Color(0, 0, 0, 0.85f),
            anchor = UIUtil.AnchorCorner.TopRight,
            pixelOffset = new Vector2(-10, -10),
            relativeDepth = 50
        };

        // Actually create the label via the ModAPI helper.
        // Use UIUtil.CreateLabel instead of new UILabel manually to
        //   - Ensures label is under a UIPanel
        //   - Picks a working font automatically (bitmap or TTF fallback)
        //   - Computes safe depth above siblings
        //   - Honors UIRoot activeHeight → resolution-scaled placement
        UIUtil.CreateLabel(__instance.gameObject, opts, out used);
    }
}

/// <summary>
/// Simple empty marker component to ensure it doesn't inject multiple times.
/// Many BasePanel classes in Sheltered can call OnShow() more than once per session. 
/// </summary>
public class MyMenuLabelMarker : MonoBehaviour { }