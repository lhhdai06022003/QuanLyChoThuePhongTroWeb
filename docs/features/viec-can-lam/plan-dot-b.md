# Kế hoạch: Trung tâm việc cần làm, ĐỢT B (giao diện)

- Nguồn: `docs/features/viec-can-lam/spec.md` bản 1.1 (đã duyệt), §7, §8, §11, §13; mockup `docs/features/viec-can-lam/mockup/dashboard-phuong-an-b.html` (Dashboard, phương án B) và `docs/features/viec-can-lam/mockup/trang-viec-can-lam-phuong-an-a-b.html` (**chỉ phương án A**). Coder KHÔNG cần đọc spec; mọi quy định đã chép vào đây.
- Nhánh `feature/viec-can-lam`. Trước khi sửa: `git status --short`. Code đợt A (backend) chưa commit nhưng đã duyệt, là nền có sẵn: chỉ sửa những chỗ ghi trong kế hoạch này. **Không sửa, xóa hay stage bất kỳ file nào trong `docs/`.** Không commit/push.
- Coder **phải đọc** `.agents/rules/frontend_code_review.md` trước khi sửa view/JS/CSS. Không dùng skill frontend-design.
- Phạm vi: Dashboard phương án B, trang `GET /QuanLyNhaTro/ViecCanLam`, menu + badge, đọc `trangThaiPhatHanh` ở trang Hóa đơn, định dạng tiền "đ" trên Dashboard và trang Việc cần làm, 4 ghi chú của reviewer đợt A.

---

## CẦN NGƯỜI DÙNG XÁC NHẬN

Schema: **KHÔNG ĐỔI SCHEMA** (không đụng entity, `Configurations/`, `ApplicationDbContext`, `Migrations/`, index, kiểu cột).

Tiền: đợt B chỉ đổi **cách hiển thị** số tiền, không đổi công thức tính (công thức đã được xác nhận ở đợt A, T1–T10).

| # | Điểm | File | Trước | Sau |
|---|---|---|---|---|
| M1 | Thẻ KPI "Đã thu" | `Views/Dashboard/Index.cshtml`, `wwwroot/js/dashboard.js` (mới) | Tiêu đề "DOANH THU ĐÃ THU", giá trị `ToString("N0") ₫` (Razor, phụ thuộc culture máy chủ) và `Intl vi-VN currency` (JS) | Tiêu đề "Đã thu T{m}/{y}", giá trị `42.500.000 đ`, chú thích "theo sổ thanh toán". Nguồn số giữ nguyên (`DoanhThuThangNay`) |
| M2 | Thẻ KPI "Chờ thu" | như M1 | "DOANH THU CHỜ THU", `… ₫` | "Chờ thu", `18.200.000 đ`, chú thích "hóa đơn đã gửi khách". Nguồn số giữ nguyên (`TongTienChoThu`, đã chỉ tính `DaGui` từ đợt A) |
| M3 | Thẻ KPI mới "Quá hạn" | như M1 | Không hiển thị (JSON đã có `tienQuaHan`, `soHoaDonQuaHan`) | Giá trị `TienQuaHan` dạng `3.590.000 đ`, chú thích "{SoHoaDonQuaHan} hóa đơn". Mọi kỳ, lọc theo chi nhánh đang chọn |
| M4 | Thẻ "Phòng đang nợ" | như M1 | Số phòng + "Từ trước tới nay" | Giữ số, chú thích "từ trước tới nay" (chỉ đổi bố cục) |
| M5 | Biểu đồ doanh thu 12 tháng | `wwwroot/js/dashboard.js` | Trục Y `toLocaleString('vi-VN') + ' ₫'`; tooltip mặc định Chart.js (số theo locale trình duyệt, không đơn vị) | Trục Y `10.000.000 đ`; tooltip `"{nhãn dataset}: 3.590.000 đ"`. Dữ liệu cột giữ nguyên |
| M6 | Hoạt động gần đây | `Application/Features/Dashboard/Services/DashboardService.cs` | `"Số tiền: {N0} ₫ - …"`, `"… - Giá thuê: {N0} ₫"` (N0 theo culture máy chủ) | `"Số tiền: 590.000 đ - …"`, `"… - Giá thuê: 2.000.000 đ"` qua hàm chung `TienTe.DinhDang` |
| M7 | Mô tả việc ("còn nợ … đ") | `Application/Features/ViecCanLam/Services/ViecCanLamService.cs` | Hàm private `DinhDangTien` | Gọi hàm chung `TienTe.DinhDang` (kết quả chuỗi **giống hệt**), hiển thị trên khối việc Dashboard (cả khi đổi bộ lọc) và trang Việc cần làm |
| M8 | Bảng "Hóa đơn quá hạn" | `Views/ViecCanLam/Index.cshtml` (mới) | Không có | Cột "Còn nợ" = `SoConNo` (đợt A: `TongTien` − Σ ledger chưa xóa) dạng `1.590.000 đ`; ≤ 50 dòng, hạn sớm nhất trước; "Còn N hóa đơn quá hạn khác" với N = `TongSo` − số dòng |
| M9 | Làm tròn khi hiển thị | `Application/Common/Formatting/TienTe.cs` (mới), `wwwroot/js/dashboard.js` | Mỗi nơi một kiểu | Server: `"{0:#,##0}"` InvariantCulture, đổi `,`→`.`, thêm `" đ"` (làm tròn tới đồng, nửa đơn vị làm tròn ra xa 0). JS: `Math.round` rồi chèn dấu chấm (khác server chỉ ở số âm có đúng ,5; số tiền VND trong hệ thống là số nguyên) |
| M10 | Trang Hóa đơn đọc `trangThaiPhatHanh` từ URL | `Views/HoaDon/Index.cshtml` | Bỏ qua tham số | Chọn sẵn ô lọc "Phát hành" theo tham số (0–4); chỉ đổi bộ lọc danh sách, không đổi số tiền |

Màn khác (Hóa đơn, Công nợ, Lịch sử thanh toán…) **giữ "₫"**.

ĐÃ XÁC NHẬN: toàn bộ M1–M10 như bảng trên, không đổi schema (08/10/2026 11:47)

## CÂU HỎI CÒN BỎ NGỎ

Coder làm theo "Mặc định" trừ khi người dùng trả lời khác trước khi bắt đầu.

1. **Ghi chú (4) của reviewer: gọi `GetScopeAsync` một lần mỗi request Dashboard.** Đã đánh giá ba cách:
   - (a) Controller lấy scope rồi truyền `EmployeeAccessScope` xuống service: cần method public nhận scope, vi phạm quy ước "tham số đầu là `actorId`" (`BranchScopeArchitectureTests`) và cho tầng gọi tự dựng scope (ví dụ `isAdmin: true`), phá nguyên tắc fail closed ở service. Không chấp nhận.
   - (b) `DashboardService` gọi lõi của `ViecCanLamService` qua method `internal` nhận scope: hoặc phụ thuộc lớp cụ thể (trái `backend_code_review.md` §8 "service phụ thuộc hoàn toàn vào Interface"), hoặc phải lộ interface public nhận scope (quay về (a)); đồng thời trái spec §9 ("DashboardController gọi cả hai service").
   - (c) Ghi nhớ scope theo request trong `EmployeeAccessService` (đã đăng ký Scoped): không phá ranh giới tầng, nhưng đổi hành vi thành phần bảo mật đang được ~50 chỗ gọi ở mọi module dùng, ngoài phạm vi đợt B.
   - Chi phí giữ nguyên: thêm 1 truy vấn nhỏ (`GetActorWithActiveBranchesAsync`) mỗi request Dashboard; trang Việc cần làm gọi 3 lần (tổng hợp, bảng quá hạn, dropdown chi nhánh).
   - **Mặc định: KHÔNG làm**, coder ghi nguyên lý do trên vào `.bangiao/thay-doi.md`. Nếu người dùng chọn (c): thêm `private readonly Dictionary<int, EmployeeAccessScope?> _scopeCache = new();` vào `src/QuanLyChoThuePhongTroWeb.Application/Features/NhanViens/Services/EmployeeAccessService.cs`, `GetScopeAsync` trả giá trị đã nhớ cho cùng `actorId` (kể cả `null`), `CanPerformAsync` không đổi; thêm test trong `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/EmployeeAccessTests.cs`: gọi hai lần cùng actor thì store chỉ bị gọi một lần, hai actor khác nhau thì độc lập.
2. **Dòng phụ của mỗi việc trong khối Dashboard** (mockup lúc ghi nhóm, lúc ghi chi nhánh, lúc ghi số còn nợ). Mặc định: dòng phụ = `MoTa` của việc (đã gồm số theo chi nhánh và "còn nợ … đ" với TT2/TT3), cắt một dòng bằng `text-truncate`, đủ nội dung trong `title`.
3. **Link "Xem tất cả {tổng} việc →" trên Dashboard.** Mặc định: khi Dashboard đang lọc một chi nhánh thì link mang `?chiNhanhId={id}` (để tổng trên trang khớp số trên Dashboard); "Tất cả chi nhánh" thì không có tham số.
4. **Lọc nhóm trên trang Việc cần làm.** Mặc định: tiêu đề "Việc cần làm ({tổng})" giữ tổng không lọc; số trên tiêu đề mỗi khối Khẩn/Cần làm/Theo dõi = Σ số lượng các dòng đang hiện; khối không còn dòng nào thì ẩn; không còn dòng nào ở mọi khối thì hiện "Không có việc trong nhóm này."; bảng "Hóa đơn quá hạn" luôn hiện, không bị lọc nhóm.
5. **Nhãn nút trên trang Việc cần làm.** Mặc định: "Xem" cho TT2, TT3, KH3; "Xử lý" cho các việc còn lại (theo mockup A).

ĐÃ TRẢ LỜI: người dùng giữ phương án Mặc định cho cả 5 câu, gồm không làm ghi chú (4) (08/10/2026 11:47).

---

## 1. Domain

Không đổi file nào.

## 2. Application

### 2.1 Tạo `src/QuanLyChoThuePhongTroWeb.Application/Common/Formatting/TienTe.cs`

```csharp
namespace QuanLyChoThuePhongTroWeb.Application.Common.Formatting
{
    // Định dạng tiền dùng trên Dashboard và trang Việc cần làm: "3.590.000 đ". Không dùng culture vi-VN (phụ thuộc ICU).
    public static class TienTe
    {
        public static string DinhDang(decimal soTien)
            => string.Format(CultureInfo.InvariantCulture, "{0:#,##0}", soTien).Replace(",", ".") + " đ";
    }
}
```

### 2.2 Sửa `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/Services/ViecCanLamService.cs`
- Xóa method private `DinhDangTien` (dòng 374–375); chỗ gọi ở dòng 333 đổi thành `TienTe.DinhDang(tongConNo.Value)`; thêm `using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;`. Bỏ `using System.Globalization;` nếu không còn dùng. Không sửa gì khác.

### 2.3 Sửa `src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard/Services/DashboardService.cs`
- Dòng 120: `$"Số tiền: {TienTe.DinhDang(p.SoTienThanhToan)} - {(…giữ nguyên…)}"`.
- Dòng 128: `$"Khách thuê: {c.HoVaTen} - Giá thuê: {TienTe.DinhDang(c.TienThuePhong)}"`.
- Thêm `using …Common.Formatting;`. Không sửa gì khác (giữ icon, màu, cách lọc).

## 3. Infrastructure

Không đổi file nào.

## 4. Web

### 4.1 Sửa `src/QuanLyChoThuePhongTroWeb.Web/GlobalUsings.cs`
Thêm `global using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;`. Trong `.cshtml` mới/sửa vẫn ghi `@using QuanLyChoThuePhongTroWeb.Application.Common.Formatting` ở đầu file cho rõ.

### 4.2 Sửa `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/DashboardController.cs` (ghi chú 2)
- Inject thêm `TimeProvider timeProvider` (đã đăng ký `TimeProvider.System` trong `AddApplication`).
- `Index`: thay `var today = DateTime.Now;` bằng `var nowVn = _timeProvider.GetUtcNow().UtcDateTime.AddHours(7);`, `selectedYear = year ?? nowVn.Year`, `selectedMonth = month ?? nowVn.Month`.
- `GetAjaxData`: **giữ nguyên** (JSON đợt A đã đủ khóa cho đợt B).

### 4.3 Sửa `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/ViecCanLamController.cs`
Inject thêm `IPhongTroService`. Thêm action (giữ nguyên `TomTat`):

```csharp
[HttpGet("/QuanLyNhaTro/ViecCanLam")]
public async Task<IActionResult> Index(int? chiNhanhId, CancellationToken cancellationToken)
{
    if (chiNhanhId <= 0) chiNhanhId = null;
    var tongHop = await _service.GetTongHopAsync(CurrentActorId, chiNhanhId, cancellationToken);
    var quaHan = await _service.GetHoaDonQuaHanAsync(CurrentActorId, chiNhanhId, cancellationToken);
    var chiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);
    return View(ViecCanLamPageViewModel.From(tongHop, quaHan, chiNhanhs, chiNhanhId));
}
```
Dropdown chi nhánh lấy từ `GetDanhSachChiNhanhDropdownAsync` (đã giới hạn theo phạm vi; copy cách dùng ở `YeuCauSuCoController.Index`). Chi nhánh ngoài phạm vi: service trả rỗng (fail closed), không báo lỗi.

### 4.4 Tạo `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/ViewModels/Responses/ViecCanLam.cs`
Namespace `QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses` (copy quy ước `Dashboard.cs` cùng thư mục: mapping thủ công bằng static `From…`). Chỉ dùng DTO/enum của Application, không tham chiếu Domain.

```csharp
public class ViecCanLamItemViewModel
{
    public string MaViec { get; set; } = "";
    public string Nhom { get; set; } = "";        // tên enum NhomViec: "ChiSo", "HoaDon", "ThuTien", "KhachThueHopDong" (dùng cho data-nhom)
    public string TenNhom { get; set; } = "";     // "Chỉ số", "Hóa đơn", "Thu tiền", "Khách thuê & HĐ"
    public MucUuTienViec MucUuTien { get; set; }
    public string TenMucUuTien { get; set; } = ""; // "Khẩn", "Cần làm", "Theo dõi"
    public string MauUuTien { get; set; } = "";   // "red" (Khan), "orange" (CanLam), "blue" (TheoDoi)
    public string TieuDe { get; set; } = "";
    public string MoTa { get; set; } = "";
    public int SoLuong { get; set; }
    public string Link { get; set; } = "";
    public string NhanNut { get; set; } = "";     // "Xem" cho TT2, TT3, KH3; còn lại "Xử lý" (câu hỏi 5)
    public static ViecCanLamItemViewModel FromDto(ViecCanLamDto dto);
}

public class ViecCanLamKhoiViewModel
{
    public int TongSo { get; set; }
    public int SoKhan { get; set; }
    public int SoCanLam { get; set; }
    public int SoTheoDoi { get; set; }
    public List<ViecCanLamItemViewModel> Items { get; set; } = new();
    // gioiHan = null: lấy hết; Dashboard truyền 5. Giữ nguyên thứ tự service trả về.
    public static ViecCanLamKhoiViewModel FromDto(ViecCanLamTongHopDto dto, int? gioiHan = null);
}

public class HoaDonQuaHanItemViewModel
{
    public int HoaDonId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public string PhongChiNhanh { get; set; } = "";   // "{SoPhong} · {TenChiNhanh}"
    public string KhachThue { get; set; } = "";
    public string SoConNoText { get; set; } = "";     // TienTe.DinhDang(SoConNo)
    public string HanThanhToanText { get; set; } = ""; // HanThanhToanUtc.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
    public int SoNgayQuaHan { get; set; }
    public string Link { get; set; } = "";             // $"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={HoaDonId}"
}

public class ViecCanLamPageViewModel
{
    public int? SelectedChiNhanhId { get; set; }
    public List<SelectListItem> ChiNhanhs { get; set; } = new();   // Selected = (Value == SelectedChiNhanhId?.ToString())
    public ViecCanLamKhoiViewModel ViecCanLam { get; set; } = new();
    public List<HoaDonQuaHanItemViewModel> HoaDonQuaHan { get; set; } = new();
    public int TongSoHoaDonQuaHan { get; set; }
    public int SoHoaDonQuaHanConLai { get; set; }   // Math.Max(0, TongSo − Items.Count)
    public static ViecCanLamPageViewModel From(ViecCanLamTongHopDto tongHop, DanhSachHoaDonQuaHanDto quaHan, IReadOnlyList<SelectOptionDto> chiNhanhs, int? chiNhanhId);
}
```

### 4.5 Sửa `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/ViewModels/Responses/Dashboard.cs`
- Thêm `public decimal TienQuaHan { get; set; }`, `public int SoHoaDonQuaHan { get; set; }`, `public string TyLeLapDayText { get; set; }` (`TyLeLapDay.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + "%"`, ví dụ `88,1%`, `100%`, `0%`).
- Thay `ToDos` bằng `public ViecCanLamKhoiViewModel ViecCanLam { get; set; } = new();`, gán `ViecCanLamKhoiViewModel.FromDto(viecCanLam, 5)`. Xóa class `ToDoItemViewModel`.
- `FromDto` map thêm `TienQuaHan`, `SoHoaDonQuaHan`, `TyLeLapDayText`. Giữ nguyên phần còn lại.

### 4.6 Menu và badge
- Sửa `src/QuanLyChoThuePhongTroWeb.Web/Models/ModelsOther/MenuItem.cs`: thêm `public string? BadgeId { get; set; }`.
- Sửa `src/QuanLyChoThuePhongTroWeb.Web/Services/MenuService.cs`: chèn ngay sau mục "Dashboard":
  `new MenuItem { Name = "Việc cần làm", Icon = "ti ti-checklist", Url = "/QuanLyNhaTro/ViecCanLam", BadgeId = "badge-viec-can-lam" }` (Roles để null như mục Dashboard).
- Sửa `src/QuanLyChoThuePhongTroWeb.Web/Views/Shared/_MenuItem.cshtml`: ở nhánh mục lá (`else`, dòng 62–74), sau `<span class="nav-link-title">…</span>` thêm
  `@if (!string.IsNullOrEmpty(Model.BadgeId)) { <span id="@Model.BadgeId" class="badge ms-auto d-none"></span> }`.
- Sửa `src/QuanLyChoThuePhongTroWeb.Web/Views/Shared/_Layout.cshtml`: ngay sau dòng `<script src="~/js/thongbao.js" …>` thêm
  `@if (User.IsInRole("Admin") || User.IsInRole("NhanVien")) { <script src="~/js/viec-can-lam-badge.js" asp-append-version="true"></script> }`.
  Lý do đặt ở `_Layout` chứ không trong `_Sidebar`: sidebar render trước khi các script cuối trang được nạp, và luật frontend yêu cầu JS nằm ở file riêng.
- Tạo `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/viec-can-lam-badge.js` (IIFE, `'use strict'`, không cần jQuery):
  - Lấy `#badge-viec-can-lam`; không có thì thoát.
  - `fetch('/QuanLyNhaTro/ViecCanLam/TomTat', { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })` **một lần** mỗi lần tải trang.
  - Coi là lỗi nếu `!res.ok`, `res.redirected`, hoặc `content-type` không chứa `application/json`.
  - Thành công: `tongSo <= 0` → giữ `d-none`. Ngược lại `textContent = String(tongSo)`, bỏ `d-none`, thêm `bg-red text-red-fg` nếu `soKhan > 0`, ngược lại `bg-orange text-orange-fg`.
  - Lỗi: giữ/thêm `d-none`, `console.error('Không tải được số việc cần làm:', err)`. Không hiện popup.

### 4.7 Sửa `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml` (H8)
Sau dòng 414 (`if (urlChiNhanh) …`) thêm: đọc `urlParams.get('trangThaiPhatHanh')`; nếu khác `null` **và** `#filterPhatHanh` có `<option>` với đúng giá trị đó (so `this.value === giáTrị`) thì `$('#filterPhatHanh').val(giáTrị)`. Không sửa gì khác trong file.

### 4.8 View, JS, CSS của Dashboard và trang Việc cần làm
Xem mục 5 GIAO DIỆN.

## 5. GIAO DIỆN

- Mockup đã duyệt: `docs/features/viec-can-lam/mockup/dashboard-phuong-an-b.html` (**phương án B**) và `docs/features/viec-can-lam/mockup/trang-viec-can-lam-phuong-an-a-b.html` (**chỉ phương án A**, bỏ qua khối B). Mockup chỉ quy định bố cục và nội dung; dùng layout chung `_Layout`, class Tabler, không chép CSS của mockup. Số liệu trong mockup là ví dụ.
- Bắt buộc theo `.agents/rules/frontend_code_review.md`: JS ở file riêng trong `wwwroot/js/` (IIFE như `wwwroot/js/doi-chieu-thanh-toan.js`), CSS riêng ở `wwwroot/css/` nạp qua `@section Styles` với `asp-append-version="true"`, chỉ icon `ti ti-*` cho phần tử mới, không `bg-white`/`text-dark`, không `@Html.Raw` cho dữ liệu người dùng, không tràn ngang ở 375 px, mọi `<select>` có `<label>` (được phép `visually-hidden`).
- **Chống XSS (ghi chú 1, H6):** mọi JS dựng giao diện từ dữ liệu server phải tạo DOM bằng `document.createElement` + `textContent` (hoặc jQuery `.text()`), gán class bằng `classList`/`className`, gán `href` qua hàm `safeLink` (copy từ `wwwroot/js/thongbao.js`: chỉ nhận chuỗi bắt đầu `/` và không bắt đầu `//` hay `/\`, ngược lại `#`). Cấm `innerHTML`, `.html(…)`, `insertAdjacentHTML`, template string chứa HTML trong `dashboard.js`, `viec-can-lam.js`, `viec-can-lam-badge.js`.
- Định dạng tiền: Razor dùng `TienTe.DinhDang(...)`; JS dùng hàm `dinhDangTien(n)`: `Math.round(Number(n) || 0)`, lấy trị tuyệt đối, chèn `.` mỗi 3 chữ số (`/\B(?=(\d{3})+(?!\d))/g`), thêm `-` nếu âm, nối `' đ'`. Không dùng `Intl` currency hay `toLocaleString` cho tiền.
- Màu mức ưu tiên dùng chung: Khẩn = đỏ (`status-red`, `bg-red text-red-fg`), Cần làm = cam (`status-orange`, `bg-orange text-orange-fg`), Theo dõi = xanh (`status-blue`, `bg-blue text-blue-fg`). Chấm màu: `<span class="status-dot status-{màu}" aria-hidden="true"></span>` kèm `<span class="visually-hidden">{TenMucUuTien}</span>`.

### 5.1 Dashboard (`src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/Dashboard/Index.cshtml`, viết lại)

**Đầu trang:** giữ tiêu đề và ba select `#branchSelect`, `#monthSelect`, `#yearSelect` (giữ `asp-items`, option "-- Tất cả chi nhánh --"). Thêm `<label class="visually-hidden" for=…>` cho từng select ("Chi nhánh", "Tháng", "Năm") và một spinner `<span id="dashboardLoading" class="spinner-border spinner-border-sm text-secondary d-none" role="status" aria-label="Đang tải"></span>` cạnh các select.

**Hàng KPI** (`row row-cards mb-3`, mỗi thẻ `col-6 col-md-4 col-xl-2`; màn hẹp tự xếp 2 cột): mỗi thẻ `card card-sm h-100 text-white bg-{màu}` → `card-body` gồm 3 dòng: tiêu đề (`subheader text-white text-opacity-75`), giá trị (`h2 mb-0 fw-bold text-nowrap`; nếu ở 375 px số `1.234.567.890 đ` tràn thì hạ xuống `h3`), chú thích (`small text-white text-opacity-75`). Bỏ icon lớn đặt tuyệt đối của bản cũ.

| Thẻ | Màu | Tiêu đề (id) | Giá trị (id) | Chú thích (id) |
|---|---|---|---|---|
| 1 | `bg-success` | `Đã thu T{SelectedMonth}/{SelectedYear}` (`kpiDaThuTitle`) | `TienTe.DinhDang(DoanhThuThangNay)` (`statDoanhThu`) | `theo sổ thanh toán` |
| 2 | `bg-warning` | `Chờ thu` | `TienTe.DinhDang(TongTienChoThu)` (`statChoThu`) | `hóa đơn đã gửi khách` |
| 3 | `bg-danger` | `Quá hạn` | `TienTe.DinhDang(TienQuaHan)` (`statQuaHan`) | `{SoHoaDonQuaHan} hóa đơn` (`statSoHoaDonQuaHan`) |
| 4 | `bg-pink` | `Phòng đang nợ` | `SoPhongChuaThanhToan` (`statChuaThanhToan`) | `từ trước tới nay` |
| 5 | `bg-primary` | `Hợp đồng hoạt động` | `TongSoHopDongHoatDong` (`statHopDong`) | `&nbsp;` (giữ chiều cao) |
| 6 | `bg-cyan` | `Tỷ lệ lấp đầy` | `TyLeLapDayText` (`statTyLeLapDay`) | `{SoPhongDaThue}/{TongSoPhong} phòng` (`statPhongLapDay`) |

**Vùng chính:** một `div.dashboard-layout` (CSS grid, khai báo trong `dashboard.css`) chứa 4 card con, mỗi card một class vùng:
- `dashboard-chart`: card "Xu hướng doanh thu (Năm <span id="chartYearText">…</span>)", canvas `#revenueBarChart` (khung cao 320 px).
- `dashboard-phong`: card "Tình trạng phòng", canvas `#roomStatusChart` (khung cao 260 px).
- `dashboard-hoatdong`: card "Hoạt động gần đây", thân `#activitiesContainer` (timeline giữ cấu trúc cũ: `.timeline > .timeline-item > .timeline-badge.{ColorClass} > i.{Icon}` + tiêu đề, mô tả, `badge bg-light-lt` thời gian). Rỗng: "Chưa ghi nhận hoạt động vận hành nào gần đây."
- `dashboard-viec`: khối việc (dưới).
- Bố cục grid: ≥ 992 px: 3 cột, areas `"chart chart viec" "phong hoatdong viec"` (khối việc chiếm cột phải 1/3, cao hết hai hàng). 768–991 px: 2 cột, areas `"chart chart" "viec viec" "phong hoatdong"`. < 768 px: 1 cột, thứ tự chart → viec → phong → hoatdong (khối việc nằm ngay dưới biểu đồ). Cột dùng `minmax(0, 1fr)` để chữ dài không làm tràn; `gap: 1rem`.

**Khối "Việc cần làm"** (`div.card.h-100` có class `dashboard-viec`):
- `card-header`: `h3.card-title` = `<i class="ti ti-checklist me-2 text-primary"></i>Việc cần làm <span id="viecTongSo" class="text-secondary fw-normal">({TongSo})</span>`.
- `card-body`:
  - `#viecCanLamChips` (`d-flex flex-wrap gap-2 mb-2`): ba badge `#viecSoKhan` "Khẩn {SoKhan}" (`bg-red text-red-fg`), `#viecSoCanLam` "Cần làm {SoCanLam}" (`bg-orange text-orange-fg`), `#viecSoTheoDoi` "Theo dõi {SoTheoDoi}" (`bg-blue text-blue-fg`).
  - `#viecCanLamList`: tối đa **5** dòng (đã sắp sẵn từ service, không sắp lại). Mỗi dòng `a.list-group-item.list-group-item-action.px-0` (trong `div.list-group.list-group-flush`), `href` = `Link`; bên trong `d-flex align-items-center gap-2`: chấm màu + `visually-hidden` mức; `div.dashboard-viec-text.flex-fill` gồm `div.fw-medium.text-truncate` = `TieuDe` và `div.small.text-secondary.text-truncate` = `MoTa` (`title` = `MoTa`); cuối dòng `span.fw-bold.ms-2` = `SoLuong`.
- `card-footer.text-end` (`#viecCanLamFooter`): `a#viecXemTatCa` "Xem tất cả {TongSo} việc →", `href` = `Url.Action("Index", "ViecCanLam", new { area = "QuanLyNhaTro", chiNhanhId = Model.SelectedBranchId })` (câu hỏi 3).
- **Rỗng** (`TongSo == 0`): ẩn `#viecCanLamChips` và `#viecCanLamFooter` (`d-none`); `#viecCanLamList` hiện `div.text-center.text-secondary.py-4` gồm `<i class="ti ti-circle-check text-success fs-1 d-block mb-2"></i>` và "Không có việc cần xử lý".
- **Lúc tải trang khối này render bằng Razor** (bắt buộc: test HTML kiểm "Hóa đơn chờ Admin chốt" có/không trong HTML). JS chỉ dựng lại khi đổi bộ lọc, dùng đúng các id/class trên.

**Script:** cuối view chỉ còn
```html
<script>window.dashboardConfig = { chartLabels: …, chartData: …, chartDataChoThu: …, roomLabels: …, roomData: …, urlAjax: …, urlViecCanLam: … };</script>
<script src="~/js/dashboard.js" asp-append-version="true"></script>
```
Giá trị lấy bằng `@Html.Raw(Json.Serialize(...))` như bản cũ (chỉ số và nhãn tĩnh; URL qua `Url.Action("GetAjaxData", "Dashboard", new { area = "QuanLyNhaTro" })` và `Url.Action("Index", "ViecCanLam", new { area = "QuanLyNhaTro" })`). `@section Styles` chỉ còn `<link rel="stylesheet" href="~/css/dashboard.css" asp-append-version="true" />`.

**Tạo `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/dashboard.js`** (IIFE `(function (window, $) { 'use strict'; … })(window, jQuery);`):
- Hàm nội bộ: `dinhDangTien`, `dinhDangPhanTram(n)` (`(Math.round(Number(n) * 10) / 10).toString().replace('.', ',') + '%'`), `safeLink`, bảng màu `{ Khan: 'red', CanLam: 'orange', TheoDoi: 'blue' }`, bảng tên mức `{ Khan: 'Khẩn', CanLam: 'Cần làm', TheoDoi: 'Theo dõi' }`.
- Khởi tạo biểu đồ cột chồng (giữ 2 dataset, nhãn và màu cũ `#2fb344`, `#f59f00`, `stacked`) với `scales.y.ticks.callback = v => dinhDangTien(v)` và `plugins.tooltip.callbacks.label = ctx => ctx.dataset.label + ': ' + dinhDangTien(ctx.parsed.y)`; biểu đồ tròn giữ nguyên.
- Đổi `#branchSelect`, `#monthSelect`, `#yearSelect` → `reload()`:
  - Trước khi gửi: disable 3 select, hiện `#dashboardLoading`; `.always` bật lại, ẩn spinner.
  - GET `cfg.urlAjax` với `{ branchId, month, year }`.
  - Thành công: cập nhật `#chartYearText`, `#kpiDaThuTitle` (`'Đã thu T' + month + '/' + year`), các `stat*` bằng `.text(...)` (tiền qua `dinhDangTien`, `#statSoHoaDonQuaHan` = `soHoaDonQuaHan + ' hóa đơn'`, `#statTyLeLapDay` = `dinhDangPhanTram(tyLeLapDay)`, `#statPhongLapDay` = `soPhongDaThue + '/' + tongSoPhong + ' phòng'`); cập nhật hai biểu đồ như bản cũ; `renderViec(res.viecCanLam, branchId)`; `renderHoatDong(res.recentActivities)`.
  - Lỗi: `console.error(…)` và `window.showError('Không tải được dữ liệu Dashboard. Vui lòng thử lại.')` (hàm toàn cục có sẵn trong `_Layout`).
- `renderViec(viec, branchId)`: xóa con của `#viecCanLamList` (`replaceChildren()` hoặc `$(…).empty()`), dựng lại đúng cấu trúc mục "Khối Việc cần làm" bằng DOM API; cập nhật `#viecTongSo` = `'(' + tongSo + ')'`, ba chip, `#viecXemTatCa` text `'Xem tất cả ' + tongSo + ' việc →'` và `href` = `cfg.urlViecCanLam + (branchId ? '?chiNhanhId=' + encodeURIComponent(branchId) : '')`; xử lý trạng thái rỗng như Razor. Mỗi item: `mucUuTien` lạ thì coi như `TheoDoi`.
- `renderHoatDong(list)`: dựng lại `#activitiesContainer` bằng DOM API; `colorClass` và `icon` gán qua `className`/`classList` (không chèn HTML); tiêu đề, mô tả, thời gian qua `textContent`; rỗng thì hiện câu rỗng như Razor.

**Tạo `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/css/dashboard.css`:** chuyển các rule `.timeline`, `.timeline-item`, `.timeline-item:last-child`, `.timeline-badge` từ `@section Styles` cũ sang (giữ nguyên giá trị). Bỏ các rule `.bg-light`, `.border-dashed`, `.avatar` (bản mới không dùng). Thêm `.dashboard-layout` + 4 class vùng + media query như trên, và `.dashboard-viec-text { min-width: 0; }`.

### 5.2 Trang `/QuanLyNhaTro/ViecCanLam` (tạo `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/ViecCanLam/Index.cshtml`, phương án A)

`@model ViecCanLamPageViewModel`, `ViewData["Title"] = "Việc cần làm"`. Toàn bộ nội dung render bằng Razor; JS chỉ ẩn/hiện, không dựng HTML. Copy quy ước khung trang/`page-header` và dropdown chi nhánh từ `Views/DoiChieuThanhToan/Index.cshtml`.

- **Đầu trang** (`page-header`, `row g-2 align-items-center`): `col-12 col-md` = `h2.page-title` "Việc cần làm ({ViecCanLam.TongSo})"; `col-12 col-md-auto` = `<form method="get" asp-area="QuanLyNhaTro" asp-controller="ViecCanLam" asp-action="Index" id="vclForm">` chứa `<label for="vclChiNhanh" class="visually-hidden">Chi nhánh</label><select id="vclChiNhanh" name="chiNhanhId" class="form-select" asp-items="Model.ChiNhanhs"><option value="">Tất cả chi nhánh</option></select>`. Đổi chọn thì submit form (tải lại với `?chiNhanhId=`).
- **Nút lọc nhóm** (ẩn khi `TongSo == 0`): `div.btn-list.mb-3#vclNhomFilter` (`role="group"`, `aria-label="Lọc theo nhóm việc"`) gồm 5 `button type="button"`: "Tất cả" (`data-vcl-nhom=""`), "Chỉ số" (`ChiSo`), "Hóa đơn" (`HoaDon`), "Thu tiền" (`ThuTien`), "Khách thuê & HĐ" (`KhachThueHopDong`). Nút đang chọn `btn-primary` + `aria-pressed="true"`, còn lại `btn-outline-secondary` + `aria-pressed="false"`. Thêm `div.alert.alert-info.d-none#vclNhomRong` "Không có việc trong nhóm này.".
- **Ba khối** theo thứ tự Khẩn, Cần làm, Theo dõi; khối không có việc thì **không render**. Mỗi khối `div.card.mb-3.vcl-khoi` có `div.card-status-top.bg-{màu}`; `card-header` → `h3.card-title`: chấm màu + "{Khẩn|Cần làm|Theo dõi} (<span class="vcl-khoi-dem">{SoKhan|SoCanLam|SoTheoDoi}</span>)"; thân `div.list-group.list-group-flush` liệt kê **toàn bộ** việc ở mức đó, giữ thứ tự service.
  - Mỗi dòng `div.list-group-item.vcl-item` với `data-nhom="{Nhom}"`, `data-so-luong="{SoLuong}"`; bên trong `row g-2 align-items-center`: `col-auto` chấm màu + `visually-hidden` mức; `col` gồm `div.fw-medium` = `TieuDe` + `span.badge.bg-secondary-lt.ms-1` = `TenNhom`, và `div.small.text-secondary` = `MoTa`; `col-auto.fw-bold` = `SoLuong`; `col-auto` = `a.btn.btn-outline-primary` `href="{Link}"` text `NhanNut`.
- **Rỗng toàn trang** (`TongSo == 0`): thay ba khối bằng `div.card > div.card-body > div.empty` gồm `div.empty-icon` `<i class="ti ti-circle-check text-success"></i>` và `p.empty-title` "Không có việc cần xử lý".
- **Bảng "Hóa đơn quá hạn"** (luôn render, kể cả khi trống): `div.card#hoa-don-qua-han`; `card-header` → `h3.card-title` "Hóa đơn quá hạn ({TongSoHoaDonQuaHan})".
  - Không có dòng: `div.card-body.text-secondary` "Không có hóa đơn quá hạn.".
  - Có dòng: `div.table-responsive > table.table.card-table.table-vcenter`. Cột: "Mã hóa đơn" (`a href="{Link}"` = `MaHoaDon`), "Phòng · Chi nhánh" (`PhongChiNhanh`), "Khách thuê" (`KhachThue`), "Còn nợ" (`text-end text-nowrap`, `SoConNoText`), "Hạn thanh toán" (`text-nowrap`, `HanThanhToanText`), "Quá hạn" (`text-end text-nowrap`, "{SoNgayQuaHan} ngày"). Giữ thứ tự service (hạn sớm nhất trước, tối đa 50).
  - `SoHoaDonQuaHanConLai > 0`: `div.card-footer.text-secondary` "Còn {SoHoaDonQuaHanConLai} hóa đơn quá hạn khác".
- Màn hẹp: header xếp dọc (col-12), nút lọc tự xuống dòng (`btn-list`), dòng việc wrap trong `row g-2`, bảng cuộn ngang trong `table-responsive`; không tràn ngang ở 375 px. Không cần CSS riêng.
- `@section Scripts { <script src="~/js/viec-can-lam.js" asp-append-version="true"></script> }`.

**Tạo `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/viec-can-lam.js`** (IIFE, jQuery):
- `#vclChiNhanh` change → `$('#vclForm').trigger('submit')` (hoặc `form.submit()`).
- Click `[data-vcl-nhom]`: đổi trạng thái nút (class + `aria-pressed`); với mỗi `.vcl-item`: hiện nếu nhóm rỗng ("Tất cả") hoặc `data-nhom` khớp, ngược lại thêm `d-none`; mỗi `.vcl-khoi`: `.vcl-khoi-dem` = Σ `data-so-luong` của dòng đang hiện, ẩn khối nếu không còn dòng; `#vclNhomRong` hiện khi mọi khối đều ẩn. Không đụng tiêu đề trang và `#hoa-don-qua-han` (câu hỏi 4).

## 6. Trường hợp biên bắt buộc

- **Phân quyền:** mọi dữ liệu đã lọc ở service (đợt A); view/JS không lọc quyền. Nhân viên không thấy HD2 trong HTML trang Việc cần làm, khối Dashboard và JSON. Nhân viên chọn chi nhánh ngoài phạm vi (`?chiNhanhId=` hoặc `branchId=`): trang và khối hiện trạng thái rỗng, bảng quá hạn rỗng, không lỗi; dropdown không có chi nhánh đó. Khách thuê gọi `/QuanLyNhaTro/ViecCanLam` bị chuyển về `/QuanLyNhaTro/DangNhap` (kế thừa `AdminBaseController`).
- **Badge:** chỉ nạp script cho Admin/NhanVien; ẩn khi 0 hoặc lỗi (kể cả bị redirect sang trang đăng nhập).
- **XSS:** tên chi nhánh, tên khách, số phòng, mô tả việc chứa `<script>` phải hiện như văn bản ở cả Razor lẫn JS (nghiệm thu 6). Không `@Html.Raw` cho chúng.
- **Thời gian:** hạn thanh toán hiển thị `dd/MM/yyyy` giờ VN (`HanThanhToanUtc.AddHours(7)`); tháng/năm mặc định của Dashboard lấy từ `TimeProvider` + 7 giờ, không dùng `DateTime.Now`.
- **Xóa mềm / số còn nợ:** không tính lại ở Web; dùng nguyên số từ service đợt A.
- `chiNhanhId <= 0` coi như không lọc.
- Spec §5.1: link việc là đường dẫn tương đối do server tạo; không ghép thêm tham số ở Web (trừ link "Xem tất cả" của Dashboard).

## 7. Ranh giới tầng (test kiến trúc chặn)

- `TienTe` ở Application chỉ dùng `System`/`System.Globalization`.
- Web không có chuỗi `QuanLyChoThuePhongTroWeb.Domain` trong `.cs`/`.cshtml`; chỉ dùng `NhomViec`, `MucUuTienViec`, DTO của Application. `SelectListItem` chỉ ở Web.
- Không thêm method nào vào `IViecCanLamService`/`IDashboardService` (trừ khi người dùng chọn (c) ở câu hỏi 1, khi đó cũng không đổi interface).

## 8. Test

Viết test trước (TDD). Copy quy ước:
- Unit: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ViecCanLamServiceTests.cs`.
- Web: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/ViecCanLamEndpointTests.cs` (`[Collection(WebTestCollection.Name)]`, `BranchScopeKit.SeedAsync`, `LoginAsync`, `JsonAsync`, `WebUtility.HtmlDecode` trước khi so chuỗi tiếng Việt); ledger seed copy từ `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/DebtTestSeeder.cs` (`Ledger`); host có cấu hình riêng copy `_factory.WithWebHostBuilder(...)` ở `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/MeterImageTenantPortalTests.cs` dòng 1060.
- DB dùng chung: chỉ assert theo chi nhánh vừa seed; tháng/năm VN tính từ `DateTime.UtcNow.AddHours(7)` (không dùng `DateTime.Now`).

### 8.1 Application.UnitTests
Tạo `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/TienTeTests.cs` (`[Theory]`): `3590000m` → `"3.590.000 đ"`; `0m` → `"0 đ"`; `999m` → `"999 đ"`; `1234567890m` → `"1.234.567.890 đ"`; `-1500000m` → `"-1.500.000 đ"`; `1500.5m` → `"1.501 đ"`. `ViecCanLamServiceTests` hiện có (chuỗi "còn nợ 3.590.000 đ") phải vẫn PASS sau khi đổi sang `TienTe`.

### 8.2 Web.IntegrationTests
- Sửa `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/BranchScopeKit.cs`: đổi kiểu tham số `factory` của `NewClient` và `LoginAsync` từ `CustomWebApplicationFactory` sang `WebApplicationFactory<Program>` (lớp con vẫn truyền được; không đổi gì khác).
- Thêm vào `ViecCanLamEndpointTests.cs` (helper seed riêng: thế giới `SeedAsync` + một hóa đơn của `ContractA`: `Thang`/`Nam` = tháng/năm VN hiện tại, `TongTien = 3_590_000m`, `TrangThaiPhatHanh = DaGui`, `TrangThaiHoaDon = ThanhToanMotPhan`, `HanThanhToan = DateTime.UtcNow.AddDays(-3)`; ledger chưa xóa `590_000m` với `NgayThanhToan = DateTime.UtcNow.AddMinutes(-5)`; ledger đã xóa `100_000m`):
  1. **(ghi chú 3)** Admin `GetAjaxData?branchId=A&year=&month=` (tháng/năm VN): `tienQuaHan == 3_000_000`, `soHoaDonQuaHan == 1`, `tongTienChoThu == 3_000_000`. Nhân viên A không truyền `branchId`: cùng ba giá trị. Nhân viên A với `branchId=B`: cả ba bằng 0.
  2. Admin HTML `/QuanLyNhaTro/Dashboard?branchId=A&year=&month=` (đã decode) chứa `"3.000.000 đ"`, `"1 hóa đơn"`, `"Số tiền: 590.000 đ"`, `"Giá thuê: 2.000.000 đ"`, `"Xem tất cả"`; không chứa `"₫"`.
  3. Admin HTML `/QuanLyNhaTro/ViecCanLam?chiNhanhId=A` chứa `id="hoa-don-qua-han"`, mã hóa đơn đã seed, `"3.000.000 đ"`, hạn `HanThanhToan.AddHours(7).ToString("dd/MM/yyyy")`, `"3 ngày"`, `"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={id}"`, `"Hóa đơn chờ Admin chốt"` (dùng seed `SeedWithPendingInvoicesAsync` cho ý này hoặc seed thêm hóa đơn `ChoDuyet`).
  4. Nhân viên A: HTML `/QuanLyNhaTro/ViecCanLam` trả 200, chứa `"Việc cần làm ("`, không chứa tên chi nhánh B, không chứa `"Hóa đơn chờ Admin chốt"`.
  5. Nhân viên A: HTML `/QuanLyNhaTro/ViecCanLam?chiNhanhId={B}` chứa `"Việc cần làm (0)"`, `"Không có việc cần xử lý"`, `"Không có hóa đơn quá hạn."`, không chứa tên chi nhánh B.
  6. Khách thuê `GET /QuanLyNhaTro/ViecCanLam` → 302 về `/QuanLyNhaTro/DangNhap` (copy `TomTat_Tenant_IsRedirectedToLogin`).
  7. **XSS (nghiệm thu 6):** đổi `TenChiNhanh` của chi nhánh A thành `$"CN_A_{guid}<script>alert(1)</script>"` trong DB; Admin HTML thô (không decode) của `/QuanLyNhaTro/ViecCanLam?chiNhanhId=A` và `/QuanLyNhaTro/Dashboard?branchId=A` không chứa `<script>alert(1)</script>` và có chứa `&lt;script&gt;`.
  8. **Menu/badge:** nhân viên A, HTML `/QuanLyNhaTro/Dashboard` chứa `href="/QuanLyNhaTro/ViecCanLam"`, `id="badge-viec-can-lam"`, `viec-can-lam-badge.js`.
  9. **(ghi chú 2)** Host `_factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2027, 1, 31, 18, 0, 0, TimeSpan.Zero))); }))` (giờ VN 01/02/2027 01:00; `FixedTime` là lớp lồng riêng trong file test, kế thừa `TimeProvider`, override `GetUtcNow`). Seed bằng `_factory`, đăng nhập Admin trên host mới. HTML `/QuanLyNhaTro/Dashboard`: trong `<select id="monthSelect">` option `value="2"` là `selected`; trong `#yearSelect` option `value="2027"` là `selected`.
- Test hiện có phải vẫn PASS: toàn bộ `ViecCanLamEndpointTests` cũ (đặc biệt `DashboardHtml_StaffDoesNotSeeAdminOnlyJob_AdminDoes`), `CoreBranchScopeTests.Dashboard_StaffOfBranchA_SeesOnlyBranchA`, `AdminScreenAuthorizationTests.StaffDashboardHtml_DoesNotExpose_AdminOnlyLinks`, `ArchitectureBoundaryTests`.

### 8.3 Kiểm tra tĩnh (tester chạy, báo kết quả)
```powershell
rg -n "innerHTML|\.html\(|insertAdjacentHTML" src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/dashboard.js src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/viec-can-lam.js src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/viec-can-lam-badge.js   # phải rỗng
rg -n "DateTime.Now" src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/DashboardController.cs   # phải rỗng
rg -n "₫" src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/Dashboard src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/ViecCanLam src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/dashboard.js src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard   # phải rỗng
rg -n "<script>" src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/ViecCanLam   # phải rỗng
```
Kiểm tra bằng trình duyệt nếu có (không có thì ghi CHƯA CHẠY): đổi bộ lọc Dashboard cập nhật khối việc và thẻ KPI; 375 px không tràn ngang; dark mode đọc được; badge đỏ/cam/ẩn; lọc nhóm trên trang Việc cần làm; link HD1/HD2/HD3/TT3 mở trang Hóa đơn với ô "Phát hành" đúng.

### 8.4 Lệnh chạy (PowerShell, từ gốc repo)
```powershell
git status --short
dotnet build QuanLyChoThuePhongTroWeb.sln
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~TienTeTests|FullyQualifiedName~ViecCanLamServiceTests|FullyQualifiedName~BranchScopeArchitectureTests|FullyQualifiedName~ArchitectureBoundaryTests"

# Cần DB riêng tên kết thúc _test hoặc _integration_test; thiếu biến thì ghi CHƯA CHẠY, không ghi PASS
$env:QLCTPT_TEST_CONNECTION_STRING = "Host=...;Database=qlctpt_test;..."
dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests --filter "FullyQualifiedName~ViecCanLamEndpointTests|FullyQualifiedName~CoreBranchScopeTests|FullyQualifiedName~AdminScreenAuthorizationTests|FullyQualifiedName~ArchitectureBoundaryTests"
dotnet test QuanLyChoThuePhongTroWeb.sln

# Không lệch schema (cần QLCTPT_DESIGNTIME_CONNECTION_STRING); phải báo không có thay đổi, không tạo migration
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj

git diff --check
```

## 9. Schema PostgreSQL

**KHÔNG ĐỔI SCHEMA.** Không sửa entity, `Configurations/`, `ApplicationDbContext`, `Migrations/`; không thêm index.

## 10. Danh sách file

Tạo mới:
- `src/QuanLyChoThuePhongTroWeb.Application/Common/Formatting/TienTe.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/ViewModels/Responses/ViecCanLam.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/ViecCanLam/Index.cshtml`
- `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/dashboard.js`
- `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/viec-can-lam.js`
- `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/viec-can-lam-badge.js`
- `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/css/dashboard.css`
- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/TienTeTests.cs`

Sửa:
- `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/Services/ViecCanLamService.cs` (chỉ đổi hàm định dạng)
- `src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard/Services/DashboardService.cs` (chỉ 2 chuỗi hoạt động gần đây)
- `src/QuanLyChoThuePhongTroWeb.Web/GlobalUsings.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/DashboardController.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/ViecCanLamController.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/ViewModels/Responses/Dashboard.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/Dashboard/Index.cshtml`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml` (chỉ thêm đọc `trangThaiPhatHanh`)
- `src/QuanLyChoThuePhongTroWeb.Web/Models/ModelsOther/MenuItem.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Services/MenuService.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Views/Shared/_MenuItem.cshtml`
- `src/QuanLyChoThuePhongTroWeb.Web/Views/Shared/_Layout.cshtml`
- `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/BranchScopeKit.cs`
- `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/ViecCanLamEndpointTests.cs`

Không sửa: mọi file trong `docs/`, `CLAUDE.md`, `AGENTS.md`, Infrastructure, Domain, `IViecCanLamService`, `IDashboardService`, `IDashboardStore`, `DashboardController.GetAjaxData`, các màn khác ngoài trang Hóa đơn (giữ "₫"). Không commit/push.
