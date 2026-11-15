        $(document).ready(function () {
            let currentNguoiDungId = 0;

            function escapeHtml(unsafe) {
                return (unsafe || "").toString()
                     .replace(/&/g, "&amp;")
                     .replace(/</g, "&lt;")
                     .replace(/>/g, "&gt;")
                     .replace(/"/g, "&quot;")
                     .replace(/'/g, "&#039;");
            }

            function formatDateTime(dateString) {
                if (!dateString) return `<span class="text-muted fst-italic">Chưa từng</span>`;
                const s = String(dateString);
                if (s.length >= 16) {
                    const d = s.slice(0, 10).split('-').reverse().join('/');
                    const t = s.slice(11, 16);
                    return `${t} ${d}`;
                }
                return s.slice(0, 10).split('-').reverse().join('/');
            }

            function loadNguoiThueDropdown() {
                $.ajax({
                    url: '/NguoiThue/GetCoHopDong',
                    type: 'GET',
                    success: function (res) {
                        let html = '<option value="">-- Chọn khách thuê --</option>';
                        res.forEach(function (item) {
                            html += `<option value="${escapeHtml(item.value)}">${escapeHtml(item.text)}</option>`;
                        });
                        $('#NguoiThueId').html(html);
                    }
                });
            }
            loadNguoiThueDropdown();

            if ($.fn.select2) {
                $('#NguoiThueId').select2({ dropdownParent: $('#nguoiDungModal') });
            }

            $('#Role').on('change', function () {
                if ($(this).val() == "2") {
                    $('#divNguoiThue').removeClass('d-none');
                    if (currentNguoiDungId === 0) {
                        $('#TenDangNhap').prop('disabled', true).val('');
                    }
                } else {
                    $('#divNguoiThue').addClass('d-none');
                    $('#NguoiThueId').val('').trigger('change.select2');
                    if (currentNguoiDungId === 0) {
                        $('#TenDangNhap').prop('disabled', false).val('');
                    }
                }
            });

            $('#NguoiThueId').on('change', function () {
                if ($('#Role').val() != "2") return;
                
                let id = $(this).val();
                if (id && currentNguoiDungId === 0) {
                    $.ajax({
                        url: '/NguoiThue/GetById/' + id,
                        type: 'GET',
                        success: function (res) {
                            if (res && res.email) {
                                $('#TenDangNhap').val(res.email);
                            } else {
                                showWarning("Người thuê này chưa có Email, vui lòng nhập tay tên đăng nhập!");
                                $('#TenDangNhap').val('').prop('disabled', false);
                            }
                        }
                    });
                }
            });

            // Khởi tạo Datatable gọi về cấu trúc chuẩn của NguoiDungController
            var table = $('#nguoiDungTable').DataTable({
                "language": { "url": window.dataTablesViUrl },
                "responsive": true,
                "autoWidth": false,
                "ajax": {
                    "url": "/QuanLyNhaTro/NguoiDung/GetAllApi", // Đường dẫn lấy dữ liệu API danh sách
                    "type": "GET",
                    "dataSrc": ""
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
                    {
                        "data": "tenDangNhap",
                        "render": function(data) {
                            return `<span class="fw-bold text-primary">${escapeHtml(data)}</span>`;
                        }
                    },
                    {
                        "data": "role",
                        "render": function(data) {
                            if (data === 0) return `<span class="badge bg-danger-lt">Admin</span>`;
                            if (data === 1) return `<span class="badge bg-primary-lt">Nhân viên</span>`;
                            if (data === 2) return `<span class="badge bg-info-lt">Khách thuê</span>`;
                            if (data === 3) return `<span class="badge bg-orange-lt">Khách vãng lai</span>`;
                            return `<span class="badge bg-secondary-lt">N/A</span>`;
                        }
                    },
                    {
                        "data": "isActive",
                        "className": "text-center align-middle",
                        "render": function(data) {
                            return data
                                ? `<span class="badge bg-success-lt"><i class="ti ti-circle-check me-1"></i> Hoạt động</span>`
                                : `<span class="badge bg-secondary-lt"><i class="ti ti-ban me-1"></i> Khóa</span>`;
                        }
                    },
                    {
                        "data": "ngayTao",
                        "render": function(data) { return formatDateTime(data); }
                    },
                    {
                        "data": "lanDangNhapCuoi",
                        "render": function(data) { return formatDateTime(data); }
                    },
                    {
                        "data": "nguoiDungId",
                        "className": "text-center align-middle",
                        "orderable": false,
                        "render": function (data, type, row) {
                            let resetBtn = '';
                            if (row.role === 2) {
                                resetBtn = `
                                    <button class="btn btn-icon btn-ghost-info btn-sm btn-reset" data-id="${data}" title="Đặt lại mật khẩu">
                                        <i class="ti ti-key"></i>
                                    </button>
                                `;
                            }
                            let branchBtn = '';
                            if (row.role === 1) {
                                branchBtn = `
                                    <a class="btn btn-ghost-primary btn-sm" href="/QuanLyNhaTro/PhanCongChiNhanh?nguoiDungId=${data}" title="Phân công chi nhánh"><i class="ti ti-building-store me-1"></i>Chi nhánh</a>
                                `;
                            }
                            return `
                                <div class="btn-list flex-nowrap justify-content-center">
                                    ${resetBtn}
                                    ${branchBtn}
                                    <button class="btn btn-icon btn-ghost-warning btn-sm btn-edit" data-id="${data}" title="Sửa">
                                        <i class="ti ti-edit"></i>
                                    </button>
                                    <button class="btn btn-icon btn-ghost-danger btn-sm btn-delete" data-id="${data}" title="Xóa">
                                        <i class="ti ti-trash"></i>
                                    </button>
                                </div>
                            `;
                        }
                    }
                ],
                "order": []
            });

            // --- SỰ KIỆN LƯU DỮ LIỆU (ADD / UPDATE) ---
            $('#btnSaveData').on('click', function () {
                let tenDangNhap = $('#TenDangNhap').val().trim();
                let matKhau = $('#MatKhau').val();
                let role = parseInt($('#Role').val(), 10);
                let isActive = $('#IsActive').is(':checked');
                let nguoiThueId = $('#NguoiThueId').val();

                if (!tenDangNhap) {
                    showWarning("Vui lòng điền Tên đăng nhập!");
                    return;
                }

                if (currentNguoiDungId <= 0 && role === 3) {
                    showWarning("Tài khoản khách vãng lai do khách tự đăng ký, không tạo ở đây!");
                    return;
                }

                if (role === 2 && !nguoiThueId) {
                    showWarning("Vui lòng chọn người thuê khi chọn quyền là Khách Thuê!");
                    return;
                }

                let isUpdate = currentNguoiDungId > 0;

                if (!isUpdate && !matKhau) {
                    showWarning("Vui lòng nhập mật khẩu cho tài khoản mới!");
                    return;
                }

                let requestData = {
                    tenDangNhap: tenDangNhap,
                    role: role,
                    isActive: isActive,
                    matKhauHash: matKhau,
                    nguoiThueId: nguoiThueId ? parseInt(nguoiThueId, 10) : null
                };

                let apiUrl = isUpdate ? `/QuanLyNhaTro/NguoiDung/EditApi/${currentNguoiDungId}` : `/QuanLyNhaTro/NguoiDung/CreateApi`;
                let httpMethod = 'POST'; // Dùng POST đồng bộ cấu trúc MVC Route của bạn

                if (isUpdate) {
                    requestData.nguoiDungId = parseInt(currentNguoiDungId, 10);
                }

                Swal.fire({
                    title: isUpdate ? 'Xác nhận cập nhật?' : 'Xác nhận thêm mới?',
                    text: isUpdate ? 'Bạn có chắc chắn muốn cập nhật tài khoản này?' : 'Bạn có chắc chắn muốn tạo tài khoản mới này?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let $btn = $(this);
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang xử lý...');

                        $.ajax({
                            url: apiUrl,
                            type: httpMethod,
                            contentType: 'application/json',
                            data: JSON.stringify(requestData),
                            success: function (res) {
                                if(res.success) {
                                    showSuccess(res.message || "Lưu thành công!");
                                    $('#nguoiDungModal').modal('hide');
                                    table.ajax.reload(null, false);
                                } else {
                                    showError(res.message || "Đã xảy ra lỗi khi lưu dữ liệu.");
                                }
                            },
                            error: function () {
                                showError("Lỗi hệ thống máy chủ hoặc dữ liệu không hợp lệ!");
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

            // --- BẮT SỰ KIỆN NÚT SỬA (EDIT) ---
            $('#nguoiDungTable tbody').on('click', '.btn-edit', function () {
                currentNguoiDungId = $(this).data('id');

                $.ajax({
                    url: '/QuanLyNhaTro/NguoiDung/GetByIdApi/' + currentNguoiDungId,
                    type: 'GET',
                    success: function (res) {
                        if (res) {
                            $('#TenDangNhap').val(res.tenDangNhap).prop('disabled', true); // Khóa không cho sửa username
                            $('#MatKhau').val(''); // Không đổ lại mật khẩu cũ vì đã băm
                            $('#Role').val(res.role).trigger('change');
                            $('#IsActive').prop('checked', res.isActive);

                            if (res.role === 2 && res.nguoiThueId) {
                                $('#NguoiThueId').val(res.nguoiThueId).trigger('change');
                            } else {
                                $('#NguoiThueId').val('').trigger('change');
                            }

                            $('.class-edit-note').removeClass('d-none');
                            $('#modalTitle').text('Cập nhật tài khoản');
                            $('#btnSaveData').html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                            $('#nguoiDungModal').modal('show');
                        }
                    },
                    error: function() {
                        showError("Không thể tải thông tin tài khoản này.");
                    }
                });
            });

            // --- BẮT SỰ KIỆN NÚT XÓA (DELETE MỀM) ---
            $('#nguoiDungTable tbody').on('click', '.btn-delete', function () {
                let id = $(this).data('id');
                let $btn = $(this);

                Swal.fire({
                    title: 'Xác nhận xóa?',
                    text: 'Bạn có chắc chắn muốn xóa tài khoản người dùng này? (Tài khoản sẽ bị hủy kích hoạt và ẩn khỏi danh sách)',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let originalHtml = $btn.html();
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i>');

                        $.ajax({
                            url: '/QuanLyNhaTro/NguoiDung/DeleteApi/' + id,
                            type: 'POST',
                            success: function (res) {
                                if (res.success) {
                                    showSuccess(res.message || "Xóa thành công!");
                                    table.ajax.reload(null, false);
                                } else {
                                    showError(res.message || "Không thể xóa!");
                                    $btn.prop('disabled', false).html(originalHtml);
                                }
                            },
                            error: function () {
                                showError("Lỗi kết nối máy chủ khi thực hiện xóa!");
                                $btn.prop('disabled', false).html(originalHtml);
                            }
                        });
                    }
                });
            });

            // --- MỞ MODAL THÊM MỚI ---
            $('#btnOpenAddModal').on('click', function () {
                currentNguoiDungId = 0;
                $('#frmNguoiDung')[0].reset();
                $('#NguoiThueId').val('').trigger('change.select2');
                $('#Role').val(1).trigger('change');
                $('#TenDangNhap').prop('disabled', false);
                $('.class-edit-note').addClass('d-none');
                $('#modalTitle').text('Thêm tài khoản mới');
                $('#btnSaveData').html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                $('#nguoiDungModal').modal('show');
            });

            // --- ĐẶT LẠI MẬT KHẨU CHO KHÁCH THUÊ ---
            $('#nguoiDungTable tbody').on('click', '.btn-reset', function () {
                let id = $(this).data('id');
                let $btn = $(this);

                Swal.fire({
                    title: 'Đặt lại mật khẩu?',
                    text: 'Mật khẩu sẽ được đặt lại thành số điện thoại của người thuê. Bạn có chắc chắn?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let originalHtml = $btn.html();
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i>');

                        $.ajax({
                            url: '/QuanLyNhaTro/NguoiDung/ResetPasswordToPhoneApi/' + id,
                            type: 'POST',
                            success: function (res) {
                                if (res.success) {
                                    showSuccess(res.message);
                                } else {
                                    showError(res.message);
                                }
                            },
                            error: function () {
                                showError("Lỗi kết nối máy chủ!");
                            },
                            complete: function () {
                                $btn.prop('disabled', false).html(originalHtml);
                            }
                        });
                    }
                });
            });
        });
