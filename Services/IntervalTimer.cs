using Microsoft.JSInterop;

namespace jtimer.Services;

public enum IntervalPhase
{
    Ready,
    Prepare,
    Exercise,
    Rest,
    Finished
}

/// <summary>
/// Runs the shared prepare, exercise, and rest sequence across screen navigation.
/// </summary>
public sealed class IntervalTimer : IAsyncDisposable
{
    private static readonly TimeSpan PrepareDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ExerciseDuration = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan RestDuration = TimeSpan.FromMinutes(1);
    public const int TotalRoundsForDisplay = 4;
    private const int TotalRounds = TotalRoundsForDisplay;
    private const int TickIntervalMilliseconds = 200;

    private readonly IJSRuntime js;
    private readonly TimerPreferences preferences;

    private CancellationTokenSource? runCancellation;
    private IJSObjectReference? timerModule;
    private DateTimeOffset? deadline;
    private bool isDisposed;

    public IntervalTimer(IJSRuntime js, TimerPreferences preferences)
    {
        this.js = js;
        this.preferences = preferences;
        Remaining = PrepareDuration;
    }

    public event Action? Changed;

    public IntervalPhase Phase { get; private set; } = IntervalPhase.Ready;

    public int CurrentRound { get; private set; } = 1;

    public TimeSpan Remaining { get; private set; }

    public TimeSpan PhaseDuration => Phase switch
    {
        IntervalPhase.Prepare => PrepareDuration,
        IntervalPhase.Exercise => ExerciseDuration,
        IntervalPhase.Rest => RestDuration,
        _ => TimeSpan.Zero
    };

    public bool IsRunning { get; private set; }

    public bool IsPaused { get; private set; }

    public string? ScreenWakeLockMessage { get; private set; }

    public async Task StartAsync()
    {
        if (IsRunning || IsPaused)
        {
            return;
        }

        if (Phase == IntervalPhase.Finished)
        {
            Reset();
        }

        Phase = IntervalPhase.Prepare;
        CurrentRound = 1;
        Remaining = PrepareDuration;
        IsPaused = false;
        await preferences.EnsureLoadedAsync();
        await InvokeTimerModuleAsync("unlockAudio");
        await SetScreenAwakeAsync(true);
        StartRun();
        Changed?.Invoke();
    }

    public async Task ResumeAsync()
    {
        if (!IsPaused || Remaining <= TimeSpan.Zero)
        {
            return;
        }

        IsPaused = false;
        await SetScreenAwakeAsync(true);
        StartRun();
        Changed?.Invoke();
    }

    public async Task PauseAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        UpdateRemainingFromDeadline();
        IsRunning = false;
        IsPaused = true;
        runCancellation?.Cancel();
        await SetScreenAwakeAsync(false);
        Changed?.Invoke();
    }

    public async Task ResetAsync()
    {
        runCancellation?.Cancel();
        runCancellation = null;
        deadline = null;
        IsRunning = false;
        IsPaused = false;
        Phase = IntervalPhase.Ready;
        CurrentRound = 1;
        Remaining = PrepareDuration;
        await SetScreenAwakeAsync(false);
        Changed?.Invoke();
    }

    private void Reset()
    {
        runCancellation?.Cancel();
        runCancellation = null;
        deadline = null;
        IsRunning = false;
        IsPaused = false;
        Phase = IntervalPhase.Ready;
        CurrentRound = 1;
        Remaining = PrepareDuration;
    }

    private void StartRun()
    {
        var cancellation = new CancellationTokenSource();
        runCancellation = cancellation;
        IsRunning = true;
        deadline = DateTimeOffset.UtcNow + Remaining;
        _ = RunAsync(cancellation);
    }

    private async Task RunAsync(CancellationTokenSource cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested && !isDisposed)
            {
                var timeLeft = deadline!.Value - DateTimeOffset.UtcNow;
                if (timeLeft <= TimeSpan.Zero)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        return;
                    }

                    var previousDeadline = deadline.Value;
                    if (!AdvancePhase())
                    {
                        IsRunning = false;
                        IsPaused = false;
                        deadline = null;
                        Changed?.Invoke();
                        await PlayPhaseChimeAsync();
                        await SetScreenAwakeAsync(false);
                        return;
                    }

                    deadline = previousDeadline + Remaining;
                    Changed?.Invoke();
                    await PlayPhaseChimeAsync();
                    continue;
                }

                Remaining = TimeSpan.FromSeconds(Math.Ceiling(timeLeft.TotalSeconds));
                Changed?.Invoke();
                await Task.Delay(TickIntervalMilliseconds, cancellation.Token);
            }
        }
        catch (TaskCanceledException)
        {
            // Pausing or resetting cancels the current phase loop.
        }
        finally
        {
            if (runCancellation == cancellation)
            {
                runCancellation = null;
                deadline = null;
            }

            cancellation.Dispose();
        }
    }

    private bool AdvancePhase()
    {
        switch (Phase)
        {
            case IntervalPhase.Prepare:
                Phase = IntervalPhase.Exercise;
                Remaining = ExerciseDuration;
                return true;

            case IntervalPhase.Exercise when CurrentRound < TotalRounds:
                Phase = IntervalPhase.Rest;
                Remaining = RestDuration;
                return true;

            case IntervalPhase.Rest:
                CurrentRound++;
                Phase = IntervalPhase.Exercise;
                Remaining = ExerciseDuration;
                return true;

            case IntervalPhase.Exercise:
                Phase = IntervalPhase.Finished;
                Remaining = TimeSpan.Zero;
                return false;

            default:
                Phase = IntervalPhase.Finished;
                Remaining = TimeSpan.Zero;
                return false;
        }
    }

    private void UpdateRemainingFromDeadline()
    {
        if (deadline is not { } currentDeadline)
        {
            return;
        }

        var timeLeft = currentDeadline - DateTimeOffset.UtcNow;
        Remaining = timeLeft > TimeSpan.Zero
            ? TimeSpan.FromSeconds(Math.Ceiling(timeLeft.TotalSeconds))
            : TimeSpan.Zero;
    }

    private async Task PlayPhaseChimeAsync()
    {
        await preferences.EnsureLoadedAsync();
        if (!preferences.SoundEnabled)
        {
            return;
        }

        await InvokeTimerModuleAsync("playIntervalChime");
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
            // Sound is a nice-to-have; never let it break the timer.
        }
        catch (JSDisconnectedException)
        {
            // The browser is already gone; nothing left to update.
        }
    }

    private async Task SetScreenAwakeAsync(bool shouldStayAwake)
    {
        var module = await GetTimerModuleAsync();
        if (module is null)
        {
            if (shouldStayAwake)
            {
                ScreenWakeLockMessage = "Screen wake lock is unavailable in this browser.";
            }

            return;
        }

        try
        {
            if (!shouldStayAwake)
            {
                await module.InvokeVoidAsync("stopKeepingScreenAwake", "interval-timer");
                ScreenWakeLockMessage = null;
                return;
            }

            var lockAcquired = await module.InvokeAsync<bool>("startKeepingScreenAwake", "interval-timer");
            ScreenWakeLockMessage = lockAcquired
                ? null
                : await module.InvokeAsync<string?>("getScreenWakeLockUnavailableReason");
        }
        catch (JSException)
        {
            ScreenWakeLockMessage = shouldStayAwake
                ? "Screen wake lock is unavailable. Use HTTPS and keep this app in the foreground."
                : null;
        }
        catch (JSDisconnectedException)
        {
            // The browser is already gone.
        }
    }

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

    public async ValueTask DisposeAsync()
    {
        isDisposed = true;
        runCancellation?.Cancel();
        await SetScreenAwakeAsync(false);

        if (timerModule is not null)
        {
            try
            {
                await timerModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser is already gone.
            }
        }
    }
}
