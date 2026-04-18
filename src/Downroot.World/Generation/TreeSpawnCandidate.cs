using Downroot.Core.World;

namespace Downroot.World.Generation;

public readonly record struct TreeSpawnCandidate(
    LocalTileCoord Coord,
    TerrainRegionKind Region,
    float Density,
    float Score,
    TreeBiomeKind Biome,
    float VariantRoll);
