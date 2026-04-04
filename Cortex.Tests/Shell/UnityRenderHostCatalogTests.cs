using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Host.Unity.Runtime;
using Cortex.Presentation.Abstractions;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering.RuntimeUi;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class UnityRenderHostCatalogTests
    {
        [Fact]
        public void RenderHostSetting_FallsBackToImgui_WhenMissingOrInvalid()
        {
            Assert.Equal(
                UnityRenderHostSettings.ImguiRenderHostId,
                UnityRenderHostSettings.ReadSelectedRenderHostId(new CortexSettings()));

            var invalidSettings = new CortexSettings
            {
                ModuleSettings = new[]
                {
                    new ModuleSettingValue
                    {
                        SettingId = UnityRenderHostSettings.RenderHostSettingId,
                        Value = "not-a-host"
                    }
                }
            };

            Assert.Equal(
                UnityRenderHostSettings.ImguiRenderHostId,
                UnityRenderHostSettings.ReadSelectedRenderHostId(invalidSettings));

            var avaloniaSettings = new CortexSettings
            {
                ModuleSettings = new[]
                {
                    new ModuleSettingValue
                    {
                        SettingId = UnityRenderHostSettings.RenderHostSettingId,
                        Value = UnityRenderHostSettings.AvaloniaExternalRenderHostId
                    }
                }
            };

            Assert.Equal(
                UnityRenderHostSettings.AvaloniaExternalRenderHostId,
                UnityRenderHostSettings.ReadSelectedRenderHostId(avaloniaSettings));
        }

        [Fact]
        public void CatalogBuilder_ReportsSpecificReason_WhenOnlyDllExistsAndDotNetIsMissing()
        {
            var root = CreateTempRoot();
            try
            {
                var bundledToolRoot = Path.Combine(Path.Combine(root, "GameHost"), Path.Combine("bin", "tools"));
                var runtimeRoot = Path.Combine(Path.Combine(Path.Combine(bundledToolRoot, "desktop-host"), "host"), "lib");
                Directory.CreateDirectory(runtimeRoot);
                File.WriteAllText(Path.Combine(runtimeRoot, "Cortex.Host.Avalonia.dll"), string.Empty);

                var builder = new UnityRenderHostCatalogBuilder(File.Exists, delegate(string name) { return string.Empty; });
                var catalog = builder.Build(
                    new TestHostEnvironment(root, bundledToolRoot),
                    UnityRenderHostSettings.AvaloniaExternalRenderHostId);

                Assert.False(catalog.AvaloniaLaunchRequest.CanLaunch);
                Assert.Equal(UnityRenderHostSettings.ImguiRenderHostId, catalog.EffectiveRenderHostId);
                Assert.Contains("dotnet.exe was not found on PATH", catalog.AvaloniaLaunchRequest.FailureReason);
                Assert.DoesNotContain(
                    catalog.BuildOptions(),
                    option => string.Equals(option.Value, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase));
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
                var bundledToolRoot = Path.Combine(Path.Combine(root, "GameHost"), Path.Combine("bin", "tools"));
                var runtimeRoot = Path.Combine(Path.Combine(Path.Combine(bundledToolRoot, "desktop-host"), "host"), "lib");
                Directory.CreateDirectory(runtimeRoot);
                File.WriteAllText(Path.Combine(runtimeRoot, "Cortex.Host.Avalonia.exe"), string.Empty);

                var builder = new UnityRenderHostCatalogBuilder(File.Exists, delegate(string name) { return string.Empty; });
                var catalog = builder.Build(
                    new TestHostEnvironment(root, bundledToolRoot),
                    UnityRenderHostSettings.AvaloniaExternalRenderHostId);

                Assert.True(catalog.AvaloniaLaunchRequest.CanLaunch);
                Assert.Equal(UnityRenderHostSettings.AvaloniaExternalRenderHostId, catalog.EffectiveRenderHostId);
                Assert.Contains(
                    catalog.BuildOptions(),
                    option => string.Equals(option.Value, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Fact]
        public void UnityWorkbenchRuntime_RegistersRenderHostChoiceSetting()
        {
            var catalog = UnityRenderHostCatalog.CreateDefault();
            catalog.AvailableOptions.Add(new SettingChoiceOption
            {
                Value = UnityRenderHostSettings.AvaloniaExternalRenderHostId,
                DisplayName = "Avalonia",
                Description = "External host."
            });

            var runtime = new UnityWorkbenchRuntime(
                new TestContributionRegistrar(catalog),
                NullWorkbenchRuntimeUi.Instance);

            SettingContribution setting = null;
            var settings = runtime.ContributionRegistry.GetSettings();
            for (var i = 0; i < settings.Count; i++)
            {
                if (string.Equals(settings[i].SettingId, UnityRenderHostSettings.RenderHostSettingId, StringComparison.OrdinalIgnoreCase))
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
                option => string.Equals(option.Value, UnityRenderHostSettings.ImguiRenderHostId, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                setting.Options,
                option => string.Equals(option.Value, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase));
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

        private sealed class TestContributionRegistrar : IUnityWorkbenchContributionRegistrar
        {
            private readonly UnityRenderHostCatalog _catalog;

            public TestContributionRegistrar(UnityRenderHostCatalog catalog)
            {
                _catalog = catalog;
            }

            public void RegisterBuiltIns(
                ICommandRegistry commandRegistry,
                IContributionRegistry contributionRegistry,
                string rendererDisplayName)
            {
                UnityRenderHostSettingContributions.Register(
                    new WorkbenchPluginContext(commandRegistry, contributionRegistry, null, null, null),
                    _catalog,
                    "Appearance",
                    10);
            }
        }

        private sealed class TestHostEnvironment : ICortexHostEnvironment
        {
            private readonly string _applicationRootPath;
            private readonly string _bundledToolRootPath;
            private readonly string _hostBinPath;

            public TestHostEnvironment(string applicationRootPath, string bundledToolRootPath)
            {
                _applicationRootPath = applicationRootPath ?? string.Empty;
                _bundledToolRootPath = bundledToolRootPath ?? string.Empty;
                _hostBinPath = Path.Combine(Path.Combine(_applicationRootPath, "GameHost"), "bin");
            }

            public string ApplicationRootPath { get { return _applicationRootPath; } }
            public string HostRootPath { get { return Path.Combine(_applicationRootPath, "GameHost"); } }
            public string HostBinPath { get { return _hostBinPath; } }
            public string BundledPluginSearchRoots { get { return string.Empty; } }
            public string BundledToolRootPath { get { return _bundledToolRootPath; } }
            public string ConfiguredPluginSearchRoots { get { return string.Empty; } }
            public string ReferenceAssemblyRootPath { get { return string.Empty; } }
            public string RuntimeContentRootPath { get { return string.Empty; } }
            public string SettingsFilePath { get { return Path.Combine(_hostBinPath, "cortex_settings.json"); } }
            public string WorkbenchPersistenceFilePath { get { return string.Empty; } }
            public string LogFilePath { get { return string.Empty; } }
            public string ProjectCatalogPath { get { return string.Empty; } }
            public string DecompilerCachePath { get { return string.Empty; } }
        }
    }
}
