using Downroot.Core.World;
using Downroot.World.Generation.Passes;

namespace Downroot.World.Generation;

public static class TerrainSemanticWorldSampler
{
    private static readonly WorldTileCoord[] CardinalNeighbors =
    [
        new WorldTileCoord(0, -1),
        new WorldTileCoord(1, 0),
        new WorldTileCoord(0, 1),
        new WorldTileCoord(-1, 0)
    ];

    private static readonly WorldTileCoord[] AllNeighbors =
    [
        new WorldTileCoord(-1, -1),
        new WorldTileCoord(0, -1),
        new WorldTileCoord(1, -1),
        new WorldTileCoord(-1, 0),
        new WorldTileCoord(1, 0),
        new WorldTileCoord(-1, 1),
        new WorldTileCoord(0, 1),
        new WorldTileCoord(1, 1)
    ];

    public static SurfaceTileSemantic SampleSemantic(IWorldGenContext context, WorldTileCoord worldTile)
        => SampleSemantic(context.WorldSpaceKind, context.WorldSeed, worldTile);

    public static SurfaceTileSemantic SampleSemantic(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return SurfaceSemanticDefaults.CreateForRegion(worldSpaceKind, SurfaceRegionSampler.SampleSurfaceRegion(worldSpaceKind, worldSeed, worldTile));
        }

        var fields = SampleFields(worldSpaceKind, worldSeed, worldTile);
        var visual = LegalizeVisual(worldSpaceKind, worldSeed, worldTile, fields);
        var height = ResolveHeight(worldSpaceKind, worldSeed, worldTile, visual, fields.Roughness);
        var shoreProfile = ResolveShoreProfile(worldSpaceKind, worldSeed, worldTile, visual);
        return CreateSemantic(visual, height, shoreProfile);
    }

    public static TerrainVisualKind SampleVisual(IWorldGenContext context, WorldTileCoord worldTile)
        => SampleVisual(context.WorldSpaceKind, context.WorldSeed, worldTile);

    public static TerrainVisualKind SampleVisual(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
        => SampleSemantic(worldSpaceKind, worldSeed, worldTile).Visual;

    public static string SampleSurfaceRegion(IWorldGenContext context, WorldTileCoord worldTile)
        => SampleSurfaceRegion(context.WorldSpaceKind, context.WorldSeed, worldTile);

    public static string SampleSurfaceRegion(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        var visual = SampleVisual(worldSpaceKind, worldSeed, worldTile);
        return visual switch
        {
            TerrainVisualKind.Grass => SurfaceRegions.GrassField,
            TerrainVisualKind.DeepWater => SurfaceRegions.River,
            TerrainVisualKind.ShallowWater => SurfaceRegions.River,
            TerrainVisualKind.Mountain => SurfaceRegions.RockyOutcrop,
            _ => SurfaceRegions.DirtField
        };
    }

    private static TerrainVisualKind LegalizeVisual(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile, TerrainFields fields)
    {
        var visual = SampleRawVisual(worldSpaceKind, worldSeed, worldTile, fields);
        var neighbors = AllNeighbors
            .Select(offset => SampleRawVisual(worldSpaceKind, worldSeed, new WorldTileCoord(worldTile.X + offset.X, worldTile.Y + offset.Y)))
            .ToArray();

        if (visual == TerrainVisualKind.DeepWater && neighbors.Contains(TerrainVisualKind.Beach))
        {
            return TerrainVisualKind.ShallowWater;
        }

        if (visual == TerrainVisualKind.Beach)
        {
            if (neighbors.Contains(TerrainVisualKind.DeepWater))
            {
                return TerrainVisualKind.ShallowWater;
            }

            if (neighbors.Contains(TerrainVisualKind.Mountain))
            {
                return TerrainVisualKind.Dirt;
            }
        }

        return visual;
    }

    private static TerrainVisualKind SampleRawVisual(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        return SampleRawVisual(worldSpaceKind, worldSeed, worldTile, SampleFields(worldSpaceKind, worldSeed, worldTile));
    }

    private static TerrainVisualKind SampleRawVisual(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile, TerrainFields fields)
    {
        if (fields.RiverDistance <= 0.42f)
        {
            return TerrainVisualKind.DeepWater;
        }

        if (fields.RiverDistance <= 1f)
        {
            return TerrainVisualKind.ShallowWater;
        }

        var mountainScore = (fields.Elevation * 0.7f) + (fields.Roughness * 0.3f);
        if (mountainScore >= 0.72f && fields.Moisture <= 0.58f && fields.RiverDistance > 1.35f)
        {
            return TerrainVisualKind.Mountain;
        }

        var shorelineBand = fields.RiverDistance <= 1.75f;
        var beachScore = ((1f - fields.Moisture) * 0.45f) + ((1f - fields.GrassPotential) * 0.35f) + (fields.CoastalExposure * 0.20f);
        if (shorelineBand && beachScore >= 0.50f)
        {
            return TerrainVisualKind.Beach;
        }

        return fields.GrassPotential >= 0.56f
            ? TerrainVisualKind.Grass
            : TerrainVisualKind.Dirt;
    }

    private static HeightKind ResolveHeight(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile, TerrainVisualKind visual, float roughness)
    {
        if (visual == TerrainVisualKind.Mountain)
        {
            return HasNeighborMatching(worldSpaceKind, worldSeed, worldTile, neighbor =>
                TerrainVisualAdjacencyRules.GetAdjacencyRule(visual, neighbor) == AdjacencyRule.AllowedButSteep)
                ? HeightKind.Cliff
                : HeightKind.Raised;
        }

        if (visual is TerrainVisualKind.Dirt or TerrainVisualKind.Grass
            && HasNeighborMatching(worldSpaceKind, worldSeed, worldTile, neighbor => neighbor == TerrainVisualKind.Mountain)
            && roughness >= 0.82f)
        {
            return HeightKind.Raised;
        }

        return HeightKind.Low;
    }

    private static ShoreProfileKind ResolveShoreProfile(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile, TerrainVisualKind visual)
    {
        if (visual == TerrainVisualKind.DeepWater || visual == TerrainVisualKind.ShallowWater)
        {
            return ShoreProfileKind.None;
        }

        if (HasNeighborMatching(worldSpaceKind, worldSeed, worldTile, neighbor =>
            TerrainVisualAdjacencyRules.GetAdjacencyRule(visual, neighbor) == AdjacencyRule.AllowedButSteep))
        {
            return ShoreProfileKind.Steep;
        }

        if (HasNeighborMatching(worldSpaceKind, worldSeed, worldTile, neighbor =>
            neighbor is TerrainVisualKind.DeepWater or TerrainVisualKind.ShallowWater or TerrainVisualKind.Beach))
        {
            return ShoreProfileKind.Gentle;
        }

        return ShoreProfileKind.None;
    }

    private static bool HasNeighborMatching(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile, Func<TerrainVisualKind, bool> predicate)
    {
        foreach (var offset in CardinalNeighbors)
        {
            var neighbor = SampleRawVisual(worldSpaceKind, worldSeed, new WorldTileCoord(worldTile.X + offset.X, worldTile.Y + offset.Y));
            if (predicate(neighbor))
            {
                return true;
            }
        }

        return false;
    }

    private static SurfaceTileSemantic CreateSemantic(TerrainVisualKind visual, HeightKind height, ShoreProfileKind shoreProfile)
    {
        return visual switch
        {
            TerrainVisualKind.DeepWater => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Water, height, shoreProfile, false, false, false),
            TerrainVisualKind.ShallowWater => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Wadeable, height, shoreProfile, false, false, false),
            TerrainVisualKind.Beach => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Ground, height, shoreProfile, false, true, false),
            TerrainVisualKind.Dirt => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Ground, height, shoreProfile, true, true, false),
            TerrainVisualKind.Grass => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Ground, height, shoreProfile, true, true, true),
            TerrainVisualKind.Mountain => new SurfaceTileSemantic(visual, SurfaceGameplayKind.SolidRock, height, shoreProfile, false, true, false),
            _ => new SurfaceTileSemantic(TerrainVisualKind.Dirt, SurfaceGameplayKind.Ground, HeightKind.Low, ShoreProfileKind.None, true, true, false)
        };
    }

    private static TerrainFields SampleFields(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        var elevation = SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.019f, 0.071f, 1303, 1379);
        var roughness = SampleRidgeNoise(worldSpaceKind, worldSeed, worldTile, 0.053f, 0.132f, 1427, 1499);
        var moisture = SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.028f, 0.087f, 1543, 1607);
        var grassPotential = (GrassRegionPass.SampleLayeredNoise(worldSpaceKind, worldSeed, worldTile) * 0.65f) + (moisture * 0.35f);
        var coastalExposure = SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.024f, 0.103f, 1663, 1721);
        var riverDistance = RiverPass.SampleNearestNormalizedDistance(worldSpaceKind, worldSeed, worldTile);
        return new TerrainFields(elevation, roughness, moisture, grassPotential, coastalExposure, riverDistance);
    }

    private static float SampleLayeredValueNoise(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        float lowFrequency,
        float highFrequency,
        int lowSalt,
        int highSalt)
    {
        var low = SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * lowFrequency, worldTile.Y * lowFrequency, lowSalt);
        var high = SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * highFrequency, worldTile.Y * highFrequency, highSalt);
        return (low * 0.7f) + (high * 0.3f);
    }

    private static float SampleRidgeNoise(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        float lowFrequency,
        float highFrequency,
        int lowSalt,
        int highSalt)
    {
        var low = 1f - MathF.Abs((SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * lowFrequency, worldTile.Y * lowFrequency, lowSalt) * 2f) - 1f);
        var high = 1f - MathF.Abs((SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * highFrequency, worldTile.Y * highFrequency, highSalt) * 2f) - 1f);
        return (low * 0.6f) + (high * 0.4f);
    }

    private static float SampleValueNoise(WorldSpaceKind worldSpaceKind, int worldSeed, float x, float y, int salt)
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

    private readonly record struct TerrainFields(
        float Elevation,
        float Roughness,
        float Moisture,
        float GrassPotential,
        float CoastalExposure,
        float RiverDistance);
}
