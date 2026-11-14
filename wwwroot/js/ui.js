// Timer screen helpers: quick presets and tap-to-select duration fields.
// Sound and tab-title alerts live in timer.js, owned by the countdown service.

// Quick presets live in localStorage so the user's own durations survive a reload.
const presetStorageKey = "jtimer.presets";

export function loadPresets() {
    const storedValue = window.localStorage.getItem(presetStorageKey);
    if (storedValue === null) {
        return null;
    }

    try {
        const parsedValue = JSON.parse(storedValue);
        return Array.isArray(parsedValue) ? parsedValue : null;
    } catch {
        // Corrupt storage should not block the page; fall back to the defaults.
        return null;
    }
}

export function savePresets(presets) {
    try {
        window.localStorage.setItem(presetStorageKey, JSON.stringify(presets));
    } catch {
        // Private browsing or a full quota only means presets stay in memory.
    }
}

// Tapping a duration field selects its value, so the next number replaces it.
let selectOnFocusEnabled = false;

export function enableSelectOnFocus() {
    if (selectOnFocusEnabled) {
        return;
    }
    selectOnFocusEnabled = true;

    const selectFieldText = (event) => {
        const input = event.target;
        if (input instanceof HTMLInputElement && input.dataset.selectOnFocus !== undefined) {
            input.select();
        }
    };

    document.addEventListener("focusin", selectFieldText);
    document.addEventListener("click", selectFieldText);
}
