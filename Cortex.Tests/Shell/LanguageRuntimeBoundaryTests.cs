using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Presentation.Abstractions;
using Cortex.Services.Semantics.Completion;
using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;
using Cortex.Services.Editor.Commands;

namespace Cortex.Tests.Shell
{
    public sealed class LanguageRuntimeBoundaryTests
    {
        [Fact]
        public void NullRuntime_Start_WithExplicitNone_PublishesDisabledHealthySnapshot()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var runtime = new NullLanguageRuntimeService();

                runtime.Start(new LanguageRuntimeConfiguration
                {
                    ProviderId = LanguageRuntimeConstants.NoneProviderId,
                    Settings = new CortexSettings
                    {
                        LanguageProviderId = LanguageRuntimeConstants.NoneProviderId
                    }
                });

                var snapshot = runtime.GetSnapshot();

                Assert.Equal(LanguageRuntimeLifecycleState.Disabled, snapshot.LifecycleState);
                Assert.Equal(LanguageRuntimeHealthState.Healthy, snapshot.HealthState);
                Assert.Equal(LanguageRuntimeConstants.NoneProviderId, snapshot.Provider.ProviderId);
            });
        }

        [Fact]
        public void RuntimeService_Start_WithNoFactories_PublishesNoProvidersSnapshot()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var runtime = CreateRuntimeService(state, new List<ILanguageProviderFactory>());

                runtime.Start(new LanguageRuntimeConfiguration
                {
                    ProviderId = "custom",
                    Settings = new CortexSettings
                    {
                        LanguageProviderId = "custom"
                    }
                });

                var snapshot = runtime.GetSnapshot();

                Assert.Equal(LanguageRuntimeLifecycleState.Disabled, snapshot.LifecycleState);
                Assert.Equal(LanguageRuntimeHealthState.NoProviders, snapshot.HealthState);
            });
        }

        [Fact]
        public void RuntimeService_Start_WithUnknownProvider_PublishesUnavailableSnapshot()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var runtime = CreateRuntimeService(
                    state,
                    new List<ILanguageProviderFactory>
                    {
                        new TestLanguageProviderFactory("known", true)
                    });

                runtime.Start(new LanguageRuntimeConfiguration
                {
                    ProviderId = "missing",
                    Settings = new CortexSettings
                    {
                        LanguageProviderId = "missing"
                    }
                });

                var snapshot = runtime.GetSnapshot();

                Assert.Equal(LanguageRuntimeLifecycleState.Disabled, snapshot.LifecycleState);
                Assert.Equal(LanguageRuntimeHealthState.Unavailable, snapshot.HealthState);
                Assert.Equal("missing", snapshot.Provider.ProviderId);
            });
        }

        [Fact]
        public void RuntimeService_Start_WithRoslynProvider_UsesBuiltInRegistration()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var runtime = CreateRuntimeService(state, null);

                runtime.Start(new LanguageRuntimeConfiguration
                {
                    ProviderId = RoslynLanguageProviderFactory.ProviderId,
                    Settings = new CortexSettings
                    {
                        LanguageProviderId = RoslynLanguageProviderFactory.ProviderId
                    }
                });

                var snapshot = runtime.GetSnapshot();

                Assert.Equal(LanguageRuntimeHealthState.Unavailable, snapshot.HealthState);
                Assert.Equal(RoslynLanguageProviderFactory.ProviderId, snapshot.Provider.ProviderId);
            });
        }

        [Fact]
        public void Bootstrapper_BuildEffectiveSettings_PreservesBlankProviderId_WhenUnset()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var bootstrapper = CreateBootstrapper();
                bootstrapper.ConfigureHostServices(new TestHostServices(RoslynLanguageProviderFactory.ProviderId));

                var settings = bootstrapper.BuildEffectiveSettings(
                    new CortexSettings
                    {
                        LanguageProviderId = string.Empty
                    },
                    new TestHostEnvironment());

                Assert.Equal(string.Empty, settings.LanguageProviderId);
            });
        }

        [Fact]
        public void Bootstrapper_ResolveLanguageProviderId_UsesHostPreferredProvider_WhenProviderUnset()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var bootstrapper = CreateBootstrapper();
                bootstrapper.ConfigureHostServices(new TestHostServices(RoslynLanguageProviderFactory.ProviderId));

                var providerId = bootstrapper.ResolveLanguageProviderId(
                    new CortexSettings
                    {
                        LanguageProviderId = string.Empty
                    });

                Assert.Equal(RoslynLanguageProviderFactory.ProviderId, providerId);
            });
        }

        [Fact]
        public void Bootstrapper_ResolveLanguageProviderId_UsesNone_WhenProviderUnsetAndNoHostPreference()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var bootstrapper = CreateBootstrapper();
                bootstrapper.ConfigureHostServices(new TestHostServices(string.Empty));

                var providerId = bootstrapper.ResolveLanguageProviderId(
                    new CortexSettings
                    {
                        LanguageProviderId = string.Empty
                    });

                Assert.Equal(LanguageRuntimeConstants.NoneProviderId, providerId);
            });
        }

        [Fact]
        public void Bootstrapper_ResolveLanguageProviderId_UsesExplicitNone_WhenConfigured()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var bootstrapper = CreateBootstrapper();
                bootstrapper.ConfigureHostServices(new TestHostServices(RoslynLanguageProviderFactory.ProviderId));

                var providerId = bootstrapper.ResolveLanguageProviderId(
                    new CortexSettings
                    {
                        LanguageProviderId = LanguageRuntimeConstants.NoneProviderId
                    });

                Assert.Equal(LanguageRuntimeConstants.NoneProviderId, providerId);
            });
        }

        [Fact]
        public void Bootstrapper_BuildLanguageRuntimeConfiguration_SelectsProviderScopedConfiguration()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var bootstrapper = CreateBootstrapper();
                bootstrapper.ConfigureHostServices(new TestHostServices(RoslynLanguageProviderFactory.ProviderId));
                var settings = new CortexSettings
                {
                    LanguageProviderId = string.Empty,
                    LanguageProviderConfigurations = new[]
                    {
                        new LanguageProviderConfiguration
                        {
                            ProviderId = RoslynLanguageProviderFactory.ProviderId,
                            Settings = new[]
                            {
                                new LanguageProviderSettingValue
                                {
                                    SettingId = RoslynLanguageProviderFactory.RequestTimeoutMsSettingId,
                                    Value = "23000"
                                }
                            }
                        },
                        new LanguageProviderConfiguration
                        {
                            ProviderId = "custom",
                            Settings = new[]
                            {
                                new LanguageProviderSettingValue
                                {
                                    SettingId = "ignored",
                                    Value = "value"
                                }
                            }
                        }
                    }
                };

                var configuration = bootstrapper.BuildLanguageRuntimeConfiguration(new TestHostEnvironment("host-bin"), settings);

                Assert.Equal(RoslynLanguageProviderFactory.ProviderId, configuration.ProviderId);
                Assert.Equal("host-bin", configuration.HostBinPath);
                Assert.NotNull(configuration.ProviderConfiguration);
                Assert.Equal(RoslynLanguageProviderFactory.ProviderId, configuration.ProviderConfiguration.ProviderId);
                Assert.Single(configuration.ProviderConfiguration.Settings);
                Assert.Equal(RoslynLanguageProviderFactory.RequestTimeoutMsSettingId, configuration.ProviderConfiguration.Settings[0].SettingId);
                Assert.Equal("23000", configuration.ProviderConfiguration.Settings[0].Value);
            });
        }

        [Fact]
        public void RoslynFactory_BuildConfigurationFingerprint_PrefersProviderScopedConfiguration()
        {
            var factory = new RoslynLanguageProviderFactory();
            var configuration = new LanguageRuntimeConfiguration
            {
                ProviderId = RoslynLanguageProviderFactory.ProviderId,
                HostBinPath = "host",
                Settings = new CortexSettings(),
                ProviderConfiguration = new LanguageProviderConfiguration
                {
                    ProviderId = RoslynLanguageProviderFactory.ProviderId,
                    Settings = new[]
                    {
                        new LanguageProviderSettingValue
                        {
                            SettingId = RoslynLanguageProviderFactory.WorkerPathOverrideSettingId,
                            Value = "scoped-path"
                        },
                        new LanguageProviderSettingValue
                        {
                            SettingId = RoslynLanguageProviderFactory.RequestTimeoutMsSettingId,
                            Value = "23000"
                        }
                    }
                }
            };

            var fingerprint = factory.BuildConfigurationFingerprint(configuration);

            Assert.Contains("|path=scoped-path|", fingerprint);
            Assert.Contains("|timeout=23000", fingerprint);
        }

        [Fact]
        public void LanguageProviderConfigurationHelper_SetSettingValue_UpsertsSetting_WithoutDroppingOtherProviders()
        {
            var settings = new CortexSettings
            {
                LanguageProviderConfigurations = new[]
                {
                    new LanguageProviderConfiguration
                    {
                        ProviderId = RoslynLanguageProviderFactory.ProviderId,
                        Settings = new[]
                        {
                            new LanguageProviderSettingValue
                            {
                                SettingId = RoslynLanguageProviderFactory.WorkerPathOverrideSettingId,
                                Value = "old-path"
                            }
                        }
                    },
                    new LanguageProviderConfiguration
                    {
                        ProviderId = "custom",
                        Settings = new[]
                        {
                            new LanguageProviderSettingValue
                            {
                                SettingId = "mode",
                                Value = "strict"
                            }
                        }
                    }
                }
            };

            LanguageProviderConfigurationHelper.SetSettingValue(
                settings,
                RoslynLanguageProviderFactory.ProviderId,
                RoslynLanguageProviderFactory.RequestTimeoutMsSettingId,
                "23000");

            Assert.Equal("23000", LanguageProviderConfigurationHelper.GetSettingValue(
                settings,
                RoslynLanguageProviderFactory.ProviderId,
                RoslynLanguageProviderFactory.RequestTimeoutMsSettingId));
            Assert.Equal("old-path", LanguageProviderConfigurationHelper.GetSettingValue(
                settings,
                RoslynLanguageProviderFactory.ProviderId,
                RoslynLanguageProviderFactory.WorkerPathOverrideSettingId));
            Assert.Equal("strict", LanguageProviderConfigurationHelper.GetSettingValue(
                settings,
                "custom",
                "mode"));
        }

        [Fact]
        public void LanguageProviderConfigurationHelper_SetSettingValue_AddsProviderConfiguration_WhenMissing()
        {
            var settings = new CortexSettings();

            LanguageProviderConfigurationHelper.SetSettingValue(
                settings,
                RoslynLanguageProviderFactory.ProviderId,
                RoslynLanguageProviderFactory.WorkerPathOverrideSettingId,
                "scoped-path");

            Assert.Single(settings.LanguageProviderConfigurations);
            Assert.Equal("scoped-path", LanguageProviderConfigurationHelper.GetSettingValue(
                settings,
                RoslynLanguageProviderFactory.ProviderId,
                RoslynLanguageProviderFactory.WorkerPathOverrideSettingId));
        }

        [Fact]
        public void EditorAvailability_UsesSnapshotHealth_ToExplainUnavailableProvider()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                state.LanguageRuntime = new LanguageRuntimeSnapshot
                {
                    LifecycleState = LanguageRuntimeLifecycleState.Disabled,
                    HealthState = LanguageRuntimeHealthState.Unavailable,
                    Capabilities = new LanguageCapabilitiesSnapshot()
                };

                var service = new EditorCommandAvailabilityService();
                string disabledReason;
                var enabled = service.TryGetAvailability(
                    "cortex.editor.rename",
                    state,
                    new EditorCommandTarget { SymbolText = "Player" },
                    out disabledReason);

                Assert.False(enabled);
                Assert.Contains("selected language provider could not be created", disabledReason);
            });
        }

        [Fact]
        public void JsonSettingsStore_Load_UsesProviderScopedLanguageConfigurationOnly()
        {
            var path = Path.Combine(Path.GetTempPath(), "cortex-settings-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(
                    path,
                    "{\"LanguageProviderId\":\"roslyn\",\"LanguageProviderConfigurations\":[{\"ProviderId\":\"roslyn\",\"Settings\":[{\"SettingId\":\"workerPathOverride\",\"Value\":\"provider-worker\"},{\"SettingId\":\"requestTimeoutMs\",\"Value\":\"23000\"}]}]}");

                var store = new JsonCortexSettingsStore(path);
                var settings = store.Load();

                Assert.Equal(RoslynLanguageProviderFactory.ProviderId, settings.LanguageProviderId);
                Assert.Equal(
                    "provider-worker",
                    LanguageProviderConfigurationHelper.GetSettingValue(
                        settings,
                        RoslynLanguageProviderFactory.ProviderId,
                        RoslynLanguageProviderFactory.WorkerPathOverrideSettingId));
                Assert.Equal(
                    "23000",
                    LanguageProviderConfigurationHelper.GetSettingValue(
                        settings,
                        RoslynLanguageProviderFactory.ProviderId,
                        RoslynLanguageProviderFactory.RequestTimeoutMsSettingId));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void JsonSettingsStore_Load_DoesNotMigrateLegacyRoslynFields()
        {
            var path = Path.Combine(Path.GetTempPath(), "cortex-settings-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(
                    path,
                    "{\"EnableRoslynLanguageService\":false,\"RoslynServicePathOverride\":\"legacy-worker\",\"RoslynServiceTimeoutMs\":23000}");

                var store = new JsonCortexSettingsStore(path);
                var settings = store.Load();

                Assert.Equal(string.Empty, settings.LanguageProviderId);
                Assert.Empty(settings.LanguageProviderConfigurations);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static CortexLanguageRuntimeService CreateRuntimeService(
            CortexShellState state,
            IList<ILanguageProviderFactory> builtinFactories)
        {
            return new CortexLanguageRuntimeService(
                state,
                delegate { return new ShellServiceMap(); },
                delegate { return false; },
                delegate { },
                delegate { },
                delegate(DocumentSession session, DocumentLanguageCompletionRequestState pending) { return null; },
                delegate(DocumentSession session, DocumentLanguageCompletionRequestState pending, CompletionAugmentationRequest request, LanguageServiceCompletionResponse response) { return false; },
                delegate { return new List<ILanguageProviderFactory>(); },
                builtinFactories);
        }

        private static ShellBootstrapper CreateBootstrapper()
        {
            var runtime = new NullLanguageRuntimeService();
            return new ShellBootstrapper(
                new CortexShellState(),
                new CortexShellModuleContributionRegistry(),
                null,
                new CortexShellBuiltInModuleRegistrar(),
                runtime,
                runtime,
                runtime);
        }

        private sealed class TestLanguageProviderFactory : ILanguageProviderFactory
        {
            private readonly bool _canCreate;

            public TestLanguageProviderFactory(string providerId, bool canCreate)
            {
                _canCreate = canCreate;
                Descriptor = new LanguageProviderDescriptor
                {
                    ProviderId = providerId,
                    DisplayName = providerId,
                    Source = "test"
                };
            }

            public LanguageProviderDescriptor Descriptor { get; private set; }

            public string BuildConfigurationFingerprint(LanguageRuntimeConfiguration configuration)
            {
                return configuration != null ? configuration.ProviderId ?? string.Empty : string.Empty;
            }

            public bool TryCreate(LanguageRuntimeConfiguration configuration, out ILanguageProviderSession session, out string unavailableReason)
            {
                session = null;
                unavailableReason = _canCreate ? string.Empty : "Provider unavailable.";
                return _canCreate;
            }
        }

        private sealed class TestHostServices : ICortexHostServices
        {
            private readonly string _preferredLanguageProviderId;

            public TestHostServices(string preferredLanguageProviderId)
            {
                _preferredLanguageProviderId = preferredLanguageProviderId;
            }

            public ICortexHostEnvironment Environment
            {
                get { return new TestHostEnvironment(); }
            }

            public IPathInteractionService PathInteractionService
            {
                get { return null; }
            }

            public IWorkbenchRuntimeFactory WorkbenchRuntimeFactory
            {
                get { return null; }
            }

            public ICortexPlatformModule PlatformModule
            {
                get { return null; }
            }

            public ICortexShellHostUi ShellHostUi
            {
                get { return new TestShellHostUi(); }
            }

            public string PreferredLanguageProviderId
            {
                get { return _preferredLanguageProviderId; }
            }

            public IList<ILanguageProviderFactory> LanguageProviderFactories
            {
                get { return new List<ILanguageProviderFactory>(); }
            }
        }

        private sealed class TestHostEnvironment : ICortexHostEnvironment
        {
            private readonly string _hostBinPath;

            public TestHostEnvironment()
                : this(string.Empty)
            {
            }

            public TestHostEnvironment(string hostBinPath)
            {
                _hostBinPath = hostBinPath ?? string.Empty;
            }

            public string GameRootPath => string.Empty;
            public string HostRootPath => string.Empty;
            public string HostBinPath => _hostBinPath;
            public string ManagedAssemblyRootPath => string.Empty;
            public string ModsRootPath => string.Empty;
            public string SettingsFilePath => string.Empty;
            public string WorkbenchPersistenceFilePath => string.Empty;
            public string LogFilePath => string.Empty;
            public string ProjectCatalogPath => string.Empty;
            public string DecompilerCachePath => string.Empty;
        }

        private sealed class TestShellHostUi : ICortexShellHostUi
        {
            public int ScreenWidth => 1920;
            public int ScreenHeight => 1080;
            public int HotControl => 0;
            public int KeyboardControl => 0;
            public bool HasCurrentEvent => false;
            public CortexShellInputEventKind CurrentEventKind => CortexShellInputEventKind.None;
            public CortexShellInputEventKind CurrentEventRawKind => CortexShellInputEventKind.None;
            public CortexShellInputKey CurrentKey => CortexShellInputKey.None;
            public int CurrentMouseButton => -1;
            public CortexShellPointerPosition CurrentMousePosition => new CortexShellPointerPosition(0f, 0f);
            public CortexShellPointerPosition PointerPosition => new CortexShellPointerPosition(0f, 0f);

            public void ConsumeCurrentEvent()
            {
            }
        }
    }
}
