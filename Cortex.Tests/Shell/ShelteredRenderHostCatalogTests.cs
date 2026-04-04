using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Host.Sheltered.Runtime;
using Cortex.Host.Unity.Runtime;
using Cortex.Rendering.RuntimeUi;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class ShelteredRenderHostCatalogTests
    {
        [Fact]
        public void RenderHostSetting_FallsBackToImgui_WhenMissingOrInvalid()
        {
            Assert.Equal(
                ShelteredRenderHostSettings.ImguiRenderHostId,
                ShelteredRenderHostSettings.ReadSelectedRenderHostId(new CortexSettings()));

            var invalidSettings = new CortexSettings
            {
                ModuleSettings = new[]
                {
                    new ModuleSettingValue
                    {
                        SettingId = ShelteredRenderHostSettings.RenderHostSettingId,
                        Value = "not-a-host"
                    }
                }
            };

            Assert.Equal(
                ShelteredRenderHostSettings.ImguiRenderHostId,
                ShelteredRenderHostSettings.ReadSelectedRenderHostId(invalidSettings));

            var avaloniaSettings = new CortexSettings
            {
                ModuleSettings = new[]
                {
                    new ModuleSettingValue
                    {
                        SettingId = ShelteredRenderHostSettings.RenderHostSettingId,
                        Value = ShelteredRenderHostSettings.AvaloniaExternalRenderHostId
                    }
                }
            };

            Assert.Equal(
                ShelteredRenderHostSettings.AvaloniaExternalRenderHostId,
                ShelteredRenderHostSettings.ReadSelectedRenderHostId(avaloniaSettings));
        }

        [Fact]
        public void CatalogBuilder_ReportsSpecificReason_WhenOnlyDllExistsAndDotNetIsMissing()
        {
            var root = CreateTempRoot();
            try
            {
                var runtimeRoot = Path.Combine(root, "SMM", "bin", "decompiler");
                Directory.CreateDirectory(runtimeRoot);
                File.WriteAllText(Path.Combine(runtimeRoot, "Cortex.Host.Avalonia.dll"), string.Empty);

                var builder = new ShelteredRenderHostCatalogBuilder(File.Exists, delegate(string name) { return string.Empty; });
                var catalog = builder.Build(
                    ShelteredHostPathLayout.FromApplicationRoot(root),
                    ShelteredRenderHostSettings.AvaloniaExternalRenderHostId);

                Assert.False(catalog.AvaloniaLaunchRequest.CanLaunch);
                Assert.Equal(ShelteredRenderHostSettings.ImguiRenderHostId, catalog.EffectiveRenderHostId);
                Assert.Contains("dotnet.exe was not found on PATH", catalog.AvaloniaLaunchRequest.FailureReason);
                Assert.DoesNotContain(
                    catalog.BuildOptions(),
                    option => string.Equals(option.Value, ShelteredRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Fact]
        public void CatalogBuilder_ExposesAvaloniaChoice_WhenDesktopHostExecutableIsAvailable()
        {
            var root = CreateTempRoot();
            try
            {
                var runtimeRoot = Path.Combine(root, "SMM", "bin", "decompiler");
                Directory.CreateDirectory(runtimeRoot);
                File.WriteAllText(Path.Combine(runtimeRoot, "Cortex.Host.Avalonia.exe"), string.Empty);

                var builder = new ShelteredRenderHostCatalogBuilder(File.Exists, delegate(string name) { return string.Empty; });
                var catalog = builder.Build(
                    ShelteredHostPathLayout.FromApplicationRoot(root),
                    ShelteredRenderHostSettings.AvaloniaExternalRenderHostId);

                Assert.True(catalog.AvaloniaLaunchRequest.CanLaunch);
                Assert.Equal(ShelteredRenderHostSettings.AvaloniaExternalRenderHostId, catalog.EffectiveRenderHostId);
                Assert.Contains(
                    catalog.BuildOptions(),
                    option => string.Equals(option.Value, ShelteredRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Fact]
        public void ShelteredWorkbenchRuntime_RegistersRenderHostChoiceSetting()
        {
            var catalog = ShelteredRenderHostCatalog.CreateDefault();
            catalog.AvailableOptions.Add(new SettingChoiceOption
            {
                Value = ShelteredRenderHostSettings.AvaloniaExternalRenderHostId,
                DisplayName = "Avalonia",
                Description = "External host."
            });

            var runtime = new UnityWorkbenchRuntime(
                new ShelteredUnityWorkbenchContributionRegistrar(catalog, "Host: IMGUI (in-game)"),
                NullWorkbenchRuntimeUi.Instance);

            SettingContribution setting = null;
            var settings = runtime.ContributionRegistry.GetSettings();
            for (var i = 0; i < settings.Count; i++)
            {
                if (string.Equals(settings[i].SettingId, ShelteredRenderHostSettings.RenderHostSettingId, StringComparison.OrdinalIgnoreCase))
                {
                    setting = settings[i];
                    break;
                }
            }

            Assert.NotNull(setting);
            Assert.Equal("Appearance", setting.Scope);
            Assert.Equal(SettingEditorKind.Choice, setting.EditorKind);
            Assert.Contains(
                setting.Options,
                option => string.Equals(option.Value, ShelteredRenderHostSettings.ImguiRenderHostId, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                setting.Options,
                option => string.Equals(option.Value, ShelteredRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase));
        }

        private static string CreateTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "cortex-render-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteTempRoot(string root)
        {
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
