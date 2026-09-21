# Quản lý cho thuê phòng trọ

Ứng dụng ASP.NET Core MVC .NET 8 quản lý nhiều chi nhánh nhà trọ, có cổng quản lý và cổng khách thuê. Mã nguồn được tổ chức theo Clean Architecture bốn tầng, dùng PostgreSQL và Entity Framework Core 8.

Roadmap mô tả công việc dự kiến, không phải danh sách tính năng đã hoàn thành.

## Chức năng hiện có

- Quản lý chi nhánh, phòng trọ, người thuê, tài khoản và các vai trò Admin, NhanVien, KhachThue.
- Hợp đồng, thành viên ở ghép, dịch vụ đăng ký và bản sao điều khoản hợp đồng.
- Giá dịch vụ theo chi nhánh, chỉ số điện nước, tính hóa đơn và lịch sử thanh toán.
- Xuất hóa đơn PDF/Excel, hợp đồng Word và sinh VietQR.
- Dashboard, email, báo sự cố kèm hình ảnh Cloudinary và thông báo SignalR.
- Trợ lý Gemini với các công cụ truy vấn dữ liệu vận hành.
- Tác vụ nền kết thúc hợp đồng, cảnh báo hết hạn và nhắc hóa đơn; bật/tắt theo cấu hình.
- Khách thuê xem hồ sơ, hợp đồng, hóa đơn, lịch sử thanh toán và báo sự cố.

Cổng khách vãng lai và API phiên bản v1 mới có thư mục khung. Đăng phòng công khai, lịch xem, giữ chỗ, OCR chỉ số, duyệt hóa đơn nháp và quy trình gửi ảnh chuyển khoản là hướng phát triển. Enum hóa đơn hiện chỉ có ChuaThanhToan và DaThanhToan; chưa có quy trình duyệt nháp.

## Cấu trúc và luồng xử lý

| Project | Trách nhiệm |
| --- | --- |
| `src/QuanLyChoThuePhongTroWeb.Domain` | Entities, Enums và quy tắc miền; không tham chiếu project/package ngoài |
| `src/QuanLyChoThuePhongTroWeb.Application` | Features gồm DTOs, Services, Persistence interfaces; Abstractions và Common |
| `src/QuanLyChoThuePhongTroWeb.Infrastructure` | EF Core, migrations, store, bảo mật, email, ảnh, AI, xuất file và background jobs |
| `src/QuanLyChoThuePhongTroWeb.Web` | Program.cs, Controllers, Areas, Razor Views, SignalR và wwwroot |
| `tests/` | Bốn project: Domain.UnitTests, Application.UnitTests, Infrastructure.IntegrationTests, Web.IntegrationTests |

Request → Web controller → Application service → store/interface → Infrastructure → PostgreSQL. Web chỉ tham chiếu Application và Infrastructure; không trực tiếp tham chiếu Domain. Application chỉ tham chiếu project Domain, có thêm package abstractions cho DI/logging; không phụ thuộc EF Core, ASP.NET Core hoặc IConfiguration.

Đăng ký nghiệp vụ tại Application/DependencyInjection.cs, store và dịch vụ ngoài tại Infrastructure/DependencyInjection.cs; Web/Program.cs cấu hình HTTP, authentication, routing và SignalR.

## Công nghệ

Backend dùng EF Core/Npgsql 8, MailKit, CloudinaryDotNet, ClosedXML, QuestPDF, DocX, QRCoder và SignalR. Frontend dùng Razor, Tabler/Bootstrap, jQuery cùng các thư viện giao diện trong wwwroot.

Mật khẩu mới dùng ASP.NET Core Identity PasswordHasher (PBKDF2). AspNetPasswordService vẫn kiểm tra SHA256 cũ; luồng đăng nhập nâng cấp hash khi cần. Không dùng SHA256 để tạo mật khẩu mới.

## Khởi chạy cục bộ

Cài .NET 8 SDK và PostgreSQL. Chạy lệnh từ thư mục gốc repository.

1. Sao chép cấu hình mẫu nếu chưa có appsettings.json; không ghi đè cấu hình đang dùng:

   ```powershell
   Copy-Item src/QuanLyChoThuePhongTroWeb.Web/appsettings.example.json src/QuanLyChoThuePhongTroWeb.Web/appsettings.json
   ```

2. Điền ConnectionStrings:DefaultConnection trỏ vào database phát triển. Cấu hình SMTP, Cloudinary, Gemini và VietQR khi dùng chức năng tương ứng. Không commit mật khẩu hoặc API key.
3. Restore, build và chạy:

   ```powershell
   dotnet restore QuanLyChoThuePhongTroWeb.sln
   dotnet build QuanLyChoThuePhongTroWeb.sln
   dotnet watch run --project src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj
   ```

Mở URL được in trong terminal và đường dẫn /QuanLyNhaTro/DangNhap. Khi khởi động, DatabaseInitializer tự chạy migrations và tạo tài khoản quản trị ban đầu. Trước khi đưa hệ thống lên môi trường dùng chung, hãy đổi thông tin đăng nhập mặc định trong quy trình khởi tạo và đặt mật khẩu riêng đủ mạnh. Lỗi kết nối hoặc migration làm khởi động thất bại.

## An toàn cấu hình

- Chỉ commit appsettings.example.json với giá trị mẫu. appsettings.json, .env, private key, certificate và file secrets đã được chặn trong .gitignore.
- Ưu tiên biến môi trường hoặc secret store của nền tảng triển khai cho connection string, SMTP, Cloudinary, Gemini và VietQR.
- Không dùng database phát triển hoặc production để chạy integration test.
- Nếu một secret từng được commit, việc xóa khỏi commit mới không làm secret biến mất khỏi lịch sử Git; cần thu hồi/đổi secret và làm sạch lịch sử trước khi public repository.

## Cấu hình cần biết

| Nhóm | Mục đích |
| --- | --- |
| ConnectionStrings:DefaultConnection | PostgreSQL lúc chạy Web |
| BackgroundJobs:Enabled | Bật/tắt toàn bộ hosted jobs; mặc định bật |
| AutoReminderSettings | Bật nhắc hóa đơn và số ngày quá hạn |
| ContractAlertSettings | Bật cảnh báo, email quản lý và các mốc ngày |
| DashboardSettings:ChotDienNuocDay | Ngày chốt điện nước; file mẫu đang đặt 25 |
| EmailSettings, Cloudinary, Gemini, VietQRSettings | Thông tin các tích hợp |

Tắt BackgroundJobs:Enabled khi cần chạy môi trường phát triển không có tác vụ tự động. Tùy chọn này không tắt migrations/seed lúc khởi động.

## EF Core migrations

Migrations nằm trong Infrastructure/Persistence/Migrations. Design-time factory đọc biến môi trường, không tự đọc appsettings.json của Web:

```powershell
$env:QLCTPT_DESIGNTIME_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=qlctpt_dev;Username=<user>;Password=<password>'
dotnet ef migrations list --project src/QuanLyChoThuePhongTroWeb.Infrastructure --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure
dotnet ef database update --project src/QuanLyChoThuePhongTroWeb.Infrastructure --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure
```

Cần công cụ dotnet-ef tương thích EF Core 8. Có thể dùng ConnectionStrings__DefaultConnection thay biến design-time. Refactor phải giữ nguyên schema; không sửa migration đã có hoặc tạo migration chỉ vì di chuyển mã.

## Kiểm thử

```powershell
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
```

Để chạy toàn solution, chuẩn bị PostgreSQL test riêng và khai báo:

```powershell
$env:QLCTPT_TEST_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=qlctpt_test;Username=<user>;Password=<password>'
dotnet test QuanLyChoThuePhongTroWeb.sln
```

Infrastructure/Web integration fixtures yêu cầu tên database kết thúc bằng _test hoặc _integration_test và chạy migrations. Thiếu biến hoặc sai tên sẽ báo lỗi, không tự bỏ qua test. Không dùng database phát triển/production. Web test host thay dịch vụ ngoài bằng fake và tắt hosted jobs. CI tại .github/workflows hiện chỉ là khung, chưa có workflow thực thi.

## Tài liệu liên quan

- [Kiến trúc hệ thống](docs/architecture/README.md)
- [Danh mục chức năng](docs/features/README.md)
- [Hướng dẫn triển khai](docs/deployment/README.md)
- [Roadmap 60 ngày và đánh giá hiện trạng](docs/roadmap-60-ngay.md)
- [Thiết kế nền tảng cơ sở dữ liệu cho hai migration](docs/superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md)
- [Kế hoạch Migration 1: chỉ số, OCR, hóa đơn và thanh toán](docs/superpowers/plans/2026-09-21-migration-1-billing-foundation.md)
- [Kế hoạch Migration 2: phòng công khai, lịch xem và giữ chỗ](docs/superpowers/plans/2026-09-21-migration-2-public-reservation-foundation.md)
