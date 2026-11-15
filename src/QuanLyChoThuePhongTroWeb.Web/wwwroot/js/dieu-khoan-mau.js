        $(document).ready(function () {
            // Khởi tạo datatable nếu có dữ liệu
            if ($('#dieuKhoanTable tbody tr .empty').length === 0) {
                $('#dieuKhoanTable').DataTable({
                    "language": {
                        "url": window.dataTablesViUrl
                    },
                    "pageLength": 10,
                    "responsive": true,
                    "order": [[0, "asc"]],
                    "columnDefs": [
                        { "orderable": false, "targets": [2, 3] }
                    ]
                });
            }

            $('#btnOpenAddModal, #btnOpenAddModalEmpty').click(function() {
                $('#DieuKhoanMauId').val(0);
                $('#TieuDe').val('');
                $('#NoiDung').val('');
                $('#dieuKhoanModalTitle').text('Thêm điều khoản mới');
                $('#btnSaveDieuKhoan').html('<i class="ti ti-plus me-1"></i> Thêm dữ liệu');
                $('#dieuKhoanModal').modal('show');
            });

            $('#btnSaveDieuKhoan').click(function() {
                let id = $('#DieuKhoanMauId').val();
                let tieuDe = $('#TieuDe').val().trim();
                let noiDung = $('#NoiDung').val().trim();

                if(!tieuDe || !noiDung) { 
                    showWarning('Vui lòng nhập đầy đủ Tiêu đề và Nội dung!'); 
                    return; 
                }

                let req = { dieuKhoanMauId: parseInt(id), tieuDe: tieuDe, noiDung: noiDung };
                let isUpdate = id > 0;

                Swal.fire({
                    title: isUpdate ? 'Xác nhận cập nhật?' : 'Xác nhận thêm mới?',
                    text: isUpdate ? 'Việc chỉnh sửa sẽ không ảnh hưởng tới các Hợp đồng đã ký trước đó.' : 'Thêm điều khoản này vào thư viện?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        let $btn = $(this);
                        let originalHtml = $btn.html();
                        $btn.prop('disabled', true).html('<i class="ti ti-loader fa-spin"></i> Đang xử lý...');

                        $.ajax({
                            url: isUpdate ? `/DieuKhoanMau/Update/${id}` : '/DieuKhoanMau/Create',
                            type: isUpdate ? 'PUT' : 'POST',
                            contentType: 'application/json',
                            data: JSON.stringify(req),
                            success: function(res) {
                                showSuccess(res.message).then(() => {
                                    location.reload(); // Reload trang đơn giản nhất vì dữ liệu không quá lớn
                                });
                            },
                            error: function(xhr) { 
                                showError(xhr.responseJSON?.message || 'Có lỗi xảy ra'); 
                            },
                            complete: function() { 
                                $btn.prop('disabled', false).html(originalHtml); 
                            }
                        });
                    }
                });
            });

            $('#dieuKhoanTable').on('click', '.btn-edit-dk', function() {
                let id = $(this).data('id');
                $.get(`/DieuKhoanMau/GetById/${id}`, function(res) {
                    $('#DieuKhoanMauId').val(res.dieuKhoanMauId);
                    $('#TieuDe').val(res.tieuDe);
                    $('#NoiDung').val(res.noiDung);
                    $('#dieuKhoanModalTitle').text('Cập nhật điều khoản');
                    $('#btnSaveDieuKhoan').html('<i class="ti ti-device-floppy me-1"></i> Lưu dữ liệu');
                    $('#dieuKhoanModal').modal('show');
                }).fail(function() {
                    showError('Không lấy được dữ liệu điều khoản');
                });
            });

            $('#dieuKhoanTable').on('click', '.btn-delete-dk', function(e) {
                e.preventDefault();
                let id = $(this).data('id');
                
                Swal.fire({
                    title: 'Xác nhận xóa?',
                    text: 'Bạn có chắc chắn muốn xóa điều khoản này khỏi thư viện? Các hợp đồng đã ký sử dụng điều khoản này sẽ không bị ảnh hưởng.',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý xóa',
                    cancelButtonText: 'Hủy bỏ'
                }).then((result) => {
                    if (result.isConfirmed) {
                        $.ajax({
                            url: `/DieuKhoanMau/Delete/${id}`, 
                            type: 'DELETE',
                            success: function(res) {
                                showSuccess(res.message).then(() => {
                                    location.reload();
                                });
                            },
                            error: function(xhr) {
                                showError(xhr.responseJSON?.message || 'Có lỗi xảy ra');
                            }
                        });
                    }
                });
            });
        });
