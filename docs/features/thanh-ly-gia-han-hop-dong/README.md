# Module: Thanh lý, gia hạn hợp đồng và lịch sử thuê

- **Nhánh Git:** `feature/thanh-ly-gia-han-hop-dong` (từ `master` = `d34fa90`).
- **Hiện trạng:** đã code xong theo spec v2 (05/11/2026, qua `/ship`), đã sửa các điểm review M1, S1–S3; toàn bộ test PASS; thử giao diện (chỉ xem) PASS; thanh lý thật trên DB phát triển PASS 05/11/2026 (hợp đồng cọc 0 ₫ còn nợ, chưa có dòng cấn trừ thật); gia hạn thật PASS 05/11/2026. Chưa gộp vào `master`.
- **Schema:** không đổi (người dùng chốt 04/11/2026: không thêm migration, kể cả cột `HopDongTruocId`). Chỉ thêm giá trị enum `PhuongThucThanhToan.CanTruTienCoc = 2`.
- **Phạm vi:** chỉ cổng quản lý; không đụng cổng khách vãng lai.

## Tính năng

- [x] Bỏ `ContractAutoCloseJob` và `ContractAutoCloseUseCase`: hệ thống không tự kết thúc hợp đồng hay trả phòng về Trống. Form sửa hợp đồng không còn trạng thái Đã kết thúc (server cũng từ chối).
- [x] `ContractExpiryAlertJob` gửi thông báo trong app **một lần** khi hợp đồng hết hạn cho Admin và nhân viên chi nhánh (khóa `app_het_han_{id}` trong `sent_contract_alerts.json`); việc cần làm KH4 "Hợp đồng đã quá hạn chưa xử lý" (lọc `boLoc=QuaHan` ở danh sách hợp đồng).
- [x] Thanh lý (`/QuanLyNhaTro/HopDong/ThanhLy/{id}`, xem chi tiết ở `/QuanLyNhaTro/HopDong/ChiTiet/{id}`): ngày trả phòng; tiền phòng tháng trả tính theo ngày, điện nước theo chỉ số chốt (qua hóa đơn tháng đó, phải đã công bố); cấn trừ cọc vào hóa đơn còn nợ của cả chuỗi gia hạn, kỳ cũ trước, tối đa bằng số nợ, ghi `LichSuThanhToan` mã `CANTRU-HD{id}-{HoaDonId}`; trả phòng, đóng thành viên và dịch vụ; biên bản thanh lý PDF. Còn nợ sau cấn trừ vẫn cho thanh lý khi tích xác nhận, nợ tiếp tục theo dõi ở KH5 và cảnh báo nợ cũ.
- [x] Gia hạn (`/QuanLyNhaTro/HopDong/GiaHan/{id}`): hợp đồng mới nối tiếp từ ngày 1 tháng sau, mã `{mã gốc}-GH{n}`, chuyển cọc, chép thành viên và điều khoản; giá thuê/cọc phải là số đồng nguyên; hộp thoại xác nhận tóm tắt hợp đồng mới trước khi gia hạn.
- [x] Lịch sử thuê theo người thuê (`/QuanLyNhaTro/NguoiThue/ChiTiet/{id}`: đứng tên và ở ghép, giới hạn theo chi nhánh); tìm hợp đồng theo mã gốc, tên, CCCD, số điện thoại; bộ lọc nhanh ở danh sách hợp đồng.
- [x] Công nợ hợp đồng đã kết thúc: việc cần làm KH5, bộ lọc hóa đơn `hopDongDaKetThucConNo`, cảnh báo nợ cũ khi lập hợp đồng hoặc thêm thành viên (`/NguoiThue/NoCu/{id}`).
- [x] Chỉ xóa được hợp đồng **Đã hủy**; hợp đồng đã kết thúc giữ lại để đối chiếu (người dùng chốt 05/11/2026).

## Cải tiến giao diện nhóm 1 (05/11/2026)

Triển khai theo [plan-giao-dien-nhom-1.md](plan-giao-dien-nhom-1.md):
- [x] **Đổi ngày trả phòng trên màn thanh lý** (`POST /HopDong/NgayTraPhong/{id}`): card "Ngày trả phòng" cho phép cập nhật ngày trả phòng trực tiếp trên màn thanh lý mà không cần qua form sửa; lưu mốc 23:59:59 giờ VN (16:59:59Z); quy tắc nghiệp vụ: chặn đổi ngày nếu đã có hóa đơn chưa hủy ở mốc cũ hoặc mốc mới (`min(thangCu, thangMoi)`); sau khi đổi thành công tự tính lại toàn bộ giao diện thanh lý.
- [x] **Nút "Lập hóa đơn T{m}/{y}" khi thiếu hóa đơn tháng trả phòng**: card "Chưa thanh lý được" hiển thị nút dẫn trực tiếp tới `/QuanLyNhaTro/QuanLyHoaDon?chiNhanhId=...&thang=...&nam=...&moPhatSinh=1&phongTroId=...`; màn hóa đơn tự động mở modal xem trước phát sinh, chọn sẵn chi nhánh/kỳ và tích đúng phòng mục tiêu; đồng thời hiển thị link "Mở {MaHoaDon}" cho các hóa đơn chưa công bố trong chuỗi.
- [x] **Danh sách hợp đồng bổ sung cột "Hết hạn", badge "Đã gia hạn", "Quá hạn N ngày"**: thêm cột "Hết hạn" (`thoiDiemKetThuc`); hợp đồng đã gia hạn hiển thị badge xanh dương "Đã gia hạn" với tooltip `title="→ {MaHopDongKeTiep}"` thay cho "Đã kết thúc"; hợp đồng quá hạn hiển thị badge đỏ "Quá hạn N ngày" (tính theo ngày VN); truy vấn lấy `MaHopDongKeTiep` tối ưu theo lô chống N+1.
- [x] **Menu "⋯" thao tác danh sách hợp đồng**: bổ sung nhanh nút "Thanh lý" và "Gia hạn" cho các hợp đồng đang hoạt động.
- [x] **Menu trái giữ mục đang chọn trên các màn con**: mở rộng `MenuItem.ActivePrefixes`; menu "Hợp đồng" giữ active khi vào `/HopDong/ChiTiet|ThanhLy|GiaHan/{id}`; menu "Người thuê" giữ active khi vào `/NguoiThue/ChiTiet/{id}`.

## Hạn chế đã biết (chưa sửa)

- Tắt `ContractAlertSettings:Enabled` (để ngừng email cảnh báo) thì cũng mất thông báo trong app khi hợp đồng hết hạn; spec chưa nói tới.
- Khách trả phòng rồi thuê lại đúng phòng cũ: nợ cũ bị ẩn khỏi KH5 và bộ lọc (hệ quả của Q1 đã chọn).

## Hồ sơ triển khai

- [spec.md](spec.md): đặc tả v2, người dùng đã duyệt 04/11/2026 (mục 10 ghi các điểm đã chốt; §4.1 tính cả chuỗi gia hạn, §4.4 quy tắc xóa bổ sung 05/11/2026).
- [plan.md](plan.md): kế hoạch 13 task của planner, kèm câu trả lời của người dùng (Q1–Q14).
- [plan-giao-dien-nhom-1.md](plan-giao-dien-nhom-1.md): kế hoạch 5 cải tiến giao diện luồng hợp đồng (nhóm 1).
- [review.md](review.md): phát hiện của reviewer, cách xử lý, kết quả test và phần chưa kiểm chứng.

## Module tiếp theo

Báo cáo thống kê (doanh thu, công nợ, công suất phòng, tiêu thụ điện nước; lọc chi nhánh và khoảng tháng, xuất Excel). Chưa có thư mục.
