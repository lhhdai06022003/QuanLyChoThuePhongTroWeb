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
