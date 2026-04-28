using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public sealed class SpritePatchDefinition
    {
        public SpritePatchDefinition()
        {
            Operations = new List<SpritePatchOperation>();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string BaseSpriteId { get; set; }
        public string BaseRelativePath { get; set; }
        public string BaseRuntimeSpriteKey { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<SpritePatchOperation> Operations { get; private set; }
    }
}
