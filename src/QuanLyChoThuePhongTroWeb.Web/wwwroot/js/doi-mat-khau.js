// Màn đổi mật khẩu cổng quản lý (spec bảo mật xác thực §2.5). Token antiforgery do $.ajaxSetup của layout gắn.
(function (window, $) {
    'use strict';

    var cfg = window.doiMatKhauConfig || {};
    var $form = $('#dmkForm');
    var $btn = $('#dmkLuu');
    var $xacNhan = $('#dmkXacNhan');

    function khopXacNhan() {
        var xacNhan = $xacNhan.val();
        var khop = !xacNhan || xacNhan === $('#dmkMatKhauMoi').val();
        $xacNhan.toggleClass('is-invalid', !khop);
        return khop;
    }

    $form.on('click', '.dmk-hien-mat-khau', function (e) {
        e.preventDefault();
        var $input = $(this).closest('.input-group').find('input');
        var hien = $input.attr('type') === 'password';
        $input.attr('type', hien ? 'text' : 'password');
        $(this).find('i').toggleClass('ti-eye', !hien).toggleClass('ti-eye-off', hien);
        $(this).attr('aria-label', hien ? 'Ẩn mật khẩu' : 'Hiện mật khẩu').attr('title', hien ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
    });

    $('#dmkMatKhauMoi, #dmkXacNhan').on('input', khopXacNhan);

    $form.on('submit', function (e) {
        e.preventDefault();

        var hienTai = $('#dmkMatKhauHienTai').val();
        var moi = $('#dmkMatKhauMoi').val();
        var xacNhan = $xacNhan.val();

        if (!hienTai || !moi || !xacNhan) {
            showWarning('Vui lòng nhập đủ ba ô mật khẩu.');
            return;
        }
        if (moi.length < 8 || moi.length > 128) {
            showWarning('Mật khẩu mới phải có từ 8 đến 128 ký tự.');
            return;
        }
        if (moi !== xacNhan) {
            khopXacNhan();
            showWarning('Mật khẩu xác nhận không khớp.');
            return;
        }

        var nutGoc = $btn.html();
        $btn.prop('disabled', true).html('<i class="ti ti-loader ti-spin me-1"></i> Đang lưu...');

        $.ajax({
            url: cfg.urlDoiMatKhau,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ MatKhauHienTai: hienTai, MatKhauMoi: moi, XacNhanMatKhau: xacNhan })
        })
            .done(function (res) {
                if (res && res.success) {
                    $form[0].reset();
                    showSuccess(res.message || 'Đổi mật khẩu thành công!').then(function () {
                        if (res.redirectUrl) window.location.href = res.redirectUrl;
                    });
                } else {
                    showError((res && res.message) || 'Không đổi được mật khẩu.');
                }
            })
            .fail(function (xhr) {
                showError((xhr.responseJSON && xhr.responseJSON.message) || 'Không đổi được mật khẩu. Vui lòng thử lại.');
            })
            .always(function () {
                $btn.prop('disabled', false).html(nutGoc);
            });
    });
})(window, window.jQuery);
