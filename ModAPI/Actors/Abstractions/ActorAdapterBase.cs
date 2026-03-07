namespace ModAPI.Actors
{
    public abstract class ActorAdapterBase : IConditionalActorAdapter
    {
        public abstract string AdapterId { get; }

        public virtual int Priority
        {
            get { return 0; }
        }

        public virtual bool ShouldSynchronize(ActorAdapterContext context)
        {
            return context != null && context.ShouldRunByDefault;
        }

        public abstract void Synchronize(IActorSystem actors, long currentTick);
    }
}
