using Downroot.Content.Packs;
using Downroot.Core.Ids;
using Downroot.Core.World;
using Xunit;

namespace Downroot.World.Tests;

public sealed class PortalWorldGenerationTests
{
    private static readonly ContentId PortalId = new("portalmod:portal");

    [Fact]
    public void OverworldGenerator_SpawnsPortalInGeneratedPortalChunk()
    {
        var registries = WorldGenTestHarness.BuildRegistries(new BaseGameContentPack(), new PortalModContentPack());
        var portalChunk = PortalPlacementRules.ResolveNearestPortalChunk(
            WorldSpaceKind.Overworld,
            WorldGenTestHarness.DefaultWorldSeed,
            WorldGenTestHarness.ChunkWidth,
            WorldGenTestHarness.ChunkHeight,
            new ChunkCoord(0, 0));

        var chunk = WorldGenTestHarness.GenerateChunk(registries, WorldSpaceKind.Overworld, portalChunk);

        Assert.Contains(chunk.NaturalSpawns, spawn => spawn.ContentId == PortalId);
    }

    [Fact]
    public void OverworldGenerator_DoesNotSpawnPortalInNearbyNonPortalChunk()
    {
        var registries = WorldGenTestHarness.BuildRegistries(new BaseGameContentPack(), new PortalModContentPack());
        var portalChunk = PortalPlacementRules.ResolveNearestPortalChunk(
            WorldSpaceKind.Overworld,
            WorldGenTestHarness.DefaultWorldSeed,
            WorldGenTestHarness.ChunkWidth,
            WorldGenTestHarness.ChunkHeight,
            new ChunkCoord(0, 0));
        var nonPortalChunk = new ChunkCoord(portalChunk.X + 1, portalChunk.Y);
        if (PortalPlacementRules.IsGeneratedPortalChunk(
            WorldSpaceKind.Overworld,
            WorldGenTestHarness.DefaultWorldSeed,
            WorldGenTestHarness.ChunkWidth,
            WorldGenTestHarness.ChunkHeight,
            nonPortalChunk))
        {
            nonPortalChunk = new ChunkCoord(portalChunk.X, portalChunk.Y + 1);
        }

        var chunk = WorldGenTestHarness.GenerateChunk(registries, WorldSpaceKind.Overworld, nonPortalChunk);

        Assert.DoesNotContain(chunk.NaturalSpawns, spawn => spawn.ContentId == PortalId);
    }
}
