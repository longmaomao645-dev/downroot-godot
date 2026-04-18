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
                definition.AvoidRiverBank),
            _ => throw new InvalidOperationException($"Unknown world gen pass type '{definition.PassType}' for '{definition.Id}'.")
        };
    }
}
