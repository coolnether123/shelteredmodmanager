using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Story;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    /// <summary>
    /// Draws the primary authoring Story Map: parchment stage cards, gold route edges with
    /// arrowheads, red/amber styling for broken/unreachable stages, and a legend. The model
    /// (nodes, edges, and deterministic positions) is built by <see cref="ScenarioStoryGraphBuilder"/>
    /// and carried on the section; this surface only draws it and routes clicks back through
    /// the shared open-stage action seam.
    /// </summary>
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const string StoryMapSectionId = "story_map";

        private static readonly Color StoryMapGold = new Color(0.82f, 0.68f, 0.32f, 1f);
        private static readonly Color StoryMapAmber = new Color(0.86f, 0.62f, 0.20f, 1f);
        private static readonly Color StoryMapRed = new Color(0.74f, 0.28f, 0.24f, 1f);

        private bool IsStoryMapSection(ScenarioAuthoringInspectorSection section)
        {
            return section != null
                && string.Equals(section.Id, StoryMapSectionId, StringComparison.OrdinalIgnoreCase);
        }

        private void DrawStoryMapSection(ScenarioAuthoringInspectorSection section)
        {
            ScenarioStoryGraphModel model = section != null ? section.StoryMap : null;
            if (model == null || model.NodeCount == 0)
            {
                GUILayout.Label(model != null && !string.IsNullOrEmpty(model.Note)
                    ? model.Note
                    : "No story stages yet. Add a stage to start the map.", _mutedTextStyle);
                return;
            }

            GUILayout.Label("Visual overview of your story stages. Click a stage to open its focused editor.", _mutedTextStyle);
            if (model.Truncated && !string.IsNullOrEmpty(model.Note))
                GUILayout.Label(model.Note, _mutedTextStyle);

            DrawStoryMapLegend();
            DrawStoryMapCanvas(model);
        }

        private void DrawStoryMapLegend()
        {
            float available = GetSectionContentWidth();
            Rect legend = GUILayoutUtility.GetRect(available, 22f, GUILayout.ExpandWidth(true), GUILayout.Height(22f));
            float x = legend.x;
            x = DrawStoryMapLegendSwatch(x, legend.y, StoryMapGold, "route");
            x = DrawStoryMapLegendSwatch(x, legend.y, StoryMapAmber, "unreachable");
            x = DrawStoryMapLegendSwatch(x, legend.y, StoryMapRed, "broken / missing");
            GUI.Label(new Rect(x + 4f, legend.y + 2f, Math.Max(80f, legend.xMax - x - 4f), 18f), "Click a stage to edit", _mutedTextStyle);
            GUILayout.Space(4f);
        }

        private float DrawStoryMapLegendSwatch(float x, float y, Color color, string label)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y + 4f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = old;
            float labelWidth = ScenarioUiMeasuredLabel.Width(label, _mutedTextStyle, 6f);
            GUI.Label(new Rect(x + 16f, y + 2f, labelWidth, 18f), label, _mutedTextStyle);
            return x + 16f + labelWidth + 14f;
        }

        private void DrawStoryMapCanvas(ScenarioStoryGraphModel model)
        {
            float available = GetSectionContentWidth();
            float viewportHeight = Mathf.Clamp(model.Height + 8f, 150f, 460f);
            Rect viewport = GUILayoutUtility.GetRect(available, viewportHeight, GUILayout.ExpandWidth(true), GUILayout.Height(viewportHeight));

            RegisterScrollRegion("story.map", viewport);
            RegisterInteractiveRegion(viewport);

            Color panelColor = GUI.color;
            GUI.color = new Color(0.20f, 0.16f, 0.11f, 0.24f);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(viewport, Texture2D.whiteTexture);
            GUI.color = panelColor;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(viewport, _uiContext.Styles.BorderSubtleTexture, _uiContext.Styles.BorderSubtleTexture);

            Vector2 scroll = GetWindowScrollPosition("story.map");
            GUILayout.BeginArea(viewport);
            scroll = GUILayout.BeginScrollView(scroll, true, true, GUILayout.Width(viewport.width), GUILayout.Height(viewport.height));

            float contentWidth = Math.Max(viewport.width, model.Width);
            float contentHeight = Math.Max(viewport.height - 18f, model.Height);
            Rect canvas = GUILayoutUtility.GetRect(contentWidth, contentHeight, GUILayout.Width(contentWidth), GUILayout.Height(contentHeight));

            Dictionary<string, ScenarioStoryGraphNode> byId = new Dictionary<string, ScenarioStoryGraphNode>(StringComparer.Ordinal);
            for (int i = 0; i < model.Nodes.Length; i++)
                if (model.Nodes[i] != null && !string.IsNullOrEmpty(model.Nodes[i].Id))
                    byId[model.Nodes[i].Id] = model.Nodes[i];

            // Edges behind the cards.
            for (int i = 0; model.Edges != null && i < model.Edges.Length; i++)
                DrawStoryMapEdge(canvas, byId, model.Edges[i]);

            // Node cards on top.
            for (int i = 0; i < model.Nodes.Length; i++)
                DrawStoryMapNode(canvas, model.Nodes[i]);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition("story.map", scroll);
        }

        private void DrawStoryMapEdge(Rect canvas, Dictionary<string, ScenarioStoryGraphNode> byId, ScenarioStoryGraphEdge edge)
        {
            if (edge == null)
                return;
            ScenarioStoryGraphNode from;
            ScenarioStoryGraphNode to;
            if (!byId.TryGetValue(edge.FromNodeId ?? string.Empty, out from) || !byId.TryGetValue(edge.ToNodeId ?? string.Empty, out to))
                return;

            Vector2 start = new Vector2(canvas.x + from.X + from.Width, canvas.y + from.Y + (from.Height * 0.5f));
            Vector2 end = new Vector2(canvas.x + to.X, canvas.y + to.Y + (to.Height * 0.5f));
            Color color = edge.Status == ScenarioStoryGraphEdgeStatus.Broken ? StoryMapRed : StoryMapGold;
            DrawStoryMapArrow(start, end, color);
        }

        private static void DrawStoryMapArrow(Vector2 from, Vector2 to, Color color)
        {
            DrawStoryMapLine(from, to, color, 2f);
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.01f)
                return;
            float baseAngle = Mathf.Atan2(delta.y, delta.x);
            const float head = 9f;
            const float spread = 0.4f; // radians (~23 degrees)
            Vector2 left = to + new Vector2(Mathf.Cos(baseAngle + Mathf.PI - spread), Mathf.Sin(baseAngle + Mathf.PI - spread)) * head;
            Vector2 right = to + new Vector2(Mathf.Cos(baseAngle + Mathf.PI + spread), Mathf.Sin(baseAngle + Mathf.PI + spread)) * head;
            DrawStoryMapLine(to, left, color, 2f);
            DrawStoryMapLine(to, right, color, 2f);
        }

        private static void DrawStoryMapLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.01f)
                return;

            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.color = color;
            GUI.DrawTexture(new Rect(a.x, a.y - (thickness * 0.5f), length, thickness), Texture2D.whiteTexture);
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }

        private void DrawStoryMapNode(Rect canvas, ScenarioStoryGraphNode node)
        {
            if (node == null)
                return;

            Rect rect = new Rect(canvas.x + node.X, canvas.y + node.Y, node.Width, node.Height);
            bool hovered = IsInteractiveHoverAllowed(rect);

            Color old = GUI.color;
            GUI.color = ResolveStoryMapFill(node.Status, node.Kind, hovered);
            ScenarioUiAtlasSkin.DrawCornerCutTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(
                rect,
                node.Status == ScenarioStoryGraphNodeStatus.Ok ? _uiContext.Styles.BorderSubtleTexture : _uiContext.Styles.BorderStrongTexture,
                _uiContext.Styles.BorderSubtleTexture);

            float pad = 8f;
            Rect titleRect = new Rect(rect.x + pad, rect.y + 6f, rect.width - (pad * 2f) - 4f, 20f);
            GUIStyle titleStyle = node.Kind == ScenarioStoryGraphNodeKind.Terminal ? _mutedTextStyle : _smallTitleStyle;
            string tooltip = node.Tooltip ?? node.Label;
            GUI.Label(titleRect, new GUIContent(ShortenToFit(node.Label ?? string.Empty, titleRect.width, titleStyle), tooltip), titleStyle);

            if (node.Kind == ScenarioStoryGraphNodeKind.Stage)
            {
                Rect subRect = new Rect(rect.x + pad, rect.y + 28f, rect.width - (pad * 2f), 16f);
                string sub = (node.StepCount == 1 ? "1 scene" : node.StepCount.ToString(CultureInfo.InvariantCulture) + " scenes")
                    + " / " + (node.LineCount == 1 ? "1 line" : node.LineCount.ToString(CultureInfo.InvariantCulture) + " lines");
                GUI.Label(subRect, sub, _mutedTextStyle);

                if (node.ProblemCount > 0)
                {
                    string badge = "!" + node.ProblemCount.ToString(CultureInfo.InvariantCulture);
                    float badgeWidth = 30f;
                    Rect badgeRect = new Rect(rect.xMax - badgeWidth - 6f, rect.y + 6f, badgeWidth, 18f);
                    ScenarioUiWidgets.DrawPill(badgeRect, badge, _uiContext.Styles, ResolveStoryMapBadgeEmphasis(node.Status));
                }

                Rect statusRect = new Rect(rect.x + pad, rect.yMax - 22f, rect.width - (pad * 2f), 16f);
                string statusText = ResolveStoryMapStatusText(node.Status);
                if (!string.IsNullOrEmpty(statusText))
                    GUI.Label(statusRect, statusText, _mutedTextStyle);
            }

            if (!string.IsNullOrEmpty(node.NavActionId))
            {
                if (!string.IsNullOrEmpty(node.Tooltip))
                    RegisterTourTarget("action:" + node.NavActionId, rect);
                if (DrawPlainButton(rect, GUIContent.none, GUIStyle.none, true))
                {
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(node.NavActionId);
                    if (Event.current != null)
                        Event.current.Use();
                }
            }
        }

        private static Color ResolveStoryMapFill(ScenarioStoryGraphNodeStatus status, ScenarioStoryGraphNodeKind kind, bool hovered)
        {
            Color color;
            if (kind == ScenarioStoryGraphNodeKind.Terminal && status != ScenarioStoryGraphNodeStatus.Broken)
                color = new Color(0.58f, 0.54f, 0.46f, 0.52f);
            else if (status == ScenarioStoryGraphNodeStatus.Broken)
                color = new Color(0.72f, 0.28f, 0.24f, 0.46f);
            else if (status == ScenarioStoryGraphNodeStatus.Unreachable)
                color = new Color(0.84f, 0.60f, 0.20f, 0.42f);
            else
                color = new Color(0.80f, 0.72f, 0.55f, 0.58f);

            if (hovered)
                color = new Color(color.r * 1.1f, color.g * 1.1f, color.b * 1.1f, Math.Min(1f, color.a + 0.12f));
            return color;
        }

        private static ScenarioUiPillEmphasis ResolveStoryMapBadgeEmphasis(ScenarioStoryGraphNodeStatus status)
        {
            if (status == ScenarioStoryGraphNodeStatus.Broken)
                return ScenarioUiPillEmphasis.Danger;
            if (status == ScenarioStoryGraphNodeStatus.Unreachable)
                return ScenarioUiPillEmphasis.Warning;
            return ScenarioUiPillEmphasis.Default;
        }

        private static string ResolveStoryMapStatusText(ScenarioStoryGraphNodeStatus status)
        {
            if (status == ScenarioStoryGraphNodeStatus.Broken)
                return "broken route";
            if (status == ScenarioStoryGraphNodeStatus.Unreachable)
                return "unreachable";
            return null;
        }
    }
}
