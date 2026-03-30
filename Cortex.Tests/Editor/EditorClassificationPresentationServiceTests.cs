using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;
using Cortex.Services.Editor.Presentation;

namespace Cortex.Tests.Editor
{
    public sealed class EditorClassificationPresentationServiceTests
    {
        [Fact]
        public void GetEffectiveCodeViewClassification_InvocationIdentifier_PromotesToMethod()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorFallbackColoringService();

                var effective = service.GetEffectiveCodeViewClassification("identifier", "ShouldOverrideCharacterRender", ".", "(", ")");

                Assert.Equal("method", effective);
            });
        }

        [Fact]
        public void GetEffectiveCodeViewClassification_PascalCaseReceiverBeforeDot_PromotesToClass()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorFallbackColoringService();

                var effective = service.GetEffectiveCodeViewClassification("identifier", "DisplayRenderUtility", "(", ".", "ShouldOverrideCharacterRender");

                Assert.Equal("class", effective);
            });
        }

        [Fact]
        public void IsHoverCandidate_GenericIdentifier_IsInteractive()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                Assert.True(service.IsHoverCandidate("identifier", "ShouldOverrideCharacterRender"));
            });
        }

        [Fact]
        public void CanNavigateToDefinition_GenericIdentifier_IsAllowed()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                Assert.True(service.CanNavigateToDefinition("identifier", "DisplayRenderUtility"));
            });
        }

        [Fact]
        public void GetEffectiveLineTokenClassification_SourceInvocationIdentifier_PromotesToMethod()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorFallbackColoringService();
                const string line = "if (!DisplayRenderUtility.ShouldOverrideCharacterRender())";
                var start = line.IndexOf("ShouldOverrideCharacterRender", System.StringComparison.Ordinal);

                var effective = service.GetEffectiveLineTokenClassification("identifier", line, start, "ShouldOverrideCharacterRender".Length);

                Assert.Equal("method", effective);
            });
        }

        [Fact]
        public void GetEffectiveLineTokenClassification_SourceReceiverBeforeDot_PromotesToClass()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorFallbackColoringService();
                const string line = "if (!DisplayRenderUtility.ShouldOverrideCharacterRender())";
                var start = line.IndexOf("DisplayRenderUtility", System.StringComparison.Ordinal);

                var effective = service.GetEffectiveLineTokenClassification("identifier", line, start, "DisplayRenderUtility".Length);

                Assert.Equal("class", effective);
            });
        }

        [Fact]
        public void GetEffectiveLineTokenClassification_TypeofArgument_PromotesToClass()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorFallbackColoringService();
                const string line = "[HarmonyPatch(typeof(CustomisationPanel), \"RefreshRenderTexture\")]";
                var start = line.IndexOf("CustomisationPanel", System.StringComparison.Ordinal);

                var effective = service.GetEffectiveLineTokenClassification("identifier", line, start, "CustomisationPanel".Length);

                Assert.Equal("class", effective);
            });
        }

        [Fact]
        public void GetEffectiveLineTokenClassification_NamespaceDeclarationSegment_PromotesToNamespace()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorFallbackColoringService();
                const string line = "namespace Sheltered_Display_Fixes.Patches";
                var start = line.IndexOf("Sheltered_Display_Fixes", System.StringComparison.Ordinal);

                var effective = service.GetEffectiveLineTokenClassification("identifier", line, start, "Sheltered_Display_Fixes".Length);

                Assert.Equal("namespace", effective);
            });
        }

        [Fact]
        public void GetEffectiveLineTokenClassification_StaticMemberWithoutInvocation_PromotesToProperty()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorFallbackColoringService();
                const string line = "overlay.mainTexture = renderTexture;";
                var start = line.IndexOf("mainTexture", System.StringComparison.Ordinal);

                var effective = service.GetEffectiveLineTokenClassification("identifier", line, start, "mainTexture".Length);

                Assert.Equal("property", effective);
            });
        }

        [Fact]
        public void NormalizeClassification_RoslynTypeParameterName_PromotesToType()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                Assert.Equal("type", service.NormalizeClassification("type parameter name"));
            });
        }

        [Fact]
        public void NormalizeClassification_RoslynLocalName_PromotesToLocal()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                Assert.Equal("local", service.NormalizeClassification("local name"));
            });
        }

        [Fact]
        public void NormalizeClassification_RoslynFieldName_PromotesToField()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                Assert.Equal("field", service.NormalizeClassification("field name"));
            });
        }

        [Fact]
        public void GetSemanticTokenType_FieldClassification_MapsToVariable()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorSemanticTokenMappingService();

                Assert.Equal("variable", service.GetSemanticTokenType("field name"));
            });
        }

        [Fact]
        public void GetSemanticTokenType_ClassClassification_MapsToClass()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorSemanticTokenMappingService();

                Assert.Equal("class", service.GetSemanticTokenType("class name"));
            });
        }

        [Fact]
        public void ResolvePresentationClassification_UsesSemanticTokenTypeWhenClassificationIsGeneric()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                Assert.Equal("class", service.ResolvePresentationClassification("identifier", "class"));
                Assert.Equal("variable", service.ResolvePresentationClassification("text", "variable"));
            });
        }

        [Fact]
        public void ResolvePresentationClassification_PrefersSpecificClassificationOverSemanticTokenType()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                Assert.Equal("field", service.ResolvePresentationClassification("field name", "variable"));
                Assert.Equal("local", service.ResolvePresentationClassification("local name", "variable"));
            });
        }

        [Fact]
        public void NormalizeClassification_MapsCoreRoslynSymbolKinds()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                Assert.Equal("namespace", service.NormalizeClassification("namespace name"));
                Assert.Equal("class", service.NormalizeClassification("class name"));
                Assert.Equal("method", service.NormalizeClassification("method name"));
                Assert.Equal("parameter", service.NormalizeClassification("parameter name"));
                Assert.Equal("local", service.NormalizeClassification("local name"));
                Assert.Equal("type", service.NormalizeClassification("interface name"));
                Assert.Equal("field", service.NormalizeClassification("field name"));
                Assert.Equal("property", service.NormalizeClassification("property name"));
            });
        }

        [Fact]
        public void FallbackColoring_DoesNotOverrideValidRoslynClassification()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorFallbackColoringService();
                const string line = "overlay.mainTexture = renderTexture;";
                var propertyStart = line.IndexOf("mainTexture", System.StringComparison.Ordinal);

                Assert.Equal("property", service.GetEffectiveLineTokenClassification("property", line, propertyStart, "mainTexture".Length));
                Assert.Equal("field", service.GetEffectiveCodeViewClassification("field", "renderTexture", "=", ";", string.Empty));
            });
        }
    }
}
