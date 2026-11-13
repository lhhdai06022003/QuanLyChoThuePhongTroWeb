# Spec đợt 4: phòng trọ, người thuê, hợp đồng

- Trạng thái: **đã duyệt thiết kế, chờ thực thi** (13/11/2026). Người dùng xác nhận các điểm ở mục 3 ngày 13/11/2026.
- Nhánh: `feature/thiet-ke-lai-giao-dien`
- Cách thực thi: một agent khác làm theo [plan-dot-4-phong-nguoi-thue-hop-dong.md](plan-dot-4-phong-nguoi-thue-hop-dong.md), viết báo cáo vào `.bangiao/giao-dien/bao-cao-dot-4.md`; phiên lập kế hoạch (Fable) nghiệm thu. Lời giao việc: [giao-viec-dot-4-5.md](giao-viec-dot-4-5.md).

## 1. Vì sao làm

Khảo sát ngày 13/11/2026 trên `c10b0dc`:

- Ba màn danh sách (Phòng trọ, Người thuê, Hợp đồng) còn kiểu cũ: tiêu đề tô xanh kèm icon và Viết Hoa Từng Chữ, nút chính xanh lá (`btn-success`), bảng `table-bordered table-striped`, đầu modal tô màu đặc chữ trắng, nút "Đóng" màu đỏ.
- Sơ đồ phòng dùng 9 mã màu gõ cứng trong `quanlyphongtro.css`, thẻ nền chuyển màu, huy hiệu tròn nhấp nháy. Modal chi tiết tô đầu màu đỏ (`#e53935`) cho phòng "Đã thuê", dễ hiểu nhầm là lỗi.
- `bg-light` ở 10 chỗ (vùng lọc, đầu bảng, khung QR) giữ nền sáng ở chế độ tối; `QuanLyHopDong.cshtml` phải vá bằng một khối `<style>` riêng.
- `hopdong.css` dùng biến `--tb-primary` không tồn tại, nên bước đang chọn của thanh bước luôn là `#206bc4`; vòng tròn bước có nền sáng gõ cứng.
- `_DangKyDichVuPartial.cshtml` mang khối `<style>` hơn 100 dòng với 30 mã màu, in ra ở cuối trang của hai màn.
- Ngày giờ JS thiếu múi giờ ở 6 chỗ (script soát báo LỖI).
- Dưới 768px ba bảng danh sách cuộn ngang. Nút "Thêm người thuê" bị ẩn dưới 576px (`d-none d-sm-inline-block`), điện thoại không thêm được.
- Các trang Chi tiết hợp đồng, Thanh lý, Gia hạn, Chi tiết người thuê (viết 04–05/11) đã sạch nhưng chưa dùng khung đầu trang chung, bảng chưa thành thẻ, số tiền chưa căn số.

Script soát: 48 LỖI trong các file của đợt này (`_DangKyDichVuPartial` 11, `QuanLyHopDong` 10, `QuanLyPhongTro` 9, `quanlyphongtro.css` 9, `hopdong.css` 6, `SoDoPhong` 2, `QuanLyNguoiThue` 1).

## 2. Thiết kế

Áp cho mọi màn của đợt (chi tiết từng file ở plan):

1. **Khung đầu trang chung**: `<h2 class="page-title">` tên ngắn theo menu ("Phòng trọ", "Người thuê", "Hợp đồng"), không icon, không tô màu; một nút chính `btn btn-primary` ("Thêm phòng", "Thêm người thuê", "Tạo hợp đồng"), hiện ở mọi độ rộng. Chữ viết hoa đầu câu.
2. **Bảng**: `table table-vcenter table-hover ts-bang-the`, bỏ viền kẻ và sọc; dưới 768px mỗi dòng một thẻ. Cột tiền và số căn phải, `num-tabular`. Không đổi thứ tự cột.
3. **Modal**: đầu modal không tô màu; nút "Đóng" là `btn btn-secondary`, nút lưu là `btn btn-primary`. Hộp xác nhận SweetAlert không tự đặt màu nút (layout đã đặt xanh thương hiệu, đỏ cho cảnh báo).
4. **Màu** chỉ lấy từ biến `--ts-*` / `--tblr-*`; không còn `bg-light`, `bg-white`, mã màu gõ cứng.

Riêng từng màn:

5. **Phòng trọ, sơ đồ**: thẻ phòng nền trắng, số phòng đậm, nhãn trạng thái nhạt (Trống xanh lá, Đã thuê xanh thương hiệu nhạt, Bảo trì vàng), giá thuê, tên người thuê, dòng "20 m² · Còn 12 ngày" (phần "Còn n ngày" màu vàng khi sắp hết hạn), dòng "Nợ 1.200.000 ₫" màu đỏ khi có nợ. Bỏ nền chuyển màu, dải màu trên đầu, ô icon đặc, huy hiệu nhấp nháy. Dưới 576px lưới hai cột.
6. **Phòng trọ, bốn ô số liệu** (Phòng trống, Đã thuê, Đang nợ, Sắp hết hạn) thành ô nhạt bấm được: bấm ô nào thì lọc sơ đồ theo ô đó (gọi đúng nút lọc sẵn có). Hàng nút lọc dùng một màu khi chọn (nền thương hiệu nhạt).
7. **Phòng trọ, modal chi tiết**: đầu modal không tô màu theo trạng thái; trạng thái hiện bằng nhãn cạnh tên phòng.
8. **Sơ đồ cũ `/PhongTros/SoDoPhong`**: sửa tối thiểu (lỗi soát, dùng chung CSS thẻ phòng mới), không thiết kế riêng.
9. **Hợp đồng**: vùng lọc nằm trong thẻ, không nền riêng; menu ⋯ của dòng dùng icon `ti-dots-vertical`. Modal tạo và sửa hợp đồng giữ dạng bốn bước; thanh bước dùng màu nền chung và đọc được ở chế độ tối.
10. **Đăng ký dịch vụ** (`_DangKyDichVuPartial`): CSS chuyển ra `wwwroot/css/dang-ky-dich-vu.css`, màu theo biến.
11. **Chi tiết hợp đồng, Thanh lý, Gia hạn, Chi tiết người thuê**: khung đầu trang chung có đường dẫn quay lại; bảng thành thẻ; số tiền `num-tabular`; bốn ô tiền của Thanh lý dùng ô số liệu nhạt.

Không làm: đổi controller, service, DTO, schema, endpoint, điều kiện hiện nút, phép tính; đổi `id`, `name`, `data-*`, tên hàm JS toàn cục mà view khác hoặc test dùng; đổi `style="display:none"` sang `d-none` ở phần tử JS bật bằng `.show()` / `.toggle()`; đổi thứ tự cột DataTables; đụng `HopDong/Print.cshtml` (bản in); chuyển modal tạo hợp đồng thành trang riêng.

Thất bại dù chạy được: một nút hay menu trước có nay không bấm được; nhân viên thấy nút chỉ dành cho Admin (thêm, xóa phòng); ngày hiển thị lệch một ngày so với trước; số tiền ở Thanh lý, Gia hạn, Thu tiền nhanh khác trước; trang tràn ngang ở 375px.

## 3. Điểm người dùng đã xác nhận (13/11/2026)

1. Thẻ phòng: thẻ trắng, nhãn trạng thái nhạt, cảnh báo bằng chữ; bốn ô thống kê thành ô số liệu nhạt bấm để lọc.
2. Màn `/PhongTros/SoDoPhong` sửa tối thiểu.
3. Làm từng đợt, xong đợt 4 dừng chờ nghiệm thu rồi mới làm đợt 5; mỗi việc một commit cục bộ trên nhánh, không push.

Người lập kế hoạch tự chọn theo quy ước đã có (không hỏi lại): nút "Đóng" của modal là `btn btn-secondary` như màn Hóa đơn; nhãn "Đã thuê" dùng màu thương hiệu nhạt; modal tạo hợp đồng giữ nguyên dạng.

## 4. Kết quả

Chưa thực thi. Agent thực thi điền bảng việc và commit vào đây khi xong, ghi "chờ nghiệm thu".

## 5. Còn để ý

Agent thực thi và người nghiệm thu ghi vào đây những gì thấy mà không sửa.
