// Thanh toán thẻ qua VNPay của khách thuê (spec VNPay §4.2, §4.3).
// start: xin server tạo link rồi chuyển khách sang VNPay (server không ghi gì vào DB, ghi sổ do IPN).
// watchResult: trang kết quả "đang ghi nhận" tự tải lại khi hệ thống ghi sổ xong hoặc khi khách bấm "Kiểm tra lại".
// Thông báo lỗi từ server hiển thị bằng text của SweetAlert2, không ghép thành HTML.
(function (window, $) {
    'use strict';

    var BASE = '/KhachThue/HoaDon/ThanhToan/VnPay/';
    var DEFAULT_ERROR = 'Không tạo được link thanh toán VNPay, vui lòng thử lại.';

    function token() {
        return $('input[name="__RequestVerificationToken"]').val();
    }

    // $button: nút "Thanh toán qua VNPay". onFailed: việc làm sau khi báo lỗi (panel tải lại để hiện số tiền mới nhất).
    function start(hoaDonId, $button, onFailed) {
        if ($button.prop('disabled')) {
            return;
        }

        var originalHtml = $button.html();
        $button.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang chuyển sang VNPay...');

        function fail(message) {
            $button.prop('disabled', false).html(originalHtml);
            Swal.fire({ icon: 'error', title: 'Chưa thanh toán được', text: message || DEFAULT_ERROR });
            if (typeof onFailed === 'function') {
                onFailed();
            }
        }

        $.ajax({
            url: BASE + encodeURIComponent(hoaDonId),
            method: 'POST',
            headers: { 'RequestVerificationToken': token() },
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

    function watchResult(hoaDonId) {
        if (window.ThongBaoHub) {
            window.ThongBaoHub.on('HoaDonThanhToanCapNhat', function (e) {
                if (e && e.hoaDonId === hoaDonId) {
                    window.location.reload();
                }
            });
        }

        $('#ktVnPayRecheck').on('click', function () {
            $(this).prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> Đang kiểm tra...');
            window.location.reload();
        });
    }

    window.KtVnPay = { start: start, watchResult: watchResult };
})(window, jQuery);
