using Downroot.Core.Definitions;
using Downroot.Core.Diagnostics;
using Downroot.Core.Gameplay;
using Downroot.Core.Ids;
using Downroot.Core.Input;
using Downroot.Gameplay.Runtime.Systems;

namespace Downroot.Gameplay.Runtime;

public sealed class GameSimulation
{
    private const float AttackRange = 28f;
    private const int EmptyHandDamage = 1;
    private const float ThrowableHitRadius = 20f;
    private const float ThrowableTraceStep = 8f;

    private readonly GameRuntime _runtime;
    private readonly WorldRuntimeFacade _worldFacade;
    private readonly WorldQueryService _worldQuery;
    private readonly WorldStreamingSystem _worldStreamingSystem;
    private readonly MovementSystem _movementSystem;
    private readonly PortalTravelSystem _portalTravelSystem;
    private readonly InteractionSystem _interactionSystem;
    private readonly PlacementSystem _placementSystem;
    private readonly DestroySystem _destroySystem;
    private readonly CraftingSystem _craftingSystem;
    private readonly CreatureSystem _creatureSystem;
    private readonly PlaceableStateSystem _placeableStateSystem;
    private readonly LightingFieldSystem _lightingFieldSystem;
    private readonly DebugRuntimeState? _debugState;

    private bool _previousDestroyHeld;
    private bool _suppressDestroyUntilRelease;

    public GameSimulation(GameRuntime runtime, DebugRuntimeState? debugState = null)
    {
        _runtime = runtime;
        _debugState = debugState;
        _worldFacade = new WorldRuntimeFacade(runtime);
        _worldQuery = new WorldQueryService(runtime, _worldFacade);
        _worldStreamingSystem = new WorldStreamingSystem(runtime, _worldFacade);
        _movementSystem = new MovementSystem(runtime, _worldFacade);
        _portalTravelSystem = new PortalTravelSystem(runtime, _worldFacade, _worldStreamingSystem, _movementSystem);
        _interactionSystem = new InteractionSystem(runtime, _worldFacade, _worldQuery, _portalTravelSystem);
        _placementSystem = new PlacementSystem(runtime, _worldFacade, _worldQuery, _movementSystem);
        _destroySystem = new DestroySystem(runtime, _worldFacade, _worldQuery);
        _craftingSystem = new CraftingSystem(runtime, _worldQuery);
        _creatureSystem = new CreatureSystem(runtime, _worldQuery, _movementSystem, DamagePlayer);
        _placeableStateSystem = new PlaceableStateSystem(runtime, _worldFacade);
        _lightingFieldSystem = new LightingFieldSystem(runtime, _worldFacade);
    }

    public void Tick(float deltaSeconds, InputFrame input)
    {
        using var tickScope = RuntimeProfiler.Measure("GameSimulation.Tick");
        if (!input.DestroyHeld)
        {
            _suppressDestroyUntilRelease = false;
        }

        using (RuntimeProfiler.Measure("GameSimulation.Status"))
        {
            _runtime.WorldState.TickStatusEvent(deltaSeconds);
        }

        using (RuntimeProfiler.Measure("GameSimulation.WorldTime"))
        {
            UpdateWorldTime(deltaSeconds);
            _lightingFieldSystem.UpdateSkylightValue();
        }

        _placeableStateSystem.Update(deltaSeconds);
        _lightingFieldSystem.Update();

        if (_runtime.WorldState.Travel.IsActive)
        {
            using (RuntimeProfiler.Measure("GameSimulation.Travel"))
            {
                _portalTravelSystem.TickTravel(deltaSeconds);
            }

            using (RuntimeProfiler.Measure("GameSimulation.RemoveDeleted"))
            {
                if (_worldFacade.RemoveDeleted())
                {
                    _runtime.WorldState.NotifyLightingStructureChanged();
                }
            }

            _previousDestroyHeld = input.DestroyHeld;
            return;
        }

        using (RuntimeProfiler.Measure("GameSimulation.Streaming"))
        {
            _worldStreamingSystem.UpdateLoadedChunks();
        }

        using (RuntimeProfiler.Measure("GameSimulation.ProjectionEnsureA"))
        {
            _worldFacade.EnsureEntityProjectionCurrent();
        }

        using (RuntimeProfiler.Measure("GameSimulation.ValidateStation"))
        {
            _interactionSystem.ValidateActiveStation();
        }

        using (RuntimeProfiler.Measure("GameSimulation.PlayerMove"))
        {
            _movementSystem.UpdatePlayerMovement(deltaSeconds, input.Movement);
        }

        using (RuntimeProfiler.Measure("GameSimulation.Hotbar"))
        {
            UpdateHotbarSelection(input);
        }

        using (RuntimeProfiler.Measure("GameSimulation.FurnaceTick"))
        {
            _craftingSystem.UpdateFurnaceTask(deltaSeconds);
        }

        using (RuntimeProfiler.Measure("GameSimulation.InteractionContextA"))
        {
            _interactionSystem.UpdateInteractionContext();
        }

        using (RuntimeProfiler.Measure("GameSimulation.Toggles"))
        {
            HandleToggles(input);
        }

        using (RuntimeProfiler.Measure("GameSimulation.Interact"))
        {
            _interactionSystem.HandleInteract(input);
        }

        var selectedItemDef = GetSelectedItemDef();
        var nearestCreature = _creatureSystem.GetNearestCreature(AttackRange);
        using (RuntimeProfiler.Measure("GameSimulation.Attack"))
        {
            HandleAttack(input, selectedItemDef, nearestCreature);
        }

        using (RuntimeProfiler.Measure("GameSimulation.Consume"))
        {
            HandleConsumption(input);
        }

        var handledThrowable = false;
        using (RuntimeProfiler.Measure("GameSimulation.Throw"))
        {
            handledThrowable = HandleThrowableUse(input, selectedItemDef);
        }

        using (RuntimeProfiler.Measure("GameSimulation.Place"))
        {
            if (!handledThrowable)
            {
                _placementSystem.HandlePlacement(input);
            }
        }

        using (RuntimeProfiler.Measure("GameSimulation.Destroy"))
        {
            _destroySystem.HandleDestroy(
                deltaSeconds,
                input,
                _suppressDestroyUntilRelease,
                selectedItemDef?.MeleeWeapon is not null && nearestCreature is not null,
                selectedItemDef,
                _debugState?.FastBreak ?? false);
        }

        using (RuntimeProfiler.Measure("GameSimulation.Creatures"))
        {
            _creatureSystem.UpdateCreatures(deltaSeconds);
        }

        using (RuntimeProfiler.Measure("GameSimulation.ReassignRuntimeEntities"))
        {
            _worldStreamingSystem.ReassignRuntimeEntities();
        }

        using (RuntimeProfiler.Measure("GameSimulation.ProjectionEnsureB"))
        {
            _worldFacade.EnsureEntityProjectionCurrent();
        }

        using (RuntimeProfiler.Measure("GameSimulation.InteractionContextB"))
        {
            _interactionSystem.UpdateInteractionContext();
        }

        using (RuntimeProfiler.Measure("GameSimulation.RemoveDeleted"))
        {
            if (_runtime.WorldState.RemoveDeleted())
            {
                _runtime.WorldState.NotifyLightingStructureChanged();
            }
        }

        _previousDestroyHeld = input.DestroyHeld;
    }

    public IReadOnlyList<RecipeDef> GetRecipesForWorkspace(CraftWorkspaceMode workspaceMode) => _craftingSystem.GetRecipesForWorkspace(workspaceMode);

    public bool Craft(ContentId recipeId) => _craftingSystem.Craft(recipeId);

    public bool TryCraft(ContentId recipeId, out string failureReason) => _craftingSystem.TryCraft(recipeId, out failureReason);

    public void MoveInventorySlotToSelectedHotbar(int inventoryIndex)
    {
        if (inventoryIndex < 0 || inventoryIndex >= _runtime.Player.Inventory.Slots.Count)
        {
            return;
        }

        var selectedHotbarIndex = _runtime.Player.SelectedHotbarIndex;
        if (selectedHotbarIndex < 0 || selectedHotbarIndex >= _runtime.Player.HotbarSize)
        {
            return;
        }

        if (inventoryIndex == selectedHotbarIndex)
        {
            return;
        }

        var inventorySlot = _runtime.Player.Inventory.Slots[inventoryIndex];
        if (inventorySlot.IsEmpty)
        {
            return;
        }

        var hotbarSlot = _runtime.Player.Inventory.Slots[selectedHotbarIndex];
        var hotbarItemId = hotbarSlot.ItemId;
        var hotbarQuantity = hotbarSlot.Quantity;

        hotbarSlot.Set(inventorySlot.ItemId!.Value, inventorySlot.Quantity);
        if (hotbarItemId is null || hotbarQuantity <= 0)
        {
            inventorySlot.Clear();
            return;
        }

        inventorySlot.Set(hotbarItemId.Value, hotbarQuantity);
    }

    public void MoveInventorySlotToStorage(int inventoryIndex)
    {
        if (_runtime.WorldState.ActiveStorageEntityId is not { } storageId
            || !_worldQuery.TryGetActiveEntity(storageId, out var storageEntity)
            || storageEntity.StorageInventory is null)
        {
            return;
        }

        if (_runtime.Player.Inventory.TryMoveSlotTo(inventoryIndex, storageEntity.StorageInventory, _runtime.Content))
        {
            _worldFacade.NotifyEntityStateChanged(storageEntity);
        }
    }

    public void MoveStorageSlotToInventory(int storageIndex)
    {
        if (_runtime.WorldState.ActiveStorageEntityId is not { } storageId
            || !_worldQuery.TryGetActiveEntity(storageId, out var storageEntity)
            || storageEntity.StorageInventory is null)
        {
            return;
        }

        if (storageEntity.StorageInventory.TryMoveSlotTo(storageIndex, _runtime.Player.Inventory, _runtime.Content))
        {
            _worldFacade.NotifyEntityStateChanged(storageEntity);
        }
    }

    private void UpdateHotbarSelection(InputFrame input)
    {
        if (input.DirectHotbarSlot is { } directIndex)
        {
            _runtime.Player.SelectedHotbarIndex = Math.Clamp(directIndex, 0, _runtime.Player.HotbarSize - 1);
        }

        if (input.HotbarScrollDelta != 0)
        {
            var next = _runtime.Player.SelectedHotbarIndex + input.HotbarScrollDelta;
            _runtime.Player.SelectedHotbarIndex = ((next % _runtime.Player.HotbarSize) + _runtime.Player.HotbarSize) % _runtime.Player.HotbarSize;
        }
    }

    private void UpdateWorldTime(float deltaSeconds)
    {
        _runtime.WorldState.TotalElapsedSeconds += deltaSeconds;
        _runtime.WorldState.TimeOfDaySeconds += deltaSeconds;

        if (_runtime.WorldState.TimeOfDaySeconds >= _runtime.BootstrapConfig.DayLengthSeconds)
        {
            _runtime.WorldState.TimeOfDaySeconds -= _runtime.BootstrapConfig.DayLengthSeconds;
        }

        if (_runtime.WorldState.TotalElapsedSeconds % 3f < deltaSeconds)
        {
            var hungerDrain = 1;

            if (_runtime.Player.Survival.Hunger > _runtime.Player.Survival.MaxHunger * 0.3f)
            {
                _runtime.Player.Survival.Heal(1);
                hungerDrain = 2;
            }

            _runtime.Player.Survival.DrainHunger(hungerDrain);
            if (_runtime.Player.Survival.Hunger == 0)
            {
                DamagePlayer(1);
            }
        }

        if (_runtime.Player.IsPoisoned)
        {
            _runtime.Player.PoisonRemainingSeconds -= deltaSeconds;
            if (_runtime.Player.PoisonRemainingSeconds <= 0)
            {
                _runtime.Player.PoisonRemainingSeconds = 0;
            }

            if (_runtime.WorldState.TotalElapsedSeconds % 1f < deltaSeconds)
            {
                DamagePlayer(_runtime.Player.PoisonDamagePerSecond);
            }
        }
    }

    private void HandleToggles(InputFrame input)
    {
        if (!input.CraftPressed)
        {
            return;
        }

        if (_runtime.WorldState.WorkspaceMode != CraftWorkspaceMode.Hidden)
        {
            _runtime.WorldState.WorkspaceMode = CraftWorkspaceMode.Hidden;
            _runtime.WorldState.ActiveStationKind = null;
            _runtime.WorldState.ActiveStationEntityId = null;
            return;
        }

        if (_interactionSystem.TryGetNearbyWorkbenchStation(out var station))
        {
            var stationKind = _worldFacade.GetEffectiveCraftingStationKind(station);
            _runtime.WorldState.ActiveStationKind = stationKind;
            _runtime.WorldState.ActiveStationEntityId = station.Id;
            _runtime.WorldState.WorkspaceMode = stationKind == CraftingStationKind.WeaponsBench
                ? CraftWorkspaceMode.WeaponsBench
                : CraftWorkspaceMode.Workbench;
            return;
        }

        _runtime.WorldState.WorkspaceMode = CraftWorkspaceMode.Handcraft;
    }

    private void HandleConsumption(InputFrame input)
    {
        if (!input.ConsumePressed)
        {
            return;
        }

        var slot = _runtime.Player.Inventory.Slots[_runtime.Player.SelectedHotbarIndex];
        if (slot.ItemId is null || !_runtime.Content.Items.TryGet(slot.ItemId.Value, out var itemDef))
        {
            return;
        }

        if (itemDef!.HungerRestore <= 0 && itemDef.HealthRestore <= 0)
        {
            return;
        }

        slot.Remove(1);
        if (itemDef.HungerRestore > 0)
        {
            _runtime.Player.Survival.RestoreHunger(itemDef.HungerRestore);
        }

        if (itemDef.HealthRestore > 0)
        {
            _runtime.Player.Survival.Heal(itemDef.HealthRestore);
        }

        if (itemDef.PoisonDuration > 0)
        {
            _runtime.Player.PoisonRemainingSeconds = itemDef.PoisonDuration;
            _runtime.Player.PoisonDamagePerSecond = itemDef.PoisonDamagePerSecond;
        }
    }

    private void HandleAttack(InputFrame input, ItemDef? selectedItem, WorldEntityState? target)
    {
        var attackPressed = input.DestroyHeld && !_previousDestroyHeld;
        if (!attackPressed)
        {
            return;
        }

        if (target is null)
        {
            return;
        }

        var damage = selectedItem?.MeleeWeapon is not null
            ? selectedItem.MeleeWeapon.Damage
            : EmptyHandDamage;
        _creatureSystem.DamageCreature(target, damage);
        _suppressDestroyUntilRelease = true;
    }

    private bool HandleThrowableUse(InputFrame input, ItemDef? selectedItem)
    {
        if (!input.PlacePressed || selectedItem?.ThrowableWeapon is null)
        {
            return false;
        }

        var slot = _runtime.Player.Inventory.Slots[_runtime.Player.SelectedHotbarIndex];
        if (slot.ItemId is null || slot.Quantity <= 0)
        {
            return false;
        }

        var direction = input.PointerWorld - _runtime.Player.Position;
        if (direction == System.Numerics.Vector2.Zero)
        {
            direction = _runtime.Player.Facing;
        }

        direction = MovementSystem.NormalizeMovement(direction);
        if (direction == System.Numerics.Vector2.Zero)
        {
            return false;
        }

        var throwable = selectedItem.ThrowableWeapon;
        var maxThrowDistance = MathF.Min(throwable.ThrowSpeed, throwable.MaxThrowRangeTiles * 32f);
        var trace = TraceThrowablePath(_runtime.Player.Position, direction, maxThrowDistance, throwable.RequiresLineOfSight);
        var landingPosition = trace.LandingPosition;
        var hitCreature = FindThrowableTarget(_runtime.Player.Position, landingPosition, throwable.RequiresLineOfSight);
        if (hitCreature is not null)
        {
            _creatureSystem.DamageCreature(hitCreature, throwable.Damage);
            landingPosition = hitCreature.Position;
        }

        var dropPosition = landingPosition;
        var dropTile = _runtime.GetWorldTile(dropPosition);
        _worldFacade.AddRuntimeEntity(_runtime.ActiveWorldSpaceKind, new WorldEntityState(
            WorldEntityKind.ItemDrop,
            throwable.RecoverAsItemId,
            _runtime.GetWorldPosition(dropTile),
            1,
            _runtime.ActiveWorldSpaceKind,
            dropTile.ToChunkCoord(_runtime.ChunkWidth, _runtime.ChunkHeight),
            stackCount: 1));
        slot.Remove(1);
        return true;
    }

    private WorldEntityState? FindThrowableTarget(System.Numerics.Vector2 origin, System.Numerics.Vector2 landingPosition, bool requiresLineOfSight)
    {
        var throwVector = landingPosition - origin;
        var throwLengthSquared = throwVector.LengthSquared();
        if (throwLengthSquared <= 0f)
        {
            return null;
        }

        WorldEntityState? best = null;
        var bestAlong = float.MaxValue;
        foreach (var entity in _worldQuery.GetActiveEntities())
        {
            if (entity.Kind != WorldEntityKind.Creature || entity.Removed)
            {
                continue;
            }

            var relative = entity.Position - origin;
            var along = System.Numerics.Vector2.Dot(relative, throwVector) / throwLengthSquared;
            if (along < 0f || along > 1f)
            {
                continue;
            }

            var projected = origin + throwVector * along;
            var distanceToPath = System.Numerics.Vector2.Distance(entity.Position, projected);
            if (distanceToPath > ThrowableHitRadius || along >= bestAlong)
            {
                continue;
            }

            if (requiresLineOfSight && !HasThrowableLineOfSight(origin, entity.Position))
            {
                continue;
            }

            best = entity;
            bestAlong = along;
        }

        return best;
    }

    private (System.Numerics.Vector2 LandingPosition, bool Blocked) TraceThrowablePath(System.Numerics.Vector2 origin, System.Numerics.Vector2 direction, float maxDistance, bool requiresLineOfSight)
    {
        var travelled = 0f;
        var lastFree = origin;
        while (travelled < maxDistance)
        {
            travelled = MathF.Min(maxDistance, travelled + ThrowableTraceStep);
            var sample = _movementSystem.ClampToWorldBounds(origin + direction * travelled);
            if (requiresLineOfSight && _movementSystem.IsBlocked(sample))
            {
                return (lastFree, true);
            }

            lastFree = sample;
        }

        return (lastFree, false);
    }

    private bool HasThrowableLineOfSight(System.Numerics.Vector2 origin, System.Numerics.Vector2 target)
    {
        var vector = target - origin;
        var distance = vector.Length();
        if (distance <= ThrowableTraceStep)
        {
            return true;
        }

        var direction = MovementSystem.NormalizeMovement(vector);
        var travelled = ThrowableTraceStep;
        while (travelled < distance)
        {
            var sample = _movementSystem.ClampToWorldBounds(origin + direction * travelled);
            if (_movementSystem.IsBlocked(sample))
            {
                return false;
            }

            travelled += ThrowableTraceStep;
        }

        return true;
    }

    private ItemDef? GetSelectedItemDef()
    {
        var slot = _runtime.Player.Inventory.Slots[_runtime.Player.SelectedHotbarIndex];
        if (slot.ItemId is null || !_runtime.Content.Items.TryGet(slot.ItemId.Value, out var item))
        {
            return null;
        }

        return item;
    }

    private void DamagePlayer(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (_debugState?.GodMode == true)
        {
            return;
        }

        _runtime.Player.Survival.Damage(amount);
        _runtime.WorldState.PlayerHitFlashSeconds = 0.18f;
    }
}
