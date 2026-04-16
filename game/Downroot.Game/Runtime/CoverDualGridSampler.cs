using Downroot.Core.Ids;
using Downroot.Core.World;
using Downroot.Gameplay.Runtime;

namespace Downroot.Game.Runtime;

public sealed class CoverDualGridSampler(WorldRuntimeFacade worldFacade, GameRuntime runtime)
{
    public ContentId? SampleCover(WorldTileCoord tile)
    {
        if (!worldFacade.TryGetChunkForTile(runtime.ActiveWorldSpaceKind, tile, out var chunk, out var localCoord))
        {
            return null;
        }

        return chunk.GeneratedChunk.Surface.GetCoverTerrainId(localCoord.X, localCoord.Y);
    }

    public bool HasCover(WorldTileCoord tile, ContentId coverId)
    {
        return SampleCover(tile) == coverId;
    }
}
