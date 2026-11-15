        let allRooms = [];
        let currentFilter = 'all';

        function formatVND(n) {
            return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n || 0);
        }

        function escapeHtml(unsafe) {
            return (unsafe || "").toString()
                 .replace(/&/g, "&amp;")
                 .replace(/</g, "&lt;")
                 .replace(/>/g, "&gt;")
                 .replace(/"/g, "&quot;")
                 .replace(/'/g, "&#039;");
        }

        function loadData() {
            let chiNhanhId = $('#filterChiNhanh').val();
            $.get(`/PhongTros/GetSoDoPhong?chiNhanhId=${chiNhanhId}`, function(data) {
                allRooms = data;
                updateStats();
                renderGrid();
            });
        }

        function updateStats() {
            let trong = allRooms.filter(r => r.trangThai === 0).length;
            let daThue = allRooms.filter(r => r.trangThai === 1).length;
            let noTien = allRooms.filter(r => r.coNoTien).length;
            let sapHetHan = allRooms.filter(r => r.sapHetHan).length;
            $('#statTrong').text(trong);
            $('#statDaThue').text(daThue);
            $('#statNoTien').text(noTien);
            $('#statSapHetHan').text(sapHetHan);
        }

        function renderGrid() {
            let filtered = allRooms;

            if (currentFilter === 'trong') filtered = allRooms.filter(r => r.trangThai === 0);
            else if (currentFilter === 'dathue') filtered = allRooms.filter(r => r.trangThai === 1);
            else if (currentFilter === 'baotri') filtered = allRooms.filter(r => r.trangThai === 2);
            else if (currentFilter === 'notien') filtered = allRooms.filter(r => r.coNoTien);
            else if (currentFilter === 'saphethan') filtered = allRooms.filter(r => r.sapHetHan);

            if (filtered.length === 0) {
                $('#roomGrid').html('');
                $('#emptyState').show();
                return;
            }
            $('#emptyState').hide();

            let html = '';
            filtered.forEach(r => {
                let cssClass = r.trangThai === 0 ? 'room-trong' : r.trangThai === 1 ? 'room-dathue' : 'room-baotri';

                let badges = '';
                if (r.coNoTien) {
                    badges += `<div class="room-badge badge-debt" title="Nợ ${formatVND(r.soTienNo)}"><i class="ti ti-alert-circle"></i></div>`;
                }
                if (r.sapHetHan) {
                    badges += `<div class="room-badge badge-expiring" title="Còn ${r.soNgayConLai} ngày"><i class="ti ti-hourglass"></i></div>`;
                }

                let tenant = r.tenNguoiThue ? `<div class="room-tenant"><i class="ti ti-user"></i> ${escapeHtml(r.tenNguoiThue)}</div>` : '';
                let meta = '';
                if (r.trangThai === 1 && r.soNgayConLai !== null) {
                    meta = `<div class="room-meta"><i class="ti ti-calendar"></i> Còn ${r.soNgayConLai} ngày</div>`;
                }
                if (r.coNoTien) {
                    meta += `<div class="room-meta text-danger"><i class="ti ti-alert-circle"></i> Nợ: ${formatVND(r.soTienNo)}</div>`;
                }

                html += `
                    <div class="room-card ${cssClass}" data-id="${r.phongTroId}" onclick="showDetail(${r.phongTroId})">
                        <div class="room-badges">${badges}</div>
                        <div class="room-number">${escapeHtml(r.soPhong)}</div>
                        <div class="room-price">${formatVND(r.giaThue)}/tháng</div>
                        ${tenant}
                        <div class="room-meta"><i class="ti ti-ruler"></i> ${r.dienTich} m² · Tầng ${r.tangLau}</div>
                        ${meta}
                    </div>
                `;
            });

            $('#roomGrid').html(html);
        }

        function showDetail(id) {
            let r = allRooms.find(x => x.phongTroId === id);
            if (!r) return;

            let statusClasses = { 0: 'bg-success', 1: 'bg-danger', 2: 'bg-warning' };
            let headerClass = statusClasses[r.trangThai] || 'bg-primary';

            $('#roomDetailStatus').removeClass('bg-success bg-danger bg-warning bg-primary').addClass(headerClass);
            $('#detailRoomName').text(`Phòng ${r.soPhong} - ${r.trangThaiText}`);

            let html = `
                <div class="datagrid">
                    <div class="datagrid-item">
                        <div class="datagrid-title">Chi nhánh</div>
                        <div class="datagrid-content">${escapeHtml(r.tenChiNhanh)}</div>
                    </div>
                    <div class="datagrid-item">
                        <div class="datagrid-title">Giá thuê</div>
                        <div class="datagrid-content text-danger fw-bold fs-3">${formatVND(r.giaThue)}/tháng</div>
                    </div>
                    <div class="datagrid-item">
                        <div class="datagrid-title">Diện tích</div>
                        <div class="datagrid-content">${r.dienTich} m²</div>
                    </div>
                    <div class="datagrid-item">
                        <div class="datagrid-title">Tầng</div>
                        <div class="datagrid-content">${r.tangLau}</div>
                    </div>
                    <div class="datagrid-item">
                        <div class="datagrid-title">Số người tối đa</div>
                        <div class="datagrid-content">${r.soNguoiToiDa}</div>
                    </div>
                </div>
            `;

            if (r.trangThai === 1 && r.tenNguoiThue) {
                html += `
                    <hr class="my-3">
                    <h3 class="card-title mb-3"><i class="ti ti-user text-muted me-2"></i>Thông tin người thuê</h3>
                    <div class="datagrid">
                        <div class="datagrid-item">
                            <div class="datagrid-title">Họ tên</div>
                            <div class="datagrid-content fw-bold">${escapeHtml(r.tenNguoiThue)}</div>
                        </div>
                        <div class="datagrid-item">
                            <div class="datagrid-title">SĐT</div>
                            <div class="datagrid-content">${escapeHtml(r.sdtNguoiThue) || 'N/A'}</div>
                        </div>
                `;
                if (r.ngayHetHan) {
                    let ngay = String(r.ngayHetHan).slice(0, 10).split('-').reverse().join('/');
                    html += `
                        <div class="datagrid-item">
                            <div class="datagrid-title">Hết hạn HĐ</div>
                            <div class="datagrid-content">${ngay}</div>
                        </div>
                    `;
                }
                html += `</div>`; // Close datagrid

                if (r.sapHetHan) {
                    html += `<div class="alert alert-warning mt-3 mb-0"><i class="ti ti-hourglass me-2"></i> <strong>Cảnh báo:</strong> Hợp đồng sắp hết hạn (còn ${r.soNgayConLai} ngày)!</div>`;
                }
                if (r.coNoTien) {
                    html += `<div class="alert alert-danger mt-3 mb-0"><i class="ti ti-alert-triangle me-2"></i> <strong>Nợ tiền:</strong> Phòng này đang nợ <strong>${formatVND(r.soTienNo)}</strong>!</div>`;
                }
            } else if (r.trangThai === 0) {
                html += `<div class="alert alert-success mt-3 mb-0"><i class="ti ti-circle-check me-2"></i> Phòng đang trống, sẵn sàng cho thuê.</div>`;
            } else if (r.trangThai === 2) {
                html += `<div class="alert alert-warning mt-3 mb-0"><i class="ti ti-tools me-2"></i> Phòng đang bảo trì.</div>`;
            }

            $('#roomDetailBody').html(html);
            $('#roomDetailModal').modal('show');
        }

        $(document).ready(function() {
            loadData();

            $('#filterChiNhanh').change(function() { loadData(); });

            $('.filter-btn').click(function() {
                $('.filter-btn').removeClass('active');
                $(this).addClass('active');
                currentFilter = $(this).data('filter');
                renderGrid();
            });
        });
