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
        var portalLink = GetDimShardPortalLink(registries);

        var overworldModel = new WorldModel("overworld", WorldSpaceKind.Overworld, bootstrapConfig.WorldSeed);
        var dimShardModel = portalLink is null
            ? null
            : new WorldModel(
                CreatePocketWorldId(bootstrapConfig.WorldSeed, portalLink.SourcePortalChunk),
                WorldSpaceKind.DimShardPocket,
                CreatePocketWorldSeed(bootstrapConfig.WorldSeed, portalLink.SourcePortalChunk),
                new ChunkCoord(-1, -1),
                new ChunkCoord(1, 1),
                portalLink.SourcePortalChunk);

        var worldState = new WorldState
        {
            ActiveWorldSpaceKind = WorldSpaceKind.Overworld,
            Overworld = new LoadedWorldState(registries, overworldModel, bootstrapConfig.OverworldLoadRadius),
            DimShardPocket = dimShardModel is null ? null : new LoadedWorldState(registries, dimShardModel, bootstrapConfig.OverworldLoadRadius)
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
            portalLink is null ? null : CreateGenerator(registries, WorldSpaceKind.DimShardPocket),
            worldState,
            player,
            bootstrapConfig)
        {
            StartOptions = request.StartOptions
        };
        ValidatePocketWorld(runtime, portalLink);

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
                    runtime.GetWorldPosition(spawn),
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
                    runtime.GetWorldPosition(spawn),
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
                    runtime.GetWorldPosition(spawn),
                    placeableDef.MaxDurability,
                    generatedChunk.WorldSpaceKind,
                    generatedChunk.Coord,
                    true,
                    CreateNaturalEntityId(generatedChunk.WorldSpaceKind, generatedChunk.Coord, spawn.Tile, placeableDef.Id))
                {
                    PlaceableState = PlaceableRuntimeStateFactory.Create(runtime, placeableDef)
                });
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

    private static WorldGenerator CreateGenerator(ContentRegistrySet registries, WorldSpaceKind worldSpaceKind)
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

    private static PortalWorldLinkDef? GetDimShardPortalLink(ContentRegistrySet registries)
    {
        var links = registries.PortalWorldLinks.Where(link =>
            link.SourceWorldSpaceKind == WorldSpaceKind.Overworld
            && link.TargetWorldSpaceKind == WorldSpaceKind.DimShardPocket).ToArray();

        if (links.Length > 1)
        {
            throw new InvalidOperationException("Only one DimShardPocket portal link is supported in the current runtime.");
        }

        return links.SingleOrDefault();
    }

    private static void ValidatePocketWorld(GameRuntime runtime, PortalWorldLinkDef? portalLink)
    {
        if (portalLink is null)
        {
            if (runtime.DimShardPocket is not null)
            {
                throw new InvalidOperationException("DimShardPocket runtime must not be created when no portal link is registered.");
            }

            return;
        }

        if (runtime.DimShardPocket is null)
        {
            throw new InvalidOperationException("DimShardPocket runtime must exist when a portal link is registered.");
        }

        var dimShardPocket = runtime.DimShardPocket;
        if (!ReferenceEquals(dimShardPocket.Model, runtime.WorldState.DimShardPocket!.Model))
        {
            throw new InvalidOperationException("DimShardPocket must have its own world model.");
        }

        if (ReferenceEquals(runtime.Overworld.Model, dimShardPocket.Model))
        {
            throw new InvalidOperationException("DimShardPocket cannot share WorldModel with Overworld.");
        }

        if (ReferenceEquals(runtime.Overworld, dimShardPocket))
        {
            throw new InvalidOperationException("DimShardPocket must use its own LoadedWorldState.");
        }

        if (dimShardPocket.WorldSpaceKind != WorldSpaceKind.DimShardPocket)
        {
            throw new InvalidOperationException("Pocket world must be tagged as DimShardPocket.");
        }

        if (portalLink.SourcePortalChunk != dimShardPocket.Model.SourcePortalChunk)
        {
            throw new InvalidOperationException("Pocket world source portal chunk must match the registered portal link.");
        }

        var expectedStableId = CreatePocketWorldId(runtime.BootstrapConfig.WorldSeed, portalLink.SourcePortalChunk);
        if (!string.Equals(dimShardPocket.Model.StableId, expectedStableId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Pocket world stable id does not match the required format.");
        }

        var expectedSeed = CreatePocketWorldSeed(runtime.BootstrapConfig.WorldSeed, portalLink.SourcePortalChunk);
        if (dimShardPocket.WorldSeed != expectedSeed || dimShardPocket.WorldSeed == runtime.Overworld.WorldSeed)
        {
            throw new InvalidOperationException("Pocket world seed must be independently derived from the overworld seed and portal chunk.");
        }
    }
}
