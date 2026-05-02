using System.Collections.Generic;

namespace TreasureRoute.Models;

public class RoutePlan
{
    public List<MapRoute> Maps { get; } = new();
    public float TotalDistance { get; set; }
}

public class MapRoute
{
    public uint TerritoryTypeId { get; set; }
    public uint MapId { get; set; }
    public string PlaceName { get; set; } = string.Empty;
    public string? AetheryteName { get; set; }
    public uint? AetheryteId { get; set; }
    public float AetheryteX { get; set; }
    public float AetheryteY { get; set; }
    public List<TreasureMark> OrderedMarks { get; } = new();
    public float Distance { get; set; }
}
