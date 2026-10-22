# TÀI LIỆU ĐẶC TẢ CHI TIẾT 4 USE CASE CỐT LÕI HỆ THỐNG

Tài liệu này đặc tả chi tiết 4 Use Case cốt lõi nhất của hệ thống **Quản lý cho thuê phòng trọ**, được thiết kế dựa trên logic nghiệp vụ thực tế của mã nguồn và cơ sở dữ liệu dự án.

---

## 1. Đặc tả Use Case: Lập hợp đồng thuê phòng (UC-07)

### Bảng đặc tả chi tiết:

| Thành phần | Nội dung mô tả chi tiết |
| :--- | :--- |
| **Tên Use Case** | Lập hợp đồng thuê phòng |
| **Mã Use Case** | UC-07 |
| **Tác nhân chính** | Chủ nhà trọ / Nhân viên vận hành (Admin / Staff) |
| **Mô tả tóm tắt** | Cho phép chủ nhà thiết lập hợp đồng thuê phòng trọ mới cho khách thuê đại diện, khai báo thời hạn thuê, giá thuê thỏa thuận, tiền cọc, đăng ký thành viên ở ghép và áp dụng các điều khoản hợp đồng mẫu. |
| **Tiền điều kiện** | 1. Nhân viên đã đăng nhập thành công vào phân hệ Admin.<br>2. Phòng trọ được chọn phải ở trạng thái trống (`TrangThaiPhong == Trong`).<br>3. Người thuê đại diện đã có hồ sơ thông tin trong danh sách người thuê (`NguoiThues`). |
| **Hậu điều kiện** | 1. Hợp đồng mới được lưu thành công vào cơ sở dữ liệu ở trạng thái hoạt động (`TrangThaiHopDong == DangHoatDong`).<br>2. Trạng thái của phòng trọ liên kết chuyển sang đã thuê (`TrangThaiPhong == DaThue`).<br>3. Khách đại diện và các thành viên được đăng ký vào bảng ở ghép (`ChiTietThanhVienHopDongs`).<br>4. Một tài khoản đăng nhập (Role: KhachThue, tên đăng nhập là Email, mật khẩu băm từ SĐT) tự động được tạo cho người đại diện (nếu chưa có). |
| **Luồng cơ bản (Basic Flow)** | 1. Nhân viên chọn chi nhánh, phòng trống cần lập hợp đồng, chọn người đại diện ký hợp đồng.<br>2. Nhập các thông tin bắt buộc: Ngày bắt đầu, Ngày kết thúc (nếu có), Tiền cọc phòng, Tiền thuê phòng thỏa thuận.<br>3. Chọn danh sách thành viên ở ghép (nếu có) và tích chọn các điều khoản mẫu áp dụng.<br>4. Nhân viên nhấn nút **"Lập hợp đồng"**.<br>5. Hệ thống kiểm tra tính hợp lệ của dữ liệu đầu vào (Ngày kết thúc không được nhỏ hơn ngày bắt đầu).<br>6. Hệ thống kiểm tra xem phòng trọ hoặc người đại diện có đang liên kết với hợp đồng hoạt động nào khác không.<br>7. Hệ thống đếm tổng số lượng thành viên ở ghép để so khớp giới hạn sức chứa của phòng (`SoNguoiToiDa`).<br>8. Hệ thống kiểm tra các thành viên ở ghép có đang sinh sống ở phòng có hợp đồng hoạt động khác không.<br>9. Hệ thống tự động sinh mã hợp đồng độc bản dạng: `HD-[Mã Chi Nhánh]-[yyMM]-[Số Thứ Tự]`.<br>10. Hệ thống mở một SQL Transaction trên PostgreSQL.<br>11. Hệ thống lưu bản ghi hợp đồng mới vào bảng `HopDongs`.<br>12. Lưu thông tin thành viên ở ghép vào bảng `ChiTietThanhVienHopDongs`.<br>13. Sao lưu (Snapshot) tiêu đề và nội dung các điều khoản mẫu được chọn vào bảng `HopDongDieuKhoans`.<br>14. Cập nhật trạng thái phòng trọ thành `DaThue`.<br>15. Kiểm tra và tự động chèn một tài khoản đăng nhập mới vào bảng `NguoiDungs` liên kết với hồ sơ người thuê đại diện nếu họ chưa có tài khoản.<br>16. Hệ thống thực hiện Commit Transaction thành công.<br>17. Trả kết quả HTTP 200 thông báo thành công và làm mới giao diện. |
| **Luồng ngoại lệ (Exception Flows)** | *   **Luồng 5.a (Ngày không hợp lệ):** Ngày kết thúc nhỏ hơn ngày bắt đầu $\rightarrow$ Hệ thống báo lỗi và hủy thao tác.<br>*   **Luồng 6.a (Phòng/Người đại diện bận):** Phát hiện phòng đang thuê hoặc người đại diện đang đứng tên hợp đồng hoạt động khác $\rightarrow$ Hệ thống báo lỗi chặn không cho lập.<br>*   **Luồng 7.a (Quá số người cho phép):** Tổng số người đăng ký vượt quá `SoNguoiToiDa` của phòng $\rightarrow$ Hệ thống trả về lỗi chặn lưu.<br>*   **Luồng 8.a (Thành viên ở ghép trùng lịch):** Thành viên ở ghép đang ở phòng hoạt động khác $\rightarrow$ Hệ thống thông báo cụ thể tên thành viên bị trùng lịch.<br>*   **Luồng 9.a (Trùng mã hợp đồng):** Hệ thống tự động cộng thêm offset tăng dần và thử lại tối đa 5 lần. Nếu vẫn trùng, báo lỗi.<br>*   **Luồng 10-15.a (Lỗi kết nối CSDL / Lỗi bất kỳ):** Hệ thống gọi Rollback Transaction, khôi phục lại dữ liệu phòng/khách thuê ban đầu và trả về HTTP 400 kèm thông điệp lỗi chi tiết. |

---

## 2. Đặc tả Use Case: Phát sinh hóa đơn hàng loạt (UC-09)

### Bảng đặc tả chi tiết:

| Thành phần | Nội dung mô tả chi tiết |
| :--- | :--- |
| **Tên Use Case** | Phát sinh hóa đơn hàng loạt |
| **Mã Use Case** | UC-09 |
| **Tác nhân chính** | Chủ nhà trọ / Nhân viên vận hành (Admin / Staff) |
| **Mô tả tóm tắt** | Cho phép tự động tính toán và tạo hóa đơn thanh toán hàng tháng (bao gồm tiền phòng, tiền điện, nước và phí dịch vụ đã đăng ký) cho toàn bộ các phòng trọ có hợp đồng đang hoạt động thuộc chi nhánh trọ được chọn. |
| **Tiền điều kiện** | 1. Nhân viên đã đăng nhập thành công vào phân hệ Admin.<br>2. Tất cả các phòng trọ được chọn phát sinh hóa đơn bắt buộc phải được chốt chỉ số điện nước mới trong tháng/năm tương ứng tại bảng `DichVuDienNuocCuaPhongs`. |
| **Hậu điều kiện** | 1. Các bản ghi hóa đơn (`HoaDons`) được tạo ở trạng thái chưa thanh toán (`TrangThaiHoaDon == ChuaThanhToan`).<br>2. Các bản ghi chi tiết hóa đơn (`ChiTietHoaDons`) được tạo để lưu vết số lượng, đơn giá thực tế.<br>3. Bản ghi chỉ số điện nước của tháng phát sinh hóa đơn được khóa (`IsLocked = true`) chặn sửa đổi chỉ số đầu vào. |
| **Luồng cơ bản (Basic Flow)** | 1. Nhân viên chọn Chi nhánh, Tháng và Năm cần phát sinh hóa đơn.<br>2. Bấm nút **"Xem trước phát sinh"** (Preview).<br>3. Hệ thống tính toán thử số tiền dự kiến của từng phòng và hiển thị bảng kê xem trước trên giao diện.<br>4. Nhân viên kiểm tra số liệu xem trước, tích chọn các phòng cần phát sinh (hoặc chọn tất cả) và nhấn **"Phát sinh hóa đơn"**.<br>5. Hệ thống kiểm tra xem các phòng được chọn đã chốt chỉ số điện nước tháng đó chưa.<br>6. Với mỗi phòng hợp lệ, hệ thống thực hiện công thức tính toán:<br>&nbsp;&nbsp;&nbsp;&nbsp;- Tiền phòng = Giá thỏa thuận trên hợp đồng.<br>&nbsp;&nbsp;&nbsp;&nbsp;- Tiền điện = (Chỉ số mới - Chỉ số cũ) × Đơn giá điện chi nhánh.<br>&nbsp;&nbsp;&nbsp;&nbsp;- Tiền nước = (Chỉ số mới - Chỉ số cũ) × Đơn giá nước chi nhánh.<br>&nbsp;&nbsp;&nbsp;&nbsp;- Tiền dịch vụ khác = Số lượng đăng ký × Đơn giá dịch vụ đăng ký.<br>&nbsp;&nbsp;&nbsp;&nbsp;- Tổng tiền = Cộng tất cả các mục trên.<br>7. Hệ thống tự động sinh mã hóa đơn duy nhất có định dạng: `HD-[yyyyMM]-[Số tự tăng]`.<br>8. Hệ thống lưu bản ghi `HoaDon` ở trạng thái chưa thanh toán (`ChuaThanhToan`).<br>9. Lưu chi tiết hóa đơn vào bảng `ChiTietHoaDons` để lưu vết đơn giá tại thời điểm chốt.<br>10. Cập nhật thuộc tính `IsLocked = true` tại bản ghi chỉ số điện nước tương ứng.<br>11. Hệ thống lưu thay đổi vào database và trả về thông báo tổng số hóa đơn đã phát sinh thành công. |
| **Luồng ngoại lệ (Exception Flows)** | *   **Luồng 5.a (Phòng chưa chốt điện nước):** Phát hiện phòng chưa được chốt chỉ số điện nước tháng đó $\rightarrow$ Hệ thống dừng phát sinh hóa đơn cho phòng đó, thông báo lỗi cụ thể để nhân viên đi chốt số trước.<br>*   **Luồng 6.a (Không có hợp đồng hoạt động):** Phòng trống hoặc hợp đồng đã thanh lý $\rightarrow$ Hệ thống tự động bỏ qua phòng đó.<br>*   **Luồng 8-10.a (Lỗi kết nối CSDL khi ghi hàng loạt):** Hệ thống gọi Rollback toàn bộ danh sách phòng lỗi, khôi phục trạng thái khóa chỉ số điện nước và báo lỗi hệ thống. |

---

## 3. Đặc tả Use Case: Ghi nhận thu tiền - Quét mã VietQR (UC-09)

### Bảng đặc tả chi tiết:

| Thành phần | Nội dung mô tả chi tiết |
| :--- | :--- |
| **Tên Use Case** | Ghi nhận thu tiền (Quét mã VietQR) |
| **Mã Use Case** | UC-09 |
| **Tác nhân chính** | Chủ nhà trọ / Nhân viên vận hành (Admin / Staff) |
| **Tác nhân phụ** | Khách thuê (Renter) |
| **Mô tả tóm tắt** | Cho phép nhân viên ghi nhận thanh toán hóa đơn của khách thuê, tự động sinh mã VietQR động để chuyển khoản và cập nhật trạng thái hóa đơn sang đã thanh toán. |
| **Tiền điều kiện** | 1. Nhân viên đã đăng nhập phân hệ Admin.<br>2. Hóa đơn được chọn thanh toán đang ở trạng thái chưa thanh toán (`TrangThaiHoaDon == ChuaThanhToan`). |
| **Hậu điều kiện** | 1. Hóa đơn chuyển sang trạng thái đã thanh toán (`DaThanhToan`).<br>2. Bản ghi giao dịch được chèn vào bảng lịch sử thanh toán (`LichSuThanhToans`).<br>3. Số tiền đã thanh toán trên hóa đơn được cập nhật bằng tổng tiền hóa đơn. |
| **Luồng cơ bản (Basic Flow)** | 1. Nhân viên mở chi tiết hóa đơn cần thu tiền từ danh sách hóa đơn hoặc sơ đồ phòng.<br>2. Hệ thống gọi Helper đọc thông tin tài khoản ngân hàng thụ hưởng từ file `appsettings.json`, đọc số tiền và sinh nội dung chuyển khoản chuẩn hóa không dấu: `THANH TOAN [MaHoaDon]`.<br>3. Hệ thống mã hóa thông tin theo chuẩn EMVCo, tính mã CRC16 sinh chuỗi VietQR và render thành ảnh QR Code PNG hiển thị trên popup giao diện.<br>4. Khách thuê sử dụng Mobile Banking quét mã QR để chuyển khoản. Số tiền và nội dung chuyển khoản được điền tự động chính xác.<br>5. Nhân viên xác nhận tiền đã vào tài khoản (hoặc thu tiền mặt), chọn phương thức thanh toán tương ứng và nhập ghi chú.<br>6. Nhân viên nhấn nút **"Xác nhận thu tiền"**.<br>7. Hệ thống ghi nhận ID nhân viên xác nhận giao dịch từ Claims đăng nhập.<br>8. Hệ thống cập nhật trạng thái hóa đơn thành `DaThanhToan`, ghi nhận ngày thanh toán thành ngày hiện tại và số tiền đã thanh toán bằng tổng số tiền hóa đơn.<br>9. Hệ thống chèn bản ghi giao dịch mới vào bảng `LichSuThanhToans`.<br>10. Lưu các thay đổi vào CSDL và thông báo thành công. |
| **Luồng ngoại lệ (Exception Flows)** | *   **Luồng 2-3.a (Lỗi file cấu hình ngân hàng):** File `appsettings.json` bị thiếu hoặc sai tài khoản ngân hàng $\rightarrow$ Hệ thống báo lỗi cấu hình và không hiển thị được ảnh QR Code. Nhân viên chuyển sang thu tiền mặt thủ công.<br>*   **Luồng 8-9.a (Lỗi cập nhật CSDL):** Lỗi lưu database $\rightarrow$ Giao dịch bị hủy bỏ, hóa đơn vẫn ở trạng thái chưa thanh toán, hệ thống thông báo lỗi thu tiền thất bại. |

---

## 4. Đặc tả Use Case: Tự động quét và gửi email nhắc nợ (UC-15)

### Bảng đặc tả chi tiết:

| Thành phần | Nội dung mô tả chi tiết |
| :--- | :--- |
| **Tên Use Case** | Tự động quét và gửi email nhắc nợ |
| **Mã Use Case** | UC-15 |
| **Tác nhân chính** | Hệ thống chạy ngầm (System Background Job - `InvoiceReminderService`) |
| **Tác nhân phụ** | Khách thuê (Renter) |
| **Mô tả tóm tắt** | Hệ thống chạy ngầm định kỳ quét cơ sở dữ liệu để tìm các hóa đơn quá hạn chưa thanh toán, tự động kết xuất tệp tin PDF hóa đơn chi tiết và gửi email thông báo nợ kèm PDF hóa đơn đến email của khách thuê đại diện. |
| **Tiền điều kiện** | 1. Dịch vụ nền `InvoiceReminderService` đang chạy trong vòng đời ứng dụng.<br>2. Cấu hình tự động nhắc nợ đang bật (`AutoReminderSettings:Enabled = true` trong `appsettings.json`).<br>3. Có hóa đơn thỏa mãn điều kiện: Chưa thanh toán (`ChuaThanhToan`), không bị xóa mềm (`!IsDeleted`) và có ngày tạo vượt quá số ngày quá hạn cấu hình (mặc định 5 ngày). |
| **Hậu điều kiện** | 1. Khách thuê nhận được email thông báo nhắc nợ đính kèm file PDF hóa đơn chi tiết.<br>2. ID hóa đơn được ghi nhận vào file `sent_reminders.json` để ngăn chặn gửi nhắc nợ lặp lại trong các chu kỳ quét sau. |
| **Luồng cơ bản (Basic Flow)** | 1. Dịch vụ chạy nền tự động kích hoạt định kỳ (mặc định cấu hình 1 giờ một lần).<br>2. Hệ thống kiểm tra cấu hình trong `appsettings.json`. Nếu `Enabled = false`, tiến trình lập tức dừng lại.<br>3. Hệ thống tính toán thời điểm trễ hạn: `overdueThreshold = Ngày hiện tại - Số ngày trễ hạn` (mặc định 5 ngày).<br>4. Hệ thống đọc file `sent_reminders.json` từ thư mục gốc để nạp danh sách ID hóa đơn đã gửi trước đó lên bộ nhớ RAM.<br>5. Hệ thống truy vấn CSDL PostgreSQL để lọc ra toàn bộ hóa đơn trễ hạn thỏa mãn điều kiện.<br>6. Với mỗi hóa đơn tìm thấy, hệ thống kiểm tra:<br>&nbsp;&nbsp;&nbsp;&nbsp;- Nếu ID hóa đơn đã có trong danh sách đã gửi (`sent_reminders.json`) $\rightarrow$ Bỏ qua.<br>&nbsp;&nbsp;&nbsp;&nbsp;- Nếu email khách thuê đại diện trống hoặc sai định dạng $\rightarrow$ Ghi log cảnh báo và bỏ qua.<br>7. Hệ thống gọi `IHoaDonService.GetHopDongById` để lấy thông tin chi tiết dịch vụ và chi nhánh.<br>8. Hệ thống gọi `IHoaDonService.ExportPdfAsync` để kết xuất tệp PDF hóa đơn lưu vào mảng byte trên bộ nhớ RAM.<br>9. Hệ thống gọi `IEmailService.SendInvoiceEmailAsync` thiết lập kết nối SMTP bảo mật (TLS) để gửi email nhắc nợ (sử dụng template responsive, tương thích Dark Mode và cá nhân hóa chữ ký chi nhánh).<br>10. Khi gửi email thành công, hệ thống cập nhật ID hóa đơn vào file nhật ký `sent_reminders.json`.<br>11. Hệ thống tiếp tục xử lý các hóa đơn tiếp theo trong danh sách cho đến hết, ghi nhận log và ngủ trong 1 giờ. |
| **Luồng ngoại lệ (Exception Flows)** | *   **Luồng 6.a (Khách thuê không có email):** Không tìm thấy email của khách thuê đại diện $\rightarrow$ Hệ thống bỏ qua hóa đơn, ghi log warning và tiếp tục xử lý hóa đơn khác.<br>*   **Luồng 8.a (Lỗi kết xuất PDF):** Lỗi tạo file PDF hóa đơn $\rightarrow$ Ghi log error và chuyển sang hóa đơn khác.<br>*   **Luồng 9.a (Lỗi SMTP Server/Gửi email thất bại):** Lỗi kết nối máy chủ email $\rightarrow$ Gửi thư thất bại, ghi log error, hệ thống không cập nhật ID hóa đơn vào `sent_reminders.json` để tiếp tục gửi lại vào chu kỳ sau. |
