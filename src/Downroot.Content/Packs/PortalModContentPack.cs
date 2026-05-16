using Downroot.Core.Content;
using Downroot.Core.Definitions;
using Downroot.Core.Gameplay;
using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.Content.Packs;

public sealed class PortalModContentPack : IContentPack
{
    public const string Id = "portalmod";

    public ContentPackManifest Manifest { get; } = new()
    {
        PackId = Id,
        DisplayName = "Portal Mod",
        Version = "0.1.0",
        Description = "Adds world portals and dim shard pocket worlds.",
        IsBuiltIn = true,
        Dependencies = [BaseGameContentPack.Id]
    };

    public string PackId => Manifest.PackId;

    public void Register(IContentRegistrar registrar)
    {
        var dimfragId = new ContentId("portalmod:dimfrag");
        var portalPlaceableId = new ContentId("portalmod:portal");
        var frostcoreItemId = new ContentId("portalmod:frostcore_item");
        var frostcoreRaisedId = new ContentId("portalmod:frostcore_raised");
        var iceCrystalItemId = new ContentId("basegame:ice_crystal");

        registrar.RegisterTerrain(new TerrainDef(dimfragId, "Dimfrag", PackId, "packs/portalmod/assets/world/terrain/ground/dimfrag.png", 32, 32, 0, 0));
        registrar.RegisterPlaceable(new PlaceableDef(
            portalPlaceableId,
            "Portal",
            PackId,
            "packs/portalmod/assets/world/ruins/portal.png",
            32,
            32,
            0,
            0,
            999,
            false,
            null,
            false,
            false,
            0,
            0,
            false,
            false,
            false,
            0,
            false,
            PlaceableBehaviorKind.None,
            new LightEmitterDef(true, 6f, 1f, 0.52f, 0.88f, 1f, LightFlickerKind.Portal, LightPresentationKind.Portal)));
        registrar.RegisterItem(new ItemDef(frostcoreItemId, "Frostcore", PackId, "packs/portalmod/assets/items/resources/frostcore_item.png", 16, 16, 32));
        registrar.RegisterRaisedFeature(new RaisedFeatureDef(frostcoreRaisedId, "Frostcore", PackId, "packs/portalmod/assets/world/nature/ores/frostcore.png", 32, 32, 13, 5, [new ItemAmount(frostcoreItemId, 1)]));
        registrar.RegisterRaisedOreFieldRule(new RaisedOreFieldRuleDef(frostcoreRaisedId, WorldSpaceKind.DimShardPocket, SurfaceRegions.DimShardField, 0.24f, false, [frostcoreRaisedId]));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("portalmod:smelt_frostcore"), "Ice Crystal", PackId, [new ItemAmount(frostcoreItemId, 1)], new ItemAmount(iceCrystalItemId, 2), CraftingStationKind.Furnace, CraftDurationSeconds: 3.5f));

        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("portalmod:portal-site"), WorldGenPassTypes.PortalSite, portalPlaceableId, WorldSpaceKind.Overworld, SpawnChance: 0.20f, MinChunkSpacing: 3));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("portalmod:dim-fill"), WorldGenPassTypes.FillTerrain, dimfragId, WorldSpaceKind.DimShardPocket, PrimarySurfaceRegion: SurfaceRegions.DimShardField));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("portalmod:frostcore_raised"), WorldGenPassTypes.RaisedOreField, frostcoreRaisedId, WorldSpaceKind.DimShardPocket));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("portalmod:dim-rock-outcrop"), WorldGenPassTypes.RockOutcrop, new ContentId("basegame:rock_outcrop"), WorldSpaceKind.DimShardPocket));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("portalmod:dim-portal-site"), WorldGenPassTypes.PortalSite, portalPlaceableId, WorldSpaceKind.DimShardPocket, SpawnChance: 1.0f, RequiredChunkCoord: new ChunkCoord(0, 0)));
    }
}
