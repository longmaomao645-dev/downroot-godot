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
            WorldGenPassTypes.RaisedOreField => new RaisedOreFieldPass(definition.TargetId, new RaisedOreFieldRuleResolver(registries)),
            WorldGenPassTypes.RockOutcrop => new RockOutcropPass(definition.TargetId),
            WorldGenPassTypes.PortalSite => new PortalSitePass(definition.TargetId, definition.SpawnChance, definition.RequiredChunkCoord, definition.MinChunkSpacing),
            WorldGenPassTypes.DirtPatch => new DirtPatchPass(definition.TargetId),
            WorldGenPassTypes.ScatterSpawn => new ScatterSpawnPass(
                definition.TargetId,
                definition.Count,
                definition.StartColumn,
                definition.StartRow,
                definition.Width,
                definition.Height,
                definition.PrimarySurfaceRegion,
                definition.MinSpacing),
            _ => throw new InvalidOperationException($"Unknown world gen pass type '{definition.PassType}' for '{definition.Id}'.")
        };
    }
}
