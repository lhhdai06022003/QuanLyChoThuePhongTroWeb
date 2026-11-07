# Xử lý yêu cầu giữ chỗ đã đặt cọc

## Mục tiêu

Khi tiền đã được xác nhận đủ trong hạn, trang công khai ẩn phòng và màn Giữ chỗ hiển thị yêu cầu đã nhận tiền. Quản lý có thể xác nhận thuê để tạo hợp đồng cho chính tài khoản đã đặt cọc, hoặc kết thúc yêu cầu khi khách không thuê và ghi lại tiền đã nhận.

## Cách thao tác

- **Xác nhận thuê** mở form điền sẵn phòng, tên, email, số điện thoại, giá thuê và khoản cọc từ dữ liệu hệ thống. Quản lý chọn ngày bắt đầu, ngày kết thúc nếu có và chỉ bổ sung CCCD nếu hồ sơ chưa có. Nút lưu tạo hợp đồng, áp tiền giữ chỗ vào cọc hợp đồng, chuyển phòng sang Đã thuê và cập nhật yêu cầu thành Đã chuyển hợp đồng.
- Nếu khách đang dùng tài khoản vãng lai, liên kết tài khoản đặt cọc với hồ sơ người thuê đã được quản lý kiểm tra. Tài khoản khách thuê đã liên kết dùng lại hồ sơ đó. Từ chối liên kết nếu hồ sơ trùng đang thuộc tài khoản khác. Sau lần đăng nhập tiếp theo khách có thể xem hợp đồng và hóa đơn bằng tài khoản đã đặt cọc.
- **Khách không thuê** cần xác nhận và ghi lý do. Yêu cầu kết thúc, phòng đủ điều kiện được công khai lại. Lịch sử giữ chỗ lưu tên khách, phòng, tiền thực nhận và khoản cọc giữ lại. Khoản này không chuyển sang lần đặt phòng sau hoặc hóa đơn tiền thuê. Báo cáo ghi số cọc thực tế giữ lại vào Thu khác theo ngày đóng. Nếu hồ sơ đã có quyết định hoàn một phần tiền cọc trước đó, chỉ ghi phần còn giữ lại sau quyết định đó.
- Chính sách không hoàn khoản cọc hợp lệ khi khách không thuê phải hiện trước khi thanh toán. Khoản dư, khoản đến muộn và các quyết định hoàn đã ghi nhận vẫn xử lý riêng theo quy trình hiện có. Đóng yêu cầu đã đủ cọc do quản lý thực hiện trong màn giữ chỗ.

## Phương án

Chọn form điền sẵn vì tài khoản vãng lai đã có dữ liệu liên hệ nhưng chưa chắc có CCCD và chưa xác định ngày bắt đầu thuê. Tạo ngay từ một lần bấm sẽ phải gán các ngày hoặc bỏ qua trường bắt buộc. Ghi kết quả đóng yêu cầu và giữ giao dịch để nhân viên tra cứu tiền đã nhận và tránh lệch báo cáo.

## Ràng buộc

Dùng schema hiện tại của nhánh `codex/khach-vang-lai-tiep-tuc`. Kiểm quyền chi nhánh, đúng người đặt và phòng, đủ tiền hợp lệ. Tạo hợp đồng, liên kết tài khoản và áp cọc phải hoàn tất cùng nhau. Tạo hợp đồng và đóng yêu cầu phải loại trừ nhau khi có thao tác đồng thời. Hóa đơn kỳ thuê tiếp tục theo quy trình phát hành hiện tại. Kiểm thử trên cơ sở dữ liệu riêng có hậu tố `_test`.
