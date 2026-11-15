        $(document).ready(function () {
            var table = $('#thongBaoTable').DataTable({
                processing: true,
                serverSide: true,
                ajax: {
                    url: '/api/ThongBao/DataTable',
                    type: 'POST',
                    data: function (d) {
                        var chuaDocStr = $('#filter-chua-doc').val();
                        if (chuaDocStr !== "") {
                            d.chuaDoc = chuaDocStr === "true";
                        }
                    },
                    error: function(xhr, error, thrown) {
                        showError('Lỗi tải dữ liệu thông báo.');
                    }
                },
                columns: [
                    {
                        data: 'isRead',
                        render: function (data) {
                            if (data) {
                                return '<span class="status status-green"><span class="status-dot status-dot-animated"></span>Đã đọc</span>';
                            } else {
                                return '<span class="status status-red"><span class="status-dot status-dot-animated"></span>Chưa đọc</span>';
                            }
                        }
                    },
                    {
                        data: 'tieuDe',
                        render: function (data, type, row) {
                            let escapedData = $('<div>').text(data).html();
                            let fontClass = row.isRead ? 'fw-normal' : 'fw-bold';
                            return `<div class="${fontClass}">${escapedData}</div>`;
                        }
                    },
                    {
                        data: 'noiDung',
                        render: function (data, type, row) {
                            let escapedNoiDung = $('<div>').text(data).html();
                            return `<div class="text-wrap" style="max-width: 400px;">${escapedNoiDung}</div>`;
                        }
                    },
                    {
                        data: 'createdAt'
                    },
                    {
                        data: null,
                        orderable: false,
                        searchable: false,
                        className: "text-center",
                        render: function (data, type, row) {
                            let btns = '<div class="btn-list flex-nowrap justify-content-center">';
                            if (!row.isRead) {
                                btns += `<button class="btn btn-icon btn-sm btn-ghost-success btn-mark-read" data-id="${row.id}" title="Đánh dấu đã đọc">
                                            <i class="ti ti-circle-check"></i>
                                        </button>`;
                            }
                            btns += `<button class="btn btn-icon btn-sm btn-ghost-danger btn-delete" data-id="${row.id}" title="Xóa">
                                        <i class="ti ti-trash"></i>
                                    </button>`;
                            if (row.linkDieuHuong) {
                                btns += `<a href="${row.linkDieuHuong}" class="btn btn-icon btn-sm btn-ghost-primary" title="Đi tới liên kết">
                                            <i class="ti ti-external-link"></i>
                                        </a>`;
                            }
                            btns += '</div>';
                            return btns;
                        }
                    }
                ],
                language: {
                    url: window.dataTablesViUrl
                },
                order: []
            });

            $('#filter-chua-doc').on('change', function () {
                table.draw();
            });

            // Mark single as read
            $('#thongBaoTable').on('click', '.btn-mark-read', function () {
                let id = $(this).data('id');
                $.ajax({
                    url: `/api/ThongBao/DanhDauDaDoc/${id}`,
                    type: 'POST',
                    headers: { 'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() },
                    success: function (res) {
                        if (res.success) {
                            table.draw(false);
                            if (typeof window.loadThongBao === 'function') {
                                window.loadThongBao();
                            }
                        }
                    }
                });
            });

            // Delete single
            $('#thongBaoTable').on('click', '.btn-delete', function () {
                let id = $(this).data('id');
                Swal.fire({
                    title: 'Xóa thông báo?',
                    text: 'Bạn có chắc chắn muốn xóa thông báo này?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $.ajax({
                            url: `/api/ThongBao/Xoa/${id}`,
                            type: 'DELETE',
                            headers: { 'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() },
                            success: function (res) {
                                if (res.success) {
                                    table.draw(false);
                                    if (typeof window.loadThongBao === 'function') {
                                        window.loadThongBao();
                                    }
                                }
                            }
                        });
                    }
                });
            });

            // Mark all read
            $('#btn-mark-all-read').on('click', function () {
                $.ajax({
                    url: '/api/ThongBao/DanhDauTatCaDaDoc',
                    type: 'POST',
                    headers: { 'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() },
                    success: function (res) {
                        if (res.success) {
                            table.draw(false);
                            if (typeof window.loadThongBao === 'function') {
                                window.loadThongBao();
                            }
                            showSuccess('Đã đánh dấu tất cả thông báo là đã đọc');
                        }
                    }
                });
            });

            // Delete all
            $('#btn-delete-all').on('click', function () {
                Swal.fire({
                    title: 'Xóa tất cả?',
                    text: 'Toàn bộ lịch sử thông báo của bạn sẽ bị xóa. Bạn chắc chứ?',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $.ajax({
                            url: '/api/ThongBao/XoaTatCa',
                            type: 'DELETE',
                            headers: { 'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() },
                            success: function (res) {
                                if (res.success) {
                                    table.draw();
                                    if (typeof window.loadThongBao === 'function') {
                                        window.loadThongBao();
                                    }
                                    showSuccess('Đã xóa tất cả thông báo');
                                }
                            }
                        });
                    }
                });
            });
            
            // Listen to SignalR to refresh table automatically
            if (typeof connection !== 'undefined') {
                connection.on("NhanThongBao", function (thongBao) {
                    table.draw(false);
                });
            }
        });
