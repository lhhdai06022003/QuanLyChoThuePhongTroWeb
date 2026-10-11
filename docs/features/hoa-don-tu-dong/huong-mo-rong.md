# Hướng mở rộng của hóa đơn tự động

- **Trạng thái:** làm rõ yêu cầu (10/10/2026). Chưa phải spec, chưa lập plan. Mỗi mục khi bắt đầu làm sẽ có spec riêng.
- Liên quan: [spec.md](spec.md) (hóa đơn tự động; các mục dưới đây nằm ngoài phạm vi của spec đó).
- Giá và chính sách của bên thứ ba trong file này lấy từ bài tổng hợp, không phải bảng giá chính thức; phải kiểm lại trước khi dùng.

## A. Đối soát chuyển khoản tự động (ưu tiên kế tiếp)

### A.1 Code hiện có dùng lại được

| Thành phần | Hiện trạng |
|---|---|
| `YeuCauThanhToanHoaDon.MaYeuCau` | Sinh bởi `PaymentCodeGenerator.NewRequestCode`: `TT{HoaDonId}{6 ký tự}`, bỏ O/I/0/1 để khó gõ nhầm; nằm trong `NoiDungChuyenKhoan` |
| VietQR | `InvoicePaymentRequestService` dựng QR với số tiền và nội dung chứa mã yêu cầu |
| `LichSuThanhToan` | Có `MaGiaoDich`, `SoTienThanhToan`, `PhuongThucThanhToan`, `NguoiXacNhanId` (cho phép null), `MinhChungThanhToanHoaDonId` |
| Hàng đợi đối chiếu | `IInvoicePaymentConfirmationService.GetReviewQueueAsync`, `ConfirmAsync`, `RejectAsync` |
| Thanh toán một phần | `ChoPhepThanhToanMotPhan`, `SoTienThanhToanToiThieu` trên `HoaDon` |

### A.2 Luồng mục tiêu

1. Hóa đơn `DaGui`, khách mở thanh toán, hệ thống tạo yêu cầu thanh toán có mã và QR (đã có).
2. Khách quét QR chuyển khoản.
3. Dịch vụ nghe biến động số dư (SePay hoặc Casso) gửi `POST` webhook về server.
4. Server xác thực, lưu giao dịch thô, rồi khớp:
   - tìm `MaYeuCau` trong nội dung (không phân biệt hoa thường, bỏ khoảng trắng và ký tự lạ, vì ngân hàng có thể chèn hoặc cắt nội dung);
   - so số tiền với yêu cầu và số còn phải thu.
5. Khớp đủ điều kiện: ghi `LichSuThanhToan` (chuyển khoản, `MaGiaoDich` = mã giao dịch của nhà cung cấp), cập nhật trạng thái thanh toán, đóng yêu cầu, thông báo khách và nhân viên. Không cần nhân viên.
6. Không khớp: đưa vào hàng đợi đối chiếu hiện có, kèm lý do.

### A.3 Ngoại lệ phải đi hàng đợi, không tự ghi tiền

| Trường hợp | Xử lý đề xuất |
|---|---|
| Không có mã, mã sai, mã của yêu cầu đã hủy/hết hạn | Hàng đợi; nhân viên gán tay |
| Số tiền thiếu | Tự ghi chỉ khi hóa đơn cho phép trả một phần và đạt mức tối thiểu; còn lại vào hàng đợi |
| Số tiền thừa, hoặc hóa đơn đã thanh toán đủ | Hàng đợi; ghi nhận tiền thừa cần hoàn (hoàn tiền ngoài phạm vi) |
| Webhook gửi lặp (nhà cung cấp tự thử lại khi lỗi) | Bỏ qua nhờ khóa duy nhất trên mã giao dịch nhà cung cấp |
| Khách chuyển sai nội dung | Vẫn dùng luồng nộp minh chứng ảnh hiện có |

### A.4 Bảo mật

- Endpoint không đăng nhập nhưng bắt buộc xác thực (API key hoặc chữ ký HMAC tùy nhà cung cấp); sai thì trả 401 và không ghi gì.
- Không tin số tiền trong payload nếu nhà cung cấp có API tra lại giao dịch; chống gửi lại (replay) bằng mã giao dịch và thời điểm.
- Secret chỉ ở cấu hình cục bộ, không vào log, tài liệu hay commit.
- Phát triển: webhook không gọi được `localhost`. Cần tunnel HTTPS (ngrok, cloudflared) hoặc chức năng giả lập giao dịch của nhà cung cấp; test tự động dùng payload giả.

### A.5 Schema sơ bộ (cần spec riêng)

Cần một bảng giao dịch ngân hàng thô (nhà cung cấp, mã giao dịch nhà cung cấp **unique**, số tiền, nội dung, tài khoản nhận, thời điểm, trạng thái khớp, `YeuCauThanhToanHoaDonId`/`HoaDonId` null, payload gốc). Không nhét vào `LichSuThanhToan` được, vì giao dịch chưa khớp chưa có hóa đơn mà vẫn phải lưu để đối chiếu và chống trùng.

### A.6 Chi phí và cách thử an toàn (tra 10/10/2026, phải kiểm lại trang chính thức)

| | SePay | Casso | payOS (của Casso) |
|---|---|---|---|
| Mô hình | Nghe biến động số dư tài khoản ngân hàng | Nghe biến động số dư | Cổng thanh toán: tạo link/QR riêng cho từng đơn, tài khoản ảo |
| Gói miễn phí | FREE 0đ/tháng, 50 giao dịch/tháng | Free 30 giao dịch/tháng | Miễn phí khởi tạo, duy trì, giao dịch cho cá nhân/hộ kinh doanh (qua một số ngân hàng liên kết, ví dụ gói KLB-1000 từ 23/01/2026) |
| Cách tính phí | Đếm **giao dịch tiền vào** trong tháng; vượt hạn mức tính đơn giá × số giao dịch vượt; không giới hạn tính năng | Đếm **tiền vào + tiền ra** trên mọi tài khoản liên kết (tài khoản ảo chỉ tính tiền vào); gói Starter/Pro/Team trả theo tháng hoặc năm (khoảng 99.000–129.000đ/tháng cho Starter theo trang bảng giá) | Không tìm thấy bảng phí trả tiền cho 2026 |
| Thử không mất tiền | **Test Mode** tách biệt Live, giả lập giao dịch, không chạm tiền thật; cổng thanh toán có sandbox riêng | Giả lập giao dịch để kiểm webhook trước khi chạy thật | Chưa xác minh có sandbox |
| Phù hợp code hiện có | Cao: QR tĩnh + mã `TT…` trong nội dung | Cao, như SePay | Phải đổi cách tạo QR (mỗi yêu cầu một link), nhưng khớp chính xác theo mã đơn, không cần đọc nội dung |

Ước lượng: 100 phòng × 1 giao dịch/tháng ≈ 100 giao dịch, vượt gói miễn phí của SePay và Casso; payOS là lựa chọn không tốn phí nếu đủ điều kiện gói miễn phí.

**Cách thử an toàn đề xuất:**
1. Viết bộ khớp và endpoint với payload giả trong test tự động (không cần tài khoản nào).
2. Thử webhook thật bằng **SePay Test Mode** (giao dịch giả lập, không tiền thật) qua tunnel HTTPS tới máy dev.
3. Chỉ khi chạy thật mới chọn nhà cung cấp cho production; giấu nhà cung cấp sau một interface nên đổi SePay ↔ Casso ↔ payOS không ảnh hưởng nghiệp vụ.
4. Không liên kết tài khoản ngân hàng cá nhân chính khi thử; secret webhook chỉ để trong cấu hình cục bộ.

**Đã chốt (10/10/2026):** hệ thống dùng cho báo cáo khóa luận, chưa cho thuê thật, nên gói miễn phí của SePay hoặc Casso đủ dùng.

### A.7 Payload webhook SePay (theo Developer Portal, tra 10/10/2026)

SePay gửi `POST` JSON mỗi khi có giao dịch:

```json
{
  "id": 92704,
  "gateway": "Vietcombank",
  "transactionDate": "2024-07-02 11:08:33",
  "accountNumber": "1017588888",
  "subAccount": "",
  "code": null,
  "content": "NGUYEN VAN A chuyen tien",
  "transferType": "in",
  "transferAmount": 10000,
  "accumulated": 105010000,
  "referenceCode": "FT24012345678"
}
```

| Trường | Dùng để |
|---|---|
| `id` | Mã giao dịch của SePay: khóa **unique** chống ghi trùng |
| `transferType` | Chỉ xử lý `"in"` (tiền vào) |
| `transferAmount` | So với số tiền yêu cầu và số còn phải thu |
| `content` | Tìm mã `TT…` của `YeuCauThanhToanHoaDon` |
| `code` | Mã thanh toán SePay tự nhận diện nếu cấu hình tiền tố (ví dụ `TT`); có thì dùng trước `content` |
| `accountNumber` | Kiểm đúng tài khoản nhận của hệ thống |
| `transactionDate` | Thời điểm giao dịch (giờ Việt Nam, đổi sang UTC khi lưu) |
| `referenceCode` | Mã tham chiếu ngân hàng, lưu để đối chiếu |

**Xác thực**, chọn một:
- API Key: header `Authorization: Apikey <khóa>`, so sánh thời gian hằng (constant-time).
- HMAC-SHA256: header `X-SePay-Signature: sha256=<hex>` và `X-SePay-Timestamp` (giây Unix). Chuỗi ký là `{timestamp}.{body gốc}`; từ chối nếu lệch giờ quá 300 giây. An toàn hơn API Key vì chống gửi lại.

**Phản hồi và gửi lại:** trả HTTP 200 với body `{"success": true}` trong 30 giây. Khác điều kiện này, SePay coi là lỗi và gửi lại tối đa 7 lần (cách 1, 1, 2, 3, 5, 8, 13 phút, khoảng 33 phút) nếu bật tự gửi lại. Thứ tự đến không đảm bảo; xử lý theo `transactionDate`.

### A.8 Cách thử an toàn, theo bốn tầng

Không tầng nào đụng tiền thật hay DB production.

| Tầng | Làm gì | Cần gì | Kiểm được |
|---|---|---|---|
| 1. Unit test | Gọi thẳng bộ khớp trong Application với payload giả (C# object) | Không gì | Tìm mã trong nội dung có khoảng trắng/chữ thường/ký tự lạ; đủ, thiếu, thừa tiền; mã sai, mã hết hạn; `transferType = "out"` bị bỏ qua |
| 2. Integration test Web | `CustomWebApplicationFactory` gửi `POST` JSON giả tới endpoint | DB `_test` | Thiếu/sai khóa → 401, không ghi gì; gửi cùng `id` hai lần → chỉ một khoản thu; trả đúng `{"success": true}` |
| 3. Thử tay trên máy | Chạy web cục bộ, tự gửi payload giả bằng PowerShell (mẫu dưới) | DB phát triển, một hóa đơn và yêu cầu thanh toán có mã thật | Hóa đơn đổi trạng thái, khách và nhân viên nhận thông báo |
| 4. SePay Test Mode | Bật Test Mode trên my.sepay.vn, thêm tài khoản ngân hàng test, cấu hình webhook trỏ về tunnel HTTPS, dùng "Mô phỏng giao dịch" nhập số tiền và nội dung | Tài khoản SePay miễn phí, tunnel (cloudflared hoặc ngrok) | Payload thật của SePay, xác thực thật, cơ chế gửi lại; xem kết quả ở Nhật ký WebHooks |

Mẫu tầng 3 (khóa và mã yêu cầu thay bằng giá trị cục bộ; không đưa khóa thật vào tài liệu hay commit):

```powershell
$body = @{
  id = 900001; gateway = "TEST"; transactionDate = "2026-10-10 10:00:00"
  accountNumber = "<so-tai-khoan-cau-hinh>"; subAccount = ""; code = $null
  content = "TT123ABCDEF thanh toan"; transferType = "in"
  transferAmount = 1500000; accumulated = 0; referenceCode = "TEST900001"
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5009/api/v1/webhooks/sepay" `
  -Headers @{ Authorization = "Apikey <khoa-cuc-bo>" } -ContentType "application/json" -Body $body
```

Chạy lệnh hai lần với cùng `id` phải chỉ ghi một khoản thu. Đường dẫn endpoint ở trên là đề xuất, chốt khi viết spec.

**Quy tắc an toàn khi thử tầng 4:**
- Chỉ mở tunnel khi đang thử, tắt ngay sau đó; tunnel trỏ tới web dùng DB phát triển hoặc `_test`, không phải DB thật.
- Khóa webhook là chuỗi ngẫu nhiên riêng cho Test Mode, nằm trong `appsettings.json` cục bộ (đã gitignore), không ghi vào log.
- Endpoint chỉ nhận `POST`, sai khóa trả 401 trước khi đọc dữ liệu.
- Webhook, token và tài khoản ngân hàng của Test Mode tách biệt với Live; khi chạy thật phải cấu hình lại, không dùng lại khóa test.

### A.9 Câu hỏi khi viết spec

1. Nhà cung cấp cho production: SePay, Casso hay payOS (xem A.6).
2. Một tài khoản nhận chung hay mỗi chi nhánh một tài khoản? Hiện `VietQrSettings` chỉ có một tài khoản.
3. Người xác nhận khi tự ghi: để `NguoiXacNhanId` null kèm nhãn tự động (cột cho phép null), hay ghi nhân viên phân công sớm nhất như hóa đơn tự động?
4. Có cho tự ghi khi tiền lệch nhỏ (ví dụ thừa dưới 1.000đ) không?

## B. Kênh gửi: Zalo và các kênh miễn phí

### B.1 Zalo có miễn phí không

Không có cách miễn phí ổn định để gửi hóa đơn hằng tháng qua Zalo:

- **Tin theo mẫu ZNS (nay gộp thành ZBS)**, đúng loại dùng cho hóa đơn: **tính phí từng tin**. Bài tổng hợp 2026 ghi khoảng 200–300đ/tin chưa VAT; nút bấm thứ hai trở đi và ảnh đính kèm tính thêm. Cần Zalo OA, mẫu được Zalo duyệt, chỉ gửi 6–22h, doanh nghiệp tại Việt Nam, khách phải đồng ý nhận tin.
- **Zalo OA gói Cơ bản** miễn phí từ 1/6/2026, nhưng chỉ gửi miễn phí **tin tư vấn trong 48 giờ kể từ lần khách tương tác cuối** và không hỗ trợ tin ngoài khung 48 giờ. Hóa đơn gửi định kỳ khi khách thường không nhắn OA trong 48 giờ trước đó, nên kênh này không dùng được.
- Ước lượng thô nếu sau này dùng ZNS: 100 phòng × 2 tin/tháng (báo nháp + báo đã chốt) × khoảng 300đ ≈ 60.000đ/tháng, chưa VAT và phí OA.

### B.2 Kênh miễn phí thay thế

| Kênh | Chi phí | Ưu | Nhược |
|---|---|---|---|
| Email (đang dùng) | Miễn phí trong hạn mức SMTP | Có sẵn, gửi kèm PDF | Khách ít đọc, dễ vào spam |
| Thông báo cổng + SignalR (đang dùng) | Miễn phí | Có sẵn | Chỉ thấy khi mở web |
| Web Push (thông báo trình duyệt, chuẩn VAPID) | Miễn phí | Hiện thông báo trên điện thoại cả khi không mở web | Khách phải cho phép; iPhone cần thêm web vào màn hình chính (iOS 16.4+) |
| Telegram Bot | Miễn phí | API đơn giản | Ít người thuê ở Việt Nam dùng |

**Đã chốt (10/10/2026):** giai đoạn này chỉ dùng thông báo trên web + email. Web Push và Zalo để sau.

## C. Công tơ IoT dùng song song với ảnh

**Đã chốt (10/10/2026):** hiện giữ nguyên cách cũ, khách thuê và nhân viên chụp ảnh (hoặc nhân viên nhập tay). Phần dưới chỉ là phương án tham khảo để phát triển sau.

### C.1 Nguyên tắc

Mỗi trọ, thậm chí mỗi phòng, trang bị khác nhau, nên **nguồn chỉ số đặt theo từng đồng hồ của phòng**, không theo toàn hệ thống:

- Mặc định: **Ảnh** (OCR) hoặc **Nhập tay**, như hiện nay.
- Đồng hồ có thiết bị: nguồn **Thiết bị**. Một phòng có thể điện dùng IoT, nước vẫn chụp ảnh.
- Phòng có thiết bị vẫn chụp ảnh được khi thiết bị mất kết nối (dự phòng).
- Mọi nguồn đi chung một chuỗi tự động (spec 3.4) và chung kiểm tra bất thường (3.1 mục 2–3). Chỉ khác độ tin cậy: thiết bị số trả thẳng giá trị được coi là 1,0; camera AI dùng độ tin cậy thiết bị báo.

### C.2 Ba nhóm thiết bị trên thị trường

| Nhóm | Phù hợp | Cách lấy số |
|---|---|---|
| Công tơ điện tử thông minh Wi-Fi/Zigbee (ví dụ hệ Tuya) | Trọ thay công tơ mới | Cloud API hoặc mạng LAN của hãng. Phải kiểm model có trả **chỉ số kWh tích lũy**, không chỉ công suất tức thời |
| Công tơ có cổng RS485/Modbus, đồng hồ nước có đầu ra xung | Dãy trọ có tủ công tơ tập trung | Một gateway (ESP32, Raspberry Pi) đọc nhiều công tơ rồi gửi lên server; rẻ khi nhiều phòng |
| Gắn camera lên công tơ cơ cũ (ESP32-CAM, firmware mã nguồn mở AI-on-the-edge) | Không muốn thay công tơ | Thiết bị chụp định kỳ, tự đọc số, gửi số kèm ảnh. Nghiên cứu báo độ chính xác khoảng 97–98% với đồng hồ nước |

### C.3 Cách tích hợp

- **Đẩy (đề xuất làm chính):** thiết bị hoặc gateway gọi API của hệ thống (ví dụ `POST /api/v1/chi-so-thiet-bi`, thư mục `Web/Api/V1` đang trống) bằng khóa riêng từng thiết bị. Không phụ thuộc hãng.
- **Kéo (adapter theo hãng):** job định kỳ gọi cloud API của hãng (ví dụ Tuya) rồi đổi về cùng định dạng.
- **Chốt kỳ:** thiết bị gửi số định kỳ; đến mốc chốt, hệ thống lấy số gần mốc nhất trong khoảng cho phép (ví dụ ±6 giờ) làm chỉ số kỳ rồi chạy chuỗi tự động.
- **Kiểm tra riêng của thiết bị:** quá lâu không gửi số (mất kết nối) thì báo nhân viên và cho chụp ảnh thay; số giảm thì coi là bất thường, trừ khi có sự kiện **thay đồng hồ** ghi chỉ số đầu mới. Sự kiện thay đồng hồ hiện cũng chưa có cho đồng hồ thường.

### C.4 Schema sau này (cần spec riêng)

- Thiết bị đo: mã, loại, hãng, khóa API đã băm, trạng thái.
- Gắn thiết bị với phòng + loại đồng hồ theo thời gian hiệu lực (thay thiết bị không mất lịch sử).
- Chỉ số thô từ thiết bị: giá trị, thời điểm đo, thời điểm nhận, ảnh nếu có (ảnh camera dùng lại cơ chế lưu Cloudinary của `AnhChiSoDongHo`).

### C.5 Lộ trình đề xuất

1. Khi bắt đầu làm IoT: tách chính sách 3.1 của spec hóa đơn tự động thành đầu vào chung (giá trị, nguồn, độ tin cậy) để thiết bị dùng chung chuỗi.
2. API nhận chỉ số + một script giả lập thiết bị gửi số. Đủ để demo khóa luận mà không cần phần cứng.
3. Thử một thiết bị thật (ESP32-CAM cho đồng hồ nước là rẻ nhất) hoặc một công tơ Wi-Fi.

## Nguồn tham khảo

- SePay webhooks: https://developer.sepay.vn/en/sepay-webhooks
- Casso webhook: https://developer.casso.vn/english-v2-new/webhook
- Bảng giá SePay: https://sepay.vn/bang-gia.html ; Test Mode: https://docs.sepay.vn/test-mode.html , https://docs.sepay.vn/gia-lap-giao-dich.html
- SePay payload, xác thực, gửi lại: https://developer.sepay.vn/en/sepay-webhooks/bat-dau-nhanh , https://developer.sepay.vn/en/sepay-webhooks/xac-thuc , https://developer.sepay.vn/en/sepay-webhooks/xu-ly-loi
- Bảng giá Casso: https://casso.vn/flow/bang-gia/ , https://api.casso.vn/pricing-table
- payOS miễn phí 2026: https://payos.vn/cong-thanh-toan-mien-phi-2026/ , https://payos.vn/
- Bảng giá ZBS 2026 (tổng hợp): https://www.oazns.vn/gia-zbs/ , https://zns.nhanh.vn/bang-gia-dich-vu-zbs-va-cach-tinh-phi-zbs-template-message-cuc-de-n97625.html
- Gói Zalo OA từ 1/6/2026 (tổng hợp): https://miniai.vn/so-sanh-goi-zalo-oa-cu-va-moi/ , https://miniai.vn/bang-gia-zalo-oa-moi-nhat-2026/
- Quy định mẫu ZNS: https://www.infobip.com/docs/zalo/compliance-guidelines
- ESP32-CAM đọc đồng hồ: https://www.libe.net/en/watermeter , https://doaj.org/article/23bd51c83fb1438aa6ebb2c770547e8d , https://www.e3s-conferences.org/articles/e3sconf/abs/2026/41/e3sconf_aiei2026_04004/e3sconf_aiei2026_04004.html
- Thư viện đọc thiết bị Tuya qua LAN: https://pypi.org/project/tuyapower/
