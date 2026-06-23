# TÀI LIỆU HƯỚNG DẪN DỰ ÁN (FOR INTERN / NEW DEVELOPER)

Tài liệu này cung cấp cái nhìn tổng quan về hệ thống **Quản lý cho thuê phòng trọ (Rental Management System)**. Được biên soạn nhằm giúp các thành viên mới/thực tập sinh nhanh chóng làm quen với công nghệ, cấu trúc thư mục, quy trình luồng dữ liệu và các quy chuẩn viết code của dự án.

---

## 1. NỀN TẢNG & CÔNG NGHỆ SỬ DỤNG (TECHNOLOGY STACK)

Dự án được phát triển theo mô hình ứng dụng web hiện đại, tập trung vào tính trực quan, hiệu năng cao và khả năng mở rộng.

### A. Backend & Database
- **Framework chính**: .NET 8.0 (ASP.NET Core Web MVC / Web API).
- **Cơ sở dữ liệu**: PostgreSQL (hệ quản trị cơ sở dữ liệu quan hệ nguồn mở mạnh mẽ).
- **ORM**: Entity Framework Core 8 (EF Core) thông qua thư viện kết nối `Npgsql.EntityFrameworkCore.PostgreSQL`. Quản lý cấu trúc bảng bằng giải pháp **Code-First Migrations**.
- **Thư viện xuất bản file**:
  - **ClosedXML**: Dùng để xử lý đọc/ghi và xuất dữ liệu báo cáo ra file Excel (.xlsx).
  - **QuestPDF**: Thư viện thế hệ mới để xuất hóa đơn/hợp đồng ra file PDF chất lượng cao.
  - **QRCoder**: Dùng để sinh mã QR chuyển khoản ngân hàng nhanh tiêu chuẩn VietQR động, hoàn toàn offline trên server.
- **Tác vụ nền**: `InvoiceReminderService` (kế thừa `BackgroundService`) chạy định kỳ mỗi giờ, tự động quét và gửi email nhắc nợ.

### B. Frontend & Libraries
- **CSS Framework**: **Tabler Theme** (được phát triển trên nền tảng **Bootstrap 5**), mang phong cách UI chuyên nghiệp, hỗ trợ tối ưu giao diện sáng/tối (Dark/Light mode) và các hiệu ứng động.
- **Javascript Core**: **jQuery** (hỗ trợ DOM Manipulation và gọi AJAX dễ dàng).
- **Các thư viện UI bổ sung**:
  - **jQuery DataTables**: Dành cho các bảng danh sách lớn, hỗ trợ phân trang, sắp xếp và tìm kiếm máy chủ (Server-side Processing).
  - **Flatpickr**: Bộ chọn ngày/khoảng ngày (Date Range Picker) hiện đại, hỗ trợ tiếng Việt.
  - **SweetAlert2**: Hộp thoại (popup/alert) thông báo xác nhận và trạng thái đẹp mắt.
  - **Select2**: Thẻ chọn dropdown hỗ trợ tự động tìm kiếm (autocomplete).
  - **FontAwesome 6**: Hệ thống icons vector dùng xuyên suốt hệ thống.

---

## 2. CẤU TRÚC THƯ MỤC DỰ ÁN (DIRECTORY STRUCTURE)

Dự án phân chia các module chính vào thư mục `Areas` để cô lập logic nghiệp vụ cụ thể khỏi cấu trúc MVC mặc định.

```text
QuanLyChoThuePhongTroWeb/
│
├── Areas/
│   └── QuanLyNhaTro/             <-- Phân hệ nghiệp vụ Quản lý nhà trọ chính
│       ├── Controllers/          <-- Nơi nhận request, điều phối hiển thị và API Endpoints
│       ├── Models/               <-- Các Entity Map trực tiếp với bảng PostgreSQL qua EF Core
│       ├── Services/             <-- Chứa lớp nghiệp vụ (Business Logic), truy vấn cơ sở dữ liệu
│       ├── ViewModels/           <-- Chứa DTOs, Requests và Responses phục vụ truyền tải dữ liệu
│       └── Views/                <-- Giao diện Razor Views (.cshtml) phân chia theo thực thể
│           └── Shared/           <-- Giao diện dùng chung (_DangKyDichVuPartial, _DanhSachThanhVienPartial)
│
├── Data/
│   └── ApplicationDbContext.cs   <-- Khai báo DbSet, cấu hình quan hệ (Fluent API) và Seed dữ liệu
│
├── Services/                     <-- Các dịch vụ dùng chung hệ thống (MenuService, InvoiceReminderService)
│
├── wwwroot/                      <-- Thư mục chứa tài nguyên tĩnh
│   ├── css/                      <-- File CSS tự viết phục vụ từng module (hopdong.css, hoadon.css...)
│   └── Theme/                    <-- Thư mục chứa thư viện Tabler, JS, hình ảnh logo...
│
├── appsettings.json              <-- Cấu hình kết nối DB (PostgreSQL), tham số SMTP, VietQR
├── Program.cs                    <-- Nơi khởi chạy ứng dụng, cấu hình DI (Dependency Injection), Routing
├── HuongDanToanDienDuAn.md       <-- Tài liệu hướng dẫn chi tiết toàn diện cho lập trình viên mới
├── tai_lieu_chuc_nang_he_thong.md <-- Tài liệu mô tả chi tiết các chức năng nghiệp vụ hệ thống
└── README.md                     <-- Chính là tài liệu hướng dẫn tổng quan này
```

---

## 3. MÔ TẢ CÁC MODULE NGHIỆP VỤ CHÍNH

1. **Dashboard (Bảng điều khiển)**:
   - Hiển thị các chỉ số vận hành quan trọng như doanh thu thực thu, doanh thu chờ thu, số phòng trống/đã thuê, tỷ lệ lấp đầy.
   - Chứa biểu đồ cột chồng doanh thu 12 tháng, cơ cấu phương thức thanh toán, bảng việc cần làm/cảnh báo và timeline hoạt động.
2. **Chi nhánh (Branches)**: Quản lý nhiều cơ sở nhà trọ khác nhau. Mỗi chi nhánh có bảng giá dịch vụ riêng biệt.
3. **Phòng trọ (Rooms)**: Quản lý số phòng, đơn giá thuê gốc, diện tích và trạng thái phòng (Trống / Đã thuê / Đang bảo trì). Hỗ trợ sơ đồ phòng trực quan và thanh toán nhanh.
4. **Khách thuê (Tenants)**: Lưu trữ thông tin cá nhân khách thuê đại diện và các thành viên ở ghép. Tích hợp autocomplete khi lập hợp đồng.
5. **Hợp đồng (Contracts)**: Quản lý thời hạn thuê phòng, số tiền cọc, giá thuê thỏa thuận. Tích hợp quản lý thành viên ở ghép (`_DanhSachThanhVienPartial`) và danh sách dịch vụ đăng ký đi kèm (`_DangKyDichVuPartial`).
6. **Dịch vụ (Services)**: Định nghĩa danh mục dịch vụ và cấu hình bảng giá riêng theo từng chi nhánh.
7. **Chỉ số Điện nước (Utilities)**: Ghi chỉ số điện/nước hàng tháng của từng phòng trọ. Hỗ trợ kế thừa chỉ số và khóa dữ liệu sau khi phát sinh hóa đơn.
8. **Hóa đơn (Invoices)**: Tự động phát sinh hóa đơn theo kỳ dựa vào tiền phòng thỏa thuận, tiền điện/nước tiêu thụ thực tế và các dịch vụ đăng ký. Hỗ trợ sinh VietQR chuyển khoản, in báo cáo PDF/Excel, gửi email thông báo đơn lẻ hoặc hàng loạt, và chạy ngầm nhắc nợ tự động (`InvoiceReminderService`).
9. **Lịch sử Thanh toán (Payment History)**: Quản lý toàn bộ giao dịch đóng tiền mặt hoặc quét VietQR chuyển khoản, cho phép Admin thực hiện hủy/hoàn tác giao dịch thu tiền khi bị lỗi.

---

## 4. CÁC QUY CHUẨN VIẾT CODE QUAN TRỌNG (CODING GUIDELINES)

### A. Mô hình Controller - Service Layer
Để code sạch và dễ viết kiểm thử, **không** viết logic nghiệp vụ (như tính toán tiền phòng, kiểm tra trạng thái...) hay truy vấn DbContext trực tiếp tại Controller.
- **Controller**: Nhận tham số, kiểm tra `ModelState.IsValid`, gọi Service tương ứng và trả về `View()` hoặc `Json()`.
- **Service**: Định nghĩa interface (ví dụ: `ILichSuThanhToanService`) và lớp triển khai (`LichSuThanhToanService`) thực thi logic nghiệp vụ và lưu trữ dữ liệu.
- *Lưu ý*: Đăng ký dịch vụ mới vào DI Container trong `Program.cs` sử dụng `builder.Services.AddScoped<IService, Service>();`.

### B. Quy trình làm việc với jQuery DataTables (Server-side)
Khi làm việc với danh sách cần phân trang phía server:
1. Định nghĩa yêu cầu phân trang bằng lớp `DataTableRequest` trong ViewModels.
2. Trả dữ liệu về bằng lớp generic `DataTableResponse<T>`.
3. Trong Controller, bắt dữ liệu từ form DataTables gửi lên và chuyển tiếp sang Service xử lý:
   ```csharp
   var request = new DataTableRequest {
       Draw = int.Parse(Request.Form["draw"].FirstOrDefault()),
       Start = int.Parse(Request.Form["start"].FirstOrDefault()),
       Length = int.Parse(Request.Form["length"].FirstOrDefault()),
       SearchValue = Request.Form["search[value]"].FirstOrDefault()
   };
   ```

### C. Quy chuẩn xử lý thời gian (Time Zone)
- Cơ sở dữ liệu PostgreSQL lưu trữ thời gian ở chuẩn **UTC** (DateTimeKind.Utc).
- Khi hiển thị ra giao diện người dùng (hoặc xuất Excel/PDF), cần cộng thêm 7 tiếng (GMT+7 - Giờ Việt Nam) bằng hàm `.AddHours(7)` và định dạng chuỗi thích hợp (ví dụ: `dd/MM/yyyy HH:mm`).

### D. Đồng bộ chế độ tối (Dark Mode) cho bộ lọc
Hệ thống sử dụng các class của theme Tabler. Để tránh bị lỗi hiển thị nền trắng chữ trắng ở chế độ tối:
- **Tuyệt đối không** sử dụng class `bg-white` trên các ô nhập liệu dạng văn bản hoặc bộ chọn ngày. Hãy để class mặc định `.form-control` tự động điều chỉnh.
- Sử dụng các CSS Variables dùng chung như `var(--tblr-bg-surface)` hay `var(--tblr-body-color)` khi viết CSS tùy biến để tự động tương thích với Dark Mode.

### E. Xóa mềm (Soft Delete)
- Mọi thực thể chính đều có trường `IsDeleted`. Khi thực hiện hành động xóa, chỉ cập nhật `IsDeleted = true`.
- Luôn lọc dữ liệu bằng `.Where(x => !x.IsDeleted)`. Không dùng lệnh xóa cứng khỏi database nhằm bảo toàn dữ liệu lịch sử.

---

## 5. HƯỚNG DẪN KHỞI CHẠY & PHÁT TRIỂN (HOW TO RUN)

1. **Cài đặt môi trường**: Yêu cầu máy cài sẵn **.NET 8 SDK** và một cơ sở dữ liệu **PostgreSQL** đang chạy.
2. **Cập nhật chuỗi kết nối**: Mở file `appsettings.json`, điều chỉnh mục `ConnectionStrings:DefaultConnection` trùng với thông tin Host, Port, Username và Password PostgreSQL của bạn.
3. **Cập nhật Database (Migrations)**:
   Mở CMD/Terminal tại thư mục gốc dự án và chạy:
   ```bash
   dotnet ef database update
   ```
4. **Khởi chạy Server**:
   ```bash
   dotnet watch run
   ```
   Trình duyệt sẽ tự động mở trang web. Khi sửa code ở file C# hay HTML, server sẽ tự động reload giúp quá trình phát triển nhanh chóng hơn.


