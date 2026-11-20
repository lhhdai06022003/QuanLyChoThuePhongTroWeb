# Nhật ký triển khai TroSync lên Azure

Ghi lại các bước đã làm thật khi đưa TroSync lên Azure, theo thứ tự, kèm lựa chọn và sự cố gặp phải. Hướng dẫn chung (giải thích từng mục, bảng biến môi trường đầy đủ) ở [azure.md](azure.md); file này ghi **đã làm gì, chọn gì, ở đâu**.

Không ghi vào file này: mật khẩu, khóa API, secret, chuỗi kết nối có mật khẩu, publish profile, email hay họ tên. Repo đang công khai. Bí mật cất ở trình quản lý mật khẩu của bạn và trong App settings của Azure, GitHub Secrets.

## Tiến độ

| Bước | Việc | Trạng thái | Ngày |
| --- | --- | --- | --- |
| 0 | Chuẩn bị code, workflow GitHub Actions, CI chạy xanh | Xong | 16/11/2026 |
| 1 | Kiểm tài khoản Azure: credit, Free services, vùng được phép, Budget | Xong | 16/11/2026 |
| 2 | Tạo Resource group và PostgreSQL | Xong | 19/11/2026 |
| 3 | Tạo App Service plan và Web App | Đang làm | |
| 4 | Cấu hình Web App và biến môi trường | Chưa làm | |
| 5 | Nối GitHub với Azure, deploy lần đầu, mở package GHCR | Chưa làm | |
| 6 | Mở web, tạo admin, kiểm sau deploy | Chưa làm | |
| 7 | Gmail và Telegram | Chưa làm | |
| 8 | SePay Test mode, rồi chạy thật | Chưa làm | |
| 9 | (Tùy chọn) Tên miền riêng từ GitHub Student Pack | Chưa làm | |

## Giá trị cần nhớ (không bí mật)

| Mục | Giá trị |
| --- | --- |
| Subscription | Azure for Students |
| Vùng của PostgreSQL và Web App | **Japan East** (`japaneast`). Korea Central bị lỗi `SkuNotAvailable` với PostgreSQL B1ms |
| Resource group | `rg-trosync` (đã tạo 17/11/2026, Korea Central) |
| PostgreSQL server | đã tạo 19/11/2026, Japan East, Burstable B1ms, 32 GiB, PostgreSQL 16 (tên và Endpoint `<ten>.postgres.database.azure.com` ghi trong bản Word và trình quản lý mật khẩu) |
| Database | `trosync` (đã tạo 19/11/2026) |
| Tài khoản admin PostgreSQL | đã đặt (tên và mật khẩu chỉ lưu ở trình quản lý mật khẩu) |
| App Service plan | chưa tạo (Linux, B1) |
| Web App | chưa tạo (địa chỉ `https://<ten-app>.azurewebsites.net`) |
| Image | `ghcr.io/lhhdai06022003/quanlychothuephongtroweb` |
| Budget | `trosync-budget`, 35 USD/tháng, cảnh báo 50/80/100% |

---

## Bước 0. Chuẩn bị code và CI (xong 16/11/2026)

Đã làm trong repo:

- Bỏ đoạn SMTP chấp nhận mọi chứng chỉ; khóa đăng nhập (Data Protection) lưu ra thư mục bền khi có `DataProtection__KeysPath`.
- Thêm workflow `.github/workflows/ci-cd.yml`: mỗi lần push `master` chạy build và test 4 tầng trên máy Windows của GitHub (có PostgreSQL), sau đó build image Docker và deploy lên Azure. Phần deploy tự bỏ qua khi chưa có biến `AZURE_WEBAPP_NAME`.
- Commit `80aa2ad` (chuẩn bị triển khai), `a6a4567` (sửa một test).

Chi phí GitHub Actions: miễn phí vì repo công khai và dùng runner tiêu chuẩn.

**Sự cố đã gặp:** lần chạy CI đầu FAIL 1/1063 test Web (`ContractPagePayloadTests`). Nguyên nhân: trên DB mới của GitHub, mã chi nhánh trùng mã người thuê nên test nhận nhầm; lỗi của test, không phải của web. Sửa test, CI lần hai xanh toàn bộ.

Xem log CI: repo công khai nhưng log chỉ xem được khi đăng nhập GitHub (tài khoản nào cũng được, không cần là chủ repo).

## Bước 1. Kiểm tài khoản Azure (xong 16/11/2026)

Làm trên portal.azure.com, đăng nhập bằng tài khoản Azure for Students.

### 1.1 Credit — xong

Ô tìm kiếm → **Education** → **Overview**.

- Credit: **100 USD**.
- Hết hạn sau **332 ngày** tính từ 16/11/2026, tức khoảng **14/10/2027**.
- Tài khoản Student không gắn thẻ: hết credit thì dịch vụ bị dừng, không trừ tiền.

### 1.2 Free services — xong

**Education → Free services**: có **Azure Database for PostgreSQL**, 750 giờ/tháng, Burstable **B1MS**, 32 GB lưu trữ, 32 GB backup.

Hệ quả chi phí: PostgreSQL miễn phí nếu tạo đúng B1ms + 32 GB; chỉ tốn App Service B1 khoảng 13 USD/tháng, nên 100 USD dùng được khoảng 7 tháng.

### 1.3 Vùng được phép — xong

**Subscriptions → Azure for Students → Policies → Compliance** → dòng **Allowed resource deployment regions** → **View assignment** → **Parameters** → `Allowed locations`.

- Vùng được phép: `malaysiawest`, `koreacentral`, `indiasouthcentral`, `indonesiacentral`, `japaneast` (vùng cuối bị cắt trên portal, xác nhận bằng Cloud Shell ngày 17/11/2026).
- **Southeast Asia (Singapore) không được phép.**
- Trạng thái "Not registered" màu đỏ của policy là bình thường với tài khoản mới.

Lúc đầu chọn Korea Central (vùng lâu năm). Khi tạo PostgreSQL B1ms thì bị lỗi `SkuNotAvailable` (Bước 2), nên **chuyển sang Japan East**. Vùng được phép không bảo đảm có máy B1ms cho tài khoản Student. Web App và PostgreSQL phải cùng vùng.

### 1.4 Budget (cảnh báo chi phí) — xong

Mục đích: Azure gửi email khi chi phí trong tháng chạm mức đặt trước. Bình thường khoảng 13 USD/tháng nên không có email; có email nghĩa là đã tạo nhầm gói tính phí.

1. Ô tìm kiếm → **Cost Management**.
2. Dòng **Scope** phía trên mặc định là tài khoản thanh toán (Billing account). Bấm **(change)** → chọn subscription **Azure for Students** → **Select**.
3. Menu trái **Monitoring → Budgets** → **+ Add**.
4. Tab **Create a budget**: Name `trosync-budget`, Reset period **Monthly**, Creation/Expiration date để mặc định, Amount **35** → **Next**.
5. Tab **Set alerts**: 3 dòng **Actual cost** 50, 80, 100 (Amount tự tính 17,5 / 28 / 35); Action group để None.
6. **Alert recipients (email)**: nhập email → **Create**.

Chữ đỏ "Action group or at least one email address is required" tự hết khi đã nhập email. Lỗi "Unique email address is required" / "value cannot be empty": mỗi email nhập một lần, xóa dòng trống thừa bằng biểu tượng thùng rác. Nên thêm `azure-noreply@microsoft.com` vào danh bạ để cảnh báo không vào Spam.

Nếu portal báo subscription không hỗ trợ Budget: bỏ qua, xem credit ở **Education → Overview** mỗi tuần.

Kết quả: đã tạo `trosync-budget`, Monthly, 35 USD, hiệu lực 01/11/2026 đến 31/10/2028, cảnh báo 50/80/100% qua email. Chi phí hiện tại 0 USD.

## Bước 2. Resource group và PostgreSQL (xong 19/11/2026)

### 2.1 Resource group — xong 17/11/2026

**Resource groups → + Create**: Subscription Azure for Students, tên `rg-trosync`, Region đổi từ (US) East US mặc định sang **(Asia Pacific) Korea Central** → Review + create → Create. Danh sách hiện `rg-trosync`, Korea Central.

### 2.2 PostgreSQL Flexible Server — xong 19/11/2026 (lần hai, Japan East)

Form **New Azure Database for PostgreSQL flexible server** mặc định: Region Canada Central, version 18, Workload Production, General Purpose D4ads_v5 (4 vCore, 128 GiB). Phải đổi hết:

- Basics: Resource group `rg-trosync`; Region (đổi trước, vì đổi vùng có thể đặt lại cấu hình máy): lần một Korea Central (lỗi, xem dưới), lần hai **Japan East**; version **16**; Workload **Dev/Test**; Availability zone No preference; High availability **Disabled**.
- Configure server: Compute tier **Burstable**, Compute size **Standard_B1ms**, Storage Premium SSD **32 GiB**, autogrow tắt, backup 7 ngày, không Geo-redundancy → Save.
- Authentication: PostgreSQL authentication only, admin username tự đặt.
- Networking: Public access, tick "Allow public access from any Azure service within Azure to this server".

**Sai đã phát hiện khi xem ảnh:** màn Compute + storage đang chọn **Standard_B2s** (ước tính 80,11 USD/tháng, không thuộc gói miễn phí). Đổi sang Standard_B1ms trước khi Save. Không chọn version 18: bản quá mới so với Npgsql 8 dự án dùng.

Đã sửa sang B1ms (17/11/2026). Ô Estimated costs hiện chữ xanh "Free upto 750 hours" và "Free upto 32 GB", tổng 23,17 USD/tháng là giá trước khi trừ phần miễn phí. Kiểm ở Cost analysis sau 2–3 ngày.

Các tab còn lại:

- Networking: Public access (allowed IP addresses) and private endpoints; tick "Allow public access to this resource through the internet" và "Allow public access from any Azure service within Azure to this server"; 0 firewall rule. Không bấm "+ Add 0.0.0.0 - 255.255.255.255" (mở DB cho cả Internet), chưa thêm IP máy cá nhân.
- Security: Service-managed key. Tags: trống.
- Review + create lần một đã kiểm: rg-trosync, Korea Central, version 16, Burstable B1ms 32 GiB P4, High availability / autogrow / Geo-redundancy Not enabled, backup 7 ngày. Tên server và tên đăng nhập quản trị đã đặt (không ghi ở đây; lưu cùng mật khẩu).

**Sự cố 17/11/2026, lần tạo đầu ở Korea Central:** deployment failed, Status Conflict, lỗi `SkuNotAvailable` ("Specified SKU is not supported in this region"). Tài khoản Student bị chặn B1ms ở vùng này dù form cho chọn. Không tốn tiền.

Kiểm bằng Cloud Shell (Bash, chọn "No storage account required"; lệnh chỉ đọc):

- `az policy assignment list --query "[].parameters.listOfAllowedLocations.value"`: đủ 5 vùng được phép là `malaysiawest`, `koreacentral`, `indiasouthcentral`, `indonesiacentral`, **`japaneast`** (danh sách ở Bước 1.3 bị cắt mất vùng cuối).
- `az postgres flexible-server list-skus -l <vung>`: cả 4 vùng đầu đều liệt kê Standard_B1ms, kể cả Korea Central vừa lỗi. Lệnh này **không** báo hạn chế riêng của tài khoản, chỉ dùng tham khảo.
- `az appservice list-locations --sku B1 --linux-workers-enabled`: cả 5 vùng đều có App Service B1 Linux.

Hướng xử lý: tạo lại ở vùng khác, thứ tự thử Japan East → Malaysia West → Indonesia Central → India South Central. Web App (Bước 3) phải đặt cùng vùng với PostgreSQL tạo được. Resource group `rg-trosync` giữ nguyên ở Korea Central (vùng của resource group không ràng buộc vùng tài nguyên bên trong).

**Lần hai, Japan East (19/11/2026):**

- Đặt tên server mới (không dùng lại tên lần một). Form ở Japan East đặt lại nhiều mục: Region về Canada Central, Compute về Standard_B2s, Authentication về "PostgreSQL and Microsoft Entra", ô "Allow public access from any Azure service" bị bỏ tick. Phải kiểm lại từng mục.
- Configure server: **Storage type mặc định Premium SSD v2**, làm ô Burstable bị mờ ("Burstable compute tier is not supported for server with Premium SSD v2"), ước tính 747,80 USD/tháng. Đổi Storage type sang **Premium SSD** trước, rồi mới chọn Burstable → Standard_B1ms. Sau đó portal hiện "Free upto 750 hours / 32 GB", tổng ước tính 23,40 USD/tháng (giá trước khi trừ miễn phí).
- Review + create: Japan East, Availability zone No preference, version 16, Burstable B1ms 32 GiB P4, High availability / autogrow / Geo-redundancy / Immutable Not enabled, backup 7 ngày, Public access + Allow Azure service Yes, 0 firewall rule, Service-managed key.
- Kết quả: deployment complete khoảng 5 phút. Overview: Status **Ready**, Japan East, Burstable B1ms 1 vCore 2 GiB RAM 32 GiB, PostgreSQL 16.15, Availability zone 1, High availability Not enabled.

Server lỗi ở Korea Central: dòng còn sót trong `rg-trosync`, Overview báo "Cannot read properties of undefined (reading 'createdAt')", `az postgres flexible-server show` báo `ResourceNotFound`, danh sách hiện Status **Dropping** rồi tự mất. `az postgres flexible-server list -o table` không còn server nào trước khi tạo lần hai. Không sửa được server lỗi (vùng không đổi được sau khi tạo). Phần miễn phí 750 giờ tính chung cả tài khoản, chỉ giữ một server chạy.

### 2.3 Database `trosync` — xong 19/11/2026

Server → **Settings → Databases** → **+ Add** → Name `trosync` (UTF8, en_US.utf8 mặc định) → Save. Danh sách: `azure_maintenance`, `azure_sys` (hệ thống), `postgres`, `trosync`. Endpoint của server ghi ở trình quản lý mật khẩu cùng tên đăng nhập quản trị (Bước 4 cần để tạo chuỗi kết nối).

## Bước 3 trở đi

Sẽ ghi tiếp khi làm.
