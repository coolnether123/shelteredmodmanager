using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class SpritePatchApplyService
    {
        private readonly SpritePatchValidator _validator;
        private readonly SpritePatchRuntimeRenderer _renderer;

        public SpritePatchApplyService(SpritePatchValidator validator, SpritePatchRuntimeRenderer renderer)
        {
            _validator = validator;
            _renderer = renderer;
        }

        public Sprite Apply(SpritePatchDefinition patch, Sprite baseSprite)
        {
            if (!_validator.IsValid(patch))
                return null;

            return _renderer.Render(baseSprite, patch);
        }
    }
}
