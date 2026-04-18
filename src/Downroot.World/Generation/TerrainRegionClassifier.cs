using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class TerrainRegionClassifier
{
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

        var riverDistance = fields.RiverBase;
        var mountainCoreScore = (fields.ElevationMacro * 0.65f) + (fields.RidgeMacro * 0.35f);
        var mountainFootScore = (fields.ElevationMacro * 0.58f) + (fields.RidgeMacro * 0.42f);
        var forestScore = (fields.ForestMass * 0.6f) + (fields.MoistureMacro * 0.4f) - (fields.OpenFieldBias * 0.25f);
        var openScore = (fields.OpenFieldBias * 0.7f) + ((1f - fields.ForestMass) * 0.3f);
        var steepBankCandidate =
            (mountainCoreScore >= 0.70f && riverDistance <= 1.25f)
            || (mountainFootScore >= 0.62f && fields.RidgeMacro >= 0.64f && riverDistance <= 1.35f);

        var region =
            riverDistance <= 0.82f ? TerrainRegionKind.RiverChannel :
            riverDistance <= 1.28f ? TerrainRegionKind.RiverBank :
            mountainCoreScore >= 0.72f && riverDistance > 1.05f ? TerrainRegionKind.MountainCore :
            mountainFootScore >= 0.57f && riverDistance > 1.05f ? TerrainRegionKind.MountainFoot :
            forestScore >= 0.60f && riverDistance > 1.25f ? TerrainRegionKind.ForestCore :
            forestScore >= 0.49f && openScore < 0.68f && riverDistance > 1.2f ? TerrainRegionKind.ForestEdge :
            fields.MoistureMacro <= 0.36f && riverDistance <= 1.95f && openScore >= 0.45f ? TerrainRegionKind.MudFlat :
            TerrainRegionKind.OpenLowland;

        var isForestCandidate = region is TerrainRegionKind.ForestCore or TerrainRegionKind.ForestEdge;
        var isOpenGroundCandidate = region is TerrainRegionKind.OpenLowland or TerrainRegionKind.MudFlat || openScore >= 0.58f;
        return new TerrainRegionSample(region, riverDistance, steepBankCandidate, isForestCandidate, isOpenGroundCandidate);
    }
}
