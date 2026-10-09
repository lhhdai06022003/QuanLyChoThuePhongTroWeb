# Module: Trung tâm việc cần làm

- **Mốc lộ trình vận hành:** bước 3, sau bước 2 "AI Assistant + Thông báo theo chi nhánh" (commit `8295e36`).
- **Nhánh Git:** `feature/viec-can-lam`.
- **Hiện trạng:** đặc tả đã duyệt ngày 06/10/2026 ([spec.md](spec.md), bản 1.1). Mockup đã duyệt trong [mockup/](mockup/). Triển khai bằng `/ship` 2 lượt: đợt A (backend) và đợt B (giao diện) xong ngày 08/10/2026, reviewer CHOT cả hai, full test PASS; review cuối của phiên chính (3 vòng, có chạy ứng dụng trên trình duyệt) ĐẠT ([review.md](review.md)); đã commit `361575e` ngày 09/10/2026. Spec 1.2 (CS1 tính theo kỳ `DaDuyet`) được người dùng xác nhận ngày 09/10/2026; người dùng đã thử bằng tài khoản nhân viên, chưa thấy lỗi.
- **Schema:** không đổi.

## Tính năng cần hoàn thành

- [x] Dashboard có khối "Việc cần làm" ở cột phải: 5 việc ưu tiên cao nhất, ô đếm theo mức ưu tiên, link xem tất cả.
- [x] Trang `/QuanLyNhaTro/ViecCanLam` liệt kê đủ 11 loại việc theo mức ưu tiên, lọc theo chi nhánh và nhóm, kèm bảng hóa đơn quá hạn.
- [x] Mục menu "Việc cần làm" có badge số việc.
- [x] Nhân viên chỉ thấy việc trong chi nhánh được phân công và không thấy việc chỉ-Admin.
- [x] KPI "Chờ thu", "Phòng đang nợ" và biểu đồ chỉ tính hóa đơn đã gửi khách; có thêm thẻ "Quá hạn" và "Tỷ lệ lấp đầy".
- [x] Số tiền trên Dashboard và trang Việc cần làm hiển thị dạng `3.590.000 đ`.
- [x] Sửa lỗ hổng XSS khi Dashboard dựng danh sách bằng JS.

## Hồ sơ triển khai

- `spec.md`: đặc tả tính năng, cần được duyệt trước.
- `plan-dot-a.md`, `plan-dot-b.md`: kế hoạch đợt A (backend) và đợt B (giao diện), chép từ `.bangiao/` của `/ship` ngày 08/10/2026.
- `review.md`: review duy nhất của module, gộp đánh giá đợt A, đợt B và review cuối của phiên chính.
- `mockup/`: mockup giao diện đã duyệt (Dashboard phương án B, trang Việc cần làm phương án A).
