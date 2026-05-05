using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Type of sprite patch operation.
    /// </summary>
    public enum SpritePatchOperationKind
    {
        Pixels = 0,
        Clear = 1
    }

    /// <summary>
    /// Ordered sprite patch operation composed from pixel runs.
    /// Operations are applied by order so small edits can layer predictably.
    /// </summary>
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
