using System.Numerics;
using Downroot.Core.Ids;

namespace Downroot.Gameplay.Runtime;

public sealed class PlayerState
{
    public PlayerState(int inventorySize, int hotbarSize, SurvivalState survival)
    {
        Id = EntityId.New();
        Inventory = new InventoryState(inventorySize);
        HotbarSize = hotbarSize;
        Survival = survival;
    }

    public EntityId Id { get; }
    public Vector2 Position { get; set; }
    public Vector2 Facing { get; set; } = Vector2.UnitY;
    public float Speed { get; set; } = 140f;
    public int HotbarSize { get; }
    public int SelectedHotbarIndex { get; set; }
    public InventoryState Inventory { get; }
    public SurvivalState Survival { get; }
    public float PoisonRemainingSeconds { get; set; }
    public int PoisonDamagePerSecond { get; set; }
    public bool IsPoisoned => PoisonRemainingSeconds > 0;
}
