using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex.Modules.Reference
{
    public sealed class ReferenceModule
    {
        private string _assemblyFilter = string.Empty;
        private string _typeName = string.Empty;
        private string _methodName = string.Empty;
        private bool _ignoreCache;
        private Vector2 _assemblyScroll = Vector2.zero;
        private Vector2 _typeScroll = Vector2.zero;
        private Vector2 _methodScroll = Vector2.zero;
        private Vector2 _previewScroll = Vector2.zero;
        private readonly List<ReferenceAssemblyDescriptor> _assemblies = new List<ReferenceAssemblyDescriptor>();
        private readonly List<ReferenceTypeDescriptor> _types = new List<ReferenceTypeDescriptor>();
        private readonly List<ReferenceMemberDescriptor> _members = new List<ReferenceMemberDescriptor>();
        private ReferenceAssemblyDescriptor _selectedAssembly;
        private ReferenceTypeDescriptor _selectedType;
        private readonly GUIStyle _pathStyle = new GUIStyle();
        private readonly GUIStyle _xmlDocStyle = new GUIStyle();

        public ReferenceModule()
        {
            _pathStyle.wordWrap = true;
            _pathStyle.normal.textColor = new Color(0.82f, 0.84f, 0.9f, 1f);
            _xmlDocStyle.wordWrap = true;
            _xmlDocStyle.normal.textColor = new Color(0.92f, 0.92f, 0.94f, 1f);
        }

        public void Draw(ISourceReferenceService sourceReferenceService, IReferenceCatalogService referenceCatalogService, IDocumentService documentService, CortexShellState state)
        {
            GUILayout.BeginVertical();
            EnsureAssembliesLoaded(referenceCatalogService, state);
            CortexIdeLayout.DrawTwoPane(
                420f,
                340f,
                delegate { DrawBrowserPane(sourceReferenceService, referenceCatalogService, state); },
                delegate { DrawPreviewPane(documentService, state); });
            GUILayout.EndVertical();
        }

        private void DrawBrowserPane(ISourceReferenceService sourceReferenceService, IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Assembly", GUILayout.Width(58f));
            _assemblyFilter = GUILayout.TextField(_assemblyFilter, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            {
                ReloadAssemblies(referenceCatalogService, state);
            }
            GUILayout.EndHorizontal();
            _assemblyScroll = GUILayout.BeginScrollView(_assemblyScroll, GUI.skin.box, GUILayout.Height(160f));
            for (var i = 0; i < _assemblies.Count; i++)
            {
                var assembly = _assemblies[i];
                var label = assembly.DisplayName + "\n" + assembly.AssemblyPath;
                if (!MatchesText(label, _assemblyFilter))
                {
                    continue;
                }

                if (GUILayout.Button(label, GUILayout.MinHeight(38f)))
                {
                    _selectedAssembly = assembly;
                    _selectedType = null;
                    LoadTypes(referenceCatalogService, assembly);
                    state.StatusMessage = "Selected assembly " + assembly.DisplayName;
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
                if (!MatchesText(type.FullName, _typeName))
                {
                    continue;
                }

                if (GUILayout.Button(type.DisplayName, GUILayout.ExpandWidth(true)))
                {
                    _selectedType = type;
                    LoadMembers(referenceCatalogService, type);
                    state.StatusMessage = "Selected type " + type.DisplayName;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Method", GUILayout.Width(58f));
            _methodName = GUILayout.TextField(_methodName, GUILayout.ExpandWidth(true));
            _ignoreCache = GUILayout.Toggle(_ignoreCache, "Ignore Cache", GUILayout.Width(110f));
            GUILayout.EndHorizontal();
            if (_selectedType != null && GUILayout.Button("Decompile Full Type", GUILayout.Width(160f)))
            {
                DecompileType(sourceReferenceService, state, _selectedType);
            }
            _methodScroll = GUILayout.BeginScrollView(_methodScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            for (var i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (!MatchesText(member.DisplayName, _methodName))
                {
                    continue;
                }

                if (GUILayout.Button(member.DisplayName, GUILayout.ExpandWidth(true)))
                {
                    DecompileMethod(sourceReferenceService, state, member);
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
            GUILayout.Label(_selectedAssembly != null ? _selectedAssembly.AssemblyPath : string.Empty, _pathStyle);
            GUILayout.Label("Type:", GUILayout.Width(70f));
            GUILayout.Label(_selectedType != null ? _selectedType.DisplayName : string.Empty, _pathStyle);
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
            GUILayout.Label("Generated Source: " + (!string.IsNullOrEmpty(state.LastReferenceResult.CachePath) ? "Yes" : "No"));
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

        private void EnsureAssembliesLoaded(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            if (_assemblies.Count == 0)
            {
                ReloadAssemblies(referenceCatalogService, state);
            }
        }

        private void ReloadAssemblies(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            _assemblies.Clear();
            _types.Clear();
            _members.Clear();
            _selectedAssembly = null;
            _selectedType = null;

            var assemblies = referenceCatalogService != null ? referenceCatalogService.GetAssemblies(state.Settings != null ? state.Settings.ManagedAssemblyRootPath : string.Empty) : new List<ReferenceAssemblyDescriptor>();
            for (var i = 0; i < assemblies.Count; i++)
            {
                _assemblies.Add(assemblies[i]);
            }
        }

        private void LoadTypes(IReferenceCatalogService referenceCatalogService, ReferenceAssemblyDescriptor assembly)
        {
            _types.Clear();
            _members.Clear();
            _selectedType = null;
            if (assembly == null || referenceCatalogService == null)
            {
                return;
            }

            var types = referenceCatalogService.GetTypes(assembly.AssemblyPath);
            for (var i = 0; i < types.Count; i++)
            {
                _types.Add(types[i]);
            }
        }

        private void LoadMembers(IReferenceCatalogService referenceCatalogService, ReferenceTypeDescriptor type)
        {
            _members.Clear();
            if (type == null || referenceCatalogService == null)
            {
                return;
            }

            var members = referenceCatalogService.GetMembers(type.AssemblyPath, type.FullName);
            for (var i = 0; i < members.Count; i++)
            {
                _members.Add(members[i]);
            }
        }

        private void DecompileMethod(ISourceReferenceService sourceReferenceService, CortexShellState state, ReferenceMemberDescriptor member)
        {
            if (member == null || sourceReferenceService == null)
            {
                return;
            }

            state.LastReferenceResult = sourceReferenceService.GetSource(new DecompilerRequest
            {
                AssemblyPath = member.AssemblyPath,
                MetadataToken = member.MetadataToken,
                IgnoreCache = _ignoreCache,
                EntityKind = DecompilerEntityKind.Method
            });
            state.StatusMessage = "Decompiled " + member.DeclaringTypeName + "." + member.DisplayName;
        }

        private void DecompileType(ISourceReferenceService sourceReferenceService, CortexShellState state, ReferenceTypeDescriptor type)
        {
            if (type == null || sourceReferenceService == null)
            {
                return;
            }

            state.LastReferenceResult = sourceReferenceService.GetSource(new DecompilerRequest
            {
                AssemblyPath = type.AssemblyPath,
                MetadataToken = type.MetadataToken,
                IgnoreCache = _ignoreCache,
                EntityKind = DecompilerEntityKind.Type
            });
            state.StatusMessage = "Decompiled type " + type.DisplayName;
        }

        private static bool MatchesText(string value, string filter)
        {
            return string.IsNullOrEmpty(filter) || (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
