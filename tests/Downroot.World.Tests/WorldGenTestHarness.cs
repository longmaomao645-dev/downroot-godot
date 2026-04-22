using Downroot.Content.Packs;
using Downroot.Content.Registries;
using Downroot.Core.Content;
using Downroot.Core.Diagnostics;
using Downroot.Core.Ids;
using Downroot.Core.World;
using Downroot.World.Generation;
using Downroot.World.Models;

namespace Downroot.World.Tests;

internal static class WorldGenTestHarness
{
    public const int ChunkWidth = 28;
    public const int ChunkHeight = 18;
    public const int DefaultWorldSeed = 1337;

    public static ContentRegistrySet BuildRegistries(params IContentPack[] packs)
    {
        var registries = new ContentRegistrySet();
        var registrar = registries.CreateRegistrar();
        foreach (var pack in packs)
        {
            pack.Register(registrar);
        }

        return registries;
    }

    public static WorldGenerator CreateGenerator(ContentRegistrySet registries, WorldSpaceKind worldSpaceKind)
    {
        return new WorldGenerator(
            registries,
            registries.WorldGenPasses
                .Where(pass => pass.WorldSpaceKind is null || pass.WorldSpaceKind == worldSpaceKind)
                .Select(pass => WorldGenPassFactory.Create(registries, pass))
                .ToArray(),
            NullDiagnosticLogger.Instance);
    }

    public static GeneratedChunk GenerateChunk(
        ContentRegistrySet registries,
        WorldSpaceKind worldSpaceKind,
        ChunkCoord chunkCoord,
        int worldSeed = DefaultWorldSeed)
    {
        return CreateGenerator(registries, worldSpaceKind)
            .GenerateChunk(worldSpaceKind, worldSeed, chunkCoord, ChunkWidth, ChunkHeight);
    }

    public static IReadOnlyList<WorldSpawnDef> FilterSpawns(GeneratedChunk chunk, ContentId contentId)
    {
        return chunk.NaturalSpawns.Where(spawn => spawn.ContentId == contentId).ToArray();
    }
}
