# Public Room Discovery and Viewing Requests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Khách vãng lai tìm phòng công khai, gửi lịch xem và dùng liên kết email một lần để theo dõi yêu cầu; nhân viên đúng chi nhánh xử lý lịch xem.

**Architecture:** Phần công khai có controller và layout riêng, chỉ đọc phòng được đăng tin. Yêu cầu xem phòng và quyền nhân viên theo chi nhánh nằm trong PostgreSQL; liên kết email đổi thành cookie khách vãng lai ngắn hạn và được đánh dấu đã sử dụng. Dịch vụ nghiệp vụ kiểm tra quyền và trạng thái, controller chỉ nhận yêu cầu và trả giao diện.

**Tech Stack:** ASP.NET Core MVC .NET 8, EF Core/Npgsql 8, PostgreSQL, Razor, MailKit, xUnit.

**Design source:** docs/superpowers/specs/2026-09-17-room-reservation-design.md. Đây là lát cắt đầu tiên có thể chạy độc lập. Kế hoạch tiếp theo xử lý giữ chỗ và thanh toán; kế hoạch cuối xử lý chuyển tiền giữ chỗ vào cọc hợp đồng và hoàn tiền.

---


## Trạng thái rà soát 20/09/2026

Đây là kế hoạch phát triển chưa triển khai. PhongTro chưa có thuộc tính đăng tin, Domain chưa có YeuCauXemPhong/NhanVienChiNhanh; area KhachVangLai chỉ có khung. Các đường dẫn Create là dự kiến, không phải file đã tồn tại. Phạm vi hiện tại chỉ đồng bộ tài liệu, không thực thi checklist hoặc tạo migration.

### Điều chỉnh kiến trúc áp dụng cho toàn bộ task

- Domain chứa entity/quy tắc thuần; Application chứa DTO, use case và store interfaces. Web dùng DTO, không nhận/trả Domain entity.
- PublicRoomVisibility và test quy tắc thuần nên nằm ở Domain/Rules/PublicRoomVisibility.cs và tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/PublicRoomVisibilityTests.cs; đường dẫn Domain có tiền tố src/QuanLyChoThuePhongTroWeb.Domain. Không thêm external package vào Domain.
- Application/Features/PublicRooms/DTOs chứa PublicRoomSearch/PublicRoomCard; Services chứa IPublicRoomService/PublicRoomService; Persistence chứa IPublicRoomStore. Các đường dẫn Application có tiền tố src/QuanLyChoThuePhongTroWeb.Application.
- ViewingRequests có DTOs, Services và Persistence/IViewingRequestStore.cs trong Application/Features. Store triển khai ở src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/ViewingRequestStore.cs; xác minh token và chuyển trạng thái nguyên tử tại store/transaction.
- Interface token đặt tại src/QuanLyChoThuePhongTroWeb.Application/Abstractions/Security/IViewingTokenService.cs; sinh ngẫu nhiên/hash ở Infrastructure/Security/ViewingTokenService.cs. Cookie, claim, antiforgery và rate limit chỉ ở Web.
- DTO đăng/sửa phòng hiện có tại Application/Features/PhongTros/DTOs và store PhongTroStore ở Infrastructure/Persistence/Features cũng cần cập nhật khi thêm thuộc tính đăng tin.
- Bổ sung quyền chi nhánh qua store và use case Application; không dùng role hoặc dữ liệu từ client làm bằng chứng duy nhất.
- DI nghiệp vụ tại src/QuanLyChoThuePhongTroWeb.Application/DependencyInjection.cs; store/token/email tại src/QuanLyChoThuePhongTroWeb.Infrastructure/DependencyInjection.cs. Program.cs chỉ cấu hình phần HTTP.
- Model/enum hóa đơn hiện có không được coi là đã hỗ trợ nháp hay tiền giữ chỗ. Lát cắt này không sửa quy trình thu hóa đơn.

### Điều kiện thực thi và kiểm thử

Bốn project test đã tồn tại. Unit test Application dùng store giả; kiểm thử EF filtering, transaction, token dùng một lần dưới tải cạnh tranh và migration thuộc Infrastructure.IntegrationTests. Kiểm thử cookie, antiforgery, rate limit và truy cập chéo yêu cầu thuộc Web.IntegrationTests. Token hash SHA256 trong kế hoạch là hash token ngẫu nhiên, không phải cách hash mật khẩu.

Full suite cần QLCTPT_TEST_CONNECTION_STRING với DB riêng có hậu tố _test hoặc _integration_test. EF CLI cần dotnet-ef 8 và QLCTPT_DESIGNTIME_CONNECTION_STRING (hoặc ConnectionStrings__DefaultConnection). Mọi lệnh migration phải chỉ rõ cả --project và --startup-project là src/QuanLyChoThuePhongTroWeb.Infrastructure. Refactor không đổi schema; các migration tính năng dưới đây phải được review và kiểm thử riêng trên DB test trước khi áp vào DB phát triển.

## File map

| File | Trách nhiệm |
| --- | --- |
| src/QuanLyChoThuePhongTroWeb.Domain/Entities/PhongTro.cs; src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml | Cờ đăng tin, ảnh đại diện và thao tác đăng tin |
| src/QuanLyChoThuePhongTroWeb.Domain/Entities/YeuCauXemPhong.cs | Dữ liệu, trạng thái và token truy cập lịch xem |
| src/QuanLyChoThuePhongTroWeb.Domain/Entities/NhanVienChiNhanh.cs | Phân công nhân viên theo chi nhánh |
| src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/ApplicationDbContext.cs; src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations/ | Ánh xạ, chỉ mục và thay đổi database |
| src/QuanLyChoThuePhongTroWeb.Application/Features/PublicRooms/Services/PublicRoomService.cs | Điều phối tìm phòng qua IPublicRoomStore; EF filtering và phí dịch vụ ở Infrastructure |
| src/QuanLyChoThuePhongTroWeb.Application/Features/ViewingRequests/Services/ViewingRequestService.cs | Tạo, xác minh và xử lý lịch xem |
| src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachVangLai/Controllers/TimPhongController.cs | Trang công khai, tạo yêu cầu và đổi liên kết email lấy cookie |
| src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/LichXemPhongController.cs | Danh sách và duyệt lịch xem trong chi nhánh |
| src/QuanLyChoThuePhongTroWeb.Infrastructure/ExternalServices/Emails/MailKitEmailService.cs | Gửi liên kết xác minh |
| src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachVangLai/Views/TimPhong/; src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachVangLai/Views/Shared/_PublicLayout.cshtml | Giao diện khách tìm phòng |
| src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/LichXemPhong/ | Giao diện nhân viên |
| tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ | Kiểm thử quy tắc và luồng nghiệp vụ |

## Phân công gợi ý cho hai người

| Phụ trách | Task chính | Điểm tích hợp |
| --- | --- | --- |
| Người A: dữ liệu và nghiệp vụ | 1, 2, 3, 5, phần quyền của 7 | Chốt DTO/API trước khi Người B làm giao diện |
| Người B: giao diện và giao tiếp khách | 4, phần email/cookie của 6, giao diện của 7 | Dùng hợp đồng dữ liệu từ Task 3 và 5 |
| Cả hai | Kiểm thử Task 8 | Kiểm tra chéo luồng khách và nhân viên |

Task 1-3 là phụ thuộc tuần tự; sau khi thống nhất PublicRoomCard và model yêu cầu xem, có thể làm giao diện và email song song.

## Task 1: Viết kiểm thử đầu tiên cho quy tắc đăng phòng

**Files:** Create tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/PublicRoomVisibilityTests.cs; Create src/QuanLyChoThuePhongTroWeb.Domain/Rules/PublicRoomVisibility.cs.

- [x] Nền tảng hiện có: bốn project test theo tầng đã nằm trong solution. Đây chỉ là xác nhận cấu trúc; chưa đánh dấu kiểm thử tính năng tìm phòng là hoàn thành.
- [ ] Viết kiểm thử cho bốn trường hợp: phòng được đăng và trống; không được đăng; đã thuê; đã xóa mềm.
- [ ] Chạy dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/QuanLyChoThuePhongTroWeb.Domain.UnitTests.csproj; mong đợi kiểm thử mới thất bại vì PublicRoomVisibility chưa tồn tại.
- [ ] Tạo src/QuanLyChoThuePhongTroWeb.Domain/Rules/PublicRoomVisibility.cs để chỉ cho hiển thị phòng được đăng, còn trống và chưa bị xóa.
- [ ] Chạy lại dotnet test; mong đợi bốn trường hợp đạt. Commit riêng quy tắc đăng phòng và kiểm thử.

## Task 2: Thêm thông tin đăng phòng và migration

**Files:** Modify src/QuanLyChoThuePhongTroWeb.Domain/Entities/PhongTro.cs, src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/PhongTrosController.cs, src/QuanLyChoThuePhongTroWeb.Application/Features/PhongTros/Services/PhongTroService.cs, src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml; Create src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations/ (EF Core generates AddPublicRoomListing migration and designer).

- [ ] Thêm vào PhongTro:

~~~csharp
public bool DuocDangTin { get; set; } = false;
public string? AnhDaiDienUrl { get; set; }
~~~

- [ ] Thêm công tắc đăng tin và ảnh đại diện vào form quản lý phòng hiện có; service thêm/sửa phòng lưu hai trường này. Chỉ phòng trạng thái Trống mới được bật đăng tin. Tạo migration bằng dotnet ef migrations add AddPublicRoomListing --project src/QuanLyChoThuePhongTroWeb.Infrastructure --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure; xem lại để cột mới có mặc định false và các phòng cũ không tự xuất hiện công khai.
- [ ] Chạy dotnet build QuanLyChoThuePhongTroWeb.sln; mong đợi 0 lỗi. Chạy dotnet ef database update --project src/QuanLyChoThuePhongTroWeb.Infrastructure --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure trên database phát triển và kiểm tra dữ liệu phòng cũ còn nguyên.
- [ ] Commit model và migration; không commit appsettings.json.

## Task 3: Dịch vụ tìm phòng công khai

**Files:** Create src/QuanLyChoThuePhongTroWeb.Application/Features/PublicRooms/Services/IPublicRoomService.cs, PublicRoomService.cs; Create src/QuanLyChoThuePhongTroWeb.Application/Features/PublicRooms/DTOs/PublicRoomSearch.cs, PublicRoomCard.cs; Create src/QuanLyChoThuePhongTroWeb.Application/Features/PublicRooms/Persistence/IPublicRoomStore.cs; Create src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/PublicRoomStore.cs; Modify src/QuanLyChoThuePhongTroWeb.Application/DependencyInjection.cs, src/QuanLyChoThuePhongTroWeb.Infrastructure/DependencyInjection.cs; Test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/PublicRoomSearchTests.cs.

- [ ] Viết kiểm thử cho lọc giá, chi nhánh và sức chứa. Ví dụ dữ liệu 3 phòng, tìm giá tối đa 3.000.000 và tối thiểu 2 người chỉ trả phòng phù hợp.
- [ ] Chạy dotnet test; mong đợi thất bại vì dịch vụ lọc chưa có.
- [ ] Tạo DTO và interface:

~~~csharp
public sealed record PublicRoomSearch(int? ChiNhanhId, double? GiaToiDa, int? SoNguoiToiThieu);
public sealed record PublicRoomCard(int Id, string SoPhong, string TenChiNhanh,
    double GiaThue, double DienTich, int SoNguoiToiDa, string? AnhDaiDienUrl, IReadOnlyList<string> PhiDichVu);
public interface IPublicRoomService
{
    Task<IReadOnlyList<PublicRoomCard>> SearchAsync(PublicRoomSearch search);
    Task<PublicRoomCard?> GetByIdAsync(int roomId);
}
~~~

- [ ] Trong PublicRoomStore ở Infrastructure, tạo truy vấn EF với điều kiện đăng tin, trạng thái trống, không xóa, lọc giá/chi nhánh/sức chứa. Trong PublicRoomStore, bắt đầu truy vấn AsNoTracking từ PhongTros, Include ChiNhanh, lọc DuocDangTin, TrangThai.Trong, !IsDeleted và !ChiNhanh.IsDeleted; chỉ sau đó áp bộ lọc và Select sang PublicRoomCard. PhiDichVu lấy từ bảng giá dịch vụ còn hiệu lực của ChiNhanh và chỉ gồm tên, đơn vị, giá công khai. GetByIdAsync áp cùng quy tắc; phòng không công khai trả null.
- [ ] PublicRoomService gọi IPublicRoomStore; đăng ký service trong Application/DependencyInjection.cs và store trong Infrastructure/DependencyInjection.cs (hai project dưới src/). Không dùng EF hoặc IQueryable trong Application. Chạy dotnet test và dotnet build; mong đợi 0 lỗi. Commit dịch vụ.

## Task 4: Trang tìm phòng

**Files:** Create src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachVangLai/Controllers/TimPhongController.cs; Create src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachVangLai/Views/Shared/_PublicLayout.cshtml; Create src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachVangLai/Views/TimPhong/Index.cshtml and Details.cshtml; Modify src/QuanLyChoThuePhongTroWeb.Web/Views/Home/Index.cshtml; Test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/PublicRoomControllerTests.cs.

- [ ] Viết kiểm thử controller: trang chi tiết của phòng chưa đăng tin trả NotFound; phòng công khai trả View.
- [ ] Chạy kiểm thử, mong đợi thất bại.
- [ ] Controller có [AllowAnonymous], GET /tim-phong đọc bộ lọc, GET /tim-phong/{id:int} chỉ lấy dữ liệu qua IPublicRoomService. Không trả entity EF trực tiếp ra giao diện.
- [ ] Trang Index có bộ lọc giá, chi nhánh, sức chứa và card phòng; Details có ảnh, giá, thông tin liên hệ, nút yêu cầu xem. _PublicLayout không hiển thị menu nội bộ. Home/Index dẫn đến /tim-phong.
- [ ] Chạy dotnet build, dotnet test và mở trang trên màn hình điện thoại; kiểm tra phòng không đăng tin không xuất hiện. Commit trang công khai.

## Task 5: Yêu cầu xem phòng và liên kết email một lần

**Files:** Create src/QuanLyChoThuePhongTroWeb.Domain/Entities/YeuCauXemPhong.cs; Modify src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/ApplicationDbContext.cs; Create src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations/ (EF Core generates AddViewingRequests migration); Create src/QuanLyChoThuePhongTroWeb.Infrastructure/Security/ViewingTokenService.cs; Test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/ViewingTokenTests.cs.

- [ ] Model lưu PhongTroId, HoTen, Email, SoDienThoai, ThoiGianMongMuonUtc, TrangThai, TokenHash, TokenHetHanUtc, TokenDaDungUtc, NgayTaoUtc; trạng thái gồm ChoXacMinhEmail, ChoXacNhan, DaXacNhan, TuChoi, DaHuy. Chỉ mục cho PhongTroId/TrangThai và TokenHash.
- [ ] Viết kiểm thử: token thô khác hash, token không hợp lệ bị từ chối, token quá hạn bị từ chối và token đã dùng không được dùng lại.
- [ ] Chạy test, mong đợi thất bại; sau đó tạo token bằng RandomNumberGenerator.GetBytes(32), mã hóa Base64Url, chỉ lưu SHA256 hash; đặt thời hạn 24 giờ. Đối chiếu hash bằng CryptographicOperations.FixedTimeEquals.
- [ ] Tạo migration, chạy dotnet build và dotnet test; kiểm tra migration trên database phát triển. Commit model/token/migration.

## Task 6: Gửi email và theo dõi yêu cầu

**Files:** Create src/QuanLyChoThuePhongTroWeb.Application/Features/ViewingRequests/Services/IViewingRequestService.cs and ViewingRequestService.cs; Modify src/QuanLyChoThuePhongTroWeb.Application/Abstractions/Services/IEmailService.cs and EmailService.cs; Modify src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachVangLai/Controllers/TimPhongController.cs, src/QuanLyChoThuePhongTroWeb.Web/Program.cs; Create src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachVangLai/Views/TimPhong/TrangThai.cshtml; Test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ViewingRequestTests.cs.

- [ ] Viết kiểm thử: yêu cầu phòng không công khai bị từ chối; email sai định dạng bị từ chối; xác minh token chỉ thành công một lần; khách chỉ xem được yêu cầu của mình.
- [ ] Chạy test, mong đợi thất bại.
- [ ] Tạo yêu cầu ở trạng thái ChoXacMinhEmail và gửi liên kết qua IEmailService.SendViewingLinkAsync. Không ghi token thô vào database hay log. Nếu gửi email lỗi, giữ trạng thái chưa xác minh và cho gửi lại có giới hạn.
- [ ] GET /tim-phong/xac-minh/{token} đổi token hợp lệ lấy cookie guest riêng có Claim ViewingRequestId, HttpOnly, Secure và hạn ngắn; đánh dấu token đã dùng trong cùng giao dịch. GET /tim-phong/yeu-cau chỉ đọc ID từ cookie, không tin ID từ query. Form tạo yêu cầu dùng antiforgery và giới hạn tần suất.
- [ ] Chạy test, build, thử liên kết thật trên môi trường phát triển, thử mở lại token và thử dùng cookie của yêu cầu A xem yêu cầu B; mong đợi bị từ chối. Commit email và luồng theo dõi.

## Task 7: Nhân viên chi nhánh xử lý lịch xem

**Files:** Create src/QuanLyChoThuePhongTroWeb.Domain/Entities/NhanVienChiNhanh.cs; Modify src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/ApplicationDbContext.cs, src/QuanLyChoThuePhongTroWeb.Web/Program.cs; Create src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations/ (EF Core generates AssignEmployeesToBranches migration); Create src/QuanLyChoThuePhongTroWeb.Application/Features/ViewingRequests/Services/IStaffViewingService.cs and StaffViewingService.cs; Create src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/LichXemPhongController.cs; Create src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/LichXemPhong/Index.cshtml; Modify src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/NguoiDungController.cs and src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/NguoiDung/QuanLyTaiKhoanDangNhap.cshtml; Test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/StaffViewingTests.cs.

- [ ] Tạo ánh xạ NguoiDungId/ChiNhanhId duy nhất. Admin xem mọi chi nhánh; nhân viên chỉ xem và xử lý chi nhánh được phân công. Bổ sung thao tác gán chi nhánh vào màn hình tài khoản hiện có; chỉ Admin được thay đổi phân công, có antiforgery. Không suy đoán quyền chỉ từ role.
- [ ] Viết kiểm thử: nhân viên A không xem hoặc xác nhận yêu cầu chi nhánh B; Admin được xem; lịch đã từ chối không được xác nhận lại.
- [ ] Chạy test, mong đợi thất bại; triển khai IStaffViewingService kiểm tra quyền trước truy vấn/chuyển trạng thái. Controller kế thừa AdminBaseController, có antiforgery cho POST và trả lỗi rõ khi trạng thái đã đổi.
- [ ] Chạy dotnet test và dotnet build; thử hai tài khoản nhân viên ở hai chi nhánh trên database phát triển. Commit quyền và màn hình xử lý.

## Task 8: Kiểm tra tích hợp và bàn giao lát cắt

- [ ] Chạy dotnet test QuanLyChoThuePhongTroWeb.sln và dotnet build QuanLyChoThuePhongTroWeb.sln; mong đợi 0 lỗi, 0 test thất bại.
- [ ] Thử: đăng phòng → tìm phòng → gửi lịch → mở email → theo dõi trạng thái → nhân viên xác nhận → khách thấy lịch đã xác nhận. Thử URL token cũ, phòng bị ẩn, email gửi lỗi và quyền sai chi nhánh.
- [ ] Kiểm tra migration từ database có dữ liệu cũ và giao diện điện thoại; ghi kết quả trong PR. Commit sửa lỗi nếu có.
- [ ] Sau khi lát cắt này ổn định, lập kế hoạch riêng cho yêu cầu giữ chỗ, xác nhận chuyển khoản và ràng buộc một phòng; kế hoạch thứ ba nối tiền giữ chỗ vào cọc hợp đồng, hủy và hoàn tiền.

## Điều kiện hoàn thành

Khách xem được danh sách phòng đăng tin, lọc và gửi lịch xem; email chỉ dùng một lần để đăng nhập vào phiên theo dõi; nhân viên đúng chi nhánh xác nhận hoặc từ chối; không lộ phòng ẩn hoặc yêu cầu của người khác. Các kiểm thử và lệnh build đạt, migration áp dụng được trên dữ liệu hiện có.
