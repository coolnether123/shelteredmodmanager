using System;
using UnityEngine;

class LabelComponent : MonoBehaviour
{
    public Rect windowRect0 = new Rect(200, 200, 300, 300);

    void Start()
    {
        // Debug.Log("Modding-API is active");
        // System.IO.File.WriteAllText("testfile.txt", "some text!");
    }

    void OnGUI()
    {
        GUI.Label(new Rect(20f, 40f, 400f, 20f), "Modding-API is active");

        // Here we make 2 windows. We set the GUI.color value to something before each.
        GUI.color = Color.white;
        windowRect0 = GUI.Window(0, windowRect0, DoMyWindow, "Modding-Debug-Window");
    }
    public String stringToEdit = "...";

    // Make the contents of the window.
    // The value of GUI.color is set to what it was when the window
    // was created in the code above.
    void DoMyWindow(int windowID)
    {

        stringToEdit = GUI.TextArea(new Rect(20, 40, 200, 100), stringToEdit, 200);

        /*
        if (GUI.Button(new Rect(10, 20, 100, 20), "Hello World"))
        {
            print("Got a click in window with color " + GUI.color);
        }
        */
        if (GUI.Button(new Rect(20f, 260f, 100f, 20f), new GUIContent("Show items")))
        {
            OnGUI();
        }
        // Make the windows be draggable.
        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }
}