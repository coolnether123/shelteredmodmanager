namespace ModAPI.InputServices
{
    /// <summary>
    /// Provides normalized vertical scroll values for UI consumers without binding them to a concrete input backend.
    /// </summary>
    public interface IScrollInputSource
    {
        bool TryGetVerticalScroll(ScrollInputQuery query, out float scroll);
        bool IsIndirectScrollActive();
    }
}
