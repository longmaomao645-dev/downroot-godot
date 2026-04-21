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
    bool avoidRiverBank,
    float candidateDensity,
    int? maxCountOverride) : IWorldGenPass
{
    public string Name => WorldGenPassTypes.ScatterSpawn;

    public void Execute(IWorldGenContext context)
    {
        if (count <= 0 && candidateDensity <= 0f && maxCountOverride is null)
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

        var scored = candidates
            .Select(coord => new ScoredCandidate(coord, ScoreCandidate(context, coord)))
            .Where(candidate => candidate.Score >= GetMinimumAcceptedScore())
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();

        var desiredCount = ComputeDesiredCount(scored.Length);
        if (desiredCount <= 0)
        {
            return;
        }

        var chosen = new List<LocalTileCoord>();
        foreach (var candidate in scored)
        {
            if (chosen.Count >= desiredCount)
            {
                break;
            }

            var coord = candidate.Coord;
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
        var fields = TerrainMacroFieldSampler.Sample(context.WorldSpaceKind, context.WorldSeed, world);
        var density = ForestDensitySampler.Sample(context.WorldSpaceKind, context.WorldSeed, world, region);
        var waterPenalty = Math.Clamp(1.15f - fields.RiverBase, 0f, 1f);
        float score = region switch
        {
            TerrainRegionKind.ForestCore => 0.92f,
            TerrainRegionKind.ForestEdge => 0.66f,
            TerrainRegionKind.OpenLowland => 0.20f,
            TerrainRegionKind.RiverBank => 0.08f,
            TerrainRegionKind.MountainCore => -100f,
            TerrainRegionKind.RiverChannel => -100f,
            TerrainRegionKind.MudFlat => 0.06f,
            TerrainRegionKind.MountainFoot => 0.58f,
            _ => 0.16f
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

        if (requiredTerrainRegion == TerrainRegionKind.MountainFoot)
        {
            score += (fields.TemperatureBias >= 0.46f ? 0.14f : -0.04f);
            score += (fields.RidgeMacro * 0.12f);
        }
        else
        {
            score += ((1f - Math.Abs(fields.TemperatureBias - 0.52f)) * 0.08f);
        }

        var semantic = context.GetSurfaceSemantic(coord);
        if (semantic.Visual == TerrainVisualKind.Mountain)
        {
            return -100f;
        }

        if (!requireSupportsTrees)
        {
            return score
                + (jitter * 0.28f)
                - (waterPenalty * 0.18f);
        }

        if (!semantic.SupportsTrees)
        {
            return -100f;
        }

        return score
            + (density * 0.85f)
            - (waterPenalty * 0.48f)
            + (jitter * 0.12f);
    }

    private int ComputeDesiredCount(int viableCandidateCount)
    {
        if (viableCandidateCount <= 0)
        {
            return 0;
        }

        if (candidateDensity > 0f || maxCountOverride.HasValue)
        {
            var desired = (int)MathF.Ceiling(viableCandidateCount * MathF.Max(0f, candidateDensity));
            if (count > 0)
            {
                desired = Math.Max(desired, Math.Min(count, viableCandidateCount));
            }

            if (maxCountOverride.HasValue)
            {
                desired = Math.Min(desired, maxCountOverride.Value);
            }

            return Math.Clamp(desired, 0, viableCandidateCount);
        }

        return Math.Min(count, viableCandidateCount);
    }

    private float GetMinimumAcceptedScore()
    {
        if (requireSupportsTrees || preferForestCore || preferForestEdge)
        {
            return 0.35f;
        }

        if (requiredTerrainRegion is not null || avoidRiverBank)
        {
            return 0.10f;
        }

        return float.MinValue;
    }

    private static int DistanceSquared(LocalTileCoord a, LocalTileCoord b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private readonly record struct ScoredCandidate(LocalTileCoord Coord, float Score);
}
