// Khung "Thanh toán" trong modal hóa đơn của khách thuê (spec thanh toán §27.1).
// Ngày giờ từ server là giờ Việt Nam dạng "yyyy-MM-ddTHH:mm:ss" (không kèm múi giờ).
(function (window, $) {
    'use strict';

    var LUOT = { ChoThanhToan: 0, DangDoiChieu: 2, HetHan: 5 };
    var MINH_CHUNG = { ChoXacNhan: 0, TuChoi: 2 };
    var BASE = '/KhachThue/HoaDon/ThanhToan';

    var current = { hoaDonId: 0, container: null, onChanged: null, timer: null };

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

    function toDate(text) {
        return text ? new Date(text) : null;
    }

    function formatDateTime(text) {
        var d = toDate(text);
        if (!d || isNaN(d.getTime())) return '';
        var p = function (x) { return String(x).padStart(2, '0'); };
        return p(d.getDate()) + '/' + p(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + p(d.getHours()) + ':' + p(d.getMinutes());
    }

    function nowForInput() {
        var d = new Date();
        d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
        return d.toISOString().slice(0, 16);
    }

    function errorMessage(xhr, fallback) {
        return (xhr && xhr.responseJSON && xhr.responseJSON.message) || fallback;
    }

    function stop() {
        if (current.timer) {
            clearInterval(current.timer);
            current.timer = null;
        }
    }

    function load(hoaDonId, container, onChanged) {
        stop();
        current.hoaDonId = hoaDonId;
        current.container = $(container);
        current.onChanged = onChanged || current.onChanged;
        current.container.html('<div class="text-center text-secondary py-3"><span class="spinner-border spinner-border-sm me-2"></span>Đang tải thông tin thanh toán...</div>');

        $.get(BASE + '/' + hoaDonId)
            .done(function (res) { render(res.data); })
            .fail(function (xhr) {
                current.container.html('<div class="alert alert-warning mb-0">' + esc(errorMessage(xhr, 'Không tải được thông tin thanh toán.')) + '</div>');
            });
    }

    function refresh(changed) {
        if (changed && typeof current.onChanged === 'function') current.onChanged();
        load(current.hoaDonId, current.container, current.onChanged);
    }

    function summary(p) {
        return '' +
            '<div class="row g-2 mb-3 text-center">' +
            '  <div class="col-4"><div class="text-secondary small">Tổng tiền</div><div class="fw-bold">' + vnd(p.tongTien) + '</div></div>' +
            '  <div class="col-4"><div class="text-secondary small">Đã thanh toán</div><div class="fw-bold text-success">' + vnd(p.daThu) + '</div></div>' +
            '  <div class="col-4"><div class="text-secondary small">Còn lại</div><div class="fw-bold text-danger">' + vnd(p.conLai) + '</div></div>' +
            '</div>';
    }

    function render(p) {
        stop();
        var html = '<h4 class="fw-bold border-bottom pb-2 mb-3"><i class="ti ti-credit-card me-1"></i> Thanh toán</h4>';

        if (p.conLai <= 0) {
            current.container.html(html + summary(p) +
                '<div class="alert alert-success mb-0"><i class="ti ti-circle-check me-1"></i> Hóa đơn đã được thanh toán đủ.</div>');
            return;
        }

        html += summary(p);
        var luot = p.luotGanNhat;

        if (luot && luot.trangThaiHieuLuc === LUOT.ChoThanhToan) {
            html += renderWaiting(p, luot);
        } else if (luot && luot.trangThaiHieuLuc === LUOT.DangDoiChieu) {
            html += renderReview(luot);
        } else {
            if (luot && luot.trangThaiHieuLuc === LUOT.HetHan) {
                html += '<div class="alert alert-warning"><i class="ti ti-clock-x me-1"></i> Lượt <strong>' + esc(luot.maYeuCau) +
                    '</strong> đã hết hạn. Đừng dùng lại QR cũ; nếu bạn đã chuyển tiền, hãy liên hệ quản lý.</div>';
            }
            html += renderCreate(p);
        }

        current.container.html(html);
        bind(p, luot);
    }

    function renderCreate(p) {
        if (!p.coTheTaoLuot) {
            return '<div class="alert alert-secondary mb-0">' + esc(p.lyDoKhongTheTao || 'Hiện chưa thể thanh toán hóa đơn này.') + '</div>';
        }

        var amount = '';
        if (p.choPhepThanhToanMotPhan) {
            var min = p.soTienThanhToanToiThieu || 0;
            var hint = p.conLai < min
                ? 'Số còn lại nhỏ hơn mức tối thiểu nên cần thanh toán đúng số còn lại.'
                : 'Bạn có thể trả một phần, tối thiểu ' + vnd(min) + '.';
            amount = '' +
                '<div class="mb-2">' +
                '  <label class="form-label">Số tiền thanh toán lần này</label>' +
                '  <div class="input-group"><input type="number" class="form-control" id="ktPaySoTien" min="1" step="1" value="' + p.conLai + '"><span class="input-group-text">đ</span></div>' +
                '  <div class="form-hint">' + esc(hint) + '</div>' +
                '</div>';
        }

        return '' +
            '<div class="card card-sm bg-body-secondary border-0"><div class="card-body">' +
            amount +
            '  <p class="text-secondary small mb-3"><i class="ti ti-info-circle me-1"></i>Quét QR để chuyển, không tự sửa số tiền hoặc nội dung. QR chỉ dùng cho hóa đơn này và không dùng lại sau khi hết hạn.</p>' +
            '  <button type="button" class="btn btn-primary w-100" id="ktPayCreate"><i class="ti ti-qrcode me-1"></i> Thanh toán</button>' +
            '</div></div>';
    }

    function rejectedNotice(luot) {
        var rejected = (luot.minhChungs || []).filter(function (m) { return m.trangThai === MINH_CHUNG.TuChoi; })[0];
        if (!rejected) return '';
        return '<div class="alert alert-danger"><i class="ti ti-alert-triangle me-1"></i> Minh chứng trước bị từ chối: <strong>' +
            esc(rejected.lyDoTuChoi || '') + '</strong>. Bạn có thể nộp lại minh chứng cho lượt này.</div>';
    }

    function renderWaiting(p, luot) {
        return rejectedNotice(luot) +
            '<div class="row g-3">' +
            '  <div class="col-md-5 text-center">' +
            '    <div class="qr-payment-box p-3 shadow-sm">' +
            '      <img src="' + BASE + '/QR/' + luot.yeuCauId + '?v=' + encodeURIComponent(luot.maYeuCau) + '" alt="VietQR" class="img-fluid" />' +
            '      <div class="small text-secondary mt-2">Hết hạn sau <strong id="ktPayCountdown" class="text-danger">--:--:--</strong></div>' +
            '    </div>' +
            '  </div>' +
            '  <div class="col-md-7">' +
            '    <div class="qr-details-list bg-body-secondary p-3 rounded mb-2">' +
            '      <div class="qr-detail-item"><span>Ngân hàng:</span><strong>' + esc(p.bankId) + '</strong></div>' +
            '      <div class="qr-detail-item"><span>Số tài khoản:</span><strong>' + esc(p.accountNumber) + '</strong></div>' +
            '      <div class="qr-detail-item"><span>Chủ tài khoản:</span><strong>' + esc(p.accountName) + '</strong></div>' +
            '      <div class="qr-detail-item"><span>Số tiền:</span><strong class="text-danger">' + vnd(luot.soTien) + '</strong></div>' +
            '      <div class="qr-detail-item"><span>Nội dung CK:</span><span><code class="text-primary fw-bold">' + esc(luot.noiDungChuyenKhoan) + '</code>' +
            '        <button type="button" class="btn btn-sm btn-ghost-primary py-0 px-1 ms-1" id="ktPayCopy" title="Sao chép nội dung"><i class="ti ti-copy"></i></button></span></div>' +
            '    </div>' +
            '    <p class="text-secondary small mb-0"><i class="ti ti-info-circle me-1"></i>Quét QR để chuyển, không tự sửa số tiền hoặc nội dung. Sau khi chuyển, hãy nộp ảnh biên lai bên dưới.</p>' +
            '  </div>' +
            '</div>' +
            '<form id="ktPayProofForm" class="mt-3 border-top pt-3" autocomplete="off">' +
            '  <h5 class="mb-2"><i class="ti ti-upload me-1"></i> Nộp minh chứng chuyển khoản</h5>' +
            '  <div class="row g-2">' +
            '    <div class="col-md-12"><label class="form-label required">Ảnh biên lai (JPG, PNG, WEBP, tối đa 5 MB)</label>' +
            '      <input type="file" class="form-control" name="file" accept="image/jpeg,image/png,image/webp" required></div>' +
            '    <div class="col-md-6"><label class="form-label required">Thời điểm chuyển</label>' +
            '      <input type="datetime-local" class="form-control" name="ngayChuyen" value="' + nowForInput() + '" required></div>' +
            '    <div class="col-md-6"><label class="form-label">Mã giao dịch ngân hàng</label>' +
            '      <input type="text" class="form-control" name="maGiaoDich" maxlength="100" placeholder="Không bắt buộc"></div>' +
            '  </div>' +
            '  <div class="d-flex gap-2 mt-3">' +
            '    <button type="submit" class="btn btn-success flex-fill" id="ktPaySubmit"><i class="ti ti-send me-1"></i> Gửi minh chứng</button>' +
            '    <button type="button" class="btn btn-outline-danger" id="ktPayCancel"><i class="ti ti-x me-1"></i> Hủy lượt này</button>' +
            '  </div>' +
            '</form>';
    }

    function renderReview(luot) {
        var pending = (luot.minhChungs || []).filter(function (m) { return m.trangThai === MINH_CHUNG.ChoXacNhan; })[0];
        var proof = pending
            ? '<a href="' + BASE + '/AnhMinhChung/' + pending.minhChungId + '" target="_blank" rel="noopener">' +
              '<img src="' + BASE + '/AnhMinhChung/' + pending.minhChungId + '" alt="Minh chứng" class="rounded border" style="max-height:160px"></a>' +
              '<div class="small text-secondary mt-1">Nộp lúc ' + esc(formatDateTime(pending.ngayNopUtc)) + '</div>'
            : '';

        return '' +
            '<div class="alert alert-info"><i class="ti ti-hourglass me-1"></i> Đang chờ nhân viên đối chiếu lượt <strong>' + esc(luot.maYeuCau) +
            '</strong> (' + vnd(luot.soTien) + '). Bạn sẽ nhận thông báo khi có kết quả.</div>' +
            '<div class="text-center">' + proof + '</div>';
    }

    function startCountdown(p, luot) {
        var remaining = toDate(luot.hanThanhToanUtc) - toDate(p.serverNowUtc);
        if (isNaN(remaining)) return;
        var endAt = Date.now() + remaining;
        var tick = function () {
            var left = Math.max(0, endAt - Date.now());
            var s = Math.floor(left / 1000);
            var text = String(Math.floor(s / 3600)).padStart(2, '0') + ':' + String(Math.floor(s / 60) % 60).padStart(2, '0') + ':' + String(s % 60).padStart(2, '0');
            $('#ktPayCountdown').text(text);
            if (left <= 0) {
                stop();
                refresh(false);
            }
        };
        tick();
        current.timer = setInterval(tick, 1000);
    }

    function setBusy($btn, busy) {
        if (busy) {
            $btn.data('html', $btn.html()).prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang xử lý...');
        } else {
            $btn.prop('disabled', false).html($btn.data('html'));
        }
    }

    function bind(p, luot) {
        $('#ktPayCreate').on('click', function () {
            var $btn = $(this);
            var soTien = $('#ktPaySoTien').length ? Number($('#ktPaySoTien').val()) : null;
            setBusy($btn, true);
            $.ajax({
                url: BASE + '/Tao',
                type: 'POST',
                contentType: 'application/json',
                headers: { 'RequestVerificationToken': token() },
                data: JSON.stringify({ hoaDonId: p.hoaDonId, soTien: soTien === p.conLai ? null : soTien })
            }).done(function () {
                refresh(false);
            }).fail(function (xhr) {
                setBusy($btn, false);
                Swal.fire('Không tạo được lượt thanh toán', errorMessage(xhr, 'Vui lòng thử lại.'), 'error');
                if (xhr.status === 400) refresh(false);
            });
        });

        if (!luot) return;

        if (luot.trangThaiHieuLuc === LUOT.ChoThanhToan) {
            startCountdown(p, luot);
        }

        $('#ktPayCopy').on('click', function () {
            if (navigator.clipboard) {
                navigator.clipboard.writeText(luot.noiDungChuyenKhoan).then(function () {
                    Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: 'Đã sao chép nội dung', showConfirmButton: false, timer: 1500 });
                });
            }
        });

        $('#ktPayCancel').on('click', function () {
            Swal.fire({
                title: 'Hủy lượt thanh toán?',
                html: 'Chỉ hủy nếu bạn <strong>chưa</strong> chuyển khoản. Nếu đã chuyển, hãy nộp minh chứng hoặc liên hệ quản lý.',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Hủy lượt',
                cancelButtonText: 'Quay lại',
                confirmButtonColor: '#d63939'
            }).then(function (r) {
                if (!r.isConfirmed) return;
                $.ajax({
                    url: BASE + '/Huy/' + luot.yeuCauId,
                    type: 'POST',
                    headers: { 'RequestVerificationToken': token() }
                }).always(function () { refresh(false); })
                  .fail(function (xhr) { Swal.fire('Lỗi', errorMessage(xhr, 'Không hủy được lượt.'), 'error'); });
            });
        });

        $('#ktPayProofForm').on('submit', function (e) {
            e.preventDefault();
            var form = this;
            var file = form.file.files[0];
            if (file && file.size > 5 * 1024 * 1024) {
                Swal.fire('Ảnh quá lớn', 'Ảnh minh chứng tối đa 5 MB.', 'warning');
                return;
            }

            var data = new FormData();
            data.append('yeuCauId', luot.yeuCauId);
            data.append('file', file);
            data.append('ngayChuyen', form.ngayChuyen.value);
            data.append('maGiaoDich', form.maGiaoDich.value);

            var $btn = $('#ktPaySubmit');
            setBusy($btn, true);
            $.ajax({
                url: BASE + '/NopMinhChung',
                type: 'POST',
                data: data,
                processData: false,
                contentType: false,
                headers: { 'RequestVerificationToken': token() }
            }).done(function (res) {
                Swal.fire('Đã gửi minh chứng', res.message || 'Vui lòng chờ đối chiếu.', 'success');
                refresh(true);
            }).fail(function (xhr) {
                setBusy($btn, false);
                Swal.fire('Không gửi được minh chứng', errorMessage(xhr, 'Vui lòng thử lại.'), 'error');
                if (xhr.status === 400 && /hết hạn|kết thúc/.test(errorMessage(xhr, ''))) refresh(false);
            });
        });
    }

    window.KtThanhToan = { load: load, stop: stop };
})(window, jQuery);
