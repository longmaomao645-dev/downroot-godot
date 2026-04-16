using Downroot.Core.World;
using Downroot.World.Generation.Passes;

namespace Downroot.World.Generation;

public static class SurfaceRegionSampler
{
    public static string SampleSurfaceRegion(IWorldGenContext context, WorldTileCoord worldTile)
        => SampleSurfaceRegion(context.WorldSpaceKind, context.WorldSeed, worldTile);

    public static string SampleSurfaceRegion(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        return worldSpaceKind switch
        {
            WorldSpaceKind.Overworld => SampleOverworldSurfaceRegion(worldSeed, worldTile),
            WorldSpaceKind.DimShardPocket => SurfaceRegions.DimShardField,
            _ => SurfaceRegions.DirtField
        };
    }

    public static string SampleOverworldSurfaceRegion(IWorldGenContext context, WorldTileCoord worldTile)
        => SampleOverworldSurfaceRegion(context.WorldSeed, worldTile);

    public static string SampleOverworldSurfaceRegion(int worldSeed, WorldTileCoord worldTile)
    {
        if (RiverPass.IsRiverTile(WorldSpaceKind.Overworld, worldSeed, worldTile))
        {
            return SurfaceRegions.River;
        }

        var grass = GrassRegionPass.SampleLayeredNoise(WorldSpaceKind.Overworld, worldSeed, worldTile);
        if (grass >= GrassRegionPass.GrassThreshold)
        {
            return SurfaceRegions.GrassField;
        }

        return SurfaceRegions.DirtField;
    }
}
