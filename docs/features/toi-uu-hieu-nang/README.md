# Tối ưu hiệu năng cổng quản lý và cổng khách thuê

Nhánh `feature/toi-uu-hieu-nang`. Phạm vi đã duyệt (08/11/2026): bước 0–4, sau đó bước 5–8 (giai đoạn 2, có duyệt sửa `Program.cs`). Ràng buộc: không đổi hành vi, API, giao diện; mỗi bước một commit; không thêm thư viện.

| Bước | Nội dung |
| --- | --- |
| 0 | Đo baseline: đếm lệnh SQL bằng `QueryCounter` (DiagnosticListener có sẵn của EF Core) trong `QueryBudgetTests` |
| 1 | Mục HD5 của Việc cần làm: lấy ngữ cảnh phòng theo lô thay vì từng phòng |
| 2 | Nhớ phạm vi chi nhánh của người dùng trong một request |
| 3 | Bỏ Chart.js, Select2, flatpickr khỏi layout chung, chỉ nạp ở trang dùng (Font Awesome giữ lại: widget AI dùng icon `fa-solid`, 24 nút đang tải dùng `fa-spin`) |
| 4 | Tách JS/CSS của widget trợ lý AI ra file tĩnh |
| 5 | Nén CSS/JS/SVG (Brotli/Gzip có sẵn trong ASP.NET Core), không nén HTML/JSON; file gắn `?v=` cache một năm (`Helpers/StaticAssetDelivery.cs`) |
| 6 | Duyệt chỉ số nhiều phòng: tra chi nhánh cả danh sách một lần (`IMeterImageStore.GetBranchIdsByRoomsAsync`) |
| 7 | Hóa đơn tự động: không đọc lại ngữ cảnh phòng sau bước xác nhận ảnh khi không có ảnh nào được xác nhận |
| 8 | Trang Hợp đồng: danh sách người thuê in một lần dạng JSON, JS đổ vào ô lọc và ô chọn trong modal |

## Số đo gốc (commit bước 0)

Lệnh: `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests --filter "FullyQualifiedName~QueryBudgetTests" --logger "console;verbosity=detailed"` (DB `*_test`). Nhân viên chi nhánh A trong `BranchScopeKit.SeedAsync`, 1 hợp đồng.

| Request | Lệnh SQL |
| --- | --- |
| `GET /QuanLyNhaTro/ViecCanLam/TomTat` (huy hiệu, chạy trên mọi trang quản lý), 0 ứng viên HD5 | 16 |
| Như trên, 5 ứng viên HD5 (phòng không hợp đồng) | 46 (mỗi ứng viên +6; phòng có hợp đồng tới +9) |
| `GET /QuanLyNhaTro/Dashboard` (5 ứng viên) | 55 |
| `GET /QuanLyNhaTro/ViecCanLam` (5 ứng viên) | 51 |
| `GET /QuanLyNhaTro/QuanLyHoaDon` | 4 |
| `GET /QuanLyNhaTro/QuanLyHopDong` | 8 |
| `POST /HoaDon/GetList` (10 dòng) | 4 |
| `GET /api/ThongBao/LayMoiNhat` (chuông, mọi trang) | 2 |
| `GET /KhachThue/Dashboard` | 7 |
| `GET /KhachThue/HoaDon` | 1 |

Một lần mở trang quản lý bất kỳ = trang + `TomTat` + `LayMoiNhat`. Ví dụ trang Hóa đơn với 5 ứng viên: 4 + 46 + 2 = 52 lệnh. Ứng viên HD5 tối đa 60 (`ViecCanLam:SoKyPhongDanhGiaHoaDonTuDong`), nên huy hiệu có thể lên tới khoảng 16 + 60 × 9 = 556 lệnh.

## Dung lượng tài nguyên layout (đo 08/11/2026)

Thư viện CDN, dung lượng tải đã nén (`curl --compressed`):

| Thư viện | Nén | Trang dùng |
| --- | --- | --- |
| Chart.js (`npm/chart.js`, không ghim phiên bản) | 61.412 B | 2 file |
| Select2 JS + CSS | 18.254 + 1.640 B | 2 file |
| flatpickr JS + 2 CSS + locale | 12.989 + 2.610 + 2.577 + 470 B | 2 file |
| Font Awesome CSS (layout quản lý) | 18.778 B | Widget AI (7 icon `fa-solid`) và 24 nút đang tải (`fa-spin`): vẫn cần, giữ lại |
| DataTables JS | 28.496 B | 19 file |
| SweetAlert2 | 18.357 B | nhiều |
| SignalR | 11.163 B | chuông thông báo |

File trong repo, không nén (`Program.cs` chỉ `UseStaticFiles()`): `tabler.min.css` 536.149 B, `tabler-icons.min.css` 203.697 B, `tabler-icons.woff2` 778.812 B, `jquery.min.js` 89.503 B, `tabler.min.js` 83.567 B. Widget AI `_AiChatWidget.cshtml` 22.053 B nhúng vào HTML mọi trang quản lý.

## Kết quả sau bước 0–4 (08/11/2026)

Cùng lệnh đo, cùng dữ liệu seed. Lượt sau thêm 10 ứng viên HD5 (tổng 15) trước khi đo các trang bên dưới, nên số "sau" còn được tính với dữ liệu nặng hơn lượt gốc.

| Request | Trước | Sau |
| --- | --- | --- |
| `TomTat` (huy hiệu, mọi trang quản lý), 0 ứng viên HD5 | 16 | 16 |
| `TomTat`, 5 ứng viên | 46 | 20 |
| `TomTat`, 15 ứng viên | khoảng 106 (16 + 15 × 6) | 20 |
| `TomTat`, 60 ứng viên có hợp đồng (trần cấu hình) | khoảng 556 | 20 |
| `GET /QuanLyNhaTro/Dashboard` | 55 (5 ứng viên) | 28 (15 ứng viên) |
| `GET /QuanLyNhaTro/ViecCanLam` | 51 (5 ứng viên) | 23 (15 ứng viên) |
| `GET /QuanLyNhaTro/QuanLyHoaDon` | 4 | 3 |
| `GET /QuanLyNhaTro/QuanLyHopDong` | 8 | 6 |
| `POST /HoaDon/GetList`, `LayMoiNhat`, cổng khách thuê | 4, 2, 7 và 1 | không đổi |

Tài nguyên trang:

- Trang không dùng biểu đồ, ô chọn ngày, Select2 (phần lớn trong 40 màn quản lý): bớt 4 file JS và 3 file CSS, khoảng 99 KB nén (Chart.js 61.412 + Select2 19.894 + flatpickr 18.646 B). Chart.js ghim `@4.6.1`.
- Widget trợ lý AI: mỗi trang quản lý và khách thuê bớt 17.363 B HTML; CSS (5,9 KB) và JS (9,8 KB) thành file tĩnh có `asp-append-version`, trình duyệt cache.

Test chặn hồi quy: `QueryBudgetTests` (số lệnh của huy hiệu không tăng theo số ứng viên), `AutoInvoiceStoreTests.GetRoomContexts_Batch_MatchesSingleReads_ForEveryScenario`, `EmployeeAccessTests` (đọc phân công một lần mỗi request), `LayoutLibraryStaticGuardTests`, `LayoutLibraryPageTests`.

## Số đo gốc giai đoạn 2 (08/11/2026, chưa sửa code)

Lệnh: `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests --filter "FullyQualifiedName~QueryBudgetTests|FullyQualifiedName~MonthlyFlows_QueryCount" --logger "console;verbosity=detailed"`.

| Request | 1 phòng | 5 phòng | Mỗi phòng thêm |
| --- | --- | --- | --- |
| `POST /DienNuoc/SaveChotDienNuoc` (chốt kỳ, kèm hóa đơn nháp tự động từng phòng) | 72 | 332 | +65 |
| `POST /HoaDon/PhatSinh` (phát sinh hóa đơn tay) | 14 | 18 | +1 |

Trang Hợp đồng trên DB test (43.945 người thuê, 41.389 phòng tích lũy từ các lượt test): nhân viên 2.924.998 B, Admin 22.881.866 B, cùng 6 lệnh SQL. Khoảng 270 B cho mỗi người thuê hoặc phòng; dữ liệu thật vài trăm người thuê thì khoảng 100–200 KB.

## Kết quả sau bước 5–8 (08/11/2026)

Cùng lệnh đo. DB test lúc đo lại có 45.522 người thuê, 42.865 phòng (nhiều hơn lượt gốc).

| Request | Trước | Sau |
| --- | --- | --- |
| Chốt kỳ, 1 phòng | 72 | 65 |
| Chốt kỳ, 5 phòng | 332 | 293 (mỗi phòng thêm 65 → 57) |
| Phát sinh hóa đơn, 1 và 5 phòng | 14 và 18 | không đổi (không nằm trong phạm vi) |
| Trang Hợp đồng, nhân viên | 2.924.998 B | 890.043 B (−70%) |
| Trang Hợp đồng, Admin | 22.881.866 B | 14.302.593 B (−37%; phần còn lại chủ yếu là JSON toàn bộ phòng) |

Bước 5, nén Gzip mức nhanh (Brotli thường nhỏ hơn): `tabler.min.css` 536.149 → 90.792 B, `tabler-icons.min.css` 203.697 → 43.095 B, `jquery.min.js` 89.503 → 36.053 B, `tabler.min.js` 83.567 → 29.236 B; tổng bốn file 912.916 → 199.176 B. HTML và JSON không nén.

Test chặn hồi quy: `StaticFilesTests` (nén đúng loại file, nội dung giải nén giống bản gốc, HTML không nén, cache file `?v=`), `MeterReadingWorkflowTests` (thứ tự báo lỗi, tra chi nhánh một lần), `MeterImageStoreTests.GetBranchIdsByRooms_MatchesSingleLookup_ExcludesDeletedAndMissing`, `AutoInvoiceServiceTests` (số lần đọc ngữ cảnh khi có/không có xác nhận ảnh), `ContractPagePayloadTests`.

## Số đo trước giai đoạn 3 (08/11/2026, chưa sửa code)

Test: `AutoInvoiceAdjustmentEndpointTests.MonthlyClose_QueryCount_Breakdown`, `QueryBudgetTests.Breakdown_Dashboard_And_ContractPage`. DB test lúc đo: 37.818 chi nhánh, 15.280 Admin, 45.999 người thuê, 43.197 phòng (tích lũy qua các lượt test, làm phồng hai số có ghi chú).

| Hạng mục | Số đo |
| --- | --- |
| 1. Chốt kỳ, mỗi phòng | Duyệt chỉ số 61 lệnh cho 5 phòng (khoảng 12/phòng, một transaction). Hóa đơn tự động 46 lệnh mỗi phòng = đọc ngữ cảnh 8 × 2 lần + tạo hóa đơn nháp 13 + báo nhân viên 18. Số 18 bị phồng: thông báo gửi cho mọi Admin (DB test có 15.280 Admin nên lệnh INSERT bị chia nhiều lô); dữ liệu thật vài Admin thì khoảng 3. Ước tính dữ liệu thật: khoảng 44 lệnh mỗi phòng |
| 2. Trang Hợp đồng (Admin) | 14.424.821 B = JSON người thuê 3.365.193 B (73 B/người) + JSON phòng 4.161.258 B (96 B/phòng) + phần còn lại 6.898.370 B, chủ yếu là 75.575 thẻ `<option>` chi nhánh (37.818 chi nhánh × 2 ô chọn). Ước tính dữ liệu thật (vài chi nhánh, 300 người thuê, 200 phòng): khoảng 40 KB dữ liệu trên nền trang |
| 3. Phát sinh hóa đơn | 1 phòng 14, 5 phòng 18, 20 phòng 33 lệnh: đúng +1 mỗi phòng |
| 4. Bảng điều khiển | Trang 28 lệnh (Việc cần làm chiếm 19) + huy hiệu 20 = 48 lệnh mỗi lần mở; đổi bộ lọc 28 |
| 5. Màn 375px | Cổng quản lý: 15/21 màn tràn ngang cả trang (bảng rộng 486–1.205 px); không tràn: Giữ chỗ, Chốt điện nước, Lịch sử thanh toán, Báo cáo, Phòng trọ (và Đối chiếu chỉ lệch 8 px). Cổng khách thuê: 4/8 (Hợp đồng, Lịch sử thanh toán, Sự cố, Thông báo). Nguyên nhân chung: `site.css` dòng 79–83 ép `.table-responsive, .dataTables_wrapper { overflow: visible !important }` để menu ⋯ không bị cắt; màn nào có CSS riêng đặt lại `overflow-x` thì không tràn |

## Kết quả sau bước 9–11 (08/11/2026)

| Bước | Thay đổi | Trước | Sau |
| --- | --- | --- | --- |
| 9 | Bảng điều khiển (không lọc chi nhánh) in sẵn số cho huy hiệu (`Helpers/ViecCanLamBadge.cs`, `window.viecCanLamTomTat`); có lọc chi nhánh và trang khác vẫn gọi `TomTat` | Mở trang 48 lệnh (28 + huy hiệu 20) | 28 |
| 10a | `IAutoInvoiceService.RunForRoomsAsync`: ngữ cảnh ban đầu của các phòng đọc một lượt bằng `GetRoomContextsAsync`; mỗi phòng vẫn đánh giá lại trước khi tạo hóa đơn; đọc lô lỗi thì cảnh báo và đọc từng phòng | Chốt kỳ 1 phòng 65, 5 phòng 293 | 62 và 254 (mỗi phòng thêm 57 → 48) |
| 10b | `ThongBaoStore` nhớ người nhận theo chi nhánh trong một store (Scoped; tác vụ nền tạo scope mỗi lượt) | (gộp trong 10a) | (gộp trong 10a) |
| 11 | `CountCancelledInvoicesByContractAsync`: đếm bản hủy của mọi hợp đồng trong một truy vấn, sau khi khóa hợp đồng | Phát sinh 1/5/20 phòng: 14/18/33 | 14/14/14 |

Kiểm chứng: Application 2775/2776 (đỏ có sẵn `ExpiryAlert_AppFailure_DoesNotBlockEmail`), Infrastructure 722 PASS (1 skip), Web 840/840. Trình duyệt (DB test, tài khoản test): Bảng điều khiển không gọi `TomTat`, huy hiệu hiện đúng số và màu; trang Hóa đơn và Bảng điều khiển có lọc chi nhánh vẫn gọi `TomTat`, cùng số; console không có lỗi mới. Ảnh `hieu-nang-09-dashboard-huy-hieu-in-san.jpg`.

Test chặn hồi quy: `ViecCanLamEndpointTests` (số in sẵn bằng `TomTat`, chỉ có khi không lọc chi nhánh, JS dùng trước khi gọi), `AutoInvoiceServiceTests` (`RunForRooms_*`: cùng kết quả và tác dụng phụ như chạy từng phòng, đọc lô lỗi, phòng lặp, tham số sai), `ThongBaoStoreTests.GetRoomResponsibleUserIds_CachedPerBranch_*`, `InvoiceDraftWorkflowTests.CreateDrafts_ManyContracts_CountsCancelledOnce_SuffixPerContract`, `InvoiceDraftConcurrencyTests.CountCancelledInvoices_*`.

## Kết quả sau bước 12 (09/11/2026)

Bước 12: phần kiểm tra khi duyệt chỉ số (`MeterReadingWorkflowService.ApprovePeriodsAsync`) đọc dữ liệu cả lô trước vòng lặp, mỗi loại một truy vấn: kỳ (khóa `FOR UPDATE` theo `PhongTroId` tăng dần, như khi khóa từng phòng), ảnh (khóa theo `AnhChiSoDongHoId`), kỳ sau đã khóa, hóa đơn đã phát hành, hợp đồng tháng trước, kỳ trước. Vòng lặp vẫn xét từng phòng theo id tăng dần nên lỗi báo ra như trước. Kỳ nháp tạo mới trong lô đọc đơn giá điện/nước của chi nhánh một lần. Phần lan truyền sang kỳ sau (`CascadeSubsequentPeriodsAsync`) giữ nguyên.

| Request | Trước | Sau |
| --- | --- | --- |
| Duyệt chỉ số 5 phòng (một transaction) | 61 lệnh | 26 |
| Chốt kỳ, 1 phòng | 62 | 60 |
| Chốt kỳ, 5 phòng | 254 | 224 (mỗi phòng thêm 48 → 41) |

Phần "báo nhân viên" tăng 18 → 19 lệnh giữa hai lượt đo vì DB test có thêm Admin (thông báo gửi mọi Admin), không do bước 12.

Kiểm chứng: Application 2780/2781 (đỏ có sẵn `ExpiryAlert_AppFailure_DoesNotBlockEmail`), Infrastructure 723 PASS (1 skip, gồm `ApprovePeriods_ConcurrentBatchesWithOppositeRoomOrder_DoNotDeadlock`), Web 840/840.

Test chặn hồi quy: `MeterImageStoreTests.ApprovalBatchReads_MatchSingleReads_ForEveryRoom` (bản theo lô ra cùng kết quả bản từng phòng: kỳ/ảnh đã xóa, hóa đơn nháp/đã hủy, hợp đồng đã hủy, phòng không có), `MeterReadingWorkflowTests`: `FirstRoomFailsLate_SecondRoomFailsEarly_ReportsFirstRoom`, `SecondRoomHasLockedInvoice_*`, `ManualAndOfficialRooms_UseEachRoomsOwnImages`, `ManyRooms_DoesNotReadChecksRoomByRoom`, `ManyRoomsWithoutPeriod_CreatesDrafts_ReadsBranchPricesOnce`.

## Còn có thể làm tiếp (chưa duyệt)

- **Chốt kỳ còn khoảng 41 lệnh mỗi phòng trên DB test** (khoảng 27 với dữ liệu thật vì thông báo không còn bị phồng): đánh giá lại 8, tạo hóa đơn nháp 13, báo nhân viên, phần duyệt còn lại (lan truyền kỳ sau, lưu). Giảm tiếp phải tạo hóa đơn nháp theo lô cho cả chi nhánh, đổi cách mỗi phòng chạy độc lập và báo lỗi riêng: chưa đề xuất.
- **Trang Hợp đồng của Admin:** với dữ liệu thật chỉ thêm khoảng 40 KB; DB test phồng vì 37.818 chi nhánh in hai lần. Nếu làm, gộp vào đợt giao diện (tải theo chi nhánh/từ khóa, `SearchAutocompleteAsync` đã có).
- **Bảng tràn ngang ở 375px** (15/21 màn quản lý, 4/8 màn khách thuê): do `site.css` dòng 79–83. Thuộc đợt giao diện: cho bảng cuộn ngang trên điện thoại mà menu ⋯ không bị cắt.

## Giới hạn kiểm chứng

- Kiểm trên trình duyệt 08/11/2026 (sau bước 8): web chạy cấu hình `web-test-db` (`.claude/launch.json`), đăng nhập bằng tài khoản do `BranchScopeKit` tạo (`ad_…`, khách thuê `kt_…`, mật khẩu là hằng `Password` của kit). Ảnh `hieu-nang-01` … `08` trong `hinhanhprojectdemo`.

| Màn | Kết quả |
| --- | --- |
| Bảng điều khiển | 2 biểu đồ vẽ được; không nạp flatpickr/Select2; widget AI đủ `data-*`; console sạch |
| File tĩnh | Trình duyệt nhận bản nén: `tabler.min.css` 536.149 → 102.519 B, `jquery.min.js` 89.503 → 39.250 B (đo bằng Resource Timing của trình duyệt) |
| Hợp đồng | Ô lọc và ô chọn trong modal đều có 45.967 người thuê, cùng thứ tự; lọc theo người thuê gửi đúng `nguoiThueId`; Select2 trong modal tìm được; flatpickr chạy; console sạch; chế độ tối ổn |
| Hợp đồng 375px | Bảng tràn ngang khỏi thẻ: **có sẵn**, bước 8 không đụng bảng; ghi lại, chưa sửa |
| Báo cáo | Chart.js 4.6.1 vẽ được; console sạch |
| Lịch sử thanh toán | flatpickr tiếng Việt (T2, "Tháng mười một"); console sạch |
| Tài khoản đăng nhập | Select2 trong modal chạy, câu "Không tìm thấy kết quả" tiếng Việt; không lưu dữ liệu |
| Khách thuê | Widget AI nạp CSS/JS tĩnh, endpoint `/KhachThue/AiAssistant/Chat`, mở được ở sáng/tối, 7 icon Font Awesome; không gửi tin (tránh gọi AI thật) |
