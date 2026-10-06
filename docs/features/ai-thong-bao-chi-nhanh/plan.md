# AI Assistant và Thông báo theo chi nhánh: kế hoạch triển khai

**Mục tiêu:** làm đúng `spec.md` bản 1.0 (duyệt 05/10/2026) trên nhánh `feature/ai-thong-bao-chi-nhanh`. **Không đổi schema, không migration.**

**Kiến trúc** (đã chốt ở spec §9 và §13.2, tách lớp đầy đủ):

- Application `Features/AiAssistants` có bốn phần:
  - Hợp đồng mô hình AI `IAiChatModel`, không phụ thuộc Gemini.
  - Store interface `IAiAssistantStore`, nhận `allowedBranchIds` hoặc `nguoiThueId`.
  - Bộ công cụ: danh mục theo vai trò, kiểm tham số, chạy truy vấn, ghi nhật ký.
  - `AiAssistantService` điều phối tối đa 3 vòng công cụ, mỗi vòng 5 lời gọi.
- Infrastructure:
  - `AiAssistantStore` (EF).
  - `GeminiChatModel`, chỉ lo HTTP và chuyển định dạng. Lượt model được gửi lại nguyên văn để giữ `thoughtSignature`.
- Web:
  - Hai controller AI (quản lý, khách thuê) lấy danh tính từ claim.
  - Widget chọn endpoint theo cổng, lưu lịch sử theo tài khoản và có nút gợi ý.
- Thông báo:
  - `ThongBaoService.GuiChoNguoiPhuTrachPhongAsync` gửi cho Admin và nhân viên được phân công chi nhánh của phòng, đẩy SignalR từng người.
  - `DanhDauDaDocAsync` kiểm chủ sở hữu.

Bản dành cho dây chuyền `/ship`: `.bangiao/ke-hoach.md` (cùng nội dung kỹ thuật).

---

## Cần người dùng xác nhận (cách đọc số tiền)

Tính năng chỉ **đọc và hiển thị** số tiền qua câu trả lời AI. Không có logic ghi tiền, làm tròn hay đổi trạng thái thanh toán. Các điểm sau đổi cách tính số tiền hiển thị so với hiện tại; phần lớn đã có trong spec §5. Chỉ bắt đầu code khi người dùng xác nhận; riêng A4 còn chờ câu Q-1.

| # | Công cụ | File hiện tại → file mới | Trước | Sau |
|---|---|---|---|---|
| A1 | Q2 hóa đơn chưa thanh toán | `Infrastructure/ExternalServices/AiAssistants/AiAssistantService.cs` `GetHoaDonChuaThanhToanAsync` → `AiAssistantStore.GetUnpaidInvoicesAsync` + `AiToolExecutor` | `!IsDeleted && TrangThaiHoaDon != DaThanhToan && PhongTro.TrangThai == DaThue`; mọi trạng thái phát hành (kể cả Nháp, Chờ duyệt, Đã chốt); toàn hệ thống | `!IsDeleted && TrangThaiPhatHanh == DaGui && TrangThaiHoaDon != DaThanhToan`; bỏ điều kiện trạng thái phòng; lọc chi nhánh. `DaThu` = Σ `LichSuThanhToan.SoTienThanhToan` chưa xóa (như `HoaDonStore.GetUnpaidInvoicesAsync`); `ConLai = TongTien − DaThu`; thêm `soHoaDon`, `tongConNo = Σ ConLai` do Application tính |
| A2 | Q7 công nợ phòng | `GetCongNoPhongAsync` → `FindRoomsByNumberAsync` + `GetUnpaidInvoicesAsync(phongTroId)` | Cộng chung công nợ của **mọi phòng trùng số ở mọi chi nhánh**; mọi trạng thái phát hành | Chọn phòng theo §5.3 (một phòng thì tính, nhiều phòng thì hỏi lại chi nhánh); công nợ theo điều kiện A1 |
| A3 | Q3 doanh thu thực thu | `GetDoanhThuThucThuAsync` → `GetRevenueAsync` | Σ thanh toán `!IsDeleted`, `NgayThanhToan` trong [tuNgay, denNgay cuối ngày] với `DateTime.Parse` theo giờ máy chủ; toàn hệ thống | Ngày nhập là giờ VN → khoảng UTC `[tuNgay 00:00 VN, (denNgay+1) 00:00 VN)`; điều kiện `!l.IsDeleted && !l.HoaDon.IsDeleted`; lọc chi nhánh theo phòng của hóa đơn; kèm `soGiaoDich`; mặc định từ đầu tháng hiện tại đến hôm nay |
| A4 | Q8 doanh thu theo chi nhánh | `GetDoanhThuChiNhanhAsync` → `GetRevenueByBranchAsync` | Σ thanh toán `!l.IsDeleted && !HoaDon.IsDeleted` của hóa đơn có **kỳ** `Thang/Nam` = tham số (0 = bỏ lọc); toàn hệ thống; gom theo tên chi nhánh | Giữ cách tính theo kỳ hóa đơn **(chờ Q-1)**; tháng/năm mặc định hiện tại, không còn "0 = mọi kỳ"; lọc chi nhánh được phân công; gom theo `ChiNhanhId`; thêm `soGiaoDich` từng chi nhánh và `tongDoanhThu` |
| A5 | K3 hóa đơn chưa thanh toán của tôi | `GetMyHoaDonChuaThanhToanAsync` → `GetTenantUnpaidInvoicesAsync` | `DaGui && != DaThanhToan && !IsDeleted` của hợp đồng có `NguoiThueId` | Giữ điều kiện; thêm lọc kỳ tùy chọn (§3.4), `soHoaDon`, `tongConNo` |
| A6 | K1 hợp đồng của tôi | `GetMyHopDongInfoAsync` | Hiển thị `TienThuePhong`, `TienCocPhong` của **một** hợp đồng đầu tiên | Giữ hai số tiền; trả **mọi** hợp đồng đang hoạt động của khách (thường là một) |

ĐÃ XÁC NHẬN: người dùng đồng ý toàn bộ A1, A2, A3, A4, A5, A6 như bảng trên; A4 tính theo kỳ hóa đơn (Q-1 chọn a) (05/10/2026 23:03)

## Câu hỏi còn bỏ ngỏ

- **Q-1 (chặn A4).** Q8 "doanh thu theo từng chi nhánh trong tháng m/yyyy" tính theo **kỳ của hóa đơn** (như code hiện tại và như ô "Doanh thu tháng này" trên Dashboard) hay theo **ngày thanh toán** nằm trong tháng đó (như Q3)? Spec không nói. Kế hoạch viết theo kỳ hóa đơn; nếu người dùng chọn ngày thanh toán thì `GetRevenueByBranchAsync` lọc `NgayThanhToan` trong `MeterPeriodPolicy.MonthRangeUtc(new MeterPeriod(thang, nam))`.
- **Q-2.** Spec yêu cầu báo lỗi khi tham số "ngoài khoảng" nhưng chỉ nêu ví dụ (tháng 13, N âm). Khoảng đề xuất: năm 2000–2100 (cùng mốc `min="2000"` ở `Areas/QuanLyNhaTro/Views/DienNuoc/Index.cshtml`), N (số ngày Q5) 1–365. Người dùng không trả lời thì dùng các giá trị này.

**Trả lời của người dùng (05/10/2026 23:03):**
- Q-1: chọn (a) — Q8 tính theo **kỳ của hóa đơn** (`HoaDon.Thang/Nam`), giữ như kế hoạch.
- Q-2: giữ đề xuất — năm 2000–2100, N (Q5) 1–365.

## Bổ sung theo người dùng (05/10/2026 23:03): hai lỗi có sẵn gộp vào đợt này

- **F1. Sự cố phải thuộc phòng của khách.** `Areas/KhachThue/Controllers/SuCoController.cs` action tạo sự cố (POST): trước khi gọi `_yeuCauSuCoService.CreateAsync`, kiểm `model.PhongTroId` nằm trong các phòng của hợp đồng thuộc `NguoiThueId` trong claim (cùng nguồn với `Index`: `_hopDongService.GetHopDongsByNguoiThueIdAsync`). Không thuộc → trả JSON `success = false`, thông điệp "Phòng không thuộc hợp đồng của bạn.", không tạo sự cố, không gửi thông báo. Ưu tiên đặt kiểm tra ở Application (`YeuCauSuCoService.CreateAsync` nhận/kiểm `NguoiThueId`) nếu làm được mà không đổi schema; nếu không thì kiểm ở controller. Test: Web.IntegrationTests — khách thuê gửi `PhongTroId` của phòng không thuộc hợp đồng mình bị từ chối và không phát sinh thông báo; (nếu đặt ở Application) unit test tương ứng.
- **F2. Chống XSS trong widget chat.** `Views/Shared/_AiChatWidget.cshtml`: `parseMarkdown` phải **escape HTML** (`&`, `<`, `>`, `"`, `'`) của toàn bộ văn bản trước khi chuyển các mẫu Markdown được hỗ trợ (in đậm, in nghiêng, danh sách, xuống dòng…) thành thẻ. Tin nhắn của người dùng hiển thị bằng `textContent`. Bỏ đoạn khôi phục lịch sử dùng `el.innerHTML.includes(...)`/`parseMarkdown(el.textContent)` nếu nó cho phép chèn HTML; lịch sử khôi phục cũng phải đi qua cùng hàm escape. Không cho phép link `javascript:`. Test: không có khung JS test trong repo → ghi "chưa kiểm chứng tự động", kiểm tay bằng tên người thuê chứa `<img src=x onerror=alert(1)>` (đưa vào danh sách thử tay §11).

Danh sách việc bổ sung:
- [x] F1: kiểm phòng thuộc hợp đồng của khách khi tạo sự cố + test.
- [x] F2: escape HTML trong `parseMarkdown` của widget + kiểm tay (người dùng kiểm đạt 06/10/2026).

---

## Danh sách việc

### A. Application

- [x] Hợp đồng mô hình AI `Features/AiAssistants/ChatModel/IAiChatModel.cs` (§3.1).
- [x] Rút gọn `AiAssistantDTOs.cs`; thêm `AiAssistantQueryRows.cs`; thêm `IAiAssistantStore` (§3.2).
- [x] `Tools/`: `AiCallerContext`, `AiToolNames`, `AiToolCatalog`, `AiToolArgs`, `AiToolResult`, `AiToolExecutor`, `AiPrompts` (§3.3–§3.5).
- [x] Chuyển `IAiAssistantService` sang `Features/AiAssistants/Services`, viết `AiAssistantService` (§3.6).
- [x] Thông báo: `IThongBaoStore`, `IThongBaoService`, `ThongBaoService` (§3.7).
- [x] DI trong `DependencyInjection.cs` (§3.8).

### B. Infrastructure

- [x] `AiAssistantStore` (§4.1).
- [x] `GeminiChatModel` (§4.2).
- [x] `ThongBaoStore`, `DependencyInjection.cs`; xóa `ExternalServices/AiAssistants/AiAssistantService.cs` (§4.3).

### C. Web

- [x] `QuanLyNhaTro/AiAssistantController` gọi `ChatQuanLyAsync`; thêm `KhachThue/AiAssistantController` (§5).
- [x] `KhachThue/SuCoController` gửi theo chi nhánh của phòng, lỗi chỉ ghi cảnh báo (§5).
- [x] `Controllers/ThongBaoController.DanhDauDaDoc` truyền id người dùng (§5).
- [x] Widget: endpoint theo cổng, nút gợi ý, khóa lịch sử theo tài khoản, giới hạn 1.000 ký tự, gửi 10 lượt; hai layout đặt `AiChatPortal`; trang đăng nhập xóa lịch sử (§5).

### D. Test

- [x] Application.UnitTests: fakes, `AiAssistantServiceTests`, `AiToolExecutorTests`, `ThongBaoServiceTests`, cập nhật `BranchScopeArchitectureTests` (§7.1).
- [x] Infrastructure.IntegrationTests: `AiAssistantStoreTests`, `ThongBaoStoreTests`, `GeminiChatModelTests` thay `AiAssistantServiceTests` (§7.2).
- [x] Web.IntegrationTests: `FakeAiChatModel` trong factory, `AiAssistantEndpointTests`, `ThongBaoChiNhanhTests` (§7.3).

### E. Kiểm chứng và tài liệu

- [x] `dotnet build`, unit test, integration test trên DB `_test`, `has-pending-model-changes`, `Migrations/` không đổi, `git diff --check` (§8).
- [x] Cập nhật checkbox ở `README.md` của tính năng (§9).
- [x] Thử tay trên app theo điều kiện nghiệm thu spec §11 (người dùng kiểm: chatbox và thông báo sự cố đạt, 06/10/2026).

---

## 1. Thứ tự thực hiện

1. Application: hợp đồng mô hình AI → DTO/store interface → bộ công cụ → service → thông báo → DI.
2. Infrastructure: `AiAssistantStore`, `GeminiChatModel`, `ThongBaoStore`, DI; xóa service AI cũ.
3. Web: hai controller AI, `SuCoController`, `ThongBaoController`, widget và layout, trang đăng nhập.
4. Test theo tầng (mục 7), rồi lệnh kiểm chứng (mục 8).

Domain: **không đổi**.

## 2. Ranh giới tầng phải giữ

- Application chỉ dùng BCL (`System.Text.Json`, `System.Text.Encodings.Web` được phép) và `Microsoft.Extensions.Logging.Abstractions`. Không EF Core, ASP.NET Core, `IConfiguration`, `HttpClient`, `IQueryable`.
- Interface trong namespace `*.Features.*.Services` không trả `object`/`Task<object>` hay entity Domain (`ArchitectureBoundaryTests` của Application.UnitTests).
- Web không chứa chuỗi `QuanLyChoThuePhongTroWeb.Domain` trong `.cs`/`.cshtml` (`Web.IntegrationTests/ArchitectureBoundaryTests`).
- Gemini (HTTP, JSON, `IConfiguration` khóa `Gemini:ApiKey`, `Gemini:Model`) chỉ nằm ở Infrastructure.

## 3. Application (`src/QuanLyChoThuePhongTroWeb.Application/`)

### 3.1 Hợp đồng mô hình AI (mới)

File `Features/AiAssistants/ChatModel/IAiChatModel.cs`, namespace `QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel`:

```csharp
public interface IAiChatModel
{
    Task<AiModelResult> GenerateAsync(AiModelRequest request, CancellationToken cancellationToken = default);
}

public enum AiTurnRole { User, Model }
public abstract record AiPart;
public sealed record AiTextPart(string Text) : AiPart;
public sealed record AiFunctionCallPart(AiFunctionCall Call) : AiPart;
public sealed record AiFunctionResponsePart(string Name, string? CallId, string ResponseJson) : AiPart; // ResponseJson luôn là một JSON object
// RawProviderContent: JSON gốc của lượt model do client trả về; khác null thì client gửi lại nguyên văn (giữ thoughtSignature của Gemini).
public sealed record AiTurn(AiTurnRole Role, IReadOnlyList<AiPart> Parts, string? RawProviderContent = null);
public sealed record AiFunctionCall(string Name, string ArgumentsJson, string? Id); // ArgumentsJson = "{}" khi không có args

public enum AiToolParameterType { String, Integer, Number }
public sealed record AiToolParameter(string Name, AiToolParameterType Type, string Description, bool Required = false);
public sealed record AiToolDefinition(string Name, string Description, IReadOnlyList<AiToolParameter> Parameters);

public sealed record AiModelRequest(string SystemInstruction, IReadOnlyList<AiTurn> Turns,
    IReadOnlyList<AiToolDefinition> Tools, bool AllowToolCalls);

public enum AiModelErrorKind { None, NotConfigured, HttpError, Unavailable, EmptyResponse }
public sealed record AiModelResult(bool Success, AiModelErrorKind ErrorKind, int? HttpStatusCode,
    string? Text, IReadOnlyList<AiFunctionCall> FunctionCalls, AiTurn? ModelTurn)
{
    // Ok(text, calls, modelTurn) và Fail(kind, httpStatusCode = null) dạng static
}
```

### 3.2 DTO và store interface

- **Sửa** `Features/AiAssistants/DTOs/AiAssistantDTOs.cs`: giữ `ChatRequest`, `ChatMessageDto`; **xóa** `PhongTroChuaChotRes`, `HoaDonChuaThanhToanRes`, `PhongTrongRes`, `HopDongSapHetHanRes`, `ThongTinKhachThueRes`, `DoanhThuChiNhanhRes`, `ChiSoDienNuocRes` (chỉ service cũ dùng).
- **Mới** `Features/AiAssistants/DTOs/AiAssistantQueryRows.cs` (dòng dữ liệu store trả về, thời gian để nguyên UTC, enum Domain được phép):
  - `AiRoomRow(int PhongTroId, string SoPhong, int TangLau, int ChiNhanhId, string TenChiNhanh, decimal GiaThue)`
  - `AiUnpaidInvoiceRow(int HoaDonId, string MaHoaDon, int PhongTroId, string SoPhong, string TenChiNhanh, string KhachThue, int Thang, int Nam, decimal TongTien, decimal DaThu)`
  - `AiRevenueTotalRow(decimal TongTien, int SoGiaoDich)`
  - `AiBranchRevenueRow(int ChiNhanhId, string TenChiNhanh, decimal TongTien, int SoGiaoDich)`
  - `AiExpiringContractRow(string SoPhong, string TenChiNhanh, string KhachThue, DateTime ThoiDiemKetThucUtc)`
  - `AiTenantLookupRow(string SoPhong, string TenChiNhanh, string HoVaTen, string SoDienThoai, TrangThaiHopDong TrangThaiHopDong)`
  - `AiMeterReadingRow(int PhongTroId, string SoPhong, string TenChiNhanh, int Thang, int Nam, decimal ChiSoDienCu, decimal ChiSoDienMoi, decimal ChiSoNuocCu, decimal ChiSoNuocMoi)`
  - `AiTenantContractRow(string MaHopDong, int PhongTroId, string SoPhong, string TenChiNhanh, DateTime ThoiDiemBatDauUtc, DateTime? ThoiDiemKetThucUtc, decimal TienThuePhong, decimal TienCocPhong)`
- **Mới** `Features/AiAssistants/Persistence/IAiAssistantStore.cs`. Quy ước tham số `allowedBranchIds` copy từ `Application/Features/Dashboard/Persistence/IDashboardStore.cs`: `null` = không giới hạn (Admin), rỗng = không thấy gì.

```csharp
public interface IAiAssistantStore
{
    Task<IReadOnlyList<AiRoomRow>> GetRentedRoomsWithoutMeterReadingAsync(int thang, int nam, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);            // Q1
    Task<IReadOnlyList<AiUnpaidInvoiceRow>> GetUnpaidInvoicesAsync(int? thang, int? nam, int? phongTroId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default); // Q2, Q7
    Task<AiRevenueTotalRow> GetRevenueAsync(DateTime fromUtc, DateTime toExclusiveUtc, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);               // Q3
    Task<IReadOnlyList<AiRoomRow>> GetVacantRoomsAsync(decimal? giaToiDa, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);                         // Q4
    Task<IReadOnlyList<AiExpiringContractRow>> GetExpiringContractsAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default); // Q5
    Task<IReadOnlyList<AiTenantLookupRow>> SearchTenantsAsync(string tuKhoa, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);                       // Q6
    Task<IReadOnlyList<AiRoomRow>> FindRoomsByNumberAsync(string soPhong, string? tenChiNhanh, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);     // Q7, Q9
    Task<IReadOnlyList<AiBranchRevenueRow>> GetRevenueByBranchAsync(int thang, int nam, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);            // Q8
    Task<IReadOnlyList<AiMeterReadingRow>> GetMeterReadingsAsync(IReadOnlyCollection<int> phongTroIds, int thang, int nam, CancellationToken ct = default);                     // Q9, K2
    Task<IReadOnlyList<AiTenantContractRow>> GetActiveContractsOfTenantAsync(int nguoiThueId, CancellationToken ct = default);                                                  // K1, K2
    Task<IReadOnlyList<AiUnpaidInvoiceRow>> GetTenantUnpaidInvoicesAsync(int nguoiThueId, int? thang, int? nam, CancellationToken ct = default);                                // K3
}
```

### 3.3 Bộ công cụ (`Features/AiAssistants/Tools/`, mới)

- `AiCallerContext.cs`: `public enum AiCallerRole { Admin, NhanVien, KhachThue }` và `public sealed record AiCallerContext(int NguoiDungId, AiCallerRole Role, IReadOnlyCollection<int>? AllowedBranchIds, int? NguoiThueId);`
- `AiToolNames.cs`: hằng tên. **Giữ tên cũ** để tương thích: Q1 `GetPhongTroChuaChotDienNuocAsync`, Q2 `GetHoaDonChuaThanhToanAsync`, Q3 `GetDoanhThuThucThuAsync`, Q4 `GetPhongTrongAsync`, Q5 `GetHopDongSapHetHanAsync`, Q6 `GetThongTinKhachThueAsync`, Q7 `GetCongNoPhongAsync`, Q8 `GetDoanhThuChiNhanhAsync`, Q9 `GetChiSoDienNuocAsync`, K1 `GetMyHopDongInfoAsync`, K2 `GetMyChiSoDienNuocAsync`, K3 `GetMyHoaDonChuaThanhToanAsync`.
- `AiToolCatalog.cs` (static): `IReadOnlyList<AiToolDefinition> ForRole(AiCallerRole role)` (Admin và NhanVien: Q1–Q9; KhachThue: K1–K3) và `bool IsAllowed(AiCallerRole role, string toolName)`. Tham số khai báo (không cái nào `Required`, trừ chỗ ghi **bắt buộc**):

| Công cụ | Tham số |
|---|---|
| Q1, Q9, K2 | `thang` INTEGER, `nam` INTEGER (Q9 thêm `soPhong` STRING **bắt buộc**, `tenChiNhanh` STRING) |
| Q2, K3, Q8 | `thang` INTEGER, `nam` INTEGER |
| Q3 | `tuNgay` STRING, `denNgay` STRING (dạng YYYY-MM-DD, giờ Việt Nam) |
| Q4 | `mucGiaToiDa` NUMBER |
| Q5 | `soNgay` INTEGER |
| Q6 | `tuKhoa` STRING **bắt buộc** |
| Q7 | `soPhong` STRING **bắt buộc**, `tenChiNhanh` STRING |
| K1 | không có |

  Mô tả công cụ ghi rõ mặc định (§3.4). K-tool không có tham số nào chứa id người thuê.
- `AiToolArgs.cs`: đọc `ArgumentsJson` bằng `JsonDocument`. JSON hỏng hoặc không phải object → lỗi "Tham số không hợp lệ.". Số nguyên nhận JSON number có giá trị nguyên hoặc chuỗi số; với `thang`/`nam`, giá trị `0` coi như không truyền. Ngày: `DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, ...)`. Tham số lạ (ví dụ `nguoiThueId`) bị bỏ qua.
- `AiToolResult.cs`: `public sealed record AiToolResult(bool Success, string Outcome, int ResultCount, string Json);` với `Outcome` ∈ `ThanhCong`, `LoiThamSo`, `KhongKhaDung`, `VuotGioiHan`, `LoiTruyVan`. Hai factory: `Ok(payload, soLuong)` và `Error(maLoi, thongBao)`. Serialize bằng `JsonSerializerOptions { PropertyNamingPolicy = CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }`.
- `AiToolExecutor.cs` (class, đăng ký scoped):

```csharp
public AiToolExecutor(IAiAssistantStore store, ILogger<AiToolExecutor> logger, TimeProvider? timeProvider = null) // mẫu: MeterImagePortalService
public Task<AiToolResult> ExecuteAsync(AiCallerContext caller, AiFunctionCall call, CancellationToken ct = default);
```

  Trình tự trong `ExecuteAsync`:
  1. `!AiToolCatalog.IsAllowed(caller.Role, call.Name)` (kể cả tên lạ) → `Error("CONG_CU_KHONG_KHA_DUNG", "Công cụ không khả dụng.")`, **không gọi store**.
  2. Phạm vi: Admin → `null`; NhanVien → `caller.AllowedBranchIds ?? Array.Empty<int>()` (fail closed); K-tool cần `NguoiThueId > 0`, không có thì lỗi như bước 1. K-tool **luôn** lấy `caller.NguoiThueId`, không lấy từ args.
  3. Đọc và kiểm tham số (§3.4). Sai → `Error("THAM_SO_KHONG_HOP_LE", <câu tiếng Việt>)`, không gọi store.
  4. Gọi store, dựng kết quả (§3.5). Các phép cộng (`ConLai`, `tongConNo`, `tongDoanhThu`, tiêu thụ) làm ở đây.
  5. Ngoại lệ không phải `OperationCanceledException` → `LogWarning(ex, ...)` rồi `Error("LOI_TRUY_VAN", "Không tra cứu được dữ liệu lúc này.")`. Không đưa `ex.Message` vào JSON.
  6. **Nhật ký** đúng một dòng mỗi lời gọi: `LogInformation("AI công cụ {ToolName}: người dùng {NguoiDungId} ({Role}), {Outcome}, {ResultCount} kết quả, {ElapsedMs} ms", ...)` (đo bằng `Stopwatch`). Không ghi câu hỏi, args, từ khóa, số phòng, tên chi nhánh hay dữ liệu trả về.

- `AiPrompts.cs` (static): `string ForRole(AiCallerRole role, DateTime nowUtc)` và `IReadOnlyList<string> Suggestions(AiCallerRole role)`.
  - Gợi ý quản lý: "Hóa đơn nào chưa thanh toán?", "Phòng nào còn trống?", "Hợp đồng nào hết hạn trong 30 ngày tới?", "Phòng nào chưa chốt điện nước tháng này?", "Doanh thu tháng này?". Khách thuê: "Hợp đồng của tôi", "Tôi còn nợ hóa đơn nào?", "Chỉ số điện nước tháng này".
  - Prompt phải chứa: vai trò; "Hôm nay là dd/MM/yyyy (giờ Việt Nam)"; chỉ dùng dữ liệu từ công cụ, không bịa số; kết quả `soLuong = 0` thì nói rõ là không có; câu hỏi ngoài các công cụ thì trả lời đúng câu "Tôi chưa hỗ trợ câu hỏi này." kèm 2–3 gợi ý (liệt kê các gợi ý của vai trò); chào hỏi xã giao thì trả lời bình thường; `thanhCong = false` với `THAM_SO_KHONG_HOP_LE` thì hỏi lại người dùng, không tự đoán; `canChonChiNhanh = true` thì hỏi người dùng chọn chi nhánh; `CONG_CU_KHONG_KHA_DUNG` thì nói chưa hỗ trợ; được gọi nhiều công cụ cùng lúc; Q2/K3 không nói tháng thì gọi ngay không truyền tháng/năm, không hỏi lại; **không** thêm ghi chú nguồn dữ liệu; trả lời tiếng Việt bằng Markdown. Prompt khách thuê thêm: không tiết lộ thông tin người khác.
  - Hằng `HetLuotCongCu` = "Đã hết lượt tra cứu. Hãy trả lời bằng dữ liệu đã có và nói rõ phần nào chưa tra cứu được."

### 3.4 Mặc định và kiểm tra tham số

`nowVn = timeProvider.GetUtcNow().UtcDateTime.AddHours(7)`; tháng/năm hiện tại dùng `MeterPeriodPolicy.CurrentVn(nowUtc)` (`Features/DienNuocs/Services/MeterPeriodPolicy.cs`).

| Tham số | Hợp lệ | Thông báo lỗi |
|---|---|---|
| `thang` | 1–12 | "Tháng phải là số nguyên từ 1 đến 12." |
| `nam` | 2000–2100 (Q-2) | "Năm phải là số nguyên từ 2000 đến 2100." |
| `soNgay` | 1–365 (Q-2) | "Số ngày phải là số nguyên từ 1 đến 365." |
| `tuNgay`, `denNgay` | `yyyy-MM-dd` có thật; `tuNgay ≤ denNgay` | "Ngày phải có dạng YYYY-MM-DD." / "Từ ngày phải trước hoặc bằng đến ngày." |
| `mucGiaToiDa` | số > 0 | "Mức giá tối đa phải là số lớn hơn 0." |
| `tuKhoa` | khác rỗng sau `Trim()` | "Cần nhập tên khách thuê hoặc số phòng để tra cứu." |
| `soPhong` | khác rỗng sau `Trim()` | "Cần cho biết số phòng." |

Mặc định:
- Q1, Q8, Q9, K2: thiếu `thang` → tháng VN hiện tại; thiếu `nam` → năm VN hiện tại.
- Q2, K3: thiếu cả hai → mọi kỳ (`null, null`); có `thang` thiếu `nam` → `nam` = năm VN hiện tại; có `nam` thiếu `thang` → cả năm.
- Q3: thiếu `tuNgay` → ngày 1 tháng VN hiện tại; thiếu `denNgay` → hôm nay (VN). Đổi sang UTC: `fromUtc = SpecifyKind(tuNgay 00:00 − 7h, Utc)`, `toExclusiveUtc = SpecifyKind((denNgay + 1 ngày) 00:00 − 7h, Utc)`.
- Q4: không có `mucGiaToiDa` → không lọc giá. Q5: thiếu `soNgay` → 30; khoảng `[nowUtc, nowUtc.AddDays(N)]`.

### 3.5 Kết quả JSON từng công cụ

Mọi kết quả thành công có `thanhCong: true` và `soLuong`. Lỗi: `{ "thanhCong": false, "maLoi": "...", "thongBao": "..." }`. Tiền là số (decimal), kèm `donVi: "VND"` ở kết quả có tiền. Ngày hiển thị `dd/MM/yyyy` theo giờ VN (`AddHours(7)`), `CultureInfo.InvariantCulture`.

| Công cụ | Trường |
|---|---|
| Q1 | `thang, nam, soLuong, phongs[{soPhong, tangLau, chiNhanh, giaThue}]` |
| Q2 | `kyLoc` ("tất cả các kỳ" / "tháng m/yyyy" / "năm yyyy"), `soLuong = soHoaDon`, `soHoaDon, tongConNo, donVi, hoaDons[{maHoaDon, soPhong, chiNhanh, khachThue, thang, nam, tongTien, daThu, conLai}]` |
| Q3 | `tuNgay, denNgay, soLuong = soGiaoDich, soGiaoDich, tongDoanhThu, donVi` |
| Q4 | `mucGiaToiDa` (null nếu không lọc), `soLuong, phongs[{soPhong, tangLau, chiNhanh, giaThue}]` |
| Q5 | `soNgay, soLuong, hopDongs[{soPhong, chiNhanh, khachThue, ngayKetThuc, soNgayConLai}]`; `soNgayConLai = (ketThucVn.Date − nowVn.Date).Days` |
| Q6 | `soLuong, ketQua[{soPhong, chiNhanh, hoVaTen, soDienThoai, trangThaiHopDong}]`; nhãn: DangHoatDong "Đang hoạt động", DaKetThuc "Đã kết thúc", DaHuy "Đã hủy" |
| Q7 | §5.3. Một phòng: `timThayPhong: true, soPhong, chiNhanh, soLuong = soHoaDon, soHoaDon, tongConNo, donVi, hoaDons[{maHoaDon, thang, nam, tongTien, daThu, conLai}]` (gọi `GetUnpaidInvoicesAsync(null, null, phongTroId, allowed)`) |
| Q8 | `thang, nam, soLuong` (số chi nhánh có dòng), `tongDoanhThu, donVi, chiNhanhs[{chiNhanh, tongDoanhThu, soGiaoDich}]` |
| Q9 | §5.3. Một phòng: `timThayPhong: true, soPhong, chiNhanh, thang, nam, soLuong` (0/1), `chiSo{dienCu, dienMoi, dienTieuThu, nuocCu, nuocMoi, nuocTieuThu}` hoặc `null` |
| K1 | `soLuong, hopDongs[{maHopDong, soPhong, chiNhanh, ngayBatDau, ngayKetThuc ("Không thời hạn" nếu null), giaThue, tienCoc}]` |
| K2 | `thang, nam, soLuong, chiSos[{soPhong, chiNhanh, dienCu, dienMoi, dienTieuThu, nuocCu, nuocMoi, nuocTieuThu}]`; phòng lấy từ `GetActiveContractsOfTenantAsync`; không có hợp đồng: `soLuong: 0, thongBao: "Bạn không có hợp đồng đang hoạt động."` |
| K3 | `kyLoc, soLuong = soHoaDon, soHoaDon, tongConNo, donVi, hoaDons[{maHoaDon, soPhong, thang, nam, tongTien, daThu, conLai}]` |

§5.3 cho Q7/Q9 (`FindRoomsByNumberAsync` trong phạm vi người hỏi):
- 0 phòng: `{thanhCong: true, timThayPhong: false, soLuong: 0, thongBao: "Không tìm thấy phòng."}`. Phòng chi nhánh ngoài phạm vi cũng ra kết quả này.
- Nhiều phòng: `{thanhCong: true, timThayPhong: true, canChonChiNhanh: true, soLuong: <số phòng>, cacChiNhanh: [tên...], huongDan: "Có nhiều phòng cùng số, hãy hỏi người dùng muốn xem chi nhánh nào."}`. Không truy vấn hóa đơn hay chỉ số.

### 3.6 Service điều phối

- **Xóa** `Abstractions/Services/IAiAssistantService.cs`.
- **Mới** `Features/AiAssistants/Services/IAiAssistantService.cs`:

```csharp
public interface IAiAssistantService
{
    Task<ServiceResult> ChatQuanLyAsync(int actorId, ChatRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ChatKhachThueAsync(int nguoiDungId, int nguoiThueId, ChatRequest request, CancellationToken cancellationToken = default);
}
```

- **Mới** `Features/AiAssistants/Services/AiAssistantService.cs`; constructor `(IAiChatModel model, AiToolExecutor executor, IEmployeeAccessService access, ILogger<AiAssistantService> logger, TimeProvider? timeProvider = null)`. Hằng `MaxMessageLength = 1000`, `MaxHistoryItems = 10`, `MaxToolRounds = 3`, `MaxCallsPerRound = 5`.
  1. Kiểm tra đầu vào (cả hai hàm): `request?.Message` trim rỗng → `Fail("Nội dung tin nhắn không được trống.")`; dài hơn 1000 ký tự → `Fail("Câu hỏi tối đa 1.000 ký tự.")`. Không gọi model.
  2. `ChatQuanLyAsync`: `scope = await access.GetScopeAsync(actorId)`. `null` → `ServiceResult.Forbidden("Bạn không có quyền sử dụng trợ lý AI.")`. `!scope.IsAdmin && scope.ActiveBranchIds.Count == 0` → `ServiceResult.Ok("Bạn chưa được phân công chi nhánh nào.")`, không gọi model. Caller = `(actorId, IsAdmin ? Admin : NhanVien, scope.AllowedBranchIds, null)`.
  3. `ChatKhachThueAsync`: `nguoiThueId <= 0` → `ServiceResult.Forbidden("Tài khoản chưa liên kết hồ sơ người thuê.")`. Caller = `(nguoiDungId, KhachThue, null, nguoiThueId)`.
  4. Lịch sử: `History == null` coi như rỗng; bỏ phần tử `null`, `Role` khác đúng `"user"`/`"model"` (phân biệt hoa thường), `Message` rỗng; lấy **10 phần tử hợp lệ cuối**, giữ thứ tự; mỗi phần tử thành `AiTurn(User|Model, [AiTextPart])`. Thêm câu hỏi hiện tại làm lượt `User` cuối.
  5. Vòng lặp `round = 1..3`: gọi `model.GenerateAsync(new AiModelRequest(prompt, turns, AiToolCatalog.ForRole(role), AllowToolCalls: true))`.
     - Lỗi → trả `Fail` theo bảng dưới.
     - Không có `FunctionCalls` → `Ok(Text)`.
     - Có lời gọi: thêm `result.ModelTurn` vào `turns`; với lời gọi thứ i (0-based): `i < 5` → `executor.ExecuteAsync`; `i ≥ 5` → `Error("VUOT_GIOI_HAN", "Đã vượt quá 5 lời gọi công cụ trong một lượt; yêu cầu này chưa được xử lý.")`, không chạy. Thêm **một** lượt `AiTurn(User, [AiFunctionResponsePart(call.Name, call.Id, result.Json) ...])` đúng thứ tự, đủ số phần tử như số lời gọi.
  6. Sau vòng 3 vẫn còn lời gọi: gọi model lần thứ 4 với `SystemInstruction = prompt + "\n" + AiPrompts.HetLuotCongCu`, `AllowToolCalls: false`. Có `Text` → `Ok(Text)` (bỏ qua lời gọi công cụ nếu có). Không có → `Ok("Tôi chưa thể hoàn tất câu trả lời vì câu hỏi cần quá nhiều bước tra cứu. Bạn hãy tách thành câu hỏi nhỏ hơn.")`. Tổng tối đa 4 lần gọi model, 15 lần chạy công cụ.

| `AiModelErrorKind` | Thông báo (`ServiceResult.Fail`) |
|---|---|
| NotConfigured | "Lỗi cấu hình: Chưa thiết lập Gemini API Key trong hệ thống." |
| HttpError | "Trợ lý AI tạm thời không phản hồi (HTTP {code}). Vui lòng thử lại sau." |
| Unavailable, EmptyResponse | "Trợ lý AI tạm thời không phản hồi. Vui lòng thử lại sau." |

### 3.7 Thông báo

- **Sửa** `Features/ThongBaos/Persistence/IThongBaoStore.cs`: **xóa** `GetByIdAsync` (chỉ `DanhDauDaDocAsync` dùng); **thêm** `Task<List<int>> GetRoomResponsibleUserIdsAsync(int phongTroId, CancellationToken cancellationToken = default);`.
- **Sửa** `Features/ThongBaos/Services/IThongBaoService.cs` + `ThongBaoService.cs`:
  - `Task<ServiceResult> DanhDauDaDocAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default);` dùng `_store.GetByIdAndUserAsync`. Không thấy → `ServiceResult.NotFound("Không tìm thấy thông báo.")`. Phần còn lại giữ nguyên.
  - Thêm `Task<ServiceResult> GuiChoNguoiPhuTrachPhongAsync(int phongTroId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default);`. Lấy `GetRoomResponsibleUserIdsAsync` rồi `Distinct()`. Rỗng → `Fail("Không có người nhận thông báo.")`. Tạo một `ThongBao` cho mỗi người (`CreatedAt = DateTime.UtcNow`), `AddRange`, `SaveChangesAsync`. Sau khi lưu, gọi `SendToUserAsync` **từng người** với payload giống `GuiChoNguoiDungAsync` (`id, tieuDe, noiDung, linhVuc, linkDieuHuong, createdAt`), mỗi lần bọc `try/catch` → `LogWarning`. **Không** gọi `SendToRoleGroupAsync`. Mẫu vòng gửi: `PushNotificationsAsync` trong `Features/HoaDons/Payments/InvoicePaymentTransaction.cs`.
  - Constructor thêm tham số cuối `ILogger<ThongBaoService>? logger = null` (null → `NullLogger`).
  - `GuiChoQuyenAsync` giữ nguyên, không xóa.

### 3.8 DI

**Sửa** `DependencyInjection.cs`: `services.AddScoped<Features.AiAssistants.Services.IAiAssistantService, Features.AiAssistants.Services.AiAssistantService>();` và `services.AddScoped<Features.AiAssistants.Tools.AiToolExecutor>();`.

## 4. Infrastructure (`src/QuanLyChoThuePhongTroWeb.Infrastructure/`)

### 4.1 `Persistence/Features/AiAssistantStore.cs` (mới)

Copy kiểu viết từ `Persistence/Features/DashboardStore.cs`: `AsNoTracking`, chiếu thẳng sang row, `if (allowedBranchIds != null) query = query.Where(x => allowedBranchIds.Contains(...ChiNhanhId))`. Tổng đã thu viết như `HoaDonStore.GetUnpaidInvoicesAsync`: `h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan)`.

| Hàm | Điều kiện |
|---|---|
| `GetRentedRoomsWithoutMeterReadingAsync` | `PhongTro !IsDeleted && TrangThai == DaThue`, không có `DichVuDienNuocCuaPhong` `!IsDeleted` cùng `PhongTroId/Thang/Nam` (giữ điều kiện hiện tại); lọc `p.ChiNhanhId`; sắp theo tên chi nhánh, số phòng |
| `GetUnpaidInvoicesAsync` | `!h.IsDeleted && h.TrangThaiPhatHanh == DaGui && h.TrangThaiHoaDon != DaThanhToan`; `thang`/`nam`/`phongTroId` (`h.HopDong.PhongTroId`) lọc khi có; lọc `h.HopDong.PhongTro.ChiNhanhId`; **không** xét trạng thái phòng; sắp `Nam, Thang, TenChiNhanh, SoPhong` |
| `GetRevenueAsync` | `!l.IsDeleted && !l.HoaDon.IsDeleted && l.NgayThanhToan >= fromUtc && l.NgayThanhToan < toExclusiveUtc`; lọc `l.HoaDon.HopDong.PhongTro.ChiNhanhId`; `SumAsync` + `CountAsync` |
| `GetVacantRoomsAsync` | `!IsDeleted && TrangThai == Trong`, `GiaThue <= giaToiDa` khi có; lọc chi nhánh |
| `GetExpiringContractsAsync` | `!IsDeleted && TrangThaiHopDong == DangHoatDong && ThoiDiemKetThuc != null && ThoiDiemKetThuc >= fromUtc && <= toUtc`; lọc `PhongTro.ChiNhanhId`; sắp theo ngày kết thúc |
| `SearchTenantsAsync` | `HopDong !IsDeleted && !NguoiThue.IsDeleted && (HoVaTen.ToLower().Contains(k) \|\| PhongTro.SoPhong.ToLower().Contains(k))` với `k = tuKhoa.Trim().ToLower()`; lọc chi nhánh |
| `FindRoomsByNumberAsync` | `!p.IsDeleted && p.SoPhong.Trim().ToLower() == soPhong.Trim().ToLower()`; `tenChiNhanh` có giá trị → `p.ChiNhanh.TenChiNhanh.ToLower().Contains(ten.Trim().ToLower())`; lọc chi nhánh |
| `GetRevenueByBranchAsync` | `!l.IsDeleted && !l.HoaDon.IsDeleted && l.HoaDon.Thang == thang && l.HoaDon.Nam == nam` (theo Q-1); lọc chi nhánh; `GroupBy(ChiNhanhId, TenChiNhanh)` → `Sum`, `Count`; sắp theo tên |
| `GetMeterReadingsAsync` | `!IsDeleted && phongTroIds.Contains(PhongTroId) && Thang == thang && Nam == nam`; mỗi phòng lấy bản có `DichVuDienNuocCuaPhongId` lớn nhất |
| `GetActiveContractsOfTenantAsync` | `!IsDeleted && NguoiThueId == id && TrangThaiHopDong == DangHoatDong` |
| `GetTenantUnpaidInvoicesAsync` | như `GetUnpaidInvoicesAsync`, thay lọc chi nhánh bằng `h.HopDong.NguoiThueId == nguoiThueId` |

Mọi truy vấn phải dịch được sang SQL, không đánh giá phía client (test chạy trên PostgreSQL thật).

### 4.2 `ExternalServices/AiAssistants/GeminiChatModel.cs` (mới, `IAiChatModel`)

- Constructor `(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiChatModel>? logger = null)`. Đọc `Gemini:ApiKey`, `Gemini:Model` (mặc định `"gemini-1.5-flash"`, giữ như cũ). Mẫu: `GeminiMeterOcrService.cs`.
- Thiếu key → `Fail(NotConfigured)`, **không gửi request**.
- `POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`, header `x-goog-api-key`. **Không** đưa key vào URL.
- Body (dựng bằng `JsonObject`/`JsonArray`):

```json
{
  "systemInstruction": { "parts": [ { "text": "<SystemInstruction>" } ] },
  "contents": [
    { "role": "user",  "parts": [ { "text": "..." } ] },
    <RawProviderContent của lượt model, gửi nguyên văn; thiếu "role" thì gán "model">,
    { "role": "user",  "parts": [
        { "functionResponse": { "name": "GetPhongTrongAsync", "id": "<CallId nếu có>", "response": <JsonNode.Parse(ResponseJson)> } },
        { "functionResponse": { "name": "...", "response": { ... } } } ] }
  ],
  "tools": [ { "functionDeclarations": [
      { "name": "...", "description": "...",
        "parameters": { "type": "OBJECT", "properties": { "thang": { "type": "INTEGER", "description": "..." } }, "required": ["..."] } } ] } ],
  "toolConfig": { "functionCallingConfig": { "mode": "NONE" } }
}
```

  - Lượt không có `RawProviderContent`: `role` = `"user"`/`"model"`; `AiTextPart` → `{text}`; `AiFunctionCallPart` → `{functionCall:{name, args, id?}}`; `AiFunctionResponsePart` → như trên.
  - Công cụ không có tham số thì bỏ `parameters`; `required` rỗng thì bỏ. `Tools` rỗng thì bỏ `tools`. `toolConfig` chỉ gửi khi `AllowToolCalls == false` và có tools.
- Phản hồi: lấy `candidates[0].content`. Không có → `LogWarning` (kèm `finishReason`/`promptFeedback.blockReason` nếu có) rồi `Fail(EmptyResponse)`. Duyệt `parts`: có `functionCall` → `AiFunctionCall(name, args?.ToJsonString() ?? "{}", id)`; có `text` và không có `"thought": true` → nối vào `Text` (`string.Concat`). Không có text lẫn lời gọi → `EmptyResponse`. `ModelTurn = new AiTurn(Model, parts đã đọc, content.ToJsonString())`.
- HTTP không thành công → `LogWarning("Lỗi Gemini API (HTTP {StatusCode}): {ErrorContent}", ..., body cắt 500 ký tự)` rồi `Fail(HttpError, code)`. `HttpRequestException`, `JsonException`, `TaskCanceledException` khi `!ct.IsCancellationRequested` → `LogWarning(ex)` rồi `Fail(Unavailable)`. Hủy từ phía người gọi thì ném tiếp. Không dùng `LogError`.

### 4.3 Sửa file có sẵn

- `Persistence/Features/ThongBaoStore.cs`: xóa `GetByIdAsync`; thêm `GetRoomResponsibleUserIdsAsync`. Lấy `chiNhanhId = PhongTros.Where(p => p.PhongTroId == phongTroId).Select(p => (int?)p.ChiNhanhId).FirstOrDefault()` (không lọc `IsDeleted` của phòng). Trả `NguoiDungs.Where(u => u.IsActive && !u.IsDeleted && (u.Role == Role.Admin || (chiNhanhId != null && u.Role == Role.NhanVien && u.NhanVienChiNhanhs.Any(nv => nv.ChiNhanhId == chiNhanhId && nv.IsActive && nv.NgayThuHoi == null)))).Select(u => u.NguoiDungId)`. Mẫu: `InvoicePaymentStore.GetReviewerUserIdsAsync`. Phòng không tồn tại → chỉ Admin.
- `DependencyInjection.cs`: thay dòng 72 bằng `services.AddHttpClient<QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel.IAiChatModel, ExternalServices.AiAssistants.GeminiChatModel>();`; thêm `services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Persistence.IAiAssistantStore, Persistence.Features.AiAssistantStore>();` ở nhóm Feature Stores.
- **Xóa** `ExternalServices/AiAssistants/AiAssistantService.cs`.

## 5. Web (`src/QuanLyChoThuePhongTroWeb.Web/`)

- **Sửa** `Areas/QuanLyNhaTro/Controllers/AiAssistantController.cs`: đổi `using` sang `QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Services`. `Chat([FromBody] ChatRequest? request, CancellationToken ct)` → `Json(await _aiService.ChatQuanLyAsync(CurrentActorId, request ?? new ChatRequest(), ct))`. Bỏ kiểm tra rỗng ở controller (service lo). Giữ `try/catch` + `LogError` + "Đã xảy ra lỗi hệ thống khi xử lý yêu cầu." cho lỗi bất ngờ.
- **Mới** `Areas/KhachThue/Controllers/AiAssistantController.cs`, namespace `QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers`, gắn `[Area("KhachThue")]`, `[Authorize(Roles = "KhachThue")]`, `[AutoValidateAntiforgeryToken]`. Một action `[HttpPost] Chat([FromBody] ChatRequest? request, CancellationToken ct)`. Dùng `TryGetTenant` copy từ `Areas/KhachThue/Controllers/ThanhToanHoaDonController.cs` (dòng 157–162); thiếu claim → `Unauthorized(new { success = false, message = "Tài khoản chưa liên kết hồ sơ người thuê." })`. Có claim → `Json(await _aiService.ChatKhachThueAsync(userId, nguoiThueId, request ?? new ChatRequest(), ct))`. Route: `/KhachThue/AiAssistant/Chat`.
- **Sửa** `Areas/KhachThue/Controllers/SuCoController.cs` (dòng 115–126): khi `success`, bọc phần tìm tên phòng và gửi thông báo trong `try { ... } catch (Exception ex) { _logger.LogWarning(ex, "Không gửi được thông báo sự cố mới cho phòng {PhongTroId}", model.PhongTroId); }`. Thay hai lời gọi `GuiChoQuyenAsync` bằng `await _thongBaoService.GuiChoNguoiPhuTrachPhongAsync(model.PhongTroId, "Sự cố mới", msg, "SuCo", "/QuanLyNhaTro/YeuCauSuCo");`. Vẫn trả `success = true`.
- **Sửa** `Controllers/ThongBaoController.cs` dòng 86: `DanhDauDaDocAsync(id, nguoiDungId, cancellationToken)`. Giữ dạng phản hồi `Ok(new { success, message })`.
- **Sửa** `Views/Shared/_Layout.cshtml` dòng 167: thêm `@{ ViewData["AiChatPortal"] = "QuanLyNhaTro"; }` ngay trước `<partial name="_AiChatWidget" />`. **Sửa** `Areas/KhachThue/Views/Shared/_LayoutKhachThue.cshtml` dòng 109: tương tự với `"KhachThue"`.
- **Sửa** `Views/Shared/_AiChatWidget.cshtml`:
  1. Đầu file (Razor): `userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value`, `portal = ViewData["AiChatPortal"] as string`. Chỉ render widget khi `userId` có giá trị **và** (`portal == "KhachThue"` và `User.IsInRole("KhachThue")`) hoặc (`portal == "QuanLyNhaTro"` và `User.IsInRole("Admin") || User.IsInRole("NhanVien")`). Ngược lại không render gì.
  2. `endpoint` = `/KhachThue/AiAssistant/Chat` hoặc `/QuanLyNhaTro/AiAssistant/Chat`; `storageKey = "ai_chat_history_" + userId`. Đưa vào JS bằng `@Json.Serialize(...)`.
  3. Nút gợi ý: container `#aiSuggestions` trong `#aiChatBody`, mỗi nút `<button type="button" class="btn btn-sm btn-outline-primary btn-ai-suggest" data-prompt="...">` (5 câu quản lý hoặc 3 câu khách thuê, đúng chữ ở §3.3). Chỉ hiện khi lịch sử rỗng; ẩn sau lần gửi đầu; hiện lại khi bấm xóa lịch sử. Bấm là gửi ngay (giữ handler `.btn-ai-suggest` có sẵn; gắn lại sau khi reset).
  4. `fetch(endpoint, ...)` thay URL cứng dòng 391. Gửi `history: getSessionHistory().slice(-10)`.
  5. `sessionStorage` dùng `storageKey` thay `"ai_chat_history"` (dòng 347, 439, 452). Khi tải trang: xóa khóa cũ `"ai_chat_history"` và mọi khóa bắt đầu bằng `"ai_chat_history_"` khác `storageKey`.
  6. Ô nhập thêm `maxlength="1000"`. Trong `sendMessage`: `text.length > 1000` thì hiện bong bóng model "Câu hỏi tối đa 1.000 ký tự." và không gửi.
- **Sửa** `Areas/QuanLyNhaTro/Views/NguoiDung/DangNhap.cshtml` (khối `<script>` dòng ~698): khi tải trang, duyệt `sessionStorage` và xóa mọi khóa bắt đầu bằng `"ai_chat_history"`. Đăng xuất luôn chuyển về trang này, nên lịch sử bị xóa.

## 6. Trường hợp biên bắt buộc

- **Fail closed**: scope `null` → từ chối; nhân viên chưa phân công → câu cố định, không gọi model; NhanVien có `AllowedBranchIds == null` → coi như rỗng; K-tool thiếu `NguoiThueId` → không khả dụng; công cụ sai vai trò hoặc tên lạ → không chạy truy vấn.
- **Dữ liệu ngoài phạm vi coi như không tồn tại**: phòng chi nhánh khác → "Không tìm thấy phòng.", không trả "không có quyền".
- **Soft-delete**: `PhongTro`, `HopDong`, `NguoiThue`, `HoaDon`, `LichSuThanhToan`, `DichVuDienNuocCuaPhong`, `NguoiDung` đều có `IsDeleted`; lọc như bảng §4.1. `ThongBao` không có `IsDeleted`. Phân công còn hiệu lực = `nv.IsActive && nv.NgayThuHoi == null`.
- **UTC**: DB lưu UTC; chỉ cộng 7 giờ khi dựng kết quả công cụ (Application) và trong prompt. Ngày người dùng nhập (Q3) là giờ VN.
- **Gemini**: số `functionResponse` bằng số `functionCall` của lượt trước, cùng thứ tự, kèm `id` nếu có; lượt model gửi lại nguyên văn.
- **Không lộ lỗi kỹ thuật**: không có `ex.Message`, nội dung lỗi Gemini hay API key trong `ServiceResult` hoặc JSON công cụ.
- **Thông báo**: lỗi SignalR hay lỗi gửi thông báo không làm hỏng việc tạo sự cố; chỉ `LogWarning` (Web test fail khi có log Error). Thông báo cũ của nhân viên bị thu hồi chi nhánh vẫn giữ.
- **Ngoài phạm vi, không sửa** (ghi nhận): `SuCoController.Create` không kiểm `PhongTroId` thuộc hợp đồng của khách; `parseMarkdown` trong widget gán `innerHTML` không escape. Hai lỗi này đều có từ trước.

## 7. Test

### 7.1 Application.UnitTests (`tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/`)

Fakes mới trong `Fakes/`:
- `FakeAiChatModel`: `List<AiModelRequest> Requests`; `Func<AiModelRequest, int, AiModelResult> Responder` (int = lần gọi, từ 1); mặc định trả `Ok("Xin chào")`.
- `FakeAiAssistantStore`: ghi lại mọi lời gọi kèm tham số (đặc biệt `allowedBranchIds`, `nguoiThueId`, `thang`, `nam`, khoảng UTC); dữ liệu trả về cấu hình được; có cờ ném lỗi.
- `FakeThongBaoStore`: danh sách `ThongBao` trong bộ nhớ, cài đủ `IThongBaoStore`.
- `CapturingLogger<T>`: ghi `(LogLevel, message)`. Mẫu: `CapturingLogger` lồng trong `InvoicePublicationTests.cs`.

Dùng lại: `FixedTimeProvider`, `FakeEmployeeAccessService` (`ScopeToReturn`), `RecordingUnitOfWork`, `RecordingNotifier` (`Fakes/PaymentTestHarness.cs`).

`AiAssistantServiceTests.cs`:
- Admin và nhân viên có phân công nhận đúng 9 công cụ Q; khách thuê nhận đúng 3 công cụ K (so tên trong `Requests[0].Tools`).
- Nhân viên chưa phân công → `Success`, message "Bạn chưa được phân công chi nhánh nào.", `Requests` rỗng. Scope `null` → thất bại, `Requests` rỗng.
- Tin nhắn rỗng hoặc 1001 ký tự → thất bại, không gọi model; đúng 1000 ký tự → có gọi.
- Lọc lịch sử: `History = null` không lỗi; bỏ role `"system"`, `"function"`, `"User"` và message rỗng; giữ 10 phần tử cuối đúng thứ tự; câu hỏi hiện tại là lượt User cuối.
- Một lời gọi rồi text → 2 lần gọi model; request 2 có `ModelTurn` (cùng `RawProviderContent`) và một lượt User với 1 `AiFunctionResponsePart` đúng `Name` và `CallId`.
- 3 lời gọi trong một lượt → chạy cả 3, một lượt trả lời gồm 3 phần tử đúng thứ tự.
- 6 lời gọi → store chạy 5 lần; phần tử thứ 6 có `maLoi` `VUOT_GIOI_HAN`.
- Model luôn gọi công cụ → đúng 4 lần gọi model; request 4 có `AllowToolCalls == false` và `SystemInstruction` chứa `AiPrompts.HetLuotCongCu`; không có text thì trả câu "Tôi chưa thể hoàn tất…".
- Khách thuê bị model gọi `GetPhongTrongAsync` → JSON trả `CONG_CU_KHONG_KHA_DUNG`, store không bị gọi. Quản lý bị gọi `GetMyHoaDonChuaThanhToanAsync` → tương tự.
- Ánh xạ lỗi: NotConfigured chứa "Lỗi cấu hình"; HttpError 400 đúng câu "Trợ lý AI tạm thời không phản hồi (HTTP 400). Vui lòng thử lại sau."; lỗi ở lần gọi thứ 2 cũng ánh xạ như vậy.
- Prompt quản lý chứa "Tôi chưa hỗ trợ câu hỏi này", 5 câu gợi ý quản lý và ngày VN (`FixedTimeProvider` 2026-09-30T18:00:00Z → "01/10/2026"). Prompt khách chứa 3 câu gợi ý khách thuê.

`AiToolExecutorTests.cs` (`FixedTimeProvider` 2026-09-30T18:00:00Z, tức 01/10/2026 01:00 giờ VN):
- Q1/Q8/Q9/K2 không tham số → store nhận `thang = 10, nam = 2026`.
- Q3 không tham số → `fromUtc = 2026-09-30T17:00Z`, `toExclusiveUtc = 2026-10-01T17:00Z`. Q3 `tuNgay = 2026-10-05`, `denNgay = 2026-10-05` → `[2026-10-04T17:00Z, 2026-10-05T17:00Z)`.
- Q2/K3 không tham số → `thang = null, nam = null`; chỉ `thang = 9` → `nam = 2026`. Q5 không tham số → N = 30.
- Tham số sai: `thang = 13`, `nam = "abc"`, `soNgay = -5`, `soNgay = 0`, `tuNgay = "2026/10/01"`, `tuNgay = "2026-02-30"`, `tuNgay > denNgay`, `mucGiaToiDa = 0`, `tuKhoa = "  "`, thiếu `soPhong`, args `"[1]"` → `THAM_SO_KHONG_HOP_LE`, thông báo tiếng Việt, store không bị gọi.
- Phạm vi: Admin → store nhận `null`; nhân viên → đúng tập chi nhánh; NhanVien có `AllowedBranchIds == null` → nhận tập rỗng.
- Q2 tính số: hai dòng (1.000.000 / đã thu 400.000) và (500.000 / 0) → `conLai` 600.000 và 500.000, `soHoaDon = 2`, `tongConNo = 1.100.000`.
- Q7/Q9: 0 phòng → `timThayPhong = false`; 2 phòng → `canChonChiNhanh = true`, không truy vấn hóa đơn/chỉ số; 1 phòng → gọi `GetUnpaidInvoicesAsync(null, null, phongTroId, ...)`.
- K-tool có args `{"nguoiThueId": 999}` → store nhận `NguoiThueId` của caller.
- Store ném lỗi → `LOI_TRUY_VAN`, JSON không chứa nội dung lỗi.
- Nhật ký: đúng một dòng Information mỗi lời gọi, chứa tên công cụ, không chứa giá trị `tuKhoa`.

`ThongBaoServiceTests.cs`:
- `GuiChoNguoiPhuTrachPhongAsync`: store trả `[1, 2, 2, 3]` → lưu 3 `ThongBao`, `RecordingNotifier.Sent` có 3 người; không phát nhóm. Notifier `Throw = true` → vẫn `Success`, vẫn lưu, có log Warning. Không có người nhận → thất bại, không lưu.
- `DanhDauDaDocAsync`: thông báo của người khác → thất bại, `ErrorKind == NotFound`, message "Không tìm thấy thông báo.", `IsRead` vẫn `false`; của mình → `IsRead = true`.

**Sửa** `BranchScopeArchitectureTests.cs`: thêm `typeof(IAiAssistantService)` (namespace mới) vào `ScopedServices`; thêm ngoại lệ `$"{nameof(IAiAssistantService)}.{nameof(IAiAssistantService.ChatKhachThueAsync)}"` với chú thích "Cổng khách thuê: giới hạn theo NguoiThueId".

### 7.2 Infrastructure.IntegrationTests (`tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/`)

- **Mới** `Persistence/AiAssistantStoreTests.cs`, `[Collection("PostgreSqlCollection")]`, mỗi test chạy trong transaction rồi bỏ. Mẫu seed: `Persistence/BranchScopeStoreTests.cs`; seed hóa đơn và thanh toán: `Persistence/InvoiceViewStoreTests.cs`. Dữ liệu: chi nhánh A, B, mỗi bên một phòng **cùng số**; phòng trống ở A và B với giá khác nhau; hóa đơn ở các trạng thái (DaGui chưa trả, DaGui trả một phần, DaGui đã trả, Nhap, ChoDuyet, DaChot, DaHuy + `IsDeleted`); một khoản thanh toán đã xóa mềm; một hóa đơn DaGui của phòng nay đã `Trong`.
  - Q2 chỉ có DaGui chưa trả và trả một phần, kể cả phòng đã trống; `DaThu` không tính khoản đã xóa; `[A]` không thấy B; rỗng → không có gì.
  - Q1, Q3 (biên trái gồm, biên phải loại), Q4, Q5, Q6, Q8 lọc đúng chi nhánh.
  - `FindRoomsByNumberAsync`: `null` → 2 phòng; `[A]` → 1; `tenChiNhanh` một phần tên B → 1; phòng `IsDeleted` không ra.
  - K1/K2/K3 chỉ dữ liệu của khách đó.
- **Mới** `Persistence/ThongBaoStoreTests.cs`: `GetRoomResponsibleUserIdsAsync(phòng A)` gồm Admin đang hoạt động và nhân viên A; không gồm Admin `IsActive = false`, nhân viên B, nhân viên A đã thu hồi (`NgayThuHoi` có giá trị). Mẫu seed người dùng và phân công: `Persistence/EmployeeBranchStoreTests.cs`.
- **Thay** `ExternalServices/AiAssistantServiceTests.cs` bằng `ExternalServices/GeminiChatModelTests.cs` (xóa file cũ). Không dùng `PostgreSqlFixture`; giữ `FakeHttpMessageHandler` có sẵn.
  - Key nằm ở header `x-goog-api-key`, query không có `key=`.
  - HTTP 400 có body bí mật → `HttpError`, `HttpStatusCode == 400`, kết quả không chứa chuỗi bí mật.
  - Key rỗng → `NotConfigured`, 0 request.
  - Body: `functionDeclarations` đúng tên, kiểu và `required`; lượt model có `RawProviderContent` được gửi nguyên văn; `functionResponse` có `name`, `id`, `response`; `toolConfig.functionCallingConfig.mode == "NONE"` khi `AllowToolCalls = false`, không có khi `true`.
  - Phản hồi 2 `functionCall` + 1 text có `thought: true` → 2 lời gọi đúng thứ tự, `Text` không lấy phần thought.
  - Handler ném `HttpRequestException` → `Unavailable`, không ném ra ngoài.

### 7.3 Web.IntegrationTests (`tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/`)

- **Sửa** `Fixtures/CustomWebApplicationFactory.cs`: thêm lớp lồng `FakeAiChatModel : IAiChatModel` (có `Requests`, `Responder`, `Reset()`) và thuộc tính `FakeAiChatModelInstance`. Trong `ConfigureServices`: `services.RemoveAll<IAiChatModel>(); services.AddSingleton<IAiChatModel>(FakeAiChatModelInstance);`. Mẫu: `FakeMeterOcrService`.
- **Mới** `AiAssistantEndpointTests.cs`, `[Collection(WebTestCollection.Name)]`. Dùng `BranchScopeKit.SeedAsync`, `LoginAsync`, `SendJsonAsync`. Tạo tài khoản khách thuê bằng `INguoiDungService.AddAsync(new CreateNguoiDungReq { Role = AppRole.KhachThue, NguoiThueId = w.TenantA, ... })`; trang lấy token cho khách: `/KhachThue/HoaDon`. Gọi `Reset()` đầu mỗi test.
  - Nhân viên A: seed thêm một phòng `Trong` ở A và một ở B. Fake lần 1 gọi `GetPhongTrongAsync`, lần 2 trả text → HTTP 200, `success = true`; `Requests[0].Tools` là bộ Q; `functionResponse` ở request 2 chứa số phòng trống A, không chứa phòng trống B.
  - Khách thuê A: seed hóa đơn DaGui chưa trả cho hợp đồng A và hợp đồng B. Fake gọi `GetMyHoaDonChuaThanhToanAsync` → `functionResponse` chỉ có mã hóa đơn A; `Requests[0].Tools` là bộ K.
  - Khách thuê POST `/QuanLyNhaTro/AiAssistant/Chat` → 302 về `/QuanLyNhaTro/DangNhap`, fake không nhận request. Nhân viên POST `/KhachThue/AiAssistant/Chat` → 302 về `/QuanLyNhaTro/DangNhap`, fake không nhận request.
  - Widget: trang quản lý chứa `/QuanLyNhaTro/AiAssistant/Chat` và `ai_chat_history_{id nhân viên}`; trang khách chứa `/KhachThue/AiAssistant/Chat`, không chứa endpoint quản lý; trang `/QuanLyNhaTro/DangNhap` chứa đoạn xóa `ai_chat_history`.
- **Mới** `ThongBaoChiNhanhTests.cs`:
  - Seed thêm nhân viên B (phân công chi nhánh B) và tài khoản khách A. Khách A POST `/KhachThue/SuCo/Create` (form `PhongTroId = RoomA`, `TieuDe`, `MoTa`, token từ `/KhachThue/SuCo`). Kiểm DB: Admin của kit và nhân viên A có `ThongBao` "Sự cố mới" mới; nhân viên B không có.
  - Tạo `ThongBao` cho Admin; nhân viên A POST `/api/ThongBao/DanhDauDaDoc/{id}` → `success = false`, message "Không tìm thấy thông báo.", DB `IsRead` vẫn `false`; Admin gọi lại → `success = true`, `IsRead = true`.

## 8. Lệnh kiểm chứng (PowerShell, từ thư mục repo)

```powershell
git status --short
dotnet build QuanLyChoThuePhongTroWeb.sln
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~AiAssistantServiceTests|FullyQualifiedName~AiToolExecutorTests|FullyQualifiedName~ThongBaoServiceTests|FullyQualifiedName~BranchScopeArchitectureTests|FullyQualifiedName~ArchitectureBoundaryTests"
# Không cần DB:
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests --filter "FullyQualifiedName~GeminiChatModelTests|FullyQualifiedName~InfrastructureBoundaryTests"
# Cần DB riêng có tên kết thúc _test hoặc _integration_test:
$env:QLCTPT_TEST_CONNECTION_STRING = "Host=...;Database=qlctpt_test;..."
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests --filter "FullyQualifiedName~AiAssistantStoreTests|FullyQualifiedName~ThongBaoStoreTests"
dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests --filter "FullyQualifiedName~AiAssistantEndpointTests|FullyQualifiedName~ThongBaoChiNhanhTests|FullyQualifiedName~ArchitectureBoundaryTests"
dotnet test QuanLyChoThuePhongTroWeb.sln
# Cần QLCTPT_DESIGNTIME_CONNECTION_STRING hoặc ConnectionStrings__DefaultConnection:
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj
git status --short src/QuanLyChoThuePhongTroWeb.Infrastructure/Migrations   # phải rỗng
git diff --check
```

Thiếu `QLCTPT_TEST_CONNECTION_STRING` thì ghi "chưa chạy" cho Infrastructure/Web integration, không ghi là PASS. Khi bàn giao, ghi rõ lệnh nào đã chạy và kết quả.

## 9. Tài liệu

- Khi xong, đánh dấu checkbox trong file này. Ở `docs/features/ai-thong-bao-chi-nhanh/README.md` (file người dùng chưa commit), chỉ sửa checkbox và dòng "Hiện mới có `spec.md`." thành "Đã có `spec.md` và `plan.md`."; giữ nguyên phần còn lại.

## 10. Bổ sung sau review (06/10/2026)

Người dùng yêu cầu sửa luôn M2 và L1–L7 trong đợt này. Các điểm chạm tiền và nghiệp vụ được hỏi lại:

| Mục | Câu hỏi | Người dùng chọn |
|---|---|---|
| L7 | Hóa đơn còn nợ của hợp đồng đã xóa mềm | Giữ nguyên ở cả AI và màn Công nợ, vì khoản nợ vẫn có thật |
| L1 | Giới hạn số dòng Q2/Q6 | 50 dòng; tổng số và tổng tiền tính trên toàn bộ |
| L2 | Sự cố cho phòng của hợp đồng cũ | Chỉ nhận phòng của hợp đồng đang hoạt động |
| L3 | K2 kỳ trước khi vào ở | Chỉ trả kỳ từ tháng bắt đầu hợp đồng |
| L4 | Q8 chi nhánh 0 đồng | Liệt kê cả chi nhánh 0 đồng |

ĐÃ XÁC NHẬN: người dùng chọn các phương án trên cho L1, L2, L3, L4, L7 (06/10/2026).

- [x] M2: escape tiêu đề, nội dung, thời gian trong `wwwroot/js/thongbao.js`; toast dùng `titleText`; bỏ `onclick` nội tuyến, chỉ nhận link nội bộ dạng `/...`.
- [x] L1: `AiToolExecutor` Q2/Q6 gửi tối đa 50 dòng, thêm `soHoaDonHienThi`/`soKetQua` và `ghiChu`; prompt dặn AI dùng tổng trong kết quả và nói lại ghi chú.
- [x] L2: `KhachThue/SuCoController` chỉ lấy hợp đồng `DangHoatDong` cho danh sách phòng và bước kiểm khi tạo.
- [x] L3: K2 bỏ phòng của hợp đồng bắt đầu sau kỳ được hỏi (theo giờ Việt Nam).
- [x] L4: `AiAssistantStore.GetRevenueByBranchAsync` thêm chi nhánh chưa xóa trong phạm vi với doanh thu 0.
- [x] L5: HttpClient Gemini timeout 30 giây.
- [x] L6: tên công cụ chỉ ghi log khi là định danh `[A-Za-z0-9_]` dài tối đa 64 ký tự, còn lại ghi "(không hợp lệ)".
- [x] L7: giữ nguyên, ghi nhận trong `review.md`.
- [x] Test: 7 test Application (`AiToolExecutorTests`), 1 test Infrastructure (`AiAssistantStoreTests`), 1 test Web (`ThongBaoChiNhanhTests`).
- [x] Thử tay M2 trên trình duyệt (người dùng kiểm đạt 06/10/2026, kèm L2).
