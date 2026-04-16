using Downroot.Core.World;

namespace Downroot.Game.Runtime;

public static class DualGridLayerCatalog
{
    public static readonly DualGridLayerDef Dirt = new(
        TerrainVisualKind.Dirt,
        DualGridRenderRole.BaseVisual,
        0,
        "basegame:dirt_dualgrid",
        "packs/basegame/assets/world/terrain/ground/dirt_dualgrid.png");

    public static readonly DualGridLayerDef DeepWater = new(
        TerrainVisualKind.DeepWater,
        DualGridRenderRole.OverlayVisual,
        1,
        "basegame:deepwater_dualgrid",
        "packs/basegame/assets/world/terrain/ground/deepwater_dualgrid.png");

    public static readonly DualGridLayerDef Beach = new(
        TerrainVisualKind.Beach,
        DualGridRenderRole.OverlayVisual,
        2,
        "basegame:sand_dualgrid",
        "packs/basegame/assets/world/terrain/ground/sand_dualgrid.png");

    public static readonly DualGridLayerDef Grass = new(
        TerrainVisualKind.Grass,
        DualGridRenderRole.OverlayVisual,
        3,
        "basegame:grass_dualgrid",
        "packs/basegame/assets/world/terrain/ground/grass_dualgrid.png");

    public static readonly IReadOnlyList<DualGridLayerDef> All = [Dirt, DeepWater, Beach, Grass];
}
