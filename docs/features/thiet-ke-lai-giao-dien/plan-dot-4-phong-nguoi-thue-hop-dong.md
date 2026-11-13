# Kế hoạch đợt 4: phòng trọ, người thuê, hợp đồng

Thiết kế và lý do: [spec-dot-4-phong-nguoi-thue-hop-dong.md](spec-dot-4-phong-nguoi-thue-hop-dong.md). Tài liệu này là lệnh làm việc cho agent thực thi. Đường dẫn file tính từ `src/QuanLyChoThuePhongTroWeb.Web/` (viết tắt `Web/`), trừ khi ghi rõ.

## 0. Trước khi bắt đầu

1. Đọc `AGENTS.md`, skill `.agents/skills/giao-dien-quan-ly/SKILL.md`, spec đợt 4, và [spec-dot-3-nhom-thu-tien.md](spec-dot-3-nhom-thu-tien.md) (cách đợt trước dùng `.ts-bang-the`).
2. `git status --short` phải sạch; đang ở nhánh `feature/thiet-ke-lai-giao-dien`. Ghi `git rev-parse --short HEAD` làm **mốc** (thay vào chữ `MOC` trong mọi lệnh so sánh bên dưới).
3. Đọc trọn từng file trước khi sửa. Số dòng trong tài liệu này là của commit `c10b0dc`, chỉ để định vị.

## 1. Ràng buộc

Cấm:

- Sửa file `.cs` trong `src/`, `.csproj`, `Program.cs`, `appsettings*`, fixture test, migration, `HopDong/Print.cshtml`, mọi thứ của cổng khách vãng lai (`Web/Controllers`, `_PublicRoomLayout`, `guest-*.css`, `khach-vang-lai.css`).
- Đổi hay xóa `id`, `name`, `data-*`, `window.<man>Config`, tên hàm JS toàn cục (`showDetail`, `openEditFromGrid`, `openDichVuPhong`, `openQuickPayment`, `openQuickContract`, `deleteRoom`, `toggleKebab`, `renderDangKyDichVu`...), lớp mà JS bắt sự kiện (`btn-edit`, `btn-delete`, `filter-btn`, `kebab-*`, `chk-dieukhoan`...).
- Đổi URL, tham số, điều kiện `if` hiện hay ẩn nút, phép tính, thứ tự và `data`/`name` của cột DataTables (`phongTroTable` lọc chi nhánh bằng `column(1)`, `hopDongTable` sắp theo cột 4).
- Đổi `style="display:none"` sang `d-none` trên phần tử JS bật bằng `.show()`, `.hide()`, `.toggle()`.
- Push, merge, đổi nhánh, tạo file `.ps1`, đọc hay sửa `appsettings.json`, trỏ test vào DB không có đuôi `_test`.

Bắt buộc:

- Mỗi việc một commit cục bộ, message tiếng Việt dạng `feat(giao-dien): ...`, không có dòng `Co-Authored-By` hay ghi công AI.
- Sau mỗi việc: `dotnet build QuanLyChoThuePhongTroWeb.sln` và script soát (`node .agents/skills/giao-dien-quan-ly/scripts/soat-giao-dien.js <file>`) trên file vừa sửa, 0 LỖI.
- Trước khi đổi một chuỗi hiển thị hay một lớp, `grep` chuỗi đó trong `tests/` và `Web/wwwroot/js/`. Test đang dò chuỗi nào thì giữ chuỗi đó, ghi vào báo cáo.
- Gặp việc phải vi phạm một điều cấm mới làm được: dừng việc đó, ghi vào báo cáo mục "Cần người dùng quyết", làm việc khác.
- Một việc sửa hai vòng chưa đạt, hoặc FAIL không tìm ra nguyên nhân sau một lượt điều tra: dừng việc đó, ghi hồ sơ vào báo cáo (theo luật "Opus bí thì chuyển Fable" trong `AGENTS.md`).

Kiểm `id` không mất, chạy cho từng view đã sửa. Lệnh phải không in ra gì, hoặc từng dòng in ra có lời giải thích trong báo cáo:

```bash
f=src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml
comm -23 <(git show MOC:$f | grep -oE 'id="[^"]+"' | sort -u) <(grep -oE 'id="[^"]+"' $f | sort -u)
```

## 2. Quy đổi chung (áp cho mọi file của đợt)

| Mã | Đang có | Đổi thành |
| --- | --- | --- |
| Q1 | `<h2 class="page-title text-primary fw-bold d-flex ..."><i ...></i> Quản Lý X</h2>` | `<h2 class="page-title">X</h2>`, tên theo menu, viết hoa đầu câu. `ViewData["Title"]` cùng chữ |
| Q2 | Nút chính `btn btn-success fw-bold shadow-sm`, có khi kèm `d-none d-sm-inline-block` | `btn btn-primary`, icon `ti ti-plus me-1`, hiện ở mọi độ rộng, chữ viết hoa đầu câu |
| Q3 | `card border-0 shadow-sm`, `card-body p-4`, vùng lọc `bg-light ... border-dashed` | `card`, `card-body`; vùng lọc là `card-body border-bottom` không lớp nền; nhãn lọc `form-label` không icon, không `fw-bold` |
| Q4 | `table table-bordered table-striped [text-nowrap] [datatable]`, `thead` có `thead-light` hoặc `bg-light` | `table table-vcenter table-hover w-100 ts-bang-the`, `thead` không lớp. Bảng nhỏ trong modal: `table table-sm table-vcenter`, bọc trong `<div class="table-responsive ts-cuon-ngang">` |
| Q5 | `<th>` Viết Hoa Từng Chữ | Viết hoa đầu câu; giữ `width` nếu có. Cột thao tác: `<th class="w-1" data-the-nhan=""><span class="visually-hidden">Thao tác</span></th>` |
| Q6 | Cột tiền, số căn trái hoặc `text-right` | `text-end num-tabular`. `text-right` → `text-end`, `mr-*` → `me-*`, `font-weight-*` → `fw-*`, `font-italic` → `fst-italic` |
| Q7 | `modal-header` kèm `bg-primary`, `bg-success`, `bg-primary-lt`, `text-white`; `modal-title text-white`; `btn-close-white` | `modal-header`, `modal-title`, `btn-close` không lớp màu |
| Q8 | Chân modal: `btn btn-danger` "Đóng" có icon; nút lưu `btn-success`, hoặc JS đổi qua lại `btn-primary` và `btn-success` | "Đóng": `btn btn-secondary`, không icon. Nút lưu: `btn btn-primary`. JS chỉ đổi chữ nút ("Thêm ..." / "Lưu thay đổi"), bỏ `.removeClass('btn-...').addClass('btn-...')` |
| Q9 | `Swal.fire({ ..., confirmButtonColor: '#3085d6' hoặc '#d33', cancelButtonColor: '#6c757d' })` | Xóa hai thuộc tính màu (layout tự đặt: xanh thương hiệu, đỏ khi `icon` là `warning` hoặc `error`) |
| Q10 | Mã màu, `rgba(...)` gõ cứng trong CSS, `style=""`, JS | Biến theo bảng màu bên dưới |
| Q11 | `new Date(x).toLocaleDateString('vi-VN')` không `timeZone` | Xem quy tắc ngày bên dưới |
| Q12 | `<link>` CSS nằm giữa view | `@section Styles { <link rel="stylesheet" href="~/css/....css" asp-append-version="true" /> }`. Trong `.cshtml`, `@media` viết là `@@media` |

Bảng màu (Q10):

| Màu gõ cứng | Chữ | Nền đặc, chấm | Nền nhạt |
| --- | --- | --- | --- |
| Xanh dương (`#206bc4`, `#0d6efd`, `#6ea8fe`, `rgba(32, 107, 196, ...)`, `--tb-primary`) | `var(--ts-brand-text)` | `var(--ts-brand)` | `var(--ts-brand-soft)` |
| Xanh lá (`#2fb344`, `#198754`, `#43a047`, `--tb-success`) | `var(--ts-ok)` | `var(--ts-ok)` | `var(--ts-ok-soft)` |
| Vàng cam (`#f59f00`, `#fb8c00`) | `var(--ts-wait)` | `var(--ts-wait)` | `var(--ts-wait-soft)` |
| Đỏ (`#d63939`, `#e53935`) | `var(--ts-bad)` | `var(--ts-bad)` | `var(--ts-bad-soft)` |
| Xám chữ (`#64748b`, `#666`, `#626976`, `#495057`, `#adb5bd`) | `var(--ts-ink-muted)` | | |
| Chữ chính (`#1e293b`, `#f4f6fa`) | `var(--ts-ink)` | | |
| Viền (`#e9ecef`, `#dee2e6`, `#dadcde`, `#2f3e56`, `rgba(101, 109, 119, 0.16)`) | | `var(--tblr-border-color)` | |
| Nền (`#fff`, `#ffffff`, `#1b2434`) | | `var(--tblr-bg-surface)` | |
| Nền phụ (`#f1f3f5`, `#f6f8fa`, `#f8f9fa`, `#111926`) | | `var(--tblr-bg-surface-secondary)` | |

Sau khi đổi sang biến, xóa các quy tắc `html[data-bs-theme="dark"] ...` chỉ để vá màu cho chế độ tối. Chữ trắng trên nền thương hiệu đặc (`color: #fff`) được giữ, kèm một dòng chú thích. Ảnh mã QR giữ nền trắng (mã phải quét được ở chế độ tối): dùng một lớp trong file CSS của màn, không dùng `bg-white`.

Quy tắc ngày (Q11). Thêm `timeZone` bừa sẽ làm lệch ngày, nên với **từng** chỗ script soát báo:

1. Mở màn trên trình duyệt, xem JSON thật ở tab Network (hoặc đọc DTO và store) để biết giá trị có hậu tố `Z` hay offset không.
2. Có `Z` hoặc offset (mốc UTC): `new Date(x).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' })`.
3. Không có `Z` (ngày thuần như ngày sinh, hoặc giờ Việt Nam không offset): không qua `Date`. Ghép từ chuỗi: `String(x).slice(0, 10).split('-').reverse().join('/')`.
4. Ghi vào báo cáo bảng: file:dòng, trường, mẫu giá trị thật, cách đã chọn. So ngày hiển thị trước và sau khi sửa trên cùng một bản ghi: phải giống nhau.

## 3. Các việc

Làm theo thứ tự V1 → V8. V1 và V2 phải commit xong mới làm việc khác (các việc sau dựa vào chúng). Nếu chia subagent song song: chỉ V3, V4, V5, V6; mỗi việc sửa bộ file riêng liệt kê dưới đây, không ai sửa file của việc khác.

### V1. Lớp dùng chung

File: `wwwroot/js/site.js`, `wwwroot/css/trosync-nen.css`.

1. `site.js`, trong IIFE `TroBangThe`: thêm hàm `ganTatCa()` gọi `gan(table)` cho mọi `table.ts-bang-the` **không** phải DataTables (`$.fn.dataTable.isDataTable(table)` là `false`, hoặc trang không nạp DataTables); chạy khi `DOMContentLoaded` (hoặc ngay nếu tài liệu đã tải xong). Xuất `window.TroBangThe = { gan: gan, ganTatCa: ganTatCa }`. Lý do: bảng Razor tĩnh (Chi tiết hợp đồng, Thanh lý...) hiện không được gán `data-label`, vì chỉ có sự kiện `draw.dt` gán nhãn.
2. `trosync-nen.css`, ngoài khối `@media`: `.table-responsive.ts-cuon-ngang { overflow-x: auto !important; }`. Quy tắc này thắng `overflow: visible` của `site.css`; dùng cho bảng nhỏ trong modal và bảng nhiều cột không thành thẻ.
3. `trosync-nen.css`, trong khối `@media (max-width: 767.98px)` của `.ts-bang-the`: `.card-body table.ts-bang-the tbody { padding-left: 0; padding-right: 0; }` để bảng thẻ nằm trong `card-body` không bị lề hai lớp.

Kiểm: mở lại năm màn đợt 3 (Hóa đơn, Điện nước, Đối chiếu, Lịch sử thanh toán, Giao dịch SePay) ở 375px: `document.documentElement.scrollWidth === 375`, thẻ vẫn có nhãn. Chạy `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests --filter "FullyQualifiedName~NhomThuTienDot3Tests"` (không cần DB) PASS.

Commit: `feat(giao-dien): bảng tĩnh tự gán nhãn thẻ, lớp cuộn ngang dùng chung`.

### V2. Đăng ký dịch vụ ra file CSS riêng

File: `Areas/QuanLyNhaTro/Views/Shared/_DangKyDichVuPartial.cshtml`, `wwwroot/css/dang-ky-dich-vu.css` (mới), và **chỉ phần khai báo CSS** của `PhongTros/QuanLyPhongTro.cshtml`, `HopDong/QuanLyHopDong.cshtml`.

1. Chuyển trọn khối `<style>` (dòng 7–231) sang `wwwroot/css/dang-ky-dich-vu.css`, đổi màu theo Q10. Partial chỉ còn chú thích đầu file và khối `<script>`.
2. `style="..."` trong chuỗi mẫu JS của partial: `font-size` cố định chuyển thành lớp trong file CSS; `style="display:none;"` và `style="${qtyDisplay}"` giữ nguyên.
3. Hai view nhúng partial: xóa `<link>` lẻ đầu file, tạo hoặc bổ sung `@section Styles` chứa CSS của màn và `dang-ky-dich-vu.css`. `QuanLyHopDong.cshtml` đã có `@section Styles` ở cuối file (flatpickr, select2, một khối `<style>`): gộp `hopdong.css` và `dang-ky-dich-vu.css` vào đó, không tạo section thứ hai.
4. Không sửa logic trong `<script>` của partial.

Kiểm: mở modal "Quản lý dịch vụ" của một phòng đang thuê và bước "Dịch vụ đăng ký" của modal tạo hợp đồng, ở sáng và tối: dịch vụ mặc định, dịch vụ tùy chọn, ô số lượng hiện đúng, lưu được.

Commit: `feat(giao-dien): đăng ký dịch vụ dùng file CSS riêng và màu nền chung`.

### V3. Phòng trọ

File: `Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml`, `Areas/QuanLyNhaTro/Views/PhongTros/SoDoPhong.cshtml`, `wwwroot/css/quanlyphongtro.css`.

Đầu trang và ô số liệu:

1. Q1 ("Phòng trọ"), Q2 ("Thêm phòng", giữ `id="btnOpenAddModal"` và điều kiện `laAdmin`). Xóa khối nút "Phát sinh 10 phòng" đang bị chú thích (dòng 22–24); phần JS `#btnPhatSinhPhong` giữ nguyên.
2. Bộ chuyển Sơ đồ / Bảng: giữ `view-toggle`, `btnGridView`, `btnTableView`; nút dùng `btn` thường, nút đang chọn có nền `var(--ts-brand-soft)`, chữ `var(--ts-brand-text)`.
3. Bốn ô thống kê thành ô số liệu nhạt bấm được, lưới `col-6 col-lg-3`:

```html
<a href="#" role="button" class="card card-sm card-link h-100 room-o-so" data-loc="trong">
    <div class="card-body">
        <div class="ts-o-so-nhan is-ok">Phòng trống</div>
        <div class="ts-o-so-gia-tri" id="statTrong">0</div>
    </div>
</a>
```

   `data-loc` và lớp nhãn: `trong` / `is-ok`; `dathue` / không lớp màu; `notien` / `is-bad`; `saphethan` / `is-wait`. Giữ bốn `id` `statTrong`, `statDaThue`, `statNoTien`, `statSapHetHan`. Liên kết không gạch chân, màu chữ theo thẻ.
4. JS thêm (không sửa hàm có sẵn): bấm `.room-o-so` thì `preventDefault()` rồi `$('.filter-btn[data-filter="' + loc + '"]').trigger('click')`. Trong handler `.filter-btn` có sẵn, thêm một dòng đánh dấu ô đang lọc: `$('.room-o-so').removeClass('is-chon').filter('[data-loc="' + currentFilter + '"]').addClass('is-chon');`. CSS `.room-o-so.is-chon` viền `var(--ts-brand)`.

Lọc:

5. Q3. "Tất cả chi nhánh" thay "-- Tất cả Chi nhánh --" (giữ `value`). Ô tìm: `type="search"`, `placeholder="Tìm phòng hoặc người thuê"`, `aria-label` cùng chữ, bỏ emoji và `style`, rộng hết hàng dưới 768px.
6. Nút lọc `.filter-btn`: khi chọn dùng một màu (nền `var(--ts-brand-soft)`, chữ `var(--ts-brand-text)`, viền trong suốt); xóa sáu quy tắc màu theo `data-filter`. Chấm trạng thái theo Q10.

Thẻ phòng (hàm `renderGrid`, chỉ sửa chuỗi HTML; giữ mọi `onclick`, `data-id`, khối `kebab-menu`):

```html
<div class="room-card" data-id="${r.phongTroId}">
    <div class="kebab-menu">...giữ nguyên...</div>
    <div class="room-body" onclick="showDetail(${r.phongTroId})">
        <div class="room-header">
            <span class="room-number">${escapeHtml(r.soPhong)}</span>
            <span class="badge ${lopTrangThai}">${tenTrangThai}</span>
        </div>
        <div class="room-price num-tabular">${formatVND(r.giaThue)}/tháng</div>
        ${tenant}
        <div class="room-meta">${r.dienTich} m²${conNgay}</div>
        ${no}
    </div>
</div>
```

7. `lopTrangThai` / `tenTrangThai`: 0 → `bg-success-lt` / "Trống"; 1 → `room-nhan-thue` / "Đã thuê"; 2 → `bg-warning-lt` / "Bảo trì". `.room-nhan-thue { color: var(--ts-brand-text); background: var(--ts-brand-soft); }`.
8. `conNgay`: khi `r.trangThai === 1 && r.soNgayConLai !== null` thêm ` · Còn ${r.soNgayConLai} ngày`, bọc `<span class="room-canh-bao">` (màu `var(--ts-wait)`) khi `r.sapHetHan`. `no`: khi `r.coNoTien` thì `<div class="room-no num-tabular">Nợ ${formatVND(r.soTienNo)}</div>` (màu `var(--ts-bad)`). Điều kiện giữ đúng như code cũ. Nhãn trạng thái không được nằm dưới nút ⋮ (chừa lề phải cho `room-header`).
9. Bỏ `room-badges`, `room-status-icon`, biến `cssClass`, `iconClass`, `iconTi` nếu không còn dùng. Icon trong menu ⋮ bỏ lớp màu (`text-warning`, `text-info`...), riêng "Xóa phòng" giữ `text-danger`.
10. `quanlyphongtro.css`: thẻ nền `var(--tblr-bg-surface)`, viền `var(--tblr-border-color)`, bo `var(--ts-radius-admin)`; xóa nền chuyển màu, `::before`, `.room-status-icon`, `.icon-*`, `.room-badge*`, `@keyframes pulse-badge`, hiệu ứng nhấc thẻ `translateY`; đổi hết màu theo Q10; xóa các quy tắc `html[data-bs-theme="dark"]`. Lưới: `repeat(auto-fill, minmax(200px, 1fr))`; dưới 576px `repeat(2, minmax(0, 1fr))` và `gap: 0.5rem`. Nút ⋮ có vùng bấm tối thiểu 36px.

Chế độ Bảng và modal:

11. `phongTroTable`: Q4, Q5, Q6. Giữ thứ tự tám cột. Nhãn "Đã thuê" trong cột trạng thái dùng `room-nhan-thue` như thẻ. Nút Sửa, Xóa thêm `aria-label`.
12. Năm modal: Q7, Q8. `#roomDetailModal`: trong `showDetail` xóa dòng `statusColors` và `.css('background', ...)`; `#detailRoomName` chỉ còn `Phòng ${r.soPhong}`, trạng thái hiện bằng nhãn (cùng lớp như thẻ) ngay sau tên, chèn bằng `.text()` hoặc phần tử DOM, không nối chuỗi chưa escape. Ngày hết hạn (dòng 914) theo Q11.
13. `#quickContractModal`, `#quickPaymentModal`: chỉ đổi lớp theo Q4, Q6, Q7, Q8, Q10 (`bg-light` → `bg-surface-secondary`; ảnh QR dùng lớp `room-qr` nền trắng khai trong `quanlyphongtro.css`). **Không sửa JS thu tiền, không đổi `id` bắt đầu bằng `qp`, `qc`, `quick`.** "TỔNG CỘNG THANH TOÁN:" → "Tổng cộng".
14. `fa-spin`, `fa-2x`, `fa-3x` dùng trên icon `ti`: giữ nguyên. Layout đang nạp Font Awesome nên các lớp này chạy; lớp `ti-spin` mà skill nhắc tới không có trong CSS của dự án (đã kiểm 13/11/2026), không dùng.

`SoDoPhong.cshtml` (tối thiểu): Q1 ("Sơ đồ phòng"), Q11 ở dòng 280, màu gõ cứng trong khối `<style>` cuối file theo Q10, `mr-3` → `me-3`. Không đổi cấu trúc, không đổi JS khác.

Kiểm: bấm từng ô số liệu và từng nút lọc, đếm thẻ khớp con số; tìm theo số phòng và tên; menu ⋮ mở đủ mục và không bị thẻ bên cạnh che; Admin thấy "Thêm phòng" và "Xóa phòng", nhân viên không thấy; mở được cả năm modal; chuyển Sơ đồ / Bảng; 375px lưới hai cột, bảng thành thẻ, không tràn ngang.

Commit: `feat(giao-dien): phòng trọ dùng thẻ phòng trắng, ô số liệu bấm để lọc, bảng thành thẻ`.

### V4. Người thuê

File: `Areas/QuanLyNhaTro/Views/NguoiThue/QuanLyNguoiThue.cshtml`, `Areas/QuanLyNhaTro/Views/NguoiThueChiTiet/ChiTiet.cshtml`.

1. Danh sách: Q1 ("Người thuê"; hiện tiêu đề ghi "Quản lý Khách Thuê" còn `Title` ghi "Người Thuê Trọ"), Q2 ("Thêm người thuê", bỏ `d-none d-sm-inline-block`), Q3, Q4, Q5, Q7, Q8, Q9. Khung trang theo `references/mau-trang.md` của skill (`container-xl mt-3` bọc `page-header d-print-none mb-3`).
2. Cột Email: "Chưa có" dùng `text-secondary`. Cột Ngày sinh (dòng 179): Q11. Ba nút thao tác thêm `aria-label`.
3. Chi tiết: khung đầu trang chung:

```html
<div class="page-header d-print-none mb-3">
    <div class="row align-items-center g-2">
        <div class="col">
            <a class="page-pretitle text-decoration-none" href="/QuanLyNhaTro/QuanLyNguoiThue">← Danh sách người thuê</a>
            <h2 class="page-title">@Model.HoVaTen</h2>
        </div>
    </div>
</div>
```

   Năm ô số liệu dùng `ts-o-so-nhan` / `ts-o-so-gia-tri` (ô "Nợ hiện tại" thêm `is-bad` khi nợ > 0, giữ điều kiện có sẵn). Bảng lịch sử thuê: thêm `ts-bang-the`, cột "Còn nợ" `text-end num-tabular`.

Kiểm: thêm, sửa, xóa một người thuê thử trên DB test; trang chi tiết ở 375px bảng thành thẻ có nhãn.

Commit: `feat(giao-dien): người thuê theo khung trang chung, bảng thành thẻ, thêm được trên điện thoại`.

### V5. Hợp đồng: danh sách và modal tạo, sửa

File: `Areas/QuanLyNhaTro/Views/HopDong/QuanLyHopDong.cshtml`, `Areas/QuanLyNhaTro/Views/Shared/_DanhSachThanhVienPartial.cshtml`, `wwwroot/css/hopdong.css`.

1. Q1 ("Hợp đồng"), Q2 ("Tạo hợp đồng"), Q3 (năm ô lọc: lưới `col-12 col-sm-6 col-lg`, lựa chọn đầu là "Tất cả", giữ `value`), Q4, Q5, Q6 (cột "Tiền thuê/tháng"), Q7, Q8, Q9, Q12.
2. Cột thao tác: nút mở menu là `btn btn-icon` với `<i class="ti ti-dots-vertical">` và `aria-label="Thao tác"` (thay `btn-light`, `ti-settings`); mục menu bỏ lớp màu chữ (`text-info`, `text-success`, `text-warning`), riêng "Xóa hợp đồng" giữ `text-danger`. Chữ: "In hợp đồng", "Tải Word", "Chỉnh sửa". Giữ nguyên các điều kiện `row.trangThaiHopDong`.
3. Ngày: dòng 589, 826, 827 theo Q11 (dòng 598 đã có `timeZone`, dùng làm căn cứ khi các trường cùng kiểu).
4. Modal `#hopDongModal`: Q7, Q8; `style="font-weight: bold; color: #206bc4;"` (dòng 136) → lớp `fw-bold text-primary`; `card-header p-2 bg-light` (dòng 241) → bỏ `bg-light`; các `style` kích thước cố định còn lại chuyển thành lớp trong `hopdong.css` (trừ `display:none`).
5. `hopdong.css`: thay `var(--tb-primary, ...)` bằng `var(--ts-brand)` (nền, viền) và `var(--ts-brand-text)` (chữ); `var(--tb-success, ...)` bằng `var(--ts-ok)`; còn lại theo Q10; xóa các khối `html[data-bs-theme="dark"] .select2-*` sau khi phần sáng đã dùng biến (kiểm ô Select2 "Người thuê" ở chế độ tối vẫn đọc được). Dưới 576px chữ tên bước nhỏ lại (`font-size: 0.75rem`) để bốn bước không tràn.
6. Xóa khối `<style>` vá `bg-light` ở cuối `QuanLyHopDong.cshtml` khi trong file không còn `bg-light`.
7. `_DanhSachThanhVienPartial.cshtml`: Q4, Q5, Q7, Q8; `style="z-index: 1060;"` giữ nguyên. `thanhVienTable` nằm trong modal: thử `ts-bang-the` ở 375px; nếu vỡ thì bỏ lớp này và bọc `ts-cuon-ngang`, ghi vào báo cáo.
8. `showToastr` (dòng 293): xóa `confirmButtonColor`.

Kiểm: lọc theo từng ô; tạo một hợp đồng thử qua đủ các bước; sửa hợp đồng (thấy bước "Thành viên ở ghép"); mở menu ⋮ của dòng đang hoạt động, đã kết thúc, đã hủy và so số mục với trước khi sửa; `ContractPagePayloadTests` và `ContractSettlementRenewalTests` PASS khi chạy test cuối đợt.

Commit: `feat(giao-dien): hợp đồng theo khung trang chung, thanh bước theo màu nền, bảng thành thẻ`.

### V6. Chi tiết hợp đồng, thanh lý, gia hạn

File: `Areas/QuanLyNhaTro/Views/HopDongChiTiet/ChiTiet.cshtml`, `ThanhLy.cshtml`, `GiaHan.cshtml`, `Loi.cshtml`.

**Chỉ sửa markup phía trên `@section Scripts`.** Khối `@section Scripts` của `ThanhLy.cshtml` và `GiaHan.cshtml` phải giống hệt mốc (lệnh in `GIONG`):

```bash
f=src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/HopDongChiTiet/ThanhLy.cshtml
diff <(git show MOC:$f | sed -n '/@section Scripts/,$p') <(sed -n '/@section Scripts/,$p' $f) && echo GIONG
```

1. Ba trang: khung đầu trang chung như mẫu ở V4 (đường dẫn quay lại là `page-pretitle`, tiêu đề `<h2 class="page-title">`); `container-xl mt-3` thay `py-4`. Trang Chi tiết: nhãn trạng thái và nhóm nút (Gia hạn, Thanh lý, In lại biên bản) nằm ở cột phải của khung đầu trang, xuống dòng dưới 768px; giữ điều kiện `CoTheGiaHan`, `CoTheThanhLy`, `CoBienBanThanhLy`.
2. Mọi `<table class="table table-vcenter">` tĩnh: thêm `ts-bang-the`. Cột tiền `text-end num-tabular`.
3. `ThanhLy.cshtml`: bốn ô tiền dùng `ts-o-so-nhan` / `ts-o-so-gia-tri`, lưới `col-6 col-lg-3`; dòng "Khách còn nợ sau cấn trừ" giữ `id="conNoSauCanTru"`. Không đổi giá trị `value`, `min` của `#ngayTraPhong` (test dò `value="2026-11-15"`).
4. `ChiTiet.cshtml`: test `ContractSettlementRenewalTests` dò chuỗi `Đã gia hạn → <mã>`; tìm nơi sinh chuỗi này trước khi sửa và giữ nguyên.
5. `Loi.cshtml`: chỉ soát lớp, không đổi nội dung.

Kiểm: mở Chi tiết, Thanh lý, Gia hạn của một hợp đồng đang hoạt động trên DB test ở 1366px và 375px, sáng và tối. **Không bấm "Xác nhận thanh lý" hay "Xác nhận gia hạn" trên hợp đồng không do chính agent tạo ở V5.** Nếu tạo được hợp đồng thử: ghi số tiền các ô của màn Thanh lý trước khi sửa (chạy web ở mốc, hoặc đọc từ DB) rồi so với sau khi sửa; chạy trọn một lượt thanh lý và một lượt gia hạn.

Commit: `feat(giao-dien): chi tiết hợp đồng, thanh lý, gia hạn theo khung trang chung, bảng thành thẻ`.

### V7. Test tĩnh

File mới: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/PhongNguoiThueHopDongDot4Tests.cs`, viết theo mẫu `NhomThuTienDot3Tests.cs` (chỉ đọc file nguồn, không cần DB, không `[Collection]`).

Phải có:

1. `quanlyphongtro.css`, `hopdong.css`, `dang-ky-dich-vu.css` không còn `#206bc4`, `#2fb344`, `#f59f00`, `#d63939`, `#0d6efd`, `--tb-primary`, `pulse-badge`.
2. `QuanLyPhongTro.cshtml`, `QuanLyNguoiThue.cshtml`, `QuanLyHopDong.cshtml`: bảng chính có `ts-bang-the`; không còn `bg-light`, `btn-success`, `table-bordered`, `thead-light`, `modal-header bg-`, `confirmButtonColor`, `text-right`, `font-weight-`.
3. `QuanLyNguoiThue.cshtml`: thẻ có `id="btnOpenAddModal"` không chứa `d-none`.
4. Theory giữ `id`: `QuanLyPhongTro` (`statTrong`, `statDaThue`, `statNoTien`, `statSapHetHan`, `btnGridView`, `btnTableView`, `roomGrid`, `phongTroTable`, `qpInvoiceId`, `btnConfirmQuickPayment`), `QuanLyHopDong` (`hopDongTable`, `hopDongStepper`, `btnSaveData`, năm `filter*`), `ThanhLy` (`ngayTraPhong`, `btnLuuNgayTraPhong`, `chkXacNhanConNo`, `btnXacNhanThanhLy`, `khuXacNhan`, `khuKetQua`, `conNoSauCanTru`, `formBienBan`), `GiaHan` (`btnXacNhanGiaHan`, `ngayKetThucMoi`, `tienThue`, `tienCoc`, `nhacChenhLechCoc`, `khuDieuKhoanMau`).
5. Đếm `display:\s*none` trong `ThanhLy.cshtml`, `GiaHan.cshtml`, `QuanLyHopDong.cshtml`, `QuanLyPhongTro.cshtml` bằng đúng số ở mốc (đếm trước khi sửa, ghi số vào `InlineData`).
6. `site.js` có `ganTatCa` và `DOMContentLoaded`; `trosync-nen.css` có `ts-cuon-ngang`.
7. `_DangKyDichVuPartial.cshtml` không còn `<style`; `QuanLyPhongTro.cshtml` và `QuanLyHopDong.cshtml` có `dang-ky-dich-vu.css` nằm trong `@section Styles`.
8. Các view của đợt không còn `toLocaleDateString('vi-VN')` dạng không có tham số thứ hai.

Commit: `test(giao-dien): chốt đợt 4 phòng, người thuê, hợp đồng`.

### V8. Kiểm cuối đợt và báo cáo

1. `git diff --check MOC` và `git diff --stat MOC`: chỉ có file thuộc V1–V7 và tài liệu.
2. Script soát trên mọi file đã sửa: 0 LỖI. Ghi số XEM LẠI và nói rõ mục nào có từ trước.
3. `dotnet build QuanLyChoThuePhongTroWeb.sln`; `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests`; `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests`.
4. `Web.IntegrationTests` toàn bộ theo skill `chay-test-tich-hop` (DB `*_test`; không chạy song song với lượt test khác). Bình thường khoảng 910 test trong 4 phút sau lần dọn DB ngày 12/11; số test gần gấp đôi kèm hàng trăm FAIL là dấu hiệu chạy chồng hoặc DB test quá lớn, khi đó dừng và ghi vào báo cáo, không tự xóa DB. Thiếu biến DB test thì ghi **CHƯA CHẠY**, không ghi PASS.
5. Trình duyệt: chạy cấu hình `web-test-db` trong `.claude/launch.json` (không dùng `web`), đăng nhập tài khoản test theo ghi nhớ `kiem-trinh-duyet-db-test` (tài khoản do `BranchScopeKit.SeedAsync` tạo; không viết mật khẩu ra báo cáo hay chat; file test tạm thì xóa, không commit). View đổi phải khởi động lại web. Mỗi màn kiểm ở 1366px sáng, 1366px tối, 375px sáng, 375px tối: `scrollWidth === clientWidth`, console không lỗi, chữ đọc được trên nền. Kiểm thêm vai trò nhân viên ở màn Phòng trọ.
6. Ảnh lưu `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo\giao-dien-4-NN-mo-ta.png` (công cụ chỉ xuất `.jpg` thì dùng `.jpg` và ghi rõ). Tối thiểu: Phòng trọ sơ đồ (1366 sáng, 375 sáng, 375 tối), Phòng trọ bảng 375, modal chi tiết phòng, Người thuê (1366, 375), Hợp đồng (1366, 375 tối), modal tạo hợp đồng bước 1 và bước 2, Chi tiết hợp đồng 375, Thanh lý (1366, 375), Gia hạn 375.
7. Điền mục 4 và 5 của spec đợt 4 (bảng việc, commit, lệch so với thiết kế, còn để ý), ghi trạng thái "đã code, chờ nghiệm thu". Commit: `docs(giao-dien): kết quả đợt 4`.
8. Viết báo cáo `.bangiao/giao-dien/bao-cao-dot-4.md` theo mẫu ở [giao-viec-dot-4-5.md](giao-viec-dot-4-5.md) rồi **dừng**. Không bắt đầu đợt 5.

## 4. Tiêu chí nghiệm thu

Người nghiệm thu tự chạy lại, không chỉ đọc báo cáo:

1. Diff từ mốc không có file ngoài phạm vi; không có `.cs` nào trong `src/` bị đổi.
2. Lệnh kiểm `id` (mục 1) không in ra gì cho mọi view; lệnh `diff` khối script của Thanh lý và Gia hạn in `GIONG`.
3. Script soát 0 LỖI trên các file của đợt; build và các bộ test PASS.
4. Bảng Q11 trong báo cáo có đủ các chỗ đã sửa, mỗi chỗ có mẫu giá trị thật.
5. Trình duyệt: mở lại ngẫu nhiên các màn ở 375px và chế độ tối; thử lọc, menu ⋮, modal; so số tiền Thanh lý với trước khi sửa.
6. Thiết kế khớp mục 2 của spec; mọi chỗ lệch có trong báo cáo kèm lý do.
