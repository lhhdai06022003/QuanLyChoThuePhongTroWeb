// Màn "Đối chiếu thanh toán" (spec thanh toán §13, §21, §27.2).
// Ngày giờ từ server là giờ Việt Nam dạng "yyyy-MM-ddTHH:mm:ss" (không kèm múi giờ).
(function (window, $) {
    'use strict';

    var cfg = window.dcConfig || { laAdmin: false, maTraCuu: '' };
    var MINH_CHUNG = { ChoXacNhan: 0, DaXacNhan: 1, TuChoi: 2 };
    var LUOT = { ChoThanhToan: 0, DangDoiChieu: 2 };
    var daXuLy = false;
    var viewer = null;
    var table = null;

    function esc(value) {
        return (value === null || value === undefined ? '' : value.toString())
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
    }

    function vnd(n) {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n || 0);
    }

    function dt(text) {
        if (!text) return '';
        var d = new Date(text);
        if (isNaN(d.getTime())) return '';
        var p = function (x) { return String(x).padStart(2, '0'); };
        return p(d.getDate()) + '/' + p(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + p(d.getHours()) + ':' + p(d.getMinutes());
    }

    function token() {
        return $('input[name="__RequestVerificationToken"]').first().val() || '';
    }

    function errorMessage(xhr, fallback) {
        return (xhr && xhr.responseJSON && xhr.responseJSON.message) || fallback;
    }

    function proofBadge(trangThai) {
        if (trangThai === MINH_CHUNG.DaXacNhan) return '<span class="badge bg-green-lt">Đã xác nhận</span>';
        if (trangThai === MINH_CHUNG.TuChoi) return '<span class="badge bg-red-lt">Bị từ chối</span>';
        return '<span class="badge bg-yellow-lt">Chờ xác nhận</span>';
    }

    // ---------- Hàng đợi ----------

    function initTable() {
        table = $('#dcTable').DataTable({
            language: { url: 'https://cdn.datatables.net/plug-ins/1.13.6/i18n/vi.json' },
            serverSide: true,
            processing: true,
            ordering: false,
            pageLength: 25,
            ajax: {
                url: '/DoiChieuThanhToan/DanhSach',
                type: 'POST',
                data: function (d) {
                    d.chiNhanhId = $('#dcChiNhanh').val();
                    d.daXuLy = daXuLy;
                    d.chiChoAdmin = $('#dcChiChoAdmin').is(':checked');
                    d.__RequestVerificationToken = token();
                }
            },
            columns: [
                { data: 'maYeuCau', className: 'fw-medium text-nowrap', render: esc },
                { data: 'maHoaDon', className: 'text-nowrap', render: esc },
                { data: 'tenPhong', render: esc },
                { data: 'tenChiNhanh', className: 'text-secondary', render: esc },
                { data: 'tenKhachThue', render: esc },
                { data: 'soTienLuot', className: 'text-end hd-num', render: vnd },
                { data: 'maGiaoDichKhaiBao', render: function (d) { return d ? '<code>' + esc(d) + '</code>' : '<span class="text-secondary">—</span>'; } },
                { data: 'ngayChuyenKhaiBaoUtc', className: 'hd-num', render: dt },
                { data: 'ngayNopUtc', className: 'hd-num', render: dt },
                {
                    data: null, render: function (row) {
                        return proofBadge(row.trangThai) + (row.choAdmin ? ' <span class="badge bg-orange text-white">Chờ Admin</span>' : '');
                    }
                },
                {
                    data: 'minhChungId', className: 'text-end', render: function (id) {
                        return '<button type="button" class="btn btn-sm btn-primary dc-open" data-id="' + id + '"><i class="ti ti-eye me-1"></i>' +
                            (daXuLy ? 'Xem' : 'Đối chiếu') + '</button>';
                    }
                }
            ]
        });

        $('#dcTable').on('click', '.dc-open', function () { openProof($(this).data('id')); });
        $('#dcChiNhanh, #dcChiChoAdmin').on('change', function () { table.ajax.reload(); });
        $('[data-dc-tab]').on('click', function (e) {
            e.preventDefault();
            $('[data-dc-tab]').removeClass('active');
            $(this).addClass('active');
            daXuLy = $(this).data('dc-tab') === 1;
            table.ajax.reload();
        });
    }

    // ---------- Tra cứu theo mã ----------

    function traCuu(ma) {
        var $out = $('#dcTraCuuKetQua');
        if (!ma) { $out.empty(); return; }
        $out.html('<div class="text-secondary"><span class="spinner-border spinner-border-sm me-2"></span>Đang tra cứu...</div>');
        $.get('/DoiChieuThanhToan/TraCuu', { ma: ma }).done(function (res) {
            var d = res.data;
            var l = d.luotThanhToan;
            var pending = (l.minhChungs || []).filter(function (m) { return m.trangThai === MINH_CHUNG.ChoXacNhan; })[0];
            var proofs = (l.minhChungs || []).map(function (m) {
                return '<li>' + proofBadge(m.trangThai) + ' nộp ' + esc(dt(m.ngayNopUtc)) +
                    (m.maGiaoDichNganHang ? ', mã <code>' + esc(m.maGiaoDichNganHang) + '</code>' : '') +
                    (m.lyDoTuChoi ? ' — ' + esc(m.lyDoTuChoi) : '') + '</li>';
            }).join('');

            $out.html(
                '<div class="card card-sm"><div class="card-body">' +
                ' <div class="d-flex flex-wrap gap-3 align-items-center mb-2">' +
                '  <div><div class="text-secondary small">Lượt</div><strong>' + esc(l.maYeuCau) + '</strong> <span class="badge bg-azure-lt">' + esc(l.trangThaiText) + '</span>' +
                (l.choAdmin ? ' <span class="badge bg-orange text-white">Chờ Admin</span>' : '') + '</div>' +
                '  <div><div class="text-secondary small">Số tiền lượt</div><strong>' + vnd(l.soTien) + '</strong></div>' +
                '  <div><div class="text-secondary small">Hóa đơn</div><strong>' + esc(d.maHoaDon) + '</strong> · ' + esc(d.tenPhong) + ' · ' + esc(d.tenKhachThue) + '</div>' +
                '  <div><div class="text-secondary small">Còn nợ hiện tại</div><strong class="text-danger">' + vnd(d.conLai) + '</strong></div>' +
                ' </div>' +
                (proofs ? '<ul class="mb-2 ps-3">' + proofs + '</ul>' : '<div class="text-secondary small mb-2">Chưa có minh chứng.</div>') +
                ' <div class="d-flex gap-2">' +
                (pending ? '<button type="button" class="btn btn-sm btn-primary dc-open" data-id="' + pending.minhChungId + '"><i class="ti ti-eye me-1"></i> Đối chiếu minh chứng</button>' : '') +
                (!d.hoaDonDaHuy && d.conLai > 0 && !pending
                    ? '<a class="btn btn-sm btn-outline-success" href="/QuanLyNhaTro/QuanLyHoaDon?hoaDonId=' + d.hoaDonId + '"><i class="ti ti-report-money me-1"></i> Mở hóa đơn để thu thủ công</a>'
                    : '') +
                ' </div>' +
                '</div></div>');
            $out.find('.dc-open').on('click', function () { openProof($(this).data('id')); });
        }).fail(function (xhr) {
            $out.html('<div class="alert alert-warning mb-0">' + esc(errorMessage(xhr, 'Không tìm thấy lượt thanh toán.')) + '</div>');
        });
    }

    // ---------- Modal đối chiếu ----------

    function initViewer() {
        if (!viewer) {
            viewer = new MeterImageViewer({
                containerId: 'dcViewerStage',
                imageId: 'dcViewerImg',
                zoomInBtnId: 'dcZoomIn',
                zoomOutBtnId: 'dcZoomOut',
                rotateBtnId: 'dcRotate',
                resetBtnId: 'dcReset'
            });
        }
    }

    function openProof(minhChungId) {
        initViewer();
        $('#dcInfo').html('<div class="text-secondary"><span class="spinner-border spinner-border-sm me-2"></span>Đang tải...</div>');
        $('#dcFormWrap, #dcHistory, #dcOtherProofs').empty();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('dcModal')).show();

        $.get('/DoiChieuThanhToan/ChiTiet/' + minhChungId).done(function (res) {
            renderDetail(res.data);
            viewer.loadImage('/DoiChieuThanhToan/AnhMinhChung/' + minhChungId);
        }).fail(function (xhr) {
            $('#dcInfo').html('<div class="alert alert-danger">' + esc(errorMessage(xhr, 'Không tải được minh chứng.')) + '</div>');
        });
    }

    function renderDetail(d) {
        var l = d.luotThanhToan;
        var proof = (l.minhChungs || []).filter(function (m) { return m.minhChungId === d.minhChungDangXemId; })[0] || {};
        $('#dcModalMa').text(l.maYeuCau);

        $('#dcInfo').html(
            (l.choAdmin ? '<div class="alert alert-warning py-2"><i class="ti ti-user-shield me-1"></i> Lượt này đang <strong>chờ Admin</strong> xử lý (số thực nhận vượt số còn nợ).</div>' : '') +
            '<div class="datagrid mb-3">' +
            ' <div class="datagrid-item"><div class="datagrid-title">Hóa đơn</div><div class="datagrid-content">' + esc(d.maHoaDon) + '</div></div>' +
            ' <div class="datagrid-item"><div class="datagrid-title">Phòng · Khách</div><div class="datagrid-content">' + esc(d.tenPhong) + ' · ' + esc(d.tenKhachThue) + '</div></div>' +
            ' <div class="datagrid-item"><div class="datagrid-title">Tổng · Đã thu</div><div class="datagrid-content hd-num">' + vnd(d.tongTien) + ' · ' + vnd(d.daThu) + '</div></div>' +
            ' <div class="datagrid-item"><div class="datagrid-title">Còn nợ</div><div class="datagrid-content text-danger fw-bold hd-num">' + vnd(d.conLai) + '</div></div>' +
            ' <div class="datagrid-item"><div class="datagrid-title">Số tiền lượt</div><div class="datagrid-content fw-bold hd-num">' + vnd(l.soTien) + '</div></div>' +
            ' <div class="datagrid-item"><div class="datagrid-title">Trạng thái lượt</div><div class="datagrid-content">' + esc(l.trangThaiText) + '</div></div>' +
            ' <div class="datagrid-item"><div class="datagrid-title">Khách khai chuyển</div><div class="datagrid-content hd-num">' + esc(dt(proof.ngayChuyenKhaiBaoUtc)) + '</div></div>' +
            ' <div class="datagrid-item"><div class="datagrid-title">Mã GD khai báo</div><div class="datagrid-content">' + (proof.maGiaoDichNganHang ? '<code>' + esc(proof.maGiaoDichNganHang) + '</code>' : '—') + '</div></div>' +
            ' <div class="datagrid-item"><div class="datagrid-title">Minh chứng</div><div class="datagrid-content">' + proofBadge(proof.trangThai) +
            (proof.lyDoTuChoi ? '<div class="small text-secondary">' + esc(proof.lyDoTuChoi) + '</div>' : '') + '</div></div>' +
            '</div>');

        if (d.coTheDoiChieu) {
            renderForm(d, l, proof);
        } else {
            $('#dcFormWrap').html(proof.trangThai === MINH_CHUNG.ChoXacNhan
                ? '<div class="alert alert-secondary mb-0">Lượt này đang chờ Admin xử lý, bạn không thể xác nhận hoặc từ chối.</div>'
                : '');
        }

        var others = (l.minhChungs || []).filter(function (m) { return m.minhChungId !== d.minhChungDangXemId; });
        if (others.length) {
            $('#dcOtherProofs').html('<div class="text-secondary mb-1">Minh chứng khác của lượt:</div>' + others.map(function (m) {
                return '<a href="#" class="me-2 dc-other" data-id="' + m.minhChungId + '">' + proofBadge(m.trangThai) + ' ' + esc(dt(m.ngayNopUtc)) + '</a>';
            }).join(''));
            $('#dcOtherProofs .dc-other').on('click', function (e) { e.preventDefault(); openProof($(this).data('id')); });
        }

        $('#dcHistory').html(
            '<details><summary class="text-secondary">Lịch sử lượt (' + (l.lichSu || []).length + ')</summary>' +
            '<div class="table-responsive mt-2"><table class="table table-sm table-vcenter"><tbody>' +
            (l.lichSu || []).map(function (h) {
                return '<tr><td class="text-nowrap hd-num text-secondary">' + esc(dt(h.thoiGianUtc)) + '</td><td>' + esc(h.trangThaiCu) + ' → ' + esc(h.trangThaiMoi) +
                    '</td><td>' + esc(h.nguoiThucHien) + '</td><td class="text-secondary">' + esc(h.lyDo) + '</td></tr>';
            }).join('') +
            '</tbody></table></div></details>');
    }

    function renderForm(d, l, proof) {
        var ngay = proof.ngayChuyenKhaiBaoUtc ? proof.ngayChuyenKhaiBaoUtc.slice(0, 16) : '';
        $('#dcFormWrap').html(
            '<form id="dcForm" class="card card-sm" autocomplete="off"><div class="card-body">' +
            ' <h6 class="card-title mb-2">Đối chiếu với sao kê ngân hàng</h6>' +
            ' <div class="mb-2"><label class="form-label required">Số tiền thực nhận trên sao kê</label>' +
            '  <div class="input-group"><input type="number" class="form-control" name="soTien" min="1" step="1" placeholder="Gõ lại theo sao kê" required><span class="input-group-text">đ</span></div>' +
            '  <div class="form-hint" id="dcPreview">Nhập số tiền thực nhận để xem kết quả ghi nhận.</div></div>' +
            ' <div class="row g-2">' +
            '  <div class="col-6"><label class="form-label">Mã giao dịch</label><input type="text" class="form-control" name="ma" maxlength="100" value="' + esc(proof.maGiaoDichNganHang || '') + '"></div>' +
            '  <div class="col-6"><label class="form-label required">Ngày giao dịch</label><input type="datetime-local" class="form-control" name="ngay" value="' + esc(ngay) + '" required></div>' +
            ' </div>' +
            ' <div class="mt-2"><label class="form-label" id="dcGhiChuLabel">Ghi chú</label><textarea class="form-control" name="ghiChu" rows="2" maxlength="500"></textarea></div>' +
            ' <div class="d-flex gap-2 mt-3">' +
            '  <button type="submit" class="btn btn-success flex-fill" id="dcXacNhan"><i class="ti ti-check me-1"></i> Xác nhận</button>' +
            '  <button type="button" class="btn btn-outline-danger" id="dcTuChoi"><i class="ti ti-x me-1"></i> Từ chối</button>' +
            ' </div>' +
            '</div></form>');

        var $form = $('#dcForm');
        $form.find('[name="soTien"]').on('input', function () {
            var thuc = Number(this.value);
            var text = 'Nhập số tiền thực nhận để xem kết quả ghi nhận.';
            var batBuocGhiChu = false;
            if (thuc > 0) {
                if (thuc > d.conLai) {
                    batBuocGhiChu = true;
                    text = cfg.laAdmin
                        ? 'Vượt số còn nợ ' + vnd(thuc - d.conLai) + '. Hệ thống ghi nhận đủ nợ ' + vnd(d.conLai) + ' và đánh dấu tiền thừa để xử lý ngoài hệ thống.'
                        : 'Vượt số còn nợ ' + vnd(thuc - d.conLai) + '. Lượt sẽ được chuyển cho Admin xử lý.';
                } else if (thuc === l.soTien) {
                    text = 'Khớp số lượt. Ghi nhận ' + vnd(thuc) + ', hóa đơn còn nợ ' + vnd(d.conLai - thuc) + '.';
                } else {
                    batBuocGhiChu = true;
                    text = 'Thực nhận ' + (thuc < l.soTien ? 'thấp' : 'cao') + ' hơn số lượt ' + vnd(Math.abs(thuc - l.soTien)) +
                        ', hệ thống ghi nhận đúng ' + vnd(thuc) + ', hóa đơn còn nợ ' + vnd(d.conLai - thuc) + '. Cần ghi chú lý do.';
                }
            }
            $('#dcPreview').text(text);
            $('#dcGhiChuLabel').toggleClass('required', batBuocGhiChu);
        });

        $form.on('submit', function (e) {
            e.preventDefault();
            var $btn = $('#dcXacNhan').prop('disabled', true);
            $.ajax({
                url: '/DoiChieuThanhToan/XacNhan',
                type: 'POST',
                contentType: 'application/json',
                headers: { 'RequestVerificationToken': token() },
                data: JSON.stringify({
                    minhChungId: d.minhChungDangXemId,
                    soTienThucNhan: Number($form.find('[name="soTien"]').val()),
                    maGiaoDich: $form.find('[name="ma"]').val(),
                    ngayGiaoDich: $form.find('[name="ngay"]').val(),
                    ghiChu: $form.find('[name="ghiChu"]').val()
                })
            }).done(function (res) {
                bootstrap.Modal.getOrCreateInstance(document.getElementById('dcModal')).hide();
                Swal.fire('Đã xử lý', res.message, 'success');
                table.ajax.reload(null, false);
            }).fail(function (xhr) {
                Swal.fire('Không xác nhận được', errorMessage(xhr, 'Có lỗi xảy ra.'), 'error');
            }).always(function () {
                $btn.prop('disabled', false);
            });
        });

        $('#dcTuChoi').on('click', function () {
            Swal.fire({
                title: 'Từ chối minh chứng?',
                html: 'Lượt sẽ quay về chờ thanh toán để khách nộp lại. "Số tiền không khớp" không phải lý do từ chối nếu tiền đã vào tài khoản.',
                input: 'textarea',
                inputPlaceholder: 'Ví dụ: Không thấy giao dịch trên sao kê',
                inputAttributes: { maxlength: 500 },
                inputValidator: function (v) { return !v || !v.trim() ? 'Vui lòng nhập lý do.' : undefined; },
                showCancelButton: true,
                confirmButtonText: 'Từ chối',
                cancelButtonText: 'Quay lại',
                confirmButtonColor: '#d63939'
            }).then(function (r) {
                if (!r.isConfirmed) return;
                $.ajax({
                    url: '/DoiChieuThanhToan/TuChoi',
                    type: 'POST',
                    contentType: 'application/json',
                    headers: { 'RequestVerificationToken': token() },
                    data: JSON.stringify({ minhChungId: d.minhChungDangXemId, lyDo: r.value })
                }).done(function (res) {
                    bootstrap.Modal.getOrCreateInstance(document.getElementById('dcModal')).hide();
                    Swal.fire('Đã từ chối', res.message, 'success');
                    table.ajax.reload(null, false);
                }).fail(function (xhr) {
                    Swal.fire('Lỗi', errorMessage(xhr, 'Không từ chối được.'), 'error');
                });
            });
        });
    }

    $(function () {
        initTable();
        $('#dcTraCuuForm').on('submit', function (e) {
            e.preventDefault();
            traCuu($.trim($('#dcMaTraCuu').val()));
        });
        if (cfg.maTraCuu) {
            traCuu(cfg.maTraCuu);
        }
    });
})(window, jQuery);
