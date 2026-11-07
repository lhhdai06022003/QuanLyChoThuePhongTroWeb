# Bảo mật xác thực

- **Mục tiêu:** bỏ `admin`/`admin`, ép đổi mật khẩu khi đang dùng mật khẩu mặc định (`admin`, số điện thoại của khách thuê, trùng tên đăng nhập), giới hạn dò mật khẩu ở màn đăng nhập, thêm màn đổi mật khẩu cho Admin và nhân viên, thống nhất chính sách mật khẩu.
- **Hiện trạng (06/11/2026):** đã code xong qua `/ship` (từ coder), sửa N1–N3 sau review, kiểm trên trình duyệt; xem [review.md](review.md). Reviewer: ĐẠT CÓ ĐIỀU KIỆN, không lỗi chặn. Code đã commit `a3c29fb` (07/11/2026), toàn bộ Web test PASS 799/799; chưa gộp `master`. Khối "Điền nhanh tài khoản mẫu" ở màn đăng nhập nay chỉ hiện ở `Development` (07/11/2026).
- **Nguồn:** đợt rà soát toàn bộ `master` ngày 05/11/2026 (build và bốn bộ test PASS; phát hiện hai lỗ hổng mức cao ở xác thực và các điểm trung bình liên quan).
- **Schema:** không đổi (ép đổi mật khẩu dựa vào nhận diện mật khẩu lúc đăng nhập, không cần cột mới).
- **Không đụng:** `Program.cs`, `.csproj`, `appsettings.json`, fixture và test kiến trúc.

## Việc người dùng cần làm sau khi code xong

- Nếu triển khai sau proxy: cân nhắc `LoginThrottle__MaxFailuresPerClient=0` (khóa theo IP không đáng tin khi chưa bật `UseForwardedHeaders`).
- Đặt `AdminSeed__Password` (biến môi trường, `.env` hoặc user-secrets) khi triển khai lên DB chưa có `admin`. DB phát triển hiện tại đã có `admin` nên không cần; admin sẽ bị ép đổi mật khẩu ở lần đăng nhập kế tiếp.

## Hạng mục

- [x] Rà soát và ghi nhận vấn đề (05/11/2026)
- [x] Spec v1
- [x] Spec được duyệt (06/11/2026: D3 phương án 2, D1, D2, D4–D8 theo đề xuất)
- [x] Plan v1
- [x] Plan được duyệt (06/11/2026, chạy `/ship` từ coder)
- [x] `AdminSeedOptions` và `SeedAdminAccountAsync` không còn mật khẩu mặc định (2.1)
- [x] Nhận diện mật khẩu cần đổi, bộ lọc ép đổi ở hai cổng (2.2)
- [x] Khách thuê giữ mật khẩu ban đầu là số điện thoại và bị ép đổi ở lần đăng nhập đầu (2.3, D3 đã chốt)
- [x] `PasswordPolicy` dùng chung (2.4)
- [x] Màn đổi mật khẩu cổng quản lý, banner ở Hồ sơ khách thuê (2.5)
- [x] Giới hạn đăng nhập (2.6)
- [x] `[Authorize]` cho hub (2.7)
- [x] Test các tầng, test kiến trúc cho bộ lọc ép đổi
- [x] Review, thử giao diện trên trình duyệt

## Ngoài phạm vi (xem spec mục 8)

Nâng gói NuGet, `docker-compose.yml` ở `Development`, cookie `Secure` và `UseForwardedHeaders`, thu hồi phiên khi đổi mật khẩu, header bảo mật, tên đăng nhập phân biệt hoa thường, giới hạn đăng ký khách (D7).
