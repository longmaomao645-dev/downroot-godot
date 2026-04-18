using Downroot.Core.Ids;

namespace Downroot.Core.World;

public interface IWorldGenContext
{
    WorldSpaceKind WorldSpaceKind { get; }
    int WorldSeed { get; }
    ChunkCoord ChunkCoord { get; }
    int Width { get; }
    int Height { get; }
    WorldTileCoord GetWorldTileCoord(LocalTileCoord coord);
    int GetStableHash(WorldTileCoord coord, int salt);
    float GetStableUnitValue(WorldTileCoord coord, int salt);
    bool HasTerrain(ContentId contentId);
    ContentId? GetBaseTerrain(LocalTileCoord coord);
    ContentId? GetCoverTerrain(LocalTileCoord coord);
    ContentId? GetRaisedFeature(LocalTileCoord coord);
    void SetBaseTerrain(LocalTileCoord coord, ContentId terrainId);
    void SetCoverTerrain(LocalTileCoord coord, ContentId? terrainId);
    void SetRaisedFeature(LocalTileCoord coord, ContentId featureId);
    void ClearRaisedFeature(LocalTileCoord coord);
    bool HasRaisedFeature(LocalTileCoord coord);
    void SetRaisedFeatureVariantIndex(LocalTileCoord coord, byte index);
    SurfaceTileSemantic GetSurfaceSemantic(LocalTileCoord coord);
    void SetSurfaceSemantic(LocalTileCoord coord, SurfaceTileSemantic semantic);
    string GetSurfaceRegion(LocalTileCoord coord);
    bool HasSurfaceRegion(LocalTileCoord coord, string regionKey);
    void SetSurfaceRegion(LocalTileCoord coord, string regionKey);
    TerrainRegionKind SampleTerrainRegion(LocalTileCoord coord);
    bool IsSpawnOccupied(LocalTileCoord coord);
    void AddSpawn(LocalTileCoord coord, ContentId contentId, int pixelOffsetX = 0, int pixelOffsetY = 0);
}
