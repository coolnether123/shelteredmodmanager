using System;
using UnityEngine;
using ConsoleCommands;
using ModAPI.Core;


class ConsoleWindowComponent : MonoBehaviour
{
    public int id = 1;
    public Rect windowRect = new Rect(20, 20, 450, 400);

    public String titleText = "Console";

    private CommandProcessor _commandProcessor;
    private Vector2 _scrollPosition;

    private string consoleOutput = "";
    private string consoleInput = "";

    void Awake()
    {

    }

    void Start()
    {
        MMLog.Write("ConsoleWindowComponent: Start called. Instantiating CommandProcessor.");
        _commandProcessor = new CommandProcessor();
        consoleOutput = "Console Initialized. Type 'help' for a list of commands.";
        Application.logMessageReceived += HandleUnityLog;
    }

    void OnGUI()
    {
        windowRect = GUI.Window(id, windowRect, DoMyWindow, titleText);
    }

    void DoMyWindow(int windowID)
    {
        bool executeCommand = (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return);

        GUILayout.BeginVertical();

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(320));
        GUILayout.TextArea(consoleOutput, new GUILayoutOption[] {
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true)
        });
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        
        GUI.SetNextControlName("ConsoleInput");
        consoleInput = GUILayout.TextField(consoleInput, new GUILayoutOption[] {
            GUILayout.ExpandWidth(true),
            GUILayout.Height(30),
        });
        GUI.FocusControl("ConsoleInput");

        if (GUILayout.Button("Execute", new GUILayoutOption[] { GUILayout.Width(80), GUILayout.Height(30) }) || executeCommand)
        {
            if (!string.IsNullOrEmpty(consoleInput))
            {
                consoleOutput += string.Format("\n> {0}", consoleInput);

                string result = _commandProcessor.ProcessCommand(consoleInput);
                
                if (result == "##CLEAR##")
                {
                    consoleOutput = "";
                }
                else if (!string.IsNullOrEmpty(result))
                {
                    consoleOutput += string.Format("\n{0}", result);
                }

                consoleInput = "";
                _scrollPosition.y = Mathf.Infinity;
            }
            Event.current.Use();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }

    void OnDestroy()
    {
        MMLog.Write("ConsoleWindowComponent: OnDestroy called.");
        Application.logMessageReceived -= HandleUnityLog;
    }

    private void HandleUnityLog(string condition, string stackTrace, LogType type)
    {
        string prefix = type == LogType.Warning ? "[Warn] " :
                        (type == LogType.Error || type == LogType.Exception) ? "[Error] " :
                        (type == LogType.Assert ? "[Assert] " : "[Log] ");
        consoleOutput += string.Format("\n{0}{1}", prefix, condition);
        _scrollPosition.y = Mathf.Infinity;
    }
}
