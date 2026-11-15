namespace jtimer.Services;

/// <summary>
/// Plan for the interval workout: how long each phase lasts (in seconds) and how many
/// rounds it repeats. Shared by the Configuration screen (editing) and the Intervals screen
/// (display), and stored with the other preferences.
/// </summary>
public sealed record IntervalSettings(int PrepareSeconds, int ExerciseSeconds, int RestSeconds, int Rounds)
{
    public const int MinimumSeconds = 5;
    public const int MaximumSeconds = 600;
    public const int MinimumRounds = 1;
    public const int MaximumRounds = 12;

    public static IntervalSettings Default { get; } = new(10, 50, 60, 4);

    public TimeSpan Prepare => TimeSpan.FromSeconds(PrepareSeconds);

    public TimeSpan Exercise => TimeSpan.FromSeconds(ExerciseSeconds);

    public TimeSpan Rest => TimeSpan.FromSeconds(RestSeconds);

    /// <summary>
    /// Length of a full workout: one prepare phase, one exercise phase per round, and one
    /// rest phase between rounds.
    /// </summary>
    public TimeSpan TotalDuration =>
        Prepare + (Exercise * Rounds) + (Rest * Math.Max(Rounds - 1, 0));

    /// <summary>Keeps every value inside the range the Configuration screen allows.</summary>
    public IntervalSettings Clamp() => new(
        Math.Clamp(PrepareSeconds, MinimumSeconds, MaximumSeconds),
        Math.Clamp(ExerciseSeconds, MinimumSeconds, MaximumSeconds),
        Math.Clamp(RestSeconds, MinimumSeconds, MaximumSeconds),
        Math.Clamp(Rounds, MinimumRounds, MaximumRounds));
}
