using System.Numerics;
using Downroot.Core.Definitions;
using Downroot.Core.Gameplay;
using Downroot.Core.Ids;
using Downroot.Core.World;
using Downroot.World.Generation;

namespace Downroot.Gameplay.Runtime;

public sealed class WorldRuntimeFacade(GameRuntime runtime)
{
    private readonly Dictionary<SurfaceSemanticCacheKey, SurfaceTileSemantic> _inferredSurfaceSemanticCache = [];

    private IEnumerable<LoadedWorldState> Worlds
    {
        get
        {
            yield return runtime.Overworld;
            if (runtime.DimShardPocket is not null)
            {
                yield return runtime.DimShardPocket;
            }
        }
    }

    public IReadOnlyList<WorldEntityState> GetActiveProjection()
    {
        runtime.WorldState.EnsureEntityProjectionCurrent();
        return runtime.WorldState.Entities;
    }

    public LoadedWorldState GetActiveWorld() => runtime.WorldState.GetActiveWorld();

    public LoadedWorldState GetWorld(WorldSpaceKind worldSpaceKind) => runtime.GetWorld(worldSpaceKind);

    public WorldGenerator GetGenerator(WorldSpaceKind worldSpaceKind) => runtime.GetWorldGenerator(worldSpaceKind);

    public ChunkCoord GetChunkCoord(Vector2 worldPosition) => runtime.GetChunkCoord(worldPosition);

    public WorldTileCoord GetWorldTile(Vector2 worldPosition) => runtime.GetWorldTile(worldPosition);

    public Vector2 GetWorldPosition(WorldTileCoord tileCoord) => runtime.GetWorldPosition(tileCoord);

    public bool TryGetChunk(WorldSpaceKind worldSpaceKind, ChunkCoord coord, out ChunkRuntimeState chunk)
    {
        return GetWorld(worldSpaceKind).TryGetChunk(coord, out chunk);
    }

    public bool TryGetChunkForTile(WorldSpaceKind worldSpaceKind, WorldTileCoord tile, out ChunkRuntimeState chunk, out LocalTileCoord localCoord)
    {
        return GetWorld(worldSpaceKind).TryGetChunkForTile(tile, runtime.ChunkWidth, runtime.ChunkHeight, out chunk, out localCoord);
    }

    public SurfaceTileSemantic SampleSurfaceSemantic(WorldSpaceKind worldSpaceKind, WorldTileCoord tile)
    {
        if (TryGetChunkForTile(worldSpaceKind, tile, out var chunk, out var localCoord))
        {
            return chunk.GeneratedChunk.Surface.GetSurfaceSemantic(localCoord.X, localCoord.Y);
        }

        var world = GetWorld(worldSpaceKind);
        var key = new SurfaceSemanticCacheKey(world.WorldSpaceKind, world.WorldSeed, tile);
        if (_inferredSurfaceSemanticCache.TryGetValue(key, out var semantic))
        {
            return semantic;
        }

        semantic = TerrainSemanticWorldSampler.SampleSemantic(world.WorldSpaceKind, world.WorldSeed, tile);
        _inferredSurfaceSemanticCache[key] = semantic;
        return semantic;
    }

    public ContentId? GetRaisedFeatureId(WorldSpaceKind worldSpaceKind, WorldTileCoord tile)
    {
        return GetWorld(worldSpaceKind).GetRaisedFeatureId(tile, runtime.ChunkWidth, runtime.ChunkHeight);
    }

    public byte GetRaisedFeatureVariantIndex(WorldSpaceKind worldSpaceKind, WorldTileCoord tile)
    {
        return GetWorld(worldSpaceKind).GetRaisedFeatureVariantIndex(tile, runtime.ChunkWidth, runtime.ChunkHeight);
    }

    public void RemoveRaisedFeature(WorldSpaceKind worldSpaceKind, WorldTileCoord tile)
    {
        GetWorld(worldSpaceKind).RemoveRaisedFeature(tile, runtime.ChunkWidth, runtime.ChunkHeight);
    }

    public void AddRuntimeEntity(WorldSpaceKind worldSpaceKind, WorldEntityState entity)
    {
        GetWorld(worldSpaceKind).AddRuntimeEntity(entity);
        if (worldSpaceKind == runtime.ActiveWorldSpaceKind)
        {
            runtime.WorldState.MarkEntityProjectionDirty();
            runtime.WorldState.NotifyLightingStructureChanged();
        }
    }

    public bool EnsureEntityProjectionCurrent()
    {
        return runtime.WorldState.EnsureEntityProjectionCurrent();
    }

    public bool RemoveDeleted()
    {
        return runtime.WorldState.RemoveDeleted();
    }

    public void MarkEntityProjectionDirty()
    {
        runtime.WorldState.MarkEntityProjectionDirty();
    }

    public bool TryGetActiveEntity(EntityId entityId, out WorldEntityState entity)
    {
        return GetActiveWorld().TryGetEntity(entityId, out entity);
    }

    public bool ContainsPersistedEntity(EntityId entityId)
    {
        return Worlds.Any(world => world.ContainsPersistedEntity(entityId));
    }

    public void NotifyEntityStateChanged(WorldEntityState entity)
    {
        GetWorld(entity.WorldSpaceKind).NotifyEntityStateChanged(entity);
        if (entity.WorldSpaceKind == runtime.ActiveWorldSpaceKind)
        {
            runtime.WorldState.NotifyEntityStateChanged();
            if (TryGetPlaceableDef(entity, out var placeableDef)
                && (placeableDef.LightEmitter is not null || placeableDef.LightOccluder is not null || placeableDef.SkylightMask is not null))
            {
                runtime.WorldState.NotifyLightingValueChanged(ResolveLightingDirtyBounds(entity, placeableDef));
            }
        }
    }

    public void NotifyLightingValueChanged(WorldEntityState entity)
    {
        if (entity.WorldSpaceKind == runtime.ActiveWorldSpaceKind)
        {
            runtime.WorldState.NotifyLightingValueChanged(ResolveLightingDirtyBounds(entity));
        }
    }

    public void NotifyLightingStructureChanged(WorldSpaceKind worldSpaceKind)
    {
        if (worldSpaceKind == runtime.ActiveWorldSpaceKind)
        {
            runtime.WorldState.NotifyLightingStructureChanged();
        }
    }

    public ContentId? GetPortalDefinitionId(WorldSpaceKind worldSpaceKind)
    {
        return runtime.Content.WorldGenPasses
            .FirstOrDefault(pass => pass.WorldSpaceKind == worldSpaceKind && pass.PassType == WorldGenPassTypes.PortalSite)
            ?.TargetId;
    }

    public PortalWorldLinkDef GetPortalLink(WorldSpaceKind worldSpaceKind, ChunkCoord portalChunk)
    {
        return runtime.Content.PortalWorldLinks.First(link =>
            (link.SourceWorldSpaceKind == worldSpaceKind && link.SourcePortalChunk == portalChunk)
            || (link.TargetWorldSpaceKind == worldSpaceKind && link.TargetPortalChunk == portalChunk));
    }

    public bool IsPortalEntity(WorldEntityState entity)
    {
        if (entity.Kind != WorldEntityKind.Placeable || !entity.IsNatural)
        {
            return false;
        }

        var portalDefId = GetPortalDefinitionId(entity.WorldSpaceKind);
        return portalDefId is not null
            && entity.DefinitionId == portalDefId.Value
            && runtime.Content.PortalWorldLinks.Any(link =>
                (link.SourceWorldSpaceKind == entity.WorldSpaceKind && link.SourcePortalChunk == entity.ChunkCoord)
                || (link.TargetWorldSpaceKind == entity.WorldSpaceKind && link.TargetPortalChunk == entity.ChunkCoord));
    }

    public bool TryGetPlaceableDef(WorldEntityState entity, out PlaceableDef placeableDef)
    {
        if (entity.Kind == WorldEntityKind.Placeable && runtime.Content.Placeables.TryGet(entity.DefinitionId, out var def))
        {
            placeableDef = def!;
            return true;
        }

        placeableDef = null!;
        return false;
    }

    public CraftingStationKind? GetEffectiveCraftingStationKind(WorldEntityState entity)
    {
        if (!TryGetPlaceableDef(entity, out var placeableDef))
        {
            return null;
        }

        return entity.PlaceableState?.UpgradedCraftingStationKind ?? placeableDef.CraftingStationKind;
    }

    private LightingFieldBounds ResolveLightingDirtyBounds(WorldEntityState entity)
    {
        return TryGetPlaceableDef(entity, out var placeableDef)
            ? ResolveLightingDirtyBounds(entity, placeableDef)
            : LightingFieldBounds.FromTile(GetWorldTile(entity.Position));
    }

    private LightingFieldBounds ResolveLightingDirtyBounds(WorldEntityState entity, PlaceableDef placeableDef)
    {
        var tile = GetWorldTile(entity.Position);
        var radius = 0;
        if (placeableDef.LightEmitter is { } emitter)
        {
            radius = Math.Max(radius, (int)MathF.Ceiling(emitter.RadiusTiles));
        }

        if (placeableDef.LightOccluder is { BlocksLight: true })
        {
            radius = Math.Max(radius, 1);
        }

        if (placeableDef.SkylightMask is { BlocksSkylight: true })
        {
            radius = Math.Max(radius, 1);
        }

        return LightingFieldBounds.FromTile(tile).Expand(radius);
    }

    public void ClearPrimaryBedAssignments()
    {
        foreach (var world in Worlds)
        {
            foreach (var entity in world.ClearAssignedPrimaryBeds())
            {
                NotifyEntityStateChanged(entity);
            }
        }
    }

    public bool TryAssignPrimaryBed(EntityId entityId)
    {
        foreach (var world in Worlds)
        {
            if (!world.TryAssignPrimaryBed(entityId, out var loadedEntity))
            {
                continue;
            }

            if (loadedEntity is not null)
            {
                NotifyEntityStateChanged(loadedEntity);
            }

            return true;
        }

        return false;
    }

    private readonly record struct SurfaceSemanticCacheKey(
        WorldSpaceKind WorldSpaceKind,
        int WorldSeed,
        WorldTileCoord Tile);
}
