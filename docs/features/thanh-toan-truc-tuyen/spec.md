# Spec: Thanh toán trực tuyến hóa đơn — đợt 1 (VietQR + SePay)

- **Trạng thái:** v1, bản nháp chờ người dùng duyệt (16/10/2026).
- **Nhánh:** `feature/thanh-toan-truc-tuyen` (từ `master` = `6b551fe`).
- **Nguồn yêu cầu:** tài liệu "Đặc tả kỹ thuật & nghiệp vụ: phân hệ thanh toán hóa đơn, biên lai điện tử & thông báo tự động" v1.0 của người dùng; các điểm điều chỉnh có ghi ở §13.
- **Nối tiếp:** [thanh toán hóa đơn](../thanh-toan-hoa-don/spec.md) (luồng lượt + minh chứng + đối chiếu tay). Spec này hiện thực hướng nâng cấp số 1 ở §35 của spec đó: tự khớp giao dịch ngân hàng qua webhook.
- **Schema:** không đổi. Kiểm bằng `has-pending-model-changes` trước khi bàn giao.

## 1. Mục tiêu

Khép kín luồng thu tiền phòng hằng tháng mà không cần nhân viên đối chiếu bằng mắt:

1. Hóa đơn được công bố → khách thuê nhận email có nút **Thanh toán trực tuyến** dẫn vào cổng khách thuê.
2. Khách chuyển khoản bằng VietQR (quét từ PC, hoặc trên điện thoại: mở app ngân hàng, tải ảnh QR, sao chép).
3. Tiền về tài khoản → SePay gọi webhook → hệ thống tự ghi sổ `LichSuThanhToan`, cập nhật trạng thái hóa đơn.
4. Màn hình khách tự đổi sang biên lai (SignalR), khách nhận email biên lai, chủ trọ nhận tin Telegram, nhân viên chi nhánh nhận thông báo trong app.

### 1.1 Phạm vi

| Có trong đợt 1 | Không có trong đợt 1 |
| --- | --- |
| Cổng khách thuê (`Areas/KhachThue`) và cổng quản lý (`Areas/QuanLyNhaTro`) | Thanh toán giữ chỗ/đặt cọc của khách vãng lai (`ReservationPayment*`) — đồng đội đang làm; chỉ để sẵn điểm mở rộng (§4.4) |
| VietQR + webhook SePay + đồng bộ bù qua API SePay | VNPay (thẻ ATM/Visa) — đợt 2 (§12) |
| Biên lai web, email biên lai, Telegram, thông báo trong app | Magic link / trang thanh toán công khai không cần đăng nhập — người dùng đã bỏ |
| Gỡ luồng khách tự tạo lượt và nộp minh chứng | Hoàn tiền thừa, ví, phân bổ một giao dịch cho nhiều hóa đơn |

## 2. Thuật ngữ

- **Mã chuyển khoản:** `TTPTHD{HoaDonId}`, ví dụ `TTPTHD105`. Viết liền để SePay tách được thành trường `code` (§4.2). Hệ thống vẫn nhận dạng có dấu cách (`TTPT HD105`) khi khách tự gõ.
- **Số còn lại:** `TongTien − Σ SoTienThanhToan` của các dòng `LichSuThanhToan` chưa xóa mềm (định nghĩa hiện có).
- **Giao dịch SePay:** một biến động số dư mà SePay báo, qua webhook hoặc qua API danh sách giao dịch.
- **Khóa chống trùng:** giá trị ghi vào `LichSuThanhToan.MaGiaoDich` cho mỗi giao dịch SePay (§5.2).

## 3. Trải nghiệm khách thuê (cổng khách thuê)

### 3.1 Vào trang thanh toán

- Link trong email: `{PublicBaseUrl}/KhachThue/HoaDon?hoaDonId={HoaDonId}`. Trang `HoaDon/Index` đã đọc `hoaDonId` để mở thẳng hóa đơn.
- Khách chưa đăng nhập → trang đăng nhập với `ReturnUrl` → sau khi đăng nhập quay lại đúng hóa đơn (hành vi hiện có của `NguoiDungController.DangNhap`, chỉ cần test lại).
- Chỉ người đứng tên hợp đồng (claim `NguoiThueId`) thấy panel thanh toán, giữ nguyên quy tắc hiện có.

### 3.2 Panel thanh toán (`/KhachThue/HoaDon/ThanhToan/{hoaDonId}`)

Thay nội dung panel hiện tại. Panel hiện khi hóa đơn `DaGui`, chưa xóa.

**Cột thông tin:** phòng, chi nhánh, kỳ `Tháng {Thang}/{Nam}`, các dòng phí (giữ phần chi tiết hiện có), tổng tiền, đã thanh toán, **số còn lại**, hạn thanh toán.

**Khối chuyển khoản** (khi số còn lại > 0):

1. **Ảnh VietQR** sinh ở server bằng `IVietQRService` với số tiền = số còn lại, nội dung = mã chuyển khoản, tài khoản trong `VietQRSettings`. Hiện to ở giữa trên màn hình lớn.
2. **Tải ảnh QR**: nút gọi `GET /KhachThue/HoaDon/ThanhToan/QR/{hoaDonId}?taiVe=true`, trả PNG với `Content-Disposition: attachment; filename="QR_Phong_{TenPhong}_HD{HoaDonId}.png"` (tên file bỏ dấu, bỏ ký tự lạ). Kèm hướng dẫn: "Tải ảnh → mở app ngân hàng → Quét QR → chọn ảnh từ thư viện".
3. **Mở app ngân hàng (deeplink)**: lưới 12 ngân hàng (MBBank, Vietcombank, Techcombank, BIDV, ACB, VietinBank, VPBank, TPBank, Agribank, Sacombank, OCB, MSB). Mỗi nút trỏ `https://dl.vietqr.io/pay?app={appId}&ba={SoTaiKhoan}@{BankId}&am={SoConLai}&tn={MaChuyenKhoan}&bn={TenChuTaiKhoan}` (tham số URL-encode). Danh sách `appId` lấy từ danh sách app chính thức của VietQR khi triển khai và kiểm trên điện thoại thật (§11.3). Lưới chỉ hiện trên màn hình nhỏ; màn hình lớn có liên kết "Đang dùng điện thoại?" để mở lưới.
4. **Sao chép 1 chạm**: ngân hàng, chủ tài khoản (chỉ hiển thị), số tài khoản, số tiền (số nguyên, không dấu chấm), nội dung — 3 nút sao chép cho STK, số tiền, nội dung; toast "Đã sao chép".
5. Ghi chú bắt buộc: "Giữ nguyên nội dung `TTPTHD{Id}` để hệ thống tự xác nhận. Sai nội dung, chủ trọ phải xác nhận tay."

**Khi số còn lại = 0** (E04): ẩn toàn bộ khối chuyển khoản, chỉ hiện biên lai của lần trả gần nhất và danh sách các lần đã trả (mỗi lần có nút xem/in biên lai).

**Khi đã trả một phần**: hiện thông báo "Đã nhận {SoTien} (đợt {N})" + liên kết biên lai, rồi khối chuyển khoản với QR mới bằng số còn lại (E01).

### 3.3 Cập nhật tức thời

- Layout khách thuê đã nạp SignalR và `thongbao.js` kết nối `ThongBaoHub` (`/thongBaoHub`). Không tạo hub mới; panel đăng ký thêm handler trên kết nối sẵn có.
- Sau khi ghi nhận tiền và commit, server gửi `Clients.User(tenantUserId).SendAsync("HoaDonThanhToanCapNhat", { hoaDonId, conLai, trangThai })`.
- Panel đang mở hóa đơn đó tải lại bằng AJAX: số còn lại = 0 → biên lai; còn lại > 0 → thông báo đợt + QR mới. Hóa đơn khác thì chỉ cập nhật dòng trong danh sách.
- Mất kết nối SignalR: panel có nút "Tôi đã chuyển khoản, kiểm tra lại" tải lại panel (không polling tự động).

### 3.4 Biên lai điện tử

Route: `GET /KhachThue/HoaDon/BienLai/{lichSuThanhToanId}` (khách) và `GET /QuanLyNhaTro/HoaDon/BienLai/{lichSuThanhToanId}` (quản lý, kiểm quyền chi nhánh). Áp dụng cho **mọi** dòng `LichSuThanhToan` chưa xóa mềm (cả tiền mặt, thu thủ công), không chỉ SePay.

Nội dung:

- Tiêu đề **BIÊN LAI XÁC NHẬN THU TIỀN PHÒNG TRỌ**.
- Huy hiệu xanh **ĐÃ THU TIỀN** nếu sau lần này hóa đơn đủ; ngược lại **XÁC NHẬN THU ĐỢT {N}** (N = thứ tự lần trả chưa xóa mềm theo `NgayThanhToan`, rồi theo Id).
- Mã hóa đơn (`MaHoaDon`, kèm `#HD{HoaDonId}`), mã tham chiếu ngân hàng (nếu có), ngày giờ thanh toán (giờ Việt Nam, `dd/MM/yyyy HH:mm:ss`), phương thức: "Chuyển khoản 24/7 (VietQR, tự động)" cho SePay; "Chuyển khoản" / "Tiền mặt" cho thu thủ công.
- Chi nhánh và địa chỉ, phòng, người thanh toán (người đứng tên hợp đồng), kỳ hóa đơn.
- Bảng: tổng hóa đơn, số tiền lần này, lũy kế đã thanh toán **tính đến lần này**, số còn nợ sau lần này; dòng "Tiền thừa {X}đ — chủ trọ sẽ liên hệ" nếu có thẻ `[TIEN-THUA:]`.
- Nút **In biên lai / Lưu PDF** dùng `window.print()` + `@media print` (ẩn menu, khổ A5 dọc).

## 4. Nhận giao dịch từ SePay

### 4.1 Endpoint webhook

`POST /api/webhooks/payment/sepay` — `[AllowAnonymous]`, `[IgnoreAntiforgeryToken]`, giới hạn body 64 KB.

1. `SePay:Enabled = false` → `404` (endpoint coi như không tồn tại). Sau đó **đọc raw body** (byte) trước khi parse JSON.
2. **Xác thực** theo `SePay:WebhookAuthMode`:
   - `Hmac` (mặc định): header `X-SePay-Timestamp` (Unix giây) và `X-SePay-Signature` = `sha256=` + hex(HMAC-SHA256(`{timestamp}.{rawBody}`, `WebhookSecret`)). So sánh constant-time. Lệch giờ quá `TimestampToleranceSeconds` (mặc định 300) → từ chối.
   - `ApiKey`: header `Authorization: Apikey {WebhookApiKey}`, so sánh constant-time. Chỉ dùng khi thử nhanh.
   - Sai/thiếu → `401`, không ghi gì, log Warning (không log secret, không log body).
3. JSON không hợp lệ, hoặc thiếu/sai kiểu trường bắt buộc (`id`, `transferType`, `transferAmount`, `transactionDate` đúng định dạng) → `400`.
4. Lỗi hệ thống khi ghi sổ (DB lỗi...) → `500` để SePay gửi lại; an toàn nhờ chống trùng (quyết định 16/10).
5. Mọi trường hợp còn lại (kể cả bỏ qua, trùng, không khớp) trả **`200` với body đúng `{"success": true}`**; SePay chỉ coi thành công khi status 200/201 và body đúng như vậy, trong 30 giây.

Payload dùng các trường: `id` (int), `gateway`, `transactionDate` (`yyyy-MM-dd HH:mm:ss`, giờ Việt Nam), `accountNumber`, `subAccount`, `code` (có thể `null`), `content`, `transferType` (`in`/`out`), `transferAmount` (int VND), `referenceCode`, `description`.

Web chỉ parse và đổi `transactionDate` sang UTC (`VietnamTime`), rồi gọi Application với DTO `IncomingBankTransfer` (đã UTC, không chứa kiểu ASP.NET).

### 4.2 Phân loại giao dịch (Application, thuần)

Theo thứ tự, dừng ở bước đầu tiên khớp:

| Bước | Điều kiện | Kết quả |
| --- | --- | --- |
| 1 | `transferType != "in"` | `Ignored` — không làm gì |
| 2 | `accountNumber` khác `VietQRSettings.AccountNumber` (so sau khi bỏ khoảng trắng) | `WrongAccount` — cảnh báo Telegram |
| 3 | `transferAmount <= 0` | `Ignored` |
| 4 | Lấy mã: ưu tiên `code`; nếu `null`/rỗng thì regex `TTPT\s*HD\s*([0-9]{1,9})(?![0-9])` (không phân biệt hoa thường; chỉ chữ số ASCII; quá 9 chữ số là không khớp) trên `content` rồi `description` | Không có → `Unmatched` — cảnh báo Telegram (E03) |
| 5 | Mã khớp → `HoaDonId` | Chuyển §5 |

Bộ phân loại là một bảng "tiền tố → bộ xử lý". Đợt 1 chỉ đăng ký `TTPTHD` → hóa đơn. Sau này thanh toán giữ chỗ thêm tiền tố riêng và bộ xử lý riêng, không sửa phần ghi sổ hóa đơn (§4.4).

### 4.3 Đồng bộ bù (cổng quản lý)

Lý do: SePay chỉ thử lại tối đa 7 lần trong ~33 phút; server tắt lâu hơn thì webhook mất.

- Màn **Giao dịch SePay** `/QuanLyNhaTro/GiaoDichSePay`, **chỉ Admin**, mục menu trong nhóm hóa đơn/thanh toán.
- Chọn khoảng ngày (mặc định 3 ngày gần nhất, tối đa 31 ngày) → bấm **Đồng bộ**.
- Server gọi `GET {SePay:ApiBaseUrl}/transactions?transaction_date_from=...&transaction_date_to=...` (API v2, `Authorization: Bearer {ApiToken}`), duyệt hết các trang (`meta.pagination.has_more`), tôn trọng giới hạn 3 request/giây (gặp `429` thì chờ `Retry-After` rồi thử lại, tối đa 3 lần).
- Mỗi giao dịch tiền vào đưa qua **đúng** §4.2 và §5 như webhook (cùng khóa chống trùng), nên chạy lại bao nhiêu lần cũng không ghi đôi.
- Kết quả hiển thị (không lưu DB): bảng giao dịch với cột trạng thái `Mới ghi nhận` / `Đã có` / `Không khớp mã` / `Hóa đơn không nhận tiền` / `Thiếu mã tham chiếu` / `Bỏ qua`, kèm tổng số mỗi loại. Dòng `Mới ghi nhận` cũng phát thông báo như webhook (§6).
- Giao dịch API không có `reference_number` → `Thiếu mã tham chiếu`, **không** tự ghi (không chống trùng được với webhook); Admin xử lý bằng "Thu thủ công".
- `SePay:ApiToken` trống → nút Đồng bộ bị khóa, hiện hướng dẫn cấu hình.

### 4.4 Điểm mở rộng cho thanh toán chung

- `IIncomingTransferHandler` (Application): `Prefix`, `HandleAsync(IncomingBankTransfer, string code)`. Đợt 1 có một handler cho hóa đơn.
- Webhook và đồng bộ bù chỉ phụ thuộc bộ phân loại, không biết hóa đơn hay giữ chỗ.
- Không đụng `ReservationPaymentService`.

## 5. Ghi sổ hóa đơn

### 5.1 Quy tắc

Dùng `InvoicePaymentTransaction.RunAsync(hoaDonId, actorId: null, ...)` hiện có: transaction → `SELECT ... FOR UPDATE` dòng `hoa_don` → ghi `HetHan` cho lượt quá hạn → logic → lưu → commit → đẩy thông báo SignalR.

Trong khóa:

1. **Đã xử lý?** Có dòng `LichSuThanhToan` của hóa đơn này (**kể cả đã xóa mềm**) với cùng khóa chống trùng → `Duplicate`, không làm gì, không thông báo. Dòng đã bị Admin "hủy ghi nhận" sẽ không bị webhook retry hay đồng bộ bù ghi lại.
2. **Hóa đơn nhận tiền được?** Tồn tại, chưa xóa, `TrangThaiPhatHanh == DaGui`, `TongTienLaSoNguyen()`. Không thỏa (đã hủy, bản cũ đã thay bằng `-R{n}`, chưa công bố) → `InvoiceNotPayable`, không ghi, cảnh báo Telegram kèm mã hóa đơn thay thế nếu có.
3. **Đã trả đủ?** Số còn lại = 0 → `AlreadyPaid`, không ghi (ledger không nhận dòng 0đ và không vượt tổng), cảnh báo Telegram "chuyển thêm {X}đ cho hóa đơn đã trả đủ".
4. **Ghi nhận:** dùng `InvoicePaymentPolicy.DecideRecording(thucNhan, soTienLuot: null, conLai, isAdmin: true)` — hệ thống được xử lý như Admin:
   - `thucNhan <= conLai` → ghi đúng `thucNhan` (đủ hoặc một phần, E01).
   - `thucNhan > conLai` → ghi `conLai`, `GhiChu` gắn `PaymentNoteTags.TienThua(thucNhan − conLai, ...)` (E02).
5. Dòng `LichSuThanhToan` mới:
   - `MaGiaoDich` = khóa chống trùng (§5.2); `SoTienThanhToan` theo bước 4; `PhuongThucThanhToan = ChuyenKhoan`;
   - `NgayThanhToan` = `transactionDate` (UTC); `NgayXacNhan` = giờ server; `NguoiXacNhanId = null` (hệ thống);
   - `GhiChu` = `[SEPAY] {gateway} · REF {referenceCode} · ND: {content}` (cắt còn 500 ký tự; xóa ký tự `[` `]` trong nội dung ngân hàng để không giả được thẻ hệ thống).
6. `HoaDon.CapNhatTrangThaiThanhToan(tongMoi, nguoiThucHienId: null, lyDo: "[TU_DONG] SePay ghi nhận {SoTien}đ, REF {referenceCode}", now)` → `ThanhToanMotPhan` hoặc `DaThanhToan`.
7. **Lượt cũ:** lượt `ChoThanhToan` đang hoạt động → `Huy(null, now, "Đã nhận chuyển khoản tự động")`. Lượt `DangDoiChieu` (khách đã nộp minh chứng trước khi gỡ tính năng) giữ nguyên; thông báo nhân viên có thêm câu "Hóa đơn còn minh chứng chờ đối chiếu, kiểm tra trùng trước khi xác nhận".
8. Vi phạm unique index `MaGiaoDich` khi commit (hai request song song qua được bước 1 — không xảy ra vì có khóa dòng, nhưng vẫn bắt) → `Duplicate`.

### 5.2 Khóa chống trùng

- Có `referenceCode` (webhook) / `reference_number` (API): `SEPAY-{referenceCode}` (viết hoa, bỏ khoảng trắng). Webhook và đồng bộ bù cùng một khóa.
- Webhook không có `referenceCode`: `SEPAY-ID-{id}`. Đồng bộ bù không tự ghi giao dịch thiếu mã tham chiếu (§4.3), nên không thể ghi đôi.
- Unique index sẵn có trên `MaGiaoDich` (lọc dòng chưa xóa) là lớp chặn thứ hai; bước 1 của §5.1 chặn cả dòng đã xóa mềm.
- Cần kiểm trong Test mode SePay rằng `referenceCode` (webhook) và `reference_number` (API v2) của cùng giao dịch trùng nhau (§11.3). Nếu không trùng, đồng bộ bù chuyển sang so `id` và spec sẽ được sửa trước khi code phần này.

### 5.3 Kết quả không ghi

`Ignored`, `WrongAccount`, `Unmatched`, `InvoiceNotPayable`, `AlreadyPaid` không ghi DB. Để retry/replay của SePay không bắn cảnh báo Telegram nhiều lần, lưu khóa chống trùng của các kết quả này trong `IMemoryCache` 24 giờ (mất khi khởi động lại — chấp nhận).

### 5.4 Chặn ghi trùng khi xác nhận minh chứng cũ (làm ở phần 2)

Review phần 1 (16/10): webhook ghi sổ khi còn lượt `DangDoiChieu`; nếu sau đó nhân viên xác nhận minh chứng của lượt đó với số thực nhận ≤ số còn lại, `InvoicePaymentConfirmationService.ConfirmAsync` vẫn ghi thêm (mã giao dịch khác `SEPAY-...` nên không bị chặn). Phần 2 thêm chặn cứng, cần người dùng xác nhận logic tiền:

- Hóa đơn có dòng `SEPAY-` do hệ thống ghi sau thời điểm khách nộp minh chứng → nhân viên phải chuyển Admin, không tự xác nhận.
- Khi nhập mã giao dịch ngân hàng, kiểm thêm `SEPAY-{mã}` đã tồn tại.
- Test cho các ca: đã thu đủ, thu một phần, mã trùng.

Ghi chú vận hành: tiền tố mã thanh toán khai báo trên SePay phải là `TTPTHD` hoặc để trống, vì trường `code` được ưu tiên (§4.2 bước 4).

## 6. Thông báo sau khi ghi nhận

### 6.1 Thứ tự và độ tin cậy

1. Trong transaction: tạo `ThongBao` trong app (qua `scope`, như các luồng thanh toán hiện có).
2. Sau commit, chạy ngay: SignalR cho khách (§3.3) và cho người nhận `ThongBao`.
3. Sau commit, đưa vào **hàng đợi trong process** (`Channel`), một worker nền gửi: Telegram (§6.2), email biên lai (§6.3).
   - Worker đăng ký **không phụ thuộc** `BackgroundJobs:Enabled` (chạy cả khi tắt job định kỳ).
   - Lỗi gửi chỉ log Warning kèm `HoaDonId`, `LichSuThanhToanId`; không thử lại, không làm hỏng giao dịch đã ghi.
   - App khởi động lại khi hàng đợi chưa gửi → mất tin; bù bằng nút "Gửi lại biên lai" (§7.2). Không có outbox vì không đổi schema.

### 6.2 Telegram

- Gọi `https://api.telegram.org/bot{BotToken}/sendMessage`, `parse_mode=HTML`, escape mọi chuỗi động (`&`, `<`, `>`).
- Người nhận: `Telegram:DefaultChatId`, cộng `Telegram:BranchChatIds[{ChiNhanhId}]` nếu có (khác chat chung). Cảnh báo không xác định được chi nhánh (`Unmatched`, `WrongAccount`) chỉ gửi chat chung.
- `Telegram:Enabled = false` hoặc thiếu token → bỏ qua, log Information một lần khi khởi động.
- Mẫu tin (soạn bằng formatter thuần ở Application để unit test):

**Đủ tiền**
```
✅ THU TIỀN TRỌ THÀNH CÔNG
🏢 Chi nhánh: {TenChiNhanh}
🏠 Phòng: {TenPhong}
📅 Kỳ: Tháng {MM}/{yyyy}
👤 Khách thuê: {HoVaTen}
💰 Vừa nhận: +{SoTienThucNhan} VNĐ
📊 Tiến độ {MaHoaDon}: {DaThu}/{TongTien} VNĐ
📌 Trạng thái: ĐÃ THANH TOÁN ĐỦ
💳 Kênh: Chuyển khoản VietQR ({gateway})
🧾 Mã GD: {referenceCode}
🕒 {dd/MM/yyyy HH:mm:ss}
```
**Một phần:** tiêu đề `⚠️ THU TIỀN TRỌ (ĐÓNG MỘT PHẦN)`, thay dòng trạng thái bằng `❗ Còn thiếu: {ConLai} VNĐ`.
**Dư tiền:** như "Đủ tiền", thêm `💡 Khách đóng dư +{TienThua} VNĐ (đã ghi chú, cần xử lý ngoài hệ thống)`. "Vừa nhận" là số thực nhận.
**Cảnh báo** (`Unmatched`, `WrongAccount`, `InvoiceNotPayable`, `AlreadyPaid`): tiêu đề `🔔 CHUYỂN KHOẢN CẦN XỬ LÝ TAY`, lý do, số tiền, nội dung gốc, mã GD, thời gian, gợi ý "Vào Hóa đơn → Thu thủ công".

Số tiền định dạng `InvoicePaymentPolicy.FormatVnd` (1.900.000).

### 6.3 Email biên lai

- Hàm mới `IEmailService.SendPaymentReceiptAsync(toEmail, PaymentReceiptEmail data)` (MailKit), cài mặc định trả lỗi "chưa hỗ trợ" như các hàm mở rộng hiện có để fake test không vỡ.
- Người nhận: email của người đứng tên hợp đồng (`NguoiThue.Email`). Không có email hợp lệ → log Warning, bỏ qua.
- Nội dung: các trường của biên lai (§3.4), nút "Xem biên lai" → `{PublicBaseUrl}/KhachThue/HoaDon/BienLai/{id}`. Không đính kèm PDF.
- Chỉ tự gửi cho giao dịch SePay. Thu thủ công không tự gửi (giữ hành vi cũ), gửi được bằng nút §7.2.

### 6.4 Thông báo trong app

- Người nhận: `GetReviewerUserIdsAsync(chiNhanhId)` (Admin + nhân viên chi nhánh) và tài khoản khách thuê đứng tên.
- Tiêu đề: "Đã nhận chuyển khoản {SoTien}đ — {TenPhong}". Link: chi tiết hóa đơn (quản lý) / `/KhachThue/HoaDon?hoaDonId=` (khách).
- Tiền thừa thêm câu cho Admin; lượt `DangDoiChieu` thêm câu ở §5.1 bước 7.

## 7. Cổng quản lý

### 7.1 Email hóa đơn có nút thanh toán

- `SendInvoiceEmailAsync` (dùng khi công bố hóa đơn và trong `InvoiceReminderJob`) thêm nút lớn **THANH TOÁN TRỰC TUYẾN** → link §3.1, và dòng "Nội dung chuyển khoản: `TTPTHD{Id}`".
- `PublicBaseUrl` trống → không chèn nút, log Warning một lần.
- QR trong PDF hóa đơn (`InvoicePdfComposer`) đổi nội dung từ `THANH TOAN {MaHoaDon}` sang mã chuyển khoản, để quét QR trên PDF cũng tự khớp. Số tiền giữ là số còn lại.

### 7.2 Chi tiết hóa đơn

- **Gửi lại email hóa đơn**: đã có (`POST /HoaDon/SendEmail/{id}` → `InvoicePublicationService.ResendEmailAsync`). Không thêm nút mới; email gửi lại tự có nút thanh toán nhờ §7.1.
- Mỗi dòng lịch sử thanh toán: nhãn **SePay** khi `MaGiaoDich` bắt đầu `SEPAY-`, hiện mã tham chiếu; nút **Biên lai** (mở §3.4) và **Gửi lại biên lai** (email §6.3; quyền `InvoiceSend`; không gửi cho dòng đã hủy ghi nhận).
- Giữ nguyên: Thu thủ công, Hủy ghi nhận (Admin), cấu hình trả một phần.

### 7.3 Màn đối chiếu minh chứng

Giữ nguyên để xử lý nốt lượt cũ. Không còn lượt mới vì khách không tạo được.

### 7.4 Màn Giao dịch SePay

Xem §4.3.

## 8. Gỡ luồng thủ công phía khách

- Gỡ endpoint `POST /KhachThue/HoaDon/ThanhToan/Tao`, `.../Huy/{yeuCauId}`, `.../NopMinhChung` và `GET .../QR/{yeuCauId}` (thay bằng QR theo `hoaDonId`, §3.2), cùng phần JS/HTML tương ứng trong `khachthue-thanhtoan.js` và view.
- `GET .../AnhMinhChung/{minhChungId}` giữ lại để khách xem minh chứng cũ trong lịch sử.
- Application: không xóa entity, store, `InvoicePaymentConfirmationService` (đối chiếu lượt cũ cần). Phương thức tạo lượt/nộp minh chứng trong `InvoicePaymentRequestService` không còn được Web gọi; dọn ở đợt sau (§14).

## 9. Kiến trúc

### 9.1 Application — `Features/OnlinePayments/` (phẳng như feature giữ chỗ)

| Thành phần | Vai trò |
| --- | --- |
| `IncomingBankTransfer` | DTO giao dịch đã chuẩn hóa (UTC) |
| `TransferCodeParser` | Lấy mã từ `code`/`content`/`description` (§4.2), thuần |
| `IncomingTransferClassifier` + `IIncomingTransferHandler` | Phân loại theo tiền tố (§4.2, §4.4) |
| `InvoiceTransferHandler` | Ghi sổ hóa đơn (§5) qua `InvoicePaymentTransaction` |
| `IBankTransferIngestionService` | Điểm vào chung cho webhook và đồng bộ bù; trả `TransferOutcome` |
| `ISePayTransactionClient` | Trừu tượng gọi API v2 (Infrastructure cài) |
| `ISePaySyncService` | Đồng bộ bù, `actorId` đầu tiên, chỉ Admin, fail closed |
| `PaymentNotificationFormatter` | Soạn tin Telegram và dữ liệu email, thuần |
| `IPaymentNotificationQueue` | Hàng đợi hậu xử lý |
| `ITelegramNotifier` | Gửi tin Telegram (Infrastructure cài) |
| `IInvoiceReceiptService` | Dữ liệu biên lai (khách theo `NguoiThueId`, quản lý theo chi nhánh) và gửi lại biên lai |
| `SePayOptions`, `TelegramOptions`, `OnlinePaymentOptions` | Typed options ở `Common/Configurations`, có `Validate()` |

Ngoại lệ `BranchScopeArchitectureTests`: `IBankTransferIngestionService.IngestAsync` không có `actorId` — được gọi từ webhook hệ thống đã xác thực chữ ký SePay, hoặc từ `ISePaySyncService` đã kiểm Admin.

### 9.2 Infrastructure

- `SePayWebhookVerifier` (HMAC/API key, constant-time), `SePayTransactionClient` (`HttpClient`, API v2), `TelegramBotNotifier` (`HttpClient`), `ChannelPaymentNotificationQueue` + `PaymentNotificationWorker` (hosted, luôn đăng ký).
- `MailKitEmailService.SendPaymentReceiptAsync`; mẫu email hóa đơn thêm nút thanh toán.
- Store: thêm truy vấn "có dòng `LichSuThanhToan` của hóa đơn với `MaGiaoDich` này, kể cả đã xóa mềm" và dữ liệu biên lai (thứ tự đợt, lũy kế).
- Bind và `Validate()` options trong `AddInfrastructure`.

### 9.3 Web

- `Controllers/Api/SePayWebhookController` (route `/api/webhooks/payment/sepay`).
- `Areas/KhachThue`: sửa `ThanhToanHoaDonController` (panel, QR theo hóa đơn, biên lai), view/JS panel.
- `Areas/QuanLyNhaTro`: `GiaoDichSePayController` (Admin), nút trên chi tiết hóa đơn, route biên lai, mục menu trong `MenuService`.
- `SignalRThongBaoNotifier` thêm phương thức gửi sự kiện `HoaDonThanhToanCapNhat`.
- Không đổi `Program.cs` ngoài những gì `AddInfrastructure`/`AddApplication` đăng ký.

## 10. Cấu hình

Thêm vào `appsettings.example.json` (giá trị mẫu, không secret thật):

```jsonc
"OnlinePayment": {
  // URL công khai của web, dùng cho link trong email (ví dụ https://abc.trycloudflare.com). Trống = không chèn link.
  "PublicBaseUrl": ""
},
"SePay": {
  "Enabled": false,
  // Hmac (khuyến nghị) hoặc ApiKey
  "WebhookAuthMode": "Hmac",
  "WebhookSecret": "<SEPAY_WEBHOOK_SECRET>",
  "WebhookApiKey": "",
  "TimestampToleranceSeconds": 300,
  // Live: https://userapi.sepay.vn/v2 · Test mode: host userapi-sandbox.sepay.vn (kiểm đường dẫn /v2 khi cài)
  "ApiBaseUrl": "https://userapi.sepay.vn/v2",
  "ApiToken": ""
},
"Telegram": {
  "Enabled": false,
  "BotToken": "<TELEGRAM_BOT_TOKEN>",
  "DefaultChatId": "<CHAT_ID_NHOM_CHU_TRO>",
  // ChiNhanhId -> ChatId, tùy chọn
  "BranchChatIds": { }
},
"VietQRSettings": {
  // thêm: tên ngân hàng hiển thị trên panel
  "BankName": "<TEN_NGAN_HANG>"
}
```

`Validate()`: `SePay:Enabled = true` thì bắt buộc secret/key tương ứng với chế độ; `TimestampToleranceSeconds` trong 30–900; `ApiBaseUrl` là URL https. `Telegram:Enabled = true` thì bắt buộc `BotToken` và `DefaultChatId`. `PublicBaseUrl` nếu có phải là URL tuyệt đối.

Không ghi đè `appsettings.json` của người dùng.

## 11. Kiểm thử

### 11.1 Unit (Application.UnitTests)

- `TransferCodeParser`: `code` có/không; `TTPTHD105`, `ttpt hd 105`, `TTPT HD105-FT26...`, nội dung không có mã, hai mã (lấy mã đầu).
- Classifier: `out`, sai số tài khoản, số tiền 0, không mã, có mã.
- `InvoiceTransferHandler` với fake store: đủ, một phần, dư (thẻ tiền thừa), đã trả đủ, hóa đơn đã hủy/chưa công bố, trùng (kể cả dòng đã xóa mềm), hủy lượt `ChoThanhToan`, giữ lượt `DangDoiChieu`.
- Formatter Telegram: 4 mẫu, escape HTML.
- `ISePaySyncService`: nhân viên bị từ chối; phân trang; thiếu `reference_number`.
- Options `Validate()`.

### 11.2 Integration

- **Infrastructure:** hai lần ghi song song cùng giao dịch → 1 dòng (E05); truy vấn trùng tính cả dòng đã xóa mềm; dữ liệu biên lai (đợt N, lũy kế).
- **Web** (`[Collection(WebTestCollection.Name)]`, fake Telegram/SePay client/email):
  - webhook: sai chữ ký → 401; timestamp lệch → 401; `out` → 200 `{"success":true}`, không ghi; gửi cùng payload 3 lần → 1 dòng, 1 tin Telegram (TC05); đủ tiền → hóa đơn `DaThanhToan`; thiếu → `ThanhToanMotPhan`;
  - panel khách: chỉ người đứng tên xem được; khách khác → 403/404; hóa đơn đã trả đủ không có QR;
  - biên lai: khách chỉ xem biên lai của mình; nhân viên chỉ xem chi nhánh được phân công;
  - màn Giao dịch SePay: nhân viên → bị chặn;
  - đăng nhập với `ReturnUrl=/KhachThue/HoaDon?hoaDonId=` → quay lại đúng trang;
  - endpoint tạo lượt/nộp minh chứng đã gỡ → 404.
- Kiến trúc: `ArchitectureBoundaryTests`, `InfrastructureBoundaryTests`, `BranchScopeArchitectureTests` PASS.

### 11.3 Thử tay (người dùng có tài khoản SePay Test mode, bot Telegram, tunnel)

1. **TC01:** công bố hóa đơn → mở email → bấm nút → đăng nhập → đúng hóa đơn.
2. **TC02:** mở panel trên PC, tạo giao dịch mô phỏng trong SePay Test mode với nội dung `TTPTHD{Id}` đúng số tiền → panel đổi sang biên lai < 5 giây, Telegram nhận tin, email biên lai tới.
3. **E01/E02:** mô phỏng thiếu rồi dư.
4. **E03:** mô phỏng nội dung sai → Telegram cảnh báo, hóa đơn không đổi.
5. **TC05:** dùng Replay của SePay 3 lần → 1 dòng ledger, 1 tin Telegram.
6. **Đồng bộ bù:** tắt app, mô phỏng giao dịch, đợi SePay hết retry hoặc tắt webhook, bật app, bấm Đồng bộ → ghi nhận đúng; bấm lại → "Đã có". Đồng thời kiểm `referenceCode` = `reference_number` (§5.2).
7. **TC04:** mở panel trên điện thoại thật: từng nút deeplink mở đúng app với số tiền và nội dung điền sẵn (ghi lại app nào không hỗ trợ), tải ảnh QR, sao chép.

Giới hạn Test mode SePay: 50 webhook mỗi công ty và hạn mức mô phỏng mỗi ngày — dùng tiết kiệm.

### 11.4 Môi trường sandbox (không chuyển tiền thật)

Mục tiêu: chạy trọn luồng ghi sổ → biên lai → email → Telegram bằng giao dịch mô phỏng.

1. **SePay Test mode** (my.sepay.vn → Test mode):
   - Bank Accounts → Add New: tạo tài khoản giả (ví dụ MBBank, cá nhân). Không cần liên kết ngân hàng thật.
   - Webhooks → Add Webhook: URL `{tunnel}/api/webhooks/payment/sepay`, sự kiện tiền vào, xác thực HMAC-SHA256; chép secret vào `SePay:WebhookSecret`.
   - Transactions → Simulate: chọn tài khoản giả, loại In, số tiền, nội dung `TTPTHD{Id}`.
   - Theo dõi: Webhooks → Delivery Logs.
2. **`appsettings.json` máy phát triển** (không commit):
   - `VietQRSettings:AccountNumber` = số tài khoản Test mode, để webhook không bị loại ở bước `WrongAccount` (§4.2) và QR hiển thị trỏ vào tài khoản giả.
   - `SePay:Enabled = true`, `SePay:ApiBaseUrl` = host sandbox `userapi-sandbox.sepay.vn` (kiểm đường dẫn `/v2`), `SePay:ApiToken` = token Test mode (cho đồng bộ bù).
   - `Telegram:*` = bot thật (miễn phí); `EmailSettings` = Gmail App Password; `OnlinePayment:PublicBaseUrl` = URL tunnel.
3. **Tunnel: ngrok với domain tĩnh miễn phí** (người dùng chọn 16/10), để URL webhook trên SePay và `PublicBaseUrl` chỉ khai báo một lần.
   - Người dùng tự đăng ký ngrok, cài (`winget install ngrok.ngrok`), chạy `ngrok config add-authtoken ...`, lấy domain tĩnh trên dashboard.
   - Mở tunnel bằng lệnh viết trong `huong-dan-thiet-lap.md`: `ngrok http --url={domain-tĩnh} 5009`. URL webhook dán vào SePay là `https://{domain-tĩnh}/api/webhooks/payment/sepay`, `PublicBaseUrl` là `https://{domain-tĩnh}`.
   - Tunnel mở cả trang web ra Internet: chỉ bật khi thử, tắt ngay sau đó, dùng mật khẩu Admin mạnh.
4. **Không có script phụ trợ** (người dùng quyết định 16/10/2026). Thử luồng webhook không cần tài khoản đã nằm trong test xUnit (`SePayWebhookTests`, `SePayWebhookBehaviourTests`: tự ký HMAC, gửi trùng, gửi song song). Thử thật dùng Test send / Simulate của SePay Test mode qua tunnel. Hướng dẫn thao tác tay chỉ viết dạng lệnh và bước trong `huong-dan-thiet-lap.md`.

Kịch bản §11.3 chạy được hoàn toàn trên sandbox; chỉ TC04 (deeplink) cần điện thoại thật, và app ngân hàng sẽ mở với tài khoản giả nên dừng ở màn xác nhận, không chuyển.

## 12. Đợt 2 (VNPay) — chỉ để định hướng, không làm ở đợt 1

- Tab "Thẻ ATM / Visa / MasterCard (VNPay)" là **kênh phụ**; chuyển khoản QR vẫn là mặc định. Thẻ có phí giao dịch và chạy thật cần đăng ký merchant; đồ án dùng sandbox.
- IPN `GET /api/webhooks/payment/vnpay-ipn` và ReturnUrl; xác thực HMAC-SHA512; `vnp_TxnRef = HD{HoaDonId}_{Ticks}`; khóa chống trùng `VNPAY-{vnp_TransactionNo}`; ghi sổ qua cùng `InvoicePaymentTransaction`, cùng thông báo §6.
- Spec riêng sau khi đợt 1 xong.

## 13. Điều chỉnh so với tài liệu gốc của người dùng

| Tài liệu gốc | Spec này | Lý do |
| --- | --- | --- |
| Magic link `/thanh-toan/{token}` không cần đăng nhập | Link vào cổng khách thuê, đăng nhập rồi quay lại | Người dùng chọn (16/10) |
| Nội dung `TTPT HD{Id}` | `TTPTHD{Id}` (vẫn nhận có dấu cách) | SePay tách mã theo tiền tố liền, phân biệt hoa thường |
| Xác thực webhook bằng API key | HMAC-SHA256 mặc định, API key tùy chọn | SePay khuyến nghị HMAC; người dùng đồng ý (16/10) |
| Chỉ webhook | Thêm đồng bộ bù qua API | SePay chỉ retry ~33 phút; người dùng đồng ý |
| Chống trùng bằng `GhiChu.Contains` | Cột `MaGiaoDich` + khóa dòng hóa đơn + unique index | Race-free, không đổi schema |
| Thiếu tiền giữ `ChuaThanhToan` | `ThanhToanMotPhan` | Enum hiện có |
| Dư tiền ghi đủ 3.900.000đ | Ghi đủ nợ, phần dư gắn thẻ `[TIEN-THUA:]` | Domain chặn tổng trả vượt tổng tiền; quyết định 02/10 |
| Hub `PaymentHub` riêng, nhóm `HoaDon_{Id}` | Dùng `ThongBaoHub`, gửi theo user | Khách đã đăng nhập; không cần hub ẩn danh |
| Ảnh QR từ `img.vietqr.io`, tải bằng `fetch` | QR sinh ở server, tải qua endpoint của server | Không phụ thuộc dịch vụ ngoài, không lỗi CORS |
| Telegram một `LandlordChatId` | Chat chung + chat theo chi nhánh | Người dùng chọn (16/10) |
| Khách tự tạo lượt + nộp minh chứng | Gỡ ở cổng khách | Người dùng chọn (16/10) |
| Nút giả lập tiền vào | Dùng Test mode SePay | Người dùng chọn (16/10) |
| `KhachThue`, `HoaDon.Id`, AdminLTE | `NguoiThue`, `HoaDonId`, Tabler | Theo code |

## 14. Giới hạn chấp nhận và hướng sau

- Thông báo Telegram/email nằm trong hàng đợi bộ nhớ; khởi động lại có thể mất (bù bằng "Gửi lại biên lai").
- Giao dịch không khớp không được lưu trong hệ thống; dấu vết nằm ở Telegram, log, nhật ký SePay và màn đồng bộ bù.
- Gõ nhầm mã sang hóa đơn khác vẫn được ghi cho hóa đơn đó; Admin "Hủy ghi nhận" rồi "Thu thủ công" cho đúng hóa đơn.
- Một tài khoản ngân hàng chung cho mọi chi nhánh.
- Tiền thừa chỉ được đánh dấu, xử lý ngoài hệ thống.
- Docker (Dockerfile + compose gồm web, PostgreSQL, tùy chọn tunnel; lưu key Data Protection; secret qua biến môi trường) không làm trong nhánh này; spec riêng khi cần deploy demo (người dùng chọn 16/10).
- Dọn mã tạo lượt/nộp minh chứng không còn dùng; outbox cho thông báo; lưu giao dịch không khớp — cần đổi schema, làm spec riêng.

## 15. Điều kiện nghiệm thu

- [ ] §11.1, §11.2 PASS; `dotnet build` không lỗi; `has-pending-model-changes` không có thay đổi.
- [ ] TC01, TC02, E01, E02, E03, TC05, đồng bộ bù chạy đúng trên SePay Test mode (người dùng thử tay).
- [ ] TC04 trên ít nhất một điện thoại Android hoặc iOS; ghi lại app ngân hàng không hỗ trợ deeplink.
- [ ] Hướng dẫn thiết lập SePay Test mode, bot Telegram, ngrok ở `huong-dan-thiet-lap.md` (lệnh và từng bước, không có script).
- [ ] `README.md` module, `docs/features/README.md` và mục Hiện trạng trong `AGENTS.md` được cập nhật.
