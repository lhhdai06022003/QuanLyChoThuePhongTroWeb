# HƯỚNG DẪN TOÀN DIỆN DỰ ÁN CHO LẬP TRÌNH VIÊN MỚI
## DỰ ÁN QUẢN LÝ CHO THUÊ PHÒNG TRỌ (QUANLYCHOTHUEPHONGTROWEB)

Tài liệu này được biên soạn nhằm cung cấp cái nhìn toàn cảnh về dự án cho thành viên mới hoặc lập trình viên thực tập. Tài liệu bao gồm: công nghệ sử dụng, cấu trúc thư mục, quy chuẩn viết code, chi tiết tất cả các chức năng và cách thức vận hành hệ thống.

---

## PHẦN I: TỔNG QUAN HỆ THỐNG & CÔNG NGHỆ (TECH STACK)

Dự án được xây dựng dưới dạng ứng dụng Web theo kiến trúc phân tầng chuyên nghiệp, hỗ trợ đầy đủ các tính năng kế toán, thanh toán thông minh và báo cáo.

### 1. Nền tảng Backend & Cơ sở dữ liệu

*   **Framework chính**: .NET 8.0 (ASP.NET Core Web MVC / Web API).
*   **Cơ sở dữ liệu**: PostgreSQL.
*   **ORM**: Entity Framework Core 8 (EF Core) thông qua thư viện kết nối `Npgsql.EntityFrameworkCore.PostgreSQL`. Quản lý cơ sở dữ liệu bằng cơ chế **Code-First Migrations**.
*   **Kiến trúc**: Mô hình **Controller - Service Layer**.
    *   *Controller*: Chỉ làm nhiệm vụ điều phối dữ liệu (nhận request, validate model qua ModelState và trả kết quả View/API).
    *   *Service*: Chứa toàn bộ logic nghiệp vụ, tính toán tiền nong, xử lý cập nhật dữ liệu và kết nối DB.

### 2. Giao diện & Thư viện Frontend

*   **CSS Framework chính**: **Tabler Theme** (phát triển trên nền tảng **Bootstrap 5**), mang giao diện chuyên nghiệp, hỗ trợ responsive hoàn chỉnh và tự động tương thích giao diện Sáng/Tối (Dark/Light Mode).
*   **Core JS**: **jQuery** phục vụ DOM manipulation và các yêu cầu AJAX bất đồng bộ.
*   **Các thư viện bổ trợ**:
    *   *jQuery DataTables*: Hiển thị danh sách lớn, phân trang và tìm kiếm phía máy chủ (Server-side Processing).
    *   *Flatpickr*: Bộ chọn ngày tháng giao diện hiện đại, hỗ trợ tiếng Việt.
    *   *SweetAlert2*: Thay thế hộp thoại thông báo thô sơ bằng modal popup xác nhận trực quan.
    *   *Select2*: Tìm kiếm thông minh (autocomplete) trong thẻ chọn dropdown.
    *   *FontAwesome 6*: Hệ thống biểu tượng vector dùng toàn dự án.

### 3. Thư viện tích hợp nâng cao

*   **CloudinaryDotNet**: Quản lý và lưu trữ hình ảnh minh họa sự cố từ khách thuê an toàn trên nền tảng đám mây Cloudinary.
*   **ASP.NET Core SignalR**: Truyền tải thông báo thời gian thực (Real-time notifications) đẩy tín hiệu chuông/pop-up thông báo tức thì đến người dùng khi có sự cố mới hoặc giao dịch phát sinh.
*   **QuestPDF**: Thư viện sinh tệp PDF chất lượng cao dùng để xuất hóa đơn và báo cáo hàng tháng.
*   **DocX**: Thư viện tạo, chỉnh sửa và xuất hợp đồng thuê phòng sang định dạng Word (.docx) chuyên nghiệp theo chuẩn A4.
*   **ClosedXML**: Thư viện đọc/ghi Excel dùng xuất báo cáo kế toán dưới định dạng `.xlsx`.
*   **QRCoder**: Sinh mã QR chuyển khoản VietQR Napas động dưới dạng luồng byte ảnh PNG hoàn toàn offline trên server (không phụ thuộc internet).
*   **Google Gemini AI (1.5/3.6 Flash)**: Tích hợp Trợ lý AI hỗ trợ Function Calling 9 C# APIs để truy vấn dữ liệu vận hành bằng ngôn ngữ tự nhiên.
*   **SMTP (TLS / MailKit)**: Gửi email thông báo hóa đơn và nhắc nợ tự động qua tài khoản cấu hình trong `appsettings.json`.

---

## PHẦN II: CẤU TRÚC THƯ MỤC DỰ ÁN

Dự án sử dụng cơ chế **Areas** của ASP.NET Core để cô lập logic nghiệp vụ Quản lý nhà trọ và Khách thuê khỏi cấu trúc MVC mặc định.

```text
QuanLyChoThuePhongTroWeb/
│
├── Areas/
│   ├── QuanLyNhaTro/             <-- Phân hệ nghiệp vụ Quản lý nhà trọ chính (Admin Portal)
│   │   ├── Controllers/          <-- Nhận request và định nghĩa các API Endpoints
│   │   ├── Models/               <-- Các lớp thực thể (Entity) liên kết trực tiếp với database
│   │   ├── Services/             <-- Chứa lớp nghiệp vụ (Business Logic), AI, Cloudinary, Sự cố, Hóa đơn...
│   │   ├── ViewModels/           <-- Chứa DTOs (Requests & Responses) truyền tải dữ liệu
│   │   └── Views/                <-- Giao diện Razor Views (.cshtml) của từng thực thể
│   │       └── Shared/           <-- Thư mục chứa giao diện dùng chung (_AiChatWidget, _Layout...)
│   │
│   └── KhachThue/                <-- Phân hệ dành riêng cho Khách thuê (Tenant Portal)
│       ├── Controllers/          <-- Controllers điều phối (Dashboard, HoSo, HopDong, HoaDon, SuCo...)
│       ├── ViewModels/           <-- ViewModels phục vụ Portal khách thuê
│       └── Views/                <-- Giao diện hiển thị thông tin và gửi báo cáo sự cố cho khách
│
├── Controllers/                  <-- HomeController điều hướng ban đầu
├── Data/
│   └── ApplicationDbContext.cs   <-- Khai báo DbSet, cấu hình mối quan hệ bảng (Fluent API) và Unique Indexes
├── Filters/                      <-- GlobalExceptionFilter xử lý ngoại lệ toàn hệ thống
├── Hubs/
│   └── ThongBaoHub.cs            <-- SignalR Hub xử lý thông báo real-time
├── Migrations/                   <-- Lưu lịch sử thay đổi Database PostgreSQL
├── Services/                     <-- Dịch vụ dùng chung hệ thống (MenuService...)
├── wwwroot/                      <-- Thư mục tài nguyên tĩnh (css, js, hình ảnh, Tabler Theme)
├── appsettings.json              <-- Cấu hình kết nối DB, SMTP Email, tài khoản VietQR, Cloudinary, Gemini AI Key
└── Program.cs                    <-- Nơi khởi chạy ứng dụng, cấu hình DI, Background Services, SignalR và Routing
```

---

## PHẦN III: CÁC QUY CHUẨN VIẾT CODE QUAN TRỌNG (CODING RULES)

1.  **Tách biệt Controller & Service**: Tuyệt đối không tiêm `ApplicationDbContext` vào Controller. Mọi hành động truy vấn DB hoặc tính toán tiền nong bắt buộc phải viết trong Service.
2.  **Đăng ký Dependency Injection (DI)**: Khi tạo mới một cặp Interface/Service, bắt buộc phải đăng ký trong `Program.cs` dưới dạng Scoped. Ví dụ:
    ```csharp
    builder.Services.AddScoped<IPhongTroService, PhongTroService>();
    ```
3.  **Hỗ trợ Dark Mode**: Tránh sử dụng class cứng chỉ định màu nền như `bg-white` trên các ô input/select hoặc datepicker. Hãy dùng các biến CSS hệ thống của Tabler như `var(--tblr-bg-surface)` để giao diện thích ứng khi chuyển sang chế độ tối.
4.  **Chuẩn hóa múi giờ**: Cơ sở dữ liệu PostgreSQL lưu thời gian ở chuẩn UTC. Khi hiển thị hoặc xuất tệp tin báo cáo, bắt buộc phải cộng thêm 7 tiếng (`.AddHours(7)`) để đổi sang giờ Việt Nam (GMT+7) và định dạng chuỗi `dd/MM/yyyy HH:mm`.
5.  **Xóa mềm (Soft Delete)**: Mọi thực thể chính đều có trường `IsDeleted`. Khi thực hiện hành động xóa, chỉ cập nhật `IsDeleted = true` trong DB và lọc lại dữ liệu bằng `.Where(x => !x.IsDeleted)`. Không dùng lệnh xóa cứng (chặn xóa vật lý khỏi database) nhằm bảo toàn dữ liệu báo cáo lịch sử.

---

## PHẦN IV: CHI TIẾT TẤT CẢ CHỨC NĂNG & CÁCH THỨC HOẠT ĐỘNG

Dưới đây là mô tả chi tiết 10 module chức năng chính của hệ thống, bao gồm cách thức vận hành và vị trí hiển thị trên giao diện của trang web:

### 1. Quản lý Tài khoản & Phân quyền

*   **Giao diện xuất hiện**:
    *   *Trang Đăng nhập*: Xuất hiện tại `/QuanLyNhaTro/DangNhap` khi người dùng chưa đăng nhập.
    *   *Trang Quản lý tài khoản*: Truy cập từ Menu bên trái: **Quản lý người dùng** -> **Quản lý tài khoản**.
*   **Cách thức hoạt động**:
    *   **Cookie Authentication**: Sử dụng Cookie để duy trì phiên làm việc. Claims ghi nhận ID người dùng, Tên đăng nhập, Vai trò (Role) và trường liên kết `NguoiThueId` (nếu là khách thuê).
    *   **Phân quyền & Điều hướng**: Hệ thống định nghĩa 3 vai trò: `Admin`, `NhanVien`, `KhachThue`. Khi đăng nhập tại trang trung tâm, `NguoiDungController` tự động điều hướng: khách thuê sang Portal Khách Thuê (`/KhachThue/Dashboard`), Admin/Nhân viên sang Portal quản lý (`/QuanLyNhaTro/Dashboard`).
    *   **Bảo mật mật khẩu**: Mật khẩu băm một chiều SHA256 phía server. Seeding tạo tài khoản mặc định `admin` / `admin123` khi DB trống.
    *   **Reset mật khẩu về số điện thoại**: Tại trang quản lý tài khoản, Admin có thể kích hoạt API `ResetPasswordToPhoneApi` để đặt lại mật khẩu của người dùng về số điện thoại liên kết trong hồ sơ (nếu là khách thuê thì lấy SĐT từ hồ sơ `NguoiThue` tương ứng) và tự động băm mật khẩu mới này.

### 2. Bảng điều khiển (Dashboard)

*   **Giao diện xuất hiện**: Xuất hiện ngay sau khi đăng nhập thành công, hoặc truy cập từ Menu: **Dashboard**.
*   **Cách thức hoạt động**:
    *   Khi tải trang, hệ thống nạp dữ liệu thống kê của chi nhánh đầu tiên và tháng hiện tại.
    *   Khi người dùng thay đổi bộ lọc trên giao diện (Chi nhánh, Tháng, Năm), một sự kiện JavaScript được kích hoạt gửi yêu cầu AJAX về Endpoint `/QuanLyNhaTro/Dashboard/GetAjaxData`. API trả về dữ liệu thống kê dạng JSON để vẽ lại biểu đồ Chart.js (biểu đồ doanh thu 12 tháng, biểu đồ cơ cấu phương thức thanh toán) và cập nhật số liệu trên các thẻ KPI mà không cần tải lại toàn bộ trang.
    *   **Cảnh báo chỉ số điện nước động (To-Dos)**: Dashboard tự động đọc ngày chốt điện nước định nghĩa tại `DashboardSettings:ChotDienNuocDay` trong `appsettings.json` (mặc định ngày 5). Nếu ngày hiện tại nhỏ hơn ngày chốt này, hệ thống sẽ cảnh báo các phòng chưa chốt của tháng trước nữa (tháng `T-2`); nếu ngày hiện tại lớn hơn hoặc bằng ngày chốt, hệ thống sẽ quét cảnh báo của tháng liền trước (tháng `T-1`). Logic này bảo đảm kế toán chốt sổ đúng hạn mà không gây cảnh báo phiền hà.
    *   *To-Dos khác*: Các hợp đồng sắp hết hạn trong 30 ngày và các hóa đơn quá hạn kỳ trước chưa thanh toán (được gộp chi tiết theo từng tháng/năm/chi nhánh).

### 3. Quản lý Chi nhánh

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Chi nhánh**.
*   **Cách thức hoạt động**:
    *   Quản lý thông tin tòa nhà (Tên chi nhánh, Địa chỉ, Số điện thoại).
    *   Mỗi chi nhánh được tạo sẽ tự động sinh mã định danh duy nhất (Unique Index trên DB).
    *   Sử dụng DataTables kết nối API phân trang để hiển thị danh sách chi nhánh chưa bị xóa mềm. Khi người dùng bấm xóa, thuộc tính `IsDeleted` chuyển thành `true` để ẩn chi nhánh khỏi giao diện.

### 4. Quản lý Phòng trọ & Sơ đồ phòng

*   **Giao diện xuất hiện**:
    *   *Trang Phòng trọ*: Truy cập từ Menu: **Quản lý nhà trọ** -> **Phòng trọ** (Giao diện danh sách dạng bảng).
    *   *Trang Sơ đồ phòng*: Click vào nút **Sơ đồ phòng** trên menu.
*   **Cách thức hoạt động**:
    *   *Giao diện Sơ đồ*: Hệ thống lấy danh sách phòng trọ và render thành dạng lưới các ô card màu sắc tượng trưng cho trạng thái phòng: Màu xanh (Trống), Màu đỏ (Đang thuê), Màu xám/vàng (Bảo trì).
    *   *Xem nhanh hợp đồng*: Khi click vào một phòng đang thuê trên sơ đồ, AJAX gọi API lấy thông tin người thuê đại diện, các thành viên ở ghép và dịch vụ đã đăng ký để hiển thị lên popup mà không cần chuyển trang.
    *   *Thanh toán nhanh*: Click vào phòng nợ tiền trên sơ đồ sẽ hiện nhanh chi tiết hóa đơn của tháng và cho phép xác nhận đóng tiền trực tiếp tại chỗ.
    *   *Đăng ký dịch vụ*: Tại trang danh sách phòng, người dùng bấm nút dịch vụ để mở popup đăng ký các dịch vụ (ví dụ: số lượng xe máy gửi, mạng Internet) cho phòng đó.

### 5. Quản lý Người thuê (Khách thuê)

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Người thuê**.
*   **Cách thức hoạt động**:
    *   Lưu trữ thông tin cá nhân khách hàng. Khi thêm mới/sửa đổi, hệ thống kiểm trùng chặt chẽ cột CCCD, Số điện thoại và Email trong database để tránh trùng lặp dữ liệu.
    *   Tích hợp API autocomplete để tự động gợi ý thông tin khách thuê khi người dùng nhập liệu ở trang tạo Hợp đồng.

### 6. Quản lý Hợp đồng & Thành viên ở ghép

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Hợp đồng**.
    *   *Danh sách thành viên*: Giao diện quản lý thành viên ở ghép được nhúng động qua Partial View `_DanhSachThanhVienPartial.cshtml` và hiển thị trên tab/nút "Thành viên".
*   **Cách thức hoạt động**:
    *   *Lập hợp đồng*: Khi tạo hợp đồng mới, hệ thống chuyển trạng thái phòng sang "Đã thuê", đồng thời tự động gán các dịch vụ mặc định bắt buộc (Điện, Nước) vào danh sách đăng ký dịch vụ của phòng.
    *   **Tự động cấp tài khoản đăng nhập Khách Thuê**: Khi hợp đồng được lưu thành công (`CreateAsync`), hệ thống sẽ kiểm tra xem người đại diện ký hợp đồng đã có tài khoản trong bảng `NguoiDungs` chưa. Nếu chưa có và người thuê có email hợp lệ, hệ thống tự động tạo một tài khoản đăng nhập với Tên đăng nhập là Email và Mật khẩu mặc định ban đầu là Số điện thoại của người thuê đó (được băm SHA256 bảo mật). Khách thuê có thể dùng tài khoản này để đăng nhập ngay vào Tenant Portal.
    *   **Bảo vệ điều khoản dạng Snapshot (`HopDongDieuKhoan`)**: Khi lưu hợp đồng, hệ thống sẽ thực hiện sao chép toàn bộ tiêu đề và nội dung các điều khoản mẫu được chọn tại thời điểm đó và lưu độc lập vào bảng `HopDongDieuKhoans`. Việc snapshot dữ liệu này đảm bảo tính pháp lý nguyên bản của hợp đồng, giúp tránh trường hợp Admin chỉnh sửa hoặc xóa điều khoản mẫu ở tương lai làm thay đổi sai lệch nội dung hợp đồng cũ.
    *   *Quản lý thành viên*: Khi thêm thành viên qua Modal thêm thành viên, hệ thống hỗ trợ autocomplete tìm kiếm khách cũ hoặc nhập mới hoàn toàn. Hệ thống đếm số người ở thực tế (bao gồm khách đại diện + thành viên cũ đang ở) so khớp với sức chứa tối đa của phòng (`SoNguoiToiDa`). Nếu vượt quá, hệ thống sẽ chặn hành động.
    *   *Báo rời phòng*: Ghi nhận ngày rời phòng là ngày hiện tại của thành viên để lưu lịch sử tạm trú và giảm số người ở thực tế của phòng.
    *   *Xuất hợp đồng sang Word (.docx)*: Cho phép người dùng xuất toàn bộ nội dung hợp đồng (bao gồm thông tin khách thuê, thông tin chi nhánh, các điều khoản và bảng đăng ký dịch vụ) ra file Word `.docx` định dạng A4 chuẩn bằng `WordExportService`. Tính năng này được gọi thông qua AJAX hoặc tải trực tiếp về máy từ bộ nhớ RAM, rất hữu ích khi người dùng không kết nối trực tiếp với máy in để in PDF mà cần lưu trữ file offline hoặc tự chỉnh sửa nội dung bằng Microsoft Word.

### 7. Quản lý Dịch vụ & Đăng ký dịch vụ

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Dịch vụ**.
    *   *Đăng ký dịch vụ cho phòng*: Được thiết kế dưới dạng Partial View `_DangKyDichVuPartial.cshtml` nhúng vào Modal/Tab của trang Hợp đồng và trang Phòng trọ để quản lý đăng ký dịch vụ động.
*   **Cách thức hoạt động**:
    *   Định nghĩa danh mục dịch vụ chung của toàn hệ thống.
    *   Cho phép thiết lập đơn giá dịch vụ riêng cho từng chi nhánh khác nhau. Để tránh dữ liệu sai lệch, database quy định Unique Index trên cặp khóa `(ChiNhanhId, DichVuId)`.
    *   *Giao diện đăng ký dịch vụ động*: AJAX tự động load các dịch vụ của chi nhánh, phân nhóm dịch vụ bắt buộc (không checkbox) và tùy chọn (có checkbox). Hỗ trợ nhập số lượng sử dụng (`soLuong`) và ngày bắt đầu áp dụng (`ngayBatDau`) cho từng dịch vụ riêng biệt. Lưu trữ gọn gàng qua gọi API `/DangKyDichVu/Luu`.

### 8. Quản lý Chỉ số Điện nước

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Điện nước**.
*   **Cách thức hoạt động**:
    *   Hiển thị danh sách phòng trọ theo Chi nhánh, Tháng, Năm để ghi nhận số liệu.
    *   *Kế thừa chỉ số*: Hệ thống tự động tìm kiếm chỉ số chốt mới của kỳ tháng trước gán làm chỉ số cũ của kỳ hiện tại.
    *   *Khóa sửa đổi*: Khi hóa đơn của tháng đó đã được phát sinh chính thức, thuộc tính `IsLocked` của chỉ số điện nước chuyển thành `true`. Hệ thống sẽ khóa toàn bộ ô nhập liệu và chặn API sửa đổi để bảo vệ tính nhất quán tài chính.

### 9. Quản lý Hóa đơn

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Hóa đơn**. Các tính năng thanh toán, xuất QR, xuất Excel/PDF, gửi mail tích hợp tại đây.
*   **Cách thức hoạt động**:
    *   *Công thức tính*: Tổng tiền = Tiền phòng thỏa thuận + (Điện mới - Điện cũ) × Giá điện + (Nước mới - Nước cũ) × Giá nước + Tổng chi phí dịch vụ đăng ký khác.
    *   *Xem trước (Preview)*: Cho phép xem bảng kê chi tiết số tiền của từng phòng trước khi nhấn nút phát sinh chính thức.
    *   *Phát sinh hóa đơn*: Lưu hóa đơn chính thức, tự động sinh mã hóa đơn dạng `HD-yyyyMM-id`, và chuyển trạng thái chỉ số điện nước của tháng sang "Đã khóa".
    *   *Sinh mã VietQR động*: Khi bấm nút thanh toán QR, `VietQRHelper` đọc tài khoản ngân hàng cấu hình ở `appsettings.json`, số tiền, nội dung chuyển khoản động chứa mã hóa đơn và tạo chuỗi VietQR chuẩn EMVCo, sau đó dùng `QRCoder` kết xuất thành ảnh PNG offline trả về giao diện.
    *   *Xuất Excel/PDF*: ClosedXML vẽ bảng tính Excel báo cáo và QuestPDF thiết kế bố cục xuất hóa đơn PDF sắc nét để tải về trực tiếp từ bộ nhớ RAM.
    *   *Gửi Email tự động (Đơn lẻ & Hàng loạt) & Nhắc nợ tự động (Background Service)*:
        *   *Gửi đơn lẻ*: Tải PDF hóa đơn sinh từ RAM và gửi qua SMTP (cấu hình trong `appsettings.json`).
        *   *Gửi hàng loạt*: Hỗ trợ mở Modal gửi email hàng loạt cho toàn bộ các hóa đơn chưa thanh toán của chi nhánh được chọn, cập nhật thanh tiến trình gửi qua SweetAlert2.
        *   *Tác vụ chạy nền nhắc nợ tự động*: Chạy ngầm định kỳ mỗi giờ (`InvoiceReminderService` kế thừa `BackgroundService`), tự động quét các hóa đơn chưa thanh toán quá 5 ngày và gửi email nhắc nợ. Lưu lại lịch sử gửi vào `sent_reminders.json` để chống trùng lặp.
        *   *Chữ ký chi nhánh*: Tự động điền lời chúc trân trọng và hotline, địa chỉ chi nhánh động từ database.
        *   *Tương thích tối*: Email hiển thị responsive hỗ trợ `prefers-color-scheme: dark` tự động chuyển đổi tông màu và có khung viền trắng bao quanh mã QR để quét dễ dàng.

### 10. Quản lý Lịch sử Thanh toán & Giao dịch

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Lịch sử thanh toán**.
*   **Cách thức hoạt động**:
    *   Lưu lại toàn bộ lịch sử nộp tiền mặt hoặc chuyển khoản quét mã của khách thuê.
    *   *Giao diện lọc*: Hỗ trợ lọc giao dịch theo chi nhánh, phương thức đóng tiền, và khoảng ngày (sử dụng Flatpickr Date Range).
    *   *Hoàn tác giao dịch (Hủy thanh toán)*: Khi Admin bấm hủy một giao dịch do nhân viên nhập nhầm, hệ thống xóa mềm giao dịch đó, khôi phục trạng thái hóa đơn tương ứng về "Chưa thanh toán", đưa số tiền đã thanh toán của hóa đơn về 0 và ghi nhận lại ID của quản trị viên thực hiện để phục vụ kiểm toán nội bộ.

### 11. Quản lý Điều khoản mẫu

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Điều khoản mẫu**.
*   **Cách thức hoạt động**:
    *   *Thiết kế SPA (Single-Page Application)*: Nhằm mang lại trải nghiệm tối ưu, toàn bộ giao diện quản lý được tập trung trên một trang duy nhất (`Index.cshtml`). Các trang con thêm mới (`Create.cshtml`) và chỉnh sửa (`Edit.cshtml`) riêng lẻ trước đây đã bị loại bỏ để giảm tải thời gian chuyển hướng trang.
    *   *Quản lý qua AJAX & Modals*: Khi nhân viên bấm nút "Thêm điều khoản" hoặc biểu tượng "Sửa" trên danh sách, một Bootstrap Modal sẽ xuất hiện chứa form nhập liệu (gồm Tiêu đề và Nội dung điều khoản). Khi lưu, dữ liệu được truyền bất đồng bộ thông qua AJAX lên Controller API (`/DieuKhoanMau/Save`), sau đó DataTables sẽ tự động tải lại danh sách dưới nền mà không tải lại toàn bộ trang.
    *   *Xóa mềm tích hợp SweetAlert2*: Khi bấm nút xóa, SweetAlert2 sẽ hiển thị popup cảnh báo. Nếu được xác nhận, AJAX sẽ gửi yêu cầu xóa mềm (`IsDeleted = true`), đảm bảo dữ liệu lịch sử của các hợp đồng cũ liên kết điều khoản này không bị ảnh hưởng.
    *   *Tương thích Dark Mode & Thống nhất giao diện*: Giao diện bảng (Table) đã được căn chỉnh khoảng cách lề (margin/padding) hợp lý với khối Card phía trên, đảm bảo độ thẩm mỹ cao và tương thích hoàn hảo với chế độ tối của Tabler Theme.

### 12. Trợ lý AI Vận Hành (AiAssistant)

*   **Giao diện xuất hiện**: Widget chat nổi (Floating Action Button - FAB) màu tím có biểu tượng robot ở góc dưới bên phải màn hình trên tất cả các trang quản lý của Admin.
*   **Cách thức hoạt động**:
    *   *Tích hợp API Gemini*: Widget chat gửi tin nhắn qua AJAX lên API `/QuanLyNhaTro/AiAssistant/Chat` để giao tiếp với `AiAssistantService`. Lịch sử chat được lưu trữ tạm thời trong `sessionStorage` (tối đa 16 tin nhắn) để giữ ngữ cảnh hội thoại.
    *   *Cơ chế Function Calling*: Trợ lý AI được huấn luyện để tự động nhận diện ý định của người dùng và gọi các hàm nghiệp vụ C# để truy vấn trực tiếp cơ sở dữ liệu hệ thống thông qua **9 chức năng tích hợp**:
        1. `GetPhongTrongAsync`: Tìm kiếm phòng trống (có hỗ trợ lọc theo mức giá tối đa).
        2. `GetHopDongSapHetHanAsync`: Tra cứu các hợp đồng chuẩn bị hết hạn trong vòng X ngày tới.
        3. `GetThongTinKhachThueAsync`: Tìm kiếm thông tin khách thuê (tên, số điện thoại) theo tên khách hoặc theo số phòng.
        4. `GetPhongTroChuaChotDienNuocAsync`: Tìm danh sách phòng chưa chốt chỉ số điện nước trong tháng hiện tại.
        5. `GetCongNoPhongAsync`: Kiểm tra tổng số dư công nợ chưa đóng của một phòng cụ thể.
        6. `GetHoaDonChuaThanhToanAsync`: Liệt kê các hóa đơn chưa đóng tiền của một tháng/năm chỉ định.
        7. `GetDoanhThuThucThuAsync`: Tính tổng doanh thu thực nhận (tiền mặt & chuyển khoản) trong một khoảng thời gian.
        8. `GetDoanhThuChiNhanhAsync`: Thống kê doanh thu thực thu phân nhóm theo từng chi nhánh cụ thể.
        9. `GetChiSoDienNuocAsync`: Xem chỉ số điện nước và lượng tiêu thụ thực tế của phòng trong kỳ trước.
    *   *Trình diễn Markdown & Bảng biểu*: Phản hồi từ AI được định dạng tự động bằng mã JavaScript Custom Parser sang HTML, hỗ trợ in đậm, danh sách gạch đầu dòng và tự động vẽ bảng dữ liệu dạng lưới kẻ viền đẹp mắt, tương thích hoàn toàn với chế độ tối (Dark Mode).

### 13. Quản lý Sự cố & Phản hồi (`YeuCauSuCoController`)

*   **Giao diện xuất hiện**: Truy cập từ Menu: **Quản lý nhà trọ** -> **Quản lý sự cố**.
*   **Cách thức hoạt động**:
    *   Hiển thị danh sách các báo cáo sự cố hư hỏng (điện, nước, thiết bị) từ khách thuê.
    *   *Xem hình ảnh sự cố Cloudinary*: Nhấp vào biểu tượng hình ảnh để xem phóng to ảnh sự cố thực tế được tải lên Cloudinary.
    *   *Cập nhật trạng thái & Phản hồi*: Admin đổi trạng thái xử lý (*ChoTiepNhan -> DangXuLy -> HoanThanh*), nhập ghi chú giải pháp/kế hoạch sửa chữa để thông báo ngược lại cho khách thuê.

### 14. Hệ thống Thông báo Real-time (`ThongBaoController` & `ThongBaoHub`)

*   **Giao diện xuất hiện**: Chuông thông báo góc trên màn hình và các thông báo pop-up nổi (Toastr/SweetAlert2).
*   **Cách thức hoạt động**:
    *   Tích hợp **SignalR WebSockets** tại đường dẫn `/thongBaoHub`.
    *   Khi khách thuê gửi báo cáo sự cố mới hoặc phát sinh giao dịch, server phát thông báo thời gian thực đến toàn bộ kết nối Admin đang mở. Chuông thông báo tự động nhấp nháy phát tiếng kèm đếm số thông báo chưa đọc.

---

### B. PHÂN HỆ KHÁCH THUÊ (TENANT PORTAL - AREA `KHACHTHUE`)

Phân hệ dành riêng cho khách thuê phòng đăng nhập để tự tra cứu thông tin cá nhân và hóa đơn của phòng mình, giúp giảm tải công việc hỗ trợ thủ công của chủ trọ.

### 1. Dashboard Khách thuê

*   **Giao diện xuất hiện**: Xuất hiện ngay sau khi tài khoản vai trò `KhachThue` đăng nhập thành công vào hệ thống.
*   **Cách thức hoạt động**:
    *   Hệ thống kiểm tra xác thực vai trò và lấy mã liên kết hồ sơ `NguoiThueId` từ Claims của User.
    *   Hiển thị thông tin chào mừng mang tên khách thuê lấy từ database.
    *   Hiển thị 2 thẻ chỉ số nhanh: **Tình trạng hợp đồng** (ví dụ: Đang hoạt động) và **Hóa đơn chưa thanh toán** (hiển thị tổng số tiền nợ hiện tại của phòng đó, ví dụ: 0 VNĐ).

### 2. Hồ sơ cá nhân & Đổi mật khẩu (HoSo)

*   **Giao diện xuất hiện**: Truy cập từ Menu bên trái: **Thông tin cá nhân**.
*   **Cách thức hoạt động**:
    *   *Xem hồ sơ*: Hiển thị ảnh đại diện tự động (sinh qua API ui-avatars theo tên), tên, email, số CCCD, số điện thoại và quê quán đã ký kết trên hợp đồng. Các trường này ở trạng thái chỉ đọc (readonly) để bảo toàn tính pháp lý của hợp đồng. Nếu muốn chỉnh sửa, khách thuê được hướng dẫn liên hệ ban quản lý.
    *   *Đổi mật khẩu*: Khách thuê nhập Mật khẩu cũ, Mật khẩu mới và Xác nhận mật khẩu mới. Khi nhấn lưu, AJAX gửi request dạng JSON lên `/KhachThue/HoSo/DoiMatKhau`. Server tiến hành so khớp mật khẩu cũ đã băm SHA256 với database, tiến hành băm SHA256 mật khẩu mới và lưu lại. Toàn bộ thông báo thành công/thất bại được điều phối mượt mà qua SweetAlert2.

### 3. Thông tin Hợp đồng (HopDong)

*   **Giao diện xuất hiện**: Truy cập từ Menu bên trái: **Hợp đồng của tôi**.
*   **Cách thức hoạt động**:
    *   Hiển thị danh sách các hợp đồng thuê phòng mà khách thuê làm người đại diện (bao gồm hợp đồng cũ và hợp đồng đang hoạt động).
    *   *Xem chi tiết hợp đồng*: Bấm nút "Xem chi tiết", AJAX gọi Endpoint `/KhachThue/HopDong/XemChiTiet/{id}` trả về cấu trúc dữ liệu JSON để hiển thị popup Modal:
        - Mã hợp đồng, thời hạn thuê, số tiền đặt cọc phòng và giá thuê phòng thỏa thuận.
        - Danh sách các thành viên ở ghép cùng phòng trọ (Họ tên, SĐT, CCCD, ngày vào ở).
        - Danh sách các dịch vụ đang đăng ký áp dụng cho phòng (Internet, Vệ sinh, Gửi xe...) kèm số lượng và đơn giá thỏa thuận.

### 4. Hóa đơn & QR Thanh toán VietQR (HoaDon)

*   **Giao diện xuất hiện**: Truy cập từ Menu bên trái: **Hóa đơn & Thanh toán**.
*   **Cách thức hoạt động**:
    *   Hiển thị bảng danh sách toàn bộ hóa đơn tiền phòng & dịch vụ của khách thuê theo từng kỳ (Tháng/Năm) kèm mã hóa đơn, tổng tiền, ngày lập và trạng thái (Đã thanh toán / Chưa thanh toán).
    *   *Xem chi tiết phí*: Bấm "Xem chi tiết", popup Modal hiện ra hiển thị bảng kê chi tiết từng khoản phí trong hóa đơn (tiền phòng, số kWh điện tiêu thụ, số khối nước tiêu thụ, và các dịch vụ khác).
    *   *Thanh toán VietQR động*: Đối với hóa đơn chưa thanh toán, hệ thống hiển thị nút **"Thanh toán"**. Khi bấm vào, một popup nổi lên hiển thị mã VietQR động được tạo hoàn toàn offline trên server qua API `/HoaDon/GetVietQR?hoaDonId=...`. Mã QR chứa thông tin số tài khoản của chủ nhà, số tiền hóa đơn và nội dung chuyển khoản tự động: `THANH TOAN [MaHoaDon]`. Khách thuê chỉ cần dùng ứng dụng ngân hàng quét mã để thực hiện chuyển khoản nhanh mà không cần nhập số tiền hay nội dung thủ công, tránh tối đa sai sót.

### 5. Lịch sử thanh toán (LichSuThanhToan)

*   **Giao diện xuất hiện**: Truy cập từ Menu bên trái: **Lịch sử đóng tiền**.
*   **Cách thức hoạt động**:
    *   Hiển thị bảng danh sách các giao dịch đóng tiền nhà thành công của khách thuê (qua quét VietQR hoặc tiền mặt).
    *   Mỗi dòng hiển thị: Mã giao dịch, số tiền đã đóng, phương thức thanh toán, ngày giờ giao dịch và ghi chú của thu ngân.

### 6. Báo cáo & Theo dõi Sự cố (SuCo)

*   **Giao diện xuất hiện**: Truy cập từ Menu bên trái: **Báo cáo sự cố**.
*   **Cách thức hoạt động**:
    *   Khách thuê tạo báo cáo sự cố (tiêu đề, mô tả hư hỏng, chọn mức độ ưu tiên).
    *   *Upload ảnh Cloudinary*: Cho phép chọn/chụp ảnh thực tế từ thiết bị. File ảnh được upload bất đồng bộ trực tiếp lên Cloudinary thông qua `CloudinaryStorageService` và lưu URL an toàn vào bảng `YeuCauSuCos`.
    *   *Theo dõi tiến độ real-time*: Khách xem được trạng thái tiếp nhận và ghi chú phản hồi từ chủ trọ ngay tại danh sách báo cáo.

---

### C. CÁC TÁC VỤ CHẠY NỀN TỰ ĐỘNG (BACKGROUND JOBS)

Hệ thống tích hợp 3 dịch vụ chạy ngầm kế thừa lớp `BackgroundService` của .NET Core để tự động hóa các tác vụ quản lý vận hành mà không cần con người can thiệp:

1.  **Dịch vụ Nhắc nợ Hóa đơn Tự động (`InvoiceReminderService`)**:
    *   *Cơ chế vận hành*: Chạy định kỳ mỗi giờ một lần.
    *   *Logic nghiệp vụ*: Quét bảng `HoaDons` tìm các hóa đơn có trạng thái "Chưa thanh toán" và đã quá hạn 5 ngày kể từ ngày lập (`NgayTao <= now - 5 ngày`). Hệ thống tự động sinh PDF hóa đơn từ bộ nhớ RAM và gửi email thông báo nhắc đóng tiền nhà đính kèm PDF hóa đơn cho khách thuê qua giao thức SMTP.
    *   *Chống lặp*: Lịch sử gửi email nhắc nợ được ghi nhận vào tệp cấu trúc JSON `sent_reminders.json` trên server. Mỗi hóa đơn chỉ gửi nhắc nợ tối đa 1 lần/kỳ để tránh làm phiền khách thuê.
2.  **Dịch vụ Đóng Hợp đồng Hết hạn Tự động (`ContractAutoCloseService`)**:
    *   *Cơ chế vận hành*: Tự động kích hoạt chạy một lần vào lúc nửa đêm hàng ngày (00:00).
    *   *Logic nghiệp vụ*: Quét database tìm các hợp đồng đang ở trạng thái "Đang hoạt động" (`TrangThaiHopDong == DangHoatDong`) nhưng có ngày kết thúc nhỏ hơn ngày hiện tại (`ThoiDiemKetThuc < Today`). Hệ thống mở một DB Transaction thực thi đồng thời:
        - Cập nhật trạng thái hợp đồng sang "Đã kết thúc" (`DaKetThuc`).
        - Cập nhật trạng thái phòng trọ liên kết về "Trống" (`Trong`).
        - Điền ngày chuyển đi (`NgayChuyenDi = Now`) cho toàn bộ thành viên đang ở ghép của hợp đồng đó.
        - Điền ngày kết thúc dịch vụ (`NgayKetThuc = Now`) cho toàn bộ đăng ký dịch vụ của phòng đó.
3.  **Dịch vụ Cảnh báo Hết hạn Hợp đồng (`ContractExpiryAlertService`)**:
    *   *Cơ chế vận hành*: Tự động kích hoạt chạy một lần vào lúc 8:00 sáng hàng ngày.
    *   *Logic nghiệp vụ*: Quét các hợp đồng đang hoạt động có ngày kết thúc. Tính toán số ngày còn lại đến khi hết hạn. Nếu số ngày còn lại khớp với cấu hình trong `appsettings.json` (mặc định là trước **30 ngày** và **15 ngày**), hệ thống tự động soạn email cảnh báo hết hiệu lực hợp đồng gửi đến email người thuê đại diện và gửi một bản sao đến email của Ban quản trị để chủ động lên lịch gia hạn hoặc tìm khách mới.
    *   *Chống lặp*: Ghi nhận các khóa gửi dạng `[HopDongId]_[SoNgayConLai]` vào tệp cấu trúc JSON `sent_contract_alerts.json` để bảo đảm mỗi mốc thời gian cảnh báo chỉ gửi đúng một lần duy nhất.

---

## PHẦN V: HƯỚNG DẪN KHỞI CHẠY & PHÁT TRIỂN (HOW TO RUN)

1.  **Cài đặt môi trường**: Máy tính cần cài sẵn **.NET 8 SDK** và một cơ sở dữ liệu **PostgreSQL** đang chạy.
2.  **Cấu hình kết nối**: Mở file `appsettings.json`, chỉnh sửa chuỗi kết nối tại mục `ConnectionStrings:DefaultConnection` trùng với thông tin Host, Port, Username và Password của PostgreSQL trên máy bạn. Đồng thời cập nhật cấu hình SMTP Email (nếu cần gửi mail) và thông tin tài khoản ngân hàng VietQR.
3.  **Khởi tạo Database**:
    Mở CMD/Terminal tại thư mục gốc dự án chạy lệnh:
    ```bash
    dotnet ef database update
    ```
4.  **Khởi chạy Server**: Chạy lệnh:
    ```bash
    dotnet watch run
    ```
    Trình duyệt sẽ tự động mở trang web. Khi sửa code ở file C# hay HTML, server sẽ tự động reload nhanh chóng.
5.  **Tài khoản mặc định**: Hệ thống tự động tạo tài khoản admin nếu DB trống. Đăng nhập với: Tên đăng nhập `admin` / Mật khẩu `admin123`.
