using Downroot.Core.World;

namespace Downroot.World.Generation;

public readonly record struct TerrainRegionSample(
    TerrainRegionKind Region,
    float RiverDistanceNormalized,
    bool IsSteepBankCandidate,
    bool IsForestCandidate,
    bool IsOpenGroundCandidate);
