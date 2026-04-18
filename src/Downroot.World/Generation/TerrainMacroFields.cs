namespace Downroot.World.Generation;

public readonly record struct TerrainMacroFields(
    float ElevationMacro,
    float RidgeMacro,
    float MoistureMacro,
    float ForestMass,
    float OpenFieldBias,
    float RiverBase,
    float RiverWidth,
    float TemperatureBias);
