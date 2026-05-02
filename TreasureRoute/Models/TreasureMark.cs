using System;

namespace TreasureRoute.Models;

[Serializable]
public class TreasureMark
{
    public uint TerritoryTypeId { get; set; }
    public uint MapId { get; set; }
    public int RawX { get; set; }
    public int RawY { get; set; }
    public float DisplayX { get; set; }
    public float DisplayY { get; set; }
    public string PlaceName { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public long PostedAtUnix { get; set; }

    public TreasureMark() { }

    public TreasureMark(
        uint territoryTypeId, uint mapId, int rawX, int rawY,
        float displayX, float displayY, string placeName, string sender, long postedAtUnix)
    {
        TerritoryTypeId = territoryTypeId;
        MapId = mapId;
        RawX = rawX;
        RawY = rawY;
        DisplayX = displayX;
        DisplayY = displayY;
        PlaceName = placeName;
        Sender = sender;
        PostedAtUnix = postedAtUnix;
    }

    public string CoordinateLabel => $"( {DisplayX:0.0} , {DisplayY:0.0} )";
}
