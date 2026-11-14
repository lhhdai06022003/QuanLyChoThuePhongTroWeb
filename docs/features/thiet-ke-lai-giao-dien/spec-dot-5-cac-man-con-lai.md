# Spec đợt 5: các màn còn lại của cổng quản lý và màn con của khách thuê

- Trạng thái: **đã duyệt thiết kế, chờ đợt 4 nghiệm thu đạt rồi mới thực thi** (13/11/2026). Người dùng xác nhận các điểm ở mục 3 ngày 13/11/2026.
- Nhánh: `feature/thiet-ke-lai-giao-dien`
- Cách thực thi: một agent khác làm theo [plan-dot-5-cac-man-con-lai.md](plan-dot-5-cac-man-con-lai.md), viết báo cáo vào `.bangiao/giao-dien/bao-cao-dot-5.md`; phiên lập kế hoạch (Fable) nghiệm thu. Lời giao việc: [giao-viec-dot-4-5.md](giao-viec-dot-4-5.md).

## 1. Vì sao làm

Khảo sát ngày 13/11/2026 trên `c10b0dc`. Sau đợt 4 còn bốn nhóm màn chưa theo nền chung:

- **Thiết lập** (Chi nhánh, Dịch vụ, Điều khoản hợp đồng, Tài khoản, Phân công chi nhánh): cùng kiểu cũ như ba màn đợt 4 (tiêu đề tô xanh kèm icon, nút chính xanh lá, bảng kẻ viền, đầu modal màu đặc, nút "Đóng" đỏ, hộp xác nhận tự đặt màu `#3085d6` / `#d33`). Nút thêm bị ẩn dưới 576px ở Chi nhánh, Tài khoản, Điều khoản. Màn Dịch vụ có hai nhãn tự tô bằng mã màu.
- **Vận hành** (Sự cố và sửa chữa, Thông báo, Báo cáo, Việc cần làm, Đổi mật khẩu): vùng lọc `bg-light` hỏng ở chế độ tối; chi phí sửa chữa in bằng `ToString("N0") đ`; hai màn Thông báo nạp DataTables lần thứ hai; biểu đồ Báo cáo dùng `#206bc4`, `#f59f00` gõ cứng.
- **Đón khách** (Tin phòng công khai, Lịch xem phòng, Đặt cọc phòng): do `thuan` viết, đã dùng biến màu, nhưng bảng chưa thành thẻ trên điện thoại.
- **Khách thuê, màn con** (Hồ sơ, Hợp đồng, Hóa đơn, Điện nước, Lịch sử thanh toán, Sự cố, Thông báo, kết quả VNPay): tiêu đề còn icon; `font-weight-bold` không có tác dụng (12 chỗ); bảng kẻ viền và cuộn ngang trên điện thoại, trong khi khách thuê chủ yếu dùng điện thoại; ngày giờ đổi bằng `.AddHours(7)` thay `VietnamTime`.

Script soát: 26 LỖI trong các file của đợt này.

Tồn từ đợt 3 nhận làm ở đợt này: nhóm nút "Khoảng thời gian" của Lịch sử thanh toán tràn khung lọc ở khoảng 1366px.

## 2. Thiết kế

Dùng lại nguyên bảng quy đổi Q1–Q12 và quy tắc ngày của [kế hoạch đợt 4](plan-dot-4-phong-nguoi-thue-hop-dong.md): khung đầu trang chung, một nút chính `btn-primary` hiện ở mọi độ rộng, bảng `ts-bang-the`, modal không tô màu đầu, màu lấy từ biến.

Riêng từng nhóm:

1. **Thiết lập**: tiêu đề theo menu ("Chi nhánh", "Dịch vụ", "Điều khoản hợp đồng", "Tài khoản", "Phân công chi nhánh"). Màn Dịch vụ giữ hai tab; mỗi tab một nút chính ("Thêm dịch vụ", "Thêm giá"); nhãn "Bắt buộc" dùng màu thương hiệu nhạt, "Tùy chọn" dùng `secondary-lt`. Nút "Thu hồi" của Phân công là `btn-outline-danger`.
2. **Sự cố và sửa chữa**: vùng lọc không nền riêng, ba ô lọc xếp dọc trên điện thoại; chi phí qua `VndFormat.Money`; bảng thành thẻ.
3. **Thông báo** (cả hai cổng): tiêu đề "Thông báo"; hai nút "Đánh dấu tất cả đã đọc" (viền thường) và "Xóa tất cả" (viền đỏ) dùng icon `ti`; bảng thành thẻ, thông báo chưa đọc đậm chữ như hiện tại.
4. **Báo cáo**: tiêu đề "Báo cáo"; bảng báo cáo **không** thành thẻ (nhiều cột số, cần so theo hàng) mà giữ cuộn ngang trong thẻ; màu biểu đồ đọc từ biến lúc vẽ như `dashboard.js`.
5. **Việc cần làm**: bảng hóa đơn quá hạn thành thẻ.
6. **Đón khách**: chạm nhẹ. Chỉ thêm `ts-bang-the` cho bảng danh sách và sửa chỗ tràn ngang, sai màu ở chế độ tối bằng cách đổi lớp trong view. Không đổi cấu trúc, không sửa `staff-guest-management.css`. Việc nào cần sửa CSS đó thì ghi lại để chuyển `thuan`.
7. **Khách thuê**: tiêu đề `<h2 class="page-title">` không icon; bảng thành thẻ bo góc `--ts-radius-tenant`; `fw-bold` thay `font-weight-bold`; modal theo Q7, Q8; `<link>` CSS vào `@section Styles`; ngày giờ Razor qua `VietnamTime.Format`.
8. **Lịch sử thanh toán (quản lý)**: nhóm nút "Khoảng thời gian" không tràn khung lọc ở 992–1440px.

Không làm: như đợt 4, thêm: không sửa `khachthue-thanhtoan.js`, `khachthue-vnpay.js`, `hoadon-thanhtoan-quanly.js`; không sửa `Areas/KhachThue/Views/Shared/_CustomerRequestSummary.cshtml` (luồng khách vãng lai của `thuan`), `NguoiDung/DangNhap.cshtml` (đã làm ở đợt 1), các màn biên lai (CSS in riêng).

Thất bại dù chạy được: khách thuê không thanh toán được hóa đơn (QR, VNPay, gửi minh chứng) như trước; một hộp xác nhận xóa mất bước hỏi lại; nhân viên thấy nút chỉ dành cho Admin; giờ hiển thị lệch so với trước; trang tràn ngang ở 375px.

## 3. Điểm người dùng đã xác nhận (13/11/2026)

1. Ba màn Đón khách: chạm nhẹ như mục 2.6.
2. Làm sau khi đợt 4 nghiệm thu đạt; mỗi việc một commit cục bộ trên nhánh, không push.

Người lập kế hoạch tự chọn theo quy ước đã có (không hỏi lại): bảng Báo cáo giữ cuộn ngang thay vì thành thẻ; tiêu đề hai màn Thông báo rút thành "Thông báo".

## 4. Kết quả

Đã code xong, chờ nghiệm thu (14/11/2026).

| Việc | Nội dung | Commit |
| --- | --- | --- |
| V1 | Thẻ bảng của cổng khách thuê bo góc theo cổng | `65066e7` |
| V2 | Chi nhánh, tài khoản, phân công theo khung trang chung, bảng thành thẻ | `f92edc8` |
| V3 | Dịch vụ, điều khoản theo khung trang chung, nhãn dùng màu nền | `255ad28` |
| V4 | Sự cố, thông báo, việc cần làm theo khung trang chung, bảng thành thẻ | `d481af2` |
| V5 | Báo cáo dùng màu biểu đồ theo nền chung | `38cd510` |
| V6 | Hóa đơn và lịch sử thanh toán của khách thuê thành thẻ trên điện thoại | `a9b0b02` |
| V7 | Hợp đồng, hồ sơ, sự cố, thông báo của khách thuê theo khung trang chung | `3aa194d` |
| V8 | Màn đón khách thành thẻ trên điện thoại, khung lọc lịch sử thanh toán không tràn | `57d30e5` |
| V9 | Test tĩnh chốt đợt 5 các màn còn lại và màn con khách thuê (42 test PASS) | `8c72d65` |

## 5. Còn để ý

- Bảng hóa đơn (quản lý) ở 1366px rộng hơn khung khi mã hóa đơn dài: tồn từ đợt 3, chưa nhận làm ở đợt này (cuộn ngang sẽ cắt menu ⋮; cần thử `data-bs-popper-config` với `strategy: fixed` trước).
- `DienNuoc/Index.cshtml` dòng 47, 60 dùng `DateTime.UtcNow.AddHours(7)`: tồn từ đợt 3, ngoài phạm vi.
- 3 file CSS luồng khách vãng lai (`guest-ai-chat.css`, `guest-discovery.css`, `khach-vang-lai.css`) còn 36 LỖI màu thương hiệu gõ cứng: thuộc phạm vi quản lý của `thuan`, chuyển người phụ trách xử trị sau.
