# Báo cáo thống kê

- **Mục tiêu:** trang Báo cáo ở cổng quản lý cho Admin và nhân viên (theo chi nhánh được giao): doanh thu theo kỳ hóa đơn, công nợ, công suất phòng, biến động hợp đồng, điện nước; có biểu đồ và xuất Excel.
- **Hiện trạng (05/11/2026):** spec v1 đã duyệt ([spec.md](spec.md), D1–D9 theo đề xuất). Plan: [plan.md](plan.md). Đã code T1–T9 trên nhánh `feature/bao-cao-thong-ke` (từ `master` = `740d219`); build và test các tầng PASS, review xong, đã sửa S1–S2 và lỗi tràn ngang, đã thử giao diện trên preview và chụp ảnh ([review.md](review.md)). Chưa commit.
- **Schema:** không đổi.

## Đã chốt

- Doanh thu tính theo kỳ của hóa đơn.
- Tiền cọc là tiền giữ hộ, chỉ thành doanh thu khi cấn trừ lúc thanh lý; "Đã thu" tách Thu tiền và Cấn trừ cọc; có ô Tiền cọc đang giữ; tiền giữ chỗ không hoàn báo riêng ở Thu khác (phương án A).
- Nhân viên xem được báo cáo của chi nhánh được phân công.
- X1 (05/11/2026): "Tiền giữ chỗ đang chờ quyết định" theo phương án A, không cộng quyết định còn `ChoHoanTien`/`DangHoanTien`.
- C1: công suất cắt theo ngày tạo phòng. C2: tab Công nợ chỉ hiện số hóa đơn, không gắn link. C3: sửa lọc khoảng ngày ở màn Lịch sử thanh toán.

## Hạng mục

- [x] Spec được duyệt (05/11/2026, D1–D9 theo đề xuất)
- [x] Plan
- [x] Quyền `Report.Read`, service, store (T1)
- [x] Tab Doanh thu (T2)
- [x] Tab Công nợ (T3)
- [x] Tab Công suất phòng (T4)
- [x] Tab Hợp đồng (T5)
- [x] Tab Điện nước (T6)
- [x] Xuất Excel (T7)
- [x] Sửa kèm màn Lịch sử thanh toán (D5: thêm ô Cấn trừ cọc, lọc ngày theo giờ VN; C3: nhận dấu ngăn " đến ") (T8)
- [x] Link từ Dashboard (T9)
- [x] Test các tầng (Domain, Application, Infrastructure, Web)
- [x] Review và thử giao diện trên trình duyệt (05/11/2026)
