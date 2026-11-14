using Microsoft.JSInterop;

namespace jtimer.Services;

public enum CountdownState
{
    Idle,
    Running,
    Paused,
    Finished
}

/// <summary>
/// Keeps the countdown running even when the user switches to the configuration screen.
/// The UI subscribes to <see cref="Changed"/> and renders this state.
/// </summary>
public sealed class CountdownTimer : IAsyncDisposable
{
    private const int TickIntervalMilliseconds = 200;
    private const int FinishedAlertMilliseconds = 2000;
    private const string BaseTitle = "jtimer";

    private readonly IJSRuntime js;
    private readonly TimerPreferences preferences;

    private DateTimeOffset? deadline;
    private CancellationTokenSource? tickerCancellation;
    private CancellationTokenSource? finishedAlertCancellation;
    private IJSObjectReference? timerModule;
    private bool isDisposed;

    public CountdownTimer(IJSRuntime js, TimerPreferences preferences)
    {
        this.js = js;
        this.preferences = preferences;
    }

    public event Action? Changed;

    public CountdownState State { get; private set; } = CountdownState.Idle;

    public TimeSpan TotalDuration { get; private set; }

    public TimeSpan Remaining { get; private set; }

    public bool ShowFinishedAlert { get; private set; }

    public bool IsBusy => State is CountdownState.Running or CountdownState.Paused;

    public double RemainingFraction =>
        TotalDuration <= TimeSpan.Zero ? 0 : Math.Clamp(Remaining / TotalDuration, 0, 1);

    public async Task SetDurationAsync(TimeSpan duration)
    {
        if (IsBusy)
        {
            return;
        }

        var wasFinished = State == CountdownState.Finished;
        TotalDuration = duration;
        Remaining = duration;
        State = CountdownState.Idle;
        ClearFinishedAlert();

        if (wasFinished)
        {
            await InvokeTimerModuleAsync("stopTitleAlert");
        }

        Changed?.Invoke();
    }

    public async Task StartAsync(TimeSpan duration)
    {
        if (IsBusy || duration <= TimeSpan.Zero)
        {
            return;
        }

        await preferences.EnsureLoadedAsync();
        await InvokeTimerModuleAsync("unlockAudio");
        await InvokeTimerModuleAsync("startKeepingScreenAwake", "countdown-timer");
        await InvokeTimerModuleAsync("stopTitleAlert");

        TotalDuration = duration;
        Remaining = duration;
        ClearFinishedAlert();
        await RunAsync();
    }

    public async Task ResumeAsync()
    {
        if (Remaining <= TimeSpan.Zero)
        {
            return;
        }

        await InvokeTimerModuleAsync("startKeepingScreenAwake", "countdown-timer");
        await RunAsync();
    }

    public void Pause()
    {
        if (State != CountdownState.Running)
        {
            return;
        }

        if (deadline is { } currentDeadline)
        {
            var timeLeft = currentDeadline - DateTimeOffset.UtcNow;
            Remaining = timeLeft > TimeSpan.Zero
                ? TimeSpan.FromSeconds(Math.Ceiling(timeLeft.TotalSeconds))
                : TimeSpan.Zero;
        }

        State = CountdownState.Paused;
        CancelTicker();
        _ = InvokeTimerModuleAsync("stopKeepingScreenAwake", "countdown-timer");
        Changed?.Invoke();
    }

    public async Task ResetAsync()
    {
        CancelTicker();
        deadline = null;
        State = CountdownState.Idle;
        Remaining = TotalDuration;
        ClearFinishedAlert();
        await InvokeTimerModuleAsync("stopKeepingScreenAwake", "countdown-timer");
        await InvokeTimerModuleAsync("stopTitleAlert");
        Changed?.Invoke();
    }

    public async Task PreviewAlarmAsync()
    {
        await preferences.EnsureLoadedAsync();
        await InvokeTimerModuleAsync("playAlarm");
    }

    private async Task RunAsync()
    {
        if (State == CountdownState.Running)
        {
            return;
        }

        CancelTicker();

        var cancellation = new CancellationTokenSource();
        tickerCancellation = cancellation;
        deadline = DateTimeOffset.UtcNow + Remaining;
        State = CountdownState.Running;
        var completed = false;
        Changed?.Invoke();

        try
        {
            while (true)
            {
                var timeLeft = deadline.Value - DateTimeOffset.UtcNow;
                if (timeLeft <= TimeSpan.Zero)
                {
                    completed = true;
                    break;
                }

                Remaining = TimeSpan.FromSeconds(Math.Ceiling(timeLeft.TotalSeconds));
                Changed?.Invoke();
                await Task.Delay(TickIntervalMilliseconds, cancellation.Token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            if (tickerCancellation == cancellation)
            {
                tickerCancellation = null;
                deadline = null;
            }

            cancellation.Dispose();
        }

        if (!completed || isDisposed)
        {
            return;
        }

        Remaining = TimeSpan.Zero;
        State = CountdownState.Finished;
        await InvokeTimerModuleAsync("stopKeepingScreenAwake", "countdown-timer");
        Changed?.Invoke();

        await PlayFinishedAlertAsync();
    }

    private async Task PlayFinishedAlertAsync()
    {
        await preferences.EnsureLoadedAsync();

        StartFinishedAlert();

        if (preferences.SoundEnabled)
        {
            _ = InvokeTimerModuleAsync("playAlarm");
        }

        await InvokeTimerModuleAsync("startTitleAlert", "⏰ Time's up!", BaseTitle);
    }

    private void StartFinishedAlert()
    {
        if (!preferences.ColorAlertsEnabled)
        {
            return;
        }

        ShowFinishedAlert = true;
        Changed?.Invoke();

        var cancellation = new CancellationTokenSource();
        finishedAlertCancellation = cancellation;
        _ = HideFinishedAlertLaterAsync(cancellation);
    }

    private async Task HideFinishedAlertLaterAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(FinishedAlertMilliseconds, cancellation.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        finally
        {
            cancellation.Dispose();
        }

        if (isDisposed || finishedAlertCancellation != cancellation)
        {
            return;
        }

        finishedAlertCancellation = null;
        ShowFinishedAlert = false;
        Changed?.Invoke();
    }

    private void ClearFinishedAlert()
    {
        finishedAlertCancellation?.Cancel();
        finishedAlertCancellation = null;
        ShowFinishedAlert = false;
    }

    private void CancelTicker() => tickerCancellation?.Cancel();

    private async Task<IJSObjectReference?> GetTimerModuleAsync()
    {
        if (timerModule is not null)
        {
            return timerModule;
        }

        try
        {
            timerModule = await js.InvokeAsync<IJSObjectReference>("import", "./js/timer.js");
        }
        catch (JSException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }

        return timerModule;
    }

    private async Task InvokeTimerModuleAsync(string identifier, params object?[] arguments)
    {
        var module = await GetTimerModuleAsync();
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync(identifier, arguments);
        }
        catch (JSException)
        {
            // Sound and the tab title are nice-to-haves; never let them break the timer.
        }
        catch (JSDisconnectedException)
        {
            // The browser is already gone; nothing left to update.
        }
    }

    public async ValueTask DisposeAsync()
    {
        isDisposed = true;
        CancelTicker();
        finishedAlertCancellation?.Cancel();

        await InvokeTimerModuleAsync("stopKeepingScreenAwake", "countdown-timer");
        await InvokeTimerModuleAsync("stopTitleAlert");

        if (timerModule is not null)
        {
            try
            {
                await timerModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser is already gone; nothing left to clean up.
            }
        }
    }
}
