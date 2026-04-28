using System;
using System.Collections.Generic;
using System.Linq;

namespace ModAPI.Internal.DebugUI
{
    internal static class UIDebugSourceDiffService
    {
        private sealed class SourceAlignRow
        {
            public string Left;
            public string Right;
        }

        public static UIDebugSourceDiffAlignedRows BuildAlignedSourceDiffRows(string originalSource, string patchedSource)
        {
            var left = SplitLines(originalSource);
            var rightAll = SplitLines(patchedSource);
            var rightReal = new List<string>(rightAll.Count);
            var rightSynthetic = new Dictionary<int, List<string>>();
            var seenReal = 0;

            for (var i = 0; i < rightAll.Count; i++)
            {
                var line = rightAll[i] ?? string.Empty;
                if (IsSyntheticPatchedOverlayLine(line))
                {
                    List<string> list;
                    if (!rightSynthetic.TryGetValue(seenReal, out list))
                    {
                        list = new List<string>();
                        rightSynthetic[seenReal] = list;
                    }

                    list.Add(line);
                    continue;
                }

                rightReal.Add(line);
                seenReal++;
            }

            var alignedRows = new UIDebugSourceDiffAlignedRows();
            var rowStack = BuildAlignedRowStack(left, rightReal);
            InjectAlignedRows(rowStack, rightSynthetic, alignedRows);
            return alignedRows;
        }

        public static List<UIDebugDiffLine> ComputeDiff(List<string> before, List<string> after)
        {
            var left = before ?? new List<string>();
            var right = after ?? new List<string>();
            var lcs = BuildLcsTable(left, right);
            var stack = new Stack<UIDebugDiffLine>();
            var x = left.Count;
            var y = right.Count;

            while (x > 0 && y > 0)
            {
                if (left[x - 1] == right[y - 1])
                {
                    stack.Push(new UIDebugDiffLine
                    {
                        LeftContent = left[x - 1],
                        RightContent = right[y - 1],
                        LeftIndex = x - 1,
                        RightIndex = y - 1,
                        LeftMarker = " ",
                        RightMarker = " ",
                        IsMatch = true
                    });
                    x--;
                    y--;
                }
                else if (lcs[x - 1, y] >= lcs[x, y - 1])
                {
                    stack.Push(new UIDebugDiffLine
                    {
                        LeftContent = left[x - 1],
                        RightContent = null,
                        LeftIndex = x - 1,
                        RightIndex = -1,
                        LeftMarker = "-",
                        RightMarker = " ",
                        IsMatch = false
                    });
                    x--;
                }
                else
                {
                    stack.Push(new UIDebugDiffLine
                    {
                        LeftContent = null,
                        RightContent = right[y - 1],
                        LeftIndex = -1,
                        RightIndex = y - 1,
                        LeftMarker = " ",
                        RightMarker = "+",
                        IsMatch = false
                    });
                    y--;
                }
            }

            while (x > 0)
            {
                stack.Push(new UIDebugDiffLine
                {
                    LeftContent = left[x - 1],
                    RightContent = null,
                    LeftIndex = x - 1,
                    RightIndex = -1,
                    LeftMarker = "-",
                    RightMarker = " ",
                    IsMatch = false
                });
                x--;
            }

            while (y > 0)
            {
                stack.Push(new UIDebugDiffLine
                {
                    LeftContent = null,
                    RightContent = right[y - 1],
                    LeftIndex = -1,
                    RightIndex = y - 1,
                    LeftMarker = " ",
                    RightMarker = "+",
                    IsMatch = false
                });
                y--;
            }

            return stack.ToList();
        }

        public static List<string> SplitLines(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Split('\n').ToList();
        }

        public static bool IsSyntheticPatchedOverlayLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("//", StringComparison.Ordinal)) return false;

            return
                line.IndexOf("TRANSPILE INJECTION PREVIEW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.StartsWith("// This shows likely runtime-injected operations", StringComparison.Ordinal) ||
                trimmed.StartsWith("// [Regex Rewrite]", StringComparison.Ordinal) ||
                trimmed.StartsWith("// Hunk", StringComparison.Ordinal) ||
                trimmed.StartsWith("//   +", StringComparison.Ordinal) ||
                trimmed.StartsWith("//   -", StringComparison.Ordinal) ||
                trimmed.StartsWith("// ... ", StringComparison.Ordinal) ||
                line.IndexOf("END INJECTION PREVIEW", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Stack<SourceAlignRow> BuildAlignedRowStack(List<string> left, List<string> right)
        {
            var lcs = BuildLcsTable(left, right);
            var rowStack = new Stack<SourceAlignRow>();
            var x = left.Count;
            var y = right.Count;

            while (x > 0 && y > 0)
            {
                if (string.Equals(left[x - 1], right[y - 1], StringComparison.Ordinal))
                {
                    rowStack.Push(new SourceAlignRow { Left = left[x - 1], Right = right[y - 1] });
                    x--;
                    y--;
                }
                else if (lcs[x - 1, y] >= lcs[x, y - 1])
                {
                    rowStack.Push(new SourceAlignRow { Left = left[x - 1], Right = string.Empty });
                    x--;
                }
                else
                {
                    rowStack.Push(new SourceAlignRow { Left = string.Empty, Right = right[y - 1] });
                    y--;
                }
            }

            while (x > 0)
            {
                rowStack.Push(new SourceAlignRow { Left = left[x - 1], Right = string.Empty });
                x--;
            }

            while (y > 0)
            {
                rowStack.Push(new SourceAlignRow { Left = string.Empty, Right = right[y - 1] });
                y--;
            }

            return rowStack;
        }

        private static void InjectAlignedRows(
            Stack<SourceAlignRow> rowStack,
            Dictionary<int, List<string>> rightSynthetic,
            UIDebugSourceDiffAlignedRows alignedRows)
        {
            var realIndex = 0;
            var injectedSynthetic = new HashSet<int>();

            while (rowStack.Count > 0)
            {
                List<string> syntheticBefore;
                if (!injectedSynthetic.Contains(realIndex) && rightSynthetic.TryGetValue(realIndex, out syntheticBefore))
                {
                    for (var s = 0; s < syntheticBefore.Count; s++)
                    {
                        AddSyntheticOverlayRow(syntheticBefore[s], alignedRows.LeftLines, alignedRows.RightLines);
                    }

                    injectedSynthetic.Add(realIndex);
                }

                var row = rowStack.Pop();
                alignedRows.LeftLines.Add(row.Left ?? string.Empty);
                alignedRows.RightLines.Add(row.Right ?? string.Empty);
                if (!string.IsNullOrEmpty(row.Right))
                {
                    realIndex++;
                }
            }

            List<string> trailingSynthetic;
            if (!injectedSynthetic.Contains(realIndex) && rightSynthetic.TryGetValue(realIndex, out trailingSynthetic))
            {
                for (var s = 0; s < trailingSynthetic.Count; s++)
                {
                    AddSyntheticOverlayRow(trailingSynthetic[s], alignedRows.LeftLines, alignedRows.RightLines);
                }
            }
        }

        private static void AddSyntheticOverlayRow(string syntheticLine, List<string> alignedLeft, List<string> alignedRight)
        {
            var line = syntheticLine ?? string.Empty;
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//   -", StringComparison.Ordinal))
            {
                alignedLeft.Add(line);
                alignedRight.Add(string.Empty);
                return;
            }

            if (trimmed.StartsWith("//   +", StringComparison.Ordinal))
            {
                alignedLeft.Add(string.Empty);
                alignedRight.Add(line);
                return;
            }

            alignedLeft.Add(string.Empty);
            alignedRight.Add(line);
        }

        private static int[,] BuildLcsTable(List<string> left, List<string> right)
        {
            var m = left.Count;
            var n = right.Count;
            var lcs = new int[m + 1, n + 1];

            for (var i = 1; i <= m; i++)
            {
                for (var j = 1; j <= n; j++)
                {
                    if (string.Equals(left[i - 1], right[j - 1], StringComparison.Ordinal))
                    {
                        lcs[i, j] = lcs[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
                    }
                }
            }

            return lcs;
        }
    }
}
