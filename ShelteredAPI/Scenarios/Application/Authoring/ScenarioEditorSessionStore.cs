using System;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioEditorSessionStore
    {
        ScenarioEditorSession Current { get; }
        bool HasActiveSession { get; }
        string CurrentFilePath { get; }

        void Set(ScenarioEditorSession session, string filePath);
        void Clear();
    }

    internal sealed class ScenarioEditorSessionStore : IScenarioEditorSessionStore
    {
        private readonly object _sync = new object();
        private ScenarioEditorSession _current;
        private string _currentFilePath;

        public ScenarioEditorSession Current
        {
            get
            {
                lock (_sync)
                {
                    return _current;
                }
            }
        }

        public bool HasActiveSession
        {
            get
            {
                lock (_sync)
                {
                    return _current != null;
                }
            }
        }

        public string CurrentFilePath
        {
            get
            {
                lock (_sync)
                {
                    return _currentFilePath;
                }
            }
        }

        public void Set(ScenarioEditorSession session, string filePath)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            lock (_sync)
            {
                _current = session;
                _currentFilePath = filePath;
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _current = null;
                _currentFilePath = null;
            }
        }
    }
}
