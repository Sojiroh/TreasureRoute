using TreasureRoute.Models;
using TreasureRoute.Services;

var tests = new (string Name, Action Test)[]
{
    ("solver exact orders shortest path", SolverExactOrdersShortestPath),
    ("dedupe uses territory map and raw coords", DedupeUsesTerritoryMapAndRawCoords),
    ("solver groups by territory and map", SolverGroupsByTerritoryAndMap),
    ("aetheryte lookup prefers map then territory fallback", AetheryteLookupPrefersMapThenFallback),
    ("chat treasure context filter", ChatTreasureContextFilter),
};

foreach (var test in tests)
{
    test.Test();
    Console.WriteLine($"PASS {test.Name}");
}

static void SolverExactOrdersShortestPath()
{
    var solver = new RouteSolver(new FakeLookup(new AetheryteEntry(1, "Start", 10, 20, 0, 0)));
    var plan = solver.BuildPlan(new[]
    {
        Mark(10, 20, 10, 0),
        Mark(10, 20, 1, 0),
        Mark(10, 20, 2, 0),
    });

    Assert(plan.Maps.Count == 1, "one map route expected");
    Assert(plan.Maps[0].OrderedMarks.Select(m => m.DisplayX).SequenceEqual(new[] { 1f, 2f, 10f }), "unexpected exact route order");
    Assert(Math.Abs(plan.TotalDistance - 10f) < 0.001f, "unexpected route distance");
}

static void DedupeUsesTerritoryMapAndRawCoords()
{
    var existing = new[] { Mark(1, 2, 10, 10, rawX: 100, rawY: 200) };
    Assert(MarkDeduper.IsDuplicate(existing, Mark(1, 2, 99, 99, rawX: 100, rawY: 200), 0.5f), "same raw coords should duplicate");
    Assert(!MarkDeduper.IsDuplicate(existing, Mark(9, 2, 10, 10, rawX: 100, rawY: 200), 0.5f), "different territory should not duplicate");
    Assert(!MarkDeduper.IsDuplicate(existing, Mark(1, 9, 10, 10, rawX: 100, rawY: 200), 0.5f), "different map should not duplicate");
    Assert(MarkDeduper.IsDuplicate(existing, Mark(1, 2, 10.1f, 10.1f, rawX: 101, rawY: 200), 0.5f), "raw mismatch should still fall back to radius");
    Assert(MarkDeduper.IsDuplicate(new[] { Mark(1, 2, 10, 10) }, Mark(1, 2, 10.2f, 10.2f), 0.5f), "missing raw coords should use radius");
}

static void SolverGroupsByTerritoryAndMap()
{
    var solver = new RouteSolver(new FakeLookup());
    var plan = solver.BuildPlan(new[]
    {
        Mark(1, 1, 1, 1, placeName: "same ids A"),
        Mark(1, 1, 2, 2, placeName: "same ids B"),
        Mark(1, 2, 1, 1),
        Mark(2, 1, 1, 1),
    });
    Assert(plan.Maps.Count == 3, "territory/map grouping expected");
    Assert(plan.Maps.Single(m => m.TerritoryTypeId == 1 && m.MapId == 1).OrderedMarks.Count == 2, "same territory/map should stay grouped");
}

static void AetheryteLookupPrefersMapThenFallback()
{
    var lookup = new FakeLookup(
        new AetheryteEntry(1, "Territory", 1, 1, 0, 0),
        new AetheryteEntry(2, "Map", 1, 2, 10, 10));
    Assert(lookup.FindClosestTo(1, 2, 9, 9)?.Name == "Map", "map candidate should win");
    Assert(lookup.FindClosestTo(1, 3, 1, 1)?.Name == "Territory", "territory fallback expected");
}

static void ChatTreasureContextFilter()
{
    Assert(ChatListener.HasTreasureContext("timeworn br'aaxskin map <flag>"), "treasure text expected");
    Assert(!ChatListener.HasTreasureContext("hello at limsa <flag>"), "non treasure text should be rejected");
}

static TreasureMark Mark(uint territory, uint map, float x, float y, int rawX = 0, int rawY = 0, string? placeName = null)
    => new(territory, map, rawX, rawY, x, y, placeName ?? $"T{territory}M{map}", "tester", 0);

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed class FakeLookup : IAetheryteLookup
{
    private readonly List<AetheryteEntry> entries;

    public FakeLookup(params AetheryteEntry[] entries) => this.entries = entries.ToList();

    public AetheryteEntry? FindClosestTo(uint territoryId, uint mapId, float displayX, float displayY)
    {
        var candidates = entries.Where(e => e.TerritoryTypeId == territoryId && e.MapId == mapId).ToList();
        if (candidates.Count == 0)
            candidates = entries.Where(e => e.TerritoryTypeId == territoryId).ToList();
        return candidates.OrderBy(e => Distance(e.DisplayX, e.DisplayY, displayX, displayY)).FirstOrDefault();
    }

    private static float Distance(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
