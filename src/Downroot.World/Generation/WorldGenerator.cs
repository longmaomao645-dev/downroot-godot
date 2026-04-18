using Downroot.Content.Registries;
using Downroot.Core.World;
using Downroot.World.Models;
using System.Text;

namespace Downroot.World.Generation;

public sealed class WorldGenerator(ContentRegistrySet registries, IReadOnlyList<IWorldGenPass> passes)
{
    public GeneratedChunk GenerateChunk(WorldSpaceKind worldSpaceKind, int worldSeed, ChunkCoord chunkCoord, int width, int height)
    {
        var spawns = new List<WorldSpawnDef>();
        var surface = new ChunkData(width, height);
        var context = new WorldGenContext(worldSpaceKind, worldSeed, chunkCoord, surface, registries, spawns);

        foreach (var pass in passes)
        {
            pass.Execute(context);
        }

        LogRegionDistribution(context);

        return new GeneratedChunk(worldSpaceKind, chunkCoord, surface, spawns.ToArray());
    }

    private static void LogRegionDistribution(IWorldGenContext context)
    {
        if (context.WorldSpaceKind != WorldSpaceKind.Overworld)
        {
            return;
        }

        var counts = new Dictionary<TerrainRegionKind, int>();
        for (var y = 0; y < context.Height; y++)
        {
            for (var x = 0; x < context.Width; x++)
            {
                var region = context.SampleTerrainRegion(new LocalTileCoord(x, y));
                counts.TryGetValue(region, out var current);
                counts[region] = current + 1;
            }
        }

        var total = Math.Max(1, context.Width * context.Height);
        var summary = new StringBuilder();
        foreach (var pair in counts.OrderByDescending(pair => pair.Value))
        {
            if (summary.Length > 0)
            {
                summary.Append(", ");
            }

            var percent = (pair.Value * 100f) / total;
            summary.Append($"{pair.Key}:{pair.Value} ({percent:0.#}%)");
        }

        Console.WriteLine($"[WorldGen][Regions] chunk {context.ChunkCoord.X},{context.ChunkCoord.Y} => {summary}");
    }
}
