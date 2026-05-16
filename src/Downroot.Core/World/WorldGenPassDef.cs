using Downroot.Core.Ids;

namespace Downroot.Core.World;

public sealed record WorldGenPassDef(
    ContentId Id,
    string PassType,
    ContentId TargetId,
    WorldSpaceKind? WorldSpaceKind = null,
    int Count = 0,
    int StartColumn = 0,
    int StartRow = 0,
    int Width = 0,
    int Height = 0,
    string? PrimarySurfaceRegion = null,
    int MinSpacing = 0,
    float SpawnChance = 0f,
    ChunkCoord? RequiredChunkCoord = null,
    int MinChunkSpacing = 0);
