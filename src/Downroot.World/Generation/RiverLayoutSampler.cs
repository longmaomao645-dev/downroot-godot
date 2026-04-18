using Downroot.Core.World;

namespace Downroot.World.Generation;

public static class RiverLayoutSampler
{
    public static float SampleCenterDistanceNormalized(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return float.MaxValue;
        }

        var angle = SampleRiverAngle(worldSpaceKind, worldSeed);
        var riverFrame = ProjectToRiverFrame(worldTile, angle);
        var centerOffset = SampleCenterOffset(worldSpaceKind, worldSeed, riverFrame.FlowCoord);
        var width = SampleWidth(worldSpaceKind, worldSeed, riverFrame.FlowCoord);
        return MathF.Abs(riverFrame.CrossCoord - centerOffset) / MathF.Max(1.35f, width);
    }

    public static float SampleLocalWidth(
        WorldSpaceKind worldSpaceKind,
        int worldSeed,
        WorldTileCoord worldTile)
    {
        if (worldSpaceKind != WorldSpaceKind.Overworld)
        {
            return 1f;
        }

        var angle = SampleRiverAngle(worldSpaceKind, worldSeed);
        var riverFrame = ProjectToRiverFrame(worldTile, angle);
        return SampleWidth(worldSpaceKind, worldSeed, riverFrame.FlowCoord);
    }

    private static float SampleRiverAngle(WorldSpaceKind worldSpaceKind, int worldSeed)
    {
        var angleRoll = StableWorldNoise.HashToUnitFloat(worldSpaceKind, worldSeed, worldSeed ^ 1301, 0, 2303);
        var degrees = 20f + (angleRoll * 140f);
        return degrees * (MathF.PI / 180f);
    }

    private static (float FlowCoord, float CrossCoord) ProjectToRiverFrame(WorldTileCoord tile, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var flowCoord = (tile.X * cos) + (tile.Y * sin);
        var crossCoord = (-tile.X * sin) + (tile.Y * cos);
        return (flowCoord, crossCoord);
    }

    private static float SampleCenterOffset(WorldSpaceKind worldSpaceKind, int worldSeed, float flowCoord)
    {
        var broad = (StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, flowCoord * 0.012f, 0.37f, 2101) - 0.5f) * 36f;
        var medium = (StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, flowCoord * 0.026f, 1.11f, 2113) - 0.5f) * 16f;
        var shapeTemplate = StableWorldNoise.HashToUnitFloat(worldSpaceKind, worldSeed, worldSeed ^ 2117, 0, 2129);
        var sineAmplitude = 2.5f + (shapeTemplate * 4.5f);
        var sineFrequency = 0.018f + (shapeTemplate * 0.020f);
        var sine = MathF.Sin((flowCoord * sineFrequency) + (worldSeed * 0.00091f)) * sineAmplitude;
        return broad + medium + sine;
    }

    private static float SampleWidth(WorldSpaceKind worldSpaceKind, int worldSeed, float flowCoord)
    {
        var broad = StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, flowCoord * 0.010f, 2.17f, 2203);
        var detail = StableWorldNoise.SampleValueNoise(worldSpaceKind, worldSeed, flowCoord * 0.021f, 3.41f, 2219);
        return 2.7f + (broad * 1.25f) + (detail * 0.85f);
    }
}
