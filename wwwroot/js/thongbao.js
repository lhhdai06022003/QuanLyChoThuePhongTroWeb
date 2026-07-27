$(document).ready(function () {
    if (typeof signalR !== 'undefined') {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/thongBaoHub")
            .withAutomaticReconnect()
            .build();

        connection.on("NhanThongBao", function (thongBao) {
            // Hiển thị popup toast
            Swal.fire({
                toast: true,
                position: 'bottom-end',
                icon: 'info',
                title: thongBao.tieuDe,
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

function loadThongBaos() {
    $.get('/api/ThongBao/LayMoiNhat', function (data) {
        var $list = $('#list-thong-bao');
        $list.empty();
        
        var unreadCount = 0;

        if (data.length === 0) {
            $list.append('<div class="p-3 text-center text-muted small">Không có thông báo nào.</div>');
        } else {
            data.forEach(function (tb) {
                if (!tb.isRead) unreadCount++;
                
                var bgClass = tb.isRead ? '' : 'bg-blue-lt';
                var iconClass = tb.linhVuc === 'SuCo' ? 'fa-tools text-orange' : 'fa-info-circle text-blue';
                
                var html = `
                    <div class="list-group-item ${bgClass}" onclick="markAsRead(${tb.id}, '${tb.linkDieuHuong || '#'}')" style="cursor:pointer">
                        <div class="row align-items-center">
                            <div class="col-auto"><i class="fas ${iconClass} fa-lg"></i></div>
                            <div class="col text-truncate">
                                <div class="text-reset d-block fw-bold">${tb.tieuDe}</div>
                                <div class="d-block text-muted text-truncate mt-n1" style="font-size: 0.85rem;">${tb.noiDung}</div>
                                <div class="text-muted small mt-1" style="font-size: 0.75rem;"><i class="fas fa-clock me-1"></i>${tb.createdAt}</div>
                            </div>
                            ${!tb.isRead ? '<div class="col-auto"><span class="badge bg-blue">Mới</span></div>' : ''}
                        </div>
                    </div>
                `;
                $list.append(html);
            });
        }

        updateBadge(unreadCount);
    });
}

function updateBadge(count) {
    var $badge = $('#badge-thong-bao');
    if (count > 0) {
        $badge.text(count > 99 ? '99+' : count);
        $badge.removeClass('d-none');
    } else {
        $badge.addClass('d-none');
    }
}

function markAsRead(id, url) {
    $.post('/api/ThongBao/DanhDauDaDoc/' + id, function (res) {
        if (url && url !== '#') {
            window.location.href = url;
        } else {
            loadThongBaos();
        }
    });
}

function markAllAsReadUi() {
    // Có thể bổ sung tính năng mark all as read nếu cần
}
