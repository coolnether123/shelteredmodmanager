using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesInteractionContent
    {
        public ParalivesInteractionContent()
        {
            Actions = new List<ActionUnit>();
            Groups = new List<InteractionGroup>();
            Interactions = new List<InteractionUnit>();
            GroupChildren = new List<ParalivesInteractionGroupChildRegistration>();
        }

        public IList<ActionUnit> Actions { get; private set; }

        public IList<InteractionGroup> Groups { get; private set; }

        public IList<InteractionUnit> Interactions { get; private set; }

        public IList<ParalivesInteractionGroupChildRegistration> GroupChildren { get; private set; }
    }
}
