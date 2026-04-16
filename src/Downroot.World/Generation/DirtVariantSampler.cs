using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class DirtVariantSampler
{
    private const float LowFrequency = 0.045f;
    private const float MidFrequency = 0.14f;
    private const float NearRiverBias = -0.24f;
    private const float NearGrassEdgeBias = -0.12f;
    private const float OpenDirtInteriorBias = 0.10f;

    public static int SampleVariantIndex(IWorldGenContext context, WorldTileCoord worldTile)
    {
        return SampleVariantIndex(context.WorldSpaceKind, context.WorldSeed, worldTile);
    }

    public static int SampleVariantIndex(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        var stoniness = SampleStoniness(worldSpaceKind, worldSeed, worldTile);
        var variant = (int)MathF.Round(stoniness * 7f);
        return Math.Clamp(variant, 0, 7);
    }

    private static float SampleStoniness(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        var low = SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * LowFrequency, worldTile.Y * LowFrequency, 3203);
        var mid = SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * MidFrequency, worldTile.Y * MidFrequency, 3259);
        var jitter = HashToUnitFloat(worldSpaceKind, worldSeed, worldTile.X, worldTile.Y, 3323);
        var stoniness = (low * 0.65f) + (mid * 0.25f) + (jitter * 0.10f);
        return ApplySemanticBias(worldSpaceKind, worldSeed, worldTile, stoniness);
    }

    private static float ApplySemanticBias(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile, float stoniness)
    {
        var semantic = TerrainSemanticWorldSampler.SampleSemantic(worldSpaceKind, worldSeed, worldTile);
        if (HasNearbyVisual(worldSpaceKind, worldSeed, worldTile, 2, TerrainVisualKind.ShallowWater, TerrainVisualKind.DeepWater))
        {
            stoniness += NearRiverBias;
        }

        if (semantic.Visual == TerrainVisualKind.Dirt && HasNearbyVisual(worldSpaceKind, worldSeed, worldTile, 1, TerrainVisualKind.Grass))
        {
            stoniness += NearGrassEdgeBias;
        }

        if (IsOpenDirtInterior(worldSpaceKind, worldSeed, worldTile, semantic.Visual))
        {
            stoniness += OpenDirtInteriorBias;
        }

        return Math.Clamp(stoniness, 0f, 1f);
    }

    private static bool HasNearbyVisual(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile, int radius, params TerrainVisualKind[] visuals)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                {
                    continue;
                }

                var sampled = TerrainSemanticWorldSampler.SampleVisual(worldSpaceKind, worldSeed, new WorldTileCoord(worldTile.X + dx, worldTile.Y + dy));
                if (visuals.Contains(sampled))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsOpenDirtInterior(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile, TerrainVisualKind visual)
    {
        if (visual != TerrainVisualKind.Dirt)
        {
            return false;
        }

        return !HasNearbyVisual(worldSpaceKind, worldSeed, worldTile, 1, TerrainVisualKind.Grass)
            && !HasNearbyVisual(worldSpaceKind, worldSeed, worldTile, 2, TerrainVisualKind.ShallowWater, TerrainVisualKind.DeepWater);
    }

    private static float SampleValueNoise(WorldSpaceKind worldSpaceKind, int worldSeed, float x, float y, int seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;

        var tx = SmoothStep(x - x0);
        var ty = SmoothStep(y - y0);

        var v00 = HashToUnitFloat(worldSpaceKind, worldSeed, x0, y0, seed);
        var v10 = HashToUnitFloat(worldSpaceKind, worldSeed, x1, y0, seed);
        var v01 = HashToUnitFloat(worldSpaceKind, worldSeed, x0, y1, seed);
        var v11 = HashToUnitFloat(worldSpaceKind, worldSeed, x1, y1, seed);

        var top = Lerp(v00, v10, tx);
        var bottom = Lerp(v01, v11, tx);
        return Lerp(top, bottom, ty);
    }

    private static float HashToUnitFloat(WorldSpaceKind worldSpaceKind, int worldSeed, int x, int y, int salt)
    {
        return GetStableHash(worldSpaceKind, worldSeed, new WorldTileCoord(x, y), salt) / (float)int.MaxValue;
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

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
