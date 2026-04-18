using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.World.Generation.Passes;

public sealed class ForestClusterSpawnPass(
    TreeBiomeKind biome,
    IReadOnlyList<ContentId> speciesPoolIds,
    int startColumn,
    int startRow,
    int width,
    int height,
    int minSpacing,
    bool requireBuildable,
    bool requireSupportsTrees,
    TerrainRegionKind? requiredTerrainRegion,
    bool avoidRiverBank,
    float candidateDensity,
    int? maxCountOverride) : IWorldGenPass
{
    public string Name => WorldGenPassTypes.ForestClusterSpawn;

    public void Execute(IWorldGenContext context)
    {
        if (speciesPoolIds.Count == 0 || candidateDensity <= 0f)
        {
            return;
        }

        var usableWidth = width > 0 ? Math.Min(width, context.Width) : context.Width;
        var usableHeight = height > 0 ? Math.Min(height, context.Height) : context.Height;
        var originX = Math.Clamp(startColumn, 0, Math.Max(0, context.Width - 1));
        var originY = Math.Clamp(startRow, 0, Math.Max(0, context.Height - 1));

        var candidates = new List<TreeSpawnCandidate>();
        for (var y = originY; y < originY + usableHeight; y++)
        {
            for (var x = originX; x < originX + usableWidth; x++)
            {
                var coord = new LocalTileCoord(x, y);
                var region = context.SampleTerrainRegion(coord);
                if (requiredTerrainRegion.HasValue && region != requiredTerrainRegion.Value)
                {
                    continue;
                }

                if (avoidRiverBank && region == TerrainRegionKind.RiverBank)
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

                if (semantic.Visual == TerrainVisualKind.Mountain || context.HasRaisedFeature(coord))
                {
                    continue;
                }

                var world = context.GetWorldTileCoord(coord);
                var density = ForestDensitySampler.Sample(context.WorldSpaceKind, context.WorldSeed, world, region);
                var score = ScoreCandidate(context, coord, region, density);
                if (score < GetMinimumAcceptedScore())
                {
                    continue;
                }

                candidates.Add(new TreeSpawnCandidate(
                    coord,
                    region,
                    score,
                    biome,
                    context.GetStableUnitValue(world, 7001 + ((int)biome * 43))));
            }
        }

        var desiredCount = ComputeDesiredCount(candidates.Count);
        if (desiredCount <= 0)
        {
            return;
        }

        var chosen = new List<TreeSpawnCandidate>();
        foreach (var candidate in candidates.OrderByDescending(candidate => candidate.Density))
        {
            if (chosen.Count >= desiredCount)
            {
                break;
            }

            if (context.IsSpawnOccupied(candidate.Coord))
            {
                continue;
            }

            if (minSpacing > 0 && chosen.Any(existing => DistanceSquared(existing.Coord, candidate.Coord) < minSpacing * minSpacing))
            {
                continue;
            }

            chosen.Add(candidate);
        }

        foreach (var candidate in chosen)
        {
            var world = context.GetWorldTileCoord(candidate.Coord);
            var profile = TreeBiomeProfileSampler.Sample(context.WorldSpaceKind, context.WorldSeed, world, biome, speciesPoolIds.Count);
            var treeId = TreeSpeciesResolver.Resolve(biome, candidate.VariantRoll, profile, speciesPoolIds);
            context.AddSpawn(candidate.Coord, treeId);
        }
    }

    private float ScoreCandidate(IWorldGenContext context, LocalTileCoord coord, TerrainRegionKind region, float density)
    {
        var world = context.GetWorldTileCoord(coord);
        var fields = TerrainMacroFieldSampler.Sample(context.WorldSpaceKind, context.WorldSeed, world);
        var jitter = context.GetStableUnitValue(world, 7103 + ((int)biome * 29));
        var waterPenalty = Math.Clamp(1.2f - fields.RiverBase, 0f, 1f);

        float regionScore = biome switch
        {
            TreeBiomeKind.TemperateForestCore => region switch
            {
                TerrainRegionKind.ForestCore => 0.95f,
                TerrainRegionKind.ForestEdge => 0.35f,
                _ => -100f
            },
            TreeBiomeKind.ConiferMountainFoot => region switch
            {
                TerrainRegionKind.MountainFoot => 0.92f,
                TerrainRegionKind.ForestCore => 0.28f,
                _ => -100f
            },
            TreeBiomeKind.SparseForestEdge => region switch
            {
                TerrainRegionKind.ForestEdge => 0.88f,
                TerrainRegionKind.OpenLowland => 0.20f,
                _ => -100f
            },
            TreeBiomeKind.OpenLowlandSparse => region switch
            {
                TerrainRegionKind.OpenLowland => 0.70f,
                TerrainRegionKind.ForestEdge => 0.16f,
                _ => -100f
            },
            _ => -100f
        };

        if (regionScore < 0f)
        {
            return regionScore;
        }

        if (biome == TreeBiomeKind.ConiferMountainFoot)
        {
            regionScore += (fields.TemperatureBias >= 0.46f ? 0.16f : -0.05f);
            regionScore += fields.RidgeMacro * 0.10f;
        }

        return regionScore
            + (density * 0.78f)
            - (waterPenalty * 0.42f)
            + (jitter * 0.10f);
    }

    private int ComputeDesiredCount(int viableCandidateCount)
    {
        if (viableCandidateCount <= 0)
        {
            return 0;
        }

        var desired = (int)MathF.Ceiling(viableCandidateCount * candidateDensity);
        if (maxCountOverride.HasValue)
        {
            desired = Math.Min(desired, maxCountOverride.Value);
        }

        return Math.Clamp(desired, 0, viableCandidateCount);
    }

    private static float GetMinimumAcceptedScore() => 0.50f;

    private static int DistanceSquared(LocalTileCoord a, LocalTileCoord b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
}
