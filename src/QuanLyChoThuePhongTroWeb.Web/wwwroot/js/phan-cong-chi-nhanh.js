        $(document).ready(function () {
            var currentModalNguoiDungId = 0;
            var openNguoiDungIdOnLoad = window.phanCongChiNhanhConfig.openNguoiDungId;

            function formatVnDateTime(value) {
                if (!value) return '<span class="text-muted fst-italic">Chưa có</span>';
                var parts = value.split('T');
                if (parts.length !== 2) return escapeHtml(value);
                var dateParts = parts[0].split('-');
                if (dateParts.length !== 3) return escapeHtml(value);
                var time = parts[1].substring(0, 5);
                return dateParts[2] + '/' + dateParts[1] + '/' + dateParts[0] + ' ' + time;
            }

            function getAntiForgeryToken() {
                return $('input[name="__RequestVerificationToken"]').first().val();
            }

            function loadBranchFilter() {
                $.ajax({
                    url: '/QuanLyNhaTro/PhanCongChiNhanh/ChiNhanhs',
                    type: 'GET',
                    success: function (res) {
                        if (!res.success) {
                            showError(res.message);
                            return;
                        }
                        var html = '<option value="">Tất cả chi nhánh</option>';
                        (res.data || []).forEach(function (item) {
                            html += '<option value="' + item.chiNhanhId + '">' + escapeHtml(item.maChiNhanh) + ' - ' + escapeHtml(item.tenChiNhanh) + '</option>';
                        });
                        $('#filterChiNhanh').html(html);
                    }
                });
            }
            loadBranchFilter();

            var table = $('#phanCongTable').DataTable({
                "language": { "url": window.dataTablesViUrl },
                "responsive": true,
                "autoWidth": false,
                "ajax": {
                    "url": "/QuanLyNhaTro/PhanCongChiNhanh/DanhSach",
                    "type": "GET",
                    "dataSrc": function (json) {
                        if (!json.success) {
                            showError(json.message);
                            return [];
                        }
                        return json.data || [];
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
                    {
                        "data": "tenDangNhap",
                        "render": function (data) {
                            return '<span class="fw-bold text-primary">' + escapeHtml(data) + '</span>';
                        }
                    },
                    {
                        "data": "isActive",
                        "className": "text-center align-middle",
                        "render": function (data) {
                            return data
                                ? '<span class="badge bg-success-lt"><i class="ti ti-circle-check me-1"></i> Hoạt động</span>'
                                : '<span class="badge bg-secondary-lt"><i class="ti ti-ban me-1"></i> Đang khóa</span>';
                        }
                    },
                    {
                        "data": "chiNhanhDangPhuTrach",
                        "render": function (data) {
                            if (!data || data.length === 0) {
                                return '<span class="text-orange">Chưa phân công</span>';
                            }
                            return data.map(function (b) {
                                return '<span class="badge bg-primary-lt me-1" title="' + escapeHtml(b.tenChiNhanh) + '">' + escapeHtml(b.maChiNhanh) + '</span>';
                            }).join('');
                        }
                    },
                    {
                        "data": "lanThayDoiGanNhatUtc",
                        "render": function (data) { return formatVnDateTime(data); }
                    },
                    {
                        "data": "nguoiDungId",
                        "className": "text-center align-middle",
                        "orderable": false,
                        "render": function (data) {
                            return '<button class="btn btn-icon btn-ghost-primary btn-sm btn-mo-phan-cong" data-id="' + data + '" title="Phân công"><i class="ti ti-building-store"></i></button>';
                        }
                    }
                ],
                "order": []
            });

            function renderModalRows(rows) {
                var html = '';
                (rows || []).forEach(function (row) {
                    var trangThaiHtml = '';
                    var actionHtml = '';
                    if (row.trangThai === 1) {
                        trangThaiHtml = '<span class="badge bg-success-lt">Đang phụ trách</span>';
                        actionHtml = '<button class="btn btn-outline-danger btn-sm btn-thu-hoi" data-chi-nhanh-id="' + row.chiNhanhId + '">Thu hồi</button>';
                    } else if (row.trangThai === 2) {
                        trangThaiHtml = '<span class="badge bg-secondary-lt">Đã thu hồi</span>';
                    } else {
                        trangThaiHtml = '<span class="badge bg-orange-lt">Chưa phân công</span>';
                    }

                    if (row.trangThai !== 1 && !row.chiNhanhDaXoa) {
                        actionHtml = '<button class="btn btn-primary btn-sm btn-phan-cong" data-chi-nhanh-id="' + row.chiNhanhId + '">Phân công</button>';
                    }

                    var tenChiNhanhHtml = escapeHtml(row.maChiNhanh) + ' - ' + escapeHtml(row.tenChiNhanh);
                    var rowClass = row.chiNhanhDaXoa ? 'text-muted' : '';
                    if (row.chiNhanhDaXoa) {
                        tenChiNhanhHtml += ' <span class="text-muted">(đã xóa)</span>';
                    }

                    html += '<tr class="' + rowClass + '">' +
                        '<td>' + tenChiNhanhHtml + '</td>' +
                        '<td>' + trangThaiHtml + '</td>' +
                        '<td>' + formatVnDateTime(row.ngayPhanCongUtc) + (row.nguoiPhanCong ? ', ' + escapeHtml(row.nguoiPhanCong) : '') + '</td>' +
                        '<td>' + formatVnDateTime(row.ngayThuHoiUtc) + (row.nguoiThuHoi ? ', ' + escapeHtml(row.nguoiThuHoi) : '') + '</td>' +
                        '<td>' + escapeHtml(row.lyDo) + '</td>' +
                        '<td class="text-center">' + actionHtml + '</td>' +
                        '</tr>';
                });
                $('#phanCongModalBody').html(html);
            }

            function openPhanCongModal(nguoiDungId) {
                $.ajax({
                    url: '/QuanLyNhaTro/PhanCongChiNhanh/ChiTiet/' + nguoiDungId,
                    type: 'GET',
                    success: function (res) {
                        if (!res.success || !res.data) {
                            showError(res.message);
                            return;
                        }
                        currentModalNguoiDungId = nguoiDungId;
                        $('#phanCongModalTitle').text('Phân công chi nhánh — ' + res.data.tenDangNhap);
                        if (!res.data.isActive) {
                            $('#phanCongLockedAlert').removeClass('d-none');
                        } else {
                            $('#phanCongLockedAlert').addClass('d-none');
                        }
                        renderModalRows(res.data.chiNhanhs);
                        $('#phanCongModal').modal('show');
                    },
                    error: function () {
                        showError('Không thể tải thông tin phân công.');
                    }
                });
            }

            function reloadModal() {
                if (currentModalNguoiDungId > 0) {
                    $.ajax({
                        url: '/QuanLyNhaTro/PhanCongChiNhanh/ChiTiet/' + currentModalNguoiDungId,
                        type: 'GET',
                        success: function (res) {
                            if (res.success && res.data) {
                                if (!res.data.isActive) {
                                    $('#phanCongLockedAlert').removeClass('d-none');
                                } else {
                                    $('#phanCongLockedAlert').addClass('d-none');
                                }
                                renderModalRows(res.data.chiNhanhs);
                            }
                        }
                    });
                }
                table.ajax.reload(null, false);
            }

            $('#filterChiNhanh').on('change', function () {
                var v = $(this).val();
                table.ajax.url('/QuanLyNhaTro/PhanCongChiNhanh/DanhSach?chiNhanhId=' + v).load();
            });

            $('#phanCongTable tbody').on('click', '.btn-mo-phan-cong', function () {
                var id = $(this).data('id');
                openPhanCongModal(id);
            });

            $('#phanCongModalBody').on('click', '.btn-phan-cong', function () {
                var chiNhanhId = $(this).data('chi-nhanh-id');

                Swal.fire({
                    title: 'Phân công chi nhánh',
                    text: 'Phân công nhân viên này phụ trách chi nhánh đã chọn?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then(function (result) {
                    if (!result.isConfirmed) return;

                    $.ajax({
                        url: '/QuanLyNhaTro/PhanCongChiNhanh/PhanCong',
                        type: 'POST',
                        contentType: 'application/json',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() },
                        data: JSON.stringify({ nguoiDungId: currentModalNguoiDungId, chiNhanhId: chiNhanhId, lyDo: null }),
                        success: function (res) {
                            reloadModal();
                            if (res.success) {
                                showSuccess(res.message);
                            } else {
                                showError(res.message);
                            }
                        },
                        error: function () {
                            showError('Lỗi kết nối máy chủ khi phân công!');
                        }
                    });
                });
            });

            $('#phanCongModalBody').on('click', '.btn-thu-hoi', function () {
                var chiNhanhId = $(this).data('chi-nhanh-id');

                Swal.fire({
                    title: 'Thu hồi phân công',
                    input: 'text',
                    inputPlaceholder: 'Lý do thu hồi (bắt buộc)',
                    inputAttributes: { maxlength: 500 },
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ',
                    inputValidator: function (value) {
                        var trimmed = (value || '').trim();
                        if (!trimmed) {
                            return 'Vui lòng nhập lý do thu hồi.';
                        }
                        if (trimmed.length > 500) {
                            return 'Lý do tối đa 500 ký tự.';
                        }
                    }
                }).then(function (result) {
                    if (!result.isConfirmed) return;

                    $.ajax({
                        url: '/QuanLyNhaTro/PhanCongChiNhanh/ThuHoi',
                        type: 'POST',
                        contentType: 'application/json',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken() },
                        data: JSON.stringify({ nguoiDungId: currentModalNguoiDungId, chiNhanhId: chiNhanhId, lyDo: result.value.trim() }),
                        success: function (res) {
                            reloadModal();
                            if (res.success) {
                                showSuccess(res.message);
                            } else {
                                showError(res.message);
                            }
                        },
                        error: function () {
                            showError('Lỗi kết nối máy chủ khi thu hồi!');
                        }
                    });
                });
            });

            if (openNguoiDungIdOnLoad) {
                openPhanCongModal(openNguoiDungIdOnLoad);
            }
        });
