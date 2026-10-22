# Module 2 — Thanh toán hóa đơn

- **Người phụ trách:** A
- **Mốc roadmap:** ngày 23–43, ổn định ngày 44–53
- **Hiện trạng:** VietQR và một phần Application Service xử lý yêu cầu/minh chứng đã có; Web chưa nối trọn luồng. Đặc tả v2.0 chốt ngày 02/10/2026 ([spec.md](spec.md)), chưa có mã triển khai.
- **Schema:** không đổi; dùng các bảng có sẵn từ Migration 1.

## Tính năng cần hoàn thành

- [ ] Khách tạo lượt thanh toán (`YeuCauThanhToanHoaDon`) cho hóa đơn đã gửi, với số tiền và hạn hợp lệ.
- [ ] Khách thuê xem VietQR và tải ảnh minh chứng chuyển khoản.
- [ ] Nhân viên đối chiếu, xác nhận hoặc từ chối minh chứng.
- [ ] Ghi nhận thanh toán đủ hoặc một phần theo mức tối thiểu được quản lý bật.
- [ ] Xác nhận trong transaction, chống ghi nhận trùng tiền/mã giao dịch.
- [ ] Hiển thị hàng đợi đối chiếu, lịch sử và trạng thái cho khách/nhân viên.
- [ ] Nối service hiện có với API/MVC, Razor và test.

## Ranh giới

A sở hữu thanh toán hóa đơn. Service hiện có `HoaDons/Services/InvoicePaymentConfirmationService.cs` không được nhân bản chỉ để khớp thư mục tài liệu. Thanh toán giữ chỗ thuộc B, dùng bảng và service riêng.

## Hồ sơ triển khai

- `spec.md`: đặc tả nghiệp vụ và thiết kế đã chốt.
- `plan.md`: kế hoạch triển khai theo từng phần, viết sau khi đặc tả được duyệt.
- `review.md`: kết quả review và kiểm chứng khi có mã triển khai.

Hiện mới có `spec.md`; `plan.md` và `review.md` chưa được tạo.
