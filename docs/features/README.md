# Danh mục chức năng

Tài liệu này liệt kê chức năng đã có trong mã nguồn hiện tại. Nội dung roadmap được tách riêng ở cuối để tránh nhầm một thiết kế dự kiến với tính năng đang vận hành.

## Theo dõi 4 module của kế hoạch 60 ngày

Các README sau mô tả **công việc dự kiến và người sở hữu**, tách khỏi danh mục chức năng đang vận hành ở phần dưới. Mỗi thư mục có `README.md`, `spec/`, `plan/`, `review/`. Ba thư mục sau chỉ có `.gitkeep`; tài liệu được viết khi module bắt đầu thực thi.

| Module | Người phụ trách | Hiện trạng |
| --- | --- | --- |
| [Ảnh chỉ số, OCR và hóa đơn](anh-chi-so-hoa-don/README.md) | A | Nền database và hóa đơn cũ đã có; luồng mới chưa hoàn chỉnh |
| [Thanh toán hóa đơn](thanh-toan-hoa-don/README.md) | A | VietQR và một phần Application Service đã có; Web chưa nối đủ |
| [Phòng công khai và lịch xem](phong-cong-khai-lich-xem/README.md) | B | Có model/schema; chưa có luồng công khai/đặt lịch hoàn chỉnh |
| [Khách vãng lai, giữ chỗ, cọc và hoàn tiền](khach-vang-lai-giu-cho/README.md) | B | Có model/schema; chưa có luồng Application/Web hoàn chỉnh |

Quyền sở hữu file và lịch 60 ngày nằm trong [roadmap](../roadmap-60-ngay.md). Không đánh dấu hoàn thành chỉ vì đã có entity, migration hoặc thư mục tài liệu.


## Vai trò người dùng

| Vai trò | Phạm vi hiện tại |
| --- | --- |
| Admin | Truy cập cổng quản lý, tài khoản, cấu hình nghiệp vụ và các thao tác quản trị |
| NhanVien | Truy cập cổng quản lý theo các controller dùng AdminBaseController; entity phân công chi nhánh đã có nhưng luồng phân quyền mới chưa hoàn chỉnh |
| KhachThue | Xem dữ liệu gắn với claim NguoiThueId: dashboard, hồ sơ, hợp đồng, hóa đơn, thanh toán, sự cố và thông báo |

## Cổng quản lý

### Dashboard

- Tổng hợp dữ liệu vận hành theo chi nhánh, tháng và năm.
- Hiển thị số phòng, hợp đồng, hóa đơn, doanh thu và các mục cần xử lý.
- Dùng DashboardService ở Application và DashboardStore ở Infrastructure.

### Chi nhánh và phòng trọ

- Tạo, đọc, cập nhật và xóa mềm chi nhánh/phòng.
- Trạng thái phòng: Trong, DaThue, BaoTri.
- Sơ đồ phòng, thông tin hợp đồng nhanh và hóa đơn chưa thanh toán theo phòng.
- Giá dịch vụ được cấu hình riêng theo chi nhánh.

### Người thuê và tài khoản

- Quản lý hồ sơ người thuê, tìm kiếm autocomplete và danh sách người chưa có phòng.
- Quản lý tài khoản với ba role Admin, NhanVien, KhachThue.
- Liên kết tài khoản khách thuê với NguoiThueId.
- Đổi/reset mật khẩu qua IPasswordService; hash mới là PBKDF2, hash SHA256 cũ được nâng cấp khi đăng nhập.

### Hợp đồng và thành viên

- Tạo, cập nhật, xem chi tiết, in/xuất hợp đồng và xóa mềm.
- Trạng thái hợp đồng: DangHoatDong, DaKetThuc, DaHuy.
- Lưu giá thuê thỏa thuận, tiền cọc, thời hạn, phòng và người thuê đại diện.
- Quản lý thành viên ở ghép, ngày chuyển vào/ra và dịch vụ đăng ký.
- Sao chép nội dung điều khoản mẫu vào hợp đồng để giữ snapshot lịch sử.
- Job tự kết thúc hợp đồng hết hạn, giải phóng phòng và đóng thành viên/dịch vụ liên quan.

### Dịch vụ và điện nước

- Quản lý danh mục dịch vụ và bảng giá theo chi nhánh.
- Đăng ký dịch vụ cho phòng/hợp đồng theo thời gian hiệu lực.
- Chốt chỉ số điện nước theo phòng, tháng và năm.
- Chặn các tình huống nghiệp vụ không hợp lệ trong Application service/store.

### Hóa đơn

- Xem trước và phát sinh hóa đơn theo kỳ cho các phòng được chọn.
- Tính tiền phòng, điện/nước và dịch vụ đăng ký.
- Chỉnh sửa, xem chi tiết và xóa mềm hóa đơn theo quy tắc hiện tại.
- Ghi nhận thu tiền mặt hoặc chuyển khoản và người xác nhận.
- Xuất Excel/PDF, gửi email kèm PDF và sinh VietQR.
- Giao diện vận hành hiện chủ yếu dùng trạng thái thanh toán ChuaThanhToan/DaThanhToan; model trạng thái phát hành đã có nhưng luồng nháp/chốt chưa hoàn chỉnh.

### Lịch sử thanh toán

- Danh sách và thống kê giao dịch theo chi nhánh/khoảng ngày.
- Hủy giao dịch theo nghiệp vụ, lưu người thực hiện.
- Khách thuê xem lịch sử giao dịch của chính mình.

### Điều khoản mẫu

- CRUD điều khoản dùng khi lập hợp đồng.
- Hợp đồng đã tạo giữ bản sao nội dung, không phụ thuộc việc mẫu thay đổi sau này.

### Sự cố

- Khách thuê tạo yêu cầu sự cố và tải ảnh qua Cloudinary.
- Quản lý/nhân viên lọc theo chi nhánh, trạng thái và khoảng thời gian.
- Trạng thái: ChoTiepNhan, DangXuLy, DaHoanThanh, DaHuy.
- Có thể ghi chi phí, lý do từ chối, ghi chú quản lý và lựa chọn cộng chi phí vào hóa đơn.

### Thông báo thời gian thực

- Lưu thông báo theo người dùng hoặc role.
- Đếm chưa đọc, đánh dấu một/tất cả đã đọc và xóa thông báo.
- SignalR hub tại /thongBaoHub đẩy cập nhật tới giao diện đang kết nối.

### Trợ lý AI

- Widget và controller dành cho cổng quản lý.
- Infrastructure gọi Gemini và cung cấp các công cụ truy vấn dữ liệu vận hành như phòng, hóa đơn, điện nước và doanh thu.
- AI đọc dữ liệu qua service hạ tầng; không được xem là nguồn quyết định thanh toán hoặc thay thế validation nghiệp vụ.

## Cổng khách thuê

Khách thuê đăng nhập bằng cookie và phải có role KhachThue cùng claim NguoiThueId hợp lệ.

- Dashboard: tổng quan hồ sơ và tổng tiền hóa đơn chưa thanh toán.
- Hồ sơ: xem thông tin cá nhân và đổi mật khẩu.
- Hợp đồng: xem danh sách và chi tiết hợp đồng của mình.
- Hóa đơn: xem danh sách/chi tiết, quyền sở hữu hóa đơn được kiểm tra ở server.
- Lịch sử thanh toán: xem các giao dịch liên quan.
- Sự cố: tạo và theo dõi yêu cầu của mình.
- Thông báo: xem và quản lý trạng thái đã đọc.

## Tích hợp và tác vụ tự động

| Thành phần | Chức năng | Điều kiện cấu hình |
| --- | --- | --- |
| PostgreSQL/EF Core | Dữ liệu nghiệp vụ và migrations | ConnectionStrings:DefaultConnection |
| MailKit SMTP | Hóa đơn và cảnh báo hợp đồng | EmailSettings |
| Cloudinary | Ảnh sự cố | Cloudinary |
| Gemini | Trợ lý AI | Gemini:ApiKey, Gemini:Model |
| QRCoder/VietQR | QR chuyển khoản | VietQRSettings |
| ClosedXML/QuestPDF/DocX | Excel, PDF và Word | Không cần dịch vụ ngoài |
| SignalR | Thông báo realtime | Kết nối hub /thongBaoHub |

Các job nền chạy trong cùng process Web. BackgroundJobs:Enabled=false tắt việc đăng ký hosted jobs, nhưng không tắt migrations/seed lúc khởi động.

## Chưa triển khai

Các mục sau có tài liệu thiết kế hoặc thư mục khung nhưng chưa có luồng hoàn chỉnh trong mã nguồn:

- API JSON /api/v1.
- Cổng khách vãng lai và đăng phòng công khai.
- Đặt lịch xem, giữ chỗ và chuyển tiền giữ chỗ sang cọc.
- Xác nhận ảnh chuyển khoản, trả một phần và hoàn tiền giữ chỗ.
- OCR chỉ số điện/nước và bước người dùng duyệt kết quả.
- Hóa đơn nháp và quy trình duyệt trước khi gửi.
- Mobile app, IoT và CI workflow.

Migration 1 và Migration 2 đã có trong mã nguồn, lần lượt thêm 5 và 15 bảng; EF model và kiểm thử kỳ vọng 37 bảng nghiệp vụ. Lần đối chiếu này chưa xác minh lại database đang chạy. Xem [roadmap 60 ngày](../roadmap-60-ngay.md), [spec hai migration](../superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md) và hai kế hoạch triển khai trong [superpowers/plans](../superpowers/plans/).
