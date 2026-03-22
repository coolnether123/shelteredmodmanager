using System.Text;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class HarmonyPatchDisplayService
    {
        public string BuildBadgeText(HarmonyMethodPatchSummary summary)
        {
            var counts = summary != null ? summary.Counts : null;
            if (counts == null || counts.TotalCount <= 0)
            {
                return string.Empty;
            }

            var breakdown = BuildCountBreakdown(counts);
            return !string.IsNullOrEmpty(breakdown) && counts.TotalCount <= 4
                ? breakdown
                : ("H: " + counts.TotalCount);
        }

        public string BuildCountBreakdown(HarmonyPatchCounts counts)
        {
            if (counts == null || counts.TotalCount <= 0)
            {
                return "No patches";
            }

            var builder = new StringBuilder();
            AppendCount(builder, "Pfx", counts.PrefixCount);
            AppendCount(builder, "Pof", counts.PostfixCount);
            if (counts.TranspilerCount > 0)
            {
                AppendCount(builder, "Trn", counts.TranspilerCount);
            }
            if (counts.FinalizerCount > 0)
            {
                AppendCount(builder, "Fin", counts.FinalizerCount);
            }

            return builder.Length > 0 ? builder.ToString() : ("H: " + counts.TotalCount);
        }

        public string BuildOwnerSummary(HarmonyMethodPatchSummary summary)
        {
            if (summary == null || summary.OwnerCount <= 0)
            {
                return "Patched by 0 mods";
            }

            return "Patched by " + summary.OwnerCount + " mod" + (summary.OwnerCount == 1 ? string.Empty : "s");
        }

        public string BuildTargetOrigin(HarmonyMethodPatchSummary summary)
        {
            if (summary == null)
            {
                return "Unknown";
            }

            if (!string.IsNullOrEmpty(summary.ProjectModId) && !string.IsNullOrEmpty(summary.LoadedModId))
            {
                return "Project " + summary.ProjectModId + " | Loaded mod " + summary.LoadedModId;
            }

            if (!string.IsNullOrEmpty(summary.ProjectModId))
            {
                return "Project " + summary.ProjectModId;
            }

            if (!string.IsNullOrEmpty(summary.LoadedModId))
            {
                return "Loaded mod " + summary.LoadedModId;
            }

            return "Unknown";
        }

        public string BuildPatchSummaryClipboardText(HarmonyMethodPatchSummary summary)
        {
            if (summary == null)
            {
                return "Harmony patch details are not available.";
            }

            var builder = new StringBuilder();
            builder.AppendLine(summary.ResolvedMemberDisplayName ?? BuildTargetDisplayName(summary));
            builder.AppendLine(BuildCountBreakdown(summary.Counts));
            builder.AppendLine(BuildOwnerSummary(summary));
            if (!string.IsNullOrEmpty(summary.ProjectModId) || !string.IsNullOrEmpty(summary.LoadedModId))
            {
                builder.AppendLine(BuildTargetOrigin(summary));
            }
            if (!string.IsNullOrEmpty(summary.ConflictHint))
            {
                builder.AppendLine(summary.ConflictHint);
            }

            if (summary.Entries != null && summary.Entries.Length > 0)
            {
                builder.AppendLine();
                for (var i = 0; i < summary.Entries.Length; i++)
                {
                    var entry = summary.Entries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    builder.Append("- ");
                    builder.Append(GetPatchKindLabel(entry.PatchKind));
                    builder.Append(": ");
                    builder.Append(entry.OwnerDisplayName ?? entry.OwnerId ?? "Unknown");
                    builder.Append(" | ");
                    builder.Append(entry.PatchMethodDeclaringType ?? string.Empty);
                    builder.Append(".");
                    builder.Append(entry.PatchMethodName ?? string.Empty);
                    builder.Append(" | priority ");
                    builder.Append(entry.Priority);
                    builder.Append(" | index ");
                    builder.Append(entry.Index);
                    if (entry.Before != null && entry.Before.Length > 0)
                    {
                        builder.Append(" | before ");
                        builder.Append(string.Join(", ", entry.Before));
                    }
                    if (entry.After != null && entry.After.Length > 0)
                    {
                        builder.Append(" | after ");
                        builder.Append(string.Join(", ", entry.After));
                    }
                    builder.AppendLine();
                }
            }

            return builder.ToString().Trim();
        }

        public string BuildTargetDisplayName(HarmonyMethodPatchSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(summary.ResolvedMemberDisplayName))
            {
                return summary.ResolvedMemberDisplayName;
            }

            return (summary.DeclaringType ?? string.Empty) + "." + (summary.MethodName ?? string.Empty) + (summary.Signature ?? string.Empty);
        }

        public string GetPatchKindLabel(HarmonyPatchKind patchKind)
        {
            switch (patchKind)
            {
                case HarmonyPatchKind.Prefix:
                    return "Prefix";
                case HarmonyPatchKind.Postfix:
                    return "Postfix";
                case HarmonyPatchKind.Transpiler:
                    return "Transpiler";
                case HarmonyPatchKind.Finalizer:
                    return "Finalizer";
                case HarmonyPatchKind.InnerPrefix:
                    return "Inner Prefix";
                case HarmonyPatchKind.InnerPostfix:
                    return "Inner Postfix";
                default:
                    return "Patch";
            }
        }

        private static void AppendCount(StringBuilder builder, string label, int value)
        {
            if (value <= 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(label);
            builder.Append(" ");
            builder.Append(value);
        }
    }
}
