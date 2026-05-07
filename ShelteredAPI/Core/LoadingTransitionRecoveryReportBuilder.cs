using System;

using ShelteredAPI.Hooks;
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
                ", target=" + Target(transition) +
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
                "Target: " + Target(transition) + "\n" +
                "Last scene: " + activeScene + "\n" +
                "SaveManager: " + LoadingTransitionRuntime.DescribeSaveManagerState();

            if (exception != null)
                text += "\nException: " + exception.GetType().Name + ": " + exception.Message;

            string[] problems = diagnostics.RecentProblems;
            if (problems.Length > 0)
                text += "\n\nRecent errors:\n" + LoadingTransitionText.JoinBullets(problems);
            else
                text += "\n\nNo Unity error details were retained for this transition exception.";

            text += "\n\nDetails were written to SMM/mod_manager.log.";
            return text;
        }

        private static string SourceScene(LoadingTransitionState transition)
        {
            return transition != null ? LoadingTransitionText.UnknownIfEmpty(transition.SourceScene) : "<unknown>";
        }

        private static string Target(LoadingTransitionState transition)
        {
            if (transition == null)
                return "<unknown>";

            if (!string.IsNullOrEmpty(transition.TargetScene))
                return transition.TargetScene;

            return LoadingTransitionText.UnknownIfEmpty(transition.TargetLabel);
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
