        $(document).ready(function () {
            // Hàm format tiền
            function formatVND(n) {
                if (n === null || n === undefined) return "0 ₫";
                return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n);
            }

            // ================== TAB 1: DỊCH VỤ ==================
            var tableDichVu = $('#dichVuTable').DataTable({
                "language": { "url": window.dataTablesViUrl },
                "serverSide": true,
                "processing": true,
                "ajax": {
                    "url": "/DichVu/GetList",
                    "type": "POST"
                },
                "columns": [
                    { "data": null, "className": "text-center", "orderable": false, "render": (d,t,r,m) => m.row + m.settings._iDisplayStart + 1 },
                    { "data": "tenDichVu", "name": "tenDichVu", "render": $.fn.dataTable.render.text() },
                    { "data": "donVi", "render": $.fn.dataTable.render.text() },
                    { "data": "ghiChu", "orderable": false, "render": $.fn.dataTable.render.text() },
                    {
                        "data": "dichVuId", "orderable": false, "className": "text-center align-middle",
                        "render": function(data) {
                            if (!window.dichVuConfig.laAdmin) return '';
                            return `
                                <div class="btn-list flex-nowrap justify-content-center">
                                    <button class="btn btn-icon btn-ghost-warning btn-sm btn-edit-dv" data-id="${data}" title="Sửa">
                                        <i class="ti ti-edit"></i>
                                    </button>
                                    <button class="btn btn-icon btn-ghost-danger btn-sm btn-delete-dv" data-id="${data}" title="Xóa">
                                        <i class="ti ti-trash"></i>
                                    </button>
                                </div>
                            `;
                        }
                    }
                ]
            });

            $('#btnOpenAddDichVu').click(function() {
                $('#DichVuId').val(0);
                $('#TenDichVu').val('');
                $('#DonVi').val('');
                $('#GhiChuDichVu').val('');
                $('#dichVuModalTitle').text('Thêm dịch vụ mới');
                $('#btnSaveDichVu').html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                $('#dichVuModal').modal('show');
            });

            $('#btnSaveDichVu').click(function() {
                let id = $('#DichVuId').val();
                let ten = $('#TenDichVu').val().trim();
                let dvi = $('#DonVi').val().trim();
                let ghiChu = $('#GhiChuDichVu').val().trim();

                if(!ten || !dvi) { showWarning('Vui lòng nhập tên và đơn vị!'); return; }

                let req = { dichVuId: parseInt(id), tenDichVu: ten, donVi: dvi, ghiChu: ghiChu };
                let isUpdate = id > 0;

                Swal.fire({
                    title: isUpdate ? 'Xác nhận cập nhật?' : 'Xác nhận thêm mới?',
                    text: isUpdate ? 'Bạn có chắc chắn muốn cập nhật dịch vụ này?' : 'Bạn có chắc chắn muốn thêm dịch vụ này?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let $btn = $(this);
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang lưu...');

                        $.ajax({
                            url: isUpdate ? `/DichVu/Update/${id}` : '/DichVu/Create',
                            type: isUpdate ? 'PUT' : 'POST',
                            contentType: 'application/json',
                            data: JSON.stringify(req),
                            success: function(res) {
                                showSuccess(res.message);
                                $('#dichVuModal').modal('hide');
                                tableDichVu.ajax.reload(null, false);
                            },
                            error: function(xhr) { showError(xhr.responseJSON?.message || 'Có lỗi xảy ra'); },
                            complete: function() { 
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

            $('#dichVuTable').on('click', '.btn-edit-dv', function() {
                let id = $(this).data('id');
                $.get(`/DichVu/GetById/${id}`, function(res) {
                    $('#DichVuId').val(res.dichVuId);
                    $('#TenDichVu').val(res.tenDichVu);
                    $('#DonVi').val(res.donVi);
                    $('#GhiChuDichVu').val(res.ghiChu);
                    $('#dichVuModalTitle').text('Cập nhật dịch vụ');
                    $('#btnSaveDichVu').html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                    $('#dichVuModal').modal('show');
                });
            });

            $('#dichVuTable').on('click', '.btn-delete-dv', function() {
                let id = $(this).data('id');
                Swal.fire({
                    title: 'Xác nhận xóa?',
                    text: 'Bạn có chắc chắn muốn xóa dịch vụ này?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $.ajax({
                            url: `/DichVu/Delete/${id}`, type: 'DELETE',
                            success: function(res) {
                                showSuccess(res.message);
                                tableDichVu.ajax.reload(null, false);
                            },
                            error: function(xhr) {
                                showError(xhr.responseJSON?.message || 'Có lỗi xảy ra');
                            }
                        });
                    }
                });
            });

            // ================== TAB 2: BẢNG GIÁ CHI NHÁNH ==================
            var tableBangGia = $('#bangGiaTable').DataTable({
                "language": { "url": window.dataTablesViUrl },
                "serverSide": true,
                "processing": true,
                "ajax": {
                    "url": "/DichVuChiNhanh/GetList",
                    "type": "POST",
                    "data": function(d) {
                        d.chiNhanhId = $('#filterChiNhanh').val() || 0;
                    }
                },
                "columns": [
                    { "data": null, "className": "text-center", "orderable": false, "render": (d,t,r,m) => m.row + m.settings._iDisplayStart + 1 },
                    { "data": "tenChiNhanh", "orderable": false, "render": $.fn.dataTable.render.text() },
                    { "data": "tenDichVu", "orderable": false, "render": $.fn.dataTable.render.text() },
                    { "data": "giaDichVu", "orderable": false, "className": "text-end num-tabular", "render": function(d) { return formatVND(d); } },
                    {
                        "data": "macDinh", "orderable": false, "className": "text-center",
                        "render": function(data) {
                            return data
                                ? `<span class="badge-service-required"><i class="ti ti-lock"></i> Bắt buộc</span>`
                                : `<span class="badge-service-optional"><i class="ti ti-pointer"></i> Tùy chọn</span>`;
                        }
                    },
                    {
                        "data": "dichVuChiNhanhId", "orderable": false, "className": "text-center align-middle",
                        "render": function(data) {
                            return `
                                <div class="btn-list flex-nowrap justify-content-center">
                                    <button class="btn btn-icon btn-ghost-warning btn-sm btn-edit-bg" data-id="${data}" title="Sửa">
                                        <i class="ti ti-edit"></i>
                                    </button>
                                    <button class="btn btn-icon btn-ghost-danger btn-sm btn-delete-bg" data-id="${data}" title="Xóa">
                                        <i class="ti ti-trash"></i>
                                    </button>
                                </div>
                            `;
                        }
                    }
                ]
            });

            $('#filterChiNhanh').change(function() { tableBangGia.ajax.reload(); });

            $('#tab-bang-gia').on('shown.bs.tab', function() {
                tableBangGia.columns.adjust().draw(false);
            });

            function loadDichVuDropdown(selectedId = 0) {
                $.get('/DichVu/GetDropdown', function(data) {
                    let html = '<option value="">-- Chọn dịch vụ --</option>';
                    data.forEach(item => {
                        let sel = item.value == selectedId ? 'selected' : '';
                        html += `<option value="${escapeHtml(item.value)}" ${sel}>${escapeHtml(item.text)}</option>`;
                    });
                    $('#BgDichVuId').html(html);
                });
            }

            $('#btnOpenAddGia').click(function() {
                $('#DichVuChiNhanhId').val(0);
                $('#BgChiNhanhId').val('');
                loadDichVuDropdown();
                $('#GiaDichVu').val(0);
                $('#MacDinhBangGia').prop('checked', false);
                $('#bangGiaModalTitle').text('Cài đặt giá dịch vụ');
                $('#btnSaveBangGia').html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                $('#bangGiaModal').modal('show');
            });

            $('#btnSaveBangGia').click(function() {
                let id = $('#DichVuChiNhanhId').val();
                let cn = $('#BgChiNhanhId').val();
                let dv = $('#BgDichVuId').val();
                let gia = parseFloat($('#GiaDichVu').val() || 0);
                let macDinh = $('#MacDinhBangGia').is(':checked');

                if(!cn || !dv) { showWarning('Vui lòng chọn chi nhánh và dịch vụ!'); return; }
                if(gia < 0) { showWarning('Giá không được âm!'); return; }

                let req = { dichVuChiNhanhId: parseInt(id), chiNhanhId: parseInt(cn), dichVuId: parseInt(dv), giaDichVu: gia, macDinh: macDinh };
                let isUpdate = id > 0;

                Swal.fire({
                    title: isUpdate ? 'Xác nhận cập nhật?' : 'Xác nhận thêm mới?',
                    text: isUpdate ? 'Bạn có chắc chắn muốn cập nhật bảng giá này?' : 'Bạn có chắc chắn muốn cài đặt bảng giá này?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let $btn = $(this);
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang lưu...');

                        $.ajax({
                            url: isUpdate ? `/DichVuChiNhanh/Update/${id}` : '/DichVuChiNhanh/Create',
                            type: isUpdate ? 'PUT' : 'POST',
                            contentType: 'application/json',
                            data: JSON.stringify(req),
                            success: function(res) {
                                showSuccess(res.message);
                                $('#bangGiaModal').modal('hide');
                                tableBangGia.ajax.reload(null, false);
                            },
                            error: function(xhr) { showError(xhr.responseJSON?.message || 'Có lỗi xảy ra'); },
                            complete: function() { 
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

            $('#bangGiaTable').on('click', '.btn-edit-bg', function() {
                let id = $(this).data('id');
                $.get(`/DichVuChiNhanh/GetById/${id}`, function(res) {
                    $('#DichVuChiNhanhId').val(res.dichVuChiNhanhId);
                    $('#BgChiNhanhId').val(res.chiNhanhId);
                    loadDichVuDropdown(res.dichVuId);
                    $('#GiaDichVu').val(res.giaDichVu);
                    $('#MacDinhBangGia').prop('checked', res.macDinh === true);
                    $('#bangGiaModalTitle').text('Cập nhật bảng giá');
                    $('#btnSaveBangGia').html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                    $('#bangGiaModal').modal('show');
                });
            });

            // Đồng bộ xác nhận xóa bảng giá
            $('#bangGiaTable').on('click', '.btn-delete-bg', function() {
                let id = $(this).data('id');
                Swal.fire({
                    title: 'Xác nhận xóa?',
                    text: 'Bạn có chắc chắn muốn xóa bảng giá này?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $.ajax({
                            url: `/DichVuChiNhanh/Delete/${id}`, type: 'DELETE',
                            success: function(res) {
                                showSuccess(res.message);
                                tableBangGia.ajax.reload(null, false);
                            },
                            error: function(xhr) {
                                showError(xhr.responseJSON?.message || 'Có lỗi xảy ra');
                            }
                        });
                    }
                });
            });
        });
