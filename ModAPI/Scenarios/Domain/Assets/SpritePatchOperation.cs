using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public enum SpritePatchOperationKind
    {
        Pixels = 0,
        Clear = 1
    }

    public sealed class SpritePatchOperation
    {
        public SpritePatchOperation()
        {
            Runs = new List<SpritePatchDeltaRun>();
            Kind = SpritePatchOperationKind.Pixels;
        }

        public string Id { get; set; }
        public int Order { get; set; }
        public SpritePatchOperationKind Kind { get; set; }
        public List<SpritePatchDeltaRun> Runs { get; private set; }
    }
}
