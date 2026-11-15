        // Hàm hiển thị thông báo thay cho alert() mặc định
        function showToastr(message, type = 'info') {
            if (type === 'success') {
                showSuccess(message);
            } else if (type === 'error') {
                showError(message);
            } else if (type === 'warning') {
                showWarning(message);
            } else {
                Swal.fire({
                    icon: 'info',
                    title: 'Thông báo',
                    text: message
                });
            }
        }

        window.currentHopDongId = 0;
        window.currentStep = 1;

        // Quản lý các bước của Stepper Wizard
        function goToStep(step) {
            let hasThanhVien = window.currentHopDongId > 0;
            let maxStepVal = hasThanhVien ? 4 : 3;

            if (step > maxStepVal || step < 1) return;

            // Validate di chuyển tiến tới từ bước 1
            if (step > window.currentStep && window.currentStep === 1) {
                let chiNhanhId = $('#ChiNhanhId').val();
                let phongTroId = $('#PhongTroId').val();
                let nguoiThueId = $('#NguoiThueId').val();
                let dateBatDau = batDauPicker.selectedDates[0];
                let tienThuePhong = parseFloat($('#TienThuePhong').val() || 0);

                if (!chiNhanhId || !phongTroId || !nguoiThueId || !dateBatDau) {
                    showToastr("Vui lòng nhập đầy đủ các trường bắt buộc (*).", "warning");
                    return;
                }
                if (tienThuePhong <= 0) {
                    showToastr("Vui lòng nhập Tiền Thuê Phòng (phải lớn hơn 0).", "warning");
                    return;
                }
            }

            window.currentStep = step;

            // Cập nhật UI Stepper Progress Bar
            $('#hopDongStepper .step-item').removeClass('active completed');
            $('#hopDongStepper .step-item').each(function() {
                let stepNum = parseInt($(this).data('step'));
                if (stepNum < window.currentStep) {
                    $(this).addClass('completed');
                } else if (stepNum === window.currentStep) {
                    $(this).addClass('active');
                }
            });

            // Cập nhật hiển thị Pane
            $('.tab-content .tab-pane').removeClass('show active');
            if (window.currentStep === 1) {
                $('#content-thongtin').addClass('show active');
            } else if (window.currentStep === 2) {
                $('#content-dichvu').addClass('show active');
            } else if (window.currentStep === 3) {
                $('#content-dieukhoan').addClass('show active');
            } else if (window.currentStep === 4) {
                $('#content-thanhvien').addClass('show active');
            }

            updateWizardButtons();
        }

        function updateWizardButtons() {
            let hasThanhVien = window.currentHopDongId > 0;
            let maxStepVal = hasThanhVien ? 4 : 3;

            // Hiển thị tiêu đề bước 4 trong stepper
            if (hasThanhVien) {
                $('#stepHeaderThanhVien').show();
            } else {
                $('#stepHeaderThanhVien').hide();
            }

            // Điều khiển hiển thị Nút Quay lại
            if (window.currentStep === 1) {
                $('#btnWizardBack').hide();
            } else {
                $('#btnWizardBack').show();
            }

            // Điều khiển hiển thị Nút Tiếp theo / Nút Lưu
            if (window.currentStep < maxStepVal) {
                $('#btnWizardNext').show();
                $('#btnSaveData').hide();
            } else {
                $('#btnWizardNext').hide();
                $('#btnSaveData').show();
            }
        }

        $(document).ready(function () {
            // Khởi tạo Partial View Thành Viên
            // Dữ liệu từ server: view đặt window.hopDongConfig ngay trước khi nạp file này.
            let allNguoiThues = window.hopDongConfig.allNguoiThues;
            // Đổ người thuê vào ô lọc và ô chọn trong modal trước khi khởi tạo Select2 và bảng.
            // new Option() gán chữ bằng textContent nên không cần escape.
            ['filterNguoiThue', 'NguoiThueId'].forEach(function (id) {
                const select = document.getElementById(id);
                if (!select) return;
                const options = document.createDocumentFragment();
                allNguoiThues.forEach(function (nt) { options.appendChild(new Option(nt.text, nt.value)); });
                select.appendChild(options);
            });
            if (typeof initThanhVienModule === "function") {
                initThanhVienModule(allNguoiThues);
            }

            // Tính toán Ngày đầu/cuối quý hiện tại bằng JS thuần
            const now = new Date();
            const quarter = Math.floor(now.getMonth() / 3);
            const startOfQuarter = new Date(now.getFullYear(), quarter * 3, 1);
            const endOfQuarter = new Date(now.getFullYear(), (quarter + 1) * 3, 0);

            // === KHỞI TẠO FLATPICKR CHO BỘ LỌC (Chọn khoảng ngày) ===
            let filterPicker = flatpickr("#filterDateRange", {
                mode: "range",
                dateFormat: "d/m/Y",
                locale: "vn"
            });

            // === KHỞI TẠO FLATPICKR CHO MODAL (Chọn ngày đơn) ===
            window.batDauPicker = flatpickr("#ThoiDiemBatDau", { dateFormat: "d/m/Y", locale: "vn" });
            window.ketThucPicker = flatpickr("#ThoiDiemKetThuc", { dateFormat: "d/m/Y", locale: "vn" });

            // Khởi tạo Select2 trong Modal (có dropdownParent tránh lỗi mất focus trong BS5 modal)
            if ($('#NguoiThueId').length > 0) {
                $('#NguoiThueId').select2({
                    theme: 'bootstrap4',
                    dropdownParent: $('#hopDongModal')
                });
            }

            // Cảnh báo nợ cũ của người thuê ở hợp đồng đã kết thúc (không chặn lưu).
            function hienCanhBaoNoCu(nguoiThueId, $dich) {
                $dich.empty();
                if (!nguoiThueId) return;
                $.get('/NguoiThue/NoCu/' + encodeURIComponent(nguoiThueId)).done(function (res) {
                    if (!res || !(res.tongNo > 0)) return;
                    const ds = (res.hoaDons || []).map(h => escapeHtml(h.maHoaDon) + ' (' + escapeHtml(h.maHopDong) + ')').join(', ');
                    $dich.html('<div class="alert alert-warning mt-2 mb-0">Người thuê đang nợ ' + formatVND(res.tongNo) + ' ở hợp đồng đã kết thúc: ' + ds + '</div>');
                });
            }

            $('#NguoiThueId').on('change', function () {
                if (window.currentHopDongId > 0) {
                    $('#canhBaoNoCu').empty();
                    return;
                }
                hienCanhBaoNoCu($(this).val(), $('#canhBaoNoCu'));
            });

            // Mảng dữ liệu phòng trọ từ ViewBag
            let allPhongTros = window.hopDongConfig.allPhongTros;

            // Logic Chọn Chi Nhánh -> Lọc Phòng Trọ
            $('#ChiNhanhId').on('change', function () {
                let chiNhanhId = $(this).val();
                let $phongTroSelect = $('#PhongTroId');
                $phongTroSelect.empty();
                if (!chiNhanhId) {
                    $phongTroSelect.append('<option value="">-- Vui lòng chọn chi nhánh trước --</option>');
                    return;
                }
                $phongTroSelect.append('<option value="">-- Chọn phòng trọ --</option>');
                let phongsFilter = allPhongTros.filter(p => p.chiNhanhId == parseInt(chiNhanhId));
                if(window.currentHopDongId == 0){
                    phongsFilter = phongsFilter.filter(p => p.trangThai == window.hopDongConfig.trangThaiPhongTrong);
                }
                phongsFilter.forEach(p => {
                    $phongTroSelect.append(`<option value="${p.phongTroId}" data-gia-thue="${p.giaThue || 0}">${escapeHtml(p.soPhong)}</option>`);
                });
            });

            // Logic Chọn Phòng Trọ -> Tải dịch vụ đăng ký
            $('#PhongTroId').on('change', function () {
                let phongTroId = $(this).val();
                
                // Tránh ghi đè tiền thuê/dịch vụ liên tục khi đang load chi tiết cập nhật
                if (window.isBindingData) return;

                // Lấy giá thuê phòng mặc định gán vào ô Tiền thuê từ thuộc tính data
                if (phongTroId) {
                    let giaThue = $(this).find(':selected').data('gia-thue');
                    if (giaThue !== undefined && giaThue !== null) {
                        $('#TienThuePhong').val(giaThue);
                    }
                } else {
                    $('#TienThuePhong').val('');
                }

                if (!phongTroId) {
                    $('#hopDongDichVuContainer').html(`
                        <div class="text-center text-muted py-4">
                            <i class="ti ti-hand-click fa-2x mb-2"></i>
                            <p>Vui lòng chọn <strong>Phòng Trọ</strong> ở tab "Thông tin hợp đồng" trước.</p>
                        </div>
                    `);
                    return;
                }
                // Hiện loading
                $('#hopDongDichVuContainer').html('<div class="text-center py-3"><i class="ti ti-loader fa-spin fa-2x"></i></div>');

                $.get(`/DangKyDichVu/GetByPhong?phongTroId=${phongTroId}`, function (data) {
                    let isNew = window.currentHopDongId === 0;
                    renderDangKyDichVu('hopDongDichVuContainer', data, isNew);
                }).fail(function () {
                    $('#hopDongDichVuContainer').html('<div class="alert alert-danger">Không thể tải dịch vụ.</div>');
                });
            });

            // Đọc query parameters từ URL
            const urlParams = new URLSearchParams(window.location.search);
            if (urlParams.has('chiNhanhId')) {
                $('#filterChiNhanh').val(urlParams.get('chiNhanhId'));
            }
            const urlBoLoc = urlParams.get('boLoc');
            if (urlBoLoc && $('#filterBoLoc option').filter(function () { return this.value === urlBoLoc; }).length > 0) {
                $('#filterBoLoc').val(urlBoLoc);
            }

            // KHỞI TẠO DATATABLE SERVER-SIDE
            var table = $('#hopDongTable').DataTable({
                "language": { "url": window.dataTablesViUrl },
                "responsive": true,
                "autoWidth": false,
                "serverSide": true,
                "processing": true,
                "ajax": {
                    "url": "/HopDong/DanhSachHopDongSideAsync",
                    "type": "POST",
                    "data": function (d) {
                        d.chiNhanhId = $('#filterChiNhanh').val();
                        d.nguoiThueId = $('#filterNguoiThue').val();
                        d.trangThai = $('#filterTrangThai').val();
                        d.boLoc = $('#filterBoLoc').val();

                        // Lấy khoảng ngày từ Flatpickr gửi lên Server
                        let selectedDates = filterPicker.selectedDates;
                        if (selectedDates.length === 2) {
                            d.startDate = flatpickr.formatDate(selectedDates[0], "Y-m-d");
                            d.endDate = flatpickr.formatDate(selectedDates[1], "Y-m-d");
                        }
                    }
                },
                "columns": [
                    {
                        "data": null,
                        "className": "text-center align-middle",
                        "orderable": false,
                        "searchable": false,
                        "render": function (data, type, row, meta) {
                            return meta.row + meta.settings._iDisplayStart + 1;
                        }
                    },
                    { "data": "maHopDong", "name": "maHopDong", "className": "fw-bold text-primary align-middle", "render": $.fn.dataTable.render.text() },
                    { "data": "soPhong", "name": "SoPhong", "orderable": true, "className": "align-middle", "render": $.fn.dataTable.render.text() },
                    { "data": "tenNguoiThue", "orderable": false, "className": "align-middle", "render": $.fn.dataTable.render.text() },
                    {
                        "data": "thoiDiemBatDau",
                        "name": "thoiDiemBatDau",
                        "className": "align-middle",
                        "render": function(data) {
                            if(!data) return '';
                            return String(data).slice(0, 10).split('-').reverse().join('/');
                        }
                    },
                    {
                        "data": "thoiDiemKetThuc",
                        "name": "thoiDiemKetThuc",
                        "className": "align-middle",
                        "render": function(data) {
                            if (!data) return '—';
                            return String(data).slice(0, 10).split('-').reverse().join('/');
                        }
                    },
                    {
                        "data": "tienThuePhong",
                        "name": "tienThuePhong",
                        "className": "align-middle text-end num-tabular",
                        "render": function(data) { return formatVND(data); }
                    },
                    {
                        "data": "trangThaiHopDong",
                        "orderable": false,
                        "className": "text-center align-middle",
                        "render": function (data, type, row) {
                            if (data === 1) {
                                let html = '<span class="badge bg-success-lt px-2 py-1">Đang hoạt động</span>';
                                if (row && row.soNgayQuaHan > 0) {
                                    html += `<div class="mt-1"><span class="badge bg-danger-lt px-2 py-1">Quá hạn ${row.soNgayQuaHan} ngày</span></div>`;
                                }
                                return html;
                            }
                            if (data === 2) {
                                if (row && row.maHopDongKeTiep) {
                                    const escapedCode = escapeHtml(row.maHopDongKeTiep);
                                    return `<span class="badge bg-info-lt px-2 py-1" title="→ ${escapedCode}">Đã gia hạn</span>`;
                                }
                                return '<span class="badge bg-secondary-lt px-2 py-1">Đã kết thúc</span>';
                            }
                            return '<span class="badge bg-danger-lt px-2 py-1">Đã hủy</span>';
                        }
                    },
                    {
                        "data": "hopDongId",
                        "orderable": false,
                        "className": "text-center align-middle",
                        "render": function (data, type, row) {
                            const nutThanhLyGiaHan = row.trangThaiHopDong !== 1 ? '' : `
                                            <a href="/QuanLyNhaTro/HopDong/ThanhLy/${data}" class="dropdown-item py-2">
                                                <i class="ti ti-file-check me-2"></i> Thanh lý
                                            </a>
                                            <a href="/QuanLyNhaTro/HopDong/GiaHan/${data}" class="dropdown-item py-2">
                                                <i class="ti ti-calendar-plus me-2"></i> Gia hạn
                                            </a>`;
                            const nutSua = row.trangThaiHopDong === 2 ? '' : `
                                            <button class="dropdown-item btn-edit py-2" data-id="${data}">
                                                <i class="ti ti-edit me-2"></i> Chỉnh sửa
                                            </button>`;
                            // Chỉ hợp đồng Đã hủy (3) mới xóa được; hợp đồng đã kết thúc được lưu trữ.
                            const nutXoa = row.trangThaiHopDong !== 3 ? '' : `
                                            <button class="dropdown-item btn-delete text-danger py-2" data-id="${data}">
                                                <i class="ti ti-trash me-2"></i> Xóa hợp đồng
                                            </button>`;
                            return `
                                <div class="d-flex justify-content-center">
                                    <div class="dropdown">
                                        <button class="btn btn-icon btn-sm" data-bs-toggle="dropdown" aria-expanded="false" title="Thao tác" aria-label="Thao tác">
                                            <i class="ti ti-dots-vertical"></i>
                                        </button>
                                        <div class="dropdown-menu dropdown-menu-end">
                                            <a href="/QuanLyNhaTro/HopDong/ChiTiet/${data}" class="dropdown-item py-2">
                                                <i class="ti ti-eye me-2"></i> Chi tiết
                                            </a>
                                            ${nutThanhLyGiaHan}
                                            <a href="/HopDong/Print/${data}" target="_blank" class="dropdown-item py-2">
                                                <i class="ti ti-printer me-2"></i> In hợp đồng
                                            </a>
                                            <a href="/HopDong/DownloadWord/${data}" class="dropdown-item py-2">
                                                <i class="ti ti-file-type-docx me-2"></i> Tải Word
                                            </a>
                                            <div class="dropdown-divider"></div>
                                            ${nutSua}
                                            ${nutXoa}
                                        </div>
                                    </div>
                                </div>
                            `;
                        }
                    }
                ],
                "order": [[4, 'desc']]
            });

            // Tự động tải lại dữ liệu khi thay đổi các bộ lọc
            $('#filterChiNhanh, #filterNguoiThue, #filterTrangThai, #filterBoLoc').on('change', function () {
                table.ajax.reload();
            });

            // Đối với khoảng ngày, tải lại khi đóng lịch (đã chọn xong)
            filterPicker.config.onClose = [function () {
                table.ajax.reload();
            }];
            // Đề phòng trường hợp người dùng xóa tay input
            $('#filterDateRange').on('change', function () {
                if (!$(this).val()) table.ajax.reload();
            });

            // Nút mở Modal Thêm mới
            $('#btnOpenAddModal').on('click', function() {
                window.currentHopDongId = 0;
                $('#MaHopDong').val('Hệ thống tự sinh khi lưu');
                $('#TienCocPhong').val('0');
                $('#TienThuePhong').val('');

                // Clear ngày tháng bằng hàm của Flatpickr
                window.batDauPicker.clear();
                window.ketThucPicker.clear();

                $('#ChiNhanhId').val('').trigger('change').prop('disabled', false);
                $('#NguoiThueId').val('').trigger('change').prop('disabled', false);
                $('#PhongTroId').prop('disabled', false);

                $('#groupTrangThaiHopDong').hide();
                $('#TrangThaiHopDong').val(1);
                $('#groupNguoiDungCoO').show();
                $('#NguoiDungCoOPhongKhong').prop('checked', true);
                $('.chk-dieukhoan').prop('checked', false);

                // Reset tab dịch vụ
                $('#hopDongDichVuContainer').html(`
                    <div class="text-center text-muted py-4">
                        <i class="ti ti-hand-click fa-2x mb-2"></i>
                        <p>Vui lòng chọn <strong>Phòng Trọ</strong> ở tab "Thông tin hợp đồng" trước.</p>
                    </div>
                `);

                // Thiết lập trạng thái Stepper về Bước 1
                goToStep(1);

                $('#modalTitle').text('Thêm Hợp Đồng Mới');
                $('#btnSaveData').html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                bootstrap.Modal.getOrCreateInstance(document.getElementById('hopDongModal')).show();
            });

            // Stepper Navigation Buttons
            $('#btnWizardNext').on('click', function() {
                goToStep(window.currentStep + 1);
            });

            $('#btnWizardBack').on('click', function() {
                goToStep(window.currentStep - 1);
            });

            $('#hopDongStepper').on('click', '.step-item', function() {
                let targetStep = parseInt($(this).data('step'));
                goToStep(targetStep);
            });

            // ================== LOGIC THÀNH VIÊN Ở GHÉP ==================
            // Khởi tạo Select2 Autocomplete và Flatpickr cho Modal Thêm Thành Viên
            $('#addMember_SearchNguoiThue').select2({
                dropdownParent: $('#addMemberModal'),
                theme: 'bootstrap4',
                placeholder: 'Gõ Tên, SĐT hoặc CCCD...',
                allowClear: true,
                minimumInputLength: 0,
                ajax: {
                    url: '/NguoiThue/SearchAutocomplete',
                    dataType: 'json',
                    delay: 250,
                    data: function (params) {
                        return {
                            searchTerm: params.term || ""
                        };
                    },
                    processResults: function (data) {
                        return {
                            results: $.map(data, function (item) {
                                return {
                                    id: item.id,
                                    text: `${item.hoVaTen} - ${item.soDienThoai || ''} (CCCD: ${item.cccd || ''})`,
                                    hoVaTen: item.hoVaTen,
                                    soDienThoai: item.soDienThoai,
                                    cccd: item.cccd
                                };
                            })
                        };
                    },
                    cache: true
                }
            });

            let memberNgayVaoPicker = flatpickr("#addMember_NgayVao", { dateFormat: "d/m/Y", locale: "vn", defaultDate: new Date() });

            // Khi chọn một khách thuê có sẵn từ danh sách tìm kiếm
            $('#addMember_SearchNguoiThue').on('select2:select', function (e) {
                let data = e.params.data;
                $('#addMember_NguoiThueId').val(data.id);
                $('#addMember_HoVaTen').val(data.hoVaTen).prop('disabled', true);
                $('#addMember_SoDienThoai').val(data.soDienThoai).prop('disabled', true);
                $('#addMember_CCCD').val(data.cccd).prop('disabled', true);
                hienCanhBaoNoCu(data.id, $('#addMember_NoCuWarning'));
            });

            // Khi người dùng click nút clear trong select2
            $('#addMember_SearchNguoiThue').on('select2:clear', function () {
                $('#addMember_NoCuWarning').empty();
                resetAddMemberForm();
            });

            // Khi người dùng bấm nút Nhập mới hoàn toàn
            $('#btnToggleNewMember').on('click', function () {
                $('#addMember_SearchNguoiThue').val(null).trigger('change');
                $('#addMember_NguoiThueId').val('');
                $('#addMember_HoVaTen').val('').prop('disabled', false).focus();
                $('#addMember_SoDienThoai').val('').prop('disabled', false);
                $('#addMember_CCCD').val('').prop('disabled', false);
            });

            // Hàm reset dữ liệu form modal thành viên
            function resetAddMemberForm() {
                $('#addMember_SearchNguoiThue').val(null).trigger('change');
                $('#addMember_NguoiThueId').val('');
                $('#addMember_HoVaTen').val('').prop('disabled', true);
                $('#addMember_SoDienThoai').val('').prop('disabled', true);
                $('#addMember_CCCD').val('').prop('disabled', true);
                memberNgayVaoPicker.setDate(new Date());
            }

            // Hàm load danh sách thành viên
            window.loadDanhSachThanhVien = function(hopDongId) {
                $('#thanhVienTbody').html('<tr><td colspan="8" class="text-center"><i class="ti ti-loader fa-spin"></i> Đang tải dữ liệu...</td></tr>');
                $.ajax({
                    url: `/ThanhVienHopDong/GetDanhSach/${hopDongId}`,
                    type: 'GET',
                    success: function (res) {
                        let html = '';
                        if (res && res.length > 0) {
                            res.forEach(function (tv, index) {
                                let ngayVao = tv.ngayVao ? String(tv.ngayVao).slice(0, 10).split('-').reverse().join('/') : '-';
                                let ngayRoi = tv.ngayChuyenDi ? String(tv.ngayChuyenDi).slice(0, 10).split('-').reverse().join('/') : '-';
                                let isChuHopDong = tv.isChuHopDong ? '<span class="badge bg-primary-lt"><i class="ti ti-crown"></i> Đại diện</span>' : '';
                                let trangThai = tv.ngayChuyenDi ? '<span class="badge bg-secondary-lt">Đã chuyển đi</span>' : '<span class="badge bg-success-lt">Đang ở</span>';
                                
                                let buttons = '';
                                if (!tv.ngayChuyenDi) {
                                    buttons += `<button type="button" class="btn btn-icon btn-ghost-warning btn-sm btn-checkout" data-id="${tv.chiTietThanhVienHopDongId}" title="Báo chuyển đi" aria-label="Báo chuyển đi"><i class="ti ti-logout"></i></button> `;
                                }
                                if (!tv.isChuHopDong) {
                                    buttons += `<button type="button" class="btn btn-icon btn-ghost-danger btn-sm btn-delete-member" data-id="${tv.chiTietThanhVienHopDongId}" title="Xoá thành viên (nhập sai)" aria-label="Xóa thành viên"><i class="ti ti-trash"></i></button>`;
                                }
                                buttons = `<div class="btn-list flex-nowrap justify-content-center">${buttons}</div>`;

                                html += `
                                    <tr>
                                        <td class="text-center align-middle">${index + 1}</td>
                                        <td class="align-middle"><strong>${escapeHtml(tv.hoVaTen)}</strong> ${isChuHopDong}</td>
                                        <td class="align-middle">${escapeHtml(tv.soDienThoai || '-')}</td>
                                        <td class="align-middle">${escapeHtml(tv.cccd || '-')}</td>
                                        <td class="align-middle">${ngayVao}</td>
                                        <td class="align-middle">${ngayRoi}</td>
                                        <td class="text-center align-middle">${trangThai}</td>
                                        <td class="text-center align-middle">${buttons}</td>
                                    </tr>
                                `;
                            });
                        } else {
                            html = '<tr><td colspan="8" class="text-center text-muted">Chưa có thành viên nào.</td></tr>';
                        }
                        $('#thanhVienTbody').html(html);
                    },
                    error: function () {
                        $('#thanhVienTbody').html('<tr><td colspan="8" class="text-center text-danger">Lỗi khi tải dữ liệu!</td></tr>');
                    }
                });
            };

            // Nút mở modal thêm thành viên
            $(document).on('click', '#btnOpenAddMemberModal', function () {
                if (!window.currentHopDongId || window.currentHopDongId === 0) {
                    showToastr("Hợp đồng chưa được lưu. Vui lòng tạo hợp đồng trước khi thêm thành viên.", "warning");
                    return;
                }
                resetAddMemberForm();
                bootstrap.Modal.getOrCreateInstance(document.getElementById('addMemberModal')).show();
            });

            // Nút lưu thành viên mới
            $(document).on('click', '#btnSaveMember', function () {
                let hopDongId = window.currentHopDongId;
                let nguoiThueId = $('#addMember_NguoiThueId').val();
                let hoVaTen = $('#addMember_HoVaTen').val().trim();
                let soDienThoai = $('#addMember_SoDienThoai').val().trim();
                let cccd = $('#addMember_CCCD').val().trim();
                let ngayVaoDate = memberNgayVaoPicker.selectedDates[0];

                if (!ngayVaoDate) {
                    showToastr("Vui lòng chọn ngày vào.", "warning");
                    return;
                }

                if (!nguoiThueId) {
                    if (!hoVaTen || !soDienThoai || !cccd) {
                        showToastr("Vui lòng nhập đủ Họ tên, Số ĐT và CCCD cho khách mới.", "warning");
                        return;
                    }
                }

                let requestData = {
                    hopDongId: hopDongId,
                    nguoiThueId: nguoiThueId ? parseInt(nguoiThueId) : null,
                    hoVaTen: hoVaTen,
                    soDienThoai: soDienThoai,
                    cccd: cccd,
                    ngayVao: flatpickr.formatDate(ngayVaoDate, "Y-m-d")
                };

                Swal.fire({
                    title: 'Xác nhận thêm thành viên?',
                    text: 'Bạn có chắc chắn muốn thêm thành viên này vào hợp đồng?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let $btn = $(this);
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang lưu...');

                        $.ajax({
                            url: '/ThanhVienHopDong/AddThanhVien',
                            type: 'POST',
                            contentType: 'application/json',
                            data: JSON.stringify(requestData),
                            success: function (res) {
                                showToastr(res.message, "success");
                                bootstrap.Modal.getOrCreateInstance(document.getElementById('addMemberModal')).hide();
                                window.loadDanhSachThanhVien(hopDongId);
                            },
                            error: function (xhr) {
                                let msg = "Đã xảy ra lỗi!";
                                if (xhr.responseJSON && xhr.responseJSON.message) msg = xhr.responseJSON.message;
                                showToastr(msg, "error");
                            },
                            complete: function () {
                                $btn.prop('disabled', false).html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                            }
                        });
                    }
                });
            });

            // Báo chuyển đi
            $(document).on('click', '.btn-checkout', function () {
                let id = $(this).data('id');
                Swal.fire({
                    title: 'Xác nhận chuyển đi?',
                    text: 'Xác nhận thành viên này đã chuyển đi? (Ngày rời phòng sẽ cập nhật hôm nay)',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $.post(`/ThanhVienHopDong/BaoRoiPhong/${id}`, function (res) {
                            showToastr(res.message, "success");
                            window.loadDanhSachThanhVien(window.currentHopDongId);
                        }).fail(function (xhr) {
                            showToastr(xhr.responseJSON ? xhr.responseJSON.message : "Lỗi!", "error");
                        });
                    }
                });
            });

            // Xoá nhầm thành viên
            $(document).on('click', '.btn-delete-member', function () {
                let id = $(this).data('id');
                Swal.fire({
                    title: 'Xác nhận xóa thành viên?',
                    text: 'Hành động này sẽ xóa hoàn toàn thành viên (nhập sai). Bạn có chắc muốn xoá?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $.ajax({
                            url: `/ThanhVienHopDong/XoaThanhVien/${id}`,
                            type: 'DELETE',
                            success: function (res) {
                                showToastr(res.message, "success");
                                window.loadDanhSachThanhVien(window.currentHopDongId);
                            },
                            error: function (xhr) {
                                showToastr(xhr.responseJSON ? xhr.responseJSON.message : "Lỗi!", "error");
                            }
                        });
                    }
                });
            });
            // =============================================================

            // SỰ KIỆN LƯU DỮ LIỆU (Hợp đồng + Dịch vụ)
            $('#btnSaveData').on('click', function () {
                let maHopDong = window.currentHopDongId > 0 ? $('#MaHopDong').val().trim() : '';
                let phongTroId = $('#PhongTroId').val();
                let nguoiThueId = $('#NguoiThueId').val();

                let dateBatDau = window.batDauPicker.selectedDates[0];
                let dateKetThuc = window.ketThucPicker.selectedDates[0];

                let tienCocPhong = parseFloat($('#TienCocPhong').val() || 0);
                let tienThuePhong = parseFloat($('#TienThuePhong').val() || 0);

                if (!phongTroId || !nguoiThueId || !dateBatDau) {
                    showToastr("Vui lòng nhập đầy đủ các trường bắt buộc (*).", "warning");
                    return;
                }

                if (tienThuePhong <= 0) {
                    showToastr("Vui lòng nhập Tiền Thuê Phòng (phải lớn hơn 0).", "warning");
                    return;
                }
                let requestData = {
                    hopDongId: window.currentHopDongId,
                    maHopDong: maHopDong,
                    phongTroId: parseInt(phongTroId),
                    nguoiThueId: parseInt(nguoiThueId),
                    thoiDiemBatDau: flatpickr.formatDate(dateBatDau, "Y-m-d"),
                    thoiDiemKetThuc: dateKetThuc ? flatpickr.formatDate(dateKetThuc, "Y-m-d") : null,
                    tienCocPhong: tienCocPhong,
                    tienThuePhong: tienThuePhong,
                    trangThaiHopDong: parseInt($('#TrangThaiHopDong').val() || 1),
                    nguoiDungCoOPhongKhong: $('#NguoiDungCoOPhongKhong').is(':checked'),
                    dieuKhoanMauIds: $('.chk-dieukhoan:checked').map(function() { return parseInt($(this).val()); }).get()
                };

                let isUpdate = window.currentHopDongId > 0;
                let apiUrl = isUpdate ? `/HopDong/Update/${window.currentHopDongId}` : `/HopDong/Create`;
                let httpMethod = isUpdate ? 'PUT' : 'POST';

                Swal.fire({
                    title: isUpdate ? 'Xác nhận cập nhật?' : 'Xác nhận thêm mới?',
                    text: isUpdate ? 'Bạn có chắc chắn muốn cập nhật hợp đồng này?' : 'Bạn có chắc chắn muốn tạo hợp đồng này?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let $btn = $(this);
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang xử lý...');

                        // Bước 1: Lưu hợp đồng
                        $.ajax({
                            url: apiUrl,
                            type: httpMethod,
                            contentType: 'application/json',
                            data: JSON.stringify(requestData),
                            success: function (res) {
                                // Bước 2: Lưu dịch vụ đăng ký (nếu có dịch vụ nào được render)
                                let hasDichVu = $('#hopDongDichVuContainer .dkdv-checkbox').length > 0;
                                if (hasDichVu) {
                                    saveDangKyDichVu(
                                        parseInt(phongTroId),
                                        'hopDongDichVuContainer',
                                        function () {
                                            showToastr(res.message || "Thao tác thành công!", "success");
                                            bootstrap.Modal.getOrCreateInstance(document.getElementById('hopDongModal')).hide();
                                            table.ajax.reload(null, false);
                                        },
                                        function (errMsg) {
                                            showToastr("Hợp đồng đã lưu, nhưng lỗi khi lưu dịch vụ: " + errMsg, "warning");
                                            bootstrap.Modal.getOrCreateInstance(document.getElementById('hopDongModal')).hide();
                                            table.ajax.reload(null, false);
                                        }
                                    );
                                } else {
                                    showToastr(res.message || "Thao tác thành công!", "success");
                                    bootstrap.Modal.getOrCreateInstance(document.getElementById('hopDongModal')).hide();
                                    table.ajax.reload(null, false);
                                }
                            },
                            error: function (xhr) {
                                let errorMessage = "Đã xảy ra lỗi!";
                                if (xhr.responseJSON) {
                                    if (xhr.responseJSON.message) {
                                        errorMessage = xhr.responseJSON.message;
                                    } else if (xhr.responseJSON.errors) {
                                        errorMessage = Object.values(xhr.responseJSON.errors).flat().join('\n');
                                    }
                                }
                                showToastr(errorMessage, "error");
                            },
                            complete: function () {
                                if (isUpdate) {
                                    $btn.prop('disabled', false).html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                                } else {
                                    $btn.prop('disabled', false).html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                                }
                            }
                        });
                    }
                });
            });

            // SỰ KIỆN NÚT SỬA (ĐỔ DỮ LIỆU)
            $('#hopDongTable tbody').on('click', '.btn-edit', function () {
                let id = $(this).data('id');
                window.currentHopDongId = id;

                let $btn = $(this);
                let originalHtml = $btn.html();
                $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i>');

                $.ajax({
                    url: `/HopDong/GetById/${id}`,
                    type: 'GET',
                    success: function (res) {
                        $('#MaHopDong').val(res.maHopDong);
                        if(res.phongTroId) {
                            window.isBindingData = true; // Tránh trigger đổi giá phòng/dịch vụ
                            $('#ChiNhanhId').val(res.chiNhanhId || '').trigger('change');
                            setTimeout(() => {
                                if ($('#PhongTroId option[value="'+res.phongTroId+'"]').length === 0) {
                                    let soPhongName = res.soPhong || res.phongTroId;
                                    $('#PhongTroId').append(`<option value="${res.phongTroId}" data-gia-thue="${res.tienThuePhong}">${escapeHtml(soPhongName)}</option>`);
                                }
                                $('#PhongTroId').val(res.phongTroId).change();
                                window.isBindingData = false;

                                setTimeout(() => {
                                    $.get(`/DangKyDichVu/GetByPhong?phongTroId=${res.phongTroId}`, function (dvData) {
                                        renderDangKyDichVu('hopDongDichVuContainer', dvData, false);
                                    });
                                }, 100);
                            }, 150);
                        }

                        if ($('#NguoiThueId option[value="'+res.nguoiThueId+'"]').length === 0) {
                            $('#NguoiThueId').append(`<option value="${res.nguoiThueId}">${escapeHtml(res.tenNguoiThue || "Khách thuê (ID: " + res.nguoiThueId + ")")}</option>`);
                        }
                        $('#NguoiThueId').val(res.nguoiThueId).trigger('change');

                        $('#ChiNhanhId').prop('disabled', true);
                        $('#PhongTroId').prop('disabled', true);
                        $('#NguoiThueId').prop('disabled', true);
                        
                        $('#groupNguoiDungCoO').hide();

                        $('#groupTrangThaiHopDong').show();
                        $('#TrangThaiHopDong').val(res.trangThaiHopDong);

                        if(res.thoiDiemBatDau) window.batDauPicker.setDate(new Date(res.thoiDiemBatDau));
                        if(res.thoiDiemKetThuc) window.ketThucPicker.setDate(new Date(res.thoiDiemKetThuc));
                        else window.ketThucPicker.clear();

                        $('#TienCocPhong').val(res.tienCocPhong);
                        $('#TienThuePhong').val(res.tienThuePhong);

                        // Check checkbox điều khoản
                        $('.chk-dieukhoan').prop('checked', false);
                        if (res.danhSachTieuDeDieuKhoan && res.danhSachTieuDeDieuKhoan.length > 0) {
                            $('.chk-dieukhoan').each(function() {
                                let labelText = $(this).siblings('label').text().trim();
                                if (res.danhSachTieuDeDieuKhoan.includes(labelText)) {
                                    $(this).prop('checked', true);
                                }
                            });
                        }

                        // Load danh sách thành viên và chuyển bước 1
                        if (typeof window.loadDanhSachThanhVien === "function") {
                            window.loadDanhSachThanhVien(id);
                        }

                        goToStep(1);

                        $('#modalTitle').text('Cập nhật Hợp Đồng');
                        $('#btnSaveData').html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                        bootstrap.Modal.getOrCreateInstance(document.getElementById('hopDongModal')).show();
                    },
                    error: function () { showToastr("Không thể tải thông tin.", "error"); },
                    complete: function() { $btn.prop('disabled', false).html(originalHtml); }
                });
            });

            // SỰ KIỆN NÚT XÓA
            $('#hopDongTable tbody').on('click', '.btn-delete', function () {
                let id = $(this).data('id');
                Swal.fire({
                    title: 'Xác nhận xóa hợp đồng?',
                    text: 'Bạn có chắc chắn muốn xóa hợp đồng này?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let $btn = $(this);
                        let originalHtml = $btn.html();
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i>');

                        $.ajax({
                            url: `/HopDong/Delete/${id}`,
                            type: 'DELETE',
                            success: function (res) {
                                showToastr(res.message || "Đã xóa.", "success");
                                table.ajax.reload(null, false);
                            },
                            error: function (xhr) {
                                let msg = "Lỗi khi xóa!";
                                if (xhr.responseJSON && xhr.responseJSON.message) msg = xhr.responseJSON.message;
                                showToastr(msg, "error");
                                $btn.prop('disabled', false).html(originalHtml);
                            }
                        });
                    }
                });
            });
        });
