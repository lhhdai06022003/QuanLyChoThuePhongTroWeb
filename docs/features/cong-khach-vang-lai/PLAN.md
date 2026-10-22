# Cổng khách vãng lai Implementation Plan

## Đợt mở rộng tìm phòng — 16/10/2026

Spec: `SPEC.md`, mục Mở rộng tìm và chọn phòng. Thực hiện trực tiếp trên worktree `codex/khachvanglai-master-schema`, giữ các thay đổi giao diện chưa commit từ đợt trước.

### 1. Tìm kiếm và sắp xếp

- [x] Viết/run test đỏ `Application.UnitTests/PublicRoomDiscoveryTests.cs` cho lọc kết hợp, không dấu, giá ngược/âm, tie-break sắp xếp trước phân trang.
- [x] Thêm `Application/Features/PublicRooms/PublicRoomDiscovery.cs`: `Normalize(PublicRoomSearch)` và `Apply(IEnumerable<PublicRoomDto>, PublicRoomSearch)`, không phụ thuộc EF/ASP.NET.
- [x] Nối `PhongController.Index`, mở rộng `PublicRoomListModel` với bộ lọc/sắp xếp, lựa chọn khu nhà và ảnh hero từ phòng công khai.
- [x] Chạy test xanh; kiểm tra HTTP lọc + phân trang giữ tham số.

### 2. Yêu thích và so sánh

- [x] Viết/run test đỏ cho module `wwwroot/js/guest-room-selection.mjs`: dữ liệu hỏng, ID trùng/sai, giới hạn 3/50, storage bị chặn.
- [x] GET trang đã lưu, GET dữ liệu công khai theo tối đa 50 ID, GET so sánh tối đa 3 ID trong `PhongController`; ViewModels không chứa entity/điện thoại riêng.
- [x] Module browser chỉ lưu ID, render bằng DOM an toàn, cập nhật nút/trạng thái/đa tab, xử lý phòng không còn công khai và lỗi mạng.
- [x] HTTP test đảm bảo phòng ẩn/đã thuê không xuất hiện trong dữ liệu yêu thích/so sánh. Dùng `_Test` riêng.

### 3. Cẩm nang và giao diện

- [x] Thêm trang Razor cẩm nang với nội dung theo SPEC; checklist có thể tích, FAQ dùng details/summary.
- [x] Tách thẻ phòng dùng chung, thiết kế Index/chi tiết/header/footer và các tiện ích mới. Font Inter + Manrope, xanh #066fd1, navy #152842, nền #f4f7fc, trắng #fff, chữ #374151, viền #dde5ef.
- [x] Bổ sung guest-discovery.css theo nhóm thành phần, tái sử dụng CSS form hẹn/giữ chỗ/đặt cọc và giữ ReturnUrl.

### 4. Kiểm chứng và bàn giao

- [x] Build solution; test Application discovery, module JS, HTTP discovery và GuestPortalFlow/SessionRevalidation.
- [x] Rà desktop/mobile, bàn phím, yêu thích sau reload, so sánh/chia sẻ, lọc rỗng, lỗi storage/mạng; lưu ảnh giao diện.
- [x] Review cuối, sửa lỗi cụ thể và chạy lại gate liên quan. Không chạy migration/seed trên DB chính; không đổi schema. Giữ thay đổi để người dùng review, không tự push đợt mới.

Review focus: URL lọc không mất tham số; so sánh không lộ phòng ẩn; dữ liệu localStorage không gây XSS; lỗi/quota storage không làm vỡ trang; menu/thanh so sánh không che nội dung trên điện thoại.

### Kết quả kiểm chứng

- Application: 14/14 kiểm thử discovery/area đạt; JavaScript: 8/8 đạt; Web HTTP: 8/8 đạt, gồm GuestPortalFlow và SessionRevalidation. Build solution: 0 lỗi, 26 cảnh báo.
- HTTP xác nhận sắp xếp trước phân trang, giữ tham số khi chuyển trang, loại phòng ẩn và đã thuê khỏi dữ liệu công khai. Trang đã lưu tải lại 2 phòng đúng; trình duyệt chặn phòng so sánh thứ tư, sắp xếp giá tăng đưa phòng rẻ nhất lên đầu; FAQ và checklist hoạt động.
- Review cuối tìm và sửa lỗi quota: localStorage đọc được nhưng không ghi được vẫn phải khôi phục các ID chuyển qua URL dự phòng. Test tái hiện đạt, reviewer xác nhận đã giải quyết.
- Rà viewport desktop và 390px: không tràn ngang tài liệu; bảng so sánh cuộn riêng. Đã lưu ảnh preview; không gửi yêu cầu hẹn/giữ chỗ hoặc chuyển tiền trên DB chính.
- Ruling: giữ CSS form cũ và thêm file discovery riêng — hạn chế rủi ro thay đổi các form nghiệp vụ đang hoạt động; CSS mới được giới hạn trong layout cổng khách và kiểm tra thứ tự selector.
- Ruling: khi storage bị chặn, bộ nhớ tạm chỉ đảm bảo trên trang đang mở và đường dẫn dự phòng — giao diện thông báo đúng giới hạn này, không hứa đồng bộ thiết bị/tài khoản.
- Không sửa schema/migration/DbContext/entity; server preview dùng QuanLyPhongTroDb, initialize/seed và background jobs tắt. Chưa commit/push đợt thay đổi mới.
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cho khách đăng ký, xem phòng, đặt lịch, giữ chỗ và theo dõi khoản cọc/hoàn với dữ liệu thật trên schema `master` hiện có.

**Architecture:** Application chứa quy tắc và interface store theo từng luồng; Infrastructure dùng `ApplicationDbContext` và giao dịch PostgreSQL; Web nối các Razor view hiện có với service, phân quyền và antiforgery. Tiền chỉ được ghi nhận qua đối soát nhân viên. Giữ giao diện `/phong` hiện tại.

**Tech Stack:** .NET 8, ASP.NET Core MVC/Razor, EF Core PostgreSQL, MailKit hiện có, xUnit.

**Spec:** [SPEC.md](SPEC.md)

## Tiến độ kiểm chứng 2026-10-08

- Đã nối giao diện và mã xử lý cho đăng ký/đăng nhập khách, lịch xem, giữ chỗ, ảnh minh chứng riêng tư, đối soát nhiều giao dịch, áp dụng vào cọc hợp đồng, quyết định/ghi nhận hoàn tiền và email nhân viên gửi sau khi xem trước.
- Đã bổ sung nhả phòng khi hết hạn chuyển tiền, gia hạn hoặc hủy sau hạn ký hợp đồng, và dùng thời điểm ngân hàng thực nhận để phân loại khoản đến muộn. Bản xem thử dùng `QuanLyPhongTroDb` với `Database:InitializeOnStartup=false`.
- `dotnet build` thành công; Domain 122/122, Application 813/813, Web integration 235/235, Infrastructure integration 302/302 khi loại 2 phép đếm số bảng. Test HTTP mới đã qua: đăng ký → đăng nhập → gửi lịch xem → gửi giữ chỗ → xem trạng thái trên DB `_Test`. Hai phép đếm số bảng kỳ vọng 37 bảng, trong khi DB `_Test` hiện có 38 bảng; không sửa schema để chiều theo DB thử.
- Chưa gửi thử email qua SMTP thật. Chưa kiểm tra tay các POST đối soát, cọc hợp đồng và hoàn tiền qua giao diện; store và service tương ứng đã có kiểm thử tự động trên DB `_Test`.

## Global Constraints

- Không tạo hoặc sửa migration, bảng, cột; dùng schema `master` qua migration `20260922163513_AddPublicViewingAndReservations`.
- Không chạy migration, seed hoặc integration test trên `QuanLyPhongTroDb`; DB test phải kết thúc `_test` hoặc `_integration_test`.
- POST cần antiforgery; xác thực chủ yêu cầu và quyền nhân viên theo chi nhánh tại service/server.
- Giao dịch thực nhận chỉ do nhân viên đối soát; ảnh minh chứng không phải bằng chứng tiền đã vào tài khoản.
- Tiền giữ chỗ áp dụng vào **tiền cọc hợp đồng** một lần, không vào hóa đơn tháng đầu.
- Giữ phong cách giao diện `/phong` và các màn hình khách đã có.

## Review Focus

1. Hai khách cùng chọn suất cuối: chỉ một yêu cầu được xác nhận.
2. Hai nhân viên duyệt giữ chỗ cùng phòng: unique index chỉ cho một giữ chỗ hoạt động, UI báo xung đột.
3. Khách sửa ID trong form để xem lịch/tiền người khác: trả `403` hoặc `404`, không rò dữ liệu.
4. Nhập lại cùng mã giao dịch hoặc bấm xác nhận lặp: không tăng tiền thực nhận.
5. Hoàn và áp dụng cùng lúc: tổng chi/áp dụng không vượt khoản thu xác nhận.

## Các quyết định cần chốt trước khi bật POST tiền

Các quy tắc chính đã được chốt trong SPEC. Cần xác nhận ranh giới người nhận email đang chờ trước Task 7. Quên mật khẩu/đổi email ở ngoài phạm vi kế hoạch này.

## Task 1 — Role khách và đăng ký/đăng nhập

**Files:** sửa `src/QuanLyChoThuePhongTroWeb.Application/Common/Enums/AppEnums.cs`, `Features/NguoiDungs/Services/NguoiDungService.cs`, `Web/Areas/QuanLyNhaTro/Controllers/NguoiDungController.cs`, `Web/Controllers/DangKyKhachController.cs`, `Web/Views/DangKyKhach/Index.cshtml`; tạo `Application/Features/GuestAccounts/{IGuestRegistrationService,GuestRegistrationService,GuestRegistrationRequest}.cs`, `Infrastructure/Persistence/Features/GuestAccountStore.cs`; đăng ký DI hai tầng. Test `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/Features/GuestAccounts/GuestRegistrationServiceTests.cs`.

**Interface:** `Task<GuestRegistrationResult> RegisterAsync(GuestRegistrationRequest request, CancellationToken cancellationToken)` tạo `NguoiDung` và `KhachVangLai` trong một giao dịch; kết quả có ID khách hoặc lỗi tên đăng nhập/email. Login nhận `AppRole.KhachVangLai=3` và redirect `returnUrl` nội bộ.

- [ ] Test đăng ký trùng tên, mật khẩu băm, role khách và rollback khi hồ sơ không lưu; chạy riêng test để thấy lỗi.
- [ ] Bổ sung role, store/service và POST đăng ký có antiforgery; chạy lại test tới khi qua.
- [ ] Test login/cookie role khách, redirect nội bộ và ngăn redirect ngoài site; build Web.
- [ ] Commit `feat: add guest registration and login`.

## Task 2 — Phòng công khai và khung giờ

**Files:** sửa `Application/Features/PublicRooms/{IPublicRoomStore,PublicRoomService}.cs`, `Infrastructure/Persistence/Features/PublicRoomStore.cs`; tạo `Application/Features/Viewings/{IViewingSlotService,ViewingSlotService}.cs`, `Infrastructure/Persistence/Features/ViewingSlotStore.cs`; sửa `Web/Areas/QuanLyNhaTro/Controllers/LichXemPhongController.cs` và view tương ứng. Test Application cho điều kiện phòng/khung, Web cho quyền chi nhánh.

**Interface:** `ListAvailableAsync(roomId, nowUtc, ct)` trả khung tương lai còn sức chứa; `CreateAsync(actorId, roomId, startUtc, endUtc, capacity, ct)` từ chối chồng lịch/nhân viên ngoài chi nhánh.

- [ ] Test phòng có giữ chỗ hoạt động không công khai và khung quá hạn/đầy không được chọn.
- [ ] Triển khai truy vấn chỉ đọc và giao diện nhân viên tạo/ẩn khung; kiểm tra quyền.
- [ ] Chạy test Application liên quan và build; commit `feat: manage public viewing slots`.

## Task 3 — Yêu cầu xem phòng

**Files:** tạo `Application/Features/Viewings/{IViewingRequestService,ViewingRequestService,ViewingRequestDto}.cs`, `Infrastructure/Persistence/Features/ViewingRequestStore.cs`, `Web/ViewModels/PublicRooms/ViewingRequestModel.cs`; sửa `Web/Controllers/PhongController.cs`, `Web/Views/Phong/HenXem.cshtml`, `Web/Views/Phong/YeuCauCuaToi.cshtml`. Test Application và Web.

**Interface:** `SubmitAsync(actorId, roomId, slotId?, preferredTimeUtc?, contact, ct)` lấy `KhachVangLaiId` từ actor, không nhận từ form; `CancelAsync(actorId, requestId, ct)` kiểm tra chủ sở hữu và mốc hủy đã chốt.

- [ ] Test email bắt buộc, không đăng nhập, khung phòng khác, thời gian quá khứ, suất cuối cạnh tranh, đề xuất chờ nhân viên.
- [ ] Triển khai service/store với transaction và lịch sử; mở form POST antiforgery, hiển thị trạng thái thật.
- [ ] Chạy test Application/Web liên quan; commit `feat: enable guest viewing requests`.

## Task 4 — Yêu cầu giữ chỗ và hạn

**Files:** tạo `Application/Features/Reservations/{IReservationService,ReservationService,ReservationDto}.cs`, `Infrastructure/Persistence/Features/ReservationStore.cs`, Web view model; sửa `PhongController`, `GiuChoController` của Area, view `GiuCho.cshtml` và trang yêu cầu cá nhân. Test Application/Infrastructure.

**Interface:** `RequestAsync(actorId, roomId, viewingRequestId?, ct)`, `ApproveAsync(staffId, reservationId, amount, paymentDeadlineUtc, contractDeadlineUtc, ct)`, `CancelAsync(actorId, reservationId, ct)`; duyệt dùng unique index hiện có và trả xung đột rõ.

- [ ] Test giữ trực tiếp/từ lịch sai chủ hoặc phòng, duyệt tiền/hạn không hợp lệ, hai duyệt cùng phòng.
- [ ] Triển khai trạng thái và lịch sử, quyền chi nhánh, trang theo dõi hạn, xử lý hủy/hết hạn theo quyết định bổ sung trong SPEC.
- [ ] Chạy test liên quan và build; commit `feat: enable guest reservations`.

## Task 5 — Minh chứng và đối soát tiền

**Files:** tạo `Application/Features/ReservationPayments/{IReservationPaymentService,ReservationPaymentService}.cs`, `Infrastructure/Persistence/Features/ReservationPaymentStore.cs`, Web upload validator/private file endpoint; sửa màn khách và Area `GiuCho`. Test Application/Infrastructure/Web.

**Interface:** `SubmitEvidenceAsync(actorId, paymentRequestId, image, claimedAmount, ct)` chỉ ghi minh chứng; `ConfirmReceivedAsync(staffId, paymentRequestId, evidenceId?, bankTransactionCode, receivedAmount, ct)` ghi tiền thực nhận đúng một lần.

- [ ] Test file sai loại/quá lớn, khách khác tải ảnh, mã giao dịch trùng, xác nhận lặp, nhiều giao dịch cộng dồn và tiền đến sau khi phòng nhả phải hoàn.
- [ ] Triển khai lưu ảnh riêng, giao dịch đối soát, lịch sử và UI phân biệt đã báo chuyển với đã nhận.
- [ ] Chạy test liên quan và build; commit `feat: reconcile guest reservation payments`.

## Task 6 — Cọc hợp đồng và hoàn tiền

**Files:** tạo `Application/Features/ReservationSettlement/{IReservationSettlementService,ReservationSettlementService}.cs`, `Infrastructure/Persistence/Features/ReservationSettlementStore.cs`; tích hợp service tạo hợp đồng hiện có, sửa Area `GiuCho` và trang khách. Test Application/Infrastructure.

**Interface:** `ApplyToContractDepositAsync(staffId, reservationId, contractId, amount, ct)`, `DecideRefundAsync(staffId, reservationId, amount, reason, ct)`, `RecordRefundAsync(staffId, decisionId, bankTransactionCode, amount, ct)`; các tổng tính trong giao dịch có khóa/thứ tự nhất quán.

- [ ] Test hợp đồng khác phòng/khách, áp dụng hai lần, khoản dư/đến muộn hoàn bắt buộc, duyệt hoàn 0/một phần/toàn bộ phần tùy quyết định, hoàn nhiều lần vượt mức và hoàn song song áp dụng.
- [ ] Triển khai bút toán vào các bảng hiện có, trang quyết định và trạng thái hoàn thực tế.
- [ ] Chạy test liên quan và build; commit `feat: settle reservation deposits and refunds`.

## Task 7 — Email nhân viên và hoàn thiện UI

**Files:** mở rộng `Application/Abstractions/Services/IEmailService.cs`, `Infrastructure/ExternalServices/Emails/MailKitEmailService.cs`, tạo service danh sách người nhận cùng phòng; sửa Area `LichXemPhong`, `GiuCho` và Razor views khách. Test Application/Web.

- [ ] Test chỉ chọn khách đang chờ cùng phòng, không lộ địa chỉ người nhận khác, quyền nhân viên và lỗi SMTP.
- [ ] Nối nút gửi thật, hiển thị kết quả riêng từng người, áp dụng quy tắc chống gửi lặp đã chốt.
- [ ] Kiểm tra desktop/mobile và giữ thiết kế công khai; commit `feat: notify waiting guests and finish guest UI`.

## Task 8 — Kiểm chứng cuối

- [ ] Chạy `dotnet build QuanLyChoThuePhongTroWeb.sln`.
- [ ] Chạy Domain và Application unit tests; chạy Infrastructure/Web integration tests **chỉ khi** `QLCTPT_TEST_CONNECTION_STRING` trỏ tới DB kết thúc `_test` hoặc `_integration_test`.
- [ ] Chạy `git diff --check`; kiểm tra `git diff --name-only` không chứa migration hoặc thay đổi schema.
- [ ] Kiểm tra tay luồng HTTP trên DB thử riêng: xem phòng → đăng ký → lịch → giữ chỗ → minh chứng → đối soát → áp dụng cọc/hoàn → email; lưu kết quả review cạnh SPEC.

## Bổ sung giao diện theo phản hồi — 08/10/2026

- [x] Thêm nút hẹn xem và đặt phòng trực tiếp trên thẻ phòng, giữ thiết kế công khai hiện có.
- [x] Làm rõ màn gửi yêu cầu đặt phòng và các bước chờ nhân viên duyệt trước khi chuyển tiền.
- [x] Tách trang đặt cọc riêng, kiểm tra chủ yêu cầu và hiển thị form minh chứng theo trạng thái/hạn.
- [x] Đưa lịch xem, nút hủy lịch và đặt phòng vào mục tài khoản có tên rõ ràng.
- [x] Kiểm thử HTTP: khách chưa đăng nhập đi qua đăng ký/đăng nhập và quay lại đúng trang hẹn xem hoặc đặt phòng, giữ query string; hủy lịch và mở trang đặt cọc đã duyệt.
- [x] `GuestPortalFlowTests` và `SessionRevalidationTests`: 5/5 đạt trên DB `_Test`.
- [x] Review code và kiểm tra giao diện danh sách/đăng nhập trên bản xem thử; giữ nguyên schema. Lỗi thông báo chờ duyệt sau hủy đã được tái hiện bằng test và sửa; 5/5 test liên quan đạt sau sửa.
- [x] Build solution thành công; `git diff --check` đạt, không có thay đổi schema hoặc migration.

## Sửa luồng tài khoản khách thuê — 08/10/2026

- [x] Tái hiện bằng trình duyệt và test: tài khoản KhachThue bấm hẹn xem bị chặn role, GET đăng nhập bỏ ReturnUrl và đưa về Dashboard.
- [x] Cho KhachThue và KhachVangLai dùng các trang hẹn xem, đặt phòng, đặt cọc và theo dõi yêu cầu riêng; GET đăng nhập giữ ReturnUrl nội bộ cho khách đã đăng nhập.
- [x] Tạo/liên kết hồ sơ người yêu cầu từ người thuê hiện có khi gửi yêu cầu đầu tiên, giữ role tài khoản, dùng bảng hiện có trong cùng giao dịch.
- [x] Kiểm thử hai loại khách, đặt phòng trực tiếp trước lịch xem, hủy, trang đặt cọc/quyền gửi minh chứng và bảo toàn role: 7/7 HTTP/session đạt. Store/concurrency giữ chỗ: 11/11 đạt trên DB `_Test`.
- [x] Build solution thành công (12 cảnh báo, không lỗi); review không có lỗi cần xử lý. Trình duyệt với phiên KhachThue thực tế mở được form hẹn xem và trang đặt phòng riêng, không về Dashboard. Chỉ mở form, không gửi yêu cầu thay khách.

## Đồng bộ giao diện với admin — 10/10/2026

- [x] Đối chiếu admin: Inter, logo SYSTEM RENTAL, màu primary Tabler `#066fd1`, nền `#f9fafb`, thẻ trắng `#ffffff`, chữ `#374151`, đường viền `#e5e7eb`, chữ phụ `#606b7a`. CSS lấy các token Tabler hiện có thay vì giữ bảng màu xanh ngọc cũ.
- [x] Thiết kế: header chung → nội dung căn trái → bộ lọc/phòng; form dùng hai cột thông tin và thao tác, chuyển một cột trên điện thoại. Ảnh phòng là điểm nhấn, bỏ hình trang trí cũ; chỉ đánh số khi biểu diễn thứ tự xem phòng/hẹn xem/giữ chỗ.
- [x] Thống nhất danh sách, chi tiết, hẹn xem, giữ chỗ, đặt cọc/minh chứng, theo dõi yêu cầu, đăng ký và đăng nhập từ cổng khách. Trang đăng nhập quản trị thông thường giữ giao diện hiện có.
- [x] Trạng thái dùng nhãn có chữ; hủy dùng nút cảnh báo. Focus bàn phím rõ, hỗ trợ reduced motion. Sửa header bị co và che menu trên điện thoại.
- [x] Header đăng ký/đăng nhập giữ ReturnUrl nội bộ, kể cả khi đăng ký có lỗi và hiển thị lại. Kiểm thử HTTP đi theo link đăng ký thật từ header.
- [x] Build solution: 0 lỗi, 26 cảnh báo; bộ GuestPortalFlowTests/SessionRevalidationTests: 7/7 đạt trên `_Test`. Review xác nhận không còn lỗi cụ thể trong sửa ReturnUrl.
- [x] Rà trình duyệt desktop 1366px và điện thoại 390px. Các form có đăng nhập được rà từ HTML Razor xuất bởi kiểm thử `_Test`, không tạo yêu cầu trên DB chính. Preview dùng QuanLyPhongTroDb, tắt initialize/seed và background jobs.
- [x] Không sửa entity, DbContext, schema hoặc migration.

## Dùng đăng nhập gốc — 16/10/2026

- [x] Theo yêu cầu mới, bỏ nhánh Razor riêng cho cổng khách; DangNhap.cshtml trở về nguyên trạng giao diện có sẵn. Không tạo controller, service hay chức năng xác thực mới.
- [x] Gỡ partial _GuestLogin, script khach-vang-lai-auth và CSS chỉ dành cho form riêng; giữ trang đăng ký và các tiện ích tìm phòng.
- [x] Giữ endpoint /QuanLyNhaTro/DangNhap, antiforgery và ReturnUrl của trang phòng. Thêm 4 ca kiểm thử GET/POST không hợp lệ đảm bảo cùng form gốc và giữ tham số, kể cả viewingId.
- [x] Test đỏ ban đầu: 4/4 không tìm thấy loginForm. Sau sửa: 12/12 HTTP (login regression, guest flow, session, discovery) đạt trên _Test. Build Web thành công; không thay đổi schema/database chính.

## Khám phá phòng không bắt đăng nhập — 16/10/2026

- [x] Khách chưa đăng nhập mở `/` vào `/phong`; danh sách, thông tin/ảnh, cẩm nang và phòng đã lưu được xem công khai. Khách đăng xuất trở về danh sách phòng; luồng nhân viên giữ nguyên.
- [x] GET hẹn xem/đặt phòng của khách chưa đăng nhập chỉ hiển thị tóm tắt phòng và lời nhắc. Chỉ nút Đăng nhập mở form gốc; ReturnUrl giữ đúng thao tác và query string.
- [x] POST gửi yêu cầu, dữ liệu lịch và trang tài khoản vẫn kiểm tra đăng nhập/quyền khách; trang nhắc công khai không tải khung giờ hay dữ liệu riêng.
- [x] Tái hiện lỗi trước sửa bằng 4 ca đỏ. Sau sửa và rà cuối: 13/13 HTTP tests đạt trên DB `_Test`, gồm luồng hai loại khách, login gốc, root công khai, session và discovery. `git diff --check` đạt; không đổi Domain/Infrastructure, schema hoặc migration.
- [x] Review không phát hiện lỗi cần sửa. Kiểm tra trình duyệt bản chạy mới: `/` → `/phong` → chi tiết phòng → lời nhắc hẹn xem → bấm Đăng nhập mở đúng form gốc với ReturnUrl. Preview dùng QuanLyPhongTroDb, tắt initialize/seed và background jobs; không gửi yêu cầu trên DB chính.

## Sửa lưu tin và tải ảnh — 22/10/2026

- Nối modal Tin phòng với POST có antiforgery; dùng tiêu đề, mô tả, cờ đăng tin và bảng anh_phong_tro hiện có. Không đổi schema/migration.
- Tải JPEG/PNG/WebP lên Cloudinary có sẵn; kiểm chữ ký tệp, tối đa 5 MB/ảnh và 10 ảnh đang hoạt động/phòng. Ảnh mới bổ sung vào ảnh cũ, ảnh đầu làm đại diện nếu chưa có.
- Giới hạn theo chi nhánh, khóa bản ghi phòng khi lưu để bảo vệ giới hạn ảnh/ảnh đại diện; lỗi tải/lưu không ghi tin dở và dọn ảnh tải dở.
- Test HTTP chỉ dùng DB Test và fake storage; kiểm tải thật qua multipart, CSRF, sai nội dung ảnh, giới hạn 10 ảnh, phòng ngoài phạm vi và hiển thị ảnh ở trang công khai.
