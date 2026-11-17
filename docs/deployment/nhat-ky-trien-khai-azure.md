# Nhật ký triển khai TroSync lên Azure

Ghi lại các bước đã làm thật khi đưa TroSync lên Azure, theo thứ tự, kèm lựa chọn và sự cố gặp phải. Hướng dẫn chung (giải thích từng mục, bảng biến môi trường đầy đủ) ở [azure.md](azure.md); file này ghi **đã làm gì, chọn gì, ở đâu**.

Không ghi vào file này: mật khẩu, khóa API, secret, chuỗi kết nối có mật khẩu, publish profile, email hay họ tên. Repo đang công khai. Bí mật cất ở trình quản lý mật khẩu của bạn và trong App settings của Azure, GitHub Secrets.

## Tiến độ

| Bước | Việc | Trạng thái | Ngày |
| --- | --- | --- | --- |
| 0 | Chuẩn bị code, workflow GitHub Actions, CI chạy xanh | Xong | 16/11/2026 |
| 1 | Kiểm tài khoản Azure: credit, Free services, vùng được phép, Budget | Xong | 16/11/2026 |
| 2 | Tạo Resource group và PostgreSQL | Đang làm | |
| 3 | Tạo App Service plan và Web App | Chưa làm | |
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
| Vùng dùng cho mọi tài nguyên | **Korea Central** (`koreacentral`) |
| Resource group | `rg-trosync` (dự kiến) |
| PostgreSQL server | chưa tạo (tên dạng `<ten>.postgres.database.azure.com`) |
| Database | `trosync` (dự kiến) |
| Tài khoản admin PostgreSQL | chưa tạo (mật khẩu: chỉ lưu ở trình quản lý mật khẩu) |
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

- Vùng được phép (đã thấy): `malaysiawest`, `koreacentral`, `indiasouthcentral`, `indonesiacentral` (danh sách bị cắt, có thể còn một vùng; bấm biểu tượng copy cạnh ô để xem đủ).
- **Southeast Asia (Singapore) không được phép.**
- Trạng thái "Not registered" màu đỏ của policy là bình thường với tài khoản mới.

**Đã chọn Korea Central**: vùng lâu năm, chắc chắn có PostgreSQL B1ms và App Service B1. Malaysia West, Indonesia Central gần hơn nhưng là vùng mới, có thể thiếu gói dịch vụ. Chênh lệch tốc độ vài chục mili giây, người dùng không cảm nhận được. Web App và PostgreSQL phải cùng vùng này.

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

## Bước 2. Resource group và PostgreSQL (đang làm)

Kế hoạch (chi tiết ở [azure.md](azure.md), Bước 1):

- Resource group `rg-trosync`, vùng **Korea Central**.
- PostgreSQL Flexible Server: Korea Central, PostgreSQL 16, Workload **Development**, Compute **Burstable Standard_B1ms**, Storage **32 GiB**, tắt High availability, backup 7 ngày; xác thực PostgreSQL; Networking **Public access** + tick "Allow public access from any Azure service".
- Tạo database `trosync`.

Kết quả: _(điền khi xong)_

## Bước 3 trở đi

Sẽ ghi tiếp khi làm.
