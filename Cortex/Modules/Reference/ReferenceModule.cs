using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Services.Navigation;
using UnityEngine;

namespace Cortex.Modules.Reference
{
    public sealed class ReferenceModule
    {
        private const float AssemblyRowHeight = 44f;
        private const float TypeRowHeight = 44f;
        private const float MemberRowHeight = 46f;

        private string _appliedTheme = string.Empty;
        private string _assemblyFilter = string.Empty;
        private string _typeName = string.Empty;
        private string _memberFilter = string.Empty;
        private bool _ignoreCache;
        private Vector2 _assemblyScroll = Vector2.zero;
        private Vector2 _typeScroll = Vector2.zero;
        private Vector2 _memberScroll = Vector2.zero;
        private Vector2 _previewScroll = Vector2.zero;
        private readonly List<ReferenceAssemblyDescriptor> _assemblies = new List<ReferenceAssemblyDescriptor>();
        private readonly List<ReferenceTypeDescriptor> _types = new List<ReferenceTypeDescriptor>();
        private readonly List<ReferenceMemberDescriptor> _members = new List<ReferenceMemberDescriptor>();
        private readonly IHoverTooltipRenderer _hoverTooltipRenderer;
        private ReferenceAssemblyDescriptor _selectedAssembly;
        private ReferenceTypeDescriptor _selectedType;
        private ReferenceMemberDescriptor _selectedMember;
        private GUIStyle _summaryStyle;
        private GUIStyle _summaryCaptionStyle;
        private GUIStyle _filterBoxStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _selectedRowStyle;
        private GUIStyle _rowTitleStyle;
        private GUIStyle _rowSubtitleStyle;
        private GUIStyle _rowMetaStyle;
        private GUIStyle _detailLabelStyle;
        private GUIStyle _detailValueStyle;
        private GUIStyle _emptyStateStyle;
        private GUIStyle _previewStyle;
        private GUIStyle _actionButtonStyle;
        private GUIStyle _tooltipStyle;
        private Texture2D _summaryBackground;
        private Texture2D _filterBackground;
        private Texture2D _rowBackground;
        private Texture2D _hoverRowBackground;
        private Texture2D _selectedRowBackground;
        private Texture2D _previewBackground;
        private Texture2D _rowBorder;
        private Texture2D _selectedRowBorder;
        private Texture2D _tooltipBackground;

        public ReferenceModule(IHoverTooltipRenderer hoverTooltipRenderer)
        {
            _hoverTooltipRenderer = hoverTooltipRenderer;
        }

        public void Draw(IReferenceCatalogService referenceCatalogService, ICortexNavigationService navigationService, CortexShellState state)
        {
            EnsureStyles();
            EnsureAssembliesLoaded(referenceCatalogService, state);
            if (_hoverTooltipRenderer != null)
            {
                _hoverTooltipRenderer.ResetTextTooltip();
            }

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            CortexIdeLayout.DrawTwoPane(
                432f,
                360f,
                delegate { DrawBrowserPane(referenceCatalogService, navigationService, state); },
                delegate { DrawPreviewPane(navigationService, state); });
            GUILayout.EndVertical();

            if (_hoverTooltipRenderer != null)
            {
                _hoverTooltipRenderer.DrawTextTooltip(BuildTooltipThemePalette());
            }
        }

        private void DrawBrowserPane(IReferenceCatalogService referenceCatalogService, ICortexNavigationService navigationService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawSelectionSummary();

            CortexIdeLayout.DrawGroup("Assemblies", delegate
            {
                DrawAssemblyToolbar(referenceCatalogService, state);
                _assemblyScroll = GUILayout.BeginScrollView(_assemblyScroll, GUI.skin.box, GUILayout.MinHeight(170f), GUILayout.Height(210f));
                DrawAssemblyList(referenceCatalogService, state);
                GUILayout.EndScrollView();
            }, GUILayout.ExpandWidth(true));

            GUILayout.Space(6f);
            CortexIdeLayout.DrawGroup("Types", delegate
            {
                DrawTypeToolbar();
                _typeScroll = GUILayout.BeginScrollView(_typeScroll, GUI.skin.box, GUILayout.MinHeight(150f), GUILayout.Height(190f));
                DrawTypeList(referenceCatalogService, state);
                GUILayout.EndScrollView();
            }, GUILayout.ExpandWidth(true));

            GUILayout.Space(6f);
            CortexIdeLayout.DrawGroup("Members", delegate
            {
                DrawMemberToolbar();
                GUILayout.BeginHorizontal();
                var previousEnabled = GUI.enabled;
                GUI.enabled = _selectedMember != null;
                if (GUILayout.Button("Decompile Member", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(132f)))
                {
                    DecompileMember(navigationService, state, _selectedMember);
                }

                GUI.enabled = _selectedType != null;
                if (GUILayout.Button("Decompile Type", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(120f)))
                {
                    DecompileType(navigationService, state, _selectedType);
                }

                GUI.enabled = previousEnabled;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                _memberScroll = GUILayout.BeginScrollView(_memberScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
                DrawMemberList(state);
                GUILayout.EndScrollView();
            }, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();
        }

        private void DrawPreviewPane(ICortexNavigationService navigationService, CortexShellState state)
        {
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            if (state.Documents.ActiveDocument != null)
            {
                CortexIdeLayout.DrawGroup("Active Source", delegate
                {
                    GUILayout.Label(CortexModuleUtil.GetDocumentDisplayName(state.Documents.ActiveDocument), _detailLabelStyle ?? GUI.skin.label);
                    GUILayout.Label(state.Documents.ActiveDocument.FilePath ?? string.Empty, _detailValueStyle ?? GUI.skin.label);
                });
                GUILayout.Space(6f);
            }

            CortexIdeLayout.DrawGroup("Reference Details", delegate
            {
                DrawMetadataRow("Assembly", _selectedAssembly != null ? _selectedAssembly.DisplayName : "None selected");
                DrawMetadataRow("Path", _selectedAssembly != null ? _selectedAssembly.AssemblyPath : "Select an assembly to browse its types.");
                DrawMetadataRow("Type", BuildSelectedTypeLabel());
                DrawMetadataRow("Member", BuildSelectedMemberLabel());
                GUILayout.Space(4f);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Ignore Cache", GUILayout.Width(90f));
                _ignoreCache = GUILayout.Toggle(_ignoreCache, _ignoreCache ? "On" : "Off", GUILayout.Width(54f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                var previousEnabled = GUI.enabled;
                GUI.enabled = _selectedMember != null;
                if (GUILayout.Button("Decompile Selected Member", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(188f)))
                {
                    DecompileMember(navigationService, state, _selectedMember);
                }

                GUI.enabled = _selectedType != null;
                if (GUILayout.Button("Decompile Selected Type", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(170f)))
                {
                    DecompileType(navigationService, state, _selectedType);
                }

                GUI.enabled = state.LastReferenceResult != null &&
                    !string.IsNullOrEmpty(state.LastReferenceResult.CachePath) &&
                    File.Exists(state.LastReferenceResult.CachePath);
                if (GUILayout.Button("Open Decompiled Source", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(178f)))
                {
                    if (navigationService != null)
                    {
                        if (_selectedMember != null)
                        {
                            navigationService.OpenDecompilerMethodTarget(
                                state,
                                _selectedMember.AssemblyPath,
                                _selectedMember.MetadataToken,
                                string.Empty,
                                _selectedMember.DeclaringTypeName,
                                string.Empty,
                                _ignoreCache,
                                "Opened decompiled source.",
                                "Could not open decompiled source.");
                        }
                        else
                        {
                            navigationService.OpenDecompilerResult(state, state.LastReferenceResult, "Opened decompiled cache file.", "Could not open decompiled cache file.");
                        }
                    }
                }

                GUI.enabled = state.LastReferenceResult != null;
                if (GUILayout.Button("Clear Result", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(108f)))
                {
                    state.LastReferenceResult = null;
                }

                GUI.enabled = previousEnabled;
                GUILayout.EndHorizontal();
            });

            GUILayout.Space(6f);
            CortexIdeLayout.DrawGroup("Decompiler Result", delegate
            {
                if (state.LastReferenceResult == null)
                {
                    GUILayout.Label("Pick an assembly, select a type or member, then decompile explicitly. Results stay here so you can inspect the cache path, XML docs, and generated source.", _emptyStateStyle ?? GUI.skin.label);
                    return;
                }

                GUILayout.Label(state.LastReferenceResult.StatusMessage ?? "Decompiler request completed.", _detailLabelStyle ?? GUI.skin.label);
                DrawMetadataRow("Resolved", !string.IsNullOrEmpty(state.LastReferenceResult.ResolvedMemberDisplayName) ? state.LastReferenceResult.ResolvedMemberDisplayName : "Type output");
                DrawMetadataRow("XML File", !string.IsNullOrEmpty(state.LastReferenceResult.XmlDocumentationPath) ? state.LastReferenceResult.XmlDocumentationPath : "None");
                DrawMetadataRow("Cache File", !string.IsNullOrEmpty(state.LastReferenceResult.CachePath) ? state.LastReferenceResult.CachePath : "Not generated");
                if (!string.IsNullOrEmpty(state.LastReferenceResult.XmlDocumentationText))
                {
                    GUILayout.Space(6f);
                    GUILayout.Label(state.LastReferenceResult.XmlDocumentationText, _detailValueStyle ?? GUI.skin.label);
                }
            });

            GUILayout.Space(6f);
            CortexIdeLayout.DrawGroup("Source Preview", delegate
            {
                if (state.LastReferenceResult == null || string.IsNullOrEmpty(state.LastReferenceResult.SourceText))
                {
                    GUILayout.Label("No decompiled source is loaded yet.", _emptyStateStyle ?? GUI.skin.label);
                    return;
                }

                _previewScroll = GUILayout.BeginScrollView(_previewScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
                GUILayout.TextArea(state.LastReferenceResult.SourceText ?? string.Empty, _previewStyle ?? GUI.skin.textArea, GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
            }, GUILayout.ExpandHeight(true));

            GUILayout.EndVertical();
        }

        private void DrawSelectionSummary()
        {
            GUILayout.BeginVertical(_summaryStyle ?? GUI.skin.box);
            GUILayout.Label(BuildSelectionPath(), _detailLabelStyle ?? GUI.skin.label);
            GUILayout.Label(BuildSelectionCaption(), _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void DrawAssemblyToolbar(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(28f));
            GUILayout.Label("Search", GUILayout.Width(52f));
            _assemblyFilter = GUILayout.TextField(_assemblyFilter ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_assemblyFilter) && GUILayout.Button("x", GUILayout.Width(20f)))
            {
                _assemblyFilter = string.Empty;
            }

            GUILayout.Space(6f);
            GUILayout.Label(CountMatchingAssemblies() + " shown", _rowMetaStyle ?? GUI.skin.label, GUILayout.Width(68f));
            if (GUILayout.Button("Refresh", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(72f)))
            {
                ReloadAssemblies(referenceCatalogService, state);
                state.StatusMessage = "Reference assemblies refreshed.";
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTypeToolbar()
        {
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(28f));
            GUILayout.Label("Filter", GUILayout.Width(52f));
            _typeName = GUILayout.TextField(_typeName ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_typeName) && GUILayout.Button("x", GUILayout.Width(20f)))
            {
                _typeName = string.Empty;
            }

            GUILayout.Space(6f);
            GUILayout.Label(CountMatchingTypes() + " shown", _rowMetaStyle ?? GUI.skin.label, GUILayout.Width(68f));
            GUILayout.EndHorizontal();
        }

        private void DrawMemberToolbar()
        {
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(28f));
            GUILayout.Label("Filter", GUILayout.Width(52f));
            _memberFilter = GUILayout.TextField(_memberFilter ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_memberFilter) && GUILayout.Button("x", GUILayout.Width(20f)))
            {
                _memberFilter = string.Empty;
            }

            GUILayout.Space(6f);
            GUILayout.Label(CountMatchingMembers() + " shown", _rowMetaStyle ?? GUI.skin.label, GUILayout.Width(68f));
            GUILayout.EndHorizontal();
        }

        private void DrawAssemblyList(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            var visibleCount = 0;
            for (var i = 0; i < _assemblies.Count; i++)
            {
                var assembly = _assemblies[i];
                if (!MatchesText(assembly.DisplayName + " " + assembly.AssemblyPath, _assemblyFilter))
                {
                    continue;
                }

                visibleCount++;
                var location = Path.GetDirectoryName(assembly.AssemblyPath) ?? string.Empty;
                var locationText = CompactPath(location, 56);
                if (DrawSelectableRow(
                    assembly.DisplayName,
                    locationText,
                    Path.GetFileName(assembly.AssemblyPath),
                    ComposeTooltip(assembly.DisplayName, assembly.AssemblyPath),
                    IsSelectedAssembly(assembly),
                    AssemblyRowHeight))
                {
                    SelectAssembly(referenceCatalogService, assembly, state);
                }
            }

            if (visibleCount == 0)
            {
                GUILayout.Label("No assemblies match the current search.", _emptyStateStyle ?? GUI.skin.label);
            }
        }

        private void DrawTypeList(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            if (_selectedAssembly == null)
            {
                GUILayout.Label("Select an assembly to browse its exported and internal top-level types.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            var visibleCount = 0;
            for (var i = 0; i < _types.Count; i++)
            {
                var type = _types[i];
                if (!MatchesText(type.FullName, _typeName))
                {
                    continue;
                }

                visibleCount++;
                string shortName;
                string namespaceName;
                SplitTypeName(type.FullName, out shortName, out namespaceName);
                if (DrawSelectableRow(
                    shortName,
                    namespaceName,
                    FormatMetadataToken(type.MetadataToken),
                    ComposeTooltip(type.FullName, FormatMetadataToken(type.MetadataToken)),
                    IsSelectedType(type),
                    TypeRowHeight))
                {
                    SelectType(referenceCatalogService, type, state);
                }
            }

            if (visibleCount == 0)
            {
                GUILayout.Label("No types match the current filter.", _emptyStateStyle ?? GUI.skin.label);
            }
        }

        private void DrawMemberList(CortexShellState state)
        {
            if (_selectedType == null)
            {
                GUILayout.Label("Select a type to inspect its constructors and methods.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            var visibleCount = 0;
            for (var i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                var filterText = member.DisplayName + " " + member.DeclaringTypeName;
                if (!MatchesText(filterText, _memberFilter))
                {
                    continue;
                }

                visibleCount++;
                string memberTitle;
                string signature;
                SplitMemberDisplay(member.DisplayName, out memberTitle, out signature);
                var subtitle = ShortTypeName(member.DeclaringTypeName);
                if (!string.IsNullOrEmpty(signature))
                {
                    subtitle = subtitle + " " + signature;
                }

                if (DrawSelectableRow(
                    memberTitle,
                    subtitle,
                    FormatMetadataToken(member.MetadataToken),
                    ComposeTooltip(member.DeclaringTypeName + "." + member.DisplayName, FormatMetadataToken(member.MetadataToken)),
                    IsSelectedMember(member),
                    MemberRowHeight))
                {
                    _selectedMember = member;
                    state.StatusMessage = "Selected member " + member.DeclaringTypeName + "." + member.DisplayName;
                }
            }

            if (visibleCount == 0)
            {
                GUILayout.Label("No members match the current filter.", _emptyStateStyle ?? GUI.skin.label);
            }
        }

        private bool DrawSelectableRow(string title, string subtitle, string meta, string tooltipText, bool selected, float height)
        {
            var rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            var current = Event.current;
            var hovered = current != null && rect.Contains(current.mousePosition);
            var style = selected ? (_selectedRowStyle ?? GUI.skin.box) : (_rowStyle ?? GUI.skin.box);
            var clicked = GUI.Button(rect, GUIContent.none, style);
            if (hovered && !selected && _hoverRowBackground != null)
            {
                GUI.DrawTexture(rect, _hoverRowBackground);
            }

            DrawBorder(rect, selected ? _selectedRowBorder : _rowBorder, selected ? 2f : 1f);

            var titleRect = new Rect(rect.x + 10f, rect.y + 5f, Mathf.Max(40f, rect.width - 112f), 18f);
            var subtitleRect = new Rect(rect.x + 10f, rect.y + 22f, Mathf.Max(40f, rect.width - 112f), Mathf.Max(18f, rect.height - 24f));
            var metaRect = new Rect(rect.xMax - 94f, rect.y + 5f, 84f, 18f);

            GUI.Label(titleRect, title ?? string.Empty, _rowTitleStyle ?? GUI.skin.label);
            GUI.Label(subtitleRect, subtitle ?? string.Empty, _rowSubtitleStyle ?? GUI.skin.label);
            if (!string.IsNullOrEmpty(meta))
            {
                GUI.Label(metaRect, meta, _rowMetaStyle ?? GUI.skin.label);
            }

            if (hovered)
            {
                RegisterHoverTooltip(rect, tooltipText);
            }

            return clicked;
        }

        private void DrawMetadataRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", _detailLabelStyle ?? GUI.skin.label, GUILayout.Width(84f));
            var labelRect = GUILayoutUtility.GetLastRect();
            GUILayout.Label(value ?? string.Empty, _detailValueStyle ?? GUI.skin.label, GUILayout.ExpandWidth(true));
            var valueRect = GUILayoutUtility.GetLastRect();
            GUILayout.EndHorizontal();

            var current = Event.current;
            if (current != null)
            {
                if (!string.IsNullOrEmpty(label) && labelRect.Contains(current.mousePosition))
                {
                    RegisterHoverTooltip(labelRect, label);
                }

                if (!string.IsNullOrEmpty(value) && valueRect.Contains(current.mousePosition))
                {
                    RegisterHoverTooltip(valueRect, value);
                }
            }
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
            _selectedMember = null;

            var assemblies = referenceCatalogService != null
                ? referenceCatalogService.GetAssemblies(state.Settings != null ? state.Settings.ManagedAssemblyRootPath : string.Empty)
                : new List<ReferenceAssemblyDescriptor>();
            for (var i = 0; i < assemblies.Count; i++)
            {
                _assemblies.Add(assemblies[i]);
            }
        }

        private void SelectAssembly(IReferenceCatalogService referenceCatalogService, ReferenceAssemblyDescriptor assembly, CortexShellState state)
        {
            if (assembly == null)
            {
                return;
            }

            _selectedAssembly = assembly;
            _selectedType = null;
            _selectedMember = null;
            LoadTypes(referenceCatalogService, assembly);
            state.StatusMessage = "Selected assembly " + assembly.DisplayName;
        }

        private void LoadTypes(IReferenceCatalogService referenceCatalogService, ReferenceAssemblyDescriptor assembly)
        {
            _types.Clear();
            _members.Clear();
            _selectedType = null;
            _selectedMember = null;
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

        private void SelectType(IReferenceCatalogService referenceCatalogService, ReferenceTypeDescriptor type, CortexShellState state)
        {
            if (type == null)
            {
                return;
            }

            _selectedType = type;
            _selectedMember = null;
            LoadMembers(referenceCatalogService, type);
            state.StatusMessage = "Selected type " + type.DisplayName;
        }

        private void LoadMembers(IReferenceCatalogService referenceCatalogService, ReferenceTypeDescriptor type)
        {
            _members.Clear();
            _selectedMember = null;
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

        private void DecompileMember(ICortexNavigationService navigationService, CortexShellState state, ReferenceMemberDescriptor member)
        {
            if (member == null || navigationService == null)
            {
                return;
            }

            _selectedMember = member;
            int highlightedLine;
            var response = navigationService.RequestDecompilerMethodView(
                state,
                member.AssemblyPath,
                member.MetadataToken,
                string.Empty,
                member.DeclaringTypeName,
                string.Empty,
                _ignoreCache,
                out highlightedLine);
            if (response == null)
            {
                state.StatusMessage = "Could not decompile " + member.DeclaringTypeName + "." + member.DisplayName;
                return;
            }

            state.StatusMessage = "Decompiled " + member.DeclaringTypeName + "." + member.DisplayName;
        }

        private void DecompileType(ICortexNavigationService navigationService, CortexShellState state, ReferenceTypeDescriptor type)
        {
            if (type == null || navigationService == null)
            {
                return;
            }

            navigationService.RequestDecompilerSource(state, type.AssemblyPath, type.MetadataToken, DecompilerEntityKind.Type, _ignoreCache);
            state.StatusMessage = "Decompiled type " + type.DisplayName;
        }

        private void EnsureStyles()
        {
            var themeId = GUI.skin != null ? GUI.skin.name ?? string.Empty : string.Empty;
            if (string.Equals(_appliedTheme, themeId, StringComparison.Ordinal) && _rowStyle != null)
            {
                return;
            }

            _appliedTheme = themeId;
            var textColor = CortexIdeLayout.GetTextColor();
            var mutedColor = CortexIdeLayout.GetMutedTextColor();
            var accentColor = CortexIdeLayout.GetAccentColor();
            var surfaceColor = CortexIdeLayout.GetSurfaceColor();
            var headerColor = CortexIdeLayout.GetHeaderColor();
            var backgroundColor = CortexIdeLayout.GetBackgroundColor();

            _summaryBackground = MakeTex(CortexIdeLayout.Blend(headerColor, backgroundColor, 0.3f));
            _filterBackground = MakeTex(CortexIdeLayout.Blend(surfaceColor, backgroundColor, 0.45f));
            _rowBackground = MakeTex(CortexIdeLayout.Blend(backgroundColor, surfaceColor, 0.4f));
            _hoverRowBackground = MakeTex(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.46f));
            _selectedRowBackground = MakeTex(CortexIdeLayout.Blend(headerColor, accentColor, 0.28f));
            _previewBackground = MakeTex(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.35f));
            _rowBorder = MakeTex(CortexIdeLayout.Blend(CortexIdeLayout.GetBorderColor(), Color.white, 0.08f));
            _selectedRowBorder = MakeTex(accentColor);
            _tooltipBackground = MakeTex(CortexIdeLayout.Blend(backgroundColor, surfaceColor, 0.26f));

            _summaryStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_summaryStyle, _summaryBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_summaryStyle, textColor);
            _summaryStyle.padding = new RectOffset(10, 10, 8, 8);

            _summaryCaptionStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_summaryCaptionStyle, mutedColor);
            _summaryCaptionStyle.wordWrap = true;

            _filterBoxStyle = new GUIStyle(GUI.skin.textField);
            GuiStyleUtil.ApplyBackgroundToAllStates(_filterBoxStyle, _filterBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_filterBoxStyle, textColor);
            _filterBoxStyle.alignment = TextAnchor.MiddleLeft;

            _rowStyle = new GUIStyle(GUI.skin.button);
            GuiStyleUtil.ApplyBackgroundToAllStates(_rowStyle, _rowBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_rowStyle, mutedColor);
            _rowStyle.alignment = TextAnchor.UpperLeft;
            _rowStyle.padding = new RectOffset(0, 0, 0, 0);
            _rowStyle.margin = new RectOffset(0, 0, 0, 4);

            _selectedRowStyle = new GUIStyle(_rowStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_selectedRowStyle, _selectedRowBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_selectedRowStyle, textColor);

            _rowTitleStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_rowTitleStyle, textColor);
            _rowTitleStyle.fontStyle = FontStyle.Bold;
            _rowTitleStyle.clipping = TextClipping.Clip;

            _rowSubtitleStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_rowSubtitleStyle, mutedColor);
            _rowSubtitleStyle.clipping = TextClipping.Clip;

            _rowMetaStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_rowMetaStyle, accentColor);
            _rowMetaStyle.alignment = TextAnchor.UpperRight;
            _rowMetaStyle.clipping = TextClipping.Clip;

            _detailLabelStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_detailLabelStyle, textColor);
            _detailLabelStyle.fontStyle = FontStyle.Bold;
            _detailLabelStyle.wordWrap = true;

            _detailValueStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_detailValueStyle, mutedColor);
            _detailValueStyle.wordWrap = true;

            _emptyStateStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_emptyStateStyle, mutedColor);
            _emptyStateStyle.wordWrap = true;

            _previewStyle = new GUIStyle(GUI.skin.textArea);
            GuiStyleUtil.ApplyBackgroundToAllStates(_previewStyle, _previewBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_previewStyle, textColor);
            _previewStyle.wordWrap = false;

            _actionButtonStyle = new GUIStyle(GUI.skin.button);
            GuiStyleUtil.ApplyBackgroundToAllStates(_actionButtonStyle, MakeTex(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.5f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_actionButtonStyle, textColor);

            _tooltipStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_tooltipStyle, _tooltipBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipStyle, textColor);
            _tooltipStyle.wordWrap = true;
            _tooltipStyle.alignment = TextAnchor.UpperLeft;
            _tooltipStyle.padding = new RectOffset(10, 10, 8, 8);
            _tooltipStyle.margin = new RectOffset(0, 0, 0, 0);
        }

        private int CountMatchingAssemblies()
        {
            var count = 0;
            for (var i = 0; i < _assemblies.Count; i++)
            {
                if (MatchesText(_assemblies[i].DisplayName + " " + _assemblies[i].AssemblyPath, _assemblyFilter))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountMatchingTypes()
        {
            var count = 0;
            for (var i = 0; i < _types.Count; i++)
            {
                if (MatchesText(_types[i].FullName, _typeName))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountMatchingMembers()
        {
            var count = 0;
            for (var i = 0; i < _members.Count; i++)
            {
                if (MatchesText(_members[i].DisplayName + " " + _members[i].DeclaringTypeName, _memberFilter))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsSelectedAssembly(ReferenceAssemblyDescriptor assembly)
        {
            return _selectedAssembly != null &&
                assembly != null &&
                string.Equals(_selectedAssembly.AssemblyPath, assembly.AssemblyPath, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSelectedType(ReferenceTypeDescriptor type)
        {
            return _selectedType != null &&
                type != null &&
                string.Equals(_selectedType.FullName, type.FullName, StringComparison.Ordinal);
        }

        private bool IsSelectedMember(ReferenceMemberDescriptor member)
        {
            return _selectedMember != null &&
                member != null &&
                _selectedMember.MetadataToken == member.MetadataToken &&
                string.Equals(_selectedMember.DeclaringTypeName, member.DeclaringTypeName, StringComparison.Ordinal);
        }

        private string BuildSelectionPath()
        {
            var assemblyName = _selectedAssembly != null ? _selectedAssembly.DisplayName : "Assembly";
            var typeName = _selectedType != null ? ShortTypeName(_selectedType.FullName) : "Type";
            var memberName = _selectedMember != null ? _selectedMember.DisplayName : "Member";
            return assemblyName + "  >  " + typeName + "  >  " + memberName;
        }

        private string BuildSelectionCaption()
        {
            if (_selectedAssembly == null)
            {
                return "Browse loaded managed assemblies, then narrow down to a type and member before decompiling.";
            }

            if (_selectedType == null)
            {
                return "Assembly selected. Pick a type to inspect its members and decompile a focused target.";
            }

            if (_selectedMember == null)
            {
                return "Type selected. Pick a member to inspect its signature or decompile just that method.";
            }

            return "Selected " + _selectedMember.DeclaringTypeName + "." + _selectedMember.DisplayName + ".";
        }

        private string BuildSelectedTypeLabel()
        {
            return _selectedType != null ? _selectedType.FullName : "No type selected";
        }

        private string BuildSelectedMemberLabel()
        {
            return _selectedMember != null ? _selectedMember.DisplayName : "No member selected";
        }

        private static string CompactPath(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            var tailLength = Mathf.Max(18, maxLength / 2);
            var headLength = Mathf.Max(12, maxLength - tailLength - 3);
            if (value.Length <= headLength + tailLength + 3)
            {
                return value;
            }

            return value.Substring(0, headLength) + "..." + value.Substring(value.Length - tailLength);
        }

        private static string ShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return "Unknown";
            }

            var separatorIndex = fullName.LastIndexOf('.');
            return separatorIndex >= 0 && separatorIndex < fullName.Length - 1
                ? fullName.Substring(separatorIndex + 1)
                : fullName;
        }

        private static void SplitTypeName(string fullName, out string shortName, out string namespaceName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                shortName = "Unknown";
                namespaceName = "(global namespace)";
                return;
            }

            var separatorIndex = fullName.LastIndexOf('.');
            if (separatorIndex <= 0 || separatorIndex >= fullName.Length - 1)
            {
                shortName = fullName;
                namespaceName = "(global namespace)";
                return;
            }

            shortName = fullName.Substring(separatorIndex + 1);
            namespaceName = fullName.Substring(0, separatorIndex);
        }

        private static void SplitMemberDisplay(string displayName, out string title, out string signature)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                title = "Unknown";
                signature = string.Empty;
                return;
            }

            var openParen = displayName.IndexOf('(');
            if (openParen <= 0)
            {
                title = displayName;
                signature = string.Empty;
                return;
            }

            title = displayName.Substring(0, openParen);
            signature = displayName.Substring(openParen);
        }

        private static string FormatMetadataToken(int metadataToken)
        {
            return "0x" + metadataToken.ToString("X8");
        }

        private static bool MatchesText(string value, string filter)
        {
            return string.IsNullOrEmpty(filter) || (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ComposeTooltip(string primary, string secondary)
        {
            if (string.IsNullOrEmpty(primary))
            {
                return secondary ?? string.Empty;
            }

            if (string.IsNullOrEmpty(secondary))
            {
                return primary;
            }

            return primary + "\n" + secondary;
        }

        private void RegisterHoverTooltip(Rect anchorRect, string text)
        {
            if (_hoverTooltipRenderer != null)
            {
                _hoverTooltipRenderer.RegisterTextTooltip(new RenderRect(anchorRect.x, anchorRect.y, anchorRect.width, anchorRect.height), text);
            }
        }

        private static HoverTooltipThemePalette BuildTooltipThemePalette()
        {
            return new HoverTooltipThemePalette
            {
                BackgroundColor = ToRenderColor(CortexIdeLayout.Blend(CortexIdeLayout.GetHeaderColor(), CortexIdeLayout.GetBackgroundColor(), 0.18f)),
                BorderColor = ToRenderColor(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.36f)),
                TextColor = ToRenderColor(CortexIdeLayout.GetTextColor()),
                MutedTextColor = ToRenderColor(CortexIdeLayout.GetMutedTextColor()),
                AccentColor = ToRenderColor(CortexIdeLayout.GetAccentColor()),
                HoverFillColor = ToRenderColor(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.18f))
            };
        }

        private static RenderColor ToRenderColor(Color color)
        {
            return new RenderColor(color.r, color.g, color.b, color.a);
        }

        private static void DrawBorder(Rect rect, Texture2D texture, float thickness)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f || thickness <= 0f)
            {
                return;
            }

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), texture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), texture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), texture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), texture);
        }

        private static Texture2D MakeTex(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
