# Review: Hóa đơn tự động

- Đối chiếu: [spec.md](spec.md) v7, [plan.md](plan.md) (gộp kế hoạch ba đợt A, B, C và đợt D sửa sau thử tay).
- Nhánh `feature/hoa-don-tu-dong`, tách từ `master` = `dc391e3`. Spec `e444f37`, đợt A `39b3d81`, đợt B `0736f4d`, đợt C (gồm N1, N6) `a161feb`; đợt D chưa commit (11/10/2026).
- Mỗi đợt chạy dây chuyền `/ship` (planner, coder, tester, reviewer); phiên chính sửa các điểm review nêu và kiểm chứng lại.

## 1. Phán quyết: ĐẠT

Reviewer `/ship` cho đợt A **CẦN SỬA** (một link 404, đã sửa), đợt B và đợt C **CHOT**. Mọi điểm "nên sửa" đã xử lý; không có thay đổi schema hay tiền nằm ngoài phạm vi người dùng đã xác nhận. Đợt D (ba vấn đề người dùng báo khi thử tay và email điều chỉnh gửi nền, mục 4 và 5) đã sửa và kiểm chứng trên giao diện thật. Trước khi gộp `master` còn: commit đợt D, dọn DB test, cập nhật mục Hiện trạng trong `AGENTS.md` và ghi chú FAIL giả (mục 6, N6).

## 2. Kiểm chứng mới nhất (11/10/2026, sau đợt D)

| Lệnh hoặc thao tác | Kết quả | Ghi chú |
| --- | --- | --- |
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS | 0 lỗi |
| `dotnet ef migrations has-pending-model-changes` | PASS | Chỉ có migration của đợt A |
| `kiem-tra-moi-truong.js` | PASS | DB `quanlyphongtro_test` |
| `dotnet test` Domain.UnitTests | PASS 138/138 | |
| `dotnet test` Application.UnitTests | PASS 1111/1111 | Thêm 40: HD5, sửa khoản phí, xem trước chỉ số, email điều chỉnh gửi nền (T30) |
| `dotnet test` Infrastructure.IntegrationTests | PASS 414/414 | Thêm 11: CS2, HD5 trên DB thật, sửa dòng ở cả hai nhánh điều chỉnh; T30 không thêm test mới, 5 test điều chỉnh có sẵn chuyển sang chờ email nền |
| `dotnet test` Web.IntegrationTests | PASS 304/304 | Thêm 16: endpoint điều chỉnh có dòng, xem trước, CS2 và HD5 qua HTTP, email điều chỉnh gửi nền (T30) |
| `git diff --check` | PASS | Chỉ cảnh báo LF/CRLF |
| Thử tay trên preview (DB test) | PASS | Mục 4, gồm đợt D |
| Gửi email qua SMTP thật | KHÔNG KIỂM hộp thư | Test dùng email giả; các lần thử tay gửi email tới địa chỉ mẫu `preview_a@example.test` (tên miền dành riêng), không kiểm hộp thư. Lần thử T30, log nền ghi "Đã gửi nền 1/1" (SMTP nhận thư) |
| Job tự chốt chạy trong ứng dụng thật | CHƯA CHẠY | Test gọi use case và job (smoke) trực tiếp; preview tắt `BackgroundJobs` |

### Lịch sử theo đợt

| Đợt | Domain | Application | Infrastructure | Web | Ghi chú |
| --- | --- | --- | --- | --- | --- |
| A | 129 | 991 | 372 | 266 | Lượt đầu của reviewer FAIL 250/514 (tổng gần gấp đôi; lúc đó ghi là chạy chồng, cùng dạng N6), chạy lại PASS |
| B | 138 | 1051 | 397 | 273 | Infrastructure chạy 4 lần đều xanh; test tranh chấp job và điều chỉnh PASS 6/6 lần; một lượt Web FAIL 257/528 (cùng dạng N6, chưa xác định nguyên nhân lúc đó) |
| C | 138 | 1065 | 403 | 288 | Một lượt Infrastructure 1 FAIL `DbUpdateException` và ba lượt Web FAIL 271–272/558, đều do N6 |
| D | 138 | 1111 | 414 | 304 | Một lượt tập con Infrastructure FAIL `AutoInvoiceChainTests.StaffManualClose_...` (xem D5), đã sửa; các lượt sau PASS. Thêm T30 (D6): email điều chỉnh gửi nền |

## 3. Cổng xác nhận

| Thời điểm (Asia/Bangkok) | Phạm vi người dùng duyệt |
| --- | --- |
| 10/10/2026 17:13 | Đợt A: S1–S5, T1–T8; câu hỏi A1–A7, riêng A4 đổi (báo khách cả `ChiSoGiam`) |
| 10/10/2026 (sau review A) | L1 dừng chuỗi `ThieuDonGia`; L2 gửi email nền sau vòng lặp |
| 10/10/2026 21:39 | Đợt B: không đổi schema, T9–T19, câu hỏi B1–B4 (B4 thêm câu hướng dẫn) |
| 11/10/2026 09:45 | Đợt C: không đổi schema, T20–T24, F1, câu hỏi C1, C3, C4; C2 đổi (QR chỉ khi Đã chốt hoặc Đã gửi) |
| 11/10/2026 11:54 | T25, T26 (chỉ hóa đơn Đã gửi ở Quản lý phòng trọ); thử tay trên DB test, được tạo dữ liệu mẫu |
| 11/10/2026 (sau thử tay) | Sửa N1 (định dạng chỉ số); N6 phương án (c): mở rộng mã seed test và dọn DB test |
| 11/10/2026 (sau commit đợt C) | Đợt D: người dùng nêu ba vấn đề trên giao diện (ảnh chờ xác nhận của kỳ đã chốt; phòng bị dừng vì điện nước bất thường chỉ có thông báo; "Điều chỉnh và gửi lại" chỉ sửa được hai chỉ số) và yêu cầu sửa đủ. Quyết định T27, T28, T29 ghi ở [plan.md](plan.md) mục 2.4; ô lý do vẫn không có (spec 6, T22) |
| 11/10/2026 (trước commit đợt D) | Người dùng yêu cầu xử lý D6 (email điều chỉnh gửi nền, T30) trước khi commit, và xem lại trạng thái migration trên DB phát triển (đã tự áp `20261010101505_AddAutoInvoiceReviewWindow` khi chạy ứng dụng) |

Nội dung từng điểm: [plan.md](plan.md) mục 2. Reviewer các đợt đối chiếu diff với từng điểm: không có migration nào ngoài `20261010101505_AddAutoInvoiceReviewWindow` (2 cột, 1 CHECK, 1 index lọc, `Down` gỡ đủ); không sửa migration cũ, `ApplicationDbContext`, `Program.cs`, `.csproj`, `appsettings.json`; fixture chỉ thêm (`ProvisionalEmailCalls`, category job mới, `ProvisionalGate`); test kiến trúc chỉ siết thêm.

## 4. Đánh giá đạt

1. **Tiền:** hóa đơn tạm tính dùng đúng `CreateDraftsAsync` của luồng tay; test so từng dòng giữa luồng tự động và luồng tay (cùng 3.270.000 đ) và giữa `CreateDraftsAsync` với `CreateDraftsInCurrentTransactionAsync`. Lõi tạo nháp tách ra không đổi logic (so bằng `git diff -w`). Hóa đơn thay thế `-R{n}` giữ dịch vụ cố định và sự cố phát sinh giữa hai lần tính. Decimal xuyên suốt.
2. **Nguyên tử và chạy song song:** chuỗi tự tạo chạy lặp/song song chỉ ra một hóa đơn (unique index + khóa dòng). Tự chốt và gửi trong một transaction. Điều chỉnh lỗi rollback toàn bộ (test trên DB thật kiểm hóa đơn, hạn, tổng tiền, lịch sử, chỉ số, ảnh, thông báo, email). Job và điều chỉnh cùng khóa kỳ trước, luồng thanh toán cùng chiều, không có vòng khóa.
3. **Phân quyền:** fail closed; người thực hiện chuỗi phải có `Meter.Review`, `Invoice.Draft`, `Invoice.Submit`; điều chỉnh kiểm trước và kiểm lại sau khi khóa (`Invoice.Cancel` khi `DaGui`); GET điều chỉnh kiểm `Invoice.Read` trước mọi dữ liệu; nhân viên chi nhánh khác nhận 403. Khách không bao giờ là người thực hiện. `IAutoInvoiceService`, `IInvoiceAdjustmentService` nằm trong `BranchScopeArchitectureTests`.
4. **Phía khách:** thấy hóa đơn tạm tính ở danh sách và chi tiết; PDF, mọi endpoint thanh toán trả 404; không tính vào công nợ, nhắc nợ, Dashboard. JSON không lộ `hanThanhToan`, `lichSuTrangThais`. `KhachXemDuoc` không đổi.
5. **DbContext dùng chung và log:** `DiscardPendingChanges()` sau mỗi bước và trước mọi thông báo; lý do lường trước chỉ ghi Information/Warning; lỗi SMTP/SignalR không hoàn tác dữ liệu.
6. **Email nền:** dispatcher singleton tạo scope riêng, chỉ dùng `IEmailService`; test M4 chứng minh request trả về khi email còn bị giữ. Email "đã điều chỉnh" dùng cùng dispatcher từ T30, có test cổng chờ tương tự (và đã chạy thử với email gửi ngay để thấy test FAIL).
7. **Giao diện:** quy tắc tạm tính nằm ở một helper; mọi chuỗi server được escape; POST có header antiforgery; chống bấm đúp; chỉ class Tabler; Web không tham chiếu Domain.

### Thử tay trên preview (11/10/2026)

Cấu hình `web-test-db` trong `.claude/launch.json` đọc `QLCTPT_TEST_CONNECTION_STRING` (chặn nếu DB không kết thúc `_test`), không ghi secret ra file, không sửa `appsettings.json`. Lúc thử tay, DB phát triển `QuanLyPhongTroDb` chưa có migration đợt A (kiểm chỉ đọc) và không bị đụng. Sau đó người dùng chạy ứng dụng với DB phát triển: `DatabaseInitializer` tự áp `20261010101505_AddAutoInvoiceReviewWindow`; kiểm lại chỉ đọc bằng `dotnet ef migrations list` thì cả 14 migration đã áp, không còn Pending. Dữ liệu mẫu: chi nhánh `PREVIEW`, 2 phòng, 7 hóa đơn đủ trạng thái, tài khoản khách `preview_khach`.

| Kiểm | Kết quả |
| --- | --- |
| Màn hóa đơn quản lý | Nhãn "Tạm tính, tự chốt lúc…"; hóa đơn tạm tính không có Chốt/Trả lại, có "Điều chỉnh và gửi lại"; hóa đơn chờ duyệt tay vẫn có Chốt/Trả lại |
| QR trong modal chi tiết (C2) | Nháp, Chờ duyệt tay, tạm tính, Đã hủy: không QR. Đã chốt, Đã gửi: có QR |
| Modal điều chỉnh | Cảnh báo khi số mới < số cũ; hộp xác nhận; 180 → 190 kWh ra 3.435.000 đ (+10 × 3.500), hạn mới +24 giờ, danh sách tự tải lại. Hóa đơn bị khóa hiện lý do, khóa ô nhập và nút gửi |
| Cổng khách | Chỉ thấy hóa đơn Đã gửi và tạm tính; chi tiết tạm tính có thông báo, SĐT chi nhánh, bảng chỉ số, chỉ nút Đóng; hóa đơn Đã gửi vẫn có Thanh toán và Tải PDF; chế độ tối ở 375px đọc rõ |
| Màn chi nhánh (T23) | Client chặn 0, 169, 1.5, rỗng; lưu 48, DB ghi 48 |
| Việc cần làm | HD2 = 1 (chờ duyệt tay), HD4 = 2 (tạm tính) |
| Quản lý phòng trọ (T25, T26) | Phòng có hóa đơn Đã gửi: nợ 3.400.000 đ, "Thu tiền" lấy hóa đơn Đã gửi dù có bản tạm tính mới hơn. Phòng chỉ có bản tạm tính: không nợ, "Thu tiền" báo không có hóa đơn |
| Console trình duyệt, log server | Không lỗi ngoài các 404/400 do chính người thử gọi |

Thử tay đợt D (dữ liệu mẫu thêm PV103–PV105, tài khoản admin thử, chỉ trên DB test):

| Kiểm | Kết quả |
| --- | --- |
| T27: kỳ T8/2026 của PV101 đã chốt, còn 3 ảnh chưa xác nhận; kỳ T9 còn 1 ảnh | Việc cần làm không còn mục "Ảnh chỉ số chờ xác nhận T8/2026" và T9; chỉ còn T10/2026 (kỳ chưa chốt của PV102): 1 |
| T28: PV103 tiêu thụ điện 2.000 kWh (kỳ đã chốt, chưa hóa đơn), PV104 đơn giá điện 0 | Mục "Chưa tạo được hóa đơn tạm tính T9/2026" (Hóa đơn, Cần làm): 2, mô tả "PV103: tiêu thụ điện nước bất thường; PV104: thiếu đơn giá"; nút Xử lý mở `ChotDienNuoc?thang=9&nam=2026&chiNhanhId=…` (200) |
| T29, hóa đơn tạm tính của PV101 | Modal có bảng khoản phí. Đổi chỉ số điện 190 → 200: dòng điện thành "Tiền điện (100 → 200)" 100 × 3.500 = 350.000, tổng cập nhật. Sửa đơn giá phòng, thêm "Phí giữ xe" 2 × 100.000, gửi: DB có 4 dòng, tổng 3.470.000, chỉ số kỳ 200, hạn mới theo 48 giờ của chi nhánh |
| T29, hóa đơn Đã gửi của PV105 | Hộp xác nhận thêm câu "Hóa đơn đã công bố sẽ bị hủy và thay bằng hóa đơn mới". Sửa tay đơn giá điện 3.600, đổi chỉ số nước 5 → 8 (dòng điện sửa tay giữ nguyên, dòng nước tính lại), xóa "Phí rác": hóa đơn cũ `DaHuy`, bản `-R1` có đúng 3 dòng đã chốt, tổng 2.300.000, tạm tính, lịch sử "(sửa 2 dòng, xóa 1 dòng)" |
| T30, email điều chỉnh gửi nền | Hóa đơn tạm tính HD-PV101-09-2026: đổi số lượng "Phí giữ xe" 2 → 3, bấm Điều chỉnh. Request `POST /HoaDon/DieuChinh/69901` mất khoảng 0,5 giây (đo bằng `performance` của trình duyệt; lần thử đợt D trước khi sửa khoảng 10 giây). Hộp thông báo thành công tổng mới 3.570.000 đ; DB có 4 dòng, hạn mới, lịch sử "(sửa 1 dòng)"; log nền "Đã gửi nền 1/1 email hóa đơn tạm tính"; không lỗi server |

## 5. Lỗi đã sửa

| # | Vấn đề | Sửa | Kiểm chứng |
| --- | --- | --- | --- |
| A1 | Thông báo "chưa tạo được hóa đơn tạm tính" có link `/QuanLyNhaTro/DienNuoc` (404) | Link `/QuanLyNhaTro/ChotDienNuoc?thang=..&nam=..` | Web test đọc link trong thông báo thật, GET trả 200 |
| B1 | Comment `CascadeSubsequentPeriodsAsync` nằm nhầm trên method mới | Chuyển về đúng method | Build |
| B2 | GET điều chỉnh trả giờ UTC thô, POST trả giờ VN | Cả hai trả `hanTuChot` giờ VN, dùng chung `FormatVietnamTime` | Web test kiểm cả GET và POST |
| B3 | Không tạo được hóa đơn thay thế luôn báo "phòng có nhiều hợp đồng trong kỳ" | Tách hai câu theo đúng trường hợp, dữ liệu vẫn rollback | Application, Infrastructure |
| C1 | `Swal.fire(title, message, icon)` chèn thông báo server như HTML | Dạng `{ title, text, icon }` | Web test, thử tay |
| C2 | Comment "HD1-HD3" lỗi thời | HD1-HD4 | Build |
| C3 | T25: modal "Thu tiền" lấy cả bản tạm tính/chờ duyệt, hiện QR | `PhongTroStore.GetUnpaidInvoiceAsync` chỉ lấy `DaGui` | `PhongTroDebtScopeTests`, thử tay |
| C4 | T26: cờ nợ của thẻ phòng cộng hóa đơn chưa công bố | `PhongTroStore.GetSoDoPhongAsync` chỉ cộng `DaGui` | `PhongTroDebtScopeTests`, thử tay |
| C5 | Màn chi nhánh hiện chữ "No newline at end of file" (có từ `a207286` trên master) | Xóa dòng rác cuối `QuanLyChiNhanh.cshtml` | Web test, thử tay |
| N1 | Diễn giải dòng điện/nước in chỉ số từ DB thành "100.000" (dễ đọc là một trăm nghìn); áp cả luồng tay | `Common/Formatting/ChiSo.DinhDang` ("100", "1.234,5", không phụ thuộc culture) cho diễn giải, thông báo lỗi ở `HoaDonCalculatorService` và 2 thông báo kỳ sau ở `MeterReadingWorkflowService`. Không đổi số tiền, số lượng; dòng cũ trong DB giữ nguyên | 6 test mới trong `HoaDonCalculatorServiceTests` |
| D1 | T27: mục "Ảnh chỉ số chờ xác nhận" đếm cả ảnh của kỳ đã chốt (chốt bằng nhập tay để lại ảnh `DocDuoc`/`Loi`), nên kỳ đã chốt và thu đủ vẫn báo cần xử lý | Truy vấn CS2 thêm điều kiện kỳ khác `DaDuyet` và kỳ không bị khóa bởi kỳ sau | `ViecCanLamStoreTests` (kỳ đã chốt, kỳ bị khóa bởi kỳ sau đã chốt hoặc có hóa đơn phát hành, mở lại kỳ thì đếm lại), `ViecCanLamEndpointTests`, thử tay |
| D2 | T28: thông báo "chưa tạo được hóa đơn tạm tính" không có việc tương ứng | HD5 tính từ dữ liệu hiện tại bằng `AutoInvoiceReadingPolicy`, không thêm bảng, không đổi schema | `ViecCanLamServiceTests` (10 test), `ViecCanLamStoreTests` (3), `ViecCanLamEndpointTests` (5), thử tay |
| D3 | T29: điều chỉnh chỉ sửa được hai chỉ số | Sửa toàn bộ khoản phí ở cả hai nhánh điều chỉnh; `InvoiceLineEditing` dùng chung với sửa hóa đơn nháp (giữ nguyên bốn thông báo lỗi cũ); thêm quyền `Invoice.Draft` | 25 test `InvoiceAdjustmentServiceTests`, 6 test `InvoiceAdjustmentTests` trên DB thật, 8 test endpoint, 1 test tĩnh |
| D4 | Phát hiện khi thử tay: đổi chỉ số điện cũng ghi đè dòng nước (đổi tên dòng, mất chỗ sửa tay) | Modal chỉ tính lại dòng của đồng hồ vừa đổi chỉ số | Thử tay lại (dòng điện sửa tay giữ nguyên khi đổi chỉ số nước) |
| D5 | Phát hiện khi chạy test: email "đã điều chỉnh" lấy chỉ số từ kỳ đã bị lõi chỉ số tách khỏi bộ theo dõi (hiện số cũ); test `AutoInvoiceChainTests.StaffManualClose_...` kiểm email gửi nền ngay sau request nên chập chờn | Email dùng chỉ số đã chốt trong phép điều chỉnh; test chờ có hạn tới 5 giây | `InvoiceAdjustmentTests` bắt được lỗi trước khi sửa; chạy lại PASS |
| D6 | Điều chỉnh gửi email ngay trong request: khi SMTP chậm, nút "Đang gửi..." treo khoảng 10 giây (lỗi cũ từ đợt B, người dùng báo lại khi thử tay) | T30: `InvoiceAdjustmentService` xếp email vào `IProvisionalInvoiceEmailDispatcher` (bộ gửi nền của L2); thông báo cổng vẫn gửi ngay; không có dispatcher thì gửi ngay như cũ | 5 test `InvoiceAdjustmentServiceTests`; 5 test `InvoiceAdjustmentTests` chờ email nền trên DB thật; test Web có cổng chờ `ProvisionalGate` (FAIL khi tạm đổi sang gửi ngay, PASS khi trả về như cũ); thử tay: 0,5 giây |
| N6 | Seed test sinh mã chi nhánh 1 ký tự + 4–6 hex (~1 triệu mã) trên DB test ~23.000 chi nhánh, trùng unique `IX_chi_nhanh_MaChiNhanh`; một test lỗi ghi log Error làm `CustomWebApplicationFactory` đánh rớt cả collection, tổng test Web gần gấp đôi | `TestCodes.MaChiNhanh(prefix)` ở hai project integration test (tiền tố cũ + hex cho đủ 10 ký tự của cột), thay 105 chỗ trong 34 file | Infrastructure rồi Web liên tiếp hai vòng PASS 403/403 và 288/288 |

## 6. Lưu ý còn mở

| # | Nội dung | Mức | Đề xuất |
| --- | --- | --- | --- |
| N6 (tiếp) | Dọn DB test bị bộ phân loại auto mode chặn (lệnh `dotnet ef database drop`). DB vẫn ~23.000 chi nhánh; mã mới 10 ký tự gần như không thể trùng nên không còn chặn test | Thấp | Người dùng tự drop `quanlyphongtro_test` nếu muốn DB sạch (fixture tự tạo lại). Sửa ghi chú "FAIL giả do chạy chồng" trong `AGENTS.md` và skill `chay-test-tich-hop`: còn nguyên nhân trùng dữ liệu seed |
| M1 | Đơn giá chụp vào kỳ lúc tạo kỳ, không cập nhật lại; khai giá chi nhánh sau đó thì kỳ đang dở vẫn 0, phòng phải lập hóa đơn tay | Trung bình | Thông báo `ThieuDonGia` đã hướng dẫn; sửa tận gốc là việc riêng, đổi luồng chỉ số |
| M2 | Email nền (chốt kỳ và, từ T30, điều chỉnh) dùng `Task.Run`, tắt máy đúng lúc gửi có thể mất email (chỉ Warning) | Thấp | Đã chấp nhận theo L2 |
| M3 | `AutoInvoiceDeadlineHarness.IsolateJobAsync` đặt `HanPhanHoi = null` cho hóa đơn tạm tính khác trong DB test (kể cả dữ liệu mẫu `PREVIEW`) | Thấp | Chỉ an toàn khi không chạy Infrastructure song song với Web; muốn thử tay lại thì seed lại |
| L8 | `MailKitEmailService` bỏ kiểm chứng chứng chỉ SMTP (`ServerCertificateValidationCallback` trả `true`), lỗi cũ của file | Bảo mật | Sửa chung cho cả file trong một việc riêng |
| N2 | Cổng khách ở 375px: bảng danh sách hóa đơn và bảng khoản phí tràn ngang (không `table-responsive`) | Thấp | Việc giao diện riêng |
| N3 | Nút đổi theme ẩn ở mobile (`_Header.cshtml`: `d-none d-md-flex`) | Thấp | Tùy người dùng |
| N4 | Modal điều chỉnh của hóa đơn Đã gửi vẫn ghi "Hạn tự chốt hiện tại" là mốc đã qua | Thấp | Ẩn dòng khi hóa đơn không còn Chờ duyệt |
| D7 | HD5 chỉ xét kỳ `DaDuyet` chưa có hóa đơn; phòng đủ điều kiện nhưng chưa có hóa đơn vì lỗi lúc tạo (`LoiKhiTao`, không có nhân viên phụ trách, thiếu quyền) không hiện, vì các lý do này không suy ra được từ dữ liệu | Thấp | Chấp nhận; vẫn có thông báo. Muốn hiện cả trường hợp này thì phải lưu trạng thái dừng (đổi schema) |
| D8 | CS2 vẫn đếm ảnh chưa xác nhận khi đồng hồ đó đã có ảnh chính thức ở kỳ chưa chốt (tới khi chốt kỳ) | Thấp | Theo định nghĩa đã duyệt của module việc cần làm |

Đã đóng: L1 (`ThieuDonGia`), L2 (email nền), L3 (đánh giá lại phòng), L4 (khoảng hở giữa các đợt, xong ở B và C), L5 (quy ước OCR giả trong test), L6 (validate số giờ ở service), L7 (comment `KhachXemDuoc`), M4 (test email nền qua F1).
