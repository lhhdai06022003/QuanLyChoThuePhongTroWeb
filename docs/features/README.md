# Danh mục chức năng

Tài liệu này liệt kê chức năng đã có trong mã nguồn hiện tại. Nội dung roadmap được tách riêng ở cuối để tránh nhầm một thiết kế dự kiến với tính năng đang vận hành.

## Theo dõi module

Các README sau mô tả **công việc dự kiến và người sở hữu**, tách khỏi danh mục chức năng đang vận hành ở phần dưới. Mỗi thư mục có `README.md`, `spec/`, `plan/`, `review/`. Ba thư mục sau chỉ có `.gitkeep`; tài liệu được viết khi module bắt đầu thực thi. Bốn module đầu thuộc kế hoạch 60 ngày; hai module tiếp theo thuộc lộ trình vận hành (bước 2 và bước 3); module cuối là tính năng mở rộng hóa đơn.

| Module | Người phụ trách | Hiện trạng |
| --- | --- | --- |
| [Ảnh chỉ số, OCR và hóa đơn](anh-chi-so-hoa-don/README.md) | A | Đã có trong mã: ảnh đồng hồ, OCR, xác nhận chỉ số và phát hành hóa đơn nháp → chờ duyệt → chốt → gửi (xem hai dòng Hóa đơn tự động bên dưới). Checkbox trong README của module chưa được cập nhật theo mã |
| [Thanh toán hóa đơn](thanh-toan-hoa-don/README.md) | A | VietQR, yêu cầu thanh toán, minh chứng và đối chiếu của nhân viên đã có. Module Thanh toán trực tuyến (đã gộp `master`) nối tiếp và gỡ luồng khách tự tạo lượt/nộp minh chứng hóa đơn. README của module chưa cập nhật |
| [Phòng công khai và lịch xem](phong-cong-khai-lich-xem/README.md) | B | Đã có trong mã: trang `/phong`, ảnh phòng (`AnhPhongTro`), lưu tin phòng và tải ảnh, hẹn xem do khách đề xuất giờ, nhân viên xác nhận/từ chối ở màn Lịch xem phòng. Đặt theo khung giờ không còn trong luồng (mã `ViewingSlotAdmin*` còn nhưng không có giao diện); API chưa có |
| [Khách vãng lai, giữ chỗ, cọc và hoàn tiền](khach-vang-lai-giu-cho/README.md) | B | Đã có trong mã: đăng ký/đăng nhập khách, giữ chỗ, đặt cọc, nộp minh chứng, đối soát, áp tiền giữ chỗ vào cọc, quyết định và ghi nhận hoàn tiền. API chưa có |
| [Cổng khách vãng lai (spec và kế hoạch)](cong-khach-vang-lai/PLAN.md) | thuan | Đã gộp `master` (`dc391e3`, `21eaa73`). `PLAN.md` còn 27 checkbox mở của các task cũ, chưa đối chiếu lại với mã nên chưa tick |
| [AI Assistant và Thông báo theo chi nhánh](ai-thong-bao-chi-nhanh/README.md) | — | Hoàn thành, đã merge vào `master` (commit `8295e36`) ngày 06/10/2026 |
| [Trung tâm việc cần làm](viec-can-lam/README.md) | — | Code xong 2 đợt, review cuối ĐẠT 08/10/2026 ([review](viec-can-lam/review.md)); spec 1.2 đã xác nhận, người dùng thử tay đạt 09/10/2026; đã commit `361575e` |
| [Hóa đơn tự động](hoa-don-tu-dong/plan.md) | — | Code xong đợt A–D và lượt dọn code, review ĐẠT 11/10/2026 ([review](hoa-don-tu-dong/review.md)); đã gộp `master` (commit `c11946c`) |
| [Kiểm tra chỉ số theo hợp đồng và duyệt hóa đơn trước khi gửi](duyet-hoa-don-tu-dong/README.md) | — | Code xong 13 task trên nhánh `feature/duyet-hoa-don-tu-dong` (13/10/2026), review cuối và sửa xong 14/10/2026; đã có trong `master` |
| [Thanh toán trực tuyến — Cổng SePay](thanh-toan-truc-tuyen/sepay/README.md) | — | Thuộc module [thanh toán trực tuyến](thanh-toan-truc-tuyen/README.md). Hỗ trợ cả Hóa đơn (`TTPTHD{Id}`) và Giữ chỗ (`TTPTGC{Id}`). Chuyển khoản VietQR, webhook SePay tự ghi sổ, SignalR realtime, Telegram, email biên lai, đối chiếu màn Giao dịch SePay. Đã hoàn thành, toàn bộ test PASS |
| [Thanh toán trực tuyến — Cổng VNPay](thanh-toan-truc-tuyen/vnpay/README.md) | — | Thuộc module [thanh toán trực tuyến](thanh-toan-truc-tuyen/README.md). Hỗ trợ cả Hóa đơn và Giữ chỗ qua thẻ ATM / Visa / MasterCard / VNPAY-QR. IPN tự ghi sổ, trang kết quả chỉ đọc, SignalR realtime, Telegram, badge quản lý. Sandbox V01–V07 PASS, toàn bộ test PASS |
| [Việt hóa giao diện](viet-hoa-giao-dien/README.md) | — | Đã khảo sát và sửa chữ tiếng Anh còn sót (nút hộp thoại, Select2, DataTables, trang lỗi, nhãn trạng thái màn nhân viên) trên nhánh `feature/viet-hoa-giao-dien` (đã có trong `master`); thông báo lỗi do framework sinh giữ nguyên, không sửa `Program.cs` (quyết định 22/10/2026). Sau khi gộp `thuan`, layout trang khách dùng logo và tên TroSync |

Quyền sở hữu file và lịch 60 ngày nằm trong [roadmap](../roadmap-60-ngay.md). Không đánh dấu hoàn thành chỉ vì đã có entity, migration hoặc thư mục tài liệu.


## Vai trò người dùng

| Vai trò | Phạm vi hiện tại |
| --- | --- |
| Admin | Truy cập cổng quản lý, tài khoản, cấu hình nghiệp vụ và các thao tác quản trị |
| NhanVien | Truy cập cổng quản lý theo các controller dùng AdminBaseController, giới hạn theo chi nhánh được phân công (`NhanVienChiNhanh`). Chốt/từ chối hóa đơn và cấu hình thanh toán một phần chỉ Admin |
| KhachThue | Xem dữ liệu gắn với claim NguoiThueId: dashboard, hồ sơ, hợp đồng, hóa đơn, thanh toán, sự cố và thông báo |
| KhachVangLai | Xem phòng công khai, hẹn xem, giữ chỗ, đặt cọc và theo dõi yêu cầu của chính mình; vào được Dashboard của cổng khách thuê nhưng không vào các trang khác của cổng này |

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
- Quản lý tài khoản với bốn role Admin, NhanVien, KhachThue, KhachVangLai.
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
- Chốt chỉ số điện nước theo phòng, tháng và năm; có ảnh đồng hồ, OCR và bước xác nhận (sửa số phải có lý do).
- Chặn các tình huống nghiệp vụ không hợp lệ trong Application service/store.

### Hóa đơn

- Xem trước và phát sinh hóa đơn theo kỳ cho các phòng được chọn.
- Tính tiền phòng, điện/nước và dịch vụ đăng ký.
- Chỉnh sửa, xem chi tiết và xóa mềm hóa đơn theo quy tắc hiện tại.
- Ghi nhận thu tiền mặt hoặc chuyển khoản và người xác nhận.
- Xuất Excel/PDF, gửi email kèm PDF và sinh VietQR.
- Trạng thái phát hành: `Nhap → ChoDuyet → DaChot → DaGui` (hủy: `DaHuy`). Trạng thái thanh toán: `ChuaThanhToan`, `DaThanhToan`, `ThanhToanMotPhan`. Công nợ chỉ tính hóa đơn `DaGui`.
- Chỉ số điện nước đáng tin cậy thì tự tạo hóa đơn nháp và báo nhân viên; nhân viên gửi duyệt, Admin chốt, nhân viên/Admin công bố. Chi tiết: [hóa đơn tự động](hoa-don-tu-dong/plan.md), [duyệt hóa đơn tự động](duyet-hoa-don-tu-dong/README.md).
- Thanh toán trực tuyến: VietQR theo số còn lại, webhook SePay tự ghi sổ (mặc định tắt), thông báo trong app/Telegram/email biên lai, màn Giao dịch SePay cho Admin. Chi tiết: [thanh toán trực tuyến](thanh-toan-truc-tuyen/README.md).

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
- Đếm chưa đọc, đánh dấu một/tất cả đã đọc và xóa thông báo; chỉ đánh dấu được thông báo của chính mình.
- Sự cố mới chỉ gửi Admin và nhân viên được phân công chi nhánh của phòng.
- SignalR hub tại /thongBaoHub đẩy cập nhật tới giao diện đang kết nối.

### Trợ lý AI

- Widget có ở cả cổng quản lý và cổng khách thuê, mỗi cổng gọi endpoint riêng; lịch sử chat lưu theo tài khoản.
- Admin và nhân viên dùng 9 công cụ quản lý; nhân viên chỉ thấy dữ liệu chi nhánh được phân công. Khách thuê dùng 3 công cụ chỉ đọc dữ liệu của mình.
- Application điều phối tool calling (tối đa 3 vòng, 5 lời gọi mỗi vòng) và tự tính số liệu; Infrastructure chỉ lo gọi Gemini và truy vấn.
- AI đọc dữ liệu qua service hạ tầng; không được xem là nguồn quyết định thanh toán hoặc thay thế validation nghiệp vụ.

## Cổng khách thuê

Khách thuê đăng nhập bằng cookie và phải có role KhachThue cùng claim NguoiThueId hợp lệ.

- Dashboard: tổng quan hồ sơ, tổng tiền hóa đơn chưa thanh toán, lịch xem và giữ chỗ của chính người dùng (mở cho cả KhachThue và KhachVangLai; công nợ chỉ hiện với KhachThue có hồ sơ người thuê).
- Hồ sơ: xem thông tin cá nhân và đổi mật khẩu.
- Hợp đồng: xem danh sách và chi tiết hợp đồng của mình.
- Hóa đơn: xem danh sách/chi tiết, quyền sở hữu hóa đơn được kiểm tra ở server.
- Lịch sử thanh toán: xem các giao dịch liên quan.
- Sự cố: tạo và theo dõi yêu cầu của mình.
- Thông báo: xem và quản lý trạng thái đã đọc.

## Cổng khách vãng lai

Phần công khai nằm ở `Web/Controllers` (thư mục `Areas/KhachVangLai` chỉ có `.gitkeep`). Spec và kế hoạch: [cong-khach-vang-lai](cong-khach-vang-lai/SPEC.md).

- Xem phòng không cần đăng nhập: `/phong` (lọc, sắp xếp), `/phong/{id}`, `/phong/cam-nang`. Phòng đã lưu và so sánh (`/phong/da-luu`, `/phong/so-sanh`) giữ ở `localStorage` của trình duyệt. Phòng hiện khi `DuocDangTin = true`, trạng thái Trống và không có giữ chỗ hiệu lực.
- Đăng ký khách tại `/dang-ky-khach`, đăng nhập chung trang `/QuanLyNhaTro/DangNhap`.
- Hẹn xem: khách đề xuất ngày giờ, nhân viên xác nhận hoặc từ chối ở màn Lịch xem phòng và hệ thống gửi email trạng thái (email lỗi không hoàn tác trạng thái; có nút Gửi lại email). Phòng đã có người thuê thì nhân viên hủy lịch bằng "Hủy lịch — phòng đã thuê".
- Giữ chỗ và đặt cọc: nhân viên duyệt số tiền và hạn, khách chuyển khoản và nộp ảnh minh chứng, nhân viên đối soát; tiền giữ chỗ có thể áp vào cọc hợp đồng hoặc hoàn lại (`ReservationSettlement`). Theo dõi tại `/tai-khoan/yeu-cau-phong`.
- Nhân viên đăng tin ở màn Tin phòng công khai: tiêu đề, mô tả, hiển thị và tải ảnh (JPG/PNG/WebP, tối đa 5 MB/ảnh, 10 ảnh/phòng) qua Cloudinary.

## Tích hợp và tác vụ tự động

| Thành phần | Chức năng | Điều kiện cấu hình |
| --- | --- | --- |
| PostgreSQL/EF Core | Dữ liệu nghiệp vụ và migrations | ConnectionStrings:DefaultConnection |
| MailKit SMTP | Hóa đơn và cảnh báo hợp đồng | EmailSettings |
| Cloudinary | Ảnh sự cố, ảnh đồng hồ, minh chứng hóa đơn và giữ chỗ, ảnh tin phòng | Cloudinary |
| SePay | Webhook tiền vào, đồng bộ bù giao dịch (mặc định tắt) | SePay (`SePay:Enabled`, `SePay:SyncRecordEnabled`) |
| Telegram | Thông báo thanh toán (mặc định tắt) | Telegram (`Telegram:Enabled`) |
| Gemini | Trợ lý AI | Gemini:ApiKey, Gemini:Model |
| QRCoder/VietQR | QR chuyển khoản | VietQRSettings |
| ClosedXML/QuestPDF/DocX | Excel, PDF và Word | Không cần dịch vụ ngoài |
| SignalR | Thông báo realtime | Kết nối hub /thongBaoHub |

Các job nền chạy trong cùng process Web. BackgroundJobs:Enabled=false tắt việc đăng ký hosted jobs, nhưng không tắt migrations/seed lúc khởi động.

## Chưa triển khai

Các mục sau chưa có hoặc chưa hoàn chỉnh trong mã nguồn:

- API JSON /api/v1 (`Web/Api/V1` chỉ có `.gitkeep`).
- Gộp tài khoản khi khách vãng lai ký hợp đồng: `HopDongService` đang tạo thêm tài khoản khách thuê thay vì nâng cấp tài khoản khách; tên đăng nhập có thể trùng. Cần spec.
- Quản lý ảnh tin phòng: chưa có xóa, sắp xếp hay đổi ảnh đại diện; phòng đủ 10 ảnh không thêm được. Test tự động dùng storage giả; người dùng đã thử tải ảnh với Cloudinary thật ngày 24/10/2026 và báo thành công.
- Đặt lịch xem theo khung giờ: mã `ViewingSlotAdmin*` còn nhưng không có giao diện và `ViewingRequestService` từ chối `SlotId`.
- Thanh toán trực tuyến đợt 2 (VNPay) đã thử sandbox V01–V07 và kiểm giao diện 375 px (26/10/2026); còn để sau: tab đang chọn khó phân biệt, DataTables trang hóa đơn khách còn tiếng Anh; đồng bộ bù SePay chưa xác nhận nên `SePay:SyncRecordEnabled` giữ tắt.
- Mobile app, IoT và CI workflow (`.github/workflows` chỉ có `.gitkeep`).

Migration 1 và Migration 2 thêm lần lượt 5 và 15 bảng; các migration sau đó (mới nhất `AddAutoInvoiceReviewWindow`) không làm đổi số bảng. EF model và kiểm thử kỳ vọng 37 bảng nghiệp vụ; đã đối chiếu lại ngày 22/10/2026 bằng `has-pending-model-changes` (không có thay đổi mô hình chưa có migration), chưa xác minh lại database đang chạy. Xem [roadmap 60 ngày](../roadmap-60-ngay.md), [spec hai migration](../superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md) và hai kế hoạch triển khai trong [superpowers/plans](../superpowers/plans/).
