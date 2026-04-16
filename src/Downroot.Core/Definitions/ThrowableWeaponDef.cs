using Downroot.Core.Ids;

namespace Downroot.Core.Definitions;

public sealed record ThrowableWeaponDef(
    int Damage,
    float ThrowSpeed,
    int MaxThrowRangeTiles,
    ContentId RecoverAsItemId,
    bool RequiresLineOfSight = true);
