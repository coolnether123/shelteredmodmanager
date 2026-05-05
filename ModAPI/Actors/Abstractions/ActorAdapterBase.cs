namespace ModAPI.Actors
{
    /// <summary>
    /// Convenience base class for live-sync adapters that should run only when actor runtime state changes.
    /// Override <see cref="IActorAdapter.Synchronize"/> and optionally <see cref="ShouldSynchronize"/> for custom gating.
    /// </summary>
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
