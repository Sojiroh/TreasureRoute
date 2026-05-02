using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRoute.Models;

namespace TreasureRoute.Services;

public sealed class RouteSolver
{
    private readonly IAetheryteLookup aetherytes;
    private const int ExactSolverThreshold = 12;

    public RouteSolver(IAetheryteLookup aetherytes)
    {
        this.aetherytes = aetherytes;
    }

    public RoutePlan BuildPlan(IEnumerable<TreasureMark> marks)
    {
        var plan = new RoutePlan();

        var byTerritory = marks
            .GroupBy(m => (m.TerritoryTypeId, m.MapId))
            .OrderBy(g => g.First().PlaceName, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byTerritory)
        {
            var groupList = group.ToList();
            if (groupList.Count == 0) continue;

            var route = SolveForMap(group.Key.TerritoryTypeId, group.Key.MapId, groupList[0].PlaceName, groupList);
            plan.Maps.Add(route);
            plan.TotalDistance += route.Distance;
        }

        return plan;
    }

    private MapRoute SolveForMap(uint territoryId, uint mapId, string placeName, List<TreasureMark> marks)
    {
        var route = new MapRoute
        {
            TerritoryTypeId = territoryId,
            MapId = mapId,
            PlaceName = placeName,
        };

        var centerX = marks.Average(m => m.DisplayX);
        var centerY = marks.Average(m => m.DisplayY);
        var aetheryte = aetherytes.FindClosestTo(territoryId, mapId, centerX, centerY);

        var startX = aetheryte?.DisplayX ?? centerX;
        var startY = aetheryte?.DisplayY ?? centerY;

        if (aetheryte != null)
        {
            route.AetheryteId = aetheryte.RowId;
            route.AetheryteName = aetheryte.Name;
            route.AetheryteX = aetheryte.DisplayX;
            route.AetheryteY = aetheryte.DisplayY;
        }

        var (orderedIndices, totalDistance) = marks.Count <= ExactSolverThreshold
            ? SolveExact(marks, startX, startY)
            : SolveHeuristic(marks, startX, startY);

        foreach (var idx in orderedIndices)
            route.OrderedMarks.Add(marks[idx]);
        route.Distance = totalDistance;
        return route;
    }

    private static (int[] order, float distance) SolveExact(List<TreasureMark> marks, float startX, float startY)
    {
        var n = marks.Count;
        var startDist = new float[n];
        var pairDist = new float[n, n];
        for (var i = 0; i < n; i++)
        {
            startDist[i] = Distance(startX, startY, marks[i].DisplayX, marks[i].DisplayY);
            for (var j = 0; j < n; j++)
                pairDist[i, j] = i == j ? 0f : Distance(marks[i].DisplayX, marks[i].DisplayY, marks[j].DisplayX, marks[j].DisplayY);
        }

        var stateCount = 1 << n;
        var dp = new float[stateCount, n];
        var parent = new int[stateCount, n];
        for (var s = 0; s < stateCount; s++)
            for (var i = 0; i < n; i++)
            {
                dp[s, i] = float.PositiveInfinity;
                parent[s, i] = -1;
            }

        for (var i = 0; i < n; i++)
            dp[1 << i, i] = startDist[i];

        for (var mask = 1; mask < stateCount; mask++)
        {
            for (var last = 0; last < n; last++)
            {
                if ((mask & (1 << last)) == 0) continue;
                var current = dp[mask, last];
                if (float.IsPositiveInfinity(current)) continue;
                for (var next = 0; next < n; next++)
                {
                    if ((mask & (1 << next)) != 0) continue;
                    var newMask = mask | (1 << next);
                    var candidate = current + pairDist[last, next];
                    if (candidate < dp[newMask, next])
                    {
                        dp[newMask, next] = candidate;
                        parent[newMask, next] = last;
                    }
                }
            }
        }

        var fullMask = stateCount - 1;
        var bestEnd = 0;
        var best = float.PositiveInfinity;
        for (var i = 0; i < n; i++)
        {
            if (dp[fullMask, i] < best)
            {
                best = dp[fullMask, i];
                bestEnd = i;
            }
        }

        var order = new int[n];
        var idxOut = n - 1;
        var maskCursor = fullMask;
        var node = bestEnd;
        while (idxOut >= 0)
        {
            order[idxOut--] = node;
            var prev = parent[maskCursor, node];
            maskCursor &= ~(1 << node);
            if (prev < 0) break;
            node = prev;
        }

        return (order, best);
    }

    private static (int[] order, float distance) SolveHeuristic(List<TreasureMark> marks, float startX, float startY)
    {
        var n = marks.Count;
        var visited = new bool[n];
        var order = new int[n];
        var totalDistance = 0f;
        var currentX = startX;
        var currentY = startY;

        for (var step = 0; step < n; step++)
        {
            var bestIdx = -1;
            var bestDist = float.PositiveInfinity;
            for (var i = 0; i < n; i++)
            {
                if (visited[i]) continue;
                var d = Distance(currentX, currentY, marks[i].DisplayX, marks[i].DisplayY);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }
            visited[bestIdx] = true;
            order[step] = bestIdx;
            totalDistance += bestDist;
            currentX = marks[bestIdx].DisplayX;
            currentY = marks[bestIdx].DisplayY;
        }

        TwoOpt(order, marks, startX, startY, ref totalDistance);
        return (order, totalDistance);
    }

    private static void TwoOpt(int[] order, List<TreasureMark> marks, float startX, float startY, ref float totalDistance)
    {
        var improved = true;
        var iterations = 0;
        while (improved && iterations++ < 50)
        {
            improved = false;
            for (var i = 0; i < order.Length - 1; i++)
            {
                for (var j = i + 1; j < order.Length; j++)
                {
                    var before = EdgeDistanceBefore(order, marks, startX, startY, i, j);
                    var after = EdgeDistanceAfter(order, marks, startX, startY, i, j);
                    var delta = after - before;
                    if (delta < -0.0001f)
                    {
                        Array.Reverse(order, i, j - i + 1);
                        totalDistance += delta;
                        improved = true;
                    }
                }
            }
        }
    }

    private static float EdgeDistanceBefore(int[] order, List<TreasureMark> marks, float startX, float startY, int i, int j)
    {
        var first = marks[order[i]];
        var total = i == 0
            ? Distance(startX, startY, first.DisplayX, first.DisplayY)
            : Distance(marks[order[i - 1]].DisplayX, marks[order[i - 1]].DisplayY, first.DisplayX, first.DisplayY);

        if (j < order.Length - 1)
        {
            var last = marks[order[j]];
            var next = marks[order[j + 1]];
            total += Distance(last.DisplayX, last.DisplayY, next.DisplayX, next.DisplayY);
        }

        return total;
    }

    private static float EdgeDistanceAfter(int[] order, List<TreasureMark> marks, float startX, float startY, int i, int j)
    {
        var newFirst = marks[order[j]];
        var total = i == 0
            ? Distance(startX, startY, newFirst.DisplayX, newFirst.DisplayY)
            : Distance(marks[order[i - 1]].DisplayX, marks[order[i - 1]].DisplayY, newFirst.DisplayX, newFirst.DisplayY);

        if (j < order.Length - 1)
        {
            var newLast = marks[order[i]];
            var next = marks[order[j + 1]];
            total += Distance(newLast.DisplayX, newLast.DisplayY, next.DisplayX, next.DisplayY);
        }

        return total;
    }

    private static float Distance(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
