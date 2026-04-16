using System.Numerics;
using Downroot.Core.Ids;
using Downroot.Core.Diagnostics;
using Downroot.Core.World;

namespace Downroot.Gameplay.Runtime.Systems;

public sealed class MovementSystem(GameRuntime runtime, WorldRuntimeFacade worldFacade)
{
    private const float BlockingRadius = 18f;

    public void UpdatePlayerMovement(float deltaSeconds, Vector2 movement)
    {
        var direction = NormalizeMovement(movement);
        if (direction == Vector2.Zero)
        {
            return;
        }

        runtime.Player.Facing = direction;
        runtime.Player.Position = MoveWithCollision(runtime.Player.Position, direction * runtime.Player.Speed * deltaSeconds);
    }

    public Vector2 MoveWithCollision(Vector2 currentPosition, Vector2 delta, EntityId? ignoreEntityId = null)
    {
        var desired = ClampToWorldBounds(currentPosition + delta);
        var slideX = ClampToWorldBounds(new Vector2(desired.X, currentPosition.Y));
        var slideY = ClampToWorldBounds(new Vector2(currentPosition.X, desired.Y));

        if (!IsBlocked(desired, ignoreEntityId))
        {
            return desired;
        }

        if (!IsBlocked(slideX, ignoreEntityId))
        {
            return slideX;
        }

        if (!IsBlocked(slideY, ignoreEntityId))
        {
            return slideY;
        }

        return currentPosition;
    }

    public bool IsBlocked(Vector2 position, EntityId? ignoreEntityId = null)
    {
        using var scope = RuntimeProfiler.Measure("Movement.IsBlocked");
        RuntimeProfiler.Increment("Movement.IsBlocked.Call");
        var tile = worldFacade.GetWorldTile(position);
        var surfaceSemantic = worldFacade.SampleSurfaceSemantic(runtime.ActiveWorldSpaceKind, tile);
        if (surfaceSemantic.Surface is SurfaceGameplayKind.SolidRock or SurfaceGameplayKind.Water)
        {
            RuntimeProfiler.Increment("Movement.IsBlocked.SurfaceBlocked");
            return true;
        }

        var world = worldFacade.GetActiveWorld();
        var raisedFeatureId = world.GetRaisedFeatureId(tile, runtime.ChunkWidth, runtime.ChunkHeight);
        if (raisedFeatureId is not null)
        {
            var raisedFeature = runtime.Content.RaisedFeatures.Get(raisedFeatureId.Value);
            if (raisedFeature.BlocksMovement)
            {
                RuntimeProfiler.Increment("Movement.IsBlocked.RaisedBlocked");
                return true;
            }
        }

        return world.IsBlocked(position, BlockingRadius, ignoreEntityId);
    }

    public Vector2 ClampToWorldBounds(Vector2 position)
    {
        var world = worldFacade.GetActiveWorld();
        if (world.Model.MinChunkCoord is not { } min || world.Model.MaxChunkCoord is not { } max)
        {
            return position;
        }

        var minTile = WorldTileCoord.FromChunkAndLocal(min, new LocalTileCoord(0, 0), runtime.ChunkWidth, runtime.ChunkHeight);
        var maxTile = WorldTileCoord.FromChunkAndLocal(max, new LocalTileCoord(runtime.ChunkWidth - 1, runtime.ChunkHeight - 1), runtime.ChunkWidth, runtime.ChunkHeight);
        return new Vector2(
            Math.Clamp(position.X, minTile.X * 32f, maxTile.X * 32f),
            Math.Clamp(position.Y, minTile.Y * 32f, maxTile.Y * 32f));
    }

    public static Vector2 NormalizeMovement(Vector2 movement)
    {
        return movement == Vector2.Zero
            ? Vector2.Zero
            : Vector2.Normalize(movement);
    }
}
