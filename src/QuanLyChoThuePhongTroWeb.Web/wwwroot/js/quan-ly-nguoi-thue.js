        function escapeHtml(unsafe) {
            return (unsafe || "").toString()
                 .replace(/&/g, "&amp;")
                 .replace(/</g, "&lt;")
                 .replace(/>/g, "&gt;")
                 .replace(/"/g, "&quot;")
                 .replace(/'/g, "&#039;");
        }

        $(document).ready(function () {
            let currentNguoiThueId = 0;

            function formatDateForInput(dateString) {
                if (!dateString) return '';
                return dateString.split('T')[0];
            }

            var table = $('#nguoiThueTable').DataTable({
                "language": { "url": window.dataTablesViUrl },
                "responsive": true,
                "autoWidth": false,
                "ajax": {
                    "url": "/NguoiThue/GetAll", 
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
                        "data": "hoVaTen",
                        "render": function(data) {
                            return `<span class="fw-bold text-primary">${escapeHtml(data)}</span>`;
                        }
                    },
                    // Hiển thị Email trên bảng. Nếu null thì hiển thị chữ "Chưa có" màu xám
                    { 
                        "data": "email",
                        "render": function(data) {
                            return data ? escapeHtml(data) : `<span class="text-secondary">Chưa có</span>`;
                        }
                    },
                    { "data": "soDienThoai", "render": $.fn.dataTable.render.text() },
                    { "data": "cccd", "render": $.fn.dataTable.render.text() },
                    {
                        "data": "ngaySinh",
                        "render": function(data) {
                            if(!data) return "";
                            return String(data).slice(0, 10).split('-').reverse().join('/');
                        }
                    },
                    { "data": "queQuan", "render": $.fn.dataTable.render.text() },
                    {
                        "data": "nguoiThueId",
                        "className": "text-center align-middle",
                        "orderable": false,
                        "render": function (data) {
                            return `
                                <div class="btn-list flex-nowrap justify-content-center">
                                    <a class="btn btn-icon btn-ghost-info btn-sm" href="/QuanLyNhaTro/NguoiThue/ChiTiet/${data}" title="Chi tiết" aria-label="Xem chi tiết">
                                        <i class="ti ti-eye"></i>
                                    </a>
                                    <button class="btn btn-icon btn-ghost-warning btn-sm btn-edit" data-id="${data}" title="Sửa" aria-label="Sửa">
                                        <i class="ti ti-edit"></i>
                                    </button>
                                    <button class="btn btn-icon btn-ghost-danger btn-sm btn-delete" data-id="${data}" title="Xóa" aria-label="Xóa">
                                        <i class="ti ti-trash"></i>
                                    </button>
                                </div>
                            `;
                        }
                    }
                ],
                "order": []
            });

            // --- SỰ KIỆN LƯU DỮ LIỆU ---
            $('#btnSaveData').on('click', function () {
                let hoVaTen = $('#HoVaTen').val().trim();
                let email = $('#Email').val().trim();
                let sdt = $('#SoDienThoai').val().trim();
                let cccd = $('#CCCD').val().trim();

                // Validation giao diện: Không ép buộc Email
                if (!hoVaTen || !sdt || !cccd) {
                    showWarning('Vui lòng nhập đầy đủ Họ tên, Số điện thoại và CCCD!');
                    return;
                }

                // Nếu có nhập email thì kiểm tra định dạng sơ bộ ở client
                if (email && !/^[^\s\x40]+\x40[^\s\x40]+\.[^\s\x40]+$/.test(email)) {
                    showWarning('Vui lòng nhập đúng định dạng Email!');
                    return;
                }

                let requestData = {
                    hoVaTen: hoVaTen,
                    email: email ? email : "",
                    soDienThoai: sdt,
                    cccd: cccd,
                    ngaySinh: $('#NgaySinh').val() ? $('#NgaySinh').val() : null,
                    ngayCapCCCD: $('#NgayCapCCCD').val() ? $('#NgayCapCCCD').val() : null,
                    noiCapCCCD: $('#NoiCapCCCD').val().trim(),
                    queQuan: $('#QueQuan').val().trim(),
                    ghiChu: $('#GhiChu').val().trim()
                };

                let isUpdate = currentNguoiThueId > 0;
                let apiUrl = isUpdate ? `/NguoiThue/Update/${currentNguoiThueId}` : `/NguoiThue/Create`;
                let httpMethod = isUpdate ? 'PUT' : 'POST';

                if (isUpdate) {
                    requestData.nguoiThueId = parseInt(currentNguoiThueId, 10);
                }

                Swal.fire({
                    title: isUpdate ? 'Xác nhận cập nhật?' : 'Xác nhận thêm mới?',
                    text: isUpdate ? 'Bạn có chắc chắn muốn cập nhật thông tin người thuê này?' : 'Bạn có chắc chắn muốn thêm người thuê này?',
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
                                showSuccess(res.message || 'Lưu thành công!');
                                $('#nguoiThueModal').modal('hide');
                                table.ajax.reload(null, false);
                            },
                            error: function (xhr) {
                                let errorMessage = "Đã xảy ra lỗi!";
                                if (xhr.status === 400) {
                                    if (xhr.responseJSON && xhr.responseJSON.errors) {
                                        errorMessage = "Chi tiết lỗi:\n";
                                        for (let key in xhr.responseJSON.errors) {
                                            errorMessage += "- " + xhr.responseJSON.errors[key][0] + "\n";
                                        }
                                    } else if (xhr.responseJSON && xhr.responseJSON.message) {
                                        errorMessage = xhr.responseJSON.message;
                                    } else {
                                        errorMessage = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại!";
                                    }
                                }
                                showError(errorMessage);
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

            // Bắt sự kiện cho nút SỬA (Edit)
            $('#nguoiThueTable tbody').on('click', '.btn-edit', function () {
                currentNguoiThueId = $(this).data('id');

                $.ajax({
                    url: '/NguoiThue/GetById/' + currentNguoiThueId, 
                    type: 'GET',
                    success: function (res) {
                        if (res) {
                            $('#HoVaTen').val(res.hoVaTen);
                            $('#Email').val(res.email);
                            $('#SoDienThoai').val(res.soDienThoai);
                            $('#CCCD').val(res.cccd);
                            $('#NgaySinh').val(formatDateForInput(res.ngaySinh));
                            $('#NgayCapCCCD').val(formatDateForInput(res.ngayCapCCCD));
                            $('#NoiCapCCCD').val(res.noiCapCCCD);
                            $('#QueQuan').val(res.queQUan);
                            $('#GhiChu').val(res.ghiChu);

                            $('#modalTitle').text('Cập nhật Người Thuê');
                            $('#btnSaveData').html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                            $('#nguoiThueModal').modal('show');
                        }
                    },
                    error: function() {
                        showError('Không thể tải thông tin người thuê.');
                    }
                });
            });

            // Bắt sự kiện cho nút XÓA (Delete)
            $('#nguoiThueTable tbody').on('click', '.btn-delete', function () {
                let id = $(this).data('id');
                let $btn = $(this);

                Swal.fire({
                    title: 'Xác nhận xóa?',
                    text: 'Hành động này không thể hoàn tác. Bạn có chắc chắn muốn xóa người thuê này?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let originalHtml = $btn.html();
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i>');

                        $.ajax({
                            url: '/NguoiThue/Delete/' + id, 
                            type: 'DELETE',
                            success: function (res) {
                                showSuccess(res.message || 'Xóa thành công!');
                                table.ajax.reload(null, false);
                            },
                            error: function (xhr) {
                                let msg = "Lỗi kết nối máy chủ khi thực hiện xóa!";
                                if (xhr.responseJSON && xhr.responseJSON.message) {
                                    msg = xhr.responseJSON.message;
                                }
                                showError('Không thể xóa: ' + msg);
                                $btn.prop('disabled', false).html(originalHtml);
                            }
                        });
                    }
                });
            });

            // Reset form khi mở Modal Thêm Mới
            $('#btnOpenAddModal').on('click', function () {
                currentNguoiThueId = 0;
                $('#frmNguoiThue')[0].reset(); 
                $('#modalTitle').text('Thêm Người Thuê Mới');
                $('#btnSaveData').html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                $('#nguoiThueModal').modal('show');
            });

            // Phát sinh khách thuê ngẫu nhiên
            $('#btnPhatSinhKhach').click(function () {
                let $btn = $(this);
                Swal.fire({
                    title: 'Phát sinh khách thuê?',
                    text: 'Bạn có chắc chắn muốn tự động phát sinh 10 khách thuê ngẫu nhiên?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang phát sinh...');

                        $.ajax({
                            url: '/NguoiThue/PhatSinhNgauNhien',
                            type: 'POST',
                            success: function (res) {
                                showSuccess(res.message);
                                table.ajax.reload(null, false);
                            },
                            error: function (xhr) {
                                showError('Lỗi khi phát sinh khách thuê: ' + (xhr.responseJSON?.message || 'Lỗi hệ thống'));
                            },
                            complete: function () {
                                $btn.prop('disabled', false).html('<i class="ti ti-wand"></i> Phát sinh 10 khách ngẫu nhiên');
                            }
                        });
                    }
                });
            });
        });
