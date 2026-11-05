# Kế hoạch triển khai: Thanh lý, gia hạn hợp đồng và lịch sử thuê

> Bản kế hoạch planner viết cho `/ship` (`.bangiao/ke-hoach.md`, 05/11/2026), lưu lại nguyên văn để đối chiếu. Đã thực hiện xong Task 1–13; phần lệch kế hoạch ghi ở [review.md](review.md). Tiêu đề gốc ở dưới.

# Kế hoạch: Thanh lý, gia hạn hợp đồng và lịch sử thuê

- Nguồn: `docs/features/thanh-ly-gia-han-hop-dong/spec.md` (v2, người dùng duyệt 04/11/2026). Gọi tắt là "spec §x" ở dưới.
- Nhánh: `feature/thanh-ly-gia-han-hop-dong`. Phạm vi gồm cổng quản lý (`Areas/QuanLyNhaTro`) và tầng Application/Infrastructure. Cổng khách thuê chỉ đổi phần hiển thị (§7). **Không đụng cổng khách vãng lai.**
- Không sửa `Program.cs`, `*.csproj`, `appsettings*.json` (kể cả comment nhắc `ContractAutoCloseJob` trong `appsettings.example.json`), `CustomWebApplicationFactory`, `StartupTests.cs`.
- Không sửa 3 file docs chưa commit (`docs/features/README.md`, `docs/features/thanh-ly-gia-han-hop-dong/README.md`, `spec.md`). Không sửa `AGENTS.md`/`CLAUDE.md`.
- Trước khi sửa code: chạy `git status --short` và giữ nguyên thay đổi đang có.

---

## CẦN NGƯỜI DÙNG XÁC NHẬN

Planner không ghi "ĐÃ XÁC NHẬN". Những điểm spec v2 §10 đã chốt được ghi rõ bên dưới để orchestrator đối chiếu. Những điểm chưa có trong spec thì phải hỏi người dùng.

### A. Schema

| # | Đổi gì | File | Trước → Sau | Spec đã chốt? |
|---|---|---|---|---|
| A1 | Thêm giá trị enum `CanTruTienCoc = 2`, `[Description("Cấn trừ tiền cọc")]` | `src/QuanLyChoThuePhongTroWeb.Domain/Enums/PhuongThucThanhToan.cs` | Có `TienMat=0, ChuyenKhoan=1` → thêm `CanTruTienCoc=2`. Cột `lich_su_thanh_toan."PhuongThucThanhToan"` vẫn là `integer`, không có CHECK. **Không** thêm migration. `has-pending-model-changes` phải báo không có thay đổi | Có ghi ở spec §4.3 và dòng "Schema" đầu spec. §10 không có dòng riêng cho điểm này. Enum `GiaoDichGiuCho.PhuongThuc` dùng chung enum này nhưng không bị ảnh hưởng |
| A2 | Không đổi bảng/cột/index/Configurations/Migrations/`ApplicationDbContext` nào khác | - | - | Spec ghi "Schema: không đổi" |

### B. Tiền

| # | Đổi gì | File | Trước → Sau | Spec đã chốt? |
|---|---|---|---|---|
| B1 | Thanh lý ghi khoản cấn trừ cọc thành bản ghi `LichSuThanhToan` (`PhuongThuc=CanTruTienCoc`), cập nhật `TrangThaiHoaDon` | `ThanhLyHopDongService` (mới), `HopDongLifecycleStore` (mới) | Chưa có → mỗi hóa đơn nợ nhận một dòng sổ, phân bổ kỳ cũ trước, tổng = min(cọc, nợ) | §10 #1 ĐÃ CHỐT |
| B2 | Còn nợ sau cấn trừ vẫn thanh lý được nếu tích xác nhận. Server từ chối khi số nợ đã đổi | như B1 | - | §10 #4 ĐÃ CHỐT |
| B3 | Chặn thanh lý khi tháng trả phòng chưa có hóa đơn `DaGui` hoặc còn hóa đơn `Nhap/ChoDuyet/DaChot` | như B1 | - | §10 #6 ĐÃ CHỐT |
| B4 | Server từ chối hủy ghi nhận dòng cấn trừ; ẩn nút hủy | `InvoiceLedgerService.VoidPaymentAsync`, `Views/LichSuThanhToan/Index.cshtml` (quản lý) | Admin hủy được mọi dòng → dòng `CanTruTienCoc` bị từ chối | Có ở spec §4.3, không có dòng §10 riêng |
| B5 | Nhãn phương thức thứ ba "Cấn trừ tiền cọc" và dòng "Trừ tiền cọc: −X đ" trên hóa đơn (quản lý và khách thuê) | `InvoiceDetailProjection.cs`, `LichSuThanhToanStore.cs`, `PaymentNotificationFormatter.cs`, `DashboardService.cs`, 4 view | Trước: mọi giá trị khác 0 hiện "Chuyển khoản" → Sau: giá trị 2 hiện "Cấn trừ tiền cọc" | Có ở spec §4.3, §7 |
| B6 | Gia hạn: giá thuê mới (> 0) và tiền cọc chuyển sang hợp đồng mới (sửa được, chênh lệch thu/trả ngoài hệ thống) | `GiaHanHopDongService` (mới) | - | Có ở spec §5.1, không có dòng §10 riêng |
| B7 | Việc cần làm KH5 và bộ lọc hóa đơn "còn nợ của hợp đồng đã kết thúc" hiện tổng còn nợ | `ViecCanLamService`, `ViecCanLamStore`, `InvoiceViewStore` | - | Có ở spec §2.3 |
| B8 | Biên bản thanh lý PDF in tiền cọc, cấn trừ, cọc hoàn trực tiếp và nợ còn lại | `BienBanThanhLyExporter` (mới) | - | Có ở spec §4.2 |
| B9 | Màn Lịch sử thuê và cảnh báo nợ cũ hiện số nợ | `HopDongTraCuuService` (mới) | - | Có ở spec §4.2a, §6.1 |
| B10 | Các câu hỏi Q2–Q6, Q13 bên dưới đều thuộc nhóm tiền | - | - | Chưa có trong spec |

### C. Khác

| # | Đổi gì | File | Spec đã chốt? |
|---|---|---|---|
| C1 | Gỡ `ContractAutoCloseJob`, `IContractAutoCloseUseCase`, `ContractAutoCloseUseCase` cùng đăng ký DI | `Application/DependencyInjection.cs` (dòng 99), `Infrastructure/DependencyInjection.cs` (dòng 156) | Spec §2.1 đã ghi; §10 #2 chốt kiểu thông báo thay thế |
| C2 | Form sửa không còn chọn `DaKetThuc` | `QuanLyHopDong.cshtml`, `HopDongService.UpdateCoreAsync` | §10 #5 ĐÃ CHỐT |
| C3 | Admin và nhân viên chi nhánh đều thanh lý, gia hạn được (`Contract.Settle`, `Contract.Renew` không admin-only) | `EmployeeActionCodes.cs` | §10 #3 ĐÃ CHỐT |
| C4 | Spec §2.1 yêu cầu cập nhật mục Hiện trạng trong `AGENTS.md`. Planner/coder **không** sửa file này; người dùng hoặc orchestrator tự làm | `AGENTS.md` | - |

ĐÃ XÁC NHẬN: A1 (thêm `PhuongThucThanhToan.CanTruTienCoc = 2`, không migration, không đưa vào `AppPhuongThucThanhToan`/form thu tay), A2, B1–B10, C1–C4 đúng như bảng trên; các câu Q1–Q14 theo mục "TRẢ LỜI CỦA NGƯỜI DÙNG" bên dưới, trong đó Q2 khác mặc định của planner (05/11/2026 08:13)

---

## CÂU HỎI CÒN BỎ NGỎ

Mỗi câu có kèm "mặc định đề xuất" để coder có cái mà code nếu người dùng chọn đúng như vậy. Nếu người dùng trả lời khác thì phải sửa kế hoạch trước khi code. **Không coi mặc định là đã duyệt.**

- **Q1 (KH5 và bộ lọc hóa đơn):** Hợp đồng cũ đã gia hạn cũng là `DaKetThuc`, nhưng khách chưa trả phòng.
  - Mặc định: loại hợp đồng `DaKetThuc` mà cùng `PhongTroId` và cùng `NguoiThueId` đang có hợp đồng `DangHoatDong` chưa xóa.
- **Q2 (tiền):** Tổng nợ và cấn trừ chỉ tính hóa đơn của hợp đồng đang thanh lý, hay tính cả các hợp đồng trước đó trong chuỗi gia hạn? Khi gia hạn, cọc đã chuyển sang hợp đồng mới.
  - Mặc định: chỉ hợp đồng đang thanh lý, đúng chữ của spec §4.1.
- **Q3 (tiền):** Hóa đơn nợ đang có lượt thanh toán `DangDoiChieu` (khách đã nộp minh chứng) thì xử lý thế nào?
  - Mặc định: chặn thanh lý, báo "đối chiếu trước".
  - Lượt `ChoThanhToan` của hóa đơn nhận cấn trừ thì hủy với lý do "Đã cấn trừ tiền cọc khi thanh lý", giống `CollectManuallyAsync`.
- **Q4 (tiền):** Có chặn thanh lý khi hóa đơn nợ có `TongTien` lẻ (không nguyên) hoặc tiền cọc lẻ không?
  - Mặc định: chặn, với thông báo "Hóa đơn {mã} có số tiền lẻ…" hoặc "Tiền cọc có số lẻ…". Không làm tròn.
- **Q5 (tiền, hiển thị):** Khi in lại biên bản, "nợ còn lại" lấy số nào?
  - Mặc định: nợ hiện tại tại lúc in, kèm chú thích "tính đến {ngày in}". Không dựng lại số nợ của lúc thanh lý.
- **Q6 (tiền, thống kê):** `TongDoanhThu` hiện cộng mọi phương thức, nên sẽ gồm cả cấn trừ. Chỗ tính: `LichSuThanhToanStore` dòng 136 và doanh thu trong `DashboardStore`.
  - Mặc định: giữ nguyên. Spec §4.3 để việc tách riêng cho module báo cáo sau.
- **Q7:** Spec §8 để "mở lại hợp đồng đã thanh lý" và "hủy một lần gia hạn" ngoài phạm vi. Vậy form sửa có chặn không?
  - Mặc định: `UpdateAsync` từ chối mọi sửa đổi trên hợp đồng `DaKetThuc`, và nút "Chỉnh sửa" ẩn với dòng `DaKetThuc`.
  - Đổi sang `DaHuy` cũng bị từ chối với hợp đồng gia hạn (mã có `-GH`).
- **Q8:** Phạm vi của cảnh báo nợ cũ và Lịch sử thuê.
  - Mặc định: nhân viên chỉ thấy nợ ở chi nhánh được phân công.
  - Nợ gắn với người đứng tên (`HopDong.NguoiThueId`); người ở ghép không mang nợ.
- **Q9:** Định nghĩa số liệu tóm tắt ở Lịch sử thuê. Mặc định:
  - Hợp đồng `DaHuy` không tính.
  - "Số lần ở ghép" = số mã gốc khác nhau ở vai trò Ở ghép.
  - "Tổng số tháng ở" = tổng các khoảng [bắt đầu, min(kết thúc, hiện tại)] của các dòng, tính theo ngày giờ VN, chia 30, làm tròn xuống. Các khoảng trùng nhau chỉ đếm một lần.
- **Q10:** Mặc định của form gia hạn.
  - Số tháng N của hợp đồng cũ = (tháng của (ngày kết thúc VN + 1 ngày)) − (tháng bắt đầu VN), tối thiểu 1. Cũ không có ngày kết thúc thì N = 12.
  - Ngày kết thúc mới mặc định = ngày bắt đầu mới + N tháng − 1 ngày, lưu 23:59:59 giờ VN.
  - Cũ không có ngày kết thúc thì tháng bắt đầu mặc định = tháng hiện tại (VN) + 1.
- **Q11 (giao diện):**
  - Hiện chưa có trang chi tiết hợp đồng hay chi tiết người thuê. Mặc định tạo hai trang mới: `/QuanLyNhaTro/HopDong/ChiTiet/{id}` và `/QuanLyNhaTro/NguoiThue/ChiTiet/{id}`. Trang người thuê có một tab "Lịch sử thuê". Thêm link "Chi tiết" ở dropdown thao tác của hai danh sách.
  - Nhãn "Đã gia hạn → {mã mới}" hiện mỗi khi hợp đồng `DaKetThuc` có hợp đồng kế tiếp, không chỉ khi gia hạn sớm.
- **Q12:** Các trường hợp ngày trả phòng.
  - Hợp đồng chưa có `ThoiDiemKetThuc`: mặc định chặn thanh lý ("Cập nhật ngày trả phòng trước").
  - `ThoiDiemKetThuc` ở tương lai: không chặn riêng; quy tắc "tháng trả phòng phải có hóa đơn `DaGui`" tự chặn.
  - Biên bản in được cho mọi hợp đồng `DaKetThuc` không có hợp đồng kế tiếp, kể cả hợp đồng kết thúc theo cách cũ.
- **Q13 (tiền, hiển thị):** Dòng cấn trừ có hiện nút "Biên lai" và "Gửi lại biên lai" ở modal hóa đơn phía quản lý không? Biên lai có tiêu đề "BIÊN LAI XÁC NHẬN THU TIỀN".
  - Mặc định: ẩn hai nút này với dòng cấn trừ. Không đổi trang biên lai và panel của khách.
- **Q14 (mã gia hạn):** Spec §5.3 ghi "n = số hợp đồng chưa xóa có cùng mã gốc + 1", nhưng ví dụ cho lần gia hạn đầu là `-GH1`. Nếu đếm cả mã gốc thì lần đầu ra `-GH2`.
  - Mặc định theo ví dụ: n = số hợp đồng **gia hạn** (mã có `-GHk`) chưa xóa cùng mã gốc + 1. Nếu mã đã tồn tại (kể cả bản đã xóa) thì tăng n, thử tối đa 5 lần.

### TRẢ LỜI CỦA NGƯỜI DÙNG (05/11/2026 08:13)

- **Q1, Q3–Q14:** dùng đúng "mặc định" ghi ở từng câu trên.
- **Q2 (khác mặc định):** tổng nợ và cấn trừ tính hóa đơn của **cả chuỗi gia hạn** (mọi hợp đồng chưa xóa, không `DaHuy`, cùng mã gốc với hợp đồng đang thanh lý theo helper mã chuỗi ở Task 3.2), không chỉ hợp đồng đang thanh lý. Lý do: khi gia hạn, cọc đã chuyển sang hợp đồng mới. Coder phải chỉnh các task liên quan:
  - Task 3.3/3.7/5: store khóa và đọc hóa đơn theo **danh sách `HopDongId` của chuỗi** (khóa `hop_dong` của hợp đồng đang thanh lý trước, rồi `hoa_don` theo id tăng dần); `GetPaidTotalsAsync`, `GetActivePaymentRequestsAsync` nhận cùng danh sách đó.
  - Task 7: phân bổ cấn trừ kỳ cũ trước trên toàn chuỗi (sắp theo `Nam`, `Thang`, rồi `HoaDonId`). Mã giao dịch `CANTRU-HD{HopDongId đang thanh lý}-{HoaDonId}`; `GhiChu` ghi mã hợp đồng đang thanh lý. Các quy tắc chặn của Q3, Q4 và "còn hóa đơn `Nhap/ChoDuyet/DaChot`" áp cho mọi hóa đơn trong chuỗi; quy tắc "tháng trả phòng phải có hóa đơn `DaGui`" chỉ xét hợp đồng đang thanh lý.
  - Màn thanh lý, preview và biên bản PDF liệt kê hóa đơn của cả chuỗi, có cột mã hợp đồng.
  - Thêm test unit: nợ ở hợp đồng cũ của chuỗi được cấn trừ trước nợ của hợp đồng hiện tại; hóa đơn của hợp đồng khác chuỗi (cùng người thuê, mã gốc khác) không bị đụng.

---

## Ảnh hưởng schema PostgreSQL

**KHÔNG ĐỔI SCHEMA.** Chỉ thêm giá trị enum `int` (A1). Kiểm bằng lệnh sau (cần `QLCTPT_DESIGNTIME_CONNECTION_STRING`; nếu thiếu thì ghi CHƯA CHẠY):

```powershell
$p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
```

Không tạo migration. Không sửa file nào trong `Persistence/Migrations/`, `Persistence/Configurations/` hay `ApplicationDbContext`.

---

## Quy ước chung (áp dụng mọi task)

- **Thời gian.** Lưu UTC với `DateTimeKind.Utc`. Giờ VN = UTC+7.
  - Đầu ngày 1 tháng (thang, nam) giờ VN tính bằng `DateTime.SpecifyKind(new DateTime(nam, thang, 1), DateTimeKind.Utc).AddHours(-7)`. Kết quả giống `MeterPeriodPolicy.MonthRangeUtc(...).StartUtc` trong `Application/Features/DienNuocs/Services/MeterPeriodPolicy.cs`.
  - 23:59:59 VN của ngày D = `SpecifyKind(D.ToDateTime(new TimeOnly(23,59,59)), Utc).AddHours(-7)`.
  - **Không dùng `ToUniversalTime()`** trong code mới.
  - "Đầu ngày hôm nay VN tính bằng UTC": copy `homNayVnBatDauUtc` ở `ViecCanLamService.cs` dòng 58–60.
  - Định dạng ngày hiển thị: `.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)` (như `InvoiceDetailProjection.cs` dòng 94).
- **Phân quyền** theo mẫu `InvoiceLedgerService.CancelInvoiceAsync`:
  - `actorId` là tham số đầu. `actorId <= 0` → `Fail`.
  - Gọi `GetScopeAsync`; scope null → `Forbidden`.
  - Lấy `ChiNhanhId` của phòng. `!scope.CanAccessBranch(id)` → `Forbidden("Bạn không có quyền thao tác hợp đồng thuộc chi nhánh này.")`.
  - Thao tác ghi còn phải qua `CanPerformAsync(actorId, chiNhanhId, EmployeeActionCodes.ContractSettle|ContractRenew)`; false → `Forbidden`.
  - Hợp đồng không tồn tại hoặc đã xóa → `NotFound("Không tìm thấy hợp đồng.")`.
- **Kết quả** trả `ServiceResult` / `ServiceResult<T>` (`Application/Common/Models`). DTO dịch vụ không chứa entity Domain và không dùng kiểu `object` (`ArchitectureBoundaryTests`). Trạng thái trả về dạng `int` hoặc chuỗi nhãn.
- **Transaction:** `await using var tx = await _unitOfWork.BeginTransactionAsync(ct)`, try/commit, catch → rollback, như `HopDongService.CreateCoreAsync` dòng 167–268.
  - Thông báo gửi **sau commit**, bọc try/catch. Lỗi chỉ `LogWarning`, không đổi kết quả.
  - Không dùng `LogError` cho các nhánh nghiệp vụ, vì Web test FAIL khi có log Error ngoài dự kiến.
- **Khóa dòng** bằng `FromSqlInterpolated(... FOR UPDATE)`, thứ tự `hop_dong` rồi `hoa_don` theo id tăng dần (mẫu `InvoiceIssuanceStore.LockContractsAsync` dòng 29–31). Không gọi `ChangeTracker.Clear()`.
  - **Không** dùng `InvoicePaymentTransaction.RunAsync`, vì nó tự mở transaction cho từng hóa đơn và không lồng được.
- **Web.**
  - Controller mới kế thừa `AdminBaseController`, **không** gắn `[ApiController]`. Route viết dạng attribute tuyệt đối, theo mẫu `Areas/QuanLyNhaTro/Controllers/BienLaiThanhToanController.cs`.
  - Mọi POST có `[ValidateAntiForgeryToken]`. AJAX gửi header `RequestVerificationToken`, lấy từ `$('input[name="__RequestVerificationToken"]').val()`. View gọi `@Html.AntiForgeryToken()`.
  - Lỗi JSON trả bằng `this.ToErrorResult(result)`.
  - Lỗi của trang HTML trả view `Loi` với `StatusCode = result.ToHttpStatusCode()`, theo mẫu `BienLaiThanhToanController.Index`.
  - View dùng thẳng DTO Application (như `Views/HopDong/Print.cshtml`). Web không `using` namespace Domain.
- **Nhãn trạng thái hợp đồng:** 1 "Đang hoạt động", 2 "Đã kết thúc", 3 "Đã hủy" (khớp `QuanLyHopDong.cshtml` dòng 566–568).
- **Xóa mềm:**
  - Có `IsDeleted`: `HopDong`, `HoaDon`, `LichSuThanhToan`, `ChiTietThanhVienHopDong`, `NguoiThue`, `PhongTro`, `YeuCauThanhToanHoaDon`. Mọi truy vấn mới phải lọc `!IsDeleted`.
  - Hóa đơn `DaHuy` luôn có `IsDeleted = true`.
  - `DangKyDichVu` **không** có `IsDeleted`; "đang hiệu lực" = `NgayKetThuc == null`.

---

## Task 1. Domain: enum cấn trừ

**File sửa:** `src/QuanLyChoThuePhongTroWeb.Domain/Enums/PhuongThucThanhToan.cs`

- Thêm `[Description("Cấn trừ tiền cọc")] CanTruTienCoc = 2`.
- **Không** thêm vào `AppPhuongThucThanhToan` (`Application/Common/Enums/AppEnums.cs`). Nhờ vậy `CollectManuallyAsync` vẫn từ chối giá trị 2 qua `Enum.IsDefined` (dòng 52).

**Test:** không cần test Domain mới (Task 4 có test cho thu tay).

**Xong khi:** build PASS; `has-pending-model-changes` báo không có thay đổi, hoặc ghi CHƯA CHẠY nếu thiếu biến môi trường.

---

## Task 2. Gỡ tự động kết thúc, thêm thông báo hết hạn trong app

### File xóa

- `src/QuanLyChoThuePhongTroWeb.Application/Features/HopDongs/UseCases/ContractAutoCloseUseCase.cs`
- `src/QuanLyChoThuePhongTroWeb.Application/Features/HopDongs/UseCases/IContractAutoCloseUseCase.cs`
- `src/QuanLyChoThuePhongTroWeb.Infrastructure/BackgroundJobs/ContractAutoCloseJob.cs`
- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ContractAutoCloseUseCaseTests.cs`

### File sửa

1. `Application/DependencyInjection.cs`: xóa dòng 99 (`IContractAutoCloseUseCase`).
2. `Infrastructure/DependencyInjection.cs`: xóa dòng 156 (`AddHostedService<BackgroundJobs.ContractAutoCloseJob>()`).
3. `Application/Features/HopDongs/Persistence/IHopDongStore.cs`: giữ nguyên chữ ký `GetExpiredActiveContractsAsync`. Sửa comment thành: "hợp đồng `DangHoatDong` chưa xóa, `ThoiDiemKetThuc != null && < beforeUtc`".
4. `Infrastructure/Persistence/Features/HopDongStore.cs` dòng 365–375, `GetExpiredActiveContractsAsync`:
   - Thay `Include(ChiTietThanhVienHopDongs)` bằng `.Include(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh).Include(hd => hd.NguoiThue)`.
   - Thêm `.AsNoTracking()`.
   - Thêm điều kiện `!hd.PhongTro.IsDeleted`.
5. `Application/Features/HopDongs/UseCases/ContractExpiryAlertUseCase.cs`:
   - Constructor mới: `(IHopDongStore store, IEmailService emailService, ContractAlertSettings settings, IContractExpiryAlertStateStore stateStore, IThongBaoService thongBao, TimeProvider timeProvider, ILogger<ContractExpiryAlertUseCase> logger)`.
   - Vòng email giữ nguyên từng dòng, kể cả `DateTime.UtcNow`.
   - Sau vòng email (vẫn nằm trong `if (_settings.Enabled)`), thêm vòng thông báo app:
     - `nowUtc = _timeProvider.GetUtcNow().UtcDateTime`; `homNayVnBatDauUtc` tính như quy ước chung; `ngayMaiVnBatDauUtc = homNayVnBatDauUtc.AddDays(1)`.
     - `var expired = await _store.GetExpiredActiveContractsAsync(ngayMaiVnBatDauUtc, ct)`. Điều kiện này nghĩa là ngày VN của `ThoiDiemKetThuc` ≤ hôm nay.
     - Với từng hợp đồng: khóa `app_het_han_{HopDongId}`. Đã có trong `sentAlerts` thì bỏ qua.
     - Gọi `_thongBao.GuiChoNguoiPhuTrachPhongAsync(hd.PhongTroId, tieuDe, noiDung, "HopDong", $"/QuanLyNhaTro/HopDong/ChiTiet/{hd.HopDongId}", ct)` với:
       - `tieuDe = $"Hợp đồng {MaHopDong} đã hết hạn"`
       - `noiDung = $"Phòng {SoPhong} - {TenChiNhanh}, người đại diện {HoVaTen}, hết hạn ngày {dd/MM/yyyy VN}. Hãy gia hạn hoặc thanh lý hợp đồng."`
     - Chỉ khi `result.Success` mới gọi `MarkAsSentAsync(key)` và tăng biến đếm.
     - Mỗi hợp đồng bọc try/catch riêng: rethrow `OperationCanceledException`, lỗi khác thì `LogError` giống vòng email. Vòng email và vòng app tách nhau, lỗi bên này không chặn bên kia.
   - `ExecuteAsync` trả về tổng số email đã gửi + số thông báo app đã gửi.

### Test cần sửa (không thêm test mới ngoài danh sách)

- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/BackgroundJobUseCaseTests.cs`:
  - Sửa mọi chỗ `new ContractExpiryAlertUseCase(...)` theo constructor mới, dùng `FakeThongBaoService` và `FixedTimeProvider` trong `Fakes/`.
  - `FakeHopDongStore.GetExpiredActiveContractsAsync` trả danh sách cấu hình được (mặc định rỗng), ghi lại `beforeUtc` nhận vào.
  - Thêm test:
    1. `ExpiryAlert_SendsAppNoticeOnce_WhenEndDateIsToday`: chạy 2 lần với cùng state store → `FakeThongBaoService.Sent` có 1 dòng Kind "Phong", link đúng.
    2. `ExpiryAlert_SendsAppNotice_ForContractExpiredBeforeRelease`: hết hạn 10 ngày trước, chưa có khóa → được gửi.
    3. `ExpiryAlert_PassesStartOfTomorrowVn`: `FixedTimeProvider` = 2026-11-04 18:00 UTC (01:00 ngày 05/11 VN) → `beforeUtc` = 2026-11-05 17:00 UTC.
    4. `ExpiryAlert_AppFailure_DoesNotMarkSent`: `FailOnSend` → không có khóa.
    5. `ExpiryAlert_EmailFailure_DoesNotBlockApp` và chiều ngược lại `ThrowOnSend`: kênh còn lại vẫn chạy.
    6. Email 30/15 ngày giữ nguyên: test cũ vẫn PASS.
    7. `Enabled=false` → không gọi store.
- `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/BackgroundJobsApplicationCompositionTests.cs`:
  - Đổi tên `autoCloseQueried` thành `expiredQueried`; proxy vẫn map `GetExpiredActiveContractsAsync` sang signal đó (giờ do `ContractExpiryAlertUseCase` gọi).
  - Xóa assert `IsType<ContractAutoCloseUseCase>` và `AssertStartupLogFor<ContractAutoCloseJob>`.
  - Nếu `IThongBaoService` không dựng được trong host này thì đăng ký `FakeThongBaoService` tương đương ngay trong file test. Không sửa `AddApplication`.
- `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/BackgroundJobsRuntimeSmokeTests.cs`:
  - Xóa `BlockingContractAutoCloseUseCase`, `ObservableContractAutoCloseUseCase` cùng các đăng ký và assert liên quan.
  - `Assert.Equal(3, ...)` → `2`; `typeof(ContractAutoCloseJob).Namespace` → `typeof(ContractExpiryAlertJob).Namespace`.
  - Bỏ `completed[0]` của job tự đóng và đánh lại chỉ số mảng nếu cần.
- `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/PaymentNotificationWorkerTests.cs` dòng 160–161: `3` → `2`; dòng 161 đổi sang `s is ContractExpiryAlertJob`.
- **Không sửa** `tests/.../Web.IntegrationTests/StartupTests.cs` và `Fixtures/CustomWebApplicationFactory.cs`. Đó là setup master; chuỗi tên job cũ trong đó vô hại.

### Tài liệu

`docs/architecture/README.md` dòng 118–124:
- Đổi "ba hosted service" thành "hai".
- Xóa dòng `ContractAutoCloseJob`.
- Dòng `ContractExpiryAlertJob` thêm câu "và thông báo trong app một lần khi hợp đồng hết hạn (khóa `app_het_han_{id}` trong sent_contract_alerts.json)".

**Xong khi:** `grep -r ContractAutoClose src tests` chỉ còn chuỗi trong `StartupTests.cs`, `CustomWebApplicationFactory.cs` và `appsettings.example.json`; build PASS; test Application PASS.

---

## Task 3. Application: helper thuần, quyền, DTO, store interface

### 3.1 Quyền

`src/QuanLyChoThuePhongTroWeb.Application/Common/Security/EmployeeActionCodes.cs`:
- Thêm `public const string ContractSettle = "Contract.Settle";` và `public const string ContractRenew = "Contract.Renew";`.
- Thêm cả hai vào `ValidActionsSet`, **không** thêm vào `AdminOnlySet`.

### 3.2 Helper mã chuỗi (mới)

`src/QuanLyChoThuePhongTroWeb.Application/Features/HopDongs/Services/HopDongCodeChain.cs`, `public static class HopDongCodeChain`:

```csharp
public const string RenewalPattern = "-GH[1-9][0-9]*$";            // dùng chung cho Npgsql Regex.IsMatch
public static bool IsRenewal(string maHopDong);                     // regex ^(?<goc>.+)-GH(?<n>[1-9][0-9]*)$
public static string RootOf(string maHopDong);                      // không phải gia hạn → trả nguyên mã
public static int RenewalIndex(string maHopDong);                   // mã gốc → 0
public static string BuildRenewal(string maGoc, int n);             // $"{maGoc}-GH{n}", n >= 1
public static string LoaiText(string maHopDong);                    // "Mới" | $"Gia hạn lần {n}"
public static bool BelongsTo(string maHopDong, string maGoc);       // RootOf(ma) == maGoc
```

### 3.3 Quy tắc và phép tính thanh lý (mới, thuần)

`src/QuanLyChoThuePhongTroWeb.Application/Features/HopDongs/Services/ThanhLyCalculator.cs`:

```csharp
public sealed record ThanhLyHoaDonInput(int HoaDonId, string MaHoaDon, int Thang, int Nam, decimal TongTien, decimal DaThu,
    int TrangThaiPhatHanh /* (int)TrangThaiPhatHanhHoaDon */, bool CoLuotDangDoiChieu, bool TongTienLaSoNguyen);
public sealed record ThanhLyPhanBo(int HoaDonId, string MaHoaDon, int Thang, int Nam, decimal TongTien, decimal DaThu, decimal ConLai, decimal CanTru);
public sealed record ThanhLyKetQuaTinh(IReadOnlyList<ThanhLyPhanBo> HoaDonNo, decimal TongNo, decimal CanTru, decimal CocHoanTrucTiep, decimal ConNoSauCanTru);

public static class ThanhLyCalculator
{
    // Chỉ hóa đơn DaGui có ConLai = TongTien - DaThu > 0. Sắp Nam, Thang, HoaDonId tăng dần.
    // CanTru = min(tienCoc, TongNo); phân bổ lần lượt, mỗi hóa đơn nhận min(phần còn lại của cấn trừ, ConLai).
    public static ThanhLyKetQuaTinh Tinh(decimal tienCoc, IEnumerable<ThanhLyHoaDonInput> hoaDons);

    // Trả danh sách lý do chặn (rỗng = cho thanh lý). Thứ tự:
    // 1. trangThaiHopDong != 1 → "Hợp đồng không còn hoạt động."
    // 2. thoiDiemKetThucUtc == null → "Hợp đồng chưa có ngày trả phòng. Cập nhật Ngày kết thúc trước khi thanh lý." (Q12)
    // 3. Không có hóa đơn DaGui của tháng VN của ThoiDiemKetThuc → $"Tháng trả phòng T{m}/{y} chưa có hóa đơn đã công bố. Lập hóa đơn, gửi duyệt, chốt và công bố trước."
    // 4. Mỗi hóa đơn Nhap/ChoDuyet/DaChot → $"Hóa đơn {Ma} (T{m}/{y}) đang ở trạng thái {Nháp|Chờ duyệt|Đã chốt}, cần công bố hoặc hủy trước."
    // 5. Mỗi hóa đơn nợ có CoLuotDangDoiChieu → $"Hóa đơn {Ma}: khách đã nộp minh chứng, hãy đối chiếu trước." (Q3)
    // 6. Mỗi hóa đơn nợ !TongTienLaSoNguyen → $"Hóa đơn {Ma} có số tiền lẻ, cần hủy và lập lại." ; tienCoc lẻ → "Tiền cọc có số lẻ, không cấn trừ được." (Q4)
    public static IReadOnlyList<string> TimLyDoChan(int trangThaiHopDong, DateTime? thoiDiemKetThucUtc, decimal tienCoc, IReadOnlyList<ThanhLyHoaDonInput> hoaDons);
}
```

### 3.4 Giá trị mặc định của form gia hạn (mới, thuần)

`src/QuanLyChoThuePhongTroWeb.Application/Features/HopDongs/Services/GiaHanDefaults.cs`:

```csharp
public sealed record GiaHanMacDinh(int Thang, int Nam, DateOnly NgayKetThuc);
public static class GiaHanDefaults
{
    // Quy tắc tháng bắt đầu (spec §5.1 + Q10):
    // endVn = oldEnd + 7h. Nếu oldEnd null → tháng (nowVn) + 1.
    // Nếu (năm, tháng) của nowVn > (năm, tháng) của endVn → tháng nowVn + 1; ngược lại → tháng endVn + 1.
    // Ngày kết thúc mặc định = DateOnly(nam, thang, 1).AddMonths(N).AddDays(-1), với N theo Q10.
    public static GiaHanMacDinh Tinh(DateTime oldStartUtc, DateTime? oldEndUtc, DateTime nowUtc);
    public static DateTime DauThangVnUtc(int thang, int nam);          // ngày 1 00:00 VN, Kind Utc
    public static DateTime CuoiNgayVnUtc(DateOnly ngay);                // 23:59:59 VN, Kind Utc
}
```

### 3.5 Nhãn phương thức (mới)

`src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Payments/PaymentMethodLabels.cs`:
- `public static string Label(PhuongThucThanhToan p)` trả "Tiền mặt" / "Chuyển khoản" / "Cấn trừ tiền cọc".
- Giá trị lạ trả "Chuyển khoản", giữ hành vi cũ.

### 3.6 DTO (mới)

Tất cả nằm trong namespace `QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs`, class `sealed`, chỉ có kiểu nguyên thủy, `DateTime` (UTC), `DateOnly` và `List<>`.

**`.../HopDongs/DTOs/ThanhLyDtos.cs`:**
- `ThanhLyHopDongPreviewDto`:
  - `HopDongId`, `MaHopDong`, `SoPhong`, `TenChiNhanh`, `TenNguoiDaiDien`
  - `List<string> ThanhVienDangO`
  - `DateTime ThoiDiemBatDauUtc`, `DateTime? ThoiDiemKetThucUtc`
  - `decimal TienCoc`
  - `List<ThanhLyHoaDonDto> HoaDonNo`
  - `decimal TongNo`, `CanTru`, `CocHoanTrucTiep`, `ConNoSauCanTru`
  - `List<string> LyDoChan`, `bool CoTheThanhLy` (= `LyDoChan.Count == 0`)
- `ThanhLyHoaDonDto`: `HoaDonId`, `MaHoaDon`, `Thang`, `Nam`, `TongTien`, `DaThu`, `ConLai`, `CanTru`.
- `ThanhLyHopDongRequest`: `int HopDongId`, `decimal TongNoLucMo`, `bool XacNhanConNo`.
- `ThanhLyHopDongResult`: `HopDongId`, `CanTru`, `CocHoanTrucTiep`, `ConNoSauCanTru`.
- `BienBanThanhLyDto` (đầu vào cho PDF):
  - `MaHopDong`
  - `TenChiNhanh`, `DiaChiChiNhanh`, `SoDienThoaiChiNhanh`, `SoPhong`
  - `TenNguoiDaiDien`, `CccdNguoiDaiDien`, `SdtNguoiDaiDien`
  - `List<string> ThanhVien`
  - `DateTime ThoiDiemBatDauUtc`, `DateTime ThoiDiemKetThucUtc`, `DateTime NgayThanhLyUtc` (= `HopDong.NgayCapNhat ?? now`)
  - `decimal TienCoc`, `TongCanTru`, `CocHoanTrucTiep`, `NoConLai`
  - `DateTime NgayInUtc`
  - `List<BienBanHoaDonDto> HoaDons`: `MaHoaDon`, `Thang`, `Nam`, `TongTien`, `DaThuKhac`, `CanTru`, `ConLai`
  - `string? GhiChu`

**`.../HopDongs/DTOs/GiaHanDtos.cs`:**
- `GiaHanHopDongFormDto`:
  - `HopDongId`, `MaHopDong`, `MaHopDongMoiDuKien`
  - `SoPhong`, `TenChiNhanh`, `TenNguoiDaiDien`
  - `DateTime ThoiDiemBatDauUtc`, `DateTime? ThoiDiemKetThucUtc`
  - `decimal TienThue`, `TienCoc`
  - `int ThangBatDauMacDinh`, `NamBatDauMacDinh`, `DateOnly NgayKetThucMacDinh`
  - `List<string> DieuKhoanHienTai` (tiêu đề), `List<string> ThanhVienDangO`
- `GiaHanHopDongRequest`:
  - `int HopDongId`, `int ThangBatDau`, `int NamBatDau`, `DateOnly NgayKetThucMoi`
  - `decimal TienThue`, `decimal TienCoc`
  - `List<int>? DieuKhoanMauIds`: null = chép điều khoản cũ; rỗng = không điều khoản
- `GiaHanHopDongResult`: `HopDongMoiId`, `MaHopDongMoi`.

**`.../HopDongs/DTOs/TraCuuDtos.cs`:**
- `HopDongChiTietDto`:
  - `HopDongId`, `MaHopDong`, `MaGoc`, `LoaiText`
  - `PhongTroId`, `SoPhong`, `ChiNhanhId`, `TenChiNhanh`
  - `NguoiThueId`, `TenNguoiDaiDien`
  - `DateTime ThoiDiemBatDauUtc`, `DateTime? ThoiDiemKetThucUtc`
  - `TienThue`, `TienCoc`
  - `int TrangThai`, `string TrangThaiText`
  - `int? HopDongKeTiepId`, `string? MaHopDongKeTiep`
  - `bool CoTheThanhLy`, `bool CoTheGiaHan` (cả hai = `TrangThai == 1`), `bool CoBienBanThanhLy` (`TrangThai == 2` và không có hợp đồng kế tiếp)
  - `List<ThanhVienDto> ThanhVien` (`HoVaTen`, `NgayVaoUtc`, `NgayChuyenDiUtc?`)
  - `List<string> DieuKhoan`
  - `List<ChuoiHopDongItemDto> ChuoiHopDong` (`HopDongId`, `MaHopDong`, `LoaiText`, `TuUtc`, `DenUtc?`, `TrangThaiText`, `LaHienTai`)
- `LichSuThueDto`:
  - `NguoiThueId`, `HoVaTen`, `CCCD`, `SoDienThoai`
  - `int SoLanThue`, `SoHopDong`, `SoLanOGhep`, `TongSoThang`
  - `decimal NoHienTai`
  - `List<LichSuThueDongDto> Dong`
- `LichSuThueDongDto`: `HopDongId`, `MaHopDong`, `VaiTro` ("Đứng tên" | "Ở ghép"), `LoaiText`, `SoPhong`, `TenChiNhanh`, `TuUtc`, `DenUtc?`, `TrangThaiText`, `ConNo`.
- `NoCuDto`: `decimal TongNo`, `List<NoCuHoaDonDto> HoaDons` (`MaHoaDon`, `MaHopDong`, `Thang`, `Nam`, `ConLai`).

**`HopDongFilterReq.cs` (sửa):**
- Thêm `public string? BoLoc { get; set; }`, nhận "QuaHan" | "SapHetHan" | "GiaHan" | null.
- Thêm `public DateTime? MocDauNgayHomNayUtc { get; set; }`. Comment: service luôn ghi đè, giống `AllowedBranchIds`.

`TrangThaiText` của hợp đồng `DaKetThuc` có hợp đồng kế tiếp = `$"Đã gia hạn → {MaHopDongKeTiep}"` (Q11).

"Kế tiếp" = hợp đồng cùng mã gốc, chưa xóa, khác `DaHuy`, có `RenewalIndex` nhỏ nhất mà lớn hơn chỉ số hiện tại.

### 3.7 Store interface mới

`src/QuanLyChoThuePhongTroWeb.Application/Features/HopDongs/Persistence/IHopDongLifecycleStore.cs`.

Interface này tự đủ cho 3 service mới, để unit test không phải giả lập `IHopDongStore` (hơn 30 method). Không sửa chữ ký nào của `IHopDongStore`.

```csharp
public interface IHopDongLifecycleStore
{
    // Đọc (AsNoTracking). Hợp đồng chưa xóa; kèm PhongTro(+ChiNhanh), NguoiThue.
    Task<HopDong?> GetHopDongAsync(int hopDongId, CancellationToken ct = default);
    // Khóa FOR UPDATE, entity được track. null nếu đã xóa.
    Task<HopDong?> LockHopDongAsync(int hopDongId, CancellationToken ct = default);
    // Mọi hóa đơn chưa xóa của hợp đồng, FOR UPDATE ORDER BY HoaDonId, được track.
    Task<IReadOnlyList<HoaDon>> LockInvoicesOfContractAsync(int hopDongId, CancellationToken ct = default);
    // Hóa đơn chưa xóa của hợp đồng (không khóa), dùng cho preview/biên bản.
    Task<IReadOnlyList<HoaDon>> GetInvoicesOfContractAsync(int hopDongId, CancellationToken ct = default);
    // HoaDonId → tổng SoTienThanhToan của LichSuThanhToan chưa xóa.
    Task<IReadOnlyDictionary<int, decimal>> GetPaidTotalsAsync(IReadOnlyCollection<int> hoaDonIds, CancellationToken ct = default);
    // HoaDonId → tổng cấn trừ (PhuongThuc = CanTruTienCoc, chưa xóa).
    Task<IReadOnlyDictionary<int, decimal>> GetOffsetTotalsAsync(IReadOnlyCollection<int> hoaDonIds, CancellationToken ct = default);
    // Lượt thanh toán đang hoạt động (YeuCauThanhToanHoaDon.DangHoatDong(nowUtc)), được track.
    Task<IReadOnlyList<YeuCauThanhToanHoaDon>> GetActivePaymentRequestsAsync(IReadOnlyCollection<int> hoaDonIds, DateTime nowUtc, CancellationToken ct = default);
    void AddLedgerEntry(LichSuThanhToan entry);
    // true nếu còn hóa đơn chưa xóa của hợp đồng có (Nam, Thang) >= (nam, thang).
    Task<bool> HasInvoiceFromPeriodAsync(int hopDongId, int thang, int nam, CancellationToken ct = default);
    Task<PhongTro?> GetPhongTroForUpdateAsync(int phongTroId, CancellationToken ct = default);    // tracked
    Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken ct = default);
    Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int excludeHopDongId, CancellationToken ct = default);
    // Thành viên chưa xóa, NgayChuyenDi == null, được track.
    Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersAsync(int hopDongId, CancellationToken ct = default);
    // Thành viên chưa xóa (mọi trạng thái), kèm NguoiThue, AsNoTracking.
    Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetMembersAsync(int hopDongId, CancellationToken ct = default);
    // DangKyDichVu của phòng có NgayKetThuc == null, được track.
    Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesOfRoomAsync(int phongTroId, CancellationToken ct = default);
    Task<IReadOnlyList<HopDongDieuKhoan>> GetTermsAsync(int hopDongId, CancellationToken ct = default);
    Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermTemplatesByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
    // Số hợp đồng chưa xóa có mã khớp {maGoc}-GH{n}.
    Task<int> CountActiveRenewalsAsync(string maGoc, CancellationToken ct = default);
    Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken ct = default);   // kể cả đã xóa
    // Chuỗi: hợp đồng chưa xóa có MaHopDong == maGoc hoặc StartsWith(maGoc + "-GH"); service lọc lại bằng HopDongCodeChain.BelongsTo.
    Task<IReadOnlyList<HopDong>> GetChainAsync(string maGoc, CancellationToken ct = default);
    Task AddHopDongAsync(HopDong hopDong, CancellationToken ct = default);
    Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken ct = default);
    Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken ct = default);
    // Lịch sử thuê: hợp đồng chưa xóa, khác DaHuy (Q9), mà nguoiThueId đứng tên hoặc có dòng thành viên chưa xóa; lọc allowedBranchIds (null = mọi chi nhánh).
    Task<IReadOnlyList<LichSuThueRow>> GetRentalHistoryAsync(int nguoiThueId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
    // Nợ cũ: hóa đơn DaGui chưa xóa, ConLai > 0, của hợp đồng DaKetThuc chưa xóa có NguoiThueId = nguoiThueId; lọc allowedBranchIds.
    Task<IReadOnlyList<NoCuRow>> GetOldDebtsAsync(int nguoiThueId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
    Task<NguoiThue?> GetNguoiThueAsync(int nguoiThueId, CancellationToken ct = default);   // chưa xóa, AsNoTracking
}
```

File `.../HopDongs/Persistence/HopDongLifecycleRows.cs`:
- `LichSuThueRow`: `HopDongId`, `MaHopDong`, `bool LaNguoiDungTen`, `SoPhong`, `TenChiNhanh`, `ThoiDiemBatDau`, `ThoiDiemKetThuc?`, `int TrangThai`, `decimal ConNo`.
  - `ConNo` = tổng (`TongTien` − sổ chưa xóa) của hóa đơn `DaGui` chưa xóa còn dương.
- `NoCuRow`: `MaHoaDon`, `MaHopDong`, `Thang`, `Nam`, `ConLai`.

**Xong khi:** build PASS.

**Test (`Application.UnitTests`, file mới):**
- `HopDongCodeChainTests.cs`:
  - `RootOf`, `IsRenewal`, `RenewalIndex`, `BuildRenewal`, `LoaiText`.
  - Mã chi nhánh chứa "GH" (`HD-GH-2510-001`) không bị coi là gia hạn.
  - `...-GH0` và `...-GHx` không phải gia hạn.
  - `...-GH1-GH2` có mã gốc `...-GH1`; service không bao giờ sinh ra mã này.
- `ThanhLyCalculatorTests.cs`:
  - Cọc > nợ, cọc < nợ, không nợ, cọc 0.
  - Nhiều hóa đơn bị đảo thứ tự đầu vào: kỳ cũ nhận trước.
  - Hóa đơn `DaGui` đã trả đủ bị bỏ qua.
  - Từng lý do chặn 1–6, kể cả biên tháng VN: `ThoiDiemKetThuc` = 2026-10-31 16:59:59 UTC là tháng 10; 2026-10-31 17:00:00 UTC là tháng 11.
- `GiaHanDefaultsTests.cs`:
  - Hết hạn 31/10/2026 23:59:59 VN, hôm nay 20/10 → 11/2026.
  - Hôm nay 04/11 (đã quá hạn) → 12/2026.
  - Hợp đồng 12 tháng → ngày kết thúc 31/10/2027.
  - `oldEnd` null.
  - `DauThangVnUtc(11, 2026)` = 2026-10-31 17:00:00 UTC.
  - `CuoiNgayVnUtc(2026-10-31)` = 2026-10-31 16:59:59 UTC.
- `EmployeeAccessTests.cs` (sửa):
  - Thêm `InlineData` cho `Contract.Settle`, `Contract.Renew` vào test nhân viên được làm tại chi nhánh được phân công. Nếu không có Theory phù hợp thì thêm một Fact mới.
  - Hai mã này không nằm trong `AdminOnlyActions`.

---

## Task 4. Sổ thanh toán: nhãn, chặn hủy, không chọn được ở thu tay

### Application

- `Application/Features/HoaDons/Services/InvoiceLedgerService.cs`, `VoidPaymentAsync`: ngay sau kiểm `ledger.IsDeleted` (dòng 212–215) thêm:

  ```csharp
  if (ledger.PhuongThucThanhToan == PhuongThucThanhToan.CanTruTienCoc)
      return ServiceResult<VoidPaymentResult>.Fail("Khoản cấn trừ tiền cọc khi thanh lý hợp đồng không hủy được.");
  ```

- `Application/Features/OnlinePayments/PaymentNotificationFormatter.cs`, `PaymentMethodLabel` (dòng 192–205): dòng đầu tiên thêm `if (phuongThuc == PhuongThucThanhToan.CanTruTienCoc) return "Cấn trừ tiền cọc";`.
- `Application/Features/Dashboard/Services/DashboardService.cs` dòng 121: thay biểu thức ba ngôi bằng `PaymentMethodLabels.Label(p.PhuongThuc)`.
- `Application/Features/HoaDons/DTOs/HoaDonDTOs.cs`, `LichSuThanhToanRes`: thêm `public bool LaCanTruCoc { get; set; }`.

### Infrastructure

- `Persistence/Features/InvoiceDetailProjection.cs`:
  - Dòng 93: `PhuongThucThanhToan = PaymentMethodLabels.Label(ls.PhuongThucThanhToan)`.
  - Thêm `LaCanTruCoc = ls.PhuongThucThanhToan == PhuongThucThanhToan.CanTruTienCoc`.
- `Persistence/Features/LichSuThanhToanStore.cs` dòng 90: `PhuongThucThanhToanText = PaymentMethodLabels.Label(l.PhuongThucThanhToan)`. Dòng 136–139 giữ nguyên (Q6).

### Web

- `Areas/QuanLyNhaTro/Views/LichSuThanhToan/Index.cshtml`:
  - `#filterPhuongThuc` thêm `<option value="2">Cấn trừ tiền cọc</option>`.
  - Hàm render dòng 319–324: `d === 2` → `<span class="badge bg-purple-lt px-2 py-1"><i class="ti ti-arrows-exchange me-1"></i> Cấn trừ tiền cọc</span>`.
  - Dòng 339: `if (isAdmin && row.phuongThucThanhToan !== 2)`.
- `Areas/KhachThue/Views/LichSuThanhToan/Index.cshtml` dòng 90–97: thêm nhánh `data === 2` → badge "Cấn trừ tiền cọc".
- `Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml`:
  - Sau dòng "Tổng cộng" (dòng 1105): nếu `canTru = sum(ls.soTienThanhToan của ls.laCanTruCoc) > 0` thì thêm `<div class="hd-total-row"><span>Trừ tiền cọc</span><span class="hd-num">−${formatVND(canTru)}</span></div>`.
  - Ở dòng 1131–1134: nếu `ls.laCanTruCoc` thì không render hai nút Biên lai và Gửi lại (Q13).
- `Areas/KhachThue/Views/HoaDon/Index.cshtml`: trước khối "Lịch sử thanh toán" (dòng 276), cùng điều kiện, thêm dòng "Trừ tiền cọc: −X đ" theo class sẵn có của modal.

### Test

**`Application.UnitTests/InvoiceLedgerServiceTests.cs`** (thêm, dùng harness `Fakes/PaymentTestHarness.cs`):
1. `Void_OffsetLedger_IsRejected`: không đổi trạng thái hóa đơn, không ghi lịch sử.
2. `CollectManually_WithMethodValue2_IsRejected`: `(AppPhuongThucThanhToan)2` → `Fail("Phương thức thanh toán không hợp lệ.")`.

**`Application.UnitTests`, file mới `PaymentMethodLabelsTests.cs`:** 3 giá trị, cùng `PaymentNotificationFormatter.PaymentMethodLabel("CANTRU-HD1-2", CanTruTienCoc)` → "Cấn trừ tiền cọc".
- Nếu `PaymentMethodLabel` là `internal` thì kiểm qua chỗ đang có test của formatter. Nếu không có `InternalsVisibleTo` thì bỏ phần formatter.

**Xong khi:** test Application PASS.

---

## Task 5. Infrastructure: `HopDongLifecycleStore`, sinh mã, tìm kiếm, bộ lọc

### File mới

`src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/HopDongLifecycleStore.cs`, `public class HopDongLifecycleStore : IHopDongLifecycleStore`, constructor `(ApplicationDbContext context)`, theo mẫu `HopDongStore.cs`.

- **Khóa:**
  - `LockHopDongAsync`: `_context.HopDongs.FromSqlInterpolated($"SELECT * FROM hop_dong WHERE \"HopDongId\" = {id} AND \"IsDeleted\" = false FOR UPDATE").SingleOrDefaultAsync(ct)`.
  - `LockInvoicesOfContractAsync`: `SELECT * FROM hoa_don WHERE "HopDongId" = {id} AND "IsDeleted" = false ORDER BY "HoaDonId" FOR UPDATE`.
- `GetPaidTotalsAsync` / `GetOffsetTotalsAsync`: `GroupBy(HoaDonId).Select(Sum)` trên `LichSuThanhToans` chưa xóa. Id không có dòng sổ thì không có trong dictionary; service coi là 0.
- `GetActivePaymentRequestsAsync`: `.Where(y => ids.Contains(y.HoaDonId)).Where(YeuCauThanhToanHoaDon.DangHoatDong(nowUtc))`, theo mẫu `InvoicePaymentStore.GetActiveRequestAsync` dòng 44–51.
- `CountActiveRenewalsAsync`: `CountAsync(x => !x.IsDeleted && x.MaHopDong.StartsWith(maGoc + "-GH") && Regex.IsMatch(x.MaHopDong, "^" + Regex.Escape(maGoc) + HopDongCodeChain.RenewalPattern))`. Npgsql dịch thành `~`.
  - Nếu Npgsql không dịch được `Regex.Escape` thì lấy danh sách mã về bộ nhớ rồi lọc bằng `HopDongCodeChain.BelongsTo` và `IsRenewal`.
- `GetRentalHistoryAsync`:
  - Hai nguồn, gộp theo `HopDongId`; đứng tên được ưu tiên hơn ở ghép.
  - `allowedBranchIds != null` → `Where(allowed.Contains(hd.PhongTro.ChiNhanhId))` **trước** khi đếm.
  - `ConNo` tính bằng subquery `HoaDons.Where(!IsDeleted && DaGui).Sum(TongTien - LichSuThanhToans.Where(!IsDeleted).Sum(SoTien))`, chỉ cộng phần dương. Mẫu: `ViecCanLamStore` (`TongConNo` của TT2).
- `GetOldDebtsAsync`: điều kiện như comment ở interface. Sắp `Nam`, `Thang`, `HoaDonId`.
- Các method còn lại copy truy vấn tương ứng từ `HopDongStore.cs`:
  - `IsRoomRentedExcludingContractAsync`, `IsTenantActiveInAnotherContractAsync`, `GetActiveMembersByHopDongIdAsync`
  - `GetActiveServicesByRoomIdsAsync`, `GetHopDongDieuKhoansByHopDongIdAsync`, `GetActiveTermsByIdsAsync`
  - `ExistsContractWithCodeAsync`, `AddAsync`, `AddMembersAsync`, `AddTermsAsync`

### File sửa

- `Infrastructure/DependencyInjection.cs`: sau dòng 44 thêm `services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence.IHopDongLifecycleStore, Persistence.Features.HopDongLifecycleStore>();`.
- `Persistence/Features/HopDongStore.cs`:
  - `CountContractsWithPrefixAsync` (dòng 159–162) → `CountAsync(x => x.MaHopDong.StartsWith(prefix) && !Regex.IsMatch(x.MaHopDong, HopDongCodeChain.RenewalPattern))`. Vẫn đếm cả bản đã xóa như hiện nay.
  - `GetDataTableResponseAsync`:
    - Ô tìm kiếm (dòng 60–67) thêm các điều kiện, dùng `ToLower()` như hiện có:
      - `x.NguoiThue.CCCD.Contains(searchValue)`
      - `x.NguoiThue.SoDienThoai.Contains(searchValue)`
      - `x.ChiTietThanhVienHopDongs.Any(m => !m.IsDeleted && (m.NguoiThue.HoVaTen.ToLower().Contains(searchValue) || m.NguoiThue.CCCD.Contains(searchValue) || m.NguoiThue.SoDienThoai.Contains(searchValue)))`
    - Mã: `Contains` hiện có đã ra cả chuỗi khi gõ mã gốc, giữ nguyên.
    - `BoLoc`, đặt ngay sau lọc trạng thái (dòng 49–52):
      - `"QuaHan"`: `DangHoatDong && ThoiDiemKetThuc != null && ThoiDiemKetThuc < MocDauNgayHomNayUtc`
      - `"SapHetHan"`: `DangHoatDong && ThoiDiemKetThuc >= Moc && ThoiDiemKetThuc < Moc.AddDays(31)`
      - `"GiaHan"`: `Regex.IsMatch(x.MaHopDong, HopDongCodeChain.RenewalPattern)`
      - `MocDauNgayHomNayUtc == null` → bỏ qua hai bộ lọc ngày.
- `Persistence/Features/InvoiceViewStore.cs`:
  - Sau lọc `TrangThaiPhatHanh` (dòng 61–69): nếu `filter.HopDongDaKetThucConNo` thì `query = query.Where(h => !h.IsDeleted && h.TrangThaiPhatHanh == DaGui && h.TrangThaiHoaDon != DaThanhToan && h.HopDong.TrangThaiHopDong == DaKetThuc && !h.HopDong.IsDeleted && !_context.HopDongs.Any(o => !o.IsDeleted && o.TrangThaiHopDong == DangHoatDong && o.PhongTroId == h.HopDong.PhongTroId && o.NguoiThueId == h.HopDong.NguoiThueId))` (Q1).
  - `Application/Features/HoaDons/DTOs/InvoiceViewDtos.cs`, `InvoiceListFilter`: thêm `public bool HopDongDaKetThucConNo { get; set; }`.

### Test (`Infrastructure.IntegrationTests`)

File mới `Persistence/HopDongLifecycleStoreTests.cs`, `[Collection("PostgreSqlCollection")]`, dùng `PostgreSqlFixture`. Copy cách seed của `Persistence/ViecCanLamStoreTests.cs` và `DebtTestSeeder.cs`.

1. `CountContractsWithPrefix_IgnoresRenewalCodes`: có `HD-X-2611-001`, `HD-X-2611-001-GH1`, `HD-X-2611-002` → 2.
2. `CountActiveRenewals_CountsOnlyNonDeletedRenewalsOfRoot`: có GH1, GH2 (GH2 đã xóa), một mã khác gốc → 1.
3. `GetChain_ReturnsRootAndRenewals`: không lấy `HD-X-2611-0010`.
4. `RentalHistory_IncludesRepresentativeAndMember_FilteredByBranch`: người A đứng tên HĐ1 (chi nhánh 1) và ở ghép HĐ2 (chi nhánh 2), có một HĐ `DaHuy`.
   - Admin (`allowed = null`) → 2 dòng.
   - `allowed = {1}` → 1 dòng.
   - `ConNo` đúng.
5. `OldDebts_OnlyEndedContractsOfRepresentative`.
6. `LockHopDong_And_LockInvoices_ReturnTrackedEntities`: trong transaction, sửa rồi `SaveChanges`, giá trị được lưu.
7. `DataTable_SearchByMemberCccdAndPhone` và `DataTable_BoLoc_QuaHan_SapHetHan_GiaHan`: gọi `HopDongStore.GetDataTableResponseAsync` với `MocDauNgayHomNayUtc` cố định.
8. `InvoiceList_HopDongDaKetThucConNo_ExcludesRenewedTenant`: gọi `InvoiceViewStore.GetEmployeeListAsync`.

File mới `Persistence/BillingMonthBoundaryTests.cs`:
- `ValidContractsForBilling_RenewalPair_SplitsAtVietnamMonthBoundary`.
- HĐ cũ `DaKetThuc`, `ThoiDiemKetThuc = 2026-10-31 16:59:59Z`. HĐ mới `DangHoatDong`, `ThoiDiemBatDau = 2026-10-31 17:00:00Z`, cùng phòng.
- Gọi `HoaDonStore.GetValidContractsForBillingAsync(chiNhanhId, [phong], startUtc, endUtc)` với start/end tính đúng như `InvoiceIssuanceService`: start = ngày 1 00:00 VN − 7h; end = ngày cuối 23:59:59 VN − 7h.
- Tháng 10 chỉ trả HĐ cũ; tháng 11 chỉ trả HĐ mới.
- **Không sửa** `HoaDonStore`. Nếu test FAIL thì dừng lại và báo orchestrator, không tự sửa.

**Xong khi:** build PASS. Test Infrastructure PASS, hoặc CHƯA CHẠY nếu thiếu `QLCTPT_TEST_CONNECTION_STRING`.

---

## Task 6. Application: `HopDongService` (form sửa, danh sách)

**File sửa:** `src/QuanLyChoThuePhongTroWeb.Application/Features/HopDongs/Services/HopDongService.cs`

1. Constructor: thêm tham số cuối tùy chọn `TimeProvider? timeProvider = null`, gán `_timeProvider = timeProvider ?? TimeProvider.System`. Theo mẫu constructor `InvoicePaymentTransaction`. `HopDongServiceTests` hiện có không phải sửa.
2. `DanhSachHopDongSideAsync`: trước khi gọi store, gán `request.MocDauNgayHomNayUtc` = đầu ngày hôm nay VN tính bằng UTC (theo `_timeProvider`).
3. `UpdateCoreAsync`:
   - Đầu hàm:
     - `if (entity.TrangThaiHopDong == TrangThaiHopDong.DaKetThuc) return (false, "Hợp đồng đã kết thúc (thanh lý hoặc gia hạn), không sửa được.");` (Q7)
     - `if (targetStatus == TrangThaiHopDong.DaKetThuc) return (false, "Muốn kết thúc hợp đồng, hãy dùng Thanh lý hoặc Gia hạn.");`
     - `if (targetStatus == TrangThaiHopDong.DaHuy && HopDongCodeChain.IsRenewal(entity.MaHopDong)) return (false, "Không hủy được hợp đồng gia hạn.");` (Q7)
   - Thông báo `DaHuy` khi đã có hóa đơn (dòng 292) → `"Hợp đồng đã phát sinh hóa đơn, không thể hủy. Hãy dùng Thanh lý hợp đồng."`.
   - Xóa khối dòng 310–313, đoạn tự điền `ThoiDiemKetThuc`.
   - Kiểm thông tin cốt lõi (dòng 302): thay `input.ThoiDiemBatDau.ToUniversalTime().Date != entity.ThoiDiemBatDau.Date` bằng `input.ThoiDiemBatDau.Date != entity.ThoiDiemBatDau.AddHours(7).Date`.
     - Lý do: form gửi ngày VN dạng `Y-m-d`. Hợp đồng gia hạn lưu 17:00Z ngày hôm trước, phải sửa được `ThoiDiemKetThuc` ở bước 1 quy trình trả phòng (spec §3).
   - Dòng 356: chỉ gán `entity.ThoiDiemBatDau = input.ThoiDiemBatDau.ToUniversalTime()` khi `entity.TrangThaiHopDong != TrangThaiHopDong.DangHoatDong`. Hợp đồng đang hoạt động giữ nguyên giá trị đã lưu.
   - Khối đổi trạng thái (dòng 337–346): khi `targetStatus == DaHuy` thì đóng thêm dịch vụ đang hiệu lực của phòng: `foreach (var dv in await _store.GetActiveServicesByRoomIdsAsync(new[] { entity.PhongTroId })) dv.NgayKetThuc = DateTime.UtcNow;`.
     - Nhánh `DaKetThuc` giờ không còn tới được; giữ code cũ là đủ.

**Test (`Application.UnitTests/HopDongServiceTests.cs`, thêm):**
- `Update_ToDaKetThuc_IsRejected`
- `Update_EndedContract_IsRejected`
- `Update_CancelRenewal_IsRejected`
- `Update_ToDaHuy_ClosesRoomServices`: `StubHopDongStore.GetActiveServicesByRoomIdsAsync` trả 1 dịch vụ → có `NgayKetThuc`.
- `Update_ActiveRenewal_StartStoredAt17Utc_EditEndDate_Succeeds`: entity `ThoiDiemBatDau = 2026-10-31 17:00Z`, input `ThoiDiemBatDau = 2026-11-01` (Unspecified) → thành công, `entity.ThoiDiemBatDau` không đổi.
- `DanhSach_SetsMocDauNgayHomNay`: `FixedTimeProvider` 2026-11-04 18:00Z → `2026-11-04 17:00Z`.

**Xong khi:** test Application PASS.

---

## Task 7. Application: `ThanhLyHopDongService` và exporter interface

### File mới

- `src/QuanLyChoThuePhongTroWeb.Application/Abstractions/Services/IBienBanThanhLyExporter.cs`: `public interface IBienBanThanhLyExporter { byte[] ExportPdf(BienBanThanhLyDto data); }`. Đặt cạnh `IInvoiceDocumentExporter`, cùng namespace.
- `.../HopDongs/Services/IThanhLyHopDongService.cs`:

  ```csharp
  public interface IThanhLyHopDongService
  {
      Task<ServiceResult<ThanhLyHopDongPreviewDto>> GetPreviewAsync(int actorId, int hopDongId, CancellationToken ct = default);
      Task<ServiceResult<ThanhLyHopDongResult>> ThanhLyAsync(int actorId, ThanhLyHopDongRequest request, CancellationToken ct = default);
      Task<ServiceResult<byte[]>> XuatBienBanAsync(int actorId, int hopDongId, string? ghiChu, CancellationToken ct = default);
  }
  ```

- `.../HopDongs/Services/ThanhLyHopDongService.cs`. Constructor: `(IHopDongLifecycleStore store, IUnitOfWork unitOfWork, IEmployeeAccessService access, IThongBaoService thongBao, IBienBanThanhLyExporter exporter, TimeProvider timeProvider, ILogger<ThanhLyHopDongService> logger)`.

### `GetPreviewAsync`

- Quyền đọc: scope + `CanAccessBranch`. Không cần `CanPerform`, để nhân viên xem được lý do chặn.
- Đọc hợp đồng, hóa đơn, sổ, lượt thanh toán (không khóa).
- `ThanhLyCalculator.Tinh` + `TimLyDoChan`. Thành viên đang ở = `GetMembersAsync` lọc `NgayChuyenDi == null`.
- Kết quả luôn `Ok`; trường hợp bị chặn thể hiện qua `LyDoChan`.

### `ThanhLyAsync`

1. Quyền ghi (`ContractSettle`).
2. Mở transaction.
3. `LockHopDongAsync`; null → `NotFound`. `TrangThai != DangHoatDong` → `Fail("Hợp đồng đã được thanh lý hoặc gia hạn. Vui lòng tải lại trang.")`.
4. `LockInvoicesOfContractAsync`, `GetPaidTotalsAsync`, `GetActivePaymentRequestsAsync(now)`. Dựng `ThanhLyHoaDonInput`: `CoLuotDangDoiChieu` = có lượt `DangDoiChieu`; `TongTienLaSoNguyen` = `hd.TongTienLaSoNguyen()`.
5. `TimLyDoChan` khác rỗng → rollback, `Fail(string.Join(" ", lyDo))`.
6. `calc.TongNo != request.TongNoLucMo` → `Fail($"Số nợ đã thay đổi từ {cũ} đ thành {mới} đ. Vui lòng tải lại trang.")`. Định dạng số bằng `InvoicePaymentPolicy.FormatVnd`.
7. `calc.ConNoSauCanTru > 0 && !request.XacNhanConNo` → `Fail($"Cần xác nhận khách còn nợ {X} đ sau khi cấn trừ cọc.")`.
8. Với mỗi phân bổ có `CanTru > 0`:
   - `AddLedgerEntry(new LichSuThanhToan { HoaDonId, MaGiaoDich = $"CANTRU-HD{HopDongId}-{HoaDonId}", SoTienThanhToan = CanTru, PhuongThucThanhToan = CanTruTienCoc, NgayThanhToan = now, NgayXacNhan = now, NguoiXacNhanId = actorId, GhiChu = $"Cấn trừ tiền cọc hợp đồng {MaHopDong}" })`
   - Hủy lượt `ChoThanhToan` của hóa đơn đó: `y.Huy(actorId, now, "Đã cấn trừ tiền cọc khi thanh lý")` (Q3).
   - `hoaDon.CapNhatTrangThaiThanhToan(daThu + CanTru, actorId, $"Cấn trừ tiền cọc hợp đồng {MaHopDong}", now)`.
9. `hopDong.TrangThaiHopDong = DaKetThuc; hopDong.NgayCapNhat = now`.
10. `!IsRoomRentedExcludingContractAsync(phongId, hopDongId)` → `GetPhongTroForUpdateAsync`, đặt `TrangThai = Trong`, `NgayCapNhat = now`.
11. `GetActiveMembersAsync` → `NgayChuyenDi = hopDong.ThoiDiemKetThuc`.
12. `GetActiveServicesOfRoomAsync` → `NgayKetThuc = max(dv.NgayBatDau, hopDong.ThoiDiemKetThuc.Value)`.
13. `SaveChangesAsync`, commit. Có exception → rollback, `LogError(ex, ...)`, `Fail("Lỗi hệ thống khi thanh lý hợp đồng.")`.
14. Sau commit:
    - `GuiChoNguoiPhuTrachPhongAsync(phongId, $"Đã thanh lý hợp đồng {Ma}", $"Phòng {SoPhong}: cấn trừ cọc {X} đ, hoàn cọc trực tiếp {Y} đ, khách còn nợ {Z} đ.", "HopDong", $"/QuanLyNhaTro/HopDong/ChiTiet/{id}")`, bọc try/catch, `LogWarning`.
    - Trả `Ok(result, "Đã thanh lý hợp đồng.")`.

### `XuatBienBanAsync`

- Quyền đọc như preview.
- Hợp đồng phải `DaKetThuc` và không có hợp đồng kế tiếp trong chuỗi (dùng `GetChainAsync`). Ngược lại → `Fail("Hợp đồng chưa thanh lý, không có biên bản.")`.
- Hóa đơn `DaGui` chưa xóa của hợp đồng, kèm `GetPaidTotalsAsync` và `GetOffsetTotalsAsync`. Chỉ lấy hóa đơn có `CanTru > 0` hoặc `ConLai > 0`.
- `DaThuKhac = paid − offset`; `NoConLai` = tổng `ConLai` hiện tại (Q5); `CocHoanTrucTiep = TienCoc − TongCanTru`.
- `GhiChu` = `ghiChu?.Trim()`, cắt tối đa 1000 ký tự.
- Trả `Ok(_exporter.ExportPdf(dto))`.

### DI

- `Application/DependencyInjection.cs`, khối Phase 6.3: `services.AddScoped<IThanhLyHopDongService, ThanhLyHopDongService>();`.
- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/BranchScopeArchitectureTests.cs`: thêm `typeof(IThanhLyHopDongService)` vào `ScopedServices`. Không thêm ngoại lệ.

### Fake (mới, `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/Fakes/`)

- `FakeHopDongLifecycleStore.cs`: danh sách trong bộ nhớ (HopDongs, HoaDons, Ledgers, Requests, Members, Services, Terms, PhongTros). Ghi lại `LockHopDongCalls` và thứ tự gọi khóa.
- `FakeBienBanThanhLyExporter.cs`: ghi lại `LastData`, trả `new byte[] { 0x25, 0x50, 0x44, 0x46 }`.
- Unit of work dùng `RecordingUnitOfWork` trong `Fakes/PaymentTestHarness.cs`. Quyền dùng `FakeEmployeeAccessService` (`ScopeToReturn`, `Permissions`).

### Test (`Application.UnitTests`, file mới `ThanhLyHopDongServiceTests.cs`)

1. Cọc 3tr, nợ 2 hóa đơn (T9 1,5tr; T10 2tr, đầu vào xếp T10 trước):
   - T9 nhận 1,5tr, T10 nhận 1,5tr.
   - 2 dòng sổ `CanTruTienCoc` đúng `MaGiaoDich`, `GhiChu`, `NguoiXacNhanId`.
   - T9 `DaThanhToan`, T10 `ThanhToanMotPhan`.
   - Đòi `XacNhanConNo = true` (còn 0,5tr).
2. Cọc > nợ: không cần cờ; `CocHoanTrucTiep` đúng.
3. Không nợ: không có dòng sổ; hợp đồng vẫn `DaKetThuc`.
4. Thiếu cờ xác nhận khi còn nợ → `Fail`; không có thay đổi; transaction rollback hoặc không commit.
5. `TongNoLucMo` lệch → `Fail` với chữ "đã thay đổi".
6. Thiếu hóa đơn `DaGui` tháng trả phòng → `Fail`. Còn hóa đơn `ChoDuyet` → `Fail`.
7. Hợp đồng đã `DaKetThuc` (bấm hai lần) → `Fail`, không tạo dòng sổ.
8. Phòng còn hợp đồng hoạt động khác → phòng giữ `DaThue`. Không có hợp đồng khác → `Trong`.
9. Thành viên `NgayChuyenDi` và dịch vụ `NgayKetThuc` = `ThoiDiemKetThuc`.
10. Lượt `ChoThanhToan` bị hủy; có lượt `DangDoiChieu` → `Fail`.
11. Phân quyền:
    - Scope null → `Forbidden`.
    - Nhân viên khác chi nhánh → `Forbidden`, không gọi `LockHopDongAsync`.
    - Thiếu `Contract.Settle` → `Forbidden`.
12. Thông báo:
    - Sau commit, `FakeThongBaoService.Sent` có 1 dòng Kind "Phong".
    - `ThrowOnSend` → kết quả vẫn `Ok`.
    - Commit lỗi (`FailOnSave`) → không gửi thông báo.
13. `XuatBienBan`:
    - Chưa thanh lý → `Fail`.
    - Đã thanh lý → exporter nhận đúng `TongCanTru`, `CocHoanTrucTiep`, `NoConLai`, `GhiChu`.
    - Gọi không có ghi chú → `GhiChu` null.

**Xong khi:** test Application PASS, kể cả `BranchScopeArchitectureTests` và `ArchitectureBoundaryTests`.

---

## Task 8. Application: `GiaHanHopDongService`

### File mới

- `.../HopDongs/Services/IGiaHanHopDongService.cs`:

  ```csharp
  public interface IGiaHanHopDongService
  {
      Task<ServiceResult<GiaHanHopDongFormDto>> GetFormAsync(int actorId, int hopDongId, CancellationToken ct = default);
      Task<ServiceResult<GiaHanHopDongResult>> GiaHanAsync(int actorId, GiaHanHopDongRequest request, CancellationToken ct = default);
  }
  ```

- `.../HopDongs/Services/GiaHanHopDongService.cs`. Constructor: `(IHopDongLifecycleStore store, IUnitOfWork unitOfWork, IEmployeeAccessService access, IThongBaoService thongBao, TimeProvider timeProvider, ILogger<GiaHanHopDongService> logger)`.

### `GetFormAsync`

- Quyền đọc.
- Hợp đồng phải `DangHoatDong`, nếu không → `Fail("Chỉ gia hạn được hợp đồng đang hoạt động.")`.
- Giá trị mặc định lấy từ `GiaHanDefaults.Tinh`.
- `MaHopDongMoiDuKien = BuildRenewal(RootOf(ma), CountActiveRenewalsAsync + 1)`.

### `GiaHanAsync`

1. Kiểm đầu vào, trước khi mở transaction:
   - `ThangBatDau` 1–12, `NamBatDau` 2000–2100.
   - `NgayKetThucMoi > new DateOnly(nam, thang, 1)`, nếu sai → `"Ngày kết thúc mới phải sau ngày bắt đầu mới."`.
   - `TienThue > 0` → `"Giá thuê mới phải lớn hơn 0."`; `TienCoc >= 0`.
2. Quyền ghi (`ContractRenew`).
3. `newStartUtc = GiaHanDefaults.DauThangVnUtc(thang, nam)`; `newEndUtc = GiaHanDefaults.CuoiNgayVnUtc(NgayKetThucMoi)`; `oldEndUtc = newStartUtc.AddSeconds(-1)`.
4. Mở transaction. `LockHopDongAsync(old)`; phải `DangHoatDong`, nếu không → `Fail("Hợp đồng đã được thanh lý hoặc gia hạn. Vui lòng tải lại trang.")`.
5. `newStartUtc <= old.ThoiDiemBatDau` → `Fail("Tháng bắt đầu mới phải sau ngày bắt đầu hợp đồng cũ.")`.
6. `HasInvoiceFromPeriodAsync(old, thang, nam)` → `Fail($"Hợp đồng cũ đã có hóa đơn từ T{thang}/{nam} trở đi. Chọn tháng bắt đầu sau kỳ hóa đơn cuối.")`.
7. `IsRoomRentedExcludingContractAsync(phong, old)` hoặc `IsTenantActiveInAnotherContractAsync(nguoiThue, old)` → `Fail` (lỗi dữ liệu). Không gọi kiểm trùng thành viên: thành viên được chép từ chính hợp đồng cũ.
8. Mã mới: `root = RootOf(old.MaHopDong)`; `n = CountActiveRenewalsAsync(root) + 1`; tối đa 5 lần, `ExistsContractWithCodeAsync(BuildRenewal(root, n))` thì `n++`. Hết lượt → `Fail("Không sinh được mã hợp đồng gia hạn, vui lòng thử lại.")`.
9. Cập nhật hợp đồng cũ: `ThoiDiemKetThuc = oldEndUtc`, `TrangThai = DaKetThuc`, `NgayCapNhat = now`.
10. Tạo hợp đồng mới: `new HopDong { MaHopDong, PhongTroId, NguoiThueId (của cũ), ThoiDiemBatDau = newStartUtc, ThoiDiemKetThuc = newEndUtc, TienThuePhong, TienCocPhong, TrangThai = DangHoatDong, NgayTao = now, IsDeleted = false }`. Gọi `AddHopDongAsync`, rồi `SaveChangesAsync` để lấy id.
11. Thành viên: `GetActiveMembersAsync(old)` → mỗi dòng đặt `NgayChuyenDi = oldEndUtc` và thêm dòng mới `{ HopDongId = moi, NguoiThueId, NgayVao = newStartUtc }`.
12. Điều khoản:
    - `DieuKhoanMauIds == null` → chép `GetTermsAsync(old)` (`TieuDe`, `NoiDung`, `ThuTu`).
    - Có giá trị → `GetActiveTermTemplatesByIdsAsync`, `ThuTu` từ 1, như `CreateCoreAsync` dòng 219–232.
13. Không đổi phòng hay dịch vụ.
14. `SaveChangesAsync`, commit. Exception → rollback, `LogError`, `Fail("Lỗi hệ thống khi gia hạn hợp đồng.")`.
15. Sau commit:
    - `GuiChoNguoiPhuTrachPhongAsync(phong, $"Đã gia hạn hợp đồng {cũ} → {mới}", ..., "HopDong", $"/QuanLyNhaTro/HopDong/ChiTiet/{moiId}")`.
    - `GuiChoKhachThueAsync(nguoiThueId, "Hợp đồng đã được gia hạn", $"Hợp đồng mới {mới} từ {dd/MM/yyyy} đến {dd/MM/yyyy}.", "HopDong", "/KhachThue/HopDong")`.
    - Cả hai bọc try/catch, `LogWarning`.

### DI

`AddApplication`: `services.AddScoped<IGiaHanHopDongService, GiaHanHopDongService>();`. Thêm `typeof(IGiaHanHopDongService)` vào `ScopedServices`.

### Test (`Application.UnitTests`, file mới `GiaHanHopDongServiceTests.cs`, dùng `FakeHopDongLifecycleStore`)

1. Gia hạn từ 11/2026:
   - Hợp đồng cũ `ThoiDiemKetThuc = 2026-10-31 16:59:59Z`, `DaKetThuc`.
   - Hợp đồng mới `ThoiDiemBatDau = 2026-10-31 17:00:00Z`, mã `{gốc}-GH1`.
   - Thành viên được chép, cũ có `NgayChuyenDi`, mới có `NgayVao`.
   - Điều khoản được chép.
   - Phòng không đổi.
2. Gia hạn lần hai từ hợp đồng `-GH1` → `-GH2`, mã gốc không lồng.
3. Mã `-GH1` đã tồn tại ở bản đã xóa → `-GH2`.
4. Có hóa đơn T11 trên hợp đồng cũ → `Fail`.
5. Tháng bắt đầu ≤ ngày bắt đầu cũ → `Fail`.
6. Giá 0 → `Fail`. Ngày kết thúc ≤ ngày bắt đầu → `Fail`.
7. `DieuKhoanMauIds` = [id] → dùng mẫu. Rỗng → không có điều khoản.
8. Phân quyền: scope null, khác chi nhánh, thiếu `Contract.Renew` → `Forbidden`, không gọi khóa.
9. Thông báo: 1 "Phong" + 1 "Khach". `ThrowOnSend` → vẫn `Ok`.
10. `GetFormAsync` trả mặc định theo `GiaHanDefaults` và `MaHopDongMoiDuKien`.

**Xong khi:** test Application PASS.

---

## Task 9. Application: `HopDongTraCuuService` và KH4/KH5

### File mới

- `.../HopDongs/Services/IHopDongTraCuuService.cs`:

  ```csharp
  public interface IHopDongTraCuuService
  {
      Task<ServiceResult<HopDongChiTietDto>> GetChiTietAsync(int actorId, int hopDongId, CancellationToken ct = default);
      Task<ServiceResult<LichSuThueDto>> GetLichSuThueAsync(int actorId, int nguoiThueId, CancellationToken ct = default);
      Task<ServiceResult<NoCuDto>> GetNoCuAsync(int actorId, int nguoiThueId, CancellationToken ct = default);
  }
  ```

- `.../HopDongs/Services/HopDongTraCuuService.cs`. Constructor: `(IHopDongLifecycleStore store, IEmployeeAccessService access, ITenantVisibilityStore tenantVisibility, TimeProvider timeProvider)`.

### Hành vi

- **`GetChiTietAsync`:**
  - Quyền đọc theo chi nhánh của phòng.
  - Chuỗi lấy từ `GetChainAsync(RootOf(ma))` lọc `BelongsTo`, sắp theo `RenewalIndex`.
  - "Kế tiếp" và `TrangThaiText` theo mục 3.6.
- **`GetLichSuThueAsync`:**
  - Scope null → `Forbidden`.
  - `GetNguoiThueAsync` null → `NotFound("Không tìm thấy người thuê.")`.
  - Nhân viên mà `!await _tenantVisibility.AreAllVisibleAsync(new[] { id }, scope.AllowedBranchIds!)` → `NotFound` (không để lộ người thuê của chi nhánh khác).
  - Dòng lấy từ `GetRentalHistoryAsync(id, scope.AllowedBranchIds)`.
  - Tóm tắt theo Q9: `SoLanThue` = số mã gốc khác nhau; `NoHienTai` = tổng `ConNo` của các dòng đứng tên (Q8).
- **`GetNoCuAsync`:** kiểm như lịch sử, rồi `GetOldDebtsAsync(id, scope.AllowedBranchIds)`.

### Việc cần làm (sửa)

- `Application/Features/ViecCanLam/Persistence/IViecCanLamStore.cs`, thêm:

  ```csharp
  // KH4: hợp đồng DangHoatDong chưa xóa, ThoiDiemKetThuc != null && < homNayVnBatDauUtc.
  Task<IReadOnlyList<DemChiNhanhRow>> DemHopDongQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime homNayVnBatDauUtc, CancellationToken ct = default);
  // KH5: hợp đồng DaKetThuc chưa xóa có hóa đơn DaGui chưa xóa còn nợ, trừ trường hợp ở Q1; SoLuong = số hợp đồng, TongConNo = tổng còn nợ.
  Task<IReadOnlyList<DemHoaDonRow>> DemHopDongDaTraPhongConNoAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
  ```

- `Infrastructure/Persistence/Features/ViecCanLamStore.cs`: cài đặt theo mẫu `DemHopDongSapHetHanAsync` (KH3) và `DemHoaDonQuaHanAsync` (`TongConNo`).
- `Application/Features/ViecCanLam/Services/ViecCanLamService.cs`, ngay sau khối KH3 (dòng 162–172):
  - KH4, không cần quyền, giống KH3: `ThemViec(items, "KH4", NhomViec.KhachThueHopDong, MucUuTienViec.Khan, "Hợp đồng đã quá hạn chưa xử lý", dongs, branchId, "/QuanLyNhaTro/QuanLyHopDong?boLoc=QuaHan")`.
  - KH5, trong `if (DuocPhep(scope, EmployeeActionCodes.InvoiceRead))`: `ThemViec(items, "KH5", NhomViec.KhachThueHopDong, MucUuTienViec.CanLam, "Khách đã trả phòng còn nợ", TuHoaDon(rows), branchId, "/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam=0&trangThaiPhatHanh=3&hopDongDaKetThucConNo=1", tongConNo: ...)`.
- `Web/Areas/QuanLyNhaTro/ViewModels/Responses/ViecCanLam.cs` dòng 51: thêm `"KH5"` vào danh sách nút "Xem".
- `tests/.../Application.UnitTests/Fakes/FakeViecCanLamStore.cs`: cài 2 method mới, trả danh sách cấu hình được, mặc định rỗng.

### DI

`AddApplication`: `services.AddScoped<IHopDongTraCuuService, HopDongTraCuuService>();`. Thêm `typeof(IHopDongTraCuuService)` vào `ScopedServices`.

### Test

- `Application.UnitTests`, file mới `HopDongTraCuuServiceTests.cs`:
  - Chuỗi sắp đúng thứ tự.
  - "Đã gia hạn → mã" xuất hiện khi có hợp đồng kế tiếp; kế tiếp `DaHuy` thì bị bỏ qua.
  - `CoBienBanThanhLy` đúng.
  - Lịch sử: tóm tắt đúng; nhân viên không thấy người thuê → `NotFound`; scope null → `Forbidden`.
- `ViecCanLamServiceTests.cs`, thêm:
  - KH4 xuất hiện với mức `Khan` và đúng link.
  - KH5 cần `InvoiceRead`, có `TongConNo`.
  - `homNayVnBatDauUtc` truyền vào đúng.
- `Infrastructure.IntegrationTests/Persistence/ViecCanLamStoreTests.cs`, thêm:
  - KH4 lọc chi nhánh và biên đầu ngày.
  - KH5 loại hợp đồng đã trả hết và loại khách đã gia hạn (Q1).

**Xong khi:** test Application PASS; test Infrastructure PASS hoặc CHƯA CHẠY.

---

## Task 10. Infrastructure: PDF biên bản

### File mới

`src/QuanLyChoThuePhongTroWeb.Infrastructure/ExternalServices/DocumentExporters/BienBanThanhLyExporter.cs`, `public class BienBanThanhLyExporter : IBienBanThanhLyExporter`.

Copy nguyên phần dựng QuestPDF của `InvoiceDocumentExporter.ExportPdf` (dòng 101 trở đi): license, font, khổ A4, lề, định dạng tiền.

Nội dung theo thứ tự:
1. Tiêu đề "BIÊN BẢN THANH LÝ HỢP ĐỒNG THUÊ PHÒNG", mã hợp đồng, ngày thanh lý.
2. Bên cho thuê: tên chi nhánh, địa chỉ, số điện thoại.
3. Bên thuê: tên, CCCD, số điện thoại; danh sách thành viên.
4. Phòng; thời hạn từ – đến; ngày trả phòng (`ThoiDiemKetThuc`, ngày VN).
5. Bảng hóa đơn: Mã | Kỳ | Tổng | Đã thu | Trừ tiền cọc (−X) | Còn lại.
6. Tổng kết: Tiền cọc; Trừ tiền cọc: −X đ; Cọc hoàn trực tiếp cho khách; **Khách còn nợ: Z đ (tính đến {ngày in})**.
7. Nếu `NoConLai > 0`: dòng "Hạn trả: ......................" để trống.
8. Ghi chú, nếu có.
9. Hai ô ký "BÊN CHO THUÊ" / "BÊN THUÊ" (ký, ghi rõ họ tên).

Mọi ngày hiển thị dùng `.AddHours(7)`.

### File sửa

`Infrastructure/DependencyInjection.cs`, cạnh dòng 63: `services.AddScoped<QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IBienBanThanhLyExporter, ExternalServices.DocumentExporters.BienBanThanhLyExporter>();`.

### Test (`Infrastructure.IntegrationTests`, file mới `DocumentExporters/BienBanThanhLyExporterTests.cs`, không cần DB)

- Có nợ, có ghi chú → bytes bắt đầu bằng `%PDF`.
- Không nợ, không ghi chú → không ném lỗi.

**Xong khi:** test PASS (riêng lớp này) hoặc CHƯA CHẠY.

---

## Task 11. Web: trang chi tiết, thanh lý, gia hạn, biên bản

### File mới

**`src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/HopDongChiTietController.cs`**
- Namespace `QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers`; `public class HopDongChiTietController : AdminBaseController`, **không** có `[ApiController]`.
- Constructor: `(IThanhLyHopDongService thanhLy, IGiaHanHopDongService giaHan, IHopDongTraCuuService traCuu, IDieuKhoanMauService dieuKhoanMau)`.

| Action | Route | Trả về |
|---|---|---|
| `ChiTiet(int id, CancellationToken ct)` | `[HttpGet("/QuanLyNhaTro/HopDong/ChiTiet/{id:int}")]` | `View("ChiTiet", dto)`; lỗi → view `Loi` với status theo `ToHttpStatusCode()` |
| `ThanhLy(int id, ct)` | `[HttpGet("/QuanLyNhaTro/HopDong/ThanhLy/{id:int}")]` | `View("ThanhLy", previewDto)` |
| `XacNhanThanhLy(int id, [FromBody] ThanhLyHopDongBody body, ct)` | `[HttpPost("/HopDong/ThanhLy/{id:int}")] [ValidateAntiForgeryToken]` | `Ok(new { success = true, message, data })` / `ToErrorResult` |
| `GiaHan(int id, ct)` | `[HttpGet("/QuanLyNhaTro/HopDong/GiaHan/{id:int}")]` | `View("GiaHan", new GiaHanHopDongViewModel(formDto, await dieuKhoanMau.GetAllAsync()))` |
| `XacNhanGiaHan(int id, [FromBody] GiaHanHopDongBody body, ct)` | `[HttpPost("/HopDong/GiaHan/{id:int}")] [ValidateAntiForgeryToken]` | `Ok(new { success = true, message, data = new { hopDongMoiId, maHopDongMoi } })` / `ToErrorResult` |
| `BienBanLanDau(int id, [FromForm] string? ghiChu, ct)` | `[HttpPost("/HopDong/BienBanThanhLy/{id:int}")] [ValidateAntiForgeryToken]` | `File(bytes, "application/pdf", $"BienBanThanhLy_{id}.pdf")` / `ToErrorResult` |
| `BienBanInLai(int id, ct)` | `[HttpGet("/HopDong/BienBanThanhLy/{id:int}")]` | như trên, `ghiChu = null` |

Ghi chú về action:
- Kiểu `IDieuKhoanMauService.GetAllAsync()` trả về thì xem ở `HopDongController.QuanLyHopDong` dòng 41.
- Body là class lồng trong controller, theo mẫu `HoaDonController` dòng 718:
  - `ThanhLyHopDongBody { decimal TongNoLucMo; bool XacNhanConNo }`
  - `GiaHanHopDongBody { int ThangBatDau; int NamBatDau; string NgayKetThucMoi /* "yyyy-MM-dd" */; decimal TienThue; decimal TienCoc; bool ChepDieuKhoanCu; List<int>? DieuKhoanMauIds }`
- Controller parse `NgayKetThucMoi` bằng `DateOnly.TryParseExact(..., "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)`; sai → `BadRequest(new { success = false, message = "Ngày kết thúc mới không hợp lệ." })`.
- `ChepDieuKhoanCu = true` → `DieuKhoanMauIds = null`.

**ViewModel và view:**
- `Areas/QuanLyNhaTro/ViewModels/GiaHanHopDongViewModel.cs`: record `(GiaHanHopDongFormDto Form, <kiểu danh sách điều khoản mẫu> DieuKhoanMau)`.
- `Areas/QuanLyNhaTro/ViewModels/HopDongLoiViewModel.cs`: `TieuDe`, `ThongDiep`, `QuayLaiUrl`. `From(ServiceResult r)`:
  - `Forbidden` → "Hợp đồng thuộc chi nhánh bạn chưa được phân công."
  - còn lại → `r.Message`
  - `QuayLaiUrl = "/QuanLyNhaTro/QuanLyHopDong"`
- `Areas/QuanLyNhaTro/Views/HopDongChiTiet/Loi.cshtml`: copy markup của `Views/Shared/_BienLaiLoi.cshtml`, icon `ti ti-file-off`.
- `Areas/QuanLyNhaTro/Views/HopDongChiTiet/ChiTiet.cshtml` (model `HopDongChiTietDto`):
  - Card thông tin, badge `TrangThaiText`.
  - Nút "Thanh lý" và "Gia hạn" (link tới 2 trang GET) chỉ hiện khi `CoTheThanhLy` / `CoTheGiaHan`.
  - Nút "In lại biên bản thanh lý" (GET PDF, `target=_blank`) khi `CoBienBanThanhLy`.
  - Bảng thành viên, danh sách điều khoản.
  - Khối "Chuỗi hợp đồng": bảng mã (link chi tiết), loại, từ – đến, trạng thái; in đậm dòng `LaHienTai`.
  - Copy bố cục card/badge Tabler từ `Views/GiuCho/ChiTiet.cshtml`.
- `Areas/QuanLyNhaTro/Views/HopDongChiTiet/ThanhLy.cshtml` (model `ThanhLyHopDongPreviewDto`):
  - `@Html.AntiForgeryToken()`.
  - Thông tin hợp đồng, bảng hóa đơn nợ (cột Cấn trừ), các ô tổng.
  - `LyDoChan` khác rỗng → `alert-danger` liệt kê lý do và ẩn nút xác nhận.
  - `ConNoSauCanTru > 0` → checkbox `#chkXacNhanConNo` với nhãn "Khách còn nợ {X} đ sau khi cấn trừ cọc"; nút `#btnXacNhanThanhLy` disabled tới khi tích.
  - Textarea `#ghiChuBienBan` (không lưu DB).
  - JS (`@section Scripts`):
    - Bấm xác nhận → SweetAlert hỏi lại → `$.ajax` POST JSON `/HopDong/ThanhLy/{id}`, header `RequestVerificationToken`, body `{ tongNoLucMo: @Model.TongNo (InvariantCulture), xacNhanConNo }`.
    - Thành công: ẩn nút, hiện form ẩn `method=post action=/HopDong/BienBanThanhLy/{id} target=_blank` gồm `@Html.AntiForgeryToken()` và `<input type=hidden name=ghiChu>` (gán từ textarea khi submit); nút "Tải biên bản thanh lý (PDF)"; link "Về chi tiết hợp đồng".
    - Lỗi: SweetAlert `xhr.responseJSON.message`.
- `Areas/QuanLyNhaTro/Views/HopDongChiTiet/GiaHan.cshtml` (model `GiaHanHopDongViewModel`):
  - Select tháng 1–12 + input năm (mặc định theo DTO).
  - Input `type=date` ngày kết thúc mới (mặc định `NgayKetThucMacDinh` dạng `yyyy-MM-dd`).
  - Giá thuê, tiền cọc. Nếu cọc khác cọc cũ thì hiện nhắc "Chênh lệch cọc thu/trả ngoài hệ thống".
  - Radio "Chép điều khoản hợp đồng cũ" (mặc định, liệt kê `DieuKhoanHienTai`) / "Chọn lại từ điều khoản mẫu" (checkbox list). Copy cách render checkbox `.chk-dieukhoan` ở `QuanLyHopDong.cshtml`.
  - Hiện `MaHopDongMoiDuKien` và thành viên sẽ chép.
  - POST JSON như trang thanh lý. Thành công → `location.href = '/QuanLyNhaTro/HopDong/ChiTiet/' + data.hopDongMoiId`.
- Escape chuỗi trong JS bằng `escapeHtml` sẵn có hoặc Razor encode. Không dùng `@Html.Raw` cho dữ liệu người dùng.

### File sửa

`Areas/QuanLyNhaTro/Views/HopDong/QuanLyHopDong.cshtml`:
1. Bộ lọc: thêm `<select id="filterBoLoc">` với các option: `""` "-- Tất cả --", `QuaHan` "Quá hạn chưa xử lý", `SapHetHan` "Sắp hết hạn (30 ngày)", `GiaHan` "Gia hạn".
   - Trong `data: function(d)` thêm `d.boLoc = $('#filterBoLoc').val();`.
   - Thêm `#filterBoLoc` vào `.on('change')` ở dòng 607.
   - Đọc `urlParams.get('boLoc')` như khối dòng 505–508.
2. Cột thao tác (dòng 575–600): render nhận `(data, type, row)`.
   - Thêm `<a href="/QuanLyNhaTro/HopDong/ChiTiet/${data}" class="dropdown-item py-2"><i class="ti ti-eye me-2"></i> Chi tiết</a>` đầu menu.
   - Chỉ render "Chỉnh sửa" khi `row.trangThaiHopDong !== 2` (Q7).
3. Select `#TrangThaiHopDong` (dòng 202–206): xóa `<option value="2">`.
4. Cảnh báo nợ cũ (spec §4.2a):
   - Khi tạo mới (`window.currentHopDongId == 0`) và `#NguoiThueId` đổi giá trị → `GET /NguoiThue/NoCu/{id}`. Nếu `tongNo > 0` thì hiện `<div class="alert alert-warning">` dưới select: "Người thuê đang nợ {tổng} ở hợp đồng đã kết thúc: {mã hóa đơn (mã hợp đồng) …}". Không chặn lưu.
   - Tương tự trong `_DanhSachThanhVienPartial.cshtml` cho `#addMember_SearchNguoiThue` khi `select2:select` (handler ở `QuanLyHopDong.cshtml` dòng 708). Thêm `<div id="addMember_NoCuWarning">` vào partial.

### Test (`Web.IntegrationTests`, file mới `ContractSettlementRenewalTests.cs`)

- `[Collection(WebTestCollection.Name)]`.
- Seed theo mẫu `TenantOnlinePaymentPortalTests.SeedAsync` (2 chi nhánh, nhân viên A/B, Admin, khách).
- Đăng nhập và lấy token: `BranchScopeKit` hoặc `LoginAsync`/`Token` của file mẫu.

1. `ThanhLy_WithoutAntiforgery_Is400`.
2. `ThanhLy_StaffOtherBranch_Is403_AndNothingChanges`: cả GET trang (403) và POST.
3. `ThanhLy_StaffOwnBranch_Succeeds`:
   - Hợp đồng có `ThoiDiemKetThuc` trong tháng 10/2026; hóa đơn T10 `DaGui` 2tr; cọc 3tr.
   - Sau POST: hợp đồng `DaKetThuc`, phòng `Trong`, 1 dòng `LichSuThanhToan` `PhuongThuc = CanTruTienCoc`.
4. `ThanhLy_TwiceClick_SecondIs400`.
5. `ThanhLy_DebtRemains_WithoutConfirm_Is400_WithConfirm_Is200`.
6. `ThanhLy_DebtChangedSinceOpen_Is400`: ghi thêm một dòng sổ trước khi POST với `TongNoLucMo` cũ.
7. `ThanhLy_MissingPublishedReturnMonthInvoice_Is400`; `ThanhLy_DraftInvoiceExists_Is400`.
8. `BienBan_FirstPrintPost_And_Reprint_ReturnPdf`: `Content-Type: application/pdf`, body bắt đầu `%PDF`. Contract chưa thanh lý → 400.
9. `GiaHan_Succeeds_CreatesGh1_AndOldEnded`; `GiaHan_StaffOtherBranch_Is403`; `GiaHan_WithoutAntiforgery_Is400`; `GiaHan_InvoiceExistsInNewMonth_Is400`.
10. `ChiTiet_ShowsChain_AndRenewedLabel`: HTML (giải mã bằng `WebUtility.HtmlDecode`) chứa `Đã gia hạn → {mã}` và cả 2 mã.
11. `EditForm_NoLongerOffersDaKetThuc`: GET `/QuanLyNhaTro/QuanLyHopDong`, HTML không chứa `<option value="2">Đã kết thúc</option>` trong `#TrangThaiHopDong`. Lọc theo id select, vì `#filterTrangThai` vẫn có option 2.
    - Thêm: `PUT /HopDong/Update/{id}` với `trangThaiHopDong = 2` → 400.
12. `TenantOfEndedContract_StillSeesAndCanPayDebt`:
    - Hợp đồng `DaKetThuc`, hóa đơn `DaGui` còn nợ.
    - Khách đăng nhập: `GET /KhachThue/HoaDon/ThanhToan/{id}` → 200, `coTheThanhToan = true`, `conLai` đúng.
    - `GET /KhachThue/HoaDon/ThanhToan/QR/{id}` → 200 `image/png`.
13. `OffsetLedger_VoidByAdmin_IsRejected`: gọi endpoint hủy ghi nhận ở `LichSuThanhToanController` (xem route trong file đó) → 400.

**Xong khi:** build PASS; test Web PASS hoặc CHƯA CHẠY.

---

## Task 12. Web: chi tiết người thuê, lịch sử thuê, nợ cũ, KH5 link

### File mới

**`src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/NguoiThueChiTietController.cs`**
- `: AdminBaseController`, không `[ApiController]`, constructor `(IHopDongTraCuuService traCuu)`.

| Action | Route | Trả về |
|---|---|---|
| `ChiTiet(int id, ct)` | `[HttpGet("/QuanLyNhaTro/NguoiThue/ChiTiet/{id:int}")]` | `View("ChiTiet", lichSuThueDto)`; lỗi → view `~/Areas/QuanLyNhaTro/Views/HopDongChiTiet/Loi.cshtml` với status theo loại lỗi (`QuayLaiUrl = "/QuanLyNhaTro/QuanLyNguoiThue"`) |
| `NoCu(int id, ct)` | `[HttpGet("/NguoiThue/NoCu/{id:int}")]` | `Ok(dto)` / `ToErrorResult` |

**`Areas/QuanLyNhaTro/Views/NguoiThueChiTiet/ChiTiet.cshtml`** (model `LichSuThueDto`):
- Đầu trang: tên, CCCD, số điện thoại.
- Card có `nav-tabs` với một tab "Lịch sử thuê".
- 5 ô tóm tắt: Số lần thuê, Số hợp đồng, Số lần ở ghép, Tổng số tháng ở, Nợ hiện tại.
- Bảng: Mã HĐ (link `/QuanLyNhaTro/HopDong/ChiTiet/{id}`), Vai trò, Loại, Phòng, Chi nhánh, Từ – Đến (ngày VN), Trạng thái, Còn nợ.

### File sửa

- `Areas/QuanLyNhaTro/Views/NguoiThue/QuanLyNguoiThue.cshtml`, nút thao tác dòng 190: thêm `<a class="btn btn-icon btn-ghost-info btn-sm" href="/QuanLyNhaTro/NguoiThue/ChiTiet/${data}" title="Chi tiết"><i class="ti ti-eye"></i></a>` trước nút sửa.
- `Areas/QuanLyNhaTro/Controllers/HoaDonController.cs`, `GetList` (dòng 169–195):
  - Thêm tham số `[FromForm] int hopDongDaKetThucConNo = 0`.
  - Gán `HopDongDaKetThucConNo = hopDongDaKetThucConNo == 1`.
- `Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml`:
  - Đọc `const urlDaKetThucConNo = urlParams.get('hopDongDaKetThucConNo') === '1';` cạnh dòng 427.
  - Trong `data: function(d)` (dòng 491–498) thêm `d.hopDongDaKetThucConNo = locDaKetThucConNo ? 1 : 0;` với biến `let locDaKetThucConNo = urlDaKetThucConNo`.
  - Khi đang lọc thì hiện badge `bg-orange-lt` "Đang lọc: hóa đơn còn nợ của hợp đồng đã kết thúc" kèm nút "Bỏ lọc": gán `false` rồi `table.ajax.reload()`.

### Test (`Web.IntegrationTests`, file mới `TenantRentalHistoryTests.cs`, `[Collection(WebTestCollection.Name)]`)

1. `LichSuThue_Admin_SeesAllRows`.
2. `LichSuThue_StaffBranchA_SeesOnlyBranchARows_SummaryCountsOnlyVisible`: người thuê ở cả 2 chi nhánh.
3. `LichSuThue_StaffCannotSeeTenant_Is404`.
4. `NoCu_ReturnsDebtOfEndedContract`: JSON có `tongNo` và mã hóa đơn; người không nợ → `tongNo = 0`.
5. `InvoiceList_FilterEndedContractDebt`: `POST /HoaDon/GetList` với `hopDongDaKetThucConNo=1` chỉ trả hóa đơn còn nợ của hợp đồng `DaKetThuc`.
6. `ContractList_SearchByMemberPhone`: `POST /HopDong/DanhSachHopDongSideAsync` với `search[value]` = SĐT thành viên → có hợp đồng.
7. `ContractList_BoLocQuaHan`.

**Xong khi:** build PASS; test Web PASS hoặc CHƯA CHẠY.

---

## Task 13. Kiểm cuối

Chạy tuần tự từ thư mục gốc. **Không** chạy hai lượt `dotnet test` song song.

```powershell
git status --short
dotnet build QuanLyChoThuePhongTroWeb.sln
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
# Lẻ từng lớp khi cần:
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~ThanhLyHopDongServiceTests"
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~GiaHanHopDongServiceTests"
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter "FullyQualifiedName~BranchScopeArchitectureTests|FullyQualifiedName~ArchitectureBoundaryTests"

# Cần DB test tên *_test hoặc *_integration_test; thiếu biến → ghi CHƯA CHẠY, không ghi PASS
$env:QLCTPT_TEST_CONNECTION_STRING = "<do người dùng cung cấp>"
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests

$p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
git diff --check
```

**Xong khi:**
- Build PASS. Domain và Application PASS, gồm `ArchitectureBoundaryTests` và `BranchScopeArchitectureTests`.
- Infrastructure và Web PASS, hoặc ghi rõ CHƯA CHẠY kèm lý do.
- `has-pending-model-changes` không có thay đổi, hoặc CHƯA CHẠY.
- `git diff --check` sạch. 3 file docs chưa commit không bị đổi.

Thử tay luồng cuối tháng trên DB phát triển (spec §9) là việc của người dùng; coder không tự chạy.
