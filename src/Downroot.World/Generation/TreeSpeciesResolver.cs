using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class TreeSpeciesResolver
{
    public static ContentId Resolve(
        TreeBiomeKind biome,
        float roll,
        TreeBiomeProfile profile,
        IReadOnlyList<ContentId> speciesPool)
    {
        if (speciesPool.Count == 0)
        {
            throw new InvalidOperationException($"Tree biome '{biome}' has no registered species.");
        }

        if (speciesPool.Count == 1)
        {
            return speciesPool[0];
        }

        return biome switch
        {
            TreeBiomeKind.TemperateForestCore => ResolveWeighted(roll, speciesPool, profile, 0.55f, 0.80f, 0.95f),
            TreeBiomeKind.ConiferMountainFoot => ResolveWeighted(roll, speciesPool, profile, 0.70f, 0.90f, 1.00f),
            TreeBiomeKind.SparseForestEdge => ResolveWeighted(roll, speciesPool, profile, 0.38f, 0.68f, 0.90f),
            TreeBiomeKind.OpenLowlandSparse => ResolveWeighted(roll, speciesPool, profile, 0.62f, 0.90f, 1.00f),
            _ => ResolveWeighted(roll, speciesPool, profile, 0.55f, 0.80f, 0.95f)
        };
    }

    private static ContentId ResolveWeighted(
        float roll,
        IReadOnlyList<ContentId> speciesPool,
        TreeBiomeProfile profile,
        float primaryThreshold,
        float secondaryThreshold,
        float accentThreshold)
    {
        if (roll < primaryThreshold)
        {
            return speciesPool[profile.PrimaryIndex];
        }

        if (roll < secondaryThreshold)
        {
            return speciesPool[profile.SecondaryIndex];
        }

        if (roll < accentThreshold)
        {
            return speciesPool[profile.AccentIndex];
        }

        var fallbackIndex = (int)MathF.Floor((roll - accentThreshold) / MathF.Max(0.0001f, 1f - accentThreshold) * speciesPool.Count);
        fallbackIndex = Math.Clamp(fallbackIndex, 0, speciesPool.Count - 1);
        return speciesPool[fallbackIndex];
    }
}
