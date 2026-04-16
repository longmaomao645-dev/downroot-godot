using Downroot.Core.World;
using Downroot.Core.Diagnostics;
using Downroot.Gameplay.Bootstrap;
using System.Threading.Tasks;

namespace Downroot.Gameplay.Runtime.Systems;

public sealed class WorldStreamingSystem(GameRuntime runtime, WorldRuntimeFacade worldFacade)
{
    private const int ChunkLoadCommitBudgetPerTick = 1;

    private readonly Dictionary<PendingChunkLoadKey, PendingChunkLoad> _pendingChunkLoads = [];
    private readonly Dictionary<WorldSpaceKind, HashSet<ChunkCoord>> _desiredChunksByWorld = [];
    private readonly Dictionary<WorldSpaceKind, ChunkCoord> _desiredCentersByWorld = [];
    private long _nextChunkLoadRequestId;

    public bool UpdateLoadedChunks()
    {
        using var scope = RuntimeProfiler.Measure("WorldStreaming.UpdateLoadedChunks");
        var world = worldFacade.GetActiveWorld();
        var centerChunk = worldFacade.GetChunkCoord(runtime.Player.Position);
        return UpdateLoadedChunksForWorldCore(world, centerChunk, useAsyncGeneration: true);
    }

    public bool UpdateLoadedChunksForWorld(LoadedWorldState world, WorldTileCoord aroundTile)
    {
        using var scope = RuntimeProfiler.Measure("WorldStreaming.UpdateLoadedChunksForWorld");
        return UpdateLoadedChunksForWorldCore(world, aroundTile.ToChunkCoord(runtime.ChunkWidth, runtime.ChunkHeight), useAsyncGeneration: false);
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

    private bool UpdateLoadedChunksForWorldCore(LoadedWorldState world, ChunkCoord centerChunk, bool useAsyncGeneration)
    {
        using var scope = RuntimeProfiler.Measure("WorldStreaming.UpdateLoadedChunksCore");
        var desired = new HashSet<ChunkCoord>();

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
            }
        }

        _desiredChunksByWorld[world.WorldSpaceKind] = desired;
        _desiredCentersByWorld[world.WorldSpaceKind] = centerChunk;
        var changed = ProcessCompletedChunkLoads();

        foreach (var coord in desired.OrderBy(coord => GetChunkPriority(coord, centerChunk)).ThenBy(coord => coord.Y).ThenBy(coord => coord.X))
        {
            if (world.LoadedChunks.ContainsKey(coord))
            {
                continue;
            }

            if (useAsyncGeneration)
            {
                QueueChunkLoad(world, coord);
                continue;
            }

            var generated = worldFacade.GetGenerator(world.WorldSpaceKind)
                .GenerateChunk(world.WorldSpaceKind, world.WorldSeed, coord, runtime.ChunkWidth, runtime.ChunkHeight);
            world.LoadChunk(generated, chunk => GameBootstrapper.CreateChunkRuntimeState(runtime, chunk));
            changed = true;
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

    private void QueueChunkLoad(LoadedWorldState world, ChunkCoord coord)
    {
        var key = new PendingChunkLoadKey(world.WorldSpaceKind, coord);
        if (_pendingChunkLoads.ContainsKey(key))
        {
            return;
        }

        var requestId = ++_nextChunkLoadRequestId;
        RuntimeProfiler.Increment("WorldStreaming.ChunkLoadRequested");
        _pendingChunkLoads[key] = new PendingChunkLoad(
            requestId,
            Task.Run(() =>
            {
                using var scope = RuntimeProfiler.Measure("WorldStreaming.GenerateChunkAsync");
                var generated = worldFacade.GetGenerator(world.WorldSpaceKind)
                    .GenerateChunk(world.WorldSpaceKind, world.WorldSeed, coord, runtime.ChunkWidth, runtime.ChunkHeight);
                var runtimeChunk = GameBootstrapper.CreateChunkRuntimeState(runtime, generated);
                return new ChunkLoadResult(world.WorldSpaceKind, coord, generated, runtimeChunk, requestId);
            }));
    }

    private bool ProcessCompletedChunkLoads()
    {
        var changed = false;
        var committed = 0;
        var orderedCompletedLoads = _pendingChunkLoads
            .Where(pair => pair.Value.Task.IsCompleted)
            .OrderBy(pair => GetPendingChunkPriority(pair.Key))
            .ThenBy(pair => pair.Key.Coord.Y)
            .ThenBy(pair => pair.Key.Coord.X)
            .ToArray();

        foreach (var pair in orderedCompletedLoads)
        {
            if (committed >= ChunkLoadCommitBudgetPerTick)
            {
                break;
            }

            _pendingChunkLoads.Remove(pair.Key);
            var result = pair.Value.Task.GetAwaiter().GetResult();
            var world = worldFacade.GetWorld(result.WorldSpaceKind);
            if (!_desiredChunksByWorld.TryGetValue(result.WorldSpaceKind, out var desired)
                || !desired.Contains(result.Coord)
                || world.LoadedChunks.ContainsKey(result.Coord))
            {
                RuntimeProfiler.Increment("WorldStreaming.ChunkLoadDiscarded");
                continue;
            }

            using (RuntimeProfiler.Measure("WorldStreaming.CommitChunkLoad"))
            {
                world.LoadChunk(result.GeneratedChunk, _ => result.RuntimeChunkState);
            }

            RuntimeProfiler.Increment("WorldStreaming.ChunkLoadCommitted");
            if (result.WorldSpaceKind == runtime.ActiveWorldSpaceKind)
            {
                worldFacade.MarkEntityProjectionDirty();
                worldFacade.NotifyLightingStructureChanged(result.WorldSpaceKind);
            }

            changed = true;
            committed++;
        }

        return changed;
    }

    private int GetPendingChunkPriority(PendingChunkLoadKey key)
    {
        return _desiredCentersByWorld.TryGetValue(key.WorldSpaceKind, out var center)
            ? GetChunkPriority(key.Coord, center)
            : int.MaxValue;
    }

    private static int GetChunkPriority(ChunkCoord coord, ChunkCoord center)
    {
        var dx = Math.Abs(coord.X - center.X);
        var dy = Math.Abs(coord.Y - center.Y);
        return Math.Max(dx, dy);
    }

    private readonly record struct PendingChunkLoadKey(
        WorldSpaceKind WorldSpaceKind,
        ChunkCoord Coord);

    private sealed record PendingChunkLoad(
        long RequestId,
        Task<ChunkLoadResult> Task);

    private sealed record ChunkLoadResult(
        WorldSpaceKind WorldSpaceKind,
        ChunkCoord Coord,
        Downroot.World.Models.GeneratedChunk GeneratedChunk,
        ChunkRuntimeState RuntimeChunkState,
        long RequestId);
}
