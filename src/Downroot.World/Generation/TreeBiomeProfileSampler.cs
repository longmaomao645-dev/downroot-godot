using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class TreeBiomeProfileSampler
{
    public static TreeBiomeProfile Sample(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TreeBiomeKind biome,
        int speciesCount)
    {
        if (speciesCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speciesCount));
        }

        if (speciesCount == 1)
        {
            return new TreeBiomeProfile(0, 0, 0);
        }

        var clusterCoord = new WorldTileCoord(FloorDiv(worldTile.X, 14), FloorDiv(worldTile.Y, 14));
        var saltBase = 6101 + ((int)biome * 101);
        var primaryIndex = StableWorldNoise.GetStableHash(worldSpaceKind, worldSeed, clusterCoord, saltBase) % speciesCount;
        var secondaryIndex = ResolveDistinctIndex(worldSpaceKind, worldSeed, clusterCoord, saltBase + 17, speciesCount, primaryIndex);
        var accentIndex = ResolveDistinctIndex(worldSpaceKind, worldSeed, clusterCoord, saltBase + 37, speciesCount, primaryIndex, secondaryIndex);
        return new TreeBiomeProfile(primaryIndex, secondaryIndex, accentIndex);
    }

    private static int ResolveDistinctIndex(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord clusterCoord,
        int salt,
        int speciesCount,
        params int[] forbidden)
    {
        var index = StableWorldNoise.GetStableHash(worldSpaceKind, worldSeed, clusterCoord, salt) % speciesCount;
        while (forbidden.Contains(index))
        {
            index = (index + 1) % speciesCount;
        }

        return index;
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder != 0 && value < 0 ? quotient - 1 : quotient;
    }
}

public readonly record struct TreeBiomeProfile(int PrimaryIndex, int SecondaryIndex, int AccentIndex);
