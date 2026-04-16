using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class TerrainVisualAdjacencyRules
{
    public static AdjacencyRule GetAdjacencyRule(TerrainVisualKind a, TerrainVisualKind b)
    {
        if (a == b)
        {
            return AdjacencyRule.Allowed;
        }

        var first = a <= b ? a : b;
        var second = a <= b ? b : a;
        return (first, second) switch
        {
            (TerrainVisualKind.DeepWater, TerrainVisualKind.ShallowWater) => AdjacencyRule.Allowed,
            (TerrainVisualKind.DeepWater, TerrainVisualKind.Beach) => AdjacencyRule.Forbidden,
            (TerrainVisualKind.DeepWater, TerrainVisualKind.Dirt) => AdjacencyRule.AllowedButSteep,
            (TerrainVisualKind.DeepWater, TerrainVisualKind.Grass) => AdjacencyRule.AllowedButSteep,
            (TerrainVisualKind.DeepWater, TerrainVisualKind.Mountain) => AdjacencyRule.AllowedButSteep,
            (TerrainVisualKind.ShallowWater, TerrainVisualKind.Beach) => AdjacencyRule.Allowed,
            (TerrainVisualKind.ShallowWater, TerrainVisualKind.Dirt) => AdjacencyRule.Allowed,
            (TerrainVisualKind.ShallowWater, TerrainVisualKind.Grass) => AdjacencyRule.AllowedButSteep,
            (TerrainVisualKind.ShallowWater, TerrainVisualKind.Mountain) => AdjacencyRule.AllowedButSteep,
            (TerrainVisualKind.Beach, TerrainVisualKind.Dirt) => AdjacencyRule.Allowed,
            (TerrainVisualKind.Beach, TerrainVisualKind.Grass) => AdjacencyRule.Allowed,
            (TerrainVisualKind.Beach, TerrainVisualKind.Mountain) => AdjacencyRule.Forbidden,
            (TerrainVisualKind.Dirt, TerrainVisualKind.Grass) => AdjacencyRule.Allowed,
            (TerrainVisualKind.Grass, TerrainVisualKind.Mountain) => AdjacencyRule.Allowed,
            (TerrainVisualKind.Dirt, TerrainVisualKind.Mountain) => AdjacencyRule.Allowed,
            _ => AdjacencyRule.Allowed
        };
    }
}
