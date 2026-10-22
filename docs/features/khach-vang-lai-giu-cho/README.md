# Module 4 — Khách vãng lai, giữ chỗ, cọc và hoàn tiền

- **Người phụ trách:** B
- **Mốc roadmap:** ngày 32–53
- **Hiện trạng:** đã có entity/schema khách vãng lai, giữ chỗ, thanh toán giữ chỗ, cọc và hoàn tiền; Application Service/API/UI của luồng chưa hoàn chỉnh.

## Tính năng cần hoàn thành

- [ ] Đăng ký tài khoản và hồ sơ `KhachVangLai`.
- [ ] Tạo yêu cầu giữ chỗ trực tiếp hoặc từ lịch xem khi đã đăng nhập.
- [ ] Nhân viên duyệt số tiền/hạn thanh toán và khóa một yêu cầu hoạt động cho mỗi phòng.
- [ ] Tạo yêu cầu thanh toán giữ chỗ, nhận minh chứng và xác nhận tiền thực nhận.
- [ ] Hủy/hết hạn giữ chỗ và giải phóng phòng theo quy tắc.
- [ ] Chuyển khách vãng lai thành người thuê khi lập hợp đồng.
- [ ] Áp dụng tiền giữ chỗ vào cọc hợp đồng đúng một lần.
- [ ] Quản lý quyết định số tiền/lý do hoàn.
- [ ] Ghi nhận nhiều giao dịch hoàn trong giới hạn quyết định và test cạnh tranh.

## Ranh giới

B sở hữu luồng giữ chỗ từ Application đến Web. Thanh toán hóa đơn thuộc A và không dùng chung service/bảng với thanh toán giữ chỗ. Thay đổi `NguoiDungs`, `NguoiThues`, `HopDongs` hoặc contract dùng chung cần thống nhất file trước. B không tạo migration hoặc sửa DI/`Program.cs` dùng chung; gửi yêu cầu cho A tích hợp.

## Hồ sơ triển khai

- `spec/`: viết đặc tả khi bắt đầu thực hiện.
- `plan/`: viết kế hoạch triển khai sau khi đặc tả được chốt.
- `review/`: lưu kết quả review và kiểm chứng khi có mã triển khai.

Hiện các thư mục trên chỉ có `.gitkeep`; chưa có tài liệu spec, plan hoặc review.
