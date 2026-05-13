using Downroot.Core.Save;
using Downroot.Gameplay.Runtime;
using Downroot.Core.Ids;

namespace Downroot.Gameplay.Persistence;

public sealed class GameSaveSnapshotBuilder
{
    private readonly InventoryPersistenceAdapter _inventoryAdapter = new();
    private readonly WorldRuntimePersistenceAdapter _worldAdapter = new();

    public SaveGameData Build(GameRuntime runtime)
    {
        return new SaveGameData
        {
            SlotId = runtime.SaveSlotId ?? string.Empty,
            DisplayName = runtime.SaveDisplayName ?? "Quick Start",
            WorldSeed = runtime.WorldSeed,
            Mods = new ModSelectionData
            {
                EnabledPackIds = runtime.EnabledPackIds.ToArray()
            },
            ActiveWorldSpaceKind = runtime.ActiveWorldSpaceKind.ToString(),
            Player = new SavedPlayerData
            {
                PositionX = runtime.Player.Position.X,
                PositionY = runtime.Player.Position.Y,
                FacingX = runtime.Player.Facing.X,
                FacingY = runtime.Player.Facing.Y,
                Health = runtime.Player.Survival.Health,
                MaxHealth = runtime.Player.Survival.MaxHealth,
                Hunger = runtime.Player.Survival.Hunger,
                MaxHunger = runtime.Player.Survival.MaxHunger,
                SelectedHotbarIndex = runtime.Player.SelectedHotbarIndex,
                PrimaryBedEntityGuid = runtime.PrimaryBedEntityId?.Value.ToString("N"),
                InventorySlots = _inventoryAdapter.Export(runtime.Player.Inventory)
            },
            TimeOfDaySeconds = runtime.WorldState.TimeOfDaySeconds,
            TotalElapsedSeconds = runtime.WorldState.TotalElapsedSeconds,
            Worlds = BuildWorlds(runtime)
        };
    }

    private IReadOnlyList<SavedWorldRuntimeData> BuildWorlds(GameRuntime runtime)
    {
        var worlds = new List<SavedWorldRuntimeData>
        {
            _worldAdapter.Export(runtime.Overworld)
        };

        foreach (var pocketWorld in runtime.PocketWorlds.Values)
        {
            worlds.Add(_worldAdapter.Export(pocketWorld));
        }

        return worlds;
    }
}
