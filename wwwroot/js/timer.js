// Sound helpers for the countdown timer page.
// The chime is synthesized with the Web Audio API, so no audio files are needed.

let audioContext;
let titleTimerId = null;
let titleBeforeAlert = null;
let wakeLock = null;
const wakeLockOwners = new Set();
let wakeLockUnavailableReason = null;

async function requestWakeLock() {
    if (wakeLockOwners.size === 0) {
        return false;
    }

    if (!window.isSecureContext) {
        wakeLockUnavailableReason = "Screen Wake Lock requires HTTPS (except on localhost).";
        return false;
    }

    if (!("wakeLock" in navigator)) {
        wakeLockUnavailableReason = "Screen Wake Lock is not supported by this browser or iOS version.";
        return false;
    }

    if (document.visibilityState !== "visible") {
        wakeLockUnavailableReason = "Keep the app open in the foreground to keep the screen awake.";
        return false;
    }

    if (wakeLock && !wakeLock.released) {
        return true;
    }

    try {
        const requestedWakeLock = await navigator.wakeLock.request("screen");
        if (wakeLockOwners.size === 0 || document.visibilityState !== "visible") {
            await requestedWakeLock.release();
            return false;
        }

        wakeLock = requestedWakeLock;
        wakeLockUnavailableReason = null;
        requestedWakeLock.addEventListener("release", () => {
            if (wakeLock === requestedWakeLock) {
                wakeLock = null;
                if (wakeLockOwners.size > 0) {
                    wakeLockUnavailableReason = "iOS released the screen lock. Keep the app open in the foreground.";
                }
            }
        }, { once: true });
        return true;
    } catch {
        wakeLockUnavailableReason = "iOS could not keep the screen awake. Keep the app open in the foreground.";
        return false;
    }
}

document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") {
        void requestWakeLock();
    }
});

export async function startKeepingScreenAwake(owner = "timer") {
    wakeLockOwners.add(owner);
    return await requestWakeLock();
}

export async function stopKeepingScreenAwake(owner = "timer") {
    wakeLockOwners.delete(owner);
    if (wakeLockOwners.size > 0) {
        return;
    }

    const currentWakeLock = wakeLock;
    wakeLock = null;

    if (currentWakeLock && !currentWakeLock.released) {
        try {
            await currentWakeLock.release();
        } catch {
            // The browser may already have released the screen lock.
        }
    }
}

export function getScreenWakeLockUnavailableReason() {
    return wakeLockUnavailableReason;
}

function getAudioContext() {
    if (!audioContext || audioContext.state === "closed") {
        const AudioContextType = window.AudioContext ?? window.webkitAudioContext;
        audioContext = new AudioContextType();
    }
    return audioContext;
}

// Browsers only allow audio after a user gesture. Call this from a click handler
// once so the chime can play later, when the countdown finishes.
// The resume is intentionally not awaited: some browsers keep it pending until a
// later gesture, and the countdown must start regardless.
export function unlockAudio() {
    const context = getAudioContext();
    if (context.state === "suspended") {
        context.resume().catch(() => {
            // The chime simply stays silent when audio cannot be unlocked.
        });
    }
}

function scheduleChime(context, startTime) {
    const notes = [659.25, 880.0, 1108.73]; // E5, A5, C#6

    notes.forEach((frequency, index) => {
        const noteStart = startTime + index * 0.18;
        const oscillator = context.createOscillator();
        const gain = context.createGain();

        oscillator.type = "triangle";
        oscillator.frequency.value = frequency;

        gain.gain.setValueAtTime(0.0001, noteStart);
        gain.gain.exponentialRampToValueAtTime(0.3, noteStart + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, noteStart + 0.45);

        oscillator.connect(gain);
        gain.connect(context.destination);
        oscillator.start(noteStart);
        oscillator.stop(noteStart + 0.5);
    });
}

export async function playAlarm() {
    const context = getAudioContext();
    if (context.state === "suspended") {
        await context.resume();
    }

    const startTime = context.currentTime + 0.05;
    for (let round = 0; round < 3; round += 1) {
        scheduleChime(context, startTime + round * 1.1);
    }

    if (typeof navigator.vibrate === "function") {
        navigator.vibrate([180, 80, 180, 80, 180]);
    }
}

// Flash the tab title so the alert is noticeable when the tab is in the background.
export function startTitleAlert(message, restingTitle) {
    stopTitleAlert();
    titleBeforeAlert = restingTitle;
    let showAlert = true;

    document.title = message;
    titleTimerId = window.setInterval(() => {
        document.title = showAlert ? message : titleBeforeAlert;
        showAlert = !showAlert;
    }, 1200);
}

export function stopTitleAlert() {
    if (titleTimerId !== null) {
        window.clearInterval(titleTimerId);
        titleTimerId = null;
    }

    if (titleBeforeAlert !== null) {
        document.title = titleBeforeAlert;
        titleBeforeAlert = null;
    }
}
