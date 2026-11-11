// Tiện ích dùng chung cho các trang quản lý.
(function (window, $) {
    'use strict';

    function baoKhongTimThay() {
        var msg = 'Không tìm thấy mục của thông báo trong danh sách hiện tại.';
        if (window.Swal) {
            Swal.fire({ toast: true, position: 'bottom-end', icon: 'info', titleText: msg, showConfirmButton: false, timer: 4000 });
        } else {
            console.warn(msg);
        }
    }

    // Làm nổi bật một dòng bảng: chuyển tới trang chứa dòng (nếu DataTable có phân trang), cuộn vào giữa màn hình, nhấp nháy.
    // rowElement: phần tử <tr> (có thể đang nằm ngoài trang hiện tại của DataTable); dataTableApi: DataTable().api hoặc null.
    function focusRow(rowElement, dataTableApi) {
        if (!rowElement) {
            baoKhongTimThay();
            return false;
        }

        try {
            if (dataTableApi && dataTableApi.page && dataTableApi.rows) {
                var viTri = dataTableApi.rows({ order: 'current', search: 'applied' }).nodes().toArray().indexOf(rowElement);
                if (viTri < 0) {
                    baoKhongTimThay();
                    return false;
                }
                var doDai = dataTableApi.page.len();
                if (doDai > 0) {
                    dataTableApi.page(Math.floor(viTri / doDai)).draw('page');
                }
            }
        } catch (err) {
            console.warn('TroFocus: không chuyển được trang bảng.', err);
        }

        var $row = $(rowElement);
        $('.tb-focus-row').removeClass('tb-focus-row tb-focus-row-flash');
        $row.addClass('tb-focus-row tb-focus-row-flash');
        rowElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        setTimeout(function () { $row.removeClass('tb-focus-row-flash'); }, 2600);
        return true;
    }

    window.TroFocus = { row: focusRow };

    // Chế độ sáng/tối: đặt data-bs-theme trên <html>, nhớ lựa chọn trong localStorage, đồng bộ mọi nút đổi giao diện
    // (hai nút ở header từ 992px và mục "Chế độ tối/sáng" trong menu người dùng của thanh trên điện thoại).
    function giaoDienHienTai() {
        return document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'light';
    }

    function apDungGiaoDien() {
        var toi = giaoDienHienTai() === 'dark';
        var nutToi = document.getElementById('btnToggleDarkTheme');
        var nutSang = document.getElementById('btnToggleLightTheme');
        if (nutToi) { nutToi.style.display = toi ? 'none' : 'inline-block'; }
        if (nutSang) { nutSang.style.display = toi ? 'inline-block' : 'none'; }

        document.querySelectorAll('.js-doi-giao-dien').forEach(function (muc) {
            var icon = muc.querySelector('i');
            var chu = muc.querySelector('span');
            if (icon) { icon.className = 'ti ' + (toi ? 'ti-sun' : 'ti-moon') + ' me-2'; }
            if (chu) { chu.textContent = toi ? 'Chế độ sáng' : 'Chế độ tối'; }
        });
    }

    function datGiaoDien(giaoDien) {
        document.documentElement.setAttribute('data-bs-theme', giaoDien);
        try {
            localStorage.setItem('tablerTheme', giaoDien);
        } catch (err) {
            // Trình duyệt chặn lưu trữ: vẫn đổi giao diện cho phiên hiện tại.
        }
        apDungGiaoDien();
    }

    document.addEventListener('click', function (e) {
        var nut = e.target.closest('#btnToggleDarkTheme, #btnToggleLightTheme, .js-doi-giao-dien');
        if (!nut) {
            return;
        }
        e.preventDefault();
        if (nut.id === 'btnToggleDarkTheme') {
            datGiaoDien('dark');
        } else if (nut.id === 'btnToggleLightTheme') {
            datGiaoDien('light');
        } else {
            datGiaoDien(giaoDienHienTai() === 'dark' ? 'light' : 'dark');
        }
    });

    window.TroTheme = { apDung: apDungGiaoDien };

    // Nút "Thêm" của thanh tab dưới (điện thoại): mở menu đầy đủ rồi đưa về đầu trang để thấy menu.
    document.addEventListener('click', function (e) {
        var nut = e.target.closest('.js-mo-menu-day-du');
        if (!nut) {
            return;
        }
        var menu = document.getElementById('sidebar-menu');
        if (!menu) {
            return;
        }
        if (window.bootstrap && window.bootstrap.Collapse) {
            window.bootstrap.Collapse.getOrCreateInstance(menu, { toggle: false }).show();
        }
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });

    // Menu quản lý luôn mở: cuộn mục đang chọn vào tầm nhìn nếu nó nằm dưới mép khung menu.
    function cuonMucMenuDangMo() {
        var muc = document.querySelector('#sidebar-menu .nav-link.active');
        if (!muc) {
            return;
        }
        var khung = muc.closest('.navbar-vertical');
        if (khung && khung.scrollHeight > khung.clientHeight) {
            muc.scrollIntoView({ block: 'nearest' });
        }
    }

    function khoiTaoVo() {
        apDungGiaoDien();
        cuonMucMenuDangMo();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', khoiTaoVo);
    } else {
        khoiTaoVo();
    }
})(window, window.jQuery);
