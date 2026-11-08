(function () {
    'use strict';

    var badge = document.getElementById('badge-viec-can-lam');
    if (!badge) return;

    function an() {
        badge.classList.add('d-none');
    }

    function hienThi(d) {
        var tongSo = Number(d.tongSo) || 0;
        if (tongSo <= 0) {
            an();
            return;
        }
        badge.textContent = String(tongSo);
        badge.classList.remove('d-none', 'bg-red', 'text-red-fg', 'bg-orange', 'text-orange-fg');
        if (Number(d.soKhan) > 0) {
            badge.classList.add('bg-red', 'text-red-fg');
        } else {
            badge.classList.add('bg-orange', 'text-orange-fg');
        }
    }

    // Trang đã tính sẵn số (Bảng điều khiển không lọc chi nhánh, xem Helpers/ViecCanLamBadge.cs) thì dùng luôn, không gọi lại.
    if (window.viecCanLamTomTat) {
        hienThi(window.viecCanLamTomTat);
        return;
    }

    fetch('/QuanLyNhaTro/ViecCanLam/TomTat', { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
        .then(function (res) {
            var contentType = res.headers.get('content-type') || '';
            if (!res.ok || res.redirected || contentType.indexOf('application/json') === -1) {
                throw new Error('Phản hồi không hợp lệ (' + res.status + ')');
            }
            return res.json();
        })
        .then(hienThi)
        .catch(function (err) {
            an();
            console.error('Không tải được số việc cần làm:', err);
        });
})();
