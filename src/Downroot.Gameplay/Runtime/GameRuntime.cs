using Downroot.Content.Registries;
using Downroot.Core.Gameplay;
using Downroot.Gameplay.Bootstrap;
using Downroot.Core.World;
using Downroot.World.Generation;
using System.Numerics;
using Downroot.Core.Ids;

namespace Downroot.Gameplay.Runtime;

public sealed class GameRuntime(
    ContentRegistrySet content,
    WorldGenerator overworldGenerator,
    WorldState worldState,
    PlayerState player,
    GameBootstrapConfig bootstrapConfig)
{
    public ContentRegistrySet Content { get; } = content;
    public WorldGenerator OverworldGenerator { get; } = overworldGenerator;
    public WorldState WorldState { get; } = worldState;
    public PlayerState Player { get; } = player;
    public GameBootstrapConfig BootstrapConfig { get; } = bootstrapConfig;
    public GameStartOptions? StartOptions { get; set; }
    public string? SaveSlotId => StartOptions?.SaveSlotId;
    public string? SaveDisplayName => StartOptions?.DisplayName;
    public int WorldSeed => StartOptions?.WorldSeed ?? BootstrapConfig.WorldSeed;
    public IReadOnlyList<string> EnabledPackIds => StartOptions?.EnabledPackIds ?? [];

    public Dictionary<string, LoadedWorldState> PocketWorlds { get; } = new();
    public Dictionary<string, WorldGenerator> PocketGenerators { get; } = new();

    public WorldSpaceKind ActiveWorldSpaceKind
    {
        get => WorldState.ActiveWorldSpaceKind;
        set => WorldState.ActiveWorldSpaceKind = value;
    }

    public LoadedWorldState Overworld => WorldState.Overworld;
    public int ChunkWidth => BootstrapConfig.ChunkWidth;
    public int ChunkHeight => BootstrapConfig.ChunkHeight;
    public EntityId? PrimaryBedEntityId
    {
        get => WorldState.PrimaryBedEntityId;
        set => WorldState.PrimaryBedEntityId = value;
    }

    public LoadedWorldState GetWorld(WorldSpaceKind worldSpaceKind)
    {
        if (worldSpaceKind == WorldSpaceKind.Overworld)
        {
            return Overworld;
        }

        // DimShardPocket: resolve via the active pocket world ID.
        var activeId = WorldState.ActivePocketWorldId;
        if (activeId is not null
            && WorldState.PocketWorlds.TryGetValue(activeId, out var pocketWorld))
        {
            return pocketWorld;
        }

        throw new InvalidOperationException($"No active pocket world available for kind '{worldSpaceKind}'. Use GetPocketWorld(worldId) for specific pocket worlds.");
    }

    public LoadedWorldState? GetPocketWorld(string worldId) => PocketWorlds.GetValueOrDefault(worldId);

    public bool HasPocketWorld(string worldId) => PocketWorlds.ContainsKey(worldId);

    public WorldGenerator GetWorldGenerator(WorldSpaceKind worldSpaceKind)
    {
        return worldSpaceKind == WorldSpaceKind.Overworld
            ? OverworldGenerator
            : throw new InvalidOperationException("Use GetPocketGenerator(worldId) for pocket world generators.");
    }

    public WorldGenerator GetPocketGenerator(string worldId)
    {
        return PocketGenerators.GetValueOrDefault(worldId)
            ?? throw new InvalidOperationException($"Pocket world '{worldId}' generator not found.");
    }

    public WorldTileCoord GetWorldTile(Vector2 worldPosition)
    {
        return new WorldTileCoord(
            (int)MathF.Floor(worldPosition.X / 32f),
            (int)MathF.Floor(worldPosition.Y / 32f));
    }

    public ChunkCoord GetChunkCoord(Vector2 worldPosition) => GetWorldTile(worldPosition).ToChunkCoord(ChunkWidth, ChunkHeight);

    public Vector2 GetWorldPosition(WorldTileCoord tileCoord) => new(tileCoord.X * 32f, tileCoord.Y * 32f);
}
