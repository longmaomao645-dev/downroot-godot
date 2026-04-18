using Downroot.Core.World;

namespace Downroot.World.Generation;

internal static class StableWorldNoise
{
    public static float SampleLayeredValueNoise(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        float lowFrequency,
        float highFrequency,
        int lowSalt,
        int highSalt,
        float lowWeight = 0.7f)
    {
        var highWeight = 1f - lowWeight;
        var low = SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * lowFrequency, worldTile.Y * lowFrequency, lowSalt);
        var high = SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * highFrequency, worldTile.Y * highFrequency, highSalt);
        return (low * lowWeight) + (high * highWeight);
    }

    public static float SampleRidgeNoise(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        float lowFrequency,
        float highFrequency,
        int lowSalt,
        int highSalt,
        float lowWeight = 0.6f)
    {
        var highWeight = 1f - lowWeight;
        var low = 1f - MathF.Abs((SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * lowFrequency, worldTile.Y * lowFrequency, lowSalt) * 2f) - 1f);
        var high = 1f - MathF.Abs((SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * highFrequency, worldTile.Y * highFrequency, highSalt) * 2f) - 1f);
        return (low * lowWeight) + (high * highWeight);
    }

    public static float SampleValueNoise(WorldSpaceKind worldSpaceKind, int worldSeed, float x, float y, int salt)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;

        var tx = SmoothStep(x - x0);
        var ty = SmoothStep(y - y0);

        var v00 = HashToUnitFloat(worldSpaceKind, worldSeed, x0, y0, salt);
        var v10 = HashToUnitFloat(worldSpaceKind, worldSeed, x1, y0, salt);
        var v01 = HashToUnitFloat(worldSpaceKind, worldSeed, x0, y1, salt);
        var v11 = HashToUnitFloat(worldSpaceKind, worldSeed, x1, y1, salt);

        var top = Lerp(v00, v10, tx);
        var bottom = Lerp(v01, v11, tx);
        return Lerp(top, bottom, ty);
    }

    public static float HashToUnitFloat(WorldSpaceKind worldSpaceKind, int worldSeed, int x, int y, int salt)
        => GetStableHash(worldSpaceKind, worldSeed, new WorldTileCoord(x, y), salt) / (float)int.MaxValue;

    public static int GetStableHash(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord coord, int salt)
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

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
