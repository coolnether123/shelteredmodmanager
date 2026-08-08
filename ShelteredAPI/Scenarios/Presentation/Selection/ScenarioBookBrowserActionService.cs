using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Selection
{
    /// <summary>
    /// Executes playable-scenario and scenario-save actions from the runtime browser.
    /// Authoring and package-management operations are intentionally outside this runtime service.
    /// </summary>
    internal sealed class ScenarioBookBrowserActionService
    {
        private readonly ScenarioBrowserPanelAdapter _adapter;
        private readonly Func<ScenarioLaunchCoordinator> _launchCoordinatorFactory;
        private readonly IScenarioSaveLibrary _saveLibrary;
        private readonly object _dependencySync = new object();
        private ScenarioLaunchCoordinator _launchCoordinator;

        public ScenarioBookBrowserActionService(
            ScenarioBrowserPanelAdapter adapter,
            ScenarioLaunchCoordinator launchCoordinator,
            IScenarioSaveLibrary saveLibrary)
            : this(adapter, delegate { return launchCoordinator; }, saveLibrary)
        {
            if (launchCoordinator == null) throw new ArgumentNullException("launchCoordinator");
        }

        internal ScenarioBookBrowserActionService(
            ScenarioBrowserPanelAdapter adapter,
            Func<ScenarioLaunchCoordinator> launchCoordinatorFactory,
            IScenarioSaveLibrary saveLibrary)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");
            if (launchCoordinatorFactory == null) throw new ArgumentNullException("launchCoordinatorFactory");
            if (saveLibrary == null) throw new ArgumentNullException("saveLibrary");

            _adapter = adapter;
            _launchCoordinatorFactory = launchCoordinatorFactory;
            _saveLibrary = saveLibrary;
        }

        private ScenarioLaunchCoordinator LaunchCoordinator
        {
            get
            {
                lock (_dependencySync)
                {
                    if (_launchCoordinator == null)
                    {
                        _launchCoordinator = _launchCoordinatorFactory();
                        if (_launchCoordinator == null)
                            throw new InvalidOperationException("Deferred scenario launch coordinator resolution returned null.");
                    }

                    return _launchCoordinator;
                }
            }
        }

        public bool StartScenario(ScenarioCatalogEntry entry, out string status)
        {
            status = null;
            if (entry == null)
                return false;

            MMLog.WriteInfo("[ScenarioBookBrowser] Start requested. scenarioId=" + entry.ScenarioId
                + " storageScenarioId=" + entry.StorageScenarioId
                + " source=" + entry.Source
                + " baseMode=" + entry.BaseGameMode
                + " virtualSaveType=" + LaunchCoordinator.GetVirtualSaveType(entry) + ".");

            if (entry.IsVanilla && entry.BaseGameMode == ScenarioBaseGameMode.Survival)
            {
                string vanillaError;
                if (!LaunchCoordinator.LaunchVanillaScenario(_adapter, entry, out vanillaError))
                {
                    status = "Start failed: " + Safe(vanillaError, "unknown error");
                    return false;
                }

                return true;
            }

            ScenarioLaunchCoordinator.NewGamePreparation preparation;
            string prepareError;
            if (!LaunchCoordinator.PrepareNewGame(entry, entry.DisplayName, out preparation, out prepareError))
            {
                status = "Start failed: " + Safe(prepareError, "unknown error");
                return false;
            }

            string commitError;
            if (!LaunchCoordinator.CommitNewGame(_adapter, preparation, out commitError))
            {
                status = "Start failed: " + Safe(commitError, "unknown error");
                return false;
            }

            return true;
        }

        public bool LoadSave(ScenarioCatalogEntry entry, SaveEntry save, out string status)
        {
            status = null;
            string error;
            if (!LaunchCoordinator.LoadSave(_adapter, entry, save, out error))
            {
                status = "Load failed: " + Safe(error, "unknown error");
                return false;
            }

            return true;
        }

        public bool DeleteSave(ScenarioCatalogEntry entry, SaveEntry save, out string status)
        {
            status = null;
            if (!LaunchCoordinator.DeleteSave(entry, save))
            {
                status = save != null ? "Delete failed for slot " + save.absoluteSlot + "." : "Delete failed.";
                return false;
            }

            if (entry != null && !string.IsNullOrEmpty(entry.StorageScenarioId))
            {
                try
                {
                    entry.SaveCount = _saveLibrary.CountSaves(entry.StorageScenarioId);
                }
                catch (Exception ex)
                {
                    entry.SaveCount = Math.Max(0, entry.SaveCount - 1);
                    MMLog.WriteWarning("[ScenarioBookBrowser] Deleted save but count refresh failed for "
                        + entry.StorageScenarioId + ": " + ex.Message);
                }
            }

            status = save != null ? "Deleted slot " + save.absoluteSlot + "." : "Deleted save.";
            return true;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }
    }
}
