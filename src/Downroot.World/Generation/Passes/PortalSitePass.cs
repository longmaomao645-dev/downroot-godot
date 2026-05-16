using Downroot.Core.Ids;
using Downroot.Core.World;

namespace Downroot.World.Generation.Passes;

public sealed class PortalSitePass(ContentId portalId, float spawnChance, ChunkCoord? requiredChunk = null, int minChunkSpacing = 0) : IWorldGenPass
{
    public string Name => WorldGenPassTypes.PortalSite;

    public void Execute(IWorldGenContext context)
    {
        // If a specific chunk is required, skip all others.
        if (requiredChunk is { } required && context.ChunkCoord != required)
        {
            return;
        }

        if (spawnChance <= 0f)
        {
            return;
        }

        // Deterministic probability check — same seed + chunk always yields the same result.
        var rootTile = context.GetWorldTileCoord(new LocalTileCoord(0, 0));
        var salt = portalId.Value.GetHashCode();
        var myValue = context.GetStableUnitValue(rootTile, salt);
        if (myValue >= spawnChance)
        {
            return;
        }

        // Minimum spacing check — Poisson-like: only the chunk with the best (lowest)
        // random value in its neighborhood spawns a portal. This guarantees no two
        // portals are within minChunkSpacing chunks of each other.
        if (minChunkSpacing > 0 && HasStrongerNeighbor(context, salt, myValue))
        {
            return;
        }

        var center = new LocalTileCoord(context.Width / 2, context.Height / 2);
        var best = FindNearestUsableTile(context, center);
        if (best is null)
        {
            return;
        }

        context.AddSpawn(best.Value, portalId);
    }

    /// <summary>
    /// Checks whether any neighboring chunk within minChunkSpacing chunks
    /// has a lower random value, meaning it has a stronger claim to the portal in this region.
    /// </summary>
    private bool HasStrongerNeighbor(IWorldGenContext context, int salt, float myValue)
    {
        var cx = context.ChunkCoord.X;
        var cy = context.ChunkCoord.Y;
        var radius = minChunkSpacing;

        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var neighborCoord = new ChunkCoord(cx + dx, cy + dy);
                var neighborTile = WorldTileCoord.FromChunkAndLocal(
                    neighborCoord, new LocalTileCoord(0, 0), context.Width, context.Height);

                var neighborValue = context.GetStableUnitValue(neighborTile, salt);
                if (neighborValue < myValue)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static LocalTileCoord? FindNearestUsableTile(IWorldGenContext context, LocalTileCoord origin)
    {
        LocalTileCoord? best = null;
        var bestDistance = int.MaxValue;
        for (var y = 0; y < context.Height; y++)
        {
            for (var x = 0; x < context.Width; x++)
            {
                var local = new LocalTileCoord(x, y);
                if (context.IsSpawnOccupied(local) || context.HasSurfaceRegion(local, SurfaceRegions.River))
                {
                    continue;
                }

                var distance = DistanceSquared(origin, local);
                if (distance >= bestDistance)
                {
                    continue;
                }

                best = local;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int DistanceSquared(LocalTileCoord a, LocalTileCoord b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
}
