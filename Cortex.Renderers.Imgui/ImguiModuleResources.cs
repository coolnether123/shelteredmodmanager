using System;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiModuleResources : IDisposable
    {
        private readonly ImguiTextureCache _textureCache = new ImguiTextureCache();

        public ImguiTextureCache TextureCache
        {
            get { return _textureCache; }
        }

        public void Dispose()
        {
            _textureCache.Dispose();
        }
    }
}
