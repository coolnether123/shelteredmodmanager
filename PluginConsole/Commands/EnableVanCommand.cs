using UnityEngine;

namespace ConsoleCommands
{
    public class EnableVanCommand : ICommand
    {
        public string Name => "van";
        public string Description => "Repairs the van, making it drivable.";

        public string Execute(string[] args)
        {
            var van = Object.FindObjectOfType<Obj_CamperVan>();
            if (van == null)
            {
                return "Camper van object not found.";
            }

            if (van.IsDrivable())
            {
                return "The van is already drivable.";
            }

            var requiredParts = van.GetListOfRequiredParts();
            foreach (var partType in requiredParts)
            {
                int remaining = van.GetRemainingRequiredPart(partType);
                if (remaining > 0)
                {
                    van.AddPart(partType, remaining);
                }
            }

            if (van.IsDrivable())
            {
                return "The van has been repaired and is now drivable.";
            }
            else
            {
                return "Could not repair the van. Some parts may be missing or an error occurred.";
            }
        }
    }
}
