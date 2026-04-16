using Downroot.Core.World;
using Downroot.Gameplay.Runtime;
using Downroot.World.Generation;

namespace Downroot.Game.Runtime;

public sealed class SurfaceSemanticSampler(WorldRuntimeFacade worldFacade, GameRuntime runtime)
{
    public TerrainVisualKind? SampleVisual(WorldTileCoord tile)
    {
        if (TryGetLoadedSemantic(tile, out var semantic))
        {
            return semantic.Visual;
        }

        return TryInferSemantic(tile, out semantic)
            ? semantic.Visual
            : null;
    }

    public SurfaceGameplayKind? SampleSurface(WorldTileCoord tile)
    {
        if (TryGetLoadedSemantic(tile, out var semantic))
        {
            return semantic.Surface;
        }

        return TryInferSemantic(tile, out semantic)
            ? semantic.Surface
            : null;
    }

    public HeightKind? SampleHeight(WorldTileCoord tile)
    {
        if (TryGetLoadedSemantic(tile, out var semantic))
        {
            return semantic.Height;
        }

        return TryInferSemantic(tile, out semantic)
            ? semantic.Height
            : null;
    }

    public ShoreProfileKind? SampleShoreProfile(WorldTileCoord tile)
    {
        if (TryGetLoadedSemantic(tile, out var semantic))
        {
            return semantic.ShoreProfile;
        }

        return TryInferSemantic(tile, out semantic)
            ? semantic.ShoreProfile
            : null;
    }

    public bool HasVisual(WorldTileCoord tile, TerrainVisualKind visualKind)
    {
        return SampleVisual(tile) == visualKind;
    }

    private bool TryGetLoadedSemantic(WorldTileCoord tile, out SurfaceTileSemantic semantic)
    {
        if (worldFacade.TryGetChunkForTile(runtime.ActiveWorldSpaceKind, tile, out var chunk, out var localCoord))
        {
            semantic = chunk.GeneratedChunk.Surface.GetSurfaceSemantic(localCoord.X, localCoord.Y);
            return true;
        }

        semantic = default;
        return false;
    }

    private bool TryInferSemantic(WorldTileCoord tile, out SurfaceTileSemantic semantic)
    {
        var world = worldFacade.GetActiveWorld();
        semantic = TerrainSemanticWorldSampler.SampleSemantic(world.WorldSpaceKind, world.WorldSeed, tile);
        return true;
    }
}
