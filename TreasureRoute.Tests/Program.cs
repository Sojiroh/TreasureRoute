using System.Buffers.Binary;
using System.Numerics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using TreasureRoute.Models;
using TreasureRoute.Services;

var tests = new (string Name, Action Test)[]
{
    ("solver exact orders shortest path", SolverExactOrdersShortestPath),
    ("dedupe uses territory map and raw coords", DedupeUsesTerritoryMapAndRawCoords),
    ("solver groups by territory and map", SolverGroupsByTerritoryAndMap),
    ("aetheryte lookup prefers map then territory fallback", AetheryteLookupPrefersMapThenFallback),
    ("aetheryte cache skips malformed rows without dropping valid rows", AetheryteCacheSkipsMalformedRowsWithoutDroppingValidRows),
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
    var sources = CreateSourceArray(
        CreateCacheSource(1, "Valid", 9001, 9002, 50.34146f, 85.07317f),
        CreateCacheSource(2, "Missing territory", null, 9002, 50.34146f, 85.07317f),
        CreateCacheSource(3, "Missing map", 9001, null, 50.34146f, 85.07317f),
        CreateCacheSource(4, "Missing coords", 9001, 9002, null, 85.07317f),
        CreateCacheSource(5, "Unresolved map", 9001, 9999, 50.34146f, 85.07317f));

    var (entries, skippedCount) = BuildCache(sources, 9002);

    Assert(entries.Count == 1, "one valid entry expected");
    Assert(skippedCount == 4, "four malformed rows should be skipped");
    Assert(entries[0].RowId == 1 && entries[0].Name == "Valid", "valid row should survive cache build");
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

static Array CreateSourceArray(params object[] sources)
{
    var sourceType = GetCacheSourceType();
    var array = Array.CreateInstance(sourceType, sources.Length);
    for (var i = 0; i < sources.Length; i++)
        array.SetValue(sources[i], i);
    return array;
}

static object CreateCacheSource(uint rowId, string name, uint? territoryTypeId, uint? mapId, float? coordX, float? coordY, bool fromMapMarker = false)
    => Activator.CreateInstance(GetCacheSourceType(), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object?[] { rowId, name, territoryTypeId, mapId, coordX, coordY, fromMapMarker }, null)!
        ?? throw new InvalidOperationException("failed to create cache source");

static (List<AetheryteEntry> Entries, int SkippedCount) BuildCache(Array sources, uint mapId)
{
    EnsureAssemblyLoaded("Dalamud.dll");
    EnsureAssemblyLoaded("Lumina.dll");
    EnsureAssemblyLoaded("Lumina.Excel.dll");

    var method = typeof(AetheryteRepository).GetMethod("BuildCache", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("BuildCache seam not found");

    var mapType = method.GetParameters()[1].ParameterType.GenericTypeArguments[1].GenericTypeArguments[0];
    var map = CreateSyntheticMap(mapType);

    var nullableMapType = typeof(Nullable<>).MakeGenericType(mapType);
    var delegateType = typeof(Func<,>).MakeGenericType(typeof(uint), nullableMapType);
    var idParameter = Expression.Parameter(typeof(uint), "mapId");
    var body = Expression.Condition(
        Expression.Equal(idParameter, Expression.Constant(mapId)),
        Expression.Convert(Expression.Constant(map, mapType), nullableMapType),
        Expression.Default(nullableMapType));
    var mapResolver = Expression.Lambda(delegateType, body, idParameter).Compile();

    var result = (ValueTuple<List<AetheryteEntry>, int>)method.Invoke(null, new object?[] { sources, mapResolver })!;
    return (result.Item1, result.Item2);
}

static object CreateSyntheticMap(Type mapType)
{
    var excelPageType = GetRuntimeType("Lumina.Excel.ExcelPage");
    var pageCtor = excelPageType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(ctor => ctor.GetParameters().Length == 3);
    var mapCtor = mapType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(ctor => ctor.GetParameters().Length == 3);

    var offsets = ResolveMapOffsets(mapType, excelPageType, pageCtor, mapCtor);
    var pageData = new byte[256];
    WriteSentinel(pageData, offsets.OffsetX, 1000, GetMemberSize(mapType, "OffsetX"));
    WriteSentinel(pageData, offsets.OffsetY, 2000, GetMemberSize(mapType, "OffsetY"));
    WriteSentinel(pageData, offsets.SizeFactor, 100, GetMemberSize(mapType, "SizeFactor"));

    var page = pageCtor.Invoke(new object?[] { null, pageData, (ushort)0 }) ?? throw new InvalidOperationException("failed to create excel page");
    return mapCtor.Invoke(new object?[] { page, 0u, 0u }) ?? throw new InvalidOperationException("failed to create map");
}

static (int OffsetX, int OffsetY, int SizeFactor) ResolveMapOffsets(Type mapType, Type excelPageType, ConstructorInfo pageCtor, ConstructorInfo mapCtor)
{
    int FindOffset(string memberName, int seed)
    {
        var property = mapType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{mapType.Name}.{memberName} property not found");

        var writeSize = property.PropertyType == typeof(short) || property.PropertyType == typeof(ushort) ? 2 : 4;
        for (var offset = 0; offset <= 124; offset += writeSize)
        {
            var pageData = new byte[256];
            WriteSentinel(pageData, offset, seed, writeSize);

            var page = pageCtor.Invoke(new object?[] { null, pageData, (ushort)0 }) ?? throw new InvalidOperationException("failed to create excel page");
            var map = mapCtor.Invoke(new object?[] { page, 0u, 0u }) ?? throw new InvalidOperationException("failed to create map");
            var actual = property.GetValue(map);
            if (MatchesSentinel(actual, seed, property.PropertyType))
                return offset;
        }

        throw new InvalidOperationException($"Unable to resolve {mapType.Name}.{memberName} offset");
    }

    return (FindOffset("OffsetX", 1000), FindOffset("OffsetY", 2000), FindOffset("SizeFactor", 100));
}

static bool MatchesSentinel(object? value, int seed, Type propertyType)
{
    if (propertyType == typeof(short)) return value is short actual && actual == seed;
    if (propertyType == typeof(ushort)) return value is ushort unsignedActual && unsignedActual == seed;
    if (propertyType == typeof(int)) return value is int intActual && intActual == seed;
    if (propertyType == typeof(uint)) return value is uint uintActual && uintActual == (uint)seed;
    return Equals(value, Convert.ChangeType(seed, propertyType));
}

static int GetMemberSize(Type mapType, string name)
{
    var property = mapType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"{mapType.Name}.{name} property not found");
    return property.PropertyType == typeof(short) || property.PropertyType == typeof(ushort) ? 2 : 4;
}

static void WriteSentinel(byte[] data, int offset, int value, int size)
{
    if (size == 2)
        BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(offset, size), (short)value);
    else
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset, size), value);
}

static AetheryteRepository CreateRepository(IEnumerable<AetheryteEntry> entries)
{
    var repository = (AetheryteRepository)FormatterServices.GetUninitializedObject(typeof(AetheryteRepository));
    var territoryGroups = entries.GroupBy(entry => entry.TerritoryTypeId).ToDictionary(group => group.Key, group => group.ToList());
    var territoryMapGroups = entries.GroupBy(entry => (entry.TerritoryTypeId, entry.MapId)).ToDictionary(group => group.Key, group => group.ToList());
    var mapGroups = entries.GroupBy(entry => entry.MapId).ToDictionary(group => group.Key, group => group.ToList());

    SetField(repository, "byTerritory", territoryGroups);
    SetField(repository, "byTerritoryMap", territoryMapGroups);
    SetField(repository, "byMap", mapGroups);
    SetField(repository, "loaded", true);

    return repository;
}

static Type GetCacheSourceType()
    => typeof(AetheryteRepository).Assembly.GetType("TreasureRoute.Services.AetheryteCacheSource", true)!;

static Type GetRuntimeType(string fullName)
{
    var loaded = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(assembly => GetTypesSafe(assembly))
        .FirstOrDefault(type => type.FullName == fullName);
    if (loaded is not null)
        return loaded;

    foreach (var assemblyName in typeof(AetheryteRepository).Assembly.GetReferencedAssemblies())
    {
        try
        {
            var assembly = Assembly.Load(assemblyName);
            var type = GetTypesSafe(assembly).FirstOrDefault(candidate => candidate.FullName == fullName);
            if (type is not null)
                return type;
        }
        catch
        {
        }
    }

    throw new InvalidOperationException($"{fullName} not found");
}

static IEnumerable<Type> GetTypesSafe(Assembly assembly)
{
    try
    {
        return assembly.GetTypes();
    }
    catch
    {
        return Array.Empty<Type>();
    }
}

static void EnsureAssemblyLoaded(string assemblyFile)
{
    var hookDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xlcore", "dalamud", "Hooks", "dev");
    var assemblyPath = Path.Combine(hookDir, assemblyFile);
    if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => string.Equals(assembly.GetName().Name + ".dll", assemblyFile, StringComparison.OrdinalIgnoreCase)))
        Assembly.LoadFrom(assemblyPath);
}

static void SetField<T>(object target, string name, T value)
{
    var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"{target.GetType().Name}.{name} not found");
    field.SetValue(target, value);
}

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
