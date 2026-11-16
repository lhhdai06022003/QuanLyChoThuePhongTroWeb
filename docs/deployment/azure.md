# Triển khai lên Azure (App Service + PostgreSQL) bằng GitHub Actions

Hướng dẫn cho tài khoản Azure for Students. Workflow: [.github/workflows/ci-cd.yml](../../.github/workflows/ci-cd.yml). Cấu hình chung và bảo mật: [README.md](README.md).

## Kiến trúc

```
push master ──► GitHub Actions
                 ├─ job test   (windows-latest + PostgreSQL có sẵn của runner: build, test 4 tầng, kiểm migration)
                 └─ job deploy (ubuntu: build Dockerfile → ghcr.io/<owner>/<repo>:<sha> → Azure App Service)

Azure App Service (Linux, container, B1) ──► Azure Database for PostgreSQL Flexible Server (B1ms)
        └─► Cloudinary, SMTP Gmail, SePay/VNPay, Telegram (dịch vụ ngoài, giữ nguyên)
```

Vì sao chọn như vậy:

- Phải chạy bằng container: model ONNX đọc đồng hồ cần `libgomp1`, Dockerfile đã cài; image .NET dựng sẵn của App Service không có.
- App Service B1 (không phải Container Apps): các job nền, hàng đợi thông báo thanh toán và SignalR cần process chạy liên tục; Container Apps co về 0 sẽ dừng chúng và mỗi lần mở lại phải nạp model 44 MB.
- Image để trên GHCR (miễn phí) thay vì Azure Container Registry (tính phí).

## Chi phí ước tính

Giá thay đổi theo vùng và thời điểm; kiểm lại bằng [Azure Pricing Calculator](https://azure.microsoft.com/pricing/calculator/) trước khi tạo.

| Dịch vụ | Gói | Ước tính/tháng |
| --- | --- | --- |
| App Service plan Linux | B1 (1 vCPU, 1,75 GB RAM) | khoảng 13 USD |
| PostgreSQL Flexible Server | Burstable B1ms, 32 GB | khoảng 15–18 USD (có thể miễn phí 12 tháng, xem mục Free services trong portal) |
| GHCR, GitHub Actions | Repo công khai | 0 |

100 USD đủ khoảng 3 tháng; nếu PostgreSQL nằm trong Free services thì khoảng 7 tháng. Tiết kiệm:

- Tạo **Budget** (Cost Management → Budgets) với cảnh báo 20/50/80 USD.
- Không demo thì **Stop** PostgreSQL (Azure tự bật lại sau 7 ngày). App Service plan vẫn tính tiền khi web dừng; muốn ngừng hẳn thì xóa plan.
- Không gói F1: không có Always On, RAM 1 GB không đủ nạp model ONNX cùng app.

## Bước 1. Tạo tài nguyên trên Azure Portal

Tài khoản Student giới hạn vùng được phép tạo; chọn Southeast Asia hoặc East Asia nếu được, không được thì vùng gần nhất trong danh sách cho phép. Đặt tất cả vào cùng một vùng.

1. **Resource group**: ví dụ `rg-trosync`.
2. **Azure Database for PostgreSQL – Flexible Server**:
   - Workload: Development; Compute: Burstable **B1ms**; Storage 32 GB; PostgreSQL 16.
   - Authentication: PostgreSQL authentication; tự đặt tên và mật khẩu quản trị.
   - Networking: Public access; tick **Allow public access from any Azure service within Azure to this server**. Thêm IP máy bạn nếu cần kết nối bằng pgAdmin.
   - Tạo xong, vào **Databases** → Add, tên ví dụ `trosync`.
   - Chuỗi kết nối: `Host=<server>.postgres.database.azure.com;Port=5432;Database=trosync;Username=<admin>;Password=<mat-khau>;SSL Mode=Require`
3. **App Service plan**: Linux, gói **B1**.
4. **Web App**: Publish = **Container**, OS Linux, chọn plan vừa tạo. Ở tab Container chọn Other container registries, image tạm `mcr.microsoft.com/appsvc/staticsite:latest` (workflow sẽ thay bằng image thật ở lần deploy đầu).

## Bước 2. Cấu hình Web App

**Settings → Configuration → General settings**:

- Always On: **On** (job nền chạy liên tục).
- Web sockets: **On** (SignalR `/thongBaoHub`).
- HTTPS Only: **On**. Minimum TLS 1.2.
- SCM Basic Auth Publishing Credentials: **On** (cần để tải publish profile).

**Settings → Health check**: đường dẫn `/QuanLyNhaTro/DangNhap`.

**Settings → Environment variables → App settings** (bấm "Show values" để kiểm, không chép ra nơi khác):

| Tên | Giá trị | Ghi chú |
| --- | --- | --- |
| `WEBSITES_PORT` | `8080` | Cổng container lắng nghe (Dockerfile) |
| `WEBSITES_ENABLE_APP_SERVICE_STORAGE` | `true` | Gắn thư mục bền `/home` vào container |
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | Bắt buộc: Azure kết thúc HTTPS trước container; thiếu thì app tưởng request là HTTP (cookie không Secure, không HSTS) và mọi người dùng chung một IP, giới hạn đăng nhập sai theo IP sẽ khóa cả hệ thống |
| `DataProtection__KeysPath` | `/home/DataProtection-Keys` | Giữ khóa cookie qua các lần deploy/khởi động lại |
| `ConnectionStrings__DefaultConnection` | chuỗi ở Bước 1 | Có `SSL Mode=Require` |
| `AdminSeed__Password` | mật khẩu admin lần đầu | Tối thiểu 8 ký tự; xóa biến sau khi đã đăng nhập và đổi mật khẩu |
| `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` | | Ảnh đồng hồ, minh chứng |
| `EmailSettings__SenderEmail`, `EmailSettings__Password` | Gmail + App Password | |
| `VietQRSettings__BankId`, `VietQRSettings__AccountNumber`, `VietQRSettings__AccountName` | | |
| `Gemini__ApiKey` (và `Gemini__Model` nếu cần) | | Trợ lý AI |
| `OnlinePayment__PublicBaseUrl` | `https://<ten-app>.azurewebsites.net` | Link trong email, webhook/IPN |
| `ContractAlertSettings__AdminEmail` | | Nhận cảnh báo hợp đồng |

Các khóa tùy chọn khác (SePay, VNPay, Telegram, giới hạn đăng nhập...) xem [.env.example](../../.env.example); tên biến giống hệt.

Hai file trạng thái job (`sent_invoice_reminders.json`, `sent_contract_alerts.json`) nằm ở `/app` trong container, mất sau mỗi lần deploy: email nhắc hóa đơn hoặc cảnh báo hợp đồng có thể gửi lại một lần. Chấp nhận được khi demo; chỉ chạy **một** instance (không scale out).

## Bước 3. Nối GitHub Actions

1. Web App → **Overview → Download publish profile**. Mở file, chép toàn bộ nội dung.
2. GitHub repo → **Settings → Secrets and variables → Actions**:
   - Tab **Secrets** → New repository secret: `AZURE_WEBAPP_PUBLISH_PROFILE` = nội dung file vừa chép. Xóa file publish profile khỏi máy sau đó.
   - Tab **Variables** → New repository variable: `AZURE_WEBAPP_NAME` = tên Web App.
3. Chưa có biến `AZURE_WEBAPP_NAME` thì job deploy tự bỏ qua, job test vẫn chạy cho mọi push/PR.

Tài khoản Student thuộc tenant của trường thường không cho tạo App registration, nên workflow dùng publish profile thay vì đăng nhập OIDC (`azure/login`). Nếu tạo được App registration, có thể đổi sang OIDC để khỏi giữ publish profile.

## Bước 4. Lần deploy đầu

1. Push `master` (hoặc Actions → CI/CD → Run workflow).
2. Lần đầu GHCR tạo package ở chế độ **private**, App Service không kéo được image. Vào GitHub → Profile → **Packages** → package của repo → Package settings → **Change visibility → Public** (image không chứa bí mật: `.dockerignore` loại `appsettings*.json` và `.env`). Rồi chạy lại job deploy.
   - Muốn giữ private: tạo Personal access token (classic) quyền `read:packages`, khai trong Web App → Deployment Center: registry `https://ghcr.io`, username GitHub, password là token.
3. Theo dõi log: Web App → **Log stream**. Lần khởi động đầu app tự migrate DB trống và tạo admin từ `AdminSeed__Password`.

## Bước 5. Kiểm sau deploy

1. `https://<ten-app>.azurewebsites.net/QuanLyNhaTro/DangNhap` mở được, khóa ổ khóa HTTPS.
2. Đăng nhập `admin`, đổi mật khẩu, xóa biến `AdminSeed__Password`.
3. Tạo chi nhánh, phòng, hợp đồng thử; tải ảnh (Cloudinary); gửi email hóa đơn (SMTP).
4. Chuông thông báo cập nhật tức thì (SignalR).
5. Restart Web App rồi tải lại trang: vẫn đăng nhập (khóa Data Protection bền).
6. Thanh toán trực tuyến: chạy thật theo **Bước 6** (VietQR + SePay). Chỉ khi thử VNPay sandbox mới cần khai IPN URL `https://<ten-app>.azurewebsites.net/api/webhooks/payment/vnpay-ipn`; Return URL app tự ghép từ địa chỉ request (cần `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` để ra `https://`).

## Bước 6. Chạy thật: nhận tiền qua VietQR + SePay (không VNPay)

VNPay chạy thật cần đăng ký merchant (thường phải có giấy phép kinh doanh hoặc hộ kinh doanh), nên khi chủ trọ là cá nhân thì chỉ dùng VietQR + SePay. `VnPay__Enabled` mặc định `false`: không đặt biến này thì khách không thấy nút VNPay và đường dẫn thanh toán VNPay bị chặn.

### Tiền đi thế nào

Khách quét mã VietQR trên hóa đơn (email hoặc cổng khách thuê) và chuyển **thẳng vào tài khoản ngân hàng của chủ trọ**; app và Azure không giữ tiền. SePay theo dõi tài khoản đó, có tiền vào thì gọi webhook; app khớp nội dung `TTPTHD{HoaDonId}`, ghi sổ hóa đơn, gửi email biên lai, báo Telegram và chuông trong web. Không bật SePay thì nhân viên tự xác nhận đã thu trong cổng quản lý.

### Gói SePay

Theo [bảng giá SePay](https://sepay.vn/bang-gia.html) (xem ngày 16/11/2026, kiểm lại khi đăng ký): nhận cả tài khoản cá nhân; gói **Free** 0đ, 50 giao dịch/tháng (vượt vẫn nhận, phí tính sau), có webhook và API; gói STARTUP 120.000đ/tháng cho 180 giao dịch. Ngân hàng hỗ trợ: Vietcombank, VPBank, ACB, Sacombank, VietinBank, MB Bank, BIDV, MSB, TPBank, KienlongBank, Shinhan Bank, OCB.

Mỗi lần khách chuyển là một giao dịch (trả hai đợt = hai giao dịch). Dưới khoảng 40 phòng thì gói Free thường đủ.

### Thiết lập trên SePay

1. Đăng ký my.sepay.vn, liên kết tài khoản ngân hàng thật nhận tiền thuê.
2. **Webhooks → Add Webhook**: URL `https://<ten-app>.azurewebsites.net/api/webhooks/payment/sepay`, sự kiện tiền vào, xác thực **HMAC-SHA256**. Chép secret.
3. Tạo **API token** (cho màn Giao dịch SePay, nút Đồng bộ bù).

### Biến môi trường trên Azure

| Tên | Giá trị |
| --- | --- |
| `VietQRSettings__BankId` | Mã VietQR của ngân hàng, ví dụ `MB`, `VCB`, `BIDV` |
| `VietQRSettings__AccountNumber` | **Đúng** số tài khoản đã liên kết trên SePay (khác thì giao dịch bị loại, lý do `WrongAccount`) |
| `VietQRSettings__AccountName` | Tên chủ tài khoản, viết hoa không dấu |
| `SePay__Enabled` | `true` |
| `SePay__WebhookSecret` | Secret ở bước 2 |
| `SePay__ApiToken` | Token ở bước 3 |
| `SePay__ApiBaseUrl` | **Không đặt** (mặc định là máy chủ thật `https://userapi.sepay.vn/v2`). Chỉ khi dùng Test mode mới đặt `https://userapi-sandbox.sepay.vn/v2` |
| `SePay__SyncRecordEnabled` | **Không đặt** (tắt) cho tới khi đạt mục 7 của hướng dẫn thanh toán với giao dịch thật |
| `Telegram__Enabled`, `Telegram__BotToken`, `Telegram__DefaultChatId` | Bot báo thu tiền cho chủ trọ (mục 2 hướng dẫn thanh toán) |
| `EmailSettings__SenderEmail`, `EmailSettings__Password` | Gmail riêng cho hệ thống + App Password (giới hạn khoảng 500 thư/ngày) |

Đổi biến xong, Web App tự khởi động lại. `SePay__Enabled=true` mà thiếu secret thì web không khởi động (xem Log stream).

### Danh sách kiểm trước khi giao cho khách thuê

Luồng SePay mới có test tự động, chưa chạy với webhook thật. Làm theo thứ tự, bước sau chỉ làm khi bước trước đạt:

- [ ] **Test mode trên Azure**: bật Test mode trên SePay, đặt `SePay__ApiBaseUrl` sandbox, dùng tài khoản ngân hàng giả của SePay; chạy kịch bản mục 6 của [hướng dẫn thanh toán](../features/thanh-toan-truc-tuyen/huong-dan-thiet-lap.md) (đủ, thiếu, thừa, sai nội dung, gửi lặp).
- [ ] **Chuyển sang thật**: tắt Test mode, xóa `SePay__ApiBaseUrl`, đổi `VietQRSettings__*` và webhook secret sang tài khoản thật.
- [ ] **Giao dịch thật nhỏ**: tạo một hóa đơn thử 10.000đ cho hồ sơ khách của chính bạn, quét QR và chuyển đúng nội dung `TTPTHD{HoaDonId}`. Đúng khi: hóa đơn "Đã thanh toán", email biên lai tới, Telegram báo, Delivery Logs của SePay trả 200.
- [ ] **Sai nội dung**: chuyển 10.000đ với nội dung khác. Đúng khi: Telegram báo "CHUYỂN KHOẢN CẦN XỬ LÝ TAY", hóa đơn không đổi.
- [ ] **Đồng bộ bù** (tùy chọn): làm mục 7 của hướng dẫn thanh toán với hai giao dịch thật trên; chỉ bật `SePay__SyncRecordEnabled` khi đạt (a) và (b).
- [ ] **Bảo mật**: đã đổi mật khẩu admin, xóa `AdminSeed__Password`; chỉ nhập dữ liệu khách thật sau khi các bước trên đạt (CCCD, số điện thoại nằm trên web công khai).
- [ ] **Chi phí**: đã có Budget alert; biết trước credit Student hết sau 12 tháng hoặc khi dùng hết 100 USD.

Tiền thuê vào tài khoản cá nhân: nghĩa vụ kê khai thuế cho thuê nhà là việc của chủ trọ, không thuộc phạm vi app.

## Sự cố thường gặp

| Hiện tượng | Nguyên nhân thường gặp |
| --- | --- |
| Container không lên, log "Failed to pull image" | Package GHCR còn private (Bước 4) |
| Lỗi 503 sau deploy, log báo lỗi kết nối DB | Thiếu `SSL Mode=Require`, chưa tick "Allow Azure services", sai tên database |
| Log "Chưa có tài khoản admin" | Chưa đặt `AdminSeed__Password` |
| Đăng xuất sau mỗi lần deploy | Thiếu `DataProtection__KeysPath` hoặc `WEBSITES_ENABLE_APP_SERVICE_STORAGE` |
| Đăng nhập sai vài lần mà mọi người đều bị khóa | Thiếu `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` |
| Job deploy bị bỏ qua | Chưa tạo biến `AZURE_WEBAPP_NAME` |
| SePay Delivery Logs trả 401 | Sai `SePay__WebhookSecret` hoặc đang dùng secret của Test mode |
| SePay Delivery Logs trả 404 | `SePay__Enabled` chưa là `true` |
| Tiền đã vào nhưng hóa đơn không đổi, Telegram có cảnh báo | Nội dung chuyển khoản thiếu `TTPTHD{HoaDonId}`, hoặc `VietQRSettings__AccountNumber` khác tài khoản trên SePay: nhân viên xác nhận tay trong cổng quản lý |
| Gói SePay báo vượt hạn mức | Quá 50 giao dịch/tháng ở gói Free: nâng gói hoặc chấp nhận phí vượt |
| Job test lỗi "connection refused" ở bước PostgreSQL | Ảnh runner Windows đổi; xem log bước "Bật PostgreSQL" |
