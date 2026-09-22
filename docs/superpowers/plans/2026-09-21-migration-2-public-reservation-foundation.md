# Public Viewing and Reservation Database Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create Migration 2 for public room media, staff branches, registered guest profiles, viewing schedules, reservations, deposits, and refunds.

**Scope Notice:** Migration 2 strictly delivers the database foundation (Domain entities, enums, EF Core configurations, migration, model snapshot, and PostgreSQL schema tests). Application Services, APIs, and UI business transactions are separate follow-up feature work and remain unchecked until implemented.

**Architecture:** This plan starts only after Migration 1 is merged. Person A owns Domain entities, enums, EF mappings, and the generated migration. Anonymous visitors can request a viewing; holding a room requires an authenticated `KhachVangLai` account.

**Tech Stack:** .NET 8, C#, EF Core 8, Npgsql/PostgreSQL, ASP.NET Core authentication, xUnit, Moq

---

## Ownership and file map

- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/PhongTro.cs`, `NguoiDung.cs`, and domain enums.
- Create: 15 approved entity files under `src/QuanLyChoThuePhongTroWeb.Domain/Entities/`.
- Create: focused EF configurations under `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Configurations/`.
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/ApplicationDbContext.cs`.
- Create: generated migration `*AddPublicViewingAndReservations*`.
- Test: Domain, Application, Infrastructure, and Web integration projects.

The approved field and relationship contract is in `docs/superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md`.

### Task 1: Add public room metadata and gallery

**Files:**
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/PhongTro.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/AnhPhongTro.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/PublicRoomRulesTests.cs`

- [x] **Step 1: Write failing publication rules**

Test that hidden rooms never appear publicly, only operationally vacant rooms can be published, and a room has at most one active cover image.

- [x] **Step 2: Add approved fields and gallery entity**

Use `DuocDangTin = false` by default (domain, EF mapping, migration `defaultValue: false`, không viết UPDATE thủ công); bỏ hoàn toàn `NgayDangTin` và `NgayNgungDang`; giữ `TieuDeDangTin` (max 255), `MaCongKhai` (nullable unique), `NguoiDangTinId` (FK tham chiếu `NguoiDungId`); keep Cloudinary file content outside PostgreSQL and store URL/public ID metadata.

- [x] **Step 3: Run Domain tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests --filter PublicRoomRulesTests`

Expected: PASS.

- [x] **Step 4: Commit room publication types**

```powershell
git add src/QuanLyChoThuePhongTroWeb.Domain tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
git commit -m "feat: add public room gallery model"
```

### Task 2: Add staff assignment and registered guest profile

**Files:**
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/NhanVienChiNhanh.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/KhachVangLai.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/NguoiDung.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Enums/AppEnums.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/BranchAssignmentTests.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/GuestConversionTests.cs`

- [x] **Step 1: Write failing authorization and conversion tests**

Verify one employee can have multiple active branches, revoked assignments deny access, a registered guest does not create `NguoiThue`, and conversion creates/links one tenant then changes the role.

- [x] **Step 2: Add the role without changing existing numeric values**

```csharp
public enum Role
{
    Admin = 0,
    NhanVien = 1,
    KhachThue = 2,
    KhachVangLai = 3
}
```

- [x] **Step 3: Add assignment and guest entities**

Use unconditional unique index on `(NguoiDungId, ChiNhanhId)` (no filter). Use `IsActive` as current authorization truth. Store assignment/revocation actors and dates for audit with `DeleteBehavior.Restrict`. Keep `KhachVangLai.NguoiDungId` unique and do not create a `NguoiThue` row until contract creation.

- [x] **Step 4: Run focused tests**

Run:
```powershell
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests --filter BranchAssignmentTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter GuestConversionTests
```

Expected: PASS.

- [x] **Step 5: Commit identity foundation**

```powershell
git add src/QuanLyChoThuePhongTroWeb.Domain tests
git commit -m "feat: add branch assignments and guest profiles"
```

### Task 3: Add hybrid AI viewing schedules

**Files:**
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/KhungGioXemPhong.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/YeuCauXemPhong.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/LichSuTrangThaiYeuCauXemPhong.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Enums/AppEnums.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ViewingScheduleServiceTests.cs`

- [x] **Step 1: Capture scheduling contract cases**

Capture anonymous submission with required name/phone, optional email, automatic confirmation inside an active slot, overflow at capacity, an outside-slot request becoming `ChoNhanVienXuLy`, and system-authored history. These tests document the approved data contract only; the final-place concurrency behavior remains part of the later Application feature.

- [x] **Step 2: Define exact scheduling enums**

```csharp
public enum NguonTaoYeuCauXemPhong { TrangCongKhai = 0, AI = 1 }
public enum HinhThucXacNhanLich { TuDong = 0, NhanVien = 1 }
public enum TrangThaiYeuCauXemPhong
{
    ChoChonLich = 0,
    ChoNhanVienXuLy = 1,
    DaXacNhanLich = 2,
    DaXemPhong = 3,
    TuChoi = 4,
    DaHuy = 5
}
public enum LoaiTacNhan { HeThong = 0, NguoiDung = 1 }
```

- [x] **Step 3: Add the three entities and check constraints**

`KhungGioXemPhong` belongs to `PhongTro` (has `PhongTroId`). Database check constraints: `ThoiGianBatDau < ThoiGianKetThuc` and `SoLuongToiDa > 0`. Do not add email-token fields. A request may link to `KhachVangLai` when logged in but remains valid without an account. Restrict delete behavior on `LichSuTrangThaiYeuCauXemPhong`.

- [ ] **Step 4: Implement deterministic booking rules in Application**

AI calls the same query and booking use cases as MVC/API. The service reloads/locks the slot, counts active bookings, confirms when capacity remains, otherwise returns conflict or routes to staff. *(Scheduled for Application Feature development phase)*.

- [ ] **Step 5: Run scheduling service tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter ViewingScheduleServiceTests`

Expected: PASS when Application service is implemented.

- [x] **Step 6: Commit viewing schedule model**

```powershell
git add src tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
git commit -m "feat: add hybrid viewing schedule model"
```

### Task 4: Add reservation workflow and room lock

**Files:**
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/YeuCauGiuCho.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/LichSuTrangThaiYeuCauGiuCho.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Enums/AppEnums.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/ReservationWorkflowTests.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/ReservationConcurrencyTests.cs`

- [x] **Step 1: Write reservation model and database concurrency tests**

Cover direct reservation, optional source viewing, employee-set amount/deadline, rejection reason, expiration, and the database guard for two overlapping transactions competing for the same room. PostgreSQL uniqueness/concurrency is verified by `ReservationConcurrencyTests.cs`; mapping a database conflict to an Application domain result remains follow-up feature work.

- [x] **Step 2: Define reservation states**

```csharp
public enum TrangThaiYeuCauGiuCho
{
    MoiTao = 0,
    ChoThanhToan = 1,
    ChoXacNhanTien = 2,
    DangGiuCho = 3,
    DaChuyenHopDong = 4,
    TuChoi = 5,
    HetHan = 6,
    DaHuy = 7
}
```

- [x] **Step 3: Add reservation and history entities**

Require `KhachVangLaiId`; allow nullable `YeuCauXemPhongId`. `SoTienGiuCho` is nullable `decimal?` (null allowed when `MoiTao`, required > 0 when transitioning to `ChoThanhToan`). Database check constraints:
- `CK_YeuCauGiuCho_SoTienGiuCho`: `"SoTienGiuCho" IS NULL OR "SoTienGiuCho" > 0`
- `CK_YeuCauGiuCho_SoTienTheoTrangThai`: `("TrangThai" NOT IN (1, 2, 3, 4)) OR ("SoTienGiuCho" IS NOT NULL AND "SoTienGiuCho" > 0)`
Restrict delete behavior on `LichSuTrangThaiYeuCauGiuCho`.

- [ ] **Step 4: Implement the room-lock transaction**

Approval checks publication/operational state and active reservations, then transitions to `ChoThanhToan` in one transaction. A filtered unique index remains the final concurrency guard. *(Scheduled for Application Feature development phase)*.

- [ ] **Step 5: Run focused tests**

Expected: one concurrent approval succeeds and the other returns a domain conflict without partial data.

- [x] **Step 6: Commit reservation workflow**

```powershell
git add src tests
git commit -m "feat: add reservation workflow model"
```

### Task 5: Add reservation payments

**Files:**
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/YeuCauThanhToanGiuCho.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/MinhChungThanhToanGiuCho.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/GiaoDichGiuCho.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/LichSuTrangThaiYeuCauThanhToanGiuCho.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ReservationPaymentTests.cs`

- [x] **Step 1: Capture payment data-contract cases**

Capture multiple evidence submissions, confirmation records, state history, and the target reservation state after confirmed money. Database uniqueness and relationship rules are covered by Infrastructure tests; atomic confirmation and management handling remain follow-up Application work.

- [x] **Step 2: Add separate reservation-payment entities**

Do not reuse `YeuCauThanhToanHoaDon` or `LichSuThanhToan`. Configure `DeleteBehavior.Restrict` on all foreign keys (`YeuCauThanhToanGiuCho`, `MinhChungThanhToanGiuCho`, `GiaoDichGiuCho`, `LichSuTrangThaiYeuCauThanhToanGiuCho`).

- [ ] **Step 3: Implement atomic confirmation**

Confirm evidence, insert `GiaoDichGiuCho`, write histories, and transition reservation in one transaction. *(Scheduled for Application Feature development phase)*.

- [ ] **Step 4: Run focused tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter ReservationPaymentTests`

- [x] **Step 5: Commit reservation payments**

```powershell
git add src tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
git commit -m "feat: add reservation payment model"
```

### Task 6: Add deposit application and refund ledger

**Files:**
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/ApDungTienGiuChoVaoTienCoc.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/QuyetDinhHoanTienGiuCho.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/GiaoDichHoanTienGiuCho.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/ReservationDepositAndRefundTests.cs`

- [x] **Step 1: Capture deposit and refund data-contract cases**

Capture the approved limits for deposit application and refund totals as data-contract examples. Database checks and unique relationships are covered by Infrastructure tests; locked aggregate validation remains follow-up Application work.

- [x] **Step 2: Add exact financial entities**

All amounts use `decimal` and `numeric(18,2)`. Configure `DeleteBehavior.Restrict` on all foreign keys. Application links are one-to-one for reservation-to-contract application and one-to-many for refund decision-to-transactions.

- [ ] **Step 3: Implement locked transactions**

Deposit application locks the reservation and contract. Refund recording locks the decision and sums existing refund transactions before insert. *(Scheduled for Application Feature development phase)*.

- [ ] **Step 4: Run focused tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter ReservationDepositAndRefundTests`

- [x] **Step 5: Commit deposit/refund model**

```powershell
git add src tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
git commit -m "feat: add reservation deposit and refund ledger"
```

### Task 7: Configure EF Core and generate Migration 2

**Files:**
- Create: focused mappings in `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Configurations/`
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: generated `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations/20260922163513_AddPublicViewingAndReservations.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/PublicReservationFoundationMappingTests.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/ReservationConcurrencyTests.cs`

- [x] **Step 1: Write failing PostgreSQL mapping and concurrency tests**

Assert 37 modeled business tables, one active cover image per room, unique unconditional staff assignment pair, unique guest account profile, viewing-slot capacity & time check constraints, one active reservation per room, nullable reservation deposit with check constraints, unique confirmed transaction/evidence links, one deposit application, refund amount constraints, and `DeleteBehavior.Restrict` across all audit/financial relationships.

- [x] **Step 2: Register DbSets and configurations**

Use the configuration assembly registration established by Migration 1. Keep table and index names stable and reviewable.

- [x] **Step 3: Generate Migration 2**

```powershell
dotnet ef migrations add AddPublicViewingAndReservations --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj
```

Expected: one migration/designer and updated snapshot; existing rooms receive `DuocDangTin = false`.

- [x] **Step 4: Apply on a database already at Migration 1**

Run the EF database update command against a dedicated PostgreSQL integration database.

Expected: update succeeds with 37 business tables and preserves all Migration 1 data.

- [x] **Step 5: Run mapping and concurrency tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests --filter "PublicReservationFoundationMappingTests|ReservationConcurrencyTests"`

Expected: PASS (122/122 passed).

- [x] **Step 6: Commit Migration 2**

```powershell
git add src/QuanLyChoThuePhongTroWeb.Infrastructure tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
git commit -m "feat: add public reservation database foundation"
```
Migration 1 và Migration 2 được đưa vào commit squash cuối cùng sau khi người dùng hoàn tất smoke test trên database dev mới.

### Task 8: Verify and hand off the frozen schema

- [x] **Step 1: Build and run all applicable tests**

Run:
```powershell
dotnet build QuanLyChoThuePhongTroWeb.sln
dotnet test QuanLyChoThuePhongTroWeb.sln --no-restore -m:1
```

*Lưu ý: Cờ `-m:1` bảo đảm kiểm thử solution chạy tuần tự, ngăn ngừa race condition giữa Infrastructure IntegrationTests và Web IntegrationTests khi cùng tự động migrate schema trên database test chung.*

Expected: build succeeds (0 errors), all test suites pass (257 passed, 0 failed, 0 skipped).

- [x] **Step 2: Check pending model changes**

Run the EF pending-model command from the repository guidelines:
```powershell
dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj
```

Expected: `No changes have been made to the model since the last migration.`

- [x] **Step 3: Inspect final schema and migration rollback on PostgreSQL test**

Verify 37 business tables, then roll back to Migration 1 (`dotnet ef database update 20260921155855_AddBillingOcrAndInvoicePayments`) and reapply Migration 2. Existing data and Migration 1 tables remain intact.

- [x] **Step 4: Freeze entity ownership**

Publish the entity/enums contract. Database foundation for Migration 2 is completed and frozen. Application Services, Web API, and UI workflows will be implemented in subsequent feature tasks.
