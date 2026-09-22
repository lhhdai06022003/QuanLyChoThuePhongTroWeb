# Billing and OCR Database Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create Migration 1 for decimal money, meter-image OCR, invoice issuance history, and invoice payment confirmation.

**Architecture:** Person A owns every Domain entity, enum, EF mapping, and generated migration in this plan. Domain holds entities and state enums; Application owns rules and DTO contracts; Infrastructure owns EF configuration, transactions, PostgreSQL constraints, and the migration. No public UI or reservation feature is included.

**Tech Stack:** .NET 8, C#, EF Core 8, Npgsql/PostgreSQL, xUnit, Moq

---

## Ownership and file map

- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/` for approved entity types.
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Enums/AppEnums.cs` for new states.
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/` DTOs and calculations affected by `decimal`.
- Create: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Configurations/` for focused EF configurations.
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/ApplicationDbContext.cs` only to register DbSets/configurations.
- Create: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations/*AddBillingOcrAndInvoicePayments*`.
- Test: Domain and Application unit tests plus Infrastructure PostgreSQL integration tests.

The approved field and relationship contract is in `docs/superpowers/specs/2026-09-21-database-foundation-two-migrations-design.md`.

### Task 1: Establish the decimal contract

**Files:**
- Modify: existing money-bearing entities under `src/QuanLyChoThuePhongTroWeb.Domain/Entities/`
- Modify: affected DTOs under `src/QuanLyChoThuePhongTroWeb.Application/Features/`
- Modify: invoice, dashboard, AI, export, and payment calculations returning `double`
- Test: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/HoaDonCalculatorServiceTests.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/MoneyPrecisionMappingTests.cs`

- [ ] **Step 1: Add failing decimal calculation cases**

```csharp
[Fact]
public void Calculate_UsesDecimalWithoutBinaryRoundingDrift()
{
    decimal quantity = 12.345m;
    decimal unitPrice = 3_456.78m;
    decimal total = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
    Assert.Equal(42_674.96m, total);
}
```

- [ ] **Step 2: Run the focused tests and confirm the type mismatch/failure**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter HoaDonCalculatorServiceTests`

Expected: the new decimal contract does not compile or fails until production signatures use `decimal`.

- [ ] **Step 3: Convert approved money and quantity fields**

Use `decimal` for every field listed in the spec. Keep `PhongTro.DienTich` unchanged. Round money explicitly:

```csharp
var tongTien = decimal.Round(donGia * soLuong, 2, MidpointRounding.AwayFromZero);
```

- [ ] **Step 4: Configure PostgreSQL precision**

Money properties use `HasPrecision(18, 2)`; readings and quantities use `HasPrecision(18, 3)`.

- [ ] **Step 5: Run Domain/Application tests**

```powershell
dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
```

Expected: both pass.

- [ ] **Step 6: Commit the decimal conversion**

```powershell
git add src tests
git commit -m "refactor: use decimal for financial calculations"
```

### Task 2: Add meter review and OCR evidence to each image

**Files:**
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/DichVuDienNuocCuaPhong.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/AnhChiSoDongHo.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Enums/TrangThaiXuLyAnhChiSo.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/MeterReadingWorkflowTests.cs`

- [ ] **Step 1: Write failing transition tests**

Cover `Nhap → ChoDuyet → DaDuyet`, rejection with a reason, a reading below the previous value, readable and unreadable images, manual confirmation, and refusing to confirm an image twice.

- [ ] **Step 2: Define exact enums**

```csharp
public enum LoaiDongHo { Dien = 0, Nuoc = 1 }
public enum TrangThaiGhiNhan { Nhap = 0, ChoDuyet = 1, DaDuyet = 2, TuChoi = 3 }
public enum TrangThaiXuLyAnhChiSo
{
    MoiTaiLen = 0, DangXuLy = 1, DocDuoc = 2, Loi = 3,
    KhongDocDuoc = 4, CanChupLai = 5, DaXacNhan = 6, DaThayThe = 7
}
```

- [ ] **Step 3: Add the image entity and navigations**

One meter-reading record has many images. Each image stores one AI result and one optional human confirmation through `GiaTriAIGoiY`, `DoTinCay`, `ThongBaoLoi`, `NgayXuLy`, `GiaTriXacNhan`, `NguoiXacNhanId`, `NgayXacNhan`, `GhiChuXacNhan`, and `DuocChonLamChiSoChinhThuc`. Do not create `KetQuaNhanDangChiSo` or `LichSuDieuChinhChiSo`. Do not store provider, model, or raw AI response. Date property names omit `Utc` while values remain UTC.

If an image is unreadable, keep it as evidence, mark it for retake, and create a new image row. A confirmed image is immutable; replace it by marking the old image as replaced and confirming another image.

- [ ] **Step 4: Add database guards**

Use `numeric(18,3)` for AI and confirmed values, constrain confidence to `0..1`, require confirmation metadata for an official image, and add a filtered unique index so each meter type has at most one official image in a monthly reading.

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests --filter MeterReadingWorkflowTests`

Expected: PASS.

- [ ] **Step 6: Commit OCR domain types**

```powershell
git add src/QuanLyChoThuePhongTroWeb.Domain tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
git commit -m "feat: add meter OCR review model"
```
### Task 3: Add invoice issuance and audit history

**Files:**
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/HoaDon.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/LichSuTrangThaiHoaDon.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Enums/AppEnums.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/InvoiceWorkflowTests.cs`

- [ ] **Step 1: Write failing workflow tests**

Verify only `DaChot` invoices can request payment, partial payment is disabled by default, and its minimum is positive and no greater than the invoice total.

- [ ] **Step 2: Define states without changing existing persisted values**

```csharp
public enum TrangThaiPhatHanhHoaDon { Nhap = 0, ChoDuyet = 1, DaChot = 2, DaGui = 3, DaHuy = 4 }
public enum TrangThaiHoaDon { ChuaThanhToan = 0, DaThanhToan = 1, ThanhToanMotPhan = 2 }
```

- [ ] **Step 3: Add invoice fields and immutable history**

Every transition creates a history row containing old/new state, actor, date, and reason.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests --filter InvoiceWorkflowTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/QuanLyChoThuePhongTroWeb.Domain tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests
git commit -m "feat: add invoice issuance workflow"
```

### Task 4: Add invoice payment request and evidence entities

**Files:**
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/YeuCauThanhToanHoaDon.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/MinhChungThanhToanHoaDon.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/LichSuTrangThaiYeuCauThanhToanHoaDon.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/LichSuThanhToan.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Enums/AppEnums.cs`
- Test: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/InvoicePaymentConfirmationTests.cs`

- [ ] **Step 1: Write failing payment rules**

Test duplicate evidence confirmation, cumulative payments exceeding total, payment below minimum, and rejected evidence resubmission.

- [ ] **Step 2: Define request states**

```csharp
public enum TrangThaiYeuCauThanhToan
{
    ChoThanhToan = 0,
    DaBaoChuyen = 1,
    DangDoiChieu = 2,
    DaHoanTat = 3,
    TuChoi = 4,
    HetHan = 5,
    DaHuy = 6
}
public enum TrangThaiMinhChungThanhToan { ChoXacNhan = 0, DaXacNhan = 1, TuChoi = 2 }
```

- [ ] **Step 3: Add entities and confirmed-evidence link**

Do not create a generic polymorphic payment table. Cash and historical payments keep a nullable evidence link.

- [ ] **Step 4: Implement confirmation as one transaction**

Reload and lock invoice/request, validate cumulative amount, mark evidence, insert `LichSuThanhToan`, update state, and commit once.

- [ ] **Step 5: Run focused tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter InvoicePaymentConfirmationTests`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src tests/QuanLyChoThuePhongTroWeb.Application.UnitTests
git commit -m "feat: add invoice payment confirmation model"
```

### Task 5: Configure EF Core and generate Migration 1

**Files:**
- Create: configuration classes in `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Configurations/`
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: generated `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Migrations/*AddBillingOcrAndInvoicePayments*`
- Test: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/BillingFoundationMappingTests.cs`

- [ ] **Step 1: Write failing PostgreSQL mapping tests**

Assert 22 business tables, numeric precision, filtered room-month index, one active payment request per invoice, and unique confirmed transaction/evidence constraints.

- [ ] **Step 2: Register configurations**

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
```

Keep each aggregate mapping in a focused configuration class.

- [ ] **Step 3: Generate Migration 1**

```powershell
dotnet ef migrations add AddBillingOcrAndInvoicePayments --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj
```

Expected: migration, designer, and updated snapshot.

- [ ] **Step 4: Review migration operations**

Confirm invoice/readings backfills, explicit numeric precision, and no unexpected drops or renames.

- [ ] **Step 5: Apply to PostgreSQL integration database**

Run the EF database-update command from `AGENTS.md` against a database ending in `_test` or `_integration_test`.

Expected: success.

- [ ] **Step 6: Run mapping tests**

Run: `dotnet test tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests --filter BillingFoundationMappingTests`

Expected: PASS and 22 business tables.

- [ ] **Step 7: Commit Migration 1**

```powershell
git add src/QuanLyChoThuePhongTroWeb.Infrastructure tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
git commit -m "feat: add billing OCR database foundation"
```

### Task 6: Verify and freeze Migration 1

- [ ] **Step 1: Build**

Run: `dotnet build QuanLyChoThuePhongTroWeb.sln`

Expected: success.

- [ ] **Step 2: Run applicable suites**

Run Domain, Application, and Infrastructure tests with the dedicated PostgreSQL test connection.

Expected: all executed tests pass; missing database configuration is a limitation, not a pass.

- [ ] **Step 3: Check pending model changes**

Run the EF pending-model command from `AGENTS.md`.

Expected: no pending model changes.

- [ ] **Step 4: Record handoff**

Document 22 business tables and prohibit parallel edits to Migration 1 entity/mapping files until Migration 2 is merged.
