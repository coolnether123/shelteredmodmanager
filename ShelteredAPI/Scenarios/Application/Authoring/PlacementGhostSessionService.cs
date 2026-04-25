namespace ShelteredAPI.Scenarios
{
    internal sealed class PlacementGhostSessionService
    {
        internal sealed class Session
        {
            public string Label;
            public string DefinitionReference;
            public Obj_GhostBase Ghost;
        }

        private Session _active;

        public Session Active
        {
            get { return _active; }
        }

        public void Start(string label, string definitionReference, Obj_GhostBase ghost)
        {
            _active = new Session
            {
                Label = label,
                DefinitionReference = definitionReference,
                Ghost = ghost
            };
        }

        public void Clear()
        {
            _active = null;
        }
    }
}
