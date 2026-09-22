# BÁO CÁO THỰC THI MIGRATION 2: NỀN TẢNG CƠ SỞ DỮ LIỆU ĐĂNG TIN CÔNG KHAI VÀ GIỮ CHỖ PHÒNG TRỌ

**Thời điểm hoàn thành kiểm chứng cuối:** 23/09/2026
- **Kế hoạch thực thi:** `docs/superpowers/plans/2026-09-21-migration-2-public-reservation-foundation.md`
- **Đặc tả thiết kế:** `docs/superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md`
- **Migration được sinh chính thức:** `20260922163513_AddPublicViewingAndReservations`
- **Trạng thái phạm vi:** Đã hoàn thành 100% **nền tảng cơ sở dữ liệu** (schema, constraints, indexes, delete behaviors, migration, model snapshot và PostgreSQL integration tests). Các tầng Application Service, Web API, giao diện và logic xử lý transaction nghiệp vụ chưa triển khai (được xếp lịch cho các task phát triển tính năng tiếp theo).

---

## 1. Danh sách 15 bảng nghiệp vụ mới (Migration 2)

Hệ thống đã bổ sung đầy đủ 15 bảng nghiệp vụ mới theo định dạng snake_case chuẩn của PostgreSQL, đưa tổng số bảng nghiệp vụ EF Core từ 22 lên 37:

1. **`anh_phong_tro`** (`AnhPhongTro`): Metadata hình ảnh phòng trọ tải lên Cloudinary (URL, PublicId, thứ tự hiển thị, cờ ảnh đại diện `LaAnhDaiDien`, trạng thái `IsActive`, người tải).
2. **`nhan_vien_chi_nhanh`** (`NhanVienChiNhanh`): Phân công đa chi nhánh cho nhân viên (`NguoiDungId`, `ChiNhanhId`, `IsActive`, audit gán/thu hồi).
3. **`khach_vang_lai`** (`KhachVangLai`): Hồ sơ khách vãng lai đã đăng ký tài khoản nhưng chưa thành người thuê chính thức (`NguoiDungId` 1-1 với `nguoi_dung`, role `KhachVangLai = 3`).
4. **`khung_gio_xem_phong`** (`KhungGioXemPhong`): Khung giờ xem phòng **thuộc về một phòng trọ cụ thể** (`PhongTroId`), thời gian bắt đầu `ThoiGianBatDau`, kết thúc `ThoiGianKetThuc`, sức chứa tối đa `SoLuongToiDa`, người phụ trách tùy chọn, trạng thái hoạt động.
5. **`yeu_cau_xem_phong`** (`YeuCauXemPhong`): Yêu cầu đặt lịch xem phòng từ khách (hỗ trợ cả khách vãng lai đăng nhập và khách ẩn danh; bắt buộc Họ tên + SĐT, email tùy chọn, khung giờ tùy chọn; không chứa email verification token).
6. **`lich_su_trang_thai_yeu_cau_xem_phong`** (`LichSuTrangThaiYeuCauXemPhong`): Lịch sử chuyển trạng thái yêu cầu xem phòng (audit thời gian, tác nhân Hệ thống/Người dùng, ghi chú).
7. **`yeu_cau_giu_cho`** (`YeuCauGiuCho`): Yêu cầu giữ chỗ phòng trọ (bắt buộc `KhachVangLaiId`, phòng `PhongTroId`, liên kết tùy chọn `YeuCauXemPhongId`, số tiền giữ chỗ `SoTienGiuCho` nullable `decimal?`, hạn thanh toán, người duyệt).
8. **`lich_su_trang_thai_yeu_cau_giu_cho`** (`LichSuTrangThaiYeuCauGiuCho`): Vết kiểm toán tiến trình trạng thái giữ chỗ.
9. **`yeu_cau_thanh_toan_giu_cho`** (`YeuCauThanhToanGiuCho`): Yêu cầu thanh toán tiền giữ chỗ qua chuyển khoản VietQR (tách biệt hoàn toàn khỏi thanh toán hóa đơn hàng tháng).
10. **`minh_chung_thanh_toan_giu_cho`** (`MinhChungThanhToanGiuCho`): Minh chứng chuyển khoản (ảnh biên lai/UNC, số tiền khai báo, ghi chú duyệt/từ chối).
11. **`giao_dich_giu_cho`** (`GiaoDichGiuCho`): Giao dịch ghi nhận tiền giữ chỗ thực nhận vào tài khoản khi nhân viên xác nhận minh chứng hợp lệ, chống trùng mã giao dịch.
12. **`lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho`** (`LichSuTrangThaiYeuCauThanhToanGiuCho`): Vết kiểm toán thanh toán giữ chỗ.
13. **`ap_dung_tien_giu_cho_vao_tien_coc`** (`ApDungTienGiuChoVaoTienCoc`): Áp dụng tiền giữ chỗ đã thu sang cấn trừ vào tiền cọc hợp đồng khi ký hợp đồng chính thức (quan hệ 1-1).
14. **`quyet_dinh_hoan_tien_giu_cho`** (`QuyetDinhHoanTienGiuCho`): Quyết định hoàn tiền giữ chỗ khi hủy giữ chỗ (số tiền duyệt hoàn, lý do, người ra quyết định, trạng thái).
15. **`giao_dich_hoan_tien_giu_cho`** (`GiaoDichHoanTienGiuCho`): Giao dịch chi tiền hoàn thực tế, chống trùng mã giao dịch hoàn.

---

## 2. Các cột bổ sung vào bảng hiện có

### Bảng `phong_tro` (`PhongTro`):
- **`DuocDangTin`** (`boolean`, NOT NULL, default: `false`): Cờ bật/tắt hiển thị công khai.
  - *Dữ liệu phòng trọ cũ tự động nhận giá trị `false` thông qua `defaultValue: false` trong migration, không chạy lệnh SQL UPDATE thủ công.*
  - *Đã loại bỏ hoàn toàn `NgayDangTin` và `NgayNgungDang` khỏi entity, DTO, EF mapping và migration.*
- **`TieuDeDangTin`** (`character varying(255)`, NULL): Tiêu đề hiển thị trên trang công khai.
- **`MaCongKhai`** (`character varying(50)`, NULL): Mã định danh phòng phục vụ tìm kiếm/chia sẻ công khai.
- **`NguoiDangTinId`** (`integer`, NULL): Khóa ngoại tham chiếu đến `nguoi_dung.NguoiDungId` (`DeleteBehavior.SetNull`).

### Bảng `nguoi_dung` (`NguoiDung`):
- Enum `Role` bổ sung giá trị `KhachVangLai = 3` (bảo toàn nguyên vẹn thứ tự các role cũ: `Admin = 0`, `NhanVien = 1`, `KhachThue = 2`).
- Navigation properties: `KhachVangLai`, `NhanVienChiNhanhs`.

---

## 3. Ràng buộc Constraint, Index và EF Delete Behavior đã sửa và chuẩn hóa

### 3.1. Khung giờ xem phòng (`khung_gio_xem_phong`):
- 2 check constraint database:
  - `CK_KhungGioXemPhong_ThoiGian`: `"ThoiGianBatDau" < "ThoiGianKetThuc"`
  - `CK_KhungGioXemPhong_SoLuongToiDa`: `"SoLuongToiDa" > 0`
- `KhungGioXemPhong` gắn trực tiếp với `PhongTroId`.
- Khóa ngoại `NguoiPhuTrachId` sử dụng `DeleteBehavior.Restrict`.

### 3.2. Phân công nhân viên chi nhánh (`nhan_vien_chi_nhanh`):
- **Unique index toàn bộ cặp `(NguoiDungId, ChiNhanhId)` không dùng filter**:
  - Không dùng filtered unique index theo `IsActive`.
  - Mỗi cặp nhân viên - chi nhánh chỉ có đúng một bản ghi. Cấp hoặc thu hồi quyền thực hiện bằng cập nhật `IsActive`, người và ngày thao tác.
  - Test `BranchStaffAssignment_RejectsDuplicatePair_RegardlessOfIsActive` xác nhận cơ sở dữ liệu từ chối insert cặp trùng bất kể giá trị `IsActive`.
- Khóa ngoại người phân công/thu hồi sử dụng `DeleteBehavior.Restrict`.

### 3.3. Số tiền giữ chỗ và ràng buộc theo trạng thái (`yeu_cau_giu_cho`):
- `SoTienGiuCho` là kiểu nullable `decimal?` (C#) và mapping sang `numeric(18,2)` (PostgreSQL):
- 2 check constraint database:
  - `CK_YeuCauGiuCho_SoTienGiuCho`: `"SoTienGiuCho" IS NULL OR "SoTienGiuCho" > 0` (không chấp nhận số tiền bằng 0 hoặc âm).
  - `CK_YeuCauGiuCho_SoTienTheoTrangThai`: `("TrangThai" NOT IN (1, 2, 3, 4)) OR ("SoTienGiuCho" IS NOT NULL AND "SoTienGiuCho" > 0)`:
    - Khi `TrangThai = 0` (`MoiTao`): `SoTienGiuCho` được phép mang giá trị `NULL`.
    - Khi `TrangThai` thuộc `1` (`ChoThanhToan`), `2` (`ChoXacNhanTien`), `3` (`DangGiuCho`), `4` (`DaChuyenHopDong`): `SoTienGiuCho` bắt buộc khác `NULL` và lớn hơn 0.
    - Các trạng thái kết thúc `5` (`TuChoi`), `6` (`HetHan`), `7` (`DaHuy`): có thể giữ lại số tiền đã duyệt trước đó nếu có.
- **Filtered unique index:** `IX_yeu_cau_giu_cho_PhongTroId_TrangThai_Active` trên `PhongTroId` với điều kiện `TrangThai IN (1, 2, 3)` (`ChoThanhToan`, `ChoXacNhanTien`, `DangGiuCho`). Mỗi phòng chỉ có tối đa một yêu cầu giữ chỗ ở trạng thái hoạt động tại một thời điểm.

### 3.4. Mã giao dịch không trùng:
- `giao_dich_giu_cho`: Unique index trên `MaGiaoDich`.
- `giao_dich_hoan_tien_giu_cho`: Unique index trên `MaGiaoDichHoan`.

### 3.5. Ràng buộc `DeleteBehavior.Restrict` cho dữ liệu kiểm toán và tài chính:
Toàn bộ các quan hệ chứa lịch sử, minh chứng và giao dịch tài chính đã được thiết lập `DeleteBehavior.Restrict`, gồm 9 bảng:
1. `lich_su_trang_thai_yeu_cau_xem_phong`
2. `lich_su_trang_thai_yeu_cau_giu_cho`
3. `yeu_cau_thanh_toan_giu_cho`
4. `minh_chung_thanh_toan_giu_cho`
5. `lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho`
6. `giao_dich_giu_cho`
7. `ap_dung_tien_giu_cho_vao_tien_coc`
8. `quyet_dinh_hoan_tien_giu_cho`
9. `giao_dich_hoan_tien_giu_cho`

`anh_phong_tro` giữ nguyên `DeleteBehavior.Cascade` theo vòng đời `phong_tro`.

---

## 4. Kết quả Chu trình Kiểm thử Cơ sở dữ liệu thật trên PostgreSQL test (`quanlyphongtro_test`)

Đã thực hiện chu trình kiểm tra tính toàn vẹn di trú theo đúng hướng dẫn:

1. **Database test ban đầu ở Migration 1** (`20260921155855_AddBillingOcrAndInvoicePayments`):
   - Kết quả: 22 bảng nghiệp vụ hoạt động bình thường.
2. **Apply Migration 2 mới** (`20260922163513_AddPublicViewingAndReservations`):
   - Kết quả: Thành công. Database đạt chính xác **37 bảng nghiệp vụ**.
3. **Kiểm tra schema, constraint, index, delete behavior**:
   - 15 bảng mới được tạo đầy đủ.
   - Các check constraint (`CK_KhungGioXemPhong_ThoiGian`, `CK_KhungGioXemPhong_SoLuongToiDa`, `CK_YeuCauGiuCho_SoTienGiuCho`, `CK_YeuCauGiuCho_SoTienTheoTrangThai`) tồn tại trên `pg_constraint`.
   - Index trên `nhan_vien_chi_nhanh` là unique vô điều kiện (không có mệnh đề `WHERE`).
   - Cột `phong_tro.DuocDangTin` có default value `false`.
4. **Rollback về Migration 1**:
   - Chạy lệnh: `dotnet ef database update 20260921155855_AddBillingOcrAndInvoicePayments`.
   - Kết quả: Revert migration `20260922163513_AddPublicViewingAndReservations` thành công. Toàn bộ 15 bảng của Migration 2 được gỡ bỏ sạch sẽ, các cột mới trên `phong_tro` được rollback, giữ nguyên 22 bảng Migration 1.
5. **Reapply Migration 2**:
   - Chạy lệnh: `dotnet ef database update 20260922163513_AddPublicViewingAndReservations`.
   - Kết quả: Applying migration thành công 100%.

---

## 5. Bộ Integration Tests Kiểm tra Concurrency và Constraint Database

Tập tin kiểm thử mới tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/ReservationConcurrencyTests.cs (sử dụng PostgreSqlFixture, PostgreSqlCollection và ApplicationDbContext thật) gồm 8 trường hợp kiểm thử độc lập. Test cạnh tranh sử dụng hai transaction chồng lấn thực sự; phần teardown xóa dữ liệu theo thứ tự phụ thuộc của các khóa ngoại Restrict và không nuốt lỗi dọn dữ liệu:

1. **`BranchStaffAssignment_RejectsDuplicatePair_RegardlessOfIsActive`**: Kiểm tra PostgreSQL từ chối hai bản ghi có cùng `(NguoiDungId, ChiNhanhId)` bất kể cờ `IsActive` (ném `DbUpdateException` với Postgres error code `23505`).
2. **`ActiveReservationPerRoom_RejectsSecondActiveReservation_ForSameRoom`**: Kiểm tra filtered unique index từ chối hai yêu cầu giữ chỗ ở trạng thái hoạt động (`ChoThanhToan`, `ChoXacNhanTien`, `DangGiuCho`) trên cùng một phòng.
3. **`ActiveReservationPerRoom_AllowsNewActiveReservation_WhenPreviousIsTerminalOrMoiTao`**: Kiểm tra phòng có yêu cầu ở trạng thái `TuChoi`, `HetHan`, `DaHuy` hoặc `MoiTao` vẫn cho phép tạo yêu cầu hoạt động mới (`ChoThanhToan`).
4. **`ConcurrentApproval_CompetingOnSameRoom_OnlyOneSucceeds`**: Mô phỏng 2 DbContext độc lập cạnh tranh duyệt giữ chỗ cho 2 khách khác nhau trên cùng một phòng; chứng minh lớp bảo vệ database đảm bảo chỉ 1 transaction commit thành công, transaction còn lại bị từ chối bởi filtered unique index.
5. **`DepositTransaction_RejectsDuplicateTransactionCode`**: Kiểm tra PostgreSQL từ chối 2 `GiaoDichGiuCho` có cùng `MaGiaoDich` (`23505`).
6. **`RefundTransaction_RejectsDuplicateRefundTransactionCode`**: Kiểm tra PostgreSQL từ chối 2 `GiaoDichHoanTienGiuCho` có cùng `MaGiaoDichHoan` (`23505`).
7. **`ViewingSlot_CheckConstraints_EnforcedByDatabase`**: Kiểm tra check constraint từ chối `ThoiGianBatDau >= ThoiGianKetThuc` và `SoLuongToiDa <= 0` (`23514`), cho phép bản ghi hợp lệ.
8. **`ReservationDeposit_CheckConstraints_EnforcedByDatabase`**: Kiểm tra check constraint:
   - Cho phép `MoiTao` với `SoTienGiuCho = NULL`.
   - Từ chối `SoTienGiuCho = 0` và `SoTienGiuCho < 0`.
   - Từ chối `ChoThanhToan` với `SoTienGiuCho = NULL`.
   - Cho phép `ChoThanhToan` với `SoTienGiuCho > 0`.

---

## 6. Kết quả Kiểm thử Toàn bộ Solution

Chạy kiểm thử toàn solution theo chế độ tuần tự để ngăn chặn xung đột (race condition) khi `Infrastructure.IntegrationTests` và `Web.IntegrationTests` cùng chạy migration trên cùng một database test:
```bash
dotnet test QuanLyChoThuePhongTroWeb.sln --no-restore -m:1
```
*Lưu ý:* Cờ `-m:1` (hoặc `-maxCpuCount:1`) đảm bảo MSBuild thực thi các test project lần lượt, ngăn hai test fixture cùng gọi `context.Database.MigrateAsync()` đồng thời dẫn tới lỗi Postgres `duplicate column DuocDangTin`.

Tất cả các dự án kiểm thử trong solution đều vượt qua 100%:

| Dự án kiểm thử | Trạng thái | Số test Đạt | Số test Thất bại | Bỏ qua |
| :--- | :---: | :---: | :---: | :---: |
| **`QuanLyChoThuePhongTroWeb.Domain.UnitTests`** | PASS | **44** | 0 | 0 |
| **`QuanLyChoThuePhongTroWeb.Application.UnitTests`** | PASS | **58** | 0 | 0 |
| **`QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests`** | PASS | **122** | 0 | 0 |
| **`QuanLyChoThuePhongTroWeb.Web.IntegrationTests`** | PASS | **33** | 0 | 0 |
| **TỔNG CỘNG TOÀN BỘ SOLUTION** | **PASS** | **257** | **0** | **0** |

*Kiểm tra pending model changes:*
- Lệnh: `dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj`
- Kết quả: `No changes have been made to the model since the last migration.` (0% schema drift).

*Kiểm tra định dạng và khoảng trắng Git:*
- Lệnh: `git diff --check`
- Kết quả: 0 lỗi.

---

## 7. Phân định Rõ ràng: Database Đã Bảo Vệ vs. Nghiệp Vụ Chưa Triển Khai

### 7.1. Các quy tắc Database đã bảo vệ 100%:
- Unique toàn cặp nhân viên và chi nhánh (`nhan_vien_chi_nhanh`), từ chối cặp trùng bất kể cờ `IsActive`.
- Một yêu cầu giữ chỗ hoạt động trên mỗi phòng (`IX_yeu_cau_giu_cho_PhongTroId_TrangThai_Active`).
- Giá trị số tiền giữ chỗ dương (`CK_YeuCauGiuCho_SoTienGiuCho`).
- Số tiền bắt buộc khác NULL và lớn hơn 0 ở các trạng thái giữ chỗ hoạt động/hợp đồng (`CK_YeuCauGiuCho_SoTienTheoTrangThai`).
- Thời gian bắt đầu nhỏ hơn thời gian kết thúc (`CK_KhungGioXemPhong_ThoiGian`).
- Sức chứa khung giờ lớn hơn 0 (`CK_KhungGioXemPhong_SoLuongToiDa`).
- Mã giao dịch không trùng (`MaGiaoDich`, `MaGiaoDichHoan`).
- Khóa ngoại và hành vi chặn xóa `DeleteBehavior.Restrict` cho toàn bộ 9 bảng kiểm toán, minh chứng và giao dịch tài chính.
- Quan hệ 1-1 qua unique index giữa `khach_vang_lai` và `nguoi_dung`, giữa `ap_dung_tien_giu_cho_vao_tien_coc` và `yeu_cau_giu_cho`.

### 7.2. Các quy tắc Nghiệp vụ CHƯA triển khai (chờ các feature sprint tiếp theo):
Các quy tắc sau chỉ là yêu cầu nghiệp vụ cho Application Service tương lai, schema hiện tại chưa và không thể tự động bảo đảm ở mức database độc lập:
- Tổng tiền hoàn không vượt số tiền được duyệt (`QuyetDinhHoanTienGiuCho.SoTienDuyetHoan`).
- Tổng tiền hoàn không vượt tiền giữ chỗ thực nhận (`GiaoDichGiuCho.SoTienNhan`).
- Tiền áp dụng vào cọc không vượt tổng tiền giữ chỗ thực nhận.
- Tiền áp dụng không vượt tiền cọc hợp đồng.
- AI tự xác nhận lịch khi còn sức chứa.
- Khóa hàng (`FOR UPDATE`) và xử lý đồng thời ở Application Service.
- Xác nhận minh chứng và chuyển trạng thái nguyên tử.
- Web API endpoints và Razor Pages/Controllers.
- Background jobs quét hạn thanh toán hoặc hết hạn giữ chỗ.

---

## 8. Trạng thái Git và Bàn giao

- Migration 1, Migration 2, kiểm thử và tài liệu được gom thành **một commit squash** trên nhánh codex/migration-1-billing-foundation sau khi smoke test database dev mới thành công.
- Nhánh được push trực tiếp lên origin/codex/migration-1-billing-foundation theo yêu cầu của người dùng.
- **Không tạo Pull Request.**
- Không đưa secrets, mật khẩu, appsettings.json hoặc AGENTS.md vào commit.

## 9. Kết luận bàn giao nền tảng database

Migration 2 đã hoàn tất trong phạm vi cập nhật cơ sở dữ liệu để phục vụ các chức năng tương lai. Entity, EF configuration, migration, snapshot, constraint/index/delete behavior và PostgreSQL integration tests đã đồng bộ. Kế hoạch đã phân biệt rõ các test hợp đồng dữ liệu hiện có với các transaction nghiệp vụ chưa triển khai; không có Application Service, API, UI hoặc background job mới được thêm trong lần hoàn thiện này.
