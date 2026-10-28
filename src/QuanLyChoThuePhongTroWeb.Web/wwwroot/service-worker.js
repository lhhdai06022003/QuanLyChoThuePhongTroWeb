// TroSync Service Worker - Progressive Web App (PWA)
// Quy tắc an toàn: Không cache POST, Auth, SignalR, Payment Webhooks.
const CACHE_NAME = 'trosync-static-v1';
const STATIC_ASSETS = [
    '/css/site.css',
    '/css/khach-vang-lai.css',
    '/css/tenant-bento.css',
    '/css/trosync-tokens.css',
    '/images/icons/icon.svg',
    '/favicon.ico'
];

// 1. Cài đặt Service Worker
self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => {
            return cache.addAll(STATIC_ASSETS).catch(err => {
                console.warn('[SW] Caching non-critical static assets encountered issue:', err);
            });
        })
    );
    self.skipWaiting();
});

// 2. Kích hoạt và dọn dẹp cache cũ
self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cache => {
                    if (cache !== CACHE_NAME) {
                        return caches.delete(cache);
                    }
                })
            );
        }).then(() => self.clients.claim())
    );
});

// 3. Xử lý fetch request theo từng chiến lược
self.addEventListener('fetch', event => {
    const request = event.request;
    const url = new URL(request.url);

    // Chỉ can thiệp các request cùng origin và phương thức GET
    if (request.method !== 'GET' || url.origin !== self.location.origin) {
        return;
    }

    // A. TUYỆT ĐỐI NETWORK-ONLY: SignalR, xác thực, API, thanh toán, webhooks
    const pathname = url.pathname.toLowerCase();
    const isNetworkOnly = 
        pathname.includes('/thongbaohub') ||
        pathname.includes('/api/') ||
        pathname.includes('/quanlynhatro/dangnhap') ||
        pathname.includes('/dangxuat') ||
        pathname.includes('/account/') ||
        pathname.includes('/vnpay') ||
        pathname.includes('/sepay');

    if (isNetworkOnly) {
        return; // Để trình duyệt xử lý qua mạng thông thường
    }

    // B. NETWORK-FIRST CHO TRANG HTML (Razor Views)
    // Đảm bảo luôn lấy phiên bản mới nhất, đúng trạng thái phiên đăng nhập & AntiForgeryToken
    const isHtmlNavigation = request.mode === 'navigate' || 
                             (request.headers.get('accept') && request.headers.get('accept').includes('text/html'));

    if (isHtmlNavigation) {
        event.respondWith(
            fetch(request).catch(() => {
                return caches.match(request);
            })
        );
        return;
    }

    // C. STALE-WHILE-REVALIDATE HOẶC CACHE-FIRST CHO TÀI NGUYÊN TĨNH
    const isStaticAsset = 
        pathname.startsWith('/css/') ||
        pathname.startsWith('/js/') ||
        pathname.startsWith('/theme/') ||
        pathname.startsWith('/images/') ||
        pathname.endsWith('.css') ||
        pathname.endsWith('.js') ||
        pathname.endsWith('.svg') ||
        pathname.endsWith('.woff2') ||
        pathname.endsWith('.woff') ||
        pathname.endsWith('.ico');

    if (isStaticAsset) {
        event.respondWith(
            caches.match(request).then(cachedResponse => {
                const fetchPromise = fetch(request).then(networkResponse => {
                    if (networkResponse && networkResponse.status === 200) {
                        const responseToCache = networkResponse.clone();
                        caches.open(CACHE_NAME).then(cache => {
                            cache.put(request, responseToCache);
                        });
                    }
                    return networkResponse;
                }).catch(() => cachedResponse);

                return cachedResponse || fetchPromise;
            })
        );
    }
});
