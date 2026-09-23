# Kế hoạch phát triển 60 ngày cho hai người

## Mục tiêu và nguyên tắc

Phát triển hệ thống cho một đơn vị quản lý nhiều chi nhánh. Hai người sở hữu các nhóm tính năng khác nhau và mỗi người triển khai trọn luồng nghiệp vụ, dữ liệu, API/MVC, giao diện và kiểm thử của nhóm mình. Người A đã chuẩn bị hai migration nền để khóa entity, quan hệ và ràng buộc database. Sau khi bàn giao schema, chỉ A tạo migration mới; B đề xuất thay đổi schema để cả hai review trước khi A thực hiện.

AI hỗ trợ đọc ảnh chỉ số, tìm phòng và đặt lịch xem trong khung giờ được cấu hình. Người có quyền vẫn quyết định chỉ số chính thức, hóa đơn, tiền thực nhận, giữ chỗ và hoàn tiền.

Giữ một ứng dụng ASP.NET Core MVC tại `src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj`. MVC và API v1 cùng gọi Application Service; không tách backend trong giai đoạn khóa luận.

## Hiện trạng đối chiếu ngày 23/09/2026

| Hạng mục | Hiện trạng |
| --- | --- |
| Kiến trúc | Đã có bốn production project và bốn test project |
| Database | Đã có Migration 1 (thêm 5 bảng) và Migration 2 (thêm 15 bảng), EF model và kiểm thử kỳ vọng 37 bảng nghiệp vụ; lần đối chiếu này chưa xác minh database đang chạy |
| Hóa đơn | Có tính hóa đơn, model trạng thái phát hành và lịch sử; chưa có luồng OCR duyệt ảnh → hóa đơn nháp → chốt hoàn chỉnh ở Application/Web |
| Thanh toán | Có VietQR, lịch sử tiền đã thu và Application Service xử lý yêu cầu/minh chứng thanh toán hóa đơn; chưa nối hoàn chỉnh vào Web |
| Trang công khai | Area `KhachVangLai` và API v1 mới có khung |
| AI | Có dịch vụ AI; chưa có luồng OCR duyệt ảnh hoặc đặt lịch xem tự động |
| CI | `.github/workflows/` mới có khung |

Thiết kế schema đã được chốt tại [database foundation design](superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md). Migration 1 thêm 5 bảng, Migration 2 thêm 15 bảng; tổng sau hai migration là 37 bảng nghiệp vụ.

## Phân công theo module tính năng

| Module | Người sở hữu trọn luồng | Phạm vi công việc |
| --- | --- | --- |
| 1. Ảnh chỉ số, OCR và hóa đơn | **A** | Tải/lưu ảnh chỉ số, AI gợi ý, nhân viên duyệt; tính tiền `decimal`, hóa đơn nháp → chốt → gửi, lịch sử; Application Service, store, API/MVC, Razor và kiểm thử |
| 2. Thanh toán hóa đơn | **A** | VietQR, yêu cầu thanh toán, khách gửi minh chứng, nhân viên đối chiếu, trả đủ/một phần, chống xác nhận trùng; hoàn thiện service hiện có, API/MVC, Razor và kiểm thử |
| 3. Phòng công khai và lịch xem | **B** | Gallery, tìm/lọc và chi tiết phòng, hội thoại AI, khung giờ, yêu cầu xem phòng, nhân viên xử lý lịch; Application Service, store, API/MVC, Razor và kiểm thử |
| 4. Khách vãng lai, giữ chỗ, cọc và hoàn tiền | **B** | Đăng ký tài khoản, duyệt giữ chỗ, xác nhận tiền giữ chỗ, chuyển sang hợp đồng, áp dụng cọc, quyết định và ghi nhận hoàn tiền; Application Service, store, API/MVC, Razor và kiểm thử |

### Hồ sơ theo dõi tại `docs/features/`

Mỗi module có một [README trong `docs/features/`](features/README.md) ghi người phụ trách, phạm vi và các việc cần hoàn thành. Bên trong từng module có `spec/`, `plan/`, `review/` chỉ chứa `.gitkeep`; **chưa có tài liệu spec, plan hoặc review**. Khi bắt đầu thực hiện mới viết các tài liệu này. Thư mục tài liệu không phải thư mục mã nguồn và không chứng minh tính năng đã hoàn thành.

| Module | Người phụ trách | README theo dõi |
| --- | --- | --- |
| 1. Ảnh chỉ số, OCR và hóa đơn | **A** | [anh-chi-so-hoa-don](features/anh-chi-so-hoa-don/README.md) |
| 2. Thanh toán hóa đơn | **A** | [thanh-toan-hoa-don](features/thanh-toan-hoa-don/README.md) |
| 3. Phòng công khai và lịch xem | **B** | [phong-cong-khai-lich-xem](features/phong-cong-khai-lich-xem/README.md) |
| 4. Khách vãng lai, giữ chỗ, cọc và hoàn tiền | **B** | [khach-vang-lai-giu-cho](features/khach-vang-lai-giu-cho/README.md) |

Agent của B đọc README module 3–4 trước khi lập spec/plan và sửa code; A đọc README module 1–2. Phân công mã nguồn vẫn tuân theo bảng ranh giới bên dưới.

Mỗi module có **một người chịu trách nhiệm chính từ nghiệp vụ đến giao diện**. Người còn lại review và kiểm thử liên luồng, không đồng phát triển service/controller/view của module đó. Model hoặc service đã có một phần không đồng nghĩa module đã hoàn thành.

### Ranh giới file và phần dùng chung

| Phạm vi | Người sửa chính | Quy tắc |
| --- | --- | --- |
| `Features/HoaDons`, `Features/DienNuocs` liên quan OCR và thanh toán hóa đơn; Web controller/view tương ứng | A | B không sửa các file tính năng này |
| Application/Infrastructure/API/Web của module phòng công khai, lịch xem, khách vãng lai, giữ chỗ và hoàn tiền; Web area `KhachVangLai` | B | A không sửa các file tính năng này; Web chỉ dùng Application DTO/Service, không tham chiếu trực tiếp Domain |
| Domain entities/enums, EF configurations, `ApplicationDbContext`, migrations và model snapshot | A | Schema nền đã khóa; B mô tả trường và use case cần thêm, A review và tạo migration riêng nếu thật sự cần |
| `Program.cs`, `Application/DependencyInjection.cs`, `Infrastructure/DependencyInjection.cs`, route/auth dùng chung | A | B gửi danh sách đăng ký service/route cần tích hợp; A sửa các file dùng chung trong PR tích hợp |
| `NguoiDung`, `PhongTro`, `HopDong` và contract giao giữa module | Thỏa thuận trước khi sửa | Thay đổi entity hoặc chữ ký dùng chung phải được cả hai review; ưu tiên thêm DTO/use case trong module sở hữu |
| CI, kiểm thử liên luồng, dữ liệu demo và tài liệu bàn giao | Cả hai | A phụ trách cấu hình CI/schema; mỗi người viết test cho module mình và review chéo trước khi hợp nhất |

Thanh toán hóa đơn và thanh toán giữ chỗ dùng bảng, store và service riêng; không gộp vào một service thanh toán chung.

## Giai đoạn nền tảng trước khi làm song song

Người A thực hiện tuần tự:

1. Migration 1: tiền dạng `decimal`, chỉ số/OCR, trạng thái hóa đơn và thanh toán hóa đơn.
2. Kiểm thử, áp/rollback trên PostgreSQL test và xác nhận 22 bảng nghiệp vụ.
3. Migration 2: ảnh phòng, tài khoản khách vãng lai, lịch xem, giữ chỗ, tiền cọc và hoàn tiền.
4. Kiểm thử, áp/rollback trên nền Migration 1 và xác nhận 37 bảng nghiệp vụ.
5. Công bố entity, enum và ràng buộc schema dùng chung; mỗi người chốt DTO và service contract của module mình trước khi nối giao diện.

Trong thời gian A hoàn tất schema, B có thể rà luồng phòng công khai, lịch xem và giữ chỗ; chuẩn bị wireframe, layout và Web ViewModel độc lập. Sau khi schema được bàn giao, B triển khai trọn các module 3–4 mà không sửa migration.

Kế hoạch thi công chi tiết:

- [Migration 1](superpowers/plans/2026-09-21-migration-1-billing-foundation.md)
- [Migration 2](superpowers/plans/2026-09-21-migration-2-public-reservation-foundation.md)

## Lộ trình 60 ngày

| Ngày | Kết quả cần có | Người A: chỉ số, hóa đơn, thanh toán hóa đơn | Người B: phòng công khai, lịch xem, giữ chỗ |
| --- | --- | --- | --- |
| **1–10** | Hai migration nền được review và thử trên PostgreSQL test; CI build/test cơ bản | Hoàn tất và bàn giao entity, enum, configuration, Migration 1 và 2; test kiểu dữ liệu, index và cạnh tranh; cấu hình CI | Rà luồng module 3–4, wireframe và Web ViewModel độc lập; chưa sửa schema |
| **11–22** | Hai luồng module bắt đầu song song trên schema đã khóa | Ảnh chỉ số → AI gợi ý → nhân viên duyệt; tính tiền `decimal`, hóa đơn nháp → chốt, API/UI/test | Phòng công khai: gallery, bộ lọc, chi tiết phòng; query, API/UI/test |
| **23–31** | Hoàn thiện luồng hóa đơn và lịch xem | Hoàn thiện hóa đơn, lịch sử, quyền; VietQR, yêu cầu thanh toán, minh chứng và hàng đợi đối chiếu, API/UI/test | Khung giờ, yêu cầu xem phòng, AI/nhân viên xác nhận, chống trùng lịch; API/UI/test |
| **32–43** | Hai nhóm tính năng đạt luồng chính end-to-end | Thanh toán đủ/một phần, transaction xác nhận, chống trùng; kiểm thử module 1–2 và sửa lỗi | Đăng ký khách vãng lai, duyệt giữ chỗ, tiền/hạn thanh toán, khóa phòng; API/UI/test |
| **44–53** | Hoàn thiện nghiệp vụ giữ chỗ và các trường hợp tài chính | Ổn định module 1–2, kiểm thử bảo mật/quyền, hỗ trợ review contract liên luồng; không sửa module của B | Minh chứng/xác nhận tiền giữ chỗ, chuyển hợp đồng và áp dụng cọc, quyết định và giao dịch hoàn tiền; test cạnh tranh |
| **54–60** | Kiểm thử liên luồng, sửa lỗi, tài liệu sử dụng và dữ liệu demo | Migration rehearsal, kiểm thử API/bảo mật/quyền, tài liệu kỹ thuật và review module của B | UX di động, kiểm thử Web, hướng dẫn người dùng, ảnh/kịch bản demo và review module của A |

Các mốc là ngày tương đối tính từ khi bắt đầu kế hoạch, không phải trạng thái đã hoàn thành. Từ ngày 11, hai người làm song song trên các module khác nhau; việc tích hợp cuối kỳ không chuyển quyền sở hữu module.

## Quy tắc nghiệp vụ đã chốt

### Chỉ số và hóa đơn

- Mỗi ảnh chỉ thuộc một loại đồng hồ điện hoặc nước; ảnh mờ/lỗi được giữ lại và lần chụp lại tạo ảnh mới.
- Mỗi ảnh lưu một kết quả AI và phần xác nhận của nhân viên ngay trên `AnhChiSoDongHo`; không có bảng kết quả OCR hoặc lịch sử điều chỉnh riêng.
- Tiền dùng `decimal/numeric(18,2)`; chỉ số và số lượng dùng `decimal/numeric(18,3)`.
- Hóa đơn có trạng thái phát hành riêng với trạng thái thanh toán.
- Thanh toán một phần được quản lý bật theo từng hóa đơn và có mức tối thiểu.

### Phòng công khai và lịch xem

- Phòng có gallery ảnh riêng; Cloudinary lưu tệp, database lưu metadata.
- Khách xem phòng công khai không cần đăng nhập.
- Yêu cầu xem phòng bắt buộc họ tên và số điện thoại; email tùy chọn, không có token email.
- AI chỉ tự chốt khung giờ được cấu hình và còn chỗ. Ngoài khung hoặc có xung đột thì chuyển nhân viên.
- AI gọi Application Service và không ghi trực tiếp database.

### Tài khoản và giữ chỗ

- Người đăng ký nhưng chưa thuê nằm trong bảng `KhachVangLai`, liên kết một-một với `NguoiDung`.
- Chỉ tạo `NguoiThue` khi lập hợp đồng; khi đó liên kết tài khoản và chuyển role thành `KhachThue`.
- Giữ chỗ bắt buộc đăng nhập; có thể tạo trực tiếp hoặc từ lịch xem.
- Nhân viên xác định số tiền và hạn thanh toán khi duyệt.
- Chỉ một yêu cầu giữ chỗ hoạt động cho một phòng tại một thời điểm.
- Thanh toán hóa đơn và thanh toán giữ chỗ dùng các bảng riêng.

### Cọc và hoàn tiền

- Tiền giữ chỗ được áp dụng vào tiền cọc hợp đồng đúng một lần và không ghi thành thanh toán hóa đơn.
- Quản lý quyết định số tiền hoàn và lý do.
- Một quyết định có thể được hoàn qua nhiều giao dịch thực tế.
- Phòng được giải phóng theo quyết định hủy dù việc hoàn tiền chưa hoàn tất.

### Phân quyền chi nhánh

- Một nhân viên có thể thuộc nhiều chi nhánh.
- `NhanVienChiNhanh.IsActive` quyết định quyền hiện tại.
- Ngày và người phân công/thu hồi chỉ phục vụ truy vết.
- Mọi quyền phải được kiểm tra ở Application/API, không dựa vào giao diện.

## Nhịp làm việc hai người

1. A bàn giao schema nền; mỗi người mở PR contract (DTO, interface, request/response và tiêu chí nghiệm thu) cho module mình trước khi nối giao diện.
2. A làm module 1–2, B làm module 3–4 trên nhánh riêng. Mỗi người tự triển khai Application, Infrastructure store, API/MVC, Razor và test trong phạm vi module.
3. Không cùng sửa một service/controller/view. B gửi các đăng ký service/route cần thêm để A cập nhật `Program.cs` và các file `DependencyInjection.cs` dùng chung trong PR tích hợp.
4. Khi một module cần dữ liệu từ module kia, người cần dùng yêu cầu DTO/use case công khai; không sửa trực tiếp service hoặc store của người còn lại.
5. B không tạo migration. Yêu cầu thay đổi schema được hai người review, A tạo migration mới và kiểm thử trên PostgreSQL test trước khi hai nhánh phụ thuộc vào thay đổi đó.
6. Mỗi PR được người còn lại review. Trước khi hợp nhất chạy build, test module liên quan, kiểm thử phân quyền và luồng giao giữa hóa đơn, hợp đồng, tài khoản và giữ chỗ.
7. Mỗi ngày đồng bộ ngắn về contract và file dùng chung; nếu có xung đột, người sở hữu file tích hợp thay đổi sau khi hai người thống nhất.

## Kiểm chứng

- Domain.UnitTests: quy tắc trạng thái, số tiền, cọc và hoàn tiền.
- Application.UnitTests: use case với store/UoW giả.
- Infrastructure.IntegrationTests: PostgreSQL mapping, filtered unique index, transaction, migration và cạnh tranh.
- Web.IntegrationTests: HTTP, cookie/authentication, antiforgery, rate limit, routing và SignalR.
- Full suite cần `QLCTPT_TEST_CONNECTION_STRING` trỏ vào database riêng có hậu tố `_test` hoặc `_integration_test`.
- Sau mỗi migration chạy build, test phù hợp và `dotnet ef migrations has-pending-model-changes`.
- Không tuyên bố integration test đã pass khi thiếu PostgreSQL test.
