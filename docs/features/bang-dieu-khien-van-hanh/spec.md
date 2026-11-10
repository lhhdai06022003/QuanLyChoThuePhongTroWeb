# Spec: Bảng điều khiển, Lịch sử thanh toán, Báo cáo cho người vận hành

- Trạng thái: **đã duyệt** (người dùng duyệt bản cuối ngày 09/11/2026); mọi điểm mở ở mục 11 đã chốt
- Ngày: 09/11/2026
- Nhánh: `feature/bang-dieu-khien-van-hanh`, người dùng tách từ `feature/toi-uu-hieu-nang` (HEAD `5989eb3`) ngày 09/11/2026
- Phạm vi: cổng quản lý (`Areas/QuanLyNhaTro`), ba màn Bảng điều khiển, Lịch sử thanh toán, Báo cáo và các liên kết sang màn Hóa đơn, Việc cần làm, Giao dịch SePay. Không đụng cổng khách thuê, khách vãng lai.
- Nguồn quyết định: mục nào ghi **(NgD)** là người dùng đã chốt ngày 09/11/2026; mục ghi **(đề xuất)** là Claude đề xuất, người dùng duyệt cùng spec này.

## 1. Vì sao làm

Hiện cùng một tháng, ba màn ra ba con số khác nhau và người vận hành không biết số nào đúng:

| Màn | Mốc tính tiền | Hóa đơn được tính |
| --- | --- | --- |
| Bảng điều khiển, "Đã thu T{m}/{y}" (`DashboardStore.GetInvoiceMonthlyStatsAsync`) | Kỳ hóa đơn | Mọi hóa đơn chưa xóa |
| Lịch sử thanh toán, "TỔNG DOANH THU" (`LichSuThanhToanStore.GetThongKeThanhToanAsync`) | **Ngày nhận tiền** | Mọi khoản thu |
| Báo cáo, tab Doanh thu (`BaoCaoStore.HoaDonDaGui`) | Kỳ hóa đơn | Chỉ hóa đơn đã gửi khách |

Ngoài ra Lịch sử thanh toán gộp SePay/VNPay vào "Chuyển khoản" còn Báo cáo tách riêng; nhân viên thấy toàn bộ doanh thu dù chủ trọ muốn giữ kín.

Người dùng (NgD):
- Admin và nhân viên đều dùng ba màn. Thông tin bí mật hoặc quan trọng chỉ Admin thấy.
- Nhân viên thao tác Bảng điều khiển hằng ngày. Admin/chủ trọ chủ yếu duyệt bước quan trọng và xem doanh thu.
- Quy mô có thể là nhiều nhân viên, một nhân viên, hoặc chỉ một Admin tự làm hết.
- Tiền mặt nhân viên thu phải giao lại cho chủ **theo tháng**. Chuyển khoản vào thẳng tài khoản của chủ.
- Telegram đã báo tiền chuyển khoản về (theo chi nhánh) cho nhân viên, nên nhân viên tự đối chiếu được minh chứng chuyển khoản.
- Cấp quyền cho nhân viên (chi nhánh nào, thao tác nào) là việc của Admin, giữ nguyên cơ chế hiện có.

## 2. Mục tiêu, không làm, thất bại

**Mục tiêu**
1. Doanh thu chỉ Admin thấy, chặn ở máy chủ (tầng Application), không chỉ ẩn giao diện.
2. Mỗi con số tiền có tên và một dòng giải thích cách tính thống nhất giữa ba màn.
3. Bảng điều khiển sắp khối theo vai trò (phương án B, NgD).
4. Mọi ô số trên Bảng điều khiển bấm được, mở danh sách tạo nên con số đó, giữ chi nhánh và kỳ đang chọn.
5. Lịch sử thanh toán lọc được theo kỳ hóa đơn và người thu; mỗi dòng kỳ trong Báo cáo mở được danh sách giao dịch của kỳ đó, tổng bằng đúng số trên dòng.
6. Kênh thu chia giống nhau giữa Lịch sử thanh toán và Báo cáo.

**Không làm (đợt này)**
- Ghi nhận việc nhân viên nộp tiền mặt cho chủ (NgD chọn "chỉ xem số đã thu").
- Bước "chờ chủ duyệt" mới cho thanh lý, hủy hóa đơn, miễn giảm (NgD: giữ phân quyền hiện có).
- Lưu chuyển khoản SePay chưa khớp vào DB (xem 6.3).
- Đổi schema, migration. Tính tiền giữ chỗ vào ô tiền mặt.
- Thiết kế lại toàn bộ giao diện, đổi Tabler. Sửa bảng tràn ngang 375px (việc riêng).

**Coi là thất bại dù chạy được**
- Nhân viên lấy được số doanh thu bằng cách gọi thẳng endpoint.
- Tổng danh sách mở từ một dòng kỳ của Báo cáo khác số "Đã thu" trên dòng đó.
- Đổi cách tính của số đang có mà không ghi trong spec này (mọi chỗ khác chỉ đổi tên, chú thích, ẩn/hiện).

## 3. Định nghĩa dùng chung

Dùng đúng các chữ này trên giao diện, kèm dòng chú thích (`text-secondary small`) dưới con số:

| Tên hiển thị | Định nghĩa | Chú thích hiển thị |
| --- | --- | --- |
| **Đã thu kỳ T{m}/{y}** | Tổng sổ thanh toán (`LichSuThanhToan`, `!IsDeleted`) của hóa đơn kỳ {m}/{y}, hóa đơn **đã gửi** và chưa xóa (đúng điều kiện `BaoCaoStore.HoaDonDaGui`), gồm cấn trừ cọc | "Theo kỳ hóa đơn, gồm cấn trừ cọc" |
| **Chờ thu kỳ T{m}/{y}** | Như hiện tại: hóa đơn kỳ đó, đã gửi, chưa thanh toán đủ, `TongTien − đã thu` | "Hóa đơn đã gửi, chưa thu đủ" |
| **Quá hạn** | Như hiện tại (`HoaDons.QuaHan(now)`), tính tới hôm nay | "Tính tới hôm nay" |
| **Tiền đã nhận** | Tổng sổ thanh toán theo **ngày nhận tiền** (giờ Việt Nam) trong khoảng lọc | "Theo ngày nhận tiền" |
| **Tiền mặt đã thu** | Sổ thanh toán `PhuongThucThanhToan = TienMat`, `!IsDeleted`, `NguoiXacNhanId` = người đó, `NgayThanhToan` trong tháng đang chọn (giờ Việt Nam) | "Tháng {m}/{y}, chưa gồm tiền giữ chỗ" |

**Đổi cách tính duy nhất:** "Đã thu kỳ" trên Bảng điều khiển chuyển từ "mọi hóa đơn chưa xóa" sang "hóa đơn đã gửi, chưa xóa" để khớp Báo cáo. Trong thực tế số không đổi vì chỉ hóa đơn đã gửi mới nhận tiền được (`InvoiceLedgerService`, `InvoiceTransferHandler` đều kiểm `DaGui`); có test chặn: cùng dữ liệu, Bảng điều khiển và dòng kỳ của Báo cáo ra cùng số.

**Kênh thu** (dùng `PaymentChannels.Of(MaGiaoDich, PhuongThuc)` như Báo cáo, tiền tố trong `InvoicePaymentPolicy`): Tiền mặt, Chuyển khoản (ghi tay, không phải SePay/VNPay), SePay, VNPay, Cấn trừ cọc.

## 4. Phân quyền (NgD)

| Nội dung | Nhân viên | Admin |
| --- | --- | --- |
| Việc cần làm, tình trạng phòng, hợp đồng sắp hết hạn | Có | Có |
| Công nợ: chờ thu, quá hạn, phòng đang nợ, tab Công nợ của Báo cáo | Có | Có |
| Lịch sử thanh toán: danh sách, tìm, lọc | Có | Có |
| Tiền mặt **mình** đã thu trong tháng | Có | Có |
| Doanh thu ("Đã thu kỳ"), biểu đồ doanh thu 12 tháng | **Không** | Có |
| Báo cáo tab Doanh thu (gồm tiền cọc đang giữ, thu khác giữ chỗ) và xuất Excel doanh thu | **Không** | Có |
| Ô tổng của Lịch sử thanh toán (Tiền đã nhận, số giao dịch, theo kênh), trong phạm vi chi nhánh của mình | Có (NgD: để rà soát) | Có |
| Lịch sử thanh toán lọc theo kỳ hóa đơn (link "Xem giao dịch" từ Báo cáo) | Không | Có |
| Bảng "Tiền mặt nhân viên đang giữ", khối "Chờ bạn duyệt" | Không | Có |
| Hủy ghi nhận thanh toán | Không (như hiện tại) | Có |

Cách chặn:
- "Admin" xác định bằng `EmployeeAccessScope.IsAdmin` lấy qua `IEmployeeAccessService.GetScopeAsync(actorId)`; fail closed (scope null thì như nhân viên không có quyền gì).
- `DashboardService`: với nhân viên, các trường doanh thu trong DTO để `null` và **không chạy truy vấn** doanh thu; thêm cờ `CoXemDoanhThu`.
- `BaoCaoService.GetDoanhThuAsync` và `XuatExcelAsync(tab DoanhThu)`: nhân viên nhận `ServiceResult.Forbidden`.
- `LichSuThanhToanService.GetThongKeThanhToanAsync`: giữ cho cả nhân viên, vẫn lọc theo `allowedBranchIds` như hiện tại.
- Giao diện chỉ hiện khối khi máy chủ trả dữ liệu; không dựa vào JS để giấu số.
- Nhân viên vào Báo cáo không có `tab` hoặc `tab=doanh-thu` thì mở tab **Công nợ**; không hiện tab Doanh thu. Báo cáo vẫn yêu cầu quyền `Report.Read` như hiện tại.

## 5. Bảng điều khiển (`/QuanLyNhaTro/Dashboard`)

Cùng một trang, cùng bộ lọc chi nhánh/tháng/năm như hiện có; thứ tự và khối hiện theo vai trò (NgD, phương án B).

### 5.1. Nhân viên: "Hôm nay tôi cần làm gì?"

```
[Việc cần làm (N)]  khẩn lên đầu, tối đa 5 dòng, "Xem tất cả" → /QuanLyNhaTro/ViecCanLam
[Quá hạn] [Chờ thu kỳ T11] [Tiền mặt bạn đã thu T11] [Phòng đang nợ]
[Tình trạng phòng]   [Hoạt động gần đây]
```

- Bỏ: ô "Đã thu", biểu đồ "Xu hướng doanh thu", nút "Xem báo cáo chi tiết" của biểu đồ.
- Giữ: "Hợp đồng hoạt động", "Tỷ lệ lấp đầy" (gộp vào khối Tình trạng phòng hoặc giữ ô, tùy bố cục, không đổi số).
- "Hoạt động gần đây" giữ như cũ (từng khoản thu của chi nhánh, nhân viên vốn xem được ở Lịch sử thanh toán).

### 5.2. Admin: "Cần tôi duyệt gì, tiền thế nào?"

```
[Chờ bạn duyệt (N)]
[Đã thu kỳ T11] [Quá hạn] [Chờ thu kỳ T11] [Phòng đang nợ]
[Tiền mặt nhân viên đang giữ T11]  một dòng mỗi người thu tiền mặt trong tháng
[Xu hướng doanh thu 12 tháng]      [Tình trạng phòng]
[Việc cần làm của nhân viên (N)]   thu gọn, bấm để mở
```

**Chờ bạn duyệt** (lấy từ `ViecCanLamTongHopDto`, không thêm truy vấn mới):
- `HD2` "Hóa đơn chờ Admin chốt", số lượng và link sẵn có.
- Lối tắt "Đối soát giao dịch SePay" → `/QuanLyNhaTro/GiaoDichSePay`, **không có số** (NgD, xem 6.3).
- Không có việc nào: hiện "Không có việc chờ duyệt".

Khối "Việc cần làm của nhân viên" của Admin không lặp lại việc đã nằm trong "Chờ bạn duyệt" (`HD2`).

**Tiền mặt nhân viên đang giữ:** gom theo `NguoiXacNhanId` (tên hiển thị `NguoiDung.TenDangNhap`) theo định nghĩa "Tiền mặt đã thu" ở mục 3, trong chi nhánh đang lọc và phạm vi của Admin; cột: Người thu, Số khoản, Tổng tiền. Có cả dòng của chính Admin nếu Admin tự thu. Không có khoản nào: "Tháng này chưa có khoản tiền mặt nào". Dòng tổng cuối bảng.

### 5.3. Ô số bấm được (giữ `chiNhanhId` đang chọn; không chọn thì bỏ tham số)

| Ô | Đích |
| --- | --- |
| Quá hạn | `/QuanLyNhaTro/ViecCanLam#hoa-don-qua-han` (đích có sẵn của việc `TT2`) |
| Chờ thu kỳ T{m}/{y} | `/QuanLyNhaTro/QuanLyHoaDon?thang={m}&nam={y}&trangThaiPhatHanh=3&trangThai=-2&chiNhanhId=…` (giá trị `-2` mới, mục 5.4) |
| Phòng đang nợ | `/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam=0&trangThaiPhatHanh=3&trangThai=-2&chiNhanhId=…` |
| Tiền mặt bạn đã thu | `/QuanLyNhaTro/LichSuThanhToan?nguoiThu=toi&phuongThuc=tien-mat&tuNgay={đầu tháng}&denNgay={cuối tháng}&chiNhanhId=…` |
| Đã thu kỳ (Admin) | `/QuanLyNhaTro/BaoCao?tab=doanh-thu&chiNhanhId=…&tuThang={m}&tuNam={y}&denThang={m}&denNam={y}` |
| Dòng người trong "Tiền mặt nhân viên đang giữ" | `/QuanLyNhaTro/LichSuThanhToan?nguoiThu={NguoiDungId}&phuongThuc=tien-mat&tuNgay=…&denNgay=…&chiNhanhId=…` |

Ô bấm được có con trỏ tay và `aria-label` mô tả đích. Đổi bộ lọc trên Bảng điều khiển (AJAX `GetAjaxData`) thì cập nhật luôn các link.

### 5.4. Bộ lọc "Chưa thu đủ" ở màn Hóa đơn

Thêm giá trị `trangThai = -2` ("Chưa thu đủ" = `ChuaThanhToan` hoặc `ThanhToanMotPhan`) vào `HoaDonStore.GetHoaDonsDataTableAsync` và ô `#filterTrangThai` của `HoaDon/Index.cshtml` (đọc từ URL như các tham số đang có). Giá trị cũ giữ nguyên nghĩa.

### 5.5. `GetAjaxData`

Trả thêm `coXemDoanhThu`, `tienMatCuaToi { soKhoan, tongTien }`, và với Admin `tienMatNhanVien [ { nguoiDungId, ten, soKhoan, tongTien } ]`, `choDuyet`. Với nhân viên các trường doanh thu (`doanhThuThangNay`, `chartData`, `chartDataChoThu`) là `null`.

## 6. Lịch sử thanh toán (`/QuanLyNhaTro/LichSuThanhToan`)

### 6.1. Nhãn và ô tổng (Admin và nhân viên, NgD)
- "TỔNG DOANH THU" → **"Tiền đã nhận"**, chú thích "Theo ngày nhận tiền".
- Ô theo kênh: Tiền mặt, Chuyển khoản, SePay, VNPay, Cấn trừ cọc (theo mục 3). "SỐ GIAO DỊCH" giữ.
- Khi lọc theo kỳ (6.2): ô đầu đổi thành **"Đã thu kỳ T{m}/{y}"**, chú thích "Theo kỳ hóa đơn, khớp Báo cáo".

### 6.2. Bộ lọc mới
- **Kỳ hóa đơn** (tháng + năm của hóa đơn). Có kỳ thì chỉ lấy giao dịch của hóa đơn kỳ đó, **đã gửi và chưa xóa** (đúng điều kiện `BaoCaoStore.HoaDonDaGui`), bỏ qua khoảng ngày. Không chọn kỳ thì hành vi như cũ. **Chỉ Admin** (NgD, 11.4): ô tổng khi lọc kỳ chính là "Đã thu kỳ" của Báo cáo, nên nhân viên có bộ lọc này sẽ đọc được doanh thu theo kỳ. Nhân viên gửi `kyThang`/`kyNam` thì máy chủ bỏ qua (lọc như không có kỳ).
- **Người thu**: `""` tất cả; `toi` người đang đăng nhập; số `NguoiDungId` (dùng cho link của Admin); `he-thong` khoản không có người xác nhận (SePay/VNPay). Nhân viên chỉ có lựa chọn Tất cả / Tôi / Hệ thống trên giao diện.
- **Phương thức** đổi thành **Kênh thu**: Tất cả, Tiền mặt, Chuyển khoản, SePay, VNPay, Cấn trừ cọc. Tham số URL dạng chữ (`tien-mat`, `chuyen-khoan`, `sepay`, `vnpay`, `can-tru-coc`).
- Đọc tham số từ URL khi mở trang: `kyThang`, `kyNam`, `nguoiThu`, `phuongThuc`, `tuNgay`, `denNgay`, `chiNhanhId`.
- Mọi bộ lọc vẫn trong phạm vi chi nhánh của người dùng (`allowedBranchIds`).

### 6.3. Chuyển khoản chưa khớp
Giao dịch SePay không ghép được hóa đơn hiện **không lưu DB** (chỉ báo Telegram, chống trùng bằng `MemoryCacheTransferAlertDeduplicator`; màn Giao dịch SePay gọi API SePay khi bấm đồng bộ). Vì vậy đợt này Bảng điều khiển chỉ có lối tắt không số (5.2). Hiện số cần bảng mới: tính năng riêng, ngoài phạm vi.

### 6.4. JS
Tách script trong `LichSuThanhToan/Index.cshtml` ra `wwwroot/js/lich-su-thanh-toan.js` (nạp bằng `asp-append-version`), cấu hình từ máy chủ qua `window.lichSuThanhToanConfig` (`laAdmin`, các giá trị lọc ban đầu). Giữ nguyên id phần tử, cột DataTables và endpoint `/LichSuThanhToan/GetList`, `/GetStats`, `/HuyThanhToan/{id}`.

## 7. Báo cáo (`/QuanLyNhaTro/BaoCao`)

- Phân quyền tab theo mục 4.
- Trang nhận thêm `tuThang`, `tuNam`, `denThang`, `denNam` trên URL để đặt sẵn bộ lọc kỳ (hiện `BaoCaoController.Index` chỉ nhận `tab`, `chiNhanhId` và bộ lọc mặc định là 12 kỳ gần nhất). Tham số thiếu hoặc sai thì dùng mặc định như cũ.
- Bảng "Theo kỳ" của tab Doanh thu: mỗi dòng kỳ (trừ dòng tổng) có link "Xem giao dịch" → `/QuanLyNhaTro/LichSuThanhToan?kyThang={m}&kyNam={y}&chiNhanhId={chi nhánh đang lọc}`. Bảng "Theo chi nhánh" không có link (khoảng nhiều kỳ, Lịch sử thanh toán lọc một kỳ).
- Không đổi số liệu, không đổi file Excel.

## 8. Ràng buộc kỹ thuật

- Bốn tầng, test kiến trúc giữ nguyên. Hàm store mới nhận `allowedBranchIds`; service nhận `actorId` làm tham số đầu.
- **KHÔNG ĐỔI SCHEMA**, không migration.
- Thời gian: lưu UTC; tháng/ngày lọc là giờ Việt Nam, đổi qua `Helpers/VietnamTime.cs` hoặc cách đang dùng trong `LichSuThanhToanStore`. Không `DateTime.Now`.
- Tiền hiển thị qua `VndFormat.Money` (Razor) hoặc `Intl.NumberFormat('vi-VN')` (JS).
- Giao diện theo skill `giao-dien-quan-ly` (`.agents/skills/giao-dien-quan-ly/SKILL.md`); chạy `node .agents/skills/giao-dien-quan-ly/scripts/soat-giao-dien.js` cho mọi view/JS/CSS đã sửa, sửa hết mục LỖI; chế độ sáng, tối, 375px.
- Không thêm thư viện. Không đổi `Program.cs`, `.csproj`, `appsettings*`.
- Hiệu năng: Bảng điều khiển của nhân viên không được nhiều lệnh SQL hơn hiện tại (`QueryBudgetTests`); thêm tối đa 2 truy vấn cho Admin (tiền mặt theo người, theo tháng).

## 9. Nghiệm thu và test

| # | Kiểm | Test |
| --- | --- | --- |
| 1 | Nhân viên: HTML Bảng điều khiển không có "Đã thu kỳ", không có biểu đồ doanh thu; `GetAjaxData` trả `doanhThuThangNay`, `chartData`, `chartDataChoThu` là `null`, `coXemDoanhThu = false` | Web.IntegrationTests |
| 2 | Nhân viên gọi `BaoCao/DuLieu?tab=doanh-thu` và `XuatExcel?tab=doanh-thu` → 403; tab khác như cũ | Web.IntegrationTests |
| 3 | Nhân viên gọi `/LichSuThanhToan/GetStats` → 200, chỉ cộng giao dịch của chi nhánh được phân công; truyền `chiNhanhId` ngoài phạm vi thì ra 0. Nhân viên gửi `kyThang`/`kyNam` cho `GetList`/`GetStats` thì kết quả như không có kỳ | Web.IntegrationTests |
| 4 | Cùng dữ liệu: "Đã thu kỳ" của Bảng điều khiển = cột Đã thu của dòng kỳ trong Báo cáo = tổng `GetList` lọc kỳ đó, kể cả có hóa đơn đã hủy, khoản đã hủy ghi nhận, cấn trừ cọc, SePay | Infrastructure hoặc Web.IntegrationTests |
| 5 | Tiền mặt theo người: chỉ `TienMat`, chỉ tháng đang chọn theo giờ VN (biên 17:00Z), bỏ khoản đã hủy, bỏ khoản không có người xác nhận, đúng phạm vi chi nhánh | Infrastructure.IntegrationTests + Application.UnitTests |
| 6 | Lọc kênh: SePay/VNPay tách khỏi Chuyển khoản, khớp `PaymentChannels.Of` | Infrastructure.IntegrationTests |
| 7 | `trangThai=-2` ở danh sách hóa đơn lấy đúng Chưa thanh toán + Một phần | Infrastructure.IntegrationTests |
| 8 | Admin thấy "Chờ bạn duyệt" với số của `HD2`; nhân viên không có khối này | Web.IntegrationTests |
| 9 | Link các ô mang đúng tham số; trang đích đọc tham số và lọc đúng | Web.IntegrationTests (HTML) + kiểm trình duyệt |
| 10 | Lệnh SQL Bảng điều khiển nhân viên không tăng | `QueryBudgetTests` |
| 11 | Script soát giao diện 0 LỖI; kiểm trình duyệt sáng/tối/375px trên DB test (tài khoản `BranchScopeKit`), ảnh lưu `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo` | thủ công |

Lệnh: `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests`, rồi lần lượt (không song song) `Infrastructure.IntegrationTests`, `Web.IntegrationTests` với `QLCTPT_TEST_CONNECTION_STRING` trỏ DB `*_test`.

## 10. Thứ tự làm (mỗi phần một commit)

1. Phân quyền doanh thu (mục 4): Application + Web, test 1–3.
2. Định nghĩa, nhãn, kênh thu (mục 3, 6.1): test 4, 6.
3. Bảng điều khiển theo vai trò (mục 5): test 5, 7, 8, 9, 10.
4. Lịch sử thanh toán lọc kỳ/người, tách JS, link từ Báo cáo (mục 6.2, 6.4, 7): test 4, 9.

Nhánh: `feature/bang-dieu-khien-van-hanh` (đã có, tách từ `feature/toi-uu-hieu-nang`). `/ship` không chạy trên `master`. Khi gộp về `master` thì gộp `feature/toi-uu-hieu-nang` trước.

## 11. Các điểm đã chốt (người dùng, 09/11/2026)

1. **Chuyển khoản chưa khớp:** khối "Chờ bạn duyệt" chỉ có lối tắt "Đối soát giao dịch SePay", không có số. Lưu giao dịch chưa khớp (đổi schema) là tính năng riêng, không làm đợt này.
2. **Ô tổng của Lịch sử thanh toán:** nhân viên vẫn xem được (Tiền đã nhận, số giao dịch, theo kênh), trong phạm vi chi nhánh của mình, để rà soát. Doanh thu theo kỳ trên Bảng điều khiển và tab Doanh thu của Báo cáo vẫn chỉ Admin.
3. **Nhánh:** `feature/bang-dieu-khien-van-hanh` người dùng đã tách từ `feature/toi-uu-hieu-nang`.
4. **Bộ lọc Kỳ hóa đơn ở Lịch sử thanh toán chỉ Admin có.** Lý do: ô tổng khi lọc theo kỳ bằng đúng doanh thu kỳ, mà doanh thu kỳ chỉ Admin thấy (mục 4). Nhân viên giữ lọc theo ngày, người thu, kênh thu và ô tổng theo ngày (điểm 2); nhân viên gửi tham số kỳ thì máy chủ bỏ qua.

## 12. Việc để lại cho người thực thi

Bố cục chi tiết từng khối (cột lưới, màu ô theo bảng màu Tabler, icon), câu chữ của trạng thái rỗng ngoài các câu đã ghi, cách gom hai truy vấn tiền mặt của Admin, tên DTO/hàm mới: theo quy ước file đang có trong cùng feature.
