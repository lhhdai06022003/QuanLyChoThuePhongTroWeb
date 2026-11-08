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

## Còn có thể làm tiếp (chưa duyệt)

- **Chốt kỳ vẫn khoảng 57 lệnh mỗi phòng:** hóa đơn tự động chạy lần lượt từng phòng (`DienNuocService.cs`, vòng `RunForRoomAsync`), mỗi phòng tự tạo hóa đơn nháp riêng. Giảm mạnh hơn phải tạo theo lô cho cả chi nhánh, đổi cách mỗi phòng chạy độc lập và báo lỗi riêng.
- **Trang Hợp đồng của Admin** còn in toàn bộ phòng dạng JSON (`ViewBag.ListPhongTro`, mọi trạng thái, mọi chi nhánh) và người thuê một lần. Muốn nhỏ hơn nữa phải tải theo chi nhánh/từ khóa (`SearchAutocompleteAsync` đã có), đổi cách ô chọn hoạt động.
- Phát sinh hóa đơn đếm hóa đơn đã hủy từng hợp đồng (`InvoiceIssuanceService.cs`), +1 lệnh mỗi phòng.
- Bảng điều khiển vẫn tính Việc cần làm hai lần (trang và huy hiệu), khoảng 20 lệnh.

## Giới hạn kiểm chứng

- Chưa xem trên trình duyệt sau đăng nhập (sáng, tối, 375px, console): tài khoản mẫu `admin`, `khachthue01` và tài khoản admin do test tạo đều không đăng nhập được trên DB `quanlyphongtro_test`. **CHƯA CHẠY.** Thứ tự nạp script, CSS và widget được kiểm trên HTML thật bằng `LayoutLibraryPageTests`; đoạn JS đổ người thuê của trang Hợp đồng chỉ được kiểm vị trí trong HTML (`ContractPagePayloadTests`), chưa chạy trên trình duyệt.
