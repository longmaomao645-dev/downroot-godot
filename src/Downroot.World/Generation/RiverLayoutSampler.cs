using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class RiverLayoutSampler
{
    public static float SampleCenterDistanceNormalized(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return float.MaxValue;
        }

        var flow = worldTile.X * 0.038f;
        var lateralOffset =
            (StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, flow, 0.13f, 2101) - 0.5f) * 26f
            + (StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, flow * 0.45f, 0.61f, 2113) - 0.5f) * 16f
            + MathF.Sin((flow * 0.85f) + (worldSeed * 0.0019f)) * 4f;
        var centerY = (lateralOffset * 0.95f) + 8f;
        var width = SampleLocalWidth(worldSpaceKind, worldSeed, worldTile);
        return MathF.Abs(worldTile.Y - centerY) / MathF.Max(1.2f, width);
    }

    public static float SampleLocalWidth(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return 1f;
        }

        var flow = worldTile.X * 0.026f;
        var broad = StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, flow, 1.17f, 2203);
        var detail = StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, flow * 1.6f, 3.41f, 2219);
        return 2.2f + (broad * 1.5f) + (detail * 0.65f);
    }
}
