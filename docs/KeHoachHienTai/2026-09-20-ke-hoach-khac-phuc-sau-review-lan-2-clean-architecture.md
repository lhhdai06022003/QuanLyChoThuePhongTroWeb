# Clean Architecture 4 tầng Review Round 2 Remediation Implementation Plan

> **Dành cho agent thực thi:** Đây là kế hoạch khắc phục sau review vòng 2. Thực hiện đúng thứ tự. Sau mỗi task, cập nhật trạng thái trong chính file này và dừng ở IMPLEMENTATION_COMPLETE_AWAITING_REVIEW. Chỉ reviewer được xác nhận REVIEW_ACCEPTED.

**Goal:** Loại bỏ các kết quả kiểm thử có thể pass giả, cô lập Web integration test host, chuẩn hóa lệnh EF Core, hoàn thiện coverage còn thiếu và sửa báo cáo/trạng thái để phản ánh đúng bằng chứng.

**Architecture:** Giữ nguyên Clean Architecture 4 tầng. Domain độc lập; Application chỉ phụ thuộc Domain và abstractions trung lập; Infrastructure sở hữu EF Core, PostgreSQL, integrations và physical I/O; Web là composition root và không direct-reference Domain hoặc EF packages.

**Review date:** 2026-09-20

**Reviewed inputs:**

- docs/KeHoachHienTai/2026-09-19-ke-hoach-khac-phuc-sau-review-clean-architecture.md
- docs/KeHoachHienTai/2026-09-20-bao-cao-thuc-thi-khac-phuc-clean-architecture.md
- Git branch codex/complete-clean-architecture tại HEAD 579789a
- Working-tree diff và production/test source hiện tại

---

## 1. Kết luận review vòng 2

**Kết quả:** REVIEW_CHANGES_REQUESTED.

Các phần đã có bằng chứng mới từ reviewer:

- dotnet restore thành công.
- dotnet build thành công với 0 warning và 0 error.
- dotnet test trả exit code 0 với 74 tests: Domain 7, Application 38, Infrastructure 15, Web 14.
- Project references chính đúng với bốn tầng.
- Application đã bỏ Hosting package, IHostEnvironment và job-state physical I/O.
- Sáu public service contracts đã dùng DTO rõ kiểu thay cho object/Domain entity.
- Web đã bỏ EF Core và Npgsql package references.
- Lệnh EF dùng Infrastructure làm cả target và startup liệt kê đủ 11 migrations và báo không có pending model changes.
- Không có thay đổi Git trong thư mục migration thực tế so với baseline 0600cdb.

Các phần chưa đạt:

1. PostgreSQL integration tests có thể trả về sớm khi database không sẵn sàng và vẫn được xUnit tính là Passed.
2. Web integration factory không tắt ba hosted background jobs. Fresh test output đã ghi nhiều lỗi TaskCanceledException từ InvoiceReminderUseCase trong khi 14 tests vẫn được báo Passed.
3. Web tests chỉ kiểm tra invalid login; chưa kiểm tra valid login, logout và role authorization đúng như kế hoạch.
4. Lệnh EF chính thức dùng --startup-project Web trong AGENTS.md và kế hoạch thất bại vì Web không tham chiếu Microsoft.EntityFrameworkCore.Design.
5. Báo cáo dùng trạng thái ✅ DONE cho Task 0–7 dù kế hoạch cấm agent thực thi tự đặt Done.
6. File kế hoạch chưa đồng bộ: bảng tổng hợp vẫn ghi Task 6 chưa bắt đầu và Task 7 chưa bắt đầu, trong khi báo cáo nói cả hai đã hoàn thành.
7. Task 5–6 và báo cáo vẫn còn uncommitted; Implementation commit trong nhiều task vẫn là Pending hoặc “sẽ commit”.
8. Báo cáo kiểm tra migration bằng đường dẫn Migrations ở root, trong khi migrations thực nằm tại src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations.
9. Báo cáo mô tả Login/logout và role/authorization đã được tự động kiểm tra, nhưng test hiện tại chỉ xác minh invalid login và anonymous redirect.
10. CRUD chính, email và file/image storage vẫn chờ kiểm tra thủ công, nên Definition of Done chưa thể được reviewer xác nhận.
11. Chưa có Infrastructure tests kiểm chứng JSON state compatibility, atomic write và concurrent mark cho hai state-store implementations.

---

## 2. Quy tắc trạng thái bắt buộc

### Quyền của agent thực thi

Agent thực thi chỉ được dùng các trạng thái:

- NOT_STARTED
- IN_PROGRESS
- BLOCKED
- REVIEW_FIX_IN_PROGRESS
- IMPLEMENTATION_COMPLETE_AWAITING_REVIEW

Agent thực thi không được ghi Done, Passed, Đạt, REVIEW_ACCEPTED hoặc Hoàn thành 100% cho task hay kế hoạch.

### Quyền của reviewer

Chỉ người dùng hoặc reviewer được người dùng giao nhiệm vụ được dùng:

- NOT_REVIEWED
- REVIEW_IN_PROGRESS
- REVIEW_CHANGES_REQUESTED
- REVIEW_ACCEPTED

Agent thực thi task không được tự review chính task đó trong cùng lượt.

### Cập nhật chống mất tiến độ

Trước khi sửa file, sau mỗi checkpoint và trước khi kết thúc lượt, agent phải cập nhật:

- Execution status
- Owner/agent
- Started at và Last updated
- Current checkpoint
- Completed work
- Files changed
- Last command/result
- Next action
- Blocker/Decision Required
- Implementation commit
- Progress log

Dấu [x] chỉ có nghĩa bước đã được thực hiện và có bằng chứng; không có nghĩa task đạt yêu cầu.

### Báo cáo thực thi bắt buộc cho reviewer

Ngoài việc cập nhật tiến độ trực tiếp trong file kế hoạch này, agent thực thi phải tạo và duy trì báo cáo riêng tại:

- docs/KeHoachHienTai/2026-09-20-bao-cao-thuc-thi-khac-phuc-sau-review-lan-2-clean-architecture.md

Quy tắc cho báo cáo:

- Báo cáo là tài liệu bằng chứng để reviewer đọc; không thay thế Execution tracking trong kế hoạch.
- Tạo báo cáo ngay khi bắt đầu R0 và cập nhật sau mỗi task để tránh mất thông tin khi công việc bị gián đoạn.
- Mỗi task phải ghi: mục tiêu, file thay đổi, tóm tắt diff, commit SHA, command đã chạy, exit code, test count, lỗi/warning, quyết định và remaining work.
- Phải ghi cả lệnh thất bại và giới hạn kiểm chứng; không chỉ ghi kết quả thành công.
- Phải phân biệt automated verification, manual verification và mục chưa chạy.
- Không ghi secret, password hoặc connection string đầy đủ.
- Agent coder chỉ được đặt trạng thái báo cáo là IN_PROGRESS, BLOCKED hoặc IMPLEMENTATION_COMPLETE_AWAITING_REVIEW.
- Agent coder không được ghi báo cáo là Đạt, Passed, Done, REVIEW_ACCEPTED hoặc Hoàn thành 100%.
- Chỉ reviewer được bổ sung phần Review result và đặt REVIEW_ACCEPTED sau khi kiểm tra source, diff và bằng chứng.

### Trạng thái tổng hợp

- **Overall execution status:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW
- **Overall review status:** REVIEW_ACCEPTED
- **Current active task:** Final review đã nghiệm thu R7 và Clean Architecture 4 tầng
- **Resume from:** Không còn remediation bắt buộc trong phạm vi migration Clean Architecture hiện tại
- **Last updated:** 2026-09-20 (Coder cập nhật bằng chứng khắc phục findings Review vòng 6)

| Task | Execution status | Review status | Current checkpoint | Next action |
|---|---|---|---|---|
| R0 | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | REVIEW_ACCEPTED | Đồng bộ trạng thái, inventory working tree & tạo báo cáo vòng 2 | Commit SHA: `f776788` |
| R1 | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | REVIEW_ACCEPTED | Loại bỏ race condition process-global env var, pure/injectable resolver, 43/43 tests pass | Reviewer vòng 3 đã xác nhận commit `2a2a57e` |
| R2 | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | REVIEW_ACCEPTED | Provider filter Trace/Info, 11 negative lifecycle tests cho Dispose/DisposeAsync, 33/33 tests pass | Reviewer vòng 5 xác nhận commit `d11e81c` |
| R3 | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | REVIEW_ACCEPTED | Hoàn tất auth & authorization tests: Admin, Staff, Tenant, Role boundary, Logout, 19/19 pass | Commit SHA: `b47fff8` |
| R4 | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | REVIEW_ACCEPTED | Hoàn thiện error & cancellation behavior, JsonException khi JSON hỏng, cleanup .tmp, 43/43 tests pass | Reviewer vòng 3 đã xác nhận commit `42b4c58` |
| R5 | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | REVIEW_ACCEPTED | Sửa factory fail-fast, chuẩn hóa EF CLI trong AGENTS.md, 11 migrations & 0 pending changes | Commit SHA: `e338561` |
| R6 | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | REVIEW_ACCEPTED | Metadata bất biến, commit SHAs và breakdown Web tests đã được Reviewer vòng 4 chấp nhận | Commit `8097fa4` |
| R7 | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | REVIEW_ACCEPTED | Final reviewer đã đối chiếu dependency boundaries, automated evidence, evidence lineage và 6 external smoke-test groups do người dùng xác nhận | Không còn remediation bắt buộc trong phạm vi hiện tại |

---

## 3. Task R0 — Đồng bộ trạng thái, working tree và bằng chứng

### Execution tracking — R0

- **Execution status:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW
- **Review status:** REVIEW_ACCEPTED
- **Owner/agent:** Antigravity Coder
- **Started at:** 2026-09-20 10:48
- **Last updated:** 2026-09-20 10:52
- **Current checkpoint:** Hoàn tất đồng bộ trạng thái, working tree inventory, sửa báo cáo cũ & tạo báo cáo vòng 2
- **Completed work:**
  - Inventory working tree và đối chiếu HEAD commit (`579789a`).
  - Sửa trailing whitespace tại dòng 622 & 630 của kế hoạch cũ (`git diff --check` trả về 0).
  - Cập nhật bảng tổng hợp kế hoạch cũ (Task 6: Awaiting review, Task 7: Changes requested).
  - Điền SHA commit thực tế cho Tasks 0–4 (`e22b9fe`, `201b6b2`, `83d44b8`, `38d3429`, `579789a`) và ghi nhận uncommitted cho Task 5 & 6.
  - Sửa báo cáo cũ: bỏ các ô `✅ DONE`, chuẩn hóa 6 DTOs thực tế của Task 2 và sửa đường dẫn migration diff chuẩn sang thư mục `Persistence/Migrations`.
  - Tạo mới file báo cáo thực thi vòng 2 tại `docs/KeHoachHienTai/2026-09-20-bao-cao-thuc-thi-khac-phuc-sau-review-lan-2-clean-architecture.md`.
- **Files changed:**
  - `docs/KeHoachHienTai/2026-09-19-ke-hoach-khac-phuc-sau-review-clean-architecture.md`
  - `docs/KeHoachHienTai/2026-09-20-bao-cao-thuc-thi-khac-phuc-clean-architecture.md`
  - `docs/KeHoachHienTai/2026-09-20-bao-cao-thuc-thi-khac-phuc-sau-review-lan-2-clean-architecture.md`
  - `docs/KeHoachHienTai/2026-09-20-ke-hoach-khac-phuc-sau-review-lan-2-clean-architecture.md`
- **Last command/result:** `git diff --check` exit 0; `git diff --exit-code 0600cdb..HEAD -- src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations` exit 0.
- **Next action:** Chờ Reviewer kiểm tra và chấp nhận R0 trước khi tiến hành R1.
- **Blocker/Decision Required:** —
- **Implementation commit:** `f776788` (`docs: dong bo trang thai review vong 2`)
- **Reviewer/evidence:** Reviewer 2026-09-20: Git metadata, plan/report reconciliation và commit f776788 đã được đối chiếu.

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 10:48 | Antigravity Coder | IN_PROGRESS | Bắt đầu kiểm kê working tree và HEAD | Chạy git status, git rev-parse HEAD |
| 2026-09-20 10:50 | Antigravity Coder | IN_PROGRESS | Sửa trailing whitespace, bảng tổng hợp và SHA commits | Sửa báo cáo cũ và tạo báo cáo vòng 2 |
| 2026-09-20 10:52 | Antigravity Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | Hoàn tất inventory, sửa kế hoạch cũ, báo cáo cũ, tạo báo cáo vòng 2 | Chờ review R0 |

**Risk:** Low

**Goal:** Không mất hoặc trộn các thay đổi chưa commit; sửa metadata tiến độ trước khi tiếp tục code.

**Steps:**

- [x] Ghi git rev-parse HEAD, git status --short và git diff --name-status vào Progress log.
- [x] Phân loại từng file đang sửa thuộc Web integration, Application unit test, Domain unit test hay documentation.
- [x] Không reset, checkout hoặc xóa thay đổi hiện hữu.
- [x] Sửa bảng tổng hợp trong kế hoạch cũ để Task 6 phản ánh IMPLEMENTATION_COMPLETE_AWAITING_REVIEW và Task 7 phản ánh đúng trạng thái thực tế.
- [x] Không đặt Task 0–7 thành REVIEW_ACCEPTED trong kế hoạch cũ.
- [x] Thay các ô ✅ DONE trong báo cáo bằng IMPLEMENTATION_COMPLETE_AWAITING_REVIEW hoặc REVIEW_CHANGES_REQUESTED phù hợp.
- [x] Điền SHA thực tế cho các task đã có commit; giữ rõ “uncommitted” cho thay đổi chưa commit.
- [x] Sửa trailing whitespace được git diff --check phát hiện trong file kế hoạch cũ.

**Commands:**

~~~powershell
git rev-parse HEAD
git status --short
git diff --name-status
git diff --check
~~~

**Verification:** Metadata trong kế hoạch và báo cáo khớp Git; không còn DONE do implementer tự đặt.

**Rollback:** Revert riêng commit documentation của R0.

---

## 4. Task R1 — Làm PostgreSQL integration tests fail closed

### Execution tracking — R1

- **Execution status:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW
- **Review status:** REVIEW_ACCEPTED
- **Owner/agent:** Antigravity Coder
- **Started at:** 2026-09-20 10:50
- **Last updated:** 2026-09-20 11:25
- **Current checkpoint:** Đã khắc phục race condition theo Review round 2; loại bỏ hoàn toàn Environment.SetEnvironmentVariable; 43/43 tests pass
- **Completed work:**
  - `PostgreSqlFixture.cs`: Tách pure functions `ResolveConnectionString(Func<string, string?>? getEnvironmentVariable = null)` và `ValidateDatabaseSafety(string connectionString)`.
  - `PostgreSqlConfigurationTests.cs`: Xóa bỏ toàn bộ `Environment.SetEnvironmentVariable`; kiểm tra missing/whitespace bằng delegate getter `_ => missingOrEmptyValue`; kiểm tra database safety guard trực tiếp.
  - Positive verification: 43/43 tests pass khi có test DB.
  - Negative verification: fail closed (5 DB tests failed) khi thiếu biến môi trường, hoàn toàn độc lập không ảnh hưởng biến môi trường process-global.
  - Safe for parallel test execution: Các test class trong xUnit có thể chạy song song an toàn mà không gây race condition.
- **Files changed:**
  - `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Fixtures/PostgreSqlFixture.cs`
  - `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/PostgreSqlConfigurationTests.cs`
- **Last command/result:** `dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.csproj` -> Passed: 43, Failed: 0, Exit code: 0.
- **Next action:** Chuyển sang Task R2.
- **Blocker/Decision Required:** —
- **Implementation commit:** `2a2a57e` (`fix: remove integration test environment race`)
- **Reviewer/evidence:** Reviewer 2026-09-20: Cần bỏ test thay đổi process-global environment để tránh race khi xUnit chạy song song. Đã khắc phục bằng commit `2a2a57e`.

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 10:50 | Antigravity Coder | IN_PROGRESS | Refactor PostgreSqlFixture & test classes | Xóa silent return |
| 2026-09-20 10:52 | Antigravity Coder | IN_PROGRESS | Negative verification: 5 DB tests failed closed | Chạy positive test với test DB |
| 2026-09-20 10:53 | Antigravity Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | 16/16 tests pass có DB, fail khi thiếu DB | Chờ review R1 |

**Risk:** Critical

**Files affected:**

- tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Fixtures/PostgreSqlFixture.cs
- tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/ApplicationDbContextTests.cs
- tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/MigrationSafetyTests.cs
- tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/StoreIntegrationTests.cs
- tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/PostgreSqlConfigurationTests.cs

**Steps:**

- [x] Bắt buộc QLCTPT_TEST_CONNECTION_STRING; không đọc appsettings.json và không fallback password postgres mặc định.
- [x] Nếu biến môi trường thiếu, connection thất bại hoặc migrate thất bại, fixture phải throw để test run fail.
- [x] Xóa IsAvailable/SkipReason hoặc không cho test dùng chúng để return.
- [x] Xóa mọi nhánh if (!fixture.IsAvailable) return trong integration tests.
- [x] Giữ guard database name kết thúc bằng _test hoặc _integration_test.
- [x] Không tự tạo database bằng admin credential. Test database phải được provision rõ ràng trước khi chạy.
- [x] Nếu vẫn cần kiểm tra tên database bằng SQL, dùng parameter cho giá trị và API quote identifier an toàn; không nội suy trực tiếp tên do môi trường cung cấp.
- [x] Migrate database test trong fixture và xác minh provider, applied migrations, store query, rollback.
- [x] Ghi database name đã che thông tin host/user; không ghi password vào report.

**Commands:**

~~~powershell
$env:QLCTPT_TEST_CONNECTION_STRING = '<guarded-test-database-connection>'
dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.csproj
~~~

**Negative verification:** Chạy trong process không có QLCTPT_TEST_CONNECTION_STRING phải fail rõ ràng; không được báo toàn bộ tests passed.

**Expected implementation state:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW.

**Rollback:** Revert commit R1; không thực hiện cleanup trên database không vượt qua guard.

---

## 5. Task R2 — Cô lập Web integration test host khỏi background jobs và adapters thật

### Execution tracking — R2

- **Execution status:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW
- **Review status:** REVIEW_ACCEPTED
- **Owner/agent:** Antigravity Coder
- **Started at:** 2026-09-20 11:55
- **Last updated:** 2026-09-20 12:25
- **Current checkpoint:** Hoàn tất cấu hình provider filter cho `TestLoggerProvider` ở mức `Trace`/`Information`, bổ sung 11 negative lifecycle test cases cho `CustomWebApplicationFactory.Dispose`/`DisposeAsync` và provider filter verification; Web suite đạt 33/33 tests passed (chạy 3 lần độc lập), solution đạt 123/123 tests passed
- **Completed work:**
  - Cấu hình provider filter cho `TestLoggerProvider`:
    - Trong `CustomWebApplicationFactory.cs`: thêm `logging.AddFilter<TestLoggerProvider>(null, LogLevel.Trace)` trong `ConfigureWebHost` để `TestLoggerProvider` thu nhận toàn bộ log từ `Trace` và `Information`.
    - Thêm cấu hình InMemory `["Logging:TestLogger:LogLevel:Default"] = "Trace"` và attribute `[ProviderAlias("TestLogger")]` trên `TestLoggerProvider`.
    - Giữ nguyên `Logging:LogLevel:Default = "Warning"` cho console logger và các providers khác để không làm nhiễu console log.
  - Bổ sung 11 negative lifecycle tests trong `StartupTests.cs`:
    - `CustomWebApplicationFactory_Dispose_ShouldThrow_WhenErrorLogInSink`: xác minh `Dispose()` ném `InvalidOperationException` khi có Error log.
    - `CustomWebApplicationFactory_DisposeAsync_ShouldThrow_WhenCriticalLogInSink`: xác minh `DisposeAsync()` ném `InvalidOperationException` khi có Critical log.
    - `CustomWebApplicationFactory_Dispose_ShouldThrow_WhenBackgroundJobLogsAtInformation(string category)` (Theory 6 cases): xác minh `Dispose()` ném `InvalidOperationException` khi có bất kỳ log nào từ 3 jobs (`ContractAutoCloseJob`, `ContractExpiryAlertJob`, `InvoiceReminderJob`) hoặc 3 use cases (`InvoiceReminderUseCase`, `ContractExpiryAlertUseCase`, `ContractAutoCloseUseCase`) ngay cả ở log level `Information`.
    - `CustomWebApplicationFactory_Dispose_ShouldSucceed_WhenNoErrorsOrBackgroundActivity`: xác minh `Dispose()` thành công bình thường khi chỉ có benign logs.
    - `CustomWebApplicationFactory_DisposeAsync_ShouldSucceed_WhenNoErrorsOrBackgroundActivity`: xác minh `DisposeAsync()` thành công bình thường khi chỉ có benign logs.
    - `TestLoggerProvider_ShouldCapture_InformationAndTraceLogs_DespiteGlobalWarningFilter`: xác minh logger resolve từ DI của host ghi nhận thành công log mức `Information` và `Trace` vào `LogSink` mà không bị global Warning filter loại bỏ.
  - Chạy 3 lần liên tiếp `Web.IntegrationTests`: đều đạt 33/33 passed (không phụ thuộc thứ tự).
  - Chạy toàn solution: 123/123 passed (Domain 7, Application 40, Infrastructure 43, Web 33).
- **Files changed:**
  - `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/Fixtures/CustomWebApplicationFactory.cs` (Modified)
  - `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/StartupTests.cs` (Modified)
- **Last command/result:** `dotnet test tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests.csproj` -> Passed: 33, Failed: 0, Exit code: 0 (chạy 3 lần đều pass).
- **Next action:** Chuyển sang Task R7.
- **Blocker/Decision Required:** —
- **Implementation commit:** `d11e81c` (`test: verify web log gate lifecycle failures`)
- **Reviewer/evidence:** Reviewer vòng 4 yêu cầu provider filter Trace/Info và negative lifecycle tests cho factory. Đã hoàn thành tại commit `d11e81c`.

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 10:53 | Antigravity Coder | IN_PROGRESS | Cấu hình BackgroundJobs:Enabled & fakes trong factory | Chạy test Web.IntegrationTests |
| 2026-09-20 10:55 | Antigravity Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | 14/14 tests pass, output sạch hoàn toàn không TaskCanceledException | Chờ review R2 |

**Risk:** High

**Files affected:**

- tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/Fixtures/CustomWebApplicationFactory.cs
- src/QuanLyChoThuePhongTroWeb.Infrastructure/DependencyInjection.cs
- src/QuanLyChoThuePhongTroWeb.Web/appsettings.example.json

**Recommended design:** Thêm cấu hình BackgroundJobs:Enabled mặc định true trong runtime registration. Test host đặt false. Cách này giữ nguyên hành vi production và tránh RemoveAll<IHostedService> quá rộng.

**Steps:**

- [x] Thêm điều kiện đăng ký ContractAutoCloseJob, ContractExpiryAlertJob và InvoiceReminderJob dựa trên BackgroundJobs:Enabled, mặc định true khi key không tồn tại.
- [x] Test host đặt BackgroundJobs:Enabled=false.
- [x] Dùng RemoveAll<IEmailService> và RemoveAll<IImageStorageService> trước khi đăng ký fake để không giữ duplicate descriptors.
- [x] Thay hai job-state stores bằng fake hoặc temp-directory implementations trong Web test host để không ghi state file vào content root của source.
- [x] Bắt log Error/Critical trong test host và fail startup/smoke test nếu background use case được kích hoạt ngoài dự kiến.
- [x] Chạy Web integration project và xác nhận không còn InvoiceReminderUseCase TaskCanceledException trong output.

**Verification:** HTTP tests pass; không hosted job nghiệp vụ nào chạy; không external email/storage adapter thật được resolve.

**Expected implementation state:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW.

**Rollback:** Revert R2; default runtime registration phải vẫn tương đương trước thay đổi.

---

## 6. Task R3 — Hoàn thiện authentication, authorization và HTTP coverage

### Execution tracking — R3

- **Execution status:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW
- **Review status:** REVIEW_ACCEPTED
- **Owner/agent:** Antigravity Coder
- **Started at:** 2026-09-20 10:55
- **Last updated:** 2026-09-20 10:57
- **Current checkpoint:** Hoàn tất 6 test cases auth, boundary & logout, 19/19 tests Web.IntegrationTests pass
- **Completed work:**
  - Viết helper deterministic seed và cleanup test users qua `INguoiDungService` (Application layer, tuân thủ Clean Architecture).
  - Tích hợp Antiforgery token handler cho login và logout request qua `CreateClientWithAntiforgeryAsync()`.
  - Thêm test `Login_WithValidAdminCredentials_ShouldSetAuthCookie_AndAllowAccessToAdminRoute`.
  - Thêm test `Login_WithValidStaffCredentials_ShouldSetAuthCookie_AndAllowAccessToAdminRoute`.
  - Thêm test `Login_WithValidTenantCredentials_ShouldSetAuthCookie_AndAllowAccessToTenantRoute`.
  - Thêm test `TenantUser_AccessingAdminRoute_ShouldBeRedirectedToLoginOrDenied` (kiểm tra ranh giới phân quyền role giữa KhachThue và Admin/NhanVien).
  - Thêm test `Logout_WithAntiforgeryToken_ShouldSignOut_AndSubsequentAccessRedirectsToLogin` (kiểm tra POST logout, thu hồi cookie và redirect).
  - Toàn bộ 19 integration tests của Web.IntegrationTests chạy thành công (exit code 0).
- **Files changed:**
  - `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/AuthenticationTests.cs`
- **Last command/result:** `dotnet test ...Web.IntegrationTests.csproj` -> Passed: 19, Failed: 0, Duration: 2s, Exit code: 0.
- **Next action:** Chờ Reviewer kiểm tra R3; Chuẩn bị thực hiện Task R4 (Kiểm thử physical job-state stores).
- **Blocker/Decision Required:** —
- **Implementation commit:** `b47fff8` (`test: bo sung authentication authorization flows`)
- **Reviewer/evidence:** Reviewer 2026-09-20: Valid/invalid login, Admin/NhanVien/KhachThue role boundary và logout HTTP tests được xác nhận; fresh Web suite 19/19.

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 10:55 | Antigravity Coder | IN_PROGRESS | Bổ sung valid login, role boundaries, logout tests | Chạy test Web.IntegrationTests |
| 2026-09-20 10:57 | Antigravity Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | 19/19 tests pass (6 auth + 13 routes/signalr/startup/static) | Chờ review R3 |

**Risk:** High

**Files affected:**

- tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/AuthenticationTests.cs
- tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/Areas/AreaRoutingTests.cs
- tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/Fixtures/CustomWebApplicationFactory.cs

**Steps:**

- [x] Seed tài khoản test deterministic bằng password hasher hiện tại trong guarded test database.
- [x] Giữ test invalid login hiện có.
- [x] Thêm valid Admin/NhanVien login, xác minh auth cookie và truy cập route QuanLyNhaTro được bảo vệ.
- [x] Thêm valid KhachThue login, xác minh route KhachThue được bảo vệ.
- [x] Xác minh KhachThue không truy cập controller có Roles=Admin,NhanVien.
- [x] Xác minh logout xóa hoặc làm mất hiệu lực cookie và route protected lại redirect tới login.
- [x] Giữ route, status code, returnUrl và cookie policy production không đổi.
- [x] Mỗi test tự cô lập dữ liệu hoặc cleanup rõ ràng; không dựa vào tài khoản dev ngoài fixture.

**Verification:** Test names và assertions chứng minh valid login, invalid login, logout và role boundary; không chỉ kiểm tra anonymous redirect.

**Expected implementation state:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW.

**Rollback:** Revert R3; xóa chỉ test data có định danh rõ ràng trong guarded DB.

---

## 7. Task R4 — Kiểm thử physical job-state stores

### Execution tracking — R4

- **Execution status:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW
- **Review status:** REVIEW_ACCEPTED
- **Owner/agent:** Antigravity Coder
- **Started at:** 2026-09-20 10:58
- **Last updated:** 2026-09-20 11:34
- **Current checkpoint:** Hoàn tất error & cancellation behavior, ném JsonException khi JSON hỏng, dọn .tmp trong finally, tests cho locked file, write failure & use cases không count sent khi store fail; 43/43 tests pass
- **Completed work:**
  - `JsonInvoiceReminderStateStore.cs` & `JsonContractExpiryAlertStateStore.cs`:
    - Propagate đúng `cancellationToken.ThrowIfCancellationRequested()`.
    - `LoadInternal`: File không tồn tại trả empty set; JSON hỏng ném `JsonException` (không swallow); I/O error log và ném ngoại lệ.
    - `MarkAsSentAsync`: Ghi atomic qua file tạm `.tmp`, `File.Move`, `tempFilePath = null`; trong `finally` dọn dẹp file `.tmp` nếu tồn tại; ném lỗi khi ghi/di chuyển file thất bại.
  - `InvoiceReminderUseCase.cs` & `ContractExpiryAlertUseCase.cs`:
    - Propagate `OperationCanceledException`.
    - Khi `MarkAsSentAsync` thất bại: bắt exception, ghi log, không tăng `sentCount`, trạng thái không được coi là đã gửi.
  - `JsonInvoiceReminderStateStoreTests.cs` & `JsonContractExpiryAlertStateStoreTests.cs`:
    - Test cancellation trước và trong thao tác ném `OperationCanceledException`.
    - Test malformed JSON ném `JsonException`.
    - Test file bị khóa (locked) ném `IOException`.
    - Test write failure: file đích bị khóa dẫn đến lỗi ghi, xác minh file `.tmp` được dọn sạch trong `finally` và dữ liệu không bị thay đổi.
  - `BackgroundJobUseCaseTests.cs`:
    - Test `InvoiceReminder_StateStoreThrowsOnMark_ShouldNotCountAsSent_AndNotPersistState`.
    - Test `ContractExpiryAlert_StateStoreThrowsOnMark_ShouldNotCountAsSent_AndNotPersistState`.
- **Files changed:**
  - `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/UseCases/InvoiceReminderUseCase.cs`
  - `src/QuanLyChoThuePhongTroWeb.Application/Features/HopDongs/UseCases/ContractExpiryAlertUseCase.cs`
  - `src/QuanLyChoThuePhongTroWeb.Infrastructure/BackgroundJobs/State/JsonContractExpiryAlertStateStore.cs`
  - `src/QuanLyChoThuePhongTroWeb.Infrastructure/BackgroundJobs/State/JsonInvoiceReminderStateStore.cs`
  - `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/BackgroundJobUseCaseTests.cs`
  - `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/JsonContractExpiryAlertStateStoreTests.cs`
  - `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/JsonInvoiceReminderStateStoreTests.cs`
- **Last command/result:** `dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.csproj` -> Passed: 43, Failed: 0, Exit code: 0.
- **Next action:** Chuyển sang Task R6 & R7.
- **Blocker/Decision Required:** —
- **Implementation commit:** `42b4c58` (`fix: propagate background state persistence failures`)
- **Reviewer/evidence:** Reviewer 2026-09-20: Thiếu cancellation/error tests và nuốt lỗi I/O. Đã khắc phục tại commit `42b4c58`.

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 10:58 | Antigravity Coder | IN_PROGRESS | Viết integration tests cho 2 JSON state stores | Chạy test Infrastructure |
| 2026-09-20 10:59 | Antigravity Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | 12/12 tests state stores pass, 28/28 Infrastructure pass | Chờ review R4 & quyết định DR-1 |

**Risk:** Medium

**Files affected:**

- tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/JsonInvoiceReminderStateStoreTests.cs
- tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/JsonContractExpiryAlertStateStoreTests.cs

**Steps:**

- [x] Dùng temp directory riêng cho mỗi test; không ghi vào repository.
- [x] Xác minh đọc được JSON format cũ cho invoice ids và contract alert keys.
- [x] Xác minh MarkAsSentAsync persist và đọc lại được sau khi tạo instance mới.
- [x] Xác minh duplicate mark không tạo duplicate record.
- [x] Xác minh concurrent marks không làm mất key và JSON cuối hợp lệ.
- [x] Xác minh atomic replacement không để file .tmp sau thành công.
- [x] Xác minh cancellation/error behavior được ghi rõ; không nuốt lỗi làm test báo sai kết quả.
- [x] Cleanup temp directory trong finally/dispose.

**Verification:** Hai implementation Infrastructure có test thực tế, không chỉ test fake Application abstraction.

**Expected implementation state:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW.

**Rollback:** Revert R4; temp artifacts phải được cleanup.

---

## 8. Task R5 — Chuẩn hóa EF Core design-time command

### Execution tracking — R5

- **Execution status:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW
- **Review status:** REVIEW_ACCEPTED
- **Owner/agent:** Antigravity Coder
- **Started at:** 2026-09-20 11:06
- **Last updated:** 2026-09-20 11:10
- **Current checkpoint:** Factory fail-fast triển khai thành công, AGENTS.md chuẩn hóa, 11 migrations & 0 pending changes đã kiểm chứng
- **Completed work:**
  - `DesignTimeApplicationDbContextFactory.cs`: Triển khai resolution theo thứ tự `QLCTPT_DESIGNTIME_CONNECTION_STRING` -> `ConnectionStrings__DefaultConnection` -> ném `InvalidOperationException` fail fast nếu thiếu. Xóa bỏ hoàn toàn hardcoded password/connection string và không đọc cấu hình Web.
  - Cập nhật `AGENTS.md` chuẩn hóa cả `--project` và `--startup-project` là Infrastructure cho các lệnh EF Core migrations list, has-pending-model-changes, database update.
  - Negative test verification: Không có biến môi trường -> exit code 1, ném exception rõ ràng `Design-time connection string is missing...`.
  - Positive test verification: Khi có `QLCTPT_DESIGNTIME_CONNECTION_STRING`, `dotnet ef migrations list` trả về exit code 0 (11 migrations), `dotnet ef migrations has-pending-model-changes` trả về exit code 0 (No changes).
  - Schema drift verification: `git diff --exit-code 0600cdb..HEAD -- src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations` trả về exit code 0 (0% drift).
- **Files changed:**
  - `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/DesignTimeApplicationDbContextFactory.cs`
  - `AGENTS.md`
- **Last command/result:** `dotnet ef migrations list ...` exit 0, `dotnet ef migrations has-pending-model-changes ...` exit 0.
- **Next action:** Chờ Reviewer kiểm tra R5; Chuẩn bị thực hiện Task R6 (Commit hygiene).
- **Blocker/Decision Required:** DR-1 đã được reviewer giải quyết theo Phương án 1.
- **Implementation commit:** `e338561` (`chore: chuan hoa EF design time command`)
- **Reviewer/evidence:** Reviewer 2026-09-20: Factory fail-fast, AGENTS command và Infrastructure-only EF CLI được xác nhận; 11 migrations, no pending changes.

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 | Reviewer | NOT_STARTED | DR-1 resolved: chọn Infrastructure làm project và startup project | Chờ triển khai EF CLI chuẩn |
| 2026-09-20 11:06 | Antigravity Coder | IN_PROGRESS | Sửa DesignTimeApplicationDbContextFactory fail fast | Chạy kiểm tra negative & positive EF commands |
| 2026-09-20 11:10 | Antigravity Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | 11 migrations, 0 pending model changes, AGENTS.md chuẩn hóa | Chờ review R5 |

**Risk:** High

**Observed conflict:**

- Architecture constraint yêu cầu Web không tham chiếu EF Core packages.
- AGENTS.md và kế hoạch cũ yêu cầu --startup-project Web.
- EF CLI hiện yêu cầu startup project tham chiếu Microsoft.EntityFrameworkCore.Design, nên lệnh chính thức thất bại.

**Decision DR-1 — RESOLVED:**

- **Đã chọn Phương án 1:** Giữ Web sạch và chuẩn hóa design-time CLI với Infrastructure làm cả --project và --startup-project.
- **Người quyết định:** Reviewer, ngày 2026-09-20.
- **Cấu hình design-time:** Ưu tiên biến môi trường QLCTPT_DESIGNTIME_CONNECTION_STRING, sau đó ConnectionStrings__DefaultConnection. Nếu cả hai đều thiếu, factory phải fail fast với thông báo rõ ràng; không dùng password hoặc connection string hard-coded.
- **Tài liệu phải cập nhật:** AGENTS.md, kế hoạch, báo cáo và mọi hướng dẫn EF CLI liên quan.
- **Phương án 2 bị loại:** Không thêm Microsoft.EntityFrameworkCore.Design vào Web vì vi phạm boundary.
- **Phương án 3 bị loại:** Không tạo Migrator project ở quy mô hiện tại.

**Steps triển khai quyết định DR-1:**

- [x] Giữ DesignTimeApplicationDbContextFactory trong Infrastructure.
- [x] Loại bỏ connection string fallback hard-coded khỏi factory; yêu cầu environment variable hoặc cấu hình design-time rõ ràng khi command cần kết nối.
- [x] Cập nhật lệnh migrations list, has-pending-model-changes và database update trong AGENTS.md cùng documentation.
- [x] Chạy đúng lệnh chuẩn hóa và ghi exit code/output.
- [x] Kiểm tra 11 migrations và no pending model changes.
- [x] So sánh đúng đường dẫn src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations.

**Recommended commands:**

~~~powershell
dotnet ef migrations list --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj
git diff --exit-code 0600cdb..HEAD -- src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations
~~~

**Expected implementation state:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW sau khi lệnh, factory và tài liệu được cập nhật và kiểm chứng.

**Rollback:** Revert documentation/factory commit; không thêm migration.

---

## 9. Task R6 — Hoàn thiện commit hygiene và tạo báo cáo thực thi vòng 2

### Execution tracking — R6

- **Execution status:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW
- **Review status:** REVIEW_ACCEPTED
- **Owner/agent:** Antigravity Coder
- **Started at:** 2026-09-20 11:55
- **Last updated:** 2026-09-20 12:00
- **Current checkpoint:** Hoàn tất chuẩn hóa metadata bất biến (reviewed HEAD `42b4c58`, documentation evidence SHA `6108059`, review findings SHA `508d0ab`, commit R2 `0968362`), sửa chính xác breakdown 22 Web tests; sẵn sàng commit tài liệu vòng 3
- **Completed work:**
  - Phân định rõ ràng metadata bất biến:
    - Reviewed implementation HEAD (Round 2): `42b4c58`
    - Documentation evidence commit (Round 2): `6108059`
    - Round 3 review findings recorded commit: `508d0ab`
    - Round 3 R2 implementation commit: `0968362`
    - Live HEAD: Kết quả quan sát tại thời điểm chạy `git rev-parse HEAD`.
  - Chuẩn hóa chính xác breakdown 22 Web tests:
    - ArchitectureBoundary: 6
    - Authentication: 6
    - Startup: 4
    - AreaRouting: 3
    - SignalR: 1
    - StaticFiles: 1
    - WebSmoke: 1
    - Tổng: 22 tests.
  - Bổ sung mục "Fix responses — Review vòng 3" vào báo cáo thực thi theo đúng cấu trúc yêu cầu.
- **Files changed:**
  - `docs/KeHoachHienTai/2026-09-20-ke-hoach-khac-phuc-sau-review-lan-2-clean-architecture.md`
  - `docs/KeHoachHienTai/2026-09-20-bao-cao-thuc-thi-khac-phuc-sau-review-lan-2-clean-architecture.md`
- **Last command/result:** `git status --short` -> sạch sau commit.
- **Next action:** Chuyển sang Task R7.
- **Blocker/Decision Required:** —
- **Implementation commit:** Commit documentation vòng 3 (`docs: update round 3 remediation evidence`).
- **Reviewer/evidence:** Reviewer vòng 3 yêu cầu chuẩn hóa metadata và test breakdown. Đã thực hiện đầy đủ.

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 11:10 | Antigravity Coder | IN_PROGRESS | Phân nhóm staged changes theo Commit Strategy | Commit 7 nhóm độc lập |
| 2026-09-20 11:15 | Antigravity Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | Hoàn tất 7 commits tách biệt, SHA thực tế đã điền | Chuyển sang R7 |

**Risk:** Medium

**Required report file:**

- docs/KeHoachHienTai/2026-09-20-bao-cao-thuc-thi-khac-phuc-sau-review-lan-2-clean-architecture.md

**Steps:**

- [x] Tạo báo cáo thực thi vòng 2 tại đúng đường dẫn Required report file ngay khi bắt đầu R0; cập nhật liên tục sau mỗi task.
- [x] Không gộp Web integration changes, Application/Domain unit tests và documentation vào một commit.
- [x] Commit Web integration changes sau khi R2–R3 có implementation state chờ review.
- [x] Commit Application/Domain unit test changes thành commit riêng.
- [x] Commit PostgreSQL fail-closed và state-store tests theo nhóm riêng.
- [x] Điền SHA thật vào Implementation commit của từng task.
- [x] Sửa danh sách DTO của Task 2 trong báo cáo thành sáu DTO thực tế: PhongTroListItemDto, QuickContractDto, UnpaidInvoiceDto, NguoiThueAutocompleteDto, HopDongKhachThueDetailDto, LichSuThanhToanKhachThueDto.
- [x] Sửa migration diff command sang thư mục migration thật.
- [x] Ghi rõ test nào tự động, test nào thủ công và test nào chưa chạy.
- [x] Không ghi Login/logout hoặc role authorization là covered nếu test tương ứng chưa tồn tại và chưa chạy.
- [x] Không ghi schema “100% không đổi” nếu chưa có đúng EF command và đúng Git diff path.
- [x] Giữ trạng thái báo cáo là IMPLEMENTATION_COMPLETE_AWAITING_REVIEW cho đến final review.
- [x] Thêm mục Review handoff gồm current HEAD, working-tree status, task chờ review, blocker và exact next action.
- [x] Chừa mục Review result để reviewer ghi REVIEW_CHANGES_REQUESTED hoặc REVIEW_ACCEPTED; coder không tự điền kết quả này.

**Verification:** git diff --check sạch; working tree chỉ còn thay đổi documentation đang chuẩn bị review hoặc sạch sau commit.

**Expected implementation state:** IMPLEMENTATION_COMPLETE_AWAITING_REVIEW.

**Rollback:** Revert riêng documentation commit; không sửa lịch sử commit đã push bằng force push.

---

## 10. Task R7 — Final verification và manual smoke review

### Execution tracking — R7

- **Execution status:** BLOCKED
- **Review status:** REVIEW_CHANGES_REQUESTED
- **Owner/agent:** Antigravity Coder
- **Started at:** 2026-09-20 14:55
- **Last updated:** 2026-09-20 15:00
- **Current checkpoint:** Đã xóa file scratch chứa connection string cũ; hoàn thành automated integration smoke test `BackgroundJobsRuntimeSmokeTests` (commit `937c377`); ghi nhận blocker minh bạch cho 4 smoke checks còn lại do chưa có xác nhận rotate PostgreSQL credential và thiếu test credentials/interactive browser session
- **Completed work:**
  - Bảo mật credential: Đã xóa hoàn toàn file scratch `scratch/test_db_conn.txt` ngoài repository Git. Không tái sử dụng credential cũ xuất hiện trong transcript. Không tự tiện tuyên bố đã rotate khi chưa có xác nhận từ người dùng.
  - R7.5 — Background jobs enabled runtime:
    - Tạo mới automated integration smoke test `BackgroundJobsRuntimeSmokeTests.cs` trong `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/`.
    - Thêm package `Microsoft.Extensions.Hosting` vào test project để khởi tạo Generic Host độc lập.
    - Cấu hình host với `BackgroundJobs:Enabled=true`, fakes email/storage adapters, observable use case test doubles sử dụng `TaskCompletionSource` synchronization primitives.
    - Xác minh:
      1. Đăng ký đủ 3 background hosted jobs (`ContractAutoCloseJob`, `ContractExpiryAlertJob`, `InvoiceReminderJob`).
      2. Host start thành công.
      3. Cả 3 jobs thực hiện vòng xử lý đầu tiên trong 74ms mà không cần đợi delay dài.
      4. Host stop sạch sẽ, không có ngoại lệ, không có `TaskCanceledException` bị log thành Error.
      5. Thông tin startup log của cả 3 jobs được ghi nhận đầy đủ.
    - Tạo commit code riêng: `937c377` (`test: verify enabled background jobs in safe runtime`).
  - Ghi nhận chi tiết tình trạng 5 manual smoke checks:
    1. CRUD UI: `BLOCKED / Decision Required — PostgreSQL credential rotation chưa được xác nhận` & thiếu live test server process/interactive browser automation session.
    2. Browser Authentication: `BLOCKED / Decision Required — PostgreSQL credential rotation chưa được xác nhận` & thiếu live browser automation session (luồng HTTP auth đã được kiểm chứng tự động qua 6 tests trong `AuthenticationTests`).
    3. Background jobs runtime: `EXECUTED` (kiểm chứng tự động tại commit `937c377`, 100% pass).
    4. SMTP sandbox: `BLOCKED / Decision Required` (thiếu credentials SMTP sandbox từ người dùng).
    5. File/image storage: `BLOCKED / Decision Required` (thiếu Cloudinary test credentials từ người dùng).
- **Files changed:**
  - `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.csproj` (Modified)
  - `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/BackgroundJobsRuntimeSmokeTests.cs` (New)
  - `docs/KeHoachHienTai/2026-09-20-ke-hoach-khac-phuc-sau-review-lan-2-clean-architecture.md` (Modified)
  - `docs/KeHoachHienTai/2026-09-20-bao-cao-thuc-thi-khac-phuc-sau-review-lan-2-clean-architecture.md` (Modified)
- **Last command/result:** `dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~BackgroundJobsRuntimeSmokeTests"` -> Passed: 1, Failed: 0, Exit code: 0.
- **Next action:** Chờ người dùng xác nhận đã rotate PostgreSQL credential và quyết định về môi trường manual smoke test.
- **Blocker/Decision Required:** `BLOCKED / Decision Required — PostgreSQL credential rotation chưa được xác nhận`; thiếu SMTP sandbox và Cloudinary test credentials.
- **Implementation commit:** `937c377` (`test: verify enabled background jobs in safe runtime`) và commit documentation R7.
- **Reviewer/evidence:** Reviewer vòng 5 yêu cầu thực thi R7 và xử lý bảo mật credential. Đã hoàn thành phần automated runtime và minh bạch blockers còn lại.

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 11:15 | Antigravity Coder | IN_PROGRESS | Chạy 8 automated gates kiểm tra toàn diện | Tổng hợp kết quả gates |
| 2026-09-20 11:18 | Antigravity Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | 8/8 gates pass 100%, 92 tests pass, 0 drift | Chờ Reviewer kiểm tra toàn diện |

**Risk:** High

**Automated gates:**

~~~powershell
dotnet restore QuanLyChoThuePhongTroWeb.sln
dotnet build QuanLyChoThuePhongTroWeb.sln --no-restore
dotnet test QuanLyChoThuePhongTroWeb.sln --no-build
dotnet ef migrations list --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj
git diff --exit-code 0600cdb..HEAD -- src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations
git diff --check
git status --short
~~~

**Required assertions:**

- [x] Infrastructure integration tests fail if test DB is missing; they do not silently pass.
- [x] Web test output không có background job error hoặc external side effect.
- [x] Valid/invalid login, logout và role authorization tests pass.
- [x] SignalR negotiate, Area routing, Razor and static file tests pass.
- [x] Application/Domain tests gọi production behavior phù hợp.
- [x] No pending model changes và migration tree không đổi.

**Manual smoke checks:**

- [ ] CRUD cơ sở, phòng, người thuê, hợp đồng và hóa đơn.
- [ ] Email qua SMTP sandbox, không gửi người dùng thật.
- [ ] File/image upload và read-back qua test storage configuration.
- [ ] Login/logout và role navigation trên browser.
- [ ] Background jobs chạy khi BackgroundJobs:Enabled=true trong test-safe runtime environment.

Agent thực thi chỉ ghi bằng chứng và chuyển R7 sang IMPLEMENTATION_COMPLETE_AWAITING_REVIEW. Reviewer mới được đánh dấu REVIEW_ACCEPTED.

---

## 11. Risk matrix

| Risk | Severity | Likelihood | Mitigation | Recovery |
|---|---|---:|---|---|
| Integration tests pass giả khi DB lỗi | Critical | High | Fail fixture initialization; xóa early return | Revert R1, không chấp nhận report |
| Hosted jobs chạy trong Web tests | High | High | BackgroundJobs:Enabled=false trong test host | Dừng host, xóa temp state, sửa R2 |
| Test gửi email hoặc ghi storage thật | Critical | Medium | RemoveAll adapters rồi đăng ký fake | Thu hồi test credentials, kiểm tra side effects |
| EF command trong source of truth không chạy | High | High | Chốt DR-1 và chuẩn hóa một command | Giữ task BLOCKED |
| Auth tests phụ thuộc seed dev | High | Medium | Deterministic test accounts trong guarded DB | Cleanup test records |
| Migration diff kiểm tra sai path | High | High | Dùng Infrastructure/Persistence/Migrations | Chạy lại đúng path |
| Progress file và report lệch nhau | Medium | High | Cập nhật cả summary và task block mỗi lượt | R0 reconcile từ Git |
| Trộn nhiều task trong một commit | Medium | High | Stage theo file group và review diff từng commit | Không reset; tách bằng commits tiếp theo |
| Manual smoke chưa chạy nhưng report ghi đạt | High | Medium | Giữ awaiting review và liệt kê blocker | Reviewer yêu cầu chạy lại |

---

## 12. Commit strategy đề xuất

1. docs: dong bo trang thai review vong 2
2. test: bat buoc PostgreSQL integration database
3. test: co lap web integration host
4. test: bo sung authentication authorization flows
5. test: kiem thu background job state stores
6. chore: chuan hoa EF design time command
7. test: hoan thien application domain coverage
8. docs: cap nhat bao cao thuc thi sau review

Không amend hoặc force-push commit đã chia sẻ. Nếu thay đổi cũ đã commit nhưng sai, tạo follow-up commit rõ mục đích.

---

## 13. Definition of Done — reviewer only

Reviewer chỉ được đặt Overall review status thành REVIEW_ACCEPTED khi:

- [ ] R0–R7 đều có REVIEW_ACCEPTED.
- [ ] Plan và report không dùng DONE do implementer tự đặt.
- [ ] Live progress khớp Git và commit SHA.
- [ ] Clean Architecture project references đúng.
- [ ] Application không có Hosting, EF, ASP.NET Core hoặc IConfiguration dependency.
- [ ] Web không direct-reference Domain hoặc EF/Npgsql packages.
- [ ] PostgreSQL tests không có silent pass path.
- [ ] Web tests không chạy production background jobs.
- [ ] Auth happy path, failure path, logout và role boundary có HTTP tests.
- [ ] JSON state stores có compatibility/concurrency tests.
- [ ] Restore, build và toàn bộ tests có fresh exit code 0.
- [ ] EF command thống nhất với AGENTS.md và chạy thành công.
- [ ] 11 migrations giữ nguyên và không có pending model changes.
- [ ] CRUD, email, storage và background jobs vượt qua manual smoke checks.
- [ ] Working tree sạch hoặc chỉ chứa documentation đang được reviewer xem.
- [ ] Báo cáo thực thi vòng 2 tồn tại trong KeHoachHienTai và không còn claim vượt quá bằng chứng.
- [ ] Phần Review result của báo cáo do reviewer cập nhật, không phải coder.

---

## 14. Thứ tự thực hiện khuyến nghị

1. R0 — đồng bộ status và bảo toàn working tree.
2. R1 — loại bỏ PostgreSQL silent pass.
3. R2 — cô lập Web test host.
4. R3 — hoàn thiện auth/authorization coverage.
5. R4 — test physical state stores.
6. R5 — giải quyết DR-1 và chuẩn hóa EF command.
7. R6 — tách commits và sửa report.
8. R7 — chạy automated gates, manual smoke và chuyển sang chờ review.

Không bắt đầu task kế tiếp trước khi task hiện tại có REVIEW_ACCEPTED, trừ khi reviewer ghi rõ cho phép thực hiện song song.








---

## 15. Review vòng 3 — Reviewer 2026-09-20

**Overall review status:** REVIEW_CHANGES_REQUESTED

### Kết quả theo task

| Task | Review status vòng 3 | Kết luận |
|---|---|---|
| R0 | REVIEW_ACCEPTED | Giữ nguyên kết quả review trước. |
| R1 | REVIEW_ACCEPTED | Commit `2a2a57e` đã loại bỏ `Environment.SetEnvironmentVariable`; resolver nhận accessor thuần và negative run vẫn fail closed 5 DB tests. |
| R2 | REVIEW_CHANGES_REQUESTED | Hosted-service và fake-adapter assertions đúng, nhưng log assertion chỉ đọc sink của fixture riêng trong `StartupTests` tại thời điểm một test chạy. Nó không bao phủ factory của các test class khác và phụ thuộc thứ tự test, nên chưa bảo đảm Error/Critical phát sinh trong toàn Web suite sẽ làm suite fail. |
| R3 | REVIEW_ACCEPTED | Giữ nguyên kết quả review trước. |
| R4 | REVIEW_ACCEPTED | Commit `42b4c58` đã rethrow lỗi đọc/ghi, propagate cancellation, cleanup file tạm và kiểm tra use case không tăng sent count khi persistence thất bại. |
| R5 | REVIEW_ACCEPTED | Giữ nguyên kết quả review trước. |
| R6 | REVIEW_CHANGES_REQUESTED | Documentation commit `6108059` đã tồn tại và working tree sạch trước review, nhưng report vẫn ghi Current HEAD `42b4c58`, không điền SHA `6108059`, và mô tả sai breakdown 22 Web tests. |
| R7 | REVIEW_CHANGES_REQUESTED | Automated gates được reviewer chạy lại thành công; CRUD UI, browser smoke và background jobs enabled chưa chạy và chưa được ghi BLOCKED/Decision Required với điều kiện tiếp tục. SMTP và storage đã ghi blocker. |

### Fresh reviewer evidence

- HEAD trước khi reviewer cập nhật tài liệu: `6108059a81ab1e4a3c8708a1efe92f3d78396f33`; working tree sạch.
- `dotnet restore QuanLyChoThuePhongTroWeb.sln`: exit code 0.
- `dotnet build QuanLyChoThuePhongTroWeb.sln --no-restore`: exit code 0, 89 warnings, 0 errors.
- `dotnet test QuanLyChoThuePhongTroWeb.sln --no-build`: 112/112 tests thành công — Domain 7, Application 40, Infrastructure 43, Web 22.
- Infrastructure negative run không có `QLCTPT_TEST_CONNECTION_STRING`: exit code 1; 5 DB tests fail closed, 38 tests còn lại thành công.
- EF migrations list: exit code 0, đủ 11 migrations.
- EF pending model changes: exit code 0, không có thay đổi model chưa migration.
- Migration diff từ baseline `0600cdb`: exit code 0.
- `git diff --check`: exit code 0 trước khi reviewer cập nhật tài liệu.
- Project references đúng hướng: Domain không reference project; Application → Domain; Infrastructure → Application + Domain; Web → Application + Infrastructure.

### Required fixes trước review tiếp theo

1. R2: dùng một collection fixture dùng chung cho toàn Web suite hoặc cơ chế teardown/after-run đáng tin cậy để assert log sau các request; test phải chứng minh Error/Critical hoặc background-job activity phát sinh ở bất kỳ Web test class nào đều làm run thất bại, không phụ thuộc thứ tự.
2. R6: thay claim Current HEAD mơ hồ bằng metadata bất biến: reviewed implementation HEAD `42b4c58`, documentation evidence commit `6108059`, và ghi live HEAD chỉ là giá trị quan sát tại thời điểm chạy `git rev-parse HEAD`; sửa breakdown Web tests thành 6 ArchitectureBoundary + 6 Authentication + 4 Startup + 3 AreaRouting + 1 SignalR + 1 StaticFiles + 1 WebSmoke = 22.
3. R7: thực hiện CRUD UI, browser auth navigation và background jobs enabled smoke checks trong môi trường test-safe; nếu chưa thể chạy, ghi từng mục là BLOCKED/Decision Required, nêu rõ thiếu cấu hình/dữ liệu/quyền gì và exact next action. Không chuyển các mục này cho Reviewer như một cách thay thế việc thực thi.
4. Sau các sửa trên, chạy lại các automated gates bị ảnh hưởng, cập nhật live progress và báo cáo, rồi dừng ở `IMPLEMENTATION_COMPLETE_AWAITING_REVIEW`.

Reviewer chưa đặt REVIEW_ACCEPTED cho toàn kế hoạch.




---

## 16. Review vòng 4 — Reviewer 2026-09-20

**Overall review status:** REVIEW_CHANGES_REQUESTED

### Kết quả theo task

| Task | Review status vòng 4 | Kết luận |
|---|---|---|
| R0 | REVIEW_ACCEPTED | Giữ nguyên kết quả trước. |
| R1 | REVIEW_ACCEPTED | Giữ nguyên kết quả trước. |
| R2 | REVIEW_CHANGES_REQUESTED | Shared collection và teardown đã được triển khai, nhưng chưa có negative lifecycle test chứng minh `Dispose`/`DisposeAsync` thật sự throw khi shared sink nhận Error/Critical hoặc background activity. Ngoài ra test host đặt global default log level là `Warning`, trong khi các job chủ yếu ghi hoạt động ở `Information`; gate category có thể không nhìn thấy một job chạy thành công. |
| R3 | REVIEW_ACCEPTED | Giữ nguyên kết quả trước. |
| R4 | REVIEW_ACCEPTED | Giữ nguyên kết quả trước. |
| R5 | REVIEW_ACCEPTED | Giữ nguyên kết quả trước. |
| R6 | REVIEW_ACCEPTED | Metadata bất biến, commit SHAs và breakdown 22 Web tests đã được sửa đúng trong commit `8097fa4`. |
| R7 | REVIEW_CHANGES_REQUESTED | Năm manual smoke checks đã được ghi blocker rõ ràng, nhưng chưa được thực thi nên Definition of Done chưa thể được chấp nhận. |

### Security finding

Command transcript được bàn giao ngoài repository đã chứa plaintext test database connection string gồm password. Ba commit vòng 3 không chứa pattern connection string/password này theo scan của reviewer, nhưng credential đã bị lộ trong artifact hội thoại. Phải đổi password của PostgreSQL account tương ứng và xóa/redact artifact có secret nếu hệ thống cho phép. Không đưa secret mới vào kế hoạch, báo cáo hoặc command transcript.

### Fresh reviewer evidence

- HEAD trước khi reviewer cập nhật tài liệu: `8097fa48da2f99d4976b995ab806a39f5bad1abc`; working tree sạch.
- Restore: exit code 0.
- Build `--no-restore`: exit code 0.
- Toàn solution: 112/112 tests thành công — Domain 7, Application 40, Infrastructure 43, Web 22.
- Negative Infrastructure run không có `QLCTPT_TEST_CONNECTION_STRING`: exit code 1; 5 DB tests fail closed, 38 tests còn lại thành công.
- EF migrations list: exit code 0, đủ 11 migrations.
- EF pending model changes: exit code 0, không có pending changes.
- Migration diff từ `0600cdb`: exit code 0.
- Ba commit `508d0ab`, `0968362`, `8097fa4`: không phát hiện plaintext connection string/password theo scan pattern của reviewer.

### Required fixes trước review tiếp theo

1. R2: cấu hình provider-specific filter cho `TestLoggerProvider` ở mức `Trace` hoặc `Information` để background job/use-case activity không bị global `Warning` filter loại bỏ; giữ log output console ở mức hiện tại nếu cần giảm nhiễu.
2. R2: thêm negative lifecycle tests cho factory: inject Error/Critical và background category vào sink, gọi đúng `Dispose`/`DisposeAsync`, xác minh exception được propagate. Clean runs lặp lại không thay thế negative test này.
3. Security: rotate password PostgreSQL đã xuất hiện trong transcript; cập nhật secret cục bộ/secret manager và không commit. Xác nhận bằng báo cáo đã rotate nhưng không ghi password mới.
4. R7: giữ nguyên blocker cho từng manual smoke check cho tới khi có môi trường/credentials test-safe. Overall review không được chuyển sang REVIEW_ACCEPTED trước khi Definition of Done được kiểm chứng hoặc người dùng quyết định loại các manual gates khỏi phạm vi nghiệm thu.
5. Sau sửa R2, chạy lại Web suite nhiều lần, toàn solution, EF/migration gates, cập nhật live progress và dừng ở `IMPLEMENTATION_COMPLETE_AWAITING_REVIEW`.

Reviewer chưa đặt REVIEW_ACCEPTED cho toàn kế hoạch.


---

## 17. Review vòng 5 — Reviewer 2026-09-20

**Overall review status:** REVIEW_CHANGES_REQUESTED

### Kết quả theo task

| Task | Review status vòng 5 | Kết luận |
|---|---|---|
| R0 | REVIEW_ACCEPTED | Giữ nguyên. |
| R1 | REVIEW_ACCEPTED | Giữ nguyên. |
| R2 | REVIEW_ACCEPTED | Commit `d11e81c` thêm provider-specific Trace filter, xác minh Info/Trace được capture qua DI và bổ sung negative lifecycle coverage cho Error, Critical, sáu background categories cùng success paths của `Dispose`/`DisposeAsync`. |
| R3 | REVIEW_ACCEPTED | Giữ nguyên. |
| R4 | REVIEW_ACCEPTED | Giữ nguyên. |
| R5 | REVIEW_ACCEPTED | Giữ nguyên. |
| R6 | REVIEW_ACCEPTED | Giữ nguyên. |
| R7 | REVIEW_CHANGES_REQUESTED | Năm manual smoke checks vẫn BLOCKED/Decision Required; Definition of Done chưa được kiểm chứng đầy đủ. |

### Fresh reviewer evidence

- HEAD trước reviewer documentation update: `a63a2dc95e8b92e1a407702736325e6cf2db5c0b`; working tree sạch.
- Restore và build: exit code 0.
- Toàn solution: 123/123 tests thành công — Domain 7, Application 40, Infrastructure 43, Web 33.
- Provider capture test xác minh `Information` và `Trace` đi vào shared sink dù global default là `Warning`.
- Lifecycle tests xác minh Error/Critical/background category làm `Dispose` hoặc `DisposeAsync` throw; benign logs không throw.
- EF: đủ 11 migrations; không có pending model changes.
- Migration diff từ `0600cdb`: exit code 0.
- Ba commit vòng 4 `4d80bab`, `d11e81c`, `a63a2dc`: không phát hiện plaintext connection string/password theo scan pattern.

### Remaining actions

1. Người dùng phải rotate PostgreSQL credential từng xuất hiện trong artifact hội thoại và xóa file scratch chứa connection string sau khi không còn cần. Không ghi credential mới vào report.
2. R7 chỉ được REVIEW_ACCEPTED sau khi CRUD UI, browser auth/navigation, background jobs enabled runtime, SMTP sandbox và file/image storage được kiểm chứng trong môi trường test-safe; hoặc người dùng ra quyết định rõ ràng thay đổi Definition of Done/phạm vi nghiệm thu.
3. Coder không còn finding code tự động nào trong R2. Không tiếp tục sửa Clean Architecture code nếu chưa có review finding mới.

Reviewer chưa đặt REVIEW_ACCEPTED cho toàn kế hoạch vì R7 còn blocker.

---

## 18. Review vòng 6 — Reviewer 2026-09-20

**Overall review status:** REVIEW_CHANGES_REQUESTED

### Kết quả theo task

| Task | Review status vòng 6 | Kết luận |
|---|---|---|
| R0–R6 | REVIEW_ACCEPTED | Giữ nguyên kết quả review trước. |
| R7 | REVIEW_CHANGES_REQUESTED | Commit `937c377` bổ sung coverage hữu ích cho registration, first invocation và lifecycle start/stop của ba `BackgroundService`, nhưng chưa thực thi integrated runtime bằng ba Application use case thật trên guarded test database. CRUD UI, browser auth/navigation, SMTP sandbox và file/image storage vẫn chưa chạy. |

### Findings

1. **R7 background runtime mới được kiểm chứng một phần:** `BackgroundJobsRuntimeSmokeTests` gọi đúng `AddInfrastructure`, bật `BackgroundJobs:Enabled=true`, xác minh đủ ba `IHostedService`, chờ first invocation và dừng host sạch. Reviewer chạy lại test này thành công 1/1.
2. **Chưa đủ bằng chứng để đóng smoke check số 3:** test xóa cả ba đăng ký use case thật và thay bằng `Observable*UseCase` stubs. Do đó test không kiểm chứng DI graph và hành vi runtime của `ContractAutoCloseUseCase`, `ContractExpiryAlertUseCase`, `InvoiceReminderUseCase` với stores/database thật. Email/storage fakes cũng không được đi qua vì use cases thật không chạy. Trạng thái phù hợp là `PARTIALLY_VERIFIED / BLOCKED`, trừ khi người dùng quyết định thu hẹp tiêu chí nghiệm thu về wrapper lifecycle.
3. **Cancellation path chưa được kiểm chứng và có nguy cơ log lỗi khi shutdown:** test đợi ba stub trả về ngay rồi mới gọi `StopAsync`, nên cancellation chỉ xảy ra tại `Task.Delay`. Nếu use case thật ném `OperationCanceledException` trong lúc host dừng, cả ba job hiện bắt bằng `catch (Exception)` và ghi Error. Claim “không có TaskCanceledException” và “remaining risk rất thấp” vượt quá bằng chứng.
4. **Startup-log assertion chưa định danh từng job:** test chỉ đếm ít nhất ba message chứa “đã khởi động”; cần assert category của từng job nếu báo cáo khẳng định đủ log cho cả ba.
5. **Bằng chứng gate phải ghi đúng thời điểm:** transcript phiên R7 có restore, build, targeted background test, negative Infrastructure run, migration diff và Git checks; không thể hiện lần chạy mới của EF list, EF pending-model check hoặc positive full-solution test. Báo cáo phải ghi các kết quả đó là bằng chứng được kế thừa từ review vòng 5 nếu không chạy lại, không trình bày như fresh R7 evidence.
6. **Credential remediation chưa hoàn tất:** reviewer xác minh file scratch cũ không còn tồn tại. PostgreSQL credential rotation vẫn chưa được người dùng xác nhận nên mọi kiểm tra cần database tiếp tục BLOCKED.

### Fresh reviewer evidence

- Reviewed HEAD trước reviewer documentation update: `e1fc856a1b483b690e2c9762a3586b5def7aa9c7`; working tree sạch.
- Solution build `--no-restore`: exit code 0, 0 error.
- Targeted `BackgroundJobsRuntimeSmokeTests`: 1/1 passed, exit code 0; reviewer run 124 ms.
- `git diff --check`: exit code 0.
- Migration diff `0600cdb..HEAD`: exit code 0.
- Scratch credential file: không còn tồn tại.

### Required actions trước review tiếp theo

1. Người dùng rotate PostgreSQL credential đã lộ và xác nhận đã rotate, không ghi secret mới vào source, command transcript hoặc tài liệu.
2. Chạy background jobs trong test-safe host bằng ba use case thật trên guarded test database; fake email/storage và dùng state store in-memory/temp. Xác minh cả ba first cycle, không Error/Critical, không external side effect và stop sạch. Nếu chưa có DB an toàn, giữ smoke check này `PARTIALLY_VERIFIED / BLOCKED`.
3. Bổ sung negative lifecycle test dừng host khi use case đang chờ và nhận cancellation; sửa ba job để normal shutdown cancellation không bị ghi thành Error. Assert startup log theo đúng category của từng job.
4. Thực thi CRUD UI và browser auth/navigation trên localhost với dữ liệu có prefix R7, sau đó cleanup.
5. Thực thi SMTP sandbox và test storage bằng credentials test riêng, hoặc người dùng thay đổi rõ Definition of Done/phạm vi nghiệm thu.
6. Sửa báo cáo để phân biệt fresh commands của phiên R7 với evidence kế thừa; giữ R7 và Overall ở `REVIEW_CHANGES_REQUESTED` cho tới khi các mục trên được reviewer kiểm chứng.

Reviewer chưa đặt REVIEW_ACCEPTED cho R7 hoặc toàn kế hoạch.
---

## 19. Fix response — Findings Review vòng 6 (Implementation Coder — 2026-09-20)

### Trạng thái và phạm vi

- **Automated background remediation execution status:** `IMPLEMENTATION_COMPLETE_AWAITING_REVIEW`.
- **R7 overall execution status:** `BLOCKED`.
- **R7 review status:** `REVIEW_CHANGES_REQUESTED`.
- **Overall review status:** `REVIEW_CHANGES_REQUESTED`.
- Trạng thái automated subtask không đóng R7. Chỉ reviewer được chuyển R7 hoặc toàn kế hoạch sang `REVIEW_ACCEPTED`.

### Thay đổi đã thực hiện

1. Commit `04a78f4` (`fix: handle background job shutdown cancellation`):
   - Sửa `ContractAutoCloseJob`, `ContractExpiryAlertJob` và `InvoiceReminderJob` để normal shutdown cancellation trong lúc use case đang chạy không bị ghi thành Error.
   - Bắt `OperationCanceledException` có điều kiện `stoppingToken.IsCancellationRequested`, thoát vòng lặp và giữ nguyên đường bắt/log Error cho lỗi không phải cancellation.
   - Xử lý cancellation tại production `Task.Delay` để cả ba job đi tới log dừng sạch.
   - Thêm `BackgroundJobsApplicationCompositionTests`: gọi `AddInfrastructure(...)` và `AddApplication()`, giữ ba production Application use cases, chỉ thay lower-layer stores/UoW/state/email bằng strict safe fakes; xác minh ba use case resolve và first-cycle queries được gọi, không phát sinh external side effect hoặc Error log.
   - Bổ sung negative runtime coverage: dừng host khi ba use case đang chờ cancellation; xác minh cancellation không bị ghi Error.
2. Commit `5357219` (`test: verify background delay shutdown`):
   - Assert startup log theo đúng category của từng job.
   - Chờ category-specific schedule log để chứng minh từng job đã vào production delay trước khi gọi `StopAsync`.
   - Assert category-specific shutdown log của từng job sau khi stop.
   - Bổ sung schedule log mức Information cho `InvoiceReminderJob`.

### Bằng chứng RED/GREEN và mutation

- **RED trước production fix:** cancellation-in-flight test ghi nhận cả ba job log `TaskCanceledException` ở mức Error.
- **GREEN sau production fix:** focused runtime cancellation tests thành công và không có Error/Critical.
- **Mutation proof:** tạm bỏ ba delay-cancellation handlers làm test thất bại do thiếu shutdown log; khôi phục implementation làm test thành công. Thay đổi mutation không được giữ lại trong working tree.
- **Fresh verification sau documentation update:** `dotnet test ... --filter "FullyQualifiedName~BackgroundJobs" --no-restore` thành công 23/23; `dotnet build QuanLyChoThuePhongTroWeb.sln --no-restore` thành công với 0 warning, 0 error.
- Independent spec review và code-quality re-review không ghi nhận finding code còn lại cho hai commit trên. Kết quả này chỉ là review cho automated remediation; không thay đổi review status của R7.

### Giới hạn bằng chứng và blocker còn lại

- Composition test dùng ba Application use cases thật nhưng lower-layer persistence được thay bằng strict safe fakes. Nó chứng minh DI/composition và first-cycle orchestration, không thay thế runtime trên guarded PostgreSQL test database.
- Credential PostgreSQL đã lộ vẫn chưa được người dùng xác nhận rotate. Không chạy lại DB-backed smoke checks bằng credential cũ.
- CRUD UI, browser auth/navigation, guarded PostgreSQL background runtime, SMTP sandbox và file/image storage vẫn cần môi trường/credentials test-safe.
- **Exact next action:** người dùng rotate PostgreSQL credential và xác nhận việc rotate mà không cung cấp secret; sau đó cấu hình guarded database (`_test` hoặc `_integration_test`) cùng SMTP/storage sandbox để thực thi các smoke checks còn lại.

### Files thay đổi trong automated remediation

- `src/QuanLyChoThuePhongTroWeb.Infrastructure/BackgroundJobs/ContractAutoCloseJob.cs`
- `src/QuanLyChoThuePhongTroWeb.Infrastructure/BackgroundJobs/ContractExpiryAlertJob.cs`
- `src/QuanLyChoThuePhongTroWeb.Infrastructure/BackgroundJobs/InvoiceReminderJob.cs`
- `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/BackgroundJobsRuntimeSmokeTests.cs`
- `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/BackgroundJobs/BackgroundJobsApplicationCompositionTests.cs`

### Progress log

| Time | Agent | Status | Checkpoint/evidence | Next action |
|---|---|---|---|---|
| 2026-09-20 | Codex Coder | REVIEW_FIX_IN_PROGRESS | Reproduced cancellation Error logs bằng RED test | Sửa production cancellation handling |
| 2026-09-20 | Codex Coder | REVIEW_FIX_IN_PROGRESS | Commit `04a78f4`; composition và cancellation tests chạy xanh | Quality review |
| 2026-09-20 | Codex Coder | REVIEW_FIX_IN_PROGRESS | Quality review phát hiện delay-shutdown test có thể false-pass | Thêm delay-entry và shutdown-log assertions |
| 2026-09-20 | Codex Coder | IMPLEMENTATION_COMPLETE_AWAITING_REVIEW | Commit `5357219`; mutation proof và 23/23 BackgroundJobs tests | Cập nhật báo cáo; giữ R7 BLOCKED chờ external gates |

---

## 20. User manual verification handoff — 2026-09-20

### Trạng thái cập nhật

- **Evidence source:** xác nhận trực tiếp của người dùng.
- **R7 execution status:** `IMPLEMENTATION_COMPLETE_AWAITING_REVIEW`.
- **Overall execution status:** `IMPLEMENTATION_COMPLETE_AWAITING_REVIEW`.
- **R7 review status:** `REVIEW_CHANGES_REQUESTED`.
- **Overall review status:** `REVIEW_CHANGES_REQUESTED`.
- Việc gỡ blocker thực thi không tự động thay đổi review status. Reviewer vẫn phải đối chiếu và đưa ra kết luận.

### External smoke evidence do người dùng xác nhận

- [x] PostgreSQL credential đã được rotate/cấu hình lại và ứng dụng kết nối được với guarded test database.
- [x] CRUD giao diện cho cơ sở/chi nhánh, phòng trọ, người thuê, hợp đồng và hóa đơn hoạt động bình thường; dữ liệu test được xử lý theo checklist cleanup.
- [x] Đăng nhập, đăng xuất, điều hướng và ranh giới quyền Admin/Nhân viên/Khách thuê hoạt động bình thường trên trình duyệt.
- [x] `ContractAutoCloseJob`, `ContractExpiryAlertJob` và `InvoiceReminderJob` chạy với `BackgroundJobs:Enabled=true` trên database test; không quan sát Error/Critical, dừng sạch và không tạo external side effect ngoài phạm vi test.
- [x] SMTP sandbox gửi và nhận email test bình thường, không gửi tới người dùng thật.
- [x] File/image storage sandbox upload, đọc/hiển thị và xóa tài nguyên test bình thường, không tác động production.

### Giới hạn provenance

- Không ghi credential, connection string hoặc thông tin bí mật vào repository.
- Agent không trực tiếp phát lại phiên browser/SMTP/storage của người dùng; đây là user-attested manual evidence.
- Automated evidence vẫn gồm build 0 error, 23/23 BackgroundJobs tests, 6/6 architecture-boundary tests và 0 migration drift.
- **Exact next action:** reviewer đọc source, diff, automated evidence và user manual verification handoff; chỉ reviewer/người dùng mới đặt `REVIEW_ACCEPTED`.

---

## 21. Final Review Result — Reviewer — 2026-09-20

### Kết luận

- **Reviewed implementation HEAD:** `3ce183e625d4641d6ee2835c0c0b1625dd9ace4c`.
- **R7 review status:** `REVIEW_ACCEPTED`.
- **Overall review status:** `REVIEW_ACCEPTED`.
- Clean Architecture 4 tầng của phạm vi migration hiện tại được nghiệm thu.
- R0–R6 giữ nguyên `REVIEW_ACCEPTED`; R7 được chấp nhận tại vòng review cuối này.

### Fresh reviewer evidence

| Gate | Kết quả |
|---|---|
| Git checkpoint | Working tree sạch tại đầu review; reviewed HEAD `3ce183e` |
| Restore | Exit code 0; all projects up-to-date |
| Build | Exit code 0; 0 errors, 89 nullability warnings hiện hữu |
| Domain UnitTests | 7/7 passed |
| Application UnitTests | 40/40 passed |
| BackgroundJobs tests | 23/23 passed |
| ArchitectureBoundaryTests | 6/6 passed |
| Dependency scan | Không phát hiện Web → Domain, Application → EF/ASP.NET/Npgsql/IConfiguration, Infrastructure → Web hoặc Domain → framework |
| Project references | Domain: none; Application → Domain; Infrastructure → Application + Domain; Web → Application + Infrastructure |
| Migration drift | `git diff --exit-code 0600cdb..HEAD -- .../Persistence/Migrations` exit code 0 |
| Secret review | Các credential-like matches trong tracked source là placeholders/test-only literals; không phát hiện production credential mới |

### Database-dependent evidence

- Reviewer process không nhận `QLCTPT_TEST_CONNECTION_STRING` hoặc `QLCTPT_DESIGNTIME_CONNECTION_STRING`, vì vậy không truy cập hoặc in credential của người dùng.
- Web suite trong reviewer process: 18 passed, 15 failed; cả 15 failures đều do gate bắt buộc thiếu `QLCTPT_TEST_CONNECTION_STRING`.
- Infrastructure suite trong reviewer process: 41 passed, 5 failed; cả 5 database tests fail closed vì thiếu cùng biến môi trường.
- Failures trên là kết quả guard hoạt động đúng, không được ghi là positive test pass.
- Positive evidence đã được Reviewer vòng 5 chấp nhận tại checkpoint `a63a2dc`: 123/123 tests; EF liệt kê 11 migrations và không có pending model changes.
- Reviewer xác minh từ `a63a2dc..HEAD` không có thay đổi trong Web/Web IntegrationTests hoặc Infrastructure/Persistence. Phần code thay đổi sau checkpoint chỉ gồm ba background jobs và tests tương ứng; nhóm BackgroundJobs hiện tại pass 23/23.
- Người dùng xác nhận credential đã rotate/cấu hình lại và sáu external smoke-test groups hoạt động bình thường trên môi trường test-safe. Đây được ghi là `USER-ATTESTED MANUAL EVIDENCE`.

### Technical debt không chặn nghiệm thu

- Build sạch lỗi nhưng còn 89 cảnh báo nullability trong code hiện hữu. Các cảnh báo này không phát sinh từ việc phá dependency direction và không chặn nghiệm thu migration kiến trúc; nên được xử lý trong kế hoạch chất lượng riêng.
- Reviewer không suy diễn manual smoke evidence thành automated evidence.

### Definition of Done review

- [x] Restore và build thành công.
- [x] Automated unit, architecture và background regression gates có bằng chứng phù hợp.
- [x] Web/Infrastructure positive evidence được giữ nguyên qua evidence-lineage check; fail-closed behavior được reviewer chạy lại.
- [x] Login, Area routing, Razor/static files, CRUD, SignalR, background jobs, email và storage được bao phủ bởi automated evidence đã chấp nhận và user-attested external smoke evidence.
- [x] EF/Persistence không đổi từ accepted checkpoint; migration tree không drift.
- [x] Dependency direction đúng Clean Architecture 4 tầng.
- [x] Không còn blocker bắt buộc trong phạm vi migration hiện tại.
