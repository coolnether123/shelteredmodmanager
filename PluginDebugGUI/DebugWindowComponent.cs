using System;
using System.Collections.Generic;
using UnityEngine;

class DebugWindowComponent : MonoBehaviour
{
    public Rect windowRect0 = new Rect(20, 20, 300, 450);

    public String labelText = "This is some text";
    public String titleText = "SMM-Debug-Window";
    public String statusLabel = "...";


    void Start() {
        
    }

    void OnGUI()
    {

        statusLabel = "";

        GameObject doorstopObject = GameObject.Find("Doorstop");
        statusLabel += "Doorstop active: " + (doorstopObject != null);
        statusLabel += "\n";
        statusLabel += "\n";

        ICollection<IPlugin> plugins = PluginManager.getInstance().GetPlugins();
        statusLabel += "Plugins loaded (" + plugins.Count + ") ...";
        statusLabel += "\n";
        foreach (IPlugin plugin in plugins)
        {
            statusLabel += "|---" + plugin.Name;
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
        windowRect0 = GUI.Window(0, windowRect0, DoMyWindow, titleText );
    }

    void DoMyWindow(int windowID)
    {
        GUI.Label(new Rect(15, 30, 280, 500), statusLabel);

        // Make the windows be draggable.
        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }

    
}