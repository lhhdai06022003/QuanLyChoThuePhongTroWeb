# Tối ưu hiệu năng cổng quản lý và cổng khách thuê

Nhánh `feature/toi-uu-hieu-nang`. Phạm vi đã duyệt (08/11/2026): bước 0–4. Ràng buộc: không đổi hành vi, API, giao diện; mỗi bước một commit; không thêm thư viện.

| Bước | Nội dung |
| --- | --- |
| 0 | Đo baseline: đếm lệnh SQL bằng `QueryCounter` (DiagnosticListener có sẵn của EF Core) trong `QueryBudgetTests` |
| 1 | Mục HD5 của Việc cần làm: lấy ngữ cảnh phòng theo lô thay vì từng phòng |
| 2 | Nhớ phạm vi chi nhánh của người dùng trong một request |
| 3 | Bỏ Chart.js, Select2, flatpickr, Font Awesome khỏi layout chung, chỉ nạp ở trang dùng |
| 4 | Tách JS/CSS của widget trợ lý AI ra file tĩnh |
| 5 (chưa duyệt) | Nén CSS/JS/font và `Cache-Control` cho file tĩnh: cần sửa `Program.cs` |

## Số đo gốc (commit bước 0)

Lệnh: `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests --filter "FullyQualifiedName~QueryBudgetTests" --logger "console;verbosity=detailed"` (DB `*_test`). Nhân viên chi nhánh A trong `BranchScopeKit.SeedAsync`, 1 hợp đồng.

| Request | Lệnh SQL |
| --- | --- |
| `GET /QuanLyNhaTro/ViecCanLam/TomTat` (huy hiệu, chạy trên mọi trang quản lý), 0 ứng viên HD5 | 16 |
| Như trên, 5 ứng viên HD5 (phòng không hợp đồng) | 46 (mỗi ứng viên +6; phòng có hợp đồng tới +9) |
| `GET /QuanLyNhaTro/Dashboard` (5 ứng viên) | 55 |
| `GET /QuanLyNhaTro/ViecCanLam` (5 ứng viên) | 51 |
| `GET /QuanLyNhaTro/QuanLyHoaDon` | 4 |
| `GET /QuanLyNhaTro/QuanLyHopDong` | 8 |
| `POST /HoaDon/GetList` (10 dòng) | 4 |
| `GET /api/ThongBao/LayMoiNhat` (chuông, mọi trang) | 2 |
| `GET /KhachThue/Dashboard` | 7 |
| `GET /KhachThue/HoaDon` | 1 |

Một lần mở trang quản lý bất kỳ = trang + `TomTat` + `LayMoiNhat`. Ví dụ trang Hóa đơn với 5 ứng viên: 4 + 46 + 2 = 52 lệnh. Ứng viên HD5 tối đa 60 (`ViecCanLam:SoKyPhongDanhGiaHoaDonTuDong`), nên huy hiệu có thể lên tới khoảng 16 + 60 × 9 = 556 lệnh.

## Dung lượng tài nguyên layout (đo 08/11/2026)

Thư viện CDN, dung lượng tải đã nén (`curl --compressed`):

| Thư viện | Nén | Trang dùng |
| --- | --- | --- |
| Chart.js (`npm/chart.js`, không ghim phiên bản) | 61.412 B | 2 file |
| Select2 JS + CSS | 18.254 + 1.640 B | 2 file |
| flatpickr JS + 2 CSS + locale | 12.989 + 2.610 + 2.577 + 470 B | 2 file |
| Font Awesome CSS (layout quản lý) | 18.778 B | 0 icon, chỉ lớp cỡ `fa-lg/2x/3x` |
| DataTables JS | 28.496 B | 19 file |
| SweetAlert2 | 18.357 B | nhiều |
| SignalR | 11.163 B | chuông thông báo |

File trong repo, không nén (`Program.cs` chỉ `UseStaticFiles()`): `tabler.min.css` 536.149 B, `tabler-icons.min.css` 203.697 B, `tabler-icons.woff2` 778.812 B, `jquery.min.js` 89.503 B, `tabler.min.js` 83.567 B. Widget AI `_AiChatWidget.cshtml` 22.053 B nhúng vào HTML mọi trang quản lý.
