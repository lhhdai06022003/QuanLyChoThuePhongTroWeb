# SPEC — Cổng khách vãng lai xem phòng và đặt cọc

**Trạng thái:** nghiệp vụ đã được chốt, 08/10/2026.
**Ràng buộc:** chỉ dùng schema đã có trên `master` (migration cuối `20260922163513_AddPublicViewingAndReservations`); không tạo hoặc sửa migration, bảng, cột. Không dùng `QuanLyPhongTroDb` cho kiểm thử.

## Mục tiêu

Khách xem phòng được đăng công khai, thông tin và nhiều ảnh tại `/phong` mà không cần đăng nhập. Khách đăng ký/đăng nhập để hẹn xem, giữ chỗ trực tiếp hoặc từ lịch xem, tải minh chứng chuyển khoản, theo dõi hạn và trạng thái. Nhân viên cấu hình lịch, duyệt giữ chỗ, đối soát tiền, gửi email khi phòng có người đặt cọc, quyết định áp dụng tiền vào cọc hợp đồng hoặc hoàn tiền. Giữ thiết kế công khai hiện tại khi nối các màn hình xem trước với backend.

## Dữ liệu và vai trò hiện có

Schema đã có `khach_vang_lai`, `khung_gio_xem_phong`, `yeu_cau_xem_phong`, `yeu_cau_giu_cho`, `yeu_cau_thanh_toan_giu_cho`, `minh_chung_thanh_toan_giu_cho`, `giao_dich_giu_cho`, `quyet_dinh_hoan_tien_giu_cho`, `giao_dich_hoan_tien_giu_cho`, `ap_dung_tien_giu_cho_vao_tien_coc`, ảnh phòng và lịch sử trạng thái. `YeuCauXemPhong.KhachVangLaiId` nullable trong DB nhưng **endpoint mới bắt buộc đăng nhập** và gắn vào hồ sơ khách. Không đổi schema để sửa nullable.

Domain có role `KhachVangLai=3`; Application và kiểm tra cookie phải hỗ trợ cùng giá trị. `KhachVangLai` không có cột CCCD, vì vậy CCCD được thu thập khi lập hợp đồng, không lưu vào trường khác sai nghĩa. Đăng ký không cần xác minh email trước khi giữ chỗ.

## Phòng công khai và lịch xem

1. Chỉ hiển thị phòng chưa xóa, thuộc chi nhánh chưa xóa, `DuocDangTin=true`, đang trống và chưa có giữ chỗ hoạt động. Danh sách lọc giá, diện tích, khu vực và phân trang; chi tiết hiển thị gallery ảnh đang hoạt động. Kiểm tra lại điều kiện trước mọi thao tác ghi.
2. Nhân viên có quyền chi nhánh tạo khung giờ với sức chứa. Khách đã đăng nhập nhập tên, số điện thoại, **email bắt buộc**, chọn khung giờ hoặc thời gian mong muốn trong tương lai. Chọn khung còn chỗ được xác nhận ngay; giờ đề xuất chờ nhân viên. Service kiểm tra sức chứa trong thao tác ghi để không vượt suất cuối.
3. Khách chỉ xem và hủy lịch của mình **trước giờ hẹn bắt đầu**; nhân viên chỉ xử lý lịch thuộc chi nhánh được phân công. Ghi trạng thái, người xử lý, thời điểm và lý do trong các bảng lịch sử hiện có.
4. Khi phòng đã có người đặt cọc, nhân viên **bấm gửi thủ công**: xem trước người nhận là khách có lịch xem sắp tới, yêu cầu giữ chỗ mới, hoặc giữ chỗ đã duyệt nhưng chưa trả đủ tiền của cùng phòng; xác nhận mỗi lần gửi. Gửi email riêng, không lộ thông tin khách khác; hiển thị lỗi nếu SMTP không gửi được. Do schema không có nhật ký email, không hứa chống gửi lặp tuyệt đối qua nhiều lần bấm.

## Tài khoản và giữ chỗ

1. Đăng ký tạo `NguoiDung` role khách vãng lai và `KhachVangLai` trong một giao dịch, kiểm tra tên đăng nhập trùng, băm mật khẩu bằng dịch vụ sẵn có. Đăng nhập thành công quay về đường dẫn nội bộ khách đang dùng.
2. Khách đã đăng nhập tạo yêu cầu giữ chỗ trực tiếp hoặc từ lịch xem **của mình, cùng phòng**. Yêu cầu mới không xác nhận tiền. Nhân viên duyệt **số tiền giữ chỗ, hạn thanh toán và hạn ký hợp đồng**; không dùng mặc định 50% tiền thuê trong SPEC cũ. Unique index hiện có chỉ cho một yêu cầu `ChoThanhToan`, `ChoXacNhanTien` hoặc `DangGiuCho` mỗi phòng. Xung đột phải trả lỗi rõ ràng.
3. Trang khách hiển thị trạng thái, số tiền, hai hạn và lịch sử. Khách hủy giữ chỗ **chưa nhận tiền** thì nhả phòng ngay; nếu đã nhận tiền, hủy chuyển nhân viên quyết định hoàn, không tự sinh giao dịch hoàn. Hết hạn chuyển tiền mà chưa có minh chứng thì nhả phòng; minh chứng gửi đúng hạn chờ nhân viên đối soát. Tiền đến sau khi phòng đã nhả **phải hoàn toàn bộ**, không tự kích hoạt lại giữ chỗ. Quá hạn ký hợp đồng khi đã nhận tiền thì nhân viên quyết định gia hạn hoặc hủy; nếu hủy thì xử lý hoàn.

## Chuyển tiền, cọc hợp đồng và hoàn

1. Sau duyệt, tạo yêu cầu thanh toán có mã duy nhất, số tiền, nội dung chuyển khoản và hạn. Tài khoản nhận tiền lấy từ cấu hình bảo mật, không ghi cứng trong Git. Ảnh minh chứng được kiểm tra loại, kích thước, nội dung ở server, lưu riêng tư; ảnh chỉ tạo trạng thái chờ đối soát.
2. Nhân viên đối chiếu ngân hàng rồi mới ghi số **tiền thực nhận**, thời điểm thực vào tài khoản theo sao kê, mã giao dịch duy nhất và người xác nhận. Thời điểm khách gửi ảnh không quyết định giao dịch đúng hạn. Cho phép nhiều giao dịch cộng dồn để đủ khoản được duyệt. Gửi ảnh hoặc bấm xác nhận lặp không được ghi khoản thu thứ hai. Trạng thái `DangGiuCho` chỉ đạt khi tổng tiền xác nhận đủ mức đã duyệt; khoản tiền đến muộn được ghi nhận để hoàn, không mở lại quyền giữ phòng.
3. Khi có hợp đồng đúng phòng/khách, tiền đã xác nhận được **trừ vào tiền cọc hợp đồng**, không trừ vào hóa đơn tiền thuê. Dùng bảng `ap_dung_tien_giu_cho_vao_tien_coc`; một yêu cầu chỉ áp dụng một lần. Nếu chuyển thừa, áp dụng tới mức cọc hợp đồng còn phải thu, **phần còn lại phải hoàn**. Nếu không ký hợp đồng, phần vượt số tiền giữ chỗ đã duyệt cũng **phải hoàn**; khoản đúng mức duyệt do nhân viên quyết định. Không được áp dụng vượt tiền cọc cần thu hoặc tiền thực nhận còn khả dụng.
4. Nhân viên quyết định mức và lý do hoàn; quyết định hoàn khác với giao dịch chuyển hoàn thực tế. Cho nhiều lần chuyển hoàn trong giới hạn quyết định. Tổng tiền áp dụng và tiền hoàn không vượt tiền thực nhận, kể cả khi thao tác đồng thời. Khách thấy riêng trạng thái minh chứng, tiền đã đối soát, duyệt hoàn và hoàn thực tế.

## Quyền, kiểm chứng và giao diện

- GET công khai chỉ trả dữ liệu được phép đăng. Mọi POST dùng antiforgery và kiểm tra role, chủ yêu cầu, quyền chi nhánh ở server; không tin ID/phòng/giá từ form. Ảnh minh chứng không được để chung gallery công khai.
- Dùng PostgreSQL `_test` hoặc `_integration_test` cho test EF/HTTP theo `AGENTS.md`; không chạy fixtures trên `QuanLyPhongTroDb`.
- Kiểm thử role khách, tranh chấp suất cuối, hai nhân viên duyệt cùng phòng, file sai loại, mã giao dịch trùng, hủy sau khi chuyển tiền, áp dụng/hoàn đồng thời và truy cập chéo tài khoản/chi nhánh.
- Giữ kiểu chữ, màu và bố cục `/phong` đang có. Khi chức năng thực hoạt động, nút gửi/duyệt tương ứng được mở và báo kết quả từ dữ liệu thật; không báo đã đặt cọc chỉ vì có ảnh minh chứng.
- Mỗi thẻ phòng có nút **Hẹn xem phòng** và **Đặt phòng**. Khách chưa đăng nhập được đưa tới trang đăng nhập, sau đó quay lại đúng đường dẫn đã chọn (kể cả tham số lịch xem); đăng ký từ trang đăng nhập cũng giữ đường dẫn này.
- Mục **Lịch xem và đặt phòng** có nút hủy lịch trước giờ hẹn và đường dẫn tới trang đặt cọc riêng của từng yêu cầu. Trang đặt cọc hiển thị số tiền được duyệt, tiền đã xác nhận, hạn, thông tin chuyển khoản, form minh chứng và kết quả xử lý; chỉ chủ yêu cầu được truy cập.

## Logic còn cần chủ dự án trả lời

1. Chính sách quên mật khẩu/đổi email của tài khoản khách chưa được yêu cầu trong phạm vi tính năng này; cần chốt nếu muốn mở thêm chức năng tự phục vụ.

**Đã xác nhận:** phải đăng nhập để đặt lịch; email lịch xem bắt buộc; khung còn chỗ xác nhận ngay, giờ đề xuất chờ nhân viên; nhân viên duyệt số tiền và hạn; nhân viên đối soát tiền; mức hoàn do nhân viên quyết định với khoản đúng mức giữ chỗ; gửi email thủ công sau khi xem trước, gồm cả giữ chỗ đã duyệt nhưng chưa trả đủ; tiền giữ chỗ áp dụng vào **cọc hợp đồng**; cho nhiều giao dịch cộng dồn, bắt buộc hoàn phần dư/tiền đến muộn; hủy lịch trước giờ bắt đầu; quá hạn ký do nhân viên xử lý; CCCD khi lập hợp đồng.
