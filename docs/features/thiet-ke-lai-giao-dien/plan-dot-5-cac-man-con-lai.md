# Kế hoạch đợt 5: các màn còn lại của cổng quản lý và màn con của khách thuê

Thiết kế và lý do: [spec-dot-5-cac-man-con-lai.md](spec-dot-5-cac-man-con-lai.md). Tài liệu này là lệnh làm việc cho agent thực thi. Đường dẫn file tính từ `src/QuanLyChoThuePhongTroWeb.Web/` (viết tắt `Web/`), trừ khi ghi rõ.

**Điều kiện bắt đầu:** `.bangiao/giao-dien/nghiem-thu-dot-4.md` có dòng đầu `NGHIEM THU: DAT`. Chưa có thì dừng và báo người dùng.

## 0. Trước khi bắt đầu

1. Đọc `AGENTS.md`, skill `.agents/skills/giao-dien-quan-ly/SKILL.md`, spec đợt 5, và [kế hoạch đợt 4](plan-dot-4-phong-nguoi-thue-hop-dong.md) mục 1 (ràng buộc) và mục 2 (bảng quy đổi Q1–Q12, bảng màu, quy tắc ngày). **Mục 1 và mục 2 của kế hoạch đợt 4 áp nguyên cho đợt này**, kể cả lệnh kiểm `id` không mất.
2. Đọc `.bangiao/giao-dien/nghiem-thu-dot-4.md`: mọi ghi chú "áp cho đợt 5" trong đó là bắt buộc.
3. `git status --short` phải sạch; nhánh `feature/thiet-ke-lai-giao-dien`. Ghi `git rev-parse --short HEAD` làm **mốc**.
4. Số dòng trong tài liệu này là của commit `c10b0dc`, chỉ để định vị.

## 1. Ràng buộc thêm của đợt 5

- Không sửa `wwwroot/js/khachthue-thanhtoan.js`, `khachthue-vnpay.js`, `hoadon-thanhtoan-quanly.js`, `wwwroot/css/staff-guest-management.css`, `Areas/KhachThue/Views/Shared/_CustomerRequestSummary.cshtml`, `NguoiDung/DangNhap.cshtml`, các view biên lai (`BienLaiThanhToan/*`, `ThanhToanHoaDon/BienLai*.cshtml`, `Views/Shared/_BienLai*.cshtml`), `wwwroot/css/bienlai.css`.
- Ở màn Hóa đơn của khách thuê: không đổi `id`, `data-*`, lớp mà hai file JS thanh toán bắt sự kiện; không đổi thứ tự các bước thanh toán. `grep` từng `id` và lớp trong `wwwroot/js/khachthue-*.js` trước khi sửa thẻ chứa nó.
- Hộp xác nhận xóa, hủy, thu hồi: giữ `icon: 'warning'`, `showCancelButton: true` và chữ nút xác nhận; chỉ xóa thuộc tính màu (Q9).
- Màn có nút chỉ dành cho Admin (`laAdmin`, `User.IsInRole("Admin")`): giữ nguyên điều kiện Razor bao quanh nút.

## 2. Các việc

Làm theo thứ tự V1 → V10. V1 commit xong mới làm việc khác. Nếu chia subagent song song: chỉ V2 đến V8; mỗi việc sửa bộ file riêng liệt kê dưới đây.

### V1. Bổ sung lớp dùng chung

File: `wwwroot/css/trosync-nen.css`.

1. Trong khối `@media (max-width: 767.98px)` của `.ts-bang-the`: `.ts-cong-khach-thue table.ts-bang-the tbody tr { border-radius: var(--ts-radius-tenant, 16px); }` (lớp `ts-cong-khach-thue` có sẵn trên `body` của `_LayoutKhachThue`).

Kiểm: không màn nào của cổng quản lý đổi dạng (bo góc thẻ vẫn 8px).

Commit: `feat(giao-dien): thẻ bảng của cổng khách thuê bo góc theo cổng`.

### V2. Thiết lập: Chi nhánh, Tài khoản, Phân công chi nhánh

File: `Areas/QuanLyNhaTro/Views/ChiNhanhs/QuanLyChiNhanh.cshtml`, `NguoiDung/QuanLyTaiKhoanDangNhap.cshtml`, `PhanCongChiNhanh/Index.cshtml`.

1. Cả ba: Q1 ("Chi nhánh", "Tài khoản", "Phân công chi nhánh"), Q2 ("Thêm chi nhánh", "Thêm tài khoản"; bỏ `d-none d-sm-inline-block`), Q3, Q4, Q5, Q7, Q8, Q9, Q10, Q12.
2. `QuanLyChiNhanh.cshtml` dòng 173 và `QuanLyTaiKhoanDangNhap.cshtml` dòng 127 (có giờ): Q11. Dạng có giờ khi giá trị là mốc UTC: `toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false })`.
3. `QuanLyTaiKhoanDangNhap.cshtml`: khối `<style>` Select2 cuối file (dòng 485 trở đi) đã dùng biến, giữ lại; `#ffffff` trong đó đổi thành `var(--tblr-white)`; kiểm `var(--tblr-color)` có giá trị không (`getComputedStyle(document.body).getPropertyValue('--tblr-color')`), rỗng thì đổi sang `var(--tblr-body-color)`. Nhãn vai trò và trạng thái (dòng 213–226): bỏ lớp `text-*` đi kèm `bg-*-lt` (nền chung đã đặt màu chữ đủ tương phản), chữ viết hoa đầu câu ("Nhân viên", "Khách thuê").
4. `PhanCongChiNhanh/Index.cshtml`: nút "Thu hồi" (dòng 203, sinh bằng JS) thành `btn btn-outline-danger btn-sm`, giữ lớp `btn-thu-hoi` và `data-chi-nhanh-id`; bảng trong modal (dòng 57) theo Q4 dạng bảng nhỏ.

Kiểm (đăng nhập Admin): thêm, sửa, xóa một chi nhánh thử và một tài khoản thử; phân công rồi thu hồi một chi nhánh cho nhân viên thử; đăng nhập nhân viên thì ba màn này bị chặn như trước.

Commit: `feat(giao-dien): chi nhánh, tài khoản, phân công theo khung trang chung, bảng thành thẻ`.

### V3. Thiết lập: Dịch vụ, Điều khoản hợp đồng

File: `Areas/QuanLyNhaTro/Views/DichVu/Index.cshtml`, `DieuKhoanMau/Index.cshtml`.

1. Cả hai: Q1 ("Dịch vụ", "Điều khoản hợp đồng"), Q2, Q3, Q4, Q5, Q6, Q7, Q8, Q9, Q10.
2. `DichVu/Index.cshtml`: giữ hai tab và mọi `id` (`tab-danh-muc-dich-vu`, `tab-bang-gia`, `dichVuTable`, `bangGiaTable`, `btnOpenAddDichVu`, `btnOpenAddGia`, `filterChiNhanh`). Tab dùng `nav nav-tabs card-header-tabs` trong `card-header`. Tiêu đề phụ trong tab (`<h4>` kèm icon) bỏ icon. Nút: "Thêm dịch vụ" (giữ điều kiện `laAdmin`), "Thêm giá". Ô lọc chi nhánh: `form-label` + `form-select`, "Tất cả chi nhánh".
3. Nhãn `.badge-service-required` / `.badge-service-optional`: giữ tên lớp (JS sinh ra ở dòng 386–387), viết lại CSS bằng biến: bắt buộc dùng `color: var(--ts-brand-text); background: var(--ts-brand-soft)`; tùy chọn dùng `color: var(--ts-ink-muted); background: var(--tblr-bg-surface-secondary)`; không viền; xóa hai quy tắc `[data-bs-theme="dark"]`. Xóa khối `.swal2-custom-popup` trùng với layout.
4. Cột đơn giá của `bangGiaTable`: Q6; tiền qua `Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })` nếu đang nối chuỗi.
5. `DieuKhoanMau/Index.cshtml`: hai nút thêm (`btnOpenAddModal`, `btnOpenAddModalEmpty`) đều `btn btn-primary`, nút đầu trang bỏ `d-none d-sm-inline-block`; dòng 187 `confirmButtonColor: isUpdate ? ... : ...` xóa cả biểu thức; `modal-header ... rounded-top-3` chỉ còn `modal-header`.

Kiểm: Admin thêm, sửa, xóa một dịch vụ thử và một dòng giá thử; nhân viên không thấy "Thêm dịch vụ"; thêm, sửa, xóa một điều khoản thử; cả hai tab ở 375px thành thẻ.

Commit: `feat(giao-dien): dịch vụ, điều khoản theo khung trang chung, nhãn dùng màu nền`.

### V4. Vận hành: Sự cố, Thông báo (quản lý), Việc cần làm, Đổi mật khẩu

File: `Areas/QuanLyNhaTro/Views/YeuCauSuCo/Index.cshtml`, `ThongBao/Index.cshtml`, `ViecCanLam/Index.cshtml`, `DoiMatKhau/Index.cshtml`.

1. `YeuCauSuCo/Index.cshtml`: Q1 ("Sự cố và sửa chữa"), Q3 (bỏ `bg-light`, `font-weight-medium`, `border-secondary`; ba ô lọc `col-12 col-sm-auto`; giữ `name`, `onchange="this.form.submit()"`, các `selected`), Q4 (`suCoTable` là DataTables dựng trên dòng Razor có sẵn: thêm `ts-bang-the`, kiểm nhãn thẻ sau khi DataTables vẽ), Q7, Q8, Q9. Dòng 118: `@VndFormat.Money(item.ChiPhiSuaChua)` (thêm `@using QuanLyChoThuePhongTroWeb.Web.Helpers` nếu chưa có), cột chi phí `text-end num-tabular`. Dòng 74: `@item.NgayGui.AddHours(7).ToString("dd/MM/yyyy HH:mm")` đổi sang `@VietnamTime.Format(item.NgayGui, "dd/MM/yyyy HH:mm")`; so kết quả trước và sau trên cùng một dòng.
2. `ThongBao/Index.cshtml`: Q1 ("Thông báo"; `ViewData["Title"]` cùng chữ), Q3, Q4 (bỏ `text-nowrap`, `datatable`, `table-striped`). Hai nút đầu trang: thay `<svg>` bằng `<i class="ti ti-checks me-1">`, `<i class="ti ti-trash me-1">`; "Đánh dấu tất cả đã đọc" là `btn btn-outline-secondary`, "Xóa tất cả" giữ `btn btn-outline-danger`; dưới 576px hai nút xếp một hàng, không tràn. Xóa hai thẻ `<script src="https://cdn.datatables.net/...">` ở dòng 81–82 (layout đã nạp DataTables) rồi kiểm bảng vẫn tải, phân trang, lọc "Chưa đọc" chạy. Dòng 126 `style="font-weight: ${fontWeight};"` thành lớp `fw-bold` / `fw-normal`, giữ điều kiện.
3. `ViecCanLam/Index.cshtml`: bảng hóa đơn quá hạn (dòng 115) thêm `ts-bang-the`; cột tiền thêm `num-tabular`. Không đổi gì khác (màn này đã làm ở đợt 2).
4. `DoiMatKhau/Index.cshtml`: Q1 ("Đổi mật khẩu"). Không đổi form, `name`, thông báo lỗi (các test `ForcedPasswordChange*`, `ManagerChangePasswordTests` dò màn này).

Kiểm: lọc sự cố theo ba ô; mở modal xử lý một sự cố thử; đánh dấu đã đọc và xóa một thông báo; 375px bốn màn không tràn.

Commit: `feat(giao-dien): sự cố, thông báo, việc cần làm theo khung trang chung, bảng thành thẻ`.

### V5. Báo cáo

File: `Areas/QuanLyNhaTro/Views/BaoCao/Index.cshtml`, `wwwroot/js/bao-cao.js`.

1. Q1 ("Báo cáo"). Trước khi đổi chuỗi "Báo cáo thống kê", `grep` trong `tests/` (`BaoCaoPageTests`, `BaoCaoPhanQuyenTests`, `MenuNhomTheoViecTests`); test dò chuỗi nào thì giữ chuỗi đó ở đúng chỗ test đọc.
2. Bảng báo cáo **không** thêm `ts-bang-the`: giữ cuộn ngang trong thẻ. Thay khối `<style>` `.bc-pane .table-responsive { overflow-x: auto !important; }` bằng lớp `ts-cuon-ngang` trên từng `table-responsive` của `.bc-pane`; quy tắc `white-space: nowrap` của ô giữ lại.
3. `bao-cao.js` dòng 277, 355 (và mọi mã màu khác trong file): đọc màu lúc vẽ bằng một hàm như `mauTheme()` của `wwwroot/js/dashboard.js` (`getComputedStyle(document.body).getPropertyValue('--tblr-primary')`, `--ts-wait`, `--ts-ok`). Không đổi dữ liệu, nhãn, trục của biểu đồ, không đổi lệnh gọi API hay xuất Excel.

Kiểm: bốn tab báo cáo vẽ được biểu đồ ở sáng và tối (đổi giao diện rồi tải lại trang); xuất Excel một tab; 375px bảng cuộn ngang trong thẻ, trang không tràn; nhân viên không thấy số doanh thu ngoài phạm vi như trước (`BaoCaoPhanQuyenTests` PASS ở lượt test cuối).

Commit: `feat(giao-dien): báo cáo dùng màu biểu đồ theo nền chung`.

### V6. Khách thuê: Hóa đơn, Lịch sử thanh toán, kết quả VNPay, Điện nước

File: `Areas/KhachThue/Views/HoaDon/Index.cshtml`, `LichSuThanhToan/Index.cshtml`, `ThanhToanHoaDon/VnPayKetQua.cshtml`, `ChiSoDienNuoc/Index.cshtml`.

1. `HoaDon/Index.cshtml`: Q1 ("Hóa đơn và thanh toán", theo menu khách thuê; `ViewBag.Title` cùng chữ, thay dấu "&"), Q4 (`hoaDonTable` thêm `ts-bang-the`; giữ lớp `kt-hoadon-list-scroll`), Q5 ("Mã hóa đơn", "Kỳ", "Tổng tiền", "Trạng thái", "Ngày lập"; cột cuối `data-the-nhan=""`), Q6 (`font-weight-bold` ở dòng 47, 107; cột tổng tiền `text-end num-tabular`), Q7, Q8, Q12. Bảng sinh bằng chuỗi trong modal chi tiết (dòng 229, 266, 286): bọc `ts-cuon-ngang` nếu chưa cuộn được ở 375px; không thêm `ts-bang-the`. **Không sửa phần thanh toán.**
2. `LichSuThanhToan/Index.cshtml`: Q1 ("Lịch sử thanh toán"), Q4, Q5, Q6 (`font-weight-bold` ở dòng 67, 71, 82; `text-right` → `text-end`; cột tiền thêm `num-tabular`). Số tiền đã thu không tô đỏ: bỏ `text-danger` ở cột số tiền.
3. `VnPayKetQua.cshtml`, `ChiSoDienNuoc/Index.cshtml`: Q12. `ChiSoDienNuoc` đã làm ở đợt 3, không đổi gì khác.

Kiểm (đăng nhập khách thuê thử có hóa đơn): danh sách hóa đơn ở 375px thành thẻ; mở chi tiết; mở bước thanh toán QR và VNPay tới trước nút xác nhận cuối, không gửi thanh toán thật ngoài môi trường test; `TenantInvoicePortalTests`, `TenantOnlinePaymentPortalTests`, `TenantOnlinePaymentSecurityTests`, `VnPayCheckout*Tests`, `MeterImageTenantPortalTests` PASS ở lượt test cuối.

Commit: `feat(giao-dien): hóa đơn và lịch sử thanh toán của khách thuê thành thẻ trên điện thoại`.

### V7. Khách thuê: Hợp đồng, Hồ sơ, Sự cố, Thông báo

File: `Areas/KhachThue/Views/HopDong/Index.cshtml`, `HoSo/Index.cshtml`, `SuCo/Index.cshtml`, `ThongBao/Index.cshtml`.

1. Cả bốn: Q1 (tên theo menu khách thuê: "Hợp đồng thuê phòng", "Hồ sơ của tôi", "Báo sự cố", "Thông báo"; tiêu đề không icon; `ViewBag.Title` / `ViewData["Title"]` cùng chữ), Q4, Q5, Q6, Q7, Q8, Q9.
2. `HopDong/Index.cshtml`: bảy chỗ `font-weight-bold` (dòng 52, 62, 73, 76, 142, 223, 241) → `fw-bold`; hai bảng trong modal (dòng 82, 97) theo Q4 dạng bảng nhỏ; hai bảng thông tin không viền (dòng 53, 63) giữ.
3. `HoSo/Index.cshtml`: chỉ Q1 và soát lớp; không đổi form, `name`, `id`.
4. `SuCo/Index.cshtml`: nút "Báo cáo mới" (dòng 19, mở `#createSuCoModal`) bỏ `d-none d-sm-inline-block` để điện thoại báo được sự cố, đổi chữ thành "Báo sự cố"; dòng 47, 86 đổi `.AddHours(7).ToString("dd/MM/yyyy HH:mm")` sang `VietnamTime.Format(..., "dd/MM/yyyy HH:mm")` (thêm `@using QuanLyChoThuePhongTroWeb.Web.Helpers` nếu chưa có), so kết quả trước và sau trên cùng một dòng; `suCoKhachThueTable` thêm `ts-bang-the`.
5. `ThongBao/Index.cshtml`: làm y như màn Thông báo của cổng quản lý ở V4.2 (hai file gần như giống hệt nhau, chỉ khác `Layout`).

Kiểm: khách thuê thử xem hợp đồng và mở modal xem nhanh; gửi một báo sự cố thử từ độ rộng 375px; đánh dấu đã đọc một thông báo; bốn màn ở 375px sáng và tối không tràn; `TenantRentalHistoryTests`, `IncidentDeleteAuthorizationTests`, `ThongBaoChiNhanhTests` PASS ở lượt test cuối.

Commit: `feat(giao-dien): hợp đồng, hồ sơ, sự cố, thông báo của khách thuê theo khung trang chung`.

### V8. Đón khách (chạm nhẹ) và việc tồn của Lịch sử thanh toán

File: `Areas/QuanLyNhaTro/Views/TinPhong/Index.cshtml`, `LichXemPhong/Index.cshtml`, `GiuCho/Index.cshtml`, `GiuCho/ChiTiet.cshtml`, `GiuCho/ThongBaoDatCoc.cshtml`, `GiuCho/XacNhanThue.cshtml`; `Areas/QuanLyNhaTro/Views/LichSuThanhToan/Index.cshtml`.

Đón khách, chỉ được làm:

1. Mở từng màn ở 375px sáng, 375px tối, 1366px tối; ghi `scrollWidth`, chụp ảnh.
2. Bảng danh sách của `TinPhong/Index`, `LichXemPhong/Index`, `GiuCho/Index`: thêm `ts-bang-the`. Nếu thẻ vỡ (ô có ảnh bìa, nhóm nút, form trong ô) mà không sửa được bằng lớp tiện ích trong view thì gỡ `ts-bang-the`, bọc `ts-cuon-ngang`, ghi lý do.
3. Sửa lớp màu sáng cố định (`bg-light`, `bg-white`, `text-dark`) nếu có.
4. Mọi vấn đề khác (cần sửa `staff-guest-management.css`, cần đổi cấu trúc): **không sửa**, ghi vào báo cáo mục "Chuyển cho thuan" kèm ảnh và file:dòng.

Lịch sử thanh toán (quản lý):

5. Nhóm nút "Khoảng thời gian" (dòng 135–146) tràn khung lọc ở khoảng 1366px: sửa bằng lưới (đổi lớp cột của các ô lọc, cho nhóm nút và ô ngày xuống dòng trong cột của nó). Không đổi `id` (`khoangNgay`, `btnDateAll`, `btnDateToday`, `btnDateWeek`, `btnDateMonth`, `filterDateRange`, `ghiChuKy`), không sửa `lich-su-thanh-toan.js`. Đo ở 992, 1024, 1200, 1366, 1440px: mép phải của nhóm nút nằm trong thẻ lọc. `NhomThuTienDot3Tests` và `LichSuThanhToanLocTests` vẫn PASS.

Commit: `feat(giao-dien): màn đón khách thành thẻ trên điện thoại, khung lọc lịch sử thanh toán không tràn`.

### V9. Test tĩnh

File mới: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/CacManConLaiDot5Tests.cs`, theo mẫu `NhomThuTienDot3Tests.cs` (chỉ đọc file nguồn, không cần DB, không `[Collection]`).

Phải có:

1. Theory trên các view đã làm đầy đủ ở V2–V4, V6, V7 (không gồm nhóm Đón khách, Báo cáo): không còn `bg-light`, `btn-success`, `table-bordered`, `thead-light`, `modal-header bg-`, `confirmButtonColor`, `cancelButtonColor`, `font-weight-`, `text-right`, `page-title text-primary`.
2. Theory bảng chính có `ts-bang-the`: `chiNhanhTable`, `nguoiDungTable`, `phanCongTable`, `dichVuTable`, `bangGiaTable`, `dieuKhoanTable`, `suCoTable`, `thongBaoTable` (hai cổng), `hoaDonTable` và `lichSuTable` của khách thuê, `suCoKhachThueTable`, `hopDongTable` của khách thuê.
3. Nút thêm không còn `d-none`: `btnOpenAddModal` ở Chi nhánh, Tài khoản, Điều khoản; nút mở `#createSuCoModal` của khách thuê.
4. Hai view Thông báo không còn `cdn.datatables.net/1.13.6/js`.
5. `bao-cao.js` không còn `#206bc4`, `#f59f00`; `BaoCao/Index.cshtml` không có `ts-bang-the`.
6. `YeuCauSuCo/Index.cshtml` không còn `ToString("N0")`; `Areas/KhachThue/Views/SuCo/Index.cshtml` không còn `AddHours(7)`.
7. Theory giữ `id` cho màn Hóa đơn khách thuê: liệt kê mọi `id` mà `khachthue-thanhtoan.js` và `khachthue-vnpay.js` dùng (lấy bằng `grep -oE "#[A-Za-z][A-Za-z0-9_-]+"` trên hai file, đối chiếu với view ở mốc).
8. Các view khách thuê có CSS riêng (`HoaDon`, `ChiSoDienNuoc`, `VnPayKetQua`): `<link` nằm trong `@section Styles`.
9. `staff-guest-management.css` và `_CustomerRequestSummary.cshtml` không đổi so với mốc thì không cần test; ghi kết quả `git diff --stat MOC -- <hai file>` (rỗng) vào báo cáo.

Commit: `test(giao-dien): chốt đợt 5 các màn còn lại và màn con khách thuê`.

### V10. Rà toàn module, kiểm cuối đợt, báo cáo

1. Script soát trên toàn bộ: `node .agents/skills/giao-dien-quan-ly/scripts/soat-giao-dien.js src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachThue/Views src/QuanLyChoThuePhongTroWeb.Web/wwwroot/css src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js`. LỖI chỉ được còn ở: `guest-*.css`, `khach-vang-lai.css`, `_CustomerRequestSummary.cshtml`, `HopDong/Print.cshtml`, `NguoiDung/DangNhap.cshtml`. LỖI ở file khác: sửa, hoặc ghi lý do không sửa được.
2. `git diff --check MOC`, `git diff --stat MOC`; lệnh kiểm `id` không mất cho từng view đã sửa.
3. `dotnet build QuanLyChoThuePhongTroWeb.sln`; hai bộ unit test; `Web.IntegrationTests` toàn bộ theo skill `chay-test-tich-hop` (các lưu ý như V8 của kế hoạch đợt 4).
4. Trình duyệt như V8 của kế hoạch đợt 4, với ba vai trò: Admin (Thiết lập, Báo cáo), nhân viên (Sự cố, Thông báo, Dịch vụ), khách thuê (bảy màn con). Mỗi màn: 1366px sáng, 1366px tối, 375px sáng, 375px tối.
5. Ảnh `giao-dien-5-NN-mo-ta.png` trong `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo`. Tối thiểu: mỗi màn một ảnh 375px (sáng hoặc tối, xen kẽ) và một ảnh 1366px cho Dịch vụ, Sự cố, Báo cáo, Hóa đơn khách thuê; ba màn Đón khách trước và sau khi chạm.
6. Điền mục 4 và 5 của spec đợt 5, trạng thái "đã code, chờ nghiệm thu". Commit: `docs(giao-dien): kết quả đợt 5`.
7. Viết báo cáo `.bangiao/giao-dien/bao-cao-dot-5.md` theo mẫu ở [giao-viec-dot-4-5.md](giao-viec-dot-4-5.md) rồi **dừng**. Không cập nhật `README.md` của module, `.agents/hien-trang.md`, không gộp nhánh: người nghiệm thu làm sau khi đạt.

## 3. Tiêu chí nghiệm thu

Như mục 4 của kế hoạch đợt 4, thêm:

1. `staff-guest-management.css`, `_CustomerRequestSummary.cshtml`, ba file JS thanh toán không đổi so với mốc.
2. Khách thuê thử đi được tới bước thanh toán QR và VNPay như trước; gửi được báo sự cố từ 375px.
3. Script soát toàn module chỉ còn LỖI ở danh sách loại trừ của V10.1.
4. Mục "Chuyển cho thuan" trong báo cáo có ảnh và file:dòng cho từng vấn đề.
