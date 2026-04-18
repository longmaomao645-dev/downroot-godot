using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class TerrainMacroFieldSampler
{
    public static TerrainMacroFields Sample(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return new TerrainMacroFields(0.5f, 0.25f, 0.5f, 0.25f, 0.5f, float.MaxValue, 1f, 0.5f);
        }

        var elevationMacro = StableWorldNoise.SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.008f, 0.021f, 1303, 1379);
        var ridgeMacro = StableWorldNoise.SampleRidgeNoise(worldSpaceKind, worldSeed, worldTile, 0.010f, 0.030f, 1427, 1499);
        var moistureMacro = StableWorldNoise.SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.009f, 0.027f, 1543, 1607);
        var forestMass = StableWorldNoise.SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.007f, 0.018f, 1663, 1721);
        var openFieldBias = StableWorldNoise.SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.011f, 0.033f, 1783, 1861);
        var temperatureBias = StableWorldNoise.SampleLayeredValueNoise(worldSpaceKind, worldSeed, worldTile, 0.006f, 0.015f, 1901, 1979);
        var riverBase = RiverLayoutSampler.SampleCenterDistanceNormalized(worldSpaceKind, worldSeed, worldTile);
        var riverWidth = RiverLayoutSampler.SampleLocalWidth(worldSpaceKind, worldSeed, worldTile);
        return new TerrainMacroFields(
            elevationMacro,
            ridgeMacro,
            moistureMacro,
            forestMass,
            openFieldBias,
            riverBase,
            riverWidth,
            temperatureBias);
    }
}
