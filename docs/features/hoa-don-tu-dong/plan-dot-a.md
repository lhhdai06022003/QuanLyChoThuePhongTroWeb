# Kế hoạch: Hóa đơn tự động, ĐỢT A

Nguồn: `docs/features/hoa-don-tu-dong/spec.md` v7, các mục 3, 4, 5.1, 5.2, 7.1 (phần dữ liệu), 7.2 (tạm tính), 8, 9, 10.
Nhánh: `feature/hoa-don-tu-dong`. Không commit, không push.

> Bản lưu của `.bangiao/ke-hoach.md` do planner `/ship` viết ngày 10/10/2026, đã qua cổng xác nhận của người dùng (dòng "ĐÃ XÁC NHẬN" ở cuối mục CẦN NGƯỜI DÙNG XÁC NHẬN). Giữ nguyên nội dung đã dùng để triển khai, trừ một chỗ sửa sau review: link thông báo nhân viên ở mục 2.8 (BaoDungAsync) ban đầu là `/QuanLyNhaTro/DienNuoc` (trang 404), đã đổi thành `/QuanLyNhaTro/ChotDienNuoc?thang={Thang}&nam={Nam}`. Kết quả triển khai và review: [review.md](review.md).

---

## CẦN NGƯỜI DÙNG XÁC NHẬN

Spec v7 ghi người dùng đã duyệt nghiệp vụ và schema. Theo quy trình, coder chỉ bắt đầu khi người dùng xác nhận lại từng điểm dưới đây ngay trên bản kế hoạch này.

### S. Schema (một migration mới `AddAutoInvoiceReviewWindow`)

| # | File | Trước | Sau |
|---|---|---|---|
| S1 | `src/QuanLyChoThuePhongTroWeb.Domain/Entities/ChiNhanh.cs` | Không có cột số giờ | Thêm `public int SoGioPhanHoiHoaDon { get; set; } = 24;` |
| S2 | `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Configurations/ChiNhanhConfiguration.cs` | Chỉ có unique index `MaChiNhanh` | Thêm `HasDefaultValue(24)` cho `SoGioPhanHoiHoaDon` và CHECK `CK_ChiNhanh_SoGioPhanHoiHoaDon`: `"SoGioPhanHoiHoaDon" BETWEEN 1 AND 168` |
| S3 | `src/QuanLyChoThuePhongTroWeb.Domain/Entities/HoaDon.cs` | Không có hạn tự chốt | Thêm `public DateTime? HanPhanHoi { get; set; }` (UTC) |
| S4 | `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Configurations/HoaDonConfiguration.cs` | Có unique index (`HopDongId`,`Thang`,`Nam`) | Thêm index `IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi` trên (`TrangThaiPhatHanh`,`HanPhanHoi`) lọc `"HanPhanHoi" IS NOT NULL AND "IsDeleted" = false` |
| S5 | `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations/<timestamp>_AddAutoInvoiceReviewWindow.cs` (+ `.Designer.cs`, `ApplicationDbContextModelSnapshot.cs`) | Chưa có | `chi_nhanh."SoGioPhanHoiHoaDon" integer NOT NULL DEFAULT 24` + CHECK; `hoa_don."HanPhanHoi" timestamp with time zone NULL`; index lọc. `Down` gỡ index, CHECK, hai cột. Không sửa migration cũ, không đổi dữ liệu cũ (chi nhánh cũ nhận 24 qua DEFAULT) |

`ApplicationDbContext.cs` KHÔNG đổi. Không thêm bảng.

### T. Tiền

| # | File | Trước | Sau |
|---|---|---|---|
| T1 | `Application/Features/HoaDons/Services/AutoInvoiceService.cs` (mới) | Hóa đơn chỉ tạo khi nhân viên bấm "Phát sinh" | Hệ thống tự gọi `CreateDraftsAsync` (cách tính tiền giữ nguyên, không đổi làm tròn) khi chỉ số đạt điều kiện mục 4 |
| T2 | `Application/Features/HoaDons/AutoInvoices/AutoInvoiceReadingPolicy.cs` (mới) | Không có | Ngưỡng quyết định có tự tạo hóa đơn: độ tin cậy ≥ 0,90; tiêu thụ ≤ 3 × trung bình 3 kỳ `DaDuyet` trước; trần 500 kWh / 30 m³ khi chưa có kỳ trước hoặc trung bình 0 |
| T3 | `Application/Features/HoaDons/Services/InvoiceIssuanceService.cs` | `CreateDraftsAsync` trả `HoaDonId` lấy trước `SaveChanges` (EF chưa gán khóa, thực tế = 0) | Điền `HoaDonId` sau `SaveChanges`. Không đổi tiền, không đổi trạng thái |
| T4 | `InvoiceIssuanceService.cs` + `Domain/Entities/HoaDon.cs` | Chỉ có `SubmitAsync` tay (`Nhap → ChoDuyet`, không hạn) | Thêm `SubmitProvisionalAsync` + `HoaDon.GuiTamTinh`: `Nhap → ChoDuyet`, đặt `HanPhanHoi = now + ChiNhanh.SoGioPhanHoiHoaDon`, lịch sử `[TU_DONG] ...` |
| T5 | `Infrastructure/Persistence/Features/HoaDonStore.cs` (`GetInvoicesByContractIdsAsync`, `CheckHoaDonOwnershipAsync`) | Khách chỉ thấy `DaGui` chưa xóa hoặc `DaHuy` đã gửi | Thêm vế `ChoDuyet && HanPhanHoi != null && !IsDeleted`: khách thấy số tiền tạm tính. `InvoicePaymentSnapshots.KhachXemDuoc` KHÔNG đổi nên vẫn không thanh toán được |
| T6 | `Application/Features/HoaDons/Services/InvoiceViewService.cs` (`ExportTenantPdfAsync`) | Khách tải PDF mọi hóa đơn xem được | Trả `null` (Web 404) khi hóa đơn `ChoDuyet` |
| T7 | `IEmailService` + `MailKitEmailService` | Chỉ có email công bố (PDF + VietQR) | Thêm email tạm tính: kỳ, tổng tiền, chi tiết, chỉ số, giờ tự chốt, SĐT chi nhánh. KHÔNG PDF, KHÔNG VietQR/số tài khoản |
| T8 | `AutoInvoiceService.cs` | Không có | Thông báo cổng cho khách có số tiền tạm tính |

ĐÃ XÁC NHẬN: S1, S2, S3, S4, S5 (schema) và T1, T2, T3, T4, T5, T6, T7, T8 (tiền) đúng như bảng trên; câu hỏi 1, 2, 3, 5, 6, 7 theo phương án đề xuất; câu hỏi 4 đổi theo người dùng: "số giảm" (`ChiSoGiam`) cũng gửi thông báo cho khách (10/10/2026 17:13, Asia/Bangkok)

---

## CÂU HỎI CÒN BỎ NGỎ

Mỗi câu có phương án đề xuất. Coder làm theo phương án đề xuất trừ khi người dùng chọn khác trước khi bắt đầu.

1. **Đồng hồ có nhiều ảnh chưa chính thức.** Spec không nói ảnh nào được xét. Đề xuất: chỉ xét ảnh MỚI NHẤT (theo `NgayGui` rồi `AnhChiSoDongHoId` giảm dần) có `TrangThaiXuLy != DaThayThe` và chưa là chính thức. Ảnh cũ hơn không xét dù đạt ngưỡng.
2. **Một đồng hồ có lý do cần báo (ví dụ OCR 0,85), đồng hồ kia chưa có ảnh.** Đề xuất: vẫn báo ngay (khách chụp lại sớm). Hệ quả: lần kích hoạt sau (tải ảnh còn lại) báo lại cùng lý do nếu ảnh cũ vẫn chưa thay.
3. **Hợp đồng trong kỳ.** Không có hợp đồng hiệu lực: đề xuất dừng im lặng (log Information). Có từ 2 hợp đồng hiệu lực trong kỳ (đổi người thuê giữa tháng): đề xuất KHÔNG tự chạy, báo nhân viên "Phòng có nhiều hợp đồng trong kỳ, lập hóa đơn theo luồng tay" (lý do này không có trong danh sách mục 4.3).
4. **Lý do nào gửi cho khách.** ~~Đề xuất: chỉ "độ tin cậy thấp" và "không đọc được"; "Số giảm" không gửi khách.~~ **NGƯỜI DÙNG QUYẾT ĐỊNH (10/10/2026): gửi khách cho ba lý do "độ tin cậy thấp", "không đọc được" (ảnh `KhongDocDuoc`, `Loi`, `CanChupLai`) VÀ "số giảm" (`ChiSoGiam`).** Nội dung cho `ChiSoGiam` xem mục 2.8 (BaoDungAsync).
5. **"Link cổng" trong email (mục 7.2).** Repo không có cấu hình địa chỉ gốc của web, nên không dựng được link tuyệt đối. Đề xuất: email ghi "Xem chi tiết tại cổng khách thuê, mục Hóa đơn" không có link. Nếu muốn link thật phải thêm một khóa cấu hình ngoài mục 10.
6. **Thử lại OCR (`RetryOcrAsync`)** không có trong bảng 5.1. Đề xuất: không kích hoạt chuỗi.
7. **Khoảng trống giữa các đợt.** Sau đợt A, trước đợt C: khách thấy hóa đơn tạm tính với nhãn hiện có "Chưa thanh toán" (`InvoiceStatusLabels.HienThi`), giao diện cũ có thể hiện nút thanh toán (server vẫn trả 404). Trước đợt B: Admin vẫn chốt/trả lại tay được hóa đơn tạm tính; trả lại rồi gửi duyệt tay thì `HanPhanHoi` cũ còn nguyên nên khách vẫn thấy. Đề xuất: chấp nhận, vì A/B/C nằm cùng nhánh và không gộp `master` trước khi xong đợt C.

---

## 0. Trước khi sửa

1. `git status --short` phải sạch (nhánh `feature/hoa-don-tu-dong`).
2. Kiểm baseline không lệch model (lệnh này không kết nối DB, chuỗi kết nối chỉ cần khác rỗng; KHÔNG trỏ DB phát triển/production):
   ```powershell
   $env:QLCTPT_DESIGNTIME_CONNECTION_STRING = "Host=localhost;Database=qlctpt_designtime_test;Username=x;Password=x"
   $p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
   dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
   ```
   Nếu báo có thay đổi trước khi sửa gì: dừng, ghi vào `.bangiao/thay-doi.md`, không tạo migration.
3. Đọc `.agents/rules/core_rules.md` và `.agents/rules/backend_code_review.md`.

---

## 1. Domain

### 1.1 `src/QuanLyChoThuePhongTroWeb.Domain/Entities/ChiNhanh.cs`
Thêm sau `SoDienThoai`: `public int SoGioPhanHoiHoaDon { get; set; } = 24;`

### 1.2 `src/QuanLyChoThuePhongTroWeb.Domain/Entities/HoaDon.cs`
- Thuộc tính sau `HanThanhToan`: `public DateTime? HanPhanHoi { get; set; }`
- Method mới (đặt sau `GuiDuyet`, copy cách ghi lịch sử của `GuiHoaDon`):
  ```csharp
  public void GuiTamTinh(int nguoiThucHienId, DateTime nowUtc, DateTime hanPhanHoiUtc, string lyDo)
  ```
  - `nguoiThucHienId <= 0` → `ArgumentOutOfRangeException`.
  - `nowUtc.Kind != Utc` hoặc `hanPhanHoiUtc.Kind != Utc` → `ArgumentOutOfRangeException`.
  - `hanPhanHoiUtc <= nowUtc` → `ArgumentOutOfRangeException`.
  - `lyDo` rỗng → `ArgumentException`.
  - `DaHuy` → `InvalidOperationException("Không thể gửi hóa đơn đã hủy.")`; khác `Nhap` → `InvalidOperationException` (cùng câu chữ kiểu `GuiDuyet`).
  - Đặt `TrangThaiPhatHanh = ChoDuyet`, `HanPhanHoi = hanPhanHoiUtc`, `NgayCapNhat = nowUtc`; thêm một `LichSuTrangThaiHoaDon` (`Nhap → ChoDuyet`, trạng thái thanh toán giữ nguyên, `NguoiThucHienId`, `NgayThucHien = nowUtc`, `LyDo = lyDo`).
- KHÔNG sửa `GuiDuyet`, `TraLai`, `ChotHoaDon` (thuộc đợt B).

---

## 2. Application

### 2.1 `src/QuanLyChoThuePhongTroWeb.Application/Common/Configurations/AutoInvoiceOptions.cs` (mới)
Copy khuôn `InvoiceIssuanceOptions.cs`.
```csharp
public class AutoInvoiceOptions
{
    public const string SectionName = "AutoInvoiceOptions";
    public double NguongDoTinCay { get; set; } = 0.90;   // 0 < x <= 1
    public decimal HeSoBatThuong { get; set; } = 3m;      // > 0
    public int SoKyTrungBinh { get; set; } = 3;           // >= 1
    public decimal TranDienKwh { get; set; } = 500m;      // > 0
    public decimal TranNuocM3 { get; set; } = 30m;        // > 0
    public int ChuKyJobPhut { get; set; } = 5;            // >= 1 (job thuộc đợt B, chỉ khai báo)
    public void Validate() { /* vi phạm → InvalidOperationException("Cấu hình AutoInvoiceOptions:<Khóa> ...") */ }
}
```

### 2.2 `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/AutoInvoices/AutoInvoiceHistoryTags.cs` (mới)
Namespace `QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices`. Theo cách `Features/HoaDons/Payments/PaymentNoteTags.cs`.
```csharp
public static class AutoInvoiceHistoryTags
{
    public const string TuDong = "[TU_DONG]";
    public const string DieuChinh = "[DIEU_CHINH]";
    public static string TaoLyDo(string nhan, string noiDung) => $"{nhan} {noiDung.Trim()}";
}
```

### 2.3 `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/DTOs/AutoInvoiceDtos.cs` (mới)
```csharp
public enum AutoInvoiceTrigger { KhachTaiAnh = 1, NhanVienTaiAnh, XacNhanAnh, SuaSoDaXacNhan, ChotKy }

public enum AutoInvoiceReason
{
    // Im lặng (chỉ log Information)
    ThieuChiSo = 1, DaCoHoaDon, KhongCoHopDong,
    // Có thông báo
    DoTinCayThap, KhongDocDuoc, ChiSoGiam, TieuThuBatThuong, KyTruocChuaChot,
    KhachKhongCoKenhNhan, ChiNhanhKhongCoNhanVien, NguoiThucHienThieuQuyen,
    NhieuHopDongTrongKy, LoiKhiTao
}

public enum AutoInvoiceOutcome { DaGuiTamTinh = 1, DungImLang, DungCoThongBao }

public sealed class AutoInvoiceRunResult
{
    public AutoInvoiceOutcome Outcome { get; init; }
    public IReadOnlyList<AutoInvoiceReason> LyDos { get; init; } = Array.Empty<AutoInvoiceReason>();
    public int? HoaDonId { get; init; }
    public int? NguoiThucHienId { get; init; }
    public DateTime? HanPhanHoiUtc { get; init; }
}
```
Thêm vào `InvoiceTransitionResult` (trong `InvoiceIssuanceDtos.cs`): `public DateTime? HanPhanHoiUtc { get; init; }`.

### 2.4 `src/QuanLyChoThuePhongTroWeb.Application/Features/Emails/DTOs/ProvisionalInvoiceEmailData.cs` (mới)
Cạnh `ContractExpiryAlertData.cs`.
```csharp
public sealed class ProvisionalInvoiceEmailData
{
    public string MaHoaDon, TenNguoiThue, TenPhong, TenChiNhanh, SoDienThoaiChiNhanh; // init, mặc định ""
    public int Thang, Nam;
    public decimal TongTien;
    public IReadOnlyList<ChiTietHoaDonRes> ChiTietHoaDons;   // từ HoaDonChiTietRes
    public decimal ChiSoDienCu, ChiSoDienMoi, ChiSoNuocCu, ChiSoNuocMoi;
    public DateTime HanTuChotUtc;
}
```

### 2.5 `src/QuanLyChoThuePhongTroWeb.Application/Abstractions/Services/IEmailService.cs`
Thêm method có thân mặc định (giống `SendRoomDepositedNoticeAsync`, để các fake cũ không vỡ):
```csharp
Task<(bool IsSuccess, string ErrorMessage)> SendProvisionalInvoiceEmailAsync(string toEmail, ProvisionalInvoiceEmailData data) =>
    Task.FromResult((IsSuccess: false, ErrorMessage: "Dịch vụ email chưa hỗ trợ hóa đơn tạm tính."));
```

### 2.6 `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Persistence/IAutoInvoiceStore.cs` (mới)
```csharp
public sealed record AutoInvoiceMeterImage(int AnhChiSoDongHoId, LoaiDongHo LoaiDongHo, TrangThaiXuLyAnhChiSo TrangThaiXuLy,
    decimal? GiaTriAIGoiY, double? DoTinCay, decimal? GiaTriXacNhan, bool DuocChonLamChiSoChinhThuc, DateTime NgayGui);
public sealed record AutoInvoiceConsumption(decimal Dien, decimal Nuoc);
public sealed record AutoInvoiceContractRef(int HopDongId, int NguoiThueId);

public sealed class AutoInvoiceRoomContext
{
    public int PhongTroId { get; init; }  public string SoPhong { get; init; } = "";  public int ChiNhanhId { get; init; }
    public int Thang { get; init; }  public int Nam { get; init; }
    public int? DichVuDienNuocCuaPhongId { get; init; }   // null = chưa có kỳ
    public TrangThaiGhiNhan? TrangThaiGhiNhan { get; init; }
    public decimal ChiSoDienCu, ChiSoDienMoi, ChiSoNuocCu, ChiSoNuocMoi; // init
    public IReadOnlyList<AutoInvoiceMeterImage> Images { get; init; }          // ảnh chưa xóa của kỳ, NgayGui giảm dần, Id giảm dần
    public IReadOnlyList<AutoInvoiceConsumption> TieuThuCacKyTruoc { get; init; } // tối đa soKyTrungBinh kỳ DaDuyet trước kỳ này, mới → cũ
    public IReadOnlyList<AutoInvoiceContractRef> HopDongHieuLuc { get; init; }
    public bool CoHoaDonHoatDong { get; init; }
    public bool KyTruocChuaChot { get; init; }
    public int? NguoiDungKhachId { get; init; }   // chỉ điền khi HopDongHieuLuc.Count == 1
    public string? EmailKhach { get; init; }      // như trên; đã Trim, rỗng → null
}

public interface IAutoInvoiceStore
{
    // AsNoTracking toàn bộ. Phòng không tồn tại hoặc IsDeleted → null.
    Task<AutoInvoiceRoomContext?> GetRoomContextAsync(int phongTroId, int thang, int nam, int soKyTrungBinh, CancellationToken ct = default);
    // Mục 8.2: NhanVienChiNhanh của chi nhánh với IsActive && NgayThuHoi == null, NguoiDung.Role == NhanVien && IsActive && !IsDeleted;
    // sắp NgayPhanCong tăng dần rồi NhanVienChiNhanhId tăng dần; trả NguoiDungId đầu tiên hoặc null.
    Task<int?> FindFallbackActorAsync(int chiNhanhId, CancellationToken ct = default);
    // ChangeTracker.Clear() của DbContext dùng chung; chỉ gọi khi không còn thay đổi chưa lưu cần giữ.
    void DiscardPendingChanges();
}
```

### 2.7 `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/AutoInvoices/AutoInvoiceReadingPolicy.cs` (mới, static, thuần, không I/O)
```csharp
public sealed record AutoInvoiceMeterDecision(LoaiDongHo LoaiDongHo, AutoInvoiceReason? LyDo, string? MoTa,
    decimal? ChiSoCu, decimal? ChiSoMoi, int? AnhTuXacNhanId, double? DoTinCay);
public sealed record AutoInvoiceRoomDecision(bool DuDieuKien, IReadOnlyList<AutoInvoiceReason> LyDos, IReadOnlyList<string> MoTaLyDos,
    AutoInvoiceMeterDecision? Dien, AutoInvoiceMeterDecision? Nuoc, AutoInvoiceContractRef? HopDong);

public static class AutoInvoiceReadingPolicy
{
    public static bool IsSilent(AutoInvoiceReason r);            // ThieuChiSo, DaCoHoaDon, KhongCoHopDong
    public static bool CanBaoKhach(AutoInvoiceReason r);         // DoTinCayThap, KhongDocDuoc, ChiSoGiam (người dùng quyết định câu 4)
    public static string GhiChuTuXacNhan(double doTinCay);       // $"Tự xác nhận, độ tin cậy {doTinCay:0.00 theo vi-VN}" → "0,93"
    public static AutoInvoiceMeterDecision EvaluateMeter(LoaiDongHo loai, AutoInvoiceRoomContext ctx, AutoInvoiceOptions o);
    public static AutoInvoiceRoomDecision EvaluateRoom(AutoInvoiceRoomContext ctx, AutoInvoiceOptions o);
}
```
**EvaluateMeter** (chỉ số cũ = `ctx.ChiSo{Dien|Nuoc}Cu`):
1. `ctx.TrangThaiGhiNhan == DaDuyet` → giá trị = `ctx.ChiSo{..}Moi` (chỉ số chính thức, nguồn tin cậy), không tự xác nhận.
2. Có ảnh cùng loại `DuocChonLamChiSoChinhThuc` có `GiaTriXacNhan` → giá trị = `GiaTriXacNhan` (nguồn tin cậy).
3. Ngược lại lấy ảnh ứng viên theo câu hỏi 1. Không có → `ThieuChiSo`.
   - `MoiTaiLen`/`DangXuLy` → `ThieuChiSo`.
   - `KhongDocDuoc`/`Loi`/`CanChupLai` → `KhongDocDuoc`.
   - `DocDuoc`: `DoTinCay == null || DoTinCay < o.NguongDoTinCay` → `DoTinCayThap`; `GiaTriAIGoiY == null` → `KhongDocDuoc`; đạt → giá trị = `GiaTriAIGoiY`, `AnhTuXacNhanId` = id ảnh.
4. Giá trị < chỉ số cũ → `ChiSoGiam`.
5. Tiêu thụ = giá trị − cũ. Lấy tiêu thụ cùng loại từ `ctx.TieuThuCacKyTruoc`. Nếu danh sách rỗng hoặc trung bình == 0: vượt trần (`TranDienKwh` / `TranNuocM3`) → `TieuThuBatThuong`. Ngược lại tiêu thụ > `HeSoBatThuong × trung bình` → `TieuThuBatThuong`. Bằng đúng ngưỡng là đạt. Áp dụng cho MỌI nguồn (kể cả bước 1, 2).
6. `MoTa` tiếng Việt, số theo vi-VN: "Điện: độ tin cậy nhận diện thấp (0,85 < 0,90)", "Nước: ảnh không đọc được, cần chụp lại", "Điện: chỉ số mới (95) nhỏ hơn chỉ số cũ (100)", "Nước: tiêu thụ bất thường (45 m³, giới hạn 30 m³)".

**EvaluateRoom** (dừng ở bước đầu tiên có kết quả):
1. `DichVuDienNuocCuaPhongId == null` → `ThieuChiSo`.
2. `CoHoaDonHoatDong` → `DaCoHoaDon`.
3. `HopDongHieuLuc.Count == 0` → `KhongCoHopDong`; `> 1` → `NhieuHopDongTrongKy` (câu hỏi 3).
4. Đánh giá Điện rồi Nước. Có lý do cần báo ở bất kỳ đồng hồ nào → trả tất cả lý do cần báo (Điện trước). Không có nhưng có `ThieuChiSo` → `ThieuChiSo`.
5. `KyTruocChuaChot` → `KyTruocChuaChot` ("Kỳ MM/yyyy trước chưa được chốt").
6. `NguoiDungKhachId == null && EmailKhach == null` → `KhachKhongCoKenhNhan`.
7. Đạt: `DuDieuKien = true`, kèm `HopDong = HopDongHieuLuc[0]`.

### 2.8 `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/IAutoInvoiceService.cs` + `AutoInvoiceService.cs` (mới)
```csharp
public interface IAutoInvoiceService
{
    // actorId = người làm thao tác gốc (nhân viên/Admin, hoặc tài khoản khách khi trigger = KhachTaiAnh).
    // Luôn trả Success = true, kết quả trong Data. Không bao giờ ném exception. Các bước bên trong dùng CancellationToken.None.
    Task<ServiceResult<AutoInvoiceRunResult>> RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam);
}
```
Phụ thuộc: `IAutoInvoiceStore`, `IMeterReadingWorkflowService`, `IInvoiceIssuanceService`, `IEmployeeAccessService`, `IThongBaoService`, `IEmailService`, `IHoaDonStore`, `AutoInvoiceOptions`, `ILogger<AutoInvoiceService>`. Không phụ thuộc `IMeterImagePortalService`/`IDienNuocService` (tránh vòng DI).

Luồng (toàn bộ bọc `try/catch (Exception)`; exception ngoài dự kiến → `LogError`, thử báo nhân viên lý do `LoiKhiTao` trong try/catch riêng, trả `DungCoThongBao`):
1. Tham số sai (`actorId <= 0`, `phongTroId <= 0`, tháng ngoài 1–12, năm < 2000) → Information, `DungImLang`.
2. `DiscardPendingChanges()`; `ctx = GetRoomContextAsync(..., o.SoKyTrungBinh)`; null → Information, `DungImLang`.
3. `d = EvaluateRoom(ctx, o)`. Không đạt: toàn lý do im lặng → Information, `DungImLang`; còn lại → Warning + `BaoDungAsync` → `DungCoThongBao`.
4. Người thực hiện (mục 8): `trigger == KhachTaiAnh` → `FindFallbackActorAsync(ctx.ChiNhanhId)`, null → `ChiNhanhKhongCoNhanVien`; trigger khác → `actorId`.
5. `CanPerformAsync(nguoiThucHien, ctx.ChiNhanhId, X)` lần lượt với `MeterReview`, `InvoiceDraft`, `InvoiceSubmit`; thiếu một → `NguoiThucHienThieuQuyen`.
6. Bước 1: với Điện rồi Nước có `AnhTuXacNhanId`: `ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId, GiaTriXacNhan = ChiSoMoi, GhiChu = GhiChuTuXacNhan(DoTinCay) }, nguoiThucHien)`. Sau mỗi lời gọi: `DiscardPendingChanges()`. Thất bại → **khôi phục** (dưới).
7. Bước 2: đọc lại ctx; nếu `TrangThaiGhiNhan != DaDuyet` → `ApprovePeriodsAsync(new ApproveMeterPeriodsRequest { Thang, Nam, DanhSachPhong = [ new ApproveMeterPeriodItem { PhongTroId, DienMode = OfficialImage, NuocMode = OfficialImage } ] }, nguoiThucHien)`; `DiscardPendingChanges()`; thất bại → khôi phục.
8. Bước 3: `CreateDraftsAsync(new CreateInvoiceDraftsRequest { ChiNhanhId = ctx.ChiNhanhId, Thang, Nam, PhongTroIds = [phongTroId] }, nguoiThucHien)`; `DiscardPendingChanges()`. `!Success` → `LoiKhiTao` (kèm Message). Lấy item `Status == Created && HopDongId == d.HopDong.HopDongId`. Không có: có item `SkippedHasActiveInvoice` → Information, `DungImLang`; còn lại → `LoiKhiTao` kèm `Message` của item.
9. Bước 4: `SubmitProvisionalAsync(item.HoaDonId!.Value, nguoiThucHien)`; `DiscardPendingChanges()`. Thất bại → `LoiKhiTao` với mô tả "Đã tạo hóa đơn nháp {MaHoaDon} nhưng chưa gửi được bản tạm tính: {Message}. Vui lòng xử lý theo luồng tay." KHÔNG thử lại.
10. Bước 5: `BaoKhachTamTinhAsync` (dưới). Information. Trả `DaGuiTamTinh` với `HoaDonId`, `NguoiThucHienId`, `HanPhanHoiUtc`.

**Khôi phục sau bước 1/2 thất bại** (chống kích hoạt song song): `DiscardPendingChanges()`, đọc lại ctx. `CoHoaDonHoatDong` → Information, `DungImLang`. Bước 1: đồng hồ đó nay đã có chỉ số chính thức (kỳ `DaDuyet` hoặc có ảnh chính thức) → đi tiếp. Bước 2: kỳ nay `DaDuyet` → đi tiếp. Còn lại → `LoiKhiTao` kèm `Message` của bước.

**BaoDungAsync(ctx, lyDos, moTas)**: mỗi lời gọi trong `try/catch` riêng, lỗi → Warning; kết quả `!Success` → Information.
- Nhân viên + Admin: `GuiChoNguoiPhuTrachPhongAsync(ctx.PhongTroId, $"Phòng {SoPhong} kỳ {Thang:00}/{Nam}: chưa tạo được hóa đơn tạm tính", "Lý do: " + string.Join("; ", moTas), "HoaDon", $"/QuanLyNhaTro/ChotDienNuoc?thang={Thang}&nam={Nam}")` (sửa sau review; bản gốc ghi `/QuanLyNhaTro/DienNuoc` là trang 404).
- Khách: chỉ khi có lý do `CanBaoKhach` và `HopDongHieuLuc.Count == 1`. Một thông báo cho mỗi đồng hồ có lý do:
  - `DoTinCayThap`/`KhongDocDuoc`: `GuiChoKhachThueAsync(nguoiThueId, "Cần chụp lại ảnh đồng hồ", $"Ảnh chỉ số {điện/nước} kỳ {Thang:00}/{Nam} của phòng {SoPhong} chưa đọc được rõ. Vui lòng chụp lại và gửi lại ảnh.", "DienNuoc", "/KhachThue/ChiSoDienNuoc")`.
  - `ChiSoGiam`: `GuiChoKhachThueAsync(nguoiThueId, "Chỉ số đồng hồ thấp hơn kỳ trước", $"Chỉ số {điện/nước} kỳ {Thang:00}/{Nam} của phòng {SoPhong} đọc được là {giá trị mới} nhỏ hơn chỉ số cũ {chỉ số cũ}. Vui lòng chụp lại rõ mặt đồng hồ hoặc liên hệ {SoDienThoaiChiNhanh}.", "DienNuoc", "/KhachThue/ChiSoDienNuoc")`. Thiếu số điện thoại chi nhánh thì bỏ vế "hoặc liên hệ …".
  - Test Application: `ChiSoGiam` gửi khách đúng một thông báo; khách không nhận thông báo cho `TieuThuBatThuong`, `KyTruocChuaChot` và các lý do còn lại.
- Mô tả cố định cho lý do không thuộc đồng hồ: `KyTruocChuaChot` (như trên), `KhachKhongCoKenhNhan` "Khách thuê đại diện không có tài khoản cổng và không có email", `ChiNhanhKhongCoNhanVien` "Chi nhánh chưa có nhân viên được phân công để thực hiện tự động", `NguoiThucHienThieuQuyen` "Người thực hiện thiếu quyền Meter.Review, Invoice.Draft hoặc Invoice.Submit tại chi nhánh", `NhieuHopDongTrongKy` (câu hỏi 3), `LoiKhiTao` = message của bước.

**BaoKhachTamTinhAsync**: `detail = IHoaDonStore.GetHoaDonDetailByIdAsync(hoaDonId)`; null → Warning, bỏ qua.
- Có `NguoiDungKhachId` → `GuiChoKhachThueAsync(nguoiThueId, $"Hóa đơn tạm tính tháng {Thang}/{Nam}", $"Hóa đơn {Ma} tháng {T}/{N}: tổng tiền {TongTien:#,##0} đ (tạm tính). Hệ thống tự chốt lúc {hạn giờ VN dd/MM/yyyy HH:mm}. Nếu có sai sót, vui lòng liên hệ {SoDienThoaiChiNhanh}.", "HoaDon", $"/KhachThue/HoaDon?hoaDonId={id}")`.
- Có `EmailKhach` → `SendProvisionalInvoiceEmailAsync(email, data)` (data từ `detail` + chỉ số của `d.Dien`/`d.Nuoc` + `HanPhanHoiUtc`).
- Mỗi kênh `try/catch` riêng; lỗi/thất bại → Warning; không hoàn tác dữ liệu, không đổi Outcome.
- Giờ VN: `utc.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)` (cách của `InvoicePublicationService.cs`).

Mức log: Information cho dừng im lặng và thành công; Warning cho dừng có thông báo, bước thất bại, gửi lỗi; Error chỉ trong catch-all.

### 2.9 `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/IInvoiceIssuanceService.cs` + `InvoiceIssuanceService.cs`
- Constructor thêm tham số cuối `TimeProvider? timeProvider = null` (`_time = timeProvider ?? TimeProvider.System`, như `MeterReadingWorkflowService.cs`).
- **Sửa `CreateDraftsAsync`** (T3): ghi nhớ `(index trong items, hoaDon)` của từng hóa đơn tạo mới; sau `SaveChangesAsync` thay phần tử đó bằng `InvoiceDraftItemResult` mới có `HoaDonId = hoaDon.HoaDonId`. Không đổi gì khác.
- Method mới, đặt sau `SubmitAsync`, copy thân `SubmitAsync`:
  ```csharp
  Task<ServiceResult<InvoiceTransitionResult>> SubmitProvisionalAsync(int hoaDonId, int actorId, CancellationToken ct = default);
  ```
  Khác `SubmitAsync`: (a) `meterPeriodId` null hoặc `LockMeterPeriodAsync` trả khác `DaDuyet` → Fail "Kỳ chỉ số điện nước liên kết chưa được chốt."; (b) `chiNhanh = _hoaDonStore.GetChiNhanhByIdAsync(branchId)` trong transaction, null hoặc `SoGioPhanHoiHoaDon` ngoài 1–168 → Fail; (c) `now = _time.GetUtcNow().UtcDateTime`, `han = now.AddHours(chiNhanh.SoGioPhanHoiHoaDon)`; (d) `hoaDon.GuiTamTinh(actorId, now, han, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.TuDong, "Gửi khách hóa đơn tạm tính"))`; (e) kết quả có `HanPhanHoiUtc = han`. Quyền `InvoiceSubmit`, fail closed như `SubmitAsync`.
- KHÔNG sửa `SubmitAsync`, `FinalizeAsync`, `RejectAsync`.

### 2.10 Điểm kích hoạt (mục 5.1)
`src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Services/MeterImagePortalService.cs`:
- Constructor thêm tham số cuối `IAutoInvoiceService? autoInvoiceService = null` (null = không kích hoạt; giữ được các test cũ).
- `UploadAsync`: sau khi đã tính `isStaff` và `resItem`, trước `return Ok`: gọi `RunForRoomAsync(actorUserId, isStaff ? NhanVienTaiAnh : KhachTaiAnh, command.PhongTroId, command.Thang, command.Nam)`.
- `ConfirmAsync` (trigger `XacNhanAnh`), `CorrectAsync` (`SuaSoDaXacNhan`): sau khi workflow trả Success, dùng `wfRes.Data!.PhongTroId/Thang/Nam`.
- Thao tác gốc thất bại → không gọi. Kết quả trả về của thao tác gốc giữ nguyên. `RetryOcrAsync` không gọi (câu hỏi 6).

`src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Services/DienNuocService.cs`:
- Constructor thêm tham số cuối `IAutoInvoiceService? autoInvoiceService = null`.
- `SaveChotDienNuocAsync`: sau `batchResult.Success`, với từng `PhongTroId` distinct tăng dần của `input.DanhSachPhong`, tuần tự `RunForRoomAsync(actorId, ChotKy, phongTroId, input.Thang, input.Nam)`. Vẫn trả `(true, string.Empty)`.

### 2.11 `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/InvoiceViewService.cs`
`ExportTenantPdfAsync`: sau khi có `detail`, nếu `detail.TrangThaiPhatHanhValue == (int)TrangThaiPhatHanhHoaDon.ChoDuyet` → `return null`. `GetTenantDetailAsync` không đổi.

### 2.12 `src/QuanLyChoThuePhongTroWeb.Application/DependencyInjection.cs`
Trong khối "Phase 6.4": `services.AddScoped<IAutoInvoiceService, AutoInvoiceService>();`

### 2.13 KHÔNG đụng
`InvoicePaymentSnapshots.cs` (kể cả comment dòng 22, sau thay đổi sẽ không còn đúng: ghi chú vào `.bangiao/thay-doi.md`), `ChiNhanhDTOs.cs`/`ChiNhanhService.cs` (đợt C), `ViecCanLamService` (đợt C), mọi job (đợt B).

---

## 3. Infrastructure

### 3.1 Cấu hình EF (S2, S4)
- `ChiNhanhConfiguration.cs`: `builder.Property(c => c.SoGioPhanHoiHoaDon).HasDefaultValue(24);` và `builder.ToTable(t => t.HasCheckConstraint("CK_ChiNhanh_SoGioPhanHoiHoaDon", "\"SoGioPhanHoiHoaDon\" BETWEEN 1 AND 168"));` (copy cách `AnhChiSoDongHoConfiguration.cs`).
- `HoaDonConfiguration.cs`: `builder.HasIndex(h => new { h.TrangThaiPhatHanh, h.HanPhanHoi }).HasDatabaseName("IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi").HasFilter("\"HanPhanHoi\" IS NOT NULL AND \"IsDeleted\" = false");` (copy cách filter của unique index cùng file).

### 3.2 Migration (S5)
```powershell
$env:QLCTPT_DESIGNTIME_CONNECTION_STRING = "Host=localhost;Database=qlctpt_designtime_test;Username=x;Password=x"
$p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
dotnet ef migrations add AddAutoInvoiceReviewWindow --project $p --startup-project $p --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
```
Migration chỉ được chứa: AddColumn `SoGioPhanHoiHoaDon` (`integer`, `nullable: false`, `defaultValue: 24`), AddColumn `HanPhanHoi` (`timestamp with time zone`, nullable), AddCheckConstraint, CreateIndex có filter; `Down` ngược lại. Có thao tác nào khác → xóa migration vừa sinh, dừng, báo trong `thay-doi.md`.

### 3.3 `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/AutoInvoiceStore.cs` (mới, `IAutoInvoiceStore`)
Toàn bộ `AsNoTracking`. Copy điều kiện, không tự chế:
- Phòng: `PhongTros` chưa xóa (như `MeterImageQueryStore.GetRoomAsync`).
- Kỳ: `DichVuDienNuocCuaPhongs` theo phòng/tháng/năm, `!IsDeleted` (như `MeterImageQueryStore.GetPeriodAsync`). Ảnh: như `MeterImageQueryStore.GetImagesAsync` (chưa xóa, `NgayGui` giảm dần, Id giảm dần).
- Tiêu thụ kỳ trước: cùng phòng, `!IsDeleted`, `TrangThaiGhiNhan == DaDuyet`, `(Nam < nam) || (Nam == nam && Thang < thang)`, sắp `Nam` rồi `Thang` giảm dần, `Take(soKyTrungBinh)`, chọn `(ChiSoDienMoi − ChiSoDienCu, ChiSoNuocMoi − ChiSoNuocCu)`.
- Hợp đồng hiệu lực: mốc tháng tính đúng như `InvoiceIssuanceService.CreateDraftsAsync` dòng 91–94 (`startOfMonthUtc`, `endOfMonthUtc`); điều kiện đúng như `HoaDonStore.GetValidContractsForBillingAsync` dòng 37–42 (thêm `PhongTroId == phongTroId`, chi nhánh của phòng).
- `CoHoaDonHoatDong`: có `HoaDon` `!IsDeleted` với `HopDongId` thuộc danh sách trên, cùng tháng/năm (như `GetExistingInvoiceContractIdsAsync`).
- `KyTruocChuaChot`: kỳ trước = `MeterPeriodPolicy.Previous`; có hợp đồng trong tháng trước theo đúng điều kiện `MeterImageStore.HasContractInMonthAsync` VÀ (kỳ trước không có bản ghi `!IsDeleted` hoặc khác `DaDuyet`).
- Kênh nhận (chỉ khi đúng 1 hợp đồng): copy `InvoicePublicationStore.GetRecipientAsync` (email Trim, rỗng → null; `NguoiDung` theo `NguoiThueId`, `!IsDeleted && IsActive`, Id nhỏ nhất).
- `FindFallbackActorAsync`: như chú thích ở 2.6.
- `DiscardPendingChanges()`: `_context.ChangeTracker.Clear();`

### 3.4 `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/HoaDonStore.cs` (T5)
Trong `GetInvoicesByContractIdsAsync` và `CheckHoaDonOwnershipAsync` thêm vế thứ ba vào biểu thức OR hiện có:
`|| (x.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.ChoDuyet && x.HanPhanHoi != null && !x.IsDeleted)`. Không đổi chỗ nào khác của file.

### 3.5 `src/QuanLyChoThuePhongTroWeb.Infrastructure/ExternalServices/Emails/MailKitEmailService.cs` (T7)
Thêm `SendProvisionalInvoiceEmailAsync`, copy khung `SendInvoiceEmailAsync` (đọc cấu hình SMTP, thiếu SenderEmail/Password → trả lỗi, `catch` → `LogWarning`). Tiêu đề `$"[HÓA ĐƠN TẠM TÍNH] - Phòng {TenPhong} - Kỳ tháng {Thang}/{Nam}"`. Nội dung: mã hóa đơn, phòng, người thuê, kỳ, bảng chi tiết, tổng tiền, chỉ số điện cũ/mới, nước cũ/mới, "Hệ thống tự chốt lúc {HanTuChotUtc.AddHours(7):dd/MM/yyyy HH:mm}", "Nếu có sai sót, vui lòng liên hệ {SoDienThoaiChiNhanh}", câu "Xem chi tiết tại cổng khách thuê, mục Hóa đơn" (câu hỏi 5). KHÔNG QR, KHÔNG thông tin ngân hàng, KHÔNG đính kèm.

### 3.6 `src/QuanLyChoThuePhongTroWeb.Infrastructure/DependencyInjection.cs`
- Đăng ký store: `services.AddScoped<...HoaDons.Persistence.IAutoInvoiceStore, Persistence.Features.AutoInvoiceStore>();` cạnh `IInvoiceIssuanceStore`.
- Sau khối `invoiceIssuanceOptions`: bind `AutoInvoiceOptions.SectionName`, `?? new()`, `Validate()`, `AddSingleton` (copy y khối `InvoiceIssuanceOptions`).

---

## 4. Web

- `src/QuanLyChoThuePhongTroWeb.Web/appsettings.example.json`: thêm sau `InvoiceIssuanceOptions`:
  ```json
  "AutoInvoiceOptions": {
    // Hóa đơn tự động: ngưỡng độ tin cậy OCR (0 < x <= 1), hệ số tiêu thụ bất thường so với trung bình,
    // số kỳ lấy trung bình, trần tiêu thụ khi chưa có kỳ trước, chu kỳ job tự chốt (phút).
    "NguongDoTinCay": 0.90, "HeSoBatThuong": 3, "SoKyTrungBinh": 3,
    "TranDienKwh": 500, "TranNuocM3": 30, "ChuKyJobPhut": 5
  }
  ```
  KHÔNG sửa `appsettings.json` cục bộ, `Program.cs`, controller, view, JS. Web không tham chiếu Domain.

---

## 5. Trường hợp biên bắt buộc

- **Thời gian:** `HanPhanHoi` là UTC (`Kind = Utc`) lấy từ `TimeProvider`; chỉ đổi sang giờ VN khi tạo nội dung thông báo/email.
- **Số giờ chi nhánh** đọc lúc gửi (bước 4); đổi số giờ sau không dời hạn đã gửi.
- **Xóa mềm:** hóa đơn `IsDeleted` không tính là hoạt động và không hiện cho khách (vế mới có `!IsDeleted`); ảnh, kỳ, phòng, hợp đồng, người dùng `IsDeleted` bị loại ở mọi truy vấn của `AutoInvoiceStore`; phân công thu hồi (`NgayThuHoi != null` hoặc `!IsActive`) không được chọn làm người thực hiện.
- **Phân quyền:** người thực hiện phải qua `CanPerformAsync` cho cả 3 mã trước bước 1; mỗi service con tự kiểm lại như luồng tay (fail closed). Khách thuê không bao giờ là người thực hiện; Admin chỉ là người thực hiện khi chính Admin làm thao tác gốc.
- **ChangeTracker dùng chung:** gọi `DiscardPendingChanges()` ở đầu chuỗi và sau MỖI bước (thành công hay thất bại) để `SaveChanges` của `ThongBaoService` không lưu nhầm thay đổi dở của bước đã rollback.
- **Không đổi kết quả thao tác gốc:** `RunForRoomAsync` không ném; caller không đổi message/Success.
- **Kích hoạt lặp/song song:** không tạo hóa đơn thứ hai (kiểm `CoHoaDonHoatDong`, `SkippedHasActiveInvoice`, khôi phục mục 2.8, unique index sẵn có).
- **Dừng sau bước 3:** hóa đơn `Nhap` không có hạn để nguyên cho luồng tay, không thử lại.
- **Không tự thay chỉ số chính thức** bằng ảnh mới.
- **Tạm tính không vào thanh toán, công nợ, nhắc nợ, Dashboard:** không đổi `KhachXemDuoc`, `GetUnpaidInvoicesAsync`, `GetOverdueInvoicesAsync`, `KhachThue/DashboardController`.

---

## 6. Test

Quy ước: Domain theo `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/InvoiceWorkflowTests.cs`; Application theo `InvoiceDraftWorkflowTests.cs`, `MeterImagePortalServiceTests.cs`, fake dùng chung trong `Fakes/` (`FakeEmployeeAccessService` namespace `...UnitTests.Fakes`; `FakeMeterReadingWorkflowService` namespace `...UnitTests`; `FixedTimeProvider`; `CapturingLogger`); Infrastructure theo `Persistence/InvoiceDraftConcurrencyTests.cs` (`[Collection("PostgreSqlCollection")]`, tự tạo service thật với DbContext riêng) và `Persistence/MeterReadingCorrectionTests.cs`; Web theo `TenantInvoicePortalTests.cs`, `MeterImageTenantPortalTests.cs` (`[Collection(WebTestCollection.Name)]`).

### 6.1 Domain.UnitTests: `InvoiceWorkflowTests.cs`
`GuiTamTinh`: từ `Nhap` → `ChoDuyet`, `HanPhanHoi` đúng, một dòng lịch sử đúng người/giờ/lý do; ném khi ở `ChoDuyet`/`DaChot`/`DaGui`/`DaHuy`; ném khi hạn ≤ now, `Kind` không UTC, người ≤ 0, lý do rỗng. `GuiDuyet` không đụng `HanPhanHoi`.

### 6.2 Application.UnitTests
- `AutoInvoiceOptionsTests.cs` (mới): mặc định hợp lệ; từng khóa ở biên sai (0 và 1,01 cho ngưỡng; 0 cho hệ số; 0 cho số kỳ; 0 cho hai trần; 0 cho chu kỳ) ném `InvalidOperationException`; ngưỡng = 1 hợp lệ.
- `AutoInvoiceReadingPolicyTests.cs` (mới): 0,89 → `DoTinCayThap`, 0,90 → đạt; `DoTinCay` null; `KhongDocDuoc`/`Loi`/`CanChupLai`; `MoiTaiLen`/`DangXuLy` → im lặng; ảnh mới nhất được xét, không xét ảnh cũ (câu hỏi 1); số giảm; đúng 3 × trung bình đạt, hơn 1 đơn vị nhỏ nhất (0,001) không đạt; chưa có kỳ trước: 500 đạt/501 không, 30 đạt/31 không; trung bình 0 dùng trần; chỉ lấy tối đa `SoKyTrungBinh` kỳ; nguồn kỳ `DaDuyet` (nhập tay) và ảnh chính thức vẫn chịu kiểm bất thường; từng bước của `EvaluateRoom` (không kỳ, có hóa đơn, 0 / 2 hợp đồng, kỳ trước chưa chốt, không kênh, chỉ email đạt, chỉ tài khoản đạt, lý do cần báo thắng thiếu chỉ số); `GhiChuTuXacNhan(0.93)` = "Tự xác nhận, độ tin cậy 0,93".
- `AutoInvoiceServiceTests.cs` (mới) với fake mới trong `Fakes/`: `FakeAutoInvoiceStore` (đếm `DiscardPendingChanges`, trả ctx theo kịch bản, đổi ctx sau lần gọi thứ n), `FakeInvoiceIssuanceService`, `FakeThongBaoService` (ghi lại lời gọi, có cờ ném), `RecordingEmailService`; dùng `FakeMeterReadingWorkflowService`, `FakeEmployeeAccessService`, `FakeHoaDonStore`, `CapturingLogger`. Kiểm: thứ tự Confirm (đúng ảnh, giá trị, ghi chú) → Approve (OfficialImage cả hai) → CreateDrafts (đúng phòng/chi nhánh) → SubmitProvisional (đúng `HoaDonId`) → thông báo + email; bỏ bước 1 khi đã có chính thức; bỏ bước 2 khi `DaDuyet`; trigger `KhachTaiAnh` dùng người dự phòng, trigger khác dùng `actorId`; không có người dự phòng → `ChiNhanhKhongCoNhanVien`; thiếu từng mã quyền → `NguoiThucHienThieuQuyen` và không gọi bước nào; lý do im lặng → không gọi `IThongBaoService`; lý do cần báo → báo nhân viên, chỉ báo khách khi `DoTinCayThap`/`KhongDocDuoc`; bước 1 thất bại rồi ctx đọc lại đã có chính thức → đi tiếp; thất bại không khôi phục được → `LoiKhiTao`, không gọi bước sau; `SkippedHasActiveInvoice` → im lặng; bước 4 thất bại → báo nhân viên, không báo khách; chỉ email/chỉ tài khoản; thông báo hoặc email ném/thất bại → vẫn `DaGuiTamTinh`, có Warning, không có Error; store ném → Error đúng một lần, không ném ra ngoài; `DiscardPendingChanges` gọi ở đầu và sau mỗi bước.
- `InvoiceDraftWorkflowTests.cs` (sửa fake riêng trong file nếu cần): `SubmitProvisionalAsync` đặt `HanPhanHoi = now + giờ chi nhánh` (dùng `FixedTimeProvider`, chi nhánh 24 và 48), lịch sử bắt đầu `[TU_DONG]`; Fail khi không `Nhap`, thiếu quyền `InvoiceSubmit`, kỳ chưa `DaDuyet`, tổng tiền lệch, không có chi tiết; `SubmitAsync` cũ không đặt `HanPhanHoi`.
- `InvoiceViewServiceTests.cs`: `ExportTenantPdfAsync` trả null cho `ChoDuyet`, vẫn trả PDF cho `DaGui`.
- `MeterImagePortalServiceTests.cs`: dùng `FakeAutoInvoiceService` (mới, ghi lại lời gọi): khách tải ảnh → `KhachTaiAnh`; nhân viên → `NhanVienTaiAnh`; Confirm/Correct thành công → đúng trigger và phòng/kỳ; thao tác gốc thất bại → không gọi; `RetryOcrAsync` → không gọi; kết quả trả về không đổi.
- `SaveChotDienNuocWorkflowTests.cs`: chốt thành công 2 phòng → 2 lời gọi `ChotKy` theo Id tăng dần; chốt thất bại → không gọi.
- `BranchScopeArchitectureTests.cs`: thêm `typeof(IAutoInvoiceService)` vào `ScopedServices`.

### 6.3 Infrastructure.IntegrationTests (DB `_test`)
- `Persistence/AutoInvoiceSchemaTests.cs` (mới): insert chi nhánh bằng SQL thô không có cột → đọc lại 24; 0 và 169 → `DbUpdateException`; 1 và 168 lưu được; `HanPhanHoi` lưu/đọc lại UTC; `pg_indexes` có `IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi` với `indexdef` chứa `"HanPhanHoi" IS NOT NULL` và `"IsDeleted" = false` (cách truy vấn như `PublicReservationFoundationMappingTests.cs` dòng ~371).
- `Persistence/AutoInvoiceStoreTests.cs` (mới): thứ tự ảnh, loại ảnh xóa; tiêu thụ chỉ kỳ `DaDuyet` trước kỳ đang xét, bỏ kỳ xóa, đúng `Take`; hợp đồng hiệu lực (bỏ `DaHuy`, xóa, ngoài tháng); `CoHoaDonHoatDong` (bỏ hóa đơn xóa); `KyTruocChuaChot`; kênh nhận; `FindFallbackActorAsync` chọn `NgayPhanCong` sớm nhất, hòa thì Id nhỏ hơn, bỏ phân công thu hồi, tài khoản khóa/xóa, role khác `NhanVien`; `DiscardPendingChanges` bỏ entity đang tracking.
- `Persistence/TenantInvoiceVisibilityTests.cs` (mới): `GetInvoicesByContractIdsAsync` và `CheckHoaDonOwnershipAsync` có `ChoDuyet + HanPhanHoi`, không có `ChoDuyet` không hạn, không có `ChoDuyet + HanPhanHoi + IsDeleted`, vẫn có `DaGui` và `DaHuy` đã gửi.
- `Persistence/AutoInvoiceChainTests.cs` (mới, service thật trên DB, fake cho OCR/lưu ảnh/email/`IThongBaoNotifier`, `FixedTimeProvider`): (a) khách tải điện + nước OCR 0,95 số hợp lý → đúng một hóa đơn `ChoDuyet`, `HanPhanHoi = now + giờ chi nhánh`, ảnh `DaXacNhan` với `NguoiXacNhanId` = nhân viên mục 8, lịch sử có dòng `[TU_DONG]` người = nhân viên đó, có `ThongBao` cho khách, có lời gọi email tạm tính; `HoaDonId` trong kết quả `CreateDraftsAsync` bằng Id trong DB; (b) chạy `RunForRoomAsync` hai lần → vẫn một hóa đơn; (c) hai DbContext chạy song song (`Task.WhenAll`) → một hóa đơn, không lỗi; (d) một ảnh 0,89 → không hóa đơn, có `ThongBao` cho nhân viên chi nhánh, Admin và khách; (e) nhân viên chốt tay số hợp lý qua `DienNuocService` có truyền `AutoInvoiceService` → có hóa đơn tạm tính.

### 6.4 Web.IntegrationTests
- `TenantInvoicePortalTests.cs`: seed thêm hóa đơn tạm tính (`ChoDuyet`, có `HanPhanHoi`) và hóa đơn `ChoDuyet` tay (không hạn), dùng kỳ khác nhau vì unique index (`HopDongId`,`Thang`,`Nam`). Kiểm: `GetDanhSach` có tạm tính, không có `ChoDuyet` tay; `XemChiTiet` tạm tính 200, `ChoDuyet` tay 404; `TaiPdf` tạm tính 404; tạo lượt thanh toán cho tạm tính 404; Dashboard khách: tổng nợ không cộng tiền tạm tính (dùng số tiền dễ phân biệt).
- `AutoInvoiceTriggerTests.cs` (mới): kiểm nối DI thật. Seed chi nhánh, nhân viên được phân công, khách có tài khoản và email, hợp đồng bắt đầu từ đầu tháng hiện tại theo giờ VN (để không vướng quy tắc kỳ trước khi tải ảnh). Đặt `FakeMeterOcrServiceInstance` giá trị 20, độ tin cậy 0,95, `Reset()` trong `finally`. Khách tải ảnh điện rồi nước qua HTTP → có một hóa đơn `ChoDuyet` có `HanPhanHoi`, và `FakeEmailService` ghi nhận email tạm tính. Trong `Fixtures/CustomWebApplicationFactory.cs`, `FakeEmailService` thêm override `SendProvisionalInvoiceEmailAsync` ghi vào hàng đợi mới `ProvisionalEmailCalls` (chỉ thêm, không đổi phần khác của fixture).
- Cảnh báo: `MeterImageTenantPortalTests.cs`, `MeterImageStaffEndpointTests.cs`, `MeterReadingSubmissionTests.cs`, `EmployeeBranchAuthorizationTests.cs` tải ảnh/chốt kỳ, nên nay có thể sinh hóa đơn tạm tính hoặc thông báo. Không sửa assertion cũ để cho qua. Chỉ được sửa test cũ khi hành vi mới đúng yêu cầu spec; liệt kê từng test, lý do trong `.bangiao/thay-doi.md`. Không chắc → dừng, báo.

### 6.5 Lệnh chạy (tuần tự, không chạy song song hai lượt `dotnet test`)
```powershell
dotnet build QuanLyChoThuePhongTroWeb.sln
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
# Chỉ khi có DB test, tên kết thúc _test hoặc _integration_test; thiếu thì ghi CHƯA CHẠY
$env:QLCTPT_TEST_CONNECTION_STRING = "Host=...;Database=qlctpt_test;..."
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests
$p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
git diff --check
```
Chạy một lớp: `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~AutoInvoiceServiceTests"`.

---

## 7. Ảnh hưởng schema PostgreSQL

CÓ ĐỔI SCHEMA: một migration mới `AddAutoInvoiceReviewWindow` như mục S (2 cột, 1 CHECK, 1 index lọc). Không bảng mới, không sửa migration cũ, không đổi dữ liệu cũ. Đã đưa vào mục CẦN NGƯỜI DÙNG XÁC NHẬN.

---

## 8. Ngoài phạm vi đợt A (không làm)

Job `AutoInvoiceFinalizeJob` (5.3); "Điều chỉnh và gửi lại" (6); chặn `FinalizeAsync`/`RejectAsync` với hóa đơn tạm tính và `SubmitAsync` xóa hạn (7.3); mọi Razor/JS (nhãn tạm tính, màn hóa đơn quản lý, màn chi nhánh, `ChiNhanhReq.SoGioPhanHoiHoaDon`); Việc cần làm `HD2`/`HD4`; cập nhật `AGENTS.md` mục Hiện trạng (làm khi xong cả tính năng).
