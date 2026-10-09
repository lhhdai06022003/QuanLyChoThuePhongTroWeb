(function (window, $) {
    'use strict';

    $('#vclChiNhanh').on('change', function () {
        $('#vclForm').trigger('submit');
    });

    $(document).on('click', '[data-vcl-nhom]', function () {
        var nhom = $(this).attr('data-vcl-nhom') || '';

        $('#vclNhomFilter [data-vcl-nhom]').each(function () {
            var chon = ($(this).attr('data-vcl-nhom') || '') === nhom;
            $(this)
                .toggleClass('btn-primary', chon)
                .toggleClass('btn-outline-secondary', !chon)
                .attr('aria-pressed', chon ? 'true' : 'false');
        });

        $('.vcl-item').each(function () {
            var hien = nhom === '' || $(this).attr('data-nhom') === nhom;
            $(this).toggleClass('d-none', !hien);
        });

        var conKhoiHien = false;
        $('.vcl-khoi').each(function () {
            var tong = 0;
            var soDong = 0;
            $(this).find('.vcl-item:not(.d-none)').each(function () {
                tong += Number($(this).attr('data-so-luong')) || 0;
                soDong++;
            });
            $(this).find('.vcl-khoi-dem').text(tong);
            $(this).toggleClass('d-none', soDong === 0);
            if (soDong > 0) conKhoiHien = true;
        });

        $('#vclNhomRong').toggleClass('d-none', conKhoiHien);
    });
})(window, jQuery);
