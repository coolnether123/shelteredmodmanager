using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Plugin.Harmony.Services.Editor;
using Cortex.Plugin.Harmony.Services.Resolution;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Models;

namespace Cortex.Plugin.Harmony
{
    internal sealed partial class HarmonyWorkflowController
    {
        private const string HarmonyRelationshipNavigationPrefix = "harmony.relationship:";

        public WorkbenchEditorAdornment[] BuildAdornments(WorkbenchEditorAdornmentContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchEditorAdornment[0];
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            var workflow = _stateStore.GetWorkflow(runtime);
            if (context == null || runtime == null || context.Target == null)
            {
                return new WorkbenchEditorAdornment[0];
            }

            var label = "HM";
            var toolTip = "View Harmony context for " + BuildTargetDisplay(context.Target) + ".";
            if (workflow != null && workflow.IsInsertionSelectionActive)
            {
                label = "Pick Insert";
                toolTip = "Select a writable editor location for the pending Harmony patch.";
            }
            else
            {
                string statusMessage;
                var summary = TryResolveSummary(runtime, context.Target, false, out statusMessage);
                if (summary != null && summary.IsPatched)
                {
                    var badge = _displayService.BuildBadgeText(summary);
                    if (!string.IsNullOrEmpty(badge))
                    {
                        label = badge;
                    }

                    toolTip = _displayService.BuildCountBreakdown(summary.Counts);
                }
            }

            return new[]
            {
                new WorkbenchEditorAdornment
                {
                    AdornmentId = HarmonyPluginIds.EditorAdornmentId,
                    Label = label,
                    ToolTip = toolTip,
                    CommandId = HarmonyPluginIds.ViewPatchesCommandId,
                    CommandParameter = context.Target,
                    Placement = WorkbenchEditorAdornmentPlacement.TopRight,
                    Enabled = true,
                    SortOrder = 100
                }
            };
        }

        public WorkbenchMethodRelationship[] BuildIncomingRelationshipAugmentations(WorkbenchMethodInspectorContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchMethodRelationship[0];
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (runtime == null || context == null || context.Target == null)
            {
                return new WorkbenchMethodRelationship[0];
            }

            string statusMessage;
            var summary = TryResolveSummary(runtime, context.Target, false, out statusMessage);
            if (summary == null || !summary.IsPatched || summary.Entries == null || summary.Entries.Length == 0)
            {
                return new WorkbenchMethodRelationship[0];
            }

            var relationships = new List<WorkbenchMethodRelationship>();
            for (var i = 0; i < summary.Entries.Length; i++)
            {
                var entry = summary.Entries[i];
                if (entry == null || entry.NavigationTarget == null)
                {
                    continue;
                }

                relationships.Add(new WorkbenchMethodRelationship
                {
                    Title = BuildPatchRelationshipTitle(entry),
                    Detail = !string.IsNullOrEmpty(entry.OwnerDisplayName) ? entry.OwnerDisplayName : entry.OwnerId ?? string.Empty,
                    SymbolKind = "Method",
                    MetadataName = entry.PatchMethodName ?? string.Empty,
                    ContainingTypeName = entry.PatchMethodDeclaringType ?? string.Empty,
                    ContainingAssemblyName = !string.IsNullOrEmpty(entry.OwnerDisplayName) ? entry.OwnerDisplayName : entry.OwnerId ?? string.Empty,
                    DocumentationCommentId = CreateRelationshipNavigationToken(entry.NavigationTarget),
                    DefinitionDocumentPath = entry.NavigationTarget.DocumentPath ?? string.Empty,
                    Relationship = "Harmony " + _displayService.GetPatchKindLabel(entry.PatchKind),
                    CallCount = 1
                });
            }

            return relationships.ToArray();
        }

        public WorkbenchMethodRelationship[] BuildOutgoingRelationshipAugmentations(WorkbenchMethodInspectorContext context)
        {
            return new WorkbenchMethodRelationship[0];
        }

        public MethodInspectorSectionViewModel BuildInspectorSection(WorkbenchMethodInspectorContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return null;
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (context == null || runtime == null || context.Target == null)
            {
                return null;
            }

            string statusMessage;
            var summary = TryResolveSummary(runtime, context.Target, false, out statusMessage);
            HarmonySourcePatchContext sourcePatchContext;
            string sourceReason;
            _resolver.TryResolveSourcePatchContext(runtime, context.Target, out sourcePatchContext, out sourceReason);

            var elements = new List<MethodInspectorElementViewModel>();
            if (sourcePatchContext != null)
            {
                elements.Add(CreateMetadata("Current Method", "Harmony " + (sourcePatchContext.PatchKind ?? string.Empty) + " patch"));
                elements.Add(CreateMetadata("Patches Into", sourcePatchContext.Target != null ? sourcePatchContext.Target.DisplayName ?? string.Empty : string.Empty));
                elements.Add(CreateMetadata("Resolved Via", sourcePatchContext.ResolutionSource ?? string.Empty));
                elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
            }

            if (summary != null)
            {
                elements.Add(CreateMetadata("Target", _displayService.BuildTargetDisplayName(summary)));
                elements.Add(CreateMetadata("Counts", _displayService.BuildCountBreakdown(summary.Counts)));
                elements.Add(CreateMetadata("Owners", _displayService.BuildOwnerSummary(summary)));
                elements.Add(CreateMetadata("Status", !string.IsNullOrEmpty(summary.ConflictHint) ? summary.ConflictHint : "Patched"));
                AppendPatchCards(elements, summary);
            }
            else
            {
                elements.Add(CreateText(string.Empty, !string.IsNullOrEmpty(statusMessage) ? statusMessage : (sourceReason ?? "No live Harmony patches are registered for this method."), false));
            }

            AppendIndirectRelationships(runtime, context, elements);
            AppendGenerationActions(runtime, context.Target, elements);

            return new MethodInspectorSectionViewModel
            {
                Id = HarmonyPluginIds.InspectorSectionId,
                Title = "Harmony",
                Expanded = true,
                Elements = elements.ToArray()
            };
        }

        public MethodInspectorActionViewModel[] BuildRelationshipActions(WorkbenchMethodRelationshipActionContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new MethodInspectorActionViewModel[0];
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            var relationship = context != null ? context.Relationship : null;
            if (runtime == null || relationship == null)
            {
                return new MethodInspectorActionViewModel[0];
            }

            HarmonyPatchNavigationTarget patchTarget;
            if (TryParseRelationshipNavigationTarget(relationship, out patchTarget))
            {
                return _navigationActionFactory.CreatePatchNavigationActions(
                    patchTarget,
                    "Open",
                    "Open this Harmony patch method.");
            }

            var target = ToCommandTarget(relationship);
            if (target == null)
            {
                return new MethodInspectorActionViewModel[0];
            }

            string statusMessage;
            var summary = TryResolveSummary(runtime, target, false, out statusMessage);
            if (summary == null || !summary.IsPatched || summary.Entries == null || summary.Entries.Length == 0)
            {
                return new MethodInspectorActionViewModel[0];
            }

            return _navigationActionFactory.CreatePatchNavigationActions(
                summary.Entries[0].NavigationTarget,
                "Open Patch Method",
                "Open the matching Harmony patch method.");
        }

        public WorkbenchMethodInspectorActionResult HandleInspectorAction(WorkbenchMethodInspectorActionContext context)
        {
            if (!IsRuntimeAvailable)
            {
                return new WorkbenchMethodInspectorActionResult();
            }

            var runtime = context != null ? HarmonyModuleRuntimeLocator.Get(context.Runtime) : null;
            if (context == null || runtime == null || string.IsNullOrEmpty(context.ActionId))
            {
                return new WorkbenchMethodInspectorActionResult();
            }

            HarmonyPatchNavigationTarget target;
            if (HarmonyMethodInspectorNavigationActionCodec.TryParse(context.ActionId, out target))
            {
                string statusMessage;
                return new WorkbenchMethodInspectorActionResult
                {
                    Handled = NavigateToPatch(runtime, target, out statusMessage)
                };
            }

            string actionStatus;
            bool executed;
            switch (context.ActionId)
            {
                case HarmonyPluginIds.ViewPatchesCommandId:
                    executed = runtime.Commands != null && runtime.Commands.Execute(HarmonyPluginIds.ViewPatchesCommandId, context.Target);
                    return new WorkbenchMethodInspectorActionResult { Handled = executed };
                case HarmonyPluginIds.GeneratePrefixCommandId:
                    executed = runtime.Commands != null && runtime.Commands.Execute(HarmonyPluginIds.GeneratePrefixCommandId, context.Target);
                    return new WorkbenchMethodInspectorActionResult { Handled = executed, CloseInspector = executed };
                case HarmonyPluginIds.GeneratePostfixCommandId:
                    executed = runtime.Commands != null && runtime.Commands.Execute(HarmonyPluginIds.GeneratePostfixCommandId, context.Target);
                    return new WorkbenchMethodInspectorActionResult { Handled = executed, CloseInspector = executed };
                case HarmonyPluginIds.CopySummaryCommandId:
                    executed = runtime.Commands != null && runtime.Commands.Execute(HarmonyPluginIds.CopySummaryCommandId, context.Target);
                    return new WorkbenchMethodInspectorActionResult { Handled = executed };
                case "cortex.harmony.navigateTarget":
                    return new WorkbenchMethodInspectorActionResult
                    {
                        Handled = NavigateToTarget(runtime, out actionStatus)
                    };
            }

            if (context.ActionId.StartsWith("cortex.harmony.openInsertion.", StringComparison.Ordinal))
            {
                var indexText = context.ActionId.Substring("cortex.harmony.openInsertion.".Length);
                int index;
                if (int.TryParse(indexText, out index))
                {
                    var workflow = _stateStore.GetWorkflow(runtime);
                    DocumentSession session;
                    if (workflow != null &&
                        index >= 0 &&
                        index < workflow.InsertionTargets.Count &&
                        _insertionService.TryOpenInsertionTarget(runtime, workflow.InsertionTargets[index], out session, out actionStatus))
                    {
                        return new WorkbenchMethodInspectorActionResult
                        {
                            Handled = true,
                            CloseInspector = true
                        };
                    }
                }
            }

            return new WorkbenchMethodInspectorActionResult();
        }

        private void AppendPatchCards(List<MethodInspectorElementViewModel> elements, HarmonyMethodPatchSummary summary)
        {
            var entries = summary != null ? summary.Entries ?? new HarmonyPatchEntry[0] : new HarmonyPatchEntry[0];
            for (var i = 0; i < entries.Length && i < 6; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                elements.Add(new MethodInspectorCardViewModel
                {
                    Title = _displayService.GetPatchKindLabel(entry.PatchKind) + ": " + (entry.PatchMethodName ?? entry.OwnerDisplayName ?? entry.OwnerId ?? string.Empty),
                    Rows = new[]
                    {
                        CreateMetadata("Owner", !string.IsNullOrEmpty(entry.OwnerDisplayName) ? entry.OwnerDisplayName : entry.OwnerId),
                        CreateMetadata("Priority", entry.Priority.ToString()),
                        CreateMetadata("Patch Method", (entry.PatchMethodDeclaringType ?? string.Empty) + "." + (entry.PatchMethodName ?? string.Empty) + (entry.PatchMethodSignature ?? string.Empty)),
                        CreateMetadata("Order", "Before: " + Join(entry.Before) + " | After: " + Join(entry.After))
                    },
                    Actions = _navigationActionFactory.CreatePatchNavigationActions(entry.NavigationTarget, "Open Patch Method", "Open this patch method.")
                });
            }
        }

        private void AppendIndirectRelationships(IWorkbenchModuleRuntime runtime, WorkbenchMethodInspectorContext context, List<MethodInspectorElementViewModel> elements)
        {
            var incoming = context != null && context.Relationships != null ? context.Relationships.IncomingCalls ?? new WorkbenchMethodRelationship[0] : new WorkbenchMethodRelationship[0];
            var patched = 0;
            for (var i = 0; i < incoming.Length && patched < 4; i++)
            {
                var caller = incoming[i];
                var target = caller != null ? ToCommandTarget(caller) : null;
                if (target == null)
                {
                    continue;
                }

                string statusMessage;
                var summary = TryResolveSummary(runtime, target, false, out statusMessage);
                if (summary == null || !summary.IsPatched)
                {
                    continue;
                }

                if (patched == 0)
                {
                    elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
                    elements.Add(CreateText("Indirect Harmony", "Incoming callers with live Harmony patches.", false));
                }

                patched++;
                elements.Add(new MethodInspectorCardViewModel
                {
                    Title = caller.Title ?? "Patched Caller",
                    Rows = new[]
                    {
                        CreateMetadata("Relationship", (caller.Relationship ?? "Call") + " (" + caller.CallCount + ")"),
                        CreateMetadata("Type", caller.ContainingTypeName ?? string.Empty),
                        CreateMetadata("Counts", _displayService.BuildCountBreakdown(summary.Counts))
                    },
                    Actions = summary.Entries != null && summary.Entries.Length > 0
                        ? _navigationActionFactory.CreatePatchNavigationActions(summary.Entries[0].NavigationTarget, "Open Patch Method", "Open the matching patch method.")
                        : new MethodInspectorActionViewModel[0]
                });
            }
        }

        private void AppendGenerationActions(IWorkbenchModuleRuntime runtime, EditorCommandTarget target, List<MethodInspectorElementViewModel> elements)
        {
            var workflow = _stateStore.GetWorkflow(runtime);
            if (workflow == null)
            {
                return;
            }

            var commands = runtime != null ? runtime.Commands : null;
            var canViewPatches = commands != null && commands.CanExecute(HarmonyPluginIds.ViewPatchesCommandId, target);
            var canGeneratePrefix = commands != null && commands.CanExecute(HarmonyPluginIds.GeneratePrefixCommandId, target);
            var canGeneratePostfix = commands != null && commands.CanExecute(HarmonyPluginIds.GeneratePostfixCommandId, target);
            var canCopySummary = commands != null && commands.CanExecute(HarmonyPluginIds.CopySummaryCommandId, target);

            elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel { Id = HarmonyPluginIds.ViewPatchesCommandId, Label = "View Harmony Patches", Enabled = canViewPatches },
                Hint = "Open the Harmony tool window for this target."
            });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel { Id = "cortex.harmony.navigateTarget", Label = "Navigate To Target", Enabled = workflow.ActiveSummary != null && workflow.ActiveSummary.Target != null },
                Hint = "Open the resolved runtime target."
            });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel { Id = HarmonyPluginIds.GeneratePrefixCommandId, Label = "Generate Prefix", Enabled = canGeneratePrefix },
                Hint = "Prepare a Prefix patch workflow."
            });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel { Id = HarmonyPluginIds.GeneratePostfixCommandId, Label = "Generate Postfix", Enabled = canGeneratePostfix },
                Hint = "Prepare a Postfix patch workflow."
            });
            elements.Add(new MethodInspectorActionElementViewModel
            {
                Action = new MethodInspectorActionViewModel { Id = HarmonyPluginIds.CopySummaryCommandId, Label = "Copy Summary", Enabled = canCopySummary },
                Hint = "Copy the current Harmony summary."
            });

            for (var i = 0; i < workflow.InsertionTargets.Count && i < 4; i++)
            {
                var current = workflow.InsertionTargets[i];
                if (current == null)
                {
                    continue;
                }

                elements.Add(new MethodInspectorCardViewModel
                {
                    Title = current.DisplayName ?? current.FilePath ?? string.Empty,
                    Body = current.Reason ?? string.Empty,
                    Actions = new[]
                    {
                        new MethodInspectorActionViewModel
                        {
                            Id = "cortex.harmony.openInsertion." + i.ToString(),
                            Label = "Open And Place Here",
                            Enabled = true
                        }
                    }
                });
            }
        }

        private static MethodInspectorMetadataViewModel CreateMetadata(string label, string value)
        {
            return new MethodInspectorMetadataViewModel
            {
                Label = label ?? string.Empty,
                Value = !string.IsNullOrEmpty(value) ? value : "Unknown"
            };
        }

        private static MethodInspectorTextViewModel CreateText(string label, string value, bool monospace)
        {
            return new MethodInspectorTextViewModel
            {
                Label = label ?? string.Empty,
                Value = value ?? string.Empty,
                Monospace = monospace
            };
        }

        private static string BuildPatchRelationshipTitle(HarmonyPatchEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var typeName = entry.PatchMethodDeclaringType ?? string.Empty;
            var methodName = entry.PatchMethodName ?? string.Empty;
            if (string.IsNullOrEmpty(typeName))
            {
                return methodName;
            }

            return typeName + "." + methodName;
        }

        private static string CreateRelationshipNavigationToken(HarmonyPatchNavigationTarget target)
        {
            var encoded = HarmonyMethodInspectorNavigationActionCodec.Create(target);
            return string.IsNullOrEmpty(encoded)
                ? string.Empty
                : HarmonyRelationshipNavigationPrefix + encoded;
        }

        private static bool TryParseRelationshipNavigationTarget(WorkbenchMethodRelationship relationship, out HarmonyPatchNavigationTarget target)
        {
            target = null;
            var token = relationship != null ? relationship.DocumentationCommentId ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(token) || !token.StartsWith(HarmonyRelationshipNavigationPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            return HarmonyMethodInspectorNavigationActionCodec.TryParse(
                token.Substring(HarmonyRelationshipNavigationPrefix.Length),
                out target);
        }

        private static EditorCommandTarget ToCommandTarget(WorkbenchMethodRelationship relationship)
        {
            if (relationship == null)
            {
                return null;
            }

            return new EditorCommandTarget
            {
                SymbolText = relationship.Title ?? string.Empty,
                MetadataName = relationship.MetadataName ?? string.Empty,
                SymbolKind = relationship.SymbolKind ?? string.Empty,
                ContainingTypeName = relationship.ContainingTypeName ?? string.Empty,
                ContainingAssemblyName = relationship.ContainingAssemblyName ?? string.Empty,
                DocumentationCommentId = relationship.DocumentationCommentId ?? string.Empty,
                DefinitionDocumentPath = relationship.DefinitionDocumentPath ?? string.Empty,
                DocumentPath = relationship.DefinitionDocumentPath ?? string.Empty,
                QualifiedSymbolDisplay = relationship.Detail ?? relationship.Title ?? string.Empty
            };
        }
    }
}
