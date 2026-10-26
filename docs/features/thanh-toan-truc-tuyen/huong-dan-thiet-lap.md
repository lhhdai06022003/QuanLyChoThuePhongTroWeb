# Hướng dẫn thiết lập và thử tay — thanh toán trực tuyến (VietQR + SePay, VNPay)

Tài liệu này dành cho người chạy thử trên máy phát triển. Mọi giá trị bí mật (token, secret, mật khẩu) chỉ điền vào `src/QuanLyChoThuePhongTroWeb.Web/appsettings.json` của máy mình. File đó không được commit; không chép secret vào tài liệu, log hay tin nhắn. Không có script phụ trợ: các bước dưới đây làm tay, lệnh nào cần chạy thì ghi sẵn.

Spec: [spec SePay](sepay/spec.md) (§10 cấu hình, §11.3 kịch bản thử tay, §11.4 sandbox). VNPay (đợt 2): mục 9 và [spec VNPay](vnpay/spec.md).

## 0. Chuẩn bị

- `appsettings.json` đã có (copy từ `appsettings.example.json` **chỉ khi chưa có**, không ghi đè).
- Có ít nhất một hợp đồng còn hiệu lực với hóa đơn trạng thái **Đã gửi** và còn nợ. Ghi lại `HoaDonId` (xem trong URL chi tiết hóa đơn) và số còn lại.
- Người đứng tên hợp đồng có **Email** là hộp thư bạn đọc được, và có tài khoản đăng nhập cổng khách thuê.

## 1. SePay Test mode

SePay Test mode không chuyển tiền thật. Vẫn cần đăng ký tài khoản SePay (tự làm trên my.sepay.vn), rồi bật **Test mode**.

1. **Bank Accounts → Add New:** tạo tài khoản ngân hàng giả (ví dụ MBBank, cá nhân). Ghi lại số tài khoản.
2. **Webhooks → Add Webhook:**
   - URL: `https://{domain-tĩnh}/api/webhooks/payment/sepay` (domain ngrok ở mục 4).
   - Sự kiện: tiền vào.
   - Xác thực: HMAC-SHA256. Chép secret SePay sinh ra.
   - Tiền tố mã thanh toán (nếu SePay hỏi): `TTPTHD` hoặc để trống.
3. **API token** (chỉ cần cho màn Giao dịch SePay / đồng bộ bù): tạo token trong mục API của SePay Test mode.
4. Theo dõi webhook ở **Webhooks → Delivery Logs**. Giới hạn Test mode khoảng 50 webhook mỗi công ty và có hạn mức mô phỏng mỗi ngày: dùng tiết kiệm.

Điền vào `appsettings.json`:

| Khóa | Giá trị |
| --- | --- |
| `VietQRSettings:BankId` | mã VietQR của ngân hàng giả, ví dụ `MB` (dùng nguyên trong tham số `ba` của deeplink) |
| `VietQRSettings:AccountNumber` | số tài khoản giả ở bước 1 (sai thì webhook bị loại là `WrongAccount`) |
| `VietQRSettings:AccountName` | tên chủ tài khoản, viết hoa không dấu |
| `VietQRSettings:BankName` | tùy chọn, tên hiển thị trên panel, ví dụ `MBBank` |
| `SePay:Enabled` | `true` |
| `SePay:WebhookAuthMode` | `Hmac` |
| `SePay:WebhookSecret` | secret ở bước 2 |
| `SePay:ApiBaseUrl` | Test mode: host `userapi-sandbox.sepay.vn` — kiểm trên tài liệu SePay có cần `/v2` không, ví dụ `https://userapi-sandbox.sepay.vn/v2` |
| `SePay:ApiToken` | token ở bước 3 (trống thì nút Đồng bộ bị khóa) |
| `SePay:SyncRecordEnabled` | để `false` cho tới khi đạt mục 6.6 (b) |

## 2. Bot Telegram

1. Chat với **@BotFather**, gửi `/newbot`, đặt tên và username (kết thúc bằng `bot`). Lấy **token**.
2. Tạo nhóm chủ trọ, thêm bot vào nhóm, gửi `/start@ten_bot` trong nhóm.
3. Mở trình duyệt: `https://api.telegram.org/bot<TOKEN>/getUpdates` (thay `<TOKEN>`, bỏ dấu `< >`). Tìm `"chat":{"id":-100...` — đó là **chat id**. Kết quả rỗng thì gửi lại tin trong nhóm rồi tải lại. Nhóm được nâng lên supergroup thì chat id đổi, lấy lại.
4. Nhóm riêng cho chi nhánh (tùy chọn): lặp lại bước 2–3 với nhóm khác. Khóa là `ChiNhanhId` (xem trong URL khi mở chi nhánh ở cổng quản lý).

| Khóa | Giá trị |
| --- | --- |
| `Telegram:Enabled` | `true` |
| `Telegram:BotToken` | token bước 1 |
| `Telegram:DefaultChatId` | chat id nhóm chung |
| `Telegram:BranchChatIds` | `{ "1": "-100..." }` hoặc `{ }` |

## 3. Email (Gmail)

1. Tài khoản Google → **Bảo mật** → bật **Xác minh 2 bước**.
2. Mở `https://myaccount.google.com/apppasswords`, tạo mật khẩu ứng dụng. Dán 16 ký tự, bỏ khoảng trắng.

| Khóa | Giá trị |
| --- | --- |
| `EmailSettings:SmtpServer` | `smtp.gmail.com` |
| `EmailSettings:Port` | `587` |
| `EmailSettings:SenderName` | tên hiển thị, ví dụ `TroSync` |
| `EmailSettings:SenderEmail` | Gmail gửi đi |
| `EmailSettings:Password` | App Password |

Email lần đầu có thể vào Spam.

## 4. ngrok (domain tĩnh)

Cần ngrok bản 3.20 trở lên (bản cũ báo `ERR_NGROK_121` hoặc `unknown flag: --url`).

```powershell
winget install ngrok.ngrok
ngrok update
ngrok config add-authtoken <authtoken-tren-dashboard-ngrok>
```

Lấy domain tĩnh miễn phí trên dashboard ngrok (Domains). Mỗi lần thử:

```powershell
# Terminal 1: chạy web (cổng 5009, không có tác vụ nền định kỳ; worker thông báo vẫn chạy)
dotnet run --project src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj --launch-profile http -- --BackgroundJobs:Enabled=false

# Terminal 2: mở tunnel
ngrok http 5009 --url=https://{domain-tĩnh}
```

Điền `OnlinePayment:PublicBaseUrl` = `https://{domain-tĩnh}` (không có `/` cuối). Sửa `appsettings.json` xong phải chạy lại web.

Tunnel mở cả trang web ra Internet: chỉ bật khi thử, tắt ngay sau đó, dùng mật khẩu Admin mạnh.

## 5. Kiểm nhanh sau khi cấu hình

- Web khởi động không lỗi (`Validate()` sẽ báo nếu thiếu secret khi `Enabled = true`).
- SePay → Webhooks → **Test send**: Delivery Logs trả 200 `{"success":true}`. Trả 401 là sai secret hoặc lệch giờ; 404 là `SePay:Enabled = false`.
- Không thấy tin Telegram: log có `401` là sai token, `400 chat not found` là sai chat id hoặc bot chưa ở trong nhóm.
- Không có email: log lỗi xác thực SMTP là sai App Password; kiểm `NguoiThue.Email`.

## 6. Kịch bản thử tay (spec §11.3)

Nội dung chuyển khoản luôn là `TTPTHD{HoaDonId}`, ví dụ `TTPTHD105`. Tạo giao dịch giả bằng **Transactions → Simulate** (loại In, tài khoản giả, số tiền, nội dung).

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| 6.1 TC01 | Công bố hóa đơn (hoặc Gửi lại email hóa đơn) → mở email → bấm **THANH TOÁN TRỰC TUYẾN** → đăng nhập khách | Về đúng hóa đơn; email có dòng nội dung `TTPTHD{Id}`, QR theo số còn lại |
| 6.2 TC02 | Mở panel thanh toán của hóa đơn trên PC → Simulate đúng số còn lại | Panel đổi sang biên lai trong vài giây; Telegram báo đã thu; email biên lai tới; chuông ở cổng quản lý và khách; hóa đơn "Đã thanh toán" |
| 6.3 E01 | Hóa đơn khác, Simulate ít hơn số còn lại | "Đã nhận X (đợt 1)" + QR mới cho số còn lại; hóa đơn "Thanh toán một phần" |
| 6.3 E02 | Simulate nhiều hơn số còn lại | Hóa đơn "Đã thanh toán"; biên lai và Telegram ghi tiền thừa |
| 6.4 E03 | Simulate nội dung sai, ví dụ `chuyen tien tro` | Telegram "CHUYỂN KHOẢN CẦN XỬ LÝ TAY"; hóa đơn không đổi |
| 6.5 TC05 | Replay một giao dịch ở 6.2 ba lần (Delivery Logs) | Vẫn 1 dòng trong lịch sử thanh toán, 1 tin Telegram, 1 email |
| 6.6 | Đồng bộ bù, xem mục 7 | |
| 6.7 TC04 | Mở panel trên điện thoại thật | Từng nút trong 12 ngân hàng mở đúng app với số tiền và nội dung điền sẵn (app mở với tài khoản giả, dừng ở màn xác nhận, không chuyển); tải ảnh QR; ba nút sao chép. Ghi lại app nào không mở được |
| 6.8 | Cổng quản lý → chi tiết hóa đơn → lịch sử thanh toán | Dòng SePay có nhãn và `REF {mã}`; nút **Biên lai** mở trang in A5; **Gửi lại biên lai** gửi email |
| 6.9 | In biên lai (Ctrl+P), cả khi đang giao diện tối | Một trang A5 dọc, chữ đen nền trắng |

## 7. Đồng bộ bù (màn Giao dịch SePay, chỉ Admin)

Menu cổng quản lý → **Giao dịch SePay** (`/QuanLyNhaTro/GiaoDichSePay`). Chọn khoảng ngày (mặc định 3 ngày gần nhất, tối đa 31 ngày) → **Đồng bộ**.

Làm theo thứ tự, bước sau chỉ làm khi bước trước đạt. Kết quả thử tay mới áp dụng cho SePay Test mode; kiểm lại một lần với giao dịch thật trước khi bật `SePay:SyncRecordEnabled` ở môi trường thật (Test mode không đảm bảo ngân hàng thật gửi cùng định dạng mã tham chiếu). Sandbox có thể trả danh sách rỗng hoặc báo lỗi; đó là kết quả hợp lệ (Test mode không chứng minh được gì về mã tham chiếu), cờ vẫn để tắt.

1. **(a) Kiểm khớp mã, cờ tắt.** Giữ `SePay:SyncRecordEnabled = false`. Đồng bộ khoảng ngày đã có giao dịch ở mục 6 (đã vào qua webhook):
   - Phải hiện **Đã có**, giờ giao dịch khớp giờ trên SePay.
   - Hiện **Chưa ghi sổ** nghĩa là mã tham chiếu bên webhook (`referenceCode`) khác bên API (`reference_number`), hoặc giờ API lệch: **giữ cờ tắt**, báo lại để sửa spec.
2. **(b) Kiểm không có dòng thiếu mã.** Trong Delivery Logs, `referenceCode` của các webhook phải khác rỗng. Trong lịch sử thanh toán không có dòng mã giao dịch bắt đầu `SEPAY-ID-`. Có dòng như vậy thì bật cờ sẽ ghi đôi giao dịch đó (spec §5.2, rủi ro ghi đôi còn lại): **giữ cờ tắt**.
3. **(c) Bật ghi sổ.** Đạt cả (a) và (b) mới đặt `SePay:SyncRecordEnabled = true`, chạy lại web. Tắt web (hoặc tạm tắt webhook trên SePay), Simulate một giao dịch đúng mã, đợi SePay hết thử lại (khoảng 33 phút) hoặc giữ webhook tắt, bật web, bấm Đồng bộ → **Mới ghi nhận**; bấm lần nữa → **Đã có**.

Khi cờ tắt, dòng **Chưa ghi sổ** hoặc **Thiếu mã tham chiếu** không được ghi tự động. Trước khi dùng "Thu thủ công" cho các dòng đó, kiểm lịch sử thanh toán của hóa đơn để không thu đôi (Thu thủ công chỉ chặn trùng với mã `SEPAY-{mã}`).

## 8. Sau khi thử

- Tắt ngrok.
- Muốn tắt hẳn: `SePay:Enabled = false` (webhook trả 404), `Telegram:Enabled = false`.
- Không commit `appsettings.json`.

## 9. VNPay sandbox (đợt 2)

Khách thuê trả hóa đơn bằng thẻ ATM nội địa, Visa, MasterCard qua trang VNPay. Tiền về thì VNPay gọi IPN, hệ thống ghi sổ và gửi thông báo giống SePay. Spec: [vnpay/spec.md](vnpay/spec.md). Chuẩn bị ở mục 0, Telegram ở mục 2, email ở mục 3, ngrok ở mục 4 áp dụng nguyên cho VNPay. Chưa có giao dịch nào thử trên sandbox VNPay; các bước dưới là việc người dùng tự làm.

### 9.1 Đăng ký sandbox

Mở `http://sandbox.vnpayment.vn/devreg/`, đăng ký merchant thử nghiệm. VNPay gửi email gồm:

- **TmnCode** (mã website) và **HashSecret** (chuỗi bí mật tạo chữ ký);
- đường vào trang quản trị merchant sandbox (để khai URL IPN và xem giao dịch);
- thông tin thẻ test (ngân hàng, số thẻ, tên chủ thẻ, ngày phát hành, mật khẩu OTP).

Giữ email đó ở nơi riêng tư. Không chép TmnCode, HashSecret hay số thẻ vào tài liệu, log hoặc commit.

### 9.2 Khai URL IPN

Trên trang quản trị merchant sandbox, khai URL IPN theo hướng dẫn VNPay cho tài khoản sandbox:

`https://{domain-tĩnh}/api/webhooks/payment/vnpay-ipn`

VNPay chỉ gọi IPN qua HTTPS nên cần tunnel ở mục 4 (domain ngrok tĩnh, `ngrok http 5009 --url=https://{domain-tĩnh}`). VNPay gọi IPN theo URL khai ở phía merchant, không theo tham số trong link thanh toán.

### 9.3 Cấu hình `appsettings.json`

| Khóa | Giá trị |
| --- | --- |
| `VnPay:Enabled` | `true` |
| `VnPay:TmnCode` | TmnCode trong email VNPay |
| `VnPay:HashSecret` | HashSecret trong email VNPay |
| `VnPay:PaymentUrl` | giữ mặc định sandbox `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` |
| `VnPay:ExpireMinutes` | `15` (hợp lệ từ 5 đến 60; là thời gian link còn hiệu lực) |

`VnPay:Enabled = true` mà thiếu TmnCode hoặc HashSecret thì web không khởi động. Sửa xong phải chạy lại web.

### 9.4 Mở trang bằng địa chỉ nào

Mở web trên trình duyệt bằng `http://localhost:5009` ở máy đang chạy web. Trang kết quả (ReturnUrl) được dựng theo địa chỉ đang mở và cookie đăng nhập gắn với địa chỉ đó, nên khi VNPay đưa khách về, trình duyệt vẫn giữ phiên đăng nhập. Web không đọc forwarded headers (không sửa `Program.cs`), nên nếu mở web qua domain ngrok thì ReturnUrl dựng ra là `http://{domain}` (không phải https). `vnp_IpAddr` là IP kết nối của khách; qua ngrok sẽ là IP của tunnel (spec VNPay §13). Đã thử trên sandbox 26/10/2026 (V01, mở web và tạo link qua domain ngrok): VNPay chấp nhận cả hai điểm này. Vẫn nên mở bằng `localhost:5009` như trên; chạy thật cần xem lại.

### 9.5 Kiểm nhanh

Mở `https://{domain-tĩnh}/api/webhooks/payment/vnpay-ipn` trên trình duyệt (không tham số):

- `VnPay:Enabled = true`: trả `{"RspCode":"97","Message":"Invalid signature"}`;
- `VnPay:Enabled = false`: trả 404.

Chưa thấy như vậy thì chưa thử tiếp: kiểm tunnel còn mở, web đã chạy lại sau khi sửa `appsettings.json`.

### 9.6 Kịch bản thử tay (spec VNPay §11.3)

Đăng nhập cổng khách thuê, mở hóa đơn **Đã gửi** còn nợ, mở panel **Thanh toán**: có hai tab, "Chuyển khoản QR" (mặc định) và "Thẻ ATM / Visa / MasterCard (VNPay)". Bấm **Thanh toán qua VNPay**, trên trang VNPay chọn ngân hàng NCB và nhập thẻ test ở email.

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| V01 | Trả đủ bằng thẻ test | Trang kết quả "Đã ghi nhận thanh toán" kèm số tiền và nút **Xem biên lai**; hóa đơn "Đã thanh toán"; email biên lai; Telegram; thông báo cho nhân viên |
| V02 | Bấm **Hủy** trên trang VNPay | Trang kết quả "Bạn đã hủy thanh toán."; không có dòng mới trong lịch sử thanh toán |
| V03 | Mở link VNPay (chưa nhập thẻ) → Simulate SePay đủ số còn lại → quay lại hoàn tất VNPay trong 15 phút | Trang kết quả "VNPay báo đã thanh toán nhưng hóa đơn không còn nhận khoản này…"; không ghi sổ; Telegram "THANH TOÁN VNPAY CẦN HOÀN TIỀN" |
| V04 | Tắt ngrok → hoàn tất VNPay → trang "đang ghi nhận" → bật lại ngrok, chờ VNPay gọi lại IPN (tối đa 5 phút) | Trang tự đổi sang "Đã ghi nhận thanh toán", hoặc bấm **Kiểm tra lại** |
| V05 | Ở trang kết quả, sửa một tham số trên thanh địa chỉ (ví dụ `vnp_Amount`) rồi tải lại | "Không xác thực được kết quả từ VNPay." |
| V06 | Đặt `VnPay:Enabled = false`, chạy lại web | Panel không có tab; URL IPN và trang kết quả trả 404 |
| V07 | Lần đầu thử | Nếu VNPay báo **sai chữ ký** (cách encode ở spec VNPay §5.3 chưa xác nhận trên sandbox), dừng và báo lại, không tự sửa code |

Kèm kiểm cổng quản lý (tương ứng 6.8): chi tiết hóa đơn → lịch sử thanh toán, dòng VNPay có nhãn **VNPay** và `TXN {mã giao dịch}`; nút **Biên lai** và **Gửi lại biên lai** dùng được như các dòng khác.

### 9.7 Cảnh báo khi thu thủ công trong lúc VNPay còn gọi lại IPN

Khi IPN chưa được trả `00` hoặc `02` (ví dụ tunnel tắt, web lỗi), VNPay gọi lại tối đa 10 lần, cách nhau 5 phút (spec VNPay §5.2). Nếu trong thời gian đó nhân viên dùng **Thu thủ công** với mã giao dịch VNPay thô (`vnp_TransactionNo`, không có tiền tố `VNPAY-`), lần gọi lại sau thấy hóa đơn đã đủ tiền nên không ghi sổ và gửi Telegram "THANH TOÁN VNPAY CẦN HOÀN TIỀN" **sai**: khách không bị trừ thêm, hệ thống chỉ không nhận ra đó là cùng một giao dịch.

- Mã có tiền tố `VNPAY-` hoặc `SEPAY-` bị từ chối khi thu thủ công; chỉ nhập mã gốc.
- Chỉ thu thủ công khi chắc VNPay đã ngừng gọi lại (kiểm trên trang merchant sandbox, hoặc đợi hơn 50 phút kể từ lúc khách trả).
- Đã nhận cảnh báo này sau khi thu thủ công: đối chiếu lịch sử thanh toán của hóa đơn trước, nếu mã giao dịch đã có ở đó thì **không hoàn tiền**.

### 9.8 Sau khi thử

- Đặt `VnPay:Enabled = false` và chạy lại web, tắt ngrok.
- Không commit `appsettings.json`.
