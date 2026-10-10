> Bản lưu của `.bangiao/ke-hoach.md` (planner `/ship`, đợt B), đã qua cổng xác nhận của người dùng ngày 10/10/2026 21:39 (dòng ĐÃ XÁC NHẬN ở cuối mục CẦN NGƯỜI DÙNG XÁC NHẬN). Nội dung giữ nguyên; các điểm sửa sau review ghi ở [review.md](review.md), mục đợt B.

# Kế hoạch: Hóa đơn tự động, ĐỢT B

Nguồn: `docs/features/hoa-don-tu-dong/spec.md` v7 mục 5.3, 6, 7.3 (phần server), 8, 9, 10, 11, 12, 14; quyết định L1, L2 và lưu ý L3, L4, L5, L7 trong `docs/features/hoa-don-tu-dong/review.md`.
Nhánh: `feature/hoa-don-tu-dong` (đợt A ở `39b3d81`). Không commit, không push. Không làm Razor/JS/CSS (đợt C).

---

## CẦN NGƯỜI DÙNG XÁC NHẬN

Coder chỉ bắt đầu khi người dùng xác nhận từng điểm dưới đây ngay trên file này.

### S. Schema

KHÔNG ĐỔI SCHEMA. Không sửa entity được map, `Configurations/`, `ApplicationDbContext`, `Migrations/`, index, ràng buộc. Truy vấn mới dùng cột và index lọc `IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi` đã có từ đợt A. Thêm method vào entity `HoaDon` (không thêm thuộc tính) nên không đổi model.

### T. Tiền (tiếp nối T1–T8 của đợt A)

| # | File | Trước | Sau |
|---|---|---|---|
| T9 | `Application/Features/HoaDons/AutoInvoices/AutoInvoiceReadingPolicy.cs` + `AutoInvoiceDtos.cs` | Kỳ có tiêu thụ nhưng đơn giá của kỳ = 0 vẫn tạo hóa đơn tạm tính chỉ có tiền phòng | Lý do dừng mới `ThieuDonGia` (L1): tiêu thụ điện > 0 mà `DonGiaDien` của kỳ ≤ 0, hoặc tiêu thụ nước > 0 mà `DonGiaNuoc` ≤ 0 thì không tạo hóa đơn; báo nhân viên phụ trách + Admin, không báo khách |
| T10 | `Application/Features/HoaDons/UseCases/AutoInvoiceFinalizeUseCase.cs` (mới) + `Infrastructure/BackgroundJobs/AutoInvoiceFinalizeJob.cs` (mới) | Hóa đơn tạm tính nằm ở `ChoDuyet` mãi; chỉ Admin chốt tay | Job mỗi `ChuKyJobPhut` phút: hóa đơn `ChoDuyet` có `HanPhanHoi <= now` được `ChoDuyet → DaChot → DaGui`, đặt `NgayGui`, `HanThanhToan` theo `SoNgayHanThanhToan`; từ lúc này tính công nợ, khách thanh toán được, nhận email PDF + VietQR. Không kiểm quyền (ngoại lệ của "chỉ Admin chốt"); người ghi lịch sử = người của lần chuyển sang `ChoDuyet` gần nhất |
| T11 | `Application/Features/HoaDons/Services/InvoiceAdjustmentService.cs` (mới) | Không có | "Điều chỉnh và gửi lại" hóa đơn tạm tính (`ChoDuyet`): đổi chỉ số kỳ, tính lại dòng điện/nước bằng `RecalculateDraftInvoices` có sẵn, đặt `HanPhanHoi` mới = bây giờ + số giờ hiện tại của chi nhánh; một transaction, lỗi thì giữ nguyên tiền và hạn cũ. Bỏ qua kiểm tiêu thụ bất thường |
| T12 | Như T11 | Không có | Điều chỉnh hóa đơn `DaGui` chưa thu đồng nào: hủy hóa đơn cũ (`DaHuy`, `IsDeleted`, rời công nợ), hủy lượt thanh toán `ChoThanhToan`, đổi chỉ số, tạo hóa đơn thay thế `-R{n}` bằng đúng phần lõi của `CreateDraftsAsync` (tính lại MỌI dòng theo dữ liệu hiện tại: tiền phòng, dịch vụ cố định, sự cố, điện nước; có thể khác hóa đơn cũ ở cả dòng không phải điện nước nếu dữ liệu nguồn đã đổi), gửi tạm tính với hạn mới. Một transaction |
| T13 | `Application/Features/HoaDons/Services/InvoiceIssuanceService.cs` (`CreateDraftsAsync`) | Lõi tạo nháp nằm trong method tự mở transaction | Tách lõi thành private dùng chung cho `CreateDraftsAsync` và method mới `CreateDraftsInCurrentTransactionAsync`. Không đổi cách tính, làm tròn, mã hóa đơn, thông báo lỗi |
| T14 | `InvoiceIssuanceService.cs` (`FinalizeAsync`, `RejectAsync`) | Admin chốt/trả lại tay được hóa đơn tạm tính | Từ chối khi hóa đơn `ChoDuyet` có `HanPhanHoi != null`: "Hóa đơn đang tự chốt theo hạn, dùng Điều chỉnh và gửi lại." |
| T15 | `Domain/Entities/HoaDon.cs` (`GuiDuyet`) | Gửi duyệt tay giữ nguyên `HanPhanHoi` | `GuiDuyet` đặt `HanPhanHoi = null`: hóa đơn gửi duyệt tay không hiện cho khách và không bị job tự chốt |
| T16 | `Application/Features/DienNuocs/Services/MeterReadingWorkflowService.cs` | Không có | Method mới `AdjustReadingsInCurrentTransactionAsync` đổi chỉ số của kỳ đã `DaDuyet` (sửa số ảnh chính thức hoặc ghi như nhập tay) và tính lại hóa đơn nháp liên kết. `CorrectConfirmedImageAsync`, `ApprovePeriodsAsync` giữ nguyên |
| T17 | `AutoInvoiceService.cs` | Email tạm tính lấy chỉ số từ lần đánh giá đầu chuỗi | L3: đánh giá lại phòng ngay trước bước tạo hóa đơn; không còn đạt thì dừng (có thể dừng thêm trường hợp trước đây vẫn tạo); email dùng chỉ số của lần đánh giá mới |
| T18 | `DienNuocService.SaveChotDienNuocAsync` + dispatcher mới ở Infrastructure | Email tạm tính gửi SMTP đồng bộ trong request chốt kỳ | L2: email tạm tính của lần chốt kỳ được gom và gửi nền sau khi request trả về; lỗi chỉ ghi Warning, không gửi lại. Nội dung email không đổi |
| T19 | `IThongBaoService` (dùng có sẵn) + `IEmailService`/`MailKitEmailService` | Không có | Sau điều chỉnh: báo khách (cổng + email) "hóa đơn đã được điều chỉnh" kèm tổng tiền mới, giờ tự chốt mới. Sau tự chốt: báo khách như công bố hiện tại (số tiền, hạn thanh toán, PDF, VietQR) |

ĐÃ XÁC NHẬN: S (không đổi schema) và T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, T19 (tiền) đúng như bảng trên; câu hỏi 1, 2, 3, 4 theo phương án đề xuất, riêng câu 4 thêm câu hướng dẫn vào thông báo `ThieuDonGia`: "Đơn giá của kỳ được lưu lúc tạo kỳ; lập hóa đơn theo luồng tay và sửa dòng điện/nước ở bản nháp." (10/10/2026 21:39, Asia/Bangkok)

---

## CÂU HỎI CÒN BỎ NGỎ

Mỗi câu có phương án đề xuất. Coder làm theo đề xuất trừ khi người dùng chọn khác trước khi bắt đầu.

1. **Phần Web của đợt B.** Spec 13 ghi đợt B gồm "7.3 (phần server)" nhưng không nói có endpoint HTTP cho "Điều chỉnh và gửi lại" hay không; tiêu chí 11.11 (nhân viên chi nhánh khác không điều chỉnh được) cần kiểm qua HTTP. Đề xuất: đợt B thêm 2 endpoint JSON trong `HoaDonController` (GET thông tin điều chỉnh: số hiện tại, có điều chỉnh được không, lý do khóa; POST điều chỉnh), không Razor/JS. Phương án khác: chỉ làm Application, endpoint để đợt C.
2. **Job tự chốt gặp hóa đơn không đạt kiểm lại.** Spec 5.3 ghi "bỏ qua, ghi Warning, báo nhân viên chi nhánh" nhưng job chạy 5 phút một lần, nên một hóa đơn lỗi kéo dài sẽ sinh thông báo mỗi 5 phút. Đề xuất: (a) hóa đơn không còn `ChoDuyet` hoặc hạn đã bị dời (vừa được điều chỉnh) là bình thường: chỉ log Information, không báo; (b) các lỗi còn lại (kỳ không còn `DaDuyet`, tổng tiền lệch, có hóa đơn hoạt động khác, không tìm được người gửi tạm tính, exception): Warning (exception: Error) và báo nhân viên + Admin MỘT lần cho mỗi cặp (hóa đơn, lý do) trong một lần chạy ứng dụng (bộ nhớ trong, khởi động lại thì có thể báo lại một lần).
3. **`ThieuDonGia` ngoài chuỗi tự tạo.** Quyết định L1 chỉ nói chuỗi tạo hóa đơn tạm tính. Điều chỉnh (mục 6) và job tự chốt có thể gặp kỳ có tiêu thụ mà đơn giá = 0 (ví dụ hóa đơn tạo từ đợt A trước khi có L1). Đề xuất: KHÔNG kiểm `ThieuDonGia` ở điều chỉnh và job (đúng phạm vi người dùng đã chốt). Nếu muốn chặn cả hai chỗ, người dùng nói rõ.
4. **Nội dung báo `ThieuDonGia` (cần biết).** Đơn giá được chụp vào kỳ (`DichVuDienNuocCuaPhong.DonGiaDien/DonGiaNuoc`) lúc tạo kỳ và không có chức năng nào cập nhật lại. Admin khai báo đơn giá chi nhánh sau đó thì kỳ đã tạo vẫn = 0, trang `ChotDienNuoc` trong link cũng không sửa được; nhân viên phải lập hóa đơn theo luồng tay và sửa dòng ở bản nháp. Đề xuất: thông báo nêu đúng chi nhánh, loại đơn giá, kỳ, link như quyết định L1, không thêm hướng dẫn. Người dùng có thể yêu cầu thêm câu "Đơn giá của kỳ được lưu lúc tạo kỳ; lập hóa đơn theo luồng tay và sửa dòng điện/nước ở bản nháp."

---

## 0. Trước khi sửa

1. `git status --short` sạch, đang ở `feature/hoa-don-tu-dong`.
2. Kiểm baseline không lệch model (không kết nối DB; KHÔNG trỏ DB phát triển/production):
   ```powershell
   $env:QLCTPT_DESIGNTIME_CONNECTION_STRING = "Host=localhost;Database=qlctpt_designtime_test;Username=x;Password=x"
   $p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
   dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
   ```
   Báo có thay đổi trước khi sửa gì: dừng, ghi vào `.bangiao/thay-doi.md`.
3. Đọc `.agents/rules/core_rules.md`, `.agents/rules/backend_code_review.md`.
4. Không thêm method vào `IInvoiceIssuanceStore`, `IMeterImageStore`, `IHoaDonStore`, `IInvoicePaymentStore`, `IInvoicePublicationStore` (nhiều fake riêng trong test sẽ vỡ). Truy vấn mới đặt ở `IAutoInvoiceStore`.

---

## 1. Domain: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/HoaDon.cs`

1. `GuiDuyet`: sau `TrangThaiPhatHanh = ChoDuyet;` thêm `HanPhanHoi = null;` (T15). Không đổi gì khác.
2. `KhoiTaoNhap(int nguoiTaoId, string lyDo = "Tạo hóa đơn nháp")`: `lyDo` rỗng/khoảng trắng → `ArgumentException`; dòng lịch sử dùng `lyDo`. Hành vi mặc định không đổi.
3. Method mới, đặt sau `GuiTamTinh`, copy cách ghi lịch sử của `GuiTamTinh`:
   ```csharp
   public void MoLaiDeDieuChinh(int nguoiThucHienId, DateTime nowUtc, string lyDo)
   ```
   - `nguoiThucHienId <= 0` hoặc `nowUtc.Kind != Utc` → `ArgumentOutOfRangeException`; `lyDo` rỗng → `ArgumentException`.
   - `TrangThaiPhatHanh != ChoDuyet` hoặc `HanPhanHoi == null` → `InvalidOperationException("Chỉ hóa đơn tạm tính mới có thể mở lại để điều chỉnh.")`.
   - Đặt `TrangThaiPhatHanh = Nhap`, `NgayCapNhat = nowUtc`; GIỮ NGUYÊN `HanPhanHoi` (bị `GuiTamTinh` ghi đè trong cùng transaction); một dòng lịch sử `ChoDuyet → Nhap`, trạng thái thanh toán giữ nguyên, `NgayThucHien = nowUtc`, `LyDo = lyDo`.
4. KHÔNG sửa `TraLai`, `ChotHoaDon`, `GuiHoaDon`, `HuyHoaDon`.

---

## 2. Application

### 2.1 L1 `ThieuDonGia`

- `Features/HoaDons/DTOs/AutoInvoiceDtos.cs`: thêm `ThieuDonGia` vào CUỐI enum `AutoInvoiceReason` (sau `LoiKhiTao`). Thêm vào `AutoInvoiceRunResult`: `public PendingProvisionalEmail? EmailChoGui { get; init; }` (mục 2.6).
- `Features/HoaDons/Persistence/IAutoInvoiceStore.cs`, `AutoInvoiceRoomContext` thêm: `public string TenChiNhanh { get; init; } = string.Empty;`, `public decimal DonGiaDien { get; init; }`, `public decimal DonGiaNuoc { get; init; }` (đơn giá của kỳ; 0 khi chưa có kỳ).
- `AutoInvoiceReadingPolicy.EvaluateRoom`: chèn bước mới SAU khối `ThieuChiSo` (bước 4) và TRƯỚC `KyTruocChuaChot`:
  - `thieuDien = (dien.ChiSoMoi - dien.ChiSoCu) > 0 && ctx.DonGiaDien <= 0`; `thieuNuoc` tương tự.
  - Có ít nhất một → `new AutoInvoiceRoomDecision(false, [ThieuDonGia], [moTa], dien, nuoc, null)` với `moTa = $"Chi nhánh {ctx.TenChiNhanh} chưa có đơn giá {loai} cho kỳ {Thang:00}/{Nam}"`, `loai` = "điện" | "nước" | "điện và nước".
  - `IsSilent(ThieuDonGia) == false`, `CanBaoKhach(ThieuDonGia) == false` (không sửa hai hàm, chỉ kiểm bằng test).
- `AutoInvoiceService`: không cần nhánh riêng. `BaoDungAsync` hiện có gửi `GuiChoNguoiPhuTrachPhongAsync` (nhân viên phụ trách + Admin) với link `/QuanLyNhaTro/ChotDienNuoc?thang={Thang}&nam={Nam}`; vòng báo khách chỉ xét lý do ở cấp đồng hồ nên khách không nhận.

### 2.2 L3 trong `Features/HoaDons/Services/AutoInvoiceService.cs`

Sau bước 2 (dù có gọi `ApprovePeriodsAsync` hay không), trước `CreateDraftsAsync`:
- `latest = await ReloadAsync(...)`; null → Information, `Done(DungImLang, [DaCoHoaDon])`.
- `decision = AutoInvoiceReadingPolicy.EvaluateRoom(latest, _options)`. Không đạt: toàn lý do im lặng → Information, `DungImLang`; còn lại → Warning + `BaoDungAsync(latest, decision.LyDos, decision.MoTaLyDos, decision)` → `DungCoThongBao`.
- Từ đây dùng `latest` thay `ctx` và `decision` mới (`HopDong`, chỉ số cho email) cho bước 3, 4, 5.

### 2.3 L2 trong `AutoInvoiceService`

- `IAutoInvoiceService.RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam, bool hoanGuiEmail = false)`.
- `BaoKhachTamTinhAsync`: dựng `ProvisionalInvoiceEmailData` như hiện tại. `hoanGuiEmail == false` → gửi như cũ. `true` → KHÔNG gọi `SendProvisionalInvoiceEmailAsync`, trả `new PendingProvisionalEmail(hoaDonId, ctx.EmailKhach!, data)` để đặt vào `AutoInvoiceRunResult.EmailChoGui`. Thông báo cổng vẫn gửi ngay.
- Trigger khác (`MeterImagePortalService`) giữ mặc định `false`.

### 2.4 `Features/DienNuocs/Services/DienNuocService.cs`

- Constructor thêm tham số cuối `IProvisionalInvoiceEmailDispatcher? emailDispatcher = null`.
- Trong vòng lặp hiện có: `RunForRoomAsync(actorId, ChotKy, phongTroId, Thang, Nam, hoanGuiEmail: _emailDispatcher != null)`; gom `result.Data?.EmailChoGui` khác null theo thứ tự phòng.
- Sau vòng lặp: danh sách có phần tử → `_emailDispatcher.Enqueue(danhSach)` (một lần). Vẫn trả `(true, string.Empty)`. Chốt thất bại → không gọi gì (như cũ).

### 2.5 `Abstractions/Services/IProvisionalInvoiceEmailDispatcher.cs` (mới) + `Features/Emails/DTOs/PendingProvisionalEmail.cs` (mới)

```csharp
public sealed record PendingProvisionalEmail(int HoaDonId, string ToEmail, ProvisionalInvoiceEmailData Data);

public interface IProvisionalInvoiceEmailDispatcher
{
    // Trả về ngay. Gửi ở nền trong DI scope riêng; không dùng DbContext/scoped service của request. Không ném.
    void Enqueue(IReadOnlyList<PendingProvisionalEmail> emails);
}
```

### 2.6 Email điều chỉnh

- `Features/Emails/DTOs/ProvisionalInvoiceEmailData.cs`: thêm `public bool LaDieuChinh { get; init; }`.
- Không thêm method vào `IEmailService` (dùng `SendProvisionalInvoiceEmailAsync`).

### 2.7 Lõi tạo nháp: `Features/HoaDons/Services/IInvoiceIssuanceService.cs` + `InvoiceIssuanceService.cs` (T13, T14)

- Tách `CreateDraftsAsync` thành 2 private, KHÔNG đổi logic, thứ tự, câu chữ:
  - `PrepareDraftsAsync(request, actorId, ct)`: toàn bộ phần trước `BeginTransactionAsync` (dòng 49–102 hiện tại: kiểm tham số, phòng, chi nhánh, quyền `InvoiceDraft`, `chiNhanh`, mốc tháng, `hopDongs`). Trả lỗi `ServiceResult` hoặc một record chuẩn bị (private).
  - `BuildDraftsAsync(prepared, actorId, string lyDoKhoiTao, ct)`: phần trong `try` từ khóa kỳ tới gán lại `HoaDonId` (dòng 109–362), gọi `hoaDon.KhoiTaoNhap(actorId, lyDoKhoiTao)`; trả `InvoiceDraftBatchResult`. Không Begin/Commit/Rollback.
  - `CreateDraftsAsync` = Prepare → Begin → Build(`"Tạo hóa đơn nháp"`) → Commit; `catch` giữ nguyên rollback + `LogError` + câu lỗi hiện tại.
- Method mới trên interface:
  ```csharp
  // Lõi của CreateDraftsAsync chạy trong transaction do caller mở: không Begin/Commit/Rollback, có SaveChanges.
  // Kiểm quyền Invoice.Draft như CreateDraftsAsync. Exception ném ra cho caller rollback.
  Task<ServiceResult<InvoiceDraftBatchResult>> CreateDraftsInCurrentTransactionAsync(CreateInvoiceDraftsRequest request, int actorId, string? lyDoKhoiTao = null, CancellationToken ct = default);
  ```
  = Prepare → Build(`lyDoKhoiTao ?? "Tạo hóa đơn nháp"`).
- `FinalizeAsync` và `RejectAsync`: ngay sau kiểm `TrangThaiPhatHanh != ChoDuyet` trong transaction, thêm: `hoaDon.HanPhanHoi != null` → `RollbackAsync`, `Fail("Hóa đơn đang tự chốt theo hạn, dùng Điều chỉnh và gửi lại.")`. Kiểm quyền vẫn đứng trước.
- `SubmitAsync` không sửa (xóa hạn nằm ở `GuiDuyet`).

### 2.8 Lõi đổi chỉ số: `Features/DienNuocs/Services/IMeterReadingWorkflowService.cs` + `MeterReadingWorkflowService.cs` (T16)

DTO mới trong `Features/DienNuocs/DTOs/` (file mới `MeterAdjustmentDtos.cs`):
```csharp
public sealed class MeterAdjustmentRequest { public int DichVuDienNuocCuaPhongId { get; init; } public decimal ChiSoDienMoi { get; init; } public decimal ChiSoNuocMoi { get; init; } public string LyDo { get; init; } = string.Empty; }
public sealed class MeterAdjustmentResult { public decimal ChiSoDienCu { get; init; } public decimal ChiSoDienMoi { get; init; } public decimal ChiSoNuocCu { get; init; } public decimal ChiSoNuocMoi { get; init; } }
```
```csharp
// Đổi chỉ số kỳ đã DaDuyet cho "Điều chỉnh và gửi lại". Chạy trong transaction do caller mở: không Begin/Commit/Rollback, có SaveChanges.
// Caller phải đã đưa hóa đơn của kỳ về Nhap hoặc hủy, và đã SaveChanges, trước khi gọi.
Task<ServiceResult<MeterAdjustmentResult>> AdjustReadingsInCurrentTransactionAsync(MeterAdjustmentRequest request, int actorUserId, CancellationToken cancellationToken = default);
```
Thân (dùng lại private helper sẵn có, KHÔNG sửa `CorrectConfirmedImageAsync`/`ApprovePeriodsAsync`; đây là cách hiểu "tách lõi" của spec 6.2: lõi dùng chung là domain method + `RecalculateDraftInvoices` + `CascadeSubsequentPeriodsAsync`, hành vi công khai của hai method cũ giữ nguyên):
1. Kiểm như `CorrectConfirmedImageAsync`: request null, id ≤ 0, số âm, > 3 chữ số thập phân, `LyDo` rỗng hoặc > 500 → `Fail` (câu chữ copy từ `CorrectConfirmedImageAsync`).
2. `period = _store.GetPeriodByIdForUpdateAsync(id)`; null/`IsDeleted` → `Fail("Không tìm thấy dữ liệu kỳ của hóa đơn.")`.
3. `branch = _store.GetBranchIdByRoomAsync(period.PhongTroId)`; `CanPerformAsync(actor, branch, MeterReview)` sai → `Forbidden("Bạn không có quyền sửa chỉ số cho chi nhánh này.")`.
4. `period.TrangThaiGhiNhan != DaDuyet` → `Fail("Kỳ chỉ số điện nước liên kết chưa được chốt.")`.
5. `HasSubsequentPeriodAsync` → `Fail("Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh.")`; `HasLockedInvoiceAsync` → `Fail("Kỳ này đã có hóa đơn không còn ở trạng thái nháp, không thể chỉnh sửa.")`.
6. Mới < cũ → `Fail("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.")` / nước tương tự.
7. Ảnh: `GetImagesForUpdateAsync(id, Dien)` và `(id, Nuoc)`. Với từng đồng hồ có số mới ≠ `period.ChiSo{X}Moi`: có ảnh `DuocChonLamChiSoChinhThuc && !IsDeleted` → `img.SuaGiaTriXacNhan(moi, actor, lyDo)`; không có → thêm phần ghi chú `[Điện thủ công: {lyDo}]` / `[Nước thủ công: {lyDo}]` (cách ghép, chống lặp copy `ApprovePeriodsAsync` dòng 1079–1111).
8. `period.CapNhatChiSo(dien, nuoc)`; `period.DuyetLai(actor, fullNote)`.
9. `GetInvoicesByMeterReadingIdsAsync([id])` → `RecalculateDraftInvoices(period, linked)`.
10. `CascadeSubsequentPeriodsAsync(period)` lỗi → `Fail(lỗi)`.
11. `SaveChangesAsync`; trả `Ok(result)` với số cũ/mới.
- KHÔNG kiểm tiêu thụ bất thường (spec 6.2). Exception để ném cho caller.

### 2.9 Truy vấn mới: `Features/HoaDons/Persistence/IAutoInvoiceStore.cs`

```csharp
public sealed record AutoInvoiceDueItem(int HoaDonId, string MaHoaDon, int PhongTroId, DateTime HanPhanHoiUtc);

public sealed class AutoInvoiceAdjustmentSnapshot
{
    public int HoaDonId { get; init; } public string MaHoaDon { get; init; } = string.Empty;
    public int HopDongId { get; init; } public int ChiNhanhId { get; init; } public int PhongTroId { get; init; }
    public int Thang { get; init; } public int Nam { get; init; }
    public TrangThaiPhatHanhHoaDon TrangThaiPhatHanh { get; init; } public TrangThaiHoaDon TrangThaiHoaDon { get; init; }
    public DateTime? HanPhanHoi { get; init; } public int? DichVuDienNuocCuaPhongId { get; init; }
    public decimal ChiSoDienCu { get; init; } public decimal ChiSoDienMoi { get; init; } public decimal ChiSoNuocCu { get; init; } public decimal ChiSoNuocMoi { get; init; }
}

// AsNoTracking. ChoDuyet, !IsDeleted, HanPhanHoi != null && HanPhanHoi <= nowUtc; sắp HanPhanHoi rồi HoaDonId tăng dần.
Task<IReadOnlyList<AutoInvoiceDueItem>> GetDueProvisionalInvoicesAsync(DateTime nowUtc, CancellationToken ct = default);
// NguoiThucHienId của dòng lịch sử mới nhất (NgayThucHien rồi LichSuTrangThaiHoaDonId giảm dần) có TrangThaiPhatHanhMoi == ChoDuyet && TrangThaiPhatHanhCu != ChoDuyet; null nếu không có.
Task<int?> GetLastProvisionalSubmitterIdAsync(int hoaDonId, CancellationToken ct = default);
// AsNoTracking, hóa đơn !IsDeleted; chỉ số lấy từ kỳ liên kết (0 nếu không có kỳ); null nếu không có hóa đơn.
Task<AutoInvoiceAdjustmentSnapshot?> GetAdjustmentSnapshotAsync(int hoaDonId, CancellationToken ct = default);
```

### 2.10 Điều chỉnh: `Features/HoaDons/Services/IInvoiceAdjustmentService.cs` + `InvoiceAdjustmentService.cs` (mới; T11, T12, T19)

DTO trong `Features/HoaDons/DTOs/InvoiceAdjustmentDtos.cs` (mới; KHÔNG dùng enum Domain vì Web đọc trực tiếp):
```csharp
public sealed class AdjustInvoiceRequest { public int HoaDonId { get; init; } public decimal ChiSoDienMoi { get; init; } public decimal ChiSoNuocMoi { get; init; } }
public sealed class InvoiceAdjustmentInfo { public int HoaDonId { get; init; } public string MaHoaDon { get; init; } = ""; public bool CoTheDieuChinh { get; init; } public string? LyDoKhoa { get; init; } public decimal ChiSoDienCu { get; init; } public decimal ChiSoDienMoi { get; init; } public decimal ChiSoNuocCu { get; init; } public decimal ChiSoNuocMoi { get; init; } public DateTime? HanPhanHoiUtc { get; init; } }
public sealed class InvoiceAdjustmentResult { public int HoaDonIdCu { get; init; } public int HoaDonIdMoi { get; init; } public string MaHoaDonMoi { get; init; } = ""; public decimal TongTienMoi { get; init; } public DateTime HanPhanHoiUtc { get; init; } public bool DaTaoHoaDonThayThe { get; init; } }
```
```csharp
public interface IInvoiceAdjustmentService
{
    Task<ServiceResult<InvoiceAdjustmentInfo>> GetAdjustmentInfoAsync(int actorId, int hoaDonId, CancellationToken ct = default);
    Task<ServiceResult<InvoiceAdjustmentResult>> AdjustAsync(int actorId, AdjustInvoiceRequest request, CancellationToken ct = default);
}
```
Phụ thuộc: `IAutoInvoiceStore`, `IInvoiceIssuanceStore`, `IInvoiceIssuanceService`, `IMeterReadingWorkflowService`, `IMeterImageStore` (chỉ `HasSubsequentPeriodAsync`), `IInvoicePaymentStore`, `IInvoicePublicationStore` (chỉ `GetRecipientAsync`), `IHoaDonStore` (`GetChiNhanhByIdAsync`, `GetHoaDonDetailByIdAsync`), `IUnitOfWork`, `IEmployeeAccessService`, `IThongBaoService`, `IEmailService`, `ILogger<InvoiceAdjustmentService>`, `TimeProvider? timeProvider = null` (cuối).

Hằng: `LyDoDieuChinh = "Điều chỉnh hóa đơn theo yêu cầu khách thuê"`; nhãn lịch sử bằng `AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.DieuChinh, ...)`.

**Lý do khóa** (private dùng chung cho Info và Adjust, xét theo thứ tự, trả câu đầu tiên):
1. `HanPhanHoi == null` → "Chỉ hóa đơn tự động mới dùng được Điều chỉnh và gửi lại."
2. Trạng thái khác `ChoDuyet`/`DaGui` → "Hóa đơn không ở trạng thái điều chỉnh được."
3. Thiếu quyền: `MeterReview` và `InvoiceSubmit`, thêm `InvoiceCancel` khi `DaGui` → "Bạn không có quyền điều chỉnh hóa đơn tại chi nhánh này."
4. `DichVuDienNuocCuaPhongId == null` → "Hóa đơn không gắn kỳ chỉ số điện nước."
5. `HasSubsequentPeriodAsync(PhongTroId, Thang, Nam)` → "Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh."
6. `DaGui` và (`GetTongTienDaThanhToanHoaDonAsync > 0` hoặc `TrangThaiHoaDon != ChuaThanhToan`) → "Hóa đơn đã có khoản thu"
7. `DaGui` và `GetActiveRequestAsync(id, now)?.TrangThai == DangDoiChieu` → "Khách đã nộp minh chứng, cần đối chiếu trước"

**GetAdjustmentInfoAsync**: `actorId <= 0` → Fail. `snap = GetAdjustmentSnapshotAsync`; null → `NotFound("Không tìm thấy hóa đơn.")`. `CanPerformAsync(actorId, snap.ChiNhanhId, InvoiceRead)` sai → `Forbidden("Bạn không có quyền xem hóa đơn tại chi nhánh này.")` (kiểm trước mọi dữ liệu khác). Trả `Ok` với số hiện tại, `CoTheDieuChinh = lyDoKhoa == null`, `LyDoKhoa`. Cuối method `_autoStore.DiscardPendingChanges()`.

**AdjustAsync**:
1. `actorId <= 0`, request null, `HoaDonId <= 0`, số âm, > 3 chữ số thập phân → `Fail`.
2. `snap` null → `NotFound`. `CanPerformAsync(actor, snap.ChiNhanhId, MeterReview)` và `InvoiceSubmit` → thiếu → `Forbidden("Bạn không có quyền điều chỉnh hóa đơn tại chi nhánh này.")`. `snap.DichVuDienNuocCuaPhongId == null` → Fail.
3. `now = _time.GetUtcNow().UtcDateTime`. `BeginTransactionAsync`. Khóa theo thứ tự **kỳ → hợp đồng → hóa đơn**: `LockMeterPeriodAsync(periodId)`, `LockContractsAsync([snap.HopDongId])`, `hoaDon = GetInvoiceForUpdateAsync(id)` (null → `NotFound`).
4. Tính lại lý do khóa 1–7 trên dữ liệu đã khóa (`hoaDon`, `hoaDon.DichVuDienNuocCuaPhong`, quyền `InvoiceCancel` khi `DaGui`). Có lý do → rollback, `Fail(lyDo)` (lý do 3 trả `Forbidden`).
5. Số mới < cũ → Fail (câu như 2.8 bước 6). Số mới == số hiện tại ở CẢ HAI đồng hồ → rollback, `Fail("Không có thay đổi")`.
6. **ChoDuyet (6.2):** `hoaDon.MoLaiDeDieuChinh(actor, now, TaoLyDo(DieuChinh, LyDoDieuChinh))` → `SaveChangesAsync` → `AdjustReadingsInCurrentTransactionAsync(new { periodId, dien, nuoc, LyDo = LyDoDieuChinh }, actor)`; thất bại → rollback, `FailFrom(kết quả)`. Hóa đơn nháp được tính lại tại chỗ (cùng instance đang tracking). Kiểm `TongTien > 0` và bằng tổng chi tiết chưa xóa, sai → rollback Fail "Tổng tiền hóa đơn không khớp với tổng chi tiết dịch vụ.". `hoaDonGui = hoaDon`.
7. **DaGui (6.3):** `GetExpiredWaitingRequestsAsync(id, now)` → `DanhDauHetHan(now)`; `active = GetActiveRequestAsync(id, now)`; `active?.TrangThai == ChoThanhToan` → `active.Huy(actor, now, $"Hóa đơn bị hủy: {lyDoTag}")`; `hoaDon.HuyHoaDon(actor, lyDoTag, now)` với `lyDoTag = TaoLyDo(DieuChinh, LyDoDieuChinh)` → `SaveChangesAsync` → `AdjustReadingsInCurrentTransactionAsync` (thất bại → rollback) → `CreateDraftsInCurrentTransactionAsync(new CreateInvoiceDraftsRequest { ChiNhanhId = snap.ChiNhanhId, Thang, Nam, PhongTroIds = [PhongTroId] }, actor, TaoLyDo(DieuChinh, "Tạo hóa đơn thay thế sau điều chỉnh"))`. `!Success` → rollback, `FailFrom`. Lấy các item `Created`: phải có đúng 1 và `HopDongId == hoaDon.HopDongId`, nếu không → rollback, `Fail("Không tạo được hóa đơn thay thế: " + message của item hợp đồng, hoặc "phòng có nhiều hợp đồng trong kỳ, xử lý theo luồng tay")`. `hoaDonGui = GetInvoiceForUpdateAsync(item.HoaDonId)`; kiểm tổng tiền như 6.
8. `chiNhanh = _hoaDonStore.GetChiNhanhByIdAsync(snap.ChiNhanhId)`; null hoặc `SoGioPhanHoiHoaDon` ngoài 1–168 → rollback Fail. `han = now.AddHours(SoGio)`; `hoaDonGui.GuiTamTinh(actor, now, han, TaoLyDo(DieuChinh, "Gửi lại hóa đơn tạm tính sau điều chỉnh"))`; `SaveChangesAsync`; `CommitAsync`.
9. `catch (UniqueConstraintViolationException)` → rollback, `LogInformation`, `Fail("Dữ liệu vừa thay đổi, vui lòng thử lại.")`. `catch (OperationCanceledException)` → rollback, ném lại. `catch (Exception)` → rollback, `LogError`, `Fail("Không thể điều chỉnh hóa đơn. Vui lòng thử lại.")`. Rollback dùng `CancellationToken.None` trong try/catch riêng (copy `TryRollbackAsync` của `InvoicePublicationService`). MỌI nhánh thất bại sau khi đã khóa: gọi `_autoStore.DiscardPendingChanges()` trước khi trả.
10. Sau commit: `DiscardPendingChanges()`; báo khách (T19), mỗi kênh try/catch riêng, lỗi/thất bại → Warning, không đổi kết quả:
   - `recipient = _publicationStore.GetRecipientAsync(hoaDon.HopDongId)`; `detail = GetHoaDonDetailByIdAsync(hoaDonGui.HoaDonId)`.
   - Cổng: `GuiChoKhachThueAsync(recipient.NguoiThueId, $"Hóa đơn tháng {T}/{N} đã được điều chỉnh", $"Hóa đơn {Ma} tháng {T}/{N} đã được điều chỉnh: tổng tiền mới {TongTien:#,##0 InvariantCulture} đ (tạm tính). Hệ thống tự chốt lúc {han giờ VN dd/MM/yyyy HH:mm}. Nếu có sai sót, vui lòng liên hệ {SoDienThoaiChiNhanh}.", "HoaDon", $"/KhachThue/HoaDon?hoaDonId={idMoi}")`.
   - Email (có `recipient.Email`): `SendProvisionalInvoiceEmailAsync` với data như `AutoInvoiceService.BaoKhachTamTinhAsync` (chỉ số từ `MeterAdjustmentResult`), `LaDieuChinh = true`, đồng bộ (một hóa đơn).
11. Trả `Ok(result, "Đã điều chỉnh và gửi lại hóa đơn tạm tính.")`.

### 2.11 Job tự chốt: `Features/HoaDons/UseCases/IAutoInvoiceFinalizeUseCase.cs` + `AutoInvoiceFinalizeUseCase.cs` (mới; T10)

```csharp
public interface IAutoInvoiceFinalizeUseCase { Task<int> ExecuteAsync(CancellationToken cancellationToken = default); } // số hóa đơn đã tự chốt
```
Guard (câu hỏi 2) `Features/HoaDons/AutoInvoices/AutoInvoiceFinalizeNotifyGuard.cs` (mới, singleton, `ConcurrentDictionary<int,string>`): `bool ShouldNotify(int hoaDonId, string lyDo)` (true khi chưa báo đúng lý do này cho hóa đơn này), `void Forget(int hoaDonId)`.

Phụ thuộc: `IAutoInvoiceStore`, `IInvoiceIssuanceStore`, `IInvoicePublicationStore` (`GetRecipientAsync`), `IHoaDonStore` (`GetHoaDonDetailByIdAsync`), `IUnitOfWork`, `IThongBaoService`, `IEmailService`, `IInvoiceDocumentExporter`, `IVietQRService`, `VietQrSettings`, `InvoiceIssuanceOptions`, `AutoInvoiceFinalizeNotifyGuard`, `ILogger<AutoInvoiceFinalizeUseCase>`, `TimeProvider? timeProvider = null`. KHÔNG phụ thuộc `IEmployeeAccessService` (spec 5.3: không kiểm quyền).

`ExecuteAsync`: `DiscardPendingChanges()`; `due = GetDueProvisionalInvoicesAsync(now)`; có phần tử → Information số lượng. Duyệt tuần tự (dừng khi `ct` bị hủy), mỗi hóa đơn gọi `FinalizeOneAsync`, lỗi một hóa đơn không dừng vòng. Trả số đã chốt.

`FinalizeOneAsync(item)` (copy cách khóa của `FinalizeAsync`, cách công bố của `InvoicePublicationService.PublishOneInTransactionAsync` + `SendInvoiceEmailCoreAsync`):
1. `DiscardPendingChanges()`; `now = _time.GetUtcNow().UtcDateTime`; `targets = GetInvoiceLockTargetsAsync(id)` null → Information, bỏ qua.
2. Begin; `status = LockMeterPeriodAsync(periodId)` (nếu có); `hoaDon = GetInvoiceForUpdateAsync(id)`.
3. Kiểm lại, sai thì rollback:
   - `hoaDon == null`, khác `ChoDuyet`, `HanPhanHoi == null` hoặc `HanPhanHoi > now` → Information, không báo (câu hỏi 2a).
   - `periodId == null || status != DaDuyet` → "Kỳ chỉ số điện nước liên kết không còn ở trạng thái đã chốt".
   - Không có chi tiết, `TongTien <= 0` hoặc lệch tổng chi tiết chưa xóa → "Tổng tiền hóa đơn không khớp với tổng chi tiết dịch vụ".
   - `HasOtherActiveInvoiceAsync` → "Đã tồn tại hóa đơn đang hoạt động khác cho hợp đồng trong kỳ này".
   - `actor = GetLastProvisionalSubmitterIdAsync(id)`; null/≤ 0 → "Không xác định được người gửi hóa đơn tạm tính".
   - Các lý do trên: Warning + `BaoNhanVienAsync` (dưới).
4. `hoaDon.ChotHoaDon(actor, TaoLyDo(TuDong, "Tự chốt khi hết hạn phản hồi"))`; `hoaDon.GuiHoaDon(actor, now, InvoiceDueDateCalculator.Compute(now, _issuanceOptions.SoNgayHanThanhToan), TaoLyDo(TuDong, "Tự công bố hóa đơn lên cổng khách thuê"))`; `SaveChangesAsync`; `CommitAsync`; `guard.Forget(id)`.
5. `catch (OperationCanceledException)` → rollback, ném lại. `catch (Exception)` → rollback, `LogError` (một lần cho hóa đơn đó), `DiscardPendingChanges()`, `BaoNhanVienAsync(item, "Lỗi hệ thống khi tự chốt")`, trả "không chốt".
6. Sau commit (mỗi kênh try/catch riêng, lỗi → Warning, không hoàn tác): `DiscardPendingChanges()`; `recipient = GetRecipientAsync(hoaDon.HopDongId)`;
   - Cổng: `GuiChoKhachThueAsync(recipient.NguoiThueId, $"Hóa đơn tháng {T}/{N}", <nội dung copy dòng 278 `InvoicePublicationService`>, "HoaDon", $"/KhachThue/HoaDon?hoaDonId={id}")`.
   - Email: có email → `detail = GetHoaDonDetailByIdAsync(id)`, `pdf = InvoicePdfComposer.Compose(detail, _vietQR, _vietQrSettings, _exporter)`, `SendInvoiceEmailAsync(email, detail, pdf)`; `pdf` rỗng hoặc `!IsSuccess` → Warning.
7. Information "Đã tự chốt hóa đơn {HoaDonId}".

`BaoNhanVienAsync(item, lyDo)`: `guard.ShouldNotify(item.HoaDonId, lyDo)` sai → bỏ qua. `DiscardPendingChanges()` rồi `GuiChoNguoiPhuTrachPhongAsync(item.PhongTroId, $"Hóa đơn {item.MaHoaDon}: chưa tự chốt được", $"Lý do: {lyDo}. Hóa đơn vẫn ở trạng thái tạm tính, vui lòng kiểm tra và dùng Điều chỉnh và gửi lại.", "HoaDon", "/QuanLyNhaTro/QuanLyHoaDon")` trong try/catch (lỗi → Warning).

### 2.12 `Application/DependencyInjection.cs`

- Khối "Phase 6.4": `services.AddScoped<IInvoiceAdjustmentService, InvoiceAdjustmentService>();`
- Khối "Phase 8": `services.AddScoped<Features.HoaDons.UseCases.IAutoInvoiceFinalizeUseCase, Features.HoaDons.UseCases.AutoInvoiceFinalizeUseCase>();` và `services.AddSingleton<Features.HoaDons.AutoInvoices.AutoInvoiceFinalizeNotifyGuard>();`

### 2.13 L7: `Features/HoaDons/Persistence/InvoicePaymentSnapshots.cs`

Chỉ sửa comment dòng 22 thành: `// Quy tắc vào luồng thanh toán: hóa đơn đã gửi hoặc đã hủy sau khi gửi. Hẹp hơn IHoaDonStore.CheckHoaDonOwnershipAsync (không gồm hóa đơn tạm tính ChoDuyet có HanPhanHoi) để hóa đơn tạm tính không vào được màn thanh toán.` Không đổi code.

---

## 3. Infrastructure

### 3.1 `Persistence/Features/AutoInvoiceStore.cs`
- `GetRoomContextAsync`: lấy thêm `TenChiNhanh = p.ChiNhanh.TenChiNhanh` (kiểm tên thuộc tính thật trong `Domain/Entities/ChiNhanh.cs`) và `DonGiaDien`, `DonGiaNuoc` từ kỳ (0 khi không có kỳ).
- Ba method mới theo chú thích ở 2.9, toàn bộ `AsNoTracking`. `GetDueProvisionalInvoicesAsync` lọc đúng điều kiện của index (`HanPhanHoi != null && !IsDeleted`) cộng `TrangThaiPhatHanh == ChoDuyet && HanPhanHoi <= nowUtc`; `PhongTroId = h.HopDong.PhongTroId`. `GetAdjustmentSnapshotAsync`: `ChiNhanhId = h.HopDong.PhongTro.ChiNhanhId`.

### 3.2 `ExternalServices/Emails/BackgroundProvisionalInvoiceEmailDispatcher.cs` (mới, `IProvisionalInvoiceEmailDispatcher`)
- Phụ thuộc `IServiceScopeFactory`, `ILogger<BackgroundProvisionalInvoiceEmailDispatcher>`. Đăng ký **singleton**.
- `Enqueue`: null/rỗng → return. Copy danh sách (`ToArray()`), `_ = Task.Run(() => SendAllAsync(copy));`, return ngay.
- `SendAllAsync`: toàn bộ trong `try/catch (Exception)` → `LogWarning`. `await using var scope = _scopeFactory.CreateAsyncScope();` `var email = scope.ServiceProvider.GetRequiredService<IEmailService>();` Với từng phần tử: try `SendProvisionalInvoiceEmailAsync(e.ToEmail, e.Data)`; `!IsSuccess` hoặc exception → `LogWarning` kèm `HoaDonId`. Cuối: Information số đã gửi. KHÔNG dùng `LogError`, không resolve DbContext/store.

### 3.3 `ExternalServices/Emails/MailKitEmailService.cs`
`SendProvisionalInvoiceEmailAsync`: khi `data.LaDieuChinh`: tiêu đề `$"[HÓA ĐƠN ĐÃ ĐIỀU CHỈNH] - Phòng {TenPhong} - Kỳ tháng {Thang}/{Nam}"`, thẻ h2 "Hóa đơn tạm tính đã được điều chỉnh", câu mở đầu "Hóa đơn tạm tính của bạn đã được điều chỉnh. Hóa đơn chưa phải bản chính thức và chưa cần thanh toán.". Phần còn lại giữ nguyên. Không sửa `ServerCertificateValidationCallback` (L8 ngoài phạm vi).

### 3.4 `BackgroundJobs/AutoInvoiceFinalizeJob.cs` (mới)
Copy `InvoiceReminderJob.cs`. Phụ thuộc `IServiceScopeFactory`, `AutoInvoiceOptions`, `ILogger<AutoInvoiceFinalizeJob>`. Mỗi vòng: scope mới, `GetRequiredService<IAutoInvoiceFinalizeUseCase>().ExecuteAsync(stoppingToken)`; chờ `TimeSpan.FromMinutes(options.ChuKyJobPhut)`. Log Information chứa đúng các cụm "đã khởi động", "sẽ chạy lại", "đang dừng" (test smoke so chuỗi). Chỉ lập lịch, không nghiệp vụ.

### 3.5 `DependencyInjection.cs`
- `services.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IProvisionalInvoiceEmailDispatcher, ExternalServices.Emails.BackgroundProvisionalInvoiceEmailDispatcher>();` cạnh `IEmailService` (KHÔNG phụ thuộc `BackgroundJobs:Enabled`).
- Trong khối `if (backgroundJobsEnabled)`: `services.AddHostedService<BackgroundJobs.AutoInvoiceFinalizeJob>();`

---

## 4. Web (theo câu hỏi 1, phương án đề xuất)

`src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/HoaDonController.cs`:
- Constructor thêm `IInvoiceAdjustmentService adjustmentService`.
- `[HttpGet("/HoaDon/DieuChinh/{id:int}")] DieuChinhThongTin(int id, CancellationToken ct)`: `TryGetActorId` → `GetAdjustmentInfoAsync(actorId, id, ct)` → `Ok(result.Data)` hoặc `this.ToErrorResult(result)`.
- `[HttpPost("/HoaDon/DieuChinh/{id:int}")] [ValidateAntiForgeryToken] DieuChinh(int id, [FromBody] DieuChinhHoaDonReq? req, CancellationToken ct)`: `id <= 0` hoặc `req == null` → `BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." })`; gọi `AdjustAsync`; lỗi → `this.ToErrorResult(result)`; thành công → `Ok(new { success = true, message = result.Message, data = new { hoaDonId = d.HoaDonIdMoi, maHoaDon = d.MaHoaDonMoi, tongTien = d.TongTienMoi, hanTuChot = d.HanPhanHoiUtc.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), daTaoHoaDonThayThe = d.DaTaoHoaDonThayThe } })`.
- Class cuối file: `public class DieuChinhHoaDonReq { public decimal ChiSoDienMoi { get; set; } public decimal ChiSoNuocMoi { get; set; } }` (cạnh `RejectInvoiceReq`).
- KHÔNG sửa `Chot`, `TraLai` (chặn nằm ở service), `Program.cs`, view, JS, `appsettings*` (khóa `ChuKyJobPhut` đã có). Web không tham chiếu Domain.

---

## 5. Trường hợp biên bắt buộc

- **Thời gian:** mọi mốc lấy từ `TimeProvider` (UTC); so `HanPhanHoi <= now` ở UTC; chỉ đổi giờ VN khi tạo nội dung thông báo/email/JSON.
- **Số giờ chi nhánh:** điều chỉnh đọc số giờ HIỆN TẠI của chi nhánh trong transaction; job không dời hạn.
- **Xóa mềm:** hóa đơn `IsDeleted` không vào job, không điều chỉnh được (`GetInvoiceForUpdateAsync`, `GetInvoiceLockTargetsAsync` đã lọc); tiền đã thu chỉ tính `LichSuThanhToan` chưa xóa; ảnh/kỳ xóa mềm bị loại; hóa đơn cũ sau 6.3 vẫn hiện cho khách là "Đã hủy" (quy tắc sẵn có).
- **Phân quyền:** điều chỉnh fail closed qua `IEmployeeAccessService`, kiểm trước khi trả dữ liệu; nhân viên chi nhánh khác → `Forbidden`; khách thuê không vào được (`AdminBaseController`). Job không kiểm quyền. Admin chốt/trả lại tay hóa đơn tạm tính bị từ chối; hóa đơn `ChoDuyet` tay (`HanPhanHoi == null`) vẫn chốt/trả lại như cũ.
- **Nguyên tử:** 6.2 và 6.3 trong MỘT transaction; mọi thất bại rollback và `DiscardPendingChanges()` trước khi gửi bất kỳ thông báo nào (`ThongBaoService` gọi `SaveChanges` trên DbContext dùng chung). Lưu (`SaveChangesAsync`) sau khi đổi trạng thái hóa đơn và TRƯỚC khi gọi lõi chỉ số, vì `HasLockedInvoiceAsync`, `CountCancelledInvoicesAsync`, unique index (`HopDongId`,`Thang`,`Nam`) đọc DB.
- **Thứ tự khóa:** job: kỳ → hóa đơn. Điều chỉnh: kỳ → hợp đồng → hóa đơn (khớp `CreateDraftsAsync` kỳ → hợp đồng). Deadlock nếu xảy ra là exception → rollback → báo lỗi, không hỏng dữ liệu.
- **Song song job và điều chỉnh:** bên sau chờ khóa rồi kiểm lại trạng thái/hạn; job bỏ qua im lặng, điều chỉnh trả lý do khóa.
- **Không đổi kết quả thao tác gốc:** `RunForRoomAsync` vẫn không ném; `SaveChotDienNuocAsync` vẫn trả `(true, "")` khi chốt thành công dù gửi email nền lỗi.
- **Log:** lý do lường trước ghi Information/Warning; Error chỉ ở exception ngoài dự kiến. Dispatcher nền không bao giờ ghi Error.
- **Biết trước, không sửa:** `RecalculateDraftInvoices` gọi `UpdateHoaDon` đánh dấu cả đồ thị đang tracking (có sẵn ở các luồng xác nhận/sửa số).

---

## 6. Test

Quy ước: Domain theo `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/InvoiceWorkflowTests.cs`; Application theo `AutoInvoiceServiceTests.cs`, `InvoiceDraftWorkflowTests.cs`, `InvoiceCancellationTests.cs`, `MeterReadingCorrectionTests.cs`, fake dùng chung trong `Fakes/` (`FakeAutoInvoiceStore`, `FakeInvoiceIssuanceService`, `FakeMeterReadingWorkflowService`, `FakeThongBaoService`, `RecordingEmailService`, `FakeEmployeeAccessService`, `FakeInvoicePaymentStore`, `FakeMeterImageStore`, `FixedTimeProvider`, `CapturingLogger`, `RecordingUnitOfWork` trong `PaymentTestHarness.cs`); Infrastructure theo `Persistence/AutoInvoiceChainTests.cs`, `Persistence/InvoiceDraftConcurrencyTests.cs` (`[Collection("PostgreSqlCollection")]`, service thật, DbContext riêng); Web theo `AutoInvoiceTriggerTests.cs`, `InvoiceLifecycleTests.cs`, `EmployeeBranchAuthorizationTests.cs` (`[Collection(WebTestCollection.Name)]`).

L5: test Web mới tải ảnh phải đặt `FakeMeterOcrServiceInstance.DefaultSuggestedValue`/`DefaultConfidence` rõ ràng và `Reset()` trong `finally`; đếm `ProvisionalEmailCalls`/`InvoiceEmailCalls` thì lọc theo mã hóa đơn/email của test (hàng đợi dùng chung).

Cập nhật fake: `FakeAutoInvoiceStore` (3 method mới + 3 trường ctx), `FakeAutoInvoiceService` (tham số `hoanGuiEmail`, trả `EmailChoGui` cấu hình được), `FakeInvoiceIssuanceService` (`CreateDraftsInCurrentTransactionAsync`, ghi lại lời gọi), `FakeMeterReadingWorkflowService` (`AdjustReadingsInCurrentTransactionAsync`, ghi lại lời gọi, kết quả cấu hình được). Chỉ sửa test cũ khi hành vi mới đúng spec/quyết định; liệt kê từng test và lý do trong `.bangiao/thay-doi.md`.

### 6.1 Domain.UnitTests: `InvoiceWorkflowTests.cs`
`GuiDuyet` xóa `HanPhanHoi`; `MoLaiDeDieuChinh` từ `ChoDuyet` có hạn → `Nhap`, một dòng lịch sử đúng người/giờ/lý do, `HanPhanHoi` giữ nguyên; ném khi không có hạn, khi ở `Nhap`/`DaChot`/`DaGui`/`DaHuy`, người ≤ 0, lý do rỗng, `Kind` không UTC; `KhoiTaoNhap` với lý do tùy chọn và mặc định "Tạo hóa đơn nháp", lý do rỗng ném.

### 6.2 Application.UnitTests
- `AutoInvoiceReadingPolicyTests.cs`: tiêu thụ điện > 0 và `DonGiaDien = 0` → `ThieuDonGia`, mô tả có tên chi nhánh và "điện"; chỉ nước; cả hai → một lý do, mô tả "điện và nước"; tiêu thụ 0 và đơn giá 0 → đạt; `ThieuChiSo` thắng `ThieuDonGia`; `ThieuDonGia` thắng `KyTruocChuaChot`; `IsSilent`/`CanBaoKhach` đều false.
- `AutoInvoiceServiceTests.cs`: `ThieuDonGia` → báo nhân viên đúng link `/QuanLyNhaTro/ChotDienNuoc?thang=..&nam=..`, không báo khách, không gọi workflow/issuance; L3: ctx đọc sau bước 2 không còn đạt → dừng đúng loại (im lặng/thông báo), không gọi `CreateDraftsAsync`; ctx sau bước 2 có chỉ số khác → email dùng chỉ số mới; `hoanGuiEmail = true` → không gọi `SendProvisionalInvoiceEmailAsync`, `EmailChoGui` có đúng email/mã/tổng tiền, thông báo cổng vẫn gửi; `false` → như cũ.
- `SaveChotDienNuocWorkflowTests.cs`: có dispatcher fake → `RunForRoomAsync` nhận `hoanGuiEmail = true`, dispatcher nhận MỘT lần danh sách theo thứ tự phòng; không có email chờ → không gọi dispatcher; không có dispatcher → `hoanGuiEmail = false`; chốt thất bại → không gọi.
- `InvoiceDraftWorkflowTests.cs`: `FinalizeAsync` và `RejectAsync` với `ChoDuyet` + `HanPhanHoi` → đúng câu chặn, không đổi trạng thái, không commit; `ChoDuyet` không hạn → vẫn chạy như cũ; `CreateDraftsInCurrentTransactionAsync` không gọi `BeginTransactionAsync`/`Commit`, có `SaveChanges`, cùng item/tiền với `CreateDraftsAsync` trên cùng dữ liệu, dòng lịch sử dùng `lyDoKhoiTao`; thiếu `InvoiceDraft` → Fail; `CreateDraftsAsync` giữ nguyên câu lỗi và log khi exception.
- `MeterAdjustmentCoreTests.cs` (mới, fake như `MeterReadingCorrectionTests.cs`): không gọi `BeginTransactionAsync`; có ảnh chính thức → `SuaGiaTriXacNhan` đúng số và lý do; không có ảnh → ghi chú `[Điện thủ công: …]` không lặp; chỉ đổi đồng hồ có số khác; kỳ vẫn `DaDuyet`; hóa đơn `Nhap` liên kết được tính lại; KHÔNG chặn tiêu thụ bất thường; Fail khi kỳ chưa `DaDuyet`, kỳ sau đã chốt, còn hóa đơn khác `Nhap`/`DaHuy`, số < cũ, thiếu quyền (`Forbidden`), lý do rỗng; `CorrectConfirmedImageAsync` vẫn bắt lý do (test cũ giữ nguyên).
- `InvoiceAdjustmentServiceTests.cs` (mới): quyền (thiếu `MeterReview`, thiếu `InvoiceSubmit`, `DaGui` thiếu `InvoiceCancel`, chi nhánh khác → `Forbidden`; Admin qua); không phải hóa đơn tự động, `Nhap`/`DaChot`/`DaHuy` → Fail đúng câu; số trùng cả hai → "Không có thay đổi", không gọi lõi chỉ số; số < cũ; kỳ sau đã chốt; `DaGui` có khoản thu hoặc `ThanhToanMotPhan` → "Hóa đơn đã có khoản thu"; lượt `DangDoiChieu` → "Khách đã nộp minh chứng, cần đối chiếu trước"; lõi chỉ số thất bại → rollback, không commit, `DiscardPendingChanges` được gọi, không thông báo/email; 6.2 thành công: thứ tự MoLai → Save → lõi chỉ số → GuiTamTinh, commit đúng một lần, `HanPhanHoi = now + 24` và `+ 48` (`FixedTimeProvider`), lịch sử mang `[DIEU_CHINH]`, khách nhận cổng + email `LaDieuChinh = true`; 6.3 thành công: hóa đơn cũ `DaHuy`, lượt `ChoThanhToan` bị hủy, `CreateDraftsInCurrentTransactionAsync` nhận lý do `[DIEU_CHINH]`, hóa đơn mới được gửi tạm tính; draft trả item `Created` cho hợp đồng khác → rollback; email/thông báo lỗi → vẫn thành công, có Warning, không có Error; `GetAdjustmentInfoAsync` trả đúng `LyDoKhoa` theo thứ tự 1–7 và `Forbidden` khi thiếu `InvoiceRead`.
- `AutoInvoiceFinalizeUseCaseTests.cs` (mới): không có hóa đơn đến hạn → 0; `HanPhanHoi == now` → chốt; `HanPhanHoi = now + 1 giây` (kiểm lại trong transaction) → bỏ qua im lặng; chốt: hai dòng lịch sử `[TU_DONG]` (`DaChot`, `DaGui`) với người = `GetLastProvisionalSubmitterIdAsync`, `HanThanhToan = InvoiceDueDateCalculator.Compute(now, SoNgayHanThanhToan)`, `NgayGui = now`; mỗi lý do 3 → Warning + báo nhân viên, chạy 2 lượt chỉ báo 1 lần; hóa đơn 1 ném exception → đúng một Error, hóa đơn 2 vẫn được chốt; email lỗi → vẫn chốt, Warning; không dùng `IEmployeeAccessService`.
- `BranchScopeArchitectureTests.cs`: thêm `typeof(IInvoiceAdjustmentService)` vào `ScopedServices`.

### 6.3 Infrastructure.IntegrationTests (DB `_test`)
- `Persistence/AutoInvoiceStoreTests.cs` (sửa): ctx có `TenChiNhanh`, `DonGiaDien`, `DonGiaNuoc`; `GetDueProvisionalInvoicesAsync` lấy `HanPhanHoi <= now`, bỏ `> now`, bỏ `HanPhanHoi` null, bỏ `IsDeleted`, bỏ `DaGui`, đúng thứ tự; `GetLastProvisionalSubmitterIdAsync` lấy lần chuyển `ChoDuyet` mới nhất; `GetAdjustmentSnapshotAsync`.
- `Persistence/InvoiceAdjustmentTests.cs` (mới, service thật): 6.2 thành công (tổng tiền mới khớp tính lại tay, ảnh chính thức đổi số, hạn mới); 6.2 rollback thật: tạo kỳ tháng sau ở `Nhap` có `ChiSoDienMoi` nhỏ hơn số mới để `CascadeSubsequentPeriodsAsync` báo lỗi → hóa đơn vẫn `ChoDuyet`, `HanPhanHoi`, `TongTien`, chỉ số kỳ giữ nguyên, không có dòng lịch sử mới; 6.3 thành công: cũ `DaHuy` + `IsDeleted`, mới mã `-R1` `ChoDuyet` có hạn, lượt `ChoThanhToan` thành hủy; 6.3 bị chặn khi có `LichSuThanhToan` chưa xóa.
- `Persistence/AutoInvoiceFinalizeTests.cs` (mới, use case thật, fake email/notifier/PDF exporter như `AutoInvoiceChainTests.cs`): đến hạn → `DaGui`, `HanThanhToan`, hai dòng `[TU_DONG]`, có `ThongBao` cho khách, email hóa đơn được gọi; chưa đến hạn, `ChoDuyet` tay, hóa đơn xóa → không đổi; hai DbContext chạy song song job và điều chỉnh cùng hóa đơn (`Task.WhenAll`) → kết thúc ở đúng một trong hai trạng thái hợp lệ, không lỗi dữ liệu, không có hóa đơn hoạt động thứ hai.
- `ExternalServices/BackgroundProvisionalInvoiceEmailDispatcherTests.cs` (mới, không cần DB): `Enqueue` trả về trước khi email fake (bị chặn bằng cổng chờ) gửi xong; email chạy trong scope khác scope gọi; một email ném, một email trả lỗi, một email thành công → cả ba được thử, hai Warning, không Error.
- `BackgroundJobs/BackgroundJobsRuntimeSmokeTests.cs` (sửa): thay `IAutoInvoiceFinalizeUseCase` bằng double (blocking/observable) như 3 job cũ; kiểm `AutoInvoiceFinalizeJob` đăng ký, chạy vòng đầu, log "đã khởi động"/"đang dừng", không Error.
- `BackgroundJobs/BackgroundJobsApplicationCompositionTests.cs` (sửa): thay `IAutoInvoiceStore` bằng proxy, `GetDueProvisionalInvoicesAsync` trả rỗng + tín hiệu, mọi method khác ném; kiểm resolve ra `AutoInvoiceFinalizeUseCase` thật và vòng đầu có truy vấn.

### 6.4 Web.IntegrationTests
- `Fixtures/CustomWebApplicationFactory.cs`: CHỈ thêm `"AutoInvoiceFinalizeJob"` và `"AutoInvoiceFinalizeUseCase"` vào danh sách category ở `AssertSuiteLogIntegrity`.
- `StartupTests.cs`: thêm `Assert.DoesNotContain(hostedServices, s => s.GetType().Name == "AutoInvoiceFinalizeJob");`
- `AutoInvoiceAdjustmentEndpointTests.cs` (mới): nhân viên chi nhánh A điều chỉnh hóa đơn tạm tính chi nhánh A qua POST (có antiforgery header) → 200, DB đổi tổng tiền/hạn; nhân viên chi nhánh B → 403 ở GET và POST, DB không đổi; Admin POST `/HoaDon/Chot/{id}` và `/HoaDon/TraLai/{id}` với hóa đơn tạm tính → 400 đúng câu, trạng thái không đổi; hóa đơn `ChoDuyet` tay → Admin chốt vẫn 200.
- `AutoInvoiceTriggerTests.cs` (thêm): nhân viên chốt 2 phòng qua `POST /DienNuoc/SaveChotDienNuoc` → 200; trong tối đa 10 giây (thăm dò) `ProvisionalEmailCalls` có email của cả hai hóa đơn; không có log Error.
- Cảnh báo: test cũ có chốt kỳ (`MeterReadingSubmissionTests.cs`, `EmployeeBranchAuthorizationTests.cs`, `MeterImageStaffEndpointTests.cs`) nay có thể sinh email tạm tính chạy nền sau khi request trả về. Không sửa assertion cũ để cho qua; nếu FAIL vì hành vi mới, ghi rõ trong `.bangiao/thay-doi.md` rồi mới sửa.

### 6.5 Lệnh chạy (tuần tự, KHÔNG chạy song song hai lượt `dotnet test`)
```powershell
dotnet build QuanLyChoThuePhongTroWeb.sln
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
# Chỉ khi có DB test tên kết thúc _test hoặc _integration_test; thiếu thì ghi CHƯA CHẠY, không ghi PASS
$env:QLCTPT_TEST_CONNECTION_STRING = "Host=...;Database=qlctpt_test;..."
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests
$env:QLCTPT_DESIGNTIME_CONNECTION_STRING = "Host=localhost;Database=qlctpt_designtime_test;Username=x;Password=x"
$p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
git diff --check
```
Một lớp: `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~InvoiceAdjustmentServiceTests"`.

---

## 7. Ảnh hưởng schema PostgreSQL

KHÔNG ĐỔI SCHEMA. Không migration mới. `has-pending-model-changes` phải báo không có thay đổi sau khi sửa.

---

## 8. Ngoài phạm vi đợt B (không làm)

Mọi Razor/JS (nút "Điều chỉnh và gửi lại", ẩn nút Chốt/Trả lại, nhãn "Tạm tính, tự chốt lúc…", cổng khách); màn chi nhánh và `ChiNhanhReq.SoGioPhanHoiHoaDon` (L6); Việc cần làm `HD2`/`HD4`; L8 (chứng chỉ SMTP); gửi nền email cho trigger khác ngoài chốt kỳ; cập nhật `AGENTS.md` mục Hiện trạng (làm khi xong cả tính năng); sửa `review.md`.
