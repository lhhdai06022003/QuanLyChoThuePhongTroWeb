# Thiết kế quy trình tìm phòng, giữ chỗ và chuyển thành hợp đồng

## Hiện trạng đối chiếu ngày 20/09/2026

Tài liệu này là thiết kế mục tiêu chưa triển khai, không phải mô tả tính năng đang vận hành. Repository đã chuyển sang Clean Architecture bốn tầng dưới src/ và có bốn project test. Area KhachVangLai và Api/V1 trong Web mới có khung. Chưa có entity lịch xem, giữ chỗ hoặc phân công NhanVienChiNhanh; PhongTro chưa có thuộc tính đăng tin/ảnh đại diện.

Hệ thống đã có hợp đồng, hóa đơn, VietQR, lịch sử thanh toán, email, Cloudinary và SignalR để tái sử dụng. Chưa được coi các cơ chế đó là quy trình xác nhận ảnh chuyển khoản hoặc giữ chỗ. Hóa đơn hiện chỉ có ChuaThanhToan/DaThanhToan.

## Ràng buộc triển khai theo kiến trúc hiện tại

- Entity và chuyển trạng thái thuần đặt ở src/QuanLyChoThuePhongTroWeb.Domain.
- Use case, DTO, store interface và quyền nghiệp vụ đặt ở src/QuanLyChoThuePhongTroWeb.Application/Features. Dùng IUnitOfWork và abstraction; không dùng DbContext, HTTP/cookie hoặc IConfiguration trong Application.
- EF store, mapping, transaction, migrations và tích hợp ngoài đặt ở src/QuanLyChoThuePhongTroWeb.Infrastructure. Dùng lại abstraction email/ảnh hiện có khi phù hợp.
- Controller, Razor, cookie khách vãng lai, antiforgery và endpoint nằm ở src/QuanLyChoThuePhongTroWeb.Web. Web chỉ dùng hợp đồng Application, không trực tiếp tham chiếu Domain.
- Phân biệt bảo vệ schema khi refactor với bổ sung dữ liệu cho tính năng mới. Thiết kế này cần migration mới được review; không sửa migration lịch sử và không tạo migration trong tác vụ cập nhật tài liệu.

Lát cắt đầu tiên là [tìm phòng và lịch xem](../plans/2026-09-17-public-room-discovery-and-viewing.md). Giữ chỗ/thanh toán và chuyển cọc/hoàn tiền cần kế hoạch riêng sau đó. Các mốc trong [roadmap](../../roadmap-60-ngay.md) là dự kiến, chưa xác nhận ngày bắt đầu.

## Mục tiêu và phạm vi

Một đơn vị quản lý nhiều chi nhánh. Khách vãng lai xem phòng công khai, liên hệ, đặt lịch xem và gửi yêu cầu giữ chỗ. Nhân viên xác nhận phòng và điều kiện thuê trước khi hệ thống mời khách thanh toán. Tiền giữ chỗ được tính vào tiền cọc khi ký hợp đồng. Quy trình này dùng cho một phòng tại một thời điểm; việc ký hợp đồng vẫn do người có quyền xác nhận.

Trong phạm vi 60 ngày, thanh toán dùng VietQR/chuyển khoản, khách gửi ảnh giao dịch và nhân viên xác nhận tiền thực nhận. Tích hợp webhook đối soát ngân hàng và IoT là phần mở rộng sau.

## Vai trò và quyền

- Khách vãng lai: xem phòng được công khai, gửi nhu cầu, đặt lịch xem, yêu cầu giữ chỗ và theo dõi yêu cầu qua liên kết một lần gửi tới email đã xác minh; hồ sơ công khai chưa cần tài khoản khách thuê.
- Nhân viên được phân công chi nhánh: xử lý lịch xem, xác nhận điều kiện và số tiền giữ chỗ, đối chiếu chuyển khoản, chuẩn bị hợp đồng.
- Quản lý: có quyền của nhân viên; quyết định hủy yêu cầu đã nhận tiền, số tiền hoàn, lý do và xử lý ngoại lệ.
- AI: chỉ gợi ý phòng từ dữ liệu công khai và giúp điền thông tin; không xác nhận tiền, giữ phòng hoặc quyết định hoàn tiền.

Mỗi tác vụ phải kiểm tra quyền tại máy chủ và phạm vi chi nhánh, không dựa vào trạng thái giao diện.

## Luồng khách tìm phòng

Phòng được công khai có ảnh, mô tả, chi nhánh, giá thuê và các khoản phí. Kết quả tìm kiếm lọc theo giá, vị trí/chi nhánh, diện tích và số người. Trạng thái có thể đăng ký được tính từ tình trạng phòng hiện có cùng yêu cầu giữ chỗ còn hiệu lực. Khách có thể gửi thông tin liên hệ hoặc đề nghị lịch xem; nhân viên xác nhận thời gian. Đặt lịch xem chưa khóa phòng.

Khách gửi yêu cầu giữ chỗ cho phòng cụ thể. Nhân viên kiểm tra phòng, điều kiện thuê, số tiền giữ chỗ và thời hạn thanh toán cụ thể; sau đó chấp thuận hoặc từ chối kèm lý do. Khi chấp thuận, hệ thống tạo mã yêu cầu và QR chuyển khoản. Trong thời hạn chờ thanh toán, phòng tạm ngừng nhận lời mời thanh toán giữ chỗ khác. Yêu cầu chờ thanh toán không được hiển thị là đã nhận tiền.

Khách tải ảnh chuyển khoản. Trạng thái chuyển sang `ChoXacNhanTien`; ảnh chỉ là chứng cứ do khách cung cấp. Nhân viên đối chiếu số tiền thực nhận và mã giao dịch trước khi xác nhận. Khi xác nhận, yêu cầu chuyển sang `DangGiuCho`, lưu số tiền và thời hạn ký hợp đồng. Chỉ khi đã có kết quả đối chiếu mới cập nhật khoản tiền đã thu.

## Trạng thái và ngoại lệ

Trạng thái yêu cầu: `MoiTao` → `ChoThanhToan` → `ChoXacNhanTien` → `DangGiuCho` → `DaChuyenHopDong`. Nhánh kết thúc khác: `TuChoi`, `HetHan`, `DaHuy`. Mỗi chuyển trạng thái lưu người thực hiện, thời điểm và lý do khi áp dụng.

Nếu hết hạn thanh toán mà chưa có báo chuyển khoản, yêu cầu hết hiệu lực và phòng có thể nhận yêu cầu mới. Khoản tiền đến muộn được xử lý thủ công; không tự kích hoạt lại yêu cầu đã hết hạn. Nếu có ảnh hoặc báo chuyển khoản đang chờ kiểm tra, nhân viên phải xử lý trước khi giải phóng phòng. Khi quá thời hạn ký hợp đồng sau khi đã nhận tiền, yêu cầu chuyển vào danh sách quản lý xử lý; hệ thống không tự quyết định việc hoàn hay giữ tiền.

Chỉ một yêu cầu của một phòng được ở trạng thái `ChoThanhToan`, `ChoXacNhanTien` hoặc `DangGiuCho` tại cùng thời điểm. Việc chấp thuận và chuyển trạng thái cần giao dịch database cùng ràng buộc duy nhất cho yêu cầu giữ chỗ đang hiệu lực trên mỗi phòng; yêu cầu thứ hai nhận thông báo phòng không còn sẵn để giữ chỗ.

## Tiền giữ chỗ, hoàn tiền và hợp đồng

Ghi riêng khoản thanh toán giữ chỗ với tiền thuê và các lần thu hóa đơn. Lưu số tiền yêu cầu, số tiền xác nhận thực nhận, mã giao dịch, ảnh, nhân viên xác nhận và lịch sử điều chỉnh. Không cho một mã giao dịch được xác nhận hai lần. Số tiền nhận khác khoản yêu cầu phải chuyển cho quản lý xử lý trước khi xác nhận giữ chỗ.

Khi tạo hợp đồng từ yêu cầu `DangGiuCho`, kế thừa hồ sơ khách và phòng. Số cọc còn phải thu bằng số cọc hợp đồng trừ tiền giữ chỗ được áp dụng; khoản áp dụng không vượt quá cọc hợp đồng. Ví dụ cọc 2.000.000 đồng, đã xác nhận giữ chỗ 500.000 đồng thì còn thu 1.500.000 đồng. Việc áp dụng khoản giữ chỗ được ghi một lần, liên kết với hợp đồng, và không tự ghi thành tiền thanh toán hóa đơn.

Nếu khách hủy sau khi đã trả tiền, quản lý quyết định số tiền hoàn từ 0 đến số đã nhận, ghi lý do và người quyết định. Trạng thái `DaHuy` lưu riêng `SoTienHoanDuKien`, `SoTienDaHoan`, chứng từ/thông tin xác nhận hoàn và ngày hoàn. Quyết định hoàn tiền không đồng nghĩa tiền đã được hoàn; nhân viên có quyền ghi nhận khi việc hoàn thực sự hoàn tất, tối đa bằng số quản lý đã duyệt. Phòng được giải phóng theo quyết định hủy, còn khoản hoàn tiếp tục được theo dõi đến khi xong.

## Thành phần và dữ liệu

Thêm các thực thể cho thông tin quan tâm/lịch xem, yêu cầu giữ chỗ, lần báo thanh toán, lần xác nhận tiền và quyết định hoàn tiền. Dịch vụ giữ chỗ chịu trách nhiệm kiểm tra phòng, trạng thái và chuyển đổi sang hợp đồng; dịch vụ thanh toán chịu trách nhiệm chống ghi nhận trùng. Chỉ giao diện và API được phép gọi các dịch vụ này, không để AI ghi trực tiếp vào database. Mở rộng phần công khai của `PhongTro` bằng ảnh và thuộc tính đăng tin; giữ trạng thái vận hành hiện có của phòng và tính khả dụng từ yêu cầu giữ chỗ.

## Kiểm chứng và giới hạn

Kiểm thử luồng thành công từ tìm phòng đến hợp đồng; hai nhân viên chấp thuận cùng lúc; ảnh chuyển khoản sai hoặc gửi trùng; hết hạn khi đang chờ xác nhận; hủy sau khi đã nhận tiền; hoàn một phần; chuyển cọc đúng một lần; và truy cập sai chi nhánh. Kiểm tra giao diện trên điện thoại. Việc hoàn tiền là thao tác thực tế do người có quyền thực hiện rồi ghi nhận; hệ thống không gọi ngân hàng để hoàn tiền trong phạm vi này.
