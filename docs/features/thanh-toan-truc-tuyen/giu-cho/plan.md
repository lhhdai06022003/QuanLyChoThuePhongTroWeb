# Kế hoạch: Thanh toán trực tuyến cho giữ chỗ (đợt 3)

- **Nhánh:** `feature/thanh-toan-truc-tuyen`. **Spec:** [spec.md](spec.md), đã duyệt 26/10/2026.
- **Trạng thái:** Phần 1 (backend ghi nhận tự động) xong 26/10/2026, reviewer CHOT; sau khi sửa khuyến nghị R1, R2 (mục 2) toàn solution PASS một lượt (Domain 135, Application 2234, Infrastructure 661, Web 671), `has-pending-model-changes` PASS. Chưa commit. Phần 2 chưa làm. Hai lượt `/ship` (Q1); mỗi lượt planner sinh kế hoạch chi tiết ở `.bangiao/ke-hoach.md` (không lưu Git, lượt sau ghi đè lượt trước). File này giữ cách chia phần và các quyết định đã chốt.

## 1. Nguyên tắc chung

- Không đổi schema trong cả hai phần; S1 (`giao_dich_giu_cho."NguoiXacNhanId"` cho phép NULL) đã làm ở `b28229e`. Kiểm bằng `has-pending-model-changes`.
- Không sửa `Program.cs`, `.csproj`, `appsettings.json`, fixture test, test kiến trúc. Chỉ thêm khóa `ReservationEnabled` vào `appsettings.example.json`.
- Test hóa đơn, SePay, VNPay hiện có phải PASS mà không sửa file nào.
- Không tạo script phụ trợ; không tự thiết lập môi trường, code xong liệt kê thứ người dùng cần bật.
- Mỗi lượt `dotnet test` chạy một mình; integration test chỉ trên DB `_test`.

## 2. Phần 1 — Backend ghi nhận tự động (xong 26/10/2026)

Quyết định đã duyệt (26/10/2026 21:54):

| # | Quyết định |
| --- | --- |
| X1 | Mã chuyển khoản yêu cầu thanh toán giữ chỗ = `TTPTGC{YeuCauGiuChoId}`, sinh cho mọi lần duyệt mới, không phụ thuộc cờ. Yêu cầu đã duyệt trước giữ mã `GC…`, không bao giờ được ghi tự động. |
| X2, X3 | Ghi tự động dùng chung phần cộng dồn/chuyển trạng thái với luồng tay, bỏ kiểm "nhận trước phút duyệt" và giờ ở tương lai; `NguoiXacNhanId` và `MinhChungThanhToanGiuChoId` null; lịch sử `HeThong`, `NguoiThucHienId` null, lý do `Ghi nhận tự động {MaGiaoDich}`. `GhiChu` chỉ `null` hoặc `LATE_REFUND_REQUIRED`. |
| X4 | Số "tiền thừa" và "cần hoàn" chỉ để hiển thị trong thông báo; số ghi sổ luôn là số thực nhận. |
| X5 | Cờ `OnlinePayment:ReservationEnabled` tắt = không ghi tự động (SePay `Unmatched` kèm cảnh báo như hiện nay, VNPay trả `01`). |
| X6 | Câu chữ thông báo trong app, Telegram, email theo `.bangiao/ke-hoach.md` mục A10 (`ReservationPaymentNotificationFormatter`): trong app "Đã nhận tiền giữ chỗ {SoTien}đ qua {SePay|VNPay} — phòng {SoPhong}"; Telegram tiêu đề "🔔 TIỀN GIỮ CHỖ CẦN HOÀN" / "✅ THU TIỀN GIỮ CHỖ — GIỮ CHỖ ĐÃ CÓ HIỆU LỰC" / "⚠️ THU TIỀN GIỮ CHỖ (MỘT PHẦN)"; email báo đã nhận, còn lại hoặc giữ chỗ có hiệu lực, hoặc khoản sẽ được hoàn. |
| X7 | Nhân viên không nhập tay mã `SEPAY-`/`VNPAY-`; mã trùng khóa hệ thống đã ghi bị từ chối. |
| X8 | Nội dung chứa cả `TTPTHD` và `TTPTGC`: lấy mã xuất hiện trước. |
| X9 | Đồng bộ bù SePay không ghi tiền giữ chỗ (`Ignored` kèm lý do). |
| X10 | IPN giữ chỗ: ghi được (kể cả cần hoàn) `00`; trùng `02`; không có yêu cầu `01`; lỗi/xung đột `99`; sai chữ ký `97`; số tiền sai `04`. |
| Q2 | `IReservationVnPayService` không vào `ScopedServices` của `BranchScopeArchitectureTests` (theo tiền lệ `IVnPayPaymentService`); không sửa test kiến trúc. |
| Q3 | Interface riêng `IReservationAutoPaymentStore`, cùng class `ReservationPaymentStore` cài đặt. |
| Q4 | Phân nhánh IPN theo `vnp_TxnRef` ở `VnPayIpnController`: `GC{id}_{ticks}` → `IReservationVnPayService`, còn lại → `IVnPayPaymentService`. |
| Q5 | Vẫn gửi email cho khách khi khoản về phải hoàn. |
| R1 | Sau review (người dùng chọn 26/10/2026): ghi tự động gặp dòng nhân viên nhập tay cùng yêu cầu thanh toán có `MaGiaoDich` (bỏ khoảng trắng, viết hoa) bằng mã gốc của khóa `SEPAY-`/`VNPAY-` thì coi là trùng (`Duplicate`, SePay không ghi, VNPay `02`), để SePay gửi lại webhook/VNPay gọi lại IPN không cộng đôi số đã nhận. Khóa `SEPAY-ID-{id}` không có mã gốc nên không so. |
| R2 | Sau review: luồng nhân viên xác nhận tay bắt lỗi Postgres qua chuỗi `InnerException` như luồng tự động (lỗi serialize bị EF bọc trả "xung đột" thay vì lỗi 500); cả hai luồng xóa change tracker khi gặp trùng/xung đột. |

**Xong khi:** build 0 lỗi; toàn solution PASS một lượt; `has-pending-model-changes` PASS; `git diff --check` sạch; không file test cũ nào bị sửa. Đạt ngày 26/10/2026 (chi tiết và các chỗ lệch kế hoạch ở `.bangiao/thay-doi.md`).

## 3. Phần 2 — Giao diện, đối chiếu, tài liệu (chưa làm)

- `IReservationVnPayService.CreatePaymentUrlAsync` và `GetReturnResultAsync`; `vnp_TxnRef` dựng bằng `VnPayProtocol.BuildReservationTxnRef` (đã có từ phần 1).
- `PhongController`: tạo link VNPay và trang kết quả; `Views/Phong/DatCoc.cshtml` hai tab (QR VietQR theo số còn lại, VNPay); JS POST và nghe SignalR `GiuChoThanhToanCapNhat` (sự kiện đã phát từ phần 1, chưa có trang nghe) ở file riêng.
- Cổng quản lý `GiuChoController.ChiTiet`: danh sách giao dịch với badge SePay/VNPay, mã tham chiếu, "Hệ thống" ở cột người xác nhận.
- Màn Giao dịch SePay: đối chiếu dòng giữ chỗ (`DaCo` khi `giao_dich_giu_cho` có khóa), không ghi bù.
- Telegram cảnh báo mã `TTPTGC…` không khớp (cờ tắt, không tìm thấy yêu cầu) còn gợi ý "Vào Hóa đơn → Thu thủ công" (`PaymentNotificationFormatter`): đổi sang gợi ý màn Giữ chỗ.
- Còn để sau (khuyến nghị thấp của reviewer phần 1): `MailKitEmailService.SendReservationPaymentNoticeAsync` chưa bỏ qua cấu hình mẫu bắt đầu `<` (giống `SendRoomDepositedNoticeAsync`).
- Tài liệu: hướng dẫn thiết lập (cờ `ReservationEnabled`, thử trên SePay Test mode), kịch bản G01–G07, README module, `docs/features/README.md`, mục Hiện trạng `AGENTS.md`.
