(function (window, $) {
    'use strict';

    var cfg = window.dashboardConfig || {};
    var MAU = { Khan: 'red', CanLam: 'orange', TheoDoi: 'blue' };
    var TEN_MUC = { Khan: 'Khẩn', CanLam: 'Cần làm', TheoDoi: 'Theo dõi' };

    function dinhDangTien(n) {
        var so = Math.round(Number(n) || 0);
        var chuoi = String(Math.abs(so)).replace(/\B(?=(\d{3})+(?!\d))/g, '.');
        return (so < 0 ? '-' : '') + chuoi + ' đ';
    }

    function dinhDangPhanTram(n) {
        return (Math.round(Number(n) * 10) / 10).toString().replace('.', ',') + '%';
    }

    function safeLink(url) {
        return typeof url === 'string' && url.charAt(0) === '/' && url.charAt(1) !== '/' && url.charAt(1) !== '\\' ? url : '#';
    }

    function tao(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    // Màu chữ và lưới biểu đồ lấy theo theme (mặc định của Chart.js quá mờ trên nền tối).
    // Chart.js chốt màu trục lúc tạo biểu đồ, nên phải gán thẳng vào options của từng biểu đồ.
    function mauTheme() {
        var css = window.getComputedStyle(document.body);
        return {
            chu: (css.getPropertyValue('--tblr-secondary-color') || css.getPropertyValue('--tblr-secondary') || '').trim() || css.color,
            luoi: (css.getPropertyValue('--tblr-border-color') || '').trim() || 'rgba(0, 0, 0, 0.1)'
        };
    }

    // Biểu đồ cột chồng doanh thu: chỉ Admin có khối này (nhân viên không có canvas).
    var revenueCanvas = cfg.coXemDoanhThu ? document.getElementById('revenueBarChart') : null;
    var revenueChart = revenueCanvas ? new Chart(revenueCanvas.getContext('2d'), {
        type: 'bar',
        data: {
            labels: cfg.chartLabels,
            datasets: [
                { label: 'Đã thu', data: cfg.chartData, backgroundColor: '#2fb344', borderRadius: 4 },
                { label: 'Chờ thu', data: cfg.chartDataChoThu, backgroundColor: '#f59f00', borderRadius: 4 }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                x: { stacked: true, ticks: {}, grid: {} },
                y: {
                    stacked: true,
                    beginAtZero: true,
                    ticks: { callback: function (v) { return dinhDangTien(v); } },
                    grid: {}
                }
            },
            plugins: {
                legend: { position: 'top', labels: {} },
                tooltip: {
                    callbacks: {
                        label: function (ctx) { return ctx.dataset.label + ': ' + dinhDangTien(ctx.parsed.y); }
                    }
                }
            }
        }
    }) : null;

    // Biểu đồ tròn tình trạng phòng
    var roomChart = new Chart(document.getElementById('roomStatusChart').getContext('2d'), {
        type: 'doughnut',
        data: {
            labels: cfg.roomLabels,
            datasets: [{
                data: cfg.roomData,
                backgroundColor: ['#206bc4', '#2fb344', '#f59f00'],
                borderWidth: 2,
                hoverOffset: 6
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom', labels: {} } }
        }
    });

    function apDungMauTheme() {
        var mau = mauTheme();
        if (revenueChart) {
            ['x', 'y'].forEach(function (truc) {
                revenueChart.options.scales[truc].ticks.color = mau.chu;
                revenueChart.options.scales[truc].grid.color = mau.luoi;
            });
            revenueChart.options.plugins.legend.labels.color = mau.chu;
            revenueChart.update();
        }
        roomChart.options.plugins.legend.labels.color = mau.chu;
        roomChart.update();
    }
    apDungMauTheme();

    function renderViec(viec, branchId) {
        var list = document.getElementById('viecCanLamList');
        list.replaceChildren();

        var tongSo = Number(viec.tongSo) || 0;
        $('#viecTongSo').text('(' + tongSo + ')');
        $('#viecSoKhan').text('Khẩn ' + (Number(viec.soKhan) || 0));
        $('#viecSoCanLam').text('Cần làm ' + (Number(viec.soCanLam) || 0));
        $('#viecSoTheoDoi').text('Theo dõi ' + (Number(viec.soTheoDoi) || 0));

        var xemTatCa = document.getElementById('viecXemTatCa');
        xemTatCa.textContent = 'Xem tất cả ' + tongSo + ' việc →';
        xemTatCa.setAttribute('href', safeLink(cfg.urlViecCanLam) + (branchId ? '?chiNhanhId=' + encodeURIComponent(branchId) : ''));

        var rong = tongSo === 0 || !viec.items || viec.items.length === 0;
        $('#viecCanLamChips, #viecCanLamFooter').toggleClass('d-none', rong);

        if (rong) {
            var khung = tao('div', 'text-center text-secondary py-4');
            khung.appendChild(tao('i', 'ti ti-circle-check text-success fs-1 d-block mb-2'));
            khung.appendChild(document.createTextNode('Không có việc cần xử lý'));
            list.appendChild(khung);
            return;
        }

        var group = tao('div', 'list-group list-group-flush');
        viec.items.forEach(function (item) {
            var muc = MAU[item.mucUuTien] ? item.mucUuTien : 'TheoDoi';
            var a = tao('a', 'list-group-item list-group-item-action px-0');
            a.setAttribute('href', safeLink(item.link));

            var hang = tao('div', 'd-flex align-items-center gap-2');
            var cham = tao('span', 'status-dot status-' + MAU[muc]);
            cham.setAttribute('aria-hidden', 'true');
            hang.appendChild(cham);
            hang.appendChild(tao('span', 'visually-hidden', TEN_MUC[muc]));

            var text = tao('div', 'dashboard-viec-text flex-fill');
            text.appendChild(tao('div', 'fw-medium', item.tieuDe));
            var moTa = tao('div', 'small text-secondary text-truncate', item.moTa);
            moTa.setAttribute('title', item.moTa || '');
            text.appendChild(moTa);
            hang.appendChild(text);

            hang.appendChild(tao('span', 'fw-bold ms-2', String(Number(item.soLuong) || 0)));
            a.appendChild(hang);
            group.appendChild(a);
        });
        list.appendChild(group);
    }

    function datLink(id, url, nhan) {
        var el = document.getElementById(id);
        if (!el || !url) return;
        el.setAttribute('href', safeLink(url));
        if (nhan) el.setAttribute('aria-label', nhan);
    }

    // Khối "Chờ bạn duyệt" (chỉ Admin): dòng hóa đơn chờ chốt, hoặc dòng "Không có việc chờ duyệt".
    function renderChoDuyet(choDuyet) {
        var box = document.getElementById('choDuyetHd2');
        if (!box || !choDuyet) return;
        var soLuong = Number(choDuyet.soLuong) || 0;
        $('#choDuyetTongSo').text('(' + soLuong + ')');
        box.replaceChildren();
        if (soLuong === 0) {
            box.appendChild(tao('div', 'list-group-item text-secondary', 'Không có việc chờ duyệt'));
            return;
        }
        var a = tao('a', 'list-group-item list-group-item-action');
        a.setAttribute('href', safeLink(choDuyet.link));
        var hang = tao('div', 'd-flex align-items-center gap-2');
        var cham = tao('span', 'status-dot status-orange');
        cham.setAttribute('aria-hidden', 'true');
        hang.appendChild(cham);
        hang.appendChild(tao('div', 'dashboard-viec-text flex-fill fw-medium', choDuyet.tieuDe));
        hang.appendChild(tao('span', 'fw-bold ms-2', String(soLuong)));
        a.appendChild(hang);
        box.appendChild(a);
    }

    // Bảng "Tiền mặt nhân viên đang giữ" (chỉ Admin): mỗi dòng là link sang Lịch sử thanh toán của người thu đó.
    function renderTienMatNhanVien(ds, ky) {
        var body = document.getElementById('tienMatBody');
        if (!body || !Array.isArray(ds)) return;
        $('#tienMatKy').text(ky);
        body.replaceChildren();
        var tongKhoan = 0;
        var tongTien = 0;
        if (ds.length === 0) {
            var tr = tao('tr');
            var td = tao('td', 'text-center text-secondary py-3', 'Tháng này chưa có khoản tiền mặt nào');
            td.setAttribute('colspan', '3');
            tr.appendChild(td);
            body.appendChild(tr);
        }
        ds.forEach(function (n) {
            tongKhoan += Number(n.soKhoan) || 0;
            tongTien += Number(n.tongTien) || 0;
            var tr = tao('tr');
            [[n.ten, 'text-reset d-block fw-medium', ''], [String(Number(n.soKhoan) || 0), 'text-reset d-block', 'text-end num-tabular'],
                [dinhDangTien(n.tongTien), 'text-reset d-block', 'text-end num-tabular']].forEach(function (cot) {
                var td = tao('td', cot[2]);
                var a = tao('a', cot[1], cot[0]);
                a.setAttribute('href', safeLink(n.link));
                td.appendChild(a);
                tr.appendChild(td);
            });
            body.appendChild(tr);
        });
        $('#tienMatFoot').toggleClass('d-none', ds.length === 0);
        $('#tienMatTongKhoan').text(tongKhoan);
        $('#tienMatTongTien').text(dinhDangTien(tongTien));
    }

    function renderHoatDong(activities) {
        var box = document.getElementById('activitiesContainer');
        box.replaceChildren();

        if (!activities || activities.length === 0) {
            var khung = tao('div', 'text-center text-secondary py-4');
            khung.appendChild(tao('i', 'ti ti-info-circle fs-1 d-block mb-2'));
            khung.appendChild(tao('div', null, 'Chưa ghi nhận hoạt động vận hành nào gần đây.'));
            box.appendChild(khung);
            return;
        }

        var timeline = tao('div', 'timeline');
        activities.forEach(function (act) {
            var item = tao('div', 'timeline-item');
            var badge = tao('div', 'timeline-badge');
            (act.colorClass || '').split(/\s+/).forEach(function (c) { if (c) badge.classList.add(c); });
            var icon = tao('i');
            (act.icon || '').split(/\s+/).forEach(function (c) { if (c) icon.classList.add(c); });
            badge.appendChild(icon);
            item.appendChild(badge);

            var noiDung = tao('div', 'ms-2');
            noiDung.appendChild(tao('div', 'fw-bold fs-4', act.title));
            noiDung.appendChild(tao('div', 'text-secondary small', act.description));
            var thoiGian = tao('span', 'badge bg-secondary-lt text-secondary mt-1');
            thoiGian.appendChild(tao('i', 'ti ti-clock me-1'));
            thoiGian.appendChild(document.createTextNode(act.timeText || ''));
            noiDung.appendChild(thoiGian);
            item.appendChild(noiDung);

            timeline.appendChild(item);
        });
        box.appendChild(timeline);
    }

    function reload() {
        var $selects = $('#branchSelect, #monthSelect, #yearSelect');
        var branchId = $('#branchSelect').val();
        var month = $('#monthSelect').val();
        var year = $('#yearSelect').val();

        $selects.prop('disabled', true);
        $('#dashboardLoading').removeClass('d-none');

        $.ajax({
            url: cfg.urlAjax,
            type: 'GET',
            data: { branchId: branchId, month: month, year: year }
        }).done(function (res) {
            if (res.coXemDoanhThu) {
                $('#chartYearText').text(year);
                $('#kpiDaThuTitle').text('Đã thu kỳ T' + month + '/' + year);
                $('#statDoanhThu').text(dinhDangTien(res.doanhThuThangNay));
            }
            $('#kpiChoThuTitle').text('Chờ thu kỳ T' + month + '/' + year);
            var ky = 'T' + month + '/' + year;
            var links = res.links || {};
            datLink('cardQuaHan', links.quaHan);
            datLink('cardChoThu', links.choThu, 'Mở danh sách hóa đơn chưa thu đủ của kỳ ' + ky);
            datLink('cardPhongDangNo', links.phongDangNo);
            if (res.coXemDoanhThu) {
                datLink('cardDaThu', links.daThuKy, 'Mở báo cáo doanh thu của kỳ ' + ky);
                datLink('linkBaoCaoNam', links.baoCaoNam, 'Mở báo cáo doanh thu năm ' + year);
            }
            if (res.tienMatCuaToi && document.getElementById('statTienMat')) {
                $('#kpiTienMatTitle').text('Tiền mặt bạn đã thu ' + ky);
                $('#statTienMat').text(dinhDangTien(res.tienMatCuaToi.tongTien));
                $('#statTienMatChuThich').text('Tháng ' + month + '/' + year + ', chưa gồm tiền giữ chỗ');
                datLink('cardTienMat', links.tienMatCuaToi, 'Mở lịch sử thanh toán tiền mặt bạn đã thu trong kỳ ' + ky);
            }
            renderTienMatNhanVien(res.tienMatNhanVien, ky);
            renderChoDuyet(res.choDuyet);
            $('#statChoThu').text(dinhDangTien(res.tongTienChoThu));
            $('#statQuaHan').text(dinhDangTien(res.tienQuaHan));
            $('#statSoHoaDonQuaHan').text((Number(res.soHoaDonQuaHan) || 0) + ' hóa đơn');
            $('#statChuaThanhToan').text(res.soPhongChuaThanhToan);
            $('#statHopDong').text(res.tongSoHopDongHoatDong);
            $('#statTyLeLapDay').text(dinhDangPhanTram(res.tyLeLapDay));
            $('#statPhongLapDay').text(res.soPhongDaThue + '/' + res.tongSoPhong + ' phòng');

            if (revenueChart && res.coXemDoanhThu) {
                revenueChart.data.labels = res.chartLabels;
                revenueChart.data.datasets[0].data = res.chartData;
                revenueChart.data.datasets[1].data = res.chartDataChoThu;
                revenueChart.update();
            }

            roomChart.data.labels = res.roomStatusLabels;
            roomChart.data.datasets[0].data = res.roomStatusData;
            roomChart.update();

            renderViec(res.viecCanLam || { tongSo: 0, items: [] }, branchId);
            renderHoatDong(res.recentActivities);
        }).fail(function (xhr) {
            console.error('Không tải được dữ liệu Dashboard:', xhr);
            window.showError('Không tải được dữ liệu Bảng điều khiển. Vui lòng thử lại.');
        }).always(function () {
            $selects.prop('disabled', false);
            $('#dashboardLoading').addClass('d-none');
        });
    }

    $('#branchSelect, #monthSelect, #yearSelect').on('change', reload);

    // Nút đổi theme chỉ đổi data-bs-theme, không tải lại trang: cập nhật màu biểu đồ theo.
    new MutationObserver(apDungMauTheme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });
})(window, jQuery);
