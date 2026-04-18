namespace Downroot.Gameplay.Runtime;

public static class TimeOfDayRules
{
    public const float DawnStartHour = 5f;
    public const float DayStartHour = 7f;
    public const float DuskStartHour = 19f;
    public const float NightStartHour = 21f;
    public const float ClockStartHour = 6f;

    public static float NormalizeProgress(float timeOfDaySeconds, float dayLengthSeconds)
    {
        if (dayLengthSeconds <= 0f)
        {
            return 0f;
        }

        return ((timeOfDaySeconds % dayLengthSeconds) + dayLengthSeconds) % dayLengthSeconds / dayLengthSeconds;
    }

    public static float ResolveClockHours(float timeOfDaySeconds, float dayLengthSeconds)
    {
        return ResolveClockHours(NormalizeProgress(timeOfDaySeconds, dayLengthSeconds));
    }

    public static float ResolveClockHours(float timeProgress)
    {
        return (timeProgress * 24f + ClockStartHour) % 24f;
    }

    public static bool IsNight(float timeOfDaySeconds, float dayLengthSeconds)
    {
        var clockHours = ResolveClockHours(timeOfDaySeconds, dayLengthSeconds);
        return clockHours >= NightStartHour || clockHours < DawnStartHour;
    }

    public static float ResolveNormalizedTimeForHour(float clockHour)
    {
        var normalizedHour = ((clockHour % 24f) + 24f) % 24f;
        return ((normalizedHour - ClockStartHour + 24f) % 24f) / 24f;
    }
}
