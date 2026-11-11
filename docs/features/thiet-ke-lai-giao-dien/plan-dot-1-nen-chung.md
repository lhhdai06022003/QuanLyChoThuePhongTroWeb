# Kế hoạch đợt 1: nền chung của giao diện mới

> **Cho agent thực thi:** làm lần lượt từng task, tick `- [ ]` khi xong. Đọc `AGENTS.md`, spec và skill `giao-dien-quan-ly` (`.agents/skills/giao-dien-quan-ly/SKILL.md`) trước khi sửa file đầu tiên. Bạn chỉ thấy tài liệu này và repo; mọi quyết định thiết kế đã nằm ở đây, chỗ nào kế hoạch không nói thì giữ nguyên hiện trạng.

**Mục tiêu:** một bộ màu, một font, menu nhóm theo việc, thanh trên dùng được trên điện thoại, vỏ cổng khách thuê mang tên TroSync, nút trợ lý AI không che nội dung.

**Cách làm:** thêm một file CSS nền đặt giá trị cho biến của Tabler 1.4 (Tabler vẽ nút, liên kết, badge qua biến nên không ghi đè từng lớp), sửa các partial vỏ và `MenuService`. Không sửa thân của bất kỳ màn nào.

**Công nghệ:** ASP.NET Core MVC .NET 8, Razor, Tabler 1.4 (Bootstrap 5), jQuery, xUnit.

**Spec:** [spec-dot-1-nen-chung.md](spec-dot-1-nen-chung.md). Kế hoạch này chốt các điểm mở ở mục 11 của spec theo đề xuất (màu `#1d5fd0`; Giao dịch SePay trong "Thu tiền"; nút AI giữ dạng nổi).

## Ràng buộc chung

- Nhánh `feature/thiet-ke-lai-giao-dien`. Trước khi sửa: `git status --short` phải sạch (trừ file của chính bạn).
- **Không sửa:** `Program.cs`, `.csproj`, `appsettings*`, `.gitignore`, fixture test, test kiến trúc, migration, mọi thứ trong `src/*.Application`, `src/*.Domain`, `src/*.Infrastructure`, `wwwroot/css/trosync-tokens.css`, `Views/Shared/_PublicRoomLayout.cshtml`, `guest-*.css`, `khach-vang-lai.css`, `staff-guest-management.css`, và mọi view trong `Areas/*/Views/<màn>/` trừ hai dòng `<link>` của `Areas/QuanLyNhaTro/Views/NguoiDung/DangNhap.cshtml` (Task 1).
- **Không đổi quyền:** thuộc tính `Roles` của từng mục menu giữ đúng như hiện tại.
- **Giữ nguyên các `id` và lớp JS/test đang dùng:** `sidebar-menu`, `badge-viec-can-lam`, `badge-thong-bao`, `badge-thong-bao-header`, `list-thong-bao`, `btn-danh-dau-tat-ca`, `btnToggleDarkTheme`, `btnToggleLightTheme`, `logoutForm`, `mobileLogoutForm`, `navbar-menu`, các lớp `badge-notification`, `status-dot`, `tb-notif-menu`, `tb-notif-list`, `tenant-bottom-nav`, `tenant-bottom-nav-item`, `btn-ai-fab`, `ai-chat-box`. Trong `_Header.cshtml` giữ nguyên khối nhãn vai trò, kể cả `style="font-size: 0.72rem;"` (test `Header_GhiDungVaiTro_AdminVaNhanVien` tìm bằng chuỗi này và lấy kết quả **đầu tiên** trong HTML, nên không dùng lại chuỗi `font-size: 0.72rem;` ở bất kỳ chỗ nào khác của vỏ).
- Chữ giao diện: tiếng Việt, viết hoa chữ đầu câu, không dùng dấu `&`.
- Icon chỉ `ti ti-*`. Chữ đậm dùng `fw-medium`/`fw-semibold`/`fw-bold`. Không `bg-light`, `bg-white`, `text-dark`, không gõ mã màu trong `.cshtml`/`.js`.
- Không tạo file `.ps1`. Không đọc/sửa `appsettings.json`. Không chạy web, không chạy `Web.IntegrationTests` hay `Infrastructure.IntegrationTests` (cần DB test, phiên chính sẽ chạy). Chỉ chạy `dotnet build`.
- Commit cục bộ sau mỗi task, **không push**, message tiếng Việt dạng `feat(giao-dien): …`, **không** có dòng `Co-Authored-By` hay ghi công AI. Chỉ `git add` đúng các file của task (không `git add -A`).
- Sau mỗi task chạy `node .agents/skills/giao-dien-quan-ly/scripts/soat-giao-dien.js <các file .cshtml/.js đã sửa>`: 0 LỖI **trên dòng bạn thêm hoặc sửa**. LỖI có sẵn ở dòng bạn không đụng thì ghi lại trong báo cáo, không sửa lan.

## Điểm phiên chính sẽ soi khi review

1. Nhân viên thấy đúng 16 đường dẫn trong menu, Admin 20; không thừa, không thiếu.
2. Cổng khách vãng lai không đổi một điểm ảnh: `git diff` không chứa file nào của cổng đó.
3. Chế độ tối: chữ trên nền thẻ, mục menu đang chọn, badge trạng thái đều đọc được.
4. 375px: thanh trên không tràn ngang; nút AI không đè thanh tab của khách thuê.
5. Không có `id` trùng trong một trang (đặc biệt chuông thông báo và form đăng xuất).

---

### Task 1: File nền màu, font và nạp vào ba nơi

**File:**
- Tạo: `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/css/trosync-nen.css`
- Sửa: `Views/Shared/_Layout.cshtml`, `Areas/KhachThue/Views/Shared/_LayoutKhachThue.cshtml`, `Areas/QuanLyNhaTro/Views/NguoiDung/DangNhap.cshtml`, `wwwroot/css/site.css`
- Test: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/GiaoDienNenStaticGuardTests.cs` (mới)

**Sinh ra cho task sau:** các biến `--ts-bg`, `--ts-surface`, `--ts-ink`, `--ts-ink-muted`, `--ts-line`, `--ts-brand`, `--ts-brand-rgb`, `--ts-brand-strong`, `--ts-brand-text`, `--ts-brand-soft`, `--ts-ok`, `--ts-ok-soft`, `--ts-wait`, `--ts-wait-soft`, `--ts-bad`, `--ts-bad-soft`, `--ts-font`.

- [x] **Bước 1: viết test tĩnh** (đọc file như `VietnameseUiStaticGuardTests`, cùng cách tìm thư mục Web; lớp không cần `CustomWebApplicationFactory`):

```csharp
[Theory]
[InlineData("Views", "Shared", "_Layout.cshtml")]
[InlineData("Areas", "KhachThue", "Views", "Shared", "_LayoutKhachThue.cshtml")]
[InlineData("Areas", "QuanLyNhaTro", "Views", "NguoiDung", "DangNhap.cshtml")]
public void HaiCongVaManDangNhap_NapNenVaFontMoi(params string[] path)
{
    var html = Read(path);
    Assert.Contains("~/css/trosync-nen.css", html);
    Assert.Contains("family=Be+Vietnam+Pro", html);
    Assert.DoesNotContain("family=Inter", html);
}

[Fact]
public void CongKhachVangLai_KhongNapNenMoi()
{
    var html = Read("Views", "Shared", "_PublicRoomLayout.cshtml");
    Assert.DoesNotContain("trosync-nen.css", html);
    Assert.Contains("family=Inter", html);
}

[Fact]
public void FileNen_KhaiBaoDuBienChoSangVaToi()
{
    var css = Read("wwwroot", "css", "trosync-nen.css");
    Assert.Contains("[data-bs-theme=\"dark\"]", css);
    Assert.Contains("'Be Vietnam Pro'", css);
    foreach (var bien in new[] { "--ts-brand:", "--ts-brand-text:", "--ts-brand-soft:", "--ts-ok:", "--ts-wait:", "--ts-bad:", "--tblr-primary:", "--tblr-primary-rgb:", "--tblr-body-bg:" })
        Assert.True(css.Split(bien).Length - 1 >= 2, $"{bien} phải có ở cả khối sáng và khối tối");
}
```

- [x] **Bước 2: tạo `trosync-nen.css`** đúng các giá trị sau (đây là quyết định thiết kế, không tự đổi):

```css
/* Nền giao diện TroSync: cổng quản lý, cổng khách thuê, màn đăng nhập. Không nạp ở _PublicRoomLayout. */
:root,
[data-bs-theme="light"] {
    --ts-bg: #f3f5f8;
    --ts-surface: #ffffff;
    --ts-ink: #16202e;
    --ts-ink-muted: #5b6676;
    --ts-line: #dde3ea;
    --ts-brand: #1d5fd0;
    --ts-brand-rgb: 29, 95, 208;
    --ts-brand-strong: #184fb0;
    --ts-brand-text: #1d5fd0;
    --ts-brand-soft: #e8f0fd;
    --ts-ok: #176f43;
    --ts-ok-soft: #e3f4ea;
    --ts-wait: #8a570c;
    --ts-wait-soft: #fbefd6;
    --ts-bad: #b02e24;
    --ts-bad-soft: #fbe6e3;
    --ts-font: 'Be Vietnam Pro', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif;

    --ts-primary: var(--ts-brand);
    --ts-primary-hover: var(--ts-brand-strong);
    --ts-primary-light: var(--ts-brand-soft);

    --tblr-primary: #1d5fd0;
    --tblr-primary-rgb: 29, 95, 208;
    --tblr-primary-darken: #184fb0;
    --tblr-primary-lt: #e8f0fd;
    --tblr-primary-lt-rgb: 232, 240, 253;
    --tblr-link-color: #1d5fd0;
    --tblr-link-color-rgb: 29, 95, 208;
    --tblr-link-hover-color: #184fb0;
    --tblr-body-bg: #f3f5f8;
    --tblr-body-bg-rgb: 243, 245, 248;
    --tblr-body-color: #16202e;
    --tblr-body-color-rgb: 22, 32, 46;
    --tblr-secondary: #5b6676;
    --tblr-secondary-rgb: 91, 102, 118;
    --tblr-muted: #5b6676;
    --tblr-border-color: #dde3ea;
    --tblr-bg-surface: #ffffff;
    --tblr-bg-surface-secondary: #f3f5f8;
    --tblr-bg-surface-tertiary: #f3f5f8;
    --tblr-font-sans-serif: var(--ts-font);
}

[data-bs-theme="dark"] {
    --ts-bg: #10161f;
    --ts-surface: #18212d;
    --ts-ink: #e8edf3;
    --ts-ink-muted: #9aa7b7;
    --ts-line: #2a3545;
    --ts-brand: #2a66d4;
    --ts-brand-rgb: 42, 102, 212;
    --ts-brand-strong: #2459bb;
    --ts-brand-text: #7fb0f7;
    --ts-brand-soft: #1c2f4d;
    --ts-ok: #5fcf96;
    --ts-ok-soft: #173426;
    --ts-wait: #f0bd62;
    --ts-wait-soft: #3a2c12;
    --ts-bad: #f08a80;
    --ts-bad-soft: #40201d;

    --tblr-primary: #2a66d4;
    --tblr-primary-rgb: 42, 102, 212;
    --tblr-primary-darken: #2459bb;
    --tblr-primary-lt: #1c2f4d;
    --tblr-primary-lt-rgb: 28, 47, 77;
    --tblr-link-color: #7fb0f7;
    --tblr-link-color-rgb: 127, 176, 247;
    --tblr-link-hover-color: #a5c8fa;
    --tblr-body-bg: #10161f;
    --tblr-body-bg-rgb: 16, 22, 31;
    --tblr-body-color: #e8edf3;
    --tblr-body-color-rgb: 232, 237, 243;
    --tblr-secondary: #9aa7b7;
    --tblr-secondary-rgb: 154, 167, 183;
    --tblr-muted: #9aa7b7;
    --tblr-border-color: #2a3545;
    --tblr-bg-surface: #18212d;
    --tblr-bg-surface-secondary: #10161f;
    --tblr-bg-surface-tertiary: #18212d;
    --tblr-bg-forms: #10161f;
}

/* Chữ thương hiệu trên nền tối phải sáng hơn màu tô nút. */
[data-bs-theme="dark"] .text-primary { color: var(--ts-brand-text) !important; }

/* Badge trạng thái nhạt: chữ đủ tương phản ở cả hai chế độ (Tabler mặc định cam trên nền kem chỉ đạt 2:1). */
.bg-success-lt, .bg-green-lt { color: var(--ts-ok) !important; background-color: var(--ts-ok-soft) !important; }
.bg-warning-lt, .bg-yellow-lt { color: var(--ts-wait) !important; background-color: var(--ts-wait-soft) !important; }
.bg-danger-lt, .bg-red-lt { color: var(--ts-bad) !important; background-color: var(--ts-bad-soft) !important; }
```

Tương phản đã tính (WCAG): chữ phụ trên thẻ 5,8 (sáng) và 6,6 (tối); trắng trên nút thương hiệu 5,8 và 5,3; chữ trạng thái trên nền nhạt từ 5,3 trở lên.

- [x] **Bước 3: nạp file và đổi font**
  - `_Layout.cshtml`, `_LayoutKhachThue.cshtml`: thay dòng Google Font Inter bằng `https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700&display=swap`; thêm `<link rel="stylesheet" href="~/css/trosync-nen.css" asp-append-version="true" />` ngay sau dòng `trosync-tokens.css`.
  - `DangNhap.cshtml`: trong URL Google Fonts thay `family=Inter:wght@400;500;600` bằng `family=Be+Vietnam+Pro:wght@400;500;600` (giữ `Plus Jakarta Sans`); thêm dòng nạp `trosync-nen.css` sau dòng nạp CSS cuối cùng của Tabler/`site.css` trong `<head>` và trước khối `<style>` của màn. Không sửa gì khác trong file này.
  - `site.css` dòng 1 đến 3: bỏ khối `:root { --tblr-font-sans-serif: 'Inter' … !important; }` (file nền đã đặt biến; `body { font-family: var(--tblr-font-sans-serif); }` giữ).
- [x] **Bước 4: dọn màu gõ cứng trong `site.css`** (chỉ file này): mọi `#206bc4` thành `var(--ts-brand-text)` khi là màu chữ, `var(--tblr-primary)` khi là nền; mọi `rgba(32, 107, 196, X)` thành `rgba(var(--tblr-primary-rgb), X)`; các khối `[data-bs-theme="dark"] .brand-title…` dùng mã màu thì đổi sang `var(--tblr-body-color)`, `var(--ts-brand-text)`, `var(--tblr-secondary)`. `.card`: bỏ `box-shadow`, viền `1px solid var(--tblr-border-color) !important`, giữ bo 8px. `.table thead th`: bỏ `text-transform` và `letter-spacing`, `font-weight: 500 !important`, `font-size: 0.8125rem`, giữ nền và màu theo biến.
- [x] **Bước 5: màu nút SweetAlert** trong hai layout: thêm một hàm đọc biến ở đầu khối script SweetAlert và dùng thay ba mã `#3085d6`, `#d33`, `#6c757d`:

```js
const mauBien = (ten) => getComputedStyle(document.documentElement).getPropertyValue(ten).trim() || undefined;
// xác nhận thường: mauBien('--tblr-primary'); icon error/warning: mauBien('--tblr-danger'); hủy: mauBien('--tblr-secondary')
```

Đọc lúc gọi `Swal.fire` (không đọc một lần lúc tải trang, vì người dùng đổi sáng/tối không tải lại). Giữ nguyên mọi chuỗi mà `VietnameseUiStaticGuardTests` kiểm (`confirmButtonText: 'Đóng'`, `cancelButtonText: 'Hủy bỏ'`, cấu hình Select2 tiếng Việt, thông báo lỗi DataTables).
- [x] **Bước 6:** `dotnet build QuanLyChoThuePhongTroWeb.sln` → 0 lỗi. Chạy script soát trên các file đã sửa.
- [x] **Bước 7: commit** `feat(giao-dien): nền màu và font Be Vietnam Pro cho hai cổng`.

### Task 2: Menu nhóm theo việc

**File:**
- Sửa: `Services/MenuService.cs`, `Views/Shared/_MenuItem.cshtml`, `wwwroot/css/site.css`, `wwwroot/js/site.js`
- Test: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/MenuNhomTheoViecTests.cs` (mới, `[Collection(WebTestCollection.Name)]`, dùng `BranchScopeKit.SeedAsync`/`LoginAsync` như `BangDieuKhienTheoVaiTroTests`)

- [x] **Bước 1: viết test.** Lấy HTML `/QuanLyNhaTro/Dashboard`, cắt đoạn từ `id="sidebar-menu"` tới `</aside>`, lấy mọi `href="…"` trong đoạn đó, so **tập hợp** (không trùng lặp) với:

```csharp
private static readonly string[] NhanVien =
{
    "/Home/Index", "/QuanLyNhaTro/ViecCanLam",
    "/PhongTros/QuanLyPhongTro", "/QuanLyNhaTro/QuanLyNguoiThue", "/QuanLyNhaTro/QuanLyHopDong",
    "/QuanLyNhaTro/ChotDienNuoc", "/QuanLyNhaTro/QuanLyHoaDon", "/QuanLyNhaTro/DoiChieuThanhToan", "/QuanLyNhaTro/LichSuThanhToan",
    "/QuanLyNhaTro/TinPhong", "/QuanLyNhaTro/LichXemPhong", "/QuanLyNhaTro/GiuCho",
    "/QuanLyNhaTro/YeuCauSuCo", "/QuanLyNhaTro/BaoCao",
    "/QuanLyNhaTro/QuanLyDichVu", "/QuanLyNhaTro/DieuKhoanMau"
};
private static readonly string[] ChiAdmin =
{
    "/QuanLyNhaTro/GiaoDichSePay", "/ChiNhanhs/QuanLyChiNhanh",
    "/QuanLyNhaTro/QuanLyTaiKhoanDangNhap", "/QuanLyNhaTro/PhanCongChiNhanh"
};
```

  - `Menu_NhanVien_ThayDung16Muc_KhongCoMucChiAdmin`: tập hợp bằng đúng `NhanVien`.
  - `Menu_Admin_ThayDu20Muc`: tập hợp bằng `NhanVien` + `ChiAdmin`.
  - `Menu_HienNhanNhom_VaKhongConNhomXoXuong`: HTML Admin chứa năm nhãn `Phòng và khách`, `Thu tiền`, `Khách mới`, `Vận hành`, `Thiết lập`; đoạn menu không chứa `dropdown-toggle`, `Quản lý nhà trọ`, `Quản lý người dùng`; vẫn chứa `id="badge-viec-can-lam"`.
  - `Menu_MucDangMo_CoLopActive`: mở `/QuanLyNhaTro/QuanLyHoaDon` bằng Admin, thẻ `<a` có `href="/QuanLyNhaTro/QuanLyHoaDon"` trong menu mang lớp `active`; đúng một mục `nav-link active` trong đoạn menu.

- [x] **Bước 2: `MenuService.GetMenu()`** trả đúng cấu trúc này (tên, icon, `Url`, `Roles`, `ActivePrefixes`, `BadgeId` của từng mục lá chép từ bản hiện tại, chỉ đổi tên ở hai mục ghi chú):

| Thứ tự | Mục cấp 1 | Con (theo thứ tự) |
| --- | --- | --- |
| 1 | Bảng điều khiển (`/Home/Index`) | |
| 2 | Việc cần làm (`BadgeId = "badge-viec-can-lam"`) | |
| 3 | Nhóm "Phòng và khách" | Phòng trọ; Người thuê; Hợp đồng |
| 4 | Nhóm "Thu tiền" | Điện nước; Hóa đơn; Đối chiếu thanh toán; Lịch sử thanh toán; Giao dịch SePay (`Roles` Admin) |
| 5 | Nhóm "Khách mới" | Tin phòng công khai; Lịch xem phòng; Đặt cọc phòng |
| 6 | Nhóm "Vận hành" | Sự cố và sửa chữa (tên cũ "Sự cố & Sửa chữa"); Báo cáo |
| 7 | Nhóm "Thiết lập" | Chi nhánh (`Roles` Admin); Dịch vụ; Điều khoản hợp đồng; Tài khoản (tên cũ "Quản lý tài khoản", thêm `Roles` Admin vì nhóm cha cũ chỉ Admin thấy); Phân công chi nhánh (`Roles` Admin) |

Mục nhóm không có `Url`, `Roles`; `Icon` để chuỗi rỗng. Giữ dòng `Name = "Bảng điều khiển"` (test có sẵn tìm đúng chuỗi này). Bỏ dòng "Sơ đồ phòng" đang chú thích.

- [x] **Bước 3: `_MenuItem.cshtml`.** Giữ nguyên toàn bộ khối `@{ … }` (`IsActiveUrl`, `IsActive`, `isVisible`). Nhánh `hasChild` thay bằng: một `<li class="ts-nav-group" aria-hidden="true"><span class="ts-nav-group-label">@Model.Name</span></li>` rồi `@await Html.PartialAsync("_MenuItem", child)` cho từng mục con được thấy. Nhánh mục lá giữ nguyên markup hiện tại (đã có huy hiệu theo `BadgeId`), thêm `aria-current="page"` khi đang mở. Không còn `dropdown`, `dropdown-toggle`, `dropdown-menu`, `data-bs-toggle`.
- [x] **Bước 4: `site.css`.** Xóa các khối đánh số 2 đến 7 của phần "Tabler Vertical Navbar" (toàn bộ quy tắc `.dropdown…` trong `.navbar-vertical`), giữ khối 0 (thương hiệu) và 1 (thanh cuộn), thay bằng:

```css
/* Menu nhóm theo việc */
.navbar-vertical .nav-item:after,
.navbar-vertical .nav-item.active:after { display: none !important; }

.navbar-vertical .ts-nav-group-label {
  display: block;
  padding: 0.9rem 1.1rem 0.25rem;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--tblr-secondary);
}

.navbar-vertical .navbar-nav .nav-link {
  margin: 1px 0.5rem;
  padding: 0.4rem 0.6rem;
  border-radius: 6px;
  font-size: 0.875rem;
  color: var(--tblr-body-color);
  transition: background-color 0.15s ease, color 0.15s ease;
}

.navbar-vertical .navbar-nav .nav-link .nav-link-icon { color: var(--tblr-secondary); }

.navbar-vertical .navbar-nav .nav-link:hover {
  background-color: rgba(var(--tblr-primary-rgb), 0.06);
  color: var(--ts-brand-text);
}

.navbar-vertical .navbar-nav .nav-item.active > .nav-link {
  background-color: var(--ts-brand-soft);
  color: var(--ts-brand-text);
  font-weight: 500;
}

.navbar-vertical .navbar-nav .nav-item.active > .nav-link .nav-link-icon { color: var(--ts-brand-text); }

/* Huy hiệu việc cần làm nằm trong dòng, không bị cắt ở mép menu */
.navbar-vertical .navbar-nav .nav-link .badge {
  position: static;
  transform: none;
  margin-left: auto;
}
```

Bỏ cả khối "7. Đồng bộ Dark Mode" của menu (biến đã lo). Giữ các khối `bg-light`, `text-dark`, `border-dashed` ở chế độ tối và mọi thứ từ "Popup thông báo" trở xuống.
- [x] **Bước 5: `site.js`.** Thêm (trong cùng IIFE, sau `window.TroFocus`) đoạn chạy lúc `DOMContentLoaded`: tìm `#sidebar-menu .nav-link.active`; nếu có và phần tử cha `.navbar-vertical` có thanh cuộn dọc, gọi `scrollIntoView({ block: 'nearest' })`. Không dùng jQuery animation.
- [x] **Bước 6:** `dotnet build` → 0 lỗi; script soát.
- [x] **Bước 7: commit** `feat(giao-dien): menu quản lý nhóm theo việc, luôn mở`.

### Task 3: Thanh trên của điện thoại và nút đổi sáng/tối

**File:**
- Sửa: `Views/Shared/_Sidebar.cshtml`, `Views/Shared/_Header.cshtml`, `wwwroot/js/site.js`, `wwwroot/js/thongbao.js`
- Test: thêm vào `GiaoDienNenStaticGuardTests.cs`

- [x] **Bước 1: test tĩnh** `ThanhTrenDienThoai_CoChuongVaDoiGiaoDien_KhongTrungId`: `_Sidebar.cshtml` chứa `js-badge-thong-bao`, `js-doi-giao-dien`, `/QuanLyNhaTro/DoiMatKhau`, `/QuanLyNhaTro/ThongBao`; không chứa `id="badge-thong-bao"`, `id="list-thong-bao"`, `id="logoutForm"`. `thongbao.js` chứa `.js-badge-thong-bao`. `_Header.cshtml` vẫn chứa `badge-notification` và `id="btnToggleDarkTheme"`.
- [x] **Bước 2: `_Sidebar.cshtml`**, khối `navbar-nav flex-row d-lg-none` (chỉ hiện dưới 992px), khi đã đăng nhập, theo thứ tự:
  1. Liên kết chuông: `<a href="/QuanLyNhaTro/ThongBao" class="nav-link px-2 position-relative" aria-label="Thông báo"><i class="ti ti-bell"></i><span class="badge bg-red badge-notification js-badge-thong-bao d-none"></span></a>`. Kích thước icon dùng lớp `fs-2`, không dùng `style`.
  2. Menu người dùng hiện có, thêm trước "Đăng xuất": mục `<a href="#" class="dropdown-item py-2 js-doi-giao-dien"><i class="ti ti-moon me-2"></i><span>Chế độ tối</span></a>`, mục "Đổi mật khẩu" tới `/QuanLyNhaTro/DoiMatKhau`, rồi `dropdown-divider`. Sửa `fa-lg` trong avatar thành `fs-2`.
- [x] **Bước 3: gom logic sáng/tối về `site.js`.** Xóa khối `<script>` cuối `_Header.cshtml`; trong `site.js` thêm `window.TroTheme` với `apDung()` và xử lý bấm cho `#btnToggleDarkTheme`, `#btnToggleLightTheme`, `.js-doi-giao-dien`. Hành vi giữ như cũ: đặt `data-bs-theme` trên `<html>`, lưu `localStorage['tablerTheme']`, ẩn/hiện hai nút header bằng `style.display`. Với `.js-doi-giao-dien`: đang sáng thì icon `ti-moon` + chữ "Chế độ tối", đang tối thì `ti-sun` + "Chế độ sáng". Bọc `localStorage` trong `try/catch`. Bỏ `shadow-sm` khỏi thẻ `<header>`; ngoài hai thay đổi đó không đụng `_Header.cshtml`.
- [x] **Bước 4: `thongbao.js`**, hàm `updateBadge`: cập nhật thêm `$('.js-badge-thong-bao')` giống `#badge-thong-bao` (đặt chữ, bật/tắt `d-none`).
- [x] **Bước 5:** `dotnet build`; script soát; `node --check` trên hai file JS.
- [x] **Bước 6: commit** `feat(giao-dien): thanh trên điện thoại có thông báo, đổi sáng tối, đổi mật khẩu`.

### Task 4: Vỏ cổng khách thuê

**File:**
- Sửa: `Areas/KhachThue/Views/Shared/_SidebarKhachThue.cshtml`, `Areas/KhachThue/Views/Shared/_LayoutKhachThue.cshtml`, `wwwroot/css/tenant-bento.css`
- Test: thêm vào `GiaoDienNenStaticGuardTests.cs`

- [x] **Bước 1: test tĩnh** `VoKhachThue_DungTenTroSync_VaIconTabler`: `_SidebarKhachThue.cshtml` chứa `TroSync`, `Cổng khách thuê`, `js-badge-thong-bao`, `/KhachThue/ThongBao`; không chứa `fa-solid`, `font-weight-bold`, `style=`, `& Thanh toán`, `Báo cáo Sự cố`. `_LayoutKhachThue.cshtml` chứa `| TroSync` và `ts-cong-khach-thue`, không chứa `Khách Thuê - Quản Lý Trọ`.
- [x] **Bước 2: `_SidebarKhachThue.cshtml`**
  - Thương hiệu: chép khối logo SVG + chữ từ `_Sidebar.cshtml` (liên kết về `/KhachThue/Dashboard`; đổi `id` gradient thành `troSyncBgKhach`), dòng phụ `brand-subtitle` ghi "Cổng khách thuê".
  - Thanh trên điện thoại (`d-lg-none`): như Task 3, chuông tới `/KhachThue/ThongBao`; menu người dùng có "Chế độ tối/sáng" (`js-doi-giao-dien`) và "Đăng xuất" (form POST `/QuanLyNhaTro/DangXuat` có `@Html.AntiForgeryToken()`, `id="mobileLogoutForm"`). Không có "Đổi mật khẩu".
  - Mục menu (đường dẫn, thứ tự giữ nguyên), tên và icon:

| Tên | Icon |
| --- | --- |
| Tổng quan | `ti ti-home` |
| Lịch xem và đặt phòng | `ti ti-calendar-event` |
| Hồ sơ của tôi | `ti ti-user` |
| Hợp đồng thuê phòng | `ti ti-file-text` |
| Hóa đơn và thanh toán | `ti ti-file-invoice` |
| Chỉ số điện nước | `ti ti-bolt` |
| Lịch sử thanh toán | `ti ti-history` |
| Báo sự cố | `ti ti-tools` |

  - Mục đang mở: thêm lớp `active` cho `li.nav-item` và `a.nav-link` khi `Context.Request.Path` bắt đầu bằng đường dẫn của mục (so không phân biệt hoa thường); kiểu dáng dùng chung CSS menu của Task 2.
- [x] **Bước 3: `_LayoutKhachThue.cshtml`**: `<title>@(string.IsNullOrEmpty(ViewBag.Title?.ToString()) ? "TroSync - Cổng khách thuê" : ViewBag.Title + " | TroSync")</title>`; `<body class="layout-fluid ts-cong-khach-thue">`.
- [x] **Bước 4: `tenant-bento.css`**, chỉ khối thanh tab: bỏ `box-shadow`; mọi mã màu dự phòng trong `var(--tblr-…, #xxxxxx)` của khối `.tenant-bottom-nav*` bỏ phần dự phòng; mục đang chọn dùng `color: var(--ts-brand-text)`, bỏ `transform: scale`. Không đụng phần `.bento-*`.
- [x] **Bước 5:** `dotnet build`; script soát.
- [x] **Bước 6: commit** `feat(giao-dien): vỏ cổng khách thuê mang tên TroSync, icon Tabler`.

### Task 5: Nút trợ lý AI không che nội dung

**File:** `wwwroot/css/ai-chat-widget.css`, `wwwroot/css/trosync-nen.css`

- [x] **Bước 1: `ai-chat-widget.css`**: `.btn-ai-fab` còn `width: 48px; height: 48px; bottom: 16px; right: 16px; font-size: 20px`; `.ai-chat-box` `bottom: 76px; right: 16px; max-width: calc(100vw - 32px); max-height: calc(100vh - 96px)`. Thêm cuối file:

```css
/* Cổng khách thuê trên điện thoại: nằm trên thanh tab dưới (cao 64px). */
@media (max-width: 767.98px) {
    .ts-cong-khach-thue .btn-ai-fab { bottom: 76px; }
    .ts-cong-khach-thue .ai-chat-box { bottom: 136px; max-height: calc(100vh - 156px); }
}
```

Không đổi tên lớp, `id`, không sửa `ai-chat-widget.js` và `_AiChatWidget.cshtml`.
- [x] **Bước 2: `trosync-nen.css`**, thêm cuối file:

```css
/* Chừa chỗ để dòng cuối và nút phân trang cuộn lên khỏi nút trợ lý AI. */
.page-wrapper { padding-bottom: 4.5rem; }
@media (max-width: 767.98px) {
    .ts-cong-khach-thue .page-wrapper { padding-bottom: 9rem !important; }
}
```

(`tenant-bento.css` đang đặt `74px !important` cho thanh tab; quy tắc trên có bộ chọn cụ thể hơn nên thắng.) Nếu footer có nền riêng bị hở mảng trống xấu thì ghi lại trong báo cáo, không tự đổi cấu trúc layout.
- [x] **Bước 3: commit** `feat(giao-dien): nút trợ lý AI thu nhỏ, không che cuối bảng và thanh tab`.

### Task 6: Cập nhật skill và script soát (ngoài Git của dự án)

**File:** `.agents/skills/giao-dien-quan-ly/SKILL.md`, `.agents/skills/giao-dien-quan-ly/references/mau-trang.md`, `.agents/skills/giao-dien-quan-ly/scripts/soat-giao-dien.js`, rồi chép y nguyên sang `.claude/skills/giao-dien-quan-ly/` (hai bản phải giống nhau: `diff -rq` không in gì).

- [x] **Bước 1: `SKILL.md`**
  - Mục "Layout đã có sẵn": dòng `trosync-tokens.css` thêm `trosync-nen.css` với bảng 16 biến `--ts-*` của Task 1 và một câu: "màu của màn mới lấy từ các biến này hoặc `var(--tblr-*)`; không gõ mã màu thương hiệu". Font ghi Be Vietnam Pro.
  - Câu ở Tổng quan "không tự chế phong cách mới" đổi thành "dùng biến trong `trosync-nen.css` và lớp có thật trong Tabler 1.4".
  - Bảng tra nhanh thêm hai dòng: "Nút chính của màn | một nút `btn btn-primary` mỗi màn; nút phụ `btn-outline-secondary` | `btn-success`/`btn-info` làm nút chính" và "Tiêu đề trang | `<h2 class="page-title">` không icon, không `text-primary` | tiêu đề tô màu thương hiệu".
  - Mục "Không áp phần bố cục cho": bỏ ý cổng khách thuê dùng nền khác; ghi cổng khách thuê dùng chung `trosync-nen.css`, vẫn bo góc `--ts-radius-tenant`.
- [x] **Bước 2: `mau-trang.md`**: thẻ tiêu đề ví dụ đổi thành `<h2 class="page-title">Tiền cọc đang giữ</h2>` (bỏ icon, `text-primary`, `fw-bold`, `d-flex`); nút "Xuất Excel" giữ `btn-outline-primary`.
- [x] **Bước 3: `soat-giao-dien.js`**: thêm quy tắc mức LỖI áp cho `.cshtml`, `.js`, `.mjs`, `.css`: biểu thức `/#(?:206bc4|066fd1|2563eb|1d4ed8)\b|rgba?\(\s*32,\s*107,\s*196/i`, thông báo "Màu thương hiệu gõ cứng - dùng `var(--tblr-primary)` / `var(--ts-brand-text)`". Bỏ qua khi tên file là `trosync-nen.css`, `trosync-tokens.css` hoặc dòng nằm trong thẻ `<svg>…</svg>` của logo (logo giữ gradient riêng). Nếu script hiện không nhận file `.css` làm đầu vào thì mở rộng phần gom file cho nhận `.css`, các quy tắc cũ vẫn chỉ áp theo danh sách `ap` của chúng.
- [x] **Bước 4:** chạy script trên `wwwroot/css/site.css` và các file vỏ đã sửa: 0 LỖI ở dòng đã sửa. Không commit (các thư mục này bị `.gitignore`; phiên chính xử lý bằng `git bn`).

### Task 7: Tự kiểm và báo cáo

- [x] `dotnet build QuanLyChoThuePhongTroWeb.sln` → 0 lỗi, không cảnh báo mới.
- [x] `git diff --check master...HEAD` sạch; `git diff --stat master...HEAD` chỉ gồm các file nêu trong kế hoạch này và ba file tài liệu của module.
- [x] `git diff master...HEAD --name-only` không có: `trosync-tokens.css`, `_PublicRoomLayout.cshtml`, `guest-*`, `khach-vang-lai*`, `Program.cs`, `*.csproj`, `appsettings*`.
- [x] Tìm `id="` trùng trong `_Sidebar.cshtml` + `_Header.cshtml`, và trong `_SidebarKhachThue.cshtml` + `_Header.cshtml`: không có `id` nào xuất hiện hai lần.
- [x] Báo cáo cuối (trả lời trực tiếp, không tạo file): danh sách commit; từng task PASS hoặc lệch kế hoạch chỗ nào và vì sao; kết quả build và script soát; mục **CHƯA CHẠY** phải ghi rõ: `Web.IntegrationTests` (ba lớp test mới và các lớp cũ), kiểm trình duyệt sáng/tối/375px. Không ghi PASS cho thứ chưa chạy.

---

## Kết quả (11/11/2026)

Agent Sonnet 5.5 (xhigh) làm xong 7 task: `f4a20f6`, `08ea722`, `ee13e45`, `266d063`, `c9502e1`. Phiên chính sửa thêm sau khi chạy test và mở trình duyệt:

- `a490c17`: dòng menu 34px từ 992px (bộ chọn dài để thắng Tabler); đầu cột bảng ép `text-transform: none`; dưới 992px `.navbar-vertical` `overflow: visible` (menu người dùng trước đó bị che hoàn toàn); chấm số trên chuông điện thoại kéo vào trong; mục "Bảng điều khiển" thêm `ActivePrefixes`; màn đăng nhập đổi `--font-body`; hai test cũ dò `dropdown-item active` đổi sang `nav-link active`; test mới `VoKhachThue_…` kiểm `brand-title` thay cho chuỗi "TroSync" (chữ bị tách bởi thẻ `span`).
- `cb9aaba`: mục thanh tab khách thuê `min-width: 0`, cỡ chữ 11px (font mới rộng hơn Inter làm trang rộng 380px trên màn 375px).

Kiểm chứng: `Web.IntegrationTests` 874/874 trên `cb9aaba`; trình duyệt ba vai trò ở sáng, tối, 375px, console không lỗi; `/phong` không đổi (vẫn Inter, không nạp `trosync-nen.css`).

Để lại: bảng "Tiền mặt nhân viên đang giữ" làm Bảng điều khiển Admin tràn ngang ở 375px khi tên nhân viên dài (đợt 2); Hóa đơn và Việc cần làm tràn do bảng (đợt 3, 5).
