# Spec: Bảo mật xác thực (mật khẩu mặc định, đổi mật khẩu, giới hạn đăng nhập)

- **Trạng thái:** v2, **người dùng đã duyệt 06/11/2026**: D3 giữ số điện thoại làm mật khẩu ban đầu của khách thuê, chỉ ép đổi (chốt 05/11); D1, D2, D4–D8 theo đề xuất ở mục 10. Plan: [plan.md](plan.md); chưa code.
- **Nhánh:** `feature/bao-mat-xac-thuc` (từ `master` = `b39f0d0`).
- **Nguồn:** kết quả rà soát toàn bộ `master` ngày 05/11/2026 (hai lỗ hổng mức cao ở xác thực và các điểm mức trung bình liên quan).
- **Phạm vi:** tầng Application (`Features/NguoiDungs`, `Common/Configurations`), Infrastructure (đăng ký DI, bind options), Web cổng quản lý (`Areas/QuanLyNhaTro`: đăng nhập, màn đổi mật khẩu, menu người dùng), cổng khách thuê (chỉ gắn bộ lọc ép đổi mật khẩu và banner ở trang Hồ sơ), `Hubs/ThongBaoHub`. Không đụng nghiệp vụ hóa đơn, hợp đồng, thanh toán.
- **Schema:** không đổi. Không thêm bảng, cột, index hay migration. Việc ép đổi mật khẩu dựa vào nhận diện mật khẩu lúc đăng nhập, không cần cột mới (mục 2.2).
- **Không đụng:** `Program.cs`, `.csproj`, `appsettings.json`, fixture test, test kiến trúc. Hai file mẫu `appsettings.example.json` và `.env.example` chỉ thêm khóa mới (D8 đã duyệt).

## 0. Vấn đề cần sửa (bằng chứng trong code)

| # | Hiện trạng | Vị trí | Mức |
| --- | --- | --- | --- |
| P1 | Khi chưa có tài khoản `admin`, khởi động tạo luôn `admin` / mật khẩu `admin`; không bắt đổi. Nhánh "nâng cấp" còn đặt lại mật khẩu về `admin` nếu hash là SHA256 cũ của `admin` | `NguoiDungService.SeedAdminAccountAsync` (`NguoiDungService.cs:34-60`), gọi từ `DatabaseInitializer.InitializeAsync` | Cao |
| P2 | Khách thuê được tự tạo khi lập hợp đồng có tên đăng nhập = email, mật khẩu = số điện thoại; nút "đặt lại" cũng đặt về số điện thoại; không bắt đổi. Biết email và số điện thoại là đăng nhập được | `HopDongService.cs:262-280`, `NguoiDungService.ResetPasswordToPhoneAsync` (`NguoiDungService.cs:267-280`) | Cao |
| P3 | Đăng nhập không giới hạn số lần thử, không khóa tạm; bộ giới hạn tần suất hiện chỉ áp cho trợ lý AI. Tên đăng nhập không tồn tại trả về nhanh hơn tên có thật (không băm), lộ thông tin qua thời gian phản hồi | `NguoiDungController.DangNhap` (POST), `NguoiDungService.ValidateUserAsync` (`NguoiDungService.cs:62-94`), `GuestAssistantRateLimiting.cs:26-31` | Trung bình |
| P4 | Cổng quản lý (Admin, nhân viên) **không có màn đổi mật khẩu**; `ChangePasswordAsync` chỉ được cổng khách thuê gọi. Admin muốn đổi mật khẩu phải tự sửa tài khoản mình ở màn quản lý tài khoản | `HoSoController.DoiMatKhau` là nơi duy nhất gọi `ChangePasswordAsync` | Trung bình |
| P5 | Không có chính sách mật khẩu thống nhất: khách vãng lai 8–128 ký tự, khách thuê đổi mật khẩu tối thiểu 6, tài khoản Admin tạo cho nhân viên và `UpdateUserAsync` không kiểm gì | `GuestRegistrationService.cs`, `DoiMatKhauViewModel.cs:11`, `NguoiDungService.AddAsync`/`UpdateUserAsync` | Trung bình |
| P6 | Hub `/thongBaoHub` nhận kết nối ẩn danh (`ThongBaoHub : Hub` không có `[Authorize]`). Người ẩn danh không nhận được dữ liệu nhưng vẫn giữ kết nối | `Hubs/ThongBaoHub.cs`, `SignalRTests.cs:20` đang khẳng định negotiate ẩn danh thành công | Thấp |

## 1. Mục tiêu và ngoài phạm vi

**Mục tiêu**

1. Không còn mật khẩu mặc định nào trong mã: chỉ tạo `admin` khi người vận hành cung cấp mật khẩu.
2. Tài khoản đang dùng mật khẩu mặc định hoặc mật khẩu do người khác cấp phải đổi mật khẩu trước khi dùng tiếp.
3. Mật khẩu ban đầu của khách thuê (số điện thoại, D3) chỉ dùng được để vào trang đổi mật khẩu, không dùng lâu dài.
4. Dò mật khẩu bằng cách thử liên tục bị chặn tạm thời.
5. Admin và nhân viên có màn tự đổi mật khẩu; mọi nơi đặt mật khẩu dùng chung một chính sách.

**Ngoài phạm vi (ghi nhận, làm sau nếu muốn; xem mục 8):** nâng gói NuGet, `docker-compose.yml` đặt `Development`, cookie `Secure` + `UseForwardedHeaders`, thu hồi phiên khi đổi mật khẩu (hai việc này cần sửa `Program.cs`), header bảo mật, tên đăng nhập không phân biệt hoa thường, giới hạn đăng ký khách (D7), màn đổi mật khẩu cho khách vãng lai.

## 2. Thiết kế

### 2.1 Tạo tài khoản `admin` lần đầu (sửa P1)

- Thêm `AdminSeedOptions` ở `Application/Common/Configurations` (đúng mẫu `SePayOptions`: `SectionName`, `Validate()`, bind và `Validate()` trong `AddInfrastructure`). Khóa cấu hình: `AdminSeed:Password` (biến môi trường `AdminSeed__Password`).
- `SeedAdminAccountAsync`:
  - Đã có tài khoản `admin` chưa xóa: **không làm gì**. Bỏ hẳn nhánh "nâng cấp SHA256 rồi đặt lại thành `admin`". Hash SHA256 cũ vẫn được nâng cấp khi chính người đó đăng nhập (đã có `needsRehash`).
  - Chưa có và `AdminSeed:Password` có giá trị hợp lệ theo chính sách (mục 2.4): tạo `admin` với mật khẩu đó. Người vận hành tự chọn nên không bị ép đổi; mật khẩu trùng một mật khẩu mặc định ở bảng 2.2 (ví dụ `admin`) bị `AdminSeedOptions.Validate()` từ chối ngay khi khởi động (mục 2.4).
  - Chưa có và không cấu hình: **không tạo**, ghi log mức **Warning** một dòng nói rõ cần đặt `AdminSeed__Password` rồi khởi động lại. Ứng dụng vẫn chạy. Không ghi mật khẩu vào log.
- Dùng mức Warning, không dùng Error, vì `CustomWebApplicationFactory` làm FAIL cả bộ test khi có log từ Error trở lên; bộ test chạy trên DB mới (chưa có `admin`) không được vì thế mà đỏ.
- Không có mật khẩu mặc định ở bất kỳ môi trường nào (kể cả `Development`).

### 2.2 Nhận diện mật khẩu cần đổi và ép đổi (sửa P1, P2, không đổi schema)

Sau khi mật khẩu đúng, `ValidateUserAsync` trả thêm `BatBuocDoiMatKhau = true` khi **một** trong các điều kiện sau đúng:

| Điều kiện | Bắt được trường hợp |
| --- | --- |
| Mật khẩu vừa nhập (không phân biệt hoa thường) bằng `admin` | `admin` / `admin` đang có trong DB hiện tại |
| Mật khẩu vừa nhập bằng tên đăng nhập | Tài khoản đặt mật khẩu trùng tên |
| Khách thuê: mật khẩu vừa nhập bằng số điện thoại của hồ sơ người thuê (so nguyên văn và so dạng chỉ-chữ-số, đổi đầu `84` thành `0`) | Mọi khách thuê còn dùng mật khẩu ban đầu là số điện thoại: tài khoản vừa tạo khi lập hợp đồng, vừa được đặt lại, hoặc đã tạo từ trước (D3) |

Quy tắc đi kèm:

- Nhận diện làm lại ở **mỗi lần đăng nhập** và không lưu trạng thái nào, nên người dùng bỏ trang đổi mật khẩu rồi đăng nhập lại vẫn bị ép cho tới khi thực sự đổi.
- Mật khẩu mới không được rơi vào điều kiện nào của bảng này (`PasswordPolicy`, mục 2.4), nên đổi xong sẽ không bị ép lại.
- **Không áp cho `KhachVangLai`**: họ chưa có màn đổi mật khẩu (mục 8), ép sẽ nhốt họ trong vòng lặp.
- Không dùng cột `LanDangNhapCuoi` để suy ra trạng thái: khoảng 40 file Web test đăng nhập bằng form với user vừa tạo qua service (cột này còn `null`), ép theo cột đó sẽ làm cả loạt test bị chuyển hướng.
- `NguoiDungController.DangNhap` (POST) gắn claim `BatBuocDoiMatKhau=1` vào cookie. Bộ lọc `YeuCauDoiMatKhauAttribute` chặn mọi yêu cầu có claim này, trừ: trang và API đổi mật khẩu, đăng xuất, tệp tĩnh.
  - Yêu cầu trang: chuyển tới màn đổi mật khẩu của cổng (quản lý: `/QuanLyNhaTro/DoiMatKhau`; khách thuê: `/KhachThue/HoSo?doiMatKhau=1`).
  - Yêu cầu AJAX (`X-Requested-With` hoặc `Accept: application/json`): trả 403 JSON `{ success: false, requireChangePassword: true, redirectUrl }`.
- Đổi mật khẩu thành công: phát hành lại cookie **không còn** claim, giữ nguyên `IsPersistent`.
- Gắn bộ lọc bằng thuộc tính: ở `AdminBaseController` (kế thừa cho mọi controller quản lý) và cho từng controller của `Areas/KhachThue` hoặc một lớp cha chung (plan chọn). Có test kiến trúc đảm bảo không controller nào của hai cổng bị sót. Không cần sửa `Program.cs`.
- Phiên đăng nhập đã có trước khi triển khai (cookie 30 ngày) không có claim nên chưa bị ép cho tới lần đăng nhập sau. Chấp nhận trong v1; xem mục 7.

### 2.3 Mật khẩu ban đầu của khách thuê (sửa P2; D3 đã chốt: giữ số điện thoại)

- Giữ nguyên cách cấp: tài khoản khách thuê tạo khi lập hợp đồng (`HopDongService`) có tên đăng nhập là email, mật khẩu là số điện thoại; nút đặt lại mật khẩu vẫn đặt về số điện thoại (`ResetPasswordToPhoneAsync`; tên hàm, đường dẫn `ResetPasswordToPhoneApi` và chữ trên nút không đổi).
- Thay đổi duy nhất ở luồng này: khách đăng nhập bằng đúng mật khẩu đó bị ép đổi (dòng cuối bảng 2.2), nên số điện thoại chỉ dùng được để vào trang đổi mật khẩu. Không có mật khẩu tạm ngẫu nhiên, không có hộp thoại hiện mật khẩu, không đổi kết quả trả về của `HopDongService`.
- **Khoảng hở còn lại, đã được chấp nhận khi chốt D3:** từ lúc tài khoản được tạo hoặc đặt lại đến lúc khách đăng nhập lần đầu, ai biết email và số điện thoại đều đăng nhập được. Người đó bị ép đổi nhưng tự đặt mật khẩu mới và chiếm tài khoản; khách thật không vào được cho tới khi nhân viên đặt lại. Bộ giới hạn đăng nhập (2.6) không chặn việc này vì kẻ đó dùng đúng mật khẩu. Giảm nhẹ bằng quy trình: nhân viên hướng dẫn khách đăng nhập và đổi mật khẩu ngay sau khi lập hợp đồng.
- Khách thuê cũ (mật khẩu đang là số điện thoại) không cần migrate dữ liệu: điều kiện cuối bảng 2.2 bắt họ đổi ở lần đăng nhập kế tiếp.

### 2.4 Chính sách mật khẩu dùng chung (sửa P5)

`PasswordPolicy` (Application), một hàm `Validate(matKhau, tenDangNhap, soDienThoai?)` trả lỗi tiếng Việt hoặc `null`:

- Dài 8–128 ký tự; không toàn khoảng trắng.
- Không trùng tên đăng nhập; không trùng số điện thoại (hai dạng như bảng 2.2).
- Không nằm trong danh sách yếu ngắn: `admin`, `password`, `matkhau`, `12345678`, `123456789`, `1234567890`, `11111111`, `00000000`, `qwertyui`, `abcd1234` (plan chốt danh sách cuối).
- Không bắt chữ hoa, chữ số hay ký tự đặc biệt.
- Áp tại: `ChangePasswordAsync` (kèm "mật khẩu mới phải khác mật khẩu cũ"), `AddAsync`/`UpdateUserAsync` khi có nhập mật khẩu, `AdminSeedOptions.Validate()`, `GuestRegistrationService` (thay phép kiểm độ dài rải rác). `DoiMatKhauViewModel` đổi `MinLength(6)` thành 8 và để service quyết định phần còn lại.

### 2.5 Màn đổi mật khẩu cổng quản lý (sửa P4)

- Menu người dùng ở header (`Views/Shared/_Header.cshtml`, dùng chung với cổng khách thuê nên mục mới chỉ hiện ở cổng quản lý) thêm "Đổi mật khẩu" → `GET /QuanLyNhaTro/DoiMatKhau`, `POST /QuanLyNhaTro/DoiMatKhau` (đã được `[AutoValidateAntiforgeryToken]` của `AdminBaseController` phủ).
- Form: mật khẩu hiện tại, mật khẩu mới, xác nhận; gợi ý chính sách ngay dưới ô mới. Khi vào từ ép đổi, banner: "Mật khẩu hiện tại là mật khẩu mặc định. Bạn cần đặt mật khẩu mới trước khi tiếp tục."
- Trang này và API của nó nằm trong danh sách ngoại lệ của bộ lọc 2.2.
- Cổng khách thuê dùng lại form ở `HoSo/Index.cshtml`: khi có `?doiMatKhau=1` hiện banner như trên và đưa con trỏ vào ô mật khẩu hiện tại. Không làm màn mới.
- **Giao diện cổng quản lý bắt buộc** theo skill `giao-dien-quan-ly` (`references/mau-trang.md`), chạy `node .agents/skills/giao-dien-quan-ly/scripts/soat-giao-dien.js` trên file đã sửa, xem trên trình duyệt (sáng, tối, 375px).

### 2.6 Giới hạn đăng nhập (sửa P3)

- Interface `ILoginAttemptThrottle` ở `Application/Abstractions/Security`; cài đặt trong bộ nhớ (`ConcurrentDictionary` + `TimeProvider`) ở Application, đăng ký singleton trong `AddApplication`. Tham số trong `LoginThrottleOptions` (section `LoginThrottle`), có giá trị mặc định trong code nên không cấu hình vẫn chạy.
- Hai bộ đếm **chỉ tính lần sai**:

  | Khóa | Mặc định | Ghi chú |
  | --- | --- | --- |
  | Tên đăng nhập (đã `Trim`, `ToLowerInvariant`, cắt 100 ký tự) | 5 lần sai / 15 phút → khóa 15 phút | Tính cả tên không tồn tại, để không lộ tên nào có thật |
  | Địa chỉ máy khách | 20 lần sai / 15 phút → khóa 15 phút | **Bỏ qua khi không biết IP** (`RemoteIpAddress == null`, như TestServer). Đặt `0` là tắt |

- Trong lúc khóa: từ chối ngay, **không kiểm mật khẩu**, kể cả mật khẩu đúng. Thông báo chung: "Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau {n} phút." Đăng nhập thành công xóa bộ đếm của tên đăng nhập đó (không xóa bộ đếm IP).
- Tên đăng nhập không tồn tại vẫn chạy một phép băm giả để thời gian phản hồi gần bằng tên có thật.
- Bộ nhớ có trần (ví dụ 10.000 khóa), dọn mục hết hạn theo lượt truy cập, để chuỗi tên ngẫu nhiên không làm phình bộ nhớ.
- Mỗi lần khóa ghi log Warning (tên đăng nhập đã cắt, không ghi mật khẩu).
- Thêm `LoginAsync(tenDangNhap, matKhau, clientKey)` ở `INguoiDungService` trả kết quả có lý do (`Sai`, `TamKhoa` kèm số phút còn lại, `ThanhCong`); controller chỉ dịch kết quả sang HTTP.

### 2.7 Hub SignalR (sửa P6)

- Thêm `[Authorize]` cho `ThongBaoHub`. Plan phải xác nhận mọi trang nối hub (`thongbao.js`, `guest-dat-coc.js`) đều là trang đã đăng nhập; trang công khai không nối hub.
- Sửa `SignalRTests`: negotiate ẩn danh trả 401; có cookie đăng nhập trả 200.

## 3. Cấu hình mới và việc người dùng cần làm

| Khóa | Bắt buộc | Mặc định | Ghi chú |
| --- | --- | --- | --- |
| `AdminSeed:Password` (`AdminSeed__Password`) | Chỉ khi DB chưa có `admin` | không có | Đặt qua biến môi trường, `.env` (Docker) hoặc user-secrets. **Tôi không sửa `appsettings.json`, không đặt giá trị này; người dùng tự cung cấp** |
| `LoginThrottle:MaxFailuresPerUsername` | Không | 5 | |
| `LoginThrottle:MaxFailuresPerClient` | Không | 20 (`0` = tắt) | |
| `LoginThrottle:WindowMinutes` / `LockMinutes` | Không | 15 / 15 | |

DB phát triển hiện tại đã có `admin` (mật khẩu `admin`), nên **không cần** đặt `AdminSeed:Password` để chạy; ở lần đăng nhập kế tiếp admin sẽ bị ép đổi mật khẩu.

## 4. Tương thích dữ liệu hiện có

| Dữ liệu | Hành vi sau thay đổi |
| --- | --- |
| `admin` / `admin` đang có | Đăng nhập được, bị ép đổi ngay (điều kiện "mật khẩu = admin") |
| Khách thuê có mật khẩu = số điện thoại | Đăng nhập được, bị ép đổi (điều kiện số điện thoại) |
| Tài khoản nhân viên do Admin tạo qua màn quản lý tài khoản | Không bị ép (Admin chủ động đặt mật khẩu và phải theo chính sách 2.4) |
| Khách vãng lai tự đăng ký | Không đổi; không bị ép |
| Hash SHA256 cũ | Vẫn đăng nhập được, tự nâng cấp lên PBKDF2 như hiện nay |
| Phiên (cookie) đang sống | Không bị ép cho tới lần đăng nhập sau (mục 7) |

## 5. Chạm vào file được bảo vệ

Trong thiết kế này **không** đổi `Program.cs`, `.csproj`, `appsettings.json`, fixture test hay test kiến trúc. Duy nhất cần duyệt (D8): thêm khóa `AdminSeed`, `LoginThrottle` có chú thích vào `appsettings.example.json` và `AdminSeed__Password=` vào `.env.example`. Test `MeterReadingSubmissionTests` (dòng 470) hiện đăng nhập bằng `admin`/`admin` của bản seed; sửa test đó để tự tạo một tài khoản Admin bằng service như các test khác, không sửa fixture.

## 6. Kế hoạch test

| Tầng | Nội dung |
| --- | --- |
| `Application.UnitTests` | `PasswordPolicy` (độ dài, trùng tên, trùng số điện thoại hai dạng, danh sách yếu); nhận diện mật khẩu mặc định (từng dòng bảng 2.2, khách vãng lai không bị ép); `LoginAsync` đặt cờ ép đổi đúng theo từng dòng bảng 2.2 và vẫn ghi `LanDangNhapCuoi`; `ChangePasswordAsync` từ chối mật khẩu trùng mật khẩu cũ; `SeedAdminAccountAsync` (không tạo khi thiếu mật khẩu, tạo khi có, không đụng `admin` có sẵn, không còn nhánh đặt lại thành `admin`); `ILoginAttemptThrottle` với `FixedTimeProvider` (khóa sau lần sai thứ 5, mở sau 15 phút, thành công xóa đếm, không tính IP khi `null`, trần bộ nhớ) |
| `Infrastructure.IntegrationTests` | Tạo `admin` qua `DatabaseInitializer` với và không có `AdminSeed:Password` trên DB test |
| `Web.IntegrationTests` | Ép đổi: tài khoản bị cờ vào trang quản lý/khách thuê bị chuyển hướng, AJAX nhận 403 JSON, trang đổi mật khẩu và đăng xuất vẫn vào được, đổi xong hết bị ép; màn `/QuanLyNhaTro/DoiMatKhau` (đúng, sai mật khẩu cũ, vi phạm chính sách, chưa đăng nhập bị chuyển về đăng nhập); khóa sau 5 lần sai và mật khẩu đúng cũng bị từ chối trong lúc khóa; hub 401/200; **test kiến trúc**: mọi controller của `Areas/QuanLyNhaTro` và `Areas/KhachThue` có bộ lọc ép đổi (trừ danh sách ngoại lệ ghi lý do); sửa `MeterReadingSubmissionTests` như mục 5 |
| Kiểm tay trên preview | Admin cũ bị ép đổi; tạo hợp đồng cho khách có email mới, khách đó đăng nhập bằng số điện thoại thì bị ép đổi; sai 5 lần thì bị khóa; ảnh chụp theo skill giao diện |

Lưu ý test: 40 file Web test đăng nhập qua form; ngoài `MeterReadingSubmissionTests` các test đó phải qua mà không sửa. Test nào cố ý nhập sai mật khẩu nhiều lần với cùng một tên đăng nhập (ví dụ `AuthenticationTests`, `SessionRevalidationTests`) có thể chạm ngưỡng khóa; khi đó đặt `LoginThrottle:MaxFailuresPerUsername` lớn qua `WithWebHostBuilder`/`UseSetting` hoặc dùng tên đăng nhập khác nhau, không sửa fixture. TestServer không có IP nên bộ đếm theo IP không chạy trong test.

Chạy tuần tự (không chạy hai lượt `dotnet test` song song) và `has-pending-model-changes` phải PASS.

## 7. Rủi ro và đánh đổi

- **Khóa theo tên đăng nhập** cho phép kẻ xấu cố ý nhập sai để khóa tài khoản người khác 15 phút (kể cả `admin`). Chấp nhận vì thời gian ngắn, không khóa vĩnh viễn, và có log Warning; đổi lấy việc chặn dò mật khẩu.
- **Bộ đếm trong bộ nhớ** mất khi khởi động lại và không dùng chung giữa nhiều tiến trình. Phù hợp triển khai một tiến trình hiện tại (Docker một container); chạy nhiều bản sao cần lưu ở nơi dùng chung (ngoài phạm vi).
- **Địa chỉ IP không đáng tin** sau proxy hoặc Docker (chưa bật `UseForwardedHeaders`): nhiều người có thể bị thấy chung một IP, nên mức theo IP để cao (20) và tắt được. Mức theo tên đăng nhập là lớp chính.
- **Mật khẩu ban đầu là số điện thoại (D3)**: có khoảng hở từ lúc cấp đến lúc khách đăng nhập lần đầu, ai biết email và số điện thoại có thể vào trước và chiếm tài khoản (mục 2.3). Muốn thu hẹp cần mật khẩu ngẫu nhiên hiện cho nhân viên hoặc gửi qua email; hai phương án đó đã bị loại khi chốt D3, có thể mở lại sau.
- **Phiên cũ** (cookie sống 30 ngày) không bị ép cho tới lần đăng nhập sau, và đổi mật khẩu chưa thu hồi phiên khác. Thu hồi phiên cần đổi `Program.cs` (`OnValidatePrincipal`), nên tách thành việc sau và phải được duyệt riêng.
- **Test hiện có** có thể đặt mật khẩu ngắn hơn 8 ký tự qua service; plan phải tìm và sửa dữ liệu test đó thay vì nới chính sách.
- **Nhận diện theo nội dung mật khẩu** (`admin`, trùng tên đăng nhập, trùng số điện thoại) không phải trạng thái lưu trong DB: chỉ biết được lúc đăng nhập, và nhân viên do Admin tạo với mật khẩu "đẹp" thì không bị ép. Muốn ép chắc chắn mọi tài khoản do Admin cấp thì cần cột cờ (D2 phương án 2).

## 8. Ngoài phạm vi, ghi nhận từ đợt rà soát

| Việc | Lý do để riêng |
| --- | --- |
| Nâng gói có cảnh báo High (ImageSharp, `System.Text.Json`, `Caching.Memory`, `Pkcs`) | AGENTS.md cấm nâng gói khi chưa hỏi; ImageSharp 3.1.12 cần đối chiếu từng cảnh báo |
| `docker-compose.yml` đặt `ASPNETCORE_ENVIRONMENT=Development` (dòng 45) và mở cổng DB 5433 (dòng 15) | Là cấu hình triển khai, ảnh hưởng cách chạy demo |
| Cookie `SecurePolicy` + `UseForwardedHeaders`, thu hồi phiên khi đổi mật khẩu | Phải sửa `Program.cs`, cần duyệt riêng |
| Header bảo mật (CSP, `X-Frame-Options`, `nosniff`) | Phải sửa pipeline ở `Program.cs` |
| Tên đăng nhập phân biệt hoa thường, `NguoiDung` không unique, hai người thuê cùng email | Cần thiết kế dữ liệu, có thể cần migration |
| `Html.Raw` điều khoản mẫu, `DateTimeJsonConverter` đọc/ghi lệch, 79 chỗ `AddHours(7)` | Không thuộc xác thực |

## 9. Tiêu chí hoàn thành

- Tìm trong `src` không còn `HashPassword("admin")`. Chỗ còn đặt mật khẩu bằng số điện thoại chỉ là `HopDongService` và `ResetPasswordToPhoneAsync` (D3), và cả hai đều đi qua bước ép đổi ở 2.2 (có test).
- Mọi dòng bảng 2.2 có test; ép đổi, khóa đăng nhập, hub, màn đổi mật khẩu có test Web.
- `dotnet build`, bốn bộ test và `has-pending-model-changes` PASS; `git diff --check` sạch.
- Màn mới qua script soát của skill giao diện (0 LỖI) và đã xem trên trình duyệt (sáng, tối, 375px), hoặc ghi CHƯA CHẠY.
- Tài liệu: README module, `review.md`, dòng danh mục `docs/features/README.md`, mục Hiện trạng trong `AGENTS.md`, báo cáo hằng ngày.

## 10. Quyết định (đã chốt đủ 06/11/2026: D3 như ghi trong bảng, các D còn lại theo cột "Đề xuất")

| # | Câu hỏi | Đề xuất | Phương án khác |
| --- | --- | --- | --- |
| D1 | Cách có `admin` lần đầu | Bắt buộc đặt `AdminSeed:Password`; thiếu thì không tạo và ghi Warning (mục 2.1) | Giữ `admin`/`admin` ở `Development` (vẫn lộ khi chạy Docker vì compose đặt `Development`); sinh mật khẩu ngẫu nhiên và in ra log (lộ bí mật trong log) |
| D2 | Cách ép đổi mật khẩu | Không đổi schema: nhận diện lúc đăng nhập theo `admin`, trùng tên đăng nhập, trùng số điện thoại (mục 2.2) | Thêm cột `nguoi_dung.BatBuocDoiMatKhau`, đặt cờ khi cấp hoặc cấp lại mật khẩu: rõ ràng hơn và ép được cả tài khoản Admin tạo, nhưng cần migration review và thử trên DB test (test hiện có không bị ảnh hưởng vì cờ mặc định false). Không dùng `LanDangNhapCuoi` vì làm vỡ khoảng 40 file test |
| D3 | Mật khẩu ban đầu của khách thuê | **Đã chốt 05/11/2026: giữ số điện thoại**, khách bị ép đổi ở lần đăng nhập đầu (mục 2.3); khoảng hở còn lại ghi ở 2.3 và mục 7 | Đã loại: (1) ngẫu nhiên, hiện một lần cho nhân viên; (3) ngẫu nhiên gửi qua email |
| D4 | Chính sách mật khẩu | 8–128 ký tự, không trùng tên đăng nhập hoặc số điện thoại, không thuộc danh sách yếu, không bắt ký tự đặc biệt (mục 2.4) | Bắt thêm chữ hoa, chữ số, ký tự đặc biệt (khó nhớ hơn, người dùng dễ viết ra giấy) |
| D5 | Ngưỡng giới hạn đăng nhập | 5 lần sai theo tên đăng nhập, 20 lần sai theo IP, cửa sổ và thời gian khóa 15 phút (mục 2.6) | Ngưỡng chặt hơn (3 lần) hoặc khóa lâu hơn; chỉ theo tên đăng nhập |
| D6 | Vị trí màn đổi mật khẩu quản lý | Mục "Đổi mật khẩu" ở menu người dùng trên header (mục 2.5) | Đặt trong trang Hồ sơ riêng (chưa có trang này ở cổng quản lý) |
| D7 | Giới hạn đăng ký khách `/dang-ky-khach` | **Để sau**: thuộc cổng khách vãng lai (người phụ trách `thuan`), hướng 04/11/2026 là chưa mở việc mới ở đó | Làm luôn trong đợt này: 5 lượt/giờ mỗi IP, 60 lượt/giờ toàn hệ thống, theo mẫu `GuestAssistantRateLimiting` |
| D8 | Thêm khóa mới vào `appsettings.example.json` và `.env.example` | Được phép, chỉ thêm khóa mới có chú thích (mục 5) | Không sửa hai file; ghi khóa mới chỉ trong README module |
