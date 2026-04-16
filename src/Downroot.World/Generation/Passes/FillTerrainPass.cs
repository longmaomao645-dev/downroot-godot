using Downroot.Core.World;
using Downroot.Core.Ids;

namespace Downroot.World.Generation.Passes;

public sealed class FillTerrainPass(ContentId terrainId, string surfaceRegion) : IWorldGenPass
{
    public string Name => WorldGenPassTypes.FillTerrain;

    public void Execute(IWorldGenContext context)
    {
        if (!context.HasTerrain(terrainId))
        {
            throw new InvalidOperationException($"Missing terrain '{terrainId}' for fill pass.");
        }

        for (var y = 0; y < context.Height; y++)
        {
            for (var x = 0; x < context.Width; x++)
            {
                var coord = new LocalTileCoord(x, y);
                context.SetBaseTerrain(coord, terrainId);
                context.SetCoverTerrain(coord, null);

                // Some world spaces still rely on fill-time regions and do not run TerrainSemanticPass.
                // Overworld semantics are finalized later by TerrainSemanticPass, which overwrites this.
                context.SetSurfaceRegion(coord, surfaceRegion);
            }
        }
    }
}
