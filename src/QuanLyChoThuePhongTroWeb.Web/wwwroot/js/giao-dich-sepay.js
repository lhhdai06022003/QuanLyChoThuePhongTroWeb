// Màn Giao dịch SePay (spec thanh toán trực tuyến §4.3, §7.4): đồng bộ bù giao dịch tiền vào khi webhook bị lỡ.
// Nội dung chuyển khoản, mã tham chiếu và ghi chú là dữ liệu ngoài: mọi chuỗi từ server đi qua esc() trước khi ghép HTML.
(function (window, $) {
    'use strict';

    var DAY_MS = 24 * 60 * 60 * 1000;

    // Màu nhãn theo trạng thái (tên enum SePaySyncRowStatus).
    var BADGE = {
        MoiGhiNhan: 'bg-green-lt',
        DaCo: 'bg-secondary-lt',
        BoQua: 'bg-secondary-lt',
        KhongKhopMa: 'bg-orange-lt',
        HoaDonKhongNhanTien: 'bg-red-lt',
        LoiGhiSo: 'bg-red-lt',
        ThieuMaThamChieu: 'bg-yellow-lt',
        ChuaGhiSo: 'bg-blue-lt'
    };

    function esc(value) {
        return (value === null || value === undefined ? '' : value.toString())
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
    }

    function vnd(n) {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n || 0);
    }

    function token() {
        return $('input[name="__RequestVerificationToken"]').first().val() || '';
    }

    function errorMessage(xhr, fallback) {
        return (xhr && xhr.responseJSON && xhr.responseJSON.message) || fallback;
    }

    function badgeClass(status) {
        return BADGE[status] || 'bg-secondary-lt';
    }

    // yyyy-MM-dd -> mili giây UTC của đầu ngày; NaN nếu sai định dạng.
    function dayStart(text) {
        var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text || '');
        return m ? Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : NaN;
    }

    // Trả thông báo lỗi nếu khoảng ngày không hợp lệ, ngược lại null. Server kiểm lại các điều kiện này.
    function validate(tuNgay, denNgay, maxDays) {
        var tu = dayStart(tuNgay);
        var den = dayStart(denNgay);
        if (isNaN(tu) || isNaN(den)) return 'Vui lòng chọn đủ hai ngày.';
        if (tu > den) return 'Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.';
        if ((den - tu) / DAY_MS + 1 > maxDays) return 'Khoảng ngày tối đa ' + maxDays + ' ngày.';
        return null;
    }

    function renderAlerts(data) {
        var html = '';
        if (data.hoanTat === false) {
            html += '<div class="alert alert-warning"><i class="ti ti-alert-triangle me-1"></i> Đồng bộ chưa hoàn tất: ' +
                esc(data.loiDongBo || 'Dừng giữa chừng.') + ' Các giao dịch đã xử lý vẫn hiện bên dưới.</div>';
        }
        if (data.canhBao) {
            html += '<div class="alert alert-secondary"><i class="ti ti-info-circle me-1"></i> ' + esc(data.canhBao) + '</div>';
        }
        $('#sepayAlerts').html(html);
    }

    function renderSummary(data) {
        var html = (data.tongTheoTrangThai || []).map(function (c) {
            return '<span class="badge ' + badgeClass(c.trangThai) + ' me-1 mb-1">' + esc(c.nhan) + ': ' + esc(c.soLuong) + '</span>';
        }).join('');
        $('#sepaySummary').html(html);
    }

    function invoiceCell(row) {
        if (!row.hoaDonId) return '';
        var label = row.maHoaDon || ('#HD' + row.hoaDonId);
        return '<a href="/QuanLyNhaTro/QuanLyHoaDon?hoaDonId=' + encodeURIComponent(row.hoaDonId) + '">' + esc(label) + '</a>';
    }

    function renderRows(rows) {
        var $body = $('#sepayRows tbody');
        if (!rows || rows.length === 0) {
            $body.html('<tr><td colspan="8" class="text-center text-secondary py-4">Không có giao dịch tiền vào trong khoảng này.</td></tr>');
            return;
        }

        $body.html(rows.map(function (r) {
            return '<tr>' +
                '<td class="text-nowrap">' + esc(r.thoiGian) + '</td>' +
                '<td class="text-end text-nowrap">' + esc(vnd(r.soTien)) + '</td>' +
                '<td>' + esc(r.noiDung) + '</td>' +
                '<td>' + esc(r.maThamChieu) + '</td>' +
                '<td>' + esc(r.maChuyenKhoan) + '</td>' +
                '<td>' + invoiceCell(r) + '</td>' +
                '<td><span class="badge ' + badgeClass(r.trangThai) + '">' + esc(r.nhanTrangThai) + '</span></td>' +
                '<td class="text-secondary">' + esc(r.ghiChu) + '</td>' +
                '</tr>';
        }).join(''));
    }

    $(function () {
        var $form = $('#sepaySyncForm');
        var $btn = $('#btnDongBoSePay');
        if ($form.length === 0) return;

        // Nút bị khóa từ server khi chưa cấu hình API token: không bật lại sau khi đồng bộ.
        var locked = $btn.prop('disabled');
        var maxDays = Number($form.attr('data-max-days')) || 31;

        $form.on('submit', function (e) {
            e.preventDefault();
            if (locked || $btn.prop('disabled')) return;

            var tuNgay = $('#sepayTuNgay').val();
            var denNgay = $('#sepayDenNgay').val();
            var invalid = validate(tuNgay, denNgay, maxDays);
            if (invalid) {
                Swal.fire('Khoảng ngày chưa hợp lệ', invalid, 'warning');
                return;
            }

            var original = $btn.html();
            $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang đồng bộ...');

            $.ajax({
                url: '/QuanLyNhaTro/GiaoDichSePay/DongBo',
                type: 'POST',
                contentType: 'application/json',
                headers: { 'RequestVerificationToken': token() },
                data: JSON.stringify({ tuNgay: tuNgay, denNgay: denNgay })
            }).done(function (res) {
                var data = res.data || {};
                renderAlerts(data);
                renderSummary(data);
                renderRows(data.rows);
            }).fail(function (xhr) {
                Swal.fire('Không đồng bộ được', errorMessage(xhr, 'Có lỗi xảy ra khi đồng bộ.'), 'error');
            }).always(function () {
                $btn.html(original);
                if (!locked) $btn.prop('disabled', false);
            });
        });
    });
})(window, jQuery);
