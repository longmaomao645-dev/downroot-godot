using Downroot.Core.World;

namespace Downroot.World.Generation.Passes;

public sealed class TerrainSemanticPass : IWorldGenPass
{
    public string Name => WorldGenPassTypes.TerrainSemantics;

    public void Execute(IWorldGenContext context)
    {
        for (var y = 0; y < context.Height; y++)
        {
            for (var x = 0; x < context.Width; x++)
            {
                var local = new LocalTileCoord(x, y);
                var world = context.GetWorldTileCoord(local);
                var semantic = TerrainSemanticWorldSampler.SampleSemantic(context, world);
                context.SetSurfaceSemantic(local, semantic);
                context.SetSurfaceRegion(local, TerrainSemanticWorldSampler.SampleSurfaceRegion(context, world));
            }
        }
    }
}
