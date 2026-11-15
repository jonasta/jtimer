// Offline-first caching for the published Blazor WebAssembly app.
// Background: https://learn.microsoft.com/aspnet/core/blazor/progressive-web-app
self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'jtimer-offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;

// Never precache the worker itself, the per-deploy asset manifest, or host-only
// configuration that Azure Static Web Apps does not serve as a file.
const precacheExclude = [
    /^service-worker\.js$/,
    /^service-worker-assets\.js$/,
    /^staticwebapp\.config\.json$/
];

// The app shell must exist locally. If it cannot be cached, activating the worker
// would only make the app fail later, so the install is retried on the next visit.
const requiredAssets = [
    'index.html',
    '_framework/blazor.webassembly.js',
    '_framework/blazor.boot.json'
];

self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

async function onInstall() {
    const cache = await caches.open(cacheName);
    const assetsToCache = self.assetsManifest.assets
        .filter(asset => !precacheExclude.some(pattern => pattern.test(asset.url)));

    // Cache each asset independently: `cache.addAll` is all-or-nothing, so a single
    // transient failure would abort the install and leave the app with no offline copy.
    const failedUrls = [];
    await Promise.all(assetsToCache.map(async asset => {
        const cached = await cacheAsset(cache, asset);
        if (!cached) {
            failedUrls.push(asset.url);
        }
    }));

    const shellMissing = [];
    for (const url of requiredAssets) {
        if (!(await cache.match(url))) {
            shellMissing.push(url);
        }
    }

    if (shellMissing.length > 0) {
        await caches.delete(cacheName);
        throw new Error(`Service worker install incomplete; missing ${shellMissing.join(', ')}`);
    }

    if (failedUrls.length > 0) {
        // Non-essential files (source maps, extra data) are fetched again on first use
        // and stored then, so the app still opens offline after they load once.
        console.warn(`Service worker: ${failedUrls.length} assets will be cached on first use.`, failedUrls);
    }
}

async function cacheAsset(cache, asset) {
    // One retry covers a flaky first request without delaying the install much.
    for (let attempt = 0; attempt < 2; attempt++) {
        try {
            const request = new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' });
            const response = await fetch(request);
            if (response.ok) {
                await cache.put(asset.url, response);
                return true;
            }
        } catch (error) {
            if (attempt === 1) {
                console.warn(`Service worker: could not precache ${asset.url}`, error);
            }
        }
    }

    return false;
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    // Start serving the cache immediately, including pages opened before activation.
    await self.clients.claim();
}

async function onFetch(event) {
    const { request } = event;

    if (request.method !== 'GET') {
        return fetch(request);
    }

    if (new URL(request.url).origin !== self.origin) {
        return fetch(request);
    }

    // A hard refresh is an explicit request for fresh content: try the network first,
    // then fall back to the cached copy when the device is offline.
    if (request.cache === 'reload') {
        return await fetchAndCache(request);
    }

    const cache = await caches.open(cacheName);
    const cachedResponse = await matchCached(cache, request);
    if (cachedResponse) {
        return cachedResponse;
    }

    // Anything not covered by the precache (for example a newly added asset) is fetched
    // once and stored, so later launches work offline as well.
    return await fetchAndCache(request);
}

async function matchCached(cache, request) {
    if (request.mode === 'navigate') {
        // Every client-side route is served by the same app shell.
        return (await cache.match('index.html')) || (await cache.match(request));
    }

    return await cache.match(request);
}

async function fetchAndCache(request) {
    const cache = await caches.open(cacheName);

    try {
        const response = await fetch(request);
        if (response.ok && response.type !== 'opaque') {
            await cache.put(request, response.clone());
        }
        return response;
    } catch (error) {
        const cachedResponse = await matchCached(cache, request);
        if (cachedResponse) {
            return cachedResponse;
        }

        if (request.mode === 'navigate') {
            return offlinePage();
        }

        throw error;
    }
}

// Shown only when the device is offline and no saved copy exists yet.
function offlinePage() {
    const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>jtimer — Offline</title>
<style>
body {
    margin: 0;
    min-height: 100vh;
    display: grid;
    place-items: center;
    padding: 2rem;
    background: #0f1426;
    color: #eef1ff;
    font-family: system-ui, -apple-system, sans-serif;
    text-align: center;
}
h1 { margin: 0 0 0.6rem; font-size: 1.15rem; }
p { margin: 0; max-width: 24rem; font-size: 0.9rem; line-height: 1.4; color: rgba(238, 241, 255, 0.7); }
</style>
</head>
<body>
<main>
    <h1>jtimer has no saved copy yet</h1>
    <p>Connect to the internet once and reopen jtimer. After that it starts without a connection.</p>
</main>
</body>
</html>`;

    return new Response(html, {
        status: 200,
        headers: { 'content-type': 'text/html; charset=utf-8' }
    });
}
