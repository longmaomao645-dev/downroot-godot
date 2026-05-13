using System.Numerics;
using Downroot.Core.Definitions;
using Downroot.Core.Ids;
using Downroot.Core.World;
using Downroot.World.Generation;

namespace Downroot.Gameplay.Runtime;

public sealed class WorldRuntimeFacade(GameRuntime runtime)
{
    private IEnumerable<LoadedWorldState> Worlds
    {
        get
        {
            yield return runtime.Overworld;
            foreach (var pocketWorld in runtime.PocketWorlds.Values)
            {
                yield return pocketWorld;
            }
        }
    }

    public IReadOnlyList<WorldEntityState> GetActiveProjection()
    {
        runtime.WorldState.EnsureEntityProjectionCurrent();
        return runtime.WorldState.Entities;
    }

    public LoadedWorldState GetActiveWorld() => runtime.WorldState.GetActiveWorld();

    public LoadedWorldState GetWorld(WorldSpaceKind worldSpaceKind)
    {
        if (worldSpaceKind == WorldSpaceKind.Overworld)
        {
            return runtime.Overworld;
        }

        // DimShardPocket: resolve via the active pocket world ID.
        var activeId = runtime.WorldState.ActivePocketWorldId;
        if (activeId is not null
            && runtime.WorldState.PocketWorlds.TryGetValue(activeId, out var pocketWorld))
        {
            return pocketWorld;
        }

        throw new InvalidOperationException($"No active pocket world available for kind '{worldSpaceKind}'.");
    }

    public WorldGenerator GetGenerator(WorldSpaceKind worldSpaceKind)
    {
        return runtime.GetWorldGenerator(worldSpaceKind);
    }

    public WorldGenerator GetGenerator(LoadedWorldState world)
    {
        if (world.WorldSpaceKind == WorldSpaceKind.Overworld)
        {
            return runtime.OverworldGenerator;
        }

        return runtime.GetPocketGenerator(world.Model.StableId);
    }

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
        if (worldSpaceKind == WorldSpaceKind.Overworld)
        {
            return new PortalWorldLinkDef(
                WorldSpaceKind.Overworld,
                WorldSpaceKind.DimShardPocket,
                portalChunk,
                new ChunkCoord(0, 0),
                $"link:overworld-{portalChunk.X},{portalChunk.Y}");
        }

        // DimShardPocket portal — return to overworld at the source portal chunk.
        // The source portal chunk is stored on the pocket world's model.
        var pocketWorld = runtime.WorldState.PocketWorlds.Values
            .FirstOrDefault(w => w.Model.SourcePortalChunk is not null
                && w.LoadedChunks.ContainsKey(portalChunk));
        var returnChunk = pocketWorld?.Model.SourcePortalChunk ?? new ChunkCoord(0, 0);

        return new PortalWorldLinkDef(
            WorldSpaceKind.DimShardPocket,
            WorldSpaceKind.Overworld,
            portalChunk,
            returnChunk,
            $"link:dimshard-{portalChunk.X},{portalChunk.Y}");
    }

    public bool IsPortalEntity(WorldEntityState entity)
    {
        if (entity.Kind != WorldEntityKind.Placeable || !entity.IsNatural)
        {
            return false;
        }

        var portalDefId = GetPortalDefinitionId(entity.WorldSpaceKind);
        return portalDefId is not null
            && entity.DefinitionId == portalDefId.Value;
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
}
