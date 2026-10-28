// Thanh toán trực tuyến và cập nhật thời gian thực cho giữ chỗ (spec giữ chỗ §4, §5, §8).
// GuestDatCoc.start: Tạo link VNPay và chuyển hướng khách sang cổng thanh toán an toàn.
// GuestDatCoc.watchResult: Nghe sự kiện SignalR GiuChoThanhToanCapNhat để tự động tải lại trang.
(function (window, $) {
    'use strict';

    var DEFAULT_ERROR = 'Không tạo được link thanh toán VNPay, vui lòng thử lại.';

    function getVerificationToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function initTabs() {
        var tabButtons = document.querySelectorAll('.guest-pay-tab-btn');
        var tabPanels = document.querySelectorAll('.guest-pay-tab-panel');

        tabButtons.forEach(function (button) {
            button.addEventListener('click', function () {
                var target = this.getAttribute('data-tab-target');

                tabButtons.forEach(function (btn) {
                    btn.classList.remove('active');
                });
                tabPanels.forEach(function (panel) {
                    panel.classList.remove('active');
                    panel.style.display = 'none';
                });

                this.classList.add('active');
                var activePanel = document.querySelector(target);
                if (activePanel) {
                    activePanel.classList.add('active');
                    activePanel.style.display = 'block';
                }
            });
        });
    }

    function initCopyButtons() {
        document.querySelectorAll('.guest-copy-btn').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var text = this.getAttribute('data-copy');
                if (!text) {
                    return;
                }

                var self = this;
                var originalTitle = self.getAttribute('title') || 'Sao chép';
                navigator.clipboard.writeText(text).then(function () {
                    self.textContent = 'Đã chép!';
                    setTimeout(function () {
                        self.textContent = 'Sao chép';
                        self.setAttribute('title', originalTitle);
                    }, 2000);
                }).catch(function () {
                    // Fallback nếu clipboard API bị từ chối
                    var tempInput = document.createElement('input');
                    tempInput.value = text;
                    document.body.appendChild(tempInput);
                    tempInput.select();
                    document.execCommand('copy');
                    document.body.removeChild(tempInput);
                    self.textContent = 'Đã chép!';
                    setTimeout(function () {
                        self.textContent = 'Sao chép';
                    }, 2000);
                });
            });
        });
    }

    function start(reservationId, buttonEl) {
        var $button = $(buttonEl);
        if ($button.prop('disabled')) {
            return;
        }

        var originalHtml = $button.html();
        $button.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang chuyển sang VNPay...');

        function fail(message) {
            $button.prop('disabled', false).html(originalHtml);
            if (typeof Swal !== 'undefined') {
                Swal.fire({ icon: 'error', title: 'Chưa thanh toán được', text: message || DEFAULT_ERROR });
            } else {
                alert(message || DEFAULT_ERROR);
            }
        }

        $.ajax({
            url: '/tai-khoan/yeu-cau-phong/giu-cho/' + encodeURIComponent(reservationId) + '/dat-coc/vnpay',
            method: 'POST',
            headers: { 'RequestVerificationToken': getVerificationToken() },
            dataType: 'json'
        }).done(function (res) {
            if (res && res.success && typeof res.url === 'string' && res.url.indexOf('https://') === 0) {
                window.location.assign(res.url);
                return;
            }

            fail(res && res.message);
        }).fail(function (xhr) {
            fail(xhr && xhr.responseJSON && xhr.responseJSON.message);
        });
    }

    function watchResult(reservationId) {
        if (typeof signalR !== 'undefined') {
            var connection = new signalR.HubConnectionBuilder()
                .withUrl('/thongBaoHub')
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            connection.on('GiuChoThanhToanCapNhat', function (e) {
                if (e && e.yeuCauGiuChoId === reservationId) {
                    var statusEl = document.getElementById('paymentSignalRStatus');
                    if (statusEl) {
                        statusEl.className = 'alert alert-success d-flex align-items-center py-2 px-3 mt-3';
                        statusEl.textContent = 'Đã nhận thanh toán thành công! Đang tải lại trang...';
                    }

                    if (typeof Swal !== 'undefined') {
                        Swal.fire({
                            icon: 'success',
                            title: 'Đã nhận thanh toán!',
                            text: 'Giao dịch giữ chỗ đã được hệ thống ghi nhận. Đang tải lại...',
                            timer: 2000,
                            showConfirmButton: false
                        }).then(function () {
                            window.location.reload();
                        });
                    } else {
                        window.location.reload();
                    }
                }
            });

            connection.start().catch(function (err) {
                console.error('Không thể kết nối SignalR thông báo:', err);
            });

            // Xử lý khi người dùng chuyển ứng dụng ngân hàng quay lại trình duyệt
            document.addEventListener('visibilitychange', function () {
                if (document.visibilityState === 'visible' && connection) {
                    if (connection.state === signalR.HubConnectionState.Disconnected) {
                        connection.start().catch(function (err) {
                            console.warn('Kết nối lại SignalR thất bại:', err);
                        });
                    }
                }
            });
        }

        $('#guestVnPayRecheck').on('click', function () {
            $(this).prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang kiểm tra...');
            window.location.reload();
        });
    }

    function init(reservationId) {
        initTabs();
        initCopyButtons();
        watchResult(reservationId);

        $('#btnGuestVnPay').on('click', function () {
            start(reservationId, this);
        });
    }

    window.GuestDatCoc = {
        init: init,
        start: start,
        watchResult: watchResult
    };
})(window, window.jQuery || {
    // Fallback nếu trang không nạp jQuery (dùng native fetch & DOM)
    ajax: function (opts) {
        var token = opts.headers ? opts.headers['RequestVerificationToken'] : '';
        var promise = fetch(opts.url, {
            method: opts.method || 'GET',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            }
        }).then(function (resp) {
            return resp.json().then(function (data) {
                if (!resp.ok) {
                    var err = new Error(data && data.message ? data.message : 'HTTP ' + resp.status);
                    err.responseJSON = data;
                    throw err;
                }
                return data;
            });
        });

        return {
            done: function (cb) {
                promise.then(cb);
                return this;
            },
            fail: function (cb) {
                promise.catch(cb);
                return this;
            }
        };
    }
});
