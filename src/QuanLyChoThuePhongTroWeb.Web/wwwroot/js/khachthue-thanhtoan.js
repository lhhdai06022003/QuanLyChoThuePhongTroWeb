// Khung "Thanh toán" trong modal hóa đơn của khách thuê (spec thanh toán trực tuyến §3.2, §3.3).
// QR, deeplink và số tiền luôn theo số còn lại của hóa đơn; khách không tự tạo lượt hay nộp minh chứng.
// Ngày giờ từ server là chuỗi giờ Việt Nam để hiển thị. Mọi chuỗi từ server đi qua esc() trước khi ghép HTML.
(function (window, $) {
    'use strict';

    var BASE = '/KhachThue/HoaDon/ThanhToan';
    var RECEIPT = '/KhachThue/HoaDon/BienLai/';

    var current = { hoaDonId: 0, container: null, onRecheck: null, seq: 0 };

    function esc(value) {
        return (value === null || value === undefined ? '' : value.toString())
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
    }

    function vnd(n) {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n || 0);
    }

    function errorMessage(xhr, fallback) {
        return (xhr && xhr.responseJSON && xhr.responseJSON.message) || fallback;
    }

    function toast(icon, title, timer) {
        Swal.fire({ toast: true, position: 'top-end', icon: icon, title: title, showConfirmButton: false, timer: timer });
    }

    // onRecheck: việc làm khi khách bấm "Tôi đã chuyển khoản, kiểm tra lại"; không truyền thì tải lại chính khung này.
    function load(hoaDonId, container, onRecheck) {
        var seq = ++current.seq;
        current.hoaDonId = hoaDonId;
        current.container = $(container);
        current.onRecheck = onRecheck || null;
        current.container.html('<div class="text-center text-secondary py-3"><span class="spinner-border spinner-border-sm me-2"></span>Đang tải thông tin thanh toán...</div>');

        $.get(BASE + '/' + hoaDonId)
            .done(function (res) {
                if (seq === current.seq) render(res.data);
            })
            .fail(function (xhr) {
                if (seq !== current.seq) return;
                current.container.html('<div class="alert alert-warning mb-0">' + esc(errorMessage(xhr, 'Không tải được thông tin thanh toán.')) + '</div>');
            });
    }

    // Đăng ký nhận sự kiện tiền vào trên kết nối ThongBaoHub của layout. Chỉ gọi một lần mỗi trang.
    function watch(onUpdated) {
        if (window.ThongBaoHub) {
            window.ThongBaoHub.on('HoaDonThanhToanCapNhat', onUpdated);
        }
    }

    function render(p) {
        var html = '<h4 class="fw-bold border-bottom pb-2 mb-3"><i class="ti ti-credit-card me-1"></i> Thanh toán</h4>' +
            '<div class="row g-3">' +
            '  <div class="col-12 col-lg-4">' + renderInfo(p) + '</div>' +
            '  <div class="col-12 col-lg-8">' + renderMain(p) + '</div>' +
            '</div>';

        current.container.html(html);
        bind();
    }

    function infoRow(label, value, valueClass) {
        return '<div class="d-flex justify-content-between align-items-center py-1"><span class="text-secondary small">' + label + '</span>' +
            '<strong class="' + (valueClass || '') + '">' + value + '</strong></div>';
    }

    function renderInfo(p) {
        return '<div class="card card-sm shadow-none border mb-0"><div class="card-body p-3">' +
            infoRow('Phòng', esc(p.tenPhong)) +
            infoRow('Chi nhánh', esc(p.tenChiNhanh)) +
            infoRow('Kỳ', 'Tháng ' + esc(p.thang) + '/' + esc(p.nam)) +
            infoRow('Tổng tiền', '<span class="hd-num">' + vnd(p.tongTien) + '</span>') +
            infoRow('Đã thanh toán', '<span class="hd-num">' + vnd(p.daThu) + '</span>', 'text-success') +
            infoRow('Còn lại', '<span class="hd-num fs-3">' + vnd(Math.max(0, Number(p.conLai) || 0)) + '</span>', 'text-danger') +
            (p.hanThanhToan ? infoRow('Hạn thanh toán', '<span class="hd-num">' + esc(p.hanThanhToan) + '</span>') : '') +
            '</div></div>';
    }

    // label là chuỗi cố định của giao diện, không lấy từ server.
    function receiptLink(entry, cssClass, label) {
        return '<a' + (cssClass ? ' class="' + cssClass + '"' : '') + ' href="' + RECEIPT + esc(entry.lichSuThanhToanId) +
            '" target="_blank" rel="noopener">' + label + '</a>';
    }

    function renderMain(p) {
        var entries = p.cacLanThanhToan || [];
        var last = entries.length ? entries[entries.length - 1] : null;
        var html = '';

        // Lượt cũ khách đã nộp minh chứng trước khi gỡ tính năng này: báo để khách không chuyển lại.
        if (p.minhChungChoDoiChieu) {
            html += renderLegacyProof(p.minhChungChoDoiChieu);
        }

        // Đã trả đủ: ẩn khối chuyển khoản, chỉ hiện biên lai.
        if (p.conLai <= 0) {
            return html + renderPaidInFull(p, entries, last);
        }

        // Đã trả một phần: báo đợt vừa nhận rồi vẫn hiện QR mới theo số còn lại.
        if (last) {
            html += '<div class="alert alert-success d-block py-2 mb-3"><i class="ti ti-circle-check me-1"></i> Đã nhận <strong>' + vnd(last.soTien) +
                '</strong> (đợt ' + esc(last.dotThu) + '). ' + receiptLink(last, 'fw-medium text-decoration-underline', 'Xem / in biên lai') + '</div>';
        }

        if (!p.coTheThanhToan) {
            return html + '<div class="alert alert-secondary mb-0">' + esc(p.lyDoKhongTheThanhToan || 'Hiện chưa thể thanh toán hóa đơn này.') + '</div>';
        }

        // VNPay bật thì hai tab (QR mặc định); tắt thì giữ nguyên khối chuyển khoản QR.
        return html + (p.vnPayEnabled ? renderTabs(p) : renderTransfer(p));
    }

    // Hai tab chọn kiểu thanh toán (spec VNPay §4.1). Nhãn tab là chuỗi cố định của giao diện.
    function renderTabs(p) {
        return '<ul class="nav nav-tabs nav-fill kt-pay-tabs mb-3" role="tablist">' +
            '  <li class="nav-item" role="presentation">' +
            '    <button type="button" class="nav-link active" id="ktPayTabQrBtn" data-bs-toggle="tab" data-bs-target="#ktPayTabQr" role="tab" aria-controls="ktPayTabQr" aria-selected="true">' +
            '<i class="ti ti-qrcode me-1"></i> Chuyển khoản QR</button>' +
            '  </li>' +
            '  <li class="nav-item" role="presentation">' +
            '    <button type="button" class="nav-link" id="ktPayTabVnPayBtn" data-bs-toggle="tab" data-bs-target="#ktPayTabVnPay" role="tab" aria-controls="ktPayTabVnPay" aria-selected="false">' +
            '<i class="ti ti-credit-card me-1"></i> Thẻ ATM / Visa / MasterCard (VNPay)</button>' +
            '  </li>' +
            '</ul>' +
            '<div class="tab-content">' +
            '  <div class="tab-pane fade show active" id="ktPayTabQr" role="tabpanel" aria-labelledby="ktPayTabQrBtn">' + renderTransfer(p) + '</div>' +
            '  <div class="tab-pane fade" id="ktPayTabVnPay" role="tabpanel" aria-labelledby="ktPayTabVnPayBtn">' + renderVnPay(p) + '</div>' +
            '</div>';
    }

    // Số tiền luôn là số còn lại, chỉ đọc; link thanh toán do server tạo khi bấm nút (khachthue-vnpay.js).
    function renderVnPay(p) {
        return '' +
            '<div class="qr-details-list p-3 rounded mb-3">' +
            copyRow('Số tiền sẽ thanh toán', '<strong class="text-danger hd-num fs-3">' + vnd(p.conLai) + '</strong>', null, '') +
            '</div>' +
            '<div class="alert alert-info d-block py-2 mb-3"><i class="ti ti-info-circle me-1"></i> Bạn sẽ được chuyển sang trang VNPay. Không đóng trình duyệt cho tới khi VNPay đưa bạn về trang kết quả.</div>' +
            '<div class="alert alert-warning d-block py-2 mb-3"><i class="ti ti-alert-triangle me-1"></i> Nếu bạn vừa chuyển khoản QR cho hóa đơn này, hãy chờ hệ thống xác nhận trước khi thanh toán thẻ để không trả hai lần.</div>' +
            '<button type="button" class="btn btn-primary btn-lg w-100 w-sm-auto" id="ktPayVnPay"><i class="ti ti-credit-card me-1"></i> Thanh toán qua VNPay</button>';
    }

    function renderLegacyProof(proof) {
        return '<div class="alert alert-warning d-block py-2 mb-3"><i class="ti ti-hourglass me-1"></i> Bạn đã gửi minh chứng chuyển <strong>' + vnd(proof.soTien) +
            '</strong> lúc ' + esc(proof.ngayNop) + ', đang chờ nhân viên đối chiếu. Nếu khoản đó đã chuyển thì đừng chuyển lại. ' +
            '<a href="' + BASE + '/AnhMinhChung/' + esc(proof.minhChungId) + '" target="_blank" rel="noopener" class="fw-medium">Xem ảnh minh chứng</a></div>';
    }

    function renderPaidInFull(p, entries, last) {
        if (!last) {
            return '<div class="alert alert-success d-block mb-0"><i class="ti ti-circle-check me-1"></i> ' +
                esc(p.lyDoKhongTheThanhToan || 'Hóa đơn đã được thanh toán đủ.') + '</div>';
        }

        var rows = entries.map(function (e) {
            return '<tr>' +
                '<td>' + esc(e.dotThu) + '</td>' +
                '<td class="hd-num">' + esc(e.ngayThanhToan) + '</td>' +
                '<td class="text-end fw-medium text-success hd-num">' + vnd(e.soTien) + '</td>' +
                '<td>' + esc(e.phuongThuc) + '</td>' +
                '<td class="text-end">' + receiptLink(e, 'btn btn-sm btn-outline-primary', '<i class="ti ti-receipt me-1"></i>Biên lai') + '</td>' +
                '</tr>';
        }).join('');

        return '<div class="card border-success mb-3"><div class="card-body text-center text-sm-start">' +
            '  <span class="badge bg-success mb-2">ĐÃ THU TIỀN</span>' +
            '  <div class="h2 mb-1 text-success hd-num">' + vnd(last.soTien) + '</div>' +
            '  <div class="text-secondary mb-3 small">' + esc(last.ngayThanhToan) + ' · ' + esc(last.phuongThuc) + '</div>' +
            '  ' + receiptLink(last, 'btn btn-primary w-100 w-sm-auto', '<i class="ti ti-receipt me-1"></i> Xem / in biên lai') +
            '</div></div>' +
            '<h5 class="mb-2 fw-semibold"><i class="ti ti-history me-1"></i> Các lần thanh toán</h5>' +
            '<div class="table-responsive"><table class="table table-sm table-vcenter card-table mb-0">' +
            '<thead><tr><th>Đợt</th><th>Ngày</th><th class="text-end">Số tiền</th><th>Phương thức</th><th class="text-end"></th></tr></thead>' +
            '<tbody>' + rows + '</tbody></table></div>';
    }

    function copyRow(label, valueHtml, copyValue, copyLabel) {
        var button = copyValue === null ? '' :
            '<button type="button" class="btn btn-ghost-primary ms-1 kt-pay-copy" data-copy="' + esc(copyValue) +
            '" title="' + copyLabel + '" aria-label="' + copyLabel + '"><i class="ti ti-copy"></i></button>';

        return '<div class="qr-detail-item"><span>' + label + ':</span><span class="d-inline-flex align-items-center justify-content-end text-end">' + valueHtml + button + '</span></div>';
    }

    function renderTransfer(p) {
        var qrUrl = BASE + '/QR/' + esc(p.hoaDonId);
        var banks = (p.deeplinks || []).map(function (d) {
            return '<a class="btn btn-outline-secondary" href="' + esc(d.url) + '" rel="noopener">' + esc(d.tenNganHang) + '</a>';
        }).join('');

        return '' +
            '<div class="text-center mb-3">' +
            '  <img src="' + qrUrl + '?v=' + esc(encodeURIComponent(p.soTienSaoChep)) + '" alt="Mã VietQR thanh toán hóa đơn" class="img-fluid kt-qr shadow-sm">' +
            '  <div class="mt-2"><a class="btn btn-outline-primary w-100 w-sm-auto" href="' + qrUrl + '?taiVe=true" download><i class="ti ti-download me-1"></i> Tải ảnh QR</a></div>' +
            '  <div class="form-hint mt-2">Tải ảnh → mở app ngân hàng → Quét QR → chọn ảnh từ thư viện</div>' +
            '</div>' +
            '<div class="mb-3">' +
            '  <div class="d-flex justify-content-between align-items-center mb-2">' +
            '    <h5 class="mb-0 fw-semibold"><i class="ti ti-building-bank me-1"></i> Mở app ngân hàng</h5>' +
            '    <a href="#" class="d-none d-lg-inline small" id="ktPayShowBanks">Đang dùng điện thoại?</a>' +
            '  </div>' +
            '  <div class="kt-bank-grid d-lg-none" id="ktPayBankGrid">' + banks + '</div>' +
            '</div>' +
            '<div class="qr-details-list p-3 rounded mb-3">' +
            copyRow('Ngân hàng', '<strong>' + esc(p.tenNganHang) + '</strong>', null, '') +
            copyRow('Chủ tài khoản', '<strong>' + esc(p.chuTaiKhoan) + '</strong>', null, '') +
            copyRow('Số tài khoản', '<strong>' + esc(p.soTaiKhoan) + '</strong>', p.soTaiKhoan, 'Sao chép số tài khoản') +
            copyRow('Số tiền', '<strong class="text-danger hd-num fs-3">' + vnd(p.conLai) + '</strong>', p.soTienSaoChep, 'Sao chép số tiền') +
            copyRow('Nội dung CK', '<code class="text-primary fw-bold">' + esc(p.maChuyenKhoan) + '</code>', p.maChuyenKhoan, 'Sao chép nội dung') +
            '</div>' +
            '<div class="alert alert-warning d-block py-2 mb-3"><i class="ti ti-alert-triangle me-1"></i> Giữ nguyên nội dung <code>' + esc(p.maChuyenKhoan) +
            '</code> để hệ thống tự xác nhận. Sai nội dung, chủ trọ phải xác nhận tay.</div>' +
            '<button type="button" class="btn btn-outline-secondary w-100 w-sm-auto" id="ktPayRecheck"><i class="ti ti-refresh me-1"></i> Tôi đã chuyển khoản, kiểm tra lại</button>';
    }

    // Chép được thì báo "Đã sao chép", không thì nhắc khách chép tay.
    function copyText(text) {
        var done = function () { toast('success', 'Đã sao chép', 1500); };
        var failed = function () { toast('error', 'Không sao chép được, vui lòng chép tay.', 3000); };
        var fallback = function () { (legacyCopy(text) ? done : failed)(); };

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(done, fallback);
        } else {
            fallback();
        }
    }

    // Trình duyệt không có Clipboard API (trang không an toàn) hoặc bị từ chối: chép qua ô nhập tạm.
    function legacyCopy(text) {
        var area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.top = '-1000px';
        document.body.appendChild(area);
        area.select();

        var ok = false;
        try {
            ok = document.execCommand('copy');
        } catch (e) {
            ok = false;
        }

        document.body.removeChild(area);
        return ok;
    }

    function bind() {
        var $box = current.container;

        $box.find('.kt-pay-copy').on('click', function () {
            copyText($(this).attr('data-copy') || '');
        });

        $box.find('#ktPayShowBanks').on('click', function (e) {
            e.preventDefault();
            $box.find('#ktPayBankGrid').removeClass('d-lg-none');
            $(this).removeClass('d-lg-inline');
        });

        $box.find('#ktPayRecheck').on('click', function () {
            $(this).prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang kiểm tra...');
            reloadPanel();
        });

        // Việc tạo link và chuyển sang VNPay nằm ở khachthue-vnpay.js; lỗi thì panel tải lại để hiện số tiền mới nhất.
        $box.find('#ktPayVnPay').on('click', function () {
            if (!window.KtVnPay) {
                toast('error', 'Không tải được chức năng thanh toán VNPay, vui lòng tải lại trang.', 3000);
                return;
            }

            window.KtVnPay.start(current.hoaDonId, $(this), reloadPanel);
        });
    }

    // Làm mới khung thanh toán: việc do trang truyền vào (nếu có), không thì tải lại chính khung này.
    function reloadPanel() {
        if (typeof current.onRecheck === 'function') {
            current.onRecheck();
        } else {
            load(current.hoaDonId, current.container, current.onRecheck);
        }
    }

    window.KtThanhToan = { load: load, watch: watch };
})(window, jQuery);
