// Điều chỉnh chỉ số điện nước và gửi lại hóa đơn tạm tính (hóa đơn tự động). Dùng ở màn Hóa đơn quản lý.
(function (window, $) {
    'use strict';

    var MODAL_ID = 'hdDieuChinhModal';

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

    function warn(message) {
        Swal.fire({ title: 'Chưa hợp lệ', text: message, icon: 'warning' });
    }

    function soVN(n) {
        return Number(n).toLocaleString('vi-VN');
    }

    function ensureModal() {
        if (document.getElementById(MODAL_ID)) return;
        $('body').append(
            '<div class="modal fade" id="' + MODAL_ID + '" tabindex="-1" aria-hidden="true">' +
            ' <div class="modal-dialog modal-dialog-centered"><div class="modal-content">' +
            '  <div class="modal-header py-2"><h5 class="modal-title"><i class="ti ti-adjustments me-1"></i> Điều chỉnh và gửi lại hóa đơn <span class="text-primary" data-f="ma"></span></h5>' +
            '   <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button></div>' +
            '  <div class="modal-body py-3">' +
            '   <div data-f="banner"></div>' +
            '   <div class="text-secondary small mb-3" data-f="han"></div>' +
            '   <div class="table-responsive mb-3">' +
            '    <table class="table table-vcenter table-sm mb-0">' +
            '     <thead><tr><th></th><th class="text-end">Chỉ số cũ</th><th class="text-end">Chỉ số mới hiện tại</th><th style="min-width:9rem">Chỉ số mới</th></tr></thead>' +
            '     <tbody>' +
            '      <tr><td>Điện</td><td class="text-end hd-num" data-f="dien-cu"></td><td class="text-end hd-num" data-f="dien-hien-tai"></td>' +
            '       <td><label for="hdDcDien" class="visually-hidden">Chỉ số điện mới</label><input type="number" id="hdDcDien" class="form-control form-control-sm" min="0" step="0.001"></td></tr>' +
            '      <tr><td>Nước</td><td class="text-end hd-num" data-f="nuoc-cu"></td><td class="text-end hd-num" data-f="nuoc-hien-tai"></td>' +
            '       <td><label for="hdDcNuoc" class="visually-hidden">Chỉ số nước mới</label><input type="number" id="hdDcNuoc" class="form-control form-control-sm" min="0" step="0.001"></td></tr>' +
            '     </tbody>' +
            '    </table>' +
            '   </div>' +
            '   <div class="form-hint" data-f="hint"></div>' +
            '  </div>' +
            '  <div class="modal-footer"><button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Đóng</button>' +
            '   <button type="button" class="btn btn-primary" data-f="luu"><i class="ti ti-send me-1"></i> Điều chỉnh và gửi lại</button></div>' +
            ' </div></div></div>');
    }

    function field(name) {
        return $('#' + MODAL_ID).find('[data-f="' + name + '"]');
    }

    function confirmText(daGui) {
        var text = 'Hệ thống tính lại tiền điện nước và gửi lại khách bản tạm tính với hạn tự chốt mới.';
        if (daGui) text += ' Hóa đơn đã công bố sẽ bị hủy và thay bằng hóa đơn mới.';
        return text;
    }

    function open(options) {
        ensureModal();
        var hoaDonId = options.hoaDonId;
        var daGui = options.daGui === true;

        $.get('/HoaDon/DieuChinh/' + hoaDonId).done(function (s) {
            field('ma').text(s.maHoaDon || options.maHoaDon || ('#' + hoaDonId));
            field('dien-cu').text(soVN(s.chiSoDienCu));
            field('dien-hien-tai').text(soVN(s.chiSoDienMoi));
            field('nuoc-cu').text(soVN(s.chiSoNuocCu));
            field('nuoc-hien-tai').text(soVN(s.chiSoNuocMoi));
            $('#hdDcDien').val(s.chiSoDienMoi);
            $('#hdDcNuoc').val(s.chiSoNuocMoi);
            field('han').text(s.hanTuChot ? 'Hạn tự chốt hiện tại: ' + s.hanTuChot : '').toggle(!!s.hanTuChot);
            field('hint').text(confirmText(daGui));

            var $btn = field('luu');
            var coThe = s.coTheDieuChinh !== false;
            if (!coThe) {
                field('banner').html('<div class="alert alert-warning"><i class="ti ti-lock me-1"></i> ' + esc(s.lyDoKhoa) + '</div>');
            } else {
                field('banner').empty();
            }
            $('#hdDcDien, #hdDcNuoc').prop('disabled', !coThe);
            $btn.prop('disabled', !coThe).html('<i class="ti ti-send me-1"></i> Điều chỉnh và gửi lại');

            $btn.off('click').on('click', function () {
                var dienRaw = $.trim($('#hdDcDien').val());
                var nuocRaw = $.trim($('#hdDcNuoc').val());
                var dien = Number(dienRaw);
                var nuoc = Number(nuocRaw);
                if (dienRaw === '' || nuocRaw === '' || !isFinite(dien) || !isFinite(nuoc) || dien < 0 || nuoc < 0) {
                    warn('Vui lòng nhập chỉ số hợp lệ.');
                    return;
                }
                if (dien < s.chiSoDienCu) {
                    warn('Chỉ số điện mới không được nhỏ hơn chỉ số cũ.');
                    return;
                }
                if (nuoc < s.chiSoNuocCu) {
                    warn('Chỉ số nước mới không được nhỏ hơn chỉ số cũ.');
                    return;
                }
                if (dien === Number(s.chiSoDienMoi) && nuoc === Number(s.chiSoNuocMoi)) {
                    warn('Không có thay đổi.');
                    return;
                }

                Swal.fire({
                    title: 'Điều chỉnh và gửi lại?',
                    text: confirmText(daGui),
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Điều chỉnh',
                    cancelButtonText: 'Hủy'
                }).then(function (r) {
                    if (!r.isConfirmed) return;
                    $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang gửi...');
                    $.ajax({
                        url: '/HoaDon/DieuChinh/' + hoaDonId,
                        type: 'POST',
                        contentType: 'application/json',
                        headers: { 'RequestVerificationToken': token() },
                        data: JSON.stringify({ chiSoDienMoi: dien, chiSoNuocMoi: nuoc })
                    }).done(function (res) {
                        var d = res.data || {};
                        bootstrap.Modal.getOrCreateInstance(document.getElementById(MODAL_ID)).hide();
                        Swal.fire({
                            icon: 'success',
                            title: 'Đã gửi lại hóa đơn tạm tính',
                            html: esc(res.message) + '<br>Mã hóa đơn: ' + esc(d.maHoaDon) +
                                '<br>Tổng tiền mới: ' + esc(vnd(d.tongTien)) +
                                '<br>Tự chốt lúc: ' + esc(d.hanTuChot)
                        });
                        if (typeof options.onDone === 'function') options.onDone(res.data);
                    }).fail(function (xhr) {
                        Swal.fire({ title: 'Không điều chỉnh được', text: errorMessage(xhr, 'Vui lòng thử lại.'), icon: 'error' });
                        $btn.prop('disabled', false).html('<i class="ti ti-send me-1"></i> Điều chỉnh và gửi lại');
                    });
                });
            });

            bootstrap.Modal.getOrCreateInstance(document.getElementById(MODAL_ID)).show();
        }).fail(function (xhr) {
            Swal.fire({ title: 'Không mở được điều chỉnh', text: errorMessage(xhr, 'Vui lòng thử lại.'), icon: 'error' });
        });
    }

    window.HdDieuChinh = { open: open };
})(window, jQuery);
