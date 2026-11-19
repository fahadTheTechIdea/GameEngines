using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceRoutePlanner
{
    // Extract bends of the impassable triangle boundary, not every mesh vertex.
    // The sampling lattice is terrain data; visible routes remain free-angle edges.
    private static IEnumerable<WorldPoint> TerrainFeatures(WorldSurfaceQuery surface,
        WorldSurfaceTraversal traversal, WorldSpatialWorkBudget budget, CancellationToken token)
    {
        if (surface.HeightField is not {} query) return [];
        var field = query.Geometry;
        var edges = new HashSet<(int A, int B)>();
        WorldPoint Point(int id) => new(field.Origin.X + id % field.Columns * field.StepX,
            field.Origin.Y + id / field.Columns * field.StepY);
        void Triangle(int a, int b, int c)
        {
            budget.Charge(4);
            var pa = Point(a); var pb = Point(b); var pc = Point(c);
            // The shared heightfield owner supplies the exact triangle gradient.
            var sample = query.Sample(new((pa.X + pb.X + pc.X) / 3, (pa.Y + pb.Y + pc.Y) / 3), token);
            if (traversal.Allows(sample)) return;
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                var key = u < v ? (u, v) : (v, u);
                if (!edges.Add(key)) edges.Remove(key);
            }
        }
        // A bounded world may occupy only part of a larger source lattice.
        // Unreachable remote triangles cannot contribute a route feature.
        // Keep the same conservative source-cell halo for full and bounded
        // storage; no terrain sample or requested world extent is changed.
        var window=surface.TerrainDomainWindow!;
        for (int y = window.FirstRow; y <= window.LastRow; y++)
        for (int x = window.FirstColumn; x <= window.LastColumn; x++)
        {
            int a = y * field.Columns + x, b = a + 1, c = a + field.Columns, d = c + 1;
            Triangle(a, b, d); Triangle(a, d, c);
        }
        var neighbors = new Dictionary<int, List<int>>();
        foreach (var (a, b) in edges)
        {
            budget.Charge(2);
            if (!neighbors.TryGetValue(a, out var aa)) neighbors[a] = aa = [];
            if (!neighbors.TryGetValue(b, out var bb)) neighbors[b] = bb = [];
            aa.Add(b); bb.Add(a);
        }
        var result = new List<WorldPoint>();
        foreach (var (id, adjacent) in neighbors)
        {
            budget.Charge();
            if (adjacent.Count == 2)
            {
                // Integer mesh coordinates make removal of straight subdivisions exact.
                int ax = adjacent[0] % field.Columns - id % field.Columns;
                int ay = adjacent[0] / field.Columns - id / field.Columns;
                int bx = adjacent[1] % field.Columns - id % field.Columns;
                int by = adjacent[1] / field.Columns - id / field.Columns;
                if (ax * by == ay * bx && ax * bx + ay * by < 0) continue;
            }
            result.Add(Point(id));
        }
        return result;
    }
}
