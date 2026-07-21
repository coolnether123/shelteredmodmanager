using System;
using System.Globalization;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Story;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    /// <summary>
    /// Draws the primary authoring Story Map: parchment stage cards, clipped orthogonal
    /// routes, red/amber styling for broken/unreachable stages, and a legend. The model
    /// (nodes, edges, and deterministic positions) is built by <see cref="ScenarioStoryGraphBuilder"/>
    /// and carried on the section; this surface only draws it and routes clicks back through
    /// the shared open-stage action seam.
    /// </summary>
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const string StoryMapSectionId = "story_map";

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
            x = DrawStoryMapLegendSwatch(x, legend.y, _uiContext.Styles.Theme.Palette.AccentGold, "route");
            x = DrawStoryMapLegendSwatch(x, legend.y, _uiContext.Styles.Theme.Palette.SemanticWarningStrong, "unreachable");
            x = DrawStoryMapLegendSwatch(x, legend.y, _uiContext.Styles.Theme.Palette.SemanticErrorStrong, "broken / missing");
            GUI.Label(new Rect(x + 4f, legend.y + 2f, Math.Max(80f, legend.xMax - x - 4f), 18f), "!N = validation issues (hover for details)", _mutedTextStyle);
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

            ScenarioUiAtlasSkin.DrawCornerCutTexture(viewport, _uiContext.Styles.ViewportTexture);
            ScenarioUiAtlasSkin.DrawCornerCutBorder(viewport, _uiContext.Styles.BorderSubtleTexture, _uiContext.Styles.BorderSubtleTexture);

            // Clip all immediate drawing to the canvas. The old rotated-line primitive also
            // rotated IMGUI's clip matrix, which is what produced detached line fragments.
            GUI.BeginGroup(viewport);
            Rect canvas = new Rect(0f, 0f, Math.Max(viewport.width, model.Width), Math.Max(viewport.height, model.Height));

            // Edges behind the cards.
            for (int i = 0; model.Edges != null && i < model.Edges.Length; i++)
                DrawStoryMapEdge(canvas, model.Nodes, model.Edges[i], i);

            // Node cards on top.
            for (int i = 0; i < model.Nodes.Length; i++)
                DrawStoryMapNode(canvas, model.Nodes[i]);
            GUI.EndGroup();
        }

        private void DrawStoryMapEdge(
            Rect canvas,
            ScenarioStoryGraphNode[] nodes,
            ScenarioStoryGraphEdge edge,
            int routeIndex)
        {
            if (edge == null)
                return;
            ScenarioStoryGraphNode from = FindStoryMapNode(nodes, edge.FromNodeId);
            ScenarioStoryGraphNode to = FindStoryMapNode(nodes, edge.ToNodeId);
            if (from == null || to == null)
                return;

            Color color = edge.Status == ScenarioStoryGraphEdgeStatus.Broken
                ? _uiContext.Styles.Theme.Palette.SemanticErrorStrong
                : _uiContext.Styles.Theme.Palette.AccentGold;
            if (to.Y > from.Y + 1f)
            {
                Vector2 start = new Vector2(canvas.x + from.X + (from.Width * 0.5f), canvas.y + from.Y + from.Height);
                Vector2 end = new Vector2(canvas.x + to.X + (to.Width * 0.5f), canvas.y + to.Y);
                float turnY = start.y + Math.Max(12f, (end.y - start.y) * 0.5f);
                DrawStoryMapOrthogonalRoute(start, new Vector2(start.x, turnY), new Vector2(end.x, turnY), end, color);
                return;
            }

            if (to.X >= from.X)
            {
                Vector2 start = new Vector2(canvas.x + from.X + from.Width, canvas.y + from.Y + (from.Height * 0.5f));
                Vector2 end = new Vector2(canvas.x + to.X, canvas.y + to.Y + (to.Height * 0.5f));
                float turnX = start.x + ((end.x - start.x) * 0.5f);
                DrawStoryMapOrthogonalRoute(start, new Vector2(turnX, start.y), new Vector2(turnX, end.y), end, color);
                return;
            }

            // A route that points backward travels above the stage row so it never cuts
            // through cards or their outcome leaves.
            Vector2 reverseStart = new Vector2(canvas.x + from.X, canvas.y + from.Y + (from.Height * 0.5f));
            Vector2 reverseEnd = new Vector2(canvas.x + to.X + to.Width, canvas.y + to.Y + (to.Height * 0.5f));
            float channelY = Math.Max(3f, Math.Min(reverseStart.y, reverseEnd.y) - 12f - ((routeIndex % 3) * 4f));
            Vector2 leftExit = new Vector2(reverseStart.x - 10f, reverseStart.y);
            Vector2 leftTurn = new Vector2(leftExit.x, channelY);
            Vector2 rightTurn = new Vector2(reverseEnd.x + 10f, channelY);
            Vector2 rightEntry = new Vector2(rightTurn.x, reverseEnd.y);
            DrawStoryMapAxisSegment(reverseStart, leftExit, color, 2f);
            DrawStoryMapAxisSegment(leftExit, leftTurn, color, 2f);
            DrawStoryMapAxisSegment(leftTurn, rightTurn, color, 2f);
            DrawStoryMapAxisSegment(rightTurn, rightEntry, color, 2f);
            DrawStoryMapAxisSegment(rightEntry, reverseEnd, color, 2f);
            DrawStoryMapRouteCap(reverseEnd, color);
        }

        private static ScenarioStoryGraphNode FindStoryMapNode(ScenarioStoryGraphNode[] nodes, string id)
        {
            for (int i = 0; nodes != null && i < nodes.Length; i++)
                if (nodes[i] != null && string.Equals(nodes[i].Id, id, StringComparison.Ordinal))
                    return nodes[i];
            return null;
        }

        private static void DrawStoryMapOrthogonalRoute(
            Vector2 start,
            Vector2 cornerOne,
            Vector2 cornerTwo,
            Vector2 end,
            Color color)
        {
            DrawStoryMapAxisSegment(start, cornerOne, color, 2f);
            DrawStoryMapAxisSegment(cornerOne, cornerTwo, color, 2f);
            DrawStoryMapAxisSegment(cornerTwo, end, color, 2f);
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(cornerOne.x - 1f, cornerOne.y - 1f, 3f, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cornerTwo.x - 1f, cornerTwo.y - 1f, 3f, 3f), Texture2D.whiteTexture);
            GUI.color = old;
            DrawStoryMapRouteCap(end, color);
        }

        private static void DrawStoryMapRouteCap(Vector2 end, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(end.x - 3f, end.y - 3f, 6f, 6f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawStoryMapAxisSegment(Vector2 a, Vector2 b, Color color, float thickness)
        {
            float left = Math.Min(a.x, b.x);
            float top = Math.Min(a.y, b.y);
            float width = Math.Abs(b.x - a.x);
            float height = Math.Abs(b.y - a.y);
            if (width < 0.01f && height < 0.01f)
                return;
            Rect segment = width >= height
                ? new Rect(left, a.y - (thickness * 0.5f), Math.Max(thickness, width), thickness)
                : new Rect(a.x - (thickness * 0.5f), top, thickness, Math.Max(thickness, height));
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(segment, Texture2D.whiteTexture);
            GUI.color = old;
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
            Texture2D nodeBorder = node.Status == ScenarioStoryGraphNodeStatus.Broken
                ? _uiContext.Styles.SemanticErrorStrongTexture
                : node.Status == ScenarioStoryGraphNodeStatus.Unreachable
                    ? _uiContext.Styles.SemanticWarningStrongTexture
                    : _uiContext.Styles.BorderSubtleTexture;
            ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, nodeBorder, _uiContext.Styles.BorderSubtleTexture);

            float pad = 8f;
            Rect titleRect = new Rect(rect.x + pad, rect.y + 6f, rect.width - (pad * 2f) - 4f, 20f);
            GUIStyle titleStyle = node.Kind == ScenarioStoryGraphNodeKind.Terminal ? _mutedTextStyle : _sectionTitleStyle;
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
                    string badgeHint = node.ProblemCount.ToString(CultureInfo.InvariantCulture)
                        + (node.ProblemCount == 1 ? " validation issue" : " validation issues")
                        + (!string.IsNullOrEmpty(node.ProblemSummary) ? ": " + node.ProblemSummary : string.Empty);
                    GUI.Label(badgeRect, new GUIContent(string.Empty, badgeHint), GUIStyle.none);
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

        private Color ResolveStoryMapFill(ScenarioStoryGraphNodeStatus status, ScenarioStoryGraphNodeKind kind, bool hovered)
        {
            if (hovered)
            {
                if (status == ScenarioStoryGraphNodeStatus.Broken)
                    return _uiContext.Styles.Theme.Palette.SemanticErrorStrong;
                if (status == ScenarioStoryGraphNodeStatus.Unreachable)
                    return _uiContext.Styles.Theme.Palette.SemanticWarningStrong;
                return _uiContext.Styles.Theme.Palette.SurfaceCardHover;
            }

            if (kind == ScenarioStoryGraphNodeKind.Terminal && status != ScenarioStoryGraphNodeStatus.Broken)
                return _uiContext.Styles.Theme.Palette.SurfaceDisabled;
            if (status == ScenarioStoryGraphNodeStatus.Broken)
                return _uiContext.Styles.Theme.Palette.SemanticError;
            if (status == ScenarioStoryGraphNodeStatus.Unreachable)
                return _uiContext.Styles.Theme.Palette.SemanticWarning;
            return _uiContext.Styles.Theme.Palette.SurfaceCard;
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
