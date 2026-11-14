// Offline-first caching follows the Blazor WebAssembly PWA template.
// Review the offline caveats before publishing changes to cached application assets:
// https://learn.microsoft.com/aspnet/core/blazor/progressive-web-app
self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'jtimer-offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
    /\.dll$/,
    /\.pdb$/,
    /\.wasm$/,
    /\.html$/,
    /\.js$/,
    /\.json$/,
    /\.webmanifest$/,
    /\.webmanifest$/,
    /\.css$/,
    /\.woff2?$/,
    /\.png$/,
    /\.jpe?g$/,
    /\.gif$/,
    /\.ico$/,
    /\.blat$/,
    /\.dat$/
];
const offlineAssetsExclude = [/^service-worker\.js$/];
const baseUrl = new URL('/', self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    if (event.request.method !== 'GET') {
        return fetch(event.request);
    }

    const shouldServeIndexHtml = event.request.mode === 'navigate'
        && !manifestUrlList.includes(event.request.url);
    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    return await cache.match(request) || fetch(event.request);
}
