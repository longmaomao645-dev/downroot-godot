using Downroot.Core.Ids;
using Downroot.Core.Gameplay;
using Downroot.Core.World;

namespace Downroot.Gameplay.Runtime;

public sealed class WorldState
{
    private readonly List<WorldEntityState> _entities = [];
    private readonly EntityProjectionBuilder _projectionBuilder = new();
    private WorldSpaceKind _activeWorldSpaceKind = WorldSpaceKind.Overworld;

    // Active-world convenience projection for renderer, UI, and query services.
    public IReadOnlyList<WorldEntityState> Entities => _entities;
    public WorldSpaceKind ActiveWorldSpaceKind
    {
        get => _activeWorldSpaceKind;
        set
        {
            if (_activeWorldSpaceKind == value)
            {
                return;
            }

            _activeWorldSpaceKind = value;
            MarkEntityProjectionDirty();
            Lighting.SetActiveWorld(value);
        }
    }

    public required LoadedWorldState Overworld { get; init; }
    public Dictionary<string, LoadedWorldState> PocketWorlds { get; } = new();
    public string? ActivePocketWorldId { get; set; }
    public WorldTravelState Travel { get; } = new();
    public float TimeOfDaySeconds { get; set; }
    public float TotalElapsedSeconds { get; set; }
    public CraftWorkspaceMode WorkspaceMode { get; set; }
    public CraftingStationKind? ActiveStationKind { get; set; }
    public EntityId? ActiveStationEntityId { get; set; }
    public EntityId? ActiveStorageEntityId { get; set; }
    public InteractionContext? CurrentInteraction { get; set; }
    public StatusEventState? ActiveStatusEvent { get; private set; }
    public float ActiveStatusEventSeconds { get; private set; }
    public DestroyProgressState? ActiveDestroyProgress { get; set; }
    public FurnaceTaskState? ActiveFurnaceTask { get; set; }
    public float PlayerHitFlashSeconds { get; set; }
    public EntityId? PrimaryBedEntityId { get; set; }
    public LightingRuntimeState Lighting { get; } = new();
    public long EntityProjectionVersion { get; private set; }
    public bool IsEntityProjectionDirty { get; private set; } = true;
    public long EntityStateVersion { get; private set; }

    public bool IsNight(float dayLengthSeconds) => TimeOfDaySeconds >= dayLengthSeconds * 0.5f;

    public LoadedWorldState GetActiveWorld()
    {
        if (ActiveWorldSpaceKind == WorldSpaceKind.Overworld)
        {
            return Overworld;
        }

        if (ActivePocketWorldId is not null
            && PocketWorlds.TryGetValue(ActivePocketWorldId, out var pocketWorld))
        {
            return pocketWorld;
        }

        throw new InvalidOperationException($"No active pocket world. ActivePocketWorldId='{ActivePocketWorldId}'");
    }

    public void RefreshEntityProjection()
    {
        EnsureEntityProjectionCurrent();
    }

    public void RebuildEntityProjection(EntityProjectionBuilder builder)
    {
        _entities.Clear();
        _entities.AddRange(builder.Build(GetActiveWorld()));
        IsEntityProjectionDirty = false;
        EntityProjectionVersion++;
    }

    public void MarkEntityProjectionDirty()
    {
        IsEntityProjectionDirty = true;
    }

    public void NotifyEntityStateChanged()
    {
        EntityStateVersion++;
    }

    public void NotifyLightingStructureChanged()
    {
        Lighting.MarkStructureDirty();
    }

    public void NotifyLightingValueChanged(LightingFieldBounds? dirtyBounds = null)
    {
        Lighting.MarkValueDirty(dirtyBounds);
    }

    public bool EnsureEntityProjectionCurrent()
    {
        if (!IsEntityProjectionDirty)
        {
            return false;
        }

        RebuildEntityProjection(_projectionBuilder);
        return true;
    }

    public void SetStatusEvent(StatusEventState statusEvent, float seconds = 2f)
    {
        ActiveStatusEvent = statusEvent;
        ActiveStatusEventSeconds = seconds;
    }

    public void TickStatusEvent(float deltaSeconds)
    {
        PlayerHitFlashSeconds = Math.Max(0f, PlayerHitFlashSeconds - deltaSeconds);
        foreach (var entity in _entities)
        {
            entity.HitFlashSeconds = Math.Max(0f, entity.HitFlashSeconds - deltaSeconds);
        }

        if (ActiveStatusEventSeconds <= 0f)
        {
            return;
        }

        ActiveStatusEventSeconds = Math.Max(0f, ActiveStatusEventSeconds - deltaSeconds);
        if (ActiveStatusEventSeconds <= 0f)
        {
            ActiveStatusEvent = null;
        }
    }

    public bool RemoveDeleted()
    {
        var deleted = false;
        foreach (var world in EnumerateWorlds())
        {
            foreach (var chunk in world.LoadedChunks.Values)
            {
                foreach (var removedNatural in chunk.NaturalEntities.Values.Where(entity => entity.Removed).ToArray())
                {
                    deleted |= world.RemoveEntity(removedNatural);
                }

                foreach (var removedRuntime in chunk.RuntimeEntities.Values.Where(entity => entity.Removed).ToArray())
                {
                    deleted |= world.RemoveEntity(removedRuntime);
                }
            }
        }

        if (deleted)
        {
            MarkEntityProjectionDirty();
            EnsureEntityProjectionCurrent();
        }

        return deleted;
    }

    private IEnumerable<LoadedWorldState> EnumerateWorlds()
    {
        yield return Overworld;
        foreach (var pocketWorld in PocketWorlds.Values)
        {
            yield return pocketWorld;
        }
    }
}
