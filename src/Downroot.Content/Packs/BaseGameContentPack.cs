using Downroot.Core.Content;
using Downroot.Core.Definitions;
using Downroot.Core.Gameplay;
using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.Content.Packs;

public sealed class BaseGameContentPack : IContentPack
{
    public const string Id = "basegame";

    public ContentPackManifest Manifest { get; } = new()
    {
        PackId = Id,
        DisplayName = "Base Game",
        Version = "1.0.0",
        Description = "Core survival, building, crafting, and overworld content.",
        IsBuiltIn = true,
        Dependencies = []
    };

    public string PackId => Manifest.PackId;

    public void Register(IContentRegistrar registrar)
    {
        var grassId = new ContentId("basegame:grass");
        var dirtId = new ContentId("basegame:dirt");
        var mountainId = new ContentId("basegame:mountain");
        var riverWaterId = new ContentId("basegame:river_water");

        var logItemId = new ContentId("basegame:log");
        var stoneItemId = new ContentId("basegame:stone");
        var blueberryItemId = new ContentId("basegame:blueberry");
        var voiditeItemId = new ContentId("basegame:voidite");
        var goldveinItemId = new ContentId("basegame:goldvein");
        var venomiteItemId = new ContentId("basegame:venomite");
        var furnaceItemId = new ContentId("basegame:furnace_item");
        var voidCrystalItemId = new ContentId("basegame:void_crystal");
        var goldIngotItemId = new ContentId("basegame:gold_ingot");
        var poisonCrystalItemId = new ContentId("basegame:poison_crystal");
        var ironIngotItemId = new ContentId("basegame:iron_ingot");
        var sandItemId = new ContentId("basegame:sand");
        var siliconWaferItemId = new ContentId("basegame:silicon_wafer");
        var iceCrystalItemId = new ContentId("basegame:ice_crystal");
        var axeItemId = new ContentId("basegame:axe");
        var ironKnifeItemId = new ContentId("basegame:iron_knife");
        var woodSpearItemId = new ContentId("basegame:wood_spear");
        var stoneWallItemId = new ContentId("basegame:stone_wall_item");
        var stoneFloorItemId = new ContentId("basegame:stone_floor_item");
        var workbenchItemId = new ContentId("basegame:workbench_item");
        var torchItemId = new ContentId("basegame:torch");
        var chestItemId = new ContentId("basegame:wooden_chest_item");
        var doorItemId = new ContentId("basegame:wooden_door_item");
        var fenceItemId = new ContentId("basegame:wooden_fence_item");
        var bedItemId = new ContentId("basegame:bed_item");
        var roofPanelItemId = new ContentId("basegame:roof_panel_item");
        var workbenchUpgradeItemId = new ContentId("basegame:upgrade_workbench_weapons_bench");

        var furnacePlaceableId = new ContentId("basegame:furnace");
        var stoneWallPlaceableId = new ContentId("basegame:stone_wall");
        var stoneFloorPlaceableId = new ContentId("basegame:stone_floor");
        var workbenchPlaceableId = new ContentId("basegame:workbench");
        var torchPlaceableId = new ContentId("basegame:torch_placeable");
        var chestPlaceableId = new ContentId("basegame:wooden_chest");
        var doorPlaceableId = new ContentId("basegame:wooden_door");
        var fencePlaceableId = new ContentId("basegame:wooden_fence");
        var bedPlaceableId = new ContentId("basegame:bed_placeable");
        var roofPanelPlaceableId = new ContentId("basegame:roof_panel_placeable");

        var playerId = new ContentId("basegame:player_human");
        var wormId = new ContentId("basegame:worm");
        var cockroachId = new ContentId("basegame:cockroach");

        var brightTreeNodeId = new ContentId("basegame:tree_bright");
        var lushTreeNodeId = new ContentId("basegame:tree_lush");
        var roundTreeNodeId = new ContentId("basegame:tree_round");
        var roundBushTreeNodeId = new ContentId("basegame:tree_round_bush");
        var lightTrunkTreeNodeId = new ContentId("basegame:tree_light_trunk");
        var pineTreeNodeId = new ContentId("basegame:tree_dark_pine");
        var blossomTreeNodeId = new ContentId("basegame:tree_pink_blossom");
        var autumnTreeNodeId = new ContentId("basegame:tree_red_autumn");
        var deadBrownTreeNodeId = new ContentId("basegame:tree_dead_brown");
        var deadBlackTreeNodeId = new ContentId("basegame:tree_dead_black");
        var youngGreenSplitTreeNodeId = new ContentId("basegame:tree_young_green_split");
        var greenSplitTreeNodeId = new ContentId("basegame:tree_green_split");
        var largeGreenSplitTreeNodeId = new ContentId("basegame:tree_large_green_split");
        var smallPineSplitTreeNodeId = new ContentId("basegame:tree_small_pine_split");
        var pineSplitTreeNodeId = new ContentId("basegame:tree_pine_split");
        var largePineSplitTreeNodeId = new ContentId("basegame:tree_large_pine_split");
        var smallSnowPineSplitTreeNodeId = new ContentId("basegame:tree_small_snow_pine_split");
        var snowPineSplitTreeNodeId = new ContentId("basegame:tree_snow_pine_split");
        var largeSnowPineSplitTreeNodeId = new ContentId("basegame:tree_large_snow_pine_split");
        var stoneNodeId = new ContentId("basegame:stone_node");
        var blueberryNodeId = new ContentId("basegame:blueberry_bush");
        var rockOutcropNodeId = new ContentId("basegame:rock_outcrop");
        var voiditeRaisedId = new ContentId("basegame:voidite_raised");
        var goldveinRaisedId = new ContentId("basegame:goldvein_raised");
        var venomiteRaisedId = new ContentId("basegame:venomite_raised");

        registrar.RegisterTerrain(new TerrainDef(grassId, "Grass", PackId, "packs/basegame/assets/world/terrain/ground/grass_dualgrid.png", 32, 32, 2, 1));
        registrar.RegisterTerrain(new TerrainDef(dirtId, "Dirt", PackId, "packs/basegame/assets/world/terrain/ground/dirt_dualgrid.png", 32, 32, 2, 1, 4, 4));
        registrar.RegisterTerrain(new TerrainDef(mountainId, "Mountain", PackId, "packs/basegame/assets/world/nature/rocks/stone.png", 32, 32, 0, 0));
        registrar.RegisterTerrain(new TerrainDef(riverWaterId, "River Water", PackId, "packs/basegame/assets/world/terrain/ground/water_dualgrid.png", 32, 32, 2, 1));

        registrar.RegisterPlaceable(new PlaceableDef(furnacePlaceableId, "Furnace", PackId, "packs/basegame/assets/production/utility/furnace.png", 32, 32, 0, 0, 5, true, CraftingStationKind.Furnace, true, Behaviors: PlaceableBehaviorKind.CraftingStation));
        registrar.RegisterPlaceable(new PlaceableDef(stoneWallPlaceableId, "Stone Wall", PackId, "packs/basegame/assets/structures/walls/stone_wall.png", 32, 32, 0, 0, 5, false, null, true, LightOccluder: new LightOccluderDef(true, LightingFootprintKind.Tile)));
        registrar.RegisterPlaceable(new PlaceableDef(stoneFloorPlaceableId, "Stone Floor", PackId, "packs/basegame/assets/world/terrain/floors/stone_floor.png", 32, 32, 0, 0, 2, false, null, false, false, 0, 0, false, true));
        registrar.RegisterPlaceable(new PlaceableDef(workbenchPlaceableId, "Workbench", PackId, "packs/basegame/assets/production/workstations/workbench.png", 28, 32, 0, 0, 3, true, CraftingStationKind.Workbench, true, Behaviors: PlaceableBehaviorKind.CraftingStation));
        registrar.RegisterPlaceable(new PlaceableDef(
            torchPlaceableId,
            "Torch",
            PackId,
            "packs/basegame/assets/items/torch.png",
            16,
            16,
            0,
            0,
            1,
            Behaviors: PlaceableBehaviorKind.LightSource,
            LightEmitter: new LightEmitterDef(true, 4f, 1f, 1f, 0.82f, 0.48f, LightFlickerKind.Torch, LightPresentationKind.Torch)));
        registrar.RegisterPlaceable(new PlaceableDef(chestPlaceableId, "Wooden Chest", PackId, "packs/basegame/assets/production/storage/wooden_chest.png", 32, 32, 0, 0, 3, false, null, true, true, 1, 0, true, false, true, 16, false, PlaceableBehaviorKind.Storage));
        registrar.RegisterPlaceable(new PlaceableDef(doorPlaceableId, "Wooden Door", PackId, "packs/basegame/assets/structures/doors/wood_door_close_open.png", 32, 32, 0, 0, 3, false, null, true, true, 1, 0, false, Behaviors: PlaceableBehaviorKind.Door));
        registrar.RegisterPlaceable(new PlaceableDef(fencePlaceableId, "Wooden Fence", PackId, "packs/basegame/assets/structures/fences/wood_fence_horizontal.png", 32, 32, 0, 0, 2, false, null, true, false, 0, 0, false, false, true, 0, true));
        registrar.RegisterPlaceable(new PlaceableDef(bedPlaceableId, "Bed", PackId, "packs/basegame/assets/furniture/beds/bed.png", 32, 32, 0, 0, 3, false, null, true, false, 0, 0, false, false, true, 0, false, PlaceableBehaviorKind.Bed));
        registrar.RegisterPlaceable(new PlaceableDef(
            roofPanelPlaceableId,
            "Roof Panel",
            PackId,
            "packs/basegame/assets/world/terrain/floors/stone_floor.png",
            32,
            32,
            0,
            0,
            2,
            false,
            null,
            false,
            false,
            0,
            0,
            false,
            false,
            true,
            0,
            false,
            PlaceableBehaviorKind.None,
            null,
            null,
            new SkylightMaskDef(true, LightingFootprintKind.Tile)));

        registrar.RegisterItem(new ItemDef(logItemId, "Log", PackId, "packs/basegame/assets/items/log_item.png", 28, 32, 99));
        registrar.RegisterItem(new ItemDef(stoneItemId, "Stone", PackId, "packs/basegame/assets/items/stone_item.png", 16, 16, 99));
        registrar.RegisterItem(new ItemDef(blueberryItemId, "Blueberry", PackId, "packs/basegame/assets/world/nature/plants/blueberry_bush.png", 16, 16, 20, null, 20));
        registrar.RegisterItem(new ItemDef(voiditeItemId, "Voidite", PackId, "packs/basegame/assets/items/resources/voidite_item.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(goldveinItemId, "Goldvein", PackId, "packs/basegame/assets/items/resources/goldvein_item.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(venomiteItemId, "Venomite", PackId, "packs/basegame/assets/items/resources/venomite_item.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(furnaceItemId, "Furnace", PackId, "packs/basegame/assets/items/resources/furnace_item.png", 16, 16, 8, furnacePlaceableId));
        registrar.RegisterItem(new ItemDef(voidCrystalItemId, "Void Crystal", PackId, "packs/basegame/assets/items/resources/void_crystal.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(goldIngotItemId, "Gold Ingot", PackId, "packs/basegame/assets/items/resources/gold_ingot.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(poisonCrystalItemId, "Poison Crystal", PackId, "packs/basegame/assets/items/resources/poison_crystal.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(ironIngotItemId, "Iron Ingot", PackId, "packs/basegame/assets/items/resources/iron_ingot.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(sandItemId, "Sand", PackId, "packs/basegame/assets/items/resources/sand.png", 16, 16, 99));
        registrar.RegisterItem(new ItemDef(siliconWaferItemId, "Silicon Wafer", PackId, "packs/basegame/assets/items/resources/silicon_wafer.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(iceCrystalItemId, "Ice Crystal", PackId, "packs/basegame/assets/items/resources/ice_crystal.png", 16, 16, 32));
        registrar.RegisterItem(new ItemDef(
            axeItemId,
            "Axe",
            PackId,
            "packs/basegame/assets/items/tools/axe.png",
            16,
            16,
            1,
            UseBehavior: ItemUseBehaviorKind.MeleeWeapon,
            HarvestTool: new HarvestToolDef(2f),
            MeleeWeapon: new MeleeWeaponDef(2)));
        registrar.RegisterItem(new ItemDef(
            ironKnifeItemId,
            "Iron Knife",
            PackId,
            "packs/basegame/assets/items/weapons/iron_knife.png",
            16,
            16,
            1,
            UseBehavior: ItemUseBehaviorKind.MeleeWeapon,
            MeleeWeapon: new MeleeWeaponDef(3)));
        registrar.RegisterItem(new ItemDef(
            woodSpearItemId,
            "Wood Spear",
            PackId,
            "packs/basegame/assets/items/weapons/wood_spear.png",
            16,
            16,
            1,
            UseBehavior: ItemUseBehaviorKind.ThrowableWeapon,
            ThrowableWeapon: new ThrowableWeaponDef(3, 176f, 6, woodSpearItemId, true)));
        registrar.RegisterItem(new ItemDef(stoneWallItemId, "Stone Wall", PackId, "packs/basegame/assets/structures/walls/stone_wall.png", 32, 32, 32, stoneWallPlaceableId));
        registrar.RegisterItem(new ItemDef(stoneFloorItemId, "Stone Floor", PackId, "packs/basegame/assets/world/terrain/floors/stone_floor.png", 32, 32, 64, stoneFloorPlaceableId));
        registrar.RegisterItem(new ItemDef(workbenchItemId, "Workbench", PackId, "packs/basegame/assets/production/workstations/workbench.png", 28, 32, 8, workbenchPlaceableId));
        registrar.RegisterItem(new ItemDef(torchItemId, "Torch", PackId, "packs/basegame/assets/items/torch.png", 16, 16, 16, torchPlaceableId));
        registrar.RegisterItem(new ItemDef(chestItemId, "Wooden Chest", PackId, "packs/basegame/assets/production/storage/wooden_chest.png", 32, 32, 8, chestPlaceableId));
        registrar.RegisterItem(new ItemDef(doorItemId, "Wooden Door", PackId, "packs/basegame/assets/structures/doors/wood_door_close_open.png", 32, 32, 8, doorPlaceableId));
        registrar.RegisterItem(new ItemDef(fenceItemId, "Wooden Fence", PackId, "packs/basegame/assets/structures/fences/wood_fence_horizontal.png", 32, 32, 32, fencePlaceableId));
        registrar.RegisterItem(new ItemDef(bedItemId, "Bed", PackId, "packs/basegame/assets/furniture/beds/bed.png", 32, 32, 4, bedPlaceableId));
        registrar.RegisterItem(new ItemDef(roofPanelItemId, "Roof Panel", PackId, "packs/basegame/assets/world/terrain/floors/stone_floor.png", 32, 32, 16, roofPanelPlaceableId));
        registrar.RegisterItem(new ItemDef(workbenchUpgradeItemId, "Weapons Bench Upgrade", PackId, "packs/basegame/assets/items/resources/upgrade_workbench_weapons_bench.png", 16, 16, 4));

        registrar.RegisterResourceNode(new ResourceNodeDef(brightTreeNodeId, "Bright Tree", PackId, "packs/basegame/assets/world/nature/trees/bright_green_tree.png", 32, 32, 0, 0, 3, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(lushTreeNodeId, "Lush Tree", PackId, "packs/basegame/assets/world/nature/trees/lush_green_tree.png", 32, 32, 0, 0, 3, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(roundTreeNodeId, "Round Tree", PackId, "packs/basegame/assets/world/nature/trees/round_green_tree.png", 32, 32, 0, 0, 3, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(roundBushTreeNodeId, "Round Bush Tree", PackId, "packs/basegame/assets/world/nature/trees/round_bush_tree.png", 32, 32, 0, 0, 3, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(lightTrunkTreeNodeId, "Light Trunk Tree", PackId, "packs/basegame/assets/world/nature/trees/light_trunk_tree.png", 32, 32, 0, 0, 3, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(pineTreeNodeId, "Dark Pine Tree", PackId, "packs/basegame/assets/world/nature/trees/dark_pine_tree.png", 32, 32, 0, 0, 4, [new ItemAmount(logItemId, 4)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(blossomTreeNodeId, "Pink Blossom Tree", PackId, "packs/basegame/assets/world/nature/trees/pink_blossom_tree.png", 32, 32, 0, 0, 3, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(autumnTreeNodeId, "Red Autumn Tree", PackId, "packs/basegame/assets/world/nature/trees/red_autumn_tree.png", 32, 32, 0, 0, 3, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(deadBrownTreeNodeId, "Dead Brown Tree", PackId, "packs/basegame/assets/world/nature/trees/dead_brown_tree.png", 32, 32, 0, 0, 2, [new ItemAmount(logItemId, 2)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(deadBlackTreeNodeId, "Dead Black Tree", PackId, "packs/basegame/assets/world/nature/trees/dead_black_tree.png", 32, 32, 0, 0, 2, [new ItemAmount(logItemId, 2)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(youngGreenSplitTreeNodeId, "Young Green Tree", PackId, "packs/basegame/assets/world/nature/trees/split/broadleaf/young_green_tree.png", 36, 49, 0, 0, 3, [new ItemAmount(logItemId, 2)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(greenSplitTreeNodeId, "Green Tree", PackId, "packs/basegame/assets/world/nature/trees/split/broadleaf/green_tree.png", 47, 61, 0, 0, 4, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(largeGreenSplitTreeNodeId, "Large Green Tree", PackId, "packs/basegame/assets/world/nature/trees/split/broadleaf/large_green_tree.png", 75, 73, 0, 0, 5, [new ItemAmount(logItemId, 4)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(smallPineSplitTreeNodeId, "Small Pine Tree", PackId, "packs/basegame/assets/world/nature/trees/split/conifers/small_pine_tree.png", 26, 40, 0, 0, 2, [new ItemAmount(logItemId, 2)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(pineSplitTreeNodeId, "Pine Tree", PackId, "packs/basegame/assets/world/nature/trees/split/conifers/pine_tree.png", 39, 56, 0, 0, 4, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(largePineSplitTreeNodeId, "Large Pine Tree", PackId, "packs/basegame/assets/world/nature/trees/split/conifers/large_pine_tree.png", 51, 76, 0, 0, 5, [new ItemAmount(logItemId, 4)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(smallSnowPineSplitTreeNodeId, "Small Snow Pine Tree", PackId, "packs/basegame/assets/world/nature/trees/split/snow_conifers/small_snow_pine_tree.png", 33, 43, 0, 0, 2, [new ItemAmount(logItemId, 2)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(snowPineSplitTreeNodeId, "Snow Pine Tree", PackId, "packs/basegame/assets/world/nature/trees/split/snow_conifers/snow_pine_tree.png", 43, 61, 0, 0, 4, [new ItemAmount(logItemId, 3)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(largeSnowPineSplitTreeNodeId, "Large Snow Pine Tree", PackId, "packs/basegame/assets/world/nature/trees/split/snow_conifers/large_snow_pine_tree.png", 57, 77, 0, 0, 5, [new ItemAmount(logItemId, 4)], true, false, false, 0, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(stoneNodeId, "Stone Node", PackId, "packs/basegame/assets/world/nature/rocks/stone.png", 32, 32, 0, 0, 1, [new ItemAmount(stoneItemId, 1)], false, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(blueberryNodeId, "Blueberry Bush", PackId, "packs/basegame/assets/world/nature/plants/blueberry_bush.png", 16, 16, 0, 0, 1, [new ItemAmount(blueberryItemId, 1)], false, true));
        registrar.RegisterResourceNode(new ResourceNodeDef(rockOutcropNodeId, "Rock Outcrop", PackId, "packs/basegame/assets/world/nature/rocks/rock_outcrop.png", 32, 32, 0, 0, 4, [new ItemAmount(stoneItemId, 2)], true));
        registrar.RegisterRaisedFeature(new RaisedFeatureDef(voiditeRaisedId, "Voidite", PackId, "packs/basegame/assets/world/nature/ores/voidite.png", 32, 32, 13, 4, [new ItemAmount(voiditeItemId, 1)]));
        registrar.RegisterRaisedFeature(new RaisedFeatureDef(goldveinRaisedId, "Goldvein", PackId, "packs/basegame/assets/world/nature/ores/goldvein.png", 32, 32, 13, 4, [new ItemAmount(goldveinItemId, 1)]));
        registrar.RegisterRaisedFeature(new RaisedFeatureDef(venomiteRaisedId, "Venomite", PackId, "packs/basegame/assets/world/nature/ores/venomite.png", 32, 32, 13, 4, [new ItemAmount(venomiteItemId, 1)]));
        registrar.RegisterRaisedOreFieldRule(new RaisedOreFieldRuleDef(voiditeRaisedId, WorldSpaceKind.Overworld, SurfaceRegions.DirtField, 0.48f, true, [voiditeRaisedId, voiditeRaisedId, voiditeRaisedId, voiditeRaisedId, goldveinRaisedId, goldveinRaisedId, goldveinRaisedId, venomiteRaisedId, venomiteRaisedId, venomiteRaisedId]));
        registrar.RegisterRaisedOreFieldRule(new RaisedOreFieldRuleDef(goldveinRaisedId, WorldSpaceKind.Overworld, SurfaceRegions.DirtField, 0.48f, true, [voiditeRaisedId, voiditeRaisedId, voiditeRaisedId, voiditeRaisedId, goldveinRaisedId, goldveinRaisedId, goldveinRaisedId, venomiteRaisedId, venomiteRaisedId, venomiteRaisedId]));
        registrar.RegisterRaisedOreFieldRule(new RaisedOreFieldRuleDef(venomiteRaisedId, WorldSpaceKind.Overworld, SurfaceRegions.DirtField, 0.48f, true, [voiditeRaisedId, voiditeRaisedId, voiditeRaisedId, voiditeRaisedId, goldveinRaisedId, goldveinRaisedId, goldveinRaisedId, venomiteRaisedId, venomiteRaisedId, venomiteRaisedId]));

        registrar.RegisterCreature(new CreatureDef(playerId, "Human", PackId, "packs/basegame/assets/characters/humans/default/idle.png", "packs/basegame/assets/characters/humans/default/run.png", null, 64, 64, 140f));
        registrar.RegisterCreature(new CreatureDef(wormId, "Worm", PackId, "packs/basegame/assets/world/nature/plants/worm.png", "packs/basegame/assets/world/nature/plants/worm.png", "packs/basegame/assets/world/nature/plants/worm.png", 16, 16, 28f, 4, true, 4, 0f, 0f, 88f, 1f));
        registrar.RegisterCreature(new CreatureDef(cockroachId, "Cockroach", PackId, "packs/basegame/assets/world/nature/plants/cockroach.png", "packs/basegame/assets/world/nature/plants/cockroach.png", "packs/basegame/assets/world/nature/plants/cockroach.png", 16, 16, 34f, 1, false, 5, 128f, 192f, 72f, 1f));

        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_workbench"), "Workbench", PackId, [new ItemAmount(logItemId, 4), new ItemAmount(stoneItemId, 1)], new ItemAmount(workbenchItemId, 1), CraftingStationKind.Handcraft));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_torch"), "Torch", PackId, [new ItemAmount(logItemId, 1), new ItemAmount(stoneItemId, 1)], new ItemAmount(torchItemId, 1), CraftingStationKind.Handcraft));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_chest"), "Wooden Chest", PackId, [new ItemAmount(logItemId, 6)], new ItemAmount(chestItemId, 1), CraftingStationKind.Workbench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_door"), "Wooden Door", PackId, [new ItemAmount(logItemId, 4)], new ItemAmount(doorItemId, 1), CraftingStationKind.Workbench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_fence"), "Wooden Fence", PackId, [new ItemAmount(logItemId, 2)], new ItemAmount(fenceItemId, 2), CraftingStationKind.Workbench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_bed"), "Bed", PackId, [new ItemAmount(logItemId, 4), new ItemAmount(stoneItemId, 2)], new ItemAmount(bedItemId, 1), CraftingStationKind.Workbench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_roof_panel"), "Roof Panel", PackId, [new ItemAmount(logItemId, 4)], new ItemAmount(roofPanelItemId, 1), CraftingStationKind.Workbench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_furnace"), "Furnace", PackId, [new ItemAmount(stoneItemId, 4)], new ItemAmount(furnaceItemId, 1), CraftingStationKind.Workbench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_stone_wall"), "Stone Wall", PackId, [new ItemAmount(stoneItemId, 2)], new ItemAmount(stoneWallItemId, 1), CraftingStationKind.Workbench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_stone_floor"), "Stone Floor", PackId, [new ItemAmount(stoneItemId, 1)], new ItemAmount(stoneFloorItemId, 1), CraftingStationKind.Workbench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_workbench_weapons_upgrade"), "Weapons Bench Upgrade", PackId, [new ItemAmount(logItemId, 6), new ItemAmount(ironIngotItemId, 2), new ItemAmount(voidCrystalItemId, 1)], new ItemAmount(workbenchUpgradeItemId, 1), CraftingStationKind.Workbench, RecipeExecutionKind.UpgradeActiveStation, CraftingStationKind.WeaponsBench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_wood_spear"), "Wood Spear", PackId, [new ItemAmount(logItemId, 2), new ItemAmount(ironIngotItemId, 1)], new ItemAmount(woodSpearItemId, 1), CraftingStationKind.WeaponsBench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_axe"), "Axe", PackId, [new ItemAmount(logItemId, 1), new ItemAmount(ironIngotItemId, 1)], new ItemAmount(axeItemId, 1), CraftingStationKind.WeaponsBench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:craft_iron_knife"), "Iron Knife", PackId, [new ItemAmount(logItemId, 1), new ItemAmount(ironIngotItemId, 2)], new ItemAmount(ironKnifeItemId, 1), CraftingStationKind.WeaponsBench));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:smelt_voidite"), "Void Crystal", PackId, [new ItemAmount(voiditeItemId, 1)], new ItemAmount(voidCrystalItemId, 2), CraftingStationKind.Furnace, CraftDurationSeconds: 3f));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:smelt_goldvein"), "Gold Ingot + Sand", PackId, [new ItemAmount(goldveinItemId, 1)], new ItemAmount(goldIngotItemId, 1), CraftingStationKind.Furnace, CraftDurationSeconds: 3.5f, ExtraResults: [new ItemAmount(sandItemId, 1)]));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:smelt_venomite"), "Poison Crystal + Iron Ingot", PackId, [new ItemAmount(venomiteItemId, 1)], new ItemAmount(poisonCrystalItemId, 1), CraftingStationKind.Furnace, CraftDurationSeconds: 3.5f, ExtraResults: [new ItemAmount(ironIngotItemId, 1)]));
        registrar.RegisterRecipe(new RecipeDef(new ContentId("basegame:smelt_silicon_wafer"), "Silicon Wafer", PackId, [new ItemAmount(sandItemId, 8)], new ItemAmount(siliconWaferItemId, 1), CraftingStationKind.Furnace, CraftDurationSeconds: 5f));

        RegisterOverworldPasses(
            registrar,
            dirtId,
            grassId,
            riverWaterId,
            rockOutcropNodeId,
            [
                brightTreeNodeId,
                lushTreeNodeId,
                roundTreeNodeId,
                roundBushTreeNodeId,
                lightTrunkTreeNodeId,
                pineTreeNodeId,
                blossomTreeNodeId,
                autumnTreeNodeId,
                deadBrownTreeNodeId,
                deadBlackTreeNodeId,
                youngGreenSplitTreeNodeId,
                greenSplitTreeNodeId,
                largeGreenSplitTreeNodeId,
                smallPineSplitTreeNodeId,
                pineSplitTreeNodeId,
                largePineSplitTreeNodeId,
                smallSnowPineSplitTreeNodeId,
                snowPineSplitTreeNodeId,
                largeSnowPineSplitTreeNodeId
            ],
            blueberryNodeId,
            stoneNodeId,
            voiditeRaisedId,
            goldveinRaisedId,
            venomiteRaisedId,
            wormId,
            cockroachId);

        registrar.SetBootstrapConfig(new GameBootstrapConfig(
            ChunkWidth: 28,
            ChunkHeight: 18,
            WorldSeed: 424242,
            OverworldLoadRadius: 1,
            DefaultTerrainId: dirtId,
            PlayerCreatureId: playerId,
            DebugItemId: stoneItemId,
            DebugPlaceableId: workbenchPlaceableId,
            DebugTerrainVariantId: grassId,
            DayLengthSeconds: 90,
            StartingHealth: 100,
            StartingHunger: 100,
            MaxHealth: 100,
            MaxHunger: 100,
            PlayerSpawn: new TileSpawn(new WorldTileCoord(10, 8)),
            DebugPlaceableSpawn: new TileSpawn(new WorldTileCoord(12, 8))));
    }

    private static void RegisterOverworldPasses(
        IContentRegistrar registrar,
        ContentId dirtId,
        ContentId grassId,
        ContentId riverWaterId,
        ContentId rockOutcropNodeId,
        IReadOnlyList<ContentId> treeNodeIds,
        ContentId blueberryNodeId,
        ContentId stoneNodeId,
        ContentId voiditeRaisedId,
        ContentId goldveinRaisedId,
        ContentId venomiteRaisedId,
        ContentId wormId,
        ContentId cockroachId)
    {
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:overworld-fill-dirt"), WorldGenPassTypes.FillTerrain, dirtId, WorldSpaceKind.Overworld, PrimarySurfaceRegion: SurfaceRegions.DirtField));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:overworld-surface-region"), WorldGenPassTypes.SurfaceRegion, grassId, WorldSpaceKind.Overworld));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:overworld-river"), WorldGenPassTypes.River, riverWaterId, WorldSpaceKind.Overworld));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:overworld-terrain-semantics"), WorldGenPassTypes.TerrainSemantics, dirtId, WorldSpaceKind.Overworld));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:raised-voidite"), WorldGenPassTypes.RaisedOreField, voiditeRaisedId, WorldSpaceKind.Overworld));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:raised-goldvein"), WorldGenPassTypes.RaisedOreField, goldveinRaisedId, WorldSpaceKind.Overworld));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:raised-venomite"), WorldGenPassTypes.RaisedOreField, venomiteRaisedId, WorldSpaceKind.Overworld));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:overworld-rock-outcrop"), WorldGenPassTypes.RockOutcrop, rockOutcropNodeId, WorldSpaceKind.Overworld));
        var temperateForestTreeIds = new[]
        {
            treeNodeIds[0],
            treeNodeIds[1],
            treeNodeIds[2],
            treeNodeIds[4],
            treeNodeIds[6],
            treeNodeIds[7],
            treeNodeIds[10],
            treeNodeIds[11],
            treeNodeIds[12]
        };
        var coniferForestTreeIds = new[]
        {
            treeNodeIds[5],
            treeNodeIds[13],
            treeNodeIds[14],
            treeNodeIds[15],
            treeNodeIds[16],
            treeNodeIds[17],
            treeNodeIds[18]
        };
        var sparseEdgeTreeIds = new[]
        {
            treeNodeIds[3],
            treeNodeIds[8],
            treeNodeIds[9]
        };

        RegisterTreeClusterPass(
            registrar,
            "basegame:spawn-temperate-forest-core",
            temperateForestTreeIds[0],
            TreeBiomeKind.TemperateForestCore,
            temperateForestTreeIds,
            minSpacing: 4,
            requiredTerrainRegion: TerrainRegionKind.ForestCore,
            avoidRiverBank: true,
            candidateDensity: 0.24f,
            maxCountOverride: 10);
        RegisterTreeClusterPass(
            registrar,
            "basegame:spawn-conifer-mountain-foot",
            coniferForestTreeIds[0],
            TreeBiomeKind.ConiferMountainFoot,
            coniferForestTreeIds,
            minSpacing: 5,
            requiredTerrainRegion: TerrainRegionKind.MountainFoot,
            avoidRiverBank: true,
            candidateDensity: 0.22f,
            maxCountOverride: 8);
        RegisterTreeClusterPass(
            registrar,
            "basegame:spawn-sparse-forest-edge",
            sparseEdgeTreeIds[0],
            TreeBiomeKind.SparseForestEdge,
            sparseEdgeTreeIds,
            minSpacing: 5,
            requiredTerrainRegion: TerrainRegionKind.ForestEdge,
            avoidRiverBank: true,
            candidateDensity: 0.18f,
            maxCountOverride: 5);
        RegisterTreeClusterPass(
            registrar,
            "basegame:spawn-open-sparse-trees",
            sparseEdgeTreeIds[0],
            TreeBiomeKind.OpenLowlandSparse,
            sparseEdgeTreeIds,
            minSpacing: 6,
            requiredTerrainRegion: TerrainRegionKind.OpenLowland,
            avoidRiverBank: true,
            candidateDensity: 0.06f,
            maxCountOverride: 2);
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:spawn-berries"), WorldGenPassTypes.ScatterSpawn, blueberryNodeId, WorldSpaceKind.Overworld, 8, 0, 0, 28, 18, SurfaceRegions.GrassField, 2, RequireSupportsTrees: true));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:spawn-stones"), WorldGenPassTypes.ScatterSpawn, stoneNodeId, WorldSpaceKind.Overworld, 10, 0, 0, 28, 18, SurfaceRegions.DirtField, 2));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:spawn-worms"), WorldGenPassTypes.ScatterSpawn, wormId, WorldSpaceKind.Overworld, 3, 0, 0, 28, 18, SurfaceRegions.DirtField, 5));
        registrar.RegisterWorldGenPass(new WorldGenPassDef(new ContentId("basegame:spawn-cockroaches"), WorldGenPassTypes.ScatterSpawn, cockroachId, WorldSpaceKind.Overworld, 4, 0, 0, 28, 18, SurfaceRegions.GrassField, 5));
    }

    private static void RegisterTreeClusterPass(
        IContentRegistrar registrar,
        string passId,
        ContentId targetId,
        TreeBiomeKind biome,
        IReadOnlyList<ContentId> speciesPool,
        int minSpacing,
        TerrainRegionKind? requiredTerrainRegion = null,
        bool avoidRiverBank = false,
        float candidateDensity = 1f,
        int? maxCountOverride = null)
    {
        registrar.RegisterWorldGenPass(new WorldGenPassDef(
            new ContentId(passId),
            WorldGenPassTypes.ForestClusterSpawn,
            targetId,
            WorldSpaceKind.Overworld,
            0,
            0,
            0,
            28,
            18,
            null,
            minSpacing,
            RequireSupportsTrees: true,
            RequiredTerrainRegion: requiredTerrainRegion,
            AvoidRiverBank: avoidRiverBank,
            CandidateDensity: candidateDensity,
            MaxCountOverride: maxCountOverride,
            TreeBiome: biome,
            SpeciesPoolIds: speciesPool));
    }
}
