using ModAPI.Actors.Internal;

namespace ModAPI.Actors
{
    public static class ActorSystem
    {
        private static IActorSystem _instance;

        public static IActorSystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ActorSystemImpl();
                return _instance;
            }
        }

        internal static ActorSystemImpl InternalInstance
        {
            get { return (ActorSystemImpl)Instance; }
        }
    }
}
