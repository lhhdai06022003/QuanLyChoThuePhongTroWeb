# Review: Thanh lý, gia hạn hợp đồng và lịch sử thuê

- **Ngày:** 05/11/2026, nhánh `feature/thanh-ly-gia-han-hop-dong` (từ `master` = `d34fa90`).
- **Quy trình:** `/ship` (planner → coder → tester → reviewer) theo [plan.md](plan.md); sau đó sửa theo review và thử giao diện.
- **Kết luận reviewer:** ĐẠT CÓ ĐIỀU KIỆN (không có lỗi chặn). Điều kiện là sửa M1, S1–S3; cả bốn điểm đã sửa (mục 2).

## 1. Cổng xác nhận

- Không có migration, không sửa `Configurations/`, `ApplicationDbContext`, `Migrations/`. Thêm `PhuongThucThanhToan.CanTruTienCoc = 2` (lưu `int`); `has-pending-model-changes` PASS.
- Không đụng `Program.cs`, `*.csproj`, `appsettings*`, fixture test, test kiến trúc. `BranchScopeArchitectureTests` chỉ thêm 3 service mới vào danh sách có kiểm phạm vi, không thêm ngoại lệ.
- Mọi điểm về tiền và schema nằm trong danh sách người dùng đã xác nhận ở `plan.md` (A1, A2, B1–B10, C1–C4, Q1–Q14; Q2 = tính cả chuỗi gia hạn).

## 2. Phát hiện và cách xử lý

| Mã | Mức | Nội dung | Xử lý |
|---|---|---|---|
| M1 | Trung bình | `HopDongService.DeleteAsync` chỉ chặn hợp đồng đang hoạt động; xóa hợp đồng đã kết thúc (có hóa đơn, dòng cấn trừ, mắt xích gia hạn) làm nợ rơi khỏi chuỗi và KH5 | **Đã sửa** theo quyết định người dùng: chỉ xóa được hợp đồng **Đã hủy**; danh sách chỉ hiện nút Xóa ở dòng Đã hủy (spec §4.4). Test `Delete_EndedContract_IsRejected_AndKeptForArchive`, `Delete_CancelledContract_SoftDeletesAndReleasesRoom` |
| S1 | Thấp | Mã giao dịch nhập tay `CANTRU-...` chiếm unique index, làm thanh lý sau đó lỗi hệ thống | **Đã sửa:** `InvoicePaymentPolicy.CanTruMaGiaoDichPrefix`, `ValidateMaGiaoDich` chặn tiền tố như `SEPAY-`/`VNPAY-`. Thêm dữ liệu vào `InvoiceManualCodePrefixTests` |
| S2 | Thấp | Body JSON rỗng/sai ở `XacNhanThanhLy`, `XacNhanGiaHan` gây NullReferenceException (500) | **Đã sửa:** trả 400 "Dữ liệu gửi lên không hợp lệ.". Test `ContractSettlementRenewalTests.NullJsonBody_Is400_AndNothingChanges` |
| S3 | Thấp | Gia hạn nhận giá thuê/cọc lẻ, rồi thanh lý hợp đồng mới lại chặn cọc lẻ | **Đã sửa:** gia hạn bắt buộc số đồng nguyên. Test `GiaHanHopDongServiceTests.FractionalRentOrDeposit_Fails_AndNothingChanges` |
| S4 | Thấp | Tìm CCCD phân biệt hoa thường | Không sửa: CCCD/SĐT thật chỉ có chữ số; test đã sửa theo dữ liệu thật |
| S5 | Thấp | Biên lai của dòng cấn trừ chỉ bị ẩn nút, gọi thẳng URL vẫn in được | **Đã sửa** 05/11/2026: `InvoiceReceiptService` coi dòng `CanTruTienCoc` là không có biên lai: xem (khách, quản lý) trả NotFound sau khi kiểm quyền, gửi lại trả lỗi "Khoản cấn trừ tiền cọc không có biên lai thu tiền." và không gửi thư. Test `InvoiceReceiptServiceTests.DepositOffsetRow_*` (4) |
| S6 | Thấp | Gia hạn sớm: thành viên vào sau ngày bắt đầu mới nhận `NgayChuyenDi` sớm hơn `NgayVao` | **Đã sửa** 05/11/2026: thành viên vào sau mốc kết thúc mới của hợp đồng cũ thì dòng cũ đóng đúng ngày vào, dòng mới giữ ngày vào thật. Test `GiaHanHopDongServiceTests.EarlyRenewal_MemberJoinedAfterNewStart_KeepsJoinDate_AndNeverMovesOutBeforeJoining` (đỏ trước khi sửa) |
| S7 | Thấp | Màn thanh lý: AJAX lỗi thì nút Xác nhận bật lại dù chưa tích xác nhận nợ (server vẫn chặn) | **Đã sửa** 05/11/2026: nút chỉ bật khi không đang gửi và (không còn nợ hoặc đã tích xác nhận); tích lại trong lúc đang gửi cũng không bật. Kiểm trên trình duyệt bằng cách chạy lại script của view với `$.ajax`/`Swal` giả (DB phát triển không còn hợp đồng thanh lý được), chưa bấm với lỗi server thật |
| S8 | — | `appsettings.example.json:52` còn nhắc `ContractAutoCloseJob` | Chưa sửa, cần người dùng duyệt (quy tắc giữ `appsettings*`) |

Lỗi tìm thấy khi thử giao diện: liên kết KH5 mở danh sách hóa đơn không lọc, vì `InvoiceViewService.GetEmployeeListAsync` bỏ sót `HopDongDaKetThucConNo` khi dựng lại bộ lọc. Đã sửa; `TenantRentalHistoryTests.InvoiceList_FilterEndedContractDebt` được làm chặt (thêm hóa đơn nợ của hợp đồng đang hoạt động, phải bị loại), đỏ trước khi sửa, xanh sau khi sửa.

## 3. Điểm đã soát kỹ

- **Tiền:** cấn trừ = min(cọc, tổng nợ cả chuỗi), phân bổ kỳ cũ trước; `decimal` xuyên suốt; hóa đơn/cọc lẻ bị chặn; `DaThu` đọc sau khi khóa `hoa_don` FOR UPDATE, cùng khóa với các luồng thu tiền (thu tay, minh chứng, SePay, VNPay); nợ đổi giữa lúc mở và lúc xác nhận bị từ chối (`TongNoLucMo`); còn nợ bắt buộc `XacNhanConNo`; lượt `ChoThanhToan` bị hủy, `DangDoiChieu` chặn thanh lý; dòng cấn trừ không hủy được.
- **`CanTruTienCoc` không lọt luồng khác:** thu tay chỉ nhận 0/1, SePay/VNPay/minh chứng ghi cứng `ChuyenKhoan`.
- **Không còn đường tự kết thúc hợp đồng:** job, use case, đăng ký DI đã gỡ; form sửa không còn `DaKetThuc`, server từ chối.
- **Phân quyền:** `actorId` là tham số đầu, fail closed; thao tác ghi kiểm `ContractSettle`/`ContractRenew`; người thuê ngoài chi nhánh trả 404; 3 POST mới có antiforgery.
- **Ranh giới tầng, XSS, UTC, xóa mềm:** đạt (chi tiết ở `.bangiao/danh-gia.md` của phiên, không lưu trong Git).

## 4. Kiểm chứng (lượt cuối, 05/11/2026)

### 4.1 Toàn bộ module (/ship + S5–S7)

| Lệnh hoặc thao tác | Kết quả |
|---|---|
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS |
| `dotnet test` Domain.UnitTests | PASS 140/140 |
| `dotnet test` Application.UnitTests | PASS 2524/2524 (lượt sau S5–S7: 2534/2534) |
| `dotnet test` Infrastructure.IntegrationTests (DB `quanlyphongtro_test`) | PASS 698 + 1 skip có từ trước |
| `dotnet test` Web.IntegrationTests (cùng DB, chạy riêng) | PASS 743/743 |
| `dotnet ef migrations has-pending-model-changes` | PASS |
| `git diff --check` | PASS |
| Thanh lý thật trên DB phát triển (05/11/2026, người dùng chọn `HD-TD1-2601-001`, cọc 0 ₫, hóa đơn T4/2026 còn nợ 2.444.500 ₫) | PASS: sửa ngày kết thúc về 30/04/2026 qua form sửa thì hết bị chặn; nút Xác nhận khóa tới khi tích xác nhận nợ; sau thanh lý hợp đồng Đã kết thúc, thành viên có ngày chuyển đi 30/04/2026, phòng P.101 về Trống; KH5 thêm 1 khoản (tổng 4.655.500 ₫ = 2.444.500 + 2.211.000 có từ trước); bộ lọc hóa đơn và `/NguoiThue/NoCu/7` trả đúng khoản nợ; lịch sử thuê hiện hợp đồng và nợ; biên bản PDF (200, 1 trang, đủ các mục); gửi thanh lý lần hai trả 400; log server không có lỗi |
| Gia hạn thật trên DB phát triển (05/11/2026, người dùng chọn `HD-GV2-2610-001`, 01/10–31/12/2026) | PASS: form mặc định tháng 1/2027 → 31/03/2027, giá 1.900.000 ₫, chép điều khoản; tạo `HD-GV2-2610-001-GH1` (id 6) Đang hoạt động, thành viên vào 01/01/2027; hợp đồng cũ hiện "Đã gia hạn → …-GH1", thành viên cũ chuyển đi 31/12/2026; phòng 102 vẫn Đã thuê; chuỗi hợp đồng và lịch sử thuê (1 lần thuê, 2 hợp đồng) đúng; gửi lần hai trả 400; log server sạch. Lúc thử, bấm Xác nhận gia hạn là chạy luôn, không có hộp thoại xác nhận như thanh lý; **đã thêm** hộp thoại (tóm tắt mã mới, thời hạn, giá thuê, tiền cọc, "không hoàn tác được"), thử trên `-GH1`: hộp thoại hiện đúng, bấm Hủy không tạo gì; `ContractSettlementRenewalTests` + `TenantRentalHistoryTests` 25/25 PASS |
| Thử giao diện trên preview cổng 5009, DB phát triển, chỉ xem | PASS sau khi sửa lỗi KH5: danh sách hợp đồng, bộ lọc nhanh, tìm theo CCCD, Chi tiết, Thanh lý (chặn đúng khi tháng trả phòng chưa có hóa đơn công bố), Gia hạn (mặc định, mã `-GH1`), PDF biên bản (200, `application/pdf`), Lịch sử thuê, KH5, cảnh báo nợ cũ khi lập hợp đồng |

### 4.2 Cải tiến giao diện nhóm 1 (05/11/2026, theo plan-giao-dien-nhom-1.md)

| Lệnh hoặc thao tác | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS | 0 Error, 0 Warning liên quan |
| `dotnet test` Domain.UnitTests | PASS | 140/140 |
| `dotnet test` Application.UnitTests | PASS | 2544/2544 (+10 test mới cho `HopDongCodeChain`, `ThanhLyCalculator`, `HopDongService`, `ThanhLyHopDongService`) |
| `dotnet test` Infrastructure.IntegrationTests | PASS | 698 passed, 1 skipped; sau khi bổ sung 2 test store (nghiệm thu): 700 passed, 1 skipped |
| `dotnet test` Web.IntegrationTests | PASS | 745/745 (+2 test mới cho `CapNhatNgayTraPhong` và `Menu_StaysActive_OnSubPages`) |
| `dotnet ef migrations has-pending-model-changes` | PASS | Model đồng bộ tuyệt đối, 0 migration rác |
| `git diff --check` | PASS | Chỉ cảnh báo CRLF/LF của git |
| Thử giao diện preview cổng 5009 (chỉ xem, đã chụp 6 ảnh minh chứng) | PASS | - Danh sách hợp đồng: có cột "Hết hạn", badge "Đã gia hạn" ở HD-GV2-2610-001 (id 1, trỏ `-GH1` id 6)<br>- Menu "⋯" có nhanh "Thanh lý" và "Gia hạn" cho hợp đồng Đang hoạt động<br>- Màn thanh lý: card "Ngày trả phòng" (input date + nút Cập nhật) và nút "Lập hóa đơn T3/2027"<br>- Màn hóa đơn: nhận URL param, tự mở modal xem trước, chọn sẵn chi nhánh/kỳ và phòng mục tiêu<br>- Menu trái: giữ active "Hợp đồng" và "Người thuê" trên mọi màn con |

## 5. Chưa kiểm chứng

- Thanh lý thật đã thử nhưng cọc 0 ₫ nên chưa có dòng cấn trừ thật (cần hợp đồng có cọc > 0; cọc không sửa được khi hợp đồng đang hoạt động).
- Chưa bấm nút "Cập nhật" đổi ngày trả phòng thật trên DB phát triển qua giao diện (ràng buộc không ghi dữ liệu thật; đã có unit test và integration test kiểm tra logic ghi, lock và chặn khi có hóa đơn).
- Chưa bấm nút xác nhận tạo hóa đơn phát sinh từ modal xem trước trên DB phát triển (đã kiểm tra modal mở và load đúng dữ liệu phòng).
- Nghiệm thu (Claude Code, 05/11/2026): bổ sung test còn thiếu của mục 3.4 trong `HopDongLifecycleStoreTests`: `DataTable_MaHopDongKeTiep_SkipsCancelledAndDeleted_AndFillsPhongTroId` (bỏ qua gia hạn đã hủy và đã xóa mềm, trỏ tới gia hạn kế tiếp còn hiệu lực, có `PhongTroId`) và `DataTable_SortByThoiDiemKetThuc`. Chạy lại toàn bộ: Domain 140, Application 2544, Infrastructure 700 + 1 skip, Web 745, `has-pending-model-changes` PASS.
- Trên DB phát triển, phòng P.101 chưa chốt điện nước T12/2026 nên nằm ở tab "Chưa chốt điện nước"; việc tích sẵn đúng phòng ở tab "Sẵn sàng" chưa thấy trên giao diện. Chưa có hợp đồng quá hạn để xem huy hiệu "Quá hạn N ngày" (unit test có).
- Hai lỗi thấy khi thử thật, **đã sửa** 05/11/2026: (1) lịch sử thuê ghi "Tổng số tháng ở 3" cho hợp đồng 01/01–30/04/2026, vì `HopDongTraCuuService.TinhTongSoThang` lấy mốc kết thúc là 00:00 ngày cuối (cách form sửa lưu ngày) rồi chia 30 và làm tròn xuống. Nay đếm theo ngày lịch VN, tính trọn ngày đầu và ngày cuối, cắt ở hôm nay, đổi ra tháng bằng 30,4375 ngày và làm tròn gần nhất; test `HopDongTraCuuServiceTests.History_MonthCount_IncludesLastDay` (5 ca, đỏ trước khi sửa). (2) Biên bản in "Trừ tiền cọc: -0 đ" khi không cấn trừ, nay in "0 đ". Kiểm lại trên DB phát triển: lịch sử thuê ra 4 tháng, biên bản in "Trừ tiền cọc: 0 đ". Application 2529/2529, `BienBanThanhLyExporterTests` 2/2, `TenantRentalHistoryTests` + `ContractSettlementRenewalTests` 25/25 PASS.
- Máy chủ chạy múi giờ UTC (code mới không phụ thuộc; code cũ ở `HopDongService` còn `ToUniversalTime()`).
- Đồng thời thật: hai lượt thanh lý cùng lúc, thanh lý cùng lúc với webhook SePay/VNPay, gia hạn cùng lúc với lập hóa đơn. Mới có bằng chứng khóa dòng chạy được và bấm hai lần tuần tự bị từ chối.
- `ContractExpiryAlertJob` chạy thật với file `sent_contract_alerts.json`. Lần chạy đầu sẽ gửi thông báo cho mọi hợp đồng đã quá hạn từ trước (đúng spec §10 #2).
