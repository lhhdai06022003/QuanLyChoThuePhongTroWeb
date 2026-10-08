# Cổng khách vãng lai Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cho khách đăng ký, xem phòng, đặt lịch, giữ chỗ và theo dõi khoản cọc/hoàn với dữ liệu thật trên schema `master` hiện có.

**Architecture:** Application chứa quy tắc và interface store theo từng luồng; Infrastructure dùng `ApplicationDbContext` và giao dịch PostgreSQL; Web nối các Razor view hiện có với service, phân quyền và antiforgery. Tiền chỉ được ghi nhận qua đối soát nhân viên. Giữ giao diện `/phong` hiện tại.

**Tech Stack:** .NET 8, ASP.NET Core MVC/Razor, EF Core PostgreSQL, MailKit hiện có, xUnit.

**Spec:** [SPEC.md](SPEC.md)

## Tiến độ kiểm chứng 2026-10-08

- Đã nối giao diện và mã xử lý cho đăng ký/đăng nhập khách, lịch xem, giữ chỗ, ảnh minh chứng riêng tư, đối soát nhiều giao dịch, áp dụng vào cọc hợp đồng, quyết định/ghi nhận hoàn tiền và email nhân viên gửi sau khi xem trước.
- Đã bổ sung nhả phòng khi hết hạn chuyển tiền, gia hạn hoặc hủy sau hạn ký hợp đồng, và dùng thời điểm ngân hàng thực nhận để phân loại khoản đến muộn. Bản xem thử dùng `QuanLyPhongTroDb` với `Database:InitializeOnStartup=false`.
- `dotnet build` thành công; Domain 122/122, Application 813/813, Web integration 234/234, Infrastructure integration 302/302 khi loại 2 phép đếm số bảng. Hai phép đếm đó kỳ vọng 37 bảng, trong khi DB `_Test` hiện có 38 bảng; không sửa schema để chiều theo DB thử.
- Chưa gửi thử email qua SMTP thật. Việc kiểm tra tay toàn bộ chuỗi thao tác với dữ liệu mẫu trong DB `_Test` vẫn cần thực hiện trước khi phát hành.

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
