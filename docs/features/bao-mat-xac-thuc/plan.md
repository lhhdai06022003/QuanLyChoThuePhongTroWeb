# Plan: Bảo mật xác thực

- **Trạng thái:** v1 (06/11/2026), **người dùng đã duyệt 06/11/2026** (giao cho `/ship` bắt đầu từ coder, bàn giao ở `.bangiao/ke-hoach.md`). Code commit `a3c29fb` (07/11/2026).
- **Spec:** [spec.md](spec.md) v2, đã duyệt đủ D1–D8 (D3: giữ số điện thoại; D1, D2, D4–D8 theo đề xuất).
- **Nhánh:** `feature/bao-mat-xac-thuc` (từ `master` = `b39f0d0`). Commit từng task khi người dùng bảo.
- **Schema:** không đổi. Sau mỗi task chạm Infrastructure chạy `has-pending-model-changes`, phải PASS.

## 0. Ràng buộc

- Không sửa `Program.cs`, `.csproj`, `appsettings.json`, `.gitignore`, fixture test (`CustomWebApplicationFactory`, `WebTestCollection`, `BranchScopeKit`...) và các test kiến trúc đang có. Được **thêm** một test kiến trúc mới cho bộ lọc ép đổi (spec mục 6). Được thêm khóa mẫu vào `appsettings.example.json` và `.env.example` (D8).
- Application không dùng ASP.NET Core, EF Core, `IConfiguration`; trả `ServiceResult`. Web không tham chiếu Domain.
- Log khi thiếu `AdminSeed:Password` và khi khóa đăng nhập chỉ ở mức **Warning**: factory test FAIL khi có log từ Error trở lên.
- Không chạy hai lượt `dotnet test` song song; test tích hợp chỉ chạy trên DB đuôi `_test` (skill `chay-test-tich-hop`), thiếu biến thì ghi CHƯA CHẠY.
- Giao diện (task 7, 8): đọc skill `giao-dien-quan-ly` trước khi viết, chạy `soat-giao-dien.js` trên file đã sửa, xem sáng/tối/375px, ảnh lưu `E:\UTE\TieuLuanChuyenNganh\hinhanhprojectdemo`.

## 1. Bản đồ code (đã đối chiếu 06/11/2026)

| Chỗ | Hiện trạng liên quan |
| --- | --- |
| `Application/Features/NguoiDungs/Services/NguoiDungService.cs` | Constructor `(INguoiDungStore, IUnitOfWork, IPasswordService)`. `SeedAdminAccountAsync` (34–60) băm `"admin"`, nhánh SHA256 đặt lại về `"admin"`. `ValidateUserAsync` (62–94). `AddAsync` (132–207) và `UpdateUserAsync` (209–245) chỉ băm khi có mật khẩu, không kiểm chính sách. `ChangePasswordAsync` (247–265) không kiểm chính sách. `ResetPasswordToPhoneAsync` (267–280) giữ nguyên (D3). Không test nào `new NguoiDungService(`, chỉ lấy qua DI |
| `INguoiDungService.cs` | `Task SeedAdminAccountAsync()`, `Task<UserAuthDto?> ValidateUserAsync(...)`. Người gọi duy nhất: `DatabaseInitializer.InitializeAsync` và `NguoiDungController.DangNhap` (POST, dòng 74) |
| `Infrastructure/Persistence/Features/NguoiDungStore.cs:21` | `GetActiveByUsernameAsync` chỉ `Include(KhachVangLai)`, **không** có `NguoiThue` nên lúc đăng nhập chưa biết số điện thoại khách thuê |
| `Application/Features/HopDongs/Services/HopDongService.cs:268-277` | Tạo `NguoiDung` khách thuê trực tiếp (không qua `AddAsync`), mật khẩu = số điện thoại. Giữ nguyên (D3) |
| `Application/Features/GuestAccounts/GuestRegistrationService.cs:19` | Kiểm `Password.Length is < 8 or > 128` |
| `Infrastructure/Persistence/DatabaseInitializer.cs` | `MigrateAsync` rồi `SeedAdminAccountAsync`, có `ILogger` |
| `Infrastructure/DependencyInjection.cs:91` | Mẫu bind options: `GetSection(X.SectionName).Get<X>() ?? new X(); x.Validate(); services.AddSingleton(x);` |
| `Program.cs:17-18` | `AddInfrastructure` chạy **trước** `AddApplication`, nên `TryAddSingleton` trong `AddApplication` không ghi đè options đã bind |
| `Program.cs:55-80` | `OnValidatePrincipal` chỉ kiểm phiên, không dựng lại principal: claim mới trong cookie được giữ nguyên |
| `Web/Areas/QuanLyNhaTro/Controllers/AdminBaseController` | Mọi controller quản lý kế thừa, **trừ** `ThongBaoController` (kế thừa `Controller`, có `[Area]` và `[Authorize(Roles="Admin,NhanVien")]`) |
| `Web/Areas/KhachThue/Controllers/*.cs` | 10 controller kế thừa `Controller`, không có lớp cha chung |
| `NguoiDungController` | `DangNhap` GET/POST `[AllowAnonymous]`, claim `NameIdentifier`, `Name`, `Role`, `NguoiThueId`; `IsPersistent = req.GhiNho`; lỗi qua `ViewBag.Error`; chuyển hướng theo vai trò và `returnUrl` |
| `Web/Areas/KhachThue/Controllers/HoSoController.cs` | `DoiMatKhau([FromBody] DoiMatKhauViewModel)` + `[ValidateAntiForgeryToken]`, trả `Json(result)`. Form ở `Areas/KhachThue/Views/HoSo/Index.cshtml:72-210` |
| `Views/Shared/_Layout.cshtml:196-198` | Nạp `signalr.min.js` và `thongbao.js` (nối hub) **cho mọi người**. Layout này dùng cho cả `Home/Privacy` và trang lỗi (ẩn danh) |
| `wwwroot/js/guest-dat-coc.js:115` | Nối hub ở `Phong/DatCoc`, `Phong/DatCocVnPayKetQua`: cả hai action `[Authorize(Roles="KhachVangLai,KhachThue")]` |
| `Web/Controllers/ThongBaoController.cs` | `[Authorize]` cho mọi vai trò, phục vụ chuông thông báo; không gắn bộ lọc ép đổi (không lộ dữ liệu ngoài thông báo của chính người đó) |
| Mật khẩu trong test | Mọi hằng mật khẩu test dài ≥ 12 ký tự (`Password123!`, `StaffPassword123!`...), không trùng tên đăng nhập hay danh sách yếu: chính sách mới không làm vỡ dữ liệu test. `MatKhauHash = "hash"` chỉ gán thẳng vào entity, không qua service |
| Test nhập sai | `AuthenticationTests` (`non_existent_user`), `AdminScreenAuthorizationTests` (`khong_ton_tai`): mỗi tên một lần, dưới ngưỡng 5 |
| `MeterReadingSubmissionTests.cs:470` | Đăng nhập bằng `admin`/`admin` của bản seed: phải sửa |
| `SignalRTests.cs:20` | Khẳng định negotiate ẩn danh trả 200: phải sửa |

## 2. Thiết kế chốt cho code

### Kiểu mới

| Tên | Nơi | Nội dung |
| --- | --- | --- |
| `PasswordPolicy` (static) | `Application/Common/Security/PasswordPolicy.cs` | `string? Validate(string? matKhau, string? tenDangNhap, string? soDienThoai)` trả lỗi tiếng Việt hoặc `null`; `bool LaMatKhauMacDinh(string matKhau, string tenDangNhap, string? soDienThoai)` (bảng 2.2); `string? ChuanHoaSoDienThoai(string?)` (chỉ giữ chữ số, đầu `84` đổi thành `0`). Danh sách yếu chốt: `admin`, `password`, `matkhau`, `12345678`, `123456789`, `1234567890`, `11111111`, `00000000`, `qwertyui`, `abcd1234` (so không phân biệt hoa thường) |
| `AdminSeedOptions` | `Application/Common/Configurations/AdminSeedOptions.cs` | `SectionName = "AdminSeed"`, `string? Password`. `Validate()`: rỗng là hợp lệ (không seed); có giá trị thì phải qua `PasswordPolicy.Validate(pw, "admin", null)`, sai thì ném `InvalidOperationException` nêu khóa `AdminSeed:Password` (không in mật khẩu) |
| `LoginThrottleOptions` | `Application/Common/Configurations/LoginThrottleOptions.cs` | `SectionName = "LoginThrottle"`; `MaxFailuresPerUsername = 5`, `MaxFailuresPerClient = 20`, `WindowMinutes = 15`, `LockMinutes = 15`, `MaxTrackedKeys = 10_000`. `Validate()`: username ≥ 1, client ≥ 0, phút ≥ 1, trần ≥ 100 |
| `ILoginAttemptThrottle` | `Application/Abstractions/Security/ILoginAttemptThrottle.cs` | `TimeSpan? GetLockRemaining(string tenDangNhap, string? clientKey)`; `void RecordFailure(string tenDangNhap, string? clientKey)`; `void RecordSuccess(string tenDangNhap)` |
| `InMemoryLoginAttemptThrottle` | `Application/Features/NguoiDungs/Services/InMemoryLoginAttemptThrottle.cs` | `ConcurrentDictionary<string, Entry>` với khóa `u:<ten>` và `ip:<key>`; tên `Trim().ToLowerInvariant()` cắt 100 ký tự; cửa sổ trượt theo lần sai đầu; vượt trần thì dọn mục hết hạn, vẫn đầy thì bỏ mục cũ nhất. `clientKey` null/rỗng hoặc `MaxFailuresPerClient = 0` thì bỏ qua IP. Log Warning `"Tạm khóa đăng nhập cho {TenDangNhap} trong {Phut} phút"` khi chạm ngưỡng |
| `LoginResult`, `LoginStatus` | `Application/Features/NguoiDungs/DTOs/` | `LoginStatus { Sai, TamKhoa, ThanhCong }`; `LoginResult { Status, UserAuthDto? User, int SoPhutCho }` |
| `SeedAdminResult` | `Application/Features/NguoiDungs/DTOs/` | `{ DaCo, DaTao, ThieuMatKhau }` |
| `UserAuthDto.BatBuocDoiMatKhau` | DTO có sẵn | thêm `bool` |
| `ForcedPasswordChange` (static) | `Web/Helpers/ForcedPasswordChange.cs` | `const string ClaimType = "BatBuocDoiMatKhau"`; `bool IsRequired(ClaimsPrincipal)`; `string ChangeUrl(ClaimsPrincipal)` (quản lý `/QuanLyNhaTro/DoiMatKhau`, khách thuê `/KhachThue/HoSo?doiMatKhau=1`); `Task ReissueWithoutClaimAsync(HttpContext)` (đọc `AuthenticateAsync`, bỏ claim, `SignInAsync` lại với `IsPersistent` cũ) |
| `YeuCauDoiMatKhauAttribute` | `Web/Filters/YeuCauDoiMatKhauAttribute.cs` | `Attribute, IAuthorizationFilter`; bỏ qua khi người dùng chưa đăng nhập, không có claim, hoặc action/controller có `[BoQuaYeuCauDoiMatKhau]`; còn lại: AJAX (`X-Requested-With: XMLHttpRequest` hoặc `Accept` chứa `application/json`) trả 403 `{ success=false, requireChangePassword=true, redirectUrl }`, trang thường `Redirect(ChangeUrl)` |
| `BoQuaYeuCauDoiMatKhauAttribute` | cùng file | đánh dấu ngoại lệ |

### Luồng đăng nhập mới (`INguoiDungService.LoginAsync(tenDangNhap, matKhau, clientKey)`)

1. `GetLockRemaining` khác null → `TamKhoa` kèm số phút làm tròn lên, **không** kiểm mật khẩu.
2. Tìm user (store có `Include(NguoiThue)`). Không có hoặc `!IsActive` → chạy `VerifyPassword(dummyHash, ...)` để thời gian gần bằng, `RecordFailure`, trả `Sai`. `dummyHash` sinh một lần bằng `_passwordService.HashPassword(<chuỗi cố định>)` và lưu tĩnh.
3. Sai mật khẩu → `RecordFailure`, `Sai`.
4. Đúng → `RecordSuccess`, cập nhật `LanDangNhapCuoi`, rehash nếu cần (như cũ), `BatBuocDoiMatKhau = role != KhachVangLai && PasswordPolicy.LaMatKhauMacDinh(matKhau, user.TenDangNhap, role == KhachThue ? user.NguoiThue?.SoDienThoai : null)`; `ThanhCong`.

`ValidateUserAsync` giữ lại (gọi `LoginAsync` với `clientKey = null`, trả `User` khi thành công) để không đổi hợp đồng interface với nơi khác; controller chuyển sang `LoginAsync`.

## 3. Các task

Mỗi task: viết test trước (đỏ) → code → test xanh. Model gợi ý theo `AGENTS.md`.

### Task 1: `PasswordPolicy` và áp vào các nơi đặt mật khẩu (model vừa)

- **File:** `Application/Common/Security/PasswordPolicy.cs` (mới); `NguoiDungService.AddAsync` (cả nhánh khôi phục xóa mềm) và `UpdateUserAsync` khi có mật khẩu: `Validate(pw, req.TenDangNhap / existingUser.TenDangNhap, existingUser.NguoiThue?.SoDienThoai)` (ở `AddAsync` chưa nạp người thuê nên truyền `null`; Admin đặt trùng số điện thoại chỉ dẫn tới bị ép đổi, không mở lỗ mới); `ChangePasswordAsync`: kiểm mật khẩu cũ trước, rồi `newPassword == oldPassword` → "Mật khẩu mới phải khác mật khẩu hiện tại.", rồi `Validate(newPassword, user.TenDangNhap, user.NguoiThue?.SoDienThoai)`; `GuestRegistrationService`: thay phép kiểm độ dài bằng `Validate(password, <tên đăng nhập/email>, <số điện thoại>)`, giữ thông báo lỗi chung như hiện có; `Areas/KhachThue/ViewModels/DoiMatKhauViewModel.cs`: `MinLength(8)` + `MaxLength(128)`.
- **Test:** `Application.UnitTests/PasswordPolicyTests.cs` (7 và 129 ký tự sai, 8 và 128 đúng, toàn khoảng trắng, trùng tên không phân biệt hoa thường, trùng số điện thoại dạng `0901 234 567` / `84901234567` / `+84901234567`, từng mục danh sách yếu, `LaMatKhauMacDinh` cho từng dòng bảng 2.2). Test `ChangePasswordAsync` (trùng mật khẩu cũ, vi phạm chính sách, hợp lệ) đặt trong `NguoiDungServiceTests.cs` mới dùng fake store (task 3 dựng fake).
- **Xong khi:** test Application xanh; `grep -rn "Length is < 8" src` không còn ở `GuestRegistrationService`.

### Task 2: `AdminSeedOptions` và seed không mật khẩu mặc định (model vừa)

- **File:** `AdminSeedOptions.cs` (mới); `Infrastructure/DependencyInjection.cs` bind + `Validate()` + `AddSingleton` theo mẫu `SePayOptions`; `INguoiDungService.SeedAdminAccountAsync` đổi thành `Task<SeedAdminResult>`; `NguoiDungService` nhận `AdminSeedOptions` qua constructor: có `admin` chưa xóa → `DaCo` (bỏ nhánh SHA256); chưa có và `Password` rỗng → `ThieuMatKhau`; chưa có và có mật khẩu → tạo, `DaTao`. `DatabaseInitializer`: `ThieuMatKhau` → `LogWarning("Chưa có tài khoản admin và chưa cấu hình AdminSeed:Password (biến môi trường AdminSeed__Password). Đặt giá trị rồi khởi động lại để tạo admin.")`.
- **Test:** `Application.UnitTests` (ba nhánh, nhánh `DaCo` không gọi `Update`, `Validate()` từ chối `admin` và chuỗi 7 ký tự, chấp nhận rỗng); `Infrastructure.IntegrationTests/DatabaseInitializerAdminSeedTests.cs` trên DB test: xóa mềm/không có `admin` thì không tạo khi thiếu mật khẩu, tạo khi có, đăng nhập được bằng mật khẩu đó. Test phải dọn `admin` nó tạo hoặc dùng giao dịch, để không ảnh hưởng test khác.
- **Xong khi:** `grep -rn 'HashPassword("admin")' src` rỗng; Web test vẫn khởi động sạch (chỉ có log Warning).

### Task 3: Bộ giới hạn đăng nhập và `LoginAsync` (model lớn: quyền, đăng nhập)

- **File:** `LoginThrottleOptions.cs`, `ILoginAttemptThrottle.cs`, `InMemoryLoginAttemptThrottle.cs`, `LoginResult`/`LoginStatus`, `UserAuthDto.BatBuocDoiMatKhau` (mới/sửa); `Application/DependencyInjection.cs`: `TryAddSingleton(new LoginThrottleOptions())`, `TryAddSingleton<ILoginAttemptThrottle, InMemoryLoginAttemptThrottle>()`; `Infrastructure/DependencyInjection.cs`: bind `LoginThrottleOptions`; `NguoiDungStore.GetActiveByUsernameAsync` thêm `.Include(x => x.NguoiThue)`; `NguoiDungService`: thêm `ILoginAttemptThrottle`, `LoginAsync` theo mục 2, `ValidateUserAsync` gọi lại `LoginAsync`.
- **Test:** `Application.UnitTests/InMemoryLoginAttemptThrottleTests.cs` với `FixedTimeProvider` (khóa ở lần sai thứ 5, lần 4 chưa khóa, mở sau 15 phút, cửa sổ hết hạn thì đếm lại, thành công xóa đếm tên nhưng giữ đếm IP, IP null không đếm, `MaxFailuresPerClient = 0` tắt, tên khác hoa thường dùng chung bộ đếm, vượt trần không phình quá `MaxTrackedKeys`); `NguoiDungServiceTests.cs` với fake `INguoiDungStore`, fake `IPasswordService`, fake `IUnitOfWork` đặt trong `Fakes/` (đang khóa thì mật khẩu đúng vẫn `TamKhoa` và không gọi `VerifyPassword` thật; tên không tồn tại vẫn gọi `VerifyPassword` một lần; cờ ép đổi đúng cho `admin`, trùng tên, khách thuê trùng số điện thoại hai dạng; khách vãng lai mật khẩu = tên **không** bị cờ; `LanDangNhapCuoi` được ghi).
- **Xong khi:** test Application xanh; `has-pending-model-changes` PASS.

### Task 4: Đăng nhập ở Web, bộ lọc ép đổi, test kiến trúc (model lớn)

- **File:** `Web/Helpers/ForcedPasswordChange.cs`, `Web/Filters/YeuCauDoiMatKhauAttribute.cs` (mới); `AdminBaseController` thêm `[YeuCauDoiMatKhau]`; `Areas/QuanLyNhaTro/Controllers/ThongBaoController.cs` thêm `[YeuCauDoiMatKhau]`; 10 controller `Areas/KhachThue` thêm `[YeuCauDoiMatKhau]` (chọn gắn từng controller thay vì tạo lớp cha mới để không đổi kế thừa); `[BoQuaYeuCauDoiMatKhau]` ở `NguoiDungController.DangNhap` (GET, POST), `DangXuat`, `HoSoController.Index`, `HoSoController.DoiMatKhau`. `NguoiDungController.DangNhap` POST: gọi `LoginAsync(req.TenDangNhap, req.MatKhau, HttpContext.Connection.RemoteIpAddress?.ToString())`; `TamKhoa` → `ViewBag.Error = $"Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau {n} phút."`; `Sai` → thông báo cũ; `ThanhCong` có cờ → thêm claim `BatBuocDoiMatKhau=1` và chuyển thẳng tới `ChangeUrl` (bỏ `returnUrl`).
- **Test:** `Web.IntegrationTests/ForcedPasswordChangeTests.cs`: tạo NhanVien có mật khẩu = tên đăng nhập (qua `INguoiDungStore`/`IPasswordService` như `SessionRevalidationTests`, vì `AddAsync` sẽ từ chối), đăng nhập → 302 tới `/QuanLyNhaTro/DoiMatKhau`; GET `/QuanLyNhaTro/Dashboard` → 302 tới trang đổi; POST AJAX bất kỳ API quản lý với `X-Requested-With` → 403 JSON có `requireChangePassword`; `DangXuat` vẫn chạy; khách thuê có mật khẩu = số điện thoại → 302 tới `/KhachThue/HoSo?doiMatKhau=1`, `HoSo/Index` trả 200; tài khoản thường không bị ảnh hưởng. `LoginThrottleTests.cs`: 5 lần sai cùng tên → lần 6 với mật khẩu đúng vẫn 200 kèm thông báo khóa và không có cookie; dùng tên ngẫu nhiên mỗi test. `ForcedPasswordChangeArchitectureTests.cs` (test kiến trúc mới): phản chiếu assembly Web, mọi controller có `[Area("QuanLyNhaTro")]` hoặc `[Area("KhachThue")]` phải có `YeuCauDoiMatKhauAttribute` (kể cả kế thừa); danh sách ngoại lệ rỗng.
- **Xong khi:** bộ Web test chạy một mình đều xanh, kể cả ~40 file đăng nhập bằng form không sửa.

### Task 5: Màn đổi mật khẩu cổng quản lý (model vừa, có giao diện)

- **File:** `Areas/QuanLyNhaTro/Controllers/DoiMatKhauController.cs` (mới, kế thừa `AdminBaseController`): `GET /QuanLyNhaTro/DoiMatKhau` trả view (truyền `BatBuoc = ForcedPasswordChange.IsRequired(User)`); `POST /QuanLyNhaTro/DoiMatKhau` nhận model (mật khẩu hiện tại, mới, xác nhận; kiểm xác nhận khớp ở Web), gọi `ChangePasswordAsync`, thành công thì `ReissueWithoutClaimAsync` và trả `{ success, message, redirectUrl = "/QuanLyNhaTro/Dashboard" }`; cả hai action `[BoQuaYeuCauDoiMatKhau]`. ViewModel ở `Areas/QuanLyNhaTro/ViewModels/` (8–128 ký tự). View `Areas/QuanLyNhaTro/Views/DoiMatKhau/Index.cshtml` theo `references/mau-trang.md`: thẻ form ba ô, gợi ý chính sách dưới ô mới, banner cảnh báo khi `BatBuoc`, gửi bằng AJAX kèm antiforgery như các màn quản lý khác, thông báo bằng SweetAlert. `Views/Shared/_Header.cshtml`: thêm mục "Đổi mật khẩu" trong menu người dùng, chỉ khi `User.IsInRole("Admin") || User.IsInRole("NhanVien")`.
- **Test:** `Web.IntegrationTests/ManagerChangePasswordTests.cs`: chưa đăng nhập → 302 tới đăng nhập; GET 200 có form; POST sai mật khẩu cũ → `success=false`; vi phạm chính sách → `success=false`; đúng → `success=true`, đăng nhập lại bằng mật khẩu mới được, mật khẩu cũ bị từ chối; người bị ép đổi xong thì GET `Dashboard` trả 200 bằng cookie mới; header có link "Đổi mật khẩu" cho Admin.
- **Xong khi:** test xanh; `soat-giao-dien.js` trên view mới và `_Header.cshtml` 0 LỖI; ảnh sáng/tối/375px của màn (thường và bị ép) đã lưu.

### Task 6: Hồ sơ khách thuê khi bị ép đổi (model vừa, có giao diện)

- **File:** `HoSoController.DoiMatKhau`: thành công và đang có claim → `ReissueWithoutClaimAsync`, thêm `redirectUrl = "/KhachThue/Dashboard"` vào JSON (trả kiểu ẩn danh `{ success, message, redirectUrl }` thay vì `Json(result)`, giữ hai trường cũ). `HoSo/Index.cshtml`: khi `ForcedPasswordChange.IsRequired(User)` hoặc `?doiMatKhau=1` thì hiện banner spec 2.5, cuộn tới form và `focus()` ô `MatKhauCu`; JS thành công mà có `redirectUrl` thì chuyển trang; gợi ý "Tối thiểu 8 ký tự".
- **Test:** thêm vào `ForcedPasswordChangeTests.cs`: khách thuê bị ép, POST `/KhachThue/HoSo/DoiMatKhau` mật khẩu mới hợp lệ → `success=true`, cookie mới vào `/KhachThue/HoaDon` được 200; mật khẩu mới = số điện thoại → `success=false`.
- **Xong khi:** test xanh; soát script 0 LỖI; ảnh trang Hồ sơ có banner (sáng/tối/375px).

### Task 7: `[Authorize]` cho hub (model nhỏ)

- **File:** `Hubs/ThongBaoHub.cs` thêm `[Authorize]`; `Views/Shared/_Layout.cshtml`: chỉ nạp `signalr.min.js` và `thongbao.js` khi `User.Identity?.IsAuthenticated == true` (trang `Privacy`, trang lỗi dùng layout này khi ẩn danh; `khachthue-thanhtoan.js`, `khachthue-vnpay.js` đã kiểm `window.ThongBaoHub`). `guest-dat-coc.js` không đổi (trang đã yêu cầu đăng nhập).
- **Test:** sửa `SignalRTests`: negotiate ẩn danh → 401; đăng nhập (tạo NhanVien bằng service) rồi negotiate → 200 có `connectionId`.
- **Xong khi:** test xanh; mở trang quản lý trên preview không có lỗi console về hub; mở `/Home/Privacy` ẩn danh không gọi `/thongBaoHub`.

### Task 8: Sửa test dùng `admin`/`admin`, cấu hình mẫu (model nhỏ)

- **File:** `MeterReadingSubmissionTests.cs:462-475`: tạo Admin riêng qua `INguoiDungService.AddAsync` với tên ngẫu nhiên và mật khẩu hằng có sẵn trong file, đăng nhập bằng tài khoản đó. `src/QuanLyChoThuePhongTroWeb.Web/appsettings.example.json`: thêm `"AdminSeed": { "Password": "" }` và `"LoginThrottle": { "MaxFailuresPerUsername": 5, "MaxFailuresPerClient": 20, "WindowMinutes": 15, "LockMinutes": 15 }` (JSON không có chú thích thì ghi giải thích vào README module); `.env.example`: thêm `AdminSeed__Password=` có chú thích "chỉ cần khi DB chưa có tài khoản admin, tối thiểu 8 ký tự, không dùng admin".
- **Xong khi:** `grep -rn '"MatKhau", "admin"' tests` rỗng.

### Task 9: Kiểm toàn bộ và tài liệu (model vừa)

- Chạy theo thứ tự, một lượt mỗi lần:

  ```powershell
  dotnet build QuanLyChoThuePhongTroWeb.sln
  dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
  dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
  $p = "src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj"
  dotnet ef migrations has-pending-model-changes --project $p --startup-project $p
  # cần $env:QLCTPT_TEST_CONNECTION_STRING trỏ DB *_test
  dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
  dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests
  git diff --check
  ```

- Kiểm tay trên preview (`--BackgroundJobs:Enabled=false`): admin cũ (`admin`/`admin`) bị ép đổi; nhập sai 5 lần thì bị khóa; khách thuê đăng nhập bằng số điện thoại bị ép đổi ở Hồ sơ; tài khoản thường không thấy gì khác. Ảnh lưu `hinhanhprojectdemo`.
- Tài liệu: `review.md` (kết quả, rủi ro còn lại), README module (tick hạng mục, "Bạn cần cung cấp"), dòng danh mục `docs/features/README.md`, mục Hiện trạng `.agents/hien-trang.md`, báo cáo hằng ngày.
- **Xong khi:** đủ tiêu chí spec mục 9.

## 4. Thứ tự và phụ thuộc

`1 → 2 → 3 → 4 → (5, 6) → 7 → 8 → 9`. Task 5 và 6 cùng dựa vào `ForcedPasswordChange` của task 4 và đều sửa view nên làm lần lượt. Task 7, 8 độc lập về file nhưng cần task 4 để Web test xanh nên để sau. Task 8 phần sửa `MeterReadingSubmissionTests` phải xong trước lần chạy Web test toàn bộ đầu tiên sau task 2 (thiếu `admin` seed thì test này đỏ): **làm phần đó ngay trong task 2**.

## 5. Rủi ro khi làm

- `Include(NguoiThue)` ở `GetActiveByUsernameAsync` làm `UserAuthDto.HoVaTen`/`Email` của khách thuê có giá trị thay vì `null`; kiểm không chỗ nào dựa vào giá trị `null` đó (hiện chỉ `DangNhap` dùng `UserAuthDto`).
- Bộ đếm đăng nhập là singleton, sống suốt bộ Web test (một factory dùng chung): test nhập sai phải dùng tên ngẫu nhiên; không test nào được dùng chung tên với test khóa.
- Ảnh hưởng tới cổng khách vãng lai chỉ có hai điểm: `GuestRegistrationService` theo chính sách chung (thêm luật trùng tên/số điện thoại, danh sách yếu) và layout không nối hub khi ẩn danh. Khách thuê bị ép đổi vẫn vào được các trang `Phong` dành cho khách (không thuộc hai cổng có bộ lọc): chấp nhận, ghi vào `review.md`.
- Người bị ép đổi vẫn gọi được `Web/Controllers/ThongBaoController` (chuông thông báo trên header của trang đổi mật khẩu cần nó).

## 6. Bạn cần cung cấp (sau khi code xong)

- `AdminSeed__Password` chỉ khi chạy trên DB chưa có `admin` (Docker mới, máy mới). DB phát triển hiện có `admin` thì không cần; đăng nhập `admin`/`admin` sẽ bị yêu cầu đổi ngay.
