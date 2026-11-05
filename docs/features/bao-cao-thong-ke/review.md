# Review: Báo cáo thống kê

Nhánh `feature/bao-cao-thong-ke`. Ngày 05/11/2026. Coder điền phần mình đã chạy; tester và điều phối điền tiếp.

## Kết quả kiểm chứng

| Lệnh | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS | 0 lỗi; cảnh báo có sẵn của repo, không có cảnh báo mới từ file báo cáo |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests` | PASS | 140 |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests` | PASS | 2610 (có `BaoCaoCalculatorTests`, `BaoCaoServiceTests`, `BaoCaoBuilderTests`, `PaymentChannelsTests`, `EmployeeAccessTests`, `BranchScopeArchitectureTests`, `HopDongTraCuuServiceTests`) |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests` | PASS | 716 + 1 bỏ qua có sẵn; DB test qua biến môi trường User, không in giá trị |
| `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests` | PASS | 759; chạy tuần tự sau Infrastructure (có `BaoCaoPageTests` 14 ca) |
| `dotnet ef migrations has-pending-model-changes` | PASS | "No changes have been made to the model"; không đổi schema |
| `git diff --check` | PASS | không báo lỗi khoảng trắng (chỉ cảnh báo LF/CRLF có sẵn) |
| Xem giao diện, chụp ảnh, kiểm C3 trên trình duyệt | CHƯA CHẠY | việc của điều phối sau chặng reviewer |

## Ghi chú triển khai

Câu trả lời của người dùng (05/11/2026):

- **X1:** phương án A. "Tiền giữ chỗ đang chờ quyết định" = Σ(nhận − áp dụng) của yêu cầu chưa có quyết định hoàn tiền, trạng thái `TuChoi`/`HetHan`/`DaHuy`, chỉ cộng khi > 0. Quyết định còn `ChoHoanTien`/`DangHoanTien` không cộng vào đâu.
- **C1:** công suất cắt ngày-phòng đã thuê trong cửa sổ `[max(ngày tạo phòng, ngày 1), min(cuối tháng, hôm nay)]`.
- **C2:** tab Công nợ chỉ hiện số hóa đơn, không gắn link; tên khách link `/QuanLyNhaTro/NguoiThue/ChiTiet/{id}`.
- **C3:** sửa trong T8. Trước: `LichSuThanhToanController.GetList/GetStats` tách `dateRange` bằng `" to "`, còn flatpickr locale `vn` (4.6.13) sinh `"2026-05-01 đến 2026-05-31"`, nên chuỗi không tách được thành hai ngày và bộ lọc ngày không được áp (chọn khoảng thì parse cả chuỗi lỗi, `tuNgay`/`denNgay` đều null; chọn một ngày không bị ảnh hưởng vì không có dấu ngăn). Sau: `TachKhoangNgay` chấp nhận cả `" to "` và `" đến "`, dùng chung cho `GetList` và `GetStats`; store đổi ngày VN sang mốc UTC (`>= đầu ngày`, `< đầu ngày kế`). Hệ quả: số tổng trên màn đổi khi người dùng chọn khoảng ngày (người dùng đã chấp nhận). Test: `BaoCaoPageTests.LichSuThanhToan_*` (cả hai dấu ngăn, một ngày, không ngày), `LichSuThanhToanStoreTests` (ranh giới 16:59:59Z / 17:00:00Z).

Điểm lệch so với plan.md (theo mục 9 của kế hoạch):

1. **Đã thu của Dashboard gồm cả sổ của hóa đơn chưa `DaGui`.** Báo cáo chỉ tính `DaGui` (spec D2). Trong dữ liệu thật hai số bằng nhau (thu tay, webhook và xác nhận minh chứng chỉ ghi sổ cho hóa đơn `DaGui`). Không sửa Dashboard. Test `DoanhThu_DaThuKhopDashboard_VaTachDungKenh` khóa sự khớp trên dữ liệu không có sổ ở hóa đơn chưa gửi. Nếu xem trên preview mà lệch thì ghi chênh lệch vào đây.
2. **Tiền thừa không nằm trong sổ** (`InvoicePaymentPolicy.DecideRecording` ghi tối đa số còn nợ): Đã thu = Σ sổ, không cắt theo `TongTien`; Tiền thừa = Σ thẻ `[TIEN-THUA:n]` của `GhiChu`.
3. **Quyết định hoàn tiền:** chỉ cần `TrangThai == DaHoanTien` (quyết định 0 đồng được đặt `DaHoanTien` ngay; `DaHuy` không code nào gán).
4. Không có `GetBoLocAsync`: danh sách chi nhánh lấy bằng `IPhongTroService.GetDanhSachChiNhanhDropdownAsync(actorId)`.
5. Store chỉ trả row; số liệu gom trong các builder static ở Application để unit test không cần DB.
6. Cờ "khách đã trả phòng" chép biểu thức bộ lọc `hopDongDaKetThucConNo` (không có hợp đồng `DangHoatDong` cùng phòng và cùng người thuê), không xét chuỗi mã gia hạn. Ở bảng khách, `DaTraPhong` = mọi hóa đơn nợ của khách đều thuộc hợp đồng đã trả phòng.
7. Menu có `ActivePrefixes` (theo spec).
8. "Sắp hết hạn 30 ngày" dùng cửa sổ 31 ngày từ đầu hôm nay, như `HopDongStore`.
9. Tab Công nợ không gắn link số hóa đơn (C2); tab Hợp đồng không gắn link số theo kỳ (màn hợp đồng không có bộ lọc tháng VN).
10. `HopDongTraCuuService.TinhTongSoThang` dùng chung `KhoangNgayThue.HopNhat`, kết quả không đổi (test `History_MonthCount_IncludesLastDay` và các test `HopDongTraCuu` PASS).

Điểm riêng của lần code này:

- `TrungBinhKwhPhongThang` là null khi không có bản ghi hợp lệ (plan ghi "null khi bằng 0"; chọn "không có bản ghi" để một kỳ có bản ghi nhưng 0 kWh vẫn hiện 0).
- `PaymentNotificationFormatter.PaymentMethodLabel` giữ nguyên chuỗi trả về, chỉ đổi cách phân loại sang `PaymentChannels.Of`.
- Thanh điều hướng tab của trang Báo cáo tự xử lý bằng JS (không dùng `data-bs-toggle`) để tải dữ liệu đúng lúc và giữ `?tab=` bằng `history.replaceState`.
- Nhân viên có từ 2 chi nhánh nhưng thiếu quyền `Report.Read` ở một chi nhánh: chọn "Tất cả chi nhánh của tôi" vẫn tính toàn bộ chi nhánh được giao (kiểm quyền chỉ khi chọn một chi nhánh, theo kế hoạch). Hiện `Report.Read` mặc định có cho mọi nhân viên của chi nhánh được giao nên chưa có khác biệt.

## Review cuối và sửa sau review (05/11/2026)

Reviewer kết luận **CẦN SỬA** (`.bangiao/danh-gia.md`). Điều phối đã sửa:

- **S1:** thẻ "Nợ của khách đã trả phòng" (tab Công nợ) link `?thang=0&nam=0&trangThaiPhatHanh=3&hopDongDaKetThucConNo=1`, cùng mẫu với việc KH5. Trước đây thiếu `thang/nam`, nên màn hóa đơn chỉ hiện tháng hiện tại.
- **S2:** tab Doanh thu chỉ gắn link "Còn nợ" cho từng kỳ. Bỏ link ở dòng Tổng và ở bảng theo chi nhánh, vì màn hóa đơn không lọc được khoảng kỳ.
- **Tràn ngang (phát hiện khi xem preview):** `site.css` ép `.table-responsive` thành `overflow: visible`, nên bảng 12 cột của tab Doanh thu đẩy rộng cả trang (1583px ở viewport 1440px). Đã thêm `@section Styles` trong `Views/BaoCao/Index.cshtml`: `.bc-pane .table-responsive { overflow-x: auto !important; }`, ô bảng `white-space: nowrap`.
- Hai điểm "nên sửa" (vòng quay tải dùng chung giữa các tab, làm tròn kiểu ngân hàng ở Điện nước) **chưa sửa**.

Sau khi sửa S1–S2: `dotnet build` PASS; `Web.IntegrationTests --filter BaoCao` PASS 22/22. Lần chạy cuối sau khi sửa CSS ghi trong báo cáo ngày.

## Thử giao diện trên preview (DB phát triển, Admin, chỉ xem)

- 5 tab tải được; `?tab=` cập nhật khi đổi tab; console không lỗi; không tràn ngang ở 1440px và 800px.
- "Đã thu" khớp Dashboard: T3/2026 2.528.000 đ, T8/2026 6.905.000 đ, T9/2026 4.321.000 đ, T11/2026 0 đ (tất cả chi nhánh). Quá hạn 4.655.500 đ và "nợ của khách đã trả phòng" 4.655.500 đ khớp Dashboard và việc KH5.
- Tiền cọc đang giữ 0 đ: đúng, vì mọi hợp đồng trên DB phát triển đều có tiền cọc 0 đ.
- Link: Còn nợ theo kỳ → `QuanLyHoaDon?thang=&nam=&trangThaiPhatHanh=3`; thẻ Quá hạn / Sắp hết hạn → `QuanLyHopDong?boLoc=`; tên khách → `/NguoiThue/ChiTiet/{id}`; số hóa đơn không có link (C2).
- Xuất Excel cả 5 tab: 200, đúng Content-Type xlsx, tên `BaoCao_{tab}_202512_202611.xlsx`, file là zip hợp lệ. Chi nhánh không tồn tại → 404.
- Lịch sử thanh toán: flatpickr cho giá trị `2026-10-20 đến 2026-10-22` → 2 giao dịch, 5.040.000 đ. Lọc tháng 9 và tháng 11 → 0. Dấu ngăn `" to "` cho cùng kết quả. 5 thẻ nằm trên một hàng.
- Tab Công suất: T12/2025–T8/2026 có 0 ngày-phòng có thể thuê, vì các phòng trên DB phát triển được tạo sau các hợp đồng nhập lùi ngày (theo C1). Tab Doanh thu vẫn có hóa đơn các tháng đó.
- Ảnh (1440×900, ngoài repo): `hinhanhprojectdemo/bao-cao-01-doanh-thu.png` … `bao-cao-06-lich-su-thanh-toan.png`.
- Điểm nhỏ chưa sửa: ô chọn kỳ (`input type=month`) hiện tên tháng theo ngôn ngữ trình duyệt ("December 2025"); nhãn "TB KWH/PHÒNG/THÁNG" xuống dòng giữa chữ ở 1440px.

## Chưa thử thật

- Mở file Excel bằng Excel thật (mới kiểm tải về và đọc bằng ClosedXML trong test).
- Đăng nhập bằng tài khoản nhân viên trên trình duyệt (đã có test HTTP cho 403/200).
- Dữ liệu thật có tiền thừa, giữ chỗ hoàn tiền, cấn trừ cọc, tiền cọc khác 0.
- Giao diện tối của biểu đồ.
