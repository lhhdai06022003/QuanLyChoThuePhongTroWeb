# Spec đợt 2: ba màn chính và thanh tab dưới

- Trạng thái: **đã code xong trên nhánh, chờ nghiệm thu** (11/11/2026); người dùng xác nhận cả bốn điểm ở mục 3 theo đề xuất ngày 11/11/2026
- Ngày: 11/11/2026
- Nhánh: `feature/thiet-ke-lai-giao-dien`
- Cách thực thi: `/doi-ngu` (người dùng chọn 11/11/2026). Phương án chi tiết, bảng công việc và tiêu chí nghiệm thu: `.bangiao/doi/phuong-an.md` (ngoài Git); phần thiết kế chép lại ở mục 2 để tài liệu này tự đứng được.

## 1. Vì sao làm

Sau đợt 1 lớp vỏ đã thống nhất nhưng ba màn mở đầu của ba vai trò vẫn như cũ:

- Chủ trọ thấy bốn ô màu đặc cạnh nhau, phải tự cộng mới biết kỳ này thu được bao nhiêu phần.
- Nhân viên thấy danh sách việc nhưng phải bấm vào dòng rồi mới biết sẽ làm gì.
- Khách thuê thấy số tiền nhưng không thấy hạn trả.
- Nhân viên dùng điện thoại phải mở menu 16 mục cho bốn việc làm hằng ngày.
- Bảng "Tiền mặt nhân viên đang giữ" làm Bảng điều khiển Admin tràn ngang ở 375px.

## 2. Thiết kế

1. **Thanh thu tiền (chỉ Admin)** thay hàng ô màu: số đã thu của kỳ, "phải thu" = đã thu + chờ thu, tỷ lệ phần trăm, thanh hai đoạn (đã thu, chờ thu). Bên dưới bốn số liệu nhạt bấm được: Đã thu, Chờ thu kỳ, Quá hạn (mọi kỳ), Phòng đang nợ. Không số nào đổi cách tính.
2. **Nhân viên:** việc đứng đầu; mỗi việc có nút làm luôn (Chốt số, Xem hóa đơn, Thu tiền, Xử lý), việc Khẩn dùng nút chính. Số liệu bên dưới dùng ô nhạt như của Admin.
3. **Thanh tab dưới (dưới 768px):** nhân viên và Admin có Việc, Phòng, Điện nước, Hóa đơn, Thêm (mở menu đầy đủ). Khách thuê có Trang chủ, Hóa đơn, Điện nước, Sự cố, Tôi. Nút trợ lý AI nằm trên thanh tab.
4. **Trang chủ khách thuê:** thẻ "Cần thanh toán" với số tiền, số hóa đơn, hạn trả gần nhất (còn n ngày hoặc quá hạn n ngày), nút thanh toán; ba dòng lối tắt Điện nước, Báo sự cố, Hợp đồng. Bỏ lưới bốn thẻ hiện tại.
5. **Sửa tràn ngang** bảng tiền mặt nhân viên; màu biểu đồ đọc từ biến của nền.

Không làm: đổi cách tính, controller của cổng quản lý, quyền, schema, các màn khác, cổng khách vãng lai.

Thất bại dù chạy được: nhân viên thấy bất kỳ số doanh thu nào; một `id` mà `dashboard.js` hoặc test đang dùng biến mất; tỷ lệ phần trăm trên thanh không bằng đã thu ÷ (đã thu + chờ thu); khách thuê thấy hạn trả lệch ngày do múi giờ.

## 3. Điểm người dùng đã xác nhận (11/11/2026)

1. Thêm trường chỉ đọc `HanThanhToanUtc` vào DTO `HoaDonRes` (tầng Application, module hóa đơn) để trang chủ khách thuê hiện hạn trả. Không đổi cách tính tiền, không thêm truy vấn.
2. Thanh thu tiền chỉ có hai đoạn; "Quá hạn" đứng riêng vì con số này tính trên mọi kỳ, không riêng kỳ đang chọn. Khác bản mẫu đã trình.
3. Thanh tab khách thuê đổi mục thứ năm từ "Hợp đồng" sang "Tôi" (hồ sơ).
4. Cho phép đội commit cục bộ từng việc trên nhánh này (không push), như đợt 1.

## 4. Kết quả (11/11/2026)

Thực thi bằng `/doi-ngu` (Opus trưởng nhóm, Sonnet thực thi; V4 Opus tự làm), mỗi việc một commit cục bộ, chưa push.

| Việc | Commit | Nội dung |
| --- | --- | --- |
| V1 | `87d56b4` | Thanh tab dưới dùng chung `.ts-bottom-nav` (trong `trosync-nen.css`): Admin và nhân viên có Việc, Phòng, Điện nước, Hóa đơn, Thêm (mở `#sidebar-menu`); khách thuê có Trang chủ, Hóa đơn, Điện nước, Sự cố, Tôi. Lớp `ts-co-thanh-tab` trên `body` nâng nút trợ lý AI và đệm cuối trang. |
| V4 | `13056b6` | `HoaDonRes.HanThanhToanUtc` (chỉ đọc cột `HanThanhToan`) điền trong `HoaDonStore.GetInvoicesByContractIdsAsync`; không thêm truy vấn, không đổi schema. |
| V2 | `01c977b` | Thanh thu tiền hai đoạn của Admin (`thuTienCard`), phần trăm = đã thu ÷ (đã thu + chờ thu), mẫu số 0 ra 0%; ô số liệu nhạt cho cả hai vai trò; hàng hai cột Chờ bạn duyệt / Tiền mặt nhân viên; bảng tiền mặt cuộn ngang trong thẻ, tên dài cắt "…"; màu biểu đồ đọc từ `--ts-ok`, `--ts-wait`, `--tblr-primary`. |
| V3 | `5547fdd` | Mỗi việc có nút làm luôn cùng đường dẫn (Chốt số, Xem hóa đơn, Thu tiền, Xử lý), việc Khẩn dùng nút chính; Razor và `renderViec` cùng markup; áp cho cả danh sách thu gọn của Admin. |
| V5 | `bc08c87` | Trang chủ khách thuê: thẻ "Cần thanh toán" (số tiền `VndFormat.Money`, số hóa đơn, "Hạn dd/MM, còn n ngày" hoặc "Quá hạn n ngày" theo ngày Việt Nam, nút "Thanh toán bằng mã QR"), không nợ thì "Bạn đã thanh toán đủ"; ba lối tắt Điện nước, Báo sự cố, Hợp đồng; bỏ lưới bento. |

Lệch so với thiết kế:

- Ô "Đã thu" của Admin ghi "Đã thu kỳ T{m}/{y}" (giữ chuỗi test `KyKhongHopLe_DungThangNamHienTai` cần); nhãn đầu thẻ thanh thu tiền là "Thu tiền kỳ T{m}/{y}".
- Bốn ô số liệu nằm ngay dưới thẻ thanh thu tiền (một hàng thẻ riêng), dùng chung markup với nhân viên.
- Thẻ thanh toán của khách thuê có `aria-label="Hóa đơn chưa thanh toán"` để giữ chuỗi mà `CustomerDashboardStatusTests` dò.
- Số hóa đơn và hạn gần nhất chỉ tính hóa đơn còn nợ > 0; tổng tiền giữ cách tính cũ.

Test mới: `ThanhTabDuoiTests`, `BangDieuKhienDot2Tests`, `TrangChuKhachThueTests` (Web.IntegrationTests), `TenantInvoiceVisibilityTests.SentInvoice_ListShowsHanThanhToanUtc` (Infrastructure.IntegrationTests). Chưa chạy trong đội (cần DB test); phiên chính chạy khi nghiệm thu. Chưa kiểm trình duyệt sáng/tối/375px.
