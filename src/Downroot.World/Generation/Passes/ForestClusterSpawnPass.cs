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
    private const int PatchRadius = 3;

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
                if (!IsEligibleRegion(region))
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

                if (!OwnsBiome(context, world, region, density))
                {
                    continue;
                }

                candidates.Add(new TreeSpawnCandidate(
                    coord,
                    region,
                    density,
                    score,
                    biome,
                    context.GetStableUnitValue(world, 7001 + ((int)biome * 43))));
            }
        }

        var desiredCount = ComputeDesiredCount(candidates.Count);
        if (desiredCount <= 0 || candidates.Count == 0)
        {
            return;
        }

        var centers = SelectPatchCenters(candidates, desiredCount);
        if (centers.Count == 0)
        {
            return;
        }

        var chosen = new List<TreeSpawnCandidate>();
        foreach (var center in centers)
        {
            if (chosen.Count >= desiredCount)
            {
                break;
            }

            foreach (var candidate in EnumeratePatchCandidates(context, center, candidates))
            {
                if (chosen.Count >= desiredCount)
                {
                    break;
                }

                if (context.IsSpawnOccupied(candidate.Coord))
                {
                    continue;
                }

                var candidateSpacing = GetEffectiveMinSpacing(candidate);
                if (candidateSpacing > 0 && chosen.Any(existing =>
                {
                    var existingSpacing = GetEffectiveMinSpacing(existing);
                    var requiredSpacing = Math.Max(1, (candidateSpacing + existingSpacing) / 2);
                    return DistanceSquared(existing.Coord, candidate.Coord) < requiredSpacing * requiredSpacing;
                }))
                {
                    continue;
                }

                chosen.Add(candidate);
            }
        }

        foreach (var candidate in chosen)
        {
            var world = context.GetWorldTileCoord(candidate.Coord);
            var profile = TreeBiomeProfileSampler.Sample(context.WorldSpaceKind, context.WorldSeed, world, biome, speciesPoolIds.Count);
            var treeId = TreeSpeciesResolver.Resolve(biome, candidate.VariantRoll, profile, speciesPoolIds);
            var offset = SampleSpawnOffset(context, candidate.Coord, treeId);
            context.AddSpawn(candidate.Coord, treeId, offset.OffsetX, offset.OffsetY);
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
                TerrainRegionKind.ForestEdge => 0.58f,
                _ => -100f
            },
            TreeBiomeKind.ConiferMountainFoot => region switch
            {
                TerrainRegionKind.MountainFoot => 0.92f,
                TerrainRegionKind.ForestCore => 0.54f,
                _ => -100f
            },
            TreeBiomeKind.SparseForestEdge => region switch
            {
                TerrainRegionKind.ForestEdge => 0.88f,
                TerrainRegionKind.OpenLowland => 0.46f,
                _ => -100f
            },
            TreeBiomeKind.OpenLowlandSparse => region switch
            {
                TerrainRegionKind.OpenLowland => 0.70f,
                TerrainRegionKind.ForestEdge => 0.28f,
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

    private int GetEffectiveMinSpacing(TreeSpawnCandidate candidate)
    {
        var spacing = minSpacing;
        if (candidate.Density >= 0.78f)
        {
            spacing--;
        }

        if (candidate.Density >= 0.90f && biome is TreeBiomeKind.TemperateForestCore or TreeBiomeKind.ConiferMountainFoot)
        {
            spacing--;
        }

        return Math.Max(biome == TreeBiomeKind.OpenLowlandSparse ? 3 : 2, spacing);
    }

    private List<TreeSpawnCandidate> SelectPatchCenters(IReadOnlyList<TreeSpawnCandidate> candidates, int desiredCount)
    {
        var centerTarget = Math.Max(1, (int)MathF.Ceiling(desiredCount / (biome == TreeBiomeKind.OpenLowlandSparse ? 1.6f : 3.5f)));
        var centers = new List<TreeSpawnCandidate>();
        foreach (var candidate in candidates.OrderByDescending(candidate => candidate.Score))
        {
            if (centers.Count >= centerTarget)
            {
                break;
            }

            if (centers.Any(existing => DistanceSquared(existing.Coord, candidate.Coord) < 36))
            {
                continue;
            }

            centers.Add(candidate);
        }

        return centers;
    }

    private IEnumerable<TreeSpawnCandidate> EnumeratePatchCandidates(
        IWorldGenContext context,
        TreeSpawnCandidate center,
        IReadOnlyList<TreeSpawnCandidate> candidates)
    {
        var byCoord = candidates.ToDictionary(candidate => candidate.Coord);
        var patchCandidates = new List<TreeSpawnCandidate>();
        for (var dy = -PatchRadius; dy <= PatchRadius; dy++)
        {
            for (var dx = -PatchRadius; dx <= PatchRadius; dx++)
            {
                var x = center.Coord.X + dx;
                var y = center.Coord.Y + dy;
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    continue;
                }

                var coord = new LocalTileCoord(x, y);
                if (!byCoord.TryGetValue(coord, out var candidate))
                {
                    continue;
                }

                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                if (distance > PatchRadius + 0.25f)
                {
                    continue;
                }

                var patchFactor = 1f - (distance / (PatchRadius + 0.5f));
                var patchScore = candidate.Score + (patchFactor * 0.38f);
                patchCandidates.Add(candidate with { Score = patchScore });
            }
        }

        return patchCandidates.OrderByDescending(candidate => candidate.Score);
    }

    private bool OwnsBiome(IWorldGenContext context, WorldTileCoord world, TerrainRegionKind region, float density)
    {
        var fields = TerrainMacroFieldSampler.Sample(context.WorldSpaceKind, context.WorldSeed, world);
        var targetScore = GetBiomeOwnershipScore(biome, region, fields, density);
        foreach (var candidateBiome in Enum.GetValues<TreeBiomeKind>())
        {
            if (candidateBiome == biome)
            {
                continue;
            }

            if (GetBiomeOwnershipScore(candidateBiome, region, fields, density) > targetScore)
            {
                return false;
            }
        }

        return targetScore > 0f;
    }

    private static float GetBiomeOwnershipScore(TreeBiomeKind candidateBiome, TerrainRegionKind region, TerrainMacroFields fields, float density)
    {
        return candidateBiome switch
        {
            TreeBiomeKind.TemperateForestCore => region switch
            {
                TerrainRegionKind.ForestCore => 1.00f + (fields.ForestMass * 0.20f) + (density * 0.15f) - (fields.RidgeMacro * 0.10f),
                TerrainRegionKind.ForestEdge => 0.55f + (fields.MoistureMacro * 0.15f) + (density * 0.10f),
                _ => -1f
            },
            TreeBiomeKind.ConiferMountainFoot => region switch
            {
                TerrainRegionKind.MountainFoot => 1.02f + (fields.RidgeMacro * 0.18f) + (fields.TemperatureBias * 0.18f),
                TerrainRegionKind.ForestCore => 0.48f + (fields.RidgeMacro * 0.16f) + (fields.TemperatureBias * 0.14f),
                TerrainRegionKind.ForestEdge => 0.42f + (fields.RidgeMacro * 0.12f) + (fields.TemperatureBias * 0.12f),
                _ => -1f
            },
            TreeBiomeKind.SparseForestEdge => region switch
            {
                TerrainRegionKind.ForestEdge => 0.98f + (fields.OpenFieldBias * 0.14f) + (density * 0.12f),
                TerrainRegionKind.OpenLowland => 0.45f + (fields.OpenFieldBias * 0.18f),
                _ => -1f
            },
            TreeBiomeKind.OpenLowlandSparse => region switch
            {
                TerrainRegionKind.OpenLowland => 0.92f + (fields.OpenFieldBias * 0.20f) + ((1f - density) * 0.10f),
                TerrainRegionKind.ForestEdge => 0.20f + (fields.OpenFieldBias * 0.08f),
                _ => -1f
            },
            _ => -1f
        };
    }

    private bool IsEligibleRegion(TerrainRegionKind region)
    {
        if (requiredTerrainRegion.HasValue && region == requiredTerrainRegion.Value)
        {
            return true;
        }

        return biome switch
        {
            TreeBiomeKind.TemperateForestCore => region is TerrainRegionKind.ForestCore or TerrainRegionKind.ForestEdge,
            TreeBiomeKind.ConiferMountainFoot => region is TerrainRegionKind.MountainFoot or TerrainRegionKind.ForestEdge or TerrainRegionKind.ForestCore,
            TreeBiomeKind.SparseForestEdge => region is TerrainRegionKind.ForestEdge or TerrainRegionKind.OpenLowland,
            TreeBiomeKind.OpenLowlandSparse => region is TerrainRegionKind.OpenLowland or TerrainRegionKind.ForestEdge,
            _ => false
        };
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

    private static float GetMinimumAcceptedScore() => 0.42f;

    private static int DistanceSquared(LocalTileCoord a, LocalTileCoord b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private (int OffsetX, int OffsetY) SampleSpawnOffset(IWorldGenContext context, LocalTileCoord coord, ContentId treeId)
    {
        var world = context.GetWorldTileCoord(coord);
        var salt = treeId.Value.GetHashCode();
        var offsetX = (int)MathF.Round((context.GetStableUnitValue(world, salt + 17) - 0.5f) * 10f);
        var offsetY = (int)MathF.Round((context.GetStableUnitValue(world, salt + 31) - 0.5f) * 8f);
        return (offsetX, offsetY);
    }
}
