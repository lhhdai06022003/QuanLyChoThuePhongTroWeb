(function (window, $) {
    'use strict';

    var cfg = window.baoCaoConfig || {};
    var TABS = ['doanh-thu', 'cong-no', 'cong-suat', 'hop-dong', 'dien-nuoc'];
    var fmtTien = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' });
    var fmtSo = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 });
    var fmtNguyen = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
    var fmtTyLe = new Intl.NumberFormat('vi-VN', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

    var state = {
        current: TABS.indexOf(cfg.tab) >= 0 ? cfg.tab : 'doanh-thu',
        filter: null,        // bộ lọc đã chốt lúc bấm "Xem"
        stale: {},           // tab cần tải lại
        used: {},            // bộ lọc của lần tải thành công gần nhất theo tab
        seq: {},             // số thứ tự yêu cầu, bỏ qua phản hồi cũ
        charts: {}
    };
    TABS.forEach(function (t) { state.stale[t] = true; state.seq[t] = 0; });

    function tien(n) { return fmtTien.format(Number(n) || 0); }
    function so(n) { return fmtSo.format(Number(n) || 0); }
    function nguyen(n) { return fmtNguyen.format(Number(n) || 0); }
    function tyLe(n) { return n === null || n === undefined ? '—' : fmtTyLe.format(Number(n)) + '%'; }

    function tao(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    function mauTheme() {
        var css = window.getComputedStyle(document.body);
        return {
            chu: (css.getPropertyValue('--tblr-secondary-color') || '').trim() || css.color,
            luoi: (css.getPropertyValue('--tblr-border-color') || '').trim() || 'rgba(0, 0, 0, 0.1)'
        };
    }

    function layBoLoc() {
        var chiNhanh = $('#bcChiNhanh').val();
        var tu = String($('#bcTuKy').val() || '').split('-');
        var den = String($('#bcDenKy').val() || '').split('-');
        var f = {
            chiNhanhId: chiNhanh ? chiNhanh : '',
            tuNam: parseInt(tu[0], 10), tuThang: parseInt(tu[1], 10),
            denNam: parseInt(den[0], 10), denThang: parseInt(den[1], 10)
        };
        f.hopLe = !isNaN(f.tuNam) && !isNaN(f.tuThang) && !isNaN(f.denNam) && !isNaN(f.denThang);
        return f;
    }

    function thamSo(tab, f) {
        var p = { tab: tab, tuThang: f.tuThang, tuNam: f.tuNam, denThang: f.denThang, denNam: f.denNam };
        if (f.chiNhanhId) p.chiNhanhId = f.chiNhanhId;
        return p;
    }

    function linkHoaDon(extra, f) {
        var p = $.extend({}, extra);
        if (f && f.chiNhanhId) p.chiNhanhId = f.chiNhanhId;
        return cfg.urlHoaDon + '?' + $.param(p);
    }

    // ----- DOM helpers -----

    function veThe(id, items) {
        var host = document.getElementById(id);
        $(host).empty();
        items.forEach(function (it) {
            var col = tao('div', 'col-6 col-md-4 col-xl-2');
            var card = tao('div', 'card h-100');
            var body = tao('div', 'card-body');
            body.appendChild(tao('div', 'subheader', it.label));
            var val = tao('div', 'h2 mb-1');
            if (it.href) {
                var a = tao('a', null, it.value);
                a.href = it.href;
                val.appendChild(a);
            } else {
                val.textContent = it.value;
            }
            body.appendChild(val);
            (it.sub || []).forEach(function (line) { body.appendChild(tao('div', 'text-secondary small', line)); });
            card.appendChild(body);
            col.appendChild(card);
            host.appendChild(col);
        });
    }

    // cols: [{ h: tiêu đề, f: function(row) -> chuỗi hoặc Node, so: true nếu căn phải }]
    function veBang(id, cols, rows, dongCuoi) {
        var table = document.getElementById(id);
        $(table).empty();
        var thead = tao('thead');
        var htr = tao('tr');
        cols.forEach(function (c) { htr.appendChild(tao('th', c.so ? 'text-end' : null, c.h)); });
        thead.appendChild(htr);
        table.appendChild(thead);
        var tbody = tao('tbody');
        if (!rows.length) {
            var tr0 = tao('tr');
            var td0 = tao('td', 'text-center text-secondary', 'Không có dữ liệu');
            td0.colSpan = cols.length;
            tr0.appendChild(td0);
            tbody.appendChild(tr0);
        }
        var all = rows.map(function (r) { return { r: r, cuoi: false }; });
        if (dongCuoi) all.push({ r: dongCuoi, cuoi: true });
        all.forEach(function (x) {
            if (!rows.length) return;
            var tr = tao('tr', x.cuoi ? 'fw-bold' : null);
            cols.forEach(function (c) {
                var td = tao('td', c.so ? 'text-end' : null);
                var v = c.f(x.r);
                if (v instanceof Node) td.appendChild(v); else td.textContent = v;
                tr.appendChild(td);
            });
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
    }

    function veBieuDo(tab, canvasId, config) {
        if (state.charts[tab]) { state.charts[tab].destroy(); state.charts[tab] = null; }
        var canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === 'undefined') return;
        var m = mauTheme();
        var scales = config.options.scales || {};
        Object.keys(scales).forEach(function (k) {
            scales[k].ticks = $.extend({ color: m.chu }, scales[k].ticks);
            scales[k].grid = $.extend({ color: m.luoi }, scales[k].grid);
        });
        config.options.scales = scales;
        config.options.responsive = true;
        config.options.maintainAspectRatio = false;
        config.options.plugins = $.extend({ legend: { labels: { color: m.chu } } }, config.options.plugins);
        state.charts[tab] = new Chart(canvas.getContext('2d'), config);
    }

    function link(text, href) {
        var a = tao('a', null, text);
        a.href = href;
        return a;
    }

    function phamVi(pv) {
        var text = 'Chi nhánh: ' + pv.tenChiNhanh;
        if (pv.tuKyText) text += ' · Kỳ: ' + pv.tuKyText + (pv.denKyText && pv.denKyText !== pv.tuKyText ? ' – ' + pv.denKyText : '');
        text += ' · Tính lúc ' + pv.thoiDiemTinhText;
        $('#bcPhamVi').text(text);
    }

    // ----- Render từng tab -----

    function renderDoanhThu(d, f) {
        phamVi(d.phamVi);
        var t = d.tong;
        var subDaThu = ['Trong đó cấn trừ cọc: ' + tien(t.canTruCoc)];
        if (t.tienThua > 0) subDaThu.push('Tiền thừa: ' + tien(t.tienThua));
        veThe('cards-doanh-thu', [
            { label: 'Phát sinh', value: tien(t.phatSinh), sub: [nguyen(t.soHoaDon) + ' hóa đơn đã gửi'] },
            { label: 'Đã thu', value: tien(t.daThu), sub: subDaThu },
            { label: 'Còn nợ', value: tien(t.conNo) },
            { label: 'Tỷ lệ thu', value: tyLe(t.tyLeThu) },
            { label: 'Tiền cọc đang giữ', value: tien(d.tienCocDangGiu), sub: ['Tiền giữ hộ khách, có thể phải trả lại; không phải doanh thu.'] },
            { label: 'Thu khác: tiền giữ chỗ không hoàn', value: tien(t.thuKhacGiuCho), sub: ['Đang chờ quyết định: ' + tien(d.giuChoDangChoQuyetDinh)] }
        ]);
        veBieuDo('doanh-thu', 'chart-doanh-thu', {
            type: 'bar',
            data: {
                labels: d.theoKy.map(function (r) { return r.nhan; }),
                datasets: [
                    { label: 'Thu tiền', data: d.theoKy.map(function (r) { return r.thuTien; }), backgroundColor: '#2fb344' },
                    { label: 'Cấn trừ cọc', data: d.theoKy.map(function (r) { return r.canTruCoc; }), backgroundColor: '#4299e1' },
                    { label: 'Còn nợ', data: d.theoKy.map(function (r) { return r.conNo; }), backgroundColor: '#f59f00' }
                ]
            },
            options: {
                scales: {
                    x: { stacked: true },
                    y: { stacked: true, beginAtZero: true, ticks: { callback: function (v) { return nguyen(v); } } }
                },
                plugins: { tooltip: { callbacks: { label: function (c) { return c.dataset.label + ': ' + tien(c.parsed.y); } } } }
            }
        });

        function cotChung(nhanCot, conNoHref) {
            return [
                { h: nhanCot, f: function (r) { return r.nhan; } },
                { h: 'Phát sinh', so: true, f: function (r) { return tien(r.phatSinh); } },
                { h: 'Thu tiền', so: true, f: function (r) { return tien(r.thuTien); } },
                { h: 'Cấn trừ cọc', so: true, f: function (r) { return tien(r.canTruCoc); } },
                { h: 'Đã thu', so: true, f: function (r) { return tien(r.daThu); } },
                { h: 'Còn nợ', so: true, f: function (r) { var h = conNoHref && r.conNo > 0 ? conNoHref(r) : null; return h ? link(tien(r.conNo), h) : tien(r.conNo); } },
                { h: 'Tỷ lệ thu', so: true, f: function (r) { return tyLe(r.tyLeThu); } },
                { h: 'Tiền mặt', so: true, f: function (r) { return tien(r.tienMat); } },
                { h: 'Chuyển khoản', so: true, f: function (r) { return tien(r.chuyenKhoan); } },
                { h: 'SePay', so: true, f: function (r) { return tien(r.sePay); } },
                { h: 'VNPay', so: true, f: function (r) { return tien(r.vnPay); } },
                { h: 'Thu khác', so: true, f: function (r) { return tien(r.thuKhacGiuCho); } }
            ];
        }
        // Chỉ dòng của từng kỳ mới có link: màn hóa đơn lọc theo một tháng, không lọc được khoảng kỳ.
        veBang('bang-doanh-thu-ky', cotChung('Kỳ', function (r) {
            if (r.thang == null || r.nam == null) return null;
            return linkHoaDon({ thang: r.thang, nam: r.nam, trangThaiPhatHanh: 3 }, f);
        }), d.theoKy, t);
        $('#wrap-doanh-thu-cn').toggleClass('d-none', !d.theoChiNhanh.length);
        veBang('bang-doanh-thu-cn', cotChung('Chi nhánh', null), d.theoChiNhanh);
    }

    function renderCongNo(d, f) {
        phamVi(d.phamVi);
        veThe('cards-cong-no', [
            { label: 'Tổng còn nợ', value: tien(d.tongConNo), sub: [nguyen(d.soHoaDonNo) + ' hóa đơn'] },
            { label: 'Quá hạn', value: tien(d.quaHan), sub: [nguyen(d.soHoaDonQuaHan) + ' hóa đơn'] },
            { label: 'Số khách nợ', value: nguyen(d.soKhachNo) },
            { label: 'Nợ của khách đã trả phòng', value: tien(d.noKhachDaTraPhong), href: linkHoaDon({ thang: 0, nam: 0, trangThaiPhatHanh: 3, hopDongDaKetThucConNo: 1 }, f) }
        ]);
        veBieuDo('cong-no', 'chart-cong-no', {
            type: 'bar',
            data: {
                labels: d.nhomTuoiNo.map(function (n) { return n.nhan; }),
                datasets: [{ label: 'Số tiền còn nợ', data: d.nhomTuoiNo.map(function (n) { return n.soTien; }), backgroundColor: '#f59f00' }]
            },
            options: {
                scales: { y: { beginAtZero: true, ticks: { callback: function (v) { return nguyen(v); } } } },
                plugins: { tooltip: { callbacks: { label: function (c) { return tien(c.parsed.y) + ' (' + d.nhomTuoiNo[c.dataIndex].soHoaDon + ' hóa đơn)'; } } } }
            }
        });
        veBang('bang-cong-no', [
            {
                h: 'Khách thuê', f: function (r) {
                    var wrap = tao('span');
                    wrap.appendChild(link(r.hoVaTen, cfg.urlNguoiThue + r.nguoiThueId));
                    if (r.daTraPhong) wrap.appendChild(tao('span', 'badge bg-secondary-lt ms-2', 'Đã trả phòng'));
                    return wrap;
                }
            },
            { h: 'Phòng', f: function (r) { return r.soPhong; } },
            { h: 'Chi nhánh', f: function (r) { return r.tenChiNhanh; } },
            { h: 'Số hóa đơn', so: true, f: function (r) { return nguyen(r.soHoaDon); } },
            { h: 'Tổng nợ', so: true, f: function (r) { return tien(r.tongNo); } },
            { h: 'Kỳ nợ cũ nhất', f: function (r) { return r.kyNoCuNhat; } },
            { h: 'Quá hạn lâu nhất', so: true, f: function (r) { return r.soNgayQuaHanLonNhat === null ? '—' : nguyen(r.soNgayQuaHanLonNhat) + ' ngày'; } }
        ], d.theoKhach);
    }

    function renderCongSuat(d) {
        phamVi(d.phamVi);
        var dau = d.phongTrongLauNhat.length ? d.phongTrongLauNhat[0] : null;
        var subTrong = d.phongTrongLauNhat.map(function (p) { return p.soPhong + ' · ' + p.tenChiNhanh + ': ' + nguyen(p.soNgayTrong) + ' ngày'; });
        veThe('cards-cong-suat', [
            { label: 'Tổng phòng', value: nguyen(d.tongPhong) },
            { label: 'Đang thuê', value: nguyen(d.dangThue) },
            { label: 'Trống', value: nguyen(d.trong) },
            { label: 'Bảo trì', value: nguyen(d.baoTri) },
            { label: 'Tỷ lệ lấp đầy', value: tyLe(d.tong.tyLeLapDay), sub: [nguyen(d.tong.ngayPhongDaThue) + '/' + nguyen(d.tong.ngayPhongCoTheThue) + ' ngày-phòng'] },
            { label: 'Phòng trống lâu nhất', value: dau ? dau.soPhong + ' · ' + nguyen(dau.soNgayTrong) + ' ngày' : '—', sub: subTrong }
        ]);
        veBieuDo('cong-suat', 'chart-cong-suat', {
            type: 'line',
            data: {
                labels: d.theoKy.map(function (r) { return r.nhan; }),
                datasets: [{ label: 'Tỷ lệ lấp đầy (%)', data: d.theoKy.map(function (r) { return r.tyLeLapDay; }), borderColor: '#206bc4', backgroundColor: '#206bc4', tension: 0.2, spanGaps: false }]
            },
            options: { scales: { y: { beginAtZero: true, suggestedMax: 100, ticks: { callback: function (v) { return v + '%'; } } } } }
        });
        function cot(nhan) {
            return [
                { h: nhan, f: function (r) { return r.nhan; } },
                { h: 'Ngày-phòng có thể cho thuê', so: true, f: function (r) { return nguyen(r.ngayPhongCoTheThue); } },
                { h: 'Ngày-phòng đã thuê', so: true, f: function (r) { return nguyen(r.ngayPhongDaThue); } },
                { h: 'Tỷ lệ lấp đầy', so: true, f: function (r) { return tyLe(r.tyLeLapDay); } }
            ];
        }
        veBang('bang-cong-suat-ky', cot('Kỳ'), d.theoKy, d.tong);
        $('#wrap-cong-suat-cn').toggleClass('d-none', !d.theoChiNhanh.length);
        veBang('bang-cong-suat-cn', cot('Chi nhánh'), d.theoChiNhanh);
        veBang('bang-cong-suat-trong', [
            { h: 'Phòng', f: function (r) { return r.soPhong; } },
            { h: 'Chi nhánh', f: function (r) { return r.tenChiNhanh; } },
            { h: 'Trống từ', f: function (r) { return r.tuNgayText; } },
            { h: 'Số ngày trống', so: true, f: function (r) { return nguyen(r.soNgayTrong); } }
        ], d.phongTrongLauNhat);
    }

    function renderHopDong(d, f) {
        phamVi(d.phamVi);
        var qs = function (boLoc) {
            var p = { boLoc: boLoc };
            if (f && f.chiNhanhId) p.chiNhanhId = f.chiNhanhId;
            return cfg.urlHopDong + '?' + $.param(p);
        };
        veThe('cards-hop-dong', [
            { label: 'Đang hoạt động', value: nguyen(d.dangHoatDong) },
            { label: 'Quá hạn chưa xử lý', value: nguyen(d.quaHanChuaXuLy), href: qs('QuaHan') },
            { label: 'Sắp hết hạn (30 ngày)', value: nguyen(d.sapHetHan), href: qs('SapHetHan') },
            { label: 'Hợp đồng mới', value: nguyen(d.tong.hopDongMoi) },
            { label: 'Gia hạn', value: nguyen(d.tong.giaHan) },
            { label: 'Tỷ lệ gia hạn', value: tyLe(d.tong.tyLeGiaHan), sub: [nguyen(d.tong.hetHanCoGiaHan) + '/' + nguyen(d.tong.hetHanDaKetThuc) + ' hợp đồng hết hạn'] }
        ]);
        veBieuDo('hop-dong', 'chart-hop-dong', {
            type: 'bar',
            data: {
                labels: d.theoKy.map(function (r) { return r.nhan; }),
                datasets: [
                    { label: 'Mới', data: d.theoKy.map(function (r) { return r.hopDongMoi; }), backgroundColor: '#2fb344' },
                    { label: 'Gia hạn', data: d.theoKy.map(function (r) { return r.giaHan; }), backgroundColor: '#4299e1' },
                    { label: 'Trả phòng', data: d.theoKy.map(function (r) { return r.traPhong; }), backgroundColor: '#d63939' }
                ]
            },
            options: { scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
        });
        veBang('bang-hop-dong', [
            { h: 'Kỳ', f: function (r) { return r.nhan; } },
            { h: 'Hợp đồng mới', so: true, f: function (r) { return nguyen(r.hopDongMoi); } },
            { h: 'Gia hạn', so: true, f: function (r) { return nguyen(r.giaHan); } },
            { h: 'Trả phòng', so: true, f: function (r) { return nguyen(r.traPhong); } },
            { h: 'Hết hạn trong kỳ', so: true, f: function (r) { return nguyen(r.hetHanTrongKy); } },
            { h: 'Hết hạn đã kết thúc', so: true, f: function (r) { return nguyen(r.hetHanDaKetThuc); } },
            { h: 'Trong đó có gia hạn', so: true, f: function (r) { return nguyen(r.hetHanCoGiaHan); } },
            { h: 'Tỷ lệ gia hạn', so: true, f: function (r) { return tyLe(r.tyLeGiaHan); } }
        ], d.theoKy, d.tong);
    }

    function renderDienNuoc(d) {
        phamVi(d.phamVi);
        var subBoQua = d.soBanGhiBoQua > 0 ? ['Bản ghi bỏ qua: ' + nguyen(d.soBanGhiBoQua) + ' (chỉ số mới nhỏ hơn chỉ số cũ)'] : [];
        veThe('cards-dien-nuoc', [
            { label: 'Tổng kWh', value: so(d.tongKwh), sub: subBoQua },
            { label: 'Tổng m³', value: so(d.tongM3) },
            { label: 'Tiền điện', value: tien(d.tienDien) },
            { label: 'Tiền nước', value: tien(d.tienNuoc) },
            { label: 'TB kWh/phòng/tháng', value: d.trungBinhKwhPhongThang === null ? '—' : so(d.trungBinhKwhPhongThang), sub: [nguyen(d.soBanGhi) + ' bản ghi'] }
        ]);
        veBieuDo('dien-nuoc', 'chart-dien-nuoc', {
            type: 'line',
            data: {
                labels: d.theoKy.map(function (r) { return r.nhan; }),
                datasets: [
                    { label: 'kWh', data: d.theoKy.map(function (r) { return r.kwh; }), borderColor: '#f59f00', backgroundColor: '#f59f00', yAxisID: 'y', tension: 0.2 },
                    { label: 'm³', data: d.theoKy.map(function (r) { return r.m3; }), borderColor: '#206bc4', backgroundColor: '#206bc4', yAxisID: 'y1', tension: 0.2 }
                ]
            },
            options: {
                scales: {
                    y: { beginAtZero: true, position: 'left', title: { display: true, text: 'kWh' } },
                    y1: { beginAtZero: true, position: 'right', title: { display: true, text: 'm³' }, grid: { drawOnChartArea: false } }
                }
            }
        });
        veBang('bang-dien-nuoc-ky', [
            { h: 'Kỳ', f: function (r) { return r.nhan; } },
            { h: 'kWh', so: true, f: function (r) { return so(r.kwh); } },
            { h: 'm³', so: true, f: function (r) { return so(r.m3); } },
            { h: 'Tiền điện', so: true, f: function (r) { return tien(r.tienDien); } },
            { h: 'Tiền nước', so: true, f: function (r) { return tien(r.tienNuoc); } },
            { h: 'Số bản ghi', so: true, f: function (r) { return nguyen(r.soBanGhi); } },
            { h: 'Bỏ qua', so: true, f: function (r) { return nguyen(r.soBanGhiBoQua); } }
        ], d.theoKy);
        veBang('bang-dien-nuoc-phong', [
            { h: 'Phòng', f: function (r) { return r.soPhong; } },
            { h: 'Chi nhánh', f: function (r) { return r.tenChiNhanh; } },
            { h: 'kWh', so: true, f: function (r) { return so(r.kwh); } },
            { h: 'm³', so: true, f: function (r) { return so(r.m3); } },
            { h: 'Tiền điện', so: true, f: function (r) { return tien(r.tienDien); } },
            { h: 'Tiền nước', so: true, f: function (r) { return tien(r.tienNuoc); } },
            { h: 'Số kỳ', so: true, f: function (r) { return nguyen(r.soKy); } }
        ], d.topPhong);
    }

    var RENDER = {
        'doanh-thu': renderDoanhThu,
        'cong-no': renderCongNo,
        'cong-suat': renderCongSuat,
        'hop-dong': renderHopDong,
        'dien-nuoc': renderDienNuoc
    };

    // ----- Tải dữ liệu -----

    function dangTai(bat) {
        $('#bcLoading').toggleClass('d-none', !bat);
        $('#bcXem').prop('disabled', bat);
    }

    function taiTab(tab) {
        var f = state.filter;
        var seq = ++state.seq[tab];
        var $xuat = $('.bc-export[data-tab="' + tab + '"]').prop('disabled', true);
        dangTai(true);
        $.ajax({ url: cfg.urlDuLieu, type: 'GET', data: thamSo(tab, f), dataType: 'json' })
            .done(function (data) {
                if (seq !== state.seq[tab]) return;
                state.stale[tab] = false;
                state.used[tab] = f;
                RENDER[tab](data, f);
                $xuat.prop('disabled', false);
            })
            .fail(function (xhr) {
                if (seq !== state.seq[tab]) return;
                var msg = (xhr.responseJSON && xhr.responseJSON.message) || 'Không tải được báo cáo.';
                Swal.fire({ icon: 'error', text: msg });
            })
            .always(function () {
                if (seq === state.seq[tab]) dangTai(false);
            });
    }

    function moTab(tab) {
        state.current = tab;
        TABS.forEach(function (t) {
            $('#tab-' + t).toggleClass('active', t === tab);
            $('#pane-' + t).toggleClass('d-none', t !== tab);
        });
        var url = new URL(window.location.href);
        url.searchParams.set('tab', tab);
        window.history.replaceState(null, '', url.toString());
        if (state.stale[tab]) taiTab(tab);
    }

    $('.nav-tabs a[data-tab]').on('click', function (e) {
        e.preventDefault();
        moTab($(this).data('tab'));
    });

    $('#bcXem').on('click', function () {
        var f = layBoLoc();
        if (!f.hopLe) {
            Swal.fire({ icon: 'warning', text: 'Vui lòng chọn kỳ bắt đầu và kỳ kết thúc.' });
            return;
        }
        state.filter = f;
        TABS.forEach(function (t) { state.stale[t] = true; });
        taiTab(state.current);
    });

    $('.bc-export').on('click', function () {
        var tab = $(this).data('tab');
        var f = state.used[tab];
        if (!f) return;
        window.location = cfg.urlXuatExcel + '?' + $.param(thamSo(tab, f));
    });

    state.filter = layBoLoc();
    if (!state.filter.hopLe) {
        Swal.fire({ icon: 'warning', text: 'Vui lòng chọn kỳ bắt đầu và kỳ kết thúc.' });
    } else {
        moTab(state.current);
    }
})(window, jQuery);
