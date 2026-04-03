using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Services.Navigation;
using Cortex.Services.Reference;
using UnityEngine;

namespace Cortex.Modules.Reference
{
    public sealed class ReferenceModule
    {
        private const float AssemblyRowHeight = 44f;
        private const float TypeRowHeight = 44f;
        private const float MemberRowHeight = 46f;

        private string _appliedTheme = string.Empty;
        private Vector2 _assemblyScroll = Vector2.zero;
        private Vector2 _typeScroll = Vector2.zero;
        private Vector2 _memberScroll = Vector2.zero;
        private Vector2 _previewScroll = Vector2.zero;
        private readonly IHoverTooltipRenderer _hoverTooltipRenderer;
        private readonly ReferenceBrowserSessionService _browserSession = new ReferenceBrowserSessionService();
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
            _browserSession.EnsureAssembliesLoaded(referenceCatalogService, state);
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
                GUI.enabled = _browserSession.SelectedMember != null;
                if (GUILayout.Button("Decompile Member", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(132f)))
                {
                    _browserSession.DecompileSelectedMember(navigationService, state);
                }

                GUI.enabled = _browserSession.SelectedType != null;
                if (GUILayout.Button("Decompile Type", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(120f)))
                {
                    _browserSession.DecompileSelectedType(navigationService, state);
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
            var selection = _browserSession.BuildSelectionPresentation();
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
                DrawMetadataRow("Assembly", selection.AssemblyLabel);
                DrawMetadataRow("Path", selection.AssemblyPath);
                DrawMetadataRow("Type", selection.TypeLabel);
                DrawMetadataRow("Member", selection.MemberLabel);
                GUILayout.Space(4f);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Ignore Cache", GUILayout.Width(90f));
                _browserSession.IgnoreCache = GUILayout.Toggle(_browserSession.IgnoreCache, _browserSession.IgnoreCache ? "On" : "Off", GUILayout.Width(54f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                var previousEnabled = GUI.enabled;
                GUI.enabled = _browserSession.SelectedMember != null;
                if (GUILayout.Button("Decompile Selected Member", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(188f)))
                {
                    _browserSession.DecompileSelectedMember(navigationService, state);
                }

                GUI.enabled = _browserSession.SelectedType != null;
                if (GUILayout.Button("Decompile Selected Type", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(170f)))
                {
                    _browserSession.DecompileSelectedType(navigationService, state);
                }

                GUI.enabled = state.LastReferenceResult != null &&
                    !string.IsNullOrEmpty(state.LastReferenceResult.CachePath) &&
                    File.Exists(state.LastReferenceResult.CachePath);
                if (GUILayout.Button("Open Decompiled Source", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(178f)))
                {
                    _browserSession.OpenDecompilerResult(navigationService, state);
                }

                GUI.enabled = state.LastReferenceResult != null;
                if (GUILayout.Button("Clear Result", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(108f)))
                {
                    _browserSession.ClearDecompilerResult(state);
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
            var selection = _browserSession.BuildSelectionPresentation();
            GUILayout.BeginVertical(_summaryStyle ?? GUI.skin.box);
            GUILayout.Label(selection.SelectionPath, _detailLabelStyle ?? GUI.skin.label);
            GUILayout.Label(selection.SelectionCaption, _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void DrawAssemblyToolbar(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            var assemblyItems = _browserSession.BuildAssemblyItems();
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(28f));
            GUILayout.Label("Search", GUILayout.Width(52f));
            _browserSession.AssemblyFilter = GUILayout.TextField(_browserSession.AssemblyFilter ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_browserSession.AssemblyFilter) && GUILayout.Button("x", GUILayout.Width(20f)))
            {
                _browserSession.AssemblyFilter = string.Empty;
            }

            GUILayout.Space(6f);
            GUILayout.Label(assemblyItems.Count + " shown", _rowMetaStyle ?? GUI.skin.label, GUILayout.Width(68f));
            if (GUILayout.Button("Refresh", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(72f)))
            {
                _browserSession.ReloadAssemblies(referenceCatalogService, state);
                state.StatusMessage = "Reference assemblies refreshed.";
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTypeToolbar()
        {
            var typeItems = _browserSession.BuildTypeItems();
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(28f));
            GUILayout.Label("Filter", GUILayout.Width(52f));
            _browserSession.TypeFilter = GUILayout.TextField(_browserSession.TypeFilter ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_browserSession.TypeFilter) && GUILayout.Button("x", GUILayout.Width(20f)))
            {
                _browserSession.TypeFilter = string.Empty;
            }

            GUILayout.Space(6f);
            GUILayout.Label(typeItems.Count + " shown", _rowMetaStyle ?? GUI.skin.label, GUILayout.Width(68f));
            GUILayout.EndHorizontal();
        }

        private void DrawMemberToolbar()
        {
            var memberItems = _browserSession.BuildMemberItems();
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(28f));
            GUILayout.Label("Filter", GUILayout.Width(52f));
            _browserSession.MemberFilter = GUILayout.TextField(_browserSession.MemberFilter ?? string.Empty, _filterBoxStyle ?? GUI.skin.textField, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(_browserSession.MemberFilter) && GUILayout.Button("x", GUILayout.Width(20f)))
            {
                _browserSession.MemberFilter = string.Empty;
            }

            GUILayout.Space(6f);
            GUILayout.Label(memberItems.Count + " shown", _rowMetaStyle ?? GUI.skin.label, GUILayout.Width(68f));
            GUILayout.EndHorizontal();
        }

        private void DrawAssemblyList(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            var assemblyItems = _browserSession.BuildAssemblyItems();
            for (var i = 0; i < assemblyItems.Count; i++)
            {
                var assembly = assemblyItems[i];
                if (DrawSelectableRow(
                    assembly.Title,
                    assembly.Subtitle,
                    assembly.Meta,
                    assembly.Tooltip,
                    assembly.IsSelected,
                    AssemblyRowHeight))
                {
                    _browserSession.SelectAssembly(referenceCatalogService, assembly.Assembly, state);
                }
            }

            if (assemblyItems.Count == 0)
            {
                GUILayout.Label("No assemblies match the current search.", _emptyStateStyle ?? GUI.skin.label);
            }
        }

        private void DrawTypeList(IReferenceCatalogService referenceCatalogService, CortexShellState state)
        {
            if (_browserSession.SelectedAssembly == null)
            {
                GUILayout.Label("Select an assembly to browse its exported and internal top-level types.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            var typeItems = _browserSession.BuildTypeItems();
            for (var i = 0; i < typeItems.Count; i++)
            {
                var type = typeItems[i];
                if (DrawSelectableRow(
                    type.Title,
                    type.Subtitle,
                    type.Meta,
                    type.Tooltip,
                    type.IsSelected,
                    TypeRowHeight))
                {
                    _browserSession.SelectType(referenceCatalogService, type.Type, state);
                }
            }

            if (typeItems.Count == 0)
            {
                GUILayout.Label("No types match the current filter.", _emptyStateStyle ?? GUI.skin.label);
            }
        }

        private void DrawMemberList(CortexShellState state)
        {
            if (_browserSession.SelectedType == null)
            {
                GUILayout.Label("Select a type to inspect its constructors and methods.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            var memberItems = _browserSession.BuildMemberItems();
            for (var i = 0; i < memberItems.Count; i++)
            {
                var member = memberItems[i];
                if (DrawSelectableRow(
                    member.Title,
                    member.Subtitle,
                    member.Meta,
                    member.Tooltip,
                    member.IsSelected,
                    MemberRowHeight))
                {
                    _browserSession.SelectMember(member.Member, state);
                }
            }

            if (memberItems.Count == 0)
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
