using Downroot.Core.World;

namespace Downroot.Gameplay.Runtime.Systems;

public sealed class LightingFieldSystem(GameRuntime runtime, WorldRuntimeFacade worldFacade)
{
    private const float NightOutdoorBrightness = 0.18f;
    private const float NightIndoorBrightness = 0.10f;
    private const float DayIndoorBrightness = 0.18f;
    private const float DayOutdoorBrightness = 1.0f;
    private readonly LightingInputCollector _collector = new(runtime, worldFacade);

    public void UpdateSkylightValue()
    {
        var outdoor = ResolveOutdoorSkylightLevel(runtime.WorldState.TimeOfDaySeconds, runtime.BootstrapConfig.DayLengthSeconds);
        runtime.WorldState.Lighting.UpdateSkylightBucket(QuantizeSkylight(outdoor));
    }

    public void Update()
    {
        var lighting = runtime.WorldState.Lighting;
        if (!lighting.IsStructureDirty && !lighting.IsValueDirty && lighting.Field is not null)
        {
            return;
        }

        LightingInputSnapshot snapshot;
        if (lighting.IsStructureDirty || lighting.Field is null)
        {
            snapshot = _collector.Collect();
        }
        else
        {
            snapshot = new LightingInputSnapshot(
                lighting.Bounds,
                _collector.RefreshEmitterValues(lighting.Emitters),
                lighting.Occluders,
                lighting.SkylightMasks);
        }

        var outdoor = ResolveOutdoorSkylightLevel(runtime.WorldState.TimeOfDaySeconds, runtime.BootstrapConfig.DayLengthSeconds);
        var indoor = ResolveIndoorSkylightLevel(outdoor);
        var dirtyBounds = ResolveDirtyBounds(lighting, snapshot);
        var field = lighting.IsStructureDirty || lighting.Field is null
            ? new LightingField(
                snapshot.Bounds.MinTileX,
                snapshot.Bounds.MinTileY,
                snapshot.Bounds.Width,
                snapshot.Bounds.Height,
                outdoor,
                indoor)
            : lighting.Field.CloneWithLevels(outdoor, indoor);

        var skylightMaskTiles = snapshot.SkylightMasks
            .Where(mask => mask.BlocksSkylight)
            .Select(mask => mask.WorldTile)
            .ToHashSet();
        var occluderTiles = snapshot.Occluders
            .Where(occluder => occluder.BlocksLight)
            .Select(occluder => occluder.WorldTile)
            .ToHashSet();
        var localLightLevels = new Dictionary<WorldTileCoord, float>();

        foreach (var emitter in snapshot.Emitters.Where(emitter => emitter.IsEnabled && emitter.RadiusTiles > 0f && emitter.Intensity > 0f))
        {
            if (!EmitterAffectsBounds(emitter, dirtyBounds))
            {
                continue;
            }

            WriteEmitterLight(localLightLevels, emitter, occluderTiles, dirtyBounds);
        }

        for (var y = dirtyBounds.MinTileY; y < dirtyBounds.MinTileY + dirtyBounds.Height; y++)
        {
            for (var x = dirtyBounds.MinTileX; x < dirtyBounds.MinTileX + dirtyBounds.Width; x++)
            {
                var tile = new WorldTileCoord(x, y);
                var skylightLevel = skylightMaskTiles.Contains(tile) ? indoor : outdoor;
                field.SetCell(tile, localLightLevels.GetValueOrDefault(tile), skylightLevel);
            }
        }

        lighting.UpdateInputs(snapshot.Bounds, snapshot.Emitters, snapshot.Occluders, snapshot.SkylightMasks);
        lighting.ApplyField(field);
    }

    private static LightingFieldBounds ResolveDirtyBounds(LightingRuntimeState lighting, LightingInputSnapshot snapshot)
    {
        if (lighting.IsStructureDirty || lighting.Field is null)
        {
            return snapshot.Bounds;
        }

        if (lighting.ValueDirtyBounds is not { } dirty)
        {
            return snapshot.Bounds;
        }

        var clamped = dirty.ClampTo(snapshot.Bounds);
        return clamped.IsEmpty ? snapshot.Bounds : clamped;
    }

    public static float ResolveOutdoorSkylightLevel(float timeOfDaySeconds, float dayLengthSeconds)
    {
        if (dayLengthSeconds <= 0f)
        {
            return DayOutdoorBrightness;
        }

        var clockHours = TimeOfDayRules.ResolveClockHours(timeOfDaySeconds, dayLengthSeconds);
        if (clockHours >= TimeOfDayRules.DayStartHour && clockHours < TimeOfDayRules.DuskStartHour)
        {
            return DayOutdoorBrightness;
        }

        if (clockHours >= TimeOfDayRules.DawnStartHour && clockHours < TimeOfDayRules.DayStartHour)
        {
            return Lerp(NightOutdoorBrightness, DayOutdoorBrightness, (clockHours - TimeOfDayRules.DawnStartHour) / (TimeOfDayRules.DayStartHour - TimeOfDayRules.DawnStartHour));
        }

        if (clockHours >= TimeOfDayRules.DuskStartHour && clockHours < TimeOfDayRules.NightStartHour)
        {
            return Lerp(DayOutdoorBrightness, NightOutdoorBrightness, (clockHours - TimeOfDayRules.DuskStartHour) / (TimeOfDayRules.NightStartHour - TimeOfDayRules.DuskStartHour));
        }

        if (clockHours < TimeOfDayRules.DawnStartHour)
        {
            return NightOutdoorBrightness;
        }

        return NightOutdoorBrightness;
    }

    private static int QuantizeSkylight(float outdoorLevel)
    {
        return (int)MathF.Round(Math.Clamp(outdoorLevel, 0f, 1f) * 32f);
    }

    private static float ResolveIndoorSkylightLevel(float outdoorLevel)
    {
        if (outdoorLevel <= NightOutdoorBrightness)
        {
            return NightIndoorBrightness;
        }

        if (outdoorLevel >= DayOutdoorBrightness)
        {
            return DayIndoorBrightness;
        }

        var t = (outdoorLevel - NightOutdoorBrightness) / (DayOutdoorBrightness - NightOutdoorBrightness);
        return Lerp(NightIndoorBrightness, DayIndoorBrightness, t);
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + ((to - from) * Math.Clamp(t, 0f, 1f));
    }

    private static void WriteEmitterLight(
        Dictionary<WorldTileCoord, float> localLightLevels,
        RuntimeLightEmitter emitter,
        HashSet<WorldTileCoord> occluderTiles,
        LightingFieldBounds dirtyBounds)
    {
        var radius = Math.Max(1, (int)MathF.Ceiling(emitter.RadiusTiles));
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var tile = new WorldTileCoord(emitter.WorldTile.X + dx, emitter.WorldTile.Y + dy);
                if (!dirtyBounds.Contains(tile))
                {
                    continue;
                }

                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                if (distance > emitter.RadiusTiles)
                {
                    continue;
                }

                if (!HasLineOfSight(emitter.WorldTile, tile, occluderTiles))
                {
                    continue;
                }

                var attenuation = 1f - (distance / Math.Max(0.001f, emitter.RadiusTiles));
                var level = Math.Clamp(attenuation * emitter.Intensity, 0f, 1f);
                if (level <= 0f)
                {
                    continue;
                }

                localLightLevels[tile] = Math.Max(localLightLevels.GetValueOrDefault(tile), level);
            }
        }
    }

    private static bool EmitterAffectsBounds(RuntimeLightEmitter emitter, LightingFieldBounds bounds)
    {
        var radius = Math.Max(1, (int)MathF.Ceiling(emitter.RadiusTiles));
        var emitterBounds = LightingFieldBounds.FromTile(emitter.WorldTile).Expand(radius);
        return emitterBounds.Intersects(bounds);
    }

    private static bool HasLineOfSight(WorldTileCoord source, WorldTileCoord target, HashSet<WorldTileCoord> occluderTiles)
    {
        var x0 = source.X;
        var y0 = source.Y;
        var x1 = target.X;
        var y1 = target.Y;
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        var currentX = x0;
        var currentY = y0;

        while (currentX != x1 || currentY != y1)
        {
            var twiceErr = err * 2;
            if (twiceErr > -dy)
            {
                err -= dy;
                currentX += sx;
            }

            if (twiceErr < dx)
            {
                err += dx;
                currentY += sy;
            }

            if (currentX == x1 && currentY == y1)
            {
                return true;
            }

            if (occluderTiles.Contains(new WorldTileCoord(currentX, currentY)))
            {
                return false;
            }
        }

        return true;
    }
}
