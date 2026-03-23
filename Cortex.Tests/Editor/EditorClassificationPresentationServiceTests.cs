using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorClassificationPresentationServiceTests
    {
        [Fact]
        public void GetEffectiveCodeViewClassification_InvocationIdentifier_PromotesToMethod()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

                var effective = service.GetEffectiveCodeViewClassification("identifier", "ShouldOverrideCharacterRender", ".", "(", ")");

                Assert.Equal("method", effective);
            });
        }

        [Fact]
        public void GetEffectiveCodeViewClassification_PascalCaseReceiverBeforeDot_PromotesToClass()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorClassificationPresentationService();

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
                var service = new EditorClassificationPresentationService();
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
                var service = new EditorClassificationPresentationService();
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
                var service = new EditorClassificationPresentationService();
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
                var service = new EditorClassificationPresentationService();
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
                var service = new EditorClassificationPresentationService();
                const string line = "overlay.mainTexture = renderTexture;";
                var start = line.IndexOf("mainTexture", System.StringComparison.Ordinal);

                var effective = service.GetEffectiveLineTokenClassification("identifier", line, start, "mainTexture".Length);

                Assert.Equal("property", effective);
            });
        }
    }
}
