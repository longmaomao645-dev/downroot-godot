using System.Numerics;
using Downroot.Content.Packs;
using Downroot.Content.Registries;
using Downroot.Core.Content;
using Downroot.Core.World;
using Downroot.Core.Save;
using Downroot.Gameplay.Persistence;
using Downroot.Gameplay.Runtime;
using Downroot.World.Generation;
using Downroot.World.Models;

namespace Downroot.Gameplay.Bootstrap;

public sealed class GameBootstrapper
{
    private readonly ContentPackResolver _packResolver = new();

    public GameRuntime Bootstrap()
    {
        return Bootstrap(new GameBootstrapRequest
        {
            StartOptions = new GameStartOptions
            {
                SaveSlotId = "quick-start",
                DisplayName = "Quick Start",
                WorldSeed = 1337,
                EnabledPackIds = [BaseGameContentPack.Id],
                IsNewGame = true
            }
        });
    }

    public GameRuntime Bootstrap(GameBootstrapRequest request)
    {
        var enabledPackIds = ResolveEnabledPackIds(request);
        var registries = BuildRegistries(_packResolver.Resolve(enabledPackIds));

        var bootstrapConfig = registries.BootstrapConfig
            ?? throw new InvalidOperationException("No bootstrap config was registered by any content pack.");
        bootstrapConfig = bootstrapConfig with { WorldSeed = request.StartOptions.WorldSeed };

        var overworldModel = new WorldModel("overworld", WorldSpaceKind.Overworld, bootstrapConfig.WorldSeed);

        var worldState = new WorldState
        {
            ActiveWorldSpaceKind = WorldSpaceKind.Overworld,
            Overworld = new LoadedWorldState(registries, overworldModel, bootstrapConfig.OverworldLoadRadius)
        };

        var player = new PlayerState(
            inventorySize: 16,
            hotbarSize: 8,
            survival: new SurvivalState(
                bootstrapConfig.StartingHealth,
                bootstrapConfig.MaxHealth,
                bootstrapConfig.StartingHunger,
                bootstrapConfig.MaxHunger))
        {
            Position = new Vector2(bootstrapConfig.PlayerSpawn.Tile.X * 32f, bootstrapConfig.PlayerSpawn.Tile.Y * 32f)
        };

        request.StartOptions.EnabledPackIds = enabledPackIds.ToArray();

        var runtime = new GameRuntime(
            registries,
            CreateGenerator(registries, WorldSpaceKind.Overworld),
            worldState,
            player,
            bootstrapConfig)
        {
            StartOptions = request.StartOptions
        };

        var spawnChunk = bootstrapConfig.PlayerSpawn.Tile.ToChunkCoord(runtime.ChunkWidth, runtime.ChunkHeight);
        LoadInitialChunks(runtime, runtime.Overworld, spawnChunk);
        if (request.ExistingSave is null)
        {
            AddDebugWorkbench(runtime);
        }
        else
        {
            new GameSaveLoader().Load(runtime, request.ExistingSave);
        }

        runtime.WorldState.RefreshEntityProjection();
        LogLoadedWorld(runtime.GetWorld(runtime.ActiveWorldSpaceKind));
        return runtime;
    }

    private static IReadOnlyList<string> ResolveEnabledPackIds(GameBootstrapRequest request)
    {
        if (request.ExistingSave?.Mods.EnabledPackIds is { Count: > 0 } savedEnabledPacks)
        {
            return savedEnabledPacks;
        }

        return request.StartOptions.EnabledPackIds;
    }

    private static ContentRegistrySet BuildRegistries(ResolvedContentPackSet packs)
    {
        var registries = new ContentRegistrySet();
        var registrar = registries.CreateRegistrar();
        foreach (var pack in packs.OrderedPacks)
        {
            pack.Register(registrar);
        }

        return registries;
    }

    public static ChunkRuntimeState CreateChunkRuntimeState(GameRuntime runtime, GeneratedChunk generatedChunk)
    {
        var chunk = new ChunkRuntimeState(generatedChunk);
        foreach (var spawn in generatedChunk.NaturalSpawns)
        {
            if (runtime.Content.ResourceNodes.TryGet(spawn.ContentId, out var resourceDef))
            {
                chunk.AddNaturalEntity(new WorldEntityState(
                    WorldEntityKind.ResourceNode,
                    resourceDef!.Id,
                    runtime.GetWorldPosition(spawn.Tile),
                    resourceDef.MaxDurability,
                    generatedChunk.WorldSpaceKind,
                    generatedChunk.Coord,
                    true,
                    CreateNaturalEntityId(generatedChunk.WorldSpaceKind, generatedChunk.Coord, spawn.Tile, resourceDef.Id)));
                continue;
            }

            if (runtime.Content.Creatures.TryGet(spawn.ContentId, out var creatureDef))
            {
                chunk.AddNaturalEntity(new WorldEntityState(
                    WorldEntityKind.Creature,
                    creatureDef!.Id,
                    runtime.GetWorldPosition(spawn.Tile),
                    creatureDef.MaxHealth,
                    generatedChunk.WorldSpaceKind,
                    generatedChunk.Coord,
                    true,
                    CreateNaturalEntityId(generatedChunk.WorldSpaceKind, generatedChunk.Coord, spawn.Tile, creatureDef.Id)));
                continue;
            }

            if (runtime.Content.Placeables.TryGet(spawn.ContentId, out var placeableDef))
            {
                chunk.AddNaturalEntity(new WorldEntityState(
                    WorldEntityKind.Placeable,
                    placeableDef!.Id,
                    runtime.GetWorldPosition(spawn.Tile),
                    placeableDef.MaxDurability,
                    generatedChunk.WorldSpaceKind,
                    generatedChunk.Coord,
                    true,
                    CreateNaturalEntityId(generatedChunk.WorldSpaceKind, generatedChunk.Coord, spawn.Tile, placeableDef.Id))
                {
                    PlaceableState = PlaceableRuntimeStateFactory.Create(runtime, placeableDef)
                });
                continue;
            }

            if (runtime.Content.Items.TryGet(spawn.ContentId, out var itemDef))
            {
                chunk.AddNaturalEntity(new WorldEntityState(
                    WorldEntityKind.ItemDrop,
                    itemDef!.Id,
                    runtime.GetWorldPosition(spawn.Tile),
                    1,
                    generatedChunk.WorldSpaceKind,
                    generatedChunk.Coord,
                    true,
                    CreateNaturalEntityId(generatedChunk.WorldSpaceKind, generatedChunk.Coord, spawn.Tile, itemDef.Id),
                    stackCount: 1));
            }
        }

        return chunk;
    }

    public static string CreateNaturalEntityId(WorldSpaceKind worldSpaceKind, ChunkCoord chunkCoord, WorldTileCoord tile, Downroot.Core.Ids.ContentId contentId)
    {
        return $"{worldSpaceKind}:{chunkCoord.X},{chunkCoord.Y}:{tile.X},{tile.Y}:{contentId.Value}";
    }

    public static int CreatePocketWorldSeed(int overworldSeed, ChunkCoord portalChunk)
    {
        unchecked
        {
            var hash = overworldSeed;
            hash = (hash * 397) ^ portalChunk.X;
            hash = (hash * 397) ^ portalChunk.Y;
            hash = (hash * 397) ^ (int)WorldSpaceKind.DimShardPocket;
            return hash;
        }
    }

    public static string CreatePocketWorldId(int overworldSeed, ChunkCoord portalChunk)
    {
        return $"dimshard:{overworldSeed}:{portalChunk.X},{portalChunk.Y}";
    }

    internal static WorldGenerator CreateGenerator(ContentRegistrySet registries, WorldSpaceKind worldSpaceKind)
    {
        return new WorldGenerator(
            registries,
            registries.WorldGenPasses
                .Where(pass => pass.WorldSpaceKind is null || pass.WorldSpaceKind == worldSpaceKind)
                .Select(pass => WorldGenPassFactory.Create(registries, pass))
                .ToArray());
    }

    private static void LoadInitialChunks(GameRuntime runtime, LoadedWorldState world, ChunkCoord centerChunk)
    {
        for (var y = centerChunk.Y - world.LoadRadius; y <= centerChunk.Y + world.LoadRadius; y++)
        {
            for (var x = centerChunk.X - world.LoadRadius; x <= centerChunk.X + world.LoadRadius; x++)
            {
                var coord = new ChunkCoord(x, y);
                if (!world.ContainsChunk(coord))
                {
                    continue;
                }

                var generated = runtime.GetWorldGenerator(world.WorldSpaceKind)
                    .GenerateChunk(world.WorldSpaceKind, world.WorldSeed, coord, runtime.ChunkWidth, runtime.ChunkHeight);
                world.LoadChunk(generated, chunk => CreateChunkRuntimeState(runtime, chunk));
            }
        }
    }

    private static void AddDebugWorkbench(GameRuntime runtime)
    {
        var placeableId = runtime.BootstrapConfig.DebugPlaceableId;
        var placeableDef = runtime.Content.Placeables.Get(placeableId);
        var tile = runtime.BootstrapConfig.DebugPlaceableSpawn.Tile;
        var chunkCoord = tile.ToChunkCoord(runtime.ChunkWidth, runtime.ChunkHeight);
        runtime.Overworld.AddRuntimeEntity(new WorldEntityState(
            WorldEntityKind.Placeable,
            placeableDef.Id,
            runtime.GetWorldPosition(tile),
            placeableDef.MaxDurability,
            WorldSpaceKind.Overworld,
            chunkCoord)
        {
            PlaceableState = PlaceableRuntimeStateFactory.Create(runtime, placeableDef)
        });
    }

    private static void LogLoadedWorld(LoadedWorldState world)
    {
        var chunkSummary = string.Join(", ", world.LoadedChunks.Keys.OrderBy(coord => coord.Y).ThenBy(coord => coord.X).Select(coord => $"({coord.X},{coord.Y})"));
        Console.WriteLine($"[WorldGen] loaded {world.WorldSpaceKind} chunks => {chunkSummary}");
    }


}
