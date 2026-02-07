using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using HarmonyLib;
using ModAPI.Harmony;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    // Live IL Inspector. Toggle with F10.
    public class RuntimeILInspector : MonoBehaviour
    {
        private Rect _window = new Rect(20, 50, 900, 600);
        private bool _visible = false; // F10
        private string _typeFilter = "Assembly-CSharp"; 
        private string _methodFilter = "";
        
        private Vector2 _scrollMethods;
        private Vector2 _scrollIL;
        
        private List<MethodBase> _foundMethods = new List<MethodBase>();
        private MethodBase _selectedMethod;
        private List<CodeInstruction> _displayedIL;
        private string _statusMessage = "";

        private void Awake()
        {
            gameObject.name = "ModAPI.RuntimeILInspector";
            DontDestroyOnLoad(gameObject);
        }

        private void Update() 
        {
            if (Input.GetKeyDown(KeyCode.F10)) 
                _visible = !_visible;
        }

        private void OnGUI() 
        {
            if (!_visible) return;
            
            var prevColor = GUI.color;
            _window = GUI.Window(0x5152, _window, DrawWindow, "ModAPI IL Inspector (F10)");
            GUI.color = prevColor;
        }

        private void DrawWindow(int id) 
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Assembly/Type:", GUILayout.Width(100));
            _typeFilter = GUILayout.TextField(_typeFilter, GUILayout.Width(200));
            GUILayout.Label("Method:", GUILayout.Width(60));
            _methodFilter = GUILayout.TextField(_methodFilter, GUILayout.Width(150));
            
            if (GUILayout.Button("Search", GUILayout.Width(80))) 
            {
                SearchMethods();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(50))) _visible = false;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            
            // Left: Method List
            GUILayout.BeginVertical(GUILayout.Width(300));
            _scrollMethods = GUILayout.BeginScrollView(_scrollMethods, GUI.skin.box);
            for (int i = 0; i < _foundMethods.Count; i++) 
            {
                var m = _foundMethods[i];
                if (GUILayout.Button($"{m.DeclaringType.Name}.{m.Name}")) 
                {
                    SelectMethod(m);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Right: IL View
            GUILayout.BeginVertical();
            if (_selectedMethod != null) 
            {
                GUILayout.Label($"Selected: {_selectedMethod.DeclaringType.Name}.{_selectedMethod.Name}", GUI.skin.box);
                if (!string.IsNullOrEmpty(_statusMessage)) GUILayout.Label(_statusMessage);
                
                _scrollIL = GUILayout.BeginScrollView(_scrollIL, GUI.skin.box);
                if (_displayedIL != null && _displayedIL.Count > 0) 
                {
                    for (int i = 0; i < _displayedIL.Count; i++) 
                    {
                        var instr = _displayedIL[i];
                        GUILayout.Label($"{i:D3}: {instr}");
                    }
                }
                else
                {
                    GUILayout.Label("No instructions available.");
                }
                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("Select a method to view IL.");
                if (!string.IsNullOrEmpty(_statusMessage)) GUILayout.Label(_statusMessage);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void SearchMethods() 
        {
            _foundMethods.Clear();
            _selectedMethod = null;
            _displayedIL = null;
            _statusMessage = "Searching...";

            try 
            {
                // Simple search logic
                // Try to find assembly
                Assembly asm = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name.IndexOf(_typeFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        asm = a;
                        break;
                    }
                }

                if (asm == null)
                {
                    // Assume Assembly-CSharp if mostly matching type name?
                    // Or try to load by name
                    try { asm = Assembly.Load(_typeFilter); } catch {}
                }

                if (asm != null)
                {
                    _statusMessage = $"Scanning {asm.GetName().Name}...";
                    var types = asm.GetTypes();
                    foreach (var t in types) 
                    {
                         // Optimization: Skip valid types if typeFilter specifies name
                         // If _typeFilter matches assembly, we search all types? Too many.
                         // Let's assume _typeFilter is primarily for Type Name if assembly matches default.
                         
                         bool typeMatch = t.Name.IndexOf(_typeFilter, StringComparison.OrdinalIgnoreCase) >= 0 || t.FullName.IndexOf(_typeFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                         // If user typed 'Assembly-CSharp', they match everything?
                         if (_typeFilter == "Assembly-CSharp" || typeMatch) 
                         {
                            var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                            foreach (var m in methods) 
                            {
                                if (string.IsNullOrEmpty(_methodFilter) || m.Name.IndexOf(_methodFilter, StringComparison.OrdinalIgnoreCase) >= 0) 
                                {
                                    _foundMethods.Add(m);
                                    if (_foundMethods.Count > 100) 
                                    {
                                        _statusMessage = "Found 100+ methods. Refine search.";
                                        return;
                                    }
                                }
                            }
                         }
                    }
                    _statusMessage = $"Found {_foundMethods.Count} methods.";
                }
                else
                {
                     _statusMessage = "Assembly/Type not found. Try 'Assembly-CSharp' or specific type name.";
                }
            } 
            catch (Exception ex) 
            {
                _statusMessage = "Error: " + ex.Message;
            }
        }

        private void SelectMethod(MethodBase method) 
        {
            _selectedMethod = method;
            _statusMessage = "Analyzing patches...";
            
            try 
            {
                // Get Original Instructions
                var patchInfo = HarmonyLib.Harmony.GetPatchInfo(method);
                List<CodeInstruction> codes = null;
                
                // Get original
                // Harmony 2.x
                var originals = PatchProcessor.GetOriginalInstructions(method);
                if (originals != null) codes = originals.ToList();
                
                if (patchInfo != null) 
                {
                    _statusMessage = $"Patched by: {string.Join(", ", patchInfo.Owners.ToArray())}. Transpilers: {patchInfo.Transpilers.Count}";
                    
                    // Can we simulate?
                    // We can try to run the transpilers on the codes
                    if (patchInfo.Transpilers.Count > 0 && codes != null)
                    {
                        // To simulate, we need instances of transpiler methods.
                        // Harmony stores them as Patch objects with MethodInfo.
                        // We can manually invoke them.
                        
                        foreach (var tr in patchInfo.Transpilers)
                        {
                            try
                            {
                                var tMethod = tr.PatchMethod;
                                // Transpilers usually return IEnumerable<CodeInstruction> and take IEnumerable<CodeInstruction> (and maybe ILGenerator)
                                // We can try to invoke
                                var parameters = tMethod.GetParameters();
                                object[] args = new object[parameters.Length];
                                for (int i=0; i<parameters.Length; i++)
                                {
                                    var pType = parameters[i].ParameterType;
                                    if (typeof(IEnumerable<CodeInstruction>).IsAssignableFrom(pType))
                                        args[i] = codes;
                                    else if (pType == typeof(ILGenerator))
                                        args[i] = null; // We don't have a generator for simulation, usually. This might crash some transpilers.
                                    else if (pType == typeof(MethodBase))
                                        args[i] = method;
                                }

                                var result = tMethod.Invoke(null, args) as IEnumerable<CodeInstruction>;
                                if (result != null)
                                    codes = result.ToList();
                            }
                            catch (Exception ex)
                            {
                                _statusMessage += $"\nSimulation failed for {tr.owner}: {ex.Message}";
                            }
                        }
                    }
                } 
                else 
                {
                    _statusMessage = "No patches (Original IL).";
                }
                
                _displayedIL = codes ?? new List<CodeInstruction>();
            } 
            catch (Exception ex) 
            {
                _statusMessage = "Error getting IL: " + ex.Message;
                _displayedIL = null;
            }
        }
    }
}
