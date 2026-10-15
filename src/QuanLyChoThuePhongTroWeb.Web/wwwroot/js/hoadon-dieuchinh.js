// Điều chỉnh hóa đơn chờ duyệt, đã chốt hoặc đã gửi: sửa chỉ số điện nước và/hoặc các khoản phí.
// Dùng ở màn Hóa đơn quản lý. Danh sách dòng gửi lên là danh sách cuối cùng của hóa đơn (server thay toàn bộ dòng).
(function (window, $) {
    'use strict';

    var MODAL_ID = 'hdDieuChinhModal';
    var PREVIEW_DELAY_MS = 400;

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

    // Ô trống hoặc không phải số thì NaN (Number('') là 0 nên phải chặn riêng).
    function parseNum(raw) {
        var text = $.trim(raw === null || raw === undefined ? '' : raw.toString());
        if (text === '') return NaN;
        var n = Number(text);
        return isFinite(n) ? n : NaN;
    }

    // Số lượng và chỉ số tối đa 3 chữ số thập phân (cột decimal(18,3)).
    function tooManyDecimals(n) {
        return Math.round(n * 1000) / 1000 !== n;
    }

    function ensureModal() {
        if (document.getElementById(MODAL_ID)) return;
        $('body').append(
            '<div class="modal fade" id="' + MODAL_ID + '" tabindex="-1" aria-hidden="true">' +
            ' <div class="modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable"><div class="modal-content">' +
            '  <div class="modal-header py-2"><h5 class="modal-title"><i class="ti ti-adjustments me-1"></i> Điều chỉnh hóa đơn <span class="text-primary" data-f="ma"></span></h5>' +
            '   <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button></div>' +
            '  <div class="modal-body py-3">' +
            '   <div data-f="banner"></div>' +
            '   <div class="text-secondary small mb-3" data-f="han"></div>' +
            '   <h6 class="mb-2">Chỉ số điện nước</h6>' +
            '   <div class="table-responsive mb-1">' +
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
            '   <div class="form-hint mb-1">Đổi chỉ số sẽ tính lại dòng tiền điện, nước bên dưới. Bạn vẫn sửa tay được từng dòng sau đó.</div>' +
            '   <div class="small mb-2" data-f="chi-so-ghi-chu" role="status"></div>' +
            '   <div class="mb-3" data-f="canh-bao"></div>' +
            '   <h6 class="mb-2">Các khoản phí</h6>' +
            '   <div class="table-responsive">' +
            '    <table class="table table-vcenter table-sm table-striped mb-0">' +
            '     <thead><tr><th>Tên khoản phí</th><th class="text-end" style="width:16%">Đơn giá (đ)</th><th class="text-center" style="width:12%">Số lượng</th>' +
            '      <th style="width:8%">Đơn vị</th><th class="text-end" style="width:16%">Thành tiền</th><th class="text-center" style="width:7%">Xóa</th></tr></thead>' +
            '     <tbody data-f="dong"></tbody>' +
            '    </table>' +
            '   </div>' +
            '   <button type="button" class="btn btn-outline-primary btn-sm mt-2" data-f="them"><i class="ti ti-plus me-1"></i> Thêm dòng phí mới</button>' +
            '   <div class="text-end mt-3">' +
            '    <div class="text-secondary small" data-f="tong-cu"></div>' +
            '    <div>Tổng cộng: <strong class="text-primary fs-3 hd-num" data-f="tong"></strong></div>' +
            '   </div>' +
            '   <div class="form-hint mt-2" data-f="hint"></div>' +
            '  </div>' +
            '  <div class="modal-footer"><button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Đóng</button>' +
            '   <button type="button" class="btn btn-primary" data-f="luu"><i class="ti ti-edit me-1"></i> Lưu điều chỉnh</button></div>' +
            ' </div></div></div>');
    }

    function field(name) {
        return $('#' + MODAL_ID).find('[data-f="' + name + '"]');
    }

    // Câu giải thích theo trạng thái phát hành: 1 ChoDuyet, 2 DaChot, 3 DaGui.
    function confirmText(trangThai) {
        if (trangThai === 3) return 'Hệ thống hủy hóa đơn cũ, tạo hóa đơn thay thế chờ duyệt và báo khách trên cổng.';
        if (trangThai === 2) return 'Lưu thay đổi, hóa đơn vẫn ở trạng thái đã chốt.';
        return 'Lưu thay đổi, hóa đơn vẫn chờ duyệt.';
    }

    // Cảnh báo bất thường từ server (chỉ báo, không chặn lưu).
    function showWarnings(list) {
        var $box = field('canh-bao');
        $box.empty();
        (list || []).forEach(function (w) {
            $box.append($('<div class="alert alert-warning py-2 mb-2">').text(w));
        });
    }

    // ---------- Bảng các khoản phí ----------

    // Thành tiền của một dòng. Dòng cũ không đổi đơn giá và số lượng giữ số đã lưu (tiền phòng tính theo ngày ở có SoLuong = 1 mà
    // thành tiền thấp hơn đơn giá), khớp cách server giữ thành tiền dòng không đổi. Nhân bằng số nguyên để làm tròn như server.
    function rowAmount($tr) {
        var g = parseNum($tr.find('.hd-dong-gia').val());
        var s = parseNum($tr.find('.hd-dong-sl').val());
        if (isNaN(g) || isNaN(s)) return 0;
        var stored = $tr.data('stored');
        if (stored && g === stored.gia && s === stored.sl) return stored.tong;
        return Math.round(g * Math.round(s * 1000) / 1000);
    }

    function refreshRow($tr) {
        $tr.find('.hd-dong-tien').val(vnd(rowAmount($tr)));
    }

    function currentTotal() {
        var total = 0;
        field('dong').children('tr').each(function () { total += rowAmount($(this)); });
        return total;
    }

    function refreshTotal() {
        field('tong').text(vnd(currentTotal()));
    }

    // line: { id, ten, donGia, soLuong, tongTien, loai, donVi }. Dòng đã lưu (id > 0) nhớ số đã lưu để giữ thành tiền khi không đổi.
    function addRow(line, locked) {
        var $tr = $('<tr>').data({ id: line.id || 0, loai: line.loai || 'khac' });
        if (line.id > 0) {
            $tr.data('stored', { gia: Number(line.donGia), sl: Number(line.soLuong), tong: Number(line.tongTien) });
        }

        var $ten = $('<input type="text" class="form-control form-control-sm hd-dong-ten" maxlength="500" aria-label="Tên khoản phí">').val(line.ten || '');
        var $gia = $('<input type="number" class="form-control form-control-sm text-end hd-dong-gia" min="0" step="1" aria-label="Đơn giá">').val(line.donGia);
        var $sl = $('<input type="number" class="form-control form-control-sm text-center hd-dong-sl" min="0" step="0.001" aria-label="Số lượng">').val(line.soLuong);
        var $tien = $('<input type="text" class="form-control form-control-sm text-end hd-num hd-dong-tien" disabled aria-label="Thành tiền">');
        var $xoa = $('<button type="button" class="btn btn-link text-danger p-0 hd-dong-xoa" aria-label="Xóa dòng"><i class="ti ti-trash"></i></button>');

        $tr.append(
            $('<td>').append($ten),
            $('<td>').append($gia),
            $('<td>').append($sl),
            $('<td class="align-middle">').append($('<span class="text-secondary small">').text(line.donVi || '')),
            $('<td>').append($tien),
            $('<td class="text-center align-middle">').append($xoa));

        if (locked) {
            $ten.add($gia).add($sl).add($xoa).prop('disabled', true);
        } else {
            $gia.add($sl).on('input', function () {
                refreshRow($tr);
                refreshTotal();
            });
            $xoa.on('click', function () {
                $tr.remove();
                refreshTotal();
            });
        }

        field('dong').append($tr);
        refreshRow($tr);
        return $tr;
    }

    // ---------- Chỉ số và xem trước dòng điện nước ----------

    function readReadings(s) {
        var dienRaw = $('#hdDcDien').val();
        var nuocRaw = $('#hdDcNuoc').val();
        var dien = parseNum(dienRaw);
        var nuoc = parseNum(nuocRaw);
        if (isNaN(dien) || isNaN(nuoc) || dien < 0 || nuoc < 0) {
            return { error: 'Vui lòng nhập chỉ số hợp lệ.' };
        }
        if (tooManyDecimals(dien) || tooManyDecimals(nuoc)) {
            return { error: 'Chỉ số mới không được có quá 3 chữ số thập phân.' };
        }
        if (dien < Number(s.chiSoDienCu)) {
            return { error: 'Chỉ số điện mới không được nhỏ hơn chỉ số cũ.' };
        }
        if (nuoc < Number(s.chiSoNuocCu)) {
            return { error: 'Chỉ số nước mới không được nhỏ hơn chỉ số cũ.' };
        }
        return { dien: dien, nuoc: nuoc };
    }

    // Cập nhật dòng điện/nước theo xem trước. Không có dòng mới nghĩa là kỳ này không phát sinh tiền loại đó: bỏ dòng đó như lúc tạo hóa đơn.
    function applyMeterLine(loai, line, tenLoai) {
        var $rows = field('dong').children('tr').filter(function () { return $(this).data('loai') === loai; });
        var $tr = $rows.first();
        if (!line) {
            if ($tr.length) {
                $tr.remove();
                return 'Không phát sinh tiền ' + tenLoai + ' với chỉ số mới nên dòng tiền ' + tenLoai + ' đã được bỏ.';
            }
            return '';
        }

        if (!$tr.length) {
            $tr = addRow({ id: 0, ten: line.tenDichVu, donGia: line.donGia, soLuong: line.soLuong, tongTien: line.tongTien, loai: loai, donVi: line.donVi }, false);
            return '';
        }

        $tr.find('.hd-dong-ten').val(line.tenDichVu);
        $tr.find('.hd-dong-gia').val(line.donGia);
        $tr.find('.hd-dong-sl').val(line.soLuong);
        $tr.removeData('stored');
        refreshRow($tr);
        return '';
    }

    // Chỉ tính lại dòng của đồng hồ vừa đổi chỉ số: dòng còn lại có thể đã được sửa tay nên không được ghi đè.
    function applyPreview(p, doiDien, doiNuoc) {
        var notes = [];
        if (doiDien) notes.push(applyMeterLine('dien', p.dien, 'điện'));
        if (doiNuoc) notes.push(applyMeterLine('nuoc', p.nuoc, 'nước'));
        notes = notes.filter(function (x) { return x; });
        showWarnings(p.canhBaos);
        field('chi-so-ghi-chu').text(notes.join(' ')).toggleClass('text-secondary', notes.length > 0);
        refreshTotal();
    }

    function open(options) {
        ensureModal();
        var hoaDonId = options.hoaDonId;

        var previewTimer = null;
        var previewSeq = 0;
        var previewRequest = null;
        var lastPreview = null;

        function clearPreviewState() {
            clearTimeout(previewTimer);
            previewTimer = null;
            previewSeq++;
            previewRequest = null;
        }

        $.get('/HoaDon/DieuChinh/' + hoaDonId).done(function (s) {
            clearPreviewState();
            var coThe = s.coTheDieuChinh !== false;
            var trangThai = Number(s.trangThaiPhatHanhValue ?? options.trangThai);
            if (isNaN(trangThai)) {
                console.warn('Không đọc được trạng thái phát hành của hóa đơn', hoaDonId);
                Swal.fire({ title: 'Không mở được điều chỉnh', text: 'Không xác định được trạng thái hóa đơn, vui lòng tải lại.', icon: 'error' });
                return;
            }
            var goc = (s.chiTiets || []).map(function (l) {
                return { id: l.chiTietHoaDonId, ten: l.tenDichVu, donGia: Number(l.donGia), soLuong: Number(l.soLuong), tongTien: Number(l.tongTien), loai: l.loai, donVi: l.donVi };
            });

            field('ma').text(s.maHoaDon || options.maHoaDon || ('#' + hoaDonId));
            field('dien-cu').text(soVN(s.chiSoDienCu));
            field('dien-hien-tai').text(soVN(s.chiSoDienMoi));
            field('nuoc-cu').text(soVN(s.chiSoNuocCu));
            field('nuoc-hien-tai').text(soVN(s.chiSoNuocMoi));
            $('#hdDcDien').val(s.chiSoDienMoi);
            $('#hdDcNuoc').val(s.chiSoNuocMoi);
            field('chi-so-ghi-chu').text('');
            field('canh-bao').empty();
            field('han').text(s.hanPhanHoi ? 'Hạn phản hồi của khách: ' + s.hanPhanHoi : '').toggle(!!s.hanPhanHoi);
            field('hint').text(confirmText(trangThai));
            field('tong-cu').text('Tổng trước điều chỉnh: ' + vnd(s.tongTien));

            field('dong').empty();
            goc.forEach(function (line) { addRow(line, !coThe); });
            refreshTotal();
            lastPreview = { dien: Number(s.chiSoDienMoi), nuoc: Number(s.chiSoNuocMoi) };

            var $btn = field('luu');
            if (!coThe) {
                field('banner').html('<div class="alert alert-warning"><i class="ti ti-lock me-1"></i> ' + esc(s.lyDoKhoa) + '</div>');
            } else {
                field('banner').empty();
            }
            $('#hdDcDien, #hdDcNuoc').prop('disabled', !coThe);
            field('them').prop('disabled', !coThe).off('click').on('click', function () {
                var $tr = addRow({ id: 0, ten: 'Khoản phí khác', donGia: 0, soLuong: 1, tongTien: 0, loai: 'khac', donVi: '' }, false);
                refreshTotal();
                $tr.find('.hd-dong-ten').trigger('focus').trigger('select');
            });
            $btn.prop('disabled', !coThe).html('<i class="ti ti-edit me-1"></i> Lưu điều chỉnh');

            // Đổi chỉ số thì chờ ngừng gõ rồi hỏi server tính lại dòng điện nước (cùng công thức lúc tạo hóa đơn).
            function runPreview() {
                clearTimeout(previewTimer);
                previewTimer = null;
                var r = readReadings(s);
                if (r.error) {
                    field('chi-so-ghi-chu').text(r.error).removeClass('text-secondary');
                    showWarnings([]);
                    lastPreview = null;
                    return $.Deferred().resolve().promise();
                }
                if (lastPreview && r.dien === lastPreview.dien && r.nuoc === lastPreview.nuoc) {
                    field('chi-so-ghi-chu').text('');
                    return $.Deferred().resolve().promise();
                }

                var doiDien = !lastPreview || r.dien !== lastPreview.dien;
                var doiNuoc = !lastPreview || r.nuoc !== lastPreview.nuoc;
                var seq = ++previewSeq;
                previewRequest = $.get('/HoaDon/DieuChinh/' + hoaDonId + '/TinhLai', { chiSoDienMoi: r.dien, chiSoNuocMoi: r.nuoc })
                    .done(function (p) {
                        if (seq !== previewSeq) return;
                        lastPreview = { dien: r.dien, nuoc: r.nuoc };
                        applyPreview(p, doiDien, doiNuoc);
                    })
                    .fail(function (xhr) {
                        if (seq !== previewSeq) return;
                        showWarnings([]);
                        lastPreview = null;
                        field('chi-so-ghi-chu').text(errorMessage(xhr, 'Không tính lại được dòng điện nước.')).removeClass('text-secondary');
                    });
                return previewRequest;
            }

            // Chờ bản tính lại đang chạy hoặc đang hẹn giờ xong để danh sách dòng khớp với chỉ số trước khi gửi.
            function flushPreview() {
                if (previewTimer !== null) return runPreview();
                if (previewRequest && previewRequest.state() === 'pending') return previewRequest;
                return $.Deferred().resolve().promise();
            }

            $('#hdDcDien, #hdDcNuoc').off('input.hdpreview').on('input.hdpreview', function () {
                clearTimeout(previewTimer);
                previewTimer = setTimeout(runPreview, PREVIEW_DELAY_MS);
            });

            function submit() {
                var r = readReadings(s);
                if (r.error) {
                    warn(r.error);
                    return;
                }

                // Đổi chỉ số mà lần tính lại gần nhất lỗi hoặc chưa khớp số đang nhập: dòng điện nước còn là số cũ, không gửi.
                var doiChiSo = r.dien !== Number(s.chiSoDienMoi) || r.nuoc !== Number(s.chiSoNuocMoi);
                if (doiChiSo && (!lastPreview || lastPreview.dien !== r.dien || lastPreview.nuoc !== r.nuoc)) {
                    warn('Chưa tính lại được dòng điện nước theo chỉ số mới, vui lòng thử lại.');
                    return;
                }

                var lines = [];
                var lineError = null;
                field('dong').children('tr').each(function () {
                    var $tr = $(this);
                    var ten = $.trim($tr.find('.hd-dong-ten').val());
                    var gia = parseNum($tr.find('.hd-dong-gia').val());
                    var sl = parseNum($tr.find('.hd-dong-sl').val());
                    if (ten === '') { lineError = 'Tên khoản phí không được để trống.'; return false; }
                    if (isNaN(gia) || isNaN(sl) || gia < 0 || sl < 0) { lineError = 'Đơn giá và số lượng phải lớn hơn hoặc bằng 0.'; return false; }
                    if (gia !== Math.floor(gia)) { lineError = 'Đơn giá phải là số nguyên VND.'; return false; }
                    if (tooManyDecimals(sl)) { lineError = 'Số lượng không được có quá 3 chữ số thập phân.'; return false; }
                    lines.push({ chiTietHoaDonId: $tr.data('id') > 0 ? $tr.data('id') : null, tenDichVu: ten, donGia: gia, soLuong: sl });
                });
                if (lineError) {
                    warn(lineError);
                    return;
                }
                if (lines.length === 0) {
                    warn('Hóa đơn phải có ít nhất một khoản phí.');
                    return;
                }
                if (currentTotal() <= 0) {
                    warn('Tổng tiền hóa đơn phải lớn hơn 0.');
                    return;
                }

                var doiDong = lines.length !== goc.length || lines.some(function (l, i) {
                    var g = goc.filter(function (x) { return x.id === l.chiTietHoaDonId; })[0];
                    return !g || g.ten !== l.tenDichVu || g.donGia !== l.donGia || g.soLuong !== l.soLuong || goc.indexOf(g) !== i;
                });
                if (!doiChiSo && !doiDong) {
                    warn('Không có thay đổi.');
                    return;
                }

                var body = { chiTiets: lines, trangThaiPhatHanhHienTai: trangThai };
                if (doiChiSo) {
                    body.chiSoDienMoi = r.dien;
                    body.chiSoNuocMoi = r.nuoc;
                }

                Swal.fire({
                    title: 'Lưu điều chỉnh?',
                    text: confirmText(trangThai),
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Điều chỉnh',
                    cancelButtonText: 'Hủy'
                }).then(function (res) {
                    if (!res.isConfirmed) return;
                    $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang lưu...');
                    $.ajax({
                        url: '/HoaDon/DieuChinh/' + hoaDonId,
                        type: 'POST',
                        contentType: 'application/json',
                        headers: { 'RequestVerificationToken': token() },
                        data: JSON.stringify(body)
                    }).done(function (res) {
                        var d = res.data || {};
                        bootstrap.Modal.getOrCreateInstance(document.getElementById(MODAL_ID)).hide();
                        Swal.fire({
                            icon: 'success',
                            title: 'Đã điều chỉnh hóa đơn',
                            html: esc(res.message) + '<br>Mã hóa đơn: ' + esc(d.maHoaDon) +
                                '<br>Tổng tiền mới: ' + esc(vnd(d.tongTien))
                        });
                        if (typeof options.onDone === 'function') options.onDone(res.data);
                    }).fail(function (xhr) {
                        Swal.fire({ title: 'Không điều chỉnh được', text: errorMessage(xhr, 'Vui lòng thử lại.'), icon: 'error' });
                        $btn.prop('disabled', false).html('<i class="ti ti-edit me-1"></i> Lưu điều chỉnh');
                    });
                });
            }

            $btn.off('click').on('click', function () {
                flushPreview().always(submit);
            });

            var modalEl = document.getElementById(MODAL_ID);
            $(modalEl).off('hidden.bs.modal.hdpreview').on('hidden.bs.modal.hdpreview', clearPreviewState);
            bootstrap.Modal.getOrCreateInstance(modalEl).show();
        }).fail(function (xhr) {
            Swal.fire({ title: 'Không mở được điều chỉnh', text: errorMessage(xhr, 'Vui lòng thử lại.'), icon: 'error' });
        });
    }

    window.HdDieuChinh = { open: open };
})(window, jQuery);
