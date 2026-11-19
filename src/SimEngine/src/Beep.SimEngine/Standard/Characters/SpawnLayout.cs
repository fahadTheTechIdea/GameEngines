namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// Where a batch of new entities stands: a grid of <see cref="Columns"/> tiles across starting at <see cref="Origin"/>, each
    /// <see cref="Spacing"/> tiles from the next. A game with no space uses <see cref="None"/>. The layout is stated by the caller; nothing is defaulted.
    /// </summary>
    public sealed class SpawnLayout
    {
        public static readonly SpawnLayout None = new SpawnLayout(false, default, 0, 0);

        private SpawnLayout(bool placed, TileCoord origin, int columns, int spacing)
        {
            Placed = placed;
            Origin = origin;
            Columns = columns;
            Spacing = spacing;
        }

        public bool Placed { get; }
        public TileCoord Origin { get; }
        public int Columns { get; }
        public int Spacing { get; }

        public static SpawnLayout Grid(TileCoord origin, int columns, int spacing) => new SpawnLayout(true, origin, columns, spacing);
    }
}
