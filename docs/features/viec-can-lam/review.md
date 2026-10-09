# Review: Trung tâm việc cần làm (bước 3)

- Ngày: 08/10/2026. Nhánh `feature/viec-can-lam`; code được commit ở `361575e` ngày 09/10/2026.
- Nguồn đối chiếu: [spec.md](spec.md) (bản 1.2), [plan-dot-a.md](plan-dot-a.md), [plan-dot-b.md](plan-dot-b.md), mockup trong [mockup/](mockup/).
- File này gộp ba lần đánh giá: reviewer `/ship` đợt A (backend), reviewer `/ship` đợt B (giao diện), và review cuối của phiên chính (3 vòng đọc code, chạy ứng dụng, sửa, chạy lại test).

## Phán quyết cuối: ĐẠT

Chức năng khớp spec 1.2, giao diện khớp mockup (Dashboard phương án B, trang Việc cần làm phương án A). Toàn bộ test PASS, không đổi schema. Các lỗi tìm thấy ở review cuối đã sửa và kiểm lại trên trình duyệt. Phần còn lại ở mục "Giới hạn" không chặn bàn giao.

## Kiểm chứng cuối

| Lệnh hoặc thao tác | Kết quả | Ghi chú |
| --- | --- | --- |
| `dotnet test QuanLyChoThuePhongTroWeb.sln` (DB `quanlyphongtro_test`) | PASS | Domain 122, Application 826, Infrastructure 325, Web 251. Chạy sau vòng sửa cuối |
| `dotnet ef migrations has-pending-model-changes` | PASS | "No changes have been made to the model since the last migration." |
| `git diff --check` | PASS | Chỉ có cảnh báo LF/CRLF; repo dùng `* text=auto` |
| Thử trên trình duyệt (built-in browser, `localhost:5009`, tài khoản Admin mẫu, `BackgroundJobs:Enabled=false`) | PASS | Dashboard ở 1440, 1200, 375 px; dark mode; đổi bộ lọc; trang Việc cần làm; lọc nhóm; link sang Chốt điện nước và Hóa đơn |
| Thử trên trình duyệt bằng tài khoản nhân viên | PASS (người dùng tự thử, 09/10/2026) | Người dùng báo chưa thấy lỗi. Phân quyền nhân viên còn được phủ bằng Web test (HTML và JSON) |
| Bảng hóa đơn quá hạn với dữ liệu thật | CHƯA CHẠY trên trình duyệt | DB phát triển không có hóa đơn quá hạn. Phủ bằng Web test (mã hóa đơn, ngày VN, số ngày quá hạn, link) |

## 1. Đợt A, backend (reviewer `/ship`: CHOT)

- **Cổng xác nhận:** không đổi schema. Mọi thay đổi về tiền nằm trong T1–T10 đã được người dùng xác nhận (08/10/2026 11:04). 4 câu hỏi mở giữ phương án Mặc định.
- **Khớp kế hoạch:**
  - Đủ 11 loại việc, đúng mã hành động, mức ưu tiên, tiêu đề, link (`chiNhanhId` đặt trước `#`), mô tả và thứ tự sắp xếp.
  - KPI: Chờ thu, Phòng đang nợ, Quá hạn chỉ tính `DaGui`; Đã thu giữ nguyên.
  - `InvoiceDebtQueries` dùng chung một định nghĩa với màn Công nợ.
  - Đã sửa H5: truy vấn kỳ chỉ số có lọc chi nhánh.
- **Test có giá trị thật:**
  - Fake store áp đúng vị từ theo mốc thời gian, nên biên KH1/KH3/CS1 là biên thật.
  - Store test chạy trên PostgreSQL, có đối chứng âm cho xóa mềm và trạng thái, có biên `HanThanhToan == nowUtc` ± 1 µs.
  - Web test có đối chứng dương với Admin.
- **Bảo mật, tính đúng:**
  - Fail closed trước mọi truy vấn.
  - Tiền dùng `decimal`; số đã thu tính bằng subquery tương quan trong cùng câu SQL, không N+1.
  - TT2/TT3 chia trọn tập hóa đơn đã gửi chưa thu đủ, không chồng nhau.
  - Thời gian lấy từ `TimeProvider`.
- **Ghi chú không chặn và cách xử lý:**

| # | Ghi chú | Xử lý |
| --- | --- | --- |
| A1 | `GetScopeAsync` bị gọi 2 lần mỗi request Dashboard | Không làm (người dùng duyệt ở đợt B). Ba cách sửa đều phá fail closed, trái luật backend, hoặc đổi dịch vụ phân quyền có khoảng 50 chỗ gọi |
| A2 | Chưa có test giá trị `tienQuaHan`/`soHoaDonQuaHan`/`tongTienChoThu` qua HTTP | Đã thêm ở đợt B (`GetAjaxData_OverdueAndPending_AreScopedByBranch`) |
| A3 | `IDashboardStore.GetOverdueSummaryAsync` để mặc định `allowedBranchIds = null` | Giữ: đúng quy ước sẵn có của interface; caller luôn truyền |
| A4 | XSS ở JS khối "hoạt động gần đây" | Đã sửa ở đợt B (dựng DOM bằng `textContent`) |
| A5 | Khối việc không cập nhật khi đổi bộ lọc | Đã sửa ở đợt B |
| A6 | `DashboardController.Index` dùng `DateTime.Now` | Đã sửa ở đợt B (`TimeProvider` + 7 giờ, có test giờ cố định) |

## 2. Đợt B, giao diện (reviewer `/ship`: CHOT)

- **Cổng xác nhận:** chỉ đổi cách hiển thị tiền (M1–M10), người dùng xác nhận lúc 08/10/2026 11:47. 5 câu hỏi mở giữ phương án Mặc định.
- **Khớp kế hoạch và mockup:**
  - Dashboard phương án B: 6 thẻ KPI có chú thích, khối việc tối đa 5 dòng, link "Xem tất cả" mang `chiNhanhId`.
  - Trang Việc cần làm phương án A: lọc chi nhánh, nút lọc nhóm, 3 khối theo mức ưu tiên, bảng `#hoa-don-qua-han`.
  - Menu có badge; trang Hóa đơn đọc `trangThaiPhatHanh` từ URL.
- **Bảo mật:**
  - JS dựng DOM bằng `createElement`/`textContent`, link qua `safeLink`, không có `innerHTML`/`.html(`.
  - Badge coi phản hồi không ok, bị chuyển hướng hoặc không phải JSON là lỗi; khi lỗi chỉ ẩn badge, không hiện popup.
  - "đ" chỉ dùng ở Dashboard và trang Việc cần làm; các màn khác giữ "₫".
- **Test:**
  - `TienTeTests`.
  - 9 test Web mới: giá trị tiền theo chi nhánh, XSS tên chi nhánh, nhân viên không thấy HD2, khách thuê bị chuyển về đăng nhập, menu và badge, tháng/năm mặc định theo `TimeProvider`.
- **Ghi chú không chặn và cách xử lý:**

| # | Ghi chú | Xử lý |
| --- | --- | --- |
| B1 | Số tiền lớn có thể tràn thẻ KPI ở 375 px | Đã sửa ở review cuối (R3, R8) |
| B2 | Chi nhánh ngoài phạm vi: dropdown hiện "Tất cả chi nhánh" trong khi dữ liệu rỗng | Đã sửa ở review cuối (R7) |
| B3 | Badge làm Dashboard tính tổng hợp việc 2 lần | Giữ, đúng spec §7.3 và §12; theo dõi khi dữ liệu lớn |
| B4 | Test dùng host có giờ cố định không dispose host | Giữ, đúng mẫu sẵn có trong repo |
| B5 | Hoạt động gần đây in tên enum thô "TienMat"/"ChuyenKhoan" | Đã sửa ở review cuối (R6) |

## 3. Review cuối của phiên chính (3 vòng)

Cách làm: đọc lại toàn bộ diff của hai đợt, chạy ứng dụng với DB phát triển (tắt background job), xem bằng trình duyệt ở nhiều độ rộng và theme, sửa, chạy lại toàn bộ test, rồi xem lại.

| # | Vòng | Vấn đề tìm thấy | Sửa | Bằng chứng |
| --- | --- | --- | --- | --- |
| R1 | 1 | **Lỗi chức năng:** CS1 coi phòng là "đã chốt" khi chỉ cần có bản ghi kỳ chỉ số. Trong hệ thống, đã chốt là `TrangThaiGhiNhan == DaDuyet` (`DienNuocService` `IsDaChot`; phát sinh hóa đơn bỏ qua phòng chưa `DaDuyet`). Thực tế trên DB phát triển: trang Chốt điện nước T9/2026 có 2 phòng "Chưa chốt" nhưng Dashboard không báo | `ViecCanLamStore.GetKyChiSoDaChotAsync` (đổi tên từ `GetKyChiSoDaGhiNhanAsync`) chỉ nhận kỳ `DaDuyet`. Spec §5 CS1 cập nhật lên 1.2 | Test store thêm kỳ Nháp/Chờ duyệt/Từ chối: FAIL trước khi sửa (ra 5 kỳ), PASS sau khi sửa (2 kỳ). Trên trình duyệt, Dashboard hiện "Phòng chưa chốt điện nước T9/2026: 2", khớp trang Chốt điện nước |
| R2 | 1 | Tiêu đề việc trên Dashboard bị cắt, hai dòng "Ảnh chỉ số chờ xác..." trông giống nhau, mất thông tin kỳ | Bỏ `text-truncate` ở tiêu đề (Razor và `dashboard.js`); mô tả vẫn cắt một dòng, đầy đủ trong `title` | Ảnh chụp 1440 px và 1200 px: tiêu đề hiện đủ "T9/2026", "T8/2026" |
| R3 | 1 | Ở 375 px, số `1.234.567.890 đ` tràn khỏi thẻ KPI (lỗi B1) | Lớp `dashboard-kpi-value`: cỡ chữ `clamp(0.95rem, 12cqi, 1.25rem)` theo bề rộng thẻ (container query), cho xuống dòng ở khoảng trắng | Đo trên trình duyệt: còn dư 25 px trong thẻ, không cuộn ngang (`scrollWidth = clientWidth = 375`) |
| R4 | 1 | Chú giải biểu đồ ghi "(VNĐ)" trong khi giá trị ghi "đ", tooltip lặp đơn vị | Đổi nhãn dataset thành "Đã thu" và "Chờ thu" | Ảnh chụp |
| R5 | 1–3 | Chữ trục và chú giải biểu đồ khó đọc ở dark mode; đổi theme bằng nút trên header không cập nhật biểu đồ (Chart.js 4.5 chốt màu trục lúc tạo) | Gán màu theo biến `--tblr-*` thẳng vào options của từng biểu đồ; theo dõi thuộc tính `data-bs-theme` bằng `MutationObserver` | Đổi sáng sang tối không tải lại trang: màu tick đổi từ `rgba(31,41,55,.75)` sang `rgba(229,231,235,.75)`, lưới `#374151`; ảnh chụp nhãn trục rõ |
| R6 | 1 | Hoạt động gần đây in "TienMat"/"ChuyenKhoan" (B5) | In "Tiền mặt"/"Chuyển khoản" như các màn khác | Trên trình duyệt: "Số tiền: 2.311.000 đ - Tiền mặt" |
| R7 | 1 | Trang Việc cần làm: chi nhánh ngoài phạm vi hiện dropdown "Tất cả chi nhánh" mà không giải thích vì sao trống (B2) | ViewModel thêm `ChiNhanhNgoaiPhamVi`; view hiện cảnh báo "Chi nhánh đã chọn không thuộc phạm vi của bạn…" | Web test `ViecCanLamPage_Staff_BranchOutsideScope_IsEmptyWithoutError` assert có cảnh báo, và chi nhánh của mình thì không có; trên trình duyệt `?chiNhanhId=999` hiện cảnh báo |
| R8 | 2 | Ở 1200 px, 6 thẻ KPI một hàng quá hẹp, số tiền bị ngắt giữa số ("1.234.567.89 / 0 đ") | Lưới KPI `col-6 col-md-4 col-xxl-2`: 3 thẻ mỗi hàng dưới 1400 px | Ở 1200 px số 10 chữ số nằm trên một dòng (cao 26 px) |

Không phát hiện thêm vấn đề ở vòng 3:
- Đổi bộ lọc chạy AJAX đúng: KPI, biểu đồ, khối việc, hoạt động và link "Xem tất cả" mang `chiNhanhId`.
- Lọc nhóm đúng: nhóm rỗng hiện thông báo, số trên khối cập nhật.
- Link CS2 mở trang Chốt điện nước đúng tháng, năm và chi nhánh. Link HD/TT3 mở trang Hóa đơn với ô "Phát hành" đúng.
- Console không có lỗi từ code mới. Chỉ có lỗi kết nối lại SignalR trong lúc khởi động lại server.

## Giới hạn và rủi ro còn lại

- CS1 đổi nghĩa theo R1. Đây là thay đổi so với spec 1.1 đã duyệt ("giữ logic hiện tại"). Lý do: logic cũ có từ trước quy trình duyệt kỳ chỉ số và mâu thuẫn với màn Chốt điện nước. Spec lên 1.2, người dùng xác nhận giữ bản 1.2 ngày 09/10/2026.
- Một phòng có ảnh chờ xác nhận có thể xuất hiện ở cả CS2 (xác nhận ảnh) lẫn CS1 (chốt phòng). Đây là hai bước khác nhau của cùng một quy trình nên giữ nguyên.
- Badge gọi `TomTat` mỗi lần tải trang quản lý, và `GetScopeAsync` bị gọi 2 lần ở Dashboard (A1, B3). Quy mô hiện tại chấp nhận được.
- Link HD1–HD3, TT3 mang `nam` của kỳ cũ nhất. Nếu hóa đơn trải qua nhiều năm thì trang Hóa đơn chỉ hiện năm cũ nhất (spec §5.1).
- Phần JS (badge, lọc nhóm, dựng lại khối việc) không có test tự động. Đã kiểm tay trên trình duyệt ở vòng review cuối.
