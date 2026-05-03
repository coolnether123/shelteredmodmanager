using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringUiDebugService
    {
        internal struct LayoutRect
        {
            public string Name;
            public Rect Rect;
            public string Detail;
        }

        private static readonly ScenarioAuthoringUiDebugService _instance = new ScenarioAuthoringUiDebugService();
        private readonly ScenarioTargetClassifier _targetClassifier = new ScenarioTargetClassifier();
        private string _lastSignature;
        private string _lastEntityDumpSignature;

        public static ScenarioAuthoringUiDebugService Instance
        {
            get { return _instance; }
        }

        private ScenarioAuthoringUiDebugService()
        {
        }

        public void LogLayout(string signature, IList<LayoutRect> rects)
        {
            if (string.IsNullOrEmpty(signature)
                || string.Equals(_lastSignature, signature, StringComparison.Ordinal)
                || rects == null
                || rects.Count == 0)
            {
                return;
            }

            _lastSignature = signature;

            StringBuilder builder = new StringBuilder();
            builder.Append("[ScenarioAuthoringUIDebug] Layout updated");
            for (int i = 0; i < rects.Count; i++)
            {
                LayoutRect entry = rects[i];
                builder.Append(" | ")
                    .Append(entry.Name ?? "rect")
                    .Append("=")
                    .Append(FormatRect(entry.Rect));
                if (!string.IsNullOrEmpty(entry.Detail))
                    builder.Append(" ").Append(entry.Detail);
            }

            MMLog.WriteInfo(builder.ToString());

            if (!UIDebug.Enabled)
                return;

            UIDebug.ResetTiming();
            for (int i = 0; i < rects.Count; i++)
            {
                LayoutRect entry = rects[i];
                UIDebug.LogTimed((entry.Name ?? "rect") + " " + FormatRect(entry.Rect)
                    + (string.IsNullOrEmpty(entry.Detail) ? string.Empty : " | " + entry.Detail));
            }
        }

        public void DumpSceneEntities(ScenarioAuthoringState state)
        {
            if (state == null || state.Settings == null || !state.Settings.GetBool("debug.overlays", false))
                return;

            GameObject[] objects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
            int count = objects != null ? objects.Length : 0;
            string signature = state.ActiveStage + "|" + state.ActiveTool + "|" + count;
            if (string.Equals(_lastEntityDumpSignature, signature, StringComparison.Ordinal))
                return;

            _lastEntityDumpSignature = signature;

            try
            {
                List<EntityDumpRow> rows = new List<EntityDumpRow>();
                for (int i = 0; objects != null && i < objects.Length; i++)
                {
                    GameObject gameObject = objects[i];
                    if (gameObject == null)
                        continue;

                    rows.Add(BuildEntityDumpRow(gameObject, state));
                }

                rows.Sort(CompareRows);
                string path = Path.Combine(ScenarioAuthoringStoragePaths.GetShellRootPath(true), "scene-entity-classification.tsv");
                WriteEntityDump(path, rows, state);
                MMLog.WriteInfo("[ScenarioAuthoringUIDebug] Scene entity classification dump wrote " + rows.Count + " rows to " + path);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringUIDebug] Failed to dump scene entity classification: " + ex.Message);
            }
        }

        public static LayoutRect Capture(string name, Rect rect, string detail)
        {
            return new LayoutRect
            {
                Name = name,
                Rect = rect,
                Detail = detail
            };
        }

        private EntityDumpRow BuildEntityDumpRow(GameObject gameObject, ScenarioAuthoringState state)
        {
            string path = BuildTransformPath(gameObject.transform);
            ScenarioAuthoringTargetKind kind = InferKind(gameObject, path);
            ScenarioAuthoringTarget target = new ScenarioAuthoringTarget
            {
                Id = kind + ":" + gameObject.GetInstanceID(),
                Kind = kind,
                DisplayName = gameObject.name,
                GameObjectName = gameObject.name,
                TransformPath = path,
                RuntimeObject = gameObject,
                HighlightObject = gameObject
            };

            ScenarioTargetClassification classification = _targetClassifier.Classify(target);
            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            string sortingLayer = string.Empty;
            string sortingOrder = string.Empty;
            string spriteName = string.Empty;
            if (spriteRenderer != null)
            {
                sortingLayer = SortingLayer.IDToName(spriteRenderer.sortingLayerID) ?? string.Empty;
                sortingOrder = spriteRenderer.sortingOrder.ToString();
                spriteName = spriteRenderer.sprite != null ? spriteRenderer.sprite.name : string.Empty;
            }

            string reason;
            bool selectable = new ScenarioSelectionScopeService(_targetClassifier).CanSelectTargetForCurrentStage(state, target, out reason);
            return new EntityDumpRow
            {
                Path = path,
                Name = gameObject.name,
                Active = gameObject.activeInHierarchy,
                Kind = kind,
                Scope = _targetClassifier.FormatScopeLabel(classification),
                SelectableForCurrentTool = selectable,
                FilterReason = reason ?? string.Empty,
                SortingLayer = sortingLayer,
                SortingOrder = sortingOrder,
                SpriteName = spriteName,
                ClassificationSource = classification != null ? classification.Source : string.Empty,
                ClassificationReason = classification != null ? classification.Reason : string.Empty
            };
        }

        private static void WriteEntityDump(string path, IList<EntityDumpRow> rows, ScenarioAuthoringState state)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Scenario authoring scene entity classification dump");
            builder.AppendLine("# ActiveStage=" + state.ActiveStage + "\tActiveTool=" + state.ActiveTool);
            builder.AppendLine("Selectable\tScope\tKind\tName\tSprite\tSortingLayer\tSortingOrder\tPath\tSource\tReason\tFilterReason");
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                EntityDumpRow row = rows[i];
                builder.Append(row.SelectableForCurrentTool ? "yes" : "no").Append("\t")
                    .Append(EscapeTsv(row.Scope)).Append("\t")
                    .Append(row.Kind).Append("\t")
                    .Append(EscapeTsv(row.Name)).Append("\t")
                    .Append(EscapeTsv(row.SpriteName)).Append("\t")
                    .Append(EscapeTsv(row.SortingLayer)).Append("\t")
                    .Append(EscapeTsv(row.SortingOrder)).Append("\t")
                    .Append(EscapeTsv(row.Path)).Append("\t")
                    .Append(EscapeTsv(row.ClassificationSource)).Append("\t")
                    .Append(EscapeTsv(row.ClassificationReason)).Append("\t")
                    .Append(EscapeTsv(row.FilterReason)).AppendLine();
            }

            File.WriteAllText(path, builder.ToString());
        }

        private static int CompareRows(EntityDumpRow left, EntityDumpRow right)
        {
            return string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
        }

        private static ScenarioAuthoringTargetKind InferKind(GameObject gameObject, string path)
        {
            if (gameObject == null)
                return ScenarioAuthoringTargetKind.Unknown;

            if (gameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>() != null)
                return ScenarioAuthoringTargetKind.SceneSprite;
            if (gameObject.GetComponentInParent<FamilyMember>() != null
                || gameObject.GetComponentInParent<NpcVisitor>() != null
                || gameObject.GetComponentInParent<BaseCharacter>() != null)
                return ScenarioAuthoringTargetKind.Character;

            string text = ((path ?? string.Empty) + " " + (gameObject.name ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(text, "wire", "wiring", "cable", "power"))
                return ScenarioAuthoringTargetKind.Wire;
            if (ContainsAny(text, "wall", "barricade"))
                return ScenarioAuthoringTargetKind.Wall;
            if (ContainsAny(text, "light", "lamp"))
                return ScenarioAuthoringTargetKind.Light;
            if (ContainsAny(text, "van", "vehicle", "rv"))
                return ScenarioAuthoringTargetKind.Vehicle;
            if (ContainsAny(text, "room"))
                return ScenarioAuthoringTargetKind.Room;
            if (ContainsAny(text, "tile", "grid"))
                return ScenarioAuthoringTargetKind.Tile;

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>() ?? gameObject.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null && (spriteRenderer.sortingOrder < 0 || ContainsAny(text, "background", "scenery", "sky", "terrain", "backdrop", "sun", "moon", "cloud")))
                return ScenarioAuthoringTargetKind.Background;

            return ScenarioAuthoringTargetKind.PlaceableObject;
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            if (string.IsNullOrEmpty(value) || parts == null)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && value.IndexOf(parts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string EscapeTsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private static string FormatRect(Rect rect)
        {
            return "("
                + Mathf.RoundToInt(rect.x) + ","
                + Mathf.RoundToInt(rect.y) + " "
                + Mathf.RoundToInt(rect.width) + "x"
                + Mathf.RoundToInt(rect.height) + ")";
        }

        private struct EntityDumpRow
        {
            public string Path;
            public string Name;
            public bool Active;
            public ScenarioAuthoringTargetKind Kind;
            public string Scope;
            public bool SelectableForCurrentTool;
            public string FilterReason;
            public string SortingLayer;
            public string SortingOrder;
            public string SpriteName;
            public string ClassificationSource;
            public string ClassificationReason;
        }
    }
}
