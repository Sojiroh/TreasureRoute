using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace TreasureRoute.Services;

public interface IAetheryteLookup
{
    AetheryteEntry? FindClosestTo(uint territoryId, uint mapId, float displayX, float displayY);
}

internal sealed record AetheryteCacheSource(
    uint RowId,
    string Name,
    uint? TerritoryTypeId,
    uint? MapId,
    float? CoordX,
    float? CoordY,
    bool FromMapMarker);

internal delegate Vector2 WorldToDisplay(Vector2 world);

public sealed class AetheryteRepository : IAetheryteLookup
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, List<AetheryteEntry>> byTerritory = new();
    private readonly Dictionary<(uint TerritoryId, uint MapId), List<AetheryteEntry>> byTerritoryMap = new();
    private readonly Dictionary<uint, List<AetheryteEntry>> byMap = new();
    private bool loaded;

    public AetheryteRepository(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
    }

    internal AetheryteRepository(IEnumerable<AetheryteEntry> entries)
    {
        dataManager = null!;
        log = null!;
        IndexEntries(entries);
        loaded = true;
    }

    private void IndexEntries(IEnumerable<AetheryteEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (!byTerritory.TryGetValue(entry.TerritoryTypeId, out var list))
                byTerritory[entry.TerritoryTypeId] = list = new List<AetheryteEntry>();
            list.Add(entry);

            var key = (entry.TerritoryTypeId, entry.MapId);
            if (!byTerritoryMap.TryGetValue(key, out var mapList))
                byTerritoryMap[key] = mapList = new List<AetheryteEntry>();
            mapList.Add(entry);

            if (!byMap.TryGetValue(entry.MapId, out var mapOnlyList))
                byMap[entry.MapId] = mapOnlyList = new List<AetheryteEntry>();
            mapOnlyList.Add(entry);
        }
    }

    public void EnsureLoaded()
    {
        if (loaded) return;

        try
        {
            byTerritory.Clear();
            byTerritoryMap.Clear();
            byMap.Clear();

            var aetherytes = dataManager.GetExcelSheet<Aetheryte>();
            if (aetherytes is null)
            {
                log.Warning("Aetheryte sheet not available.");
                return;
            }

            var sources = new List<AetheryteCacheSource>();
            var mapsById = new Dictionary<uint, Map>();
            var skippedCount = 0;

            var mapMarkers = dataManager.GetSubrowExcelSheet<MapMarker>();
            var aetheryteMarkers = new Dictionary<uint, MapMarker>();
            if (mapMarkers != null)
            {
                foreach (var subrows in mapMarkers)
                    foreach (var marker in subrows)
                        if (marker.DataType == 3)
                            aetheryteMarkers.TryAdd(marker.DataKey.RowId, marker);
            }

            foreach (var aetheryte in aetherytes)
            {
                try
                {
                    if (aetheryte.RowId == 0) continue;

                    var territory = aetheryte.Territory.ValueNullable;
                    var map = aetheryte.Map.ValueNullable;
                    uint? territoryId = territory?.RowId;
                    uint? mapId = map?.RowId;

                    // Resolve territory/map from aetheryte direct fields first
                    map ??= territory?.Map.ValueNullable;
                    mapId ??= map?.RowId;

                    float? coordX = null;
                    float? coordY = null;
                    bool fromMapMarker = false;

                    if (map is not null && aetheryteMarkers.TryGetValue(aetheryte.RowId, out var marker))
                    {
                        coordX = MarkerToMap(marker.X, map.Value.SizeFactor);
                        coordY = MarkerToMap(marker.Y, map.Value.SizeFactor);
                        fromMapMarker = true;
                    }

                    if (!coordX.HasValue || !coordY.HasValue)
                    {
                        float? worldX = null;
                        float? worldZ = null;

                        if (mapId.HasValue)
                        {
                            foreach (var levelRef in aetheryte.Level)
                            {
                                var level = levelRef.ValueNullable;
                                if (level is null) continue;

                                var levelMapId = level.Value.Map.ValueNullable?.RowId;
                                if (levelMapId == mapId.Value)
                                {
                                    worldX = level.Value.X;
                                    worldZ = level.Value.Z;
                                    territoryId ??= level.Value.Territory.ValueNullable?.RowId;
                                    break;
                                }
                            }
                        }

                        if (!worldX.HasValue || !worldZ.HasValue)
                        {
                            foreach (var levelRef in aetheryte.Level)
                            {
                                var level = levelRef.ValueNullable;
                                if (level is null) continue;

                                territoryId ??= level.Value.Territory.ValueNullable?.RowId;
                                map ??= level.Value.Map.ValueNullable;
                                mapId ??= level.Value.Map.ValueNullable?.RowId;
                                worldX ??= level.Value.X;
                                worldZ ??= level.Value.Z;

                                if (worldX.HasValue && worldZ.HasValue)
                                    break;
                            }
                        }

                        // Final map resolution
                        map ??= territory?.Map.ValueNullable;
                        mapId ??= map?.RowId;

                        if (!territoryId.HasValue || !mapId.HasValue || !worldX.HasValue || !worldZ.HasValue)
                        {
                            skippedCount++;
                            continue;
                        }

                        coordX = worldX.Value;
                        coordY = worldZ.Value;
                    }

                    sources.Add(new AetheryteCacheSource(
                        aetheryte.RowId,
                        aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? $"Aetheryte #{aetheryte.RowId}",
                        territoryId!.Value,
                        mapId!.Value,
                        coordX.Value,
                        coordY.Value,
                        fromMapMarker));

                    if (map is not null && !mapsById.ContainsKey(mapId.Value))
                        mapsById[mapId.Value] = map.Value;
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "Skipped aetheryte row {RowId} due to malformed data", aetheryte.RowId);
                    skippedCount++;
                }
            }

            var (entries, buildSkippedCount) = BuildCache(sources, mapId =>
                mapsById.TryGetValue(mapId, out var map)
                    ? new WorldToDisplay(world => MapUtil.WorldToMap(world, map))
                    : null);
            skippedCount += buildSkippedCount;

            IndexEntries(entries);

            loaded = true;
            log.Information($"Cached aetherytes across {byTerritory.Count} territories.");
            log.Debug($"Aetheryte cache: {entries.Count} cached, {skippedCount} skipped");
        }
        catch (System.Exception ex)
        {
            byTerritory.Clear();
            byTerritoryMap.Clear();
            log.Error(ex, "Failed to cache aetherytes; will retry on next lookup.");
        }
    }

    internal static (List<AetheryteEntry> entries, int skippedCount) BuildCache(IEnumerable<AetheryteCacheSource> sources, Func<uint, WorldToDisplay?> mapResolver)
    {
        var entries = new List<AetheryteEntry>();
        var skippedCount = 0;

        foreach (var source in sources)
        {
            try
            {
                if (source.TerritoryTypeId is null || source.MapId is null || source.CoordX is null || source.CoordY is null)
                {
                    skippedCount++;
                    continue;
                }

                float displayX;
                float displayY;

                if (source.FromMapMarker)
                {
                    displayX = source.CoordX.Value;
                    displayY = source.CoordY.Value;
                }
                else
                {
                    var convert = mapResolver(source.MapId.Value);
                    if (convert is null)
                    {
                        skippedCount++;
                        continue;
                    }

                    var display = convert(new Vector2(source.CoordX.Value, source.CoordY.Value));
                    displayX = display.X;
                    displayY = display.Y;
                }

                entries.Add(new AetheryteEntry(
                    source.RowId,
                    source.Name,
                    source.TerritoryTypeId.Value,
                    source.MapId.Value,
                    displayX,
                    displayY));
            }
            catch
            {
                skippedCount++;
            }
        }

        return (entries, skippedCount);
    }

    public IReadOnlyList<AetheryteEntry> ForTerritory(uint territoryId)
    {
        EnsureLoaded();
        return byTerritory.TryGetValue(territoryId, out var list) ? list : System.Array.Empty<AetheryteEntry>();
    }

    public AetheryteEntry? FindClosestTo(uint territoryId, float displayX, float displayY)
        => FindClosest(ForTerritory(territoryId), displayX, displayY);

    public IReadOnlyList<AetheryteEntry> ForTerritoryMap(uint territoryId, uint mapId)
    {
        EnsureLoaded();
        return byTerritoryMap.TryGetValue((territoryId, mapId), out var list) ? list : System.Array.Empty<AetheryteEntry>();
    }

    public IReadOnlyList<AetheryteEntry> ForMap(uint mapId)
    {
        EnsureLoaded();
        return byMap.TryGetValue(mapId, out var list) ? list : System.Array.Empty<AetheryteEntry>();
    }

    public IReadOnlyList<AetheryteEntry> GetAll()
    {
        EnsureLoaded();
        return byTerritory.Values.SelectMany(x => x).ToList();
    }

    public AetheryteEntry? FindClosestTo(uint territoryId, uint mapId, float displayX, float displayY)
    {
        var candidates = ForTerritoryMap(territoryId, mapId);
        if (candidates.Count == 0)
            candidates = ForMap(mapId);
        if (candidates.Count == 0)
            candidates = ForTerritory(territoryId);
        if (candidates.Count == 0)
            candidates = GetAll();
        return FindClosest(candidates, displayX, displayY);
    }

    private static AetheryteEntry? FindClosest(IReadOnlyList<AetheryteEntry> candidates, float displayX, float displayY)
    {
        if (candidates.Count == 0) return null;

        AetheryteEntry? best = null;
        var bestDistanceSq = float.MaxValue;
        foreach (var entry in candidates)
        {
            var dx = entry.DisplayX - displayX;
            var dy = entry.DisplayY - displayY;
            var d = dx * dx + dy * dy;
            if (d < bestDistanceSq)
            {
                bestDistanceSq = d;
                best = entry;
            }
        }
        return best;
    }

    private static float MarkerToMap(int pos, uint scale)
    {
        var num = scale / 100f;
        return (pos / 2048.0f) * (41.0f / num) + 1.0f;
    }
}

public sealed record AetheryteEntry(
    uint RowId,
    string Name,
    uint TerritoryTypeId,
    uint MapId,
    float DisplayX,
    float DisplayY);
