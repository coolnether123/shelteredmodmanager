using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Core
{
    internal sealed class LoadingTransitionDiagnostics
    {
        private readonly Queue<string> _recentProblems = new Queue<string>();
        private readonly List<string> _breadcrumbs = new List<string>();

        public string[] RecentProblems
        {
            get { return _recentProblems.ToArray(); }
        }

        public string[] Breadcrumbs
        {
            get { return _breadcrumbs.ToArray(); }
        }

        public void ClearBreadcrumbs()
        {
            _breadcrumbs.Clear();
        }

        public void RecordProblem(string problem)
        {
            if (string.IsNullOrEmpty(problem))
                return;

            TrimQueue(_recentProblems);
            _recentProblems.Enqueue(problem);
        }

        public void RecordUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            string message = LoadingTransitionText.Compact(condition);
            if (!string.IsNullOrEmpty(stackTrace))
                message = message + " at " + LoadingTransitionText.FirstStackFrame(stackTrace);

            RecordProblem(type + ": " + message);
        }

        public void MarkBreadcrumb(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (_breadcrumbs.Count >= LoadingTransitionRecoveryConstants.MaxRecentEvents)
                _breadcrumbs.RemoveAt(0);

            _breadcrumbs.Add(LoadingTransitionText.FormatSeconds(Time.realtimeSinceStartup) + " " + message);
        }

        private static void TrimQueue(Queue<string> queue)
        {
            while (queue.Count >= LoadingTransitionRecoveryConstants.MaxRecentEvents)
                queue.Dequeue();
        }
    }

    internal static class LoadingTransitionText
    {
        public static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }

        public static string UnknownIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? "<unknown>" : value;
        }

        public static string Compact(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= 220 ? value : value.Substring(0, 220) + "...";
        }

        public static string FirstStackFrame(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return string.Empty;

            string[] lines = stackTrace.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] != null ? lines[i].Trim() : string.Empty;
                if (line.Length > 0)
                    return Compact(line);
            }

            return string.Empty;
        }

        public static string JoinInline(string[] values)
        {
            if (values == null || values.Length == 0)
                return "<none>";
            return string.Join(" | ", values);
        }

        public static string JoinBullets(string[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;

            string[] lines = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                lines[i] = "- " + values[i];
            return string.Join("\n", lines);
        }

        public static string FormatSeconds(float seconds)
        {
            return seconds.ToString("0.000");
        }
    }
}
