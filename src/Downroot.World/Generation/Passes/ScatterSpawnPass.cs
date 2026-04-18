using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.World.Generation.Passes;

public sealed class ScatterSpawnPass(
    ContentId targetId,
    int count,
    int startColumn,
    int startRow,
    int width,
    int height,
    string? requiredSurfaceRegion,
    int minSpacing,
    bool requireBuildable,
    bool requireSupportsTrees,
    TerrainRegionKind? requiredTerrainRegion,
    bool preferForestCore,
    bool preferForestEdge,
    bool avoidRiverBank) : IWorldGenPass
{
    public string Name => WorldGenPassTypes.ScatterSpawn;

    public void Execute(IWorldGenContext context)
    {
        if (count <= 0)
        {
            return;
        }

        var usableWidth = width > 0 ? Math.Min(width, context.Width) : context.Width;
        var usableHeight = height > 0 ? Math.Min(height, context.Height) : context.Height;
        var originX = Math.Clamp(startColumn, 0, Math.Max(0, context.Width - 1));
        var originY = Math.Clamp(startRow, 0, Math.Max(0, context.Height - 1));

        var candidates = new List<LocalTileCoord>();
        for (var y = originY; y < originY + usableHeight; y++)
        {
            for (var x = originX; x < originX + usableWidth; x++)
            {
                var coord = new LocalTileCoord(x, y);
                if (requiredSurfaceRegion is not null && !context.HasSurfaceRegion(coord, requiredSurfaceRegion))
                {
                    continue;
                }

                var terrainRegion = context.SampleTerrainRegion(coord);
                if (requiredTerrainRegion.HasValue && terrainRegion != requiredTerrainRegion.Value)
                {
                    continue;
                }

                if (avoidRiverBank && terrainRegion == TerrainRegionKind.RiverBank)
                {
                    continue;
                }

                var semantic = context.GetSurfaceSemantic(coord);
                if (requireBuildable && !semantic.Buildable)
                {
                    continue;
                }

                if (requireSupportsTrees && !semantic.SupportsTrees)
                {
                    continue;
                }

                if (context.HasRaisedFeature(coord))
                {
                    continue;
                }

                candidates.Add(coord);
            }
        }

        var ordered = candidates
            .OrderByDescending(coord => ScoreCandidate(context, coord))
            .ToArray();
        var chosen = new List<LocalTileCoord>();
        foreach (var coord in ordered)
        {
            if (chosen.Count >= count)
            {
                break;
            }

            if (context.IsSpawnOccupied(coord))
            {
                continue;
            }

            if (minSpacing > 0 && chosen.Any(existing => DistanceSquared(existing, coord) < minSpacing * minSpacing))
            {
                continue;
            }

            context.AddSpawn(coord, targetId);
            chosen.Add(coord);
        }
    }

    private float ScoreCandidate(IWorldGenContext context, LocalTileCoord coord)
    {
        var world = context.GetWorldTileCoord(coord);
        var jitter = context.GetStableUnitValue(world, targetId.Value.GetHashCode());
        if (!requireSupportsTrees && requiredTerrainRegion is null && !preferForestCore && !preferForestEdge && !avoidRiverBank)
        {
            return jitter;
        }

        var region = context.SampleTerrainRegion(coord);
        float score = region switch
        {
            TerrainRegionKind.ForestCore => 1.0f,
            TerrainRegionKind.ForestEdge => 0.74f,
            TerrainRegionKind.OpenLowland => 0.38f,
            TerrainRegionKind.RiverBank => 0.18f,
            TerrainRegionKind.MountainCore => -100f,
            TerrainRegionKind.RiverChannel => -100f,
            TerrainRegionKind.MudFlat => 0.22f,
            TerrainRegionKind.MountainFoot => 0.46f,
            _ => 0.30f
        };

        if (preferForestCore)
        {
            score += region == TerrainRegionKind.ForestCore ? 0.45f : -0.10f;
        }

        if (preferForestEdge)
        {
            score += region == TerrainRegionKind.ForestEdge ? 0.32f : -0.05f;
        }

        if (avoidRiverBank && region == TerrainRegionKind.RiverBank)
        {
            score -= 0.8f;
        }

        return score + (jitter * 0.15f);
    }

    private static int DistanceSquared(LocalTileCoord a, LocalTileCoord b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
}
