# Repository Guidelines

## Project Structure & Module Organization

Hệ thống được tổ chức chuẩn mực theo **Clean Architecture 4 tầng**:

### 1. Production Projects (`src/`)
- `src/QuanLyChoThuePhongTroWeb.Domain/`: Tầng cốt lõi chứa Entities, Enums, Value Objects. **Không phụ thuộc vào bất kỳ project hay external package nào.**
- `src/QuanLyChoThuePhongTroWeb.Application/`: Tầng nghiệp vụ ứng dụng chứa Features (UseCases, Services, DTOs), Common Models, Abstractions (Persistence Stores, Security, Notifications). **Chỉ phụ thuộc Domain. Hoàn toàn độc lập với ASP.NET Core, EF Core, và IConfiguration.**
- `src/QuanLyChoThuePhongTroWeb.Infrastructure/`: Tầng hạ tầng chứa `ApplicationDbContext`, EF Core Migrations, Store Implementations (`I*Store`), `UnitOfWork`, Security services (PBKDF2 Password Hashing), External Services (Email, Cloudinary, AI, Export), và `DatabaseInitializer`. **Phụ thuộc Application và Domain; không phụ thuộc Web.**
- `src/QuanLyChoThuePhongTroWeb.Web/`: Tầng giao diện người dùng ASP.NET Core MVC & Web API (`Areas/QuanLyNhaTro/`, `Areas/KhachThue/`, `Controllers/`, `Views/`, `wwwroot/`). **Phụ thuộc Application và Infrastructure; KHÔNG direct-reference Domain.** Giao tiếp qua Application DTOs và Services.

### 2. Test Projects (`tests/`)
- `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/`: Unit tests cho các business rules, constraints độc lập của Domain.
- `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/`: Unit tests cho Application UseCases, Services, Validation logic sử dụng Store/UoW mocks.
- `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/`: Integration tests cho EF Core mapping, Store queries, transaction safety, và migrations.
- `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/`: Integration tests cho HTTP endpoints, Architecture boundary enforcement, MVC routes, và SignalR.

## Build, Test, and Development Commands

Cài đặt .NET 8 SDK và PostgreSQL. Copy `appsettings.example.json` thành `appsettings.json` tại `src/QuanLyChoThuePhongTroWeb.Web/` và cấu hình connection string.

- `dotnet restore QuanLyChoThuePhongTroWeb.sln`: Khôi phục NuGet packages cho toàn solution.
- `dotnet build QuanLyChoThuePhongTroWeb.sln`: Biên dịch toàn bộ 4 production projects và 4 test projects.
- `dotnet test QuanLyChoThuePhongTroWeb.sln`: Chạy toàn bộ automated unit & integration test suites.
- `dotnet watch run --project src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj`: Chạy website với cơ chế hot-reload.
- Quản lý EF Core Migrations (yêu cầu cấu hình biến môi trường `QLCTPT_DESIGNTIME_CONNECTION_STRING` hoặc `ConnectionStrings__DefaultConnection`):
  - Liệt kê migrations: `dotnet ef migrations list --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj`
  - Kiểm tra pending changes: `dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj`
  - Cập nhật database: `dotnet ef database update --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj`

## Coding Style & Architecture Constraints

1. **Ranh giới Clean Architecture**:
   - `Web` KHÔNG ĐƯỢC reference trực tiếp `Domain` (`ProjectReference` hoặc `using Domain.*`).
   - `Application` KHÔNG ĐƯỢC reference `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, `IConfiguration`, hoặc các kiểu dữ liệu UI/HTTP.
   - Controllers chỉ đóng vai trò HTTP handling, validation và điều phối; toàn bộ xử lý nghiệp vụ phải đặt trong Application Services/UseCases.
2. **Naming Conventions**:
   - 4 spaces trong file C#, PascalCase cho classes/interfaces/methods, camelCase cho local variables/parameters.
   - Prefix `I` cho interfaces (ví dụ: `IChiNhanhService`, `IPhongTroStore`, `IUnitOfWork`).
3. **Database & Schema Protection**:
   - Schema PostgreSQL là nguồn chân lý bất biến trong các đợt refactoring (0% schema drift). Không sinh migration rác hoặc sửa migration đã có.

## Hiện trạng và cách làm việc (đối chiếu 20/09/2026)

- API v1 tại `src/QuanLyChoThuePhongTroWeb.Web/Api/V1/`, area `KhachVangLai` và `.github/workflows/` mới có khung. Không mô tả chúng là tính năng/CI đã chạy.
- Roadmap và docs/superpowers là yêu cầu phát triển; kiểm tra code trước khi đánh dấu hoàn thành. Hóa đơn hiện chưa có trạng thái nháp, Domain chưa có thực thể lịch xem/giữ chỗ.
- Kiểm tra `git status --short` trước khi sửa. Giữ nguyên thay đổi có sẵn của người dùng; không khôi phục file bị xóa hoặc ghi đè cấu hình cá nhân.
- Đọc feature tương ứng ở cả Application, Infrastructure và Web trước khi thay đổi. Không suy ra namespace chỉ từ đường dẫn: repository còn một số namespace cũ.
- Application có package Microsoft.Extensions.DependencyInjection.Abstractions và Logging.Abstractions. Ràng buộc “chỉ phụ thuộc Domain” áp dụng cho ProjectReference, không có nghĩa cấm hai package abstraction đang dùng.
- Domain hiện có Entities và Enums; chỉ tạo Value Objects khi có nhu cầu thực tế.

## Vị trí triển khai

- Nghiệp vụ: `Application/Features/<Feature>/Services`; DTO: `DTOs`; store interface: `Persistence` của feature.
- Abstraction dùng chung: `Application/Abstractions`; cấu hình dạng typed settings, kết quả service, phân trang và kiểu upload: `Application/Common`.
- EF query/store: `Infrastructure/Persistence/Features`; DbContext, transaction, UnitOfWork và migrations: `Infrastructure/Persistence`.
- Tích hợp: `Infrastructure/ExternalServices`; password: `Infrastructure/Security`; bộ lập lịch và lưu trạng thái job: `Infrastructure/BackgroundJobs`. Use case nghiệp vụ của job ở Application.
- DI: dùng `AddApplication` và `AddInfrastructure` tại DependencyInjection.cs tương ứng. Web/Program.cs là composition root cho HTTP, cookie, routing và SignalR.
- Web giao tiếp qua DTO/enums của Application; chuyển kiểu MVC tại Web. Không đưa IFormFile, SelectListItem hoặc EF IQueryable vào hợp đồng mới của Application.
- Dùng IPasswordService cho mật khẩu. Hash mới là PBKDF2; SHA256 chỉ để tương thích dữ liệu cũ và nâng cấp khi đăng nhập.
- Giữ quy tắc xóa mềm của từng thực thể, kiểm tra cả dữ liệu liên quan; không giả định mọi bảng đều có IsDeleted. Giữ quy ước UTC và kiểm tra chuyển múi giờ tại ranh giới hiển thị.

## Môi trường và kiểm chứng

- Không ghi đè appsettings.json đã có. Mẫu cấu hình nằm trong Web; không đưa secrets vào tài liệu, log hoặc commit.
- Web khởi động sẽ tự migrate và seed admin qua DatabaseInitializer. BackgroundJobs:Enabled=false chỉ tắt hosted jobs, không tắt initializer.
- Hai project unit test có thể chạy riêng bằng `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests` và `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests`.
- Full suite cần `QLCTPT_TEST_CONNECTION_STRING` trỏ vào PostgreSQL riêng, tên database kết thúc bằng `_test` hoặc `_integration_test`. Fixtures migrate schema; không dùng DB phát triển/production. Thiếu cấu hình là lỗi, không phải test đã pass.
- Chọn kiểm thử theo thay đổi: Domain rules, Application với store/UoW giả, Infrastructure với PostgreSQL, Web với HTTP/architecture/SignalR. Chỉ thay tài liệu thì đối chiếu đường dẫn, lệnh và git diff; không tuyên bố đã chạy test nếu chưa chạy.
- Trước bàn giao kiểm tra `git diff --check`, diff và phạm vi thay đổi. Báo rõ lệnh đã chạy và giới hạn kiểm chứng.
- Mọi refactor giữ 0% schema drift. Tính năng mới thực sự cần schema phải có thiết kế thay đổi rõ ràng, migration mới được review và thử trên DB test; không tự áp roadmap vào database trong tác vụ cập nhật tài liệu.
