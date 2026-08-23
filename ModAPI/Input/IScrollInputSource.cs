namespace ModAPI.InputServices
{
    /// <summary>
    /// Reads normalized vertical scroll values without exposing a concrete input backend.
    /// </summary>
    public interface IScrollInputSource
    {
        bool TryGetVerticalScroll(ScrollInputQuery query, out float scroll);
        bool IsIndirectScrollActive();
    }
}
