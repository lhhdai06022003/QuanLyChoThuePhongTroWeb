# Kiểm tra chỉ số theo hợp đồng và duyệt hóa đơn trước khi gửi: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** So tiêu thụ điện nước với lịch sử trong cùng hợp đồng để phát hiện bất thường; hóa đơn tự tạo dừng ở Nháp và đi luồng gửi duyệt → Admin chốt → công bố; công bố đặt hạn phản hồi; gỡ toàn bộ luồng tạm tính tự chốt.

**Architecture:** Một chính sách thuần `MeterAnomalyPolicy` (Application, DienNuocs) dùng chung cho chuỗi tự động, màn xác nhận ảnh và hộp điều chỉnh hóa đơn; lịch sử lấy qua store mới `IMeterConsumptionHistoryStore`. Luồng hóa đơn dùng lại các service sẵn có (`InvoiceIssuanceService`, `InvoicePublicationService`, `InvoiceAdjustmentService`), bỏ nhánh tạm tính và job tự chốt. Không đổi schema.

**Tech Stack:** ASP.NET Core MVC .NET 8, EF Core 8 + PostgreSQL, xUnit, Razor + jQuery.

**Spec:** [spec.md](spec.md) (v4, đã duyệt 12/10/2026).

## Global Constraints

- Không đổi schema: `dotnet ef migrations has-pending-model-changes` phải báo không có thay đổi. Không sửa migration cũ.
- Ranh giới tầng (AGENTS.md): Application không dùng EF/ASP.NET/`IQueryable`; Web không tham chiếu Domain (DTO cho Web dùng `int`/`string`, không dùng enum Domain).
- Service phía quản lý nhận `actorId` làm tham số đầu và fail closed (`BranchScopeArchitectureTests`).
- Lưu UTC; chỉ đổi sang giờ Việt Nam (UTC+7) ở ranh giới hiển thị và khi tính mốc tháng.
- Lỗi đã lường trước ghi log Information/Warning, không dùng Error (`CustomWebApplicationFactory` FAIL khi có log Error ngoài dự kiến).
- Chuỗi văn bản giao diện và thông báo viết tiếng Việt, đúng các câu đặt trong plan.
- Hóa đơn không có dòng âm: đơn giá, số lượng ≥ 0, tổng tiền > 0 (quy tắc sẵn có, không nới).
- Quyết định đã chốt ngoài spec (12/10/2026): khách không có tài khoản cổng lẫn email **vẫn** được tự tạo nháp; điều chỉnh hóa đơn đã gửi chỉ báo khách qua **thông báo cổng**.

## Quy tắc thực thi (đã thống nhất với người dùng)

- **Model/mức:** ghi ở đầu mỗi task (người làm) và dòng "Review" (người review). Luôn truyền `model` khi giao subagent. Vòng sửa 1–3 cùng người làm; vòng 4–5 người mới, model cao hơn một bậc; quá 5 vòng thì dừng, báo người dùng.
- **Mỗi lúc một subagent.** Subagent chỉ chạy `dotnet build` và unit test (`Domain.UnitTests`, `Application.UnitTests`, có `--filter`). Subagent **viết/sửa** integration test của task, còn phiên chính chạy chúng (skill `chay-test-tich-hop`, DB `quanlyphongtro_test`) trước khi giao review.
- **Commit:** mỗi task commit cục bộ trên `feature/duyet-hoa-don-tu-dong`, không push. Cuối cùng gộp còn 3 commit: Phần 1 (Task 1–4), Phần 2 (Task 5–9), Phần 3 (Task 10–13). Chỉ gộp khi người dùng bảo.
- Gặp chỗ plan và code mâu thuẫn: dừng, hỏi phiên chính.

## Review Focus

1. **Biên giờ Việt Nam của "tháng trọn":** hợp đồng bắt đầu 00:00 ngày 1 giờ VN (= 17:00 UTC ngày cuối tháng trước) phải được tính là trọn tháng; bắt đầu 00:01 thì không. Test ở Task 2.
2. **Hợp đồng kết thúc giữa tháng đang xét** (khách sắp trả phòng): trần chung chỉ tính theo số ngày còn ở. Test ở Task 2 và Task 1.
3. **Trung vị bằng 0** (các tháng trước không dùng): tiêu thụ 50 kWh qua, 51 kWh bị cờ; không xét giảm. Test ở Task 1.
4. **Hóa đơn tạm tính cũ** (`ChoDuyet` có `HanPhanHoi`): Admin chốt được, khách không thấy, công bố ghi đè hạn mới. Test ở Task 7 và Task 9.
5. **Admin điều chỉnh hóa đơn Đã chốt đúng lúc nhân viên công bố:** điều chỉnh phải từ chối ("Hóa đơn vừa đổi trạng thái, vui lòng tải lại"), không được rơi sang nhánh hủy/thay thế. Test ở Task 6.

---

## Phần 1: Kiểm tra chỉ số bất thường

### Task 1: Chính sách bất thường và cấu hình

**Người làm:** `opus` / `high` (rủi ro cao). **Review:** `opus` / `high`.

**Files:**
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Services/MeterAnomalyPolicy.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Persistence/IMeterConsumptionHistoryStore.cs` (chỉ interface + record, implementation ở Task 2)
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Common/Configurations/AutoInvoiceOptions.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/appsettings.example.json` (mục `AutoInvoiceOptions`)
- Test: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/MeterAnomalyPolicyTests.cs` (mới), `AutoInvoiceOptionsTests.cs` (sửa)

**Interfaces:**
- Produces:
  ```csharp
  // Features/DienNuocs/Persistence/IMeterConsumptionHistoryStore.cs
  public sealed record MeterConsumption(decimal Dien, decimal Nuoc);
  // CacKyTronThang: mới → cũ, tối đa soKyToiDa. SoNgayPhu: số ngày lịch VN hợp đồng phủ trong tháng đang xét.
  // HopDongId null khi phòng không có đúng một hợp đồng hiệu lực trong tháng (khi đó danh sách rỗng, SoNgayPhu = SoNgayCuaThang).
  public sealed record MeterConsumptionHistory(IReadOnlyList<MeterConsumption> CacKyTronThang, int SoNgayPhu, int SoNgayCuaThang, int? HopDongId);
  public interface IMeterConsumptionHistoryStore
  {
      Task<MeterConsumptionHistory> GetAsync(int phongTroId, int thang, int nam, int soKyToiDa, CancellationToken ct = default);
  }

  // Features/DienNuocs/Services/MeterAnomalyPolicy.cs
  public enum MeterAnomalyReason { ChiSoGiam = 1, NghiSaiChuSo, TieuThuBatThuong, TieuThuThapBatThuong }
  public sealed record MeterAnomalyResult(MeterAnomalyReason LyDo, string MoTa);
  public static class MeterAnomalyPolicy
  {
      // null = hợp lệ.
      public static MeterAnomalyResult? Evaluate(LoaiDongHo loai, decimal cu, decimal moi, MeterConsumptionHistory lichSu, AutoInvoiceOptions o);
  }
  ```
- `AutoInvoiceOptions` sau task: `NguongDoTinCay` (0.90), `SoKyLichSuToiDa` (6), `SoKyToiThieu` (2), `HeSoTang` (2), `SanTangDienKwh` (50), `SanTangNuocM3` (5), `HeSoThap` (0.3), `NguongXetThapDienKwh` (20), `NguongXetThapNuocM3` (2), `TranDienKwh` (500), `TranNuocM3` (30). Xóa `SoKyTrungBinh`, `HeSoBatThuong`. **Giữ `ChuKyJobPhut` tới Task 7** (job còn dùng).

**Quy tắc `Evaluate`** (thứ tự, trả lý do đầu tiên):
1. `moi < cu` → `ChiSoGiam`, mô tả `"{Ten}: chỉ số mới ({moi}) nhỏ hơn chỉ số cũ ({cu})"`.
2. `cu >= 100 && moi >= cu * 9` → `NghiSaiChuSo`, `"{Ten}: chỉ số mới ({moi}) quá lớn so với chỉ số cũ ({cu}), có thể đọc thừa một chữ số"`.
3. `t = moi - cu`. Nếu `CacKyTronThang.Count >= SoKyToiThieu`: M = trung vị (chẵn thì trung bình hai giá trị giữa) của tiêu thụ cùng loại.
   - `t > M * HeSoTang && t - M > SanTang` → `TieuThuBatThuong`.
   - `M > NguongXetThap && t < M * HeSoThap` → `TieuThuThapBatThuong`.
   - Mô tả cả hai: `"{Ten}: tiêu thụ {t} {dv}, trung vị {n} kỳ của hợp đồng {M} {dv} (cho phép {min}–{max} {dv})"` với `max = Max(M * HeSoTang, M + SanTang)`, `min = M > NguongXetThap ? M * HeSoThap : 0`.
4. Ngược lại: `tran = Tran * SoNgayPhu / SoNgayCuaThang`; `t > tran` → `TieuThuBatThuong`, `"{Ten}: tiêu thụ {t} {dv}, hợp đồng mới chưa đủ lịch sử, so với trần chung {Round(tran,1)} {dv}"`.
- `{Ten}` = "Điện"/"Nước", `{dv}` = "kWh"/"m³". Số in bằng `ChiSo.DinhDang` (namespace `Application.Common.Formatting`). So sánh dùng giá trị chính xác, chỉ làm tròn khi in.

**Validate:** `SoKyLichSuToiDa >= 1`; `1 <= SoKyToiThieu <= SoKyLichSuToiDa`; `HeSoTang > 1`; `San*`, `NguongXetThap* >= 0`; `0 < HeSoThap < 1`; giữ ràng buộc cũ của các khóa còn lại. Thông báo lỗi theo mẫu sẵn có (`"Cấu hình AutoInvoiceOptions:<Khoa> ..."`).

- [ ] **Step 1: Viết test thất bại** `MeterAnomalyPolicyTests` (lịch sử 6 kỳ điện 38, 40, 40, 42, 45, 39 → M = 40; `SoNgayPhu = SoNgayCuaThang = 31`):
  - `TangSatNguong`: t = 90 → null; t = 91 → `TieuThuBatThuong`.
  - `GiamSatNguong`: t = 12 → null; t = 11 và t = 0 → `TieuThuThapBatThuong`.
  - `TrungViChan_LayTrungBinhHaiGiaTriGiua`: 2 kỳ 30 và 50 → M = 40, t = 91 bị cờ.
  - `KhongXetGiam_KhiTrungViKhongVuotNguong`: M = 20, t = 0 → null; M = 21, t = 6 → `TieuThuThapBatThuong`.
  - `TrungViBangKhong`: M = 0, t = 50 → null; t = 51 → `TieuThuBatThuong`.
  - `MotKy_DungTranChung`: 1 kỳ → so trần; t = 500 đủ tháng → null, 501 → cờ.
  - `TranTheoNgay`: `SoNgayPhu = 12, SoNgayCuaThang = 31`, không lịch sử: t = 150 → null, t = 200 → cờ, mô tả chứa "193,5".
  - `NghiSaiChuSo`: cu 1234, moi 12345 → `NghiSaiChuSo`; cu 99, moi 900 → không phải `NghiSaiChuSo`.
  - `ChiSoGiam`: cu 100, moi 99 → `ChiSoGiam`.
  - Nước: M = 4 m³ (đủ lịch sử): t = 9 → null (max = Max(8, 9)), t = 9.001 → cờ.
- [ ] **Step 2:** `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~MeterAnomalyPolicyTests"` → FAIL (chưa có kiểu).
- [ ] **Step 3:** Tạo record/interface/policy, đổi `AutoInvoiceOptions` và `appsettings.example.json` (chú thích mục cập nhật theo khóa mới; vẫn còn `ChuKyJobPhut`). Sửa mọi chỗ dùng `SoKyTrungBinh`/`HeSoBatThuong` để build xanh: tạm thời `AutoInvoiceReadingPolicy` và các caller `GetRoomContextAsync` dùng `SoKyLichSuToiDa`; phần so sánh cũ giữ nguyên tới Task 3 nhưng đọc `HeSoTang` thay cho `HeSoBatThuong`.
- [ ] **Step 4:** Sửa `AutoInvoiceOptionsTests` theo khóa mới (mặc định, từng ràng buộc). Chạy `MeterAnomalyPolicyTests|AutoInvoiceOptionsTests|AutoInvoiceReadingPolicyTests` → PASS (sửa kỳ vọng của test cũ chỉ ở chỗ hệ số đổi 3 → 2).
- [ ] **Step 5:** `dotnet build QuanLyChoThuePhongTroWeb.sln` xanh. Commit `feat(duyet-hoa-don): chính sách bất thường chỉ số và cấu hình`.

### Task 2: Store lịch sử tiêu thụ theo hợp đồng

**Người làm:** `sonnet` / `medium`. **Review:** `sonnet` / `medium`.

**Files:**
- Create: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/MeterConsumptionHistoryStore.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/DependencyInjection.cs` (đăng ký scoped)
- Test: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/MeterConsumptionHistoryStoreTests.cs` (mới)

**Interfaces:** Consumes `IMeterConsumptionHistoryStore`, `MeterConsumptionHistory` (Task 1). Produces implementation `MeterConsumptionHistoryStore`.

**Quy tắc truy vấn** (AsNoTracking):
- Hợp đồng hiệu lực trong tháng: cùng điều kiện `AutoInvoiceStore.GetRoomContextAsync` (`!IsDeleted`, khác `DaHuy`, `ThoiDiemBatDau <= endOfMonthUtc`, `ThoiDiemKetThuc == null || >= startOfMonthUtc + 1 ngày`), mốc tháng từ `MeterPeriodPolicy.MonthRangeUtc`. Khác đúng 1 hợp đồng → `new(Array.Empty, soNgayCuaThang, soNgayCuaThang, null)`.
- Kỳ lịch sử: `DichVuDienNuocCuaPhongs` cùng phòng, `!IsDeleted`, `DaDuyet`, trước kỳ đang xét, và tháng của kỳ nằm trọn trong hợp đồng: `ThoiDiemBatDau <= startUtc(kỳ)` và `ThoiDiemKetThuc == null || ThoiDiemKetThuc >= nextStartUtc(kỳ) - 1 giây`. Sắp mới → cũ, `Take(soKyToiDa)`. Lọc tháng trọn có thể làm ở bộ nhớ sau khi lấy tối đa 24 kỳ gần nhất (đủ cho 6 kỳ hợp lệ).
- `SoNgayPhu`: số ngày lịch VN từ `max(ngàyVN(ThoiDiemBatDau), ngày 1)` đến `min(ngàyVN(ThoiDiemKetThuc) nếu có, ngày cuối tháng)`, tính cả hai đầu; tối thiểu 1.

- [ ] **Step 1:** Viết test (theo mẫu fixture của `AutoInvoiceStoreTests`):
  - `ChiLayKyTronThangCuaHopDongHienTai`: phòng có 2 kỳ của khách cũ (hợp đồng đã kết thúc) và 3 kỳ trọn tháng của hợp đồng hiện tại → 3 phần tử, đúng thứ tự mới → cũ, `HopDongId` = hợp đồng hiện tại.
  - `BienGioVietNam`: hợp đồng bắt đầu `2026-05-31T17:00:00Z` (00:00 01/06 VN) → kỳ 6/2026 được tính; bắt đầu `2026-05-31T17:01:00Z` → không.
  - `ThangVaoGiuaChung_KhongTinh`: kỳ tháng khách vào ngày 20 không có trong danh sách.
  - `SoNgayPhu`: hợp đồng bắt đầu 20/10/2026 VN → `SoNgayPhu = 12`, `SoNgayCuaThang = 31`; hợp đồng kết thúc 15/10/2026 VN, bắt đầu từ trước → 15.
  - `HaiHopDongTrongThang_KhongCoLichSu`: → danh sách rỗng, `HopDongId == null`.
  - `GioiHanSoKy`: 8 kỳ hợp lệ, `soKyToiDa = 6` → 6 phần tử.
- [ ] **Step 2:** Viết implementation + đăng ký DI. `dotnet build` xanh.
- [ ] **Step 3:** Báo phiên chính chạy `--filter "FullyQualifiedName~MeterConsumptionHistoryStoreTests"` (Infrastructure.IntegrationTests) → PASS.
- [ ] **Step 4:** Commit `feat(duyet-hoa-don): store lịch sử tiêu thụ theo hợp đồng`.

### Task 3: Chuỗi tự động dùng chính sách mới

**Người làm:** `opus` / `high`. **Review:** `opus` / `high`.

**Files:**
- Modify: `Application/Features/HoaDons/Persistence/IAutoInvoiceStore.cs` (thay `TieuThuCacKyTruoc` bằng `MeterConsumptionHistory LichSu`, xóa `AutoInvoiceConsumption`, đổi tên tham số `soKyLichSuToiDa`)
- Modify: `Infrastructure/Persistence/Features/AutoInvoiceStore.cs` (lấy `LichSu` qua `IMeterConsumptionHistoryStore` inject vào store)
- Modify: `Application/Features/HoaDons/AutoInvoices/AutoInvoiceReadingPolicy.cs`, `Application/Features/HoaDons/DTOs/AutoInvoiceDtos.cs`, `Application/Features/HoaDons/Services/AutoInvoiceService.cs` (`BaoDungAsync`), `Application/Features/ViecCanLam/Services/ViecCanLamService.cs` (`NhanLyDoDung`)
- Test: `AutoInvoiceReadingPolicyTests.cs`, `AutoInvoiceServiceTests.cs`, `ViecCanLamServiceTests.cs`, `Fakes/FakeAutoInvoiceStore.cs`; Infrastructure `AutoInvoiceStoreTests.cs` (bỏ assert `TieuThuCacKyTruoc`, thêm assert `LichSu.HopDongId`)

**Interfaces:**
- Consumes: `MeterAnomalyPolicy.Evaluate`, `IMeterConsumptionHistoryStore.GetAsync`.
- Produces: `AutoInvoiceReason` thêm **ở cuối enum** `NghiSaiChuSo`, `TieuThuThapBatThuong` (giữ số cũ). `AutoInvoiceRoomContext.LichSu` (không null; mặc định `new(Array.Empty<MeterConsumption>(), 30, 30, null)`).

**Thay đổi:**
- `EvaluateMeter`: giữ nguyên phần chọn chỉ số mới (ảnh chính thức / OCR đạt ngưỡng / kỳ `DaDuyet`); thay toàn bộ phần "số giảm + tiêu thụ" bằng `MeterAnomalyPolicy.Evaluate(loai, cu, moi, ctx.LichSu, o)`, ánh xạ `MeterAnomalyReason` → `AutoInvoiceReason` cùng tên; `MoTa` lấy từ policy.
- `CanBaoKhach` thêm `NghiSaiChuSo`, `TieuThuThapBatThuong`.
- `BaoDungAsync`: nhánh `ChiSoGiam` giữ nguyên; thêm nhánh `NghiSaiChuSo` và `TieuThuThapBatThuong` với tiêu đề `"Chỉ số đồng hồ có thể chưa đúng"` và nội dung `"Chỉ số {điện|nước} kỳ MM/yyyy của phòng {SoPhong} đọc được là {moi}, chênh lệch nhiều so với các tháng trước. Vui lòng chụp lại rõ mặt đồng hồ"` + (`" hoặc liên hệ {SĐT}."` nếu có SĐT, ngược lại `"."`).
- `NhanLyDoDung`: `NghiSaiChuSo` → `"nghi đọc sai chữ số"`, `TieuThuThapBatThuong` → `"tiêu thụ thấp bất thường"`.

- [ ] **Step 1:** Sửa helper `Ctx(...)` trong `AutoInvoiceReadingPolicyTests` nhận `MeterConsumptionHistory? lichSu`. Viết test mới: `TieuThuThap_DungVaBaoKhach` (M = 40, t = 5 → `LyDo = TieuThuThapBatThuong`, `CanBaoKhach` true), `NghiSaiChuSo_Dung`, `HopDongMoi_TranTheoNgay` (12/31 ngày, t = 200 → `TieuThuBatThuong`). Sửa các test "bất thường" cũ sang lịch sử mới (≥ 2 kỳ). Chạy → FAIL.
- [ ] **Step 2:** Làm thay đổi ở trên; cập nhật `FakeAutoInvoiceStore`, `AutoInvoiceServiceTests` (test thông báo khách cho lý do mới: tiêu đề đúng chuỗi trên), `ViecCanLamServiceTests` (dùng `LichSu`).
- [ ] **Step 3:** `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~AutoInvoice|FullyQualifiedName~ViecCanLam|FullyQualifiedName~MeterAnomaly"` → PASS. Build solution xanh.
- [ ] **Step 4:** Báo phiên chính chạy Infrastructure `--filter "FullyQualifiedName~AutoInvoiceStoreTests|FullyQualifiedName~AutoInvoiceChainTests"` → PASS.
- [ ] **Step 5:** Commit `feat(duyet-hoa-don): chuỗi tự động so lịch sử theo hợp đồng`.

### Task 4: Kiểm tra bất thường khi nhân viên nhập (server)

**Người làm:** `sonnet` / `medium`. **Review:** `sonnet` / `medium`.

**Files:**
- Create: `Application/Features/DienNuocs/Services/IMeterAnomalyCheckService.cs`, `MeterAnomalyCheckService.cs`; DTO trong `Application/Features/DienNuocs/DTOs/MeterAnomalyCheckDtos.cs`
- Modify: `Application/DependencyInjection.cs` (đăng ký scoped)
- Modify: `Application/Features/DienNuocs/Services/MeterReadingWorkflowService.cs` (`ConfirmImageAsync` + constructor nhận `IMeterConsumptionHistoryStore`, `AutoInvoiceOptions`)
- Modify: `Application/Features/HoaDons/Services/InvoiceAdjustmentService.cs` (`PreviewMeterLinesAsync` trả cảnh báo) + `DTOs/InvoiceAdjustmentDtos.cs` (`InvoiceMeterLinesPreview.CanhBaos`)
- Test: `MeterAnomalyCheckServiceTests.cs` (mới), `MeterReadingWorkflowTests.cs`, `InvoiceAdjustmentServiceTests.cs`; Infrastructure `MeterImageStoreTests.cs` (constructor)

**Interfaces:**
- Produces:
  ```csharp
  public sealed class MeterAnomalyCheckRequest
  {
      public int PhongTroId { get; init; }
      public int Thang { get; init; }
      public int Nam { get; init; }
      public decimal? ChiSoDienMoi { get; init; }
      public decimal? ChiSoNuocMoi { get; init; }
  }
  public sealed class MeterAnomalyWarning { public string MaLyDo { get; init; } = ""; public string MoTa { get; init; } = ""; }  // MaLyDo = tên MeterAnomalyReason
  public sealed class MeterAnomalyCheckResult { public MeterAnomalyWarning? Dien { get; init; } public MeterAnomalyWarning? Nuoc { get; init; } }
  public interface IMeterAnomalyCheckService
  {
      Task<ServiceResult<MeterAnomalyCheckResult>> KiemTraAsync(int actorId, MeterAnomalyCheckRequest request, CancellationToken ct = default);
  }
  // InvoiceMeterLinesPreview thêm: public IReadOnlyList<string> CanhBaos { get; init; } = Array.Empty<string>();
  ```
- `KiemTraAsync`: phòng không tồn tại → `NotFound`; thiếu quyền `Meter.Read` tại chi nhánh → `Forbidden`. Chỉ số cũ = kỳ hiện có (`GetPeriodRecordAsync`) hoặc `GetNearestPreviousReadingAsync` nếu chưa có kỳ. Đồng hồ nào không gửi số thì kết quả của đồng hồ đó null.
- `ConfirmImageAsync`: sau các kiểm tra hiện có (trước khi xác nhận), tính `MeterAnomalyPolicy.Evaluate` cho đồng hồ của ảnh với `period` đã khóa; kết quả khác null và khác `ChiSoGiam` mà `GhiChu` trống → `Fail("Chỉ số bất thường so với lịch sử, cần ghi chú lý do. " + MoTa)`. (`ChiSoGiam` đã bị chặn sẵn.)
- `PreviewMeterLinesAsync`: thêm `CanhBaos` = mô tả của policy cho điện và nước (bỏ qua null). Không chặn.

- [ ] **Step 1:** Test thất bại: `KiemTra_TraCanhBaoDienKhiVuotNguong`, `KiemTra_ThieuQuyen_Forbidden`, `XacNhanSoBatThuong_KhongGhiChu_BiTuChoi`, `XacNhanSoBatThuong_CoGhiChu_ThanhCong`, `XacNhanSoBinhThuong_KhongCanGhiChu`, `PreviewDieuChinh_CoCanhBao`. Dùng fake `IMeterConsumptionHistoryStore` (tạo `Fakes/FakeMeterConsumptionHistoryStore.cs`).
- [ ] **Step 2:** Implementation + DI; sửa 3 chỗ `new MeterReadingWorkflowService(` trong test.
- [ ] **Step 3:** `--filter "FullyQualifiedName~MeterAnomalyCheck|FullyQualifiedName~MeterReadingWorkflow|FullyQualifiedName~InvoiceAdjustment"` → PASS; build xanh. Báo phiên chính chạy Infrastructure `MeterImageStoreTests`.
- [ ] **Step 4:** Commit `feat(duyet-hoa-don): cảnh báo và bắt ghi chú khi xác nhận số bất thường`.

---

## Phần 2: Luồng duyệt và công bố

### Task 5: Chuỗi tự động dừng ở Nháp

**Người làm:** `opus` / `high`. **Review:** `opus` / `high`.

**Files:**
- Modify: `Application/Features/HoaDons/Services/AutoInvoiceService.cs`, `IAutoInvoiceService.cs`, `DTOs/AutoInvoiceDtos.cs`
- Modify: `Application/Features/HoaDons/Services/InvoiceIssuanceService.cs` + `IInvoiceIssuanceService.cs` (`CreateDraftsAsync` thêm `string? lyDoKhoiTao = null` trước `ct`, truyền xuống `BuildDraftsAsync`, null thì `DefaultDraftReason`)
- Modify: `Application/Features/HoaDons/AutoInvoices/AutoInvoiceReadingPolicy.cs` (bỏ điều kiện `KhachKhongCoKenhNhan`)
- Modify: `Application/Features/DienNuocs/Services/DienNuocService.cs` (bỏ thu thập `EmailChoGui`)
- Test: `AutoInvoiceServiceTests.cs`, `AutoInvoiceReadingPolicyTests.cs`, `SaveChotDienNuocWorkflowTests.cs`, `Fakes/FakeInvoiceIssuanceService.cs`, `Fakes/FakeAutoInvoiceService.cs`; Infrastructure `AutoInvoiceChainTests.cs`; Web `AutoInvoiceTriggerTests.cs`

**Interfaces:**
- Produces: `RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam)` (bỏ `hoanGuiEmail`). `AutoInvoiceOutcome { DaTaoNhap = 1, DungImLang, DungCoThongBao }`. `AutoInvoiceRunResult` bỏ `HanPhanHoiUtc`, `EmailChoGui`. `AutoInvoiceReason.KhachKhongCoKenhNhan` giữ trong enum (không còn được sinh).

**Thay đổi `RunForRoomAsync`:**
- Quyền người thực hiện: chỉ `Meter.Review` và `Invoice.Draft`.
- Bước 3 gọi `CreateDraftsAsync(..., lyDoKhoiTao: AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.TuDong, "Tạo hóa đơn nháp tự động"))`.
- Bỏ bước 4–5 (`SubmitProvisionalAsync`, `BaoKhachTamTinhAsync`). Thay bằng báo nhân viên: `_thongBaoService.GuiChoNguoiPhuTrachPhongAsync(phongTroId, $"Phòng {SoPhong} kỳ {MM}/{yyyy}: có hóa đơn tự động chờ gửi duyệt", $"Hóa đơn {MaHoaDon} đã được tạo ở trạng thái nháp. Kiểm tra rồi bấm Gửi duyệt.", "HoaDon", "/QuanLyNhaTro/QuanLyHoaDon")` qua `TryNotifyAsync`. Không gửi gì cho khách. Trả `Outcome = DaTaoNhap`, `HoaDonId`.
- Xóa `BaoKhachTamTinhAsync`, phụ thuộc `IEmailService` nếu không còn dùng.
- `DienNuocService`: gọi `RunForRoomAsync` không còn `hoanGuiEmail`; xóa danh sách `emailChoGui` và lời gọi `_emailDispatcher.Enqueue` (giữ tham số constructor tới Task 7 nếu cần để build, hoặc xóa luôn cùng test).

- [ ] **Step 1:** Sửa/viết test: `ChiSoHopLe_TaoNhap_KhongBaoKhach` (hóa đơn `Nhap`, lịch sử khởi tạo có tiền tố `[TU_DONG]`, nhân viên nhận một thông báo với tiêu đề đúng, `GuiChoKhachThueAsync` không được gọi, email không gửi), `KhachKhongCoKenhNhan_VanTaoNhap`, `NguoiThucHienKhongCanInvoiceSubmit`. Xóa các test về tạm tính/email chờ gửi. Chạy → FAIL.
- [ ] **Step 2:** Implementation; cập nhật fake và test bị ảnh hưởng.
- [ ] **Step 3:** `--filter "FullyQualifiedName~AutoInvoice|FullyQualifiedName~SaveChotDienNuoc|FullyQualifiedName~InvoiceDraftWorkflow"` → PASS; build xanh.
- [ ] **Step 4:** Sửa Infrastructure `AutoInvoiceChainTests` và Web `AutoInvoiceTriggerTests` theo kết quả mới (hóa đơn `Nhap`, không email tạm tính). Báo phiên chính chạy hai lớp này.
- [ ] **Step 5:** Commit `feat(duyet-hoa-don): hóa đơn tự tạo dừng ở nháp`.

### Task 6: Điều chỉnh hóa đơn theo trạng thái

**Người làm:** `opus` / `high`. **Review:** `opus` / `high`.

**Files:**
- Modify: `Domain/Entities/HoaDon.cs` (`MoLaiDeDieuChinh`)
- Modify: `Application/Features/HoaDons/Services/InvoiceAdjustmentService.cs`, `DTOs/InvoiceAdjustmentDtos.cs`
- Test: Domain `InvoiceWorkflowTests.cs`; `InvoiceAdjustmentServiceTests.cs`; Infrastructure `InvoiceAdjustmentTests.cs`, `InvoiceAdjustmentEdgeCaseTests.cs`; Web `AutoInvoiceAdjustmentEndpointTests.cs`

**Interfaces:**
- Domain: `MoLaiDeDieuChinh(int nguoiThucHienId, DateTime nowUtc, string lyDo)` nhận `ChoDuyet` **hoặc** `DaChot` (bỏ điều kiện `HanPhanHoi`); từ `DaChot` thì xóa `NguoiChotId`, `NgayChot`. Trạng thái khác → `InvalidOperationException("Chỉ hóa đơn chờ duyệt hoặc đã chốt mới có thể mở lại để điều chỉnh.")`.
- `AdjustInvoiceRequest` thêm `int TrangThaiPhatHanhHienTai` (giá trị số của trạng thái màn hình đang thấy). `InvoiceAdjustmentInfo` thêm `int TrangThaiPhatHanhValue`; `HanPhanHoiUtc` chỉ có khi `DaGui`.
- `InvoiceAdjustmentResult`: bỏ `HanPhanHoiUtc`, thêm `int TrangThaiPhatHanhMoi`.

**Quy tắc khóa (`GetLockReasonAsync`), thay toàn bộ bản cũ:**

| Trạng thái | Quyền cần | Thêm điều kiện |
|---|---|---|
| `ChoDuyet` | `Meter.Review` + `Invoice.Draft` | — |
| `DaChot` | như trên + `Invoice.Finalize` (chỉ Admin có) | — |
| `DaGui` | như trên (không cần Finalize) + `Invoice.Cancel` | chưa thu, không `DangDoiChieu` (giữ code cũ) |
| khác | — | `TrangThaiKhongHopLe` |

Giữ các khóa chung: không có kỳ → `KhongCoKy`; kỳ sau đã chốt → `KySauDaChot`. Xóa `KhongPhaiTuDong`. Thiếu quyền ở `DaChot` báo `"Chỉ Quản trị viên mới điều chỉnh được hóa đơn đã chốt."` (Forbidden).

**Luồng `AdjustAsync` trong transaction:**
- Sau khi khóa hóa đơn: nếu `(int)hoaDon.TrangThaiPhatHanh != request.TrangThaiPhatHanhHienTai` → abort `Fail("Hóa đơn vừa đổi trạng thái, vui lòng tải lại.")`.
- `ChoDuyet`/`DaChot`: `MoLaiDeDieuChinh` → đổi chỉ số/dòng như cũ → `GuiDuyet(actorId, lyDoTag)`; nếu ban đầu `DaChot` thì tiếp `ChotHoaDon(actorId, lyDoTag)`. Không báo khách.
- `DaGui`: giữ nhánh hủy + tạo bản thay thế; bản thay thế `GuiDuyet(actorId, tag "Tạo hóa đơn thay thế sau điều chỉnh")` → `ChoDuyet`, `HanPhanHoi = null`. Sau commit báo khách **chỉ cổng**: tiêu đề `"Hóa đơn tháng {M}/{Y} đã được điều chỉnh"`, nội dung `"Hóa đơn {MaCu} đã được hủy để điều chỉnh, hóa đơn mới sẽ được gửi sau khi duyệt."`, link `/KhachThue/HoaDon`.
- Bỏ: kiểm `SoGioPhanHoiHoaDon`, `GuiTamTinh`, email, `IProvisionalInvoiceEmailDispatcher`, `IEmailService` khỏi constructor nếu không còn dùng. `LyDoDieuChinh` đổi thành `"Điều chỉnh hóa đơn"`.
- Message thành công: `ChoDuyet` → `"Đã điều chỉnh hóa đơn, hóa đơn vẫn chờ duyệt."`; `DaChot` → `"Đã điều chỉnh hóa đơn đã chốt."`; `DaGui` → `"Đã hủy hóa đơn cũ và tạo hóa đơn thay thế chờ duyệt."`.

- [ ] **Step 1:** Domain test: `MoLaiTuDaChot_VeNhap_XoaNguoiChot`, `MoLaiTuNhap_NemLoi`. Application test: `NhanVienDieuChinhChoDuyet_VanChoDuyet`, `NhanVienDieuChinhDaChot_Forbidden`, `AdminDieuChinhDaChot_VanDaChot`, `DieuChinhDaGui_TaoThayTheChoDuyet_BaoKhachCong`, `DaGuiCoKhoanThu_BiKhoa`, `TrangThaiDaDoi_TuChoi` (request `ChoDuyet` nhưng hóa đơn đã `DaChot`; và request `DaChot` nhưng đã `DaGui`), `HoaDonLapTay_DieuChinhDuoc` (không có `HanPhanHoi`). Chạy → FAIL.
- [ ] **Step 2:** Implementation; xóa/sửa test cũ về "gửi lại tạm tính".
- [ ] **Step 3:** `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests` và Application `--filter "FullyQualifiedName~InvoiceAdjustment"` → PASS; build xanh.
- [ ] **Step 4:** Sửa Infrastructure `InvoiceAdjustmentTests`, `InvoiceAdjustmentEdgeCaseTests` và Web `AutoInvoiceAdjustmentEndpointTests` (gửi `trangThaiPhatHanhHienTai`). Controller Web `HoaDonController.DieuChinh` nhận thêm trường này trong `DieuChinhHoaDonReq` và trả `trangThaiPhatHanhMoi` thay `hanTuChot` (chỉ sửa JSON, giao diện ở Task 10). Báo phiên chính chạy ba lớp test.
- [ ] **Step 5:** Commit `feat(duyet-hoa-don): điều chỉnh hóa đơn theo trạng thái`.

### Task 7: Gỡ luồng tạm tính tự chốt

**Người làm:** `opus` / `high`. **Review:** `opus` / `high`.

**Xóa hẳn (file và test tương ứng):**
- `Infrastructure/BackgroundJobs/AutoInvoiceFinalizeJob.cs`; `Application/Features/HoaDons/UseCases/AutoInvoiceFinalizeUseCase.cs`, `IAutoInvoiceFinalizeUseCase.cs`; `AutoInvoices/AutoInvoiceFinalizeNotifyGuard.cs`
- `Application/Abstractions/Services/IProvisionalInvoiceEmailDispatcher.cs`; `Infrastructure/ExternalServices/Emails/BackgroundProvisionalInvoiceEmailDispatcher.cs`; `Application/Features/Emails/DTOs/ProvisionalInvoiceEmailData.cs`, `PendingProvisionalEmail.cs`
- Tests: `AutoInvoiceFinalizeUseCaseTests.cs`, `Fakes/RecordingProvisionalEmailDispatcher.cs`, Infrastructure `AutoInvoiceFinalizeTests.cs`, `AutoInvoiceDeadlineHarness.cs`, `ExternalServices/BackgroundProvisionalInvoiceEmailDispatcherTests.cs`

**Sửa:**
- `IEmailService.SendProvisionalInvoiceEmailAsync` và implementation trong `MailKitEmailService` (xóa); fake trong `CustomWebApplicationFactory` và `RecordingEmailService` (xóa phần tạm tính).
- `IAutoInvoiceStore`/`AutoInvoiceStore`: xóa `GetDueProvisionalInvoicesAsync`, `GetLastProvisionalSubmitterIdAsync`, `AutoInvoiceDueItem`.
- `InvoiceIssuanceService`/`IInvoiceIssuanceService`: xóa `SubmitProvisionalAsync`, hằng `AutoFinalizingMessage` và hai khối chặn `HanPhanHoi != null` trong `FinalizeAsync`, `RejectAsync`.
- `HoaDon.GuiTamTinh`: xóa.
- DI: `Application/DependencyInjection.cs` (dòng đăng ký use case và guard), `Infrastructure/DependencyInjection.cs` (dispatcher, `AddHostedService<AutoInvoiceFinalizeJob>`), constructor `DienNuocService` bỏ `IProvisionalInvoiceEmailDispatcher`.
- `AutoInvoiceOptions`: xóa `ChuKyJobPhut` (+ Validate, test, `appsettings.example.json`).
- `CustomWebApplicationFactory`: bỏ hai điều kiện `AutoInvoiceFinalizeJob`/`AutoInvoiceFinalizeUseCase`; `StartupTests`: bỏ các `InlineData`/assert về job này. `BackgroundJobsApplicationCompositionTests`, `BackgroundJobsRuntimeSmokeTests`: bỏ phần job tự chốt.

- [ ] **Step 1:** Viết test mới trước khi xóa: `InvoiceDraftWorkflowTests.AdminChotHoaDonTamTinhCu_DuocChot` (hóa đơn `ChoDuyet` có `HanPhanHoi` → `FinalizeAsync` thành công), `AdminTraLaiHoaDonTamTinhCu_DuocTraLai`. Chạy → FAIL.
- [ ] **Step 2:** Xóa/sửa theo danh sách. `grep -rE "TamTinh|Provisional|AutoInvoiceFinalize|ChuKyJobPhut|SubmitProvisional|GuiTamTinh" src tests --include=*.cs` không còn kết quả ngoài `LaTamTinh`/`HanTuChot` (xử lý ở Task 9) và `DemHoaDonTamTinhAsync` (Task 9).
- [ ] **Step 3:** Build solution xanh; `dotnet test` Domain + Application (toàn bộ) → PASS.
- [ ] **Step 4:** Báo phiên chính chạy Infrastructure `BackgroundJobs` + Web `StartupTests`.
- [ ] **Step 5:** Commit `refactor(duyet-hoa-don): gỡ luồng hóa đơn tạm tính tự chốt`.

### Task 8: Công bố đặt hạn phản hồi

**Người làm:** `sonnet` / `medium`. **Review:** `sonnet` / `medium`.

**Files:**
- Modify: `Domain/Entities/HoaDon.cs` (`GuiHoaDon`)
- Modify: `Application/Features/HoaDons/Services/InvoicePublicationService.cs`
- Modify: `Application/Features/HoaDons/DTOs/HoaDonDTOs.cs` (`HoaDonChiTietRes.HanPhanHoiUtc`), `Infrastructure/Persistence/Features/InvoiceDetailProjection.cs` (gán trường mới)
- Modify: `Infrastructure/ExternalServices/Emails/MailKitEmailService.cs` (`SendInvoiceEmailAsync`)
- Test: Domain `InvoiceWorkflowTests.cs`; `InvoicePublicationTests.cs`; Infrastructure test công bố nếu có

**Interfaces:**
- Domain: `GuiHoaDon(int nguoiGuiId, DateTime nowUtc, DateTime hanThanhToanMacDinhUtc, DateTime hanPhanHoiUtc, string? lyDo = null)`. `hanPhanHoiUtc` phải `Kind == Utc` và `> nowUtc` (ngược lại `ArgumentOutOfRangeException`). Sau khi đặt `HanThanhToan`: `HanPhanHoi = hanPhanHoiUtc <= HanThanhToan ? hanPhanHoiUtc : HanThanhToan`.
- `HoaDonChiTietRes.HanPhanHoiUtc` (`DateTime?`): `HanPhanHoi` khi `DaGui && !IsDeleted`, ngược lại null.

**Thay đổi:**
- `PublishOneInTransactionAsync`: đọc chi nhánh qua `_hoaDonStore.GetChiNhanhByIdAsync(snapshot.ChiNhanhId)` (truyền `ChiNhanhId` vào hàm); `han = now.AddHours(chiNhanh.SoGioPhanHoiHoaDon)`; gọi `GuiHoaDon(..., han, ...)`. Thông báo cổng thêm câu `" Nếu thấy sai sót, vui lòng liên hệ {SoDienThoai} trước {dd/MM/yyyy HH:mm}."` (giờ VN; bỏ cụm "liên hệ {SĐT}" nếu chi nhánh không có SĐT, giữ "trước …").
- Email (`SendInvoiceEmailAsync`): nếu `HanPhanHoiUtc > DateTime.UtcNow` thì thêm đoạn `"Nếu thấy sai sót, vui lòng liên hệ {SĐT chi nhánh} trước {dd/MM/yyyy HH:mm}."` ngay trên dòng Hotline.

- [ ] **Step 1:** Test: Domain `GuiHoaDon_DatHanPhanHoi`, `GuiHoaDon_HanPhanHoiKhongQuaHanThanhToan` (hạn TT 2 ngày, phản hồi 168 giờ → bằng hạn TT), `GuiHoaDon_HanPhanHoiKhongHopLe_NemLoi`. Application: `CongBo_DatHanPhanHoiTheoSoGioChiNhanh`, `CongBo_ThongBaoCoDongLienHe`, `CongBo_HoaDonTamTinhCu_GhiDeHan` (hóa đơn `DaChot` có `HanPhanHoi` cũ). Chạy → FAIL.
- [ ] **Step 2:** Implementation. Build xanh; Domain + Application `--filter "FullyQualifiedName~Publish|FullyQualifiedName~InvoiceWorkflow"` → PASS.
- [ ] **Step 3:** Báo phiên chính chạy Infrastructure/Web test công bố (`grep -l "CongBo\|PublishAsync" tests/*IntegrationTests`).
- [ ] **Step 4:** Commit `feat(duyet-hoa-don): công bố đặt hạn phản hồi`.

### Task 9: Hiển thị dữ liệu, khách xem được, Việc cần làm

**Người làm:** `sonnet` / `medium`. **Review:** `sonnet` / `medium`.

**Files:**
- Modify: `Infrastructure/Persistence/Features/HoaDonStore.cs` (2 chỗ quy tắc khách xem + projection danh sách), `InvoiceViewStore.cs`, `InvoiceDetailProjection.cs`
- Modify: `Application/Features/HoaDons/DTOs/HoaDonDTOs.cs` (`HoaDonRes`, `HoaDonChiTietRes`), `Services/InvoiceViewService.cs` (`ExportTenantPdfAsync`), `Persistence/InvoicePaymentSnapshots.cs` (chú thích)
- Modify: `Application/Features/ViecCanLam/Services/ViecCanLamService.cs`, `Persistence/IViecCanLamStore.cs`, `Infrastructure/Persistence/Features/ViecCanLamStore.cs`
- Modify (chỉ để build và giữ JSON cũ chạy được tới Task 10–11): `Web/Areas/KhachThue/Controllers/HoaDonController.cs`, `Web/Areas/QuanLyNhaTro/Controllers/HoaDonController.cs`
- Test: `InvoiceViewServiceTests.cs`, `ViecCanLamServiceTests.cs`, `Fakes/FakeViecCanLamStore.cs`; Infrastructure `TenantInvoiceVisibilityTests.cs`, `ProvisionalInvoiceProjectionTests.cs` (đổi tên thành `InvoiceAutoFlagProjectionTests.cs`), `ViecCanLamStoreTests.cs`, `PhongTroDebtScopeTests.cs`; Web `TenantInvoicePortalTests.cs`

**Interfaces (Produces):**
- `HoaDonRes` và `HoaDonChiTietRes`: xóa `LaTamTinh`, `HanTuChot`. `LaHoaDonTuDong` = có dòng lịch sử `LyDo` bắt đầu bằng `AutoInvoiceHistoryTags.TuDong` (`"[TU_DONG]"`), tính trong truy vấn EF bằng `h.LichSuTrangThaiHoaDons.Any(l => l.LyDo != null && l.LyDo.StartsWith("[TU_DONG]"))`. `HoaDonRes` thêm `DateTime? HanPhanHoiUtc` cùng quy tắc Task 8.
- Quy tắc khách xem: chỉ `(DaGui && !IsDeleted) || (DaHuy && NgayGui != null)` ở `GetInvoicesByContractIdsAsync` và `CheckHoaDonOwnershipAsync`.
- `ExportTenantPdfAsync`: bỏ khối chặn `ChoDuyet`.
- Việc cần làm: `DemHoaDonChoDuyetAsync` đếm mọi `ChoDuyet` (bỏ `HanPhanHoi == null`); xóa `DemHoaDonTamTinhAsync` và khối `HD4`. Tiêu đề `HD2` giữ `"Hóa đơn chờ Admin chốt"`.
- Hai controller Web: bỏ `laTamTinh`, `hanTuChot` khỏi JSON; thêm `hanPhanHoi` = chuỗi giờ VN `dd/MM/yyyy HH:mm` khi `HanPhanHoiUtc > now` (dùng `Helpers/VietnamTime.cs`), ngược lại chuỗi rỗng.

- [ ] **Step 1:** Test: `TenantKhongThayHoaDonChoDuyetCoHan` (Infrastructure), `LaHoaDonTuDong_TheoNhanLichSu` (hóa đơn lập tay có `HanPhanHoi` sau công bố → false; hóa đơn có lịch sử `[TU_DONG]` → true), `HD2_DemCaHoaDonTamTinhCu`, `KhongConHD4`. Chạy → FAIL.
- [ ] **Step 2:** Implementation; sửa test cũ về tạm tính.
- [ ] **Step 3:** Build xanh; Application `--filter "FullyQualifiedName~InvoiceView|FullyQualifiedName~ViecCanLam"` → PASS.
- [ ] **Step 4:** Báo phiên chính chạy các lớp Infrastructure/Web đã liệt kê.
- [ ] **Step 5:** Commit `feat(duyet-hoa-don): bỏ hiển thị tạm tính, khách chỉ thấy hóa đơn đã công bố`.

---

## Phần 3: Giao diện

### Task 10: Màn hóa đơn quản lý

**Người làm:** `sonnet` / `medium`. **Review:** `sonnet` / `medium`.

**Files:**
- Modify: `Web/Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml`, `Web/wwwroot/js/hoadon-dieuchinh.js`, `Web/Areas/QuanLyNhaTro/Views/ChiNhanhs/QuanLyChiNhanh.cshtml`
- Test: Web `AutoInvoiceUiEndpointTests.cs`

**Thay đổi:**
- Danh sách: bỏ dòng "Tạm tính, tự chốt lúc …"; thêm badge `"Tự động"` (`bg-azure-lt`) khi `laHoaDonTuDong`.
- Nút "Điều chỉnh" trong menu hàng: hiện khi `trangThaiPhatHanhValue` là `ChoDuyet` (1), `DaChot` (2, chỉ khi `isAdmin`) hoặc `DaGui` (3); gửi kèm `data-trangthai`. Bỏ điều kiện `laHoaDonTuDong`. Nút Chốt/Trả lại hiện cho mọi `ChoDuyet` khi `isAdmin` (bỏ `!row.laTamTinh`).
- Chi tiết: bỏ khối "Hóa đơn tạm tính…"; khi `hanPhanHoi` khác rỗng hiện `"Hạn phản hồi của khách: {hanPhanHoi}"`.
- `hoadon-dieuchinh.js`: đổi chú thích đầu file và câu giải thích theo trạng thái (`ChoDuyet`: "Lưu thay đổi, hóa đơn vẫn chờ duyệt."; `DaChot`: "Lưu thay đổi, hóa đơn vẫn ở trạng thái đã chốt."; `DaGui`: "Hệ thống hủy hóa đơn cũ, tạo hóa đơn thay thế chờ duyệt và báo khách trên cổng."); gửi `trangThaiPhatHanhHienTai`; bỏ hiển thị "Hạn tự chốt"; hiện `preview.canhBaos` (Task 4) thành `alert-warning` dưới phần chỉ số; hộp kết quả dùng `message` của server, tiêu đề `"Đã điều chỉnh hóa đơn"`.
- `QuanLyChiNhanh.cshtml`: form-text số giờ đổi thành `"Hạn để khách báo sai sót, tính từ lúc công bố hóa đơn (1–168 giờ). Đổi số giờ không làm dời hạn của hóa đơn đã công bố."`.

- [ ] **Step 1:** Sửa `AutoInvoiceUiEndpointTests` theo JSON mới (không còn `laTamTinh`; có `hanPhanHoi`; nút điều chỉnh không phụ thuộc tự động thì kiểm qua endpoint `GET /HoaDon/DieuChinh/{id}` trả `coTheDieuChinh` cho hóa đơn lập tay `ChoDuyet`).
- [ ] **Step 2:** Sửa view/JS. Build xanh.
- [ ] **Step 3:** Phiên chính chạy Web test và kiểm bằng trình duyệt (preview `web`): danh sách, mở hộp điều chỉnh ở 3 trạng thái, tài khoản nhân viên không thấy "Điều chỉnh" ở hóa đơn đã chốt.
- [ ] **Step 4:** Commit `feat(duyet-hoa-don): giao diện màn hóa đơn quản lý`.

### Task 11: Cổng khách thuê

**Người làm:** `sonnet` / `medium`. **Review:** `sonnet` / `medium`.

**Files:** Modify `Web/Areas/KhachThue/Views/HoaDon/Index.cshtml`. Test: Web `TenantInvoicePortalTests.cs`.

**Thay đổi:** bỏ mọi nhánh `laTamTinh` (badge, khối cảnh báo, ẩn nút thanh toán/PDF); hiện chỉ số điện nước cho mọi hóa đơn có chỉ số (bỏ điều kiện tạm tính); khi `hanPhanHoi` khác rỗng hiện `alert-info`: `"Nếu thấy sai sót, vui lòng liên hệ {soDienThoaiChiNhanh} trước {hanPhanHoi}."`.

- [ ] **Step 1:** Test Web: chi tiết hóa đơn vừa công bố có `hanPhanHoi`; hóa đơn công bố đã quá hạn có `hanPhanHoi` rỗng.
- [ ] **Step 2:** Sửa view. Phiên chính chạy Web test và kiểm trình duyệt bằng tài khoản khách test.
- [ ] **Step 3:** Commit `feat(duyet-hoa-don): cổng khách hiển thị hạn phản hồi`.

### Task 12: Cảnh báo bất thường ở màn điện nước

**Người làm:** `sonnet` / `medium`. **Review:** `sonnet` / `medium`.

**Files:**
- Modify: `Web/Areas/QuanLyNhaTro/Controllers/DienNuocController.cs` (endpoint mới), `Views/DienNuoc/_AnhChiSoModal.cshtml`, `Views/DienNuoc/Index.cshtml`
- Modify: `Application/Features/DienNuocs/DTOs/DienNuocPhongRes.cs` (thêm `bool SuaQuaHoaDon`), `Persistence/IDienNuocStore.cs` + `Infrastructure/Persistence/Features/DienNuocStore.cs` (thêm `Task<HashSet<int>> GetRoomIdsWithReviewInvoiceAsync(IReadOnlyList<int> phongTroIds, int thang, int nam)`: phòng có hóa đơn `ChoDuyet` hoặc `DaChot`, chưa xóa, gắn kỳ của tháng), `Services/DienNuocService.cs` (gán cờ; khi cờ bật thì `LyDoKhoa = "Kỳ này đã có hóa đơn chờ duyệt hoặc đã chốt, sửa chỉ số qua hộp Điều chỉnh của hóa đơn."`)
- Test: Web test mới `MeterAnomalyEndpointTests.cs` (`[Collection(WebTestCollection.Name)]`); Application `SaveChotDienNuocWorkflowTests.cs` hoặc test `GetDanhSachPhongs` hiện có (thêm ca cờ `SuaQuaHoaDon`); Infrastructure test cho truy vấn mới

**Interfaces:** `GET /DienNuoc/KiemTraBatThuong?phongTroId&thang&nam&chiSoDienMoi&chiSoNuocMoi` → `{ dien: { maLyDo, moTa } | null, nuoc: ... }`; lỗi dùng `this.ToErrorResult(...)`. Gọi `IMeterAnomalyCheckService.KiemTraAsync(actorId, ...)`.

**Thay đổi:**
- `_AnhChiSoModal.cshtml`: khi ô giá trị xác nhận đổi (debounce 400 ms) gọi endpoint cho đồng hồ của ảnh; có cảnh báo thì hiện `alert-warning` (id `canhBaoBatThuong`) với `moTa`, đánh dấu ô ghi chú bắt buộc (`required` trên label như `canLyDo`) và chặn gửi khi ghi chú trống; hết cảnh báo thì ẩn.
- `Index.cshtml` (chốt điện nước): ô nhập tay điện/nước của mỗi phòng đổi giá trị thì gọi endpoint, hiện cảnh báo nhỏ `text-warning` dưới ô (không chặn, vì nhập tay đã bắt lý do). Dòng có `suaQuaHoaDon` hiện link `"Sửa qua hóa đơn"` tới `/QuanLyNhaTro/QuanLyHoaDon?thang={thang}&nam={nam}`.

- [ ] **Step 1:** Web test: nhân viên đúng chi nhánh → 200 có cảnh báo khi vượt ngưỡng; nhân viên chi nhánh khác → 403; khách thuê → bị chuyển về đăng nhập/403 theo quy tắc hiện có.
- [ ] **Step 2:** Endpoint + view. Build xanh. Phiên chính chạy Web test và kiểm trình duyệt.
- [ ] **Step 3:** Commit `feat(duyet-hoa-don): cảnh báo chỉ số bất thường ở màn điện nước`.

### Task 13: Tài liệu và kiểm chứng cuối

**Người làm:** `haiku` / `medium` (tài liệu). **Review:** phiên chính.

**Files:** `AGENTS.md` (mục Hiện trạng: hóa đơn tự tạo dừng ở nháp, không còn job tự chốt và tạm tính, kiểm tra bất thường theo hợp đồng), `docs/features/hoa-don-tu-dong/spec.md` (đầu file ghi các mục hết hiệu lực, trỏ sang spec mới; sửa dòng "chưa gộp master"), `docs/features/duyet-hoa-don-tu-dong/README.md` (tick checkbox, hiện trạng), `docs/features/README.md`.

- [ ] **Step 1:** Cập nhật tài liệu.
- [ ] **Step 2 (phiên chính):** `dotnet build`; `has-pending-model-changes` (skill `kiem-tra-migration`) → không thay đổi; toàn bộ test qua skill `chay-test-tich-hop`; `git diff --check`.
- [ ] **Step 3 (phiên chính):** Review cả nhánh (`opus` / `xhigh`) + `pr-review-toolkit:silent-failure-hunter` (`sonnet` / `high`); xử lý phát hiện.
- [ ] **Step 4:** Commit `docs(duyet-hoa-don): cập nhật hiện trạng`. Bàn giao (`ban-giao`), báo cáo ngày, chờ người dùng bảo gộp còn 3 commit.
