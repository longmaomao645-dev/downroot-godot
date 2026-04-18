using Downroot.Core.World;

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

        var fields = SampleMacroFields(worldSpaceKind, worldSeed, worldTile);
        var region = SampleRegion(worldSpaceKind, worldSeed, worldTile, fields);
        var visual = SampleLegalizedVisual(worldSpaceKind, worldSeed, worldTile, fields, region);
        var neighborVisuals = SampleLegalizedNeighborVisuals(worldSpaceKind, worldSeed, worldTile);
        var height = ResolveHeight(visual, neighborVisuals, fields, region);
        var shoreProfile = ResolveShoreProfile(visual, neighborVisuals, region);
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
            TerrainVisualKind.Beach => SurfaceRegions.BeachShore,
            TerrainVisualKind.DeepWater => SurfaceRegions.River,
            TerrainVisualKind.ShallowWater => SurfaceRegions.River,
            TerrainVisualKind.Mountain => SurfaceRegions.RockyOutcrop,
            _ => SurfaceRegions.DirtField
        };
    }

    private static TerrainMacroFields SampleMacroFields(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
        => TerrainMacroFieldSampler.Sample(worldSpaceKind, worldSeed, worldTile);

    private static TerrainRegionSample SampleRegion(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TerrainMacroFields fields)
        => TerrainRegionClassifier.Sample(worldSpaceKind, worldSeed, worldTile, fields);

    private static TerrainVisualKind SampleLegalizedVisual(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        var fields = SampleMacroFields(worldSpaceKind, worldSeed, worldTile);
        var region = SampleRegion(worldSpaceKind, worldSeed, worldTile, fields);
        return SampleLegalizedVisual(worldSpaceKind, worldSeed, worldTile, fields, region);
    }

    private static TerrainVisualKind SampleLegalizedVisual(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TerrainMacroFields fields,
        TerrainRegionSample region)
    {
        var visual = SampleRawVisualFromRegion(worldSpaceKind, worldSeed, worldTile, fields, region);
        var neighbors = AllNeighbors
            .Select(offset =>
            {
                var neighborTile = new WorldTileCoord(worldTile.X + offset.X, worldTile.Y + offset.Y);
                var neighborFields = SampleMacroFields(worldSpaceKind, worldSeed, neighborTile);
                var neighborRegion = SampleRegion(worldSpaceKind, worldSeed, neighborTile, neighborFields);
                return SampleRawVisualFromRegion(worldSpaceKind, worldSeed, neighborTile, neighborFields, neighborRegion);
            })
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

            if (neighbors.Contains(TerrainVisualKind.Mountain) && region.Region != TerrainRegionKind.RiverBank)
            {
                return TerrainVisualKind.Dirt;
            }
        }

        if (visual == TerrainVisualKind.Dirt
            && region.Region == TerrainRegionKind.RiverBank
            && !region.IsSteepBankCandidate
            && neighbors.Count(neighbor => neighbor is TerrainVisualKind.DeepWater or TerrainVisualKind.ShallowWater) >= 2)
        {
            return TerrainVisualKind.Beach;
        }

        return visual;
    }

    private static TerrainVisualKind[] SampleLegalizedNeighborVisuals(WorldSpaceKind worldSpaceKind, int worldSeed, WorldTileCoord worldTile)
    {
        var neighbors = new TerrainVisualKind[CardinalNeighbors.Length];
        for (var index = 0; index < CardinalNeighbors.Length; index++)
        {
            var offset = CardinalNeighbors[index];
            neighbors[index] = SampleLegalizedVisual(worldSpaceKind, worldSeed, new WorldTileCoord(worldTile.X + offset.X, worldTile.Y + offset.Y));
        }

        return neighbors;
    }

    private static TerrainVisualKind SampleRawVisualFromRegion(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TerrainMacroFields fields,
        TerrainRegionSample region)
    {
        return region.Region switch
        {
            TerrainRegionKind.RiverChannel => region.RiverDistanceNormalized <= 0.45f
                ? TerrainVisualKind.DeepWater
                : TerrainVisualKind.ShallowWater,
            TerrainRegionKind.RiverBank => SampleRiverBankVisual(worldSpaceKind, worldSeed, worldTile, fields, region),
            TerrainRegionKind.MountainCore => TerrainVisualKind.Mountain,
            TerrainRegionKind.MountainFoot => fields.MoistureMacro >= 0.58f && fields.ForestMass >= 0.48f
                ? TerrainVisualKind.Grass
                : TerrainVisualKind.Dirt,
            TerrainRegionKind.ForestCore => TerrainVisualKind.Grass,
            TerrainRegionKind.ForestEdge => SampleForestEdgeVisual(worldSpaceKind, worldSeed, worldTile, fields),
            TerrainRegionKind.MudFlat => TerrainVisualKind.Dirt,
            _ => SampleOpenLowlandVisual(worldSpaceKind, worldSeed, worldTile, fields)
        };
    }

    private static TerrainVisualKind SampleRiverBankVisual(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TerrainMacroFields fields,
        TerrainRegionSample region)
    {
        if (region.IsSteepBankCandidate)
        {
            return TerrainVisualKind.Dirt;
        }

        if (fields.RiverBase <= 1.05f && fields.MoistureMacro <= 0.52f)
        {
            return TerrainVisualKind.Dirt;
        }

        if (fields.MoistureMacro >= 0.62f && fields.ForestMass >= 0.57f)
        {
            return TerrainVisualKind.Grass;
        }

        return TerrainVisualKind.Beach;
    }

    private static TerrainVisualKind SampleForestEdgeVisual(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TerrainMacroFields fields)
    {
        var blend = StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * 0.071f, worldTile.Y * 0.071f, 4103);
        return blend + (fields.MoistureMacro * 0.25f) >= 0.62f
            ? TerrainVisualKind.Grass
            : TerrainVisualKind.Dirt;
    }

    private static TerrainVisualKind SampleOpenLowlandVisual(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile,
        TerrainMacroFields fields)
    {
        var blend = (fields.MoistureMacro * 0.55f) + ((1f - fields.OpenFieldBias) * 0.25f) + (fields.ForestMass * 0.20f);
        var localVariation = StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, worldTile.X * 0.082f, worldTile.Y * 0.082f, 4201);
        return blend + (localVariation * 0.12f) >= 0.56f
            ? TerrainVisualKind.Grass
            : TerrainVisualKind.Dirt;
    }

    private static HeightKind ResolveHeight(
        TerrainVisualKind visual,
        IReadOnlyList<TerrainVisualKind> neighborVisuals,
        TerrainMacroFields fields,
        TerrainRegionSample region)
    {
        if (visual == TerrainVisualKind.Mountain)
        {
            if (neighborVisuals.Any(neighbor => neighbor is TerrainVisualKind.DeepWater or TerrainVisualKind.ShallowWater))
            {
                return HeightKind.Cliff;
            }

            if (neighborVisuals.Any(neighbor => neighbor != TerrainVisualKind.Mountain))
            {
                return fields.RidgeMacro >= 0.68f
                    ? HeightKind.Cliff
                    : HeightKind.Raised;
            }

            return HeightKind.Raised;
        }

        if (visual is TerrainVisualKind.Dirt or TerrainVisualKind.Grass
            && region.Region == TerrainRegionKind.MountainFoot
            && (neighborVisuals.Any(neighbor => neighbor == TerrainVisualKind.Mountain)
                || (region.IsSteepBankCandidate && neighborVisuals.Any(neighbor => neighbor is TerrainVisualKind.DeepWater or TerrainVisualKind.ShallowWater))))
        {
            return region.IsSteepBankCandidate
                ? HeightKind.Cliff
                : HeightKind.Raised;
        }

        return HeightKind.Low;
    }

    private static ShoreProfileKind ResolveShoreProfile(
        TerrainVisualKind visual,
        IReadOnlyList<TerrainVisualKind> neighborVisuals,
        TerrainRegionSample region)
    {
        if (visual is TerrainVisualKind.DeepWater or TerrainVisualKind.ShallowWater)
        {
            return ShoreProfileKind.None;
        }

        var hasWaterNeighbor = neighborVisuals.Any(neighbor => neighbor is TerrainVisualKind.DeepWater or TerrainVisualKind.ShallowWater);
        if (!hasWaterNeighbor)
        {
            return ShoreProfileKind.None;
        }

        return region.IsSteepBankCandidate
            ? ShoreProfileKind.Steep
            : ShoreProfileKind.Gentle;
    }

    private static SurfaceTileSemantic CreateSemantic(TerrainVisualKind visual, HeightKind height, ShoreProfileKind shoreProfile)
    {
        var supportsTrees = visual == TerrainVisualKind.Grass
            || (visual == TerrainVisualKind.Dirt && height == HeightKind.Raised)
            || (visual == TerrainVisualKind.Dirt && shoreProfile == ShoreProfileKind.None);

        return visual switch
        {
            TerrainVisualKind.DeepWater => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Water, height, shoreProfile, false, false, false),
            TerrainVisualKind.ShallowWater => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Wadeable, height, shoreProfile, false, false, false),
            TerrainVisualKind.Beach => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Ground, height, shoreProfile, false, true, false),
            TerrainVisualKind.Dirt => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Ground, height, shoreProfile, true, true, supportsTrees),
            TerrainVisualKind.Grass => new SurfaceTileSemantic(visual, SurfaceGameplayKind.Ground, height, shoreProfile, true, true, supportsTrees),
            TerrainVisualKind.Mountain => new SurfaceTileSemantic(visual, SurfaceGameplayKind.SolidRock, height, shoreProfile, false, true, false),
            _ => new SurfaceTileSemantic(TerrainVisualKind.Dirt, SurfaceGameplayKind.Ground, HeightKind.Low, ShoreProfileKind.None, true, true, false)
        };
    }
}
