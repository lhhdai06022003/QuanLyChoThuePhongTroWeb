# Kế hoạch: Trung tâm việc cần làm, ĐỢT A (backend)

- Nguồn: `docs/features/viec-can-lam/spec.md` bản 1.1 (đã duyệt). Coder KHÔNG cần đọc spec; mọi quy định cần thiết đã chép vào đây.
- Nhánh: `feature/viec-can-lam`. Trước khi sửa: `git status --short`. Có thay đổi chưa commit trong `docs/` (spec, README, mockup): **không sửa, không xóa, không stage**.
- Phạm vi đợt A: Application `Features/ViecCanLam`, `ViecCanLamStore`, sửa KPI Dashboard, endpoint JSON `TomTat` và `GetAjaxData`, DI, options, test.
- KHÔNG làm trong đợt A (để đợt B): trang HTML `GET /QuanLyNhaTro/ViecCanLam`, view Dashboard mới, menu/badge, sửa XSS của hoạt động gần đây, định dạng "đ" trên thẻ KPI/biểu đồ, đọc `trangThaiPhatHanh` ở trang Hóa đơn. Không dùng skill frontend-design.

---

## CẦN NGƯỜI DÙNG XÁC NHẬN

Đợt này đụng logic tiền. Schema: **KHÔNG ĐỔI SCHEMA** (không đụng `Configurations/`, `ApplicationDbContext`, `Migrations/`, index, kiểu cột).

| # | Điểm | File | Trước | Sau |
|---|---|---|---|---|
| T1 | KPI "Chờ thu" kỳ đang chọn | `Infrastructure/Persistence/Features/DashboardStore.cs` (`GetInvoiceMonthlyStatsAsync`), `Application/Features/Dashboard/Services/DashboardService.cs` | Σ(`TongTien` − đã thu) của mọi hóa đơn chưa xóa, `TrangThaiHoaDon ≠ DaThanhToan`, kể cả `Nhap`, `ChoDuyet`, `DaChot` | Chỉ hóa đơn `TrangThaiPhatHanh = DaGui`, chưa xóa, `≠ DaThanhToan` |
| T2 | Cột "Chờ thu" biểu đồ 12 tháng | như T1 | như T1, theo từng tháng | như T1, theo từng tháng |
| T3 | Cột/KPI "Đã thu" | như T1 | Σ `LichSuThanhToan` chưa xóa của mọi hóa đơn chưa xóa trong kỳ | **Giữ nguyên** (không lọc theo trạng thái phát hành) |
| T4 | KPI "Phòng đang nợ" | `DashboardStore.CountUnpaidRoomsAsync` | Số phòng khác nhau có hóa đơn chưa xóa `≠ DaThanhToan` (mọi trạng thái phát hành) | Thêm điều kiện `DaGui` |
| T5 | KPI mới "Quá hạn" (`TienQuaHan`, `SoHoaDonQuaHan`) | `DashboardStore.GetOverdueSummaryAsync` (mới), `DashboardService`, `DashboardController.GetAjaxData` | Không có | Mọi kỳ, lọc chi nhánh: hóa đơn chưa xóa, `DaGui`, `≠ DaThanhToan`, `HanThanhToan != null && HanThanhToan < nowUtc`. Tiền = Σ(`TongTien` − Σ`SoTienThanhToan` của `LichSuThanhToan` chưa xóa) |
| T6 | Việc TT2 "Hóa đơn quá hạn", TT3 "Hóa đơn đã gửi, chưa thu đủ": số lượng và tổng còn nợ trong mô tả | `ViecCanLamStore`, `ViecCanLamService` (mới) | Không có | TT2 như T5; TT3: `DaGui`, `≠ DaThanhToan`, `HanThanhToan == null || HanThanhToan >= nowUtc`. Mô tả ghi "còn nợ 3.590.000 đ" |
| T7 | Việc HD3 "Hóa đơn đã chốt, chưa gửi khách" | `ViecCanLamStore` | Không có | `DaChot`, chưa xóa, `TrangThaiHoaDon ≠ DaThanhToan` |
| T8 | Bảng hóa đơn quá hạn (service, đợt B mới hiển thị) | `ViecCanLamService.GetHoaDonQuaHanAsync` | Không có | Số còn nợ từng hóa đơn = `TongTien` − Σ ledger chưa xóa; hạn sớm nhất trước; tối đa 50 dòng |
| T9 | Bỏ việc cũ "Hóa đơn T{m}/{y} chưa thu ({chi nhánh})" | `DashboardService` dòng 200–213, `DashboardStore.GetUnpaidInvoicesGroupedAsync` | Một dòng cho mỗi tháng × chi nhánh, đếm mọi trạng thái phát hành | Xóa; thay bằng HD1/HD2/HD3/TT2/TT3 |
| T10 | Định dạng tiền trong mô tả việc | `ViecCanLamService` | — | `3.590.000 đ` (chấm ngăn nghìn, đơn vị "đ"). Trong đợt A, khối việc trên Dashboard (render từ mô tả) sẽ hiện "đ" trong khi thẻ KPI còn "₫" cho tới đợt B |

ĐÃ XÁC NHẬN: toàn bộ T1–T10 như bảng trên, không đổi schema (08/10/2026 11:04)

## CÂU HỎI CÒN BỎ NGỎ

Coder làm theo "Mặc định" trừ khi người dùng trả lời khác trước khi bắt đầu.

1. **Biên đúng ngưỡng KH1/KH2.** Sự cố gửi đúng 24 giờ (KH1) có tính là Khẩn không? Sự cố đang xử lý đúng 7 ngày (KH2) có tính là "quá lâu" không? Mặc định: CÓ cho cả hai (`NgayGui <= nowUtc − ngưỡng`).
2. **Sắp xếp CS1/CS2 nhiều kỳ (§5.2).** Spec ghi: mức ưu tiên → số lượng giảm dần → mã việc, và "CS1, CS2 nhiều kỳ thì kỳ cũ hơn đứng trước". Hai kỳ cùng mã, cùng mức nhưng khác số lượng thì xếp theo số lượng hay theo kỳ? Mặc định: khóa sắp `(MucUuTien, SoLuong giảm, MaViec, (Nam, Thang) tăng)`, tức kỳ chỉ là tiêu chí phụ cuối.
3. **Khóa `soHopDongSapHetHan`, `soPhongChuaChotDienNuoc` trong `GetAjaxData`.** Spec §8 ghi "như hiện tại … bỏ `toDos`" nên hai khóa này còn, nhưng không View/JS nào đọc. Mặc định: giữ hai khóa, tính trong controller từ kết quả `IViecCanLamService` (tổng `SoLuong` của các dòng `CS1`, và `SoLuong` dòng `KH3`, không có thì 0).
4. **Đơn vị đếm CS2.** Mặc định: đếm số **ảnh** (`AnhChiSoDongHo`) thỏa điều kiện, không đếm số phòng.

ĐÃ TRẢ LỜI: người dùng giữ phương án Mặc định cho cả 4 câu (08/10/2026 11:04).

---

## 1. Domain

Không đổi file nào.

## 2. Application

### 2.1 Tạo `src/QuanLyChoThuePhongTroWeb.Application/Common/Configurations/ViecCanLamSettings.cs`

Copy quy ước `DashboardSettings.cs` (sealed record, `init`, giá trị mặc định):

```csharp
public sealed record ViecCanLamSettings
{
    public int SuCoChoTiepNhanGapSauGio { get; init; } = 24;   // KH1
    public int SuCoXuLyQuaLauSauNgay { get; init; } = 7;       // KH2
    public int HopDongSapHetHanTrongNgay { get; init; } = 30;  // KH3
    public int HopDongSapHetHanGapTrongNgay { get; init; } = 7; // KH3
    public int SoKyQuetChiSo { get; init; } = 6;               // CS1
    public int SoDongHoaDonQuaHan { get; init; } = 50;         // bảng quá hạn
}
```

`DashboardSettings.ChotDienNuocDay` giữ nguyên, dùng cho CS1.

### 2.2 Tạo `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/DTOs/ViecCanLamDTOs.cs`

Namespace `QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs`. Chỉ dùng kiểu Application (Web sẽ dùng).

```csharp
public enum NhomViec { ChiSo = 0, HoaDon = 1, ThuTien = 2, KhachThueHopDong = 3 }
public enum MucUuTienViec { Khan = 0, CanLam = 1, TheoDoi = 2 }   // giá trị nhỏ = gấp hơn

public sealed class ViecCanLamDto
{
    public string MaViec { get; init; } = string.Empty;   // "CS1", "CS2", "HD1"... "KH3"
    public NhomViec Nhom { get; init; }
    public MucUuTienViec MucUuTien { get; init; }
    public string TieuDe { get; init; } = string.Empty;
    public string MoTa { get; init; } = string.Empty;
    public int SoLuong { get; init; }
    public string Link { get; init; } = string.Empty;     // đường dẫn tương đối
    public int? Thang { get; init; }                       // chỉ CS1, CS2
    public int? Nam { get; init; }                         // chỉ CS1, CS2
    public decimal? TongConNo { get; init; }               // chỉ TT2, TT3
}

public sealed class ViecCanLamTongHopDto
{
    public int TongSo { get; init; }
    public int SoKhan { get; init; }
    public int SoCanLam { get; init; }
    public int SoTheoDoi { get; init; }
    public IReadOnlyList<ViecCanLamDto> Items { get; init; } = Array.Empty<ViecCanLamDto>();
    public static ViecCanLamTongHopDto Rong { get; } = new();
}

public sealed class HoaDonQuaHanDto
{
    public int HoaDonId { get; init; }
    public string MaHoaDon { get; init; } = string.Empty;
    public string SoPhong { get; init; } = string.Empty;
    public string TenChiNhanh { get; init; } = string.Empty;
    public string KhachThue { get; init; } = string.Empty;   // người đại diện hợp đồng (HopDong.NguoiThue.HoVaTen)
    public decimal SoConNo { get; init; }
    public DateTime HanThanhToanUtc { get; init; }
    public int SoNgayQuaHan { get; init; }
}

public sealed class DanhSachHoaDonQuaHanDto
{
    public IReadOnlyList<HoaDonQuaHanDto> Items { get; init; } = Array.Empty<HoaDonQuaHanDto>();
    public int TongSo { get; init; }   // tổng số hóa đơn quá hạn (để đợt B ghi "Còn N hóa đơn quá hạn khác")
}
```

### 2.3 Tạo `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/DTOs/ViecCanLamStoreRows.cs`

```csharp
public sealed class DemChiNhanhRow { int ChiNhanhId; string TenChiNhanh; int SoLuong; int SoLuongGap; }   // SoLuongGap chỉ KH1, KH3; còn lại 0
public sealed class DemTheoKyRow  { int ChiNhanhId; string TenChiNhanh; int Thang; int Nam; int SoLuong; }
public sealed class DemHoaDonRow  { int ChiNhanhId; string TenChiNhanh; int SoLuong; int NamCuNhat; decimal TongConNo; } // TongConNo chỉ TT2, TT3; HD* = 0
public sealed class HopDongChoChotChiSoRow { int PhongTroId; DateTime ThoiDiemBatDau; int ChiNhanhId; string TenChiNhanh; }
public sealed class KyChiSoDaGhiRow { int PhongTroId; int Thang; int Nam; }
public sealed class HoaDonQuaHanRow { int HoaDonId; string MaHoaDon; string SoPhong; int ChiNhanhId; string TenChiNhanh; string KhachThue; decimal TongTien; decimal DaThu; DateTime HanThanhToan; }
```
(Viết dạng property `{ get; init; }`, string mặc định `string.Empty`.)

### 2.4 Tạo `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/Persistence/IViecCanLamStore.cs`

Mọi method nhận `int? branchId` (chi nhánh đang lọc, null = mọi chi nhánh) và `IReadOnlyCollection<int>? allowedBranchIds` (null = không giới hạn; rỗng = không có dữ liệu), **không có giá trị mặc định** cho `allowedBranchIds`. Ghi comment điều kiện lọc trên từng method.

```csharp
// CS1
Task<IReadOnlyList<HopDongChoChotChiSoRow>> GetHopDongDangHoatDongAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
Task<IReadOnlyList<KyChiSoDaGhiRow>> GetKyChiSoDaGhiNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, int tuThang, int tuNam, CancellationToken ct = default);
// CS2
Task<IReadOnlyList<DemTheoKyRow>> DemAnhChiSoChoXacNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
// HD1, HD2, HD3
Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonNhapAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonChoDuyetAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaChotChuaGuiAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
// TT1
Task<IReadOnlyList<DemChiNhanhRow>> DemMinhChungChoDoiChieuAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
// TT2, TT3
Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default);
Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaGuiChuaToiHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default);
// KH1: SoLuongGap = số sự cố có NgayGui <= mocGapUtc
Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoChoTiepNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocGapUtc, CancellationToken ct = default);
// KH2: NgayGui <= mocUtc
Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoXuLyQuaLauAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocUtc, CancellationToken ct = default);
// KH3: tuUtc <= ThoiDiemKetThuc < denTruocUtc; SoLuongGap = số có ThoiDiemKetThuc < gapTruocUtc
Task<IReadOnlyList<DemChiNhanhRow>> DemHopDongSapHetHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime tuUtc, DateTime denTruocUtc, DateTime gapTruocUtc, CancellationToken ct = default);
// Bảng quá hạn: điều kiện như TT2, sắp HanThanhToan tăng rồi HoaDonId tăng, lấy gioiHan dòng
Task<IReadOnlyList<HoaDonQuaHanRow>> GetHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, int gioiHan, CancellationToken ct = default);
```

### 2.5 Tạo `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/Services/IViecCanLamService.cs` và `ViecCanLamService.cs`

```csharp
public interface IViecCanLamService
{
    // Việc suy ra từ dữ liệu, giới hạn theo chi nhánh của actor; không lọc theo tháng/năm.
    Task<ViecCanLamTongHopDto> GetTongHopAsync(int actorId, int? branchId, CancellationToken ct = default);
    Task<DanhSachHoaDonQuaHanDto> GetHoaDonQuaHanAsync(int actorId, int? branchId, CancellationToken ct = default);
}

public class ViecCanLamService : IViecCanLamService
{
    public ViecCanLamService(IViecCanLamStore store, IEmployeeAccessService access,
        DashboardSettings dashboardSettings, ViecCanLamSettings settings, TimeProvider timeProvider) { ... }
}
```

**Phân quyền (cả hai method):**
1. `scope = await _access.GetScopeAsync(actorId, ct)` đúng **một lần**.
2. `scope == null` → trả `ViecCanLamTongHopDto.Rong` / `DanhSachHoaDonQuaHanDto` rỗng, không gọi store.
3. `branchId.HasValue && !scope.CanAccessBranch(branchId.Value)` → rỗng, không gọi store.
4. `allowed = scope.AllowedBranchIds`; nếu `allowed != null && allowed.Count == 0` (nhân viên chưa phân công) → rỗng, không gọi store.
5. Mỗi loại việc khai báo mã hành động (bảng dưới). Nếu `!scope.IsAdmin && EmployeeActionCodes.IsAdminOnly(maHanhDong)` → bỏ loại việc đó và **không gọi** truy vấn của nó.
6. Không báo lỗi, không ném exception cho các trường hợp trên. Không ghi gì vào DB.

**Thời gian:** `nowUtc = _timeProvider.GetUtcNow().UtcDateTime`; `nowVn = nowUtc.AddHours(7)`; `homNayVnBatDauUtc = DateTime.SpecifyKind(nowVn.Date.AddHours(-7), DateTimeKind.Utc)`. Không dùng `DateTime.UtcNow`/`DateTime.Now`.

**Danh mục việc** (gọi store theo đúng thứ tự này, tuần tự, không song song):

| Mã | Nhóm | Tiêu đề (`TieuDe`) | Mã hành động | Nguồn | Ưu tiên | Một dòng cho |
|---|---|---|---|---|---|---|
| CS1 | ChiSo | `Phòng chưa chốt điện nước T{m}/{y}` | `EmployeeActionCodes.MeterUpload` | xem "CS1" dưới | Kỳ mục tiêu: CanLam; kỳ cũ hơn: Khan | Mỗi kỳ |
| CS2 | ChiSo | `Ảnh chỉ số chờ xác nhận T{m}/{y}` | `MeterReview` | `DemAnhChiSoChoXacNhanAsync` | CanLam | Mỗi kỳ |
| HD1 | HoaDon | `Hóa đơn nháp chưa gửi duyệt` | `InvoiceSubmit` | `DemHoaDonNhapAsync` | CanLam | Cả nhóm |
| HD2 | HoaDon | `Hóa đơn chờ Admin chốt` | `InvoiceFinalize` (chỉ Admin) | `DemHoaDonChoDuyetAsync` | CanLam | Cả nhóm |
| HD3 | HoaDon | `Hóa đơn đã chốt, chưa gửi khách` | `InvoiceSend` | `DemHoaDonDaChotChuaGuiAsync` | CanLam | Cả nhóm |
| TT1 | ThuTien | `Minh chứng chuyển khoản chờ đối chiếu` | `PaymentReview` | `DemMinhChungChoDoiChieuAsync` | Khan | Cả nhóm |
| TT2 | ThuTien | `Hóa đơn quá hạn` | `InvoiceRead` | `DemHoaDonQuaHanAsync(nowUtc)` | Khan | Cả nhóm |
| TT3 | ThuTien | `Hóa đơn đã gửi, chưa thu đủ` | `InvoiceRead` | `DemHoaDonDaGuiChuaToiHanAsync(nowUtc)` | TheoDoi | Cả nhóm |
| KH1 | KhachThueHopDong | `Sự cố chờ tiếp nhận` | không có | `DemSuCoChoTiepNhanAsync(mocGapUtc = nowUtc.AddHours(-SuCoChoTiepNhanGapSauGio))` | Khan nếu tổng `SoLuongGap > 0`, ngược lại CanLam | Cả nhóm |
| KH2 | KhachThueHopDong | `Sự cố xử lý quá lâu` | không có | `DemSuCoXuLyQuaLauAsync(mocUtc = nowUtc.AddDays(-SuCoXuLyQuaLauSauNgay))` | CanLam | Cả nhóm |
| KH3 | KhachThueHopDong | `Hợp đồng sắp hết hạn` | không có | `DemHopDongSapHetHanAsync(tuUtc = homNayVnBatDauUtc, denTruocUtc = homNayVnBatDauUtc.AddDays(HopDongSapHetHanTrongNgay + 1), gapTruocUtc = homNayVnBatDauUtc.AddDays(HopDongSapHetHanGapTrongNgay + 1))` | CanLam nếu tổng `SoLuongGap > 0` (còn ≤ 7 ngày theo ngày VN), ngược lại TheoDoi | Cả nhóm |

**CS1 (giữ logic hiện tại ở `DashboardService.cs` dòng 107–182, chuyển sang service mới):**
- Kỳ mục tiêu: theo `nowVn`; nếu `nowVn.Day < _dashboardSettings.ChotDienNuocDay` thì lùi 2 tháng, ngược lại lùi 1 tháng (qua năm đúng). Viết thành hàm `internal static (int Thang, int Nam) TinhKyMucTieu(DateTime nowVn, int chotDienNuocDay)` (cho phép test nếu cần; `InternalsVisibleTo` chỉ thêm nếu project đã có, không thì test qua kết quả `GetTongHopAsync`).
- Quét `SoKyQuetChiSo` kỳ tính đến kỳ mục tiêu: từ `kyMucTieu − (SoKyQuetChiSo − 1)` tháng tới kỳ mục tiêu.
- Gọi `GetHopDongDangHoatDongAsync(branchId, allowed)` và `GetKyChiSoDaGhiNhanAsync(branchId, allowed, tuThang, tuNam)` (kỳ đầu quét).
- Mỗi kỳ (y, m): phòng hợp lệ = hợp đồng có tháng của `ThoiDiemBatDau.AddHours(7)` ≤ kỳ đó; phòng thiếu = `PhongTroId` hợp lệ chưa có trong kỳ đã ghi của (m, y). Đếm **số `PhongTroId` khác nhau** theo từng chi nhánh. Kỳ có 0 phòng thiếu thì không tạo dòng.
- Ưu tiên: kỳ mục tiêu → CanLam; kỳ cũ hơn → Khan.

**Gộp dòng:** "Cả nhóm" = cộng mọi row của các chi nhánh thành một `ViecCanLamDto` (`SoLuong` = Σ, `TongConNo` = Σ, `NamCuNhat` = min, `SoLuongGap` = Σ). "Mỗi kỳ" = một dto cho mỗi (Nam, Thang), gán `Thang`, `Nam`. Dòng có `SoLuong == 0` không xuất hiện.

**Mô tả (`MoTa`):**
- Phần chi nhánh: sắp row theo `SoLuong` giảm dần, cùng số thì `TenChiNhanh` (ordinal) tăng; lấy tối đa 3: `"{TenChiNhanh}: {SoLuong}"` nối bằng `" · "`; còn N chi nhánh nữa thì nối thêm `" · và {N} chi nhánh khác"`.
- TT2, TT3: thêm `" · còn nợ {DinhDangTien(TongConNo)}"`.
- KH1 khi `SoLuongGap > 0`: thêm `" · {SoLuongGap} sự cố quá {SuCoChoTiepNhanGapSauGio} giờ"`.
- KH3 khi `SoLuongGap > 0`: thêm `" · {SoLuongGap} hợp đồng còn tối đa {HopDongSapHetHanGapTrongNgay} ngày"`.
- `DinhDangTien(decimal v)` (private static): `string.Format(CultureInfo.InvariantCulture, "{0:#,##0}", v).Replace(",", ".") + " đ"` → `3.590.000 đ`. Không dùng culture `vi-VN` (phụ thuộc ICU).

**Link (`Link`)**: `chiNhanhIdLink = branchId ?? (số ChiNhanhId khác nhau của các row tạo nên dòng == 1 ? id đó : null)`. Có `chiNhanhIdLink` thì nối `&chiNhanhId={id}` (hoặc `?chiNhanhId={id}` nếu đường dẫn chưa có query), luôn đặt **trước** `#`.

| Mã | Link (không có chi nhánh) |
|---|---|
| CS1, CS2 | `/QuanLyNhaTro/ChotDienNuoc?thang={m}&nam={y}` |
| HD1 / HD2 / HD3 | `/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam={NamCuNhat}&trangThaiPhatHanh=0` / `=1` / `=2` |
| TT1 | `/QuanLyNhaTro/DoiChieuThanhToan` |
| TT2 | `/QuanLyNhaTro/ViecCanLam#hoa-don-qua-han` (có chi nhánh: `/QuanLyNhaTro/ViecCanLam?chiNhanhId=5#hoa-don-qua-han`) |
| TT3 | `/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam={NamCuNhat}&trangThaiPhatHanh=3` |
| KH1 / KH2 | `/QuanLyNhaTro/YeuCauSuCo?trangThai=0&soThang=0` / `trangThai=1&soThang=0` |
| KH3 | `/QuanLyNhaTro/QuanLyHopDong` |

`NamCuNhat` của dòng = min `NamCuNhat` các row.

**Sắp xếp `Items`:** `MucUuTien` tăng (Khan → CanLam → TheoDoi), rồi `SoLuong` giảm, rồi `MaViec` (ordinal) tăng, rồi `(Nam, Thang)` tăng (xem câu hỏi 2).

**Tổng:** `TongSo` = Σ `SoLuong` mọi dòng; `SoKhan`/`SoCanLam`/`SoTheoDoi` = Σ `SoLuong` các dòng mức tương ứng.

**`GetHoaDonQuaHanAsync`:** sau bước phân quyền (TT2 dùng `InvoiceRead`, không chỉ-Admin), gọi `GetHoaDonQuaHanAsync(branchId, allowed, nowUtc, _settings.SoDongHoaDonQuaHan)` và `DemHoaDonQuaHanAsync(branchId, allowed, nowUtc)` (`TongSo` = Σ `SoLuong`). Map: `SoConNo = TongTien − DaThu`; `HanThanhToanUtc = HanThanhToan`; `SoNgayQuaHan = (nowVn.Date − HanThanhToan.AddHours(7).Date).Days`. Giữ thứ tự store trả về.

### 2.6 Sửa `src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard/DTOs/DashboardDTOs.cs`
- `DashboardDataDto`: xóa `ToDos`, `SoHopDongSapHetHan`, `SoPhongChuaChotDienNuoc`; thêm `decimal TienQuaHan`, `int SoHoaDonQuaHan`.
- Xóa class `DashboardToDoDto`, `DashboardContractUtilityCheckDto`, `DashboardRecordedUtilityDto`, `DashboardUnpaidGroupDto`.
- `DashboardInvoiceStatDto` đổi thành `{ int Thang; decimal DaThu; decimal ChoThu; }` (bỏ `TrangThai`, `TongTien`).
- Thêm `public class DashboardOverdueSummaryDto { public int SoHoaDon { get; set; } public decimal TongConNo { get; set; } }`.

### 2.7 Sửa `src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard/Persistence/IDashboardStore.cs`
- Xóa `GetActiveContractsForUtilityCheckAsync`, `GetRecordedUtilitiesAsync`, `CountExpiringContractsAsync`, `GetUnpaidInvoicesGroupedAsync`.
- Thêm `Task<DashboardOverdueSummaryDto> GetOverdueSummaryAsync(int? branchId, DateTime nowUtc, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);`
- Comment cho `GetInvoiceMonthlyStatsAsync` (DaThu mọi hóa đơn; ChoThu chỉ `DaGui`) và `CountUnpaidRoomsAsync` (chỉ `DaGui`).

### 2.8 Sửa `src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard/Services/DashboardService.cs`
- Constructor: `(IDashboardStore store, IEmployeeAccessService access, TimeProvider timeProvider)`; bỏ `DashboardSettings`.
- `today = _timeProvider.GetUtcNow().UtcDateTime.AddHours(7)` thay cho `DateTime.UtcNow.AddHours(7)`.
- `DoanhThuThangNay = stats.Where(Thang == selectedMonth).Sum(DaThu)`; `TongTienChoThu = ...Sum(ChoThu)`; biểu đồ: `ChartData` = DaThu tháng, `ChartDataChoThu` = ChoThu tháng.
- Thêm `var quaHan = await _store.GetOverdueSummaryAsync(branchId, nowUtc, allowedBranchIds: allowed); model.TienQuaHan = quaHan.TongConNo; model.SoHoaDonQuaHan = quaHan.SoHoaDon;`
- Xóa toàn bộ mục "7. DANH SÁCH VIỆC CẦN LÀM" (dòng 106–213). Giữ nguyên các mục khác (phạm vi, phòng, hợp đồng, tình trạng phòng, hoạt động gần đây).

### 2.9 Sửa `src/QuanLyChoThuePhongTroWeb.Application/DependencyInjection.cs`
Thêm `services.AddScoped<IViecCanLamService, ViecCanLamService>();` ngay dưới dòng `IDashboardService` (thêm `using ...Features.ViecCanLam.Services;`).

## 3. Infrastructure

### 3.1 Tạo `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/InvoiceDebtQueries.cs`
`internal static class InvoiceDebtQueries` — một định nghĩa dùng chung cho Dashboard và ViecCanLam:
```csharp
// Hóa đơn đã gửi khách, chưa xóa, chưa thanh toán đủ (khớp màn Công nợ).
public static IQueryable<HoaDon> DaGuiChuaThuDu(this IQueryable<HoaDon> q) =>
    q.Where(h => !h.IsDeleted && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui && h.TrangThaiHoaDon != TrangThaiHoaDon.DaThanhToan);
public static IQueryable<HoaDon> QuaHan(this IQueryable<HoaDon> q, DateTime nowUtc) =>
    q.DaGuiChuaThuDu().Where(h => h.HanThanhToan != null && h.HanThanhToan < nowUtc);
public static IQueryable<HoaDon> ChuaToiHan(this IQueryable<HoaDon> q, DateTime nowUtc) =>
    q.DaGuiChuaThuDu().Where(h => h.HanThanhToan == null || h.HanThanhToan >= nowUtc);
```

### 3.2 Tạo `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/ViecCanLamStore.cs`
`public class ViecCanLamStore : IViecCanLamStore`, constructor nhận `ApplicationDbContext`. Copy quy ước lọc chi nhánh từ `DashboardStore.cs` (`if (branchId.HasValue) ...; if (allowedBranchIds != null) ... Contains(...)`). Mọi truy vấn `AsNoTracking()`, async, truyền `ct`. Chi nhánh lấy qua `PhongTro.ChiNhanhId` / `PhongTro.ChiNhanh.TenChiNhanh`.

| Method | Bảng gốc | Điều kiện | Chi nhánh qua | Cách gom |
|---|---|---|---|---|
| `GetHopDongDangHoatDongAsync` | `HopDongs` | `!IsDeleted && TrangThaiHopDong == DangHoatDong` | `h.PhongTro` | Project thẳng (như `DashboardStore.GetActiveContractsForUtilityCheckAsync`) |
| `GetKyChiSoDaGhiNhanAsync` | `DichVuDienNuocCuaPhongs` | `!IsDeleted && (Nam > tuNam \|\| (Nam == tuNam && Thang >= tuThang))` | `d.PhongTro` (**sửa H5: có lọc chi nhánh**) | Project `PhongTroId, Thang, Nam` |
| `DemAnhChiSoChoXacNhanAsync` | `AnhChiSoDongHos` | `!a.IsDeleted && TrangThaiXuLy ∈ {DocDuoc, KhongDocDuoc, Loi} && !a.DichVuDienNuocCuaPhong.IsDeleted` | `a.DichVuDienNuocCuaPhong.PhongTro` | GroupBy (ChiNhanhId, TenChiNhanh, Thang, Nam) → Count |
| `DemHoaDonNhapAsync` | `HoaDons` | `!IsDeleted && TrangThaiPhatHanh == Nhap` | `h.HopDong.PhongTro` | GroupBy (ChiNhanhId, TenChiNhanh) → Count, Min(Nam) |
| `DemHoaDonChoDuyetAsync` | `HoaDons` | `!IsDeleted && ChoDuyet` | như trên | như trên |
| `DemHoaDonDaChotChuaGuiAsync` | `HoaDons` | `!IsDeleted && DaChot && TrangThaiHoaDon != DaThanhToan` | như trên | như trên |
| `DemMinhChungChoDoiChieuAsync` | `MinhChungThanhToanHoaDons` | `!m.IsDeleted && TrangThaiDoiChieu == ChoXacNhan && !m.YeuCauThanhToanHoaDon.IsDeleted && !m.YeuCauThanhToanHoaDon.HoaDon.IsDeleted` | `m.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro` | GroupBy → Count |
| `DemHoaDonQuaHanAsync` | `HoaDons.QuaHan(nowUtc)` | — | `h.HopDong.PhongTro` | **Project từng hóa đơn** `{ChiNhanhId, TenChiNhanh, Nam, TongTien, DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan)}` rồi gom **trong bộ nhớ** (Count, Min(Nam), Σ(TongTien − DaThu)). Lý do: tránh GroupBy trên cột tính từ subquery ledger, giống comment ở `DashboardStore.GetInvoiceMonthlyStatsAsync` |
| `DemHoaDonDaGuiChuaToiHanAsync` | `HoaDons.ChuaToiHan(nowUtc)` | — | như trên | như trên |
| `DemSuCoChoTiepNhanAsync` | `YeuCauSuCos` | `!IsDeleted && TrangThai == ChoTiepNhan` | `s.PhongTro` | GroupBy → Count, `SoLuongGap = g.Count(x => x.NgayGui <= mocGapUtc)` |
| `DemSuCoXuLyQuaLauAsync` | `YeuCauSuCos` | `!IsDeleted && TrangThai == DangXuLy && NgayGui <= mocUtc` | `s.PhongTro` | GroupBy → Count |
| `DemHopDongSapHetHanAsync` | `HopDongs` | `!IsDeleted && DangHoatDong && ThoiDiemKetThuc != null && ThoiDiemKetThuc >= tuUtc && ThoiDiemKetThuc < denTruocUtc` | `h.PhongTro` | GroupBy → Count, `SoLuongGap = g.Count(x => x.ThoiDiemKetThuc < gapTruocUtc)` |
| `GetHoaDonQuaHanAsync` | `HoaDons.QuaHan(nowUtc)` | — | `h.HopDong.PhongTro` | OrderBy `HanThanhToan`, ThenBy `HoaDonId`, `Take(gioiHan)`, project `HoaDonQuaHanRow` (`KhachThue = h.HopDong.NguoiThue.HoVaTen`, `DaThu` như trên, `HanThanhToan = h.HanThanhToan!.Value`) |

Nếu EF không dịch được `g.Count(predicate)` thì dùng `g.Sum(x => cond ? 1 : 0)`; không chuyển sang tải toàn bộ bản ghi.

### 3.3 Sửa `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/DashboardStore.cs`
- Xóa 4 method đã bỏ khỏi interface.
- `GetInvoiceMonthlyStatsAsync`: project thêm `TrangThaiPhatHanh`; gom trong bộ nhớ theo `Thang`: `DaThu = Σ DaThu` (mọi hóa đơn chưa xóa của năm, như cũ), `ChoThu = Σ(TongTien − DaThu)` chỉ của hóa đơn `DaGui && TrangThaiHoaDon != DaThanhToan`.
- `CountUnpaidRoomsAsync`: dùng `_context.HoaDons.DaGuiChuaThuDu()` thay điều kiện cũ.
- Thêm `GetOverdueSummaryAsync`: `_context.HoaDons.QuaHan(nowUtc)` + lọc chi nhánh, project từng hóa đơn `{TongTien, DaThu}` (ledger chưa xóa), tính `SoHoaDon` và `TongConNo` trong bộ nhớ.

### 3.4 Sửa `src/QuanLyChoThuePhongTroWeb.Infrastructure/DependencyInjection.cs`
- Đăng ký store ngay dưới dòng `IDashboardStore`: `services.AddScoped<...Features.ViecCanLam.Persistence.IViecCanLamStore, Persistence.Features.ViecCanLamStore>();`
- Bind options ngay dưới dòng `DashboardSettings` (copy y hệt): `services.AddSingleton(configuration.GetSection("ViecCanLamSettings").Get<...ViecCanLamSettings>() ?? new ...ViecCanLamSettings());`. Section là tùy chọn; không thêm vào appsettings.

## 4. Web

### 4.1 Sửa `src/QuanLyChoThuePhongTroWeb.Web/GlobalUsings.cs`
Thêm `global using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Services;` và `...Features.ViecCanLam.DTOs;`.

### 4.2 Tạo `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/ViecCanLamController.cs`
Namespace `QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers`, `public class ViecCanLamController : AdminBaseController` (kế thừa `[Authorize(Roles = "Admin,NhanVien")]`, nên Khách thuê bị chuyển về `/QuanLyNhaTro/DangNhap`). Chỉ một action trong đợt A:
```csharp
[HttpGet("/QuanLyNhaTro/ViecCanLam/TomTat")]
public async Task<IActionResult> TomTat(CancellationToken cancellationToken)
{
    var t = await _service.GetTongHopAsync(CurrentActorId, null, cancellationToken);
    return Json(new { tongSo = t.TongSo, soKhan = t.SoKhan, soCanLam = t.SoCanLam, soTheoDoi = t.SoTheoDoi });
}
```
Không tạo action trang HTML (đợt B).

### 4.3 Sửa `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/DashboardController.cs`
- Inject thêm `IViecCanLamService`.
- `Index(int? branchId, int? year, int? month, CancellationToken cancellationToken)`: gọi `GetDashboardDataAsync` như cũ và `GetTongHopAsync(CurrentActorId, branchId, cancellationToken)`; `DashboardViewModel.FromDto(dto, viec)`.
- `GetAjaxData(int? branchId, int year, int month, CancellationToken cancellationToken)`: gọi cả hai service. JSON giữ các khóa cũ, **bỏ `toDos`**, thêm:
  - `tienQuaHan = model.TienQuaHan`, `soHoaDonQuaHan = model.SoHoaDonQuaHan`
  - `viecCanLam = new { tongSo, soKhan, soCanLam, soTheoDoi, items = viec.Items.Take(5).Select(i => new { maViec = i.MaViec, nhom = i.Nhom.ToString(), mucUuTien = i.MucUuTien.ToString(), tieuDe = i.TieuDe, moTa = i.MoTa, soLuong = i.SoLuong, link = i.Link }) }`
  - `soHopDongSapHetHan`, `soPhongChuaChotDienNuoc`: theo câu hỏi 3 (mặc định: `viec.Items.Where(i => i.MaViec == "CS1").Sum(i => i.SoLuong)` và tương tự `"KH3"`).

### 4.4 Sửa `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/ViewModels/Responses/Dashboard.cs`
- `FromDto(DashboardDataDto dto, ViecCanLamTongHopDto viecCanLam)`.
- Xóa property `SoHopDongSapHetHan`, `SoPhongChuaChotDienNuoc` (view không dùng).
- **Phương án tạm cho view hiện tại (tới đợt B):** giữ `ToDos` và `ToDoItemViewModel`; ánh xạ tối thiểu `ToDos = viecCanLam.Items.Take(5).Select(i => new ToDoItemViewModel { Type = i.MucUuTien switch { Khan => "danger", CanLam => "warning", _ => "info" }, Title = i.TieuDe, Description = i.MoTa, Link = i.Link, Icon = "ti ti-checklist" })`. Razor tự encode nên an toàn; trang render đúng việc theo chi nhánh đang chọn lúc tải trang.

## 5. GIAO DIỆN (chỉ sửa tối thiểu để không lỗi runtime)

- Mockup `docs/features/viec-can-lam/mockup/` (Dashboard phương án B, trang phương án A) **không áp dụng ở đợt A**.
- Coder phải đọc `.agents/rules/frontend_code_review.md` trước khi sửa view.
- File duy nhất: `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/Dashboard/Index.cshtml`. Trong `reloadDashboard()` → `success`, **xóa nguyên khối "// 4. Cập nhật danh sách Việc cần làm"** (từ dòng comment đó tới hết `$('#toDoContainer').html(todoHtml);`, hiện là dòng 315–344). Lý do: JSON không còn `toDos`, giữ lại sẽ ném `TypeError` khi đổi bộ lọc. Không sửa gì khác (thẻ KPI, `formatVND`, khối hoạt động gần đây giữ nguyên cho đợt B).
- Hệ quả chấp nhận tạm thời: đổi bộ lọc trên Dashboard thì khối việc giữ danh sách lúc tải trang; tải lại trang mới cập nhật. Đợt B sẽ dựng lại khối này.

## 6. Trường hợp biên bắt buộc

- **Fail closed:** scope null, nhân viên chưa phân công, `branchId` ngoài phạm vi → kết quả rỗng, không lỗi, không gọi store.
- **Chỉ-Admin:** nhân viên không nhận HD2 trong cả JSON lẫn HTML; store HD2 không được gọi.
- **Xóa mềm** (không có global query filter, phải tự lọc): hóa đơn, `LichSuThanhToan` (khi tính đã thu), ảnh chỉ số và kỳ chỉ số, minh chứng + yêu cầu thanh toán + hóa đơn, sự cố, hợp đồng. Hóa đơn `DaHuy` luôn có `IsDeleted = true` nên tự bị loại.
- **UTC:** so hạn bằng `nowUtc` từ `TimeProvider`; `HanThanhToan == nowUtc` thuộc TT3, không thuộc TT2. Kỳ tháng, ngày hạn, số ngày quá hạn, "hôm nay" của KH3 tính theo giờ VN (UTC+7). Không dùng `DateTime.Now`.
- Hóa đơn cũ `DaChot` chưa thu đủ hiện ở HD3 (đúng ý spec).
- Mỗi lời gọi service gọi `GetScopeAsync` một lần; `GetAjaxData` gọi hai service nên hai lần (chấp nhận).
- Không cache, không thêm index, truy vấn tuần tự (DbContext không an toàn đa luồng).

## 7. Ranh giới tầng (test kiến trúc chặn)

- Application không dùng EF Core, ASP.NET Core, `IConfiguration`, `IQueryable` trong contract. `InvoiceDebtQueries` (dùng `IQueryable`) nằm ở **Infrastructure**, `internal`.
- Interface service không trả Domain entity (`ArchitectureBoundaryTests`).
- Web không có chuỗi `QuanLyChoThuePhongTroWeb.Domain` trong `.cs`/`.cshtml`; chỉ dùng enum `NhomViec`, `MucUuTienViec` của Application.

## 8. Test

Viết test trước (TDD). Copy quy ước:
- Unit test service + fake: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/YeuCauSuCoBranchScopeTests.cs`, `Fakes/FakeEmployeeAccessService.cs`, `Fakes/FixedTimeProvider.cs`.
- Store test: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/AiAssistantStoreScopeTests.cs` (`[Collection("PostgreSqlCollection")]`, `_fixture.CreateDbContext()`, `BeginTransactionAsync` … `RollbackAsync`, tên có hậu tố Guid). DB dùng chung với test khác: **luôn lọc theo `branchId` hoặc `allowedBranchIds` của chi nhánh vừa seed**, không assert tổng toàn hệ thống. Dùng ngày cố định (ví dụ năm 2003) và truyền `nowUtc` tường minh.
- Web test: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/CoreBranchScopeTests.cs`, `BranchScopeKit.cs`, `AiAssistantEndpointTests.cs` (tạo tài khoản khách thuê, kiểm tra redirect).

### 8.1 Application.UnitTests
- Tạo `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/Fakes/FakeViecCanLamStore.cs`: ghi lại mọi lời gọi (tên method, `branchId`, `allowedBranchIds`, tham số mốc thời gian, `gioiHan`). Với KH1, KH3 và CS1 giữ **dữ liệu thô** (sự cố: chi nhánh + `NgayGui`; hợp đồng hết hạn: chi nhánh + `ThoiDiemKetThuc`; hợp đồng đang hoạt động + kỳ đã ghi) và áp **đúng vị từ đã ghi trong comment interface** với mốc được truyền vào; các method còn lại trả danh sách row cấu hình sẵn.
- Tạo `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ViecCanLamServiceTests.cs`, mỗi ý một `[Fact]`/`[Theory]`:
  1. Scope null → `TongSo == 0`, `Items` rỗng, store không bị gọi (cả hai method service).
  2. Nhân viên chưa phân công → rỗng, store không bị gọi.
  3. Nhân viên chọn chi nhánh ngoài phạm vi → rỗng, store không bị gọi.
  4. Nhân viên: không có `HD2`, `DemHoaDonChoDuyetAsync` không được gọi; Admin: có `HD2`.
  5. Nhân viên truyền `allowedBranchIds = [A]`, Admin truyền `null`; `branchId` được chuyển nguyên xuống store.
  6. KH1: sự cố gửi 23 giờ 59 phút trước → `CanLam`; gửi đúng 24 giờ trước → `Khan` (theo mặc định câu hỏi 1), mô tả chứa `"1 sự cố quá 24 giờ"`.
  7. KH3: hợp đồng kết thúc ngày VN hôm nay + 7 → `CanLam`; + 8 → `TheoDoi`; + 31 → không được đếm.
  8. CS1: kỳ mục tiêu → `CanLam`; kỳ cũ hơn → `Khan`; tiêu đề `"Phòng chưa chốt điện nước T{m}/{y}"`; kỳ không thiếu phòng thì không có dòng; hợp đồng bắt đầu sau kỳ thì không tính kỳ đó.
  9. Kỳ mục tiêu CS1 với `ChotDienNuocDay = 5`: VN 04/01/2027 → T11/2026; VN 05/01/2027 → T12/2026; `nowUtc = 2027-01-04T17:00Z` (= 00:00 VN 05/01) → T12/2026; kiểm tra thêm kỳ đầu quét (`tuThang`, `tuNam`) truyền xuống store đúng 6 kỳ.
  10. Sắp xếp theo §5.2 (khóa ở mục 2.5).
  11. Mô tả: 4 chi nhánh → 3 chi nhánh số lớn trước + `"và 1 chi nhánh khác"`; TT2 có `"còn nợ 3.590.000 đ"`.
  12. Link: lọc `branchId` → có `chiNhanhId`; dòng một chi nhánh → có; dòng nhiều chi nhánh không lọc → không có; TT2 có chi nhánh → `/QuanLyNhaTro/ViecCanLam?chiNhanhId={id}#hoa-don-qua-han`; HD1 dùng `NamCuNhat` nhỏ nhất.
  13. `TongSo`, `SoKhan`, `SoCanLam`, `SoTheoDoi` bằng tổng `SoLuong` theo mức.
  14. `GetHoaDonQuaHanAsync`: truyền `gioiHan = 50` (mặc định settings) xuống store; `TongSo` lấy từ `DemHoaDonQuaHanAsync`; `SoConNo = TongTien − DaThu`; `SoNgayQuaHan` theo ngày VN.
  15. Store trả rỗng → `Items` rỗng (việc số lượng 0 không hiện).
- Sửa `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/BranchScopeArchitectureTests.cs`: thêm `typeof(IViecCanLamService)` vào `ScopedServices` (và `using`).

### 8.2 Infrastructure.IntegrationTests
- Tạo `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/ViecCanLamStoreTests.cs`. Seed hai chi nhánh A, B; kiểm tra:
  - Mỗi method: lọc `branchId = A` và `allowedBranchIds = [B]` đều không lẫn dữ liệu chi nhánh kia; `allowedBranchIds = []` → rỗng.
  - Bản ghi xóa mềm bị loại ở từng method (hóa đơn, ảnh, kỳ chỉ số của ảnh, minh chứng, yêu cầu thanh toán, hóa đơn của minh chứng, sự cố, hợp đồng).
  - TT2/TT3 chỉ `DaGui`: hóa đơn `Nhap`/`ChoDuyet`/`DaChot` chưa thu không vào.
  - HD3: `DaChot` đã `DaThanhToan` không vào.
  - CS2: ảnh `MoiTaiLen`, `DangXuLy`, `CanChupLai`, `DaXacNhan`, `DaThayThe` không vào; `DocDuoc`, `KhongDocDuoc`, `Loi` vào, gom đúng kỳ.
  - TT1: minh chứng `DaXacNhan`, `TuChoi` không vào.
  - Biên `HanThanhToan == nowUtc` → TT3, không TT2 (dùng giá trị UTC cắt tới micro giây).
  - `TongConNo` và `DaThu` của bảng quá hạn bỏ `LichSuThanhToan` đã xóa; bảng sắp hạn sớm nhất trước và tôn trọng `gioiHan`.
  - `GetKyChiSoDaGhiNhanAsync` lọc theo chi nhánh (H5) và bỏ bản ghi xóa mềm.
  - KH1 `SoLuongGap`, KH3 `SoLuongGap` và cửa sổ `[tuUtc, denTruocUtc)` đúng.
- Tạo `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/DashboardStoreTests.cs`:
  - `GetInvoiceMonthlyStatsAsync`: `ChoThu` của tháng chỉ gồm hóa đơn `DaGui` chưa thu đủ; `DaThu` vẫn cộng ledger chưa xóa của mọi hóa đơn chưa xóa trong tháng; ledger xóa mềm bị loại.
  - `CountUnpaidRoomsAsync`: phòng chỉ có hóa đơn `Nhap`/`ChoDuyet`/`DaChot` không được đếm.
  - `GetOverdueSummaryAsync`: đếm và cộng còn nợ đúng, lọc chi nhánh, biên `HanThanhToan == nowUtc` không tính.

### 8.3 Web.IntegrationTests
- Tạo `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/ViecCanLamEndpointTests.cs` (`[Collection(WebTestCollection.Name)]`, `BranchScopeKit.SeedAsync`; seed thêm một hóa đơn `ChoDuyet` ở A và một ở B; tên chi nhánh A đọc từ DB):
  1. Nhân viên A: `GET /QuanLyNhaTro/Dashboard/GetAjaxData?year=..&month=..` có `tienQuaHan`, `soHoaDonQuaHan`, `viecCanLam`, **không có** `toDos`; mọi `items[].moTa` không chứa tên chi nhánh B; không có item `maViec == "HD2"`.
  2. Nhân viên A với `branchId = B` → `viecCanLam.tongSo == 0`, `items` rỗng.
  3. Admin `GetAjaxData?branchId=A` có item `HD2`; `branchId=B` có `moTa` chứa tên chi nhánh B.
  4. Nhân viên A: `GET /QuanLyNhaTro/ViecCanLam/TomTat` trả 200, `tongSo` = `viecCanLam.tongSo` của `GetAjaxData` (không lọc chi nhánh) và = `tongSo` của Admin với `branchId=A` trừ `soLuong` của item `HD2`.
  5. Nhân viên A: HTML `GET /QuanLyNhaTro/Dashboard` không chứa `"Hóa đơn chờ Admin chốt"`.
  6. Khách thuê gọi `/QuanLyNhaTro/ViecCanLam/TomTat` → 302 về `/QuanLyNhaTro/DangNhap`.
- Các test Dashboard hiện có phải vẫn PASS: `CoreBranchScopeTests.Dashboard_StaffOfBranchA_SeesOnlyBranchA`, `AdminScreenAuthorizationTests.StaffDashboardHtml_DoesNotExpose_AdminOnlyLinks`.

### 8.4 Lệnh chạy (PowerShell, từ gốc repo)

```powershell
git status --short
dotnet build QuanLyChoThuePhongTroWeb.sln
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~ViecCanLamServiceTests|FullyQualifiedName~BranchScopeArchitectureTests|FullyQualifiedName~ArchitectureBoundaryTests"

# Cần DB riêng tên kết thúc _test hoặc _integration_test; thiếu biến thì ghi CHƯA CHẠY, không ghi PASS
$env:QLCTPT_TEST_CONNECTION_STRING = "Host=...;Database=qlctpt_test;..."
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests --filter "FullyQualifiedName~ViecCanLamStoreTests|FullyQualifiedName~DashboardStoreTests"
dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests --filter "FullyQualifiedName~ViecCanLamEndpointTests|FullyQualifiedName~CoreBranchScopeTests|FullyQualifiedName~AdminScreenAuthorizationTests|FullyQualifiedName~ArchitectureBoundaryTests"
dotnet test QuanLyChoThuePhongTroWeb.sln

# Xác nhận không lệch schema (cần QLCTPT_DESIGNTIME_CONNECTION_STRING); phải báo không có thay đổi, không tạo migration
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj

git diff --check
```

## 9. Schema PostgreSQL

**KHÔNG ĐỔI SCHEMA.** Không sửa entity, `Configurations/`, `ApplicationDbContext`, `Migrations/`; không thêm index. `has-pending-model-changes` phải báo không có thay đổi.

## 10. Danh sách file

Tạo mới:
- `src/QuanLyChoThuePhongTroWeb.Application/Common/Configurations/ViecCanLamSettings.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/DTOs/ViecCanLamDTOs.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/DTOs/ViecCanLamStoreRows.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/Persistence/IViecCanLamStore.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/Services/IViecCanLamService.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/Features/ViecCanLam/Services/ViecCanLamService.cs`
- `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/InvoiceDebtQueries.cs`
- `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/ViecCanLamStore.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/ViecCanLamController.cs`
- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/Fakes/FakeViecCanLamStore.cs`
- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ViecCanLamServiceTests.cs`
- `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/ViecCanLamStoreTests.cs`
- `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/DashboardStoreTests.cs`
- `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/ViecCanLamEndpointTests.cs`

Sửa:
- `src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard/DTOs/DashboardDTOs.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard/Persistence/IDashboardStore.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/Features/Dashboard/Services/DashboardService.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/DependencyInjection.cs`
- `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/DashboardStore.cs`
- `src/QuanLyChoThuePhongTroWeb.Infrastructure/DependencyInjection.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/GlobalUsings.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/DashboardController.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/ViewModels/Responses/Dashboard.cs`
- `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/Dashboard/Index.cshtml` (chỉ xóa khối JS mục 5)
- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/BranchScopeArchitectureTests.cs`

Không sửa: mọi file trong `docs/`, `CLAUDE.md`, `AGENTS.md`, `MenuService.cs`, `_Sidebar`, `Views/HoaDon/Index.cshtml`. Không commit/push.
