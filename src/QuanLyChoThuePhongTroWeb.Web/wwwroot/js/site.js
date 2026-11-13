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

// Bảng thành thẻ dưới 768px: gán data-label cho từng ô theo tiêu đề cột để CSS .ts-bang-the hiện nhãn.
(function (window, $) {
    'use strict';

    // data-the-nhan trên <th> ghi đè chữ nhãn; data-the-nhan="" nghĩa là ô không có nhãn.
    function nhan(th) {
        if (!th) { return ''; }
        if (th.hasAttribute('data-the-nhan')) { return th.getAttribute('data-the-nhan'); }
        return (th.textContent || '').trim();
    }

    // headers: mảng <th> theo thứ tự cột hiển thị. Ô đã có data-label thì giữ nguyên.
    function ganTheoTieuDe(table, headers) {
        if (!table || !headers) { return; }
        var dong = table.querySelectorAll('tbody tr');
        for (var r = 0; r < dong.length; r++) {
            var o = dong[r].children;
            var i = 0;
            for (var c = 0; c < o.length; c++) {
                var td = o[c];
                if (td.tagName !== 'TD') { continue; }
                var gop = parseInt(td.getAttribute('colspan'), 10);
                if (gop > 1) { i += gop; continue; }
                if (!td.hasAttribute('data-label')) {
                    td.setAttribute('data-label', nhan(headers[i]));
                }
                i++;
            }
        }
    }

    // Bảng thường: lấy hàng cuối của thead; hàng có th gộp cột thì bỏ qua (bảng tự ghi data-label).
    function gan(table) {
        if (!table || !table.classList || !table.classList.contains('ts-bang-the')) { return; }
        var hangTieuDe = table.querySelectorAll('thead tr');
        if (!hangTieuDe.length) { return; }
        var hang = hangTieuDe[hangTieuDe.length - 1];
        var headers = [];
        for (var k = 0; k < hang.children.length; k++) {
            var th = hang.children[k];
            if (th.tagName !== 'TH' && th.tagName !== 'TD') { continue; }
            if (parseInt(th.getAttribute('colspan'), 10) > 1) { return; }
            headers.push(th);
        }
        ganTheoTieuDe(table, headers);
    }

    // Gán nhãn cho mọi bảng thẻ tĩnh (không qua DataTables).
    function ganTatCa() {
        var tables = document.querySelectorAll('table.ts-bang-the');
        for (var i = 0; i < tables.length; i++) {
            var tbl = tables[i];
            if ($ && $.fn && $.fn.dataTable && $.fn.dataTable.isDataTable && $.fn.dataTable.isDataTable(tbl)) {
                continue;
            }
            gan(tbl);
        }
    }

    window.TroBangThe = { gan: gan, ganTatCa: ganTatCa };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', ganTatCa);
    } else {
        ganTatCa();
    }

    // Bảng DataTables vẽ lại sau mỗi lần đổi trang/lọc/sắp xếp: gán lại theo cột đang hiển thị.
    if ($ && $.fn && $.fn.dataTable) {
        $(document).on('draw.dt', function (e, settings) {
            var table = e.target;
            if (!table || !table.classList || !table.classList.contains('ts-bang-the')) { return; }
            var api = new $.fn.dataTable.Api(settings);
            var headers = api.columns(':visible').header().toArray();
            ganTheoTieuDe(table, headers);
        });
    }
})(window, window.jQuery);
