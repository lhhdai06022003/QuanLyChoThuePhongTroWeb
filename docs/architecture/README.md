# Kiến trúc hệ thống

Tài liệu này mô tả kiến trúc đang tồn tại trong mã nguồn. Các thiết kế trong [superpowers](../superpowers/) và [roadmap 60 ngày](../roadmap-60-ngay.md) là hướng phát triển, không mặc nhiên là chức năng đã triển khai.

## Tổng quan

Solution dùng Clean Architecture bốn tầng trên .NET 8:

~~~mermaid
flowchart LR
    Web[Web: MVC, API, Razor, SignalR] --> Application[Application: use cases, services, DTOs, abstractions]
    Web --> Infrastructure[Infrastructure: EF Core, stores, integrations, jobs]
    Infrastructure --> Application
    Infrastructure --> Domain[Domain: entities, enums, business rules]
    Application --> Domain
    Infrastructure --> PostgreSQL[(PostgreSQL)]
    Infrastructure --> External[SMTP / Cloudinary / Gemini]
~~~

Quy tắc phụ thuộc:

- Domain không có ProjectReference hoặc package ngoài.
- Application chỉ tham chiếu project Domain; không phụ thuộc Web hoặc Infrastructure.
- Infrastructure tham chiếu Application và Domain; không phụ thuộc Web.
- Web tham chiếu Application và Infrastructure; không tham chiếu trực tiếp Domain.

Các test kiến trúc kiểm tra ranh giới này trong bốn project dưới tests/.

## Trách nhiệm từng tầng

### Domain

src/QuanLyChoThuePhongTroWeb.Domain chứa entity và enum cốt lõi như chi nhánh, phòng, người thuê, hợp đồng, hóa đơn, thanh toán, dịch vụ, sự cố và thông báo. Domain không biết EF Core, HTTP, cấu hình hoặc dịch vụ ngoài.

Khi thêm quy tắc thuần không cần I/O, ưu tiên đặt tại Domain. Không thêm dependency chỉ để phục vụ giao diện hay persistence.

### Application

src/QuanLyChoThuePhongTroWeb.Application tổ chức theo feature:

~~~text
Features/<Feature>/
├── DTOs/
├── Services/
├── Persistence/
└── UseCases/       # khi feature có tác vụ điều phối độc lập
~~~

Application chịu trách nhiệm validation nghiệp vụ, chuyển trạng thái, điều phối transaction qua abstraction và trả DTO. Store interface nằm cạnh feature; abstraction dùng chung nằm trong Application/Abstractions; cấu hình kiểu mạnh và model dùng chung nằm trong Application/Common.

Application không được dùng DbContext, IQueryable, IFormFile, HttpContext, IConfiguration, SelectListItem hoặc truy cập file vật lý trực tiếp. Dữ liệu upload đi qua UploadFile; lựa chọn giao diện dùng SelectOptionDto.

### Infrastructure

src/QuanLyChoThuePhongTroWeb.Infrastructure triển khai các cổng do Application định nghĩa:

- Persistence/ApplicationDbContext.cs: DbContext và mapping EF Core.
- Persistence/Features/: store triển khai truy vấn và ghi dữ liệu.
- Persistence/Migrations/: lịch sử schema PostgreSQL.
- Persistence/EfUnitOfWork.cs và ApplicationTransaction.cs: commit và transaction.
- ExternalServices/: SMTP, Cloudinary, Gemini, xuất PDF/Excel/Word và VietQR.
- Security/: PBKDF2 và tương thích hash SHA256 cũ.
- BackgroundJobs/: hosted services và state store dạng JSON.

Infrastructure đăng ký dependency trong Infrastructure/DependencyInjection.cs. Mọi refactor phải giữ nguyên schema; tính năng mới cần thay đổi dữ liệu phải tạo migration mới và được review, không sửa migration lịch sử.

### Web

src/QuanLyChoThuePhongTroWeb.Web là composition root và lớp giao diện:

- Program.cs: DI, MVC, cookie authentication, antiforgery, static files, routing và SignalR.
- Areas/QuanLyNhaTro/: cổng Admin/NhanVien.
- Areas/KhachThue/: cổng KhachThue.
- Controllers/: endpoint dùng chung.
- Hubs/: SignalR notifier và hub /thongBaoHub.
- Views/ và wwwroot/: Razor và tài nguyên tĩnh.

Controller chỉ xử lý HTTP, ModelState, claim và điều phối Application service. Web dùng DTO và enum của Application; không sử dụng Domain entity trực tiếp.

## Luồng request và dữ liệu

~~~mermaid
sequenceDiagram
    participant U as Người dùng
    participant C as Web Controller
    participant S as Application Service
    participant P as Store interface
    participant E as Infrastructure Store
    participant D as PostgreSQL

    U->>C: HTTP request
    C->>C: Bind + ModelState + claim/role
    C->>S: DTO / command
    S->>P: Đọc hoặc ghi qua abstraction
    P->>E: DI chọn implementation
    E->>D: EF Core / transaction
    D-->>E: Kết quả
    E-->>S: Domain data hoặc projection
    S-->>C: DTO / ServiceResult
    C-->>U: View, JSON, file hoặc redirect
~~~

SignalR là trường hợp bổ sung: Application gọi IThongBaoNotifier; Web cung cấp SignalRThongBaoNotifier để đẩy sự kiện tới client.

## Xác thực và phân quyền

- Cookie authentication dùng login path /QuanLyNhaTro/DangNhap và thời hạn tối đa 30 ngày khi ghi nhớ đăng nhập.
- Cổng quản lý kế thừa AdminBaseController, yêu cầu role Admin hoặc NhanVien và tự động kiểm tra antiforgery cho action thay đổi dữ liệu.
- Cổng khách thuê dùng role KhachThue; claim NguoiThueId liên kết tài khoản với hồ sơ người thuê.
- Quyền phải được kiểm tra ở server. Trạng thái nút hoặc menu trên giao diện không phải cơ chế bảo vệ.

Mật khẩu mới dùng ASP.NET Core Identity PasswordHasher (PBKDF2). SHA256 chỉ được giữ để xác minh dữ liệu cũ và nâng cấp hash sau lần đăng nhập hợp lệ.

## Khởi tạo và tác vụ nền

Khi Web khởi động, DatabaseInitializer chạy toàn bộ EF Core migrations rồi seed tài khoản quản trị. Nếu kết nối hoặc migration lỗi, ứng dụng không khởi động hoàn chỉnh.

Khi BackgroundJobs:Enabled bật, Infrastructure đăng ký ba hosted service:

| Job | Hành vi hiện tại |
| --- | --- |
| ContractAutoCloseJob | Chạy khi service bắt đầu, sau đó đợi đến 00:00 giờ Việt Nam mỗi ngày |
| ContractExpiryAlertJob | Chạy khi service bắt đầu, sau đó đợi đến 08:00 giờ Việt Nam mỗi ngày; mốc cảnh báo lấy từ cấu hình |
| InvoiceReminderJob | Chạy khi service bắt đầu rồi lặp mỗi giờ; chỉ gửi khi AutoReminderSettings:Enabled bật |

Trạng thái email đã gửi được lưu trong sent_invoice_reminders.json và sent_contract_alerts.json tại content root. Đây là state cục bộ, cần volume ghi bền nếu triển khai bằng container và không phù hợp để nhiều instance ghi chung.

## Kiểm thử theo tầng

| Project test | Phạm vi |
| --- | --- |
| Domain.UnitTests | Quy tắc và trạng thái thuần |
| Application.UnitTests | Service/use case với store và UnitOfWork giả; architecture boundary |
| Infrastructure.IntegrationTests | PostgreSQL, mapping, store, migrations, job runtime và tích hợp hạ tầng |
| Web.IntegrationTests | Startup, HTTP, auth, Areas, static files, SignalR và boundary của Web |

Full suite cần QLCTPT_TEST_CONNECTION_STRING trỏ tới database riêng có hậu tố _test hoặc _integration_test.

## Thành phần mới chỉ là khung

Web/Api/V1/, Web/Areas/KhachVangLai/ và .github/workflows/ hiện chưa có implementation chạy. Tìm phòng công khai, lịch xem, giữ chỗ, OCR chỉ số, hóa đơn nháp và xác nhận ảnh chuyển khoản vẫn thuộc roadmap.
## Quyền sở hữu schema trong roadmap

Database hiện có 17 bảng nghiệp vụ. Thiết kế dự kiến thêm hai migration nền, tổng cộng 22 bảng mới; đây chưa phải schema đang chạy.

Người A sở hữu Domain entities, enums, EF configurations, DbContext và migrations. Người B phát triển Razor, Web ViewModel và tài nguyên giao diện dựa trên DTO/Application Service đã chốt. Mọi yêu cầu đổi schema quay lại Người A để tránh hai nhánh cùng sửa model snapshot. Xem [spec hai migration](../superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md).