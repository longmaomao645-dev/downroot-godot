using Downroot.Content.Registries;
using Downroot.Core.World;
using Downroot.World.Generation.Passes;

namespace Downroot.World.Generation;

public static class WorldGenPassFactory
{
    public static IWorldGenPass Create(ContentRegistrySet registries, WorldGenPassDef definition)
    {
        return definition.PassType switch
        {
            WorldGenPassTypes.FillTerrain => new FillTerrainPass(definition.TargetId, definition.PrimarySurfaceRegion ?? SurfaceRegions.DirtField),
            WorldGenPassTypes.SurfaceRegion => new GrassRegionPass(definition.TargetId),
            WorldGenPassTypes.River => new RiverPass(definition.TargetId),
            WorldGenPassTypes.TerrainSemantics => new TerrainSemanticPass(),
            WorldGenPassTypes.RaisedOreField => new RaisedOreFieldPass(definition.TargetId, new RaisedOreFieldRuleResolver(registries)),
            WorldGenPassTypes.RockOutcrop => new RockOutcropPass(definition.TargetId),
            WorldGenPassTypes.PortalSite => new PortalSitePass(definition.TargetId, registries.PortalWorldLinks.ToArray()),
            WorldGenPassTypes.DirtPatch => new DirtPatchPass(definition.TargetId),
            WorldGenPassTypes.BerryPatchSpawn => new BerryPatchSpawnPass(
                definition.TargetId,
                definition.Count,
                definition.StartColumn,
                definition.StartRow,
                definition.Width,
                definition.Height,
                definition.MinSpacing,
                definition.MaxCountOverride),
            WorldGenPassTypes.ScatterSpawn => new ScatterSpawnPass(
                definition.TargetId,
                definition.Count,
                definition.StartColumn,
                definition.StartRow,
                definition.Width,
                definition.Height,
                definition.PrimarySurfaceRegion,
                definition.MinSpacing,
                definition.RequireBuildable,
                definition.RequireSupportsTrees,
                definition.RequiredTerrainRegion,
                definition.PreferForestCore,
                definition.PreferForestEdge,
                definition.AvoidRiverBank,
                definition.CandidateDensity,
                definition.MaxCountOverride),
            WorldGenPassTypes.ForestClusterSpawn => new ForestClusterSpawnPass(
                definition.TreeBiome ?? throw new InvalidOperationException($"Tree biome missing for '{definition.Id}'."),
                definition.SpeciesPoolIds ?? throw new InvalidOperationException($"Species pool missing for '{definition.Id}'."),
                definition.StartColumn,
                definition.StartRow,
                definition.Width,
                definition.Height,
                definition.MinSpacing,
                definition.RequireBuildable,
                definition.RequireSupportsTrees,
                definition.RequiredTerrainRegion,
                definition.AvoidRiverBank,
                definition.CandidateDensity,
                definition.MaxCountOverride),
            _ => throw new InvalidOperationException($"Unknown world gen pass type '{definition.PassType}' for '{definition.Id}'.")
        };
    }
}
