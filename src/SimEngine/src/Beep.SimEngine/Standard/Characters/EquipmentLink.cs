namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// An item a character or formation carries, named by its id in an outside catalog. The engine keeps the id and the count and nothing
    /// else: item numbers belong to the catalog, and a test that reads both checks that every id exists there.
    /// </summary>
    public sealed class EquipmentLink
    {
        public EquipmentLink(string item, int count) { Item = item; Count = count; }
        public string Item { get; }
        public int Count { get; }
    }
}
