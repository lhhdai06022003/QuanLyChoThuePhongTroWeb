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

*   **QuestPDF**: Thư viện sinh tệp PDF chất lượng cao dùng để in hợp đồng, xuất hóa đơn hàng tháng.
*   **ClosedXML**: Thư viện đọc/ghi Excel dùng xuất báo cáo kế toán dưới định dạng `.xlsx`.
*   **QRCoder**: Sinh mã QR chuyển khoản VietQR Napas động dưới dạng luồng byte ảnh PNG hoàn toàn offline trên server (không phụ thuộc internet).
*   **SMTP (TLS)**: Gửi email thông báo hóa đơn và nhắc nợ tự động qua tài khoản cấu hình trong `appsettings.json`.

---

## PHẦN II: CẤU TRÚC THƯ MỤC DỰ ÁN

Dự án sử dụng cơ chế **Areas** của ASP.NET Core để cô lập logic nghiệp vụ Quản lý nhà trọ khỏi cấu trúc MVC mặc định.

```text
QuanLyChoThuePhongTroWeb/
│
├── Areas/
│   └── QuanLyNhaTro/             <-- Phân hệ nghiệp vụ Quản lý nhà trọ chính
│       ├── Controllers/          <-- Nhận request và định nghĩa các API Endpoints
│       ├── Models/               <-- Các lớp thực thể (Entity) liên kết trực tiếp với database
│       ├── Services/             <-- Chứa lớp nghiệp vụ (Business Logic) và tính toán tiền phòng
│       ├── ViewModels/           <-- Chứa DTOs (Requests & Responses) truyền tải dữ liệu
│       └── Views/                <-- Giao diện Razor Views (.cshtml) của từng thực thể
│           └── Shared/           <-- Thư mục chứa giao diện dùng chung
│               ├── _DangKyDichVuPartial.cshtml     <-- Giao diện đăng ký dịch vụ động
│               └── _DanhSachThanhVienPartial.cshtml <-- Giao diện danh sách thành viên ở ghép
│
├── Controllers/                  <-- HomeController điều hướng ban đầu
├── Data/
│   └── ApplicationDbContext.cs   <-- Khai báo DbSet và cấu hình mối quan hệ bảng (Fluent API)
├── Migrations/                   <-- Lưu lịch sử thay đổi Database
├── Services/                     <-- Dịch vụ dùng chung hệ thống (MenuService, InvoiceReminderService)
├── wwwroot/                      <-- Thư mục tài nguyên tĩnh (css, js, hình ảnh, Tabler Theme)
├── appsettings.json              <-- Cấu hình kết nối DB, SMTP Email, tài khoản VietQR
└── Program.cs                    <-- Nơi khởi chạy ứng dụng, cấu hình DI, Background Services và Routing
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
    *   *Trang Đăng nhập*: Xuất hiện khi người dùng mở trang web chưa đăng nhập hoặc khi nhấn nút đăng xuất.
    *   *Trang Quản lý tài khoản*: Truy cập từ Menu bên trái: **Quản lý người dùng** -> **Quản lý tài khoản**.
*   **Cách thức hoạt động**:
    *   Sử dụng Cookie Authentication để duy trì phiên đăng nhập của nhân viên (`ClaimsIdentity`).
    *   Mật khẩu được băm mã hóa một chiều bằng thuật toán **SHA256** trước khi so sánh hoặc lưu vào database.
    *   Nếu cơ sở dữ liệu trống, hệ thống tự động chạy cơ chế Seeding để tạo một tài khoản quản trị mặc định (Tên đăng nhập: `admin` / Mật khẩu: `admin123`).
    *   Các hành động của nhân viên được bảo vệ chặt chẽ bởi thuộc tính `[Authorize]` trên Controller.

### 2. Bảng điều khiển (Dashboard)

*   **Giao diện xuất hiện**: Xuất hiện ngay sau khi đăng nhập thành công, hoặc truy cập từ Menu: **Dashboard**.
*   **Cách thức hoạt động**:
    *   Khi tải trang, hệ thống nạp dữ liệu thống kê của chi nhánh đầu tiên và tháng hiện tại.
    *   Khi người dùng thay đổi bộ lọc trên giao diện (Chi nhánh, Tháng, Năm), một sự kiện JavaScript được kích hoạt gửi yêu cầu AJAX về Endpoint `/QuanLyNhaTro/Dashboard/GetAjaxData`. API trả về dữ liệu thống kê dạng JSON để vẽ lại biểu đồ Chart.js (biểu đồ doanh thu 12 tháng, biểu đồ cơ cấu phương thức thanh toán) và cập nhật số liệu trên các thẻ KPI mà không cần tải lại toàn bộ trang.
    *   Hệ thống dùng thuật toán tập hợp để tự động hiển thị mục "Việc cần làm" (To-Dos) như: Lọc danh sách phòng chưa chốt điện nước, các hợp đồng sắp hết hạn trong 30 ngày, và các hóa đơn quá hạn kỳ trước chưa thanh toán.

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
    *   *Quản lý thành viên*: Khi thêm thành viên qua Modal thêm thành viên, hệ thống hỗ trợ autocomplete tìm kiếm khách cũ hoặc nhập mới hoàn toàn. Hệ thống đếm số người ở thực tế (bao gồm khách đại diện + thành viên cũ đang ở) so khớp với sức chứa tối đa của phòng (`SoNguoiToiDa`). Nếu vượt quá, hệ thống sẽ chặn hành động.
    *   *Báo rời phòng*: Ghi nhận ngày rời phòng là ngày hiện tại của thành viên để lưu lịch sử tạm trú và giảm số người ở thực tế của phòng.

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
