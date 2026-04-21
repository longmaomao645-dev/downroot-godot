using System.Numerics;

namespace Downroot.Core.Input;

public sealed record InputFrame(
    Vector2 Movement,
    Vector2 PointerWorld,
    bool InteractPressed,
    bool DestroyHeld,
    bool PlacePressed,
    bool ZoomOutHeld,
    bool InventoryToggled,
    bool CraftPressed,
    bool ConsumePressed,
    int HotbarScrollDelta,
    int? DirectHotbarSlot);
