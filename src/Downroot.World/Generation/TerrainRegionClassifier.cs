using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class TerrainRegionClassifier
{
    private static readonly WorldTileCoord[] ForestEdgeNeighborOffsets =
    [
        new WorldTileCoord(-6, 0),
        new WorldTileCoord(6, 0),
        new WorldTileCoord(0, -6),
        new WorldTileCoord(0, 6),
        new WorldTileCoord(-4, -4),
        new WorldTileCoord(4, -4),
        new WorldTileCoord(-4, 4),
        new WorldTileCoord(4, 4)
    ];

    public static TerrainRegionSample Sample(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TerrainMacroFields fields)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return new TerrainRegionSample(TerrainRegionKind.OpenLowland, float.MaxValue, false, false, true);
        }

        var riverChannelScore = ComputeRiverChannelScore(fields);
        var riverBankScore = ComputeRiverBankScore(fields);
        var mountainCoreScore = ComputeMountainCoreScore(fields);
        var mountainFootScore = ComputeMountainFootScore(fields);
        var forestScore = ComputeForestScore(fields);
        var openScore = ComputeOpenScore(fields);
        var bankSharpness = ComputeBankSharpness(fields, riverChannelScore, riverBankScore, mountainCoreScore, mountainFootScore);
        var forestCoreThreshold = 0.43f + (openScore * 0.08f) + (Math.Clamp(1.02f - fields.RiverBase, 0f, 1f) * 0.03f);
        var mountainFootThreshold = 0.52f + (fields.MoistureMacro * 0.04f);
        var supportsForestCore = forestScore >= forestCoreThreshold
            && openScore <= 0.68f
            && fields.RiverBase >= 0.98f;
        var supportsMountainFoot = mountainFootScore >= mountainFootThreshold
            && fields.RidgeMacro >= 0.34f
            && fields.RiverBase >= 0.98f;
        var supportsRiverBank = riverBankScore >= 0.52f
            && fields.RiverBase <= 1.30f
            && !supportsForestCore
            && mountainFootScore < mountainFootThreshold + 0.08f;
        var forestTransitionScore = ComputeForestTransitionScore(fields, forestScore, openScore, forestCoreThreshold);
        var hasNearbyForestCore = HasNearbyForestCorePotential(worldSpaceKind, worldSeed, worldTile);
        var supportsForestEdge = hasNearbyForestCore
            && forestTransitionScore >= 0.50f
            && forestScore >= 0.34f
            && !supportsForestCore
            && !supportsMountainFoot
            && !supportsRiverBank
            && fields.RiverBase >= 0.94f
            && openScore <= 0.74f;

        var region =
            fields.RiverBase <= 0.90f ? TerrainRegionKind.RiverChannel :
            riverChannelScore >= 0.72f ? TerrainRegionKind.RiverChannel :
            mountainCoreScore >= 0.74f ? TerrainRegionKind.MountainCore :
            supportsForestCore ? TerrainRegionKind.ForestCore :
            supportsMountainFoot ? TerrainRegionKind.MountainFoot :
            supportsRiverBank ? TerrainRegionKind.RiverBank :
            supportsForestEdge ? TerrainRegionKind.ForestEdge :
            fields.MoistureMacro <= 0.34f && fields.RiverBase <= 1.75f && openScore >= 0.47f ? TerrainRegionKind.MudFlat :
            TerrainRegionKind.OpenLowland;

        var isForestCandidate = region is TerrainRegionKind.ForestCore or TerrainRegionKind.ForestEdge;
        var isOpenGroundCandidate = region is TerrainRegionKind.OpenLowland or TerrainRegionKind.MudFlat || openScore >= 0.58f;
        var isSteepBankCandidate =
            region == TerrainRegionKind.MountainCore && fields.RiverBase <= 1.18f
            || region == TerrainRegionKind.MountainFoot && fields.RiverBase <= 1.24f && bankSharpness >= 0.58f
            || region == TerrainRegionKind.RiverBank && bankSharpness >= 0.72f;

        return new TerrainRegionSample(region, fields.RiverBase, isSteepBankCandidate, isForestCandidate, isOpenGroundCandidate);
    }

    private static float ComputeRiverChannelScore(TerrainMacroFields fields)
    {
        var centerProximity = 1f - Math.Clamp((fields.RiverBase - 0.20f) / 0.95f, 0f, 1f);
        var widthSupport = Math.Clamp((fields.RiverWidth - 2.4f) / 1.6f, 0f, 1f);
        return (centerProximity * 0.76f) + (widthSupport * 0.24f);
    }

    private static float ComputeRiverBankScore(TerrainMacroFields fields)
    {
        var bankBand = 1f - Math.Clamp(Math.Abs(fields.RiverBase - 1.05f) / 0.85f, 0f, 1f);
        var moistureSupport = 0.42f + (fields.MoistureMacro * 0.32f);
        var mountainResistance = ((fields.ElevationMacro * 0.40f) + (fields.RidgeMacro * 0.60f)) * 0.34f;
        return Math.Clamp((bankBand * moistureSupport) - mountainResistance + 0.26f, 0f, 1f);
    }

    private static float ComputeMountainCoreScore(TerrainMacroFields fields)
    {
        return (fields.ElevationMacro * 0.68f)
            + (fields.RidgeMacro * 0.42f)
            - (fields.MoistureMacro * 0.10f);
    }

    private static float ComputeMountainFootScore(TerrainMacroFields fields)
    {
        return (fields.ElevationMacro * 0.54f)
            + (fields.RidgeMacro * 0.38f)
            + ((1f - Math.Abs(fields.RiverBase - 1.24f)) * 0.10f)
            + (fields.ForestMass * 0.06f)
            - (fields.MoistureMacro * 0.08f);
    }

    private static float ComputeForestScore(TerrainMacroFields fields)
    {
        return (fields.ForestMass * 0.58f)
            + (fields.MoistureMacro * 0.36f)
            - (fields.OpenFieldBias * 0.14f)
            - (Math.Clamp(0.88f - fields.RiverBase, 0f, 1f) * 0.06f);
    }

    private static float ComputeOpenScore(TerrainMacroFields fields)
    {
        return (fields.OpenFieldBias * 0.56f)
            + ((1f - fields.ForestMass) * 0.18f)
            + ((1f - fields.MoistureMacro) * 0.12f);
    }

    private static float ComputeForestTransitionScore(
        TerrainMacroFields fields,
        float forestScore,
        float openScore,
        float forestCoreThreshold)
    {
        var nearCoreBand = 1f - Math.Clamp(MathF.Abs(forestScore - (forestCoreThreshold - 0.06f)) / 0.16f, 0f, 1f);
        var openBand = 1f - Math.Clamp(MathF.Abs(openScore - 0.52f) / 0.22f, 0f, 1f);
        var riverPenalty = Math.Clamp(1.02f - fields.RiverBase, 0f, 1f);
        return (nearCoreBand * 0.52f)
            + (openBand * 0.28f)
            + (fields.ForestMass * 0.14f)
            + (fields.MoistureMacro * 0.10f)
            - (riverPenalty * 0.18f);
    }

    private static bool HasNearbyForestCorePotential(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile)
    {
        var qualifyingNeighbors = 0;
        foreach (var offset in ForestEdgeNeighborOffsets)
        {
            var neighborTile = new WorldTileCoord(worldTile.X + offset.X, worldTile.Y + offset.Y);
            var neighborFields = TerrainMacroFieldSampler.Sample(worldSpaceKind, worldSeed, neighborTile);
            if (neighborFields.RiverBase < 0.98f)
            {
                continue;
            }

            var neighborOpenScore = ComputeOpenScore(neighborFields);
            var neighborForestScore = ComputeForestScore(neighborFields);
            var neighborForestCoreThreshold = 0.43f
                + (neighborOpenScore * 0.08f)
                + (Math.Clamp(1.02f - neighborFields.RiverBase, 0f, 1f) * 0.03f);

            if (neighborForestScore >= neighborForestCoreThreshold && neighborOpenScore <= 0.68f)
            {
                qualifyingNeighbors++;
                if (qualifyingNeighbors >= 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float ComputeBankSharpness(
        TerrainMacroFields fields,
        float riverChannelScore,
        float riverBankScore,
        float mountainCoreScore,
        float mountainFootScore)
    {
        var widthTightness = 1f - Math.Clamp((fields.RiverWidth - 2.4f) / 2.8f, 0f, 1f);
        var mountainPressure = Math.Max(mountainCoreScore, mountainFootScore);
        return (fields.RidgeMacro * 0.42f)
            + (widthTightness * 0.22f)
            + (mountainPressure * 0.26f)
            + (Math.Clamp(riverBankScore - riverChannelScore + 0.5f, 0f, 1f) * 0.10f);
    }
}
