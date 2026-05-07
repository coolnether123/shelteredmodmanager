using System;

namespace ShelteredAPI.Core
{
    internal static class LoadingTransitionRecoveryConstants
    {
        public const string MenuSceneName = "MenuScene";
        public const string LoadingSceneName = "LoadingScene";
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

        public static LoadingTransitionState Start(string targetScene, string sourceScene, string requestReason)
        {
            return new LoadingTransitionState
            {
                TargetScene = targetScene,
                SourceScene = sourceScene,
                LastScene = sourceScene,
                RequestReason = requestReason,
                Phase = LoadingTransitionPhase.WaitingForLoadingScene
            };
        }

        public static LoadingTransitionState StartMenuTransition(string targetLabel, string sourceScene, string requestReason)
        {
            return new LoadingTransitionState
            {
                TargetScene = null,
                TargetLabel = string.IsNullOrEmpty(targetLabel) ? "menu transition" : targetLabel,
                SourceScene = sourceScene,
                LastScene = sourceScene,
                RequestReason = requestReason,
                Phase = LoadingTransitionPhase.WaitingForLoadingScene
            };
        }

        public bool IsTargetScene(string sceneName)
        {
            if (string.IsNullOrEmpty(TargetScene))
                return false;

            return string.Equals(sceneName, TargetScene, StringComparison.Ordinal);
        }

        public void MarkLoadingSceneEntered()
        {
            Phase = LoadingTransitionPhase.LoadingScene;
        }
    }

    internal sealed class LoadingTransitionRecoveryNotice
    {
        public string Title;
        public string Message;
        public float CreatedAt;
    }
}
