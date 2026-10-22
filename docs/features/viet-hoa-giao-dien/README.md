# Module: Việt hóa giao diện

- **Nhánh Git:** `feature/viet-hoa-giao-dien` (tách từ `master` ở `e77514b`).
- **Hiện trạng:** Đã khảo sát toàn bộ chuỗi hiển thị (view Razor, JS, controller, service, email) và sửa các chỗ còn tiếng Anh. Mọi quyết định đã chốt (mục "Quyết định đã chốt"). Chưa commit.
- **Phạm vi:** chỉ chữ người dùng nhìn thấy. Không đổi tên định danh, route, schema hay migration.

## Kết quả khảo sát

Phần lớn giao diện đã là tiếng Việt. Chỗ còn tiếng Anh nằm ở chữ mặc định của thư viện và vài trang mẫu:

| Nhóm | Trước | Sau |
| --- | --- | --- |
| Hộp thoại SweetAlert (hơn 80 chỗ, dạng chuỗi hoặc object không đặt chữ nút) | nút "OK" / "Cancel" | "Đóng" / "Hủy bỏ"; đặt một lần ở lớp bọc `Swal.fire` trong `_Layout.cshtml` và `_LayoutKhachThue.cshtml`; hộp thoại nào tự đặt chữ nút thì giữ nguyên |
| Ô chọn Select2 | "No results found", "Searching…" | "Không tìm thấy kết quả", "Đang tìm kiếm…"... (`$.fn.select2.defaults.set('language', …)` trong `_Layout.cshtml`) |
| Bảng DataTables khi tải lỗi | `alert("DataTables warning: …")` | hộp thoại "Không tải được dữ liệu bảng. Vui lòng tải lại trang."; chi tiết ghi ra console |
| Trang lỗi `Views/Shared/Error.cshtml` (hiện khi có lỗi chưa xử lý) | toàn tiếng Anh | tiếng Việt, tiêu đề "Lỗi", "Mã yêu cầu" |
| `Home/Index`, `Home/Privacy` (trang mẫu) | tiếng Anh | tiếng Việt |
| `aria-label` của nút đóng modal, bật/tắt thanh điều hướng, mở menu người dùng | Close / Toggle navigation / Open user menu | Đóng / Bật/tắt thanh điều hướng / Mở menu người dùng |
| Thanh bên cổng khách thuê | "TENANT PORTAL" | "Cổng khách thuê" |
| Menu cổng quản lý | "Dashboard" | "Bảng điều khiển" |
| Cột và nhãn "Quyền Hạn (Role)" | có chú thích tiếng Anh | "Quyền Hạn" |
| Màn nhân viên: yêu cầu giữ chỗ, chi tiết giữ chỗ, lịch xem phòng | in tên enum thô (`MoiTao`, `ChoNhanVienXuLy`, `DaXacNhan`...) | nhãn tiếng Việt qua `Helpers/GuestStatusText.cs`, lấy theo cách các trang khách (`YeuCauCuaToi`, `DatCoc`) đang hiển thị |
| Thông báo lỗi lẫn từ Anh | "database", "request", "Role", "file", "form", "tick", "Gemini API Key", "Dashboard", "chat" | "cơ sở dữ liệu", "yêu cầu", "vai trò", "tệp", "biểu mẫu", "đánh dấu", "khóa API Gemini", "Bảng điều khiển", "trò chuyện" |
| Model đăng ký khách: độ dài tên đăng nhập, mật khẩu, họ tên | thông báo mặc định của DataAnnotations (tiếng Anh) | thông báo tiếng Việt trong `GuestRegistrationModel` |

## Quyết định của người dùng (21/10/2026)

- Nút mặc định của hộp thoại thông báo: **"Đóng"**.
- Menu "Dashboard": **"Bảng điều khiển"** (khớp tiêu đề trang "Bảng điều khiển hệ thống").
- Từ **"Admin"** giữ nguyên trên toàn web; chỉ bỏ chú thích "(Role)".
- Thông báo lỗi do framework tự sinh (`The JSON value could not be converted...`, `The value 'x' is not valid...`, tiêu đề 400 `One or more validation errors occurred.`): **không Việt hóa**, không sửa `Program.cs` (người dùng quyết định 22/10/2026).

## Giữ nguyên có chủ đích

- Từ mượn đã quen dùng: Email, CCCD, STT, QR, VietQR, SePay, PDF, AI, webhook, Admin.
- Tên thương hiệu: TroSync. Tên phím: Enter.
- Tên khóa cấu hình và file (`SePay:ApiToken`, `appsettings.json`, `SenderEmail/Password`) và mã quyền (`Invoice.Draft`, `Meter.Review`).
- Thông báo log và ngoại lệ dành cho lập trình viên (ví dụ `Design-time connection string is missing...`).
- Thông báo lỗi do framework tự sinh (model binding, JSON, lỗi 400 tự động của `[ApiController]`, `The X field is required.` cho thuộc tính không nullable): giữ tiếng Anh, không đụng `Program.cs`.
- Từ khóa so khớp dữ liệu không dấu trong `MeterServiceClassifier` ("dien sinh hoat"): không phải chữ hiển thị.

## Quyết định đã chốt (22/10/2026)

1. Thông báo lỗi do framework sinh: không Việt hóa, không sửa `Program.cs`.
2. Hai nhãn chưa có sẵn ở trang nào, dịch sát nghĩa đen, người dùng xác nhận giữ nguyên: `ChoChonLich` → "Chờ chọn lịch" (hẹn xem phòng), `TuChoi` của yêu cầu thanh toán giữ chỗ → "Đã từ chối".

Không còn mục nào chờ quyết định.

## Ngoài phạm vi, ghi nhận để xử lý sau

- Header (`Views/Shared/_Header.cshtml`) hiển thị "Quản trị viên" cho mọi tài khoản không phải khách thuê, kể cả nhân viên. Đây là lỗi logic nhãn vai trò, không phải Việt hóa.
- Chữ do trình duyệt tự sinh phụ thuộc ngôn ngữ trình duyệt, web không đổi được: thông báo kiểm tra ô bắt buộc của HTML5, nút "Choose file", bốn lời gọi `alert()` ở `ChiSoDienNuoc/Index.cshtml`, `_AnhChiSoModal.cshtml`, `DangNhap.cshtml`.
- Ba bản sao `switch` nhãn trạng thái trong trang khách (`YeuCauCuaToi`, `DatCoc`) chưa chuyển sang `GuestStatusText`; có thể gộp khi người phụ trách cổng khách vãng lai sửa các trang đó.

## Kiểm chứng

- Trang thử dựng từ chính các khối script trong `_Layout.cshtml` (trình duyệt trong app): nút "Đóng"/"Hủy bỏ" đúng cho dạng chuỗi, dạng object và `showSuccess`; chữ nút tự đặt giữ nguyên; Select2 hiện "Không tìm thấy kết quả"; bảng tải lỗi không còn `alert` tiếng Anh mà hiện hộp thoại tiếng Việt.
- Test mới: `GuestStatusTextTests` (mỗi giá trị enum trạng thái phải có nhãn), `VietnameseUiStaticGuardTests` (không còn `aria-label` tiếng Anh, lớp bọc SweetAlert/DataTables/Select2, trang lỗi, menu).
- Test cũ phải đổi theo chữ mới: `SessionDbErrorTests` (câu trên trang lỗi), `SePaySyncServiceTests` và `SePayTransactionClientTests` ("số request" → "số yêu cầu").
- Kết quả chạy ngày 21/10/2026: `dotnet build` 0 lỗi; `has-pending-model-changes` PASS; Domain 135/135, Application 1770/1770, Infrastructure 604/604, Web 556/556 (523 cũ + 33 mới), đều PASS.
- Chưa kiểm: giao diện thật trên server chạy với tài khoản đăng nhập (chưa mở các trang giữ chỗ, lịch xem phòng, trang lỗi bằng trình duyệt); chưa thử Select2 trên màn Hợp đồng và Tài khoản đăng nhập trong app thật.
