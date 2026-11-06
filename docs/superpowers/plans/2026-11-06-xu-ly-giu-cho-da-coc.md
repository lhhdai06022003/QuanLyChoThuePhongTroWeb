# Xử lý yêu cầu giữ chỗ đã đặt cọc Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tạo hợp đồng từ yêu cầu đủ cọc và ghi lại khoản cọc giữ lại khi khách không thuê.

**Architecture:** Dùng màn Giữ chỗ và dịch vụ quyết toán hiện có để kiểm quyền và xử lý yêu cầu. Khi tạo hợp đồng, dùng logic lập hợp đồng hiện tại trong cùng giao dịch với hồ sơ, tài khoản và dòng áp cọc. Khi khách không thuê, kết thúc yêu cầu cùng quyết định tiền giữ lại và giữ lịch sử giao dịch.

**Tech Stack:** ASP.NET Core MVC, Razor, EF Core, PostgreSQL, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-06-xu-ly-giu-cho-da-coc-design.md`

## Global Constraints

- Dùng schema hiện tại của nhánh `codex/khach-vang-lai-tiep-tuc`.
- Kiểm quyền chi nhánh, đúng người đặt và phòng, đủ tiền hợp lệ.
- Kiểm thử trên cơ sở dữ liệu riêng có hậu tố `_test`.

## Review Focus

- Thay ID phòng hoặc người thuê từ trình duyệt không đổi được khách và phòng của hợp đồng.
- Bấm lặp, bấm đồng thời hoặc lỗi cấp tài khoản không để lại hợp đồng hay áp cọc một phần.
- Hồ sơ trùng thuộc tài khoản khác được xử lý bằng lỗi rõ ràng.
- Tiền dư hoặc tiền đến sau khi đóng được ghi riêng với tiền cọc giữ lại.
- Các endpoint cũ không cho hủy yêu cầu đã cọc mà bỏ qua bước ghi nhận tiền.

## Task 1 Tạo hợp đồng từ yêu cầu giữ chỗ

- [x] Viết ca HTTP trong bộ Web integration với khách vãng lai đã trả 300.000 đ. Sau khi POST xác nhận thuê, kiểm tra hợp đồng đúng khách/phòng, tiền áp cọc 300.000 đ, trạng thái Đã thuê, tài khoản gốc đã liên kết, chỉ một hợp đồng sau lần bấm lại. Thêm ca khách ngoài chi nhánh và CCCD trùng.
- [x] Chạy ca mới và xác nhận thất bại tại điểm vào chưa có.
- [x] Bổ sung `GetContractDraftAsync(actorId, reservationId, ct)` và `CreateContractAsync(actorId, reservationId, input, ct)` vào `ReservationSettlementService` và store tương ứng. DTO dự thảo lấy người đặt/phòng/mức cọc ở server. DTO gửi chỉ chứa CCCD nếu thiếu, ngày thuê và điều khoản tiền thuê được quản lý xác nhận.
- [x] Tách phần tạo hợp đồng trong giao dịch hiện có của `HopDongService` thành phương thức dùng chung cho luồng trực tiếp và luồng giữ chỗ. Store quyết toán kiểm tra và khóa yêu cầu/phòng, chuẩn bị người thuê và liên kết tài khoản gốc trước khi gọi phần tạo dùng chung, sau đó lưu dòng áp cọc và trạng thái trong cùng giao dịch.
- [x] Thêm GET và POST `XacNhanThue` trong `GiuChoController`, view form điền sẵn và nút vào form tại `Views/GiuCho/ChiTiet.cshtml`.
- [x] Chạy các ca mới và các test tạo hợp đồng hiện có.

## Task 2 Ghi cọc giữ lại khi khách không thuê

- [x] Viết ca HTTP khách không thuê sau khi trả đủ 300.000 đ. Kiểm tra phòng hiện lại, còn dòng giao dịch, quyết định hoàn 0 đ, không có hợp đồng hay hóa đơn, từ chối POST sửa khoản cọc thành tiền hoàn. Thêm ca đã nhận dư và tiền đến sau khi đóng.
- [x] Chạy ca mới và xác nhận thất bại tại điểm vào chưa có.
- [x] Thêm `CloseWithoutRentAsync(actorId, reservationId, reason, ct)` vào dịch vụ và store quyết toán. Lưu trạng thái, lý do, lịch sử và quyết định xử lý tiền trong cùng giao dịch. Dùng dữ liệu giữ chỗ hiện có để phân biệt khoản cọc giữ lại với tiền dư hoặc đến muộn, kể cả trên báo cáo.
- [x] Thêm POST `KhachKhongThue` và form xác nhận, cập nhật nút hủy quá hạn về cùng quy tắc. Chặn khách tự đóng yêu cầu đã đủ cọc và hiển thị kết quả cọc giữ lại trong lịch sử.
- [x] Sửa thông báo trước khi chuyển tiền, trang yêu cầu, cẩm nang và nội dung hướng dẫn AI theo chính sách đã chọn.
- [x] Chạy các ca mới, kiểm thử giữ chỗ và quyết toán hiện có, kiểm thử Báo cáo và `git diff --check`.
