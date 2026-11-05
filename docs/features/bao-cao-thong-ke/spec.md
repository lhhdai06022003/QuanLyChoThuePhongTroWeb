# Spec: Báo cáo thống kê (cổng quản lý)

- **Trạng thái:** v1, người dùng đã duyệt (05/11/2026): chốt Q1–Q3 (mục 0) và D1–D9 theo đúng cột Đề xuất (mục 11).
- **Nhánh:** `feature/bao-cao-thong-ke` (từ `master` = `740d219`).
- **Phạm vi:** chỉ cổng quản lý (`Areas/QuanLyNhaTro`) và tầng Application/Infrastructure tương ứng. Không đụng cổng khách thuê, cổng khách vãng lai.
- **Schema:** không đổi. Không thêm bảng, cột, index, view hay migration. Mọi số liệu tính từ bảng đang có lúc người dùng mở báo cáo.

## 0. Quyết định đã chốt (05/11/2026)

| # | Câu hỏi | Người dùng chốt |
|---|---|---|
| Q1 | Doanh thu tính theo ngày thu tiền hay kỳ hóa đơn | **Theo kỳ của hóa đơn** (`Thang`, `Nam`) |
| Q2 | Tiền cọc và cấn trừ cọc có tính vào doanh thu không | **Phương án A**: tiền cọc là tiền giữ hộ khách, không phải doanh thu; chỉ thành doanh thu khi cấn trừ vào hóa đơn lúc thanh lý. "Đã thu" tách hai cột **Thu tiền** và **Cấn trừ cọc**. Có thêm ô **Tiền cọc đang giữ**. Tiền giữ chỗ khách bỏ không được hoàn báo riêng ở mục **Thu khác**, không trộn vào doanh thu tiền phòng |
| Q3 | Nhân viên có xem được báo cáo không | **Có**, chỉ số liệu của chi nhánh đang được phân công. Admin xem mọi chi nhánh |

## 1. Bối cảnh trong code hiện tại

| Điểm | Hiện trạng | Ảnh hưởng |
|---|---|---|
| Dashboard (`DashboardService`, `DashboardStore.GetInvoiceMonthlyStatsAsync`) | "Doanh thu tháng" = tổng sổ `LichSuThanhToan` chưa xóa của các hóa đơn có `(Thang, Nam)` là kỳ đang chọn; biểu đồ 12 tháng cùng cách tính; đã lọc theo chi nhánh được giao | Cách tính theo kỳ đã có, đã gồm cấn trừ cọc. Báo cáo phải ra cùng số "Đã thu" với Dashboard |
| Tiền cọc hợp đồng | Chỉ là cột `HopDong.TienCocPhong`; ký hợp đồng không ghi phiếu thu | Không có số liệu "đã nhận cọc ngày nào"; chỉ biết số cọc đang giữ của hợp đồng |
| Tiền giữ chỗ | `GiaoDichGiuCho.SoTienThucNhan` (đã nhận), `ApDungTienGiuChoVaoTienCoc.SoTienApDung` (chuyển vào cọc hợp đồng), `QuyetDinhHoanTienGiuCho` + `GiaoDichHoanTienGiuCho.SoTienHoan` (hoàn); không vào sổ `LichSuThanhToan` | Phần giữ lại không hoàn chưa được tính ở đâu |
| Cấn trừ cọc lúc thanh lý | Dòng `LichSuThanhToan` có `PhuongThucThanhToan = CanTruTienCoc`, mã `CANTRU-HD{id}-{HoaDonId}` | Được tính là "đã thu" của hóa đơn được trừ; không bị đếm hai lần vì tiền cọc chưa từng được ghi nhận lúc nhận |
| Phân biệt cách thu | `PhuongThucThanhToan` (`TienMat`, `ChuyenKhoan`, `CanTruTienCoc`) + tiền tố `MaGiaoDich` (`SEPAY-`, `VNPAY-`; xem `PaymentNotificationFormatter.PaymentMethodLabel`, `InvoicePaymentPolicy`) | Đủ để tách Tiền mặt / Chuyển khoản tay / SePay / VNPay / Cấn trừ |
| Màn Lịch sử thanh toán (`LichSuThanhToanStore`, khoảng dòng 110–150) | "Tổng doanh thu" cộng cả dòng cấn trừ nhưng phần chia chỉ có Tiền mặt + Chuyển khoản; lọc ngày lấy `tuNgay.Date` làm nửa đêm **UTC** | Tổng ≠ tổng hai phần; mỗi đầu khoảng lệch 7 giờ. Xem mục 9 |
| Quyền nhân viên | `EmployeeActionCodes` là hằng số trong code; `EmployeeAccessService.CanPerformAsync` cho nhân viên làm mọi mã không thuộc `IsAdminOnly` trên chi nhánh được phân công; `GetScopeAsync` trả `AllowedBranchIds` | Thêm mã quyền không cần đổi schema |
| Xuất Excel | ClosedXML có sẵn ở Infrastructure (`InvoiceDocumentExporter`); interface xuất file đặt ở `Application/Abstractions/Services` | Dùng lại cùng cách |
| Biểu đồ | Chart.js đã dùng ở Dashboard | Dùng lại, không thêm thư viện |

## 2. Màn hình chung

- Mục menu mới **"Báo cáo"** (icon `ti ti-chart-bar`) trong nhóm "Quản lý nhà trọ" của `MenuService`, URL `/QuanLyNhaTro/BaoCao`, `ActivePrefixes = ["/QuanLyNhaTro/BaoCao"]`. Hiện cho Admin và nhân viên.
- Một trang, 5 tab: **Doanh thu**, **Công nợ**, **Công suất phòng**, **Hợp đồng**, **Điện nước**. Tab đang mở ghi trên URL (`?tab=doanh-thu`) để tải lại và gửi link không mất chỗ.
- Bộ lọc dùng chung đầu trang:
  - **Chi nhánh**: Admin có "Tất cả" và từng chi nhánh; nhân viên chỉ thấy chi nhánh được phân công (có "Tất cả chi nhánh của tôi" nếu được giao từ 2 chi nhánh).
  - **Từ kỳ – đến kỳ** (tháng/năm). Mặc định 12 tháng gần nhất tính tới tháng hiện tại (giờ VN). Tối đa 24 kỳ; vượt thì báo lỗi, không truy vấn.
  - Tab Công nợ chỉ dùng chi nhánh (số liệu tính tới hôm nay), bỏ qua khoảng kỳ.
- Mỗi tab có: hàng thẻ tổng, một biểu đồ, bảng chi tiết, nút **Xuất Excel** (xuất đúng tab và bộ lọc đang chọn).
- Tiền hiển thị dạng `1.234.567 ₫`, kỳ dạng `T3/2026`, ngày `dd/MM/yyyy` giờ VN.
- Số liệu trên trang và trong file Excel phải bằng nhau (cùng một service).

## 3. Báo cáo Doanh thu (theo kỳ hóa đơn)

### 3.1 Hóa đơn được tính

- Hóa đơn `!IsDeleted`, `TrangThaiPhatHanh = DaGui` (đã công bố cho khách), `(Nam, Thang)` trong khoảng kỳ, chi nhánh = chi nhánh của phòng của hợp đồng.
- Hóa đơn Nháp, Chờ duyệt, Đã chốt chưa công bố **không** tính (chưa thành khoản phải thu). Hóa đơn đã hủy (`DaHuy`, `IsDeleted`) không tính.

### 3.2 Chỉ số mỗi kỳ (và tổng của khoảng)

| Chỉ số | Cách tính |
|---|---|
| **Phát sinh** | Σ `TongTien` của hóa đơn mục 3.1 |
| **Thu tiền** | Σ dòng `LichSuThanhToan` chưa xóa của các hóa đơn đó, `PhuongThucThanhToan ≠ CanTruTienCoc`, tính tới lúc xem (khách trả trễ sang tháng sau vẫn cộng vào kỳ của hóa đơn) |
| **Cấn trừ cọc** | Σ dòng `PhuongThucThanhToan = CanTruTienCoc` chưa xóa của các hóa đơn đó |
| **Đã thu** | Thu tiền + Cấn trừ cọc. Phải bằng "Doanh thu tháng" của Dashboard cùng kỳ, cùng chi nhánh |
| **Còn nợ** | Σ theo từng hóa đơn `max(0, TongTien − đã thu của hóa đơn)` |
| **Tỷ lệ thu** | Đã thu / Phát sinh (hiện `—` khi Phát sinh = 0) |

- Phần thu vượt `TongTien` của một hóa đơn (nếu sổ có ghi): không cộng vào Đã thu; hiện riêng dòng "Tiền thừa" ở bảng tổng. Cách nhận biết tiền thừa phải theo đúng quy tắc đang dùng cho biên lai (`PaymentNoteTags`, `LuyKeDaThanhToan`); plan phải đọc code và ghi rõ.

### 3.3 Chia theo cách thu (cho phần Thu tiền)

Tiền mặt · Chuyển khoản (nhân viên ghi tay) · SePay (`SEPAY-…`) · VNPay (`VNPAY-…`). Quy tắc nhận diện dùng chung một hàm với `PaymentMethodLabel` (tách ra hàm public nếu cần), không viết lại chuỗi tiền tố ở chỗ khác.

### 3.4 Ô tổng phụ

- **Tiền cọc đang giữ** (số tại thời điểm xem, không theo kỳ): Σ `TienCocPhong` của hợp đồng `DangHoatDong` thuộc chi nhánh đang lọc. Chú thích: "Tiền giữ hộ khách, có thể phải trả lại; không phải doanh thu."
- **Thu khác: tiền giữ chỗ không hoàn** (theo kỳ): xem mục 3.5.

### 3.5 Tiền giữ chỗ không hoàn

- Tính cho yêu cầu giữ chỗ đã có **quyết định hoàn tiền** (`QuyetDinhHoanTienGiuCho`) ở trạng thái `DaHoanTien` (hoặc quyết định hoàn 0 đồng), không phải `DaHuy`.
- Số tiền = Σ `GiaoDichGiuCho.SoTienThucNhan` − Σ `ApDungTienGiuChoVaoTienCoc.SoTienApDung` − Σ `GiaoDichHoanTienGiuCho.SoTienHoan` của yêu cầu đó; chỉ cộng khi > 0.
- Kỳ ghi nhận = tháng/năm (giờ VN) của `NgayQuyetDinh`. Chi nhánh = chi nhánh của phòng được giữ.
- Yêu cầu chưa có quyết định (kể cả lượt gắn `LATE_REFUND_REQUIRED`) không tính; hiện riêng con số "Tiền giữ chỗ đang chờ quyết định" để người xem biết.
- Plan phải đối chiếu với `ReservationSettlementService` để chắc công thức khớp màn đối soát giữ chỗ.

### 3.6 Giao diện

Thẻ: Phát sinh, Đã thu (ghi chú nhỏ "trong đó cấn trừ cọc …"), Còn nợ, Tỷ lệ thu, Tiền cọc đang giữ, Thu khác. Biểu đồ cột chồng theo kỳ (Thu tiền, Cấn trừ cọc, Còn nợ). Bảng: mỗi kỳ một dòng (khi chọn "Tất cả" thì thêm bảng theo chi nhánh), cột như mục 3.2 và 3.3. Bấm số Còn nợ của một kỳ mở màn hóa đơn đã lọc kỳ và chi nhánh đó.

## 4. Báo cáo Công nợ (tính tới hôm nay)

- Hóa đơn `!IsDeleted`, `DaGui`, còn nợ > 0 (cùng công thức mục 3.2). Khớp số "Quá hạn" của Dashboard.
- Nhóm tuổi nợ theo ngày VN của `HanThanhToan` so với hôm nay: **Chưa đến hạn**, **1–30 ngày**, **31–60**, **61–90**, **trên 90**, **Không có hạn** (`HanThanhToan` null).
- Thẻ: Tổng còn nợ, Quá hạn, Số khách nợ, Nợ của khách đã trả phòng (hợp đồng `DaKetThuc` không có hợp đồng gia hạn kế tiếp; cùng quy tắc bộ lọc `hopDongDaKetThucConNo` của màn hóa đơn).
- Biểu đồ cột theo nhóm tuổi nợ.
- Bảng theo người đại diện hợp đồng: tên, phòng, chi nhánh, số hóa đơn nợ, tổng nợ, kỳ nợ cũ nhất, số ngày quá hạn lớn nhất, nhãn "Đã trả phòng". Bấm tên mở `/QuanLyNhaTro/NguoiThue/ChiTiet/{id}`; bấm số hóa đơn mở màn hóa đơn đã lọc.

## 5. Báo cáo Công suất phòng (theo kỳ)

- Phòng được tính trong tháng: `!IsDeleted`, ngày VN của `NgayTao` ≤ ngày cuối tháng. (Phòng đã xóa mềm không có ngày xóa nên bị loại khỏi mọi kỳ; ghi rõ hạn chế này trên màn.)
- **Ngày-phòng có thể cho thuê** = Σ số ngày từ max(ngày tạo phòng, ngày 1) tới ngày cuối tháng (cắt ở hôm nay nếu là tháng hiện tại).
- **Ngày-phòng đã thuê** = với mỗi phòng, hợp nhất các khoảng `[ThoiDiemBatDau, ThoiDiemKetThuc]` (ngày VN, tính cả hai đầu) của hợp đồng không `DaHuy`, `!IsDeleted`; hợp đồng `DangHoatDong` không có ngày kết thúc thì tính tới hôm nay; cắt theo tháng và cắt ở hôm nay. Cùng cách hợp nhất khoảng đã dùng ở `HopDongTraCuuService.TinhTongSoThang`.
- **Tỷ lệ lấp đầy** = đã thuê / có thể cho thuê.
- Trạng thái Bảo trì không có lịch sử nên không tách riêng theo kỳ; chỉ hiện ở thẻ "hiện tại".
- Thẻ hiện tại (giống Dashboard): Tổng phòng, Đang thuê, Trống, Bảo trì. Thêm **Phòng trống lâu nhất**: phòng đang Trống, số ngày kể từ ngày kết thúc hợp đồng gần nhất (chưa từng có hợp đồng thì tính từ ngày tạo phòng).
- Biểu đồ đường tỷ lệ lấp đầy theo kỳ. Bảng theo kỳ (và theo chi nhánh khi chọn "Tất cả").

## 6. Báo cáo Hợp đồng (theo kỳ)

Mỗi kỳ (tháng VN):

| Chỉ số | Cách tính |
|---|---|
| Hợp đồng mới | Mã không phải gia hạn (`HopDongCodeChain.IsRenewal = false`), `ThoiDiemBatDau` trong tháng, không `DaHuy`, `!IsDeleted` |
| Gia hạn | Mã gia hạn (`-GH{n}`), `ThoiDiemBatDau` trong tháng, không `DaHuy` |
| Trả phòng (thanh lý) | `DaKetThuc`, `ThoiDiemKetThuc` trong tháng, **không** có hợp đồng kế tiếp (`HopDongCodeChain.TimMaKeTiep`) |
| Hết hạn trong kỳ | Hợp đồng không `DaHuy` có `ThoiDiemKetThuc` trong tháng |
| Tỷ lệ gia hạn | Trong số "Hết hạn trong kỳ" đã kết thúc: số có hợp đồng kế tiếp / tổng |

- Thẻ hiện tại: Đang hoạt động, Quá hạn chưa xử lý (cùng quy tắc bộ lọc `QuaHan`), Sắp hết hạn trong 30 ngày (cùng quy tắc `SapHetHan`).
- Biểu đồ cột nhóm theo kỳ (Mới, Gia hạn, Trả phòng). Bảng theo kỳ; bấm số mở danh sách hợp đồng đã lọc nếu bộ lọc hiện có đáp ứng được, không thì chỉ hiện số.

## 7. Báo cáo Điện nước (theo kỳ)

- Bản ghi `DichVuDienNuocCuaPhong` `!IsDeleted`, `TrangThaiGhiNhan = DaDuyet`, `(Nam, Thang)` trong khoảng, chi nhánh của phòng.
- Tiêu thụ điện = Σ (`ChiSoDienMoi − ChiSoDienCu`) kWh; nước = Σ (`ChiSoNuocMoi − ChiSoNuocCu`) m³; tiền điện, tiền nước = tiêu thụ × đơn giá của bản ghi. Bản ghi có hiệu âm (dữ liệu lệch) bị loại và đếm vào "Bản ghi bỏ qua" để người xem biết.
- Thẻ: Tổng kWh, Tổng m³, Tiền điện, Tiền nước, Trung bình kWh/phòng/tháng.
- Biểu đồ đường kWh và m³ theo kỳ. Bảng 1 theo kỳ; bảng 2 "Phòng dùng nhiều nhất" (10 phòng có tổng kWh cao nhất trong khoảng).
- Không tính lại mức cảnh báo `MeterAnomalyPolicy` trong báo cáo (để ngoài phạm vi, mục 8).

## 8. Ngoài phạm vi

- Thêm bảng tổng hợp sẵn, view, job tính trước, cache.
- Báo cáo cho cổng khách thuê/khách vãng lai; gửi báo cáo định kỳ qua email/Telegram.
- Xuất PDF; in trực tiếp.
- Sửa Dashboard (chỉ thêm link "Xem báo cáo chi tiết" nếu người dùng đồng ý ở mục 11).
- Sổ thu/chi tiền cọc lúc ký hợp đồng, sổ hoàn cọc khi thanh lý (hệ thống chưa ghi các khoản này).
- Lịch sử trạng thái Bảo trì của phòng; ngày xóa phòng.
- Mức cảnh báo điện nước theo kỳ; trợ lý AI dùng số liệu báo cáo.

## 9. Kiến trúc và phân quyền

- **Quyền:** thêm `EmployeeActionCodes.ReportRead = "Report.Read"`, không thuộc `IsAdminOnly`, khai báo trong `IsValid`. Service lấy `GetScopeAsync(actorId)`; chi nhánh lọc phải thuộc `AllowedBranchIds` (Admin: `null`, không giới hạn); chọn chi nhánh ngoài phạm vi → `Forbidden`. Fail closed khi `actorId <= 0` hoặc nhân viên không được giao chi nhánh nào.
- **Application** `Features/BaoCaos/`: `DTOs`, `Services/IBaoCaoService` + `BaoCaoService` (mỗi tab một method, nhận `actorId` đầu tiên và `BaoCaoFilter { ChiNhanhId?, TuThang, TuNam, DenThang, DenNam }`), `Persistence/IBaoCaoStore`. Hàm tính thuần (hợp nhất khoảng ngày, nhóm tuổi nợ, tỷ lệ) đặt ở lớp static riêng để unit test. Trả `ServiceResult<T>`.
- **Infrastructure:** `Persistence/Features/BaoCaoStore.cs` (truy vấn `AsNoTracking`, gom nhóm trong DB khi dịch được, không N+1); `ExternalServices/DocumentExporters/BaoCaoExcelExporter.cs` (ClosedXML) qua `Application/Abstractions/Services/IBaoCaoExcelExporter`.
- **Web:** `Areas/QuanLyNhaTro/Controllers/BaoCaoController.cs` kế thừa `AdminBaseController`: `GET /QuanLyNhaTro/BaoCao` (trang), `GET /QuanLyNhaTro/BaoCao/DuLieu?tab=&...` (JSON cho tab), `GET /QuanLyNhaTro/BaoCao/XuatExcel?tab=&...` (file `BaoCao_{tab}_{tuKy}_{denKy}.xlsx`). Đổi giờ bằng `VietnamTime`; lỗi bằng `this.ToErrorResult`.
- Đăng ký DI ở `AddApplication`/`AddInfrastructure`. Không sửa `Program.cs`.
- Thêm `IBaoCaoService` vào danh sách service phải kiểm quyền của `BranchScopeArchitectureTests`.
- **Sửa kèm màn Lịch sử thanh toán (nếu người dùng đồng ý, mục 11):** thêm ô "Cấn trừ cọc" vào thống kê để Tổng = Tiền mặt + Chuyển khoản + Cấn trừ; đổi lọc `tuNgay/denNgay` sang ranh giới ngày giờ VN (00:00 VN = 17:00Z hôm trước).

## 10. Kiểm thử dự kiến

- **Application.UnitTests:** hàm tính thuần (hợp nhất khoảng, cắt hôm nay, nhóm tuổi nợ ở các ranh giới 0/1/30/31/60/61/90/91 ngày, tỷ lệ khi mẫu = 0, kỳ vượt 24 tháng); `BaoCaoService` với store giả: nhân viên chọn chi nhánh không được giao → Forbidden, nhân viên không có chi nhánh → Forbidden, Admin "Tất cả" → `AllowedBranchIds = null`.
- **Infrastructure.IntegrationTests (`BaoCaoStoreTests`):** seed riêng theo hậu tố Guid như `DebtTestSeeder`:
  - Doanh thu: hóa đơn Nháp/hủy không tính; trả trễ tháng sau vẫn vào kỳ hóa đơn; tách Thu tiền / Cấn trừ / SePay / VNPay; dòng sổ đã xóa mềm không tính; Đã thu bằng số Dashboard cùng dữ liệu.
  - Tiền giữ chỗ không hoàn: nhận 500k, áp 0, hoàn 200k → 300k ở kỳ quyết định; chưa có quyết định → chỉ ở "đang chờ".
  - Công nợ: nhóm tuổi nợ, khách đã trả phòng có/không có gia hạn.
  - Công suất: hai hợp đồng nối tiếp không đếm trùng; hợp đồng hủy không tính; phòng tạo giữa tháng.
  - Hợp đồng: mới/gia hạn/trả phòng, tỷ lệ gia hạn.
  - Điện nước: chỉ `DaDuyet`, bản ghi hiệu âm bị bỏ và được đếm.
  - Lọc chi nhánh: số liệu chi nhánh khác không lọt vào.
- **Web.IntegrationTests:** chưa đăng nhập → về trang đăng nhập; nhân viên xem chi nhánh được giao → 200; chi nhánh khác → 403; khoảng > 24 kỳ → 400; Xuất Excel trả `.xlsx` mở được (đọc lại bằng ClosedXML, kiểm tiêu đề sheet và một ô tổng); menu "Báo cáo" active.
- Chạy toàn bộ test tuần tự, `has-pending-model-changes` không có thay đổi. Thử giao diện trên preview cổng 5009 (chỉ xem), chụp ảnh vào `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo`.

## 11. Điểm cần người dùng duyệt

| # | Điểm | Đề xuất (người dùng duyệt 05/11/2026) |
|---|---|---|
| D1 | Phạm vi v1 gồm 5 tab (Doanh thu, Công nợ, Công suất phòng, Hợp đồng, Điện nước) | Làm đủ 5 tab; nếu muốn gọn thì để Điện nước sang đợt sau |
| D2 | Chỉ tính hóa đơn đã công bố (`DaGui`) vào Phát sinh | Đồng ý (khớp màn công nợ hiện tại) |
| D3 | "Đã thu" của một kỳ gồm cả tiền khách trả trễ ở tháng sau | Đồng ý (đúng nghĩa "theo kỳ hóa đơn") |
| D4 | Tiền giữ chỗ không hoàn ghi nhận theo tháng của quyết định hoàn tiền; chưa quyết định thì chỉ hiện "đang chờ" | Đồng ý |
| D5 | Sửa kèm hai lỗi màn Lịch sử thanh toán (thiếu ô Cấn trừ cọc, lọc ngày theo UTC) | Sửa trong module này, vì báo cáo dùng chung cách tách cách thu |
| D6 | Mục menu "Báo cáo" đặt trong nhóm "Quản lý nhà trọ" | Đồng ý |
| D7 | Khoảng tối đa 24 kỳ, mặc định 12 tháng gần nhất | Đồng ý |
| D8 | Xuất Excel theo từng tab (một file một tab) | Đồng ý; xuất gộp nhiều tab để đợt sau |
| D9 | Thêm link "Xem báo cáo chi tiết" trên Dashboard | Có, chỉ là một link, không đổi số liệu Dashboard |
