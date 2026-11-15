namespace jtimer.Services;

/// <summary>
/// Formats durations for the timer screens, for example "45 sec", "1 min", or "1 min 30 sec".
/// </summary>
public static class DurationText
{
    public static string FromSeconds(int totalSeconds)
    {
        var seconds = Math.Max(totalSeconds, 0);
        if (seconds < 60)
        {
            return $"{seconds} sec";
        }

        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;
        var minutesText = minutes == 1 ? "1 min" : $"{minutes} min";

        return remainingSeconds == 0 ? minutesText : $"{minutesText} {remainingSeconds} sec";
    }

    public static string FromDuration(TimeSpan duration) => FromSeconds((int)duration.TotalSeconds);
}
