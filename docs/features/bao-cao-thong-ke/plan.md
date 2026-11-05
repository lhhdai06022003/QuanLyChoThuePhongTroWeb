# Plan: Báo cáo thống kê

- **Spec:** [spec.md](spec.md), v1, người dùng duyệt 05/11/2026 (Q1–Q3, D1–D9 theo đề xuất). Khi plan và spec lệch nhau, **spec thắng**; ghi điểm lệch vào bàn giao.
- **Nhánh:** `feature/bao-cao-thong-ke` (từ `master` = `740d219`). Không tạo nhánh khác, không chạy trên `master`.
- **Lập:** 05/11/2026.

## 0. Ràng buộc bắt buộc

- **Không đổi schema.** Không thêm entity, cột, index, view, migration. Cuối cùng `dotnet ef migrations has-pending-model-changes` phải báo không có thay đổi.
- Không sửa `Program.cs`, `*.csproj`, `appsettings*`, `.gitignore`, fixture test (`CustomWebApplicationFactory`, `PostgreSqlFixture`), test kiến trúc **ngoại trừ** thêm `IBaoCaoService` vào danh sách service phải kiểm quyền trong `BranchScopeArchitectureTests` (được duyệt trong spec mục 9). Không thêm package NuGet hay thư viện JS.
- Ranh giới tầng: Domain không đổi; Application không dùng EF Core/ASP.NET/`IQueryable` trong hợp đồng; Web không tham chiếu Domain. Service nhận `actorId` làm tham số đầu, fail closed. Kết quả trả `ServiceResult<T>`.
- Giờ: lưu UTC, tính "ngày VN" bằng `+7h`; ranh giới đầu ngày VN = 17:00Z hôm trước. Web đổi giờ bằng `Helpers/VietnamTime.cs`.
- Không tạo script PowerShell phụ trợ. Không đọc/ghi secret. Không commit, push, merge.
- Giao diện, thông báo lỗi viết tiếng Việt. Escape mọi chuỗi dữ liệu đưa vào HTML (`$.fn.dataTable.render.text()` hoặc hàm escape); Swal dùng `text`.
- Không bấm thao tác ghi thật trên DB phát triển khi thử giao diện.

## 1. Bản đồ code cần đọc trước

| Mục đích | File |
|---|---|
| Mã quyền | `Application/Common/Security/EmployeeActionCodes.cs` (`ValidActionsSet`, `AdminOnlySet`), `EmployeeAccessScope.cs` (`AllowedBranchIds`, `CanAccessBranch`), `Features/NhanViens/Services/EmployeeAccessService.cs`; test `Application.UnitTests/EmployeeAccessTests.cs` (có liệt kê `AllValidActions`) |
| Mẫu service có lọc chi nhánh | `Features/Dashboard/Services/DashboardService.cs` (cách xử lý `scope == null`, chi nhánh ngoài phạm vi) |
| Số liệu Dashboard phải khớp | `Infrastructure/Persistence/Features/DashboardStore.cs`: `GetInvoiceMonthlyStatsAsync` (Đã thu = Σ sổ chưa xóa), `GetOverdueSummaryAsync` |
| Định nghĩa công nợ dùng chung | `Infrastructure/Persistence/Features/InvoiceDebtQueries.cs`: `DaGuiChuaThuDu`, `QuaHan(nowUtc)`, `ChuaToiHan` (`internal`, dùng lại trong Infrastructure) |
| Khách đã trả phòng còn nợ | `InvoiceViewStore.cs` khoảng dòng 75–87 (`HopDongDaKetThucConNo`): hợp đồng `DaKetThuc`, không có hợp đồng `DangHoatDong` cùng phòng và cùng người thuê |
| Cách thu | `Application/Features/OnlinePayments/PaymentNotificationFormatter.PaymentMethodLabel` (`internal`), `Features/HoaDons/Payments/InvoicePaymentPolicy` (`IsVnPayKey`, `SePayMaGiaoDichPrefix`), `PaymentNoteTags` (thẻ `[TIEN-THUA:`) |
| Chuỗi gia hạn | `Features/HopDongs/Services/HopDongCodeChain.cs` (`IsRenewal`, `RootOf`, `BelongsTo`, `TimMaKeTiep`), `HopDongTraCuuService.TinhTongSoThang` (hợp nhất khoảng ngày) |
| Bộ lọc hợp đồng quá hạn/sắp hết hạn | `HopDongStore.GetDataTableResponseAsync` (`QuaHan`, `SapHetHan`), `HopDongService.DanhSachHopDongSideAsync` (`MocDauNgayHomNayUtc`) |
| Giữ chỗ, hoàn tiền | `Features/ReservationSettlement/SettlementContracts.cs` (`SettlementSummaryDto`, `SettlementMath`), `Infrastructure/Persistence/Features/ReservationSettlement/` (cách tính `ConfirmedAmount`, `RefundedAmount`, `RefundStatus`) |
| Điện nước | Entity `DichVuDienNuocCuaPhong` (`Thang`, `Nam`, `ChiSo*Cu/Moi`, `DonGia*`, `TrangThaiGhiNhan`) |
| Xuất Excel | `Application/Abstractions/Services/IInvoiceDocumentExporter.cs`, `Infrastructure/ExternalServices/DocumentExporters/InvoiceDocumentExporter.cs` (ClosedXML), đăng ký DI trong `Infrastructure/DependencyInjection.cs` |
| Màn Lịch sử thanh toán (D5) | `LichSuThanhToanStore.cs` (lọc ngày dòng ~52–60 và ~121–129; thống kê ~131–149), `LichSuThanhToanDTOs.ThongKeThanhToanRes`, `Views/LichSuThanhToan/Index.cshtml` (ô `statTongDoanhThu`, `statDoanhThuTienMat`, `statDoanhThuChuyenKhoan`, JS ~264) |
| Web | `AdminBaseController` (`CurrentActorId`), `Helpers/ServiceResultHttpExtensions.cs`, `Services/MenuService.cs` (`ActivePrefixes`), `Views/Dashboard/Index.cshtml` (cách nạp Chart.js và vẽ biểu đồ) |
| Test helper | `Infrastructure.IntegrationTests/Persistence/DebtTestSeeder.cs` (`BranchAsync`, `TenantAsync`, `RoomAsync`, `ContractAsync`, `InvoiceAsync`), `HopDongLifecycleStoreTests.cs` (mẫu test store); `Web.IntegrationTests/BranchScopeKit` (`SeedAsync`, `LoginAsync`, `ReadAsync`), `ContractSettlementRenewalTests.cs` (mẫu test HTTP + phân quyền) |

## 2. Thiết kế chung

### 2.1 Application `Features/BaoCaos/`

```
Features/BaoCaos/
  DTOs/BaoCaoFilter.cs          // ChiNhanhId?, TuThang, TuNam, DenThang, DenNam
  DTOs/BaoCaoDoanhThuDto.cs ... // một DTO kết quả cho mỗi tab (mục 4–8)
  DTOs/BaoCaoKy.cs              // record (int Thang, int Nam) + helper so sánh, liệt kê kỳ
  Persistence/IBaoCaoStore.cs   // nhận IReadOnlyCollection<int>? allowedBranchIds, int? chiNhanhId, khoảng kỳ, nowUtc
  Services/IBaoCaoService.cs
  Services/BaoCaoService.cs
  Services/BaoCaoCalculator.cs  // static, hàm thuần: liệt kê kỳ, hợp nhất khoảng ngày, nhóm tuổi nợ, tỷ lệ
```

- `IBaoCaoService` có 5 method `GetDoanhThuAsync`, `GetCongNoAsync`, `GetCongSuatPhongAsync`, `GetHopDongAsync`, `GetDienNuocAsync` (mỗi method `(int actorId, BaoCaoFilter filter, CancellationToken ct)`), cộng `GetBoLocAsync(actorId)` trả danh sách chi nhánh được xem (dùng `IDashboardStore.GetActiveBranchesAsync` hoặc truy vấn tương đương qua `IBaoCaoStore`).
- `BaoCaoService` mỗi method:
  1. `actorId <= 0` → `Forbidden`. `scope = GetScopeAsync(actorId)`; `null` → `Forbidden`. Nhân viên có `AllowedBranchIds` rỗng → `Forbidden("Bạn chưa được phân công chi nhánh nào.")`.
  2. `filter.ChiNhanhId` có giá trị mà `!scope.CanAccessBranch(id)` → `Forbidden`. Kiểm thêm `_access.CanPerformAsync(actorId, id, EmployeeActionCodes.ReportRead)` khi chọn một chi nhánh (giữ đúng cơ chế mã quyền).
  3. Kiểm khoảng kỳ bằng `BaoCaoCalculator.KiemTraKhoang`: tháng 1–12, năm 2000–2100, từ ≤ đến, số kỳ ≤ 24 → sai thì `Fail` (Web trả 400). Tab Công nợ bỏ qua khoảng kỳ.
  4. Gọi store với `allowedBranchIds = scope.AllowedBranchIds` và `chiNhanhId`, `nowUtc = _timeProvider.GetUtcNow().UtcDateTime`.
- Quyền: thêm `public const string ReportRead = "Report.Read";` vào `EmployeeActionCodes`, thêm vào `ValidActionsSet`, **không** thêm vào `AdminOnlySet`. Cập nhật `EmployeeAccessTests` nếu test liệt kê đủ danh sách.
- Đăng ký `IBaoCaoService` trong `Application/DependencyInjection.cs`.

### 2.2 Infrastructure

- `Persistence/Features/BaoCaoStore.cs` cài `IBaoCaoStore`: `AsNoTracking`, lọc chi nhánh qua `PhongTro.ChiNhanhId` trước mọi phép đếm/tổng, không truy vấn trong vòng lặp. Khi một phép gom nhóm khó dịch sang SQL, lấy dòng đã chiếu (projection) gọn rồi gom trong bộ nhớ (cách `DashboardStore.GetInvoiceMonthlyStatsAsync` đang làm).
- `ExternalServices/DocumentExporters/BaoCaoExcelExporter.cs` cài `Application/Abstractions/Services/IBaoCaoExcelExporter` (`byte[] Export(BaoCaoDoanhThuDto ...)`, một overload cho mỗi DTO hoặc một method nhận tên tab + DTO). ClosedXML: sheet "Tổng hợp" (bộ lọc, ngày xuất giờ VN, các ô tổng) + sheet bảng chi tiết; định dạng số `#,##0`.
- Đăng ký cả hai trong `Infrastructure/DependencyInjection.cs`.

### 2.3 Web

- `Areas/QuanLyNhaTro/Controllers/BaoCaoController.cs` kế thừa `AdminBaseController`:
  - `GET /QuanLyNhaTro/BaoCao` → view `Views/BaoCao/Index.cshtml` (danh sách chi nhánh từ `GetBoLocAsync`, kỳ mặc định 12 tháng gần nhất theo giờ VN).
  - `GET /QuanLyNhaTro/BaoCao/DuLieu?tab={doanh-thu|cong-no|cong-suat|hop-dong|dien-nuoc}&chiNhanhId=&tuThang=&tuNam=&denThang=&denNam=` → JSON. `tab` lạ → 400.
  - `GET /QuanLyNhaTro/BaoCao/XuatExcel?...` (cùng tham số) → `File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCao_{tab}_{tuNam}{tuThang:00}_{denNam}{denThang:00}.xlsx")`.
  - Lỗi `ServiceResult` → `this.ToErrorResult(result)`.
- View một trang, 5 tab Tabler (`nav-tabs`), tab đang mở đồng bộ `?tab=` bằng `history.replaceState`; mỗi tab tải JSON khi mở lần đầu và khi đổi bộ lọc. JS đặt trong `@section Scripts` của view (hoặc `wwwroot/js/bao-cao.js` nếu dài). Biểu đồ Chart.js nạp giống Dashboard.
- `MenuService`: thêm `new MenuItem { Name = "Báo cáo", Icon = "ti ti-chart-bar", Url = "/QuanLyNhaTro/BaoCao" }` trong nhóm "Quản lý nhà trọ" (sau "Lịch sử thanh toán" hoặc vị trí hợp lý). Không cần `ActivePrefixes` vì mọi URL đều bắt đầu bằng `Url + "/"`.
- Dashboard (D9): thêm một link "Xem báo cáo chi tiết" tới `/QuanLyNhaTro/BaoCao?tab=doanh-thu` gần khối doanh thu. Không đổi số liệu Dashboard.

## 3. Các task (làm theo thứ tự)

Mỗi task: viết test trước khi được, chạy test tầng đó, rồi sang task sau. Ghi tiến độ vào `README.md` của module.

### T1. Khung chung, quyền, bộ lọc

- Thêm `ReportRead`, `BaoCaoFilter`, `BaoCaoKy`, `BaoCaoCalculator.KiemTraKhoang/LietKeKy`, `IBaoCaoService`/`BaoCaoService` (5 method trả rỗng tạm) và `GetBoLocAsync`, `IBaoCaoStore`/`BaoCaoStore` khung, DI, controller + view khung + menu, thêm `IBaoCaoService` vào `BranchScopeArchitectureTests`.
- Test Application (`BaoCaoServiceTests`, dùng `FakeEmployeeAccessService`, `FixedTimeProvider`, store giả): actorId 0 → Forbidden; nhân viên không chi nhánh → Forbidden; nhân viên chọn chi nhánh không được giao → Forbidden và store không được gọi; Admin không chọn → store nhận `allowedBranchIds = null`; nhân viên không chọn → store nhận đúng danh sách chi nhánh; khoảng 25 kỳ → Fail; từ > đến → Fail.
- Test `BaoCaoCalculatorTests`: `LietKeKy(T11/2025 → T2/2026)` = 4 kỳ đúng thứ tự; 24 kỳ hợp lệ, 25 kỳ không.

### T2. Tab Doanh thu (spec mục 3)

- Store `GetDoanhThuAsync`: lấy hóa đơn `!IsDeleted && TrangThaiPhatHanh == DaGui` trong khoảng kỳ (so sánh `Nam*100 + Thang`), chiếu `HoaDonId, Thang, Nam, ChiNhanhId, TenChiNhanh, TongTien` và các dòng sổ chưa xóa (`SoTienThanhToan, PhuongThucThanhToan, MaGiaoDich, GhiChu`). Gom trong bộ nhớ.
- Phân loại cách thu: tạo hàm public dùng chung, ví dụ `Features/HoaDons/Payments/PaymentChannel.cs` với `enum KenhThu { TienMat, ChuyenKhoan, SePay, VnPay, CanTruCoc }` và `static KenhThu Of(string maGiaoDich, PhuongThucThanhToan pt)`; sửa `PaymentNotificationFormatter.PaymentMethodLabel` gọi hàm này (giữ nguyên chuỗi hiển thị). Thứ tự xét giống `PaymentMethodLabel`: VNPay → SePay → Cấn trừ → Tiền mặt → Chuyển khoản.
- Đã thu của một hóa đơn: đọc kỹ `PaymentNoteTags` và nơi ghi `[TIEN-THUA:` để biết `SoTienThanhToan` của dòng có gồm tiền thừa không. Quy tắc trong spec 3.2: Đã thu hóa đơn ≤ `TongTien`, phần vượt vào "Tiền thừa". Ghi kết luận vào `review.md` (mục "Ghi chú triển khai"). **Đồng thời** bảo đảm tổng Đã thu khớp Dashboard: nếu Dashboard cộng cả tiền thừa thì báo lại trong bàn giao (không tự sửa Dashboard), ghi rõ chênh lệch.
- Tiền cọc đang giữ: Σ `HopDong.TienCocPhong` của hợp đồng `DangHoatDong && !IsDeleted` trong phạm vi chi nhánh.
- Thu khác (spec 3.5): đọc `Infrastructure/Persistence/Features/ReservationSettlement/` để dùng đúng cách tính đã nhận/đã áp/đã hoàn. Chỉ tính yêu cầu có `QuyetDinhHoanTienGiuCho` trạng thái `DaHoanTien`, hoặc quyết định có `SoTienHoanDuyet = 0` không ở `DaHuy`; kỳ = tháng VN của `NgayQuyetDinh`. Yêu cầu đã nhận tiền mà chưa có quyết định (trạng thái giữ chỗ `TuChoi`, `HetHan`, `DaHuy`) → cộng vào "Đang chờ quyết định" (không theo kỳ). Nếu phát hiện trạng thái thực tế khác giả định này, dừng và ghi vào bàn giao.
- DTO trả: danh sách kỳ (kể cả kỳ không có hóa đơn, giá trị 0), bảng theo chi nhánh khi không chọn chi nhánh, tổng, chia kênh thu, tiền thừa, tiền cọc đang giữ, thu khác, đang chờ quyết định.
- Giao diện: thẻ, biểu đồ cột chồng (Thu tiền, Cấn trừ cọc, Còn nợ), bảng kỳ; số Còn nợ của kỳ là link `/QuanLyNhaTro/QuanLyHoaDon?chiNhanhId=&thang=&nam=` (kiểm lại tên tham số trong `Views/HoaDon/Index.cshtml`). Chú thích dưới ô Tiền cọc đang giữ như spec.
- Test Infrastructure `BaoCaoStoreTests` (seed bằng `DebtTestSeeder`, chi nhánh riêng):
  - hóa đơn Nháp và hóa đơn đã hủy không vào Phát sinh;
  - hóa đơn T1 trả một phần trong T1 và phần còn lại trong T2 → cả hai vào kỳ T1;
  - dòng tiền mặt, chuyển khoản tay, `SEPAY-…`, `VNPAY-…`, `CANTRU-…` vào đúng kênh; dòng sổ đã xóa mềm không tính;
  - Đã thu (khi không có tiền thừa) bằng `DashboardStore.GetInvoiceMonthlyStatsAsync(...).DaThu` của cùng kỳ, cùng chi nhánh;
  - tiền cọc đang giữ chỉ gồm hợp đồng đang hoạt động;
  - giữ chỗ: nhận 500.000, hoàn 200.000, quyết định `DaHoanTien` ngày 10/03 VN → Thu khác T3 = 300.000; yêu cầu `HetHan` đã nhận tiền chưa có quyết định → Đang chờ quyết định; quyết định `DaHuy` → không tính. Nếu seeder chưa có hàm tạo giữ chỗ, thêm helper trong file test (không sửa fixture dùng chung).
  - chi nhánh khác không lọt vào kết quả.
- Test Application: `PaymentChannel.Of` cho 5 kênh; `PaymentNotificationFormatter.PaymentMethodLabel` vẫn trả chuỗi cũ (test hiện có phải PASS nguyên).

### T3. Tab Công nợ (spec mục 4)

- Store: hóa đơn `DaGuiChuaThuDu()` có còn nợ > 0 (cùng công thức Đã thu ở T2), chiếu `HoaDonId, MaHoaDon, Thang, Nam, HanThanhToan, TongTien, đã thu, NguoiThueId, HoVaTen, SoPhong, ChiNhanh, HopDongId, TrangThaiHopDong, PhongTroId`; cờ "đã trả phòng" dùng **đúng** điều kiện của `HopDongDaKetThucConNo` (hợp đồng `DaKetThuc`, không có hợp đồng `DangHoatDong` cùng phòng và cùng người thuê) — tách điều kiện đó thành extension dùng chung trong `InvoiceDebtQueries` nếu sửa được mà không đổi hành vi màn hóa đơn.
- Nhóm tuổi nợ (`BaoCaoCalculator.NhomTuoiNo(DateTime? hanThanhToanUtc, DateTime nowUtc)`): hóa đơn **không** thỏa `QuaHan(nowUtc)` (hạn null hoặc hạn ≥ now) → "Chưa đến hạn" hoặc "Không có hạn"; hóa đơn quá hạn: `soNgay = max(1, ngàyVN(now) − ngàyVN(han))`, nhóm 1–30, 31–60, 61–90, >90. Như vậy tổng "Quá hạn" khớp `DashboardStore.GetOverdueSummaryAsync`.
- DTO: thẻ (Tổng còn nợ, Quá hạn, Số khách nợ, Nợ của khách đã trả phòng), nhóm tuổi nợ, bảng theo người đại diện (gộp theo `NguoiThueId`; phòng/chi nhánh lấy theo hóa đơn nợ mới nhất; kỳ nợ cũ nhất; số ngày quá hạn lớn nhất; cờ đã trả phòng nếu mọi hóa đơn nợ của người đó thuộc hợp đồng đã trả phòng — ghi rõ quy tắc trong DTO comment).
- Giao diện: link tên → `/QuanLyNhaTro/NguoiThue/ChiTiet/{id}`; nút "Hóa đơn đã trả phòng còn nợ" → `/QuanLyNhaTro/QuanLyHoaDon?hopDongDaKetThucConNo=1` (kèm `chiNhanhId` nếu đang lọc).
- Test: `BaoCaoCalculatorTests` cho ranh giới (hạn = now−1 giây → 1 ngày; 30/31/60/61/90/91 ngày; hạn null; hạn tương lai); `BaoCaoStoreTests`: tổng Quá hạn = `GetOverdueSummaryAsync.TongConNo`; khách đã trả phòng có gia hạn không bị gắn cờ.

### T4. Tab Công suất phòng (spec mục 5)

- Store trả dữ liệu thô: phòng `!IsDeleted` (Id, ChiNhanhId, NgayTao, TrangThai) và khoảng hợp đồng không `DaHuy`, `!IsDeleted` của các phòng đó (PhongTroId, BatDau, KetThuc, TrangThai, MaHopDong) giao với khoảng kỳ. Không tính trong SQL.
- `BaoCaoCalculator`: `HopNhatKhoang` (tách/dùng lại logic của `HopDongTraCuuService.TinhTongSoThang`; nếu tách thì sửa `TinhTongSoThang` gọi hàm chung và giữ nguyên test `History_MonthCount_IncludesLastDay`), `NgayPhongCoTheThue(phong, ky, homNayVn)`, `NgayPhongDaThue(khoangDaHopNhat, ky, homNayVn)`. Hợp đồng `DangHoatDong` không có ngày kết thúc → tới hôm nay. Tháng sau hôm nay → 0/0 (tỷ lệ `null`).
- Thẻ hiện tại lấy như Dashboard (`GetRoomStatusCountsAsync` cùng điều kiện). Phòng trống lâu nhất: phòng `TrangThai = Trong`, số ngày từ ngày VN kết thúc hợp đồng gần nhất (hoặc ngày tạo) tới hôm nay; trả top 5.
- Giao diện: biểu đồ đường tỷ lệ lấp đầy; bảng kỳ (ngày-phòng có thể thuê, đã thuê, tỷ lệ); dòng chú thích hạn chế "Phòng đã xóa không được tính; trạng thái Bảo trì chỉ có số hiện tại."
- Test calculator: hai hợp đồng nối tiếp (31/12 và 01/01) không đếm trùng và không hở ngày; hợp đồng chồng nhau; phòng tạo ngày 15; tháng hiện tại cắt ở hôm nay; tháng tương lai. Test store: hợp đồng hủy không tính; chi nhánh khác không lọt vào.

### T5. Tab Hợp đồng (spec mục 6)

- Store trả hợp đồng `!IsDeleted` của phạm vi (Id, MaHopDong, PhongTroId, BatDau, KetThuc, TrangThai). Tính trong Application bằng `HopDongCodeChain` (`IsRenewal`, `RootOf`, `BelongsTo`, `TimMaKeTiep` trên các mã cùng phòng chưa hủy — giống `HopDongStore` đã làm cho cột "Đã gia hạn").
- Chỉ số theo kỳ như bảng spec mục 6; tỷ lệ gia hạn `null` khi mẫu = 0. Thẻ hiện tại: Đang hoạt động; Quá hạn và Sắp hết hạn tính cùng mốc `MocDauNgayHomNayUtc` và cùng điều kiện của `HopDongStore` (`QuaHan`: kết thúc < đầu ngày hôm nay; `SapHetHan`: trong 31 ngày từ đầu ngày hôm nay).
- Giao diện: biểu đồ cột nhóm; bảng kỳ; thẻ Quá hạn/Sắp hết hạn link tới `/QuanLyNhaTro/QuanLyHopDong?boLoc=QuaHan|SapHetHan`.
- Test: mới / gia hạn `-GH1` / trả phòng (không có kế tiếp) / kết thúc có kế tiếp (không tính trả phòng, tính vào tỷ lệ gia hạn) / hợp đồng hủy bị loại; ranh giới tháng theo giờ VN (bắt đầu 01/03 00:00 VN = 28/02 17:00Z thuộc T3).

### T6. Tab Điện nước (spec mục 7)

- Store: `DichVuDienNuocCuaPhong` `!IsDeleted && TrangThaiGhiNhan == DaDuyet` trong khoảng kỳ, phạm vi chi nhánh; chiếu phòng, chi nhánh, kỳ, chỉ số, đơn giá.
- Application: bản ghi có `Moi < Cu` ở điện hoặc nước → loại cả bản ghi, đếm "Bản ghi bỏ qua". Trung bình kWh/phòng/tháng = tổng kWh / số cặp (phòng, kỳ) có bản ghi hợp lệ. Top 10 phòng theo kWh.
- Giao diện: thẻ, biểu đồ đường 2 trục (kWh, m³), bảng kỳ, bảng top 10.
- Test: chỉ `DaDuyet`; bản ghi hiệu âm bị bỏ và được đếm; tiền = tiêu thụ × đơn giá của từng bản ghi (đơn giá khác nhau giữa các kỳ).

### T7. Xuất Excel (D8)

- `IBaoCaoExcelExporter` + `BaoCaoExcelExporter`; controller `XuatExcel` gọi cùng method service với `DuLieu`.
- Test Web: tải file, `new XLWorkbook(stream)` đọc được, có sheet "Tổng hợp", một ô tổng bằng số trong JSON `DuLieu` cùng tham số; header `Content-Type` đúng; tên file đúng mẫu.

### T8. Sửa màn Lịch sử thanh toán (D5)

- `ThongKeThanhToanRes` thêm `DoanhThuCanTruCoc`; store tính `PhuongThucThanhToan == CanTruTienCoc`. View thêm ô "Cấn trừ cọc" (cùng kiểu ô hiện có) và JS gán giá trị. Bảo đảm `TongDoanhThu = TienMat + ChuyenKhoan + CanTruCoc` (giá trị enum lạ, nếu có, phải được xét: ghi vào bàn giao nếu thấy).
- Lọc ngày (cả danh sách ~dòng 52–60 và thống kê ~121–129): `tuNgay` là ngày VN → `utcTu = DateTime.SpecifyKind(tuNgay.Date, Utc).AddHours(-7)`; `denNgay` → `utcDenExclusive = DateTime.SpecifyKind(denNgay.Date.AddDays(1), Utc).AddHours(-7)` và so sánh `< utcDenExclusive`. Kiểm controller/JS gửi ngày dạng `yyyy-MM-dd` (không kèm giờ) trước khi sửa.
- Test Infrastructure: giao dịch lúc 16:59:59Z ngày 09/03 (= 23:59:59 VN 09/03) thuộc ngày 09/03, lúc 17:00:00Z ngày 09/03 (= 00:00 VN 10/03) thuộc ngày 10/03; tổng = tổng các phần khi có dòng cấn trừ. Tìm test hiện có của `GetThongKeThanhToanAsync`/lọc ngày và sửa kỳ vọng nếu chúng đang khóa hành vi UTC cũ (ghi rõ trong bàn giao).

### T9. Hoàn thiện

- Link Dashboard (D9). Test Web: menu "Báo cáo" active ở `/QuanLyNhaTro/BaoCao`; chưa đăng nhập → chuyển về `/QuanLyNhaTro/DangNhap`; nhân viên được giao → 200; `chiNhanhId` không được giao → 403; khoảng 25 kỳ → 400; `tab` lạ → 400.
- Cập nhật tài liệu (mục 5).

## 4. Kiểm chứng

Chạy tuần tự, không song song. Dừng preview trước khi build/test.

1. `dotnet build QuanLyChoThuePhongTroWeb.sln`
2. `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests`
3. `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests`
4. Integration (skill `chay-test-tich-hop`; chuỗi kết nối DB test lấy từ biến môi trường User `QLCTPT_TEST_CONNECTION_STRING`, DB phải kết thúc `_test`): `Infrastructure.IntegrationTests` rồi `Web.IntegrationTests`. Thiếu biến → ghi CHƯA CHẠY.
5. `dotnet ef migrations has-pending-model-changes` (lệnh trong `AGENTS.md`) → không thay đổi.
6. `git diff --check`.
7. Preview `web` (`.claude/launch.json`, cổng 5009, DB phát triển, Admin đã đăng nhập; navigate đầu tiên có thể lỗi, đợi ~5 giây). Chỉ xem: mở 5 tab với bộ lọc mặc định và một chi nhánh; đối chiếu "Đã thu" tháng hiện tại với Dashboard cùng chi nhánh; bấm Xuất Excel từng tab (chỉ tải file, mở bằng ClosedXML hoặc xem kích thước); kiểm console không lỗi; bề rộng ~800px không tràn ngang. Chụp ảnh (viewport 1440×900, `computer` action `zoom` toàn khung ra PNG, đợi biểu đồ vẽ xong) và chép vào `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo` tên `bao-cao-01-doanh-thu.png`, `bao-cao-02-cong-no.png`, `bao-cao-03-cong-suat.png`, `bao-cao-04-hop-dong.png`, `bao-cao-05-dien-nuoc.png`, `bao-cao-06-lich-su-thanh-toan.png`.

## 5. Tài liệu và bàn giao

- `docs/features/bao-cao-thong-ke/README.md`: tick hạng mục, cập nhật hiện trạng.
- `docs/features/bao-cao-thong-ke/review.md` (mới): kết quả từng lệnh (PASS/FAIL/CHƯA CHẠY), mục "Ghi chú triển khai" (quy tắc tiền thừa, trạng thái giữ chỗ thực tế, điểm lệch plan), việc chưa thử thật.
- `docs/features/README.md`: cập nhật dòng module.
- `docs/architecture/README.md`: nếu có bảng feature/route thì thêm Báo cáo.
- Báo cáo ngày `.agents/bao-cao-hang-ngay/<ngày>/bao-cao.md` theo mẫu `.agents/bao-cao-hang-ngay/README.md`.
- Bàn giao cho người dùng: file đã sửa theo task, lệnh đã chạy và kết quả, ảnh đã chụp, điểm lệch plan/spec kèm lý do, việc còn lại.
