using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.World.Generation.Passes;

public sealed class RiverPass(ContentId riverTerrainId) : IWorldGenPass
{
    public string Name => WorldGenPassTypes.River;

    public void Execute(IWorldGenContext context)
    {
        if (!context.HasTerrain(riverTerrainId))
        {
            throw new InvalidOperationException($"Missing terrain '{riverTerrainId}' for river pass.");
        }

        for (var y = 0; y < context.Height; y++)
        {
            for (var x = 0; x < context.Width; x++)
            {
                var local = new LocalTileCoord(x, y);
                var world = context.GetWorldTileCoord(local);
                if (!IsRiverTile(context, world))
                {
                    continue;
                }

                context.SetCoverTerrain(local, riverTerrainId);
            }
        }
    }

    public static bool IsRiverTile(IWorldGenContext context, WorldTileCoord world)
        => IsRiverTile(context.WorldSpaceKind, context.WorldSeed, world);

    public static bool IsRiverTile(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord world)
        => SampleNearestNormalizedDistance(worldSpaceKind, worldSeed, world) <= 1f;

    public static float SampleNearestNormalizedDistance(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord world)
    {
        var primaryCenter = (MathF.Sin((world.X * 0.085f) + (worldSeed * 0.013f)) * 4.25f)
            + (GetStableUnitValue(worldSpaceKind, worldSeed, new WorldTileCoord(world.X / 6, 0), 991) * 6f)
            - 3f;
        var secondaryCenter = (MathF.Cos((world.X * 0.048f) - (worldSeed * 0.009f)) * 6.5f)
            + 22f;
        var primaryWidth = 1.8f + (GetStableUnitValue(worldSpaceKind, worldSeed, new WorldTileCoord(world.X / 4, 1), 1441) * 0.9f);
        var secondaryWidth = 1.4f + (GetStableUnitValue(worldSpaceKind, worldSeed, new WorldTileCoord(world.X / 5, 2), 2111) * 0.7f);
        var primaryDistance = MathF.Abs(world.Y - primaryCenter) / primaryWidth;
        var secondaryDistance = MathF.Abs(world.Y - secondaryCenter) / secondaryWidth;
        return MathF.Min(primaryDistance, secondaryDistance);
    }

    private static float GetStableUnitValue(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord coord, int salt)
    {
        return GetStableHash(worldSpaceKind, worldSeed, coord, salt) / (float)int.MaxValue;
    }

    private static int GetStableHash(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord coord, int salt)
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (int)worldSpaceKind;
            hash = (hash * 31) + worldSeed;
            hash = (hash * 31) + coord.X;
            hash = (hash * 31) + coord.Y;
            hash = (hash * 31) + salt;
            hash ^= hash >> 16;
            hash *= unchecked((int)0x7feb352d);
            hash ^= hash >> 15;
            hash *= unchecked((int)0x846ca68b);
            hash ^= hash >> 16;
            return hash & int.MaxValue;
        }
    }
}
