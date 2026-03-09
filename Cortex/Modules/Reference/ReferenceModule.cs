using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex.Modules.Reference
{
    public sealed class ReferenceModule
    {
        private sealed class MethodItem
        {
            public string Label;
            public MethodBase Method;
        }

        private string _assemblyFilter = string.Empty;
        private string _typeName = string.Empty;
        private string _methodName = string.Empty;
        private bool _ignoreCache;
        private Vector2 _assemblyScroll = Vector2.zero;
        private Vector2 _typeScroll = Vector2.zero;
        private Vector2 _methodScroll = Vector2.zero;
        private Vector2 _previewScroll = Vector2.zero;
        private readonly List<Assembly> _assemblies = new List<Assembly>();
        private readonly List<Type> _types = new List<Type>();
        private readonly List<MethodItem> _methods = new List<MethodItem>();
        private string _selectedAssemblyPath = string.Empty;
        private string _selectedTypeName = string.Empty;
        private readonly GUIStyle _pathStyle = new GUIStyle();
        private readonly GUIStyle _xmlDocStyle = new GUIStyle();

        public ReferenceModule()
        {
            _pathStyle.wordWrap = true;
            _pathStyle.normal.textColor = new Color(0.82f, 0.84f, 0.9f, 1f);
            _xmlDocStyle.wordWrap = true;
            _xmlDocStyle.normal.textColor = new Color(0.92f, 0.92f, 0.94f, 1f);
        }

        public void Draw(ISourceReferenceService sourceReferenceService, IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginVertical();
            EnsureAssembliesLoaded(state);
            CortexIdeLayout.DrawTwoPane(
                420f,
                340f,
                delegate { DrawBrowserPane(sourceReferenceService, state); },
                delegate { DrawPreviewPane(documentService, state); });
            GUILayout.EndVertical();
        }

        private void DrawBrowserPane(ISourceReferenceService sourceReferenceService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Assembly", GUILayout.Width(58f));
            _assemblyFilter = GUILayout.TextField(_assemblyFilter, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            {
                ReloadAssemblies(state);
            }
            GUILayout.EndHorizontal();
            _assemblyScroll = GUILayout.BeginScrollView(_assemblyScroll, GUI.skin.box, GUILayout.Height(160f));
            for (var i = 0; i < _assemblies.Count; i++)
            {
                var assembly = _assemblies[i];
                var path = SafeAssemblyPath(assembly);
                var label = assembly.GetName().Name + "\n" + path;
                if (!MatchesText(label, _assemblyFilter))
                {
                    continue;
                }

                if (GUILayout.Button(label, GUILayout.MinHeight(38f)))
                {
                    _selectedAssemblyPath = path;
                    LoadTypes(assembly);
                    state.StatusMessage = "Selected assembly " + assembly.GetName().Name;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Type", GUILayout.Width(58f));
            _typeName = GUILayout.TextField(_typeName, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            _typeScroll = GUILayout.BeginScrollView(_typeScroll, GUI.skin.box, GUILayout.Height(180f));
            for (var i = 0; i < _types.Count; i++)
            {
                var type = _types[i];
                if (!MatchesText(type.FullName ?? type.Name, _typeName))
                {
                    continue;
                }

                if (GUILayout.Button(type.FullName ?? type.Name, GUILayout.ExpandWidth(true)))
                {
                    _selectedTypeName = type.FullName ?? type.Name;
                    LoadMethods(type);
                    state.StatusMessage = "Selected type " + _selectedTypeName;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Method", GUILayout.Width(58f));
            _methodName = GUILayout.TextField(_methodName, GUILayout.ExpandWidth(true));
            _ignoreCache = GUILayout.Toggle(_ignoreCache, "Ignore Cache", GUILayout.Width(110f));
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_selectedTypeName) && GUILayout.Button("Decompile Full Type", GUILayout.Width(160f)))
            {
                var type = FindSelectedType();
                if (type != null)
                {
                    DecompileType(sourceReferenceService, state, type);
                }
            }
            _methodScroll = GUILayout.BeginScrollView(_methodScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            for (var i = 0; i < _methods.Count; i++)
            {
                var item = _methods[i];
                if (!MatchesText(item.Label, _methodName))
                {
                    continue;
                }

                if (GUILayout.Button(item.Label, GUILayout.ExpandWidth(true)))
                {
                    DecompileMethod(sourceReferenceService, state, item.Method);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawPreviewPane(IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label("Decompiler Preview");
            if (state.LastReferenceResult == null)
            {
                GUILayout.Label("Browse a loaded assembly, type, and method to generate decompiled source for game or mod code.");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(state.LastReferenceResult.StatusMessage ?? string.Empty);
            GUILayout.Label("Assembly:", GUILayout.Width(70f));
            GUILayout.Label(_selectedAssemblyPath, _pathStyle);
            GUILayout.Label("Type:", GUILayout.Width(70f));
            GUILayout.Label(_selectedTypeName, _pathStyle);
            if (!string.IsNullOrEmpty(state.LastReferenceResult.ResolvedMemberDisplayName))
            {
                GUILayout.Label("Member:", GUILayout.Width(70f));
                GUILayout.Label(state.LastReferenceResult.ResolvedMemberDisplayName, _pathStyle);
            }
            if (!string.IsNullOrEmpty(state.LastReferenceResult.XmlDocumentationPath))
            {
                GUILayout.Label("XML:", GUILayout.Width(70f));
                GUILayout.Label(state.LastReferenceResult.XmlDocumentationPath, _pathStyle);
            }
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(state.LastReferenceResult.CachePath) && File.Exists(state.LastReferenceResult.CachePath) &&
                GUILayout.Button("Open Cached Source", GUILayout.Width(150f)))
            {
                CortexModuleUtil.OpenDocument(documentService, state, state.LastReferenceResult.CachePath, 1);
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                state.StatusMessage = "Opened decompiled cache file.";
            }
            if (GUILayout.Button("Clear", GUILayout.Width(80f)))
            {
                state.LastReferenceResult = null;
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(state.LastReferenceResult.XmlDocumentationText))
            {
                CortexIdeLayout.DrawGroup("XML Documentation", delegate
                {
                    GUILayout.Label(state.LastReferenceResult.XmlDocumentationText, _xmlDocStyle);
                });
            }
            _previewScroll = GUILayout.BeginScrollView(_previewScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.TextArea(state.LastReferenceResult.SourceText ?? string.Empty, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void EnsureAssembliesLoaded(CortexShellState state)
        {
            if (_assemblies.Count == 0)
            {
                ReloadAssemblies(state);
            }
        }

        private void ReloadAssemblies(CortexShellState state)
        {
            _assemblies.Clear();
            _types.Clear();
            _methods.Clear();
            _selectedAssemblyPath = string.Empty;
            _selectedTypeName = string.Empty;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var managedRoot = state.Settings != null ? state.Settings.ManagedAssemblyRootPath : string.Empty;
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                var path = SafeAssemblyPath(assembly);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(managedRoot) &&
                    path.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _assemblies.Insert(0, assembly);
                    continue;
                }

                _assemblies.Add(assembly);
            }

            _assemblies.Sort(delegate(Assembly left, Assembly right)
            {
                return string.Compare(left.GetName().Name, right.GetName().Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void LoadTypes(Assembly assembly)
        {
            _types.Clear();
            _methods.Clear();
            _selectedTypeName = string.Empty;
            if (assembly == null)
            {
                return;
            }

            try
            {
                var types = assembly.GetTypes();
                for (var i = 0; i < types.Length; i++)
                {
                    if (!types[i].IsNested)
                    {
                        _types.Add(types[i]);
                    }
                }
            }
            catch
            {
            }
        }

        private void LoadMethods(Type type)
        {
            _methods.Clear();
            if (type == null)
            {
                return;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            for (var i = 0; i < methods.Length; i++)
            {
                _methods.Add(new MethodItem
                {
                    Method = methods[i],
                    Label = BuildMethodLabel(methods[i])
                });
            }
        }

        private void DecompileMethod(ISourceReferenceService sourceReferenceService, CortexShellState state, MethodBase method)
        {
            if (method == null)
            {
                return;
            }

            state.LastReferenceResult = sourceReferenceService.GetSource(new DecompilerRequest
            {
                AssemblyPath = method.Module.Assembly.Location,
                MetadataToken = method.MetadataToken,
                IgnoreCache = _ignoreCache,
                EntityKind = DecompilerEntityKind.Method
            });
            state.StatusMessage = "Decompiled " + method.DeclaringType.FullName + "." + method.Name;
        }

        private void DecompileType(ISourceReferenceService sourceReferenceService, CortexShellState state, Type type)
        {
            state.LastReferenceResult = sourceReferenceService.GetSource(new DecompilerRequest
            {
                AssemblyPath = type.Assembly.Location,
                MetadataToken = type.MetadataToken,
                IgnoreCache = _ignoreCache,
                EntityKind = DecompilerEntityKind.Type
            });
            state.StatusMessage = "Decompiled type " + (type.FullName ?? type.Name);
        }

        private Type FindSelectedType()
        {
            for (var i = 0; i < _types.Count; i++)
            {
                var type = _types[i];
                if (string.Equals(type.FullName ?? type.Name, _selectedTypeName, StringComparison.Ordinal))
                {
                    return type;
                }
            }

            return null;
        }

        private static bool MatchesText(string value, string filter)
        {
            return string.IsNullOrEmpty(filter) || (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SafeAssemblyPath(Assembly assembly)
        {
            try
            {
                return assembly != null ? assembly.Location : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildMethodLabel(MethodBase method)
        {
            if (method == null)
            {
                return "Unknown";
            }

            var parameters = method.GetParameters();
            var builder = new StringBuilder();
            builder.Append(method.Name);
            builder.Append('(');
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(parameters[i].ParameterType.Name);
                builder.Append(' ');
                builder.Append(parameters[i].Name);
            }
            builder.Append(')');
            return builder.ToString();
        }
    }
}
