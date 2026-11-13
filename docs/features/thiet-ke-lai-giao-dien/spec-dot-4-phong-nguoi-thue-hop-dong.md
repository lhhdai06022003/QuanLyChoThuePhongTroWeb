# Spec đợt 4: phòng trọ, người thuê, hợp đồng

- Trạng thái: **đã thực thi xong, chờ nghiệm thu** (13/11/2026).
- Nhánh: `feature/thiet-ke-lai-giao-dien`
- Mốc bắt đầu: `88af77e`
- Báo cáo bàn giao: [.bangiao/giao-dien/bao-cao-dot-4.md](../../../.bangiao/giao-dien/bao-cao-dot-4.md)

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

Đã hoàn thành 8/8 việc trong kế hoạch đợt 4 (chờ nghiệm thu):

| Việc | Commit | Nội dung tóm tắt |
| --- | --- | --- |
| V1 | `9f37a95` | `feat(giao-dien): bảng tĩnh tự gán nhãn thẻ, lớp cuộn ngang dùng chung`: thêm `ganTatCa()` trong `site.js`, lớp `.ts-cuon-ngang` trong `trosync-nen.css`. |
| V2 | `5153eaa` | `feat(giao-dien): đăng ký dịch vụ dùng file CSS riêng và màu nền chung`: tách CSS đăng ký dịch vụ sang `dang-ky-dich-vu.css`, xóa khối style inline trong partial. |
| V3 | `c38d848` | `feat(giao-dien): phòng trọ dùng thẻ phòng trắng, ô số liệu bấm để lọc, bảng thành thẻ`: làm mới thẻ phòng, ô số liệu bấm lọc, bảng danh sách thành thẻ, dọn CSS. |
| V4 | `70f2647` | `feat(giao-dien): người thuê theo khung trang chung, bảng thành thẻ, thêm được trên điện thoại`: khung trang chung, bảng thành thẻ, sửa modal và chi tiết người thuê. |
| V5 | `d29d701`<br>`81677bd` | `feat(giao-dien): hợp đồng theo khung trang chung, thanh bước theo màu nền, bảng thành thẻ`: danh sách hợp đồng, stepper wizard, modal thêm/sửa, partial thành viên. |
| V6 | `451aa60` | `feat(giao-dien): chi tiết hợp đồng, thanh lý, gia hạn theo khung trang chung, bảng thành thẻ`: khung trang chi tiết, thanh lý, gia hạn; giữ nguyên script. |
| V7 | `9d5f3c9` | `test(giao-dien): chốt đợt 4 phòng, người thuê, hợp đồng`: tạo `PhongNguoiThueHopDongDot4Tests.cs` (27/27 test PASS). |
| V8 | `HEAD` | Kiểm tra cuối đợt: `git diff --check` sạch, soat-giao-dien 0 LỖI trên 16 file, build 0 Error, Domain UnitTests PASS, Application UnitTests PASS, Web.IntegrationTests PASS. |

## 5. Còn để ý

1. Nút `btnPhatSinhPhong` trong `QuanLyPhongTro.cshtml` vốn đã bị chú thích trong HTML gốc, được dọn dẹp theo kế hoạch V3.
2. `filterNguoiThue` trong `QuanLyHopDong.cshtml` giữ tùy chọn mặc định là `-- Tất cả --` để tương thích hoàn toàn với test kiến trúc `ContractPagePayloadTests`.
3. Khối `@section Scripts` của `ThanhLy.cshtml` và `GiaHan.cshtml` được bảo toàn nguyên vẹn 100% so với mốc `88af77e` (lệnh kiểm diff in `GIONG`).
4. Bảng thành viên `thanhVienTable` trong modal tạo/sửa hợp đồng áp dụng lớp `ts-bang-the` và hiển thị tốt ở 375px.
