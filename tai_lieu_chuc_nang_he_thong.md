# TÀI LIỆU CHỨC NĂNG VÀ CÁCH THỨC HOẠT ĐỘNG HỆ THỐNG
## DỰ ÁN QUẢN LÝ CHO THUÊ PHÒNG TRỌ (RENTAL MANAGEMENT SYSTEM)

Tài liệu này mô tả chi tiết tất cả các chức năng nghiệp vụ của hệ thống **Quản lý cho thuê phòng trọ** cùng với cách thức vận hành và vị trí xuất hiện của từng chức năng trên giao diện khi người dùng mở trang web.

> **Đối tượng đọc**: Lập trình viên mới, QA tester, nhân viên nghiệp vụ muốn hiểu hệ thống hoạt động.
> **Tham chiếu kỹ thuật**: Xem thêm `HuongDanToanDienDuAn.md` để biết chi tiết kiến trúc và quy chuẩn code.

---

### 1. QUẢN LÝ TÀI KHOẢN & PHÂN QUYỀN (AUTHENTICATION & AUTHORIZATION)

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Đăng nhập**: Xuất hiện khi người dùng mở trang web mà chưa được xác thực (chưa đăng nhập), hoặc khi nhấn vào nút đăng xuất.
*   **Trang Quản lý Tài khoản**: Xuất hiện khi người dùng truy cập từ thanh Menu bên trái: **Quản lý người dùng** -> **Quản lý tài khoản**.

#### B. Danh sách chức năng:
*   **Đăng nhập hệ thống**: Xác thực tài khoản người dùng (Admin hoặc Staff) để cấp quyền truy cập.
*   **Đăng xuất hệ thống**: Hủy phiên làm việc hiện tại, xóa thông tin định danh trên thiết bị.
*   **Quản lý tài khoản nội bộ (CRUD)**: Cho phép Admin tạo mới, sửa đổi phân quyền, cập nhật trạng thái hoạt động hoặc xóa tài khoản nhân viên.
*   **Khởi tạo dữ liệu mẫu (Seeding)**: Tự động tạo tài khoản quản trị tối cao ban đầu nếu cơ sở dữ liệu chưa có dữ liệu. Tài khoản mặc định: `admin` / `admin123`.

#### C. Cách thức hoạt động chi tiết:
1.  **Xác thực Cookie**: Hệ thống sử dụng cơ chế xác thực dựa trên Cookie của ASP.NET Core (`CookieAuthenticationDefaults`). Khi đăng nhập thành công, một Cookie chứa các thông tin định danh (`ClaimsIdentity`) như ID người dùng, tên đăng nhập và vai trò (Role) sẽ được ghi xuống trình duyệt của người dùng với thời hạn tối đa 30 ngày (nếu chọn ghi nhớ).
2.  **Mã hóa bảo mật**: Mật khẩu người dùng nhập vào được mã hóa một chiều sử dụng thuật toán băm **SHA256** (hàm `BamMatKhauSHA256` trong Controller) trước khi lưu vào DB hoặc đem so khớp. Hệ thống không bao giờ lưu trữ mật khẩu dưới dạng văn bản thuần (plain text).
3.  **Tự động tạo Admin**: Khi người dùng truy cập trang Đăng nhập (`/QuanLyNhaTro/DangNhap`), hàm `SeedAdminAccountAsync()` trong `NguoiDungService` sẽ kiểm tra số lượng bản ghi trong bảng `NguoiDungs`. Nếu bằng 0, hệ thống tự động chèn một tài khoản quản trị mặc định: Tên đăng nhập `admin` / Mật khẩu `admin123` (đã được băm SHA256) để người dùng có tài khoản đăng nhập ban đầu.
4.  **Kiểm soát truy cập**: Các Controller nghiệp vụ được bảo vệ bằng thuộc tính đầu lọc `[Authorize]`. Khi người dùng chưa đăng nhập, hệ thống tự động chuyển hướng về trang `/QuanLyNhaTro/DangNhap`.

---

### 2. BẢNG ĐIỀU KHIỂN (DASHBOARD)

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Bảng điều khiển chính**: Xuất hiện ngay khi người dùng đăng nhập thành công vào hệ thống, hoặc khi người dùng truy cập từ thanh Menu: **Dashboard**.

#### B. Danh sách chức năng:
*   **Thống kê chỉ số nhanh (KPIs)**: Đếm số lượng phòng theo trạng thái (Trống, Đang thuê, Bảo trì), tỷ lệ lấp đầy, số hợp đồng sắp hết hạn trong 30 ngày, doanh thu thực tế và nợ hóa đơn trong tháng.
*   **Biểu đồ cột chồng doanh thu**: Thể hiện tỷ lệ tiền phòng đã thu so với tiền phòng chờ thu trong 12 tháng của năm được chọn.
*   **Biểu đồ tròn phương thức thanh toán**: Thống kê tỷ trọng tiền thu được qua Tiền mặt so với Chuyển khoản trong tháng được chọn.
*   **Danh sách việc cần làm (To-Dos)**: Cảnh báo tự động các phòng chưa chốt điện nước, các hợp đồng chuẩn bị hết hiệu lực và các hóa đơn của các tháng trước vẫn còn nợ tiền.
*   **Dòng thời gian hoạt động (Timeline)**: Hiển thị danh sách 5 giao dịch thanh toán hoặc hợp đồng mới phát sinh gần nhất.

#### C. Cách thức hoạt động chi tiết:
1.  **Tải dữ liệu bất đồng bộ (AJAX)**: Khi truy cập giao diện dashboard, hệ thống tải dữ liệu mặc định của Chi nhánh đầu tiên và tháng/năm hiện tại. Khi người dùng thay đổi bộ lọc Chi nhánh, Tháng hoặc Năm trên dropdown, một lệnh gọi AJAX sẽ được gửi tới API Endpoint `/QuanLyNhaTro/Dashboard/GetAjaxData`. API này gọi hàm `GetDashboardDataAsync` truy vấn dữ liệu từ PostgreSQL và trả về định dạng JSON, sau đó JavaScript sẽ vẽ lại biểu đồ Chart.js và cập nhật các con số thống kê mà không cần reload trang.
2.  **Tính toán To-Dos động**:
    *   *Chưa chốt điện nước*: Lấy danh sách ID phòng có hợp đồng đang hoạt động trong tháng trừ đi (Except) danh sách ID phòng đã được chốt chỉ số trong bảng `DichVuDienNuocCuaPhongs`. Nếu kết quả > 0, lập tức hiển thị cảnh báo.
    *   *Hợp đồng sắp hết hạn*: Truy vấn trong bảng `HopDongs` các hợp đồng có trạng thái đang hoạt động và có ngày kết thúc thỏa mãn điều kiện `NgayKetThuc <= Ngày hiện tại + 30 ngày`.
    *   *Hóa đơn quá hạn*: Tìm trong bảng `HoaDons` các hóa đơn chưa thanh toán mà có thời gian kỳ hóa đơn nhỏ hơn tháng/năm đang được chọn.

---

### 3. QUẢN LÝ CHI NHÁNH (BRANCHES)

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Quản lý Chi nhánh**: Xuất hiện khi người dùng truy cập từ thanh Menu: **Quản lý nhà trọ** -> **Chi nhánh**.

#### B. Danh sách chức năng:
*   **Hiển thị danh sách chi nhánh**: Xem danh sách các tòa nhà/cơ sở nhà trọ đang hoạt động.
*   **Thêm mới / Cập nhật chi nhánh**: Quản lý các thông tin: Tên chi nhánh, Mã chi nhánh, Địa chỉ, Số điện thoại, Ngày tạo.
*   **Xóa mềm chi nhánh**: Ngưng hoạt động chi nhánh mà không làm mất lịch sử liên kết dữ liệu cũ.

#### C. Cách thức hoạt động chi tiết:
1.  **Phân trang phía Server**: Sử dụng thư viện jQuery DataTables kết nối trực tiếp với API `/ChiNhanhs/DanhSachChiNhanh`. Dữ liệu trả về chỉ bao gồm danh sách các chi nhánh có thuộc tính `IsDeleted == false`.
2.  **Sinh mã tự động**: Khi thêm mới chi nhánh, hệ thống tự động sinh Mã chi nhánh dựa trên tên chi nhánh hoặc quy tắc mã tăng dần và thiết lập giá trị duy nhất (Unique Index trên cột `MaChiNhanh` trong DB) để phục vụ việc định danh.
3.  **Xóa mềm (Soft Delete)**: Khi bấm nút xóa chi nhánh, SweetAlert2 hiển thị popup xác nhận. Nếu người dùng đồng ý, AJAX gửi yêu cầu lên `XoaChiNhanh(id)`. Hàm service sẽ cập nhật thuộc tính `IsDeleted = true`. Mọi liên kết phòng trọ hay hóa đơn cũ của chi nhánh này vẫn được giữ nguyên trong cơ sở dữ liệu để phục vụ báo cáo tài chính lịch sử, nhưng chi nhánh sẽ không còn hiển thị ở bất kỳ danh sách hay bộ lọc nào trên giao diện nữa.

---

### 4. QUẢN LÝ PHÒNG TRỌ & SƠ ĐỒ PHÒNG

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Quản lý Phòng trọ**: Xuất hiện khi người dùng truy cập từ thanh Menu: **Quản lý nhà trọ** -> **Phòng trọ**.
*   **Trang Sơ đồ trạng thái phòng**: Xuất hiện khi người dùng nhấn vào nút **Sơ đồ phòng** (hoặc biểu tượng sơ đồ nhà trọ góc trên).

#### B. Danh sách chức năng:
*   **Quản lý danh sách phòng (CRUD)**: Quản lý thông tin số phòng, tầng lầu, diện tích, giá thuê gốc, số người ở tối đa và trạng thái của phòng.
*   **Sơ đồ trạng thái phòng**: Hiển thị danh sách phòng trực quan dưới dạng ô lưới màu sắc (Màu xanh: Phòng trống, Màu đỏ: Phòng đã thuê có hợp đồng, Màu xám/vàng: Phòng đang bảo trì).
*   **Xem nhanh hợp đồng (Quick View Contract)**: Xem trực tiếp thông tin hợp đồng, người đại diện, thành viên ở ghép và dịch vụ đăng ký khi click vào phòng đã thuê trên sơ đồ phòng.
*   **Xem nhanh hóa đơn chưa thanh toán**: Xem chi tiết hóa đơn nợ của phòng và thực hiện xác nhận đóng tiền trực tiếp trên sơ đồ phòng.
*   **Đăng ký dịch vụ cho phòng**: Gán danh mục dịch vụ và số lượng sử dụng cho từng phòng cụ thể.
*   **Phát sinh phòng mẫu ngẫu nhiên**: Tự động sinh nhanh 10 phòng mẫu cho các chi nhánh để kiểm thử giao diện.

#### C. Cách thức hoạt động chi tiết:
1.  **Vẽ sơ đồ phòng động**: Khi truy cập trang Sơ đồ phòng, JavaScript gọi AJAX `/PhongTros/GetSoDoPhong?chiNhanhId=x`. Service truy vấn bảng `PhongTros` lọc theo chi nhánh, đồng thời liên kết (`Include`) bảng `HopDongs` để lấy tên khách thuê đại diện hiện tại. Sau đó, giao diện render các ô card đại diện cho từng phòng, phân nhóm theo Tầng lầu (TangLau).
2.  **Popup chi tiết nhanh (Quick View)**:
    *   Khi click vào ô phòng đang thuê, JavaScript bắt sự kiện và gọi API `/PhongTros/GetQuickContract?phongTroId=x`. Backend sẽ truy vấn bảng `HopDongs` tìm hợp đồng đang hoạt động của phòng đó, đồng thời truy vấn bảng `ChiTietThanhVienHopDongs` để lấy danh sách thành viên ở ghép và bảng `DangKyDichVus` để lấy danh sách dịch vụ đăng ký đi kèm. Toàn bộ dữ liệu được gom lại và trả về hiển thị trên một Modal popup mà không làm tải lại trang sơ đồ.
    *   Tương tự, click vào phòng có hóa đơn chưa thanh toán sẽ gọi `/PhongTros/GetUnpaidInvoice?phongTroId=x` để lấy hóa đơn chưa thanh toán của phòng đó kèm chi tiết tiền điện, tiền nước và tiền dịch vụ để người dùng xác nhận thu tiền nhanh.
3.  **Lưu thông tin đăng ký dịch vụ**: Tại trang Quản lý phòng trọ, khi chọn tính năng "Đăng ký dịch vụ", hệ thống gọi `/DangKyDichVu/GetByPhong?phongTroId=x` lấy danh sách dịch vụ được phép đăng ký tại chi nhánh của phòng đó. Khi người dùng nhấn lưu, API `/DangKyDichVu/Luu` sẽ xóa toàn bộ danh mục đăng ký cũ của phòng đó trong bảng `DangKyDichVus` và chèn lại các dịch vụ được chọn mới nhằm tối ưu hóa câu lệnh lưu trữ.

---

### 5. QUẢN LÝ NGƯỜI THUÊ (TENANTS)

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Quản lý Người thuê**: Xuất hiện khi người dùng truy cập từ thanh Menu: **Quản lý nhà trọ** -> **Người thuê**.

#### B. Danh sách chức năng:
*   **Quản lý hồ sơ khách thuê (CRUD)**: Lưu trữ và chỉnh sửa Họ tên, Số điện thoại, Email, Số CCCD, Quê quán của khách hàng.
*   **Tìm kiếm tự động gợi ý (Autocomplete)**: Hỗ trợ tìm nhanh khách thuê khi nhập liệu tạo hợp đồng.
*   **Phát sinh khách thuê ngẫu nhiên**: Tạo nhanh 10 khách hàng mẫu ngẫu nhiên có đầy đủ thông tin định danh mẫu.

#### C. Cách thức hoạt động chi tiết:
1.  **Ràng buộc nghiệp vụ**: Khi thêm mới khách thuê qua API `/NguoiThue/Create`, service thực hiện kiểm tra kiểm trùng dữ liệu trên 3 cột: `CCCD`, `SoDienThoai`, `Email` đối với các bản ghi chưa bị xóa. Nếu phát hiện trùng lặp, hệ thống lập tức hủy thao tác ghi và trả về mã lỗi 400 kèm thông điệp cảnh báo chi tiết.
2.  **Autocomplete Select2**: Khi lập hợp đồng mới, tại ô nhập thông tin Khách thuê đại diện, hệ thống tích hợp Select2 gọi AJAX về `/NguoiThue/SearchAutocomplete`. Khi người dùng gõ từ khóa, SQL sinh câu lệnh truy vấn dạng `LIKE` tìm kiếm trên các trường `HoVaTen`, `SoDienThoai` hoặc `CCCD` và trả về danh sách tối đa 10 gợi ý khớp nhất để điền tự động thông tin.

---

### 6. QUẢN LÝ HỢP ĐỒNG & THÀNH VIÊN Ở GHÉP (CONTRACTS & MEMBERS)

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Quản lý Hợp đồng**: Xuất hiện khi người dùng truy cập từ thanh Menu: **Quản lý nhà trọ** -> **Hợp đồng**.
*   **Popup Thành viên ở ghép**: Xuất hiện dưới dạng Modal popup nổi lên ngay trên trang Quản lý Hợp đồng khi nhân viên bấm vào nút "Thành viên" của một hợp đồng bất kỳ.

#### B. Danh sách chức năng:
*   **Lập hợp đồng thuê phòng**: Thiết lập ngày bắt đầu/kết thúc thuê, số tiền đặt cọc phòng, giá thuê thỏa thuận hàng tháng và gán người thuê đại diện.
*   **Cập nhật hợp đồng**: Điều chỉnh thời hạn hoặc giá trị thỏa thuận thuê phòng.
*   **Xóa / Chấm dứt hợp đồng**: Thanh lý hợp đồng và giải phóng phòng về trạng thái trống.
*   **Quản lý thành viên ở ghép**: Thêm thành viên ở cùng phòng, báo rời phòng (giảm số người ở), và xóa thành viên nhập nhầm.

#### C. Cách thức hoạt động chi tiết:
1.  **Quy trình lập hợp đồng (`CreateAsync`)**:
    *   Hệ thống kiểm tra trạng thái phòng trọ được chọn. Nếu phòng không ở trạng thái "Trống" (`Trong`), hệ thống báo lỗi không cho phép lập hợp đồng.
    *   Lưu thông tin hợp đồng vào bảng `HopDongs`, sinh mã hợp đồng tự động theo định dạng độc bản.
    *   Cập nhật trạng thái của phòng trọ sang "Đã thuê" (`DaThue`).
    *   Tự động đăng ký các dịch vụ bắt buộc (như Điện, Nước) vào bảng `DangKyDichVus` cho phòng này để đảm bảo kỳ chốt số điện nước hàng tháng hoạt động bình thường.
2.  **Chấm dứt/Xóa hợp đồng**: Khi hợp đồng bị xóa hoặc hết hạn thanh lý, thuộc tính `IsDeleted` của hợp đồng được cập nhật thành `true` (hoặc chuyển trạng thái sang `DaKetThuc`). Đồng thời, trạng thái của phòng trọ liên kết được tự động chuyển về "Trống" (`Trong`) để sẵn sàng cho khách thuê tiếp theo.
3.  **Ràng buộc sức chứa thành viên**: Khi thêm thành viên ở ghép thông qua API `/ThanhVienHopDong/AddThanhVien`, hệ thống đếm số lượng thành viên đang ở thực tế trong phòng (các bản ghi trong bảng `ChiTietThanhVienHopDongs` có ngày vào ở và trường `NgayChuyenDi == null`) cộng thêm người đại diện ký hợp đồng, sau đó so sánh với trường `SoNguoiToiDa` cấu hình tại bảng `PhongTros`. Nếu vượt quá sức chứa tối đa của phòng, hệ thống sẽ từ chối thêm mới và báo lỗi.
4.  **Báo rời phòng (`BaoRoiPhongAsync`)**: Khi một thành viên ở ghép dọn đi trước khi hợp đồng kết thúc, nhân viên nhấn nút "Báo rời phòng". Hệ thống cập nhật cột `NgayChuyenDi` của bản ghi đó thành ngày hiện tại để ghi nhận lịch sử tạm trú, đồng thời giảm số người ở thực tế của phòng đó xuống phục vụ việc tính toán các dịch vụ tính theo đầu người (nếu có).

---

### 7. QUẢN LÝ DỊCH VỤ & ĐĂNG KÝ DỊCH VỤ

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Quản lý Dịch vụ**: Xuất hiện khi người dùng truy cập từ thanh Menu: **Quản lý nhà trọ** -> **Dịch vụ**.
*   **Popup Đăng ký dịch vụ cho phòng**: Xuất hiện dưới dạng Modal popup khi người dùng đang ở giao diện **Trang Phòng trọ** và nhấp vào biểu tượng "Dịch vụ" của một phòng trọ cụ thể.

#### B. Danh sách chức năng:
*   **Danh mục dịch vụ hệ thống**: Định nghĩa các dịch vụ dùng chung (Điện, Nước, Internet, Gửi xe, Rác, Vệ sinh...) và đơn vị tính của chúng.
*   **Bảng giá dịch vụ theo chi nhánh**: Cấu hình đơn giá riêng biệt cho từng dịch vụ tại mỗi chi nhánh khác nhau.
*   **Đăng ký dịch vụ cho phòng**: Quy định chi tiết phòng trọ đó sử dụng những dịch vụ nào và số lượng đăng ký là bao nhiêu (ví dụ: đăng ký 2 xe máy, 1 đường truyền Internet).

#### C. Cách thức hoạt động chi tiết:
1.  **Cấu hình giá linh hoạt**: Bảng `DichVuChiNhanh` liên kết giữa dịch vụ hệ thống và chi nhánh. Để đảm bảo dữ liệu nhất quán, DB thiết lập một Unique Index trên cặp khóa `(ChiNhanhId, DichVuId)`. Hệ thống ngăn chặn việc cấu hình đơn giá của một dịch vụ 2 lần tại cùng một chi nhánh.
2.  **Kế thừa đơn giá khi tính hóa đơn**: Khi tính toán hóa đơn cho một phòng, hệ thống tìm các dịch vụ phòng đó đăng ký trong bảng `DangKyDichVus`, sau đó dò tìm đơn giá tương ứng của dịch vụ đó tại bảng giá `DichVuChiNhanh` của chi nhánh quản lý phòng đó để áp giá tính tiền.

---

### 8. QUẢN LÝ CHỈ SỐ ĐIỆN NƯỚC (UTILITIES)

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Chốt số Điện nước**: Xuất hiện khi người dùng truy cập từ thanh Menu: **Quản lý nhà trọ** -> **Điện nước**.

#### B. Danh sách chức năng:
*   **Tải danh sách nhập số nhanh**: Hiển thị bảng danh sách tất cả các phòng đang có hợp đồng hoạt động của chi nhánh để nhập chỉ số hàng tháng.
*   **Chốt chỉ số điện nước**: Ghi nhận chỉ số điện mới và chỉ số nước mới của tháng.
*   **Tự động kế thừa chỉ số cũ**: Tự động lấy chỉ số mới chốt của tháng trước để điền vào chỉ số cũ của tháng này.
*   **Khóa dữ liệu**: Khóa chỉ số không cho phép chỉnh sửa nếu hóa đơn tháng đó đã được phát sinh.

#### C. Cách thức hoạt động chi tiết:
1.  **Kế thừa số cũ thông minh**: Khi nhân viên chọn Chi nhánh, Tháng `T`, Năm `N` và bấm lọc dữ liệu, API `/DienNuoc/GetDanhSachPhongs` được gọi. Đối với mỗi phòng trọ:
    *   Hệ thống kiểm tra xem đã có bản ghi chốt số của tháng `T` năm `N` trong bảng `DichVuDienNuocCuaPhongs` chưa.
    *   Nếu chưa có, hệ thống truy vấn chỉ số điện/nước mới chốt của tháng liền trước (`T-1` năm `N`, hoặc tháng 12 năm `N-1` nếu `T = 1`). Chỉ số mới này được gán làm chỉ số cũ của tháng hiện tại.
    *   Nếu là tháng đầu tiên thuê phòng (chưa từng chốt số trước đó), hệ thống lấy chỉ số khởi tạo mặc định bằng 0 hoặc chỉ số bàn giao ghi trên hợp đồng.
2.  **Khóa chỉ số bảo vệ số liệu (`IsLocked`)**: Khi hóa đơn tiền phòng của tháng đó đã được tạo thành công, trường `IsLocked` của bản ghi chỉ số điện nước tương ứng sẽ được cập nhật thành `true`. Khi đó, nếu người dùng cố ý gọi API sửa đổi chỉ số qua `/DienNuoc/SaveChotDienNuoc`, hệ thống sẽ trả về mã lỗi chặn không cho sửa, ngăn ngừa việc thay đổi chỉ số đầu vào làm sai lệch số tiền hóa đơn đã xuất.

---

### 9. QUẢN LÝ HÓA ĐƠN (INVOICES)

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Quản lý Hóa đơn**: Xuất hiện khi người dùng truy cập từ thanh Menu: **Quản lý nhà trọ** -> **Hóa đơn**. *Tất cả popup xem trước hóa đơn, thanh toán thu tiền, xuất QR chuyển khoản hay tải báo cáo PDF/Excel và gửi email đều nằm tích hợp tại trang này*.
*   **Popup Thanh toán nhanh trên sơ đồ phòng**: Xuất hiện khi người dùng đang xem trang **Sơ đồ phòng**, click vào một phòng đang nợ hóa đơn và bấm chọn "Thanh toán hóa đơn".

#### B. Danh sách chức năng:
*   **Xem trước hóa đơn (Preview)**: Hiển thị bảng kê chi tiết các khoản phí dự kiến thu của từng phòng để kiểm tra trước khi phát sinh chính thức.
*   **Phát sinh hóa đơn**: Tạo hóa đơn hàng loạt cho toàn bộ chi nhánh hoặc phát sinh lẻ cho từng phòng trọ được chọn.
*   **Xác nhận thanh toán (Thu tiền)**: Ghi nhận đóng tiền và chuyển trạng thái hóa đơn sang "Đã thanh toán".
*   **Sinh mã VietQR chuyển khoản động**: Tạo mã QR ngân hàng chứa số tài khoản, số tiền và nội dung chuyển khoản động chứa mã hóa đơn.
*   **Xuất tệp tin PDF và Excel**: Tải hóa đơn chi tiết dạng file Excel hoặc file PDF để gửi/in cho khách.
*   **Gửi Email hóa đơn tự động (Đơn lẻ & Hàng loạt)**: Gửi trực tiếp hóa đơn dạng bảng HTML đính kèm tệp PDF vào hòm thư điện tử của khách thuê.
*   **Bộ lọc và Gửi email hàng loạt qua Modal**: Tự động lọc ra toàn bộ hóa đơn chưa thanh toán của chi nhánh và kỳ đã chọn để gửi email nhắc nợ hàng loạt trong một giao diện Modal riêng biệt.
*   **Xóa hóa đơn**: Xóa các hóa đơn chưa thanh toán bị lập sai.

#### C. Cách thức hoạt động chi tiết:
1.  **Công thức tính toán tiền hóa đơn**:
    *   *Tiền phòng*: Lấy giá thuê phòng thỏa thuận trong hợp đồng.
    *   *Tiền điện*: Lấy `(Chỉ số điện mới - Chỉ số điện cũ) × Đơn giá điện của chi nhánh`.
    *   *Tiền nước*: Lấy `(Chỉ số nước mới - Chỉ số nước cũ) × Đơn giá nước của chi nhánh`.
    *   *Tiền dịch vụ khác*: Lấy `Số lượng đăng ký × Đơn giá dịch vụ tương ứng`.
    *   *Tổng tiền hóa đơn*: Bằng tổng cộng tất cả các khoản chi phí trên.
2.  **Quy trình Phát sinh hóa đơn (`PhatSinhHoaDonAsync`)**:
    *   Hệ thống kiểm tra xem các phòng được chọn đã chốt chỉ số điện nước của tháng/năm đó chưa. Nếu chưa chốt, hệ thống sẽ từ chối tạo hóa đơn và yêu cầu đi chốt số trước.
    *   Tạo bản ghi `HoaDon` chính thức, tự động sinh mã hóa đơn duy nhất dạng `HD-yyyyMM-id`.
    *   Tạo các bản ghi chi tiết chi phí tương ứng trong bảng `ChiTietHoaDons` để lưu lại vết đơn giá và số lượng tại thời điểm chốt (tránh việc thay đổi bảng giá chi nhánh sau này làm ảnh hưởng đến hóa đơn cũ).
    *   Cập nhật trạng thái chỉ số điện nước sang trạng thái khóa (`IsLocked = true`).
3.  **Tạo mã VietQR động ngoại tuyến**:
    *   Khi người dùng bấm "Xem mã QR" trên giao diện hóa đơn chưa thanh toán, hệ thống gọi API `/HoaDon/GetVietQR?hoaDonId=x`.
    *   `VietQRHelper.cs` đọc thông tin tài khoản ngân hàng thụ hưởng cấu hình tại tệp `appsettings.json`, cộng với tổng số tiền hóa đơn và nội dung chuyển khoản chuẩn hóa không dấu: `THANH TOAN [MaHoaDon]`.
    *   Helper thực hiện mã hóa thông tin theo chuẩn EMVCo và tính toán mã kiểm tra CRC16 để sinh chuỗi VietQR thô.
    *   Sử dụng thư viện `QRCoder` kết xuất chuỗi này thành định dạng ảnh PNG lưu trong bộ nhớ đệm RAM và truyền thẳng luồng byte ảnh về thẻ `<img>` trên giao diện người dùng. Việc này diễn ra hoàn toàn offline trên server, đảm bảo tính bảo mật và tốc độ tối đa.
4.  **Xuất bản tài liệu chất lượng cao**:
    *   *Excel*: Thư viện `ClosedXML` tạo một Workbook mới, vẽ bảng kê chi tiết hóa đơn, kẻ bảng, tô màu tiêu đề và định dạng số tiền tệ, trả về luồng byte tải về của trình duyệt.
    *   *PDF*: Thư viện `QuestPDF` vẽ bố cục hóa đơn A4 sắc nét gồm logo, thông tin chi nhánh, thông tin khách thuê, bảng chi phí chi tiết, và chữ ký người lập phiếu, trả về file PDF chuẩn.
5.  **Gửi Email tự động, Gửi hàng loạt qua Modal & Tự động gửi nhắc nợ**:
    *   *Gửi đơn lẻ*: Khi Admin bấm "Gửi Email" trên bảng thao tác, hệ thống sinh PDF hóa đơn dưới dạng mảng byte trong bộ nhớ RAM, gọi `EmailService.cs` khởi tạo kết nối SMTP (TLS) tới máy chủ thư điện tử (cấu hình trong `appsettings.json`) để gửi đính kèm file PDF hóa đơn và nội dung HTML tóm tắt đến email khách thuê phòng.
    *   *Gửi hàng loạt*: Bấm nút **"Gửi Email Hóa Đơn"** ở thanh công cụ chính, Modal gửi email mở ra hỗ trợ bộ lọc riêng biệt (Chi nhánh, Tháng, Năm). Hệ thống gọi API `/HoaDon/GetUnpaidList` để lấy toàn bộ các hóa đơn chưa thanh toán, hỗ trợ tích chọn hàng loạt. Khi bấm bắt đầu gửi, Javascript chạy vòng lặp tuần tự gọi API gửi email cho từng hóa đơn, đồng thời cập nhật thanh tiến trình (Progress Bar) động qua SweetAlert2 để đảm bảo UX trực quan và mượt mà.
    *   *Tự động quét và gửi nhắc nợ (Auto-scheduler Background Service)*: Hệ thống tích hợp một tác vụ chạy nền định kỳ mỗi giờ (`InvoiceReminderService` kế thừa từ `BackgroundService`). Cứ mỗi giờ, hệ thống quét các hóa đơn chưa thanh toán có ngày tạo quá 5 ngày (`NgayTao <= now - 5 days`) và tự động gửi email nhắc nợ kèm PDF hóa đơn. Tiến trình này ghi nhận lịch sử vào tệp `sent_reminders.json` để ngăn chặn gửi lặp lại và ghi nhật ký trực tiếp ra console, đảm bảo vận hành ổn định và độc lập không tốn chi phí.
    *   *Cá nhân hóa chữ ký chi nhánh (Friendly Signature)*: Các email gửi đi (bao gồm cả gửi thủ công và tự động) được tích hợp chữ ký động. Phần chân trang (Footer) hiển thị lời chúc trân trọng và thông tin liên hệ cụ thể của chi nhánh như Tên chi nhánh, Địa chỉ chi nhánh, và Hotline hỗ trợ lấy động từ cơ sở dữ liệu.
    *   *Tương thích giao diện tối (Dark Mode)*: Email được thiết kế responsive với mã CSS thích ứng tự động (`prefers-color-scheme: dark`) giúp tự động chuyển tông màu nền, màu chữ và màu chữ ký chi nhánh dịu mắt khi khách thuê bật chế độ tối trên thiết bị. Mã QR Code VietQR được bọc trong viền trắng cố định giúp camera quét chuyển khoản nhanh và chính xác nhất.

---

### 10. QUẢN LÝ LỊCH SỬ THANH TOÁN & GIAO DỊCH (PAYMENTS)

#### A. Các giao diện xuất hiện trên Website:
*   **Trang Lịch sử Thanh toán**: Xuất hiện khi người dùng truy cập từ thanh Menu: **Quản lý nhà trọ** -> **Lịch sử thanh toán**.

#### B. Danh sách chức năng:
*   **Ghi nhận giao dịch thanh toán**: Ghi nhận chi tiết số tiền đóng, ngày đóng, phương thức (Tiền mặt / Chuyển khoản) và ghi chú giao dịch.
*   **Hiển thị danh sách giao dịch**: Quản lý và tìm kiếm lịch sử thu tiền của toàn bộ hệ thống, lọc theo khoảng thời gian và chi nhánh.
*   **Thống kê tổng tiền thu**: Tính tổng tiền thu về theo phương thức thanh toán tiền mặt và chuyển khoản để đối soát kế toán.
*   **Hủy giao dịch thanh toán (Hoàn tác)**: Hủy giao dịch thanh toán bị lỗi hoặc ghi nhận nhầm lẫn của nhân viên thu ngân.

#### C. Cách thức hoạt động chi tiết:
1.  **Ghi nhận giao dịch**: Khi hóa đơn được thanh toán thành công (dù qua thu tiền mặt hay chuyển khoản quét QR), hệ thống chèn một dòng giao dịch vào bảng `LichSuThanhToans`. Bản ghi lưu trữ số tiền thực đóng, phương thức thanh toán, thời gian giao dịch thực tế và ID của nhân viên xác nhận giao dịch.
2.  **Quy trình Hủy giao dịch (Hoàn tác thanh toán - `HuyGiaoDichAsync`)**:
    *   Khi nhân viên bấm hủy giao dịch trên giao diện lịch sử thanh toán, hệ thống yêu cầu xác nhận. Nếu đồng ý, AJAX gửi yêu cầu lên API `/LichSuThanhToan/HuyThanhToan/{id}`.
    *   Backend tìm bản ghi giao dịch tương ứng trong bảng `LichSuThanhToans`.
    *   Thiết lập trạng thái xóa mềm của giao dịch: `IsDeleted = true`.
    *   Tìm hóa đơn liên kết với giao dịch này. Khôi phục trạng thái của hóa đơn về **Chưa thanh toán** (`TrangThaiHoaDon = ChuaThanhToan`), cập nhật ngày thanh toán của hóa đơn về `null` và cập nhật lại số tiền đã thanh toán của hóa đơn về 0.
    *   Lưu thay đổi vào DB. Giao dịch biến mất khỏi danh sách lịch sử, và hóa đơn tương ứng lập tức hiển thị nợ trở lại trên Sơ đồ phòng và trang Hóa đơn để thu ngân có thể tiến hành thu tiền lại đúng chuẩn.
