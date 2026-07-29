using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesInteractions
    {
        event ParalivesInteractionSelectedEventHandler InteractionSelected;

        int RegisteredActionCount { get; }

        int RegisteredGroupCount { get; }

        int RegisteredInteractionCount { get; }

        int RegisteredGroupChildCount { get; }

        ulong GetRootGroupGuid(ParalivesInteractionRootGroup rootGroup);

        void AddInteractionToOtherCharacterInteractions(ulong interactionGuid, string nestedInteractionDisplayName);
    }
}
