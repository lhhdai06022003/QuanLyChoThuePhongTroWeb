# Spec đợt 2: ba màn chính và thanh tab dưới

- Trạng thái: **đang thực thi**; người dùng xác nhận cả bốn điểm ở mục 3 theo đề xuất ngày 11/11/2026
- Ngày: 11/11/2026
- Nhánh: `feature/thiet-ke-lai-giao-dien`
- Cách thực thi: `/doi-ngu` (người dùng chọn 11/11/2026). Phương án chi tiết, bảng công việc và tiêu chí nghiệm thu: `.bangiao/doi/phuong-an.md` (ngoài Git); phần thiết kế chép lại ở mục 2 để tài liệu này tự đứng được.

## 1. Vì sao làm

Sau đợt 1 lớp vỏ đã thống nhất nhưng ba màn mở đầu của ba vai trò vẫn như cũ:

- Chủ trọ thấy bốn ô màu đặc cạnh nhau, phải tự cộng mới biết kỳ này thu được bao nhiêu phần.
- Nhân viên thấy danh sách việc nhưng phải bấm vào dòng rồi mới biết sẽ làm gì.
- Khách thuê thấy số tiền nhưng không thấy hạn trả.
- Nhân viên dùng điện thoại phải mở menu 16 mục cho bốn việc làm hằng ngày.
- Bảng "Tiền mặt nhân viên đang giữ" làm Bảng điều khiển Admin tràn ngang ở 375px.

## 2. Thiết kế

1. **Thanh thu tiền (chỉ Admin)** thay hàng ô màu: số đã thu của kỳ, "phải thu" = đã thu + chờ thu, tỷ lệ phần trăm, thanh hai đoạn (đã thu, chờ thu). Bên dưới bốn số liệu nhạt bấm được: Đã thu, Chờ thu kỳ, Quá hạn (mọi kỳ), Phòng đang nợ. Không số nào đổi cách tính.
2. **Nhân viên:** việc đứng đầu; mỗi việc có nút làm luôn (Chốt số, Xem hóa đơn, Thu tiền, Xử lý), việc Khẩn dùng nút chính. Số liệu bên dưới dùng ô nhạt như của Admin.
3. **Thanh tab dưới (dưới 768px):** nhân viên và Admin có Việc, Phòng, Điện nước, Hóa đơn, Thêm (mở menu đầy đủ). Khách thuê có Trang chủ, Hóa đơn, Điện nước, Sự cố, Tôi. Nút trợ lý AI nằm trên thanh tab.
4. **Trang chủ khách thuê:** thẻ "Cần thanh toán" với số tiền, số hóa đơn, hạn trả gần nhất (còn n ngày hoặc quá hạn n ngày), nút thanh toán; ba dòng lối tắt Điện nước, Báo sự cố, Hợp đồng. Bỏ lưới bốn thẻ hiện tại.
5. **Sửa tràn ngang** bảng tiền mặt nhân viên; màu biểu đồ đọc từ biến của nền.

Không làm: đổi cách tính, controller của cổng quản lý, quyền, schema, các màn khác, cổng khách vãng lai.

Thất bại dù chạy được: nhân viên thấy bất kỳ số doanh thu nào; một `id` mà `dashboard.js` hoặc test đang dùng biến mất; tỷ lệ phần trăm trên thanh không bằng đã thu ÷ (đã thu + chờ thu); khách thuê thấy hạn trả lệch ngày do múi giờ.

## 3. Điểm người dùng đã xác nhận (11/11/2026)

1. Thêm trường chỉ đọc `HanThanhToanUtc` vào DTO `HoaDonRes` (tầng Application, module hóa đơn) để trang chủ khách thuê hiện hạn trả. Không đổi cách tính tiền, không thêm truy vấn.
2. Thanh thu tiền chỉ có hai đoạn; "Quá hạn" đứng riêng vì con số này tính trên mọi kỳ, không riêng kỳ đang chọn. Khác bản mẫu đã trình.
3. Thanh tab khách thuê đổi mục thứ năm từ "Hợp đồng" sang "Tôi" (hồ sơ).
4. Cho phép đội commit cục bộ từng việc trên nhánh này (không push), như đợt 1.
