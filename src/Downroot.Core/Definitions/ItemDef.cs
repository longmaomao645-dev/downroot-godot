using Downroot.Core.Ids;

namespace Downroot.Core.Definitions;

public sealed record ItemDef(
    ContentId Id,
    string DisplayName,
    string SourcePackId,
    string IconPath,
    int IconWidth,
    int IconHeight,
    int MaxStack,
    ContentId? PlaceableId = null,
    int HungerRestore = 0,
    int HealthRestore = 0,
    ItemUseBehaviorKind UseBehavior = ItemUseBehaviorKind.None,
    HarvestToolDef? HarvestTool = null,
    MeleeWeaponDef? MeleeWeapon = null,
    ThrowableWeaponDef? ThrowableWeapon = null,
    float TreeBreakSpeedMultiplier = 1f,
    int MeleeDamage = 0,
    int IconAtlasColumn = 0,
    int IconAtlasRow = 0,
    float PoisonDuration = 0f,
    int PoisonDamagePerSecond = 0) : ContentDef(Id, DisplayName, SourcePackId);
