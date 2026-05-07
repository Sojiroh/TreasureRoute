using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using TreasureRoute.Models;
using TreasureRoute.Services;

RegisterDalamudResolver();

var tests = new (string Name, Action Test)[]
{
    ("solver exact orders shortest path", SolverExactOrdersShortestPath),
    ("dedupe uses territory map and raw coords", DedupeUsesTerritoryMapAndRawCoords),
    ("solver groups by territory and map", SolverGroupsByTerritoryAndMap),
    ("aetheryte lookup prefers map then territory fallback", AetheryteLookupPrefersMapThenFallback),
    ("aetheryte cache skips malformed rows without dropping valid rows", AetheryteCacheSkipsMalformedRowsWithoutDroppingValidRows),
    ("aetheryte cache passes marker coordinates through", AetheryteCachePassesMarkerCoordinatesThrough),
    ("aetheryte cache supports territory fallback candidates", AetheryteCacheSupportsTerritoryFallbackCandidates),
    ("aetheryte cache supports map fallback candidates", AetheryteCacheSupportsMapFallbackCandidates),
    ("aetheryte lookup still returns null without candidates", AetheryteLookupStillReturnsNullWithoutCandidates),
    ("chat treasure context filter", ChatTreasureContextFilter),
    ("proximity uses displayed map coordinates", ProximityUsesDisplayedMapCoordinates),
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

static void AetheryteCacheSkipsMalformedRowsWithoutDroppingValidRows()
{
    var sources = new[]
    {
        new AetheryteCacheSource(1, "Valid", 9001, 9002, 50.34146f, 85.07317f, false),
        new AetheryteCacheSource(2, "Missing territory", null, 9002, 50.34146f, 85.07317f, false),
        new AetheryteCacheSource(3, "Missing map", 9001, null, 50.34146f, 85.07317f, false),
        new AetheryteCacheSource(4, "Missing coords", 9001, 9002, null, 85.07317f, false),
        new AetheryteCacheSource(5, "Unresolved map", 9001, 9999, 50.34146f, 85.07317f, false),
    };

    var (entries, skippedCount) = AetheryteRepository.BuildCache(sources, IdentityResolverFor(9002));

    Assert(entries.Count == 1, "one valid entry expected");
    Assert(skippedCount == 4, "four malformed rows should be skipped");
    Assert(entries[0].RowId == 1 && entries[0].Name == "Valid", "valid row should survive cache build");
}

static void AetheryteCachePassesMarkerCoordinatesThrough()
{
    var sources = new[]
    {
        new AetheryteCacheSource(1, "FromMarker", 9001, 9002, 12.5f, 13.5f, FromMapMarker: true),
    };

    var (entries, skippedCount) = AetheryteRepository.BuildCache(sources, _ => null);

    Assert(entries.Count == 1, "marker-sourced entry should not need a map resolver");
    Assert(skippedCount == 0, "marker-sourced entry should not be skipped");
    Assert(entries[0].DisplayX == 12.5f && entries[0].DisplayY == 13.5f, "marker coordinates should pass through unchanged");
}

static void AetheryteCacheSupportsTerritoryFallbackCandidates()
{
    var repository = CreateRepository(new[] { new AetheryteEntry(10, "Fallback candidate", 9001, 9002, 21.5f, 22.2f) });
    var found = repository.FindClosestTo(9001, 9999, 21.5f, 22.2f);

    Assert(found is not null, "territory fallback should return a candidate");
    Assert(found!.RowId == 10 && found.Name == "Fallback candidate", "fallback candidate should win");
}

static void AetheryteCacheSupportsMapFallbackCandidates()
{
    var repository = CreateRepository(new[] { new AetheryteEntry(11, "Map fallback", 9001, 9002, 21.5f, 22.2f) });
    var found = repository.FindClosestTo(9999, 9002, 21.5f, 22.2f);

    Assert(found is not null, "map fallback should return a candidate when territory differs");
    Assert(found!.RowId == 11 && found.Name == "Map fallback", "map fallback candidate should win");
}

static void AetheryteLookupStillReturnsNullWithoutCandidates()
{
    var repository = CreateRepository(Array.Empty<AetheryteEntry>());

    Assert(repository.FindClosestTo(9001, 9999, 21.5f, 22.2f) is null, "empty cache should return null");
}

static void ChatTreasureContextFilter()
{
    Assert(ChatListener.HasTreasureContext("timeworn br'aaxskin map <flag>"), "treasure text expected");
    Assert(!ChatListener.HasTreasureContext("hello at limsa <flag>"), "non treasure text should be rejected");
}

static void ProximityUsesDisplayedMapCoordinates()
{
    var mark = Mark(1, 2, 6.1f, 6.0f);
    Assert(ProximityTracker.IsWithinVisitRadius(new Vector2(6.1f, 6.0f), mark, 0.1f), "exact display coords should visit");
    Assert(ProximityTracker.IsWithinVisitRadius(new Vector2(6.2f, 6.0f), mark, 0.11f), "near display coords should visit");
    Assert(!ProximityTracker.IsWithinVisitRadius(new Vector2(6.4f, 6.0f), mark, 0.1f), "distant display coords should not visit");
}

static TreasureMark Mark(uint territory, uint map, float x, float y, int rawX = 0, int rawY = 0, string? placeName = null)
    => new(territory, map, rawX, rawY, x, y, placeName ?? $"T{territory}M{map}", "tester", 0);

static Func<uint, WorldToDisplay?> IdentityResolverFor(uint mapId)
    => id => id == mapId ? new WorldToDisplay(v => v) : null;

static AetheryteRepository CreateRepository(IEnumerable<AetheryteEntry> entries)
    => new AetheryteRepository(entries);

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void RegisterDalamudResolver()
{
    var searchDirs = new List<string>();

    var dalamudHome = Environment.GetEnvironmentVariable("DALAMUD_HOME");
    if (!string.IsNullOrWhiteSpace(dalamudHome)) searchDirs.Add(dalamudHome);

    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    if (!string.IsNullOrWhiteSpace(appData))
        searchDirs.Add(Path.Combine(appData, "XIVLauncher", "addon", "Hooks", "dev"));

    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    if (!string.IsNullOrWhiteSpace(userProfile))
        searchDirs.Add(Path.Combine(userProfile, ".xlcore", "dalamud", "Hooks", "dev"));

    AssemblyLoadContext.Default.Resolving += (context, name) =>
    {
        foreach (var dir in searchDirs)
        {
            var path = Path.Combine(dir, name.Name + ".dll");
            if (File.Exists(path))
                return context.LoadFromAssemblyPath(path);
        }
        return null;
    };
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
