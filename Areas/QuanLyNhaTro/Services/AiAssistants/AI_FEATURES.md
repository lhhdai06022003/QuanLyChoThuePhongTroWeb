# Bảng Thống Kê Chức Năng Trợ Lý AI (AI Features)

File này lưu trữ danh sách các chức năng (Function Calling) mà Trợ lý AI (Gemini) đã được tích hợp để truy vấn dữ liệu trong hệ thống Quản lý Nhà Trọ. 

Mục đích: Giúp lập trình viên và người quản lý dễ dàng tra cứu AI có thể làm được những gì và lên ý tưởng cho các tính năng mới.

---

## 🟢 Các chức năng ĐÃ TÍCH HỢP (Hiện có)

Tính đến thời điểm hiện tại, AI có khả năng thực thi **9 chức năng** chia làm 2 nhóm chính:

### Nhóm 1: Quản lý Vận hành (Lễ tân)
1. **Tìm phòng trống** (`GetPhongTrongAsync`):
   - **Mô tả:** Lấy danh sách các phòng đang trống. Có thể lọc theo mức giá tối đa.
   - **Prompt ví dụ:** "Có phòng nào trống giá dưới 3 triệu không?"
2. **Hợp đồng sắp hết hạn** (`GetHopDongSapHetHanAsync`):
   - **Mô tả:** Lấy danh sách các hợp đồng sắp kết thúc trong vòng X ngày tới.
   - **Prompt ví dụ:** "Tháng này có hợp đồng nào hết hạn không?"
3. **Tra cứu thông tin khách thuê** (`GetThongTinKhachThueAsync`):
   - **Mô tả:** Tra cứu tên, số điện thoại dựa vào tên khách hoặc số phòng.
   - **Prompt ví dụ:** "Ai đang thuê phòng 101?", "Số điện thoại của anh Nguyễn Văn A là gì?"
4. **Kiểm tra trạng thái chốt điện nước** (`GetPhongTroChuaChotDienNuocAsync`):
   - **Mô tả:** Lọc ra các phòng đang thuê nhưng chưa có số liệu điện nước trong tháng.
   - **Prompt ví dụ:** "Còn phòng nào chưa chốt số điện nước tháng này không?"

### Nhóm 2: Quản lý Tài chính (Kế toán)
5. **Kiểm tra công nợ cụ thể** (`GetCongNoPhongAsync`):
   - **Mô tả:** Tính tổng số tiền chưa thanh toán (tổng hóa đơn - tổng đã thu) của một phòng.
   - **Prompt ví dụ:** "Phòng 102 đang nợ bao nhiêu tiền?"
6. **Thống kê hóa đơn chưa thanh toán** (`GetHoaDonChuaThanhToanAsync`):
   - **Mô tả:** Liệt kê toàn bộ các hóa đơn nợ trong tháng/năm chỉ định.
   - **Prompt ví dụ:** "Tháng 5 có những phòng nào chưa đóng tiền nhà?"
7. **Tổng doanh thu thực thu** (`GetDoanhThuThucThuAsync`):
   - **Mô tả:** Tính tổng tiền mặt/chuyển khoản đã thực sự nhận được trong một khoảng thời gian.
   - **Prompt ví dụ:** "Tuần này thu được bao nhiêu tiền rồi?"
8. **Doanh thu theo chi nhánh** (`GetDoanhThuChiNhanhAsync`):
   - **Mô tả:** Nhóm doanh thu thực thu theo từng chi nhánh.
   - **Prompt ví dụ:** "Tháng 4 chi nhánh trung tâm thu được bao nhiêu?"
9. **Chỉ số điện nước** (`GetChiSoDienNuocAsync`):
   - **Mô tả:** Xem chỉ số cũ, mới và số tiêu thụ điện/nước của phòng.
   - **Prompt ví dụ:** "Tháng trước phòng 101 dùng bao nhiêu khối nước?"

---

## 🟡 Các chức năng CÓ THỂ PHÁT TRIỂN (Ý tưởng tương lai)

Dưới đây là một số gợi ý để mở rộng thêm "Trợ lý AI" thành "Hệ thống tự động hóa":

### 1. Phân tích & Báo cáo nâng cao
- **So sánh doanh thu:** So sánh doanh thu tháng này với tháng trước để xem tăng hay giảm.
- **Dự báo dòng tiền:** Tính tổng doanh thu dự kiến (dựa trên hợp đồng) vs. doanh thu thực thu.
- **Thống kê chi phí bảo trì:** "Tháng này sửa chữa hết bao nhiêu tiền?"

### 2. Quản lý Sự cố & Bảo trì
- **Tạo yêu cầu bảo trì (Ghi log sự cố):** AI có thể gọi hàm INSERT để tạo phiếu sự cố. Ví dụ: *"Ghi nhận phòng 202 bị hỏng điều hòa."* -> AI tự tạo ticket.
- **Kiểm tra tài sản:** *"Phòng 101 có tủ lạnh không?"*

### 3. Tương tác Nghiệp vụ Ghi (Insert/Update)
> *Lưu ý: Chức năng ghi cần được thiết kế cẩn thận để tránh AI làm sai dữ liệu.*
- **Đăng ký dịch vụ mới:** *"Phòng 102 muốn đăng ký gửi thêm 1 xe máy."* -> AI gọi hàm thêm dịch vụ.
- **Dự thảo trả phòng:** *"Làm thủ tục trả phòng cho phòng 301."* -> AI tính toán tự động các khoản bồi thường, cọc, nợ và xuất ra phiếu nháp.

---
*Lưu ý cho Developer:* Khi muốn thêm một chức năng mới, cần thực hiện 3 bước:
1. Viết logic C# trong `AiAssistantService.cs`.
2. Định nghĩa Schema mô tả trong mảng `toolsConfig`.
3. Bắt case gọi hàm trong khối `try-catch` của `ChatWithAssistantAsync`.
