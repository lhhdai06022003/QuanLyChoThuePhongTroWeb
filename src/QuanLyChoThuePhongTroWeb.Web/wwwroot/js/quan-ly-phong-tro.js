        // ========== Shared State ==========
        // LA_ADMIN, bankId, accountNumber, accountName: view khai báo ngay trước khi nạp file này.
        // Lỗi 403/404 (không có quyền tại chi nhánh, không tìm thấy) trả kèm message trong JSON.
        function serverMessage(xhr, fallback) {
            return (xhr && xhr.responseJSON && xhr.responseJSON.message) || fallback;
        }
        var table;
        let currentPhongTroId = 0;
        let allRooms = [];
        let currentFilter = 'all';

        function formatVND(n) {
            return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n || 0);
        }

        // ========== VIEW TOGGLE ==========
        $('#btnGridView').click(function () {
            $(this).addClass('active');
            $('#btnTableView').removeClass('active');
            $('#gridViewSection').show();
            $('#tableViewSection').hide();
            loadGridData();
        });
        $('#btnTableView').click(function () {
            $(this).addClass('active');
            $('#btnGridView').removeClass('active');
            $('#tableViewSection').show();
            $('#gridViewSection').hide();
            if (table) table.ajax.reload();
        });

        // ========== GRID VIEW LOGIC ==========
        function loadGridData() {
            let chiNhanhId = $('#filterChiNhanhGrid').val();
            $.get(`/PhongTros/GetSoDoPhong?chiNhanhId=${chiNhanhId}`, function (data) {
                allRooms = data;
                updateStats();
                renderGrid();
            });
        }

        function updateStats() {
            $('#statTrong').text(allRooms.filter(r => r.trangThai === 0).length);
            $('#statDaThue').text(allRooms.filter(r => r.trangThai === 1).length);
            $('#statNoTien').text(allRooms.filter(r => r.coNoTien).length);
            $('#statSapHetHan').text(allRooms.filter(r => r.sapHetHan).length);
        }

        function renderGrid() {
            let filtered = allRooms;
            let search = ($('#searchRoom').val() || '').toLowerCase();

            if (currentFilter === 'trong') filtered = filtered.filter(r => r.trangThai === 0);
            else if (currentFilter === 'dathue') filtered = filtered.filter(r => r.trangThai === 1);
            else if (currentFilter === 'baotri') filtered = filtered.filter(r => r.trangThai === 2);
            else if (currentFilter === 'notien') filtered = filtered.filter(r => r.coNoTien);
            else if (currentFilter === 'saphethan') filtered = filtered.filter(r => r.sapHetHan);

            if (search) {
                filtered = filtered.filter(r =>
                    r.soPhong.toLowerCase().includes(search) ||
                    (r.tenNguoiThue || '').toLowerCase().includes(search)
                );
            }

            if (filtered.length === 0) {
                $('#roomGrid').html('');
                $('#emptyState').show();
                return;
            }
            $('#emptyState').hide();

            // Group by floor
            let floors = {};
            filtered.forEach(r => {
                let key = r.tangLau || 0;
                if (!floors[key]) floors[key] = [];
                floors[key].push(r);
            });

            let html = '';
            let sortedFloors = Object.keys(floors).sort((a, b) => a - b);

            sortedFloors.forEach(floor => {
                html += `<div class="floor-group">
                    <div class="floor-label"><i class="ti ti-stack me-1"></i>Tầng ${floor}</div>
                    <div class="room-grid">`;

                floors[floor].forEach(r => {
                    let lopTrangThai = r.trangThai === 0 ? 'bg-success-lt' : r.trangThai === 1 ? 'room-nhan-thue' : 'bg-warning-lt';
                    let tenTrangThai = r.trangThai === 0 ? 'Trống' : r.trangThai === 1 ? 'Đã thuê' : 'Bảo trì';

                    let conNgay = '';
                    if (r.trangThai === 1 && r.soNgayConLai !== null) {
                        let textNgay = ` · Còn ${r.soNgayConLai} ngày`;
                        if (r.sapHetHan) {
                            conNgay = `<span class="room-canh-bao">${textNgay}</span>`;
                        } else {
                            conNgay = textNgay;
                        }
                    }

                    let no = '';
                    if (r.coNoTien) {
                        no = `<div class="room-no num-tabular">Nợ ${formatVND(r.soTienNo)}</div>`;
                    }

                    let tenant = r.tenNguoiThue ? `<div class="room-tenant"><i class="ti ti-user me-1"></i>${escapeHtml(r.tenNguoiThue)}</div>` : '';

                    // Kebab menu items based on status
                    let menuItems = `
                        <button class="kebab-item" onclick="openEditFromGrid(${r.phongTroId})"><i class="ti ti-edit me-2"></i>Sửa thông tin</button>
                        <button class="kebab-item" onclick="showDetail(${r.phongTroId})"><i class="ti ti-info-circle me-2"></i>Xem chi tiết</button>`;
                    if (r.trangThai === 1) {
                        menuItems += `<div class="kebab-divider"></div>`;
                        menuItems += `<button class="kebab-item" onclick="openDichVuPhong(${r.phongTroId})"><i class="ti ti-apps me-2"></i>Quản lý dịch vụ</button>`;
                        if (r.coNoTien) menuItems += `<button class="kebab-item" onclick="openQuickPayment(${r.phongTroId})"><i class="ti ti-report-money me-2"></i>Thu tiền</button>`;
                        menuItems += `<button class="kebab-item" onclick="openQuickContract(${r.phongTroId})"><i class="ti ti-file-text me-2"></i>Xem hợp đồng</button>`;
                    }
                    menuItems += `<div class="kebab-divider"></div>
                        ${LA_ADMIN ? `<button class="kebab-item text-danger" onclick="deleteRoom(${r.phongTroId})"><i class="ti ti-trash me-2"></i>Xóa phòng</button>` : ''}`;

                    html += `
                        <div class="room-card" data-id="${r.phongTroId}">
                            <div class="kebab-menu">
                                <button class="kebab-btn" aria-label="Thao tác" onclick="event.stopPropagation(); toggleKebab(this)"><i class="ti ti-dots-vertical"></i></button>
                                <div class="kebab-dropdown">${menuItems}</div>
                            </div>
                            <div class="room-body" onclick="showDetail(${r.phongTroId})">
                                <div class="room-header">
                                    <span class="room-number">${escapeHtml(r.soPhong)}</span>
                                    <span class="badge ${lopTrangThai}">${tenTrangThai}</span>
                                </div>
                                <div class="room-price num-tabular">${formatVND(r.giaThue)}/tháng</div>
                                ${tenant}
                                <div class="room-meta">${r.dienTich} m²${conNgay}</div>
                                ${no}
                            </div>
                        </div>`;
                });

                html += `</div></div>`;
            });

            $('#roomGrid').html(html);
        }

        function toggleKebab(btn) {
            let dropdown = $(btn).next('.kebab-dropdown');
            let card = $(btn).closest('.room-card');
            // Close all others first
            $('.kebab-dropdown.show').not(dropdown).removeClass('show');
            $('.room-card.kebab-open').not(card).removeClass('kebab-open');

            let isOpening = !dropdown.hasClass('show');
            if (isOpening) {
                dropdown.addClass('show');
                card.addClass('kebab-open');

                // Đảm bảo menu nằm trọn trong màn hình (không lệch trái hoặc phải)
                dropdown.css({ 'left': '', 'right': '0' });
                let rect = dropdown[0].getBoundingClientRect();
                if (rect.left < 8) {
                    let offset = 8 - rect.left;
                    dropdown.css({ 'right': `-${offset}px` });
                } else if (rect.right > window.innerWidth - 8) {
                    let offset = rect.right - (window.innerWidth - 8);
                    dropdown.css({ 'right': `${offset}px` });
                }
            } else {
                dropdown.removeClass('show');
                card.removeClass('kebab-open');
            }
        }

        // Close kebab when clicking outside
        $(document).click(function (e) {
            if (!$(e.target).closest('.kebab-menu').length) {
                $('.kebab-dropdown.show').removeClass('show');
                $('.room-card.kebab-open').removeClass('kebab-open');
            }
        });

        function openEditFromGrid(id) {
            $('.kebab-dropdown.show').removeClass('show');
            currentPhongTroId = id;
            $.get('/PhongTros/GetPhongTro?id=' + id, function (res) {
                if (res.success && res.data) {
                    let d = res.data;
                    $('#ChiNhanhId').val(d.chiNhanhId);
                    $('#SoPhong').val(d.soPhong);
                    $('#TangLau').val(d.tangLau);
                    $('#GiaThue').val(d.giaThue);
                    $('#DienTich').val(d.dienTich);
                    $('#SoNguoiToiDa').val(d.soNguoiToiDa);
                    $('#TrangThai').val(d.trangThai);
                    $('#MoTa').val(d.moTa);
                    $('#modalTitle').text('Chỉnh sửa: Phòng ' + (d.soPhong || ""));
                    $('#btnSaveData').html('<i class="ti ti-device-floppy me-1"></i> Lưu thay đổi');
                    $('#phongTroModal').modal('show');
                }
            }).fail(function (xhr) {
                showError(serverMessage(xhr, 'Không tải được thông tin phòng.'));
            });
        }

        function deleteRoom(id) {
            $('.kebab-dropdown.show').removeClass('show');
            Swal.fire({
                title: 'Xác nhận xóa phòng?',
                text: 'Hành động này không thể hoàn tác. Bạn có chắc chắn muốn xóa phòng này?',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Đồng ý xóa',
                cancelButtonText: 'Hủy bỏ'
            }).then((result) => {
                if (result.isConfirmed) {
                    $.post('/PhongTros/XoaPhongTro', { id: id }, function (res) {
                        if (res.success) {
                            Swal.fire('Thành công!', res.message, 'success');
                            loadGridData();
                            if (table) table.ajax.reload(null, false);
                        } else {
                            Swal.fire('Lỗi!', res.message, 'error');
                        }
                    }).fail(function (xhr) {
                        Swal.fire('Lỗi!', serverMessage(xhr, 'Không thể kết nối đến máy chủ.'), 'error');
                    });
                }
            });
        }

        let currentQuickPaymentRoomId = 0;

        function openQuickContract(phongTroId) {
            $('.kebab-dropdown.show').removeClass('show');
            let room = allRooms.find(r => r.phongTroId === phongTroId);
            let roomName = room ? room.soPhong : phongTroId;
            $('#quickContractRoom').text(roomName);

            $('#qcMaHD').text('Đang tải...');
            $('#qcNgayBatDau').text('Đang tải...');
            $('#qcNgayKetThuc').text('Đang tải...');
            $('#qcTienCoc').text('Đang tải...');
            $('#qcTienThue').text('Đang tải...');
            $('#qcTenDaiDien').text('Đang tải...');
            $('#qcSDTDaiDien').text('Đang tải...');
            $('#qcCCCDDaiDien').text('Đang tải...');
            $('#qcMembersBody').html('<tr><td colspan="5" class="text-center"><i class="ti ti-loader fa-spin me-1"></i> Đang tải thành viên...</td></tr>');
            $('#qcServicesBody').html('<tr><td colspan="5" class="text-center"><i class="ti ti-loader fa-spin me-1"></i> Đang tải dịch vụ...</td></tr>');
            $('#qcMemberCount').text(0);
            $('#qcServiceCount').text(0);

            $('#quickContractModal').modal('show');

            $.get(`/PhongTros/GetQuickContract?phongTroId=${phongTroId}`, function (res) {
                $('#qcMaHD').text(res.maHopDong);
                $('#qcNgayBatDau').text(res.thoiDiemBatDau);
                $('#qcNgayKetThuc').text(res.thoiDiemKetThuc);
                $('#qcTienCoc').text(formatVND(res.tienCocPhong));
                $('#qcTienThue').text(formatVND(res.tienThuePhong));
                $('#qcTenDaiDien').text(res.tenNguoiDaiDien);
                $('#qcSDTDaiDien').text(res.soDienThoaiDaiDien || '-');
                $('#qcCCCDDaiDien').text(res.cccdDaiDien || '-');

                let mHtml = '';
                if (res.members && res.members.length > 0) {
                    res.members.forEach((m, idx) => {
                        mHtml += `
                            <tr>
                                <td class="text-center">${idx + 1}</td>
                                <td class="fw-bold">${escapeHtml(m.hoVaTen)}</td>
                                <td class="text-center">${escapeHtml(m.soDienThoai) || '-'}</td>
                                <td class="text-center">${escapeHtml(m.cccd) || '-'}</td>
                                <td class="text-center">${m.ngayVao}</td>
                            </tr>`;
                    });
                    $('#qcMemberCount').text(res.members.length);
                } else {
                    mHtml = '<tr><td colspan="5" class="text-center text-muted">Chưa có thành viên nào khác ở ghép.</td></tr>';
                }
                $('#qcMembersBody').html(mHtml);

                let sHtml = '';
                if (res.services && res.services.length > 0) {
                    res.services.forEach((s, idx) => {
                        sHtml += `
                            <tr>
                                <td class="text-center">${idx + 1}</td>
                                <td class="fw-bold">${escapeHtml(s.tenDichVu)}</td>
                                <td class="text-end num-tabular">${formatVND(s.donGia)}</td>
                                <td class="text-center">${s.soLuong}</td>
                                <td class="text-center">${escapeHtml(s.donVi) || '-'}</td>
                            </tr>`;
                    });
                    $('#qcServiceCount').text(res.services.length);
                } else {
                    sHtml = '<tr><td colspan="5" class="text-center text-muted">Không có dịch vụ đăng ký nào.</td></tr>';
                }
                $('#qcServicesBody').html(sHtml);
            }).fail(function (xhr) {
                $('#quickContractModal').modal('hide');
                Swal.fire({
                    icon: 'error',
                    title: 'Lỗi',
                    text: xhr.responseJSON?.message || 'Không thể tải thông tin hợp đồng.'
                });
            });
        }

        function openQuickPayment(phongTroId) {
            $('.kebab-dropdown.show').removeClass('show');
            currentQuickPaymentRoomId = phongTroId;
            let room = allRooms.find(r => r.phongTroId === phongTroId);
            let roomName = room ? room.soPhong : phongTroId;
            $('#quickPaymentRoom').text(roomName);

            $('#qpInvoiceId').val('');
            $('#qpInvoicePeriod').text('');
            $('#qpInvoiceCode').text('Đang tải...');
            $('#qpTenantName').text('Đang tải...');
            $('#qpTotalAmount').text('0 ₫');
            $('#qpDetailsBody').html('<tr><td colspan="6" class="text-center"><i class="ti ti-loader fa-spin"></i> Đang tải hóa đơn...</td></tr>');
            $('#qpQRImage').attr('src', '');
            $('#qpCKContent').text('');

            $('#qpBankId').text(bankId || '-');
            $('#qpAccountNumber').text(accountNumber || '-');
            $('#qpAccountName').text(accountName || '-');

            $('#quickPaymentModal').modal('show');

            $.get(`/PhongTros/GetUnpaidInvoice?phongTroId=${phongTroId}`, function (res) {
                $('#qpInvoiceId').val(res.hoaDonId);
                $('#qpInvoicePeriod').text(`Tháng ${res.thang}/${res.nam}`);
                $('#qpInvoiceCode').text(res.maHoaDon);
                $('#qpTenantName').text(res.tenNguoiThue);
                $('#qpTotalAmount').html(formatVND(res.tongTien) +
                    (res.conLai < res.tongTien ? `<div class="small text-secondary fw-normal">Còn lại ${formatVND(res.conLai)}</div>` : ''));

                $('#qpQRImage').attr('src', `/HoaDon/GetVietQR?hoaDonId=${res.hoaDonId}`);
                $('#qpCKContent').text(`TTPTHD${res.hoaDonId}`);

                let dHtml = '';
                if (res.chiTiets && res.chiTiets.length > 0) {
                    res.chiTiets.forEach((d, idx) => {
                        dHtml += `
                            <tr>
                                <td class="text-center">${idx + 1}</td>
                                <td>${escapeHtml(d.tenDichVu)}</td>
                                <td class="text-end num-tabular">${formatVND(d.donGia)}</td>
                                <td class="text-center">${d.soLuong}</td>
                                <td class="text-center">${escapeHtml(d.donVi) || '-'}</td>
                                <td class="text-end fw-bold num-tabular">${formatVND(d.tongTien)}</td>
                            </tr>`;
                    });
                } else {
                    dHtml = '<tr><td colspan="6" class="text-center text-muted">Không có chi tiết dịch vụ nào.</td></tr>';
                }
                $('#qpDetailsBody').html(dHtml);
            }).fail(function (xhr) {
                $('#quickPaymentModal').modal('hide');
                Swal.fire({
                    icon: 'error',
                    title: 'Lỗi',
                    text: xhr.responseJSON?.message || 'Không tìm thấy hóa đơn chưa thanh toán cho phòng này.'
                });
            });
        }

        // Thu tiền thủ công qua modal dùng chung (ghi số thực nhận, spec thanh toán §18)
        $(document).on('click', '#btnConfirmQuickPayment', function () {
            let hoaDonId = parseInt($('#qpInvoiceId').val());
            if (!hoaDonId) return;

            $('#quickPaymentModal').modal('hide');
            HdThanhToan.openThuTien({
                hoaDonId: hoaDonId,
                maHoaDon: $('#qpInvoiceCode').text(),
                onDone: function (data) {
                    if (!data || data.conLai > 0) return;

                    // Hóa đơn đã trả đủ: bỏ huy hiệu nợ của phòng
                    let card = $(`.room-card[data-id="${currentQuickPaymentRoomId}"]`);
                    card.find('.badge-debt').fadeOut(400, function() { $(this).remove(); });

                    let statNo = $('#statNoTien');
                    if (statNo.length > 0) {
                        let currentVal = parseInt(statNo.text()) || 0;
                        if (currentVal > 0) statNo.text(currentVal - 1);
                    }

                    let roomItem = allRooms.find(r => r.phongTroId === currentQuickPaymentRoomId);
                    if (roomItem) {
                        roomItem.coNoTien = false;
                    }
                }
            });
        });

        // ========== QUẢN LÝ DỊCH VỤ PHÒNG ==========
        let currentDichVuPhongId = 0;

        function openDichVuPhong(phongTroId) {
            $('.kebab-dropdown.show').removeClass('show');
            currentDichVuPhongId = phongTroId;

            let room = allRooms.find(r => r.phongTroId === phongTroId);
            let roomName = room ? room.soPhong : phongTroId;
            $('#dichVuPhongTitle').text(`Quản lý dịch vụ - Phòng ${roomName}`);
            $('#dichVuPhongContainer').html('<div class="text-center py-4"><i class="ti ti-loader fa-spin fa-2x"></i></div>');
            $('#dichVuPhongModal').modal('show');

            $.get(`/DangKyDichVu/GetByPhong?phongTroId=${phongTroId}`, function (data) {
                renderDangKyDichVu('dichVuPhongContainer', data, false);
            }).fail(function () {
                $('#dichVuPhongContainer').html('<div class="alert alert-danger">Không thể tải dịch vụ.</div>');
            });
        }

        // Nút lưu dịch vụ từ modal phòng trọ
        $(document).on('click', '#btnSaveDichVuPhong', function () {
            let $btn = $(this);
            Swal.fire({
                title: 'Xác nhận lưu đăng ký dịch vụ?',
                text: 'Bạn có chắc chắn muốn lưu thông tin đăng ký dịch vụ cho phòng này?',
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Đồng ý',
                cancelButtonText: 'Hủy bỏ'
            }).then((result) => {
                if (result.isConfirmed) {
                    $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin me-1"></i> Đang lưu...');
                    saveDangKyDichVu(
                        currentDichVuPhongId,
                        'dichVuPhongContainer',
                        function (res) {
                            $btn.prop('disabled', false).html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                            if (res && res.success) {
                                showSuccess(res.message || 'Đăng ký dịch vụ thành công!');
                                $('#dichVuPhongModal').modal('hide');
                            } else {
                                showError(res?.message || 'Đã có lỗi xảy ra!');
                            }
                        },
                        function (errMsg) {
                            $btn.prop('disabled', false).html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                            showError(errMsg);
                        }
                    );
                }
            });
        });

        function showDetail(id) {
            let r = allRooms.find(x => x.phongTroId === id);
            if (!r) return;
            let lopTrangThai = r.trangThai === 0 ? 'bg-success-lt' : r.trangThai === 1 ? 'room-nhan-thue' : 'bg-warning-lt';
            let tenTrangThai = r.trangThai === 0 ? 'Trống' : r.trangThai === 1 ? 'Đã thuê' : 'Bảo trì';
            $('#detailRoomName').empty().append(
                document.createTextNode(`Phòng ${r.soPhong} `)
            ).append(
                $('<span>').addClass(`badge ${lopTrangThai}`).text(tenTrangThai)
            );

            let html = `
                <div class="detail-row"><span class="detail-label">Chi nhánh:</span> <span class="detail-value">${escapeHtml(r.tenChiNhanh)}</span></div>
                <div class="detail-row"><span class="detail-label">Giá thuê:</span> <span class="detail-value text-danger fw-bold num-tabular">${formatVND(r.giaThue)}/tháng</span></div>
                <div class="detail-row"><span class="detail-label">Diện tích:</span> <span class="detail-value">${r.dienTich} m²</span></div>
                <div class="detail-row"><span class="detail-label">Tầng:</span> <span class="detail-value">${r.tangLau}</span></div>
                <div class="detail-row"><span class="detail-label">Số người tối đa:</span> <span class="detail-value">${r.soNguoiToiDa}</span></div><hr>`;

            if (r.trangThai === 1 && r.tenNguoiThue) {
                html += `<h6 class="fw-bold"><i class="ti ti-user me-1"></i>Thông tin người thuê</h6>
                    <div class="detail-row"><span class="detail-label">Họ tên:</span> <span class="detail-value">${escapeHtml(r.tenNguoiThue)}</span></div>
                    <div class="detail-row"><span class="detail-label">SĐT:</span> <span class="detail-value">${escapeHtml(r.sdtNguoiThue) || 'N/A'}</span></div>`;
                if (r.ngayHetHan) {
                    let ngayStr = String(r.ngayHetHan).slice(0, 10).split('-').reverse().join('/');
                    html += `<div class="detail-row"><span class="detail-label">Hết hạn HĐ:</span> <span class="detail-value">${ngayStr}</span></div>`;
                }
                if (r.sapHetHan) html += `<div class="alert alert-warning mt-2 py-2"><i class="ti ti-hourglass me-1"></i><strong>Cảnh báo:</strong> Hợp đồng sắp hết hạn (còn ${r.soNgayConLai} ngày)!</div>`;
                if (r.coNoTien) html += `<div class="alert alert-danger mt-2 py-2"><i class="ti ti-alert-triangle me-1"></i><strong>Nợ tiền:</strong> Phòng này đang nợ <strong class="num-tabular">${formatVND(r.soTienNo)}</strong>!</div>`;
            } else if (r.trangThai === 0) {
                html += `<div class="alert alert-success mt-2 py-2"><i class="ti ti-circle-check me-1"></i>Phòng đang trống, sẵn sàng cho thuê.</div>`;
            } else if (r.trangThai === 2) {
                html += `<div class="alert alert-warning mt-2 py-2"><i class="ti ti-tools me-1"></i>Phòng đang bảo trì.</div>`;
            }

            $('#roomDetailBody').html(html);
            $('#roomDetailModal').modal('show');
        }

        // ========== TABLE VIEW LOGIC ==========
        $(document).ready(function () {
            // DataTable
            table = $('#phongTroTable').DataTable({
                "language": { "url": window.dataTablesViUrl },
                "responsive": true, "autoWidth": false,
                "ajax": { "url": "/PhongTros/DanhSachPhongTro", "type": "GET", "datatype": "json" },
                "columns": [
                    { "data": null, "className": "text-center align-middle", "orderable": false, "searchable": false,
                        "render": (d,t,r,m) => m.row + m.settings._iDisplayStart + 1 },
                    { "data": "tenChiNhanh", "className": "align-middle", "render": $.fn.dataTable.render.text() },
                    { "data": "soPhong", "className": "align-middle", "render": d => `<span class="fw-bold">${escapeHtml(d)}</span>` },
                    { "data": "tangLau", "className": "text-center align-middle" },
                    { "data": "giaThue", "className": "text-end align-middle num-tabular", "render": d => formatVND(d) },
                    { "data": "dienTich", "className": "text-center align-middle", "render": d => d + " m²" },
                    { "data": "trangThai", "className": "text-center align-middle",
                        "render": d => d === 0 ? '<span class="badge bg-success-lt"><i class="ti ti-door-enter me-1"></i>Trống</span>'
                                     : d === 1 ? '<span class="badge room-nhan-thue"><i class="ti ti-user-check me-1"></i>Đã thuê</span>'
                                     : '<span class="badge bg-warning-lt"><i class="ti ti-tools me-1"></i>Bảo trì</span>' },
                    { "data": "phongTroId", "className": "text-center align-middle", "orderable": false, "searchable": false,
                        "render": id => `
                            <div class="btn-list flex-nowrap justify-content-center">
                                <button class="btn btn-icon btn-ghost-secondary btn-sm btn-edit" data-id="${id}" aria-label="Sửa phòng" title="Sửa"><i class="ti ti-edit"></i></button>
                                ${LA_ADMIN ? `<button class="btn btn-icon btn-ghost-danger btn-sm btn-delete" data-id="${id}" aria-label="Xóa phòng" title="Xóa"><i class="ti ti-trash"></i></button>` : ''}
                            </div>
                        ` }
                ],
                "order": [[0, "desc"]]
            });

            // Table filter by Chi nhánh
            $('#filterChiNhanhTable').change(function () {
                table.column(1).search($(this).val()).draw();
            });

            // Load grid on start
            loadGridData();

            // Grid filters
            $('#filterChiNhanhGrid').change(function () { loadGridData(); });
            $('.filter-btn').click(function () {
                $('.filter-btn').removeClass('active');
                $(this).addClass('active');
                currentFilter = $(this).data('filter');
                $('.room-o-so').removeClass('is-chon').filter('[data-loc="' + currentFilter + '"]').addClass('is-chon');
                renderGrid();
            });

            // Bấm ô số liệu để lọc sơ đồ theo ô đó
            $(document).on('click', '.room-o-so', function (e) {
                e.preventDefault();
                let loc = $(this).data('loc');
                $('.filter-btn[data-filter="' + loc + '"]').trigger('click');
            });
            $('#searchRoom').on('input', function () { renderGrid(); });

            // Phát sinh phòng ngẫu nhiên
            $('#btnPhatSinhPhong').click(function () {
                let $btn = $(this);
                Swal.fire({
                    title: 'Phát sinh phòng ngẫu nhiên?',
                    text: 'Bạn muốn tự động phát sinh 10 phòng trọ ngẫu nhiên cho các chi nhánh hiện có?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang phát sinh...');
                        $.post('/PhongTros/PhatSinhNgauNhien', function (res) {
                            if (res.success) {
                                Swal.fire('Thành công!', res.message, 'success');
                                loadGridData();
                                if (table) table.ajax.reload(null, false);
                            } else {
                                Swal.fire('Lỗi!', res.message, 'error');
                            }
                        }).fail(function (xhr) {
                            Swal.fire('Lỗi!', serverMessage(xhr, 'Lỗi hệ thống khi phát sinh phòng!'), 'error');
                        }).always(function() {
                            $btn.prop('disabled', false).html('<i class="ti ti-wand"></i> Phát sinh 10 phòng ngẫu nhiên');
                        });
                    }
                });
            });

            // ========== SHARED CRUD ==========
            $('#btnOpenAddModal').on('click', function () {
                currentPhongTroId = 0;
                $('#ChiNhanhId').val(''); $('#SoPhong').val(''); $('#TangLau').val('1');
                $('#GiaThue').val(''); $('#DienTich').val(''); $('#SoNguoiToiDa').val('2');
                $('#TrangThai').val('0'); $('#MoTa').val('');
                $('#modalTitle').text('Thêm phòng trọ mới');
                $('#btnSaveData').html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                $('#phongTroModal').modal('show');
            });

            $('#btnSaveData').on('click', function () {
                let chiNhanhId = $('#ChiNhanhId').val();
                let soPhong = $('#SoPhong').val() || "";
                let giaThue = $('#GiaThue').val() || 0;
                if (!chiNhanhId) { showWarning("Vui lòng chọn chi nhánh!"); return; }
                if (soPhong.trim() === "") { showWarning("Vui lòng nhập số phòng!"); return; }
                if (giaThue <= 0) { showWarning("Giá thuê phải lớn hơn 0!"); return; }

                let isUpdate = currentPhongTroId > 0;
                let actionUrl = isUpdate ? '/PhongTros/CapNhatPhongTro' : '/PhongTros/ThemPhongTro';

                Swal.fire({
                    title: isUpdate ? 'Xác nhận cập nhật?' : 'Xác nhận thêm mới?',
                    text: isUpdate ? 'Bạn có chắc chắn muốn cập nhật thông tin phòng trọ này?' : 'Bạn có chắc chắn muốn thêm phòng trọ này?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let $btn = $(this);
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang lưu...');
                        $.ajax({
                            url: actionUrl,
                            type: 'POST',
                            data: {
                                PhongTroId: currentPhongTroId,
                                ChiNhanhId: chiNhanhId,
                                SoPhong: soPhong,
                                TangLau: $('#TangLau').val() || 0,
                                GiaThue: giaThue,
                                DienTich: $('#DienTich').val() || 0,
                                SoNguoiToiDa: $('#SoNguoiToiDa').val() || 0,
                                TrangThai: $('#TrangThai').val() || 0,
                                MoTa: $('#MoTa').val() || ""
                            },
                            success: function (res) {
                                if (res.success) {
                                    showSuccess(res.message);
                                    $('#phongTroModal').modal('hide');
                                    table.ajax.reload(null, false);
                                    loadGridData();
                                } else {
                                    showError("Lỗi: " + res.message);
                                }
                            },
                            error: function (xhr) {
                                showError(serverMessage(xhr, "Không thể kết nối đến máy chủ!"));
                            },
                            complete: function () {
                                if (isUpdate) {
                                    $btn.prop('disabled', false).html('<i class="ti ti-device-floppy me-1"></i> Lưu thay đổi');
                                } else {
                                    $btn.prop('disabled', false).html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                                }
                            }
                        });
                    }
                });
            });

            // Table Edit
            $('#phongTroTable tbody').on('click', '.btn-edit', function () {
                let id = $(this).data('id');
                currentPhongTroId = id;
                let $btn = $(this), orig = $btn.html();
                $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i>');
                $.get('/PhongTros/GetPhongTro?id=' + id, function (res) {
                    if (res.success && res.data) {
                        let d = res.data;
                        $('#ChiNhanhId').val(d.chiNhanhId); $('#SoPhong').val(d.soPhong);
                        $('#TangLau').val(d.tangLau); $('#GiaThue').val(d.giaThue);
                        $('#DienTich').val(d.dienTich); $('#SoNguoiToiDa').val(d.soNguoiToiDa);
                        $('#TrangThai').val(d.trangThai); $('#MoTa').val(d.moTa);
                        $('#modalTitle').text('Chỉnh sửa: Phòng ' + (d.soPhong || ""));
                        $('#btnSaveData').html('<i class="ti ti-device-floppy me-1"></i> Lưu thay đổi');
                        $('#phongTroModal').modal('show');
                    } else showError("Lỗi: " + (res.message || "Không tải được."));
                    $btn.prop('disabled', false).html(orig);
                }).fail(function (xhr) {
                    showError(serverMessage(xhr, 'Không tải được thông tin phòng.'));
                    $btn.prop('disabled', false).html(orig);
                });
            });

            // Table Delete
            $('#phongTroTable tbody').on('click', '.btn-delete', function () {
                let id = $(this).data('id'), $btn = $(this);
                Swal.fire({
                    title: 'Xác nhận xóa phòng?',
                    text: 'Hành động này không thể hoàn tác. Bạn có chắc chắn muốn xóa phòng này?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let orig = $btn.html();
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i>');
                        $.post('/PhongTros/XoaPhongTro', { id: id }, function (res) {
                            if (res.success) {
                                Swal.fire('Thành công!', res.message, 'success');
                                table.ajax.reload(null, false);
                                loadGridData();
                            } else {
                                Swal.fire('Lỗi!', res.message, 'error');
                                $btn.prop('disabled', false).html(orig);
                            }
                        }).fail(function (xhr) {
                            Swal.fire('Lỗi!', serverMessage(xhr, 'Không thể kết nối đến máy chủ.'), 'error');
                            $btn.prop('disabled', false).html(orig);
                        });
                    }
                });
            });
        });
