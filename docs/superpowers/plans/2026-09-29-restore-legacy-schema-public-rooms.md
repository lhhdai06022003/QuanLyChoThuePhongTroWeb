# Restore legacy schema and keep room browsing

**Goal:** Run the application against the schema ending at `20260802160246_ChangeChiTietHoaDonSoLuongToDouble`, with no new tables or columns, while retaining a read-only public room page.

**Agreed scope:** The user chose the pre-foundation schema and accepted pausing viewing requests, reservations, payments, contract conversion, and refunds. Existing data from the newer tables must remain recoverable in a local backup.

**Safety:** The database backup is `.agent/backups/QuanLyPhongTroDb_before_schema_restore_2026-09-29.dump`. Verify it before rollback. Never run a destructive command against a different database. The app must not be running during rollback.

## Tasks

- [x] Record the current migration list, row counts, and the IDs of rooms currently published. Confirm the backup is readable.
- [x] Build a local, ignored migration runner using the current EF assembly. Generate and review the SQL for migrating down to `20260802160246_ChangeChiTietHoaDonSoLuongToDouble`.
- [x] Restore `src/` and `tests/` to commit `71fc82a`, the last code state before the two foundation migrations. Keep the branch history and backup intact.
- [x] Add a read-only `/phong` list and `/phong/{id}` detail route. Query only rooms and branches that are not deleted and rooms with `TrangThaiPhong.Trong`. Restrict public visibility to configured room IDs; default to an empty list so deployments reveal nothing unexpectedly. Use only columns from the legacy schema.
- [x] Add the currently published room IDs to the ignored local configuration, update the example configuration without any credentials, and document that publishing is controlled by configuration rather than database columns.
- [x] Verify the solution builds and the database-free unit tests pass on the rewritten code.
- [x] Run the prepared down migration against `QuanLyPhongTroDb` only. Confirm the migration history ends at the legacy target, the newer tables and columns are absent, existing core tables retain their rows, and the public room page works.
- [x] Review the final diff for accidental removal of unrelated local files or secrets. Keep the backup ignored by Git.

## Risks and recovery

The down migrations remove the new feature tables, which currently contain room photos, a guest profile, a viewing slot, viewing requests, and a reservation. Those rows remain in the verified backup but cannot be used by the legacy schema. If rollback fails, stop and investigate before any further database operation; restore from the backup only after identifying the failure and confirming the intended target.
