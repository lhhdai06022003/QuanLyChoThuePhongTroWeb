(function (window, $) {
    'use strict';

    var cfg = window.lichSuThanhToanConfig || {};
    var tienTe = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' });

    function formatVND(n) {
        if (n === null || n === undefined) return '0 ₫';
        return tienTe.format(n);
    }

    function esc(unsafe) {
        return (unsafe || '').toString()
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    $(function () {
        var isAdmin = !!cfg.laAdmin;
        var table = null;
        var flatpickrInstance = null;

        // ----- Bộ lọc kỳ hóa đơn (chỉ Admin có ô này) -----
        function layKy() {
            var v = $('#filterKy').val();
            var m = /^(\d{4})-(\d{2})$/.exec(v || '');
            if (!m) return null;
            return { kyThang: parseInt(m[2], 10), kyNam: parseInt(m[1], 10) };
        }

        // Đang lọc theo kỳ thì máy chủ bỏ qua khoảng ngày: khóa ô ngày và ghi chú rõ.
        function capNhatTrangThaiKy() {
            var coKy = layKy() !== null;
            $('#khoangNgay button, #khoangNgay input').prop('disabled', coKy);
            $('#ghiChuKy').toggleClass('d-none', !coKy);
        }

        function thamSoLoc() {
            var data = {
                chiNhanhId: $('#filterChiNhanh').val(),
                dateRange: $('#filterDateRange').val(),
                nguoiThu: $('#filterNguoiThu').val()
            };
            var ky = layKy();
            if (ky) {
                data.kyThang = ky.kyThang;
                data.kyNam = ky.kyNam;
            }
            return data;
        }

        // ----- Khởi tạo bộ lọc từ URL (qua cấu hình) -----
        $('#filterChiNhanh').val(String(cfg.chiNhanhId || 0));
        $('#filterPhuongThuc').val(cfg.phuongThuc || '');
        $('#filterNguoiThu').val(cfg.nguoiThu || '');

        // Khởi tạo Flatpickr cho khoảng ngày thanh toán
        flatpickrInstance = flatpickr('#filterDateRange', {
            mode: 'range',
            dateFormat: 'Y-m-d',
            altInput: true,
            altFormat: 'd/m/Y',
            locale: 'vn',
            onChange: function (selectedDates) {
                $('#khoangNgay .btn-group .btn').removeClass('active');
                if (selectedDates.length === 2 || selectedDates.length === 0) {
                    table.ajax.reload();
                    loadStats();
                }
            },
            onReady: function (selectedDates, dateStr, instance) {
                var clearBtn = $('<div class="flatpickr-clear-btn text-end p-2 border-top" style="cursor: pointer; color: var(--tblr-danger); font-weight: bold;"><i class="ti ti-x me-1"></i>Xóa chọn</div>');
                clearBtn.on('click', function () {
                    instance.clear();
                    instance.close();
                    $('#btnDateAll').addClass('active');
                });
                $(instance.calendarContainer).append(clearBtn);
            }
        });

        // Logic các nút lọc nhanh
        function setActiveDateBtn(btnId) {
            $('#khoangNgay .btn-group .btn').removeClass('active');
            if (btnId) $('#' + btnId).addClass('active');
        }

        // Khoảng ngày mở từ link (Bảng điều khiển...): sáng nút nhanh trùng khoảng đó, không trùng thì không nút nào sáng.
        function nutTrungKhoang(tu, den) {
            var homNay = flatpickr.formatDate(new Date(), 'Y-m-d');
            var d = new Date();
            var dauThang = flatpickr.formatDate(new Date(d.getFullYear(), d.getMonth(), 1), 'Y-m-d');
            var cuoiThang = flatpickr.formatDate(new Date(d.getFullYear(), d.getMonth() + 1, 0), 'Y-m-d');
            if (tu === homNay && den === homNay) return 'btnDateToday';
            if (tu === dauThang && den === cuoiThang) return 'btnDateMonth';
            return null;
        }

        if (cfg.tuNgay && cfg.denNgay) {
            flatpickrInstance.setDate([cfg.tuNgay, cfg.denNgay], false);
            setActiveDateBtn(nutTrungKhoang(cfg.tuNgay, cfg.denNgay));
        }

        $('#btnDateAll').click(function () {
            setActiveDateBtn('btnDateAll');
            flatpickrInstance.clear();
        });

        $('#btnDateToday').click(function () {
            setActiveDateBtn('btnDateToday');
            var today = new Date();
            flatpickrInstance.setDate([today, today]);
            table.ajax.reload();
            loadStats();
        });

        $('#btnDateWeek').click(function () {
            setActiveDateBtn('btnDateWeek');
            var curr = new Date();
            var first = curr.getDate() - curr.getDay() + (curr.getDay() === 0 ? -6 : 1); // Monday
            var last = first + 6; // Sunday
            var firstDay = new Date(curr.setDate(first));
            var lastDay = new Date(curr.setDate(last));
            flatpickrInstance.setDate([firstDay, lastDay]);
            table.ajax.reload();
            loadStats();
        });

        $('#btnDateMonth').click(function () {
            setActiveDateBtn('btnDateMonth');
            var date = new Date();
            var firstDay = new Date(date.getFullYear(), date.getMonth(), 1);
            var lastDay = new Date(date.getFullYear(), date.getMonth() + 1, 0);
            flatpickrInstance.setDate([firstDay, lastDay]);
            table.ajax.reload();
            loadStats();
        });

        // Tải thống kê
        function loadStats() {
            $.ajax({
                url: cfg.urlGetStats,
                type: 'GET',
                data: thamSoLoc()
            }).done(function (res) {
                if (res.locTheoKy) {
                    $('#statTongTitle').text('Đã thu kỳ T' + res.kyThang + '/' + res.kyNam);
                    $('#statTongChuThich').text('Theo kỳ hóa đơn, khớp Báo cáo');
                } else {
                    $('#statTongTitle').text('Tiền đã nhận');
                    $('#statTongChuThich').text('Theo ngày nhận tiền');
                }
                $('#statTongDoanhThu').text(formatVND(res.tongDoanhThu));
                $('#statTongSoGiaoDich').text(res.tongSoGiaoDich);
                $('#statDoanhThuTienMat').text(formatVND(res.doanhThuTienMat));
                $('#statDoanhThuChuyenKhoan').text(formatVND(res.doanhThuChuyenKhoan));
                $('#statDoanhThuSePay').text(formatVND(res.doanhThuSePay));
                $('#statDoanhThuVnPay').text(formatVND(res.doanhThuVnPay));
                $('#statDoanhThuCanTruCoc').text(formatVND(res.doanhThuCanTruCoc));
            }).fail(function (xhr) {
                console.error('Không tải được thống kê giao dịch:', xhr);
                window.showError('Không tải được thống kê giao dịch. Vui lòng thử lại.');
            });
        }

        // Khởi tạo DataTable
        table = $('#lichSuTable').DataTable({
            language: { url: 'https://cdn.datatables.net/plug-ins/1.13.6/i18n/vi.json' },
            autoWidth: false,
            serverSide: true,
            processing: true,
            ajax: {
                url: cfg.urlGetList,
                type: 'POST',
                data: function (d) {
                    $.extend(d, thamSoLoc());
                    d.phuongThuc = $('#filterPhuongThuc').val();
                    d.coTienThua = $('#filterCoTienThua').is(':checked');
                }
            },
            columns: [
                {
                    data: null,
                    className: 'text-center align-middle',
                    orderable: false,
                    render: function (d, t, r, m) { return m.row + m.settings._iDisplayStart + 1; }
                },
                {
                    // Hai mã dài xếp hai dòng trong một ô, không cho xuống dòng giữa mã.
                    data: null, className: 'align-middle text-nowrap',
                    render: function (d, t, r) { return '<div class="fw-bold">' + esc(r.maGiaoDich) + '</div><div class="small text-info">' + esc(r.maHoaDon) + '</div>'; }
                },
                {
                    data: null, className: 'align-middle text-nowrap',
                    render: function (d, t, r) { return '<div class="fw-bold text-primary">' + esc(r.tenPhong) + '</div><div class="small text-muted">' + esc(r.tenChiNhanh) + '</div>'; }
                },
                { data: 'tenNguoiThue', className: 'align-middle text-nowrap', render: $.fn.dataTable.render.text() },
                {
                    data: 'soTienThanhToan',
                    className: 'text-end align-middle fw-bold text-success num-tabular',
                    render: function (d, t, row) {
                        // Thẻ [CHENH-LECH], [TIEN-THUA] trong ghi chú hiển thị thành nhãn dễ đọc
                        var tags = (row.nhanGhiChu || []).map(function (n) { return '<div><span class="badge bg-orange-lt fw-normal">' + esc(n) + '</span></div>'; }).join('');
                        return formatVND(d) + tags;
                    }
                },
                {
                    data: 'phuongThucThanhToan',
                    className: 'text-center align-middle',
                    orderable: false,
                    render: function (d) {
                        if (d === 1) {
                            return '<span class="badge bg-success-lt px-2 py-1"><i class="ti ti-building-bank me-1"></i> Chuyển khoản</span>';
                        }
                        if (d === 2) {
                            return '<span class="badge bg-purple-lt px-2 py-1"><i class="ti ti-arrows-exchange me-1"></i> Cấn trừ tiền cọc</span>';
                        }
                        return '<span class="badge bg-blue-lt px-2 py-1"><i class="ti ti-cash me-1"></i> Tiền mặt</span>';
                    }
                },
                { data: 'ngayThanhToan', className: 'align-middle text-center', render: $.fn.dataTable.render.text() },
                { data: 'nguoiXacNhan', className: 'align-middle', render: $.fn.dataTable.render.text() },
                {
                    data: null,
                    orderable: false,
                    className: 'text-center align-middle',
                    render: function (data, t, row) {
                        var actionHtml = '<div class="btn-list flex-nowrap justify-content-center">'
                            + '<button class="btn btn-icon btn-ghost-info btn-sm btn-detail" data-id="' + esc(row.hoaDonId) + '" title="Xem chi tiết hóa đơn" aria-label="Xem chi tiết hóa đơn">'
                            + '<i class="ti ti-eye"></i></button>';
                        if (isAdmin && row.phuongThucThanhToan !== 2) {
                            actionHtml += '<button class="btn btn-icon btn-ghost-danger btn-sm btn-void" data-id="' + esc(row.lichSuThanhToanId) + '" data-code="' + esc(row.maGiaoDich) + '" title="Hủy ghi nhận thanh toán" aria-label="Hủy ghi nhận thanh toán">'
                                + '<i class="ti ti-trash"></i></button>';
                        }
                        actionHtml += '</div>';
                        return actionHtml;
                    }
                }
            ]
        });

        capNhatTrangThaiKy();
        loadStats();

        // Tự động lọc khi thay đổi input/select
        $('#filterChiNhanh, #filterPhuongThuc, #filterNguoiThu, #filterCoTienThua').change(function () {
            table.ajax.reload();
            loadStats();
        });

        $('#filterKy').on('change', function () {
            capNhatTrangThaiKy();
            table.ajax.reload();
            loadStats();
        });

        $('#btnXoaKy').on('click', function () {
            if (!layKy()) return;
            $('#filterKy').val('');
            capNhatTrangThaiKy();
            table.ajax.reload();
            loadStats();
        });

        // Chi tiết hóa đơn
        $('#lichSuTable').on('click', '.btn-detail', function () {
            var id = $(this).data('id');
            $.get(cfg.urlHoaDonChiTiet + id, function (res) {
                var html = '<div class="datagrid mb-3">'
                    + '<div class="datagrid-item"><div class="datagrid-title">Mã HĐ</div><div class="datagrid-content fw-medium text-primary">' + esc(res.maHoaDon) + '</div></div>'
                    + '<div class="datagrid-item"><div class="datagrid-title">Ngày tạo</div><div class="datagrid-content fw-medium hd-num text-secondary">' + esc(res.ngayTao) + '</div></div>'
                    + '<div class="datagrid-item"><div class="datagrid-title">Phòng</div><div class="datagrid-content fw-medium text-primary">' + esc(res.tenPhong) + '</div></div>'
                    + '<div class="datagrid-item"><div class="datagrid-title">Kỳ</div><div class="datagrid-content fw-medium hd-num">Tháng ' + esc(res.thang) + '/' + esc(res.nam) + '</div></div>'
                    + '<div class="datagrid-item"><div class="datagrid-title">Người thuê</div><div class="datagrid-content fw-medium ">' + esc(res.tenNguoiThue) + '</div></div>'
                    + '<div class="datagrid-item"><div class="datagrid-title">Trạng thái</div><div class="datagrid-content fw-medium "><span class="badge ' + (res.trangThaiHoaDon === 'Đã thanh toán' ? 'bg-success-lt' : 'bg-warning-lt') + ' px-2 py-1">' + esc(res.trangThaiHoaDon) + '</span></div></div>'
                    + '</div>'
                    + '<h4 class="fw-bold border-bottom pb-2 mt-4"><i class="ti ti-list me-1"></i> Chi tiết các khoản phí</h4>'
                    + '<div class="table-responsive mb-3">'
                    + '<table class="table card-table table-vcenter table-striped table-hover w-100 table-sm">'
                    + '<thead><tr>'
                    + '<th class="text-center hd-col-stt" width="6%">STT</th>'
                    + '<th>Dịch vụ</th>'
                    + '<th class="text-end" width="20%">Đơn giá</th>'
                    + '<th class="text-center" width="10%">SL</th>'
                    + '<th class="text-center hd-col-unit" width="12%">Đơn vị</th>'
                    + '<th class="text-end" width="22%">Thành tiền</th>'
                    + '</tr></thead><tbody>';

                res.chiTietHoaDons.forEach(function (ct, idx) {
                    var donViText = ct.donVi ? ct.donVi : '-';
                    html += '<tr>'
                        + '<td class="text-center align-middle hd-col-stt">' + (idx + 1) + '</td>'
                        + '<td class="align-middle">' + esc(ct.tenDichVu) + '</td>'
                        + '<td class="text-end align-middle hd-num">' + formatVND(ct.donGia) + '</td>'
                        + '<td class="text-center align-middle hd-num">' + esc(ct.soLuong) + '<span class="hd-unit-inline text-secondary"> ' + esc(donViText) + '</span></td>'
                        + '<td class="text-center align-middle hd-col-unit">' + esc(donViText) + '</td>'
                        + '<td class="text-end align-middle fw-medium hd-num">' + formatVND(ct.tongTien) + '</td>'
                        + '</tr>';
                });

                html += '</tbody></table></div>'
                    + '<div class="hd-total-row mb-3"><span class="fw-bold">Tổng cộng</span><span class="fw-bold text-danger fs-3 hd-num">' + formatVND(res.tongTien) + '</span></div>';

                // Lịch sử thanh toán
                if (res.lichSuThanhToans && res.lichSuThanhToans.length > 0) {
                    html += '<h4 class="fw-bold border-bottom pb-2 mt-4"><i class="ti ti-history me-1"></i> Lịch sử thanh toán</h4>'
                        + '<div class="table-responsive mb-3">'
                        + '<table class="table card-table table-vcenter table-striped table-hover w-100 table-sm">'
                        + '<thead><tr><th>Mã GD</th><th class="text-end">Số tiền</th><th>Phương thức</th><th>Ngày</th><th>Người xác nhận</th><th class="hd-note">Ghi chú</th></tr></thead><tbody>';
                    res.lichSuThanhToans.forEach(function (ls) {
                        html += '<tr><td>' + esc(ls.maGiaoDich) + '</td><td class="text-end fw-medium text-success hd-num">' + formatVND(ls.soTienThanhToan) + '</td><td>' + esc(ls.phuongThucThanhToan) + '</td><td class="hd-num">' + esc(ls.ngayThanhToan) + '</td><td>' + (esc(ls.nguoiXacNhan) || '—') + '</td><td class="hd-note">' + esc(ls.ghiChu || '') + '</td></tr>';
                    });
                    html += '</tbody></table></div>';
                }

                // QR theo số còn lại (chỉ hiển thị dự phòng)
                if (!res.daHuy && res.conLai > 0) {
                    html += '<div class="row mt-4 pt-3 border-top justify-content-center">'
                        + '<div class="col-12 col-md-9 col-lg-8 text-center">'
                        + '<div class="qr-payment-box p-3 p-md-4">'
                        + '<h4 class="qr-title text-primary fw-medium mb-3"><i class="ti ti-qrcode me-1"></i> Quét mã QR để chuyển khoản nhanh</h4>'
                        + '<div class="mb-3"><img src="' + cfg.urlVietQr + '?hoaDonId=' + encodeURIComponent(res.hoaDonId) + '" alt="VietQR" /></div>'
                        + '<p class="small text-secondary mb-3">Sử dụng ứng dụng ngân hàng hoặc ví điện tử để quét mã QR</p>'
                        + '<div class="qr-details-list p-3 rounded text-start">'
                        + '<div class="qr-detail-item"><span>Ngân hàng:</span><strong>' + esc(cfg.bankId) + '</strong></div>'
                        + '<div class="qr-detail-item"><span>Số tài khoản:</span><strong>' + esc(cfg.accountNumber) + '</strong></div>'
                        + '<div class="qr-detail-item"><span>Chủ tài khoản:</span><strong>' + esc(cfg.accountName) + '</strong></div>'
                        + '<div class="qr-detail-item"><span>Số tiền:</span><strong class="text-primary hd-num fs-3">' + formatVND(res.conLai) + '</strong></div>'
                        + '<div class="qr-detail-item"><span>Nội dung CK:</span><code class="text-primary fw-bold">TTPTHD' + esc(res.hoaDonId) + '</code></div>'
                        + '</div></div></div></div>';
                }

                $('#chiTietBody').html(html);
                bootstrap.Modal.getOrCreateInstance(document.getElementById('chiTietModal')).show();
            }).fail(function () {
                window.showError('Không tải được chi tiết hóa đơn. Vui lòng thử lại.');
            });
        });

        // Hủy ghi nhận thanh toán (chỉ Admin, bắt buộc lý do; spec thanh toán §20)
        $('#lichSuTable').on('click', '.btn-void', function () {
            var id = $(this).data('id');
            var code = $(this).data('code');

            Swal.fire({
                title: 'Hủy ghi nhận thanh toán?',
                html: 'Khoản <strong>' + esc(code) + '</strong> sẽ không còn được tính vào công nợ; trạng thái hóa đơn được tính lại từ các khoản còn lại.<br>' +
                      '<span class="text-danger">Thao tác này chỉ thay đổi ghi nhận trong hệ thống, không hoàn tiền qua ngân hàng.</span>',
                icon: 'warning',
                input: 'textarea',
                inputPlaceholder: 'Lý do hủy ghi nhận (bắt buộc, tối đa 500 ký tự)',
                inputAttributes: { maxlength: 500 },
                inputValidator: function (value) { return !value || !value.trim() ? 'Vui lòng nhập lý do.' : undefined; },
                showCancelButton: true,
                confirmButtonText: 'Đồng ý hủy',
                cancelButtonText: 'Không, giữ lại',
                customClass: {
                    confirmButton: 'btn btn-danger me-2',
                    cancelButton: 'btn btn-secondary'
                },
                buttonsStyling: false
            }).then(function (result) {
                if (result.isConfirmed) {
                    $.ajax({
                        url: cfg.urlHuyThanhToan + id,
                        type: 'DELETE',
                        contentType: 'application/json',
                        data: JSON.stringify({ lyDo: result.value }),
                        success: function (res) {
                            Swal.fire('Đã hủy ghi nhận', res.message, 'success');
                            table.ajax.reload(null, false);
                            loadStats();
                        },
                        error: function (xhr) {
                            Swal.fire('Lỗi!', (xhr.responseJSON && xhr.responseJSON.message) || 'Có lỗi xảy ra khi thực hiện yêu cầu.', 'error');
                        }
                    });
                }
            });
        });
    });
})(window, jQuery);
