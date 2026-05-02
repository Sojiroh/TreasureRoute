using System.Collections.Generic;
using TreasureRoute.Models;

namespace TreasureRoute.Services;

public static class MarkDeduper
{
    public static bool IsDuplicate(IEnumerable<TreasureMark> existingMarks, TreasureMark candidate, float radius)
    {
        var radiusSq = radius * radius;
        foreach (var existing in existingMarks)
        {
            if (existing.TerritoryTypeId != candidate.TerritoryTypeId || existing.MapId != candidate.MapId)
                continue;

            if ((existing.RawX != 0 || existing.RawY != 0) && (candidate.RawX != 0 || candidate.RawY != 0))
            {
                if (existing.RawX == candidate.RawX && existing.RawY == candidate.RawY)
                    return true;
            }

            var dx = existing.DisplayX - candidate.DisplayX;
            var dy = existing.DisplayY - candidate.DisplayY;
            if (dx * dx + dy * dy <= radiusSq)
                return true;
        }

        return false;
    }
}
