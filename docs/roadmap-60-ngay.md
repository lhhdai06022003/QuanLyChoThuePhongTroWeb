# Kế hoạch phát triển 60 ngày cho hai người

## Mục tiêu và nguyên tắc

Phát triển hệ thống cho **một đơn vị quản lý nhiều chi nhánh**. Trong 60 ngày, ưu tiên giảm thao tác cho quản lý, nhân viên và khách thuê bằng quy trình tự động có bước duyệt. AI hỗ trợ đọc ảnh chỉ số và gợi ý thông tin; người có quyền quyết định chỉ số, hóa đơn và tiền thực nhận. Trang khách vãng lai giúp tìm phòng, liên hệ, giữ chỗ và tiến tới hợp đồng. Mobile và IoT là hướng mở rộng sau giai đoạn này.

Giữ ASP.NET Core MVC trong `QuanLyChoThuePhongTroWeb.csproj`. API JSON phiên bản `/api/v1` sẽ ở cùng ứng dụng; MVC và API cùng gọi service nghiệp vụ. Không tách hệ thống thành nhiều backend trong giai đoạn khóa luận.

## Cấu trúc repository

Giữ source MVC và EF Core hiện tại tại root; chỉ thêm project test riêng và chỗ dành cho API/khách vãng lai. Các thư mục đánh dấu “khung” hiện chưa có tính năng.

~~~text
QuanLyChoThuePhongTroWeb/
├── Api/V1/{Controllers,Contracts}/    # Khung API JSON
├── Areas/{QuanLyNhaTro,KhachThue,KhachVangLai}/
├── Controllers/ Data/ Models/ Services/ Views/
├── Migrations/ wwwroot/
├── tests/
│   ├── QuanLyChoThuePhongTroWeb.UnitTests/
│   └── QuanLyChoThuePhongTroWeb.IntegrationTests/
├── docs/{architecture,features,deployment,superpowers}/
├── .github/workflows/               # CI sẽ thêm khi có test nghiệp vụ
├── Program.cs
├── QuanLyChoThuePhongTroWeb.csproj
└── QuanLyChoThuePhongTroWeb.sln
~~~
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
| **1–7** | Kiểm kê chức năng hiện có; backlog ưu tiên; test project và CI chạy build/test; sơ đồ quyền theo chi nhánh và trạng thái hóa đơn/thanh toán/giữ chỗ | Rà database, service, migration, phân quyền; chuẩn bị test | Rà toàn bộ luồng UI, ghi lỗi và màn hình thiếu |
| **8–20** | Khách hoặc nhân viên gửi ảnh chỉ số; AI gợi ý; người có quyền sửa/duyệt; hệ thống tạo hóa đơn nháp; người có quyền chốt trước khi gửi | Xử lý dữ liệu, phép tính, nhật ký, test; AI có đường nhập tay | Màn hình nộp ảnh, đối chiếu, duyệt, chốt hóa đơn |
| **21–30** | Yêu cầu thanh toán/VietQR; khách gửi ảnh chuyển khoản; nhân viên xác nhận tiền; trả một phần khi quản lý cho phép | Trạng thái thanh toán, số dư, chống ghi nhận trùng | Trang khách thuê và hàng đợi xác nhận |
| **31–42** | Phòng công khai có tìm/lọc, xem chi tiết, liên hệ/đặt lịch xem; nhân viên xử lý; khách theo dõi qua liên kết email | Service phòng và lịch xem, API/DTO, email link có hạn, test | Trang công khai và màn hình nhân viên |
| **43–52** | Nhân viên duyệt yêu cầu giữ chỗ trước; hệ thống tạo yêu cầu thanh toán có hạn; xác nhận tiền; tính cọc còn lại; quản lý quyết định hoàn tiền và ghi lý do | Quy tắc trạng thái, tính cọc, quyền, test tích hợp | Giao diện khách vãng lai, nhân viên, quản lý |
| **53–60** | Sửa lỗi, kiểm thử liên luồng, hoàn thiện báo sự cố cơ bản, tài liệu sử dụng, dữ liệu demo và diễn tập bảo vệ | Kiểm thử, tối ưu luồng lỗi, tài liệu API | UX trên điện thoại, hướng dẫn và kịch bản demo |

**Mốc trình diễn:** ngày 20 trình diễn ảnh chỉ số → duyệt → hóa đơn; ngày 30 trình diễn thanh toán và xác nhận; ngày 42 trình diễn tìm phòng → yêu cầu → email theo dõi; ngày 52 trình diễn giữ chỗ → cọc còn lại; ngày 60 chạy toàn bộ kịch bản demo.

## Quy tắc nghiệp vụ bắt buộc

- Chỉ số AI đọc từ ảnh luôn là gợi ý. Quản lý hoặc nhân viên có quyền xác nhận trước khi tính tiền; lưu ảnh, giá trị trước/sau, người và thời điểm duyệt.
- Hóa đơn tự sinh ở trạng thái nháp. Người có quyền xem và chốt trước khi gửi; có thể tự duyệt trong phạm vi quyền, với nhật ký thao tác.
- Ảnh chuyển khoản không chứng minh hệ thống đã nhận tiền. Nhân viên đối chiếu và xác nhận; quản lý quyết định có cho trả một phần.
- Nhân viên xác nhận yêu cầu giữ chỗ trước khi hệ thống mời thanh toán. Tiền giữ chỗ đã xác nhận được trừ vào cọc hợp đồng đúng một lần.
- Khi hủy sau khi đã nhận tiền, quản lý ghi số tiền hoàn và lý do; quyết định hoàn và việc đã hoàn thực tế là hai trạng thái riêng.
- Khách vãng lai truy cập trạng thái yêu cầu qua liên kết email có hạn. Mọi quyền nhân viên phải kiểm tra theo chi nhánh ở máy chủ.

## Kiểm thử, cấu trúc và kiểm soát phạm vi

`tests/QuanLyChoThuePhongTroWeb.UnitTests` kiểm tra phép tính, quyền và chuyển trạng thái. `tests/QuanLyChoThuePhongTroWeb.IntegrationTests` kiểm tra luồng HTTP/service/database khi đã có môi trường test cô lập; không dùng database phát triển hoặc production. Chạy `dotnet build QuanLyChoThuePhongTroWeb.sln` và `dotnet test QuanLyChoThuePhongTroWeb.sln` trước khi hợp nhất thay đổi. Chỉ thêm E2E khi luồng chính ổn định.

`Api/V1/Controllers` và `Api/V1/Contracts` dành cho endpoint và DTO; không trả trực tiếp EF entity. `Areas/KhachVangLai` dành cho giao diện khách công khai. Các thư mục này là khung, chưa có chức năng chạy. Kế hoạch triển khai chi tiết cho lát cắt tìm phòng/lịch xem nằm tại `docs/superpowers/plans/2026-09-17-public-room-discovery-and-viewing.md`; đường dẫn test đã được đồng bộ với cấu trúc mới.

Nếu chậm tiến độ, giữ nguyên các luồng chỉ số → hóa đơn → thanh toán và tìm phòng → yêu cầu → giữ chỗ. Giảm trước AI hội thoại, OCR nâng cao, E2E, mobile và IoT. IoT tương lai chỉ gửi dữ liệu thành bản ghi nháp, sau đó vẫn đi qua bước duyệt của người có quyền.