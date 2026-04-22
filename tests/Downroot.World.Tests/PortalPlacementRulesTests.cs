using Downroot.Core.World;
using Xunit;

namespace Downroot.World.Tests;

public sealed class PortalPlacementRulesTests
{
    [Fact]
    public void GeneratedPortalChunks_AppearExactlyOncePerSector()
    {
        var sectorWidthChunks = (int)MathF.Round(PortalPlacementRules.AveragePortalSpacingTiles / (float)WorldGenTestHarness.ChunkWidth);
        var sectorHeightChunks = (int)MathF.Round(PortalPlacementRules.AveragePortalSpacingTiles / (float)WorldGenTestHarness.ChunkHeight);

        for (var sectorY = -1; sectorY <= 1; sectorY++)
        {
            for (var sectorX = -1; sectorX <= 1; sectorX++)
            {
                var hits = 0;
                for (var y = sectorY * sectorHeightChunks; y < (sectorY + 1) * sectorHeightChunks; y++)
                {
                    for (var x = sectorX * sectorWidthChunks; x < (sectorX + 1) * sectorWidthChunks; x++)
                    {
                        if (PortalPlacementRules.IsGeneratedPortalChunk(
                            WorldSpaceKind.Overworld,
                            WorldGenTestHarness.DefaultWorldSeed,
                            WorldGenTestHarness.ChunkWidth,
                            WorldGenTestHarness.ChunkHeight,
                            new ChunkCoord(x, y)))
                        {
                            hits++;
                        }
                    }
                }

                Assert.Equal(1, hits);
            }
        }
    }

    [Fact]
    public void NearestPortalChunk_IsStableForSameInput()
    {
        var reference = new ChunkCoord(57, -33);
        var portalA = PortalPlacementRules.ResolveNearestPortalChunk(
            WorldSpaceKind.Overworld,
            WorldGenTestHarness.DefaultWorldSeed,
            WorldGenTestHarness.ChunkWidth,
            WorldGenTestHarness.ChunkHeight,
            reference);
        var portalB = PortalPlacementRules.ResolveNearestPortalChunk(
            WorldSpaceKind.Overworld,
            WorldGenTestHarness.DefaultWorldSeed,
            WorldGenTestHarness.ChunkWidth,
            WorldGenTestHarness.ChunkHeight,
            reference);

        Assert.Equal(portalA, portalB);
        Assert.True(PortalPlacementRules.IsGeneratedPortalChunk(
            WorldSpaceKind.Overworld,
            WorldGenTestHarness.DefaultWorldSeed,
            WorldGenTestHarness.ChunkWidth,
            WorldGenTestHarness.ChunkHeight,
            portalA));
    }
}
