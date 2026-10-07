# Khach vang lai on master schema Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Port the current `thuan` guest UI preview to a branch from `origin/master`, using only the schema already in master.

**Architecture:** Reuse the public room Application service and EF read store, adapted to the master publishing flag and photo records. Keep booking, viewing, deposit, refund, and staff screens as read-only previews, with no POST actions. Preserve master role and branch scope rules.

**Tech Stack:** .NET 8, ASP.NET Core MVC/Razor, EF Core, xUnit.

**Spec:** Current uncommitted guest UI in the `thuan` working tree and user instruction in this chat. The old SPEC document targets the old database and is not authoritative for master schema.

## Global Constraints

- No migration, entity mapping, table, column, or snapshot changes.
- Do not run startup migrations against the existing local database.
- Do not overwrite or modify the source `thuan` working tree.
- Staff screens must use the current actor's branch scope.

## Review Focus

- Unpublished, occupied, or deleted rooms do not appear publicly.
- Missing or inactive photos fall back safely.
- Staff cannot see rooms outside assigned branches.
- Guest preview pages create no booking or payment records.
- Build and database-free unit tests work on master.

---

### Task 1: Port guest read-only screens

**Files:** `Application/Features/PublicRooms/*`, `Infrastructure/Persistence/Features/PublicRoomStore.cs`, `Web/Controllers/PhongController.cs`, guest Razor views, CSS and JS, and their unit tests.

- [x] Copy the current UI files from `thuan` to this worktree.
- [x] Adapt the query to `DuocDangTin`, active `AnhPhongTro`, decimal rent, and master DI.
- [x] Verify allowlist behavior and filtering in database-free unit tests; EF visibility query is checked in code review.

### Task 2: Port staff previews and navigation

**Files:** admin controllers/views/view model, `Web/Services/MenuService.cs`, login view, `Web/Program.cs`.

- [x] Keep master branch scope via `CurrentActorId`.
- [x] Wire navigation without replacing master entries.
- [x] Disable startup initializer by default in this branch; set `Database__InitializeOnStartup=true` only with an appropriate isolated database.

### Task 3: Verify and commit

- [x] Build solution and run Domain/Application unit tests without any DB.
- [x] Inspect changed paths and confirm zero schema/migration changes.
- [ ] Commit the new local branch; leave `thuan` untouched.
