using System.Globalization;
using Microsoft.JSInterop;

namespace jtimer.Services;

/// <summary>
/// User preferences shared by the timer and configuration screens.
/// Values are kept in localStorage so they survive reloads and screen changes.
/// </summary>
public sealed class TimerPreferences : IAsyncDisposable
{
    private const string SoundEnabledKey = "jtimer.soundEnabled";
    private const string ColorAlertsEnabledKey = "jtimer.colorAlertsEnabled";
    private const string ThemeColorKey = "jtimer.themeColor";
    private const string PrepareSecondsKey = "jtimer.interval.prepareSeconds";
    private const string ExerciseSecondsKey = "jtimer.interval.exerciseSeconds";
    private const string RestSecondsKey = "jtimer.interval.restSeconds";
    private const string RoundsKey = "jtimer.interval.rounds";

    private readonly IJSRuntime js;
    private Task? loadingTask;
    private IJSObjectReference? themeModule;

    public TimerPreferences(IJSRuntime js)
    {
        this.js = js;
    }

    public bool SoundEnabled { get; private set; } = true;

    public bool ColorAlertsEnabled { get; private set; } = true;

    public string ThemeColor { get; private set; } = AppThemes.DefaultId;

    public IntervalSettings Intervals { get; private set; } = IntervalSettings.Default;

    public Task EnsureLoadedAsync() => loadingTask ??= LoadAsync();

    public async Task SetSoundEnabledAsync(bool value)
    {
        SoundEnabled = value;
        await WriteAsync(SoundEnabledKey, value);
    }

    public async Task SetColorAlertsEnabledAsync(bool value)
    {
        ColorAlertsEnabled = value;
        await WriteAsync(ColorAlertsEnabledKey, value);
    }

    public async Task SetThemeColorAsync(string themeId)
    {
        ThemeColor = AppThemes.Normalize(themeId);
        await WriteAsync(ThemeColorKey, ThemeColor);
        await ApplyThemeAsync(ThemeColor);
    }

    public async Task SetIntervalsAsync(IntervalSettings intervals)
    {
        Intervals = intervals.Clamp();
        await WriteAsync(PrepareSecondsKey, Intervals.PrepareSeconds.ToString(CultureInfo.InvariantCulture));
        await WriteAsync(ExerciseSecondsKey, Intervals.ExerciseSeconds.ToString(CultureInfo.InvariantCulture));
        await WriteAsync(RestSecondsKey, Intervals.RestSeconds.ToString(CultureInfo.InvariantCulture));
        await WriteAsync(RoundsKey, Intervals.Rounds.ToString(CultureInfo.InvariantCulture));
    }

    private async Task LoadAsync()
    {
        SoundEnabled = await ReadAsync(SoundEnabledKey, true);
        ColorAlertsEnabled = await ReadAsync(ColorAlertsEnabledKey, true);
        ThemeColor = AppThemes.Normalize(await ReadStringAsync(ThemeColorKey));
        Intervals = new IntervalSettings(
            await ReadIntAsync(PrepareSecondsKey, IntervalSettings.Default.PrepareSeconds),
            await ReadIntAsync(ExerciseSecondsKey, IntervalSettings.Default.ExerciseSeconds),
            await ReadIntAsync(RestSecondsKey, IntervalSettings.Default.RestSeconds),
            await ReadIntAsync(RoundsKey, IntervalSettings.Default.Rounds)).Clamp();
        await ApplyThemeAsync(ThemeColor);
    }

    private async Task<bool> ReadAsync(string key, bool fallback)
    {
        var storedValue = await ReadStringAsync(key);
        return bool.TryParse(storedValue, out var parsedValue) ? parsedValue : fallback;
    }

    private async Task<int> ReadIntAsync(string key, int fallback)
    {
        var storedValue = await ReadStringAsync(key);
        return int.TryParse(storedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : fallback;
    }

    private async Task<string?> ReadStringAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    private Task WriteAsync(string key, bool value) => WriteAsync(key, value ? "true" : "false");

    private async Task WriteAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // Storage is optional; the preference still applies for this session.
        }
        catch (JSDisconnectedException)
        {
            // The browser is already gone; nothing left to save.
        }
    }

    private async Task ApplyThemeAsync(string themeId)
    {
        try
        {
            themeModule ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/theme.js");
            await themeModule.InvokeVoidAsync("applyTheme", themeId);
        }
        catch (JSException)
        {
            // The palette still works for the current session if the module cannot load.
        }
        catch (JSDisconnectedException)
        {
            // The browser is already gone; nothing left to update.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (themeModule is null)
        {
            return;
        }

        try
        {
            await themeModule.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The browser is already gone; nothing left to clean up.
        }
    }
}
