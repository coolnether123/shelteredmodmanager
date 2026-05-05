using System;

namespace ShelteredAPI.Core
{
    internal sealed class LoadingTransitionRecoveryReportBuilder
    {
        public string BuildLogDetails(
            string reason,
            Exception exception,
            LoadingTransitionState transition,
            string activeScene,
            LoadingTransitionDiagnostics diagnostics)
        {
            return "Recovered failed load. reason=" + reason +
                ", source=" + SourceScene(transition) +
                ", target=" + TargetScene(transition) +
                ", activeScene=" + activeScene +
                ", phase=" + Phase(transition) +
                ", request=" + RequestReason(transition) +
                ", saveState=" + LoadingTransitionRuntime.DescribeSaveManagerState() +
                ExceptionDetails(exception) +
                ", recentProblems=" + LoadingTransitionText.JoinInline(diagnostics.RecentProblems) +
                ", breadcrumbs=" + LoadingTransitionText.JoinInline(diagnostics.Breadcrumbs);
        }

        public string BuildDialogMessage(
            string reason,
            Exception exception,
            LoadingTransitionState transition,
            string activeScene,
            LoadingTransitionDiagnostics diagnostics)
        {
            string text = "ShelteredAPI detected a failed loading transition and returned you to the main menu.\n\n" +
                "Reason: " + reason + "\n" +
                "From: " + SourceScene(transition) + "\n" +
                "Target: " + TargetScene(transition) + "\n" +
                "Last scene: " + activeScene + "\n" +
                "SaveManager: " + LoadingTransitionRuntime.DescribeSaveManagerState();

            if (exception != null)
                text += "\nException: " + exception.GetType().Name + ": " + exception.Message;

            string[] problems = diagnostics.RecentProblems;
            if (problems.Length > 0)
                text += "\n\nRecent errors:\n" + LoadingTransitionText.JoinBullets(problems);
            else
                text += "\n\nNo Unity exception was reported before the transition stalled.";

            text += "\n\nDetails were written to SMM/mod_manager.log.";
            return text;
        }

        private static string SourceScene(LoadingTransitionState transition)
        {
            return transition != null ? LoadingTransitionText.UnknownIfEmpty(transition.SourceScene) : "<unknown>";
        }

        private static string TargetScene(LoadingTransitionState transition)
        {
            return transition != null ? LoadingTransitionText.UnknownIfEmpty(transition.TargetScene) : "<unknown>";
        }

        private static string Phase(LoadingTransitionState transition)
        {
            return transition != null ? transition.Phase.ToString() : "<unknown>";
        }

        private static string RequestReason(LoadingTransitionState transition)
        {
            return transition != null ? LoadingTransitionText.UnknownIfEmpty(transition.RequestReason) : "<unknown>";
        }

        private static string ExceptionDetails(Exception exception)
        {
            return exception != null
                ? ", exception=" + exception.GetType().Name + ": " + exception.Message
                : string.Empty;
        }
    }
}
