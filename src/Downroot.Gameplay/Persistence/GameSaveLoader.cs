using System.Numerics;
using Downroot.Core.Save;
using Downroot.Core.World;
using Downroot.Gameplay.Bootstrap;
using Downroot.Gameplay.Runtime;
using Downroot.Gameplay.Runtime.Systems;
using Downroot.Core.Ids;
using Downroot.World.Generation;
using Downroot.World.Models;

namespace Downroot.Gameplay.Persistence;

public sealed class GameSaveLoader
{
    private readonly InventoryPersistenceAdapter _inventoryAdapter = new();
    private readonly WorldRuntimePersistenceAdapter _worldAdapter = new();

    public void Load(GameRuntime runtime, SaveGameData save)
    {
        var worldFacade = new WorldRuntimeFacade(runtime);
        runtime.Player.Position = new Vector2(save.Player.PositionX, save.Player.PositionY);
        runtime.Player.Facing = new Vector2(save.Player.FacingX, save.Player.FacingY);
        runtime.Player.SelectedHotbarIndex = save.Player.SelectedHotbarIndex;
        runtime.PrimaryBedEntityId = !string.IsNullOrWhiteSpace(save.Player.PrimaryBedEntityGuid) && Guid.TryParse(save.Player.PrimaryBedEntityGuid, out var bedGuid)
            ? new EntityId(bedGuid)
            : null;
        runtime.Player.Survival.SetHealth(save.Player.Health);
        runtime.Player.Survival.SetHunger(save.Player.Hunger);
        _inventoryAdapter.Import(runtime.Player.Inventory, save.Player.InventorySlots);

        runtime.WorldState.TimeOfDaySeconds = save.TimeOfDaySeconds;
        runtime.WorldState.TotalElapsedSeconds = save.TotalElapsedSeconds;
        runtime.WorldState.ActiveFurnaceTask = null;
        runtime.WorldState.WorkspaceMode = CraftWorkspaceMode.Hidden;
        runtime.WorldState.ActiveStationEntityId = null;
        runtime.WorldState.ActiveStationKind = null;
        runtime.WorldState.ActiveStorageEntityId = null;
        runtime.WorldState.CurrentInteraction = null;
        runtime.WorldState.ActiveDestroyProgress = null;

        foreach (var savedWorld in save.Worlds)
        {
            var worldSpaceKind = Enum.Parse<WorldSpaceKind>(savedWorld.WorldSpaceKind, ignoreCase: true);

            if (worldSpaceKind == WorldSpaceKind.DimShardPocket)
            {
                // Recreate the pocket world from its stable world ID.
                var worldId = savedWorld.StableWorldId;
                var portalChunk = ParsePortalChunkFromWorldId(worldId);
                var pocketSeed = GameBootstrapper.CreatePocketWorldSeed(save.WorldSeed, portalChunk);

                var model = new WorldModel(
                    worldId,
                    WorldSpaceKind.DimShardPocket,
                    pocketSeed,
                    new ChunkCoord(-1, -1),
                    new ChunkCoord(1, 1),
                    portalChunk);

                var pocketWorld = new LoadedWorldState(runtime.Content, model, runtime.BootstrapConfig.OverworldLoadRadius);
                _worldAdapter.Import(pocketWorld, savedWorld);

                runtime.PocketWorlds[worldId] = pocketWorld;
                runtime.PocketGenerators[worldId] = GameBootstrapper.CreateGenerator(runtime.Content, WorldSpaceKind.DimShardPocket);
                runtime.WorldState.PocketWorlds[worldId] = pocketWorld;

                // Set the active pocket world ID if the saved state was in a pocket world.
                if (worldSpaceKind == runtime.ActiveWorldSpaceKind)
                {
                    runtime.WorldState.ActivePocketWorldId = worldId;
                }
            }
            else
            {
                _worldAdapter.Import(runtime.GetWorld(worldSpaceKind), savedWorld);
            }
        }

        runtime.ActiveWorldSpaceKind = Enum.Parse<WorldSpaceKind>(save.ActiveWorldSpaceKind, ignoreCase: true);
        var streamer = new WorldStreamingSystem(runtime, worldFacade);
        streamer.UpdateLoadedChunks();
        worldFacade.ClearPrimaryBedAssignments();
        if (runtime.PrimaryBedEntityId is { } primaryBedId)
        {
            if (!worldFacade.ContainsPersistedEntity(primaryBedId))
            {
                runtime.PrimaryBedEntityId = null;
            }
            else
            {
                worldFacade.TryAssignPrimaryBed(primaryBedId);
            }
        }

        runtime.WorldState.RefreshEntityProjection();
    }

    /// <summary>
    /// Parses a ChunkCoord from a stable world ID of the format "dimshard:{seed}:{x},{y}".
    /// </summary>
    private static ChunkCoord ParsePortalChunkFromWorldId(string worldId)
    {
        var parts = worldId.Split(':');
        if (parts.Length < 3)
        {
            return new ChunkCoord(0, 0);
        }

        var coords = parts[^1].Split(',');
        if (coords.Length != 2
            || !int.TryParse(coords[0], out var x)
            || !int.TryParse(coords[1], out var y))
        {
            return new ChunkCoord(0, 0);
        }

        return new ChunkCoord(x, y);
    }
}