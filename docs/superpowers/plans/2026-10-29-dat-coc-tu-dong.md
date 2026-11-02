# Đặt cọc tự động — Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for orchestration; independent file storage/UI tasks may run in parallel under superpowers:dispatching-parallel-agents. Steps use checkbox syntax.

**Goal:** Giá cọc công khai theo phòng, thanh toán ngay không chờ duyệt, tự ẩn sau khi nhận đủ tiền, không đổi schema.

**Architecture:** Giá cọc nằm trong file cấu hình riêng có khóa và ghi nguyên tử. Số tiền/hạn/mã thanh toán được chốt vào các bản ghi giữ chỗ và thanh toán hiện có. Trạng thái đã nhận đủ tiền quyết định hiển thị công khai; unique index hiện có bảo vệ một lượt thanh toán/phòng.

**Tech Stack:** .NET 8 MVC, EF Core/PostgreSQL, JSON file, Razor/JavaScript, SePay/VNPay hiện có.

**Spec:** `docs/superpowers/specs/2026-10-29-dat-coc-tu-dong-design.md` — người dùng đã duyệt 29/10/2026.

## Global Constraints

- Chỉ làm trên `codex/khach-vang-lai`; không merge/push về `thuan` hoặc `master`.
- Không thêm/chỉnh entity, DbContext, mapping hoặc migration.
- Giá cọc theo phòng do quản lý nhập, lưu ngoài wwwroot và Git.
- Mỗi lượt thanh toán 30 phút; hạn ký 48 giờ sau khi nhận đủ cọc.
- Khách phải đăng nhập; ảnh minh chứng không xác nhận tiền; webhook/IPN phải xác thực và chống lặp.
- Test chỉ trên DB có hậu tố `_test`; không tạo giao dịch hoặc gửi thông báo thật khi kiểm thử.

## Review Focus

- Giá đổi trong lúc khách đang thanh toán: số tiền đã chốt trên yêu cầu không đổi.
- Hai khách tạo cùng lúc: chỉ một mã thanh toán hoạt động; bấm lại của cùng khách tiếp tục đúng mã cũ.
- File lỗi/ghi dở: fail closed, không ghi giá 0 hoặc tạo yêu cầu sai giá; có khóa và thay file nguyên tử.
- Trả thiếu, gửi ảnh, callback lặp/đến muộn: không tự ẩn hoặc giành lại phòng sai điều kiện.
- Các yêu cầu lịch sử chưa duyệt/đã nhận tiền: vẫn đọc và xử lý được, không tự sửa dữ liệu cũ hàng loạt.

## Task 1 — Cấu hình giá cọc

- [x] Tạo `Application/Features/PublicRooms/RoomDepositPolicy.cs`: record `RoomDepositPolicy(decimal Amount, int PaymentWindowMinutes = 30, int ContractWindowHours = 48)` và `IRoomDepositPolicyStore.GetAsync`, `GetAllAsync`, `SetAsync`, `UpdateListingAsync`.
- [x] Viết test lỗi trước, xác nhận RED, rồi triển khai `Infrastructure/ExternalServices/Storage/JsonRoomDepositPolicyStore.cs`: khóa theo path, file riêng App_Data, ghi file tạm/thay nguyên tử, validation và xử lý cập nhật cùng tin.
- [x] Test đọc lại sau tạo instance mới, ghi đồng thời không mất giá phòng khác, dữ liệu hỏng không dùng mặc định và rollback nếu lưu tin thất bại. Bỏ file giá thực tế khỏi Git.

## Task 2 — Giá công khai và giao diện

- [x] Bổ sung `DepositAmount` vào DTO/command tin phòng và phòng công khai; nối policy store vào service (không thêm cột).
- [x] TinPhong thêm input giá cọc, nạp lại giá đã lưu; giá bắt buộc khi đăng tin mới/sửa tin công khai.
- [x] Thẻ phòng/chi tiết/trang đặt hiển thị giá, khóa đặt nếu chưa có giá; đăng nhập/ReturnUrl giữ nguyên.
- [x] POST đặt phòng chuyển thẳng trang thanh toán của yêu cầu vừa tạo; bỏ nội dung chờ duyệt; giữ hủy/xem trạng thái của tài khoản.
- [x] Sửa script đặt cọc dùng DOM/fetch, không phụ thuộc jQuery; test init và checkout handler khi không có jQuery.

## Task 3 — Tạo thanh toán ngay và chỉ ẩn khi đủ tiền

- [x] Viết test RED cho `CreateForPaymentAsync` và service Request: giá canonical, không cần gọi Approve, hạn 30 phút, người duyệt null, mã TTPTGC, cùng khách bấm lại/cạnh tranh hai khách.
- [x] Giữ API tạo/duyệt cũ để đọc/xử lý yêu cầu lịch sử; luồng khách mới dùng `CreateForPaymentAsync` trong transaction và unique index hiện có.
- [x] Tách query đã nhận đủ tiền khỏi query giữ lượt thanh toán. Trang công khai/tin phòng chỉ ẩn theo khoản đủ tiền đang giữ hợp lệ; phòng đang thanh toán vẫn hiện.
- [x] Ghi đủ tiền bằng SePay/VNPay hoặc đối soát có sẵn thì cập nhật hạn ký = thời điểm nhận đủ tiền + 48 giờ cho yêu cầu tự tạo; không đổi hạn của yêu cầu lịch sử có nhân viên duyệt.
- [x] Test thiếu/đủ/đến muộn/lặp, hết hạn nhả lượt và ảnh minh chứng không che phòng.

## Task 4 — Kiểm chứng và bàn giao

- [x] Chạy focused tests từng task rồi chạy các bộ liên quan; giải quyết thất bại theo nguyên nhân, không nới kiểm tra để bỏ lỗi.
- [x] Build toàn solution; rà diff không có schema/migration, config ngân hàng/SMTP hoặc file giá thực tế.
- [x] Review độc lập toàn luồng tiền và file cấu hình; sửa lỗi quan trọng rồi chạy lại test cần thiết.
- [x] Cập nhật SPEC/PLAN chức năng hiện có và báo giới hạn SMTP/webhook thực tế; không tự push khi chưa được yêu cầu.
