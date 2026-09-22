# Hướng dẫn triển khai

Tài liệu này mô tả cách chuẩn bị và triển khai ứng dụng Web hiện tại. Repository chưa chứa Dockerfile, manifest cloud hay CI/CD workflow; các bước dưới đây dùng dotnet publish và không giả định nhà cung cấp hosting cụ thể.

## Yêu cầu

- .NET 8 SDK để build; máy chạy chỉ cần ASP.NET Core Runtime 8 nếu publish framework-dependent.
- PostgreSQL có database và tài khoản riêng cho ứng dụng.
- Quyền kết nối từ máy chạy ứng dụng tới PostgreSQL.
- SMTP, Cloudinary, Gemini và thông tin VietQR nếu bật các chức năng tương ứng.
- Thư mục chạy có quyền ghi nếu bật job email, vì state được lưu thành file JSON tại content root.

## Cấu hình

Không commit appsettings.json thật. Dùng biến môi trường hoặc secret store của nền tảng. ASP.NET Core ánh xạ dấu __ sang dấu : trong khóa cấu hình.

| Biến môi trường | Bắt buộc | Mục đích |
| --- | --- | --- |
| ConnectionStrings__DefaultConnection | Có | PostgreSQL runtime |
| ASPNETCORE_ENVIRONMENT | Nên đặt | Dùng Production khi triển khai |
| ASPNETCORE_URLS | Tùy host | Địa chỉ/port Kestrel lắng nghe |
| BackgroundJobs__Enabled | Không | Mặc định true; tắt toàn bộ hosted jobs khi cần |
| EmailSettings__SmtpServer | Khi gửi email | SMTP host |
| EmailSettings__Port | Khi gửi email | SMTP port, mặc định 587 |
| EmailSettings__SenderName | Khi gửi email | Tên người gửi |
| EmailSettings__SenderEmail | Khi gửi email | Tài khoản SMTP |
| EmailSettings__Password | Khi gửi email | Mật khẩu/app password SMTP |
| Cloudinary__CloudName | Khi tải ảnh | Cloudinary cloud name |
| Cloudinary__ApiKey | Khi tải ảnh | Cloudinary API key |
| Cloudinary__ApiSecret | Khi tải ảnh | Cloudinary API secret |
| Gemini__ApiKey | Khi dùng AI | API key Gemini |
| Gemini__Model | Không | Model Gemini |
| VietQRSettings__BankId | Khi dùng QR | Mã ngân hàng |
| VietQRSettings__AccountNumber | Khi dùng QR | Số tài khoản nhận |
| VietQRSettings__AccountName | Khi dùng QR | Tên chủ tài khoản |
| AutoReminderSettings__Enabled | Không | Bật email nhắc hóa đơn |
| AutoReminderSettings__OverdueDays | Không | Số ngày quá hạn, mặc định 5 |
| ContractAlertSettings__Enabled | Không | Bật cảnh báo hết hạn hợp đồng |
| ContractAlertSettings__AdminEmail | Khi bật cảnh báo | Email quản lý nhận cảnh báo |
| ContractAlertSettings__AlertDays__0 | Không | Phần tử đầu danh sách mốc ngày; thêm __1, __2 khi cần |
| DashboardSettings__ChotDienNuocDay | Không | Ngày chốt điện nước hiển thị trên dashboard |

Mẫu đầy đủ nằm tại [appsettings.example.json](../../src/QuanLyChoThuePhongTroWeb.Web/appsettings.example.json).

## Chuẩn bị database

Ứng dụng gọi Database.MigrateAsync() và seed tài khoản quản trị mỗi lần khởi động. Do đó tài khoản PostgreSQL runtime hiện cần quyền tạo/thay đổi schema. Nếu migration hoặc kết nối thất bại, process khởi động thất bại.

Để kiểm tra migration trước khi triển khai:

~~~powershell
$env:QLCTPT_DESIGNTIME_CONNECTION_STRING = 'Host=<host>;Port=5432;Database=<database>;Username=<user>;Password=<password>'
dotnet ef migrations list --project src/QuanLyChoThuePhongTroWeb.Infrastructure --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure
dotnet ef database update --project src/QuanLyChoThuePhongTroWeb.Infrastructure --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure
~~~

Trước khi migrate production, sao lưu database và thử migration trên bản sao hoặc staging. Không sửa migration đã chạy; tạo migration mới khi tính năng thực sự thay đổi schema.

Migration 1 trong [spec database](../superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md) đã có trên nhánh hiện tại; Migration 2 vẫn là kế hoạch. Phải áp Migration 1 trước Migration 2, xác nhận lần lượt 22 và 37 bảng nghiệp vụ trên PostgreSQL test rồi mới đưa lên staging/production.

## Build và publish

Từ thư mục gốc repository:

~~~powershell
dotnet restore QuanLyChoThuePhongTroWeb.sln
dotnet build QuanLyChoThuePhongTroWeb.sln -c Release --no-restore
dotnet publish src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj -c Release --no-build -o ./publish
~~~

Chép thư mục publish/ tới máy chạy, cấu hình environment variables rồi chạy:

~~~powershell
dotnet QuanLyChoThuePhongTroWeb.Web.dll
~~~

Trên Linux có thể dùng systemd; trên Windows có thể đặt sau IIS hoặc một service manager. Reverse proxy phải chuyển tiếp HTTPS/WebSocket đúng cách để SignalR hoạt động.

## HTTPS và reverse proxy

Production bật HSTS và UseHttpsRedirection. Cookie đăng nhập có HttpOnly, SameSite=Lax và SecurePolicy=SameAsRequest.

Khi TLS kết thúc tại Nginx/IIS/load balancer, ứng dụng cần nhận đúng scheme HTTPS. Program.cs hiện chưa gọi UseForwardedHeaders; phải cấu hình/bổ sung forwarded headers trước khi tin X-Forwarded-*. Không công khai Kestrel trực tiếp nếu chưa có TLS, firewall và reverse proxy phù hợp.

SignalR dùng endpoint /thongBaoHub; proxy phải hỗ trợ WebSocket hoặc fallback transport.

## Background jobs và lưu trạng thái

Ba job chạy bên trong mỗi instance Web. Khi triển khai nhiều instance, mỗi instance sẽ chạy job riêng; state store JSON hiện chỉ khóa trong một process, không điều phối phân tán.

| File | Mục đích |
| --- | --- |
| sent_invoice_reminders.json | Hóa đơn đã gửi nhắc |
| sent_contract_alerts.json | Cảnh báo hợp đồng đã gửi |

Nếu dùng container, mount content root hoặc hai file trên vào storage bền. Mất file có thể khiến hệ thống gửi lại email; nhiều replica có thể gửi trùng. Với production nhiều instance, nên chỉ bật job ở một instance hoặc thay state store bằng PostgreSQL/distributed store.

## Bảo mật trước khi public/production

- Thay cơ chế mật khẩu quản trị bootstrap đang hard-code trong NguoiDungService; đọc mật khẩu ban đầu từ secret và buộc đổi sau đăng nhập đầu tiên.
- Thu hồi mọi password/API key từng commit. Xóa file ở commit mới không xóa dữ liệu khỏi lịch sử Git.
- Chỉ commit appsettings.example.json; kiểm tra git status và secret scan trước khi push.
- Hạn chế quyền PostgreSQL theo môi trường và không dùng tài khoản production cho test.
- SMTP hiện chấp nhận mọi certificate qua ServerCertificateValidationCallback; phải bỏ cơ chế này trước production để xác minh chứng chỉ đúng chuẩn.
- Xem xét giới hạn upload, rate limit, CSP và log redaction theo môi trường triển khai.

## Kiểm tra sau triển khai

1. Process khởi động không có lỗi migration hoặc seed.
2. Trang đăng nhập /QuanLyNhaTro/DangNhap trả về thành công qua HTTPS.
3. Admin/NhanVien vào đúng cổng quản lý; KhachThue chỉ thấy dữ liệu gắn với hồ sơ của mình.
4. Static assets tải được và /thongBaoHub kết nối thành công.
5. Thử tạo dữ liệu không nhạy cảm, transaction và xóa mềm trên staging.
6. Thử SMTP, Cloudinary, Gemini, PDF/Excel/Word và VietQR nếu đã cấu hình.
7. Kiểm tra quyền ghi và tính bền của hai file trạng thái job.
8. Theo dõi log Error/Critical, dung lượng database và thời gian phản hồi.

## Rollback

- Giữ lại artifact phiên bản trước để rollback ứng dụng.
- Sao lưu database trước migration. EF Core không tự rollback dữ liệu an toàn khi đổi schema.
- Nếu migration có thay đổi phá vỡ tương thích, chuẩn bị script/chiến lược rollback được review trước khi deploy.
- Khi lỗi chỉ thuộc integration, có thể tắt BackgroundJobs__Enabled, AutoReminderSettings__Enabled hoặc ContractAlertSettings__Enabled rồi khởi động lại; thao tác này không hoàn tác migration.
