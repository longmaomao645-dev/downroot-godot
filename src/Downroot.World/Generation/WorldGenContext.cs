using Downroot.Content.Registries;
using Downroot.Core.Ids;
using Downroot.Core.World;
using Downroot.World.Models;

namespace Downroot.World.Generation;

public sealed class WorldGenContext(
    WorldSpaceKind worldSpaceKind,
    int worldSeed,
    ChunkCoord chunkCoord,
    ChunkData surface,
    ContentRegistrySet registries,
    IList<WorldSpawnDef> spawns) : IWorldGenContext
{
    private readonly WorldSpaceKind _worldSpaceKind = worldSpaceKind;
    private readonly int _worldSeed = worldSeed;
    private readonly ChunkCoord _chunkCoord = chunkCoord;

    public WorldSpaceKind WorldSpaceKind => _worldSpaceKind;
    public int WorldSeed => _worldSeed;
    public ChunkCoord ChunkCoord => _chunkCoord;
    public int Width => surface.Width;
    public int Height => surface.Height;

    public bool HasTerrain(ContentId contentId) => registries.Terrains.TryGet(contentId, out _);

    public WorldTileCoord GetWorldTileCoord(LocalTileCoord coord)
    {
        return WorldTileCoord.FromChunkAndLocal(_chunkCoord, coord, surface.Width, surface.Height);
    }

    public int GetStableHash(WorldTileCoord coord, int salt)
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (int)_worldSpaceKind;
            hash = (hash * 31) + _worldSeed;
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

    public float GetStableUnitValue(WorldTileCoord coord, int salt)
    {
        return GetStableHash(coord, salt) / (float)int.MaxValue;
    }

    public ContentId? GetBaseTerrain(LocalTileCoord coord) => surface.GetBaseTerrainId(coord.X, coord.Y);

    public ContentId? GetTerrain(LocalTileCoord coord) => surface.GetTerrainId(coord.X, coord.Y);

    public ContentId? GetRaisedFeature(LocalTileCoord coord) => surface.GetRaisedFeatureId(coord.X, coord.Y);

    public void SetBaseTerrain(LocalTileCoord coord, ContentId terrainId) => surface.SetBaseTerrain(coord.X, coord.Y, terrainId);

    public void SetCoverTerrain(LocalTileCoord coord, ContentId? terrainId) => surface.SetCoverTerrain(coord.X, coord.Y, terrainId);

    public void SetRaisedFeature(LocalTileCoord coord, ContentId featureId) => surface.SetRaisedFeature(coord.X, coord.Y, featureId);

    public void ClearRaisedFeature(LocalTileCoord coord) => surface.ClearRaisedFeature(coord.X, coord.Y);

    public bool HasRaisedFeature(LocalTileCoord coord) => surface.HasRaisedFeature(coord.X, coord.Y);

    public void SetRaisedFeatureVariantIndex(LocalTileCoord coord, byte index) => surface.SetRaisedFeatureVariantIndex(coord.X, coord.Y, index);

    public string GetSurfaceRegion(LocalTileCoord coord) => surface.GetSurfaceRegion(coord.X, coord.Y);

    public bool HasSurfaceRegion(LocalTileCoord coord, string regionKey) => surface.HasSurfaceRegion(coord.X, coord.Y, regionKey);

    public void SetSurfaceRegion(LocalTileCoord coord, string regionKey) => surface.SetSurfaceRegion(coord.X, coord.Y, regionKey);

    public bool IsSpawnOccupied(LocalTileCoord coord)
    {
        var worldTile = GetWorldTileCoord(coord);
        return spawns.Any(spawn => spawn.Tile == worldTile);
    }

    public void AddSpawn(LocalTileCoord coord, ContentId contentId)
    {
        if (IsSpawnOccupied(coord))
        {
            return;
        }

        spawns.Add(new WorldSpawnDef(contentId, GetWorldTileCoord(coord)));
    }
}
