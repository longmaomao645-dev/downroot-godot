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
    WorldGenerator? dimShardGenerator,
    WorldState worldState,
    PlayerState player,
    GameBootstrapConfig bootstrapConfig)
{
    public ContentRegistrySet Content { get; } = content;
    public WorldGenerator OverworldGenerator { get; } = overworldGenerator;
    public WorldGenerator? DimShardGenerator { get; } = dimShardGenerator;
    public WorldState WorldState { get; } = worldState;
    public PlayerState Player { get; } = player;
    public GameBootstrapConfig BootstrapConfig { get; } = bootstrapConfig;
    public GameStartOptions? StartOptions { get; set; }
    public string? SaveSlotId => StartOptions?.SaveSlotId;
    public string? SaveDisplayName => StartOptions?.DisplayName;
    public int WorldSeed => StartOptions?.WorldSeed ?? BootstrapConfig.WorldSeed;
    public IReadOnlyList<string> EnabledPackIds => StartOptions?.EnabledPackIds ?? [];

    public WorldSpaceKind ActiveWorldSpaceKind
    {
        get => WorldState.ActiveWorldSpaceKind;
        set => WorldState.ActiveWorldSpaceKind = value;
    }

    public LoadedWorldState Overworld => WorldState.Overworld;
    public LoadedWorldState? DimShardPocket => WorldState.DimShardPocket;
    public int ChunkWidth => BootstrapConfig.ChunkWidth;
    public int ChunkHeight => BootstrapConfig.ChunkHeight;
    public EntityId? PrimaryBedEntityId
    {
        get => WorldState.PrimaryBedEntityId;
        set => WorldState.PrimaryBedEntityId = value;
    }

    public LoadedWorldState GetWorld(WorldSpaceKind worldSpaceKind)
    {
        return worldSpaceKind == WorldSpaceKind.Overworld
            ? Overworld
            : DimShardPocket ?? throw new InvalidOperationException("DimShardPocket is not available in this runtime.");
    }

    public WorldGenerator GetWorldGenerator(WorldSpaceKind worldSpaceKind)
    {
        return worldSpaceKind == WorldSpaceKind.Overworld
            ? OverworldGenerator
            : DimShardGenerator ?? throw new InvalidOperationException("DimShardPocket generator is not available in this runtime.");
    }

    public WorldTileCoord GetWorldTile(Vector2 worldPosition)
    {
        return new WorldTileCoord(
            (int)MathF.Floor(worldPosition.X / 32f),
            (int)MathF.Floor(worldPosition.Y / 32f));
    }

    public ChunkCoord GetChunkCoord(Vector2 worldPosition) => GetWorldTile(worldPosition).ToChunkCoord(ChunkWidth, ChunkHeight);

    public Vector2 GetWorldPosition(WorldTileCoord tileCoord) => new(tileCoord.X * 32f, tileCoord.Y * 32f);

    public Vector2 GetWorldPosition(WorldSpawnDef spawn)
        => new((spawn.Tile.X * 32f) + spawn.PixelOffsetX, (spawn.Tile.Y * 32f) + spawn.PixelOffsetY);
}
