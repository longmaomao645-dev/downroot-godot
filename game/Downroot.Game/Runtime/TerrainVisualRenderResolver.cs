using Downroot.Core.Ids;
using Downroot.Core.World;
using Godot;

namespace Downroot.Game.Runtime;

public static class TerrainVisualRenderResolver
{
    private static readonly ContentId DirtTerrainId = new("basegame:dirt");
    private static readonly ContentId MountainTerrainId = new("basegame:mountain");
    private static readonly ContentId RiverWaterTerrainId = new("basegame:river_water");
    private static readonly Color DefaultTint = Colors.White;
    private static readonly Color RaisedMountainTint = new(0.78f, 0.80f, 0.84f, 1f);
    private static readonly Color CliffMountainTint = new(0.58f, 0.62f, 0.68f, 1f);

    public static IReadOnlyList<DualGridLayerDef> DualGridLayers => DualGridLayerCatalog.All;

    public static TerrainVisualRenderProfile Resolve(
        WorldSpaceKind worldSpaceKind,
        SurfaceTileSemantic semantic,
        ContentId fallbackBaseTerrainId,
        ContentId? legacyCoverTerrainId)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return new TerrainVisualRenderProfile(
                fallbackBaseTerrainId,
                legacyCoverTerrainId is not null,
                DefaultTint);
        }

        return semantic.Visual switch
        {
            TerrainVisualKind.DeepWater => new TerrainVisualRenderProfile(RiverWaterTerrainId, false, DefaultTint),
            TerrainVisualKind.ShallowWater => new TerrainVisualRenderProfile(RiverWaterTerrainId, false, DefaultTint),
            TerrainVisualKind.Beach => new TerrainVisualRenderProfile(DirtTerrainId, false, DefaultTint),
            TerrainVisualKind.Grass => new TerrainVisualRenderProfile(DirtTerrainId, false, DefaultTint),
            TerrainVisualKind.Mountain => new TerrainVisualRenderProfile(MountainTerrainId, false, ResolveMountainTint(semantic.Height)),
            _ => new TerrainVisualRenderProfile(DirtTerrainId, false, DefaultTint)
        };
    }

    private static Color ResolveMountainTint(HeightKind heightKind)
    {
        return heightKind switch
        {
            HeightKind.Cliff => CliffMountainTint,
            HeightKind.Raised => RaisedMountainTint,
            _ => DefaultTint
        };
    }
}

public sealed record TerrainVisualRenderProfile(
    ContentId BaseTerrainId,
    bool RenderLegacyCover,
    Color BaseTint);
