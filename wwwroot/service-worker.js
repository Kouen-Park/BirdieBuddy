const CACHE_NAME = 'birdiebuddy-shell-v3';
const SHELL = [
  '/',
  '/index.html',
  '/live-round.html',
  '/rounds.html',
  '/round-details.html',
  '/css/styles.css?v=20260909-records',
  '/css/styles.css?v=20260908-local-assets',
  '/js/api.js?v=20260918-member-navigation',
  '/js/live-store.js?v=20260903-rail3',
  '/js/live-round.js?v=20260903-rail3',
  '/vendor/fonts/outfit-400.ttf',
  '/vendor/fonts/outfit-500.ttf',
  '/vendor/fonts/outfit-600.ttf',
  '/vendor/fonts/space-grotesk-600.ttf',
  '/vendor/fonts/dm-mono-400.ttf',
  '/icons/birdie-buddy.svg'
];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(SHELL)));
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(
    keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))
  )));
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== 'GET' || url.origin !== self.location.origin || url.pathname.startsWith('/api')) return;

  const isDocument = request.mode === 'navigate' || url.pathname.endsWith('.html') || url.pathname === '/';
  event.respondWith(isDocument
    ? fetch(request).then(response => {
        const copy = response.clone();
        caches.open(CACHE_NAME).then(cache => cache.put(request, copy));
        return response;
      }).catch(() => caches.match(request).then(cached => cached || caches.match('/live-round.html')))
    : caches.match(request).then(cached => cached || fetch(request).then(response => {
        const copy = response.clone();
        caches.open(CACHE_NAME).then(cache => cache.put(request, copy));
        return response;
      })));
});
