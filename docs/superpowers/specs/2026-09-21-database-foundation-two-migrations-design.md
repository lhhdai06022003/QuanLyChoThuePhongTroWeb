# Thiết kế nền tảng cơ sở dữ liệu cho hai migration

- **Trạng thái:** Đã cập nhật và chốt nền tảng schema ngày 22/09/2026
- **Phạm vi:** Toàn bộ cấu trúc thực thể, ràng buộc và migration cho roadmap 60 ngày
- **Chủ sở hữu schema:** Người A

## Mục tiêu

Chuẩn bị trước entity, quan hệ, ràng buộc và migration cần thiết cho roadmap. Sau khi hai migration được hoàn thành, Người A và Người B có thể phát triển song song mà không cùng sửa entity, `ApplicationDbContext` hoặc EF model snapshot.

Hai migration được chia theo nghiệp vụ:

1. Chỉ số điện nước, OCR, hóa đơn và thanh toán hóa đơn.
2. Phòng công khai, lịch xem, giữ chỗ, tiền cọc và hoàn tiền.

Không sửa migration lịch sử. Mọi thay đổi được tạo thành migration mới và phải được thử trên PostgreSQL test trước khi hợp nhất.

> [!IMPORTANT]
> **Phân định ranh giới cốt lõi:**
> - Migration 2 hoàn thành 100% **nền tảng cơ sở dữ liệu** (Entities, Enums, EF Core Configurations, Schema, Check Constraints, Unique/Filtered Indexes, Foreign Keys & Delete Behaviors, Migration, Model Snapshot, PostgreSQL Integration Tests).
> - Các tầng Application Service, Web API, màn hình giao diện và nghiệp vụ xử lý transaction là phần việc phát triển tính năng tiếp theo và **chưa được triển khai** trong phạm vi migration này.

---

## Phân định Quy tắc Database Đã Bảo Vệ vs Nghiệp Vụ Chưa Triển Khai

### 1. Các quy tắc đã được Database bảo vệ tự động (Enforced by Schema & Constraints)
- **Unique phân công nhân viên - chi nhánh:** Unique index toàn cặp `(NguoiDungId, ChiNhanhId)` không dùng filter. Mỗi cặp chỉ có một bản ghi; `IsActive = true` là điều kiện xác định quyền nghiệp vụ hiện tại.
- **Một yêu cầu giữ chỗ hoạt động trên mỗi phòng:** Filtered unique index trên `PhongTroId` với điều kiện `WHERE "TrangThai" IN (1, 2, 3)` (`ChoThanhToan`, `ChoXacNhanTien`, `DangGiuCho`).
- **Ràng buộc số tiền dương:** Check constraint `CK_YeuCauGiuCho_SoTienGiuCho` bảo đảm `"SoTienGiuCho" IS NULL OR "SoTienGiuCho" > 0`.
- **Ràng buộc số tiền theo trạng thái:** Check constraint `CK_YeuCauGiuCho_SoTienTheoTrangThai` bảo đảm khi `TrangThai IN (1, 2, 3, 4)` thì `SoTienGiuCho` bắt buộc khác NULL và lớn hơn 0; khi ở `MoiTao` được phép là NULL.
- **Ràng buộc thời gian khung giờ:** Check constraint `CK_KhungGioXemPhong_ThoiGian` bảo đảm `"ThoiGianBatDau" < "ThoiGianKetThuc"`.
- **Ràng buộc sức chứa khung giờ:** Check constraint `CK_KhungGioXemPhong_SoLuongToiDa` bảo đảm `"SoLuongToiDa" > 0`.
- **Chống trùng mã giao dịch:** Unique index trên `GiaoDichGiuCho.MaGiaoDich` và `GiaoDichHoanTienGiuCho.MaGiaoDichHoan`.
- **Bảo toàn dữ liệu kiểm toán:** `DeleteBehavior.Restrict` trên 9 bảng tài chính/kiểm toán ngăn chặn cascade delete.
- **Quan hệ duy nhất qua index:** 1-1 giữa `YeuCauGiuCho` và `ApDungTienGiuChoVaoTienCoc`; 1-1 giữa `YeuCauGiuCho` và `QuyetDinhHoanTienGiuCho`.

### 2. Các quy tắc nghiệp vụ thuộc Application Service tương lai (Chưa triển khai)
Các quy tắc sau chỉ là yêu cầu nghiệp vụ cho Application Service tương lai và **chưa được triển khai trong Migration 2**:
- Tổng tiền các giao dịch hoàn không vượt số tiền duyệt hoàn trên `QuyetDinhHoanTienGiuCho`.
- Tổng tiền hoàn không vượt tổng tiền giữ chỗ thực nhận.
- Tiền áp dụng vào cọc không vượt tổng tiền giữ chỗ thực nhận.
- Tiền áp dụng vào cọc không vượt tiền cọc phòng trên hợp đồng (`HopDong.TienCocPhong`).
- AI tự động xác nhận lịch xem khi khung giờ còn sức chứa.
- Khóa hàng và xử lý cạnh tranh cấp ứng dụng (`SELECT ... FOR UPDATE` trong transaction).
- Xác nhận minh chứng và chuyển trạng thái giữ chỗ nguyên tử trong service.

---

## Quy ước chung

- Trường tiền dùng C# `decimal` và PostgreSQL `numeric(18,2)`.
- Chỉ số điện nước và số lượng dùng `decimal` và PostgreSQL `numeric(18,3)`.
- Thuộc tính thời gian không dùng hậu tố `Utc`.
- Tên không có hậu tố `Utc`, nhưng toàn bộ thời gian vẫn lưu theo UTC và chỉ chuyển múi giờ tại ranh giới hiển thị.
- Thanh toán hóa đơn và thanh toán giữ chỗ là hai cụm nghiệp vụ riêng biệt.
- Bảng lịch sử, minh chứng và giao dịch là dữ liệu kiểm toán; sử dụng `DeleteBehavior.Restrict`, không xóa vật lý.
- AI chỉ gọi Application Service; không ghi trực tiếp database, xác nhận tiền, duyệt giữ chỗ hoặc quyết định hoàn tiền.

## Số lượng bảng

EF model snapshot sau hai migration có **37 bảng nghiệp vụ**.

| Nhóm | Bảng mới | Tổng lũy kế |
| --- | ---: | ---: |
| Baseline ban đầu | 0 | 17 |
| Migration 1 | 5 | 22 |
| Migration 2 | 15 | 37 |

Nếu tính bảng nội bộ `__EFMigrationsHistory` của EF Core, database vật lý PostgreSQL có **38 bảng**.

---

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

### Năm bảng mới (Migration 1)

1. **`anh_chi_so_dong_ho`**: lưu URL ảnh Cloudinary, chỉ số AI gợi ý, độ tin cậy và quyết định xác nhận của nhân viên.
2. **`lich_su_trang_thai_hoa_don`**: vết kiểm toán trạng thái hóa đơn.
3. **`yeu_cau_thanh_toan_hoa_don`**: đợt yêu cầu thanh toán hóa đơn qua VietQR.
4. **`minh_chung_thanh_toan_hoa_don`**: ảnh minh chứng chuyển khoản của khách.
5. **`lich_su_trang_thai_yeu_cau_thanh_toan_hoa_don`**: vết kiểm toán thanh toán hóa đơn.

---

## Migration 2: phòng công khai, lịch xem và giữ chỗ

### Thay đổi bảng hiện hữu

**`phong_tro`:** bổ sung:
- `DuocDangTin` (`boolean`, NOT NULL, default: `false`).
- `TieuDeDangTin` (`character varying(255)`, NULL).
- `MaCongKhai` (`character varying(50)`, NULL, unique filtered index `WHERE "MaCongKhai" IS NOT NULL`).
- `NguoiDangTinId` (`integer`, NULL, FK tham chiếu `nguoi_dung.NguoiDungId`, `DeleteBehavior.SetNull`).
- Bỏ hoàn toàn `NgayDangTin` và `NgayNgungDang`. Dữ liệu cũ tự động nhận `DuocDangTin = false` thông qua `defaultValue: false` trong migration (không viết câu SQL UPDATE thủ công).

**`nguoi_dung`:** bổ sung role `KhachVangLai = 3` (bảo toàn nguyên vẹn giá trị numeric: `Admin = 0`, `NhanVien = 1`, `KhachThue = 2`). Bổ sung navigation properties `KhachVangLai` và `NhanVienChiNhanhs`.

### Mười lăm bảng mới (Migration 2)

1. **`anh_phong_tro`**: Ảnh phòng trọ tải lên Cloudinary. Cờ `LaAnhDaiDien`, thứ tự hiển thị, người tải. Filtered unique index: mỗi phòng trọ chỉ có tối đa một ảnh đại diện đang hoạt động (`WHERE "LaAnhDaiDien" = true AND "IsActive" = true`). `DeleteBehavior.Cascade` theo vòng đời `phong_tro`.
2. **`nhan_vien_chi_nhanh`**: Phân công nhân viên vào chi nhánh. Ràng buộc duy nhất: **Unique index toàn bộ cặp `(NguoiDungId, ChiNhanhId)` không dùng filter**. Mỗi cặp chỉ có một bản ghi; cấp/thu hồi quyền thực hiện bằng cập nhật `IsActive`, người và ngày phân công/thu hồi. `IsActive = true` là điều kiện xác định quyền hiện tại. Khóa ngoại audit dùng `DeleteBehavior.Restrict`.
3. **`khach_vang_lai`**: Hồ sơ một-một với `nguoi_dung`, unique index trên `NguoiDungId`. Không tạo bản ghi `nguoi_thue` cho đến khi ký hợp đồng.
4. **`khung_gio_xem_phong`**: Khung giờ xem phòng **thuộc về một `PhongTro` cụ thể** (`PhongTroId`), không thiết kế theo `ChiNhanhId` hoặc thứ lặp định kỳ. Check constraints:
   - `CK_KhungGioXemPhong_ThoiGian`: `"ThoiGianBatDau" < "ThoiGianKetThuc"`
   - `CK_KhungGioXemPhong_SoLuongToiDa`: `"SoLuongToiDa" > 0`
5. **`yeu_cau_xem_phong`**: Yêu cầu xem phòng từ khách vãng lai hoặc ẩn danh. Bắt buộc họ tên và số điện thoại; email tùy chọn; `KhachVangLaiId` tùy chọn; `KhungGioXemPhongId` tùy chọn. Không lưu token email.
6. **`lich_su_trang_thai_yeu_cau_xem_phong`**: Dữ liệu kiểm toán chuyển trạng thái lịch xem (`DeleteBehavior.Restrict`).
7. **`yeu_cau_giu_cho`**: Yêu cầu giữ chỗ (bắt buộc `KhachVangLaiId`, phòng `PhongTroId`, `YeuCauXemPhongId` tùy chọn).
   - `SoTienGiuCho` là kiểu nullable `decimal?`. Khi ở `MoiTao`, số tiền được phép là `null`. Khi chuyển sang `ChoThanhToan`, số tiền bắt buộc phải được nhân viên duyệt lớn hơn 0.
   - Check constraints:
     - `CK_YeuCauGiuCho_SoTienGiuCho`: `"SoTienGiuCho" IS NULL OR "SoTienGiuCho" > 0`
     - `CK_YeuCauGiuCho_SoTienTheoTrangThai`: `("TrangThai" NOT IN (1, 2, 3, 4)) OR ("SoTienGiuCho" IS NOT NULL AND "SoTienGiuCho" > 0)`
   - Filtered unique index chống race-condition: mỗi phòng chỉ có tối đa một yêu cầu giữ chỗ ở trạng thái hoạt động (`TrangThai IN (1, 2, 3)` tức `ChoThanhToan`, `ChoXacNhanTien`, `DangGiuCho`).
8. **`lich_su_trang_thai_yeu_cau_giu_cho`**: Kiểm toán trạng thái giữ chỗ (`DeleteBehavior.Restrict`).
9. **`yeu_cau_thanh_toan_giu_cho`**: Yêu cầu nộp tiền giữ chỗ qua chuyển khoản/VietQR (`DeleteBehavior.Restrict`).
10. **`minh_chung_thanh_toan_giu_cho`**: Minh chứng chuyển khoản (`DeleteBehavior.Restrict`).
11. **`giao_dich_giu_cho`**: Ghi nhận tiền thực nhận vào tài khoản khi nhân viên xác nhận (`DeleteBehavior.Restrict`). Unique index trên `MaGiaoDich`.
12. **`lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho`**: Kiểm toán thanh toán (`DeleteBehavior.Restrict`).
13. **`ap_dung_tien_giu_cho_vao_tien_coc`**: Áp dụng tiền giữ chỗ đã thu sang cấn trừ vào tiền cọc hợp đồng khi ký hợp đồng chính thức (`DeleteBehavior.Restrict`). Unique index trên `YeuCauGiuChoId`.
14. **`quyet_dinh_hoan_tien_giu_cho`**: Quyết định hoàn tiền giữ chỗ khi hủy giữ chỗ (`DeleteBehavior.Restrict`). Unique index trên `YeuCauGiuChoId`.
15. **`giao_dich_hoan_tien_giu_cho`**: Giao dịch chi tiền hoàn thực tế (`DeleteBehavior.Restrict`). Unique index trên `MaGiaoDichHoan`.

### Ràng buộc DeleteBehavior.Restrict cho dữ liệu kiểm toán và tài chính

Toàn bộ các khóa ngoại thuộc 9 bảng sau đây sử dụng `DeleteBehavior.Restrict` (PostgreSQL `ON DELETE RESTRICT` hoặc `NO ACTION`) để đảm bảo không bị xóa dây chuyền gây mất dữ liệu kiểm toán:
1. `lich_su_trang_thai_yeu_cau_xem_phong`
2. `lich_su_trang_thai_yeu_cau_giu_cho`
3. `yeu_cau_thanh_toan_giu_cho`
4. `minh_chung_thanh_toan_giu_cho`
5. `lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho`
6. `giao_dich_giu_cho`
7. `ap_dung_tien_giu_cho_vao_tien_coc`
8. `quyet_dinh_hoan_tien_giu_cho`
9. `giao_dich_hoan_tien_giu_cho`

---

## Kiểm chứng nền tảng cơ sở dữ liệu đã hoàn thành

1. **Biên dịch**: Solution biên dịch thành công 0 lỗi.
2. **Kiểm thử tự động toàn solution (Chạy tuần tự với `-m:1`)**:
   ```powershell
   dotnet test QuanLyChoThuePhongTroWeb.sln --no-restore -m:1
   ```
   - `Domain.UnitTests`: 44/44 PASS
   - `Application.UnitTests`: 58/58 PASS
   - `Infrastructure.IntegrationTests`: 122/122 PASS (bao gồm 8 test `ReservationConcurrencyTests` mới kiểm tra race condition và constraints thật)
   - `Web.IntegrationTests`: 33/33 PASS
   - Tổng cộng: **257/257 tests PASS**, 0 Failed, 0 Skipped.
3. **Chu trình Database PostgreSQL thật (`quanlyphongtro_test`)**:
   - Apply Migration 2 thành công đạt đủ 37 bảng nghiệp vụ.
   - Kiểm tra đầy đủ check constraints, unique indexes, foreign keys, delete behaviors.
   - Rollback về Migration 1 thành công (gỡ sạch 15 bảng Migration 2 và rollback cột `phong_tro`).
   - Tự động reapply Migration 2 qua test fixture thành công 100%.
4. **Kiểm tra pending model changes**:
   - `dotnet ef migrations has-pending-model-changes` trả về `No changes have been made to the model since the last migration` (0% schema drift).
5. **Git diff check**:
   - `git diff --check` trả về 0 lỗi.
