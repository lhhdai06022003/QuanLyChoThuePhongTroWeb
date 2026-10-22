// Thu tiền thủ công theo số thực nhận (spec thanh toán §18) và tóm tắt thanh toán trong modal hóa đơn,
// kèm công tắc "Cho phép trả một phần" cho Admin (§19). Dùng chung cho màn Hóa đơn và Phòng trọ.
(function (window, $) {
    'use strict';

    var LUOT = { ChoThanhToan: 0, DangDoiChieu: 2 };
    var MODAL_ID = 'hdThuTienModal';

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

    function nowForInput() {
        var d = new Date();
        d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
        return d.toISOString().slice(0, 16);
    }

    function errorMessage(xhr, fallback) {
        return (xhr && xhr.responseJSON && xhr.responseJSON.message) || fallback;
    }

    function ensureModal() {
        if (document.getElementById(MODAL_ID)) return;
        $('body').append(
            '<div class="modal fade" id="' + MODAL_ID + '" tabindex="-1" aria-hidden="true">' +
            ' <div class="modal-dialog modal-dialog-centered"><div class="modal-content">' +
            '  <div class="modal-header py-2"><h5 class="modal-title"><i class="ti ti-report-money me-1"></i> Thu tiền hóa đơn <span class="text-primary" data-f="ma"></span></h5>' +
            '   <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button></div>' +
            '  <div class="modal-body py-3">' +
            '   <div data-f="banner"></div>' +
            '   <div class="datagrid mb-3">' +
            '    <div class="datagrid-item"><div class="datagrid-title">Tổng tiền</div><div class="datagrid-content hd-num" data-f="tong"></div></div>' +
            '    <div class="datagrid-item"><div class="datagrid-title">Đã thu</div><div class="datagrid-content text-success hd-num" data-f="dathu"></div></div>' +
            '    <div class="datagrid-item"><div class="datagrid-title">Còn lại</div><div class="datagrid-content text-danger fw-bold hd-num" data-f="conlai"></div></div>' +
            '   </div>' +
            '   <div class="row g-2">' +
            '    <div class="col-sm-6"><label class="form-label required">Số tiền thực nhận</label>' +
            '     <div class="input-group"><input type="number" class="form-control" data-f="sotien" min="1" step="1"><span class="input-group-text">đ</span></div></div>' +
            '    <div class="col-sm-6"><label class="form-label required">Phương thức</label>' +
            '     <select class="form-select" data-f="pt"><option value="0">Tiền mặt</option><option value="1">Chuyển khoản</option></select></div>' +
            '    <div class="col-sm-6"><label class="form-label required">Ngày thanh toán</label>' +
            '     <input type="datetime-local" class="form-control" data-f="ngay"></div>' +
            '    <div class="col-sm-6" data-f="ma-wrap"><label class="form-label">Mã giao dịch ngân hàng</label>' +
            '     <input type="text" class="form-control" data-f="magd" maxlength="100" placeholder="Mã trên sao kê"></div>' +
            '    <div class="col-12"><label class="form-label" data-f="ghichu-label">Ghi chú</label>' +
            '     <textarea class="form-control" rows="2" data-f="ghichu" maxlength="500"></textarea>' +
            '     <div class="form-hint" data-f="hint"></div></div>' +
            '   </div>' +
            '  </div>' +
            '  <div class="modal-footer"><button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Đóng</button>' +
            '   <button type="button" class="btn btn-primary" data-f="luu"><i class="ti ti-device-floppy me-1"></i> Ghi nhận</button></div>' +
            ' </div></div></div>');
    }

    function field(name) {
        return $('#' + MODAL_ID).find('[data-f="' + name + '"]');
    }

    function updateHint(summary) {
        var pt = Number(field('pt').val());
        var soTien = Number(field('sotien').val());
        var ma = $.trim(field('magd').val());
        var hints = [];
        var ghiChuBatBuoc = false;

        field('ma-wrap').toggle(pt === 1);
        if (pt === 1 && !ma) {
            ghiChuBatBuoc = true;
            hints.push('Không có mã giao dịch thì phải ghi chú tham chiếu (ví dụ một lần chuyển trả nhiều hóa đơn).');
        }

        if (soTien > summary.conLai) {
            if (summary.laAdmin) {
                ghiChuBatBuoc = true;
                hints.push('Vượt số còn nợ ' + vnd(soTien - summary.conLai) + ': hệ thống ghi đủ nợ và đánh dấu tiền thừa để xử lý ngoài hệ thống.');
            } else {
                hints.push('Số tiền vượt số còn nợ, cần Admin xử lý.');
            }
        } else if (soTien > 0 && soTien < summary.conLai) {
            hints.push('Ghi nhận ' + vnd(soTien) + ', hóa đơn còn nợ ' + vnd(summary.conLai - soTien) + '.');
        }

        field('ghichu-label').toggleClass('required', ghiChuBatBuoc);
        field('hint').text(hints.join(' '));
    }

    function openThuTien(options) {
        ensureModal();
        var hoaDonId = options.hoaDonId;
        $.get('/HoaDon/ThanhToanTomTat/' + hoaDonId).done(function (s) {
            field('ma').text(options.maHoaDon || ('#' + hoaDonId));
            field('tong').text(vnd(s.tongTien));
            field('dathu').text(vnd(s.daThu));
            field('conlai').text(vnd(s.conLai));
            field('sotien').val(s.conLai);
            field('pt').val('0');
            field('ngay').val(nowForInput());
            field('magd').val('');
            field('ghichu').val('');

            var banner = '';
            var blocked = s.trangThaiLuotHoatDong === LUOT.DangDoiChieu;
            if (blocked) {
                banner = '<div class="alert alert-warning"><i class="ti ti-lock me-1"></i> Khách đã nộp minh chứng cho lượt <strong>' + esc(s.maLuotHoatDong) +
                    '</strong>. Hãy đối chiếu trước để tránh ghi trùng. <a href="/QuanLyNhaTro/DoiChieuThanhToan?ma=' + encodeURIComponent(s.maLuotHoatDong) + '">Mở đối chiếu</a></div>';
            } else if (s.trangThaiLuotHoatDong === LUOT.ChoThanhToan) {
                banner = '<div class="alert alert-info"><i class="ti ti-info-circle me-1"></i> Khách đang có lượt <strong>' + esc(s.maLuotHoatDong) +
                    '</strong> chờ chuyển khoản. Lượt này sẽ bị hủy khi bạn ghi nhận thu tiền.</div>';
            }

            field('banner').html(banner);
            field('luu').prop('disabled', blocked);
            $('#' + MODAL_ID).find('input,select,textarea').off('.hdtt').on('input.hdtt change.hdtt', function () { updateHint(s); });
            updateHint(s);

            field('luu').off('click').on('click', function () {
                var $btn = $(this);
                $btn.prop('disabled', true);
                $.ajax({
                    url: '/HoaDon/ThuTien',
                    type: 'POST',
                    contentType: 'application/json',
                    headers: { 'RequestVerificationToken': token() },
                    data: JSON.stringify({
                        hoaDonId: hoaDonId,
                        soTienThucNhan: Number(field('sotien').val()),
                        phuongThucThanhToan: Number(field('pt').val()),
                        ngayThanhToan: field('ngay').val(),
                        maGiaoDich: Number(field('pt').val()) === 1 ? field('magd').val() : null,
                        ghiChu: field('ghichu').val()
                    })
                }).done(function (res) {
                    bootstrap.Modal.getOrCreateInstance(document.getElementById(MODAL_ID)).hide();
                    Swal.fire('Đã ghi nhận', res.message, 'success');
                    if (typeof options.onDone === 'function') options.onDone(res.data);
                }).fail(function (xhr) {
                    Swal.fire('Không ghi nhận được', errorMessage(xhr, 'Có lỗi xảy ra.'), 'error');
                }).always(function () {
                    $btn.prop('disabled', false);
                });
            });

            bootstrap.Modal.getOrCreateInstance(document.getElementById(MODAL_ID)).show();
        }).fail(function (xhr) {
            Swal.fire('Lỗi', errorMessage(xhr, 'Không tải được thông tin thanh toán.'), 'error');
        });
    }

    // Tóm tắt thanh toán trong modal chi tiết; Admin bật/tắt trả một phần khi hóa đơn đã gửi và còn nợ.
    function renderSummary(hoaDonId, container, options) {
        options = options || {};
        var $c = $(container);
        $.get('/HoaDon/ThanhToanTomTat/' + hoaDonId).done(function (s) {
            var html = '<h4 class="fw-medium border-bottom pb-2 mt-4"><i class="ti ti-credit-card me-1"></i> Thanh toán</h4>' +
                '<div class="datagrid mb-3">' +
                ' <div class="datagrid-item"><div class="datagrid-title">Đã thu</div><div class="datagrid-content text-success hd-num">' + vnd(s.daThu) + '</div></div>' +
                ' <div class="datagrid-item"><div class="datagrid-title">Còn lại</div><div class="datagrid-content text-danger fw-bold hd-num">' + vnd(s.conLai) + '</div></div>' +
                ' <div class="datagrid-item"><div class="datagrid-title">Lượt đang hoạt động</div><div class="datagrid-content">' +
                (s.maLuotHoatDong
                    ? '<a href="/QuanLyNhaTro/DoiChieuThanhToan?ma=' + encodeURIComponent(s.maLuotHoatDong) + '">' + esc(s.maLuotHoatDong) + '</a>' +
                      (s.trangThaiLuotHoatDong === LUOT.DangDoiChieu ? ' <span class="badge bg-orange-lt">Chờ đối chiếu</span>' : ' <span class="badge bg-blue-lt">Chờ chuyển khoản</span>')
                    : '<span class="text-secondary">Không có</span>') +
                ' </div></div>' +
                '</div>';

            if (s.laAdmin && options.daGui && s.conLai > 0) {
                var locked = !!s.maLuotHoatDong;
                html += '<div class="card card-sm mb-2"><div class="card-body">' +
                    ' <label class="form-check form-switch mb-2"><input class="form-check-input" type="checkbox" id="hdttChoPhep" ' +
                    (s.choPhepThanhToanMotPhan ? 'checked ' : '') + (locked ? 'disabled ' : '') + '>' +
                    ' <span class="form-check-label">Cho phép khách trả một phần</span></label>' +
                    ' <div class="input-group input-group-sm" id="hdttMinWrap"' + (s.choPhepThanhToanMotPhan ? '' : ' style="display:none"') + '>' +
                    '  <span class="input-group-text">Tối thiểu</span><input type="number" class="form-control" id="hdttMin" min="1" step="1" value="' + (s.soTienThanhToanToiThieu || '') + '"' + (locked ? ' disabled' : '') + '>' +
                    '  <span class="input-group-text">đ</span><button class="btn btn-primary" type="button" id="hdttLuu"' + (locked ? ' disabled' : '') + '>Lưu</button></div>' +
                    (locked ? ' <div class="form-hint mt-1">Đang có lượt thanh toán hoạt động nên chưa đổi được cấu hình.</div>' : '') +
                    '</div></div>';
            }

            $c.html(html);

            $c.find('#hdttChoPhep').on('change', function () {
                if (this.checked) {
                    $c.find('#hdttMinWrap').show();
                } else {
                    savePartial(hoaDonId, false, null, $c, options);
                }
            });
            $c.find('#hdttLuu').on('click', function () {
                savePartial(hoaDonId, true, Number($c.find('#hdttMin').val()), $c, options);
            });
        }).fail(function () {
            $c.empty();
        });
    }

    function savePartial(hoaDonId, choPhep, toiThieu, $c, options) {
        $.ajax({
            url: '/HoaDon/CauHinhThanhToanMotPhan/' + hoaDonId,
            type: 'POST',
            contentType: 'application/json',
            headers: { 'RequestVerificationToken': token() },
            data: JSON.stringify({ choPhep: choPhep, soTienToiThieu: toiThieu })
        }).done(function (res) {
            Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: res.message, showConfirmButton: false, timer: 2000 });
        }).fail(function (xhr) {
            Swal.fire('Lỗi', errorMessage(xhr, 'Không lưu được cấu hình.'), 'error');
        }).always(function () {
            renderSummary(hoaDonId, $c, options);
        });
    }

    window.HdThanhToan = { openThuTien: openThuTien, renderSummary: renderSummary };
})(window, jQuery);
