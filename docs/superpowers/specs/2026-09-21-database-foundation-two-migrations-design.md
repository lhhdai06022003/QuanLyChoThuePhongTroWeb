# Thiết kế nền tảng cơ sở dữ liệu cho hai migration

**Trạng thái:** Đã thống nhất ngày 21/09/2026
**Phạm vi:** Toàn bộ chức năng trong roadmap 60 ngày
**Chủ sở hữu schema:** Người A

## Mục tiêu

Chuẩn bị trước entity, quan hệ, ràng buộc và migration cần thiết cho roadmap. Sau khi hai migration được hợp nhất, Người A và Người B có thể phát triển song song mà không cùng sửa entity, `ApplicationDbContext` hoặc EF model snapshot.

Hai migration được chia theo nghiệp vụ:

1. Chỉ số điện nước, OCR, hóa đơn và thanh toán hóa đơn.
2. Phòng công khai, lịch xem, giữ chỗ, tiền cọc và hoàn tiền.

Không sửa migration lịch sử. Mọi thay đổi được tạo thành migration mới và phải được thử trên PostgreSQL test trước khi hợp nhất.

## Quy ước chung

- Trường tiền dùng C# `decimal` và PostgreSQL `numeric(18,2)`.
- Chỉ số điện nước và số lượng dùng `decimal` và PostgreSQL `numeric(18,3)`.
- Thuộc tính thời gian không dùng hậu tố `Utc`.
- Tên không có hậu tố `Utc`, nhưng toàn bộ thời gian vẫn lưu theo UTC và chỉ chuyển múi giờ tại ranh giới hiển thị.
- Thanh toán hóa đơn và thanh toán giữ chỗ là hai cụm nghiệp vụ riêng.
- Bảng lịch sử, minh chứng và giao dịch là dữ liệu kiểm toán; không xóa vật lý.
- AI chỉ gọi Application Service; không ghi trực tiếp database, xác nhận tiền, duyệt giữ chỗ hoặc quyết định hoàn tiền.

## Số lượng bảng

EF model snapshot hiện có 17 bảng nghiệp vụ.

| Nhóm | Bảng mới | Tổng lũy kế |
| --- | ---: | ---: |
| Hiện tại | 0 | 17 |
| Migration 1 | 7 | 24 |
| Migration 2 | 15 | 39 |

Sau hai migration có **39 bảng nghiệp vụ**. Nếu tính bảng nội bộ `__EFMigrationsHistory` của EF Core, database vật lý có **40 bảng**. Thay đổi cột, kiểu dữ liệu và index không được tính là bảng mới.

## Migration 1: chỉ số, OCR, hóa đơn và thanh toán

### Chuẩn hóa kiểu dữ liệu

Chuyển sang `numeric(18,2)`:

- `PhongTro.GiaThue`
- `HopDong.TienCocPhong`, `HopDong.TienThuePhong`
- `HoaDon.TongTien`
- `ChiTietHoaDon.DonGia`, `ChiTietHoaDon.TongTien`
- `DichVuChiNhanh.GiaDichVu`
- `DichVuDienNuocCuaPhong.DonGiaDien`, `DichVuDienNuocCuaPhong.DonGiaNuoc`
- `LichSuThanhToan.SoTienThanhToan`
- `YeuCauSuCo.ChiPhiSuaChua`

Chuyển sang `numeric(18,3)`:

- `DichVuDienNuocCuaPhong.ChiSoDienCu`, `ChiSoDienMoi`
- `DichVuDienNuocCuaPhong.ChiSoNuocCu`, `ChiSoNuocMoi`
- `ChiTietHoaDon.SoLuong`

`PhongTro.DienTich` chưa đổi vì không phải dữ liệu tiền và không tham gia phép tính hóa đơn. Việc đổi kiểu phải cập nhật đồng bộ Domain entity, Application DTO, phép tính, projection, export và test.

### Thay đổi bảng hiện hữu

**`DichVuDienNuocCuaPhong`:** bổ sung `TrangThaiGhiNhan` gồm `Nhap`, `ChoDuyet`, `DaDuyet`, `TuChoi`; cùng `NguoiTaoId`, `NguoiDuyetId`, `NgayDuyet`, `GhiChuDuyet`. Thêm unique filtered index cho `(PhongTroId, Thang, Nam)` trên bản ghi chưa bị xóa. Dữ liệu cũ được backfill thành `DaDuyet`.

**`HoaDon`:** giữ `TrangThaiHoaDon` cho trạng thái tiền `ChuaThanhToan`, `ThanhToanMotPhan`, `DaThanhToan`. Bổ sung trạng thái phát hành `Nhap`, `ChoDuyet`, `DaChot`, `DaGui`, `DaHuy`; cùng `ChoPhepThanhToanMotPhan`, `SoTienThanhToanToiThieu`, `HanThanhToan`, `NguoiChotId`, `NgayChot`, `NgayGui`. Hóa đơn cũ được backfill `DaChot`.

**`LichSuThanhToan`:** bổ sung `MinhChungThanhToanHoaDonId` nullable và `NgayXacNhan`. Thêm unique filtered index cho mã giao dịch ngân hàng hợp lệ.

### Bảy bảng mới

1. **`AnhChiSoDongHo`**: loại đồng hồ, URL, Cloudinary public ID, người gửi, ngày gửi và trạng thái; cho phép nhiều ảnh và gửi lại.
2. **`KetQuaNhanDangChiSo`**: từng lần AI xử lý, giá trị gợi ý, độ tin cậy, model, phản hồi thô/lỗi và ngày xử lý.
3. **`LichSuDieuChinhChiSo`**: giá trị trước/sau, người điều chỉnh, ngày và lý do.
4. **`LichSuTrangThaiHoaDon`**: trạng thái phát hành/thanh toán cũ mới, người thực hiện, ngày và lý do.
5. **`YeuCauThanhToanHoaDon`**: hóa đơn, mã yêu cầu, số tiền, nội dung VietQR, hạn, trạng thái và người tạo.
6. **`MinhChungThanhToanHoaDon`**: ảnh, mã giao dịch/số tiền/ngày khách khai báo, trạng thái đối chiếu, người đối chiếu và lý do.
7. **`LichSuTrangThaiYeuCauThanhToanHoaDon`**: trạng thái cũ/mới, người thực hiện, ngày và lý do.

### Quy tắc Migration 1

- Chỉ hóa đơn đã chốt mới được tạo yêu cầu thanh toán.
- Thanh toán một phần mặc định tắt và được quản lý bật theo từng hóa đơn.
- Số tiền tối thiểu phải lớn hơn 0 và không vượt tổng hóa đơn.
- Tổng tiền xác nhận không được vượt tổng hóa đơn.
- Xác nhận minh chứng, tạo `LichSuThanhToan` và cập nhật hóa đơn chạy trong cùng transaction.
- Ảnh, kết quả AI, minh chứng bị từ chối và lịch sử vẫn được giữ lại.

## Migration 2: phòng công khai, lịch xem và giữ chỗ

### Thay đổi bảng hiện hữu

**`PhongTro`:** bổ sung `DuocDangTin`, `TieuDeDangTin`, `MaCongKhai` duy nhất, `NgayDangTin`, `NgayNgungDang`, `NguoiDangTinId`. Dữ liệu cũ được backfill `DuocDangTin = false`. Khả dụng công khai được tính từ trạng thái phòng và yêu cầu giữ chỗ hiệu lực; không thêm trạng thái giữ chỗ vào `TrangThaiPhong`.

**`NguoiDung`:** bổ sung role `KhachVangLai`, là người có tài khoản nhưng chưa là người thuê. Khi ký hợp đồng, tạo `NguoiThue`, gán `NguoiDung.NguoiThueId` và chuyển role thành `KhachThue`.

### Mười lăm bảng mới

1. **`AnhPhongTro`**: nhiều ảnh, thứ tự, ảnh đại diện, chú thích, trạng thái, người tải và ngày tải.
2. **`NhanVienChiNhanh`**: nhân viên, chi nhánh, `IsActive`, ngày/người phân công và thu hồi, lý do. `IsActive` quyết định quyền hiện tại.
3. **`KhachVangLai`**: hồ sơ một-một với `NguoiDung`, gồm họ tên, email, email chuẩn hóa, số điện thoại, xác minh email và ngày tạo/cập nhật.
4. **`KhungGioXemPhong`**: phòng, thời gian bắt đầu/kết thúc, số lượng tối đa, người phụ trách tùy chọn, trạng thái, người tạo và ngày tạo/cập nhật.
5. **`YeuCauXemPhong`**: phòng, khách vãng lai tùy chọn, họ tên/số điện thoại bắt buộc, email tùy chọn, thời gian mong muốn/xác nhận, khung giờ tùy chọn, nguồn tạo, hình thức xác nhận, trạng thái và ghi chú. Không có token email.
6. **`LichSuTrangThaiYeuCauXemPhong`**: trạng thái cũ/mới, tác nhân hệ thống/người dùng, người thực hiện tùy chọn, ngày và lý do.
7. **`YeuCauGiuCho`**: bắt buộc liên kết khách vãng lai; lịch xem tùy chọn; phòng, số tiền, hạn thanh toán/ký hợp đồng, người duyệt, trạng thái và lý do.
8. **`LichSuTrangThaiYeuCauGiuCho`**.
9. **`YeuCauThanhToanGiuCho`**.
10. **`MinhChungThanhToanGiuCho`**.
11. **`GiaoDichGiuCho`**: tiền thực nhận đã xác nhận.
12. **`LichSuTrangThaiYeuCauThanhToanGiuCho`**.
13. **`ApDungTienGiuChoVaoTienCoc`**: liên kết duy nhất giữa yêu cầu giữ chỗ và hợp đồng.
14. **`QuyetDinhHoanTienGiuCho`**: số tiền duyệt, lý do, quản lý quyết định, ngày và trạng thái.
15. **`GiaoDichHoanTienGiuCho`**: nhiều lần hoàn thuộc một quyết định, gồm số tiền, mã giao dịch, ảnh, người ghi nhận và ngày hoàn.

Luồng giữ chỗ chính:

`MoiTao → ChoThanhToan → ChoXacNhanTien → DangGiuCho → DaChuyenHopDong`

Nhánh kết thúc: `TuChoi`, `HetHan`, `DaHuy`.

### AI và lịch xem

AI thu thập thông tin, tra cứu `KhungGioXemPhong`, để khách chọn rồi gọi Application Service. AI tự xác nhận khung giờ được cấu hình và còn chỗ. Thời gian ngoài cấu hình hoặc xung đột chuyển sang `ChoNhanVienXuLy`. Service kiểm tra lại và đặt lịch trong transaction.

### Quy tắc Migration 2

- Chỉ tài khoản `KhachVangLai` đang hoạt động mới được gửi yêu cầu giữ chỗ.
- Xem phòng không cần đăng nhập; họ tên và số điện thoại bắt buộc, email tùy chọn.
- Đặt lịch kiểm tra sức chứa trong transaction.
- Chỉ một yêu cầu của một phòng ở `ChoThanhToan`, `ChoXacNhanTien` hoặc `DangGiuCho` cùng lúc.
- Xác nhận tiền và chuyển sang `DangGiuCho` phải nguyên tử.
- Tiền giữ chỗ chỉ áp dụng vào tiền cọc một lần và không trở thành thanh toán hóa đơn.
- Một quyết định hoàn có thể có nhiều giao dịch; tổng hoàn không vượt số duyệt hoặc số thực nhận.
- Phòng được giải phóng theo quyết định hủy dù hoàn tiền chưa hoàn tất.
- Nhân viên chỉ xử lý chi nhánh có phân công `IsActive = true`.

## Trình tự thực hiện và khóa phạm vi schema

1. Người A cập nhật entity, enum và EF configuration của Migration 1.
2. Người A tạo, review và thử Migration 1 trên PostgreSQL test.
3. Người A thực hiện Migration 2 trên nền Migration 1.
4. Người A tạo, review và thử Migration 2 trên PostgreSQL test.
5. Sau khi hai migration được hợp nhất, chốt entity và quan hệ làm hợp đồng dùng chung.
6. Hai người phát triển qua DTO và Application Service đã thống nhất; mọi yêu cầu đổi schema chuyển lại Người A.

## Kiểm chứng trước khi chia việc

- Build toàn solution.
- Unit test quy tắc chuyển trạng thái và tính tiền.
- Integration test PostgreSQL cho kiểu `numeric`, khóa ngoại, filtered unique index và migration.
- Test cạnh tranh khi đặt lịch, duyệt giữ chỗ, xác nhận tiền, áp dụng tiền cọc và hoàn tiền.
- Chạy `dotnet ef migrations has-pending-model-changes` sau từng migration.
- Đối chiếu tổng bảng nghiệp vụ: 24 sau Migration 1 và 39 sau Migration 2.
