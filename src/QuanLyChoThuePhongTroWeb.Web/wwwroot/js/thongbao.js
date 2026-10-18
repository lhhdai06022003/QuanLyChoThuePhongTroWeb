// Cho trang khác đăng ký sự kiện trên kết nối ThongBaoHub sẵn có (spec thanh toán trực tuyến §3.3). Đăng ký trước hay sau khi kết nối đều được.
window.ThongBaoHub = (function () {
    var handlers = [], connection = null;
    return {
        on: function (eventName, handler) { handlers.push([eventName, handler]); if (connection) connection.on(eventName, handler); },
        attach: function (conn) { connection = conn; handlers.forEach(function (h) { conn.on(h[0], h[1]); }); }
    };
})();

$(document).ready(function () {
    if (typeof signalR !== 'undefined') {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/thongBaoHub")
            .withAutomaticReconnect()
            .build();

        window.ThongBaoHub.attach(connection);

        connection.on("NhanThongBao", function (thongBao) {
            // Hiển thị popup toast
            Swal.fire({
                toast: true,
                position: 'bottom-end',
                icon: 'info',
                // titleText thay cho title: SweetAlert2 hiểu title là HTML.
                titleText: thongBao.tieuDe,
                text: thongBao.noiDung,
                showConfirmButton: false,
                timer: 5000,
                timerProgressBar: true,
                customClass: {
                    popup: 'colored-toast'
                },
                didOpen: (toast) => {
                    toast.addEventListener('mouseenter', Swal.stopTimer)
                    toast.addEventListener('mouseleave', Swal.resumeTimer)
                }
            });

            // Cập nhật lại danh sách thông báo
            loadThongBaos();
        });

        connection.start().catch(function (err) {
            return console.error(err.toString());
        });

        // Load danh sách thông báo khi tải trang
        loadThongBaos();
    }
});

// Tiêu đề và nội dung thông báo có thể chứa chữ do khách thuê nhập (sự cố), phải escape trước khi chèn HTML.
function escapeHtml(value) {
    return String(value == null ? '' : value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// Chỉ nhận đường dẫn nội bộ dạng "/..."; chặn "//host", "javascript:" và URL tuyệt đối.
function safeLink(url) {
    return typeof url === 'string' && url.charAt(0) === '/' && url.charAt(1) !== '/' && url.charAt(1) !== '\\' ? url : '#';
}

// Màu/biểu tượng theo lĩnh vực của thông báo (HoaDon, DienNuoc, SuCo; còn lại dùng chuông).
function iconThongBao(linhVuc) {
    switch (linhVuc) {
        case 'HoaDon': return { bg: 'bg-blue-lt', icon: 'ti-file-invoice' };
        case 'DienNuoc': return { bg: 'bg-yellow-lt', icon: 'ti-bolt' };
        case 'SuCo': return { bg: 'bg-orange-lt', icon: 'ti-tool' };
        case 'ThanhToan': return { bg: 'bg-green-lt', icon: 'ti-cash' };
        default: return { bg: 'bg-secondary-lt', icon: 'ti-bell' };
    }
}

// Bấm dòng hoặc tiêu đề: đánh dấu đã đọc rồi chuyển trang. Ctrl/Cmd/Shift/chuột giữa trên link vẫn mở tab mới.
$(document).on('click auxclick', '#list-thong-bao .list-group-item[data-id]', function (e) {
    if (e.type === 'auxclick' && e.which !== 2) return;
    var id = $(this).data('id');
    var url = safeLink($(this).attr('data-link'));
    var moTabMoi = e.type === 'auxclick' || e.ctrlKey || e.metaKey || e.shiftKey;
    if (moTabMoi) {
        // Trúng link thì trình duyệt tự mở tab mới, ngoài link thì không làm gì; cả hai chỉ đánh dấu đã đọc, không chuyển trang hiện tại.
        markAsRead(id, '#');
        return;
    }
    e.preventDefault();
    markAsRead(id, url);
});

$(document).on('click', '#btn-danh-dau-tat-ca', function (e) {
    e.preventDefault();
    e.stopPropagation();
    markAllAsRead();
});

function loadThongBaos() {
    $.get('/api/ThongBao/LayMoiNhat', function (data) {
        var $list = $('#list-thong-bao');
        $list.empty();

        var unreadCount = 0;

        if (!data || data.length === 0) {
            $list.append('<div class="list-group-item text-center text-secondary py-4"><i class="ti ti-bell-off fs-1 d-block mb-1"></i>Không có thông báo nào.</div>');
        } else {
            data.forEach(function (tb) {
                if (!tb.isRead) unreadCount++;

                var kind = iconThongBao(tb.linhVuc);
                var link = escapeHtml(safeLink(tb.linkDieuHuong));
                var dot = tb.isRead ? '<span class="d-block" style="width: .5rem; height: .5rem;"></span>' : '<span class="status-dot status-dot-animated bg-red d-block"></span>';
                var html = `
                    <div class="list-group-item" data-id="${Number(tb.id)}" data-link="${link}" style="cursor:pointer">
                        <div class="row align-items-center">
                            <div class="col-auto">${dot}</div>
                            <div class="col-auto"><span class="avatar avatar-sm ${kind.bg}"><i class="ti ${kind.icon}"></i></span></div>
                            <div class="col">
                                <a href="${link}" class="text-body d-block text-wrap ${tb.isRead ? '' : 'fw-bold'}">${escapeHtml(tb.tieuDe)}</a>
                                <div class="text-secondary tb-notif-text">${escapeHtml(tb.noiDung)}</div>
                                <div class="small text-secondary mt-1"><i class="ti ti-clock me-1"></i>${escapeHtml(tb.createdAt)}</div>
                            </div>
                        </div>
                    </div>
                `;
                $list.append(html);
            });
        }

        updateBadge(unreadCount);
    }).fail(function (xhr) {
        console.warn('Không tải được thông báo.', xhr && xhr.status);
        $('#list-thong-bao').html('<div class="list-group-item text-center text-secondary py-4">Không tải được thông báo.</div>');
    });
}

function updateBadge(count) {
    var $badge = $('#badge-thong-bao');
    var $headerBadge = $('#badge-thong-bao-header');
    var $markAll = $('#btn-danh-dau-tat-ca');
    if (count > 0) {
        var text = count > 99 ? '99+' : count;
        $badge.text(text).removeClass('d-none');
        $headerBadge.text(text).removeClass('d-none');
        $markAll.removeClass('d-none');
    } else {
        $badge.addClass('d-none');
        $headerBadge.addClass('d-none');
        $markAll.addClass('d-none');
    }
}

// url: nơi chuyển tới sau khi đánh dấu ('#' = chỉ tải lại danh sách). Lỗi đánh dấu không chặn việc chuyển trang.
function markAsRead(id, url) {
    $.post('/api/ThongBao/DanhDauDaDoc/' + id)
        .fail(function (xhr) { console.warn('Không đánh dấu được thông báo đã đọc.', xhr && xhr.status); })
        .always(function () {
            url = safeLink(url);
            if (url !== '#') {
                window.location.href = url;
            } else {
                loadThongBaos();
            }
        });
}

function markAllAsRead() {
    var $btn = $('#btn-danh-dau-tat-ca').prop('disabled', true);
    $.post('/api/ThongBao/DanhDauTatCaDaDoc')
        .done(function () { loadThongBaos(); })
        .fail(function (xhr) {
            console.warn('Không đánh dấu được tất cả thông báo đã đọc.', xhr && xhr.status);
            if (window.Swal) {
                Swal.fire({ toast: true, position: 'bottom-end', icon: 'error', titleText: 'Không đánh dấu được thông báo đã đọc.', showConfirmButton: false, timer: 3000 });
            }
        })
        .always(function () { $btn.prop('disabled', false); });
}
