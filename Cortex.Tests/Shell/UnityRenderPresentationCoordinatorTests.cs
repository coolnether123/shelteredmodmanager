using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Host.Unity.Runtime;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class UnityRenderPresentationCoordinatorTests
    {
        [Fact]
        public void Synchronize_FallsBackToImgui_WhenExternalHostLaunchFails()
        {
            var root = CreateTempRoot();
            try
            {
                CreateAvaloniaHostExecutable(root);
                var host = new TestPresentationHost(UnityRenderHostSettings.AvaloniaExternalRenderHostId);
                var launcher = new StubExternalHostLauncher(new UnityExternalHostLaunchResult(false, "Launch failed.", 0, DateTime.MinValue, string.Empty));
                var coordinator = new UnityRenderPresentationCoordinator(
                    NullWorkbenchFrameContext.Instance,
                    new UnityRenderHostCatalogBuilder(File.Exists, delegate(string name) { return string.Empty; }),
                    launcher);

                coordinator.Synchronize(host, new TestHostEnvironment(root));

                Assert.Equal(1, launcher.LaunchCount);
                Assert.NotNull(host.LastRuntimeUiFactory);
                Assert.Equal(WorkbenchRuntimeUiLayoutMode.IntegratedShellWindow, host.LastRuntimeUiFactory.Create().LayoutMode);
                Assert.Contains("Launch failed.", host.StatusMessage);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Fact]
        public void Shutdown_RequestsExternalHostShutdown_WhenExternalSessionWasLaunched()
        {
            var root = CreateTempRoot();
            try
            {
                CreateAvaloniaHostExecutable(root);
                var host = new TestPresentationHost(UnityRenderHostSettings.AvaloniaExternalRenderHostId);
                var launcher = new StubExternalHostLauncher(new UnityExternalHostLaunchResult(true, "Launched external host.", 0, DateTime.UtcNow, "launch-1"));
                var coordinator = new UnityRenderPresentationCoordinator(
                    NullWorkbenchFrameContext.Instance,
                    new UnityRenderHostCatalogBuilder(File.Exists, delegate(string name) { return string.Empty; }),
                    launcher);

                coordinator.Synchronize(host, new TestHostEnvironment(root));
                coordinator.Shutdown(host);

                Assert.Equal(1, launcher.LaunchCount);
                Assert.Equal(1, host.ExternalShutdownRequestCount);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        private static void CreateAvaloniaHostExecutable(string root)
        {
            var bundledToolRoot = Path.Combine(Path.Combine(root, "GameHost"), Path.Combine("bin", "tools"));
            var runtimeRoot = Path.Combine(Path.Combine(Path.Combine(bundledToolRoot, "desktop-host"), "host"), "lib");
            Directory.CreateDirectory(runtimeRoot);
            File.WriteAllText(Path.Combine(runtimeRoot, "Cortex.Host.Avalonia.exe"), string.Empty);
        }

        private static string CreateTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "cortex-render-coordinator-" + Guid.NewGuid().ToString("N"));
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

        private sealed class TestPresentationHost : IUnityRenderPresentationHost
        {
            private readonly CortexSettings _settings;

            public TestPresentationHost(string selectedRenderHostId)
            {
                _settings = new CortexSettings
                {
                    ModuleSettings = new[]
                    {
                        new ModuleSettingValue
                        {
                            SettingId = UnityRenderHostSettings.RenderHostSettingId,
                            Value = selectedRenderHostId
                        }
                    }
                };
            }

            public CortexSettings Settings
            {
                get { return _settings; }
            }

            public string StatusMessage { get; private set; }

            public IWorkbenchRuntimeUiFactory LastRuntimeUiFactory { get; private set; }

            public int ExternalShutdownRequestCount { get; private set; }

            public void ApplyStatusMessage(string statusMessage)
            {
                StatusMessage = statusMessage ?? string.Empty;
            }

            public bool ApplyRuntimeUiFactory(IWorkbenchRuntimeUiFactory runtimeUiFactory)
            {
                LastRuntimeUiFactory = runtimeUiFactory;
                return true;
            }

            public void RequestExternalHostShutdown()
            {
                ExternalShutdownRequestCount++;
            }

            public void RegisterOrUpdateStatusItem(StatusItemContribution contribution)
            {
            }

            public void RegisterOrUpdateSettingContribution(SettingContribution contribution)
            {
            }
        }

        private sealed class StubExternalHostLauncher : UnityExternalHostLauncher
        {
            private readonly UnityExternalHostLaunchResult _result;

            public StubExternalHostLauncher(UnityExternalHostLaunchResult result)
            {
                _result = result;
            }

            public int LaunchCount { get; private set; }

            public override UnityExternalHostLaunchResult Launch(UnityExternalHostLaunchRequest request)
            {
                LaunchCount++;
                return _result;
            }
        }

        private sealed class TestHostEnvironment : ICortexHostEnvironment
        {
            private readonly string _applicationRootPath;
            private readonly string _bundledToolRootPath;
            private readonly string _hostBinPath;

            public TestHostEnvironment(string applicationRootPath)
            {
                _applicationRootPath = applicationRootPath ?? string.Empty;
                _bundledToolRootPath = Path.Combine(Path.Combine(_applicationRootPath, "GameHost"), Path.Combine("bin", "tools"));
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
