﻿using System;
using System.Collections.Generic;
using UnityEngine;

/**
* Author: benjaminfoo
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* The userinterface which displays overall information at runtime
*/
class DebugWindowComponent : MonoBehaviour
{
    public Rect windowRect0 = new Rect(20 + Screen.width - 350, 20, 300, 450);

    public String labelText = "This is some text";
    public String titleText = "SMM-Debug-Window";
    public String statusLabel = "...";


    void Start()
    {

    }

    string[] messages = new string[] {
        "Waddup...?",
        "Why you ackin' so cray cray?",
        "...",
        "zZzZzZz....",
        "The unity-version of this game is " + Application.unityVersion,
        "The application language is " + Application.systemLanguage,
        "The data-path of this application is " + Application.dataPath
    };

    void OnGUI()
    {
        if (Input.GetKeyUp(KeyCode.P))
        {
            var familyMembers = FamilyManager.Instance.GetAllFamilyMembers();
            familyMembers.ForEach(member => member.Say(messages[UnityEngine.Random.Range(0, messages.Length)]));
            UISound.instance.PlayPreset(UISound.PresetSound.Accept);
        }

        statusLabel = "";

        GameObject doorstopObject = GameObject.Find("Doorstop");
        statusLabel += "Doorstop active: " + (doorstopObject != null);
        statusLabel += "\n";
        statusLabel += "\n";

        // The loader now exposes IModPlugin instead of IPlugin. We also no longer
        // rely on plugin.Name, so we show the concrete type name for clarity.
        IEnumerable<IModPlugin> plugins = PluginManager.getInstance().GetPlugins(); //
        // show count as before
        int pluginCount = 0; foreach (var _ in plugins) pluginCount++; // simple count to keep style
        statusLabel += "Plugins loaded (" + pluginCount + ") ...";
        statusLabel += "\n";
        foreach (IModPlugin plugin in plugins) //
        {
            statusLabel += "|---" + plugin.GetType().Name; // show type name instead of plugin.Name
            statusLabel += "\n";
        }
        statusLabel += "\n";


        statusLabel += "Application: " + Application.productName;
        statusLabel += "\n";
        statusLabel += "\n";

        statusLabel += "Version: " + Application.version;
        statusLabel += "\n";
        statusLabel += "\n";

        statusLabel += "Unity-Version: " + Application.unityVersion;
        statusLabel += "\n";
        statusLabel += "\n";

        statusLabel += "Resolution: " + Screen.width + "x" + Screen.height + " @ " + Screen.dpi;
        statusLabel += "\n";
        statusLabel += "\n";

        statusLabel += "Current level: " + Application.loadedLevelName;
        statusLabel += "\n";
        statusLabel += "\n";

        statusLabel += "Mouse-Position: " + Input.mousePosition.ToString();
        statusLabel += "\n";
        statusLabel += "\n";

        statusLabel += "Real time:\n" + DateTime.Now.ToString("dd.MM.yyyy - HH:mm:ss");
        statusLabel += "\n";
        statusLabel += "\n";

        statusLabel += "Frames per second: " + (int)(1.0f / Time.smoothDeltaTime);
        statusLabel += "\n";
        statusLabel += "\n";


        // Register the window. We create two windows that use the same function
        // Notice that their IDs differ
        windowRect0 = GUI.Window(0, windowRect0, DoMyWindow, titleText);
    }

    void DoMyWindow(int windowID)
    {
        GUI.Label(new Rect(15, 30, 280, 500), statusLabel);

        // Make the windows be draggable.
        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }


}
