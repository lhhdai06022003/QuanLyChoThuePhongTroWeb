# Kế hoạch phát triển 60 ngày cho hai người

## Mục tiêu và nguyên tắc

Phát triển hệ thống cho một đơn vị quản lý nhiều chi nhánh. Trước khi hai người triển khai song song, Người A hoàn tất hai migration nền để khóa entity, quan hệ và ràng buộc database. Sau thời điểm bàn giao schema, Người B chỉ sử dụng DTO/Application Service đã thống nhất và không sửa Domain model hoặc EF migration.

AI hỗ trợ đọc ảnh chỉ số, tìm phòng và đặt lịch xem trong khung giờ được cấu hình. Người có quyền vẫn quyết định chỉ số chính thức, hóa đơn, tiền thực nhận, giữ chỗ và hoàn tiền.

Giữ một ứng dụng ASP.NET Core MVC tại `src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj`. MVC và API v1 cùng gọi Application Service; không tách backend trong giai đoạn khóa luận.

## Hiện trạng đối chiếu ngày 21/09/2026

| Hạng mục | Hiện trạng |
| --- | --- |
| Kiến trúc | Đã có bốn production project và bốn test project |
| Database | 17 bảng nghiệp vụ; chưa có schema OCR, lịch xem, giữ chỗ hoặc phân công chi nhánh |
| Hóa đơn | Có tính hóa đơn và hai trạng thái thanh toán; chưa có nháp, duyệt/chốt và lịch sử trạng thái |
| Thanh toán | Có VietQR và lịch sử tiền đã thu; chưa có yêu cầu thanh toán, ảnh minh chứng hoặc trả một phần được duyệt |
| Trang công khai | Area `KhachVangLai` và API v1 mới có khung |
| AI | Có dịch vụ AI; chưa có luồng OCR duyệt ảnh hoặc đặt lịch xem tự động |
| CI | `.github/workflows/` mới có khung |

Thiết kế schema đã được chốt tại [database foundation design](superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md). Migration 1 thêm 7 bảng, Migration 2 thêm 15 bảng; tổng sau hai migration là 39 bảng nghiệp vụ.

## Quyền sở hữu để tránh xung đột Git

| Phạm vi | Người sở hữu chính | Quy tắc |
| --- | --- | --- |
| Domain entities và enums | A | B không sửa trực tiếp |
| EF configurations, DbContext, migrations | A | Chỉ A tạo migration |
| Application DTO, interface và service | A | Chốt hợp đồng trước khi B nối giao diện |
| API v1 và kiểm tra quyền máy chủ | A | B gửi yêu cầu thay đổi qua DTO contract |
| Razor, Web ViewModel, CSS/JS và trải nghiệm di động | B | Không tham chiếu trực tiếp Domain |
| Email/UI nội dung | B, qua abstraction do A cung cấp | Không đổi persistence model |
| `Program.cs` và DependencyInjection | A | Tránh hai người cùng sửa composition root |
| Kiểm thử liên luồng và demo | Cả hai | Review chéo trước khi hợp nhất |

Nếu giao diện phát hiện thiếu dữ liệu, B mô tả trường cần thêm và use case; A quyết định DTO-only hay schema change. Không tạo migration từ nhánh của B.

## Giai đoạn nền tảng trước khi làm song song

Người A thực hiện tuần tự:

1. Migration 1: tiền dạng `decimal`, chỉ số/OCR, trạng thái hóa đơn và thanh toán hóa đơn.
2. Kiểm thử, áp/rollback trên PostgreSQL test và xác nhận 24 bảng nghiệp vụ.
3. Migration 2: ảnh phòng, tài khoản khách vãng lai, lịch xem, giữ chỗ, tiền cọc và hoàn tiền.
4. Kiểm thử, áp/rollback trên nền Migration 1 và xác nhận 39 bảng nghiệp vụ.
5. Công bố entity, enum, DTO và service contract cho cả hai người.

Trong thời gian đó, Người B có thể rà luồng UI, tạo wireframe, layout và Web ViewModel độc lập; chưa nối dữ liệu hoặc sửa model.

Kế hoạch thi công chi tiết:

- [Migration 1](superpowers/plans/2026-09-21-migration-1-billing-foundation.md)
- [Migration 2](superpowers/plans/2026-09-21-migration-2-public-reservation-foundation.md)

## Lộ trình 60 ngày

| Ngày | Kết quả cần có | Người A | Người B |
| --- | --- | --- | --- |
| **1–10** | Hai migration nền được review và thử trên PostgreSQL test; CI build/test cơ bản | Tạo entity, enum, configuration, Migration 1 rồi Migration 2; test kiểu dữ liệu, index và cạnh tranh | Rà luồng, wireframe, Web ViewModel, nội dung màn hình; không sửa schema |
| **11–22** | Ảnh chỉ số → AI gợi ý → người duyệt → hóa đơn nháp → chốt | Use case OCR, phép tính decimal, lịch sử, quyền và API/test | Màn hình tải ảnh, đối chiếu, sửa chỉ số, duyệt và chốt hóa đơn |
| **23–31** | VietQR → khách gửi ảnh → nhân viên đối chiếu → trả đủ/một phần | Yêu cầu thanh toán, transaction xác nhận, chống trùng và test | Trang khách thuê, tải minh chứng và hàng đợi đối chiếu |
| **32–43** | Phòng công khai, bộ lọc, gallery và lịch xem kết hợp AI/nhân viên | Query/API công khai, khung giờ, transaction giữ chỗ lịch và quyền chi nhánh | Trang tìm phòng, chi tiết, hội thoại AI và màn hình nhân viên |
| **44–53** | Đăng ký khách vãng lai → giữ chỗ → xác nhận tiền → hợp đồng/cọc → hoàn tiền | State machine, thanh toán giữ chỗ, áp dụng cọc, refund ledger và test cạnh tranh | Giao diện khách vãng lai, nhân viên và quản lý |
| **54–60** | Kiểm thử liên luồng, sửa lỗi, tài liệu sử dụng và dữ liệu demo | Migration rehearsal, kiểm thử bảo mật/quyền, API và tài liệu kỹ thuật | UX di động, hướng dẫn người dùng, ảnh/kịch bản demo |

Các mốc là ngày tương đối tính từ khi bắt đầu kế hoạch, không phải trạng thái đã hoàn thành.

## Quy tắc nghiệp vụ đã chốt

### Chỉ số và hóa đơn

- Mỗi ảnh chỉ thuộc một loại đồng hồ điện hoặc nước; cho phép gửi lại nhiều ảnh.
- Lưu từng kết quả AI; người có quyền chọn, sửa hoặc nhập tay rồi duyệt.
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

1. A mở PR contract trước: DTO, interface, enum và tiêu chí nghiệm thu.
2. B bắt đầu UI sau khi contract được chốt; dùng fake data hoặc mock service trong Web nếu backend chưa hoàn tất.
3. A hợp nhất service/API và cung cấp ví dụ request/response.
4. B nối UI, không đổi chữ ký service trực tiếp.
5. Hai người review chéo và chạy kiểm thử liên quan.
6. Mỗi ngày đồng bộ ngắn về contract, không cùng sửa một file.
7. Mọi thay đổi schema quay lại backlog của A và được tạo thành migration riêng sau khi review.

## Kiểm chứng

- Domain.UnitTests: quy tắc trạng thái, số tiền, cọc và hoàn tiền.
- Application.UnitTests: use case với store/UoW giả.
- Infrastructure.IntegrationTests: PostgreSQL mapping, filtered unique index, transaction, migration và cạnh tranh.
- Web.IntegrationTests: HTTP, cookie/authentication, antiforgery, rate limit, routing và SignalR.
- Full suite cần `QLCTPT_TEST_CONNECTION_STRING` trỏ vào database riêng có hậu tố `_test` hoặc `_integration_test`.
- Sau mỗi migration chạy build, test phù hợp và `dotnet ef migrations has-pending-model-changes`.
- Không tuyên bố integration test đã pass khi thiếu PostgreSQL test.
