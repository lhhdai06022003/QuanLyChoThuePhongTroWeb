# Kế hoạch phát triển 60 ngày cho hai người

## Mục tiêu và nguyên tắc

Phát triển hệ thống cho **một đơn vị quản lý nhiều chi nhánh**. Trong 60 ngày, ưu tiên giảm thao tác cho quản lý, nhân viên và khách thuê bằng quy trình tự động có bước duyệt. AI hỗ trợ đọc ảnh chỉ số và gợi ý thông tin; người có quyền quyết định chỉ số, hóa đơn và tiền thực nhận. Trang khách vãng lai giúp tìm phòng, liên hệ, giữ chỗ và tiến tới hợp đồng. Mobile và IoT là hướng mở rộng sau giai đoạn này.

Giữ một ứng dụng ASP.NET Core MVC tại `src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj`, với nghiệp vụ và hạ tầng tách thành các thư viện Clean Architecture. API JSON phiên bản `/api/v1` sẽ ở cùng ứng dụng; MVC và API cùng gọi service nghiệp vụ. Không tách hệ thống thành nhiều backend trong giai đoạn khóa luận.

## Cấu trúc repository

Source hiện ở bốn project trong src/, với bốn project test tương ứng. API v1 và KhachVangLai bên trong Web mới có .gitkeep; chưa có endpoint/màn hình thực thi.

~~~text
src/
├── QuanLyChoThuePhongTroWeb.Domain/          # Entities, Enums
├── QuanLyChoThuePhongTroWeb.Application/     # Features, DTOs, Services, store interfaces
├── QuanLyChoThuePhongTroWeb.Infrastructure/  # Persistence/Migrations, ExternalServices, BackgroundJobs
└── QuanLyChoThuePhongTroWeb.Web/             # Areas, Api/V1, Controllers, Views, wwwroot
tests/
├── QuanLyChoThuePhongTroWeb.Domain.UnitTests/
├── QuanLyChoThuePhongTroWeb.Application.UnitTests/
├── QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/
└── QuanLyChoThuePhongTroWeb.Web.IntegrationTests/
docs/{architecture,features,deployment,superpowers}/
.github/workflows/                          # Hiện chỉ có .gitkeep
QuanLyChoThuePhongTroWeb.sln
~~~

## Đánh giá phù hợp ngày 20/09/2026

Roadmap vẫn phù hợp về hướng nghiệp vụ cho một đơn vị nhiều chi nhánh, nhưng không còn phù hợp nếu dùng cấu trúc MVC ở root hoặc coi test là chưa tồn tại. Mốc ngày là thứ tự trong kế hoạch 60 ngày; chưa có ngày bắt đầu mới và không suy ra tiến độ thực tế từ ngày trên tài liệu.

| Hạng mục | Bằng chứng hiện tại | Điều chỉnh |
| --- | --- | --- |
| Nền tảng | Bốn production project và bốn project test đã có | Rà hồi quy, không tạo lại test project |
| CI | .github/workflows chỉ có .gitkeep | Còn phải triển khai workflow với PostgreSQL test |
| Chỉ số/hóa đơn | Đã có dịch vụ điện nước, tính hóa đơn; enum chỉ có ChuaThanhToan/DaThanhToan | OCR, lịch sử duyệt và trạng thái nháp là phần mới |
| Thanh toán | Đã có VietQR và lịch sử thu tiền | Chưa coi gửi ảnh/xác nhận/duyệt trả một phần là hoàn thành |
| Khách vãng lai | Area và API v1 mới là khung; PhongTro chưa có DuocDangTin/AnhDaiDienUrl | Lát cắt tìm phòng/lịch xem chưa triển khai |
| Giữ chỗ | Chưa có thực thể giữ chỗ, lịch xem hoặc NhanVienChiNhanh | Cần bổ sung quyền theo chi nhánh, dữ liệu và kiểm thử cạnh tranh |
| Sự cố, AI, email | Đã có các dịch vụ tương ứng | Ưu tiên tích hợp và kiểm thử hồi quy |

Khối lượng còn lại gồm nhiều luồng mới và thay đổi dữ liệu; 60 ngày là mục tiêu dự kiến, cần chốt năng lực hai người và lịch bắt đầu trước khi cam kết ngày bàn giao.

## Phân công và cách phối hợp

| Người | Trách nhiệm chính | Bàn giao cho người còn lại |
| --- | --- | --- |
| A | Dữ liệu, migration, service, quyền, API, unit/integration test | Trạng thái nghiệp vụ, DTO và service đã kiểm thử |
| B | Razor/MVC, giao diện quản lý và khách, email, trải nghiệm trên điện thoại | Màn hình chạy được và phản hồi về dữ liệu còn thiếu |
| Cả hai | Thiết kế luồng, review chéo, kiểm thử và demo | Mỗi pull request có mô tả, cách kiểm tra, ảnh nếu đổi UI |

Trước mỗi tính năng, chốt trạng thái, quyền thao tác, trường dữ liệu và tiêu chí nghiệm thu. Một người quản lý migration của tính năng đó để tránh xung đột.

## Lộ trình và mốc nghiệm thu

| Ngày | Kết quả cần có | Người A | Người B |
| --- | --- | --- | --- |
| **1–7** | Kiểm kê chức năng hiện có; backlog ưu tiên; xác minh bốn project test và bổ sung CI build/test; sơ đồ quyền theo chi nhánh và trạng thái hóa đơn/thanh toán/giữ chỗ | Rà database, service, migration, phân quyền; bổ sung test còn thiếu và cấu hình PostgreSQL test | Rà toàn bộ luồng UI, ghi lỗi và màn hình thiếu |
| **8–20** | Khách hoặc nhân viên gửi ảnh chỉ số; AI gợi ý; người có quyền sửa/duyệt; hệ thống tạo hóa đơn nháp; người có quyền chốt trước khi gửi | Xử lý dữ liệu, phép tính, nhật ký, test; AI có đường nhập tay | Màn hình nộp ảnh, đối chiếu, duyệt, chốt hóa đơn |
| **21–30** | Yêu cầu thanh toán/VietQR; khách gửi ảnh chuyển khoản; nhân viên xác nhận tiền; trả một phần khi quản lý cho phép | Trạng thái thanh toán, số dư, chống ghi nhận trùng | Trang khách thuê và hàng đợi xác nhận |
| **31–42** | Phòng công khai có tìm/lọc, xem chi tiết, liên hệ/đặt lịch xem; nhân viên xử lý; khách theo dõi qua liên kết email | Service phòng và lịch xem, API/DTO, email link có hạn, test | Trang công khai và màn hình nhân viên |
| **43–52** | Nhân viên duyệt yêu cầu giữ chỗ trước; hệ thống tạo yêu cầu thanh toán có hạn; xác nhận tiền; tính cọc còn lại; quản lý quyết định hoàn tiền và ghi lý do | Quy tắc trạng thái, tính cọc, quyền, test tích hợp | Giao diện khách vãng lai, nhân viên, quản lý |
| **53–60** | Sửa lỗi, kiểm thử liên luồng, hoàn thiện báo sự cố cơ bản, tài liệu sử dụng, dữ liệu demo và diễn tập bảo vệ | Kiểm thử, tối ưu luồng lỗi, tài liệu API | UX trên điện thoại, hướng dẫn và kịch bản demo |

**Mốc trình diễn:** ngày 20 trình diễn ảnh chỉ số → duyệt → hóa đơn; ngày 30 trình diễn thanh toán và xác nhận; ngày 42 trình diễn tìm phòng → yêu cầu → email theo dõi; ngày 52 trình diễn giữ chỗ → cọc còn lại; ngày 60 chạy toàn bộ kịch bản demo.

## Quy tắc nghiệp vụ mục tiêu (chưa phải hành vi đã triển khai)

- Chỉ số AI đọc từ ảnh luôn là gợi ý. Quản lý hoặc nhân viên có quyền xác nhận trước khi tính tiền; lưu ảnh, giá trị trước/sau, người và thời điểm duyệt.
- Hóa đơn tự sinh ở trạng thái nháp. Người có quyền xem và chốt trước khi gửi; có thể tự duyệt trong phạm vi quyền, với nhật ký thao tác.
- Ảnh chuyển khoản không chứng minh hệ thống đã nhận tiền. Nhân viên đối chiếu và xác nhận; quản lý quyết định có cho trả một phần.
- Nhân viên xác nhận yêu cầu giữ chỗ trước khi hệ thống mời thanh toán. Tiền giữ chỗ đã xác nhận được trừ vào cọc hợp đồng đúng một lần.
- Khi hủy sau khi đã nhận tiền, quản lý ghi số tiền hoàn và lý do; quyết định hoàn và việc đã hoàn thực tế là hai trạng thái riêng.
- Khách vãng lai truy cập trạng thái yêu cầu qua liên kết email có hạn. Mọi quyền nhân viên phải kiểm tra theo chi nhánh ở máy chủ.

## Kiểm thử, cấu trúc và kiểm soát phạm vi

Domain.UnitTests kiểm tra quy tắc miền; Application.UnitTests kiểm tra use case với store/UoW giả; Infrastructure.IntegrationTests kiểm tra PostgreSQL, mapping và migration; Web.IntegrationTests kiểm tra HTTP, cookie, routing, SignalR và ranh giới kiến trúc. Tất cả nằm dưới tests/ với tiền tố QuanLyChoThuePhongTroWeb. Chạy dotnet build QuanLyChoThuePhongTroWeb.sln và dotnet test QuanLyChoThuePhongTroWeb.sln trước khi hợp nhất thay đổi code. Full suite cần QLCTPT_TEST_CONNECTION_STRING trỏ vào DB riêng có hậu tố _test hoặc _integration_test; xem README ở root. Chỉ thêm E2E khi luồng chính ổn định.

Endpoint và hợp đồng HTTP đặt tại src/QuanLyChoThuePhongTroWeb.Web/Api/V1; Razor khách công khai tại Web/Areas/KhachVangLai. DTO/use case và interface nằm trong Application; entity ở Domain; EF query, mapping, migration ở Infrastructure. Không để Application dùng DbContext, ASP.NET Core hoặc IConfiguration. Web không trực tiếp tham chiếu Domain.

Refactor giữ nguyên schema. Tính năng mới như lịch xem/giữ chỗ cần thiết kế schema và migration riêng được review; không sinh hoặc áp migration chỉ để đồng bộ tài liệu. Kế hoạch lát cắt tìm phòng/lịch xem: [public-room-discovery-and-viewing](superpowers/plans/2026-09-17-public-room-discovery-and-viewing.md).
Nếu chậm tiến độ, giữ nguyên các luồng chỉ số → hóa đơn → thanh toán và tìm phòng → yêu cầu → giữ chỗ. Giảm trước AI hội thoại, OCR nâng cao, E2E, mobile và IoT. IoT tương lai chỉ gửi dữ liệu thành bản ghi nháp, sau đó vẫn đi qua bước duyệt của người có quyền.
