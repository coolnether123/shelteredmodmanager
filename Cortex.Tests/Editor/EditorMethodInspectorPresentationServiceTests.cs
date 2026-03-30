using System.Linq;
using System.Reflection;
using Cortex;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Editor;
using Cortex.Presentation.Models;
using Cortex.Rendering.Models;
using Cortex.Services.Harmony.Editor;
using Cortex.Services.Harmony.Presentation;
using Cortex.Services.Inspector.Relationships;
using Cortex.Tests.Testing;
using Xunit;
using Cortex.Services.Inspector;

namespace Cortex.Tests.Editor
{
    public sealed class EditorMethodInspectorPresentationServiceTests
    {
        private interface IContractExample
        {
            void Execute(string input);
        }

        private sealed class ContractExample : IContractExample
        {
            public void Execute(string input)
            {
            }
        }

        private sealed class OverloadedExample
        {
            public void Execute(string input)
            {
            }

            public void Execute(int input)
            {
            }
        }

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
        public void BuildViewModel_NonHarmonyMethod_OmitsHarmonySection_AndShowsRelationships()
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
                    new EditorMethodRelationshipItem
                    {
                        Title = "CallerA()",
                        ContainingTypeName = "IncomingType",
                        Relationship = "Incoming Call",
                        CallCount = 2,
                        SymbolKind = "Method",
                        MetadataName = "CallerA",
                        ContainingAssemblyName = "Incoming.Assembly"
                    }
                },
                OutgoingCalls = new[]
                {
                    new EditorMethodRelationshipItem
                    {
                        Title = "DependencyA()",
                        ContainingTypeName = "OutgoingType",
                        Relationship = "Outgoing Call",
                        CallCount = 1,
                        SymbolKind = "Method",
                        MetadataName = "DependencyA",
                        ContainingAssemblyName = "Outgoing.Assembly"
                    }
                },
                IncomingCallCount = 1,
                OutgoingCallCount = 1
            };

            var document = service.BuildViewModel(
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
            Assert.Contains(relationshipsSection.Elements.OfType<MethodInspectorMetadataViewModel>(), element => element.Label == "Depends On" && element.Value == "1");
            Assert.Contains(relationshipsSection.Elements.OfType<MethodInspectorMetadataViewModel>(), element => element.Label == "Used By" && element.Value == "1");
            Assert.Contains(relationshipsSection.Elements.OfType<MethodInspectorCardViewModel>(), element => element.Title == "DependencyA()");
            Assert.Contains(relationshipsSection.Elements.OfType<MethodInspectorCardViewModel>(), element => element.Title == "CallerA()");
            Assert.Contains(relationshipsSection.Elements.OfType<MethodInspectorCardViewModel>(), element => element.Actions.Length == 1 && element.Actions[0].Label == "Open");
        }

        [Fact]
        public void BuildViewModel_DirectHarmonyPatch_IncludesHarmonySection()
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

            var document = service.BuildViewModel(
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
            Assert.Contains(harmonySection.Elements.OfType<MethodInspectorMetadataViewModel>(), element => element.Label == "Patch Kind" && element.Value == "Prefix");
        }

        [Fact]
        public void BuildViewModel_IndirectHarmonyContext_ShowsHarmonyOnlyWhenPatchedCallersExist()
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
            var noIndirectDocument = service.BuildViewModel(
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
            var indirectDocument = service.BuildViewModel(
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

        [Fact]
        public void BuildViewModel_UsedByRelationship_KeepsGenericSymbolAction_WhenHarmonyAssociationExists()
        {
            var service = new EditorMethodInspectorPresentationService(null);
            var state = new CortexShellState
            {
                SelectedProject = new CortexProjectDefinition
                {
                    ModId = "coolnether123.sheltereddisplayfixes",
                    SourceRootPath = @"D:\Projects\Sheltered Modding\Sheltered Display Fixes"
                }
            };
            var inspector = new CortexMethodInspectorState
            {
                Title = "Worker",
                RelationshipsExpanded = true
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
            var relationships = new EditorMethodRelationshipsContext
            {
                IsExpanded = true,
                HasResponse = true,
                IncomingCalls = new[]
                {
                    new EditorMethodRelationshipItem
                    {
                        Title = "OnShow()",
                        SymbolKind = "Method",
                        MetadataName = "OnShow",
                        ContainingTypeName = "CustomisationPanel",
                        ContainingAssemblyName = "Sheltered Display Fixes",
                        DocumentationCommentId = "M:ShelteredDisplayFixes.CustomisationPanel.OnShow",
                        Relationship = "Incoming Call",
                        CallCount = 1
                    }
                },
                IncomingCallCount = 1
            };
            var indirectHarmonyContext = new EditorIndirectHarmonyContext
            {
                PatchedCallerCount = 1,
                IncomingCallerCount = 1,
                PatchedCallers = new[]
                {
                    new EditorIndirectHarmonyCallerContext
                    {
                        Caller = new LanguageServiceCallHierarchyItem
                        {
                            SymbolDisplay = "OnShow()",
                            SymbolKind = "Method",
                            MetadataName = "OnShow",
                            ContainingTypeName = "CustomisationPanel",
                            ContainingAssemblyName = "Assembly-CSharp",
                            DocumentationCommentId = "M:Game.CustomisationPanel.OnShow",
                            Relationship = "Incoming Call",
                            CallCount = 1
                        },
                        Summary = new HarmonyMethodPatchSummary
                        {
                            IsPatched = true,
                            Counts = new HarmonyPatchCounts { PrefixCount = 1, TotalCount = 1 },
                            Owners = new[] { "coolnether123.sheltereddisplayfixes" },
                            Entries = new[]
                            {
                                new HarmonyPatchEntry
                                {
                                    OwnerId = "modapi.core",
                                    OwnerAssociation = new HarmonyPatchOwnerAssociation
                                    {
                                        LoadedModId = "modapi.core",
                                        HasMatch = true
                                    },
                                    NavigationTarget = new HarmonyPatchNavigationTarget
                                    {
                                        AssemblyPath = "ModAPI",
                                        MetadataToken = 10,
                                        MethodName = "Prefix"
                                    }
                                },
                                new HarmonyPatchEntry
                                {
                                    OwnerId = "coolnether123.sheltereddisplayfixes",
                                    OwnerAssociation = new HarmonyPatchOwnerAssociation
                                    {
                                        ProjectModId = "coolnether123.sheltereddisplayfixes",
                                        ProjectSourceRootPath = @"D:\Projects\Sheltered Modding\Sheltered Display Fixes",
                                        HasMatch = true
                                    },
                                    NavigationTarget = new HarmonyPatchNavigationTarget
                                    {
                                        AssemblyPath = "Sheltered.DisplayFixes",
                                        MetadataToken = 42,
                                        MethodName = "OnShowPatch"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var document = service.BuildViewModel(
                state,
                new DocumentSession { FilePath = target.DocumentPath, Text = "class WorkerType { void Worker() { } }" },
                inspector,
                target,
                relationships,
                new EditorSourceHarmonyContext(),
                null,
                string.Empty,
                indirectHarmonyContext,
                new HarmonyPatchDisplayService(),
                true,
                false,
                false,
                string.Empty);

            var relationshipsSection = document.Sections.Single(section => section.Title == "Relationships");
            var callerCard = relationshipsSection.Elements.OfType<MethodInspectorCardViewModel>().Single(element => element.Title == "OnShow()");
            string symbolKind;
            string metadataName;
            string containingTypeName;
            string containingAssemblyName;
            string documentationCommentId;
            string definitionDocumentPath;
            LanguageServiceRange definitionRange;

            Assert.Single(callerCard.Actions);
            Assert.True(EditorMethodInspectorNavigationActionCodec.TryParse(
                callerCard.Actions[0].Id,
                out symbolKind,
                out metadataName,
                out containingTypeName,
                out containingAssemblyName,
                out documentationCommentId,
                out definitionDocumentPath,
                out definitionRange));
            Assert.Equal("Method", symbolKind);
            Assert.Equal("OnShow", metadataName);
            Assert.Equal("CustomisationPanel", containingTypeName);
        }

        [Fact]
        public void PanelDocumentAdapter_MapsPresentationModel_ToRenderDocument()
        {
            var adapter = new EditorMethodInspectorPanelDocumentAdapter();
            var viewModel = new MethodInspectorViewModel
            {
                Title = "Method Info: Example",
                Subtitle = "Sample.Namespace | SampleType",
                Sections = new[]
                {
                    new MethodInspectorSectionViewModel
                    {
                        Id = "relationships",
                        Title = "Relationships",
                        Expanded = true,
                        Elements = new MethodInspectorElementViewModel[]
                        {
                            new MethodInspectorMetadataViewModel
                            {
                                Label = "Depends On",
                                Value = "2"
                            }
                        }
                    }
                }
            };

            var document = adapter.Build(viewModel);

            Assert.Equal("Method Info: Example", document.Title);
            Assert.Equal("Sample.Namespace | SampleType", document.Subtitle);
            Assert.Single(document.Sections);
            Assert.Equal("relationships", document.Sections[0].Id);
            Assert.Contains(document.Sections[0].Elements.OfType<PanelMetadataElement>(), element => element.Label == "Depends On" && element.Value == "2");
        }

        [Fact]
        public void RelationshipsContext_AddsSignatureDependencies_AndContractUsage()
        {
            var method = typeof(ContractExample).GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);

            var inspector = new CortexMethodInspectorState
            {
                RelationshipsExpanded = true,
                RelationshipsRequested = true,
                RelationshipsRequestKey = string.Empty,
                RelationshipsCallHierarchy = new LanguageServiceCallHierarchyResponse
                {
                    Success = true,
                    IncomingCalls = new LanguageServiceCallHierarchyItem[0],
                    OutgoingCalls = new LanguageServiceCallHierarchyItem[0]
                }
            };
            var target = new EditorCommandTarget
            {
                MetadataName = method.Name,
                SymbolText = method.Name,
                SymbolKind = "Method",
                ContainingAssemblyName = method.DeclaringType.Assembly.GetName().Name,
                ContainingTypeName = method.DeclaringType.FullName,
                DocumentationCommentId = "M:" + method.DeclaringType.FullName.Replace('+', '.') + "." + method.Name + "(System.String)"
            };

            var context = new EditorMethodRelationshipsContextService().BuildContext(inspector, target);

            Assert.Contains(context.OutgoingCalls, item => item.Relationship == "Parameter Type" && item.Title == "String");
            Assert.Contains(context.IncomingCalls, item => item.Relationship == "Implemented Contract" && item.ContainingTypeName == typeof(IContractExample).FullName);
        }

        [Fact]
        public void RelationshipsContext_UsesQualifiedSignature_ToResolveCorrectOverload()
        {
            var inspector = new CortexMethodInspectorState
            {
                RelationshipsExpanded = true,
                RelationshipsRequested = true,
                RelationshipsRequestKey = string.Empty,
                RelationshipsCallHierarchy = new LanguageServiceCallHierarchyResponse
                {
                    Success = true,
                    IncomingCalls = new LanguageServiceCallHierarchyItem[0],
                    OutgoingCalls = new LanguageServiceCallHierarchyItem[0]
                }
            };
            var target = new EditorCommandTarget
            {
                MetadataName = "Execute",
                SymbolText = "Execute",
                SymbolKind = "Method",
                ContainingAssemblyName = typeof(OverloadedExample).Assembly.GetName().Name,
                ContainingTypeName = typeof(OverloadedExample).Name,
                QualifiedSymbolDisplay = typeof(OverloadedExample).FullName + ".Execute(string input)"
            };

            var context = new EditorMethodRelationshipsContextService().BuildContext(inspector, target);

            Assert.Contains(context.OutgoingCalls, item => item.Relationship == "Parameter Type" && item.Title == "String");
            Assert.DoesNotContain(context.OutgoingCalls, item => item.Relationship == "Parameter Type" && item.Title == "Int32");
        }

        [Fact]
        public void RelationshipsContext_SkipsAmbiguousAugmentation_WhenExactMethodIdentityIsUnavailable()
        {
            var inspector = new CortexMethodInspectorState
            {
                RelationshipsExpanded = true,
                RelationshipsRequested = true,
                RelationshipsRequestKey = string.Empty,
                RelationshipsCallHierarchy = new LanguageServiceCallHierarchyResponse
                {
                    Success = true,
                    IncomingCalls = new LanguageServiceCallHierarchyItem[0],
                    OutgoingCalls = new LanguageServiceCallHierarchyItem[0]
                }
            };
            var target = new EditorCommandTarget
            {
                MetadataName = "Execute",
                SymbolText = "Execute",
                SymbolKind = "Method",
                ContainingAssemblyName = typeof(OverloadedExample).Assembly.GetName().Name,
                ContainingTypeName = typeof(OverloadedExample).Name
            };

            var context = new EditorMethodRelationshipsContextService().BuildContext(inspector, target);

            Assert.DoesNotContain(context.OutgoingCalls, item => item.Relationship == "Parameter Type");
            Assert.DoesNotContain(context.IncomingCalls, item => item.Relationship == "Implemented Contract" || item.Relationship == "Override Contract");
        }
    }
}
