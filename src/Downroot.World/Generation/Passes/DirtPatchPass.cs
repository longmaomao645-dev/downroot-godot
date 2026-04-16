using Downroot.Core.World;
using Downroot.Core.Ids;

namespace Downroot.World.Generation.Passes;

public sealed class DirtPatchPass(ContentId terrainId) : IWorldGenPass
{
    public string Name => WorldGenPassTypes.DirtPatch;

    public void Execute(IWorldGenContext context)
    {
        if (!context.HasTerrain(terrainId))
        {
            throw new InvalidOperationException($"Missing terrain '{terrainId}' for dirt patch pass.");
        }

        for (var y = 0; y < context.Height; y++)
        {
            for (var x = 0; x < context.Width; x++)
            {
                if ((x + y) % 7 == 0 || (x * 3 + y) % 11 == 0)
                {
                    var coord = new LocalTileCoord(x, y);
                    context.SetBaseTerrain(coord, terrainId);
                    context.SetCoverTerrain(coord, null);
                }
            }
        }
    }
}
