using System;
using UnityEngine;

class SceneGraphWindowComponent : MonoBehaviour
{
    public Rect windowRect0 = new Rect(20, 20, 240, 400);

    public String titleText = "SceneGraph";
    public String statusLabel = "...";

    void OnGUI()
    {
        statusLabel = "...";

        /*
        object[] obj = GameObject.FindSceneObjectsOfType(typeof(GameObject));
        int counter = 0;
        foreach (object o in obj)
        {
            if (counter > 10) continue;

            GameObject g = (GameObject)o;
            statusLabel += "Node: " + g.name;
            statusLabel += "\n";
            counter++;
        }
        */

        // windowRect0 = GUI.Window(1, windowRect0, DoMyWindow, titleText );
    }

    // Make the contents of the window
    void DoMyWindow(int windowID)
    {
        GUI.Label(new Rect(15, 30, 200, 400), statusLabel);

        // Make the windows be draggable.
        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }
}