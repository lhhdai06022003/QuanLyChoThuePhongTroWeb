        function escapeHtml(unsafe) {
            return (unsafe || "").toString()
                 .replace(/&/g, "&amp;")
                 .replace(/</g, "&lt;")
                 .replace(/>/g, "&gt;")
                 .replace(/"/g, "&quot;")
                 .replace(/'/g, "&#039;");
        }

        $(document).ready(function () {
            $('#hopDongTable').DataTable({
                "ajax": {
                    "url": "/KhachThue/HopDong/GetDanhSach",
                    "dataSrc": ""
                },
                "columns": [
                    { "data": "maHopDong", "render": $.fn.dataTable.render.text() },
                    { 
                        "data": null,
                        "render": function (data, type, row) {
                            return '<div class="fw-bold">Phòng ' + escapeHtml(row.soPhong) + '</div>' +
                                   '<div class="text-muted">' + escapeHtml(row.tenChiNhanh) + '</div>';
                        }
                    },
                    { "data": "thoiDiemBatDau", "render": $.fn.dataTable.render.text() },
                    { "data": "thoiDiemKetThuc", "render": $.fn.dataTable.render.text() },
                    { 
                        "data": "tienThuePhong",
                        "className": "text-end num-tabular",
                        "render": function (data) {
                            return formatVND(data);
                        }
                    },
                    { 
                        "data": "trangThaiHopDong",
                        "render": function (data) {
                            if (data === 1) {
                                return '<span class="badge bg-success-lt">Đang hoạt động</span>';
                            } else if (data === 2) {
                                return '<span class="badge bg-secondary-lt">Đã kết thúc</span>';
                            } else if (data === 3) {
                                return '<span class="badge bg-danger-lt">Đã hủy</span>';
                            }
                            return '';
                        }
                    },
                    { 
                        "data": "hopDongId",
                        "orderable": false,
                        "className": "text-center",
                        "render": function (data, type, row) {
                            return '<button type="button" class="btn btn-primary btn-sm" onclick="showChiTiet(' + data + ', ' + escapeHtml(JSON.stringify(row.soPhong || '')) + ')"><i class="ti ti-eye me-1"></i> Xem chi tiết</button>';
                        }
                    }
                ],
                "language": {
                    "url": window.dataTablesViUrl
                },
                "pageLength": 10,
                "ordering": true,
                "info": true,
                "autoWidth": false
            });
        });

        function formatVND(n) {
            return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n || 0);
        }

        function showChiTiet(hopDongId, soPhong) {
            $('#quickContractRoom').text(soPhong);

            $('#qcMaHD').text('Đang tải...');
            $('#qcNgayBatDau').text('Đang tải...');
            $('#qcNgayKetThuc').text('Đang tải...');
            $('#qcTienCoc').text('Đang tải...');
            $('#qcTienThue').text('Đang tải...');
            $('#qcTenDaiDien').text('Đang tải...');
            $('#qcSDTDaiDien').text('Đang tải...');
            $('#qcCCCDDaiDien').text('Đang tải...');
            $('#qcMembersBody').html('<tr><td colspan="5" class="text-center"><i class="ti ti-loader fa-spin"></i> Đang tải thành viên...</td></tr>');
            $('#qcServicesBody').html('<tr><td colspan="5" class="text-center"><i class="ti ti-loader fa-spin"></i> Đang tải dịch vụ...</td></tr>');
            $('#qcMemberCount').text(0);
            $('#qcServiceCount').text(0);

            var modal = new bootstrap.Modal(document.getElementById('quickContractModal'));
            modal.show();

            $.get(`/KhachThue/HopDong/XemChiTiet?id=${hopDongId}`, function (res) {
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
                    text: xhr.responseText || 'Không thể tải thông tin hợp đồng.'
                });
            });
        }
