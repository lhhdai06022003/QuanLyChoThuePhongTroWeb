# Spec: Thanh toán hóa đơn qua VNPay — đợt 2 của thanh toán trực tuyến

- **Trạng thái:** đã duyệt (25/10/2026). Quyết định ở §2 (Q11–Q13 chốt cùng ngày khi duyệt); kế hoạch ở [plan.md](plan.md).
- **Nhánh:** `feature/thanh-toan-truc-tuyen` (người dùng chọn làm tiếp trên nhánh này, 25/10/2026).
- **Nối tiếp:** [thanh toán trực tuyến đợt 1](../sepay/spec.md), cụ thể là §12 (định hướng VNPay). Spec này dùng lại cách ghi sổ (§5), khóa chống trùng, thông báo (§6), biên lai (§3.4) của đợt 1.
- **Schema:** không đổi. Kiểm bằng `has-pending-model-changes` trước khi bàn giao.
- **Nguồn kỹ thuật:** tài liệu VNPay sandbox `https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html` (đọc ngày 25/10/2026). Chỗ tài liệu không thống nhất có ghi ở §5.3; phải thử trên sandbox trước khi coi là đúng.

## 1. Mục tiêu

Khách thuê có thêm một cách trả hóa đơn: thanh toán bằng thẻ ATM nội địa, Visa, MasterCard (hoặc phương thức khác mà trang VNPay cho chọn). Tiền về thì hệ thống tự ghi sổ và gửi thông báo giống hệt khi nhận chuyển khoản qua SePay.

1. Khách mở panel thanh toán, chọn tab **Thẻ ATM / Visa / MasterCard (VNPay)**.
2. Khách bấm **Thanh toán qua VNPay**. Hệ thống tạo link với số tiền bằng số còn lại và chuyển khách sang trang VNPay.
3. Khách trả tiền trên trang VNPay.
4. VNPay gọi IPN về hệ thống. Hệ thống kiểm chữ ký, ghi sổ `LichSuThanhToan`, cập nhật trạng thái hóa đơn, gửi thông báo trong app, SignalR, Telegram và email biên lai.
5. VNPay đưa khách về trang kết quả (ReturnUrl). Trang này chỉ hiển thị, không ghi sổ.

### 1.1 Phạm vi

| Có trong đợt 2 | Không có trong đợt 2 |
| --- | --- |
| Hóa đơn của khách thuê (`Areas/KhachThue`) | Giữ chỗ, đặt cọc của khách vãng lai (thuộc cổng của `thuan`) |
| Tạo link thanh toán, IPN, trang kết quả | Đối soát bù qua API truy vấn giao dịch của VNPay (`querydr`) |
| Ghi sổ, thông báo, biên lai dùng chung với SePay | Hoàn tiền qua API VNPay (`refund`); hoàn tiền làm tay trên trang merchant |
| Nhãn VNPay trên biên lai, lịch sử thanh toán, Telegram | Cộng phí thẻ vào số tiền khách trả |
| Sandbox VNPay | Chạy thật (cần đăng ký merchant) |

## 2. Quyết định đã chốt (người dùng, 25/10/2026)

| # | Câu hỏi | Quyết định |
| --- | --- | --- |
| Q1 | Khách chọn VNPay thế nào | Khách tự chọn kiểu thanh toán trong panel. Chuyển khoản QR vẫn là tab mặc định. |
| Q2 | Trả một phần qua VNPay | Không. Số tiền cố định bằng số còn lại lúc tạo link, khách không sửa được. Ghi sổ theo quy tắc của SePay. |
| Q3 | Nguồn ghi sổ | Chỉ IPN được ghi sổ. ReturnUrl chỉ hiển thị trạng thái. |
| Q4 | Đối soát bù | Không làm ở đợt này. |
| Q5 | IPN báo thành công nhưng hóa đơn không còn nhận tiền | Làm giống SePay: không ghi sổ, trả VNPay mã `02` để dừng gọi lại, cảnh báo Telegram "cần hoàn tiền thủ công trên trang merchant VNPay". |
| Q6 | Phí thẻ | Không cộng vào số tiền khách trả. Sandbox không thu phí; khi chạy thật chủ trọ chịu (§13). |
| Q7 | Phương thức trên biên lai | Giữ `PhuongThucThanhToan = ChuyenKhoan`; nhãn "Thẻ ATM / Visa / MasterCard (VNPay)" nhận theo tiền tố `VNPAY-` của `MaGiaoDich`. Không đổi enum. |
| Q8 | Phạm vi | Chỉ hóa đơn của khách thuê. |
| Q9 | Nhánh | Làm tiếp trên `feature/thanh-toan-truc-tuyen`. |
| Q10 | Thiết lập môi trường | Người dùng tự đăng ký sandbox VNPay, cấu hình IPN, mở tunnel. Agent chỉ lập trình và viết hướng dẫn. |
| Q11 | Chặn ghi trùng dòng `VNPAY-` ở luồng thủ công | Có, giống quy tắc SePay chọn ngày 17/10 (§6.1). |
| Q12 | Thời hạn link VNPay | 15 phút (`VnPay:ExpireMinutes`). |
| Q13 | Chia việc | 2 lượt `/ship` (§14, [plan.md](plan.md)). |

## 3. Thuật ngữ

- **Mã đơn VNPay (`vnp_TxnRef`):** `HD{HoaDonId}_{Ticks}`, ví dụ `HD105_638654321098765432`. `Ticks` là `DateTime.UtcNow.Ticks` lúc tạo link, để mỗi lần bấm có một mã khác nhau (VNPay không cho trùng mã trong ngày). Hệ thống không lưu mã này; khi IPN về chỉ tách `HoaDonId` ra từ nó.
- **Mã giao dịch VNPay (`vnp_TransactionNo`):** mã VNPay cấp cho giao dịch.
- **Khóa chống trùng VNPay:** `VNPAY-{vnp_TransactionNo}`, ghi vào `LichSuThanhToan.MaGiaoDich`.
- **Số còn lại:** định nghĩa như đợt 1 (`TongTien − Σ SoTienThanhToan` của các dòng chưa xóa mềm).

## 4. Trải nghiệm khách thuê

### 4.1 Tab chọn kiểu thanh toán

Panel `/KhachThue/HoaDon/ThanhToan/{hoaDonId}` (đợt 1 §3.2) có hai tab khi số còn lại > 0 và `VnPay:Enabled = true`:

1. **Chuyển khoản QR** (mặc định, nội dung giữ nguyên đợt 1).
2. **Thẻ ATM / Visa / MasterCard (VNPay)**:
   - Số tiền sẽ thanh toán = số còn lại (chỉ đọc).
   - Ghi chú: "Bạn sẽ được chuyển sang trang VNPay. Không đóng trình duyệt cho tới khi VNPay đưa bạn về trang kết quả."
   - Ghi chú: "Nếu bạn vừa chuyển khoản QR cho hóa đơn này, hãy chờ hệ thống xác nhận trước khi thanh toán thẻ để không trả hai lần."
   - Nút **Thanh toán qua VNPay**.

`VnPay:Enabled = false` thì không hiện tab, panel giống đợt 1. Số còn lại = 0 thì ẩn cả hai tab như đợt 1 (chỉ hiện biên lai).

### 4.2 Tạo link thanh toán

- Nút gọi `POST /KhachThue/HoaDon/ThanhToan/VnPay/{hoaDonId}` (antiforgery qua header `RequestVerificationToken`, như các AJAX khác).
- Server kiểm lại ngay lúc bấm:
  - hóa đơn thuộc khách (claim `NguoiThueId`); không thuộc thì `NotFound`;
  - hóa đơn nhận tiền được (`DaGui`, chưa xóa, tổng tiền là số nguyên), cùng điều kiện với ghi sổ đợt 1 §5.1 bước 2;
  - số còn lại > 0.
- Đạt thì trả `{ success: true, url }`, trình duyệt chuyển sang `url`. Không đạt thì trả lỗi tiếng Việt và panel tải lại.
- Không ghi gì vào DB khi tạo link.

### 4.3 Trang kết quả (ReturnUrl)

- Route `GET /KhachThue/HoaDon/ThanhToan/VnPay/KetQua`, yêu cầu đăng nhập `KhachThue` như các trang khác của cổng. Cookie đăng nhập là `SameSite=Lax` nên trình duyệt vẫn gửi cookie khi VNPay chuyển về bằng GET.
- Trang kiểm chữ ký các tham số `vnp_*`, tách `HoaDonId` từ `vnp_TxnRef`, kiểm hóa đơn thuộc khách. Không ghi sổ.
- Nội dung hiển thị:

| Trường hợp | Hiển thị |
| --- | --- |
| Sai chữ ký, thiếu tham số, hóa đơn không thuộc khách | "Không xác thực được kết quả từ VNPay." Không hiện số tiền hay mã hóa đơn. Nút về danh sách hóa đơn. |
| `vnp_ResponseCode = 24` (khách hủy) | "Bạn đã hủy thanh toán." Nút quay lại hóa đơn. |
| Mã khác `00` | "Thanh toán chưa thành công" kèm mô tả theo bảng mã lỗi VNPay (§5.5). Nút quay lại hóa đơn. |
| `00` và sổ đã có dòng `VNPAY-{vnp_TransactionNo}` chưa xóa | "Đã ghi nhận thanh toán", số tiền, nút **Xem biên lai**. |
| `00` nhưng sổ chưa có dòng đó (IPN chưa tới) | "VNPay báo thanh toán thành công, hệ thống đang ghi nhận." Trang lắng nghe `HoaDonThanhToanCapNhat` (SignalR sẵn có) và có nút "Kiểm tra lại"; khi đã ghi nhận thì hiện như dòng trên. |

### 4.4 Sau khi ghi sổ

Giữ nguyên đợt 1 §3.3: SignalR `HoaDonThanhToanCapNhat` cho khách; panel đang mở tự chuyển sang biên lai. Biên lai web (đợt 1 §3.4) hiện phương thức "Thẻ ATM / Visa / MasterCard (VNPay)" và mã tham chiếu là `vnp_TransactionNo`.

## 5. Tích hợp VNPay

### 5.1 Tham số tạo link (`vnp_Command = pay`, phiên bản `2.1.0`)

| Tham số | Giá trị |
| --- | --- |
| `vnp_Version` | `2.1.0` |
| `vnp_Command` | `pay` |
| `vnp_TmnCode` | `VnPay:TmnCode` |
| `vnp_Amount` | số còn lại × 100 (số nguyên, không dấu phân cách) |
| `vnp_CurrCode` | `VND` |
| `vnp_TxnRef` | `HD{HoaDonId}_{Ticks}` (§3) |
| `vnp_OrderInfo` | `Thanh toan hoa don {MaHoaDon}` đã bỏ dấu và ký tự đặc biệt (VNPay yêu cầu tiếng Việt không dấu) |
| `vnp_OrderType` | `other` (mã danh mục hàng hóa; kiểm lại danh mục VNPay khi code) |
| `vnp_Locale` | `vn` |
| `vnp_ReturnUrl` | URL tuyệt đối của trang kết quả (§4.3), dựng từ host của request hiện tại |
| `vnp_IpAddr` | IP của khách (`HttpContext.Connection.RemoteIpAddress`); xem giới hạn ở §13 |
| `vnp_CreateDate` | giờ Việt Nam (GMT+7), `yyyyMMddHHmmss` |
| `vnp_ExpireDate` | `vnp_CreateDate` + `VnPay:ExpireMinutes` (mặc định 15 phút) |
| `vnp_BankCode` | không gửi, để khách chọn phương thức trên trang VNPay |
| `vnp_SecureHash` | chữ ký §5.3, gắn cuối URL |

### 5.2 IPN (`GET /api/webhooks/payment/vnpay-ipn`)

- Ẩn danh (`[AllowAnonymous]`, `[IgnoreAntiforgeryToken]`), chỉ GET. `VnPay:Enabled = false` thì trả 404, như webhook SePay.
- VNPay gọi IPN theo URL khai báo ở phía merchant (người dùng cấu hình trên sandbox), không theo tham số trong link.
- Trả JSON `{"RspCode":"..","Message":".."}` với HTTP 200. VNPay gọi lại khi nhận `01`, `04`, `97`, `99` hoặc hết thời gian chờ, tối đa 10 lần, cách nhau 5 phút; nhận `00` hoặc `02` thì dừng.
- Thứ tự xử lý:

| Bước | Điều kiện | Kết quả | `RspCode` |
| --- | --- | --- | --- |
| 1 | Thiếu `vnp_SecureHash` hoặc chữ ký sai | không ghi, log Warning (không log tham số) | `97` |
| 2 | `vnp_TmnCode` khác cấu hình | không ghi, log Warning | `97` |
| 3 | `vnp_TxnRef` không đúng dạng `HD{1–9 chữ số}_{số}` | không ghi | `01` (không tìm thấy đơn) |
| 4 | `vnp_ResponseCode ≠ 00` hoặc `vnp_TransactionStatus ≠ 00` (giao dịch không thành công) | không ghi, không thông báo | `00` (đã nhận kết quả) |
| 4b | Giao dịch thành công nhưng hóa đơn `HD{id}` không có trong DB (hóa đơn đã xóa mềm hoặc đã hủy vẫn có, đi tiếp bước 6) | không ghi, không cảnh báo hoàn tiền, log Warning | `01` (T2, người dùng chọn 26/10/2026; VNPay gọi lại tối đa 10 lần rồi dừng) |
| 5 | `vnp_Amount` không chia hết cho 100 hoặc ≤ 0 | không ghi, log Warning | `04` |
| 6 | Ghi sổ theo §6: `Recorded` | đã ghi, gửi thông báo | `00` |
| 6 | `Duplicate` | không ghi | `02` |
| 6 | `InvoiceNotPayable`, `AlreadyPaid` | không ghi, cảnh báo Telegram cần hoàn tiền (§7.2) | `02` |
| 6 | `Failed` (lỗi hệ thống khi ghi) | không ghi | `99` (để VNPay gọi lại; an toàn nhờ khóa chống trùng) |

- Không kiểm `vnp_Amount` với "số tiền đơn hàng" như mẫu của VNPay, vì hệ thống không lưu đơn (không đổi schema). Số tiền nằm trong phần được ký, nên khách không sửa được. Số còn lại có thể đã đổi giữa lúc tạo link và lúc IPN tới (khách vừa chuyển khoản QR); trường hợp này xử lý như SePay ở §6.

### 5.3 Chữ ký

- HMAC-SHA512 với khóa `VnPay:HashSecret`, kết quả dạng hex.
- Chuỗi ký: các tham số `vnp_*` có giá trị khác rỗng, trừ `vnp_SecureHash` và `vnp_SecureHashType`, sắp xếp theo tên tăng dần (so sánh thứ tự byte), nối dạng `key=value&key=value`, giá trị URL-encode.
- **Chưa chắc chắn:** tài liệu VNPay có mẫu code dùng URL-encode (PHP, Java, .NET) và mẫu không encode (NodeJS). Spec chọn cách của mẫu .NET (`WebUtility.UrlEncode`, dấu cách thành `+`). Phải thử một giao dịch trên sandbox; nếu VNPay báo sai chữ ký thì sửa cách encode và ghi lại vào spec.
- So chữ ký bằng phép so sánh thời gian hằng (`CryptographicOperations.FixedTimeEquals`), không phân biệt hoa thường.
- Cùng một bộ ký dùng cho tạo link, IPN và trang kết quả.

### 5.4 Thời gian

- `vnp_CreateDate`, `vnp_ExpireDate`, `vnp_PayDate` là giờ Việt Nam. Đổi sang UTC trước khi lưu.
- `NgayThanhToan` của dòng sổ = `vnp_PayDate` đổi sang UTC; thiếu hoặc sai định dạng thì dùng giờ server.

### 5.5 Mã phản hồi hiển thị cho khách

Trang kết quả dùng bảng mã `vnp_ResponseCode` của VNPay (ví dụ `24` khách hủy, `11` hết hạn chờ thanh toán, `51` không đủ số dư, `65` vượt hạn mức trong ngày, `75` ngân hàng bảo trì, `79` nhập sai mật khẩu quá số lần). Bảng đầy đủ lấy từ tài liệu VNPay khi code; mã không có trong bảng hiện "Giao dịch không thành công (mã {code})".

## 6. Ghi sổ hóa đơn

Dùng đúng quy tắc đợt 1 §5.1 với các khác biệt sau. Mọi bước chạy trong `InvoicePaymentTransaction.RunAsync` (khóa dòng `hoa_don` `FOR UPDATE`).

| Bước đợt 1 §5.1 | VNPay |
| --- | --- |
| 1. Đã xử lý? | So khóa `VNPAY-{vnp_TransactionNo}`, kể cả dòng đã xóa mềm → `Duplicate`. |
| 2. Hóa đơn nhận tiền được? | Giống SePay → `InvoiceNotPayable`. |
| 3. Đã trả đủ? | Giống SePay → `AlreadyPaid`. |
| 4. Ghi nhận | `DecideRecording(thucNhan = vnp_Amount / 100, null, conLai, isAdmin: true)`. Bình thường `thucNhan = conLai`. Nếu số còn lại đã giảm sau lúc tạo link (khách vừa chuyển khoản QR một phần) thì ghi `conLai` và gắn thẻ `[TIEN-THUA:]`, giống SePay E02. |
| 5. Dòng `LichSuThanhToan` | `MaGiaoDich = VNPAY-{vnp_TransactionNo}`; `PhuongThucThanhToan = ChuyenKhoan`; `NgayThanhToan` theo §5.4; `NguoiXacNhanId = null`; `GhiChu = [VNPAY] {vnp_BankCode} · {vnp_CardType} · TXN {vnp_TransactionNo} · REF {vnp_TxnRef}` (bỏ `[` `]` trong giá trị từ VNPay, cắt 500 ký tự). |
| 6. Lịch sử hóa đơn | lý do `[TU_DONG] VNPay ghi nhận {SoTien}đ, TXN {vnp_TransactionNo}`. |
| 7. Lượt cũ | Giống SePay: hủy lượt `ChoThanhToan`, giữ lượt `DangDoiChieu` kèm câu cảnh báo kiểm trùng. |
| 8. Vi phạm unique `MaGiaoDich` | `Duplicate`. |

Kết quả không ghi (`InvoiceNotPayable`, `AlreadyPaid`) lưu khóa trong bộ chống lặp cảnh báo 24 giờ như SePay (đợt 1 §5.3), để IPN gọi lại không bắn Telegram nhiều lần. Vì IPN trả `02` cho hai trường hợp này nên VNPay thường không gọi lại.

### 6.1 Chặn ghi trùng ở luồng thủ công

Đợt 1 §5.4 chặn nhân viên ghi trùng với dòng SePay. Mở rộng cho dòng VNPay:

- Mã giao dịch nhân viên nhập (thu thủ công, xác nhận minh chứng cũ) hoặc khách khai trên minh chứng, mà sổ đã có dòng chưa xóa `VNPAY-{mã}` → báo lỗi "Mã giao dịch này đã được hệ thống ghi nhận tự động qua VNPay.", không ghi.
- Kiểm "hóa đơn có dòng hệ thống tự ghi sau thời điểm khách nộp minh chứng" (bắt nhân viên chuyển Admin) tính cả dòng `VNPAY-`.
- Mã nhân viên nhập tay (thu thủ công, xác nhận minh chứng cũ) bắt đầu bằng `VNPAY-` hoặc `SEPAY-` (không phân biệt hoa thường) bị từ chối: "Mã bắt đầu bằng SEPAY- hoặc VNPAY- dành cho thanh toán hệ thống ghi tự động. Vui lòng nhập mã giao dịch gốc (không có tiền tố)." Dòng nhập tay không bao giờ trông như dòng hệ thống ghi (T4, người dùng chọn 26/10/2026).

## 7. Thông báo

### 7.1 Khi ghi sổ

Giống đợt 1 §6: thông báo trong app (Admin, nhân viên chi nhánh, khách), SignalR, Telegram, email biên lai, cùng hàng đợi và worker. Khác biệt về chữ:

- Thông báo trong app: "Đã nhận thanh toán VNPay {SoTien}đ — {TenPhong}"; nội dung nhân viên ghi "qua VNPay (TXN {vnp_TransactionNo})".
- Telegram: dòng "Kênh: Thẻ ATM / Visa / MasterCard (VNPay{, BankCode})", "Mã GD: {vnp_TransactionNo}".
- Email biên lai và biên lai web: phương thức "Thẻ ATM / Visa / MasterCard (VNPay)", mã tham chiếu `vnp_TransactionNo`.

### 7.2 Cảnh báo cần xử lý tay

`InvoiceNotPayable` và `AlreadyPaid` gửi Telegram:

```
🔔 THANH TOÁN VNPAY CẦN HOÀN TIỀN
Lý do: Hóa đơn {MaHoaDon} đã trả đủ / không nhận tiền được: {LyDo}[; hóa đơn thay thế: ...]
Số tiền: +{SoTien} VNĐ
Mã GD VNPay: {vnp_TransactionNo} · Mã đơn: {vnp_TxnRef}
Thời gian: {dd/MM/yyyy HH:mm:ss}
Gợi ý: Hoàn tiền cho khách trên trang merchant VNPay
```

Giao dịch không thành công (bước 4 của §5.2) và sai chữ ký không gửi Telegram, chỉ log.

## 8. Cổng quản lý

- Lịch sử thanh toán trong chi tiết hóa đơn: dòng `VNPAY-` hiện nhãn "VNPay" và mã tham chiếu `vnp_TransactionNo`, như nhãn SePay hiện có. Nút Biên lai, Gửi lại biên lai, Hủy ghi nhận dùng được như các dòng khác.
- Admin hủy ghi nhận dòng VNPay thì IPN gọi lại cũng không ghi lại (bước 1 tính cả dòng đã xóa mềm). Hủy ghi nhận không hoàn tiền thẻ; Admin hoàn tiền trên trang merchant VNPay.
- Màn Giao dịch SePay không đổi, không hiện giao dịch VNPay.

## 9. Kiến trúc

### 9.1 Application (`Features/OnlinePayments/`, phẳng như đợt 1)

- `Common/Configurations/VnPayOptions.cs` (§10).
- Bộ ký và đọc tham số VNPay, thuần, không DI (`System.Security.Cryptography` có sẵn trong BCL): dựng chuỗi ký, ký, kiểm chữ ký, tách `HoaDonId` từ `vnp_TxnRef`, đổi giờ.
- `IVnPayPaymentService`:
  - `CreatePaymentUrlAsync(hoaDonId, nguoiThueId, clientIp, returnUrl)` → `ServiceResult<string>` (§4.2);
  - `HandleIpnAsync(IReadOnlyDictionary<string, string> query)` → mã và thông điệp trả VNPay (§5.2);
  - `GetReturnResultAsync(query, nguoiThueId)` → dữ liệu trang kết quả (§4.3), chỉ đọc.
- **Dùng chung phần ghi sổ với SePay.** Phần chạy trong khóa của `InvoiceTransferHandler` (§5.1 bước 1–8 đợt 1) được tách để hai kênh cùng gọi; mỗi kênh cung cấp khóa chống trùng, số tiền, thời gian, mã tham chiếu và nhãn. Kết quả vẫn là `TransferOutcome` để dùng lại `IPaymentOutcomePublisher`, `PaymentNotificationDispatcher` và bộ chống lặp cảnh báo. Plan chốt cách tách và tên kiểu; yêu cầu bắt buộc: test SePay hiện có giữ nguyên hành vi.
- Nhãn và mã tham chiếu theo tiền tố: `PaymentNotificationFormatter.PaymentMethodLabel`, `InvoicePaymentPolicy.SePayBankReference` (đổi tên hoặc thêm hàm cho VNPay), cờ `LaSePay` trong DTO lịch sử thanh toán.

### 9.2 Infrastructure

- Bind `VnPayOptions` và gọi `Validate()` trong `AddInfrastructure`, như `SePayOptions`.
- Không gọi API VNPay (không có đối soát bù, không hoàn tiền), nên không cần HTTP client.

### 9.3 Web

- `Controllers/Api/VnPayIpnController` (`GET /api/webhooks/payment/vnpay-ipn`).
- `Areas/KhachThue/Controllers/ThanhToanHoaDonController`: thêm `POST .../ThanhToan/VnPay/{hoaDonId}` và `GET .../ThanhToan/VnPay/KetQua` cùng view kết quả.
- Panel JSON thêm `vnPayEnabled`; `wwwroot/js/khachthue-thanhtoan.js` thêm tab.
- Không sửa `Program.cs`.

## 10. Cấu hình

Thêm vào `appsettings.example.json` (giá trị mẫu, không secret thật):

```jsonc
"VnPay": {
  // Tắt thì không hiện tab VNPay, endpoint tạo link, IPN và trang kết quả trả 404.
  "Enabled": false,
  "TmnCode": "<VNPAY_TMN_CODE>",
  "HashSecret": "<VNPAY_HASH_SECRET>",
  // Sandbox. Chạy thật đổi theo URL VNPay cấp khi đăng ký merchant.
  "PaymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
  // Thời gian link thanh toán còn hiệu lực.
  "ExpireMinutes": 15
}
```

`Validate()`: `Enabled = true` thì bắt buộc `TmnCode`, `HashSecret`; `PaymentUrl` là URL tuyệt đối https, không chứa `?` hoặc `#` (link được dựng bằng cách nối `?` + tham số đã ký); `ExpireMinutes` trong 5–60.

Không ghi đè `appsettings.json` của người dùng. URL IPN khai báo phía VNPay: `https://{domain-ngrok}/api/webhooks/payment/vnpay-ipn`.

## 11. Kiểm thử

### 11.1 Unit (Application.UnitTests)

- Bộ ký: sắp xếp tham số, bỏ tham số rỗng và `vnp_SecureHash*`, encode, chữ ký hex; kiểm chữ ký đúng, sai một ký tự, thiếu chữ ký, khác hoa thường.
- Tách `vnp_TxnRef`: dạng đúng, sai tiền tố, id = 0, quá 9 chữ số, thiếu `_`.
- Đổi giờ `yyyyMMddHHmmss` GMT+7 ↔ UTC.
- Tạo link: hóa đơn không thuộc khách, chưa công bố, đã hủy, đã trả đủ, tiền lẻ; số tiền = số còn lại × 100; `vnp_ExpireDate` đúng.
- IPN: đủ các dòng của bảng §5.2; đã trả một phần bằng QR rồi IPN tới (ghi `conLai`, gắn tiền thừa); IPN gọi lại (Duplicate → `02`); dòng đã hủy ghi nhận không ghi lại.
- Thông báo: nhãn VNPay trong Telegram, email biên lai, cảnh báo hoàn tiền.
- Chặn ghi trùng §6.1.
- Test SePay hiện có PASS không sửa kỳ vọng.

### 11.2 Integration

- Infrastructure: tìm dòng `VNPAY-` (kể cả đã xóa mềm), unique index `MaGiaoDich`, ghi song song cùng một `vnp_TransactionNo` chỉ ra một dòng.
- Web (`[Collection(WebTestCollection.Name)]`): IPN tắt → 404; chữ ký sai → `97`; IPN hợp lệ → ghi sổ, SignalR; gọi lại → `02`; tạo link cần đăng nhập và antiforgery, khách khác → 404; trang kết quả sai chữ ký không lộ thông tin hóa đơn.

### 11.3 Thử tay trên sandbox (người dùng làm)

| Mã | Kịch bản | Kỳ vọng |
| --- | --- | --- |
| V01 | Trả đủ bằng thẻ test | ghi sổ, panel sang biên lai, email biên lai, Telegram, thông báo nhân viên |
| V02 | Hủy trên trang VNPay | trang kết quả "Bạn đã hủy thanh toán", không ghi sổ |
| V03 | Trả đủ bằng QR SePay rồi hoàn tất VNPay đã mở trước đó | không ghi sổ, Telegram "cần hoàn tiền" |
| V04 | IPN tới muộn hơn ReturnUrl | trang kết quả "đang ghi nhận", tự đổi khi ghi xong |
| V05 | Sửa một tham số trên URL kết quả | "Không xác thực được kết quả từ VNPay" |
| V06 | `VnPay:Enabled = false` | không có tab, endpoint 404 |
| V07 | Lần đầu thử | xác nhận cách encode chữ ký ở §5.3 đúng với sandbox |

## 12. Việc người dùng tự làm

- Đăng ký sandbox tại `http://sandbox.vnpayment.vn/devreg/`, lấy `TmnCode` và `HashSecret`, điền vào `appsettings.json` máy phát triển.
- Khai URL IPN `https://{domain-ngrok}/api/webhooks/payment/vnpay-ipn` theo hướng dẫn của VNPay cho tài khoản sandbox.
- Mở tunnel ngrok như đợt 1 (`ngrok http 5009 --url=https://{domain-tĩnh}`); VNPay yêu cầu IPN dùng HTTPS.
- Lấy thông tin thẻ test VNPay cấp khi đăng ký.
- Agent viết thêm mục VNPay vào `docs/features/thanh-toan-truc-tuyen/huong-dan-thiet-lap.md` (từng bước, không script).

## 13. Giới hạn chấp nhận

- Không cộng phí thẻ; chạy thật thì chủ trọ chịu phí VNPay.
- Không đối soát bù: IPN bị lỡ sau 10 lần gọi lại (ví dụ tunnel tắt) thì giao dịch không được ghi. Admin đối chiếu trên trang merchant VNPay rồi dùng "Thu thủ công" với mã giao dịch VNPay.
- Không lưu đơn VNPay đã tạo: không có danh sách "link đã tạo", không kiểm số tiền với đơn (§5.2).
- Khách mở hai link VNPay và trả cả hai, hoặc trả QR rồi trả VNPay: khoản sau ghi phần còn nợ (nếu có) và đánh dấu tiền thừa, hoặc không ghi và cảnh báo hoàn tiền. Hoàn tiền làm tay trên trang merchant.
- `vnp_IpAddr` lấy IP kết nối; qua ngrok sẽ là IP của tunnel vì không cấu hình forwarded headers (không sửa `Program.cs`). Đã thử trên sandbox 26/10/2026 (V01, tạo link qua domain ngrok nên `vnp_IpAddr` là IP tunnel và ReturnUrl là `http://{domain}`): VNPay chấp nhận. Chạy thật cần xem lại.
- Hàng đợi thông báo trong bộ nhớ như đợt 1; khởi động lại có thể mất tin, bù bằng "Gửi lại biên lai".

## 14. Chia việc

Hai lượt `/ship` (Q13), chi tiết ở [plan.md](plan.md):

1. **Phần 1:** cấu hình, bộ ký, tách phần ghi sổ dùng chung, IPN, thông báo, chặn ghi trùng (§6.1).
2. **Phần 2:** tab trong panel, tạo link, trang kết quả, nhãn ở cổng quản lý, hướng dẫn thiết lập.

## 15. Điều kiện nghiệm thu

- [x] §11.1, §11.2 PASS; `dotnet build` không lỗi; `has-pending-model-changes` không có thay đổi (26/10/2026, chi tiết ở [review.md](review.md) mục 4).
- [x] V01–V07 chạy đúng trên sandbox VNPay (người dùng thử tay 26/10/2026; [review.md](review.md) mục 7).
- [x] Hướng dẫn thiết lập VNPay sandbox trong `huong-dan-thiet-lap.md` (mục 9, 26/10/2026).
- [x] `README.md` module, `docs/features/README.md` và mục Hiện trạng trong `AGENTS.md` được cập nhật (26/10/2026).

## 16. Lịch sử thay đổi

| Ngày | Thay đổi |
| --- | --- |
| 25/10/2026 | Bản nháp đầu, theo 10 quyết định của người dùng (§2). |
| 25/10/2026 | Người dùng duyệt: chặn ghi trùng dòng `VNPAY-` (Q11), link hết hạn 15 phút (Q12), chia 2 lượt `/ship` (Q13). |
| 26/10/2026 | Người dùng duyệt các ca spec chưa nói (B1–B5', ghi ở plan §3 Kết quả): `vnp_TransactionNo` rỗng → `99`; `vnp_Amount` không hợp lệ → `04`; câu chữ riêng cho kênh VNPay. Phần 1 xong. |
| 26/10/2026 | Người dùng duyệt các ca spec chưa nói ở phần 2 (X1–X5, ghi ở plan §4 Kết quả): X1 trang "Đã ghi nhận" hiện số đã ghi sổ, kèm dòng "VNPay báo bạn đã trả …" nếu khác `vnp_Amount / 100` (§4.3); X2 thêm trạng thái "không ghi nhận" khi `00` nhưng hóa đơn không còn nhận khoản này hoặc dòng đã bị hủy ghi nhận (§4.3); X3 mã `07` dùng mô tả riêng, IPN không đổi; X4 từ chối tạo link khi số còn lại không nguyên (§4.2); X5 không tab VNPay khi chủ trọ chưa cấu hình tài khoản nhận tiền (§4.1). Phần 2 code xong, chưa thử sandbox. |
| 26/10/2026 | Sau review phần 2: trang kết quả đọc lại sổ trước khi kết luận "không ghi nhận"; fixture Web test cố định `VnPayOptions` tắt. Thử sandbox V01–V07 đạt ([review.md](review.md) mục 7). Gộp hồ sơ vào `thanh-toan-truc-tuyen/vnpay/`. Phần 2 commit. |
| 26/10/2026 | Người dùng chọn cách xử lý các việc còn mở: T2 IPN thành công cho hóa đơn không có trong DB trả `01`, không cảnh báo hoàn tiền (§5.2 bước 4b); T4 mã nhập tay mang tiền tố `VNPAY-`/`SEPAY-` bị từ chối (§6.1); `PaymentUrl` không chứa `?`/`#` (§10); §13 ghi kết quả thử sandbox V01; lệnh ngrok thống nhất `ngrok http 5009 --url=https://{domain-tĩnh}` (§12). Hạn mức số tiền: tài liệu VNPay không nêu tối thiểu/tối đa, người dùng xác nhận không xảy ra nên không kiểm. |
