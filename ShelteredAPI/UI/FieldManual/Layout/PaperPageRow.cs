namespace ShelteredAPI.UI.FieldManual.Layout
{
    /// <summary>
    /// Theme-neutral row metadata used by field-manual paged panels.
    /// </summary>
    internal sealed class PaperPageRow<T>
    {
        public readonly T Item;
        public readonly int Height;
        public readonly bool KeepWithNext;

        public PaperPageRow(T item, int height, bool keepWithNext)
        {
            Item = item;
            Height = height < 1 ? 1 : height;
            KeepWithNext = keepWithNext;
        }
    }
}
