# Review: Bảo mật xác thực

- **Ngày:** 06/11/2026. Nhánh `feature/bao-mat-xac-thuc` (từ `master` = `b39f0d0`); code commit `a3c29fb` (07/11/2026), đã gộp `master` ngày 07/11/2026 (merge `809772c`, xem mục 7).
- **Cách làm:** `/ship` bắt đầu từ coder (plan đã duyệt): coder (Opus) → tester → reviewer (Opus), bàn giao ở `.bangiao/`. Điều phối sửa thêm N1–N3 theo review và kiểm trên trình duyệt.
- **Phán quyết reviewer:** ĐẠT CÓ ĐIỀU KIỆN, không có lỗi chặn. Ba điểm nên sửa (N1–N3) đã sửa ở mục 3.
- **Schema:** không đổi (`has-pending-model-changes` PASS). Không đổi `Program.cs`, `.csproj`, `appsettings.json`, fixture, test kiến trúc cũ.

## 1. Đã làm (theo spec mục 2)

| Mục | Kết quả |
| --- | --- |
| 2.1 Seed admin | `AdminSeedOptions` (`AdminSeed:Password`); không còn mật khẩu mặc định; thiếu cấu hình thì không tạo và ghi Warning; bỏ nhánh đặt lại hash SHA256 về `admin` |
| 2.2 Ép đổi | `LoginAsync` gắn cờ khi mật khẩu là `admin`, trùng tên đăng nhập, hoặc (khách thuê) trùng số điện thoại; claim `BatBuocDoiMatKhau` + `YeuCauDoiMatKhauAttribute` trên mọi controller của hai cổng (test kiến trúc mới `ForcedPasswordChangeArchitectureTests`); trang thường chuyển hướng, AJAX nhận 403 JSON; đổi xong phát hành lại cookie, giữ "Ghi nhớ" |
| 2.3 Khách thuê (D3) | Giữ số điện thoại làm mật khẩu ban đầu; đăng nhập bằng số đó bị ép đổi ở Hồ sơ |
| 2.4 Chính sách | `PasswordPolicy` (8–128 ký tự, không trùng tên/số điện thoại, danh sách yếu) áp ở đổi mật khẩu, tạo/sửa tài khoản, seed admin, đăng ký khách vãng lai, `DoiMatKhauViewModel` |
| 2.5 Màn đổi mật khẩu | `GET/POST /QuanLyNhaTro/DoiMatKhau`, mục "Đổi mật khẩu" ở menu người dùng (chỉ Admin/NhanVien); banner và tự cuộn tới form ở `/KhachThue/HoSo?doiMatKhau=1` |
| 2.6 Giới hạn đăng nhập | `InMemoryLoginAttemptThrottle`: 5 lần sai theo tên, 20 theo IP, khóa 15 phút; lần thứ 6 bị từ chối kể cả mật khẩu đúng; tên không tồn tại vẫn băm giả và bị khóa cùng câu |
| 2.7 Hub | `[Authorize]` cho `ThongBaoHub`; `_Layout` chỉ nạp `thongbao.js` khi đã đăng nhập |

Lệch so với plan (hợp lý, ghi trong `.bangiao/thay-doi.md`): POST đổi mật khẩu cổng quản lý lỗi trả 400; số điện thoại chỉ so dạng chuẩn hóa khi mật khẩu trông như số; bộ đếm vượt trần bỏ mục không bị khóa trước; negotiate hub ẩn danh trả 401 khi có `X-Requested-With`, không có thì 302.

Sửa kèm để script soát giao diện đạt 0 LỖI: icon mặt trời ở `_Header` bỏ màu gõ cứng (`text-yellow`), 4 chỗ `font-weight-bold` ở `KhachThue/HoSo/Index.cshtml` thành `fw-bold`.

## 2. Kiểm chứng

| Lệnh hoặc thao tác | Kết quả | Ghi chú |
| --- | --- | --- |
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS | 0 lỗi |
| `Domain.UnitTests` | PASS 140/140 | |
| `Application.UnitTests` | 2758/2759 | 1 FAIL **có sẵn trên master**, không do module này: `BackgroundJobUseCaseTests.ExpiryAlert_AppFailure_DoesNotBlockEmail` (`ContractExpiryAlertUseCase.cs:69` dùng `DateTime.UtcNow`, test cố định ngày 05/11/2026) |
| `has-pending-model-changes` | PASS | |
| `Infrastructure.IntegrationTests` | PASS 719, 1 bỏ qua | Bỏ qua có sẵn: `OnnxMeterAccuracyEvaluationTests` thiếu `QLCTPT_METER_EVAL_DIR` |
| `Web.IntegrationTests` toàn bộ, chạy một mình | PASS 797/797 | Trước khi sửa N1–N3 |
| Web test liên quan sau khi sửa N1–N3 (`ForcedPasswordChange*`, `ManagerChangePassword`, `LoginThrottle`, `MeterReadingSubmissionTests`, `ViecCanLamEndpointTests`, `SignalRTests`) | PASS 63/63 | Chưa chạy lại toàn bộ Web sau lần sửa này |
| `soat-giao-dien.js` (view `DoiMatKhau`, `doi-mat-khau.js`, `_Header`, `_Layout`, `HoSo/Index`) | 0 LỖI | XEM LẠI về `ViewBag.Title` có sẵn |
| Sau khi ẩn khối tài khoản mẫu (07/11): `AuthenticationTests` (thêm `LoginPage_OutsideDevelopment_DoesNotRenderSampleAccounts`), `ForcedPasswordChange*`, `LoginThrottle`, `AdminScreenAuthorizationTests` | PASS 65/65 | Chưa chạy lại toàn bộ Web |
| `soat-giao-dien.js` `DangNhap.cshtml` | 0 LỖI | |
| Trước commit (07/11): build; `Application.UnitTests`; toàn bộ `Web.IntegrationTests` chạy một mình | Build PASS; Application 2758/2759 (1 FAIL có sẵn ở trên); **Web PASS 799/799** | Infrastructure không chạy lại (không đổi từ lần 719 PASS) |
| `git diff --check` | PASS | |
| Trình duyệt (preview `web-test-db`, DB `_test`) | PASS | Xem mục 4 |

## 3. Sửa sau review

- **N1:** thêm `ForcedPasswordChangeTests.Tenant_AfterAdminResetToPhone_LoginWithPhone_IsForced`: khách có mật khẩu riêng không bị ép → Admin gọi `POST /QuanLyNhaTro/NguoiDung/ResetPasswordToPhoneApi/{id}` → mật khẩu cũ bị từ chối → đăng nhập bằng số điện thoại bị chuyển tới `/KhachThue/HoSo?doiMatKhau=1`, vào `/KhachThue/HoaDon` cũng bị chuyển về. Luồng tạo tài khoản trong `HopDongService` vẫn chỉ được phủ gián tiếp (cùng phép băm số điện thoại).
- **N2:** `_Layout.cshtml` không nạp `viec-can-lam-badge.js` khi đang bị ép đổi (API đó trả 403). Test khẳng định trang đổi mật khẩu khi bị ép không chứa script này; `ViecCanLamEndpointTests` vẫn khẳng định trang thường có script.
- **N3:** `MeterReadingSubmissionTests.GetChotDienNuocPage_...` dọn tài khoản Admin trong `finally`.

## 4. Kiểm trên trình duyệt (DB `quanlyphongtro_test`)

Ảnh ở `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo\bao-mat-0[1-7]-*.jpg`.

- `admin`/`admin` (mật khẩu seed cũ còn trong DB test) đăng nhập → chuyển thẳng `/QuanLyNhaTro/DoiMatKhau`, có banner; console không lỗi; hub negotiate 200; không nạp badge việc cần làm (ảnh 01 sáng, 02 tối 375px, không cuộn ngang).
- Nhập `12345678` → "Mật khẩu quá dễ đoán" (ảnh 04). Đổi mật khẩu hợp lệ → về Bảng điều khiển, badge việc cần làm tải lại (200), menu người dùng có "Đổi mật khẩu".
- Admin đặt lại mật khẩu một khách thuê về số điện thoại → khách đăng nhập bằng số đó bị chuyển tới Hồ sơ có banner, trang tự cuộn tới form và đặt con trỏ vào "Mật khẩu hiện tại" (ảnh 03, 06); bấm "Hóa đơn" bị đưa về Hồ sơ; đổi xong về Tổng quan và mở được Hóa đơn.
- Chạy `Development` (07/11): màn đăng nhập vẫn có khối tài khoản mẫu, nút "Khách thuê mẫu" điền đúng, console không lỗi (ảnh 07). Môi trường khác: test Web khẳng định trang không có khối và không lộ chuỗi mật khẩu mẫu.
- Nhập sai 6 lần với một tên không tồn tại → "Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau 15 phút." (ảnh 05).
- Lưu ý: mật khẩu `admin` và tài khoản khách thử nghiệm `kh_d3b4c26a` trên **DB test** đã đổi sang hằng mật khẩu Admin/khách thuê có trong test của dự án. DB phát triển không bị đụng.
- CHƯA CHẠY: chế độ tối của cổng khách thuê (nút đổi giao diện bị ẩn ở 375px; cổng khách thuê không thuộc phạm vi bắt buộc của skill giao diện quản lý); `/Home/Privacy` ẩn danh không gọi hub (chỉ kiểm bằng đọc điều kiện trong `_Layout`).

## 5. Rủi ro còn lại (G1–G9 của reviewer)

- **G1** Khóa theo IP sau proxy/Docker (chưa bật `UseForwardedHeaders`): 20 lần sai cộng dồn của mọi người có thể khóa tất cả 15 phút. Tắt bằng `LoginThrottle__MaxFailuresPerClient=0`.
- **G2** Màn đổi mật khẩu không giới hạn số lần nhập sai mật khẩu hiện tại (người đã có cookie phiên dò được).
- **G3** Race nhỏ ở bộ đếm (`TryRemove` song song có thể mất một lần đếm); chấp nhận với một tiến trình.
- **G4** Tên đăng nhập đưa nguyên vào log Warning (có thể chèn xuống dòng vào console log).
- **G5** Nhân viên sửa số điện thoại hồ sơ trước lần đăng nhập đầu thì mật khẩu (số cũ) không còn bị nhận diện, khách không bị ép (giới hạn của D2).
- **G6** Người đang bị ép đổi vẫn gọi được `/api/ThongBao/*`, hub, `Phong/*`, `ReservationPayment/*` (ngoài hai cổng; plan đã chấp nhận).
- **G7** Thêm `?doiMatKhau=1` là banner hiện cả khi không bị ép (không ảnh hưởng an toàn).
- **G8** ~~Đăng ký khách vãng lai bị từ chối theo chính sách mới nhưng chỉ nhận câu chung "Thông tin đăng ký không hợp lệ."~~ Đã hết khi gộp nhánh của `thuan` (07/11/2026, mục 7): đăng ký khách nay trả đúng thông báo của `PasswordPolicy`.
- **G9** Test đỏ có sẵn ở `Application.UnitTests` (mục 2).
- **Mới phát hiện khi kiểm tay (đã xử lý 07/11/2026, người dùng chọn "chỉ hiện ở Development"):** màn đăng nhập có khối "Điền nhanh tài khoản mẫu" điền `admin`/`admin` và `khachthue01`/`123456`. Nay cả khối HTML lẫn đoạn JS chứa mật khẩu mẫu chỉ được render khi môi trường là `Development` (`DangNhap.cshtml`, `@inject IWebHostEnvironment`). Lưu ý: `docker-compose.yml` hiện đặt `ASPNETCORE_ENVIRONMENT=Development` (ngoài phạm vi, spec mục 8), nên chạy bằng Docker vẫn thấy khối này; triển khai thật cần đổi môi trường. Nút Admin vẫn điền `admin`/`admin`, sẽ sai sau khi admin đổi mật khẩu.

## 6. Bạn cần cung cấp

- `AdminSeed__Password` chỉ khi chạy trên DB chưa có `admin` (tối thiểu 8 ký tự, không dùng `admin`). DB phát triển hiện có `admin`/`admin` nên không cần; lần đăng nhập kế tiếp sẽ bị yêu cầu đổi.
- Triển khai sau proxy: cân nhắc `LoginThrottle__MaxFailuresPerClient=0` (G1).

## 7. Gộp vào `master` (07/11/2026)

- Gộp `feature/bao-mat-xac-thuc` vào `master` cùng nhánh `codex/khach-vang-lai-tiep-tuc` của `thuan` (6 commit, `d436c3c`…`e940d91`), commit merge `809772c`. Một xung đột nội dung ở `Application/Features/GuestAccounts/GuestRegistrationService.cs`: giữ cách kiểm từng trường của nhánh khách vãng lai, thay kiểm độ dài mật khẩu bằng `PasswordPolicy.Validate(request.Password, username, request.Phone?.Trim())` để chính sách mật khẩu vẫn áp cho đăng ký khách. `DependencyInjection.cs` tự gộp được.
- Kiểm sau gộp (một lượt `dotnet test` toàn solution, DB `quanlyphongtro_test`): build PASS 0 lỗi; Domain PASS 140/140; Application 2761/2762 (1 FAIL có sẵn ở mục 2); Web PASS 819/819; `has-pending-model-changes` PASS (không đổi schema). Infrastructure 718/720 trong lượt toàn solution do 1 FAIL chập chờn `AutoInvoiceChainTests.StaffManualClose_ThroughDienNuocService_CreatesDraftInvoice_EvenWhenTenantHasNoChannel` (chưa bắt được thông báo lỗi; xanh khi chạy riêng lớp 11/11 và khi chạy riêng project 719 PASS, 1 bỏ qua; cùng commit chạy lượt trước cũng xanh; nguyên nhân chưa rõ, diff không đụng service mà test gọi).
- Nhánh của `thuan` thêm hàm `CreateReservationSettlementStore` vào fixture `PostgreSqlFixture.cs`. Mục này chưa được duyệt riêng theo quy tắc "Giữ setup của `master`".
