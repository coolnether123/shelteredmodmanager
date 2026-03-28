using System.Linq;
using Cortex;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Models;
using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorMethodInspectorPresentationServiceTests
    {
        [Fact]
        public void ShouldShowHarmony_ReturnsFalseForOrdinaryMethod()
        {
            var result = EditorMethodInspectorPresentationService.ShouldShowHarmony(
                new EditorSourceHarmonyContext(),
                null,
                new EditorIndirectHarmonyContext());

            Assert.False(result);
        }

        [Fact]
        public void ShouldShowHarmony_ReturnsTrueForDirectHarmonyPatch()
        {
            var result = EditorMethodInspectorPresentationService.ShouldShowHarmony(
                new EditorSourceHarmonyContext
                {
                    IsPatchMethod = true
                },
                null,
                new EditorIndirectHarmonyContext());

            Assert.True(result);
        }

        [Fact]
        public void ShouldShowHarmony_ReturnsTrueOnlyWhenIndirectContextHasPatchedCallers()
        {
            var noIndirect = EditorMethodInspectorPresentationService.ShouldShowHarmony(
                new EditorSourceHarmonyContext(),
                null,
                new EditorIndirectHarmonyContext
                {
                    PatchedCallerCount = 0
                });
            var hasIndirect = EditorMethodInspectorPresentationService.ShouldShowHarmony(
                new EditorSourceHarmonyContext(),
                null,
                new EditorIndirectHarmonyContext
                {
                    PatchedCallerCount = 1
                });

            Assert.False(noIndirect);
            Assert.True(hasIndirect);
        }

        [Fact]
        public void BuildDocument_NonHarmonyMethod_OmitsHarmonySection_AndShowsRelationships()
        {
            var service = new EditorMethodInspectorPresentationService(null);
            var inspector = new CortexMethodInspectorState
            {
                Title = "Example",
                RelationshipsExpanded = true
            };
            var target = new EditorCommandTarget
            {
                SymbolText = "Example",
                SymbolKind = "Method",
                DocumentPath = @"D:\workspace\Example.cs",
                DefinitionDocumentPath = @"D:\workspace\Example.cs",
                DefinitionLine = 5,
                DefinitionColumn = 9,
                Line = 5,
                Column = 9,
                ContainingTypeName = "SampleType",
                ContainingAssemblyName = "Sample.Assembly",
                QualifiedSymbolDisplay = "Sample.Namespace.SampleType.Example()"
            };
            var session = new DocumentSession
            {
                FilePath = target.DocumentPath,
                Text = "class SampleType\r\n{\r\n    void Example() { CallA(); }\r\n}\r\n"
            };
            var relationships = new EditorMethodRelationshipsContext
            {
                IsExpanded = true,
                HasResponse = true,
                StatusMessage = "Semantic call hierarchy resolved.",
                IncomingCalls = new[]
                {
                    new LanguageServiceCallHierarchyItem
                    {
                        SymbolDisplay = "CallerA()",
                        ContainingTypeName = "IncomingType",
                        Relationship = "Incoming Call",
                        CallCount = 2
                    }
                },
                OutgoingCalls = new[]
                {
                    new LanguageServiceCallHierarchyItem
                    {
                        SymbolDisplay = "DependencyA()",
                        ContainingTypeName = "OutgoingType",
                        Relationship = "Outgoing Call",
                        CallCount = 1
                    }
                },
                IncomingCallCount = 1,
                OutgoingCallCount = 1
            };

            var document = service.BuildDocument(
                null,
                session,
                inspector,
                target,
                relationships,
                new EditorSourceHarmonyContext(),
                null,
                string.Empty,
                new EditorIndirectHarmonyContext(),
                new HarmonyPatchDisplayService(),
                false,
                false,
                false,
                string.Empty);

            Assert.DoesNotContain(document.Sections, section => section.Title == "Harmony Context");

            var relationshipsSection = document.Sections.Single(section => section.Title == "Relationships");
            Assert.Contains(relationshipsSection.Elements.OfType<PanelMetadataElement>(), element => element.Label == "Depends On" && element.Value == "1");
            Assert.Contains(relationshipsSection.Elements.OfType<PanelMetadataElement>(), element => element.Label == "Used By" && element.Value == "1");
            Assert.Contains(relationshipsSection.Elements.OfType<PanelCardElement>(), element => element.Title == "DependencyA()");
            Assert.Contains(relationshipsSection.Elements.OfType<PanelCardElement>(), element => element.Title == "CallerA()");
        }

        [Fact]
        public void BuildDocument_DirectHarmonyPatch_IncludesHarmonySection()
        {
            var service = new EditorMethodInspectorPresentationService(null);
            var inspector = new CortexMethodInspectorState
            {
                Title = "Prefix",
                HarmonyExpanded = true
            };
            var target = new EditorCommandTarget
            {
                SymbolText = "Prefix",
                SymbolKind = "Method",
                DocumentPath = @"D:\workspace\Patch.cs",
                DefinitionDocumentPath = @"D:\workspace\Patch.cs",
                DefinitionLine = 3,
                DefinitionColumn = 5,
                Line = 3,
                Column = 5,
                ContainingTypeName = "PatchType",
                QualifiedSymbolDisplay = "Sample.Namespace.PatchType.Prefix()"
            };

            var document = service.BuildDocument(
                null,
                new DocumentSession { FilePath = target.DocumentPath, Text = "class PatchType { void Prefix() { } }" },
                inspector,
                target,
                new EditorMethodRelationshipsContext(),
                new EditorSourceHarmonyContext
                {
                    IsPatchMethod = true,
                    StatusMessage = "This method is a Harmony Prefix patch.",
                    PatchKind = "Prefix",
                    SourceMethodName = "Prefix",
                    TargetDisplayName = "GameType.Target",
                    TargetTypeName = "GameType",
                    TargetMethodName = "Target",
                    TargetSignature = "()",
                    ResolutionSource = "attribute"
                },
                null,
                "No live Harmony patches are registered for the patched runtime target.",
                new EditorIndirectHarmonyContext(),
                new HarmonyPatchDisplayService(),
                true,
                false,
                false,
                string.Empty);

            var harmonySection = document.Sections.Single(section => section.Title == "Harmony Context");
            Assert.Contains(harmonySection.Elements.OfType<PanelMetadataElement>(), element => element.Label == "Patch Kind" && element.Value == "Prefix");
        }

        [Fact]
        public void BuildDocument_IndirectHarmonyContext_ShowsHarmonyOnlyWhenPatchedCallersExist()
        {
            var service = new EditorMethodInspectorPresentationService(null);
            var inspector = new CortexMethodInspectorState
            {
                Title = "Worker"
            };
            var target = new EditorCommandTarget
            {
                SymbolText = "Worker",
                SymbolKind = "Method",
                DocumentPath = @"D:\workspace\Worker.cs",
                DefinitionDocumentPath = @"D:\workspace\Worker.cs",
                DefinitionLine = 3,
                DefinitionColumn = 5,
                Line = 3,
                Column = 5,
                ContainingTypeName = "WorkerType",
                QualifiedSymbolDisplay = "Sample.Namespace.WorkerType.Worker()"
            };
            var relationships = new EditorMethodRelationshipsContext();
            var noIndirectDocument = service.BuildDocument(
                null,
                new DocumentSession { FilePath = target.DocumentPath, Text = "class WorkerType { void Worker() { } }" },
                inspector,
                target,
                relationships,
                new EditorSourceHarmonyContext(),
                null,
                string.Empty,
                new EditorIndirectHarmonyContext(),
                new HarmonyPatchDisplayService(),
                false,
                false,
                false,
                string.Empty);
            var indirectDocument = service.BuildDocument(
                null,
                new DocumentSession { FilePath = target.DocumentPath, Text = "class WorkerType { void Worker() { } }" },
                inspector,
                target,
                relationships,
                new EditorSourceHarmonyContext(),
                null,
                string.Empty,
                new EditorIndirectHarmonyContext
                {
                    PatchedCallerCount = 1,
                    IncomingCallerCount = 2,
                    StatusMessage = "This method is called by 1 directly patched caller."
                },
                new HarmonyPatchDisplayService(),
                true,
                false,
                false,
                string.Empty);

            Assert.DoesNotContain(noIndirectDocument.Sections, section => section.Title == "Harmony Context");
            Assert.Contains(indirectDocument.Sections, section => section.Title == "Harmony Context");
        }
    }
}
