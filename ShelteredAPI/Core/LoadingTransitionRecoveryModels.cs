using System;

namespace ShelteredAPI.Core
{
    internal static class LoadingTransitionRecoveryConstants
    {
        public const string MenuSceneName = "MenuScene";
        public const string LoadingSceneName = "LoadingScene";
        public const float MenuTransitionTimeoutSeconds = 6f;
        public const float PreLoadingSceneTimeoutSeconds = 12f;
        public const float LoadingSceneTimeoutSeconds = 25f;
        public const int MaxRecentEvents = 8;
        public const string DialogTitle = "ShelteredAPI Recovered A Failed Load";

        public static bool IsMenuScene(string sceneName)
        {
            return string.Equals(sceneName, MenuSceneName, StringComparison.Ordinal);
        }

        public static bool IsLoadingScene(string sceneName)
        {
            return string.Equals(sceneName, LoadingSceneName, StringComparison.Ordinal);
        }
    }

    internal enum LoadingTransitionPhase
    {
        WaitingForLoadingScene,
        LoadingScene,
        Recovering
    }

    internal sealed class LoadingTransitionState
    {
        public string TargetScene;
        public string TargetLabel;
        public string SourceScene;
        public string LastScene;
        public string RequestReason;
        public LoadingTransitionPhase Phase;
        public float StartedAt;
        public float LoadingSceneEnteredAt;

        public static LoadingTransitionState Start(string targetScene, string sourceScene, string requestReason, float now)
        {
            return new LoadingTransitionState
            {
                TargetScene = targetScene,
                SourceScene = sourceScene,
                LastScene = sourceScene,
                RequestReason = requestReason,
                Phase = LoadingTransitionPhase.WaitingForLoadingScene,
                StartedAt = now,
                LoadingSceneEnteredAt = 0f
            };
        }

        public static LoadingTransitionState StartMenuTransition(string targetLabel, string sourceScene, string requestReason, float now)
        {
            return new LoadingTransitionState
            {
                TargetScene = null,
                TargetLabel = string.IsNullOrEmpty(targetLabel) ? "menu transition" : targetLabel,
                SourceScene = sourceScene,
                LastScene = sourceScene,
                RequestReason = requestReason,
                Phase = LoadingTransitionPhase.WaitingForLoadingScene,
                StartedAt = now,
                LoadingSceneEnteredAt = 0f
            };
        }

        public bool IsTargetScene(string sceneName)
        {
            if (string.IsNullOrEmpty(TargetScene))
                return false;

            return string.Equals(sceneName, TargetScene, StringComparison.Ordinal);
        }

        public void MarkLoadingSceneEntered(float now)
        {
            Phase = LoadingTransitionPhase.LoadingScene;
            LoadingSceneEnteredAt = now;
        }

        public bool TryGetTimeoutReason(float now, out string reason)
        {
            reason = null;

            if (string.IsNullOrEmpty(TargetScene) &&
                Phase == LoadingTransitionPhase.WaitingForLoadingScene &&
                now - StartedAt >= LoadingTransitionRecoveryConstants.MenuTransitionTimeoutSeconds)
            {
                reason = "Sheltered stayed in a menu transition after " + TargetLabel + " and no loading route started.";
                return true;
            }

            if (Phase == LoadingTransitionPhase.WaitingForLoadingScene &&
                now - StartedAt >= LoadingTransitionRecoveryConstants.PreLoadingSceneTimeoutSeconds)
            {
                reason = "Sheltered stayed on the transition screen before LoadingScene could start.";
                return true;
            }

            if (Phase == LoadingTransitionPhase.LoadingScene &&
                now - LoadingSceneEnteredAt >= LoadingTransitionRecoveryConstants.LoadingSceneTimeoutSeconds)
            {
                reason = "Sheltered stayed in LoadingScene and never reached the requested scene.";
                return true;
            }

            return false;
        }
    }

    internal sealed class LoadingTransitionRecoveryNotice
    {
        public string Title;
        public string Message;
        public float CreatedAt;
    }
}
