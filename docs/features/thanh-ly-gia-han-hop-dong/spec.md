# Spec: Thanh lý, gia hạn hợp đồng và lịch sử thuê

- **Trạng thái:** v2, người dùng đã duyệt (04/11/2026; chốt cả 6 điểm ở mục 10). Đã code 05/11/2026, xem [review.md](review.md).
- **Nhánh:** `feature/thanh-ly-gia-han-hop-dong` (từ `master` = `d34fa90`).
- **Phạm vi:** chỉ cổng quản lý (`Areas/QuanLyNhaTro`) và tầng Application/Infrastructure tương ứng. Không đụng cổng khách vãng lai.
- **Schema:** không đổi. Không thêm bảng, cột, index hay migration. Thêm một giá trị enum lưu dạng `int` (mục 4.3), phải kiểm lại bằng `has-pending-model-changes`.

## 1. Bối cảnh trong code hiện tại

| Điểm | Hiện trạng | Ảnh hưởng |
|---|---|---|
| `ContractAutoCloseJob` → `ContractAutoCloseUseCase` | 00:00 giờ VN mỗi ngày: hợp đồng `DangHoatDong` có `ThoiDiemKetThuc < now` → `DaKetThuc`, phòng → `Trong`, thành viên `NgayChuyenDi = now`, đóng `DangKyDichVu` của phòng | Phòng tự về Trống khi nhân viên chưa lập hóa đơn tháng cuối, chưa trả phòng thật hay đang chờ gia hạn |
| `ContractExpiryAlertUseCase` | Gửi **email** cho khách và `ContractAlertSettings.AdminEmail` ở mốc 30/15 ngày trước hết hạn; chống lặp bằng file `sent_*.json` | Nhân viên chi nhánh không nhận gì; không có báo khi đã quá hạn |
| `HopDongService.UpdateAsync` đổi sang `DaKetThuc` | Phòng → `Trong`, thành viên chuyển đi; **không** đóng `DangKyDichVu`, không xử lý tiền cọc | Kết thúc tay và kết thúc tự động cho hai kết quả khác nhau |
| Hóa đơn | Duy nhất theo `(HopDongId, Thang, Nam)`; `TongTien` phải > 0 khi phát hành/điều chỉnh; dòng có `DonGia`, `SoLuong` ≥ 0 | Không thêm được hóa đơn thứ hai trong tháng trả phòng; không thêm được dòng âm "Trừ tiền cọc" |
| Tính tiền phòng | `HoaDonCalculatorService.TinhTienPhong` đã chia theo số ngày ở dựa trên `ThoiDiemKetThuc` | Trả phòng giữa tháng đã tính đúng tiền phòng nếu `ThoiDiemKetThuc` đúng ngày trả |
| Chọn hợp đồng để lập hóa đơn | `GetValidContractsForBillingAsync` lấy cả `DaKetThuc` (chỉ loại `DaHuy`) có thời gian chồng lên tháng | Hợp đồng đã kết thúc vẫn lập được hóa đơn tháng cuối |
| `DangKyDichVu`, `DichVuDienNuocCuaPhong` | Gắn theo **phòng**, không theo hợp đồng | Gia hạn không cần chép dịch vụ hay chỉ số |
| `ChiTietThanhVienHopDong`, `HopDongDieuKhoan` | Gắn theo **hợp đồng** | Gia hạn phải chép thành viên đang ở và điều khoản |
| `MaHopDong` | `text`, không unique; sinh dạng `HD-{MaChiNhanh}-{yyMM}-{seq}`, `seq` = số mã cùng tiền tố + 1 | Ghi được hậu tố gia hạn mà không đổi schema |
| Người nhận thông báo theo phòng | `ThongBaoService.GuiChoNguoiPhuTrachPhongAsync`: Admin + nhân viên đang được phân công chi nhánh của phòng | Dùng lại cho thông báo hết hạn |

## 2. Bỏ tự động kết thúc hợp đồng, thay bằng thông báo

### 2.1 Gỡ bỏ

- Gỡ `ContractAutoCloseJob`, `IContractAutoCloseUseCase`, `ContractAutoCloseUseCase`, đăng ký DI (`Application/DependencyInjection.cs`, `Infrastructure/DependencyInjection.cs`) và test tương ứng. Hệ thống **không bao giờ** tự đổi trạng thái hợp đồng hay phòng.
- Giữ `IHopDongStore.GetExpiredActiveContractsAsync` để dùng cho thông báo quá hạn (mục 2.2), bỏ `Include` không cần.
- Cập nhật `docs/architecture/README.md` (bảng tác vụ nền) và mục Hiện trạng trong `AGENTS.md`.

### 2.2 Thông báo trong app cho Admin và nhân viên chi nhánh

Mở rộng `ContractExpiryAlertUseCase` (giữ job `ContractExpiryAlertJob`, 08:00 giờ VN):

| Mốc (theo ngày giờ VN của `ThoiDiemKetThuc`) | Người nhận | Kênh |
|---|---|---|
| Còn 30 và 15 ngày (`ContractAlertSettings.AlertDays`) | Khách, `AdminEmail` | Email (giữ nguyên như hiện nay) |
| **Hết hạn**: hợp đồng `DangHoatDong` có ngày hết hạn ≤ hôm nay | Admin + nhân viên chi nhánh của phòng | Thông báo trong app, **một lần duy nhất** cho mỗi hợp đồng (người dùng chốt 04/11/2026) |

- Nội dung: mã hợp đồng, phòng, chi nhánh, người đại diện, ngày hết hạn; link tới chi tiết hợp đồng. Lời nhắc gợi ý hai việc: **Gia hạn** hoặc **Thanh lý**.
- Gửi bằng `GuiChoNguoiPhuTrachPhongAsync(phongTroId, ...)`, `LinhVuc` = "HopDong".
- Chống lặp: dùng lại `IContractExpiryAlertStateStore` với khóa `app_het_han_{HopDongId}`. Điều kiện "≤ hôm nay" (không phải "= hôm nay") để hợp đồng đã quá hạn từ trước khi phát hành, hoặc ngày job không chạy, vẫn được báo đúng một lần. Không nhắc lại; việc cần làm ở mục 2.3 giữ lời nhắc tới khi xử lý xong.
- Email lỗi không chặn thông báo app và ngược lại.
- Cờ `ContractAlertSettings.Enabled` vẫn bật/tắt cả use case như hiện nay.

### 2.3 Trung tâm việc cần làm

Thêm việc **"Hợp đồng đã quá hạn chưa xử lý"** (nhóm `KhachThueHopDong`, mức `Khan`): đếm hợp đồng `DangHoatDong`, chưa xóa, `ThoiDiemKetThuc` < đầu ngày hôm nay (giờ VN), lọc theo chi nhánh được phép; link tới danh sách hợp đồng lọc "Quá hạn". Việc biến mất khi nhân viên gia hạn hoặc thanh lý. Mã việc đặt nối tiếp mã hiện có (KH4).

Thêm việc **"Khách đã trả phòng còn nợ"** (nhóm `KhachThueHopDong`, mức `CanLam`, KH5): đếm hợp đồng `DaKetThuc` chưa xóa có hóa đơn `DaGui` còn nợ, lọc theo chi nhánh được phép; link tới danh sách hóa đơn còn nợ lọc theo các hợp đồng đó. Việc biến mất khi khách trả hết.

## 3. Quy trình cuối tháng (thủ công, thay cho job)

Thứ tự nhân viên/Admin làm khi khách trả phòng:

1. Cập nhật **ngày trả phòng** vào `ThoiDiemKetThuc` của hợp đồng (sửa được khi đang hoạt động; không thuộc nhóm thông tin cốt lõi bị khóa).
2. Chốt chỉ số điện nước kỳ tháng trả phòng (luồng chỉ số hiện có).
3. Lập hóa đơn tháng trả phòng theo luồng hiện có: tiền phòng tự chia theo số ngày ở; thêm khoản phí hư hỏng/vệ sinh (nếu có) bằng chức năng sửa dòng có sẵn; gửi duyệt → Admin chốt → công bố.
4. Bấm **Thanh lý hợp đồng** (mục 4).

Màn thanh lý **chặn** (người dùng chốt 04/11/2026) khi tháng trả phòng chưa có hóa đơn đã công bố (`DaGui`), hoặc còn hóa đơn của hợp đồng ở `Nhap`/`ChoDuyet`/`DaChot`. Thông báo lỗi nói rõ còn thiếu bước nào.

## 4. Thanh lý hợp đồng

### 4.1 Màn hình

Nút **Thanh lý** ở chi tiết hợp đồng `DangHoatDong`. Màn thanh lý hiện:

- Thông tin hợp đồng, phòng, người đại diện, thành viên đang ở, ngày trả phòng (`ThoiDiemKetThuc`).
- **Tiền cọc** (`HopDong.TienCocPhong`).
- Danh sách hóa đơn `DaGui` còn nợ của **cả chuỗi gia hạn** (mọi hợp đồng cùng mã gốc, mọi kỳ; người dùng chốt 05/11/2026 vì cọc đã chuyển qua các lần gia hạn): mã hợp đồng, mã hóa đơn, kỳ, tổng, đã thu, còn lại. **Tổng nợ** = tổng còn lại.
- **Cấn trừ cọc** = min(tiền cọc, tổng nợ).
- **Cọc hoàn trực tiếp cho khách** = tiền cọc − cấn trừ (≥ 0).
- **Khách còn nợ sau cấn trừ** = tổng nợ − cấn trừ (≥ 0).
- Ghi chú (không lưu DB, chỉ in lên biên bản).

### 4.2 Xác nhận (một transaction, khóa dòng hợp đồng)

1. Kiểm lại điều kiện ở mục 3 và quyền (mục 7). Hợp đồng phải vẫn `DangHoatDong` (chống bấm hai lần).
2. Phân bổ **cấn trừ cọc** vào các hóa đơn nợ, kỳ cũ trước. Mỗi hóa đơn ghi một bản ghi `LichSuThanhToan` (mục 4.3), cập nhật `TrangThaiHoaDon` theo quy tắc sổ hiện có.
3. Hợp đồng → `DaKetThuc`, `NgayCapNhat = now`.
4. Phòng → `Trong` (chỉ khi phòng không còn hợp đồng `DangHoatDong` khác).
5. Thành viên đang ở: `NgayChuyenDi = ThoiDiemKetThuc`.
6. Đóng `DangKyDichVu` đang hiệu lực của phòng: `NgayKetThuc = ThoiDiemKetThuc`.
7. Sau commit: thông báo app cho người phụ trách phòng; tải được **biên bản thanh lý PDF** (QuestPDF) tạo từ dữ liệu: hợp đồng, hóa đơn, khoản cấn trừ, cọc hoàn trực tiếp, nợ còn lại, ghi chú, chỗ ký hai bên. Biên bản in lại được bất kỳ lúc nào (ghi chú chỉ có ở lần in đầu).

### 4.2a Khách còn nợ sau cấn trừ (phương án A, người dùng chốt 04/11/2026)

- **Cho thanh lý**, nhưng nút Xác nhận chỉ bật khi nhân viên tích ô "Khách còn nợ X đ sau khi cấn trừ cọc". Server kiểm lại cờ xác nhận và số nợ tính tại thời điểm xác nhận; số nợ đổi so với lúc mở màn (khách vừa trả thêm) thì từ chối, yêu cầu tải lại.
- Biên bản PDF in rõ **số nợ còn lại**, chỗ trống ghi hạn trả và chữ ký hai bên. Hạn trả không lưu DB.
- Hóa đơn còn nợ giữ nguyên `DaGui`, vẫn tính trong công nợ, Dashboard và báo cáo như hiện nay.
- Khách vẫn đăng nhập cổng khách thuê để xem và trả hóa đơn đó (VietQR, SePay, VNPay): quyền xem hóa đơn (`HoaDonStore.CheckHoaDonOwnershipAsync`) chỉ xét `NguoiThueId`, không xét trạng thái hợp đồng. Plan phải có test Web khẳng định khách của hợp đồng `DaKetThuc` vẫn xem và tạo thanh toán được.
- Việc cần làm KH5 (mục 2.3) theo dõi khoản nợ tới khi trả hết.
- Tab Lịch sử thuê (mục 6.1) hiện khoản nợ cũ. Khi tạo hợp đồng mới hoặc thêm thành viên là người đang có nợ ở hợp đồng đã kết thúc, form hiện **cảnh báo** kèm số nợ và mã hóa đơn; không chặn.
- Không có chức năng xóa nợ (ngoài phạm vi, mục 8).

### 4.3 Ghi cấn trừ cọc bằng sổ thanh toán, không bằng dòng âm

Quyết định trước đó là "hóa đơn cuối kỳ có dòng Trừ tiền cọc". Code hiện chặn dòng âm và hóa đơn có tổng ≤ 0 (mục 1). Spec đề xuất đạt cùng kết quả mà không phá các ràng buộc đó:

- Thêm `PhuongThucThanhToan.CanTruTienCoc = 2` (`[Description("Cấn trừ tiền cọc")]`). Enum lưu dạng `int`, không có CHECK, nên không cần migration; kiểm bằng `has-pending-model-changes`.
- Khoản cấn trừ là một bản ghi `LichSuThanhToan` của hóa đơn, `PhuongThuc = CanTruTienCoc`, `GhiChu = "Cấn trừ tiền cọc hợp đồng {MaHopDong}"`, người xác nhận = actor. Trên hóa đơn và biên bản, khoản này hiện là dòng **"Trừ tiền cọc: −X đ"** trong phần thanh toán, số còn phải trả giảm tương ứng.
- Không cho chọn `CanTruTienCoc` ở form thu tiền tay, VietQR, SePay, VNPay.
- Giao dịch cấn trừ **không hủy được** ở màn Lịch sử thanh toán (nút hủy ẩn, server từ chối), vì hủy nó đòi mở lại hợp đồng đã thanh lý, việc này nằm ngoài phạm vi (mục 8).
- Báo cáo doanh thu sau này tách riêng phương thức này.

### 4.4 Bỏ lối kết thúc tắt

Form sửa hợp đồng không còn cho chọn `DaKetThuc` (chỉ kết thúc qua Thanh lý hoặc Gia hạn). `DaHuy` giữ nguyên cho hợp đồng tạo nhầm; khi hủy, đóng thêm `DangKyDichVu` của phòng để khớp với thanh lý.

**Xóa hợp đồng** (người dùng chốt 05/11/2026, điểm M1 của review): chỉ xóa mềm được hợp đồng `DaHuy`. Hợp đồng `DangHoatDong` và `DaKetThuc` không xóa được, vì hợp đồng đã kết thúc có hóa đơn, sổ thanh toán, cấn trừ cọc và chuỗi gia hạn cần lưu trữ để đối chiếu. Server từ chối (`HopDongService.DeleteAsync`); danh sách hợp đồng chỉ hiện nút "Xóa hợp đồng" ở dòng Đã hủy.

## 5. Gia hạn hợp đồng (tạo hợp đồng mới nối tiếp)

### 5.1 Màn hình

Nút **Gia hạn** ở chi tiết hợp đồng `DangHoatDong` (cả khi đã quá hạn). Nhập:

| Trường | Mặc định | Ràng buộc |
|---|---|---|
| Hợp đồng mới bắt đầu từ tháng | Tháng sau tháng của `ThoiDiemKetThuc` cũ (nếu đã quá hạn sang tháng sau: tháng hiện tại + 1) | Luôn ngày **1**, 00:00 giờ VN; sau ngày bắt đầu hợp đồng cũ; hợp đồng cũ **chưa có hóa đơn** cho tháng ≥ tháng này |
| Ngày kết thúc mới | Bắt đầu mới + cùng số tháng của hợp đồng cũ | > ngày bắt đầu mới |
| Giá thuê mới | Giá cũ | > 0 |
| Tiền cọc | Cọc cũ, chuyển nguyên | Đổi được; chênh lệch thu/trả ngoài hệ thống, chỉ hiện nhắc |
| Điều khoản | Chép điều khoản của hợp đồng cũ | Chọn lại được từ điều khoản mẫu |

### 5.2 Xác nhận (một transaction, khóa dòng hợp đồng cũ)

1. Hợp đồng cũ: `ThoiDiemKetThuc` = 23:59:59 ngày cuối tháng trước tháng bắt đầu mới (giờ VN, lưu UTC), `TrangThaiHopDong = DaKetThuc`. Tháng cuối của hợp đồng cũ vẫn lập hóa đơn bình thường (mục 1, chọn hợp đồng để lập hóa đơn).
2. Hợp đồng mới `DangHoatDong`: cùng phòng, cùng `NguoiThueId`, giá/cọc/ngày theo form, `MaHopDong` theo mục 5.3.
3. Thành viên đang ở của hợp đồng cũ: `NgayChuyenDi` = ngày kết thúc cũ; tạo dòng mới ở hợp đồng mới với `NgayChuyenVao` = ngày bắt đầu mới.
4. Chép `HopDongDieuKhoan`.
5. Phòng giữ `DaThue`. `DangKyDichVu`, chỉ số điện nước không đổi (theo phòng).
6. Bỏ qua kiểm tra "phòng/người thuê đang có hợp đồng hoạt động khác" cho đúng cặp cũ → mới.
7. Sau commit: thông báo app cho người phụ trách phòng và khách thuê (`GuiChoKhachThueAsync`).

Gia hạn sớm (trước tháng cuối): hợp đồng cũ đã `DaKetThuc` nhưng ngày kết thúc còn ở tương lai. Màn chi tiết hiện **"Đã gia hạn → {mã mới}"** thay cho "Đã kết thúc". Trong thời gian này khách vẫn thấy hợp đồng cũ trong danh sách.

### 5.3 Mã hợp đồng gia hạn

- Mã gốc: mã của hợp đồng đầu tiên trong chuỗi, ví dụ `HD-CN1-2510-001`.
- Lần gia hạn thứ n: `{mã gốc}-GH{n}`, ví dụ `HD-CN1-2510-001-GH1`, `-GH2`. `n` = số hợp đồng chưa xóa có cùng mã gốc + 1.
- Nhận biết: hợp đồng là gia hạn khi mã có `-GH`; mã gốc = phần trước `-GH`. Một helper thuần ở Application dùng chung cho sinh mã, hiển thị, tìm kiếm và báo cáo.
- Sửa `GenerateMaHopDongAsync`/`CountContractsWithPrefixAsync`: không đếm mã có `-GH` để số thứ tự của tháng gốc không bị nhảy.
- `MaHopDong` không sửa được trên form (giữ như hiện nay).

## 6. Lịch sử thuê của khách và tìm kiếm

Không cần cột mới: dữ liệu suy từ `HopDong.NguoiThueId` và `ChiTietThanhVienHopDong`.

### 6.1 Tab "Lịch sử thuê" ở chi tiết người thuê

- Tóm tắt: **số lần thuê** (số mã gốc khác nhau), số hợp đồng (gồm gia hạn), số lần ở ghép, tổng số tháng ở, **nợ hiện tại** (hóa đơn `DaGui` còn lại).
- Bảng: mã hợp đồng, vai trò (Đứng tên / Ở ghép), loại (Mới / Gia hạn lần n), phòng, chi nhánh, từ – đến, trạng thái, còn nợ; link chi tiết.
- Nhân viên chỉ thấy hợp đồng của chi nhánh được phân công; các dòng chi nhánh khác **không hiện** và số tóm tắt chỉ đếm phần được phép thấy.

### 6.2 Danh sách hợp đồng

- Tìm theo mã (gõ mã gốc ra cả chuỗi), tên, CCCD, số điện thoại của người đại diện **hoặc thành viên**.
- Bộ lọc thêm: "Quá hạn chưa xử lý", "Sắp hết hạn (30 ngày)", "Gia hạn".
- Chi tiết hợp đồng có khối **Chuỗi hợp đồng**: các hợp đồng cùng mã gốc, theo thứ tự.

## 7. Phân quyền

- Thêm `EmployeeActionCodes.ContractSettle` ("Contract.Settle") và `ContractRenew` ("Contract.Renew"). Admin và nhân viên được phân công chi nhánh của phòng đều làm được (người dùng chốt 04/11/2026). Service nhận `actorId` làm tham số đầu, kiểm `EmployeeAccessScope` và fail closed như các service hiện có; store nhận `allowedBranchIds`.
- Cổng khách thuê: chỉ xem (hợp đồng mới, dòng "Trừ tiền cọc" trên hóa đơn); không thao tác.

## 8. Ngoài phạm vi

- Theo dõi việc đã hoàn phần cọc dư cho khách (chỉ in trên biên bản).
- Mở lại hợp đồng đã thanh lý; hủy một lần gia hạn.
- Chuyển phòng.
- Khách mới vào **cùng tháng** với khách cũ trả phòng giữa tháng: kỳ chỉ số đang là một bản ghi cho mỗi phòng/tháng, nên hai hợp đồng trong một tháng dùng chung kỳ chỉ số. Ghi thành hạn chế đã biết.
- Xóa nợ hoặc miễn giảm khoản nợ còn lại sau thanh lý.
- Báo cáo thống kê (module tiếp theo).
- Mọi thay đổi ở cổng khách vãng lai.

## 9. Kiểm thử dự kiến

| Tầng | Nội dung |
|---|---|
| Application.UnitTests | Thông báo app đúng một lần khi hết hạn (cả hợp đồng đã quá hạn từ trước), email 30/15 ngày giữ nguyên, kênh email lỗi không chặn app; tính cấn trừ (cọc > nợ, cọc < nợ, không nợ, nhiều hóa đơn kỳ cũ trước); chặn thanh lý khi thiếu hóa đơn tháng cuối hoặc còn hóa đơn chưa công bố; gia hạn: ngày kết thúc cũ cuối tháng, chép thành viên/điều khoản, mã `-GH{n}`, chặn khi đã có hóa đơn ở tháng mới; helper mã gốc; phân quyền chi nhánh (fail closed) |
| Infrastructure.IntegrationTests | Không còn hosted service tự đóng hợp đồng; truy vấn lịch sử thuê (đứng tên + ở ghép, lọc chi nhánh); tìm theo CCCD/SĐT thành viên; sinh mã không đếm `-GH`; lập hóa đơn tháng M không lấy hợp đồng mới bắt đầu ngày 1 tháng M+1 (biên giờ VN/UTC); transaction thanh lý/gia hạn và khóa dòng |
| Web.IntegrationTests | Endpoint Thanh lý/Gia hạn: antiforgery, nhân viên khác chi nhánh bị chặn, bấm hai lần; thiếu cờ xác nhận nợ bị từ chối; số nợ đổi giữa lúc mở và lúc xác nhận bị từ chối; khách của hợp đồng `DaKetThuc` còn nợ vẫn xem và tạo thanh toán được; tải PDF biên bản; form sửa không còn `DaKetThuc`; tab lịch sử thuê; cảnh báo nợ cũ khi tạo hợp đồng |
| Kiến trúc | `BranchScopeArchitectureTests`, `ArchitectureBoundaryTests` vẫn PASS |
| Khác | `has-pending-model-changes` không có thay đổi; thử tay luồng cuối tháng trên DB phát triển |

## 10. Điểm cần người dùng duyệt

| # | Điểm | Trạng thái |
|---|---|---|
| 1 | Cấn trừ cọc ghi vào bảng `LichSuThanhToan` (màn **Lịch sử thanh toán** của cổng quản lý và cổng khách thuê) thay cho dòng âm trên hóa đơn (mục 4.3) | ĐÃ CHỐT 04/11/2026 |
| 2 | Thông báo hết hạn: một lần khi hết hạn (mục 2.2) | ĐÃ CHỐT 04/11/2026 |
| 3 | Admin và nhân viên chi nhánh đều thanh lý, gia hạn được (mục 7) | ĐÃ CHỐT 04/11/2026 |
| 4 | Khách còn nợ sau cấn trừ cọc: phương án A, cho thanh lý có xác nhận, nợ vẫn theo dõi (mục 4.2a) | ĐÃ CHỐT 04/11/2026 |
| 5 | Bỏ `DaKetThuc` khỏi form sửa (mục 4.4) | ĐÃ CHỐT 04/11/2026 |
| 6 | Chặn thanh lý khi hóa đơn tháng trả phòng chưa công bố (mục 3) | ĐÃ CHỐT 04/11/2026 |
