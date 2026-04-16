using Downroot.Core.Definitions;
using Downroot.Core.Ids;
using Downroot.Core.Input;
using Downroot.Core.World;
using Downroot.Core.Diagnostics;
using Downroot.Game.Infrastructure;
using Downroot.Gameplay.Runtime;
using Downroot.World.Generation;
using Godot;
using NumericsVector2 = System.Numerics.Vector2;

namespace Downroot.Game.Runtime;

public sealed partial class WorldRenderer : Node2D
{
    private const int TileSize = 32;
    private const int TerrainLayerZ = 0;
    private const int RaisedFeatureLayerZ = 256;
    private const int GroundCoverLayerZ = 512;
    private const int EntityBandLayerZ = 768;
    private const int ChunkBoundsLayerZ = 1536;
    private const int MaxEntitySortSpan = 1023;

    private readonly TextureContentLoader _textureLoader;
    private readonly PlayerAnimationFactory _animationFactory;
    private readonly Dictionary<string, Texture2D> _textureCache = [];
    private readonly Dictionary<EntityId, Sprite2D> _entitySprites = [];
    private readonly Dictionary<EntityId, EntityVisualState> _entityVisualStates = [];
    private readonly Dictionary<EntityId, ChunkCoord> _entitySpriteChunks = [];
    private readonly HashSet<EntityId> _dynamicEntityIds = [];
    private readonly HashSet<EntityId> _staticEntityIds = [];
    private readonly Dictionary<ChunkCoord, ChunkVisualState> _chunkVisuals = [];

    private GameRuntime? _runtime;
    private WorldRuntimeFacade? _worldFacade;
    private Node2D? _terrainLayer;
    private Node2D? _entityLayer;
    private CharacterBody2D? _playerBody;
    private AnimatedSprite2D? _playerSprite;
    private string _lastFacing = "down";
    private WorldSpaceKind? _lastRenderedWorldSpaceKind;
    private long _lastChunkVisualVersion = -1;
    private long _lastEntityProjectionVersion = -1;
    private long _lastLightingFieldVersion = -1;
    private bool _showChunkBounds;

    public WorldRenderer(TextureContentLoader textureLoader, PlayerAnimationFactory animationFactory)
    {
        _textureLoader = textureLoader;
        _animationFactory = animationFactory;
        Name = "WorldRenderer";
    }

    public void Initialize(GameRuntime runtime)
    {
        _runtime = runtime;
        _worldFacade = new WorldRuntimeFacade(runtime);
        _terrainLayer = new Node2D { Name = "TerrainLayer", ZIndex = TerrainLayerZ };
        _entityLayer = new Node2D { Name = "EntityLayer" };
        AddChild(_terrainLayer);
        AddChild(_entityLayer);
        CreatePlayer();
        _runtime.WorldState.EnsureEntityProjectionCurrent();
        SynchronizeChunks();
        RefreshDirtyRaisedFeatures();
        SynchronizeEntityStructure();
        UpdateEntitySprites(true);
        _lastRenderedWorldSpaceKind = runtime.ActiveWorldSpaceKind;
        _lastChunkVisualVersion = _worldFacade.GetActiveWorld().ChunkVisualVersion;
        _lastEntityProjectionVersion = runtime.WorldState.EntityProjectionVersion;
        _lastLightingFieldVersion = runtime.WorldState.Lighting.FieldVersion;
    }

    public void Update(InputFrame frame)
    {
        using var updateScope = RuntimeProfiler.Measure("WorldRenderer.Update");
        if (_runtime is null || _playerBody is null || _playerSprite is null)
        {
            return;
        }

        using (RuntimeProfiler.Measure("WorldRenderer.ProjectionEnsure"))
        {
            _runtime.WorldState.EnsureEntityProjectionCurrent();
        }

        using (RuntimeProfiler.Measure("WorldRenderer.Player"))
        {
            _playerBody.Position = ToGodot(_runtime.Player.Position);
            _playerBody.Velocity = ToGodot(frame.Movement * _runtime.Player.Speed);
            _playerBody.ZIndex = ResolvePlayerZIndex();
            if (frame.Movement != NumericsVector2.Zero)
            {
                _lastFacing = ResolveFacing(frame.Movement);
                _playerSprite.Play($"run_{_lastFacing}");
            }
            else
            {
                _playerSprite.Play($"idle_{_lastFacing}");
            }

            _playerSprite.Modulate = ResolvePlayerModulate();
        }

        using (RuntimeProfiler.Measure("WorldRenderer.SyncWorldVisuals"))
        {
            SynchronizeWorldVisuals();
        }

        using (RuntimeProfiler.Measure("WorldRenderer.RefreshRaised"))
        {
            RefreshDirtyRaisedFeatures();
        }

        var lightingFieldChanged = _lastLightingFieldVersion != _runtime.WorldState.Lighting.FieldVersion;
        if (lightingFieldChanged)
        {
            RefreshLightingFieldVisuals();
            _lastLightingFieldVersion = _runtime.WorldState.Lighting.FieldVersion;
        }

        using (RuntimeProfiler.Measure("WorldRenderer.UpdateEntitySprites"))
        {
            UpdateEntitySprites(lightingFieldChanged);
        }
    }

    public void ValidateContentLoads(GameRuntime runtime)
    {
        foreach (var terrain in runtime.Content.Terrains.All)
        {
            _ = ResolveTerrainTexture(terrain, terrain.AtlasColumn, terrain.AtlasRow);
        }

        foreach (var item in runtime.Content.Items.All)
        {
            _ = ResolveItemTexture(item);
        }

        foreach (var raisedFeature in runtime.Content.RaisedFeatures.All)
        {
            _ = ResolveRaisedFeatureTexture(raisedFeature, 0);
        }

        foreach (var placeable in runtime.Content.Placeables.All)
        {
            _ = ResolvePlaceableTexture(placeable, false);
        }

        foreach (var resourceNode in runtime.Content.ResourceNodes.All)
        {
            _ = ResolveResourceNodeTexture(resourceNode);
        }

        GD.Print($"Loaded chunks: {runtime.WorldState.GetActiveWorld().LoadedChunks.Count}, active entities: {runtime.WorldState.Entities.Count}");
    }

    public Vector2 WorldToScreen(NumericsVector2 worldPosition)
    {
        return GetViewport().GetCanvasTransform() * ToGodot(worldPosition);
    }

    public Texture2D ResolveItemIcon(ContentId itemId)
    {
        return ResolveItemTexture(_runtime!.Content.Items.Get(itemId));
    }

    public void SetShowChunkBounds(bool enabled)
    {
        if (_showChunkBounds == enabled)
        {
            return;
        }

        _showChunkBounds = enabled;
        foreach (var visual in _chunkVisuals.Values)
        {
            visual.BoundsRoot.Visible = enabled;
        }
    }

    private void SynchronizeWorldVisuals()
    {
        var activeWorld = _worldFacade!.GetActiveWorld();
        if (_lastRenderedWorldSpaceKind != activeWorld.WorldSpaceKind)
        {
            ResetWorldVisuals();
            _lastRenderedWorldSpaceKind = activeWorld.WorldSpaceKind;
        }

        if (_lastChunkVisualVersion != activeWorld.ChunkVisualVersion)
        {
            SynchronizeChunks();
            _lastChunkVisualVersion = activeWorld.ChunkVisualVersion;
        }

        if (_lastEntityProjectionVersion != _runtime!.WorldState.EntityProjectionVersion)
        {
            SynchronizeEntityStructure();
            _lastEntityProjectionVersion = _runtime.WorldState.EntityProjectionVersion;
        }
    }

    private void SynchronizeChunks()
    {
        var world = _worldFacade!.GetActiveWorld();
        var desiredChunks = world.LoadedChunks.Keys.ToHashSet();
        foreach (var staleChunk in _chunkVisuals.Keys.Where(coord => !desiredChunks.Contains(coord)).ToArray())
        {
            _chunkVisuals[staleChunk].BoundsRoot.QueueFree();
            _chunkVisuals[staleChunk].TerrainRoot.QueueFree();
            _chunkVisuals[staleChunk].RaisedFeatureRoot.QueueFree();
            _chunkVisuals[staleChunk].EntityRoot.QueueFree();
            _chunkVisuals.Remove(staleChunk);
        }

        foreach (var pair in world.LoadedChunks.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            if (_chunkVisuals.ContainsKey(pair.Key))
            {
                continue;
            }

            var terrainRoot = new Node2D { Name = $"ChunkTerrain_{pair.Key.X}_{pair.Key.Y}" };
            var raisedFeatureRoot = new Node2D { Name = $"ChunkRaised_{pair.Key.X}_{pair.Key.Y}", ZIndex = RaisedFeatureLayerZ };
            var entityRoot = new Node2D { Name = $"ChunkEntities_{pair.Key.X}_{pair.Key.Y}" };
            var boundsRoot = BuildChunkBounds(pair.Key);
            _terrainLayer!.AddChild(terrainRoot);
            _terrainLayer.AddChild(raisedFeatureRoot);
            _terrainLayer.AddChild(boundsRoot);
            _entityLayer!.AddChild(entityRoot);
            _chunkVisuals.Add(pair.Key, new ChunkVisualState(terrainRoot, raisedFeatureRoot, entityRoot, boundsRoot));
            BuildChunkTerrain(pair.Value.GeneratedChunk, _chunkVisuals[pair.Key]);
            BuildChunkRaisedFeatures(pair.Value, _chunkVisuals[pair.Key]);
        }
    }

    private void ResetWorldVisuals()
    {
        foreach (var visual in _chunkVisuals.Values)
        {
            visual.TerrainRoot.QueueFree();
            visual.RaisedFeatureRoot.QueueFree();
            visual.EntityRoot.QueueFree();
            visual.BoundsRoot.QueueFree();
        }

        _chunkVisuals.Clear();

        foreach (var sprite in _entitySprites.Values)
        {
            sprite.QueueFree();
        }

        _entitySprites.Clear();
        _entityVisualStates.Clear();
        _entitySpriteChunks.Clear();
        _dynamicEntityIds.Clear();
        _staticEntityIds.Clear();
        _lastChunkVisualVersion = -1;
        _lastEntityProjectionVersion = -1;
        _lastLightingFieldVersion = -1;
    }

    private void BuildChunkTerrain(Downroot.World.Models.GeneratedChunk chunk, ChunkVisualState visual)
    {
        var chunkOriginTile = WorldTileCoord.FromChunkAndLocal(chunk.Coord, new LocalTileCoord(0, 0), _runtime!.ChunkWidth, _runtime.ChunkHeight);
        for (var y = 0; y < chunk.Surface.Height; y++)
        {
            for (var x = 0; x < chunk.Surface.Width; x++)
            {
                var worldTile = new WorldTileCoord(chunkOriginTile.X + x, chunkOriginTile.Y + y);
                var baseTerrainId = chunk.Surface.GetBaseTerrainId(x, y) ?? _runtime.BootstrapConfig.DefaultTerrainId;
                var baseTerrainDef = _runtime.Content.Terrains.Get(baseTerrainId);
                var baseSprite = CreateTerrainSprite($"BaseTerrain_{x}_{y}", worldTile, baseTerrainDef, 0);
                visual.TerrainRoot.AddChild(baseSprite);
                visual.BaseTerrainSprites[worldTile] = baseSprite;

                var coverTerrainId = chunk.Surface.GetCoverTerrainId(x, y);
                if (coverTerrainId is null)
                {
                    continue;
                }

                var coverTerrainDef = _runtime.Content.Terrains.Get(coverTerrainId.Value);
                var coverSprite = CreateTerrainSprite($"CoverTerrain_{x}_{y}", worldTile, coverTerrainDef, 1);
                visual.TerrainRoot.AddChild(coverSprite);
                visual.CoverTerrainSprites[worldTile] = coverSprite;
            }
        }
    }

    private Sprite2D CreateTerrainSprite(string name, WorldTileCoord worldTile, TerrainDef terrainDef, int zIndex)
    {
        var (atlasColumn, atlasRow) = ResolveTerrainVariant(terrainDef, worldTile);
        return new Sprite2D
        {
            Name = name,
            Centered = false,
            Texture = ResolveTerrainTexture(terrainDef, atlasColumn, atlasRow),
            Position = new Vector2(worldTile.X * TileSize, worldTile.Y * TileSize),
            Modulate = ResolveTileBrightnessColor(worldTile),
            ZIndex = zIndex
        };
    }

    private void BuildChunkRaisedFeatures(ChunkRuntimeState chunk, ChunkVisualState visual)
    {
        var chunkOriginTile = WorldTileCoord.FromChunkAndLocal(chunk.GeneratedChunk.Coord, new LocalTileCoord(0, 0), _runtime!.ChunkWidth, _runtime.ChunkHeight);
        for (var y = 0; y < chunk.GeneratedChunk.Surface.Height; y++)
        {
            for (var x = 0; x < chunk.GeneratedChunk.Surface.Width; x++)
            {
                RefreshRaisedFeatureTile(new WorldTileCoord(chunkOriginTile.X + x, chunkOriginTile.Y + y), visual);
            }
        }
    }

    private void RefreshDirtyRaisedFeatures()
    {
        var world = _worldFacade!.GetActiveWorld();
        foreach (var tile in world.ConsumeDirtyRaisedFeatureTiles())
        {
            var chunkCoord = tile.ToChunkCoord(_runtime!.ChunkWidth, _runtime.ChunkHeight);
            if (!_chunkVisuals.TryGetValue(chunkCoord, out var visual))
            {
                continue;
            }

            RefreshRaisedFeatureTile(tile, visual);
        }
    }

    private void RefreshRaisedFeatureTile(WorldTileCoord tile, ChunkVisualState visual)
    {
        if (visual.RaisedSprites.Remove(tile, out var existing))
        {
            existing.QueueFree();
        }

        if (!_worldFacade!.GetActiveWorld().TryGetRaisedFeature(tile, _runtime!.ChunkWidth, _runtime.ChunkHeight, out var featureId, out var variantIndex))
        {
            return;
        }

        var raisedFeature = _runtime.Content.RaisedFeatures.Get(featureId!.Value);
        var sprite = new Sprite2D
        {
            Name = $"Raised_{tile.X}_{tile.Y}",
            Centered = false,
            Texture = ResolveRaisedFeatureTexture(raisedFeature, variantIndex),
            Position = new Vector2(tile.X * TileSize, tile.Y * TileSize),
            Modulate = ResolveTileBrightnessColor(tile),
            ZIndex = 0
        };
        visual.RaisedFeatureRoot.AddChild(sprite);
        visual.RaisedSprites[tile] = sprite;
    }

    private void CreatePlayer()
    {
        var creature = _runtime!.Content.Creatures.Get(_runtime.BootstrapConfig.PlayerCreatureId);
        var frames = _animationFactory.Create(creature);
        _playerBody = new CharacterBody2D
        {
            Name = "Player",
            Position = ToGodot(_runtime.Player.Position),
            ZIndex = ResolvePlayerZIndex()
        };

        _playerSprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Animation = "idle_down"
        };
        _playerSprite.Play("idle_down");
        _playerBody.AddChild(_playerSprite);

        var camera = new Camera2D
        {
            Enabled = true,
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 6f
        };
        _playerBody.AddChild(camera);
        AddChild(_playerBody);
    }

    private void SynchronizeEntityStructure()
    {
        var runtime = _runtime!;
        var aliveIds = runtime.WorldState.Entities.Where(entity => !entity.Removed).Select(entity => entity.Id).ToHashSet();
        foreach (var removedId in _entitySprites.Keys.Where(id => !aliveIds.Contains(id)).ToArray())
        {
            _entitySprites[removedId].QueueFree();
            _entitySprites.Remove(removedId);
            _entityVisualStates.Remove(removedId);
            _entitySpriteChunks.Remove(removedId);
            _dynamicEntityIds.Remove(removedId);
            _staticEntityIds.Remove(removedId);
        }

        foreach (var entity in runtime.WorldState.Entities.Where(entity => !entity.Removed))
        {
            if (!_chunkVisuals.TryGetValue(entity.ChunkCoord, out var chunkVisual))
            {
                continue;
            }

            if (!_entitySprites.TryGetValue(entity.Id, out var sprite))
            {
                sprite = new Sprite2D { Centered = false };
                chunkVisual.EntityRoot.AddChild(sprite);
                _entitySprites.Add(entity.Id, sprite);
                _entitySpriteChunks[entity.Id] = entity.ChunkCoord;
                _entityVisualStates[entity.Id] = EntityVisualState.CreateUninitialized(entity.OpenState);
            }
            else if (_entitySpriteChunks[entity.Id] != entity.ChunkCoord)
            {
                sprite.Reparent(chunkVisual.EntityRoot);
                _entitySpriteChunks[entity.Id] = entity.ChunkCoord;
            }

            if (IsDynamicEntity(entity))
            {
                _dynamicEntityIds.Add(entity.Id);
                _staticEntityIds.Remove(entity.Id);
            }
            else
            {
                _staticEntityIds.Add(entity.Id);
                _dynamicEntityIds.Remove(entity.Id);
            }
        }
    }

    private void UpdateEntitySprites(bool lightingFieldChanged)
    {
        var runtime = _runtime!;
        foreach (var entityId in _dynamicEntityIds.ToArray())
        {
            if (!TryGetRenderableEntity(entityId, out var entity, out var sprite))
            {
                continue;
            }

            ApplyEntityVisual(entity, sprite, runtime);
        }

        foreach (var entityId in _staticEntityIds.ToArray())
        {
            if (!TryGetRenderableEntity(entityId, out var entity, out var sprite))
            {
                continue;
            }

            if (!lightingFieldChanged && !NeedsStaticRefresh(entity))
            {
                continue;
            }

            ApplyEntityVisual(entity, sprite, runtime);
        }
    }

    private bool TryGetRenderableEntity(EntityId entityId, out WorldEntityState entity, out Sprite2D sprite)
    {
        entity = null!;
        sprite = null!;
        Sprite2D? resolvedSprite = null;
        if (!_worldFacade!.TryGetActiveEntity(entityId, out var activeEntity)
            || activeEntity.Removed
            || !_entitySprites.TryGetValue(entityId, out resolvedSprite))
        {
            return false;
        }

        entity = activeEntity;
        sprite = resolvedSprite;
        return true;
    }

    private bool NeedsStaticRefresh(WorldEntityState entity)
    {
        var current = BuildEntityVisualState(entity, _runtime!);
        return !_entityVisualStates.TryGetValue(entity.Id, out var cached)
            || !cached.Matches(current);
    }

    private void ApplyEntityVisual(WorldEntityState entity, Sprite2D sprite, GameRuntime runtime)
    {
        var nextState = BuildEntityVisualState(entity, runtime);
        if (!_entityVisualStates.TryGetValue(entity.Id, out var currentState))
        {
            currentState = EntityVisualState.CreateUninitialized(entity.OpenState);
        }

        if (!ReferenceEquals(currentState.Texture, nextState.Texture))
        {
            sprite.Texture = nextState.Texture;
        }

        if (currentState.Position != nextState.Position)
        {
            sprite.Position = nextState.Position;
        }

        if (currentState.FlipH != nextState.FlipH)
        {
            sprite.FlipH = nextState.FlipH;
        }

        if (currentState.Modulate != nextState.Modulate)
        {
            sprite.Modulate = nextState.Modulate;
        }

        if (currentState.ZIndex != nextState.ZIndex)
        {
            sprite.ZIndex = nextState.ZIndex;
        }

        _entityVisualStates[entity.Id] = nextState;
    }

    private EntityVisualState BuildEntityVisualState(WorldEntityState entity, GameRuntime runtime)
    {
        var texture = entity.Kind switch
        {
            WorldEntityKind.ResourceNode => ResolveResourceNodeTexture(runtime.Content.ResourceNodes.Get(entity.DefinitionId)),
            WorldEntityKind.Placeable => ResolvePlaceableTexture(entity, runtime.Content.Placeables.Get(entity.DefinitionId), entity.OpenState),
            WorldEntityKind.Creature => ResolveCreatureTexture(runtime.Content.Creatures.Get(entity.DefinitionId)),
            WorldEntityKind.ItemDrop => ResolveItemTexture(runtime.Content.Items.Get(entity.DefinitionId)),
            _ => throw new InvalidOperationException($"Unsupported entity kind '{entity.Kind}'.")
        };

        return new EntityVisualState(
            texture,
            ToGodot(entity.Position),
            entity.Kind == WorldEntityKind.Creature && entity.Position.X > runtime.Player.Position.X,
            ResolveEntityModulate(entity),
            ResolveZIndex(entity),
            entity.OpenState);
    }

    private static bool IsDynamicEntity(WorldEntityState entity)
    {
        return entity.Kind is WorldEntityKind.Creature or WorldEntityKind.ItemDrop;
    }

    private Texture2D ResolveTerrainTexture(TerrainDef terrainDef, int atlasColumn, int atlasRow)
    {
        return ResolveCachedTexture(
            $"terrain:{terrainDef.Id.Value}:{atlasColumn}:{atlasRow}",
            () => _textureLoader.LoadTerrain(terrainDef, atlasColumn, atlasRow).Texture);
    }

    private Texture2D ResolveItemTexture(ItemDef itemDef) => ResolveCachedTexture($"item:{itemDef.Id.Value}", () => _textureLoader.LoadItem(itemDef).Texture);

    private Texture2D ResolvePlaceableTexture(WorldEntityState entity, PlaceableDef placeableDef, bool isOpen)
    {
        if (placeableDef.ConnectsToSameNeighbors)
        {
            return ResolveConnectedPlaceableTexture(entity, placeableDef, isOpen);
        }

        return ResolveCachedTexture($"placeable:{placeableDef.Id.Value}:{isOpen}", () => _textureLoader.LoadPlaceable(placeableDef, isOpen).Texture);
    }

    private Texture2D ResolvePlaceableTexture(PlaceableDef placeableDef, bool isOpen)
    {
        return ResolveCachedTexture($"placeable:{placeableDef.Id.Value}:{isOpen}", () => _textureLoader.LoadPlaceable(placeableDef, isOpen).Texture);
    }

    private Texture2D ResolveConnectedPlaceableTexture(WorldEntityState entity, PlaceableDef placeableDef, bool isOpen)
    {
        var tile = _worldFacade!.GetWorldTile(entity.Position);
        var north = HasMatchingConnectedNeighbor(entity, new WorldTileCoord(tile.X, tile.Y - 1));
        var east = HasMatchingConnectedNeighbor(entity, new WorldTileCoord(tile.X + 1, tile.Y));
        var south = HasMatchingConnectedNeighbor(entity, new WorldTileCoord(tile.X, tile.Y + 1));
        var west = HasMatchingConnectedNeighbor(entity, new WorldTileCoord(tile.X - 1, tile.Y));
        var variant = ResolveConnectedPlaceableVariant(north, east, south, west);
        return ResolveCachedTexture(
            $"placeable-connected:{placeableDef.Id.Value}:{variant}:{isOpen}",
            () => _textureLoader.LoadTexture($"{placeableDef.Id.Value}:{variant}", $"{System.IO.Path.GetDirectoryName(placeableDef.SpritePath)!.Replace('\\', '/')}/{variant}.png").Texture);
    }

    private bool HasMatchingConnectedNeighbor(WorldEntityState entity, WorldTileCoord neighborTile)
    {
        foreach (var candidate in _runtime!.WorldState.Entities)
        {
            if (candidate.Id == entity.Id
                || candidate.Removed
                || candidate.Kind != WorldEntityKind.Placeable
                || candidate.DefinitionId != entity.DefinitionId)
            {
                continue;
            }

            if (_worldFacade!.GetWorldTile(candidate.Position) == neighborTile)
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveConnectedPlaceableVariant(bool north, bool east, bool south, bool west)
    {
        var connections = (north ? 1 : 0) + (east ? 1 : 0) + (south ? 1 : 0) + (west ? 1 : 0);
        if (connections <= 0)
        {
            return "wood_fence_horizontal";
        }

        if (north && east && !south && !west)
        {
            return "wood_fence_corner_ne";
        }

        if (!north && east && south && !west)
        {
            return "wood_fence_corner_es";
        }

        if (!north && !east && south && west)
        {
            return "wood_fence_corner_sw";
        }

        if (north && !east && !south && west)
        {
            return "wood_fence_corner_wn";
        }

        if ((north || south) && !(east || west))
        {
            return "wood_fence_vertical";
        }

        if ((east || west) && !(north || south))
        {
            return east && !west
                ? "wood_fence_end_east"
                : west && !east
                    ? "wood_fence_end_west"
                    : "wood_fence_horizontal";
        }

        if (east && west)
        {
            return "wood_fence_horizontal";
        }

        if (north && south)
        {
            return "wood_fence_vertical";
        }

        return "wood_fence_horizontal";
    }

    private Texture2D ResolveResourceNodeTexture(ResourceNodeDef resourceNodeDef)
    {
        return ResolveCachedTexture($"resource:{resourceNodeDef.Id.Value}", () => _textureLoader.LoadResourceNode(resourceNodeDef).Texture);
    }

    private Texture2D ResolveRaisedFeatureTexture(RaisedFeatureDef raisedFeatureDef, byte variantIndex)
    {
        return ResolveCachedTexture($"raised:{raisedFeatureDef.Id.Value}:{variantIndex}", () => _textureLoader.LoadRaisedFeature(raisedFeatureDef, variantIndex).Texture);
    }

    private Texture2D ResolveCreatureTexture(CreatureDef creatureDef)
    {
        return ResolveCachedTexture($"creature:{creatureDef.Id.Value}", () => _textureLoader.LoadCreature(creatureDef).Texture);
    }

    private Color ResolveEntityModulate(WorldEntityState entity)
    {
        var baseColor = ResolveEntityBaseModulate(entity);
        var brightness = ResolveEntityBrightness(entity);
        return ApplyBrightness(baseColor, brightness);
    }

    private Color ResolveEntityBaseModulate(WorldEntityState entity)
    {
        if (entity.HitFlashSeconds > 0f)
        {
            return new Color(1f, 0.65f, 0.65f, 1f);
        }

        if (entity.Kind == WorldEntityKind.Placeable
            && _runtime!.Content.Placeables.TryGet(entity.DefinitionId, out var placeableDef)
            && placeableDef!.HasBehavior(PlaceableBehaviorKind.LightSource)
            && entity.PlaceableState?.IsLit == false)
        {
            return new Color(0.72f, 0.72f, 0.72f, 0.9f);
        }

        return Colors.White;
    }

    private Color ResolvePlayerModulate()
    {
        var brightness = ResolveCreatureBrightness(_runtime!.Player.Position, _runtime.Content.Creatures.Get(_runtime.BootstrapConfig.PlayerCreatureId).SpriteHeight, 0.16f);
        if (_runtime.WorldState.PlayerHitFlashSeconds > 0f)
        {
            return ApplyBrightness(new Color(1f, 0.72f, 0.72f, 1f), brightness);
        }

        return ApplyBrightness(Colors.White, brightness);
    }

    private void RefreshLightingFieldVisuals()
    {
        foreach (var visual in _chunkVisuals.Values)
        {
            foreach (var pair in visual.BaseTerrainSprites)
            {
                pair.Value.Modulate = ResolveTileBrightnessColor(pair.Key);
            }

            foreach (var pair in visual.CoverTerrainSprites)
            {
                pair.Value.Modulate = ResolveTileBrightnessColor(pair.Key);
            }

            foreach (var pair in visual.RaisedSprites)
            {
                pair.Value.Modulate = ResolveTileBrightnessColor(pair.Key);
            }
        }
    }

    private Color ResolveTileBrightnessColor(WorldTileCoord tile)
    {
        return ApplyBrightness(Colors.White, SampleBrightness(tile));
    }

    private float ResolveEntityBrightness(WorldEntityState entity)
    {
        return entity.Kind switch
        {
            WorldEntityKind.Placeable => ResolvePlaceableBrightness(entity),
            WorldEntityKind.ResourceNode => ResolveResourceBrightness(entity),
            WorldEntityKind.Creature => ResolveCreatureBrightness(entity.Position, _runtime!.Content.Creatures.Get(entity.DefinitionId).SpriteHeight, 0.16f),
            WorldEntityKind.ItemDrop => ResolveItemBrightness(entity),
            _ => 1f
        };
    }

    private float ResolvePlaceableBrightness(WorldEntityState entity)
    {
        var placeableDef = _runtime!.Content.Placeables.Get(entity.DefinitionId);
        var footWorld = new NumericsVector2(entity.Position.X + (placeableDef.SpriteWidth * 0.5f), entity.Position.Y + placeableDef.SpriteHeight - 4f);
        return ResolveBiasedBrightness(_worldFacade!.GetWorldTile(footWorld), 0.08f);
    }

    private float ResolveResourceBrightness(WorldEntityState entity)
    {
        var resourceDef = _runtime!.Content.ResourceNodes.Get(entity.DefinitionId);
        var footWorld = new NumericsVector2(entity.Position.X + (resourceDef.SpriteWidth * 0.5f), entity.Position.Y + resourceDef.SpriteHeight - 4f);
        return ResolveBiasedBrightness(_worldFacade!.GetWorldTile(footWorld), 0.10f);
    }

    private float ResolveItemBrightness(WorldEntityState entity)
    {
        var itemDef = _runtime!.Content.Items.Get(entity.DefinitionId);
        var footWorld = new NumericsVector2(entity.Position.X + (itemDef.IconWidth * 0.5f), entity.Position.Y + itemDef.IconHeight - 2f);
        return ResolveBiasedBrightness(_worldFacade!.GetWorldTile(footWorld), 0.08f);
    }

    private float ResolveCreatureBrightness(NumericsVector2 position, int spriteHeight, float ambientBias)
    {
        var footWorld = new NumericsVector2(position.X + 16f, position.Y + spriteHeight - 10f);
        return ResolveBiasedBrightness(_worldFacade!.GetWorldTile(footWorld), ambientBias);
    }

    private float ResolveBiasedBrightness(WorldTileCoord tile, float ambientBias)
    {
        var sampled = SampleBrightness(tile);
        return Math.Clamp(sampled + ((1f - sampled) * ambientBias), 0f, 1f);
    }

    private float SampleBrightness(WorldTileCoord tile)
    {
        return _runtime!.WorldState.Lighting.Field?.SampleCombined(tile) ?? 1f;
    }

    private static Color ApplyBrightness(Color baseColor, float brightness)
    {
        return new Color(
            baseColor.R * brightness,
            baseColor.G * brightness,
            baseColor.B * brightness,
            baseColor.A);
    }

    private Texture2D ResolveCachedTexture(string key, Func<Texture2D> factory)
    {
        if (_textureCache.TryGetValue(key, out var texture))
        {
            return texture;
        }

        texture = factory();
        _textureCache[key] = texture;
        return texture;
    }

    private (int AtlasColumn, int AtlasRow) ResolveTerrainVariant(TerrainDef terrainDef, WorldTileCoord tile)
    {
        if (terrainDef.Id.Value == "basegame:dirt")
        {
            var activeWorld = _worldFacade!.GetActiveWorld();
            var dirtVariantIndex = DirtVariantSampler.SampleVariantIndex(activeWorld.WorldSpaceKind, activeWorld.WorldSeed, tile);
            return ResolveDirtAtlasCoords(terrainDef, dirtVariantIndex);
        }

        if (terrainDef.VariantColumnCount <= 1 && terrainDef.VariantRowCount <= 1)
        {
            return (terrainDef.AtlasColumn, terrainDef.AtlasRow);
        }

        var variantCount = terrainDef.VariantColumnCount * terrainDef.VariantRowCount;
        var variantIndex = GetDeterministicTerrainVariantIndex(terrainDef.Id.Value, tile, _worldFacade!.GetActiveWorld().WorldSeed, variantCount);
        return (
            terrainDef.AtlasColumn + (variantIndex % terrainDef.VariantColumnCount),
            terrainDef.AtlasRow + (variantIndex / terrainDef.VariantColumnCount));
    }

    private static (int AtlasColumn, int AtlasRow) ResolveDirtAtlasCoords(TerrainDef terrainDef, int variantIndex)
    {
        return (terrainDef.AtlasColumn + variantIndex, terrainDef.AtlasRow);
    }

    private static int GetDeterministicTerrainVariantIndex(string terrainId, WorldTileCoord tile, int worldSeed, int variantCount)
    {
        unchecked
        {
            var hash = worldSeed;
            foreach (var character in terrainId)
            {
                hash = (hash * 397) ^ character;
            }

            hash = (hash * 397) ^ tile.X;
            hash = (hash * 397) ^ tile.Y;
            return (int)(uint)hash % variantCount;
        }
    }

    private int ResolveZIndex(WorldEntityState entity)
    {
        if (entity.Kind == WorldEntityKind.Placeable && _runtime!.Content.Placeables.Get(entity.DefinitionId).IsGroundCover)
        {
            return GroundCoverLayerZ;
        }

        return ResolveEntityBandZ(ResolveEntitySortY(entity));
    }

    private int ResolvePlayerZIndex()
    {
        return ResolveEntityBandZ(ResolvePlayerSortY());
    }

    private float ResolveEntitySortY(WorldEntityState entity)
    {
        return entity.Kind switch
        {
            WorldEntityKind.ResourceNode => ResolveResourceSortY(entity),
            WorldEntityKind.Placeable => ResolvePlaceableSortY(entity),
            WorldEntityKind.Creature => ResolveCreatureSortY(entity),
            WorldEntityKind.ItemDrop => entity.Position.Y + ResolveItemTexture(_runtime!.Content.Items.Get(entity.DefinitionId)).GetHeight() * 0.5f,
            _ => entity.Position.Y + TileSize
        };
    }

    private float ResolvePlayerSortY()
    {
        var spriteHeight = _runtime!.Content.Creatures.Get(_runtime.BootstrapConfig.PlayerCreatureId).SpriteHeight;
        return _runtime.Player.Position.Y + spriteHeight * 0.75f;
    }

    private float ResolveResourceSortY(WorldEntityState entity)
    {
        var resourceDef = _runtime!.Content.ResourceNodes.Get(entity.DefinitionId);
        return entity.Position.Y + (resourceDef.IsTree ? resourceDef.SpriteHeight * 0.75f : resourceDef.SpriteHeight);
    }

    private float ResolvePlaceableSortY(WorldEntityState entity)
    {
        var placeableDef = _runtime!.Content.Placeables.Get(entity.DefinitionId);
        if (placeableDef.IsGroundCover)
        {
            return entity.Position.Y;
        }

        return entity.Position.Y + placeableDef.SpriteHeight;
    }

    private float ResolveCreatureSortY(WorldEntityState entity)
    {
        var creatureDef = _runtime!.Content.Creatures.Get(entity.DefinitionId);
        return entity.Position.Y + creatureDef.SpriteHeight * 0.75f;
    }

    private int ResolveEntityBandZ(float sortY)
    {
        var sortTileY = (int)MathF.Floor(sortY / TileSize);
        var localSortTileY = sortTileY - GetVisibleSortOriginTileY();
        return EntityBandLayerZ + Math.Clamp(localSortTileY, 0, MaxEntitySortSpan);
    }

    private int GetVisibleSortOriginTileY()
    {
        var activeWorld = _worldFacade!.GetActiveWorld();
        if (activeWorld.LoadedChunks.Count == 0)
        {
            return 0;
        }

        var minChunkY = activeWorld.LoadedChunks.Keys.Min(coord => coord.Y);
        return minChunkY * _runtime!.ChunkHeight;
    }

    private static string ResolveFacing(NumericsVector2 movement)
    {
        if (MathF.Abs(movement.X) > MathF.Abs(movement.Y))
        {
            return movement.X > 0 ? "right" : "left";
        }

        return movement.Y > 0 ? "down" : "up";
    }

    private static Vector2 ToGodot(NumericsVector2 vector) => new(vector.X, vector.Y);

    private Node2D BuildChunkBounds(ChunkCoord coord)
    {
        var root = new Node2D
        {
            Name = $"ChunkBounds_{coord.X}_{coord.Y}",
            Visible = _showChunkBounds,
            ZIndex = ChunkBoundsLayerZ
        };
        var chunkPixelWidth = _runtime!.ChunkWidth * TileSize;
        var chunkPixelHeight = _runtime.ChunkHeight * TileSize;
        var line = new Line2D
        {
            Width = 2f,
            DefaultColor = new Color(0.2f, 0.9f, 1f, 0.85f),
            Closed = true
        };
        line.AddPoint(new Vector2(coord.X * chunkPixelWidth, coord.Y * chunkPixelHeight));
        line.AddPoint(new Vector2((coord.X + 1) * chunkPixelWidth, coord.Y * chunkPixelHeight));
        line.AddPoint(new Vector2((coord.X + 1) * chunkPixelWidth, (coord.Y + 1) * chunkPixelHeight));
        line.AddPoint(new Vector2(coord.X * chunkPixelWidth, (coord.Y + 1) * chunkPixelHeight));
        root.AddChild(line);
        return root;
    }

    private sealed record ChunkVisualState(
        Node2D TerrainRoot,
        Node2D RaisedFeatureRoot,
        Node2D EntityRoot,
        Node2D BoundsRoot,
        Dictionary<WorldTileCoord, Sprite2D> BaseTerrainSprites,
        Dictionary<WorldTileCoord, Sprite2D> CoverTerrainSprites,
        Dictionary<WorldTileCoord, Sprite2D> RaisedSprites)
    {
        public ChunkVisualState(Node2D terrainRoot, Node2D raisedFeatureRoot, Node2D entityRoot, Node2D boundsRoot)
            : this(terrainRoot, raisedFeatureRoot, entityRoot, boundsRoot, [], [], [])
        {
        }
    }

    private readonly record struct EntityVisualState(
        Texture2D? Texture,
        Vector2 Position,
        bool FlipH,
        Color Modulate,
        int ZIndex,
        bool OpenState)
    {
        public static EntityVisualState CreateUninitialized(bool openState)
        {
            return new EntityVisualState(null, new Vector2(float.NaN, float.NaN), false, new Color(0f, 0f, 0f, 0f), int.MinValue, openState);
        }

        public bool Matches(EntityVisualState other)
        {
            return ReferenceEquals(Texture, other.Texture)
                && Position == other.Position
                && FlipH == other.FlipH
                && Modulate == other.Modulate
                && ZIndex == other.ZIndex
                && OpenState == other.OpenState;
        }
    }
}
