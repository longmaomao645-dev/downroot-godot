using Downroot.Core.World;
using Downroot.Gameplay.Runtime;
using Downroot.World.Generation;

namespace Downroot.Game.Runtime;

public sealed class SurfaceSemanticSampler(WorldRuntimeFacade worldFacade, GameRuntime runtime)
{
    public TerrainVisualKind? SampleVisual(WorldTileCoord tile)
    {
        return worldFacade.SampleSurfaceSemantic(runtime.ActiveWorldSpaceKind, tile).Visual;
    }

    public SurfaceGameplayKind? SampleSurface(WorldTileCoord tile)
    {
        return worldFacade.SampleSurfaceSemantic(runtime.ActiveWorldSpaceKind, tile).Surface;
    }

    public HeightKind? SampleHeight(WorldTileCoord tile)
    {
        return worldFacade.SampleSurfaceSemantic(runtime.ActiveWorldSpaceKind, tile).Height;
    }

    public ShoreProfileKind? SampleShoreProfile(WorldTileCoord tile)
    {
        return worldFacade.SampleSurfaceSemantic(runtime.ActiveWorldSpaceKind, tile).ShoreProfile;
    }

    public bool HasVisual(WorldTileCoord tile, TerrainVisualKind visualKind)
    {
        return SampleVisual(tile) == visualKind;
    }
}
