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

public sealed class AetheryteRepository : IAetheryteLookup
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, List<AetheryteEntry>> byTerritory = new();
    private readonly Dictionary<(uint TerritoryId, uint MapId), List<AetheryteEntry>> byTerritoryMap = new();
    private bool loaded;

    public AetheryteRepository(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
    }

    public void EnsureLoaded()
    {
        if (loaded) return;

        try
        {
            byTerritory.Clear();
            byTerritoryMap.Clear();

            var aetherytes = dataManager.GetExcelSheet<Aetheryte>();
            if (aetherytes is null)
            {
                log.Warning("Aetheryte sheet not available.");
                return;
            }

            foreach (var aetheryte in aetherytes)
            {
                if (!aetheryte.IsAetheryte) continue;
                if (aetheryte.RowId == 0) continue;

                var territory = aetheryte.Territory.ValueNullable;
                if (territory is null) continue;
                var map = territory.Value.Map.ValueNullable;
                if (map is null) continue;

                if (aetheryte.Level.Count == 0) continue;
                var level = aetheryte.Level[0].ValueNullable;
                if (level is null) continue;

                var display = MapUtil.WorldToMap(
                    new Vector2(level.Value.X, level.Value.Z), map.Value);

                var entry = new AetheryteEntry(
                    aetheryte.RowId,
                    aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? $"Aetheryte #{aetheryte.RowId}",
                    territory.Value.RowId,
                    map.Value.RowId,
                    display.X,
                    display.Y);

                if (!byTerritory.TryGetValue(territory.Value.RowId, out var list))
                {
                    list = new List<AetheryteEntry>();
                    byTerritory[territory.Value.RowId] = list;
                }
                list.Add(entry);

                var key = (territory.Value.RowId, map.Value.RowId);
                if (!byTerritoryMap.TryGetValue(key, out var mapList))
                {
                    mapList = new List<AetheryteEntry>();
                    byTerritoryMap[key] = mapList;
                }
                mapList.Add(entry);
            }

            loaded = true;
            log.Information($"Cached aetherytes across {byTerritory.Count} territories.");
        }
        catch (System.Exception ex)
        {
            byTerritory.Clear();
            byTerritoryMap.Clear();
            log.Error(ex, "Failed to cache aetherytes; will retry on next lookup.");
        }
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

    public AetheryteEntry? FindClosestTo(uint territoryId, uint mapId, float displayX, float displayY)
    {
        var candidates = ForTerritoryMap(territoryId, mapId);
        if (candidates.Count == 0)
            candidates = ForTerritory(territoryId);
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
}

public sealed record AetheryteEntry(
    uint RowId,
    string Name,
    uint TerritoryTypeId,
    uint MapId,
    float DisplayX,
    float DisplayY);
