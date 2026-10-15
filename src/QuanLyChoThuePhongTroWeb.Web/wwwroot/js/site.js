// Tiện ích dùng chung cho các trang quản lý.
(function (window, $) {
    'use strict';

    function baoKhongTimThay() {
        var msg = 'Không tìm thấy mục của thông báo trong danh sách hiện tại.';
        if (window.Swal) {
            Swal.fire({ toast: true, position: 'bottom-end', icon: 'info', titleText: msg, showConfirmButton: false, timer: 4000 });
        } else {
            console.warn(msg);
        }
    }

    // Làm nổi bật một dòng bảng: chuyển tới trang chứa dòng (nếu DataTable có phân trang), cuộn vào giữa màn hình, nhấp nháy.
    // rowElement: phần tử <tr> (có thể đang nằm ngoài trang hiện tại của DataTable); dataTableApi: DataTable().api hoặc null.
    function focusRow(rowElement, dataTableApi) {
        if (!rowElement) {
            baoKhongTimThay();
            return false;
        }

        try {
            if (dataTableApi && dataTableApi.page && dataTableApi.rows) {
                var viTri = dataTableApi.rows({ order: 'current', search: 'applied' }).nodes().toArray().indexOf(rowElement);
                if (viTri < 0) {
                    baoKhongTimThay();
                    return false;
                }
                var doDai = dataTableApi.page.len();
                if (doDai > 0) {
                    dataTableApi.page(Math.floor(viTri / doDai)).draw('page');
                }
            }
        } catch (err) {
            console.warn('TroFocus: không chuyển được trang bảng.', err);
        }

        var $row = $(rowElement);
        $('.tb-focus-row').removeClass('tb-focus-row tb-focus-row-flash');
        $row.addClass('tb-focus-row tb-focus-row-flash');
        rowElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        setTimeout(function () { $row.removeClass('tb-focus-row-flash'); }, 2600);
        return true;
    }

    window.TroFocus = { row: focusRow };
})(window, window.jQuery);
