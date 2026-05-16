using Downroot.Content.Registries;
using Downroot.Core.Ids;
using Downroot.Core.Save;
using Downroot.Core.World;
using Downroot.World.Models;
using System.Numerics;

namespace Downroot.Gameplay.Runtime;

public sealed class LoadedWorldState
{
    private readonly ContentRegistrySet _content;
    private readonly Dictionary<ChunkCoord, ChunkRuntimeState> _loadedChunks = [];
    private readonly Dictionary<ChunkCoord, ChunkRuntimeArchive> _archivedChunks = [];
    private readonly Dictionary<EntityId, WorldEntityState> _loadedEntitiesById = [];
    private readonly Dictionary<WorldEntityState, WorldTileCoord> _blockerTiles = [];
    private readonly Dictionary<WorldTileCoord, HashSet<WorldEntityState>> _blockingEntitiesByTile = [];

    public LoadedWorldState(ContentRegistrySet content, WorldModel model, int loadRadius)
    {
        _content = content;
        Model = model;
        LoadRadius = loadRadius;
    }

    public WorldModel Model { get; }
    public WorldSpaceKind WorldSpaceKind => Model.WorldSpaceKind;
    public int WorldSeed => Model.WorldSeed;
    public int LoadRadius { get; }
    public Dictionary<ChunkCoord, ChunkRuntimeState> LoadedChunks => _loadedChunks;
    public HashSet<WorldTileCoord> DirtyRaisedFeatureTiles { get; } = [];
    public long ChunkVisualVersion { get; private set; }
    public long EntityStructureVersion { get; private set; }
    public long BlockerVersion { get; private set; }

    public bool ContainsChunk(ChunkCoord coord) => Model.ContainsChunk(coord);

    public IReadOnlyDictionary<ChunkCoord, ChunkRuntimeArchive> ArchivedChunks => _archivedChunks;

    public IEnumerable<WorldEntityState> EnumerateEntities() => EnumerateLoadedEntities();

    public IEnumerable<WorldEntityState> EnumerateLoadedEntities() => _loadedEntitiesById.Values;

    public IEnumerable<WorldEntityState> EnumerateLoadedEntities(WorldEntityKind kind) => _loadedEntitiesById.Values.Where(entity => entity.Kind == kind);

    public bool TryGetChunk(ChunkCoord coord, out ChunkRuntimeState chunk) => _loadedChunks.TryGetValue(coord, out chunk!);

    public bool TryGetEntity(EntityId entityId, out WorldEntityState entity) => _loadedEntitiesById.TryGetValue(entityId, out entity!);

    public bool ContainsPersistedEntity(EntityId entityId)
    {
        if (_loadedEntitiesById.ContainsKey(entityId))
        {
            return true;
        }

        return _archivedChunks.Values.Any(archive => archive.RuntimeEntities.Any(entity => entity.Id == entityId && !entity.Removed));
    }

    public IReadOnlyList<WorldEntityState> ClearAssignedPrimaryBeds()
    {
        var changedLoadedEntities = new List<WorldEntityState>();
        foreach (var entity in _loadedEntitiesById.Values)
        {
            if (entity.PlaceableState?.AssignedAsPrimaryBed != true)
            {
                continue;
            }

            entity.PlaceableState.AssignedAsPrimaryBed = false;
            changedLoadedEntities.Add(entity);
        }

        foreach (var archive in _archivedChunks.Values)
        {
            foreach (var entity in archive.RuntimeEntities)
            {
                if (entity.PlaceableState?.AssignedAsPrimaryBed != true)
                {
                    continue;
                }

                entity.PlaceableState.AssignedAsPrimaryBed = false;
            }
        }

        return changedLoadedEntities;
    }

    public bool TryAssignPrimaryBed(EntityId entityId, out WorldEntityState? loadedEntity)
    {
        if (_loadedEntitiesById.TryGetValue(entityId, out var activeEntity))
        {
            activeEntity.PlaceableState ??= new PlaceableRuntimeState();
            activeEntity.PlaceableState.AssignedAsPrimaryBed = true;
            loadedEntity = activeEntity;
            return true;
        }

        foreach (var archive in _archivedChunks.Values)
        {
            var archivedEntity = archive.RuntimeEntities.FirstOrDefault(entity => entity.Id == entityId && !entity.Removed);
            if (archivedEntity is null)
            {
                continue;
            }

            archivedEntity.PlaceableState ??= new PlaceableRuntimeState();
            archivedEntity.PlaceableState.AssignedAsPrimaryBed = true;
            loadedEntity = null;
            return true;
        }

        loadedEntity = null;
        return false;
    }

    public bool TryGetChunkForTile(WorldTileCoord tile, int chunkWidth, int chunkHeight, out ChunkRuntimeState chunk, out LocalTileCoord localCoord)
    {
        var chunkCoord = tile.ToChunkCoord(chunkWidth, chunkHeight);
        if (_loadedChunks.TryGetValue(chunkCoord, out chunk!))
        {
            localCoord = tile.ToLocalCoord(chunkWidth, chunkHeight);
            return true;
        }

        localCoord = default;
        return false;
    }

    public bool HasRaisedFeature(WorldTileCoord tile, int chunkWidth, int chunkHeight)
    {
        if (!TryGetChunkForTile(tile, chunkWidth, chunkHeight, out var chunk, out var localCoord))
        {
            return false;
        }

        return chunk.GeneratedChunk.Surface.GetRaisedFeatureId(localCoord.X, localCoord.Y) is not null
            && !chunk.RemovedRaisedFeatureTiles.Contains(tile);
    }

    public ContentId? GetRaisedFeatureId(WorldTileCoord tile, int chunkWidth, int chunkHeight)
    {
        if (!TryGetChunkForTile(tile, chunkWidth, chunkHeight, out var chunk, out var localCoord))
        {
            return null;
        }

        return chunk.RemovedRaisedFeatureTiles.Contains(tile)
            ? null
            : chunk.GeneratedChunk.Surface.GetRaisedFeatureId(localCoord.X, localCoord.Y);
    }

    public byte GetRaisedFeatureVariantIndex(WorldTileCoord tile, int chunkWidth, int chunkHeight)
    {
        if (!TryGetChunkForTile(tile, chunkWidth, chunkHeight, out var chunk, out var localCoord))
        {
            return 0;
        }

        var featureId = GetRaisedFeatureId(tile, chunkWidth, chunkHeight);
        if (featureId is null)
        {
            return 0;
        }

        if (!HasRemovedRaisedFeatureInNeighborhood(tile, chunkWidth, chunkHeight))
        {
            return chunk.GeneratedChunk.Surface.GetRaisedFeatureVariantIndex(localCoord.X, localCoord.Y);
        }

        return (byte)Downroot.World.Generation.RaisedFeatureAutotileResolver.Resolve(
            IsSameRaisedFeature(tile, new WorldTileCoord(tile.X, tile.Y - 1), featureId.Value, chunkWidth, chunkHeight),
            IsSameRaisedFeature(tile, new WorldTileCoord(tile.X + 1, tile.Y), featureId.Value, chunkWidth, chunkHeight),
            IsSameRaisedFeature(tile, new WorldTileCoord(tile.X, tile.Y + 1), featureId.Value, chunkWidth, chunkHeight),
            IsSameRaisedFeature(tile, new WorldTileCoord(tile.X - 1, tile.Y), featureId.Value, chunkWidth, chunkHeight),
            IsSameRaisedFeature(tile, new WorldTileCoord(tile.X + 1, tile.Y - 1), featureId.Value, chunkWidth, chunkHeight),
            IsSameRaisedFeature(tile, new WorldTileCoord(tile.X - 1, tile.Y - 1), featureId.Value, chunkWidth, chunkHeight),
            IsSameRaisedFeature(tile, new WorldTileCoord(tile.X + 1, tile.Y + 1), featureId.Value, chunkWidth, chunkHeight),
            IsSameRaisedFeature(tile, new WorldTileCoord(tile.X - 1, tile.Y + 1), featureId.Value, chunkWidth, chunkHeight));
    }

    public bool TryGetRaisedFeature(WorldTileCoord tile, int chunkWidth, int chunkHeight, out ContentId? featureId, out byte variantIndex)
    {
        featureId = GetRaisedFeatureId(tile, chunkWidth, chunkHeight);
        if (featureId is null)
        {
            variantIndex = 0;
            return false;
        }

        variantIndex = GetRaisedFeatureVariantIndex(tile, chunkWidth, chunkHeight);
        return true;
    }

    public void RemoveRaisedFeature(WorldTileCoord tile, int chunkWidth, int chunkHeight)
    {
        if (!TryGetChunkForTile(tile, chunkWidth, chunkHeight, out var chunk, out _))
        {
            return;
        }

        chunk.RemovedRaisedFeatureTiles.Add(tile);
        MarkRaisedFeatureDirty(EnumerateRaisedFeatureDirtyTiles(tile));
    }

    public void MarkRaisedFeatureDirty(IEnumerable<WorldTileCoord> tiles)
    {
        foreach (var tile in tiles)
        {
            DirtyRaisedFeatureTiles.Add(tile);
        }
    }

    public WorldTileCoord[] ConsumeDirtyRaisedFeatureTiles()
    {
        var dirty = DirtyRaisedFeatureTiles.ToArray();
        DirtyRaisedFeatureTiles.Clear();
        return dirty;
    }

    public void LoadChunk(GeneratedChunk generatedChunk, Func<GeneratedChunk, ChunkRuntimeState> initializeChunk)
    {
        if (!ContainsChunk(generatedChunk.Coord))
        {
            throw new InvalidOperationException($"Chunk {generatedChunk.Coord} is outside the bounds of world '{Model.StableId}'.");
        }

        if (generatedChunk.WorldSpaceKind != WorldSpaceKind)
        {
            throw new InvalidOperationException($"Chunk {generatedChunk.Coord} belongs to {generatedChunk.WorldSpaceKind}, but this container is {WorldSpaceKind}.");
        }

        if (_loadedChunks.ContainsKey(generatedChunk.Coord))
        {
            return;
        }

        var chunk = initializeChunk(generatedChunk);
        if (_archivedChunks.Remove(generatedChunk.Coord, out var archived))
        {
            chunk.ApplyArchive(archived);
        }

        _loadedChunks.Add(generatedChunk.Coord, chunk);
        IndexChunkEntities(chunk);
        IncrementChunkVisualVersion();
        IncrementEntityStructureVersion();
    }

    public void UnloadChunk(ChunkCoord coord)
    {
        if (_loadedChunks.Remove(coord, out var chunk))
        {
            RemoveChunkEntitiesFromIndexes(chunk);
            if (chunk.HasPersistentState())
            {
                _archivedChunks[coord] = chunk.CreateArchive();
            }
            else
            {
                _archivedChunks.Remove(coord);
            }

            IncrementChunkVisualVersion();
            IncrementEntityStructureVersion();
        }
    }

    public IReadOnlyList<SavedChunkRuntimeData> ExportPersistedChunks()
    {
        var persisted = new Dictionary<ChunkCoord, SavedChunkRuntimeData>();
        foreach (var archived in _archivedChunks)
        {
            persisted[archived.Key] = ChunkRuntimeState.ToSavedData(archived.Key, archived.Value);
        }

        foreach (var chunk in _loadedChunks.Values)
        {
            if (!chunk.HasPersistentState())
            {
                continue;
            }

            persisted[chunk.GeneratedChunk.Coord] = chunk.ToSavedData();
        }

        return persisted.Values
            .OrderBy(chunk => chunk.ChunkY)
            .ThenBy(chunk => chunk.ChunkX)
            .ToArray();
    }

    public void ImportPersistedChunks(IEnumerable<SavedChunkRuntimeData> chunks)
    {
        _archivedChunks.Clear();
        var updatedLoadedChunks = false;
        foreach (var chunk in chunks)
        {
            var coord = new ChunkCoord(chunk.ChunkX, chunk.ChunkY);
            var archive = ChunkRuntimeState.CreateArchive(chunk);
            if (_loadedChunks.TryGetValue(coord, out var loadedChunk))
            {
                RemoveChunkEntitiesFromIndexes(loadedChunk);
                loadedChunk.ApplyArchive(archive);
                IndexChunkEntities(loadedChunk);
                updatedLoadedChunks = true;
                continue;
            }

            _archivedChunks[coord] = archive;
        }

        if (updatedLoadedChunks)
        {
            IncrementChunkVisualVersion();
            IncrementEntityStructureVersion();
        }
    }

    public void AddRuntimeEntity(WorldEntityState entity)
    {
        if (!_loadedChunks.TryGetValue(entity.ChunkCoord, out var chunk))
        {
            throw new InvalidOperationException($"Chunk {entity.ChunkCoord} is not loaded in world space {WorldSpaceKind}.");
        }

        chunk.AddRuntimeEntity(entity);
        IndexEntity(entity);
        IncrementEntityStructureVersion();
    }

    public bool TryFindEntity(EntityId entityId, out WorldEntityState? entity, out ChunkRuntimeState? chunk)
    {
        if (_loadedEntitiesById.TryGetValue(entityId, out var loadedEntity)
            && _loadedChunks.TryGetValue(loadedEntity.ChunkCoord, out var loadedChunk))
        {
            entity = loadedEntity;
            chunk = loadedChunk;
            return true;
        }

        entity = null;
        chunk = null;
        return false;
    }

    public bool RemoveEntity(WorldEntityState entity)
    {
        if (!_loadedChunks.TryGetValue(entity.ChunkCoord, out var chunk))
        {
            return false;
        }

        if (!chunk.RemoveEntity(entity))
        {
            return false;
        }

        UnindexEntity(entity);
        IncrementEntityStructureVersion();
        return true;
    }

    public bool MoveRuntimeEntity(EntityId entityId, ChunkCoord targetChunk)
    {
        if (!_loadedEntitiesById.TryGetValue(entityId, out var entity)
            || !_loadedChunks.TryGetValue(entity.ChunkCoord, out var sourceChunk)
            || !_loadedChunks.TryGetValue(targetChunk, out var destinationChunk))
        {
            return false;
        }

        if (!sourceChunk.TakeRuntimeEntity(entityId, out _))
        {
            return false;
        }

        UnindexEntity(entity);
        entity.ChunkCoord = targetChunk;
        destinationChunk.AddRuntimeEntity(entity);
        IndexEntity(entity);
        IncrementEntityStructureVersion();
        return true;
    }

    public void NotifyEntityStateChanged(WorldEntityState entity)
    {
        UpdateBlockerIndex(entity);
    }

    public bool IsBlocked(Vector2 position, float blockingRadius, EntityId? ignoreEntityId)
    {
        var centerTile = new WorldTileCoord(
            (int)MathF.Floor(position.X / 32f),
            (int)MathF.Floor(position.Y / 32f));
        var tileRadius = (int)MathF.Ceiling(blockingRadius / 32f);
        var blockingRadiusSquared = blockingRadius * blockingRadius;

        for (var dy = -tileRadius; dy <= tileRadius; dy++)
        {
            for (var dx = -tileRadius; dx <= tileRadius; dx++)
            {
                var tile = new WorldTileCoord(centerTile.X + dx, centerTile.Y + dy);
                if (!_blockingEntitiesByTile.TryGetValue(tile, out var blockers))
                {
                    continue;
                }

                foreach (var blocker in blockers)
                {
                    if (blocker.Removed || blocker.Id == ignoreEntityId)
                    {
                        continue;
                    }

                    var blockerCenter = GetCollisionCenter(blocker);
                    if (Vector2.DistanceSquared(blockerCenter, position) < blockingRadiusSquared)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool IsSameRaisedFeature(WorldTileCoord originTile, WorldTileCoord neighborTile, ContentId featureId, int chunkWidth, int chunkHeight)
    {
        if (!TryGetChunkForTile(neighborTile, chunkWidth, chunkHeight, out var chunk, out var localCoord))
        {
            var originChunk = originTile.ToChunkCoord(chunkWidth, chunkHeight);
            var neighborChunk = neighborTile.ToChunkCoord(chunkWidth, chunkHeight);
            if (!ContainsChunk(neighborChunk) || !_archivedChunks.TryGetValue(neighborChunk, out var archived))
            {
                return false;
            }

            var removed = archived.RemovedRaisedFeatureTiles.Contains(neighborTile);
            if (removed)
            {
                return false;
            }

            return false;
        }

        if (chunk.RemovedRaisedFeatureTiles.Contains(neighborTile))
        {
            return false;
        }

        return chunk.GeneratedChunk.Surface.GetRaisedFeatureId(localCoord.X, localCoord.Y) == featureId;
    }

    private bool HasRemovedRaisedFeatureInNeighborhood(WorldTileCoord tile, int chunkWidth, int chunkHeight)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var sample = new WorldTileCoord(tile.X + dx, tile.Y + dy);
                var chunkCoord = sample.ToChunkCoord(chunkWidth, chunkHeight);
                if (_loadedChunks.TryGetValue(chunkCoord, out var chunk) && chunk.RemovedRaisedFeatureTiles.Contains(sample))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<WorldTileCoord> EnumerateRaisedFeatureDirtyTiles(WorldTileCoord origin)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                yield return new WorldTileCoord(origin.X + dx, origin.Y + dy);
            }
        }
    }

    private void IndexChunkEntities(ChunkRuntimeState chunk)
    {
        foreach (var entity in chunk.Entities)
        {
            IndexEntity(entity);
        }
    }

    private void RemoveChunkEntitiesFromIndexes(ChunkRuntimeState chunk)
    {
        foreach (var entity in chunk.Entities.ToArray())
        {
            UnindexEntity(entity);
        }
    }

    private void IndexEntity(WorldEntityState entity)
    {
        _loadedEntitiesById[entity.Id] = entity;
        UpdateBlockerIndex(entity);
    }

    private void UnindexEntity(WorldEntityState entity)
    {
        _loadedEntitiesById.Remove(entity.Id);
        RemoveBlockerIndex(entity);
    }

    private void UpdateBlockerIndex(WorldEntityState entity)
    {
        RemoveBlockerIndex(entity);
        if (!ShouldIndexAsBlocker(entity))
        {
            return;
        }

        var collisionCenter = GetCollisionCenter(entity);
        var tile = new WorldTileCoord(
            (int)MathF.Floor(collisionCenter.X / 32f),
            (int)MathF.Floor(collisionCenter.Y / 32f));
        if (!_blockingEntitiesByTile.TryGetValue(tile, out var blockers))
        {
            blockers = [];
            _blockingEntitiesByTile[tile] = blockers;
        }

        blockers.Add(entity);
        _blockerTiles[entity] = tile;
        BlockerVersion++;
    }

    private void RemoveBlockerIndex(WorldEntityState entity)
    {
        if (!_blockerTiles.Remove(entity, out var tile))
        {
            return;
        }

        if (_blockingEntitiesByTile.TryGetValue(tile, out var blockers))
        {
            blockers.Remove(entity);
            if (blockers.Count == 0)
            {
                _blockingEntitiesByTile.Remove(tile);
            }
        }

        BlockerVersion++;
    }

    private Vector2 GetCollisionCenter(WorldEntityState entity)
    {
        return entity.Kind switch
        {
            WorldEntityKind.ResourceNode => GetResourceCollisionCenter(entity),
            WorldEntityKind.Placeable => GetPlaceableCollisionCenter(entity),
            _ => entity.Position
        };
    }

    private Vector2 GetResourceCollisionCenter(WorldEntityState entity)
    {
        var def = _content.ResourceNodes.Get(entity.DefinitionId);
        return new Vector2(
            entity.Position.X + def.SpriteWidth * 0.5f,
            entity.Position.Y + def.SpriteHeight * 0.5f);
    }

    private Vector2 GetPlaceableCollisionCenter(WorldEntityState entity)
    {
        var def = _content.Placeables.Get(entity.DefinitionId);
        return new Vector2(
            entity.Position.X + def.SpriteWidth * 0.5f,
            entity.Position.Y + def.SpriteHeight * 0.5f);
    }

    private bool ShouldIndexAsBlocker(WorldEntityState entity)
    {
        if (entity.Removed)
        {
            return false;
        }

        return entity.Kind switch
        {
            WorldEntityKind.Placeable => GetPlaceableBlocksMovement(entity),
            WorldEntityKind.ResourceNode => _content.ResourceNodes.Get(entity.DefinitionId).BlocksMovement,
            _ => false
        };
    }

    private bool GetPlaceableBlocksMovement(WorldEntityState entity)
    {
        var placeable = _content.Placeables.Get(entity.DefinitionId);
        return entity.OpenState ? placeable.BlocksMovementWhenOpen : placeable.BlocksMovement;
    }

    private void IncrementChunkVisualVersion()
    {
        ChunkVisualVersion++;
    }

    private void IncrementEntityStructureVersion()
    {
        EntityStructureVersion++;
    }
}
