using System;
using System.Collections.Generic;
using System.Text;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using UnityEngine;
using Cortex.Services.Editor.Presentation;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorSyntaxHighlighter
    {
        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();
        private string _cachedKey = string.Empty;
        private string _cachedRichText = string.Empty;

        public string GetRichText(DocumentSession session)
        {
            var cacheKey = BuildCacheKey(session);
            if (string.Equals(_cachedKey, cacheKey, StringComparison.Ordinal))
            {
                return _cachedRichText;
            }

            _cachedKey = cacheKey;
            _cachedRichText = BuildRichText(session, _classificationPresentationService);
            return _cachedRichText;
        }

        public void Invalidate()
        {
            _cachedKey = string.Empty;
            _cachedRichText = string.Empty;
        }

        private static string BuildCacheKey(DocumentSession session)
        {
            if (session == null)
            {
                return string.Empty;
            }

            var analysis = session.LanguageAnalysis;
            var classificationCount = analysis != null && analysis.Classifications != null
                ? analysis.Classifications.Length
                : 0;

            return (session.FilePath ?? string.Empty) + "|" +
                   (session.Text != null ? session.Text.Length.ToString() : "0") + "|" +
                   session.LastLanguageAnalysisUtc.Ticks + "|" +
                   classificationCount;
        }

        private static string BuildRichText(DocumentSession session, EditorClassificationPresentationService classificationPresentationService)
        {
            var text = session != null ? session.Text ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var spans = session != null && session.LanguageAnalysis != null
                ? session.LanguageAnalysis.Classifications
                : null;

            if (spans == null || spans.Length == 0)
            {
                MMLog.WriteInfo("[Cortex.Syntax] No classifications for " + GetDisplayName(session) + ". Rendering plain text.");
                return Escape(text);
            }

            var ordered = new List<LanguageServiceClassifiedSpan>(spans.Length);
            for (var i = 0; i < spans.Length; i++)
            {
                if (spans[i] == null || spans[i].Length <= 0 || spans[i].Start < 0 || spans[i].Start >= text.Length)
                {
                    continue;
                }

                ordered.Add(spans[i]);
            }

            if (ordered.Count == 0)
            {
                MMLog.WriteInfo("[Cortex.Syntax] Classifications were empty after filtering for " + GetDisplayName(session) + ".");
                return Escape(text);
            }

            MMLog.WriteInfo("[Cortex.Syntax] Building rich text for " + GetDisplayName(session) +
                ". Spans=" + ordered.Count +
                ", Summary=" + BuildSummary(ordered));

            ordered.Sort(CompareSpans);

            var sb = new StringBuilder(text.Length + (ordered.Count * 24));
            var cursor = 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                var span = ordered[i];
                var start = span.Start;
                var end = Math.Min(text.Length, span.Start + span.Length);
                if (end <= start)
                {
                    continue;
                }

                if (start < cursor)
                {
                    start = cursor;
                }

                if (start > cursor)
                {
                    sb.Append(Escape(text.Substring(cursor, start - cursor)));
                }

                var color = classificationPresentationService != null
                    ? classificationPresentationService.GetHexColor(span.Classification, span.SemanticTokenType)
                    : string.Empty;
                if (string.IsNullOrEmpty(color))
                {
                    sb.Append(Escape(text.Substring(start, end - start)));
                }
                else
                {
                    sb.Append("<color=");
                    sb.Append(color);
                    sb.Append(">");
                    sb.Append(Escape(text.Substring(start, end - start)));
                    sb.Append("</color>");
                }

                cursor = end;
            }

            if (cursor < text.Length)
            {
                sb.Append(Escape(text.Substring(cursor)));
            }

            return sb.ToString();
        }

        private static int CompareSpans(LanguageServiceClassifiedSpan left, LanguageServiceClassifiedSpan right)
        {
            if (left.Start != right.Start)
            {
                return left.Start.CompareTo(right.Start);
            }

            return right.Length.CompareTo(left.Length);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string GetDisplayName(DocumentSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.FilePath))
            {
                return "(unknown)";
            }

            try
            {
                return System.IO.Path.GetFileName(session.FilePath);
            }
            catch
            {
                return session.FilePath;
            }
        }

        private static string BuildSummary(List<LanguageServiceClassifiedSpan> spans)
        {
            if (spans == null || spans.Count == 0)
            {
                return "(none)";
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < spans.Count; i++)
            {
                var key = spans[i] != null ? spans[i].Classification ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(key))
                {
                    key = "(empty)";
                }

                int current;
                counts.TryGetValue(key, out current);
                counts[key] = current + 1;
            }

            var parts = new List<string>();
            foreach (var pair in counts)
            {
                parts.Add(pair.Key + "=" + pair.Value);
                if (parts.Count >= 8)
                {
                    break;
                }
            }

            return string.Join("; ", parts.ToArray());
        }
    }
}
