using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class ForestDensitySampler
{
    public static float Sample(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TerrainRegionKind region)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return 0f;
        }

        var broad = StableWorldNoise.SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.010f, 0.028f, 5101, 5113);
        var detail = StableWorldNoise.SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.023f, 0.052f, 5209, 5227, 0.64f);
        var density = (broad * 0.68f) + (detail * 0.32f);
        return region switch
        {
            TerrainRegionKind.ForestCore => Math.Clamp(0.55f + (density * 0.45f), 0f, 1f),
            TerrainRegionKind.ForestEdge => Math.Clamp(0.28f + (density * 0.42f), 0f, 0.82f),
            TerrainRegionKind.MountainFoot => Math.Clamp(0.34f + (density * 0.40f), 0f, 0.84f),
            TerrainRegionKind.OpenLowland => Math.Clamp(density * 0.22f, 0f, 0.30f),
            _ => 0f
        };
    }
}
