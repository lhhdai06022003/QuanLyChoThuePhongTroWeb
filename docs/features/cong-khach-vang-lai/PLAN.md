# Cổng khách vãng lai — Implementation Plan

## Phạm vi và kiến trúc

Triển khai [SPEC.md](SPEC.md) trên nhánh `thuan`: khách xem phòng, đặt lịch, đăng ký, giữ chỗ, gửi minh chứng; nhân viên đối chiếu, lập hợp đồng và xử lý hoàn. Razor MVC theo bốn tầng hiện có. Các chính sách chưa được người dùng chốt tại mục 11 của SPEC được giữ ngoài luồng tự động.

## Tiến độ thực thi

| Task | Kết quả triển khai | Bằng chứng chính |
| --- | --- | --- |
| T00 — Chốt nghiệp vụ | Đã cập nhật D01–D07; các câu hỏi còn mở được ghi riêng | SPEC, hội thoại người dùng |
| T01 — Tài khoản | Dùng trang đăng nhập hiện có; email/CCCD, quyền sở hữu, làm mới cookie. Trang đăng nhập dẫn đến đăng ký khách và giữ đường quay lại phòng đã chọn | GuestAccountServiceTests, GuestJourneyTests |
| T02 — Tin phòng/ảnh | Danh sách, bộ lọc, phân trang, chi tiết, gallery; nhân viên đăng/ẩn tin và quản lý ảnh | PublicRoomServiceTests, PublicRoomManagementServiceTests, PublicRoomQueryTests |
| T03 — Lịch xem | Lịch ẩn danh/có tài khoản, sức chứa; quản lý khung giờ, xác nhận/từ chối, đổi/hủy/đã xem | GuestViewingServiceTests, ViewingSlotServiceTests, GuestReservationJourneyTests |
| T04 — Giữ chỗ | Trực tiếp hoặc từ lịch cùng chủ đã xác nhận; duyệt cố định 50%, chốt giá thuê và thời hạn | GuestReservationServiceTests, ReservationConcurrencyTests |
| T05 — Tiền/minh chứng | Ảnh riêng, đối chiếu đủ tiền một giao dịch, chống mã ngân hàng trùng; từ chối ghi nhận tiền thực nhận để hoàn | HoldPaymentServiceTests, ReservationProofStorageTests, GuestJourneyTests |
| T06 — Hết hạn/hủy | Hạn cộng 5 phút, bảo vệ minh chứng đang chờ; nhân viên kết thúc yêu cầu chưa có tiền/minh chứng | ReservationExpiryServiceTests, GuestReservationJourneyTests |
| T07 — Hợp đồng/cấn trừ | Transaction chuyển tài khoản/người thuê/hợp đồng/phòng; cấn trừ hóa đơn tháng bắt đầu, dư chuyển tháng sau | ReservationContractServiceTests, ReservationInvoiceCreditTests, ActualBillingServiceCreditsPartialStartMonthAndCarriesRemainder |
| T08 — Hoàn tiền | Quyết định 0/một phần/toàn bộ, nhiều lần chi không vượt quyết định, loại trừ chuyển hợp đồng | HoldRefundServiceTests, GuestReservationJourneyTests |
| T09 — Bàn giao | Review độc lập, kiểm thử PostgreSQL/HTTP, kiểm tra bố cục mobile/desktop; cập nhật tài liệu | Nhật ký và hướng dẫn bên dưới |

## Vị trí mã nguồn

- Application: `Features/PhongCongKhais`, `KhachVangLais`, `LichXemPhongs`, `GiuChos`; DTO, service, persistence interface và use case.
- Domain: `GiuChoRules`, CCCD hồ sơ khách, giá thuê đã chốt, `CanTruTienGiuChoHoaDon`.
- Infrastructure: các store trong `Persistence/Features`, job `HetHanGiuChoJob`, Cloudinary ảnh phòng và local storage minh chứng riêng.
- Web: `Areas/KhachVangLai`, controller/view mới trong `Areas/QuanLyNhaTro`, CSS `khach-vang-lai.css`.
- Migration mới: `20260925075200_AddGuestIdentityForReservations`, `20260926131005_AddReservationContractCredit`. Không sửa migration đã tồn tại.

## Quyết định triển khai

- Cổng người thuê có lối vào danh sách phòng, lịch xem và yêu cầu đặt cọc. Trang phòng công khai hiển thị tên tài khoản đang đăng nhập; nút hẹn xem/đặt cọc mở trang bước tương ứng. Nếu chưa đăng nhập, trang đăng nhập chung giữ `returnUrl` để trở lại đúng bước đã chọn.
- Người thuê hiện có dùng cùng `NguoiDung` và `NguoiThue`. Khi tạo lịch/yêu cầu mới, hệ thống liên kết một hồ sơ `KhachVangLai` với tài khoản đó vì các bảng lịch/giữ chỗ đang dùng khóa ngoại này; không đổi vai trò hay tạo tài khoản thứ hai. Khi lập hợp đồng từ khoản giữ chỗ mới, dùng lại hồ sơ người thuê hiện có.
- Giá thuê được lưu tại lúc nhân viên duyệt; đổi giá niêm yết sau đó không đổi khoản giữ chỗ/hợp đồng đã chốt.
- Cả thao tác ghi và đọc dữ liệu nhân viên kiểm tra tài khoản đang hoạt động cùng phân công chi nhánh chưa bị thu hồi; role lấy từ database.
- Tạo hợp đồng từ giữ chỗ là command nguyên tử riêng. Hóa đơn vẫn dùng `HoaDonService` và calculator hiện có. Tạo/kích hoạt hợp đồng thông thường cũng khóa phòng và chặn giữ chỗ hoạt động.
- Quyết định hoàn kết thúc giữ phòng; 0 đồng có trạng thái `KhongHoan`, không tạo giao dịch chi 0 đồng. Quyết định đã lưu chưa được sửa/hủy.
- Hóa đơn tháng bắt đầu phải phát sinh trước các tháng sau. Chỉ số điện/nước vẫn phải được duyệt theo luồng hóa đơn hiện có.
- Hóa đơn đã cấn trừ chưa cho sửa/xóa trực tiếp để giữ nguyên phân bổ tiền; chưa có quy trình điều chỉnh hóa đơn cấn trừ.
- Lịch đã xác nhận được nhân viên đổi sang giờ thỏa thuận thủ công; giải phóng sức chứa của khung cũ, lưu giờ cũ/mới và lý do. Chỉ đánh dấu đã xem sau giờ hẹn.
- Queue hiện trả toàn bộ bản ghi được phép xem, không cắt im lặng 100 dòng. Khi dữ liệu lớn cần bổ sung phân trang/filter phía server.
- Không liên kết lịch ẩn danh tự động bằng số điện thoại; không tự xác nhận tiền thừa/đến muộn, không cho khách tự hủy/đổi khi chính sách còn mở.

## Nhật ký và kiểm chứng

- 25–26/09/2026: Triển khai rồi tiếp tục sau yêu cầu tạm dừng. Giữ nguyên thay đổi AI/config có sẵn của người dùng; chưa commit/push.
- PostgreSQL: người dùng cung cấp database `QuanLyPhongTroDb_Test`; migration và kiểm thử dùng database này. Không dùng database phát triển để chạy test.
- TDD: đã ghi nhận RED rồi GREEN cho quy tắc tiền, hợp đồng, cấn trừ, khung giờ, hủy chưa thanh toán, rollback job hết hạn và ưu tiên giờ khung đã chọn.
- Kiểm thử database phát hiện và đã sửa: EF không dịch được sắp xếp sau DTO projection; exception serialization bị bọc thêm; trạng thái EF còn bẩn sau transaction thất bại.
- Review độc lập đã sửa: kích hoạt hợp đồng cũ bỏ qua giữ chỗ, job hết hạn giữ dữ liệu rollback, queue che minh chứng cũ, thiếu thao tác nhân viên trên lịch đã xác nhận. Bổ sung kiểm tra thời điểm thu tiền khi từ chối minh chứng.
- Kiểm thử tài chính thực tế: tranh suất cuối/phòng cuối; hoàn và chuyển hợp đồng đồng thời chỉ một bên phân bổ; tổng hoàn không vượt quyết định; cọc cấn trừ đúng một lần và chuyển dư.
- HTTP: khách tạo giữ chỗ, tải minh chứng riêng, mở các màn nhân viên; thao tác nhân viên xác nhận/chuyển hợp đồng được kiểm tra qua service thực. Cookie cập nhật role, tài khoản bị khóa bị đăng xuất. Kiểm tra IDOR giữa hai khách, CSRF, return URL bên ngoài và role giả.
- Theo phản hồi người dùng: bỏ view/model và xử lý đăng nhập riêng của cổng khách; dùng `/QuanLyNhaTro/DangNhap`, giữ return URL hợp lệ để quay lại giữ chỗ. Đường dẫn `/tai-khoan/dang-nhap` chỉ chuyển hướng về trang chung. Đăng ký xử lý sau theo yêu cầu.
- 28/09/2026: yêu cầu mới bắt buộc đăng nhập trước khi liên hệ, hẹn xem và đặt cọc. Cập nhật route lịch xem, nút trên chi tiết và đặc tả; trang chi tiết công khai vẫn xem được. Đăng công khai ba phòng trống sẵn có (103, 919, 111) trong database phát triển để kiểm tra danh sách nhiều phòng; dữ liệu giá giữ nguyên, cả ba phòng chưa có ảnh.
- 28/09/2026: chạy Web trên `http://localhost:5009` với database phát triển. HTTP `/phong` trả 200 và đúng 3 thẻ phòng; chi tiết trả 200, lịch xem/giữ chỗ ẩn danh đều chuyển 302 đến đăng nhập chung với `ReturnUrl`. Bộ Web integration sau thay đổi đạt **39/39** (`.agent/viewing-full-web.log`).
- Yêu cầu tiếp theo: tải nhiều ảnh trong một lần tại màn nhân viên và đổi ảnh bằng mũi tên/ảnh thu nhỏ ở chi tiết công khai. Kiểm thử controller giới hạn 10 ảnh và số ảnh đã tải; kiểm thử HTTP gallery đủ ảnh hoạt động, bỏ ảnh ẩn.
- Kiểm chứng sau thay đổi ảnh: Web integration **41/41** (`.agent/photo-full-web.log`), `node --check` cho script gallery, `git diff --check` đạt. Web đã khởi động lại ở `http://localhost:5009`; HTTP trả 3 phòng, chi tiết 200 và script gallery 200. Chưa tải ảnh thật lên Cloudinary vì chưa có tệp ảnh phòng do người dùng cung cấp.
- Khi người dùng tải ảnh từ màn Tin phòng, Cloudinary qua proxy `127.0.0.1:9` bị từ chối kết nối và tạo trang lỗi. Sửa môi trường Development dùng lưu ảnh công khai tại máy, đồng thời chuyển lỗi kết nối upload thành thông báo trong form; ảnh phòng người dùng tải lên cần thử lại sau khi web khởi động lại.
- Kiểm chứng sửa lỗi: Web integration **42/42** (`.agent/photo-error-web-final.log`), gồm POST multipart hai ảnh qua HTTP và thông báo khi kết nối storage lỗi; kiểm thử local storage **2/2** (`.agent/photo-storage-final.log`). Web tại `http://localhost:5009/phong` trả 200 và 3 phòng; ảnh tĩnh từ thư mục upload trả 200. Ảnh tải lên trước khi sửa đã thất bại, cần chọn tệp và tải lại.
- Kiểm chứng phần đăng nhập: bộ Web integration đạt **39/39** (`.agent/shared-login-green.log`); đã mở nút đăng nhập từ `/phong` trên trình duyệt và xác nhận giao diện đăng nhập hiện có.
- 28/09/2026: nối đăng nhập chung → đăng ký khách → đăng nhập lại → lịch xem/giữ chỗ của phòng đã chọn. Cấu hình tài khoản VietQR dùng chung trong `appsettings.json` cục bộ bị Git bỏ qua; không ghi thông tin ngân hàng vào mã nguồn. Form đăng ký chỉ hiển thị `ReturnUrl` nội bộ đã kiểm tra. Web integration **43/43** (`.agent/booking-final-web.log`); Web cổng 5009 trả HTTP 200 cho danh sách, đăng nhập và đăng ký, và 302 về đăng nhập với `ReturnUrl` khi khách chưa đăng nhập mở bước giữ chỗ.
- 28/09/2026: theo phản hồi về cổng người thuê, bổ sung menu/dashboard tìm phòng, lịch xem, đặt cọc; cho tài khoản `KhachThue` đặt lịch và giữ chỗ theo chính hồ sơ người thuê. Trang phòng hiện tên đăng nhập; kiểm thử riêng xác nhận hai đường quay lại sau đăng nhập và luồng người thuê đến hợp đồng.
- Kiểm chứng thay đổi này: Web integration **44/44** (`.agent/tenant-booking-web.log`); nhóm Infrastructure giữ chỗ **12/12** (`.agent/tenant-booking-infra.log`). Web đã khởi động lại tại `http://localhost:5009`; HTTP `/phong` trả 200, hai đường hẹn xem/đặt cọc ẩn danh đều trả 302 tới trang đăng nhập chung với `ReturnUrl` chính xác.
- UI: đã xem danh sách, chi tiết, form lịch xem và đăng ký ở 375/1280 px; không tràn ngang. Dữ liệu minh họa thuộc DB test, không dùng ảnh phòng/ảnh chuyển khoản thật. Screenshot toàn trang của IAB có lỗi ghép hình; đã đối chiếu DOM chỉ có một card/phòng.
- Storage/email bên ngoài được fake trong integration test; chưa xác minh upload Cloudinary hoặc kết nối ngân hàng thật.

### Lệnh kiểm tra

Dùng .NET 8 và cấu hình `QLCTPT_TEST_CONNECTION_STRING` trỏ DB riêng có hậu tố `_test` hoặc `_integration_test`:

```powershell
dotnet build QuanLyChoThuePhongTroWeb.sln --no-restore -m:1
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests --no-restore
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --no-restore
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests --no-restore
dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests --no-restore
git diff --check
```

Kết quả lượt cuối được ghi sau khi chạy, không coi thiếu database là pass. Máy này dùng SDK 8 được ghim trong thư mục `.agent` bị Git bỏ qua; output build/test riêng để tránh DLL của app người dùng đang chạy.
