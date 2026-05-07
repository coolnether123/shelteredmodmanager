using System;
using ModAPI.Core;
using ShelteredAPI.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

using ShelteredAPI.Hooks;
namespace ShelteredAPI.Core
{
    internal sealed class LoadingTransitionRecoveryService : MonoBehaviour
    {
        private static LoadingTransitionRecoveryService _instance;

        private readonly LoadingTransitionDiagnostics _diagnostics = new LoadingTransitionDiagnostics();
        private readonly LoadingTransitionRecoveryReportBuilder _reportBuilder = new LoadingTransitionRecoveryReportBuilder();

        private LoadingTransitionState _transition;
        private LoadingTransitionRecoveryNotice _pendingRecovery;
        private float _nextDialogAttemptAt;
        private bool _recoveryInProgress;

        public static void EnsureInstalled(GameObject host)
        {
            if (host == null || _instance != null)
                return;

            _instance = host.GetComponent<LoadingTransitionRecoveryService>();
            if (_instance == null)
                _instance = host.AddComponent<LoadingTransitionRecoveryService>();
        }

        public static void NotifyLoadingScreenRequested(string targetScene)
        {
            if (_instance != null)
                _instance.BeginTransition(targetScene, "LoadingScreen.ShowLoadingScreen");
        }

        public static void NotifyMenuTransitionRequested(string targetLabel, string reason)
        {
            if (_instance != null)
                _instance.BeginMenuTransition(targetLabel, reason);
        }

        public static void NotifyLoadingLevelAwake()
        {
            if (_instance == null)
                return;

            _instance._diagnostics.MarkBreadcrumb("LoadingLevel.Awake");
            _instance.EnterLoadingSceneIfActive("LoadingLevel.Awake");
        }

        public static void NotifyLoadingLevelTriggered(string targetScene)
        {
            if (_instance != null)
                _instance._diagnostics.MarkBreadcrumb("LoadingLevel.Update triggered target=" + LoadingTransitionText.Safe(targetScene));
        }

        public static void NotifyLoadingLevelSceneLoadIssued(string targetScene, bool asyncOperationMissing)
        {
            if (_instance == null)
                return;

            _instance._diagnostics.MarkBreadcrumb(
                "LoadingLevel.LoadSceneAsync target=" + LoadingTransitionText.Safe(targetScene) +
                " opMissing=" + asyncOperationMissing);
        }

        public static void ReportTransitionException(string source, Exception exception)
        {
            if (_instance == null || exception == null)
                return;

            _instance._diagnostics.RecordProblem(source + " threw " + exception.GetType().Name + ": " + exception.Message);
            _instance.Recover("ShelteredAPI caught a loading transition exception in " + source + ".", exception);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += OnUnityLog;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            Application.logMessageReceived -= OnUnityLog;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            TryShowPendingRecoveryDialog();
        }

        private void OnGUI()
        {
            if (_pendingRecovery != null &&
                !LoadingTransitionRuntime.CanUseNgui() &&
                LoadingTransitionFallbackDialog.Draw(_pendingRecovery))
            {
                _pendingRecovery = null;
            }
        }

        private void BeginTransition(string targetScene, string requestReason)
        {
            if (string.IsNullOrEmpty(targetScene) || LoadingTransitionRecoveryConstants.IsMenuScene(targetScene))
                return;

            string sourceScene = LoadingTransitionRuntime.GetActiveSceneName();
            _transition = LoadingTransitionState.Start(targetScene, sourceScene, requestReason);

            _diagnostics.ClearBreadcrumbs();
            _diagnostics.MarkBreadcrumb("Transition requested target=" + targetScene + " source=" + sourceScene);

            MMLog.WriteInfo("[LoadingTransitionRecovery] Monitoring transition to " + targetScene + ".");
        }

        private void BeginMenuTransition(string targetLabel, string requestReason)
        {
            string sourceScene = LoadingTransitionRuntime.GetActiveSceneName();
            _transition = LoadingTransitionState.StartMenuTransition(targetLabel, sourceScene, requestReason);

            _diagnostics.ClearBreadcrumbs();
            _diagnostics.MarkBreadcrumb("Menu transition requested target=" + LoadingTransitionText.Safe(targetLabel) + " source=" + sourceScene);

            MMLog.WriteInfo("[LoadingTransitionRecovery] Monitoring menu transition for " + LoadingTransitionText.Safe(targetLabel) + ".");
        }

        private void TrackSceneChange(string activeScene)
        {
            if (string.Equals(activeScene, _transition.LastScene, StringComparison.Ordinal))
                return;

            _transition.LastScene = activeScene;
            _diagnostics.MarkBreadcrumb("Active scene changed to " + activeScene);
        }

        private void CompleteTransition(string activeScene)
        {
            _diagnostics.MarkBreadcrumb("Target scene entered");
            MMLog.WriteInfo("[LoadingTransitionRecovery] Transition reached target scene " + activeScene + ".");
            _transition = null;
        }

        private void EnterLoadingSceneIfActive(string reason)
        {
            if (_transition == null || _transition.Phase != LoadingTransitionPhase.WaitingForLoadingScene)
                return;

            if (!LoadingTransitionRecoveryConstants.IsLoadingScene(LoadingTransitionRuntime.GetActiveSceneName()))
                return;

            _transition.MarkLoadingSceneEntered();
            _diagnostics.MarkBreadcrumb("LoadingScene entered via " + reason);
        }

        private void Recover(string reason, Exception exception)
        {
            if (_recoveryInProgress)
                return;

            _recoveryInProgress = true;
            try
            {
                LoadingTransitionState failed = _transition;
                if (failed != null)
                    failed.Phase = LoadingTransitionPhase.Recovering;

                string activeScene = LoadingTransitionRuntime.GetActiveSceneName();
                MMLog.WriteWithSource(
                    MMLog.LogLevel.Error,
                    MMLog.LogCategory.General,
                    "ShelteredAPI",
                    "[LoadingTransitionRecovery] " + _reportBuilder.BuildLogDetails(reason, exception, failed, activeScene, _diagnostics));
                MMLog.Flush();

                _pendingRecovery = new LoadingTransitionRecoveryNotice
                {
                    Title = LoadingTransitionRecoveryConstants.DialogTitle,
                    Message = _reportBuilder.BuildDialogMessage(reason, exception, failed, activeScene, _diagnostics),
                    CreatedAt = Time.realtimeSinceStartup
                };
                _nextDialogAttemptAt = Time.realtimeSinceStartup + 0.75f;

                LoadingTransitionRuntime.ResetAfterFailedTransition();
                bool returnedToMainMenu = LoadingTransitionRuntime.TryReturnToMainMenu();
                if (!returnedToMainMenu)
                    SceneManager.LoadScene(LoadingTransitionRecoveryConstants.MenuSceneName);
            }
            catch (Exception recoverEx)
            {
                MMLog.WriteError("[LoadingTransitionRecovery] Recovery failed: " + recoverEx);
            }
            finally
            {
                _transition = null;
                _recoveryInProgress = false;
            }
        }

        private void TryShowPendingRecoveryDialog()
        {
            if (_pendingRecovery == null || Time.realtimeSinceStartup < _nextDialogAttemptAt)
                return;

            string activeScene = LoadingTransitionRuntime.GetActiveSceneName();
            if (!LoadingTransitionRecoveryConstants.IsMenuScene(activeScene) || !LoadingTransitionRuntime.CanUseNgui())
            {
                _nextDialogAttemptAt = Time.realtimeSinceStartup + 0.5f;
                return;
            }

            LoadingTransitionRecoveryNotice recovery = _pendingRecovery;
            _pendingRecovery = null;
            LoadingTransitionRecoveryDialog.Show(recovery.Title, recovery.Message);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string sceneName = string.IsNullOrEmpty(scene.name) ? "<empty>" : scene.name;
            _diagnostics.MarkBreadcrumb("Scene loaded: " + sceneName);

            if (_transition == null)
                return;

            TrackSceneChange(sceneName);

            if (LoadingTransitionRecoveryConstants.IsLoadingScene(sceneName))
            {
                EnterLoadingSceneIfActive("SceneManager.sceneLoaded");
                return;
            }

            if (_transition.IsTargetScene(sceneName))
                CompleteTransition(sceneName);
        }

        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            _diagnostics.RecordUnityLog(condition, stackTrace, type);
        }
    }
}
