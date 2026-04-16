using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.World.Generation.Passes;

public sealed class GrassRegionPass(ContentId terrainId) : IWorldGenPass
{
    private const float BaseFrequency = 0.16f;
    private const float DetailFrequency = 0.34f;
    public const float GrassThreshold = 0.57f;

    public string Name => WorldGenPassTypes.SurfaceRegion;

    public void Execute(IWorldGenContext context)
    {
        if (!context.HasTerrain(terrainId))
        {
            throw new InvalidOperationException($"Missing terrain '{terrainId}' for grass region pass.");
        }

        for (var y = 0; y < context.Height; y++)
        {
            for (var x = 0; x < context.Width; x++)
            {
                var coord = new LocalTileCoord(x, y);
                var worldCoord = context.GetWorldTileCoord(coord);
                var value = SampleLayeredNoise(context, worldCoord);
                if (value >= GrassThreshold)
                {
                    context.SetCoverTerrain(coord, terrainId);
                }
                else
                {
                    context.SetCoverTerrain(coord, null);
                }
            }
        }
    }

    public static float SampleLayeredNoise(IWorldGenContext context, WorldTileCoord coord)
    {
        return SampleLayeredNoise(context.WorldSpaceKind, context.WorldSeed, coord);
    }

    public static float SampleLayeredNoise(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord coord)
    {
        var baseNoise = SampleValueNoise(worldSpaceKind, worldSeed, coord.X * BaseFrequency, coord.Y * BaseFrequency, 17);
        var detailNoise = SampleValueNoise(worldSpaceKind, worldSeed, coord.X * DetailFrequency, coord.Y * DetailFrequency, 79);
        return baseNoise * 0.75f + detailNoise * 0.25f;
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

    private static float HashToUnitFloat(WorldSpaceKind worldSpaceKind, int worldSeed, int x, int y, int seed)
    {
        return GetStableHash(worldSpaceKind, worldSeed, new WorldTileCoord(x, y), seed) / (float)int.MaxValue;
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
