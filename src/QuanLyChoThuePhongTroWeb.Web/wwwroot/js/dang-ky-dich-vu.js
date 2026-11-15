    // Render danh sách dịch vụ đăng ký vào container.
    // containerId: ID của div container (không có #)
    // data: Mảng DichVuChiNhanhWithDangKyRes từ API
    // isNew: true nếu đang tạo mới hợp đồng (auto-tick MacDinh)
    // Lấy Ngày Bắt Đầu Hợp Đồng (hoặc ngày hôm nay)
    function getContractStartDate() {
        let flatpickrEl = document.getElementById('ThoiDiemBatDau');
        if (flatpickrEl && flatpickrEl._flatpickr && flatpickrEl._flatpickr.selectedDates.length > 0) {
            let d = flatpickrEl._flatpickr.selectedDates[0];
            let year = d.getFullYear();
            let month = String(d.getMonth() + 1).padStart(2, '0');
            let day = String(d.getDate()).padStart(2, '0');
            return `${year}-${month}-${day}`;
        }
        // Fallback sang parse text
        let val = $('#ThoiDiemBatDau').val();
        if (val) {
            let parts = val.split('/');
            if (parts.length === 3) {
                return `${parts[2]}-${parts[1]}-${parts[0]}`;
            }
            parts = val.split('-');
            if (parts.length === 3) {
                if (parts[0].length === 4) return val;
                return `${parts[2]}-${parts[1]}-${parts[0]}`;
            }
        }
        
        let now = new Date();
        let year = now.getFullYear();
        let month = String(now.getMonth() + 1).padStart(2, '0');
        let day = String(now.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    // Render danh sách dịch vụ đăng ký vào container.
    // containerId: ID của div container (không có #)
    // data: Mảng DichVuChiNhanhWithDangKyRes từ API
    // isNew: true nếu đang tạo mới hợp đồng (auto-tick MacDinh)
    function renderDangKyDichVu(containerId, data, isNew) {
        let $container = $('#' + containerId);
        $container.empty();

        if (!data || data.length === 0) {
            $container.html(`
                <div class="dkdv-empty">
                    <i class="ti ti-info-circle"></i>
                    <p>Chi nhánh này chưa có dịch vụ nào được cài đặt giá.<br>
                    Vui lòng vào <strong>Quản lý Dịch vụ → Bảng giá</strong> để thiết lập.</p>
                </div>
            `);
            return;
        }

        function formatVNDShort(n) {
            return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n || 0);
        }

        // Lấy ngày bắt đầu mặc định
        let defaultStartDate = getContractStartDate();

        // Phân nhóm: Mặc định vs. Tùy chọn
        let defaults = data.filter(x => x.macDinh);
        let optionals = data.filter(x => !x.macDinh);

        let html = '<div class="dkdv-container">';

        // ===== NHÓM 1: DỊCH VỤ MẶC ĐỊNH (luôn bao gồm, không checkbox) =====
        if (defaults.length > 0) {
            html += `
                <div class="dkdv-section-header default-header">
                    <i class="ti ti-lock"></i> Dịch vụ bắt buộc
                    <span class="dkdv-section-count">${defaults.length} dịch vụ</span>
                </div>
            `;

            defaults.forEach(function (item) {
                let ngayBatDauStr = '';
                if (item.ngayBatDau && !isNew) {
                    ngayBatDauStr = item.ngayBatDau.split('T')[0];
                } else {
                    ngayBatDauStr = defaultStartDate;
                }

                // Dịch vụ mặc định: luôn selected, dùng hidden checkbox để collectDangKyDichVu vẫn thu thập được
                html += `
                    <div class="dkdv-item dkdv-default" data-dkdv-id="${item.dichVuChiNhanhId}">
                        <div class="dkdv-left">
                            <input type="checkbox" class="dkdv-checkbox" 
                                   data-id="${item.dichVuChiNhanhId}" 
                                   data-macdinh="true" 
                                   checked style="display:none;">
                            <div class="dkdv-action-col">
                                <div class="dkdv-default-icon">
                                    <i class="ti ti-check"></i>
                                </div>
                            </div>
                            <div class="dkdv-info">
                                <div class="dkdv-name">
                                    ${escapeHtml(item.tenDichVu)}
                                    <span class="dkdv-badge-included"><i class="ti ti-lock"></i> Luôn bao gồm</span>
                                </div>
                                <div class="dkdv-detail">
                                    <i class="ti ti-tag"></i> ${escapeHtml(item.donVi)}
                                </div>
                            </div>
                        </div>
                        <div class="dkdv-right">
                            <div class="dkdv-qty-spacer"></div>
                            <div class="dkdv-date-group">
                                <label>Bắt đầu:</label>
                                <input type="date" class="form-control form-control-sm dkdv-date-input" 
                                       data-date-id="${item.dichVuChiNhanhId}" 
                                       value="${ngayBatDauStr}">
                            </div>
                            <div class="dkdv-price dkdv-price-default">${formatVNDShort(item.giaDichVu)}/${escapeHtml(item.donVi)}</div>
                        </div>
                    </div>
                `;
            });
        }

        // ===== NHÓM 2: DỊCH VỤ TÙY CHỌN (checkbox + số lượng) =====
        if (optionals.length > 0) {
            html += `
                <div class="dkdv-section-header optional-header">
                    <i class="ti ti-circle-plus"></i> Dịch vụ tùy chọn
                    <span class="dkdv-section-count">${optionals.length} dịch vụ</span>
                </div>
            `;

            optionals.forEach(function (item) {
                let isChecked = item.isSelected;
                let checkedAttr = isChecked ? 'checked' : '';
                let selectedClass = isChecked ? 'selected' : '';
                let qtyDisplay = isChecked ? '' : 'display:none;';
                let soLuong = item.soLuong || 1;

                let ngayBatDauStr = '';
                if (item.ngayBatDau && !isNew) {
                    ngayBatDauStr = item.ngayBatDau.split('T')[0];
                } else {
                    ngayBatDauStr = defaultStartDate;
                }

                html += `
                    <div class="dkdv-item dkdv-optional ${selectedClass}" data-dkdv-id="${item.dichVuChiNhanhId}">
                        <div class="dkdv-left">
                            <div class="dkdv-action-col">
                                <input type="checkbox" class="dkdv-checkbox" 
                                       data-id="${item.dichVuChiNhanhId}" 
                                       data-macdinh="false" 
                                       ${checkedAttr}>
                            </div>
                            <div class="dkdv-info">
                                <div class="dkdv-name">${escapeHtml(item.tenDichVu)}</div>
                                <div class="dkdv-detail">
                                    <i class="ti ti-tag"></i> ${escapeHtml(item.donVi)}
                                </div>
                            </div>
                        </div>
                        <div class="dkdv-right">
                            <div class="dkdv-qty-group" data-qty-for="${item.dichVuChiNhanhId}" style="${qtyDisplay}">
                                <label>SL:</label>
                                <input type="number" class="form-control form-control-sm dkdv-qty-input" 
                                       data-qty-id="${item.dichVuChiNhanhId}" 
                                       value="${soLuong}" min="1" max="99">
                            </div>
                            <div class="dkdv-date-group" data-date-for="${item.dichVuChiNhanhId}" style="${qtyDisplay}">
                                <label>Bắt đầu:</label>
                                <input type="date" class="form-control form-control-sm dkdv-date-input" 
                                       data-date-id="${item.dichVuChiNhanhId}" 
                                       value="${ngayBatDauStr}">
                            </div>
                            <div class="dkdv-price">${formatVNDShort(item.giaDichVu)}/${escapeHtml(item.donVi)}</div>
                        </div>
                    </div>
                `;
            });
        }

        html += '</div>';
        $container.html(html);

        // Auto-check khi chỉ có đúng 1 dịch vụ tùy chọn
        let $optionalCheckboxes = $container.find('.dkdv-optional .dkdv-checkbox');
        if ($optionalCheckboxes.length === 1 && !$optionalCheckboxes.first().is(':checked')) {
            $optionalCheckboxes.first().prop('checked', true);
            let $item = $optionalCheckboxes.first().closest('.dkdv-item');
            let dvId = $optionalCheckboxes.first().data('id');
            $item.addClass('selected');
            $container.find(`[data-qty-for="${dvId}"]`).show();
            $container.find(`[data-date-for="${dvId}"]`).show();
        }

        // Event: checkbox change → toggle selected class + qty & date visibility (chỉ cho tùy chọn)
        $container.find('.dkdv-optional .dkdv-checkbox').on('change', function () {
            let $item = $(this).closest('.dkdv-item');
            let dvId = $(this).data('id');
            let $qty = $container.find(`[data-qty-for="${dvId}"]`);
            let $date = $container.find(`[data-date-for="${dvId}"]`);

            if ($(this).is(':checked')) {
                $item.addClass('selected');
                $qty.slideDown(150);
                $date.slideDown(150);
            } else {
                $item.removeClass('selected');
                $qty.slideUp(150);
                $date.slideUp(150);
            }
        });
    }

    // Thu thập dữ liệu đăng ký dịch vụ từ container.
    // containerId: ID của div container (không có #)
    // Returns: Mảng DangKyDichVuItem
    function collectDangKyDichVu(containerId) {
        let $container = $('#' + containerId);
        let items = [];

        $container.find('.dkdv-checkbox').each(function () {
            let dvId = parseInt($(this).data('id'));
            let isSelected = $(this).is(':checked');
            let soLuong = 1;
            let ngayBatDau = null;

            // Nếu không phải mặc định, lấy giá trị từ ô nhập
            let isMacDinh = $(this).data('macdinh') === true || $(this).data('macdinh') === 'true';
            if (!isMacDinh) {
                let $qty = $container.find(`[data-qty-id="${dvId}"]`);
                if ($qty.length > 0) {
                    soLuong = parseInt($qty.val()) || 1;
                }
            }

            // Lấy ngày bắt đầu
            let $dateInput = $container.find(`[data-date-id="${dvId}"]`);
            if ($dateInput.length > 0 && $dateInput.val()) {
                ngayBatDau = $dateInput.val();
            }

            items.push({
                dichVuChiNhanhId: dvId,
                isSelected: isSelected,
                soLuong: soLuong,
                ngayBatDau: ngayBatDau
            });
        });

        return items;
    }

    // Gọi API lưu đăng ký dịch vụ.
    // phongTroId: ID phòng trọ
    // containerId: ID container chứa UI dịch vụ
    // onSuccess: callback khi thành công
    // onError: callback khi lỗi
    function saveDangKyDichVu(phongTroId, containerId, onSuccess, onError) {
        let dichVus = collectDangKyDichVu(containerId);

        if (dichVus.length === 0) {
            if (onSuccess) onSuccess();
            return;
        }

        $.ajax({
            url: '/DangKyDichVu/Luu',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                phongTroId: phongTroId,
                dichVus: dichVus
            }),
            success: function (res) {
                if (onSuccess) onSuccess(res);
            },
            error: function (xhr) {
                let msg = xhr.responseJSON?.message || 'Lỗi khi lưu dịch vụ!';
                if (onError) onError(msg);
                else showError(msg);
            }
        });
    }
