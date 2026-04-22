using Downroot.Content.Packs;
using Downroot.Core.Ids;
using Downroot.Core.World;
using Xunit;

namespace Downroot.World.Tests;

public sealed class StoneWorldGenerationTests
{
    private static readonly ContentId StoneNodeId = new("basegame:stone_node");

    [Fact]
    public void StoneScatter_IsDeterministicForSameChunkAndSeed()
    {
        var registries = WorldGenTestHarness.BuildRegistries(new BaseGameContentPack());
        var sampleChunk = FindChunkWithStoneSpawn(registries);

        var first = WorldGenTestHarness.FilterSpawns(
            WorldGenTestHarness.GenerateChunk(registries, WorldSpaceKind.Overworld, sampleChunk),
            StoneNodeId);
        var second = WorldGenTestHarness.FilterSpawns(
            WorldGenTestHarness.GenerateChunk(registries, WorldSpaceKind.Overworld, sampleChunk),
            StoneNodeId);

        Assert.NotEmpty(first);
        Assert.Equal(
            first.Select(spawn => (spawn.Tile.X, spawn.Tile.Y, spawn.PixelOffsetX, spawn.PixelOffsetY)),
            second.Select(spawn => (spawn.Tile.X, spawn.Tile.Y, spawn.PixelOffsetX, spawn.PixelOffsetY)));
    }

    private static ChunkCoord FindChunkWithStoneSpawn(Downroot.Content.Registries.ContentRegistrySet registries)
    {
        for (var y = -8; y <= 8; y++)
        {
            for (var x = -8; x <= 8; x++)
            {
                var chunk = new ChunkCoord(x, y);
                var generated = WorldGenTestHarness.GenerateChunk(registries, WorldSpaceKind.Overworld, chunk);
                if (WorldGenTestHarness.FilterSpawns(generated, StoneNodeId).Count > 0)
                {
                    return chunk;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Failed to find a sample chunk with stone spawns in the searched range.");
    }
}
