# Đặc tả: Trung tâm việc cần làm

- Phiên bản: 1.2, ngày 08/10/2026. Người dùng đã duyệt bản 1.0; 1.1 cập nhật §12 (trang Sự cố đã sửa ở `a98be85`), thêm đường dẫn mockup và cách triển khai (§13). 1.2 (review cuối, người dùng xác nhận ngày 09/10/2026): CS1 tính "đã chốt" là kỳ chỉ số `DaDuyet` (§5); lưới KPI 3 thẻ mỗi hàng dưới 1400 px (§7.1). Xem [review.md](review.md) R1, R8.
- Mốc lộ trình vận hành: bước 3, sau bước 2 "AI + thông báo theo chi nhánh" (`8295e36`).
- Nhánh Git: `feature/viec-can-lam`.
- Phạm vi:
  - Danh sách việc cần làm suy ra từ dữ liệu, có phân quyền theo vai trò và chi nhánh.
  - Khối việc trên Dashboard, trang `/QuanLyNhaTro/ViecCanLam`, badge trên menu.
  - Sửa số liệu KPI của Dashboard.
- Ngoài phạm vi:
  - Lưu việc thành bản ghi, giao việc cho người cụ thể, đánh dấu xong, ẩn hoặc nhắc lại.
  - Đẩy badge theo thời gian thực qua SignalR.
  - Đổi ký hiệu tiền tệ ở các màn khác.
- **Không đổi schema.**

---

## 1. Mục tiêu

1. Admin và nhân viên mở Dashboard là biết ngay việc gì cần làm, việc nào gấp, và bấm một lần là tới đúng màn xử lý.
2. Mỗi người chỉ thấy việc thuộc chi nhánh được phân công và việc mình có quyền làm.
3. Số liệu KPI của Dashboard khớp với màn Công nợ: hóa đơn khách chưa nhận không bị tính là nợ.
4. Việc tự biến mất khi đã xử lý xong ở màn nghiệp vụ, không cần thao tác thêm.

## 2. Hiện trạng (kiểm tra trong code ngày 06/10/2026)

| # | Vấn đề | Vị trí |
|---|---|---|
| H1 | Khối việc cần làm chỉ có 3 loại: chưa chốt điện nước, hợp đồng sắp hết hạn, hóa đơn chưa thu. Không sắp theo ưu tiên, không phân biệt vai trò | `DashboardService.cs` dòng 106–213 |
| H2 | Mục "Hóa đơn chưa thu" tạo một dòng cho mỗi tháng × chi nhánh, nên danh sách dài ra theo thời gian | `DashboardService.cs` dòng 200–213 |
| H3 | Các hàng đợi đã chạy thật nhưng Dashboard không hiện: ảnh chỉ số chờ xác nhận, hóa đơn nháp, hóa đơn chờ chốt, đã chốt chưa gửi, minh chứng chờ đối chiếu, sự cố, hóa đơn quá hạn | — |
| H4 | "Chờ thu", cột "Chờ thu" trên biểu đồ, "Phòng đang nợ" và "Hóa đơn chưa thu" tính cả hóa đơn nháp, chờ duyệt và đã chốt chưa gửi. Màn Công nợ chỉ tính hóa đơn đã gửi | `DashboardService.cs` dòng 75, 90; `DashboardStore.CountUnpaidRoomsAsync`, `GetUnpaidInvoicesGroupedAsync` |
| H5 | Truy vấn chỉ số đã ghi nhận không lọc theo chi nhánh (kết quả vẫn đúng nhờ so theo phòng, nhưng tải dữ liệu của mọi chi nhánh) | `DashboardStore.GetRecordedUtilitiesAsync` |
| H6 | JS của Dashboard ghép HTML bằng template string từ dữ liệu server, trong đó có tên chi nhánh và tên khách do người dùng nhập. Đây là lỗ hổng XSS | `Views/Dashboard/Index.cshtml` dòng 326–371 |
| H7 | Tỷ lệ lấp đầy đã tính sẵn nhưng không hiển thị | `DashboardService.cs`, `Index.cshtml` |
| H8 | Trang Hóa đơn chưa đọc tham số `trangThaiPhatHanh` trên URL | `Views/HoaDon/Index.cshtml` dòng 405–414 |

## 3. Nguyên tắc bất biến

1. **Việc suy ra từ dữ liệu.** Không có bảng việc. Số lượng tính từ trạng thái hiện tại của hóa đơn, ảnh chỉ số, minh chứng, sự cố và hợp đồng.
2. **Fail closed.** Actor không hợp lệ, nhân viên chưa được phân công, hoặc chọn chi nhánh ngoài phạm vi: kết quả rỗng, không báo lỗi.
3. **Lọc quyền ở service, không ở giao diện.** Nhân viên không nhận việc chỉ-Admin trong cả JSON lẫn HTML.
4. **Thời gian lưu UTC.** So hạn bằng UTC lấy từ `TimeProvider`. Kỳ tháng, ngày hạn và số ngày quá hạn tính theo giờ Việt Nam (UTC+7).
5. Chỉ đọc dữ liệu, không ghi gì vào hệ thống.

## 4. Phân quyền

- Gọi `IEmployeeAccessService.GetScopeAsync(actorId)` **một lần** cho mỗi yêu cầu.
- **Admin:** thấy mọi việc của mọi chi nhánh.
- **Nhân viên:** thấy việc trong các chi nhánh được phân công, trừ việc có mã hành động chỉ-Admin (`EmployeeActionCodes.IsAdminOnly`). Quy tắc này khớp với `EmployeeAccessService.CanPerformAsync`.
- **Khách thuê:** không truy cập được trang hay endpoint nào của tính năng này.

## 5. Danh mục việc (đã chốt)

Mức ưu tiên: **Khẩn** (đỏ), **Cần làm** (cam), **Theo dõi** (xanh).

| Mã | Nhóm | Việc | Điều kiện | Mã hành động | Ưu tiên | Một dòng cho |
|---|---|---|---|---|---|---|
| CS1 | Chỉ số | Phòng chưa chốt điện nước | Giữ cách quét kỳ hiện tại: kỳ mục tiêu theo `DashboardSettings.ChotDienNuocDay`, quét 6 kỳ tính đến kỳ mục tiêu. Phòng có hợp đồng đang hoạt động bắt đầu từ kỳ đó trở về trước, chưa có kỳ chỉ số **đã chốt** (chưa xóa, `TrangThaiGhiNhan = DaDuyet`, khớp `IsDaChot` của màn Chốt điện nước; phát sinh hóa đơn cũng chỉ nhận kỳ `DaDuyet`) | `Meter.Upload` | Kỳ mục tiêu: Cần làm. Kỳ cũ hơn: Khẩn | Mỗi kỳ |
| CS2 | Chỉ số | Ảnh chỉ số chờ xác nhận | Ảnh chưa xóa, trạng thái `DocDuoc`, `KhongDocDuoc` hoặc `Loi`; kỳ chỉ số chưa xóa | `Meter.Review` | Cần làm | Mỗi kỳ |
| HD1 | Hóa đơn | Hóa đơn nháp chưa gửi duyệt | Chưa xóa, `TrangThaiPhatHanh = Nhap` | `Invoice.Submit` | Cần làm | Cả nhóm |
| HD2 | Hóa đơn | Hóa đơn chờ Admin chốt | Chưa xóa, `ChoDuyet` | `Invoice.Finalize` (chỉ Admin) | Cần làm | Cả nhóm |
| HD3 | Hóa đơn | Hóa đơn đã chốt, chưa gửi khách | Chưa xóa, `DaChot`, `TrangThaiHoaDon ≠ DaThanhToan` | `Invoice.Send` | Cần làm | Cả nhóm |
| TT1 | Thu tiền | Minh chứng chuyển khoản chờ đối chiếu | Minh chứng chưa xóa, `ChoXacNhan`; yêu cầu thanh toán và hóa đơn chưa xóa | `Payment.Review` | Khẩn | Cả nhóm |
| TT2 | Thu tiền | Hóa đơn quá hạn | Chưa xóa, `DaGui`, `TrangThaiHoaDon ≠ DaThanhToan`, `HanThanhToan` có giá trị và nhỏ hơn hiện tại | `Invoice.Read` | Khẩn | Cả nhóm |
| TT3 | Thu tiền | Hóa đơn đã gửi, chưa thu đủ | Như TT2 nhưng `HanThanhToan` rỗng hoặc chưa tới | `Invoice.Read` | Theo dõi | Cả nhóm |
| KH1 | Khách thuê & HĐ | Sự cố chờ tiếp nhận | Chưa xóa, `ChoTiepNhan` | — | Khẩn nếu có sự cố gửi quá 24 giờ, còn lại Cần làm | Cả nhóm |
| KH2 | Khách thuê & HĐ | Sự cố xử lý quá lâu | Chưa xóa, `DangXuLy`, `NgayGui` cách hiện tại hơn 7 ngày | — | Cần làm | Cả nhóm |
| KH3 | Khách thuê & HĐ | Hợp đồng sắp hết hạn | Chưa xóa, `DangHoatDong`, ngày kết thúc từ hôm nay đến hôm nay + 30 ngày | — | Cần làm nếu có hợp đồng còn ≤ 7 ngày, còn lại Theo dõi | Cả nhóm |

**Quy tắc chung:**

1. Việc có số lượng bằng 0 thì không hiện.
2. Dòng gom nhiều chi nhánh lấy mức ưu tiên cao nhất trong các bản ghi của nó. Ví dụ: KH1 có 1 sự cố quá 24 giờ thì cả dòng là Khẩn, mô tả ghi rõ số sự cố quá 24 giờ.
3. Mô tả dòng liệt kê số lượng theo chi nhánh, nhiều nhất 3 chi nhánh (số lớn trước), phần còn lại ghi "và N chi nhánh khác". TT2 và TT3 ghi thêm tổng số còn nợ.
4. **Số còn nợ** = `TongTien` − tổng `SoTienThanhToan` của các bản ghi `LichSuThanhToan` chưa xóa. Đây là cách tính đang dùng ở màn Công nợ.
5. Ngưỡng nằm trong options `ViecCanLamSettings`, có giá trị mặc định: 24 giờ (KH1), 7 ngày (KH2), 30 và 7 ngày (KH3), 6 kỳ (CS1), 50 dòng (bảng quá hạn). Section cấu hình là tùy chọn; thiếu thì dùng mặc định.

### 5.1 Link đi tới

Thêm `chiNhanhId` khi người dùng đang lọc một chi nhánh, hoặc khi dòng chỉ có đúng một chi nhánh.

| Mã | Link |
|---|---|
| CS1, CS2 | `/QuanLyNhaTro/ChotDienNuoc?thang={m}&nam={y}` |
| HD1 / HD2 / HD3 | `/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam={năm của kỳ cũ nhất}&trangThaiPhatHanh=0 / 1 / 2` |
| TT1 | `/QuanLyNhaTro/DoiChieuThanhToan` |
| TT2 | `/QuanLyNhaTro/ViecCanLam#hoa-don-qua-han` |
| TT3 | `/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam={năm của kỳ cũ nhất}&trangThaiPhatHanh=3` |
| KH1 / KH2 | `/QuanLyNhaTro/YeuCauSuCo?trangThai=0 / 1&soThang=0` |
| KH3 | `/QuanLyNhaTro/QuanLyHopDong` |

Link là đường dẫn tương đối do server tạo. Trang Hóa đơn được sửa để đọc `trangThaiPhatHanh` từ URL (H8).

### 5.2 Sắp xếp

Theo thứ tự: Khẩn → Cần làm → Theo dõi. Cùng mức thì số lượng lớn hơn đứng trước. Cùng số lượng thì theo mã việc. CS1 và CS2 nhiều kỳ thì kỳ cũ hơn đứng trước.

## 6. KPI của Dashboard

| Thẻ | Định nghĩa | Thay đổi |
|---|---|---|
| Đã thu T{m}/{y} | Tổng `LichSuThanhToan` chưa xóa của hóa đơn kỳ đang chọn | Giữ nguyên |
| Chờ thu | Tổng số còn nợ của hóa đơn kỳ đang chọn, chỉ hóa đơn `DaGui` chưa thanh toán đủ. Chú thích: "hóa đơn đã gửi khách" | Chỉ tính `DaGui` |
| Quá hạn | Tổng số còn nợ và số hóa đơn của TT2, **mọi kỳ**. Chú thích: "{n} hóa đơn" | Mới |
| Phòng đang nợ | Số phòng khác nhau có hóa đơn `DaGui` chưa thanh toán đủ, mọi kỳ. Chú thích: "từ trước tới nay" | Chỉ tính `DaGui` |
| Hợp đồng hoạt động | Như hiện tại | Giữ nguyên |
| Tỷ lệ lấp đầy | Phòng đã thuê / tổng phòng. Chú thích: "{đã thuê}/{tổng} phòng" | Hiển thị số đã có |

- Cột "Chờ thu" trên biểu đồ 12 tháng chỉ tính `DaGui`. Cột "Đã thu" giữ nguyên.
- Thẻ KPI, biểu đồ và hoạt động gần đây vẫn lọc theo chi nhánh, tháng và năm như hiện tại. **Khối việc chỉ lọc theo chi nhánh**, không phụ thuộc tháng hay năm.

## 7. Giao diện (đã chốt qua mockup)

Mockup đã duyệt: [mockup/dashboard-phuong-an-b.html](mockup/dashboard-phuong-an-b.html) và [mockup/trang-viec-can-lam-phuong-an-a-b.html](mockup/trang-viec-can-lam-phuong-an-a-b.html) (file này có hai phương án; **chỉ làm theo phương án A**). Mockup chỉ quy định bố cục và nội dung; khi làm thật dùng class Tabler và style sẵn có của repo, theo `.agents/rules/frontend_code_review.md`. Số liệu trong mockup là ví dụ.

### 7.1 Dashboard: phương án B

- **Hàng KPI:** 6 thẻ, mỗi thẻ có dòng chú thích nhỏ. Từ 1400 px: 6 thẻ một hàng; 768–1399 px: 3 thẻ mỗi hàng; màn hình hẹp: 2 cột. Cỡ số co theo bề rộng thẻ để số tiền 10 chữ số không tràn.
- **Bên trái (2/3):** biểu đồ doanh thu 12 tháng; dưới nó là hai cột "Tình trạng phòng" và "Hoạt động gần đây".
- **Bên phải (1/3), khối "Việc cần làm (tổng)":**
  - Ô đếm Khẩn, Cần làm, Theo dõi.
  - Tối đa **5 dòng** đầu theo §5.2. Mỗi dòng có chấm màu ưu tiên, tên việc, dòng phụ (nhóm, chi nhánh hoặc số còn nợ) và số lượng.
  - Link "Xem tất cả {tổng} việc →".
  - Không có việc nào thì hiện "Không có việc cần xử lý" kèm dấu tích xanh.
  - Màn hình hẹp: khối này xuống dưới biểu đồ.
- **Đổi bộ lọc:** gọi AJAX `GetAjaxData` và cập nhật cả khối việc. Dựng DOM bằng `textContent` hoặc hàm escape, không ghép HTML từ dữ liệu (sửa H6, áp dụng cho cả hoạt động gần đây).

### 7.2 Trang `/QuanLyNhaTro/ViecCanLam`: phương án A

- **Đầu trang:** tiêu đề "Việc cần làm ({tổng})" và dropdown chi nhánh (mặc định "Tất cả chi nhánh" trong phạm vi). Đổi chi nhánh thì tải lại trang với `?chiNhanhId=`.
- **Nút lọc nhóm:** Tất cả, Chỉ số, Hóa đơn, Thu tiền, Khách thuê & HĐ. Lọc ở trình duyệt, không gọi lại server.
- **Ba khối Khẩn, Cần làm, Theo dõi:** mỗi khối liệt kê toàn bộ việc ở mức đó. Mỗi dòng có tên việc, nhãn nhóm, mô tả theo chi nhánh, số lượng và nút "Xử lý" (hoặc "Xem"). Khối rỗng thì ẩn.
- **Bảng "Hóa đơn quá hạn"** (`id="hoa-don-qua-han"`):
  - Cột: mã hóa đơn, phòng · chi nhánh, khách thuê (người đại diện hợp đồng), số còn nợ, hạn thanh toán (dd/MM/yyyy giờ Việt Nam), số ngày quá hạn.
  - Hạn sớm nhất đứng đầu, tối đa 50 dòng. Nếu còn nhiều hơn thì ghi "Còn N hóa đơn quá hạn khác".
  - Bấm mã hóa đơn mở `/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={id}`.
- Trang được render bằng Razor, không ghép HTML bằng JS.

### 7.3 Menu và badge

- Thêm mục "Việc cần làm" (icon `ti ti-checklist`) ngay dưới "Dashboard" trong `MenuService`.
- Badge do JS gọi `GET /QuanLyNhaTro/ViecCanLam/TomTat` một lần mỗi lần tải trang. Badge hiện tổng số; nền đỏ khi có việc Khẩn, nền cam khi không có; ẩn khi bằng 0 hoặc khi tải lỗi (ghi `console.error`).

### 7.4 Định dạng tiền

Trên Dashboard và trang Việc cần làm, số tiền hiển thị đầy đủ, có dấu chấm ngăn cách hàng nghìn và đơn vị "đ". Ví dụ: `3.590.000 đ`. Áp dụng cho thẻ KPI, trục và tooltip biểu đồ, hoạt động gần đây, mô tả việc và bảng quá hạn. Các màn khác giữ `₫`.

## 8. Endpoint

| Endpoint | Vai trò | Trả về |
|---|---|---|
| `GET /QuanLyNhaTro/ViecCanLam?chiNhanhId=` | Admin, NhanVien | Trang HTML (§7.2) |
| `GET /QuanLyNhaTro/ViecCanLam/TomTat` | Admin, NhanVien | JSON `{ tongSo, soKhan, soCanLam, soTheoDoi }`, mọi chi nhánh trong phạm vi |
| `GET /QuanLyNhaTro/Dashboard/GetAjaxData` | Admin, NhanVien | Như hiện tại, thêm `tienQuaHan`, `soHoaDonQuaHan`, `viecCanLam { tongSo, soKhan, soCanLam, soTheoDoi, items (≤ 5) }`. Bỏ `toDos` |

- Tổng số = tổng `SoLuong` của mọi dòng; các số theo mức tính tương tự.
- Khách thuê gọi các endpoint trên bị chặn như mọi controller khác của cổng quản lý.

## 9. Kiến trúc (đã chốt: feature riêng)

- **Application `Features/ViecCanLam`:**
  - DTO: `ViecCanLamDto`, `ViecCanLamTongHopDto`, `HoaDonQuaHanDto`. Enum `NhomViec`, `MucUuTienViec` đặt ở Application để Web dùng được.
  - `IViecCanLamStore`: mỗi loại việc một truy vấn đếm, gom theo chi nhánh hoặc kỳ. Mọi truy vấn nhận `branchId?` và `allowedBranchIds`.
  - `IViecCanLamService`: `GetTongHopAsync(actorId, branchId?, ct)` và `GetHoaDonQuaHanAsync(actorId, branchId?, ct)`. Mỗi loại việc khai báo mã hành động; service lọc việc chỉ-Admin, tính ưu tiên, sắp xếp, dựng mô tả và link. Thời gian lấy từ `TimeProvider`.
  - `ViecCanLamSettings` trong `Common/Configurations`, bind tại `AddInfrastructure` giống `DashboardSettings`.
- **Dashboard:**
  - `DashboardService` bỏ phần dựng việc cần làm (dòng 106–213) và thêm KPI quá hạn.
  - `IDashboardStore` bỏ các method đã chuyển sang `IViecCanLamStore`.
  - Truy vấn KPI chỉ tính `DaGui` theo §6.
- **Infrastructure:** `ViecCanLamStore` (EF). Truy vấn chỉ số đã ghi nhận có lọc chi nhánh (H5).
- **Web:**
  - `ViecCanLamController : AdminBaseController`.
  - `DashboardController` gọi cả hai service.
  - Sửa view Dashboard, thêm view Việc cần làm, thêm mục trong `MenuService`, thêm JS badge trong `_Sidebar`, thêm đọc tham số URL ở trang Hóa đơn.
  - Web không tham chiếu Domain.
- **Hiệu năng:** khoảng 10 truy vấn `COUNT … GROUP BY` chạy tuần tự mỗi lần tải. Không thêm index, không cache. Nếu sau này chậm thì cân nhắc cache ngắn theo actor.

Chi tiết lớp và file nằm trong `plan.md`.

## 10. Kiểm thử (viết test trước, theo TDD)

| Tầng | Nội dung |
|---|---|
| Application.UnitTests (`ViecCanLamServiceTests`, store và access giả) | Scope null, chưa phân công, hoặc chi nhánh ngoài phạm vi trả rỗng. Nhân viên không có HD2, Admin có. Ưu tiên quanh các ngưỡng: KH1 23 giờ 59 phút và 24 giờ; KH3 7 và 8 ngày; CS1 kỳ mục tiêu so với kỳ cũ. Kỳ mục tiêu CS1 trước và sau `ChotDienNuocDay`, kể cả qua năm. Sắp xếp theo §5.2. Mô tả tối đa 3 chi nhánh. Link có hoặc không có `chiNhanhId`. Tổng số theo mức. Giới hạn 50 dòng quá hạn. Việc có số lượng 0 không hiện |
| Application.UnitTests (`BranchScopeArchitectureTests`) | Thêm `IViecCanLamService` vào danh sách service phải nhận `actorId` làm tham số đầu |
| Infrastructure.IntegrationTests (`ViecCanLamStoreTests`, `DashboardStore`) | Mỗi truy vấn lọc đúng chi nhánh và bỏ bản ghi đã xóa mềm. Chỉ `DaGui` tính là nợ. `DaChot` đã thu đủ không vào HD3. Ảnh `DaThayThe`, `DaXacNhan`, `CanChupLai` không vào CS2. Minh chứng `DaXacNhan`, `TuChoi` không vào TT1. Biên `HanThanhToan` đúng bằng hiện tại. Số còn nợ bỏ bản ghi thanh toán đã xóa. KPI "Chờ thu" và "Phòng đang nợ" bỏ hóa đơn chưa gửi |
| Web.IntegrationTests | Nhân viên chi nhánh A chỉ thấy việc của A trên trang, trong `TomTat` và `GetAjaxData`. Nhân viên không thấy "chờ Admin chốt" trong HTML lẫn JSON. Khách thuê bị chặn khỏi `/QuanLyNhaTro/ViecCanLam` và `/TomTat`. Các test Dashboard hiện có vẫn PASS |
| ArchitectureBoundaryTests | Giữ nguyên ranh giới tầng |

## 11. Điều kiện nghiệm thu

1. Admin thấy đủ các loại việc ở §5 khi có dữ liệu tương ứng; nhân viên thấy tất cả trừ HD2.
2. Nhân viên chi nhánh A không thấy số lượng hay tên chi nhánh B ở khối việc, trang Việc cần làm và badge.
3. Bấm mỗi dòng việc mở đúng màn xử lý với bộ lọc tương ứng.
4. Xử lý xong việc (ví dụ chốt hóa đơn, xác nhận minh chứng) rồi tải lại trang thì số lượng giảm tương ứng.
5. "Chờ thu" và "Phòng đang nợ" không đổi khi tạo hóa đơn nháp mới; chỉ tăng sau khi hóa đơn được gửi khách.
6. Tên chi nhánh chứa `<script>` hiện ra như văn bản, không chạy script.
7. Số tiền trên Dashboard và trang Việc cần làm có dạng `3.590.000 đ`.
8. `dotnet test` toàn solution PASS trên DB `_test`; `has-pending-model-changes` báo không có thay đổi; không có migration mới.

## 12. Rủi ro và giới hạn chấp nhận

- **Trang Sự cố:** đã được giới hạn theo chi nhánh của nhân viên ở commit `a98be85` (nhánh `feature/su-co-chi-nhanh`, đã fast-forward vào `feature/viec-can-lam`). Link KH1/KH2 giữ nguyên tham số `chiNhanhId`, `trangThai`, `soThang`.
- Hóa đơn cũ được migration chuyển sang `DaChot` mà chưa thu đủ sẽ hiện ở HD3 cho tới khi được gửi khách hoặc thu xong. Đây là hành vi mong muốn: những hóa đơn này đúng là chưa tới tay khách.
- Badge chỉ cập nhật khi tải trang, không theo thời gian thực.
- Mỗi lần tải Dashboard, trang Việc cần làm hay bất kỳ trang nào (vì có badge) đều chạy các truy vấn đếm. Quy mô hiện tại chấp nhận được.

## 13. Quyết định đã chốt (06/10/2026)

1. Việc suy ra từ dữ liệu, không lưu bản ghi việc, không đổi schema.
2. Có cả khối trên Dashboard lẫn trang riêng, kèm badge trên menu.
3. Đợt này gồm đủ 4 nhóm: Chỉ số, Hóa đơn, Thu tiền, Khách thuê & HĐ.
4. Sửa lệch số liệu KPI (chỉ tính `DaGui`); thêm thẻ Quá hạn và thẻ Tỷ lệ lấp đầy.
5. Kiến trúc: feature riêng `Features/ViecCanLam`; KPI vẫn ở Dashboard.
6. Danh mục việc, ngưỡng và mức ưu tiên theo §5.
7. Dashboard theo phương án B (khối việc ở cột phải, 5 dòng). Trang Việc cần làm theo phương án A (chia theo mức ưu tiên, có nút lọc nhóm).
8. Số tiền hiển thị đầy đủ, đơn vị "đ", chỉ trên Dashboard và trang Việc cần làm.
9. Tạo nhánh `feature/viec-can-lam` trước khi sửa mã nguồn.
10. Triển khai bằng dây chuyền `/ship` thay cho skill writing-plans, chạy **2 lượt**: đợt A là backend (Application, Infrastructure, KPI, endpoint JSON, test); đợt B là giao diện (Dashboard B, trang A, menu và badge, sửa XSS, định dạng tiền). Kế hoạch và đánh giá của mỗi đợt được chép từ `.bangiao/` vào thư mục module.
11. Giao diện làm theo mockup đã duyệt và `.agents/rules/frontend_code_review.md` (cách a); agent không dùng skill frontend-design.
12. Chỉ commit hoặc push khi người dùng yêu cầu.
13. (Bản 1.2, người dùng xác nhận 09/10/2026) CS1 dùng nghĩa "đã chốt" = `DaDuyet` thay cho "có bản ghi", vì logic cũ có từ trước quy trình duyệt kỳ chỉ số và báo sai so với màn Chốt điện nước.
