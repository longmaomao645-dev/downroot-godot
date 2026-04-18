using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.World.Generation.Passes;

public sealed class BerryPatchSpawnPass(
    ContentId targetId,
    int patchCount,
    int startColumn,
    int startRow,
    int width,
    int height,
    int minSpacing,
    int? maxCountOverride) : IWorldGenPass
{
    public string Name => WorldGenPassTypes.BerryPatchSpawn;

    public void Execute(IWorldGenContext context)
    {
        if (patchCount <= 0)
        {
            return;
        }

        var usableWidth = width > 0 ? Math.Min(width, context.Width) : context.Width;
        var usableHeight = height > 0 ? Math.Min(height, context.Height) : context.Height;
        var originX = Math.Clamp(startColumn, 0, Math.Max(0, context.Width - 1));
        var originY = Math.Clamp(startRow, 0, Math.Max(0, context.Height - 1));

        var centers = new List<ScoredCandidate>();
        for (var y = originY; y < originY + usableHeight; y++)
        {
            for (var x = originX; x < originX + usableWidth; x++)
            {
                var coord = new LocalTileCoord(x, y);
                var score = ScorePatchCenter(context, coord);
                if (score < 0.55f)
                {
                    continue;
                }

                centers.Add(new ScoredCandidate(coord, score));
            }
        }

        var chosenCenters = new List<LocalTileCoord>();
        foreach (var candidate in centers.OrderByDescending(candidate => candidate.Score))
        {
            if (chosenCenters.Count >= patchCount)
            {
                break;
            }

            if (chosenCenters.Any(existing => DistanceSquared(existing, candidate.Coord) < 36))
            {
                continue;
            }

            chosenCenters.Add(candidate.Coord);
        }

        if (chosenCenters.Count == 0)
        {
            return;
        }

        var spawned = 0;
        var totalCap = maxCountOverride ?? (chosenCenters.Count * 5);
        foreach (var center in chosenCenters)
        {
            foreach (var coord in EnumeratePatch(context, center))
            {
                if (spawned >= totalCap)
                {
                    return;
                }

                if (context.IsSpawnOccupied(coord))
                {
                    continue;
                }

                if (minSpacing > 0 && HasNearbyBerry(context, coord, minSpacing))
                {
                    continue;
                }

                if (ScorePatchMember(context, center, coord) < 0.46f)
                {
                    continue;
                }

                context.AddSpawn(coord, targetId);
                spawned++;
            }
        }
    }

    private static IEnumerable<LocalTileCoord> EnumeratePatch(IWorldGenContext context, LocalTileCoord center)
    {
        var ordered = new List<ScoredCandidate>();
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var x = center.X + dx;
                var y = center.Y + dy;
                if (x < 0 || y < 0 || x >= context.Width || y >= context.Height)
                {
                    continue;
                }

                var coord = new LocalTileCoord(x, y);
                var distanceScore = 1f - (MathF.Sqrt((dx * dx) + (dy * dy)) / 3.2f);
                ordered.Add(new ScoredCandidate(coord, distanceScore));
            }
        }

        foreach (var candidate in ordered.OrderByDescending(candidate => candidate.Score))
        {
            yield return candidate.Coord;
        }
    }

    private static float ScorePatchCenter(IWorldGenContext context, LocalTileCoord coord)
    {
        var semantic = context.GetSurfaceSemantic(coord);
        if (!semantic.SupportsTrees || semantic.Visual == TerrainVisualKind.Mountain)
        {
            return -100f;
        }

        var world = context.GetWorldTileCoord(coord);
        var region = context.SampleTerrainRegion(coord);
        var fields = TerrainMacroFieldSampler.Sample(context.WorldSpaceKind, context.WorldSeed, world);
        var density = ForestDensitySampler.Sample(context.WorldSpaceKind, context.WorldSeed, world, region);
        var jitter = context.GetStableUnitValue(world, 8401);

        var regionScore = region switch
        {
            TerrainRegionKind.ForestEdge => 0.92f,
            TerrainRegionKind.ForestCore => 0.64f,
            TerrainRegionKind.OpenLowland => 0.58f,
            TerrainRegionKind.MountainFoot => 0.40f,
            _ => -100f
        };

        if (regionScore < 0f || fields.RiverBase <= 0.95f)
        {
            return -100f;
        }

        var waterAffinity = 1f - Math.Clamp(MathF.Abs(fields.RiverBase - 1.35f) / 0.9f, 0f, 1f);
        return regionScore
            + (density * 0.25f)
            + (fields.MoistureMacro * 0.30f)
            + (waterAffinity * 0.22f)
            + (jitter * 0.08f);
    }

    private static float ScorePatchMember(IWorldGenContext context, LocalTileCoord center, LocalTileCoord coord)
    {
        var semantic = context.GetSurfaceSemantic(coord);
        if (!semantic.SupportsTrees || semantic.Visual == TerrainVisualKind.Mountain)
        {
            return -100f;
        }

        var world = context.GetWorldTileCoord(coord);
        var region = context.SampleTerrainRegion(coord);
        var fields = TerrainMacroFieldSampler.Sample(context.WorldSpaceKind, context.WorldSeed, world);
        if (fields.RiverBase <= 0.95f || region == TerrainRegionKind.RiverChannel || region == TerrainRegionKind.RiverBank)
        {
            return -100f;
        }

        var dx = coord.X - center.X;
        var dy = coord.Y - center.Y;
        var distanceScore = 1f - (MathF.Sqrt((dx * dx) + (dy * dy)) / 3.2f);
        var density = ForestDensitySampler.Sample(context.WorldSpaceKind, context.WorldSeed, world, region);
        var jitter = context.GetStableUnitValue(world, 8423);
        return (distanceScore * 0.52f)
            + (fields.MoistureMacro * 0.22f)
            + (density * 0.16f)
            + (jitter * 0.10f);
    }

    private static bool HasNearbyBerry(IWorldGenContext context, LocalTileCoord coord, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var x = coord.X + dx;
                var y = coord.Y + dy;
                if (x < 0 || y < 0 || x >= context.Width || y >= context.Height)
                {
                    continue;
                }

                if (context.IsSpawnOccupied(new LocalTileCoord(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int DistanceSquared(LocalTileCoord a, LocalTileCoord b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private readonly record struct ScoredCandidate(LocalTileCoord Coord, float Score);
}
