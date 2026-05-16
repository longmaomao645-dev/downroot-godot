using Downroot.Core.World;
using Downroot.Core.Diagnostics;
using Downroot.Gameplay.Bootstrap;

namespace Downroot.Gameplay.Runtime.Systems;

public sealed class WorldStreamingSystem(GameRuntime runtime, WorldRuntimeFacade worldFacade)
{
    public bool UpdateLoadedChunks()
    {
        using var scope = RuntimeProfiler.Measure("WorldStreaming.UpdateLoadedChunks");
        var world = worldFacade.GetActiveWorld();
        var centerChunk = worldFacade.GetChunkCoord(runtime.Player.Position);
        return UpdateLoadedChunksForWorldCore(world, centerChunk);
    }

    public bool UpdateLoadedChunksForWorld(LoadedWorldState world, WorldTileCoord aroundTile)
    {
        using var scope = RuntimeProfiler.Measure("WorldStreaming.UpdateLoadedChunksForWorld");
        return UpdateLoadedChunksForWorldCore(world, aroundTile.ToChunkCoord(runtime.ChunkWidth, runtime.ChunkHeight));
    }

    public bool ReassignRuntimeEntities()
    {
        using var scope = RuntimeProfiler.Measure("WorldStreaming.ReassignRuntimeEntities");
        var world = worldFacade.GetActiveWorld();
        var moved = false;
        foreach (var sourceChunk in world.LoadedChunks.Values.ToArray())
        {
            foreach (var entity in sourceChunk.RuntimeEntities.Values.Where(entity => !entity.Removed).ToArray())
            {
                var targetChunk = worldFacade.GetChunkCoord(entity.Position);
                if (targetChunk == entity.ChunkCoord || !world.ContainsChunk(targetChunk) || !world.LoadedChunks.ContainsKey(targetChunk))
                {
                    continue;
                }

                if (!world.MoveRuntimeEntity(entity.Id, targetChunk))
                {
                    continue;
                }

                moved = true;
            }
        }

        if (moved)
        {
            worldFacade.MarkEntityProjectionDirty();
        }

        return moved;
    }

    private bool UpdateLoadedChunksForWorldCore(LoadedWorldState world, ChunkCoord centerChunk)
    {
        using var scope = RuntimeProfiler.Measure("WorldStreaming.UpdateLoadedChunksCore");
        var desired = new HashSet<ChunkCoord>();
        var changed = false;

        for (var y = centerChunk.Y - world.LoadRadius; y <= centerChunk.Y + world.LoadRadius; y++)
        {
            for (var x = centerChunk.X - world.LoadRadius; x <= centerChunk.X + world.LoadRadius; x++)
            {
                var coord = new ChunkCoord(x, y);
                if (!world.ContainsChunk(coord))
                {
                    continue;
                }

                desired.Add(coord);
                if (world.LoadedChunks.ContainsKey(coord))
                {
                    continue;
                }

                var generated = worldFacade.GetGenerator(world)
                    .GenerateChunk(world.WorldSpaceKind, world.WorldSeed, coord, runtime.ChunkWidth, runtime.ChunkHeight);
                world.LoadChunk(generated, chunk => GameBootstrapper.CreateChunkRuntimeState(runtime, chunk));
                changed = true;
            }
        }

        foreach (var staleChunk in world.LoadedChunks.Keys.Where(coord => !desired.Contains(coord)).ToArray())
        {
            world.UnloadChunk(staleChunk);
            changed = true;
        }

        if (changed && world.WorldSpaceKind == runtime.ActiveWorldSpaceKind)
        {
            worldFacade.MarkEntityProjectionDirty();
            worldFacade.NotifyLightingStructureChanged(world.WorldSpaceKind);
        }

        return changed;
    }
}
