using System;
using UnityEngine;

/**
* Author: benjaminfoo
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* The console-userinterface which gets instantiated by the console-plugin
*/
class ConsoleWindowComponent : MonoBehaviour
{
    public int id = 1;
    public Rect windowRect1 = new Rect(20, 20, 300, 300);

    public String titleText = "Console";

    private bool executeHasBeenPressed = false;

    private String consoleOutput = String.Empty;
    private string consoleInput = String.Empty;

    void Start() {
        
    }

    void OnGUI()
    {
        windowRect1 = GUI.Window(id, windowRect1, DoMyWindow, titleText);

        if (Event.current.keyCode == KeyCode.Return || executeHasBeenPressed)
        {
            appendMessage(consoleInput);
        }
    }

    public void appendMessage(string message) {
        consoleOutput += "\n";
        consoleOutput += message;

        consoleInput = String.Empty;
    }


    void DoMyWindow(int windowID)
    {
        GUILayout.BeginVertical();
        GUILayout.TextArea(consoleOutput, new GUILayoutOption[] {
            GUILayout.ExpandWidth(true),
            GUILayout.Height(240)
        });

        GUILayout.BeginHorizontal();
        consoleInput = GUILayout.TextField(consoleInput, new GUILayoutOption[] {
            GUILayout.ExpandWidth(true),
            GUILayout.Height(30),
        });
        executeHasBeenPressed = GUILayout.Button("Execute" , new GUILayoutOption[] {
            GUILayout.Width(80),
            GUILayout.Height(30),
        });
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        // Make the windows be draggable.
        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }

    
}