// Offline copy helpers: report what is stored on this device and reset the local shell.
// The cache name and lifetime are owned by service-worker.published.js.

const offlineCachePrefix = "jtimer-offline-cache-";

export async function getCacheStatus() {
    if (!("caches" in window)) {
        return { supported: false, ready: false, cachedFileCount: 0 };
    }

    const cacheKeys = (await caches.keys()).filter(key => key.startsWith(offlineCachePrefix));

    let cachedFileCount = 0;
    for (const cacheKey of cacheKeys) {
        const cache = await caches.open(cacheKey);
        cachedFileCount += (await cache.keys()).length;
    }

    let hasActiveWorker = false;
    if ("serviceWorker" in navigator) {
        const registration = await navigator.serviceWorker.getRegistration();
        hasActiveWorker = Boolean(registration && registration.active);
    }

    return {
        supported: true,
        ready: hasActiveWorker && cachedFileCount > 0,
        cachedFileCount
    };
}

// Removes the saved app files and the worker, then reloads from the network.
// Timer settings live in localStorage, so they are intentionally kept.
// Returns "offline" without touching anything when there is no connection, because
// clearing the copy would leave the app unable to start.
export async function clearCacheAndReload() {
    if (navigator.onLine === false) {
        return "offline";
    }

    if ("caches" in window) {
        const cacheKeys = (await caches.keys()).filter(key => key.startsWith(offlineCachePrefix));
        await Promise.all(cacheKeys.map(key => caches.delete(key)));
    }

    if ("serviceWorker" in navigator) {
        const registrations = await navigator.serviceWorker.getRegistrations();
        await Promise.all(registrations.map(registration => registration.unregister()));
    }

    // Refresh the app shell in the HTTP cache as well, so the reload cannot be served
    // from an older copy of index.html.
    try {
        await fetch(window.location.href, { cache: "reload", credentials: "same-origin" });
    } catch {
        // Offline: still reload so the user sees the browser's offline notice.
    }

    window.location.reload();
    return "reloading";
}
