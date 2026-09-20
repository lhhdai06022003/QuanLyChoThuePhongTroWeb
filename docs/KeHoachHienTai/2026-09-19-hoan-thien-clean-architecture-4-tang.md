# Hoàn thiện Clean Architecture 4 tầng Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Hoàn thiện ranh giới Domain, Application, Infrastructure và Web để project references, framework dependencies, HTTP contracts, persistence và tests tuân thủ Clean Architecture mà không đổi hành vi hoặc database schema.

**Architecture:** Domain giữ C# thuần. Application giữ use case, DTO và các persistence/provider port thuần; Application không tham chiếu EF Core hoặc ASP.NET Core. Infrastructure triển khai persistence, password hashing, storage, email, scheduler và startup database operation. Web chỉ làm presentation/composition, giao tiếp với Application qua DTO và abstraction.

**Tech Stack:** .NET 8, ASP.NET Core MVC, EF Core 8, PostgreSQL/Npgsql, xUnit, SignalR, MailKit, Cloudinary, QuestPDF, ClosedXML, DocX, QRCoder.

---

## 1. Phạm vi và quyết định kiến trúc

### Những điểm đã xác nhận cần xử lý

1. Application đang tham chiếu Microsoft.EntityFrameworkCore và Microsoft.AspNetCore.App.
2. IApplicationDbContext đang expose DbSet<T>; 17 service/use case trong Application dùng EF Core extensions.
3. Application còn dùng IFormFile, SelectListItem, IPasswordHasher<T> và IConfiguration.
4. Web còn tham chiếu EF Core, Npgsql và Domain trực tiếp.
5. Program.cs của Web gọi ApplicationDbContext.Database.Migrate().
6. Tests hiện chỉ có một UnitTests project hỗn hợp và một IntegrationTests project chưa có test source.
7. Cấu trúc test chưa đạt bốn project theo mục tiêu.
8. AGENTS.md đã được cập nhật sơ bộ; cần cập nhật lần cuối sau khi test projects và dependency thay đổi.

### Quyết định bắt buộc

- Dùng feature-specific persistence ports, không tạo generic repository cho mọi entity.
- Application service giữ orchestration nghiệp vụ; EF queries và tracking nằm trong Infrastructure.
- Dùng IUnitOfWork thuần cho SaveChangesAsync và transaction dùng chung.
- Không trả IQueryable, DbSet, EntityEntry, EF expression hoặc provider-specific type qua Application boundary.
- Public Application service contracts dùng Application DTO/command/result. Web không nhận hoặc gửi Domain entity trực tiếp.
- Logging abstractions và DI abstractions được phép trong Application. EF Core, ASP.NET MVC/HTTP/Identity và IConfiguration không được phép.
- Không tạo EF migration mới. Không thay đổi table, column, index, constraint hoặc migration history.
- Mỗi feature được chuyển và kiểm tra riêng; không chuyển toàn bộ persistence trong một commit.

## 2. Dependency matrix mục tiêu

| Project | Được tham chiếu | Bị cấm |
|---|---|---|
| Domain | BCL | Application, Infrastructure, Web, EF, ASP.NET |
| Application | Domain, DI abstractions, logging abstractions | EF, ASP.NET, Infrastructure, Web, provider packages |
| Infrastructure | Application, Domain, EF, Npgsql, providers | Web |
| Web | Application, Infrastructure, ASP.NET presentation packages | Direct Domain project reference, EF/Npgsql packages |

Test projects được phép tham chiếu layer đang kiểm thử. Test references không thay đổi production dependency direction.

## 3. Cấu trúc đích

~~~text
src/
  QuanLyChoThuePhongTroWeb.Domain/
  QuanLyChoThuePhongTroWeb.Application/
    Abstractions/
      Persistence/
      Security/
      Services/
    Common/
      Configuration/
      Files/
      Models/
    Features/<Feature>/
      DTOs/
      Persistence/
      Services/
      UseCases/
  QuanLyChoThuePhongTroWeb.Infrastructure/
    Persistence/
      Features/<Feature>/
      Migrations/
      DatabaseInitializer.cs
    Security/
    BackgroundJobs/
    ExternalServices/
  QuanLyChoThuePhongTroWeb.Web/
    Areas/
    Api/
    Controllers/
    Mapping/
    ViewModels/
    Views/
    wwwroot/
tests/
  QuanLyChoThuePhongTroWeb.Domain.UnitTests/
  QuanLyChoThuePhongTroWeb.Application.UnitTests/
  QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/
  QuanLyChoThuePhongTroWeb.Web.IntegrationTests/
~~~

## 4. Global safety gates

Chạy các gate này ở đầu và cuối mỗi task có thay đổi production code:

~~~powershell
dotnet restore QuanLyChoThuePhongTroWeb.sln
dotnet build QuanLyChoThuePhongTroWeb.sln --no-restore
dotnet test QuanLyChoThuePhongTroWeb.sln --no-build
~~~

Gate EF trước và sau mọi task chạm persistence:

~~~powershell
dotnet ef migrations list --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj
~~~

Kết quả bắt buộc: migration list không đổi và “No changes have been made to the model since the last migration.”

---

### Task 0: Tạo môi trường triển khai sạch và chụp baseline

**Files:** chỉ đọc AGENTS.md, solution và project metadata.

- [ ] **Step 1: Không triển khai trên working tree đang có 2.000+ thay đổi**

Tạo worktree từ commit cuối của feature/clean-architecture. Không reset hoặc clean working tree hiện tại.

~~~powershell
git worktree add ..\QuanLyChoThuePhongTroWeb-clean-final feature/clean-architecture
~~~

Expected: worktree sạch ở commit 16febc6 hoặc branch codex/complete-clean-architecture tạo từ commit đó.

- [ ] **Step 2: Ghi baseline**

~~~powershell
git status --short
git log --oneline -20
dotnet list QuanLyChoThuePhongTroWeb.sln reference
dotnet list QuanLyChoThuePhongTroWeb.sln package
~~~

- [ ] **Step 3: Chạy global safety gates và EF gates**

Lưu output trong PR hoặc nhật ký thực thi, không commit file log.

**Risk:** Low.  
**Rollback:** xóa worktree mới; không tác động working tree hiện tại.

---

### Task 1: Tách bốn test project và khóa dependency

**Files:**
- Create: tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/QuanLyChoThuePhongTroWeb.Domain.UnitTests.csproj
- Create: tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/QuanLyChoThuePhongTroWeb.Application.UnitTests.csproj
- Create: tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.csproj
- Create: tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests.csproj
- Move HopDongRulesTests.cs vào Domain.UnitTests.
- Move HoaDonCalculatorServiceTests.cs, BackgroundJobLogicTests.cs và ArchitectureBoundaryTests.cs vào Application.UnitTests.
- Move VietQRServiceTests.cs và NguoiDungPasswordTests.cs vào Infrastructure.IntegrationTests.
- Replace tests/QuanLyChoThuePhongTroWeb.IntegrationTests bằng Web.IntegrationTests.
- Modify: QuanLyChoThuePhongTroWeb.sln.
- Delete hai test project cũ chỉ sau khi số test đã chuyển bằng baseline.

- [ ] **Step 1: Tạo project references**

Domain.UnitTests → Domain. Application.UnitTests → Application và Domain. Infrastructure.IntegrationTests → Infrastructure, Application và Domain. Web.IntegrationTests → Web.

- [ ] **Step 2: Sửa namespace test**

Dùng bốn namespace tương ứng tên project. Sửa namespace cũ QuanLyChoThuePhongTroWeb.Models thành Domain namespace hiện hữu.

- [ ] **Step 3: Mở rộng ArchitectureBoundaryTests**

Kiểm tra project reference và package/framework reference:

~~~text
Domain references: none
Application project references: Domain only
Infrastructure project references: Application + Domain
Web project references: Application + Infrastructure only
Application packages: no EF Core, no ASP.NET Core
Web packages: no EF Core, no Npgsql
~~~

- [ ] **Step 4: Add bốn projects vào solution và remove hai projects cũ**

- [ ] **Step 5: Verify và commit**

~~~powershell
dotnet test QuanLyChoThuePhongTroWeb.sln
git add QuanLyChoThuePhongTroWeb.sln tests
git commit -m "test: tach test projects theo bon tang"
~~~

**Risk:** Medium.  
**Rollback:** revert commit; production projects chưa thay đổi.

---

### Task 2: Loại ASP.NET contracts khỏi Application

**Files:**
- Create: src/QuanLyChoThuePhongTroWeb.Application/Common/Models/SelectOptionDto.cs
- Create: src/QuanLyChoThuePhongTroWeb.Application/Common/Files/UploadFile.cs
- Modify: Application/Abstractions/Services/IImageStorageService.cs
- Modify: PhongTros và NguoiThues service interfaces/implementations.
- Modify: Infrastructure/ExternalServices/Storage/CloudinaryStorageService.cs
- Modify: Web/Areas/KhachThue/Controllers/SuCoController.cs
- Create: Web/Mapping/SelectOptionMappingExtensions.cs
- Modify các controller/view-model đang dùng dropdown.

- [ ] **Step 1: Thêm dropdown DTO thuần**

~~~csharp
public sealed record SelectOptionDto(string Value, string Text);
~~~

Đổi các phương thức dropdown của IPhongTroService và INguoiThueService sang Task<IReadOnlyList<SelectOptionDto>>.

- [ ] **Step 2: Map DTO sang SelectListItem trong Web**

Web Mapping extension chuyển SelectOptionDto thành SelectListItem. Không để SelectListItem xuất hiện trong Application.

- [ ] **Step 3: Thay IFormFile bằng stream contract**

~~~csharp
public sealed record UploadFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);
~~~

IImageStorageService nhận UploadFile. Web mở IFormFile.OpenReadStream(); Cloudinary implementation đọc UploadFile.Content.

- [ ] **Step 4: Thêm focused tests**

Kiểm tra Value/Text của dropdown và storage adapter nhận MemoryStream mà không phụ thuộc ASP.NET type. Không gọi Cloudinary thật.

- [ ] **Step 5: Verify**

~~~powershell
rg -n "IFormFile|SelectListItem|Microsoft.AspNetCore.Mvc|Microsoft.AspNetCore.Http" src/QuanLyChoThuePhongTroWeb.Application
~~~

Expected: không có kết quả.

- [ ] **Step 6: Commit**

~~~powershell
git commit -am "refactor: loai aspnet contracts khoi application"
~~~

**Risk:** Medium.  
**Rollback:** revert commit; schema không bị ảnh hưởng.

---

### Task 3: Tách password hashing và configuration khỏi Application

**Files:**
- Create: Application/Abstractions/Security/IPasswordService.cs
- Create: Application/Common/Configuration/DashboardSettings.cs
- Create: Application/Common/Configuration/VietQrSettings.cs
- Create: Application/Common/Configuration/AutoReminderSettings.cs
- Create: Application/Common/Configuration/ContractAlertSettings.cs
- Create: Infrastructure/Security/AspNetPasswordService.cs
- Modify: NguoiDungService.cs và HopDongService.cs
- Modify: DashboardService.cs, HoaDonService.cs, InvoiceReminderUseCase.cs và ContractExpiryAlertUseCase.cs
- Modify Application và Infrastructure DependencyInjection.cs.

- [ ] **Step 1: Định nghĩa password port thuần**

~~~csharp
public enum PasswordCheckResult { Failed, Success, SuccessRehashNeeded }

public interface IPasswordService
{
    string Hash(NguoiDung user, string password);
    PasswordCheckResult Verify(
        NguoiDung user,
        string hashedPassword,
        string providedPassword);
}
~~~

- [ ] **Step 2: Chuyển Identity implementation sang Infrastructure**

AspNetPasswordService wrap PasswordHasher<NguoiDung>. Giữ chính xác PBKDF2 và nhánh legacy password hiện tại.

- [ ] **Step 3: Thay IConfiguration bằng immutable settings**

~~~csharp
public sealed record DashboardSettings(int ChotDienNuocDay = 5);
public sealed record VietQrSettings(
    string BankId,
    string AccountNumber,
    string AccountName);
public sealed record AutoReminderSettings(bool Enabled, int OverdueDays = 5);
public sealed record ContractAlertSettings(
    bool Enabled,
    IReadOnlyList<int> AlertDays,
    string? AdminEmail);
~~~

Bind config tại composition/Infrastructure rồi inject records vào Application. Application không gọi GetValue, GetSection hoặc config indexer.

- [ ] **Step 4: Verify**

~~~powershell
rg -n "IConfiguration|IPasswordHasher|PasswordHasher<|Microsoft.AspNetCore.Identity" src/QuanLyChoThuePhongTroWeb.Application
~~~

Expected: không có kết quả.

- [ ] **Step 5: Commit**

~~~powershell
git commit -am "refactor: tach security va configuration khoi application"
~~~

**Risk:** High vì authentication và job settings.  
**Rollback:** revert commit và chạy lại password/login regression tests.

---

### Task 4: Tạo persistence ports và chuyển nhóm ít coupling

**Files:**
- Create: Application/Abstractions/Persistence/IUnitOfWork.cs
- Retain: Application/Abstractions/Persistence/IApplicationTransaction.cs
- Create IChiNhanhStore.cs, IDieuKhoanMauStore.cs, IYeuCauSuCoStore.cs dưới feature tương ứng.
- Create matching implementations dưới Infrastructure/Persistence/Features/.
- Modify ba Application services và DI registrations.
- Modify Infrastructure/Persistence/ApplicationDbContext.cs để implement IUnitOfWork.

- [ ] **Step 1: Add IUnitOfWork**

~~~csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<IApplicationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);
}
~~~

- [ ] **Step 2: Tạo business-shaped ports**

Mỗi port chỉ expose list/get/add/update/remove/existence operations thực sự được service sử dụng. Không expose IQueryable, DbSet, EF predicate hoặc tracking API.

- [ ] **Step 3: Viết Application tests bằng fake stores**

Mỗi create/update/delete path kiểm tra result và số lần SaveChangesAsync.

- [ ] **Step 4: Implement Infrastructure stores**

Chuyển Include, AsNoTracking, FirstOrDefaultAsync, ToListAsync, AnyAsync và tracking operations khỏi Application. Giữ nguyên filter/order/include graph.

- [ ] **Step 5: Commit từng feature**

~~~text
refactor: tach persistence chi nhanh
refactor: tach persistence dieu khoan mau
refactor: tach persistence yeu cau su co
~~~

**Risk:** Medium.  
**Rollback:** revert từng feature commit.

---

### Task 5: Chuyển persistence nhóm feature trung bình

**Files:**
- Create Application ports và Infrastructure implementations cho DichVus, NguoiThues, PhongTros, ThanhVienHopDongs, ThongBaos và NguoiDungs.
- Modify matching Application services và DI registrations.

- [ ] **Step 1: Làm đúng một feature mỗi lần**

Thứ tự: DichVus → NguoiThues → PhongTros → ThanhVienHopDongs → ThongBaos → NguoiDungs.

- [ ] **Step 2: Bảo toàn hành vi**

Ports phải giữ pagination, filtering, ordering, uniqueness check, soft delete và include graph hiện tại. Không trả IQueryable.

- [ ] **Step 3: Giữ orchestration ở Application**

ThongBaoService điều phối save và IThongBaoNotifier. NguoiDungService dùng IPasswordService. Store chỉ đọc/ghi dữ liệu.

- [ ] **Step 4: Chạy global và EF gates, commit từng feature**

**Risk:** Medium–High.  
**Rollback:** revert feature gần nhất; migrations không thay đổi.

---

### Task 6: Chuyển persistence aggregate và query phức tạp

**Files:**
- Create Application ports và Infrastructure implementations cho HopDongs, HoaDons, DienNuocs, LichSuThanhToans và Dashboard.
- Modify matching services, use cases và DI.
- Add Application tests cho calculation, state transition, transaction và job selection.

- [ ] **Step 1: HopDong**

IHopDongStore sở hữu EF query graph và persistence commands. Application giữ quy tắc hợp đồng, thành viên, password và document workflow.

- [ ] **Step 2: HoaDon**

IHoaDonStore sở hữu read models, duplicate checks, detail graph, payment persistence và transaction operations. Application giữ calculation, generation workflow, exporter/VietQR invocation và result aggregation.

- [ ] **Step 3: DienNuoc và LichSuThanhToan**

Store giữ query/filter/aggregate database operations. Application giữ validation, kỳ chốt và state transition.

- [ ] **Step 4: Dashboard**

IDashboardReadStore trả materialized projections. DashboardSettings cung cấp ngày chốt.

- [ ] **Step 5: Background use cases**

ContractAutoCloseUseCase, ContractExpiryAlertUseCase và InvoiceReminderUseCase dùng feature store hoặc dedicated read store. Scheduler chỉ tạo scope và gọi use case.

- [ ] **Step 6: Transaction và idempotency tests**

Kiểm tra commit, rollback, notification/export failure và job chạy lại không tạo dữ liệu trùng.

- [ ] **Step 7: Commit per aggregate**

**Risk:** High.  
**Rollback:** revert theo aggregate; giữ nguyên database.

---

### Task 7: Xóa EF Core và ASP.NET framework khỏi Application

**Files:**
- Delete: Application/Abstractions/Persistence/IApplicationDbContext.cs
- Modify: Application.csproj và GlobalUsings.cs
- Modify Infrastructure DbContext/DI references.
- Modify architecture tests.

- [ ] **Step 1: Verify không còn forbidden code**

~~~powershell
rg -n "Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|DbSet<|IFormFile|SelectListItem|IPasswordHasher|IConfiguration" src/QuanLyChoThuePhongTroWeb.Application
~~~

Expected: không có kết quả.

- [ ] **Step 2: Remove package/framework references**

Application.csproj chỉ giữ Domain ProjectReference, Microsoft.Extensions.DependencyInjection.Abstractions và Microsoft.Extensions.Logging.Abstractions.

- [ ] **Step 3: Delete IApplicationDbContext**

Infrastructure đăng ký feature stores và IUnitOfWork cùng scoped ApplicationDbContext.

- [ ] **Step 4: Run gates và commit**

~~~powershell
git commit -m "refactor: make application framework independent"
~~~

**Risk:** High.  
**Rollback:** revert Task 7 và feature persistence gần nhất nếu còn missed dependency.

---

### Task 8: Chuyển startup database operation vào Infrastructure

**Files:**
- Create: Infrastructure/Persistence/DatabaseInitializer.cs
- Modify: Web/Program.cs
- Modify: Web.csproj
- Modify Infrastructure integration tests.

- [ ] **Step 1: Encapsulate database initialization**

Infrastructure cung cấp extension InitializeDatabaseAsync(IServiceProvider, CancellationToken). Implementation tạo scope, resolve ApplicationDbContext và migrate/seed đúng thứ tự hiện tại.

- [ ] **Step 2: Remove EF code khỏi Program**

Xóa using Microsoft.EntityFrameworkCore và direct resolution của ApplicationDbContext.

- [ ] **Step 3: Remove Web persistence packages**

Xóa Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore, EF Design và EF Tools khỏi Web.csproj. Chúng chỉ nằm ở Infrastructure.

- [ ] **Step 4: Verify EF CLI bằng Infrastructure target**

Chạy EF gates. Không thêm migration để chữa tooling.

- [ ] **Step 5: Commit**

~~~powershell
git commit -m "refactor: move database initialization to infrastructure"
~~~

**Risk:** High vì startup migration/seed.  
**Rollback:** revert commit; schema không đổi.

---

### Task 9: Chuẩn hóa DTO boundary và bỏ Web → Domain

**Files:**
- Modify public service interfaces trong Application/Features/*/Services/.
- Create/modify DTO và command files trong từng Application feature.
- Modify Web controllers, ViewModels và Razor model declarations.
- Modify Web/GlobalUsings.cs và Web.csproj.
- Create Web mapping helpers.

- [ ] **Step 1: Replace entity inputs**

Create/update methods không nhận trực tiếp Domain entity. Tạo command DTO chứa đúng field hiện đang bind từ HTTP.

- [ ] **Step 2: Replace entity outputs**

Service interfaces không trả Domain entity cho Web. Dùng feature DTO/detail DTO/list DTO/result. Thay object return type bằng DTO cụ thể.

- [ ] **Step 3: Map tại Web boundary**

Web ViewModel/HTTP contract map sang Application command. Razor chỉ dùng Web ViewModel hoặc Application response DTO có chủ đích.

- [ ] **Step 4: Remove Domain import và reference**

Xóa Domain global usings rồi xóa Domain ProjectReference khỏi Web.csproj.

- [ ] **Step 5: Add boundary test**

Test fail nếu Web.csproj reference Domain hoặc Web source dùng namespace Domain.

- [ ] **Step 6: Commit từng feature**

Thứ tự: NguoiDungs/NguoiThues → PhongTros → HopDongs → HoaDons/LichSuThanhToans → DichVus/DienNuocs → YeuCauSuCos/ThongBaos/Dashboard.

**Risk:** High vì model binding và Razor compile.  
**Rollback:** revert từng feature commit.

---

### Task 10: Hoàn thiện integration coverage

**Files:**
- Create: Infrastructure.IntegrationTests/Persistence/ApplicationDbContextTests.cs
- Create: Infrastructure.IntegrationTests/Persistence/MigrationSafetyTests.cs
- Create: Web.IntegrationTests/Areas/AreaRoutingTests.cs
- Create: Web.IntegrationTests/AuthenticationTests.cs
- Create: Web.IntegrationTests/StartupTests.cs
- Create: Web.IntegrationTests/StaticFilesTests.cs
- Create: Web.IntegrationTests/SignalRTests.cs

- [ ] **Step 1: Dùng PostgreSQL test database riêng**

Không reuse development database. Reset dữ liệu giữa tests; không reset history của database thật.

- [ ] **Step 2: Persistence coverage**

Kiểm tra mapping, relationship, filtered unique index, transaction và representative feature-store queries.

- [ ] **Step 3: Web coverage**

Kiểm tra startup DI, login/auth redirect, Area routes, Razor view discovery, static files và SignalR negotiate endpoint.

- [ ] **Step 4: Cô lập external providers**

Email, Cloudinary, AI và document exporters dùng fake/test adapters trong Web tests.

- [ ] **Step 5: Verify và commit**

~~~powershell
dotnet test QuanLyChoThuePhongTroWeb.sln
git commit -m "test: add layered integration coverage"
~~~

**Risk:** Medium.  
**Rollback:** revert test commit.

---

### Task 11: Cập nhật documentation và nghiệm thu cuối

**Files:**
- Modify: AGENTS.md
- Modify README.md nếu tồn tại.
- Modify tài liệu còn dùng project path cũ.
- Không sửa migrations hiện hữu.

- [ ] **Step 1: Update AGENTS final state**

Ghi bốn production projects, bốn test projects, reference matrix, EF commands và config path cuối cùng.

- [ ] **Step 2: Search stale paths**

~~~powershell
rg -n "QuanLyChoThuePhongTroWeb\.csproj|Data/ApplicationDbContext|Data\\ApplicationDbContext|^Migrations/|tests/QuanLyChoThuePhongTroWeb\.UnitTests|tests/QuanLyChoThuePhongTroWeb\.IntegrationTests" . --glob "!src/QuanLyChoThuePhongTroWeb.Web/wwwroot/Theme/**"
~~~

Mỗi kết quả phải được sửa hoặc ghi rõ là historical documentation.

- [ ] **Step 3: Final dependency checks**

~~~powershell
rg -n "Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|DbSet<|IFormFile|SelectListItem|IPasswordHasher|IConfiguration" src/QuanLyChoThuePhongTroWeb.Application
rg -n "QuanLyChoThuePhongTroWeb\.Domain|Microsoft\.EntityFrameworkCore|Npgsql" src/QuanLyChoThuePhongTroWeb.Web --glob "*.cs" --glob "*.csproj"
~~~

Expected: không có forbidden result.

- [ ] **Step 4: Final gates**

~~~powershell
dotnet restore QuanLyChoThuePhongTroWeb.sln
dotnet build QuanLyChoThuePhongTroWeb.sln --no-restore
dotnet test QuanLyChoThuePhongTroWeb.sln --no-build
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj
~~~

- [ ] **Step 5: Runtime smoke checklist**

Login/logout; role/claim authorization; Area routing; Razor; static/upload; CRUD chính; jobs; SignalR; email/storage/document export với test config; migration history và schema không đổi.

- [ ] **Step 6: Final documentation commit**

~~~powershell
git add AGENTS.md README.md docs
git commit -m "chore: update clean architecture documentation"
~~~

## 5. Commit order đề xuất

1. test: tach test projects theo bon tang
2. refactor: loai aspnet contracts khoi application
3. refactor: tach security va configuration khoi application
4. Một commit cho mỗi feature persistence port
5. refactor: make application framework independent
6. refactor: move database initialization to infrastructure
7. Một commit DTO boundary cho mỗi feature Web
8. test: add layered integration coverage
9. chore: update clean architecture documentation

Không squash các checkpoint persistence trước khi review EF gates.

## 6. Definition of Done

- [ ] Domain.csproj không có package hoặc project reference.
- [ ] Application chỉ project-reference Domain.
- [ ] Application không có EF Core, ASP.NET Core, Identity, MVC, HTTP hoặc IConfiguration dependency.
- [ ] Application không expose DbSet/IQueryable/provider types.
- [ ] Infrastructure reference Application và Domain; không reference Web.
- [ ] Web reference Application và Infrastructure; không direct-reference Domain.
- [ ] Web không package-reference EF Core hoặc Npgsql.
- [ ] Program.cs không resolve DbContext hoặc gọi EF APIs.
- [ ] Bốn test projects tồn tại và nằm trong solution.
- [ ] Architecture tests khóa project, package và framework references.
- [ ] Restore, build và toàn bộ tests pass.
- [ ] EF báo không có pending model changes.
- [ ] Migration IDs, migration files và database history không đổi.
- [ ] Login, Areas, Razor, static assets, CRUD, jobs, SignalR, email và storage đạt smoke checklist.
- [ ] AGENTS.md và tài liệu vận hành phản ánh trạng thái cuối.

## 7. Rollback tổng thể

Mỗi task là một hoặc nhiều commit độc lập theo feature. Khi gate thất bại:

1. Không tạo migration để làm build/test pass.
2. Revert commit feature gần nhất.
3. Xác nhận migration list và pending-model-change trở lại baseline.
4. Giữ các task đã pass trước đó.
5. Sửa nguyên nhân trong commit mới có test tái hiện.

Không dùng git reset --hard hoặc git clean trên working tree hiện tại vì branch review đang có số lượng lớn file move/untracked.
