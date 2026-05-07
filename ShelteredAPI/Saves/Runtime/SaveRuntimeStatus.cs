namespace ShelteredAPI.Saves.Runtime
{
    internal static class SaveRuntimeStatus
    {
        private static bool _quitSaveCompleted;

        internal static bool IsQuitSaveCompleted
        {
            get { return _quitSaveCompleted; }
        }

        internal static void MarkQuitSaveCompleted()
        {
            _quitSaveCompleted = true;
        }

        internal static void ResetQuitSaveCompleted()
        {
            _quitSaveCompleted = false;
        }
    }
}
