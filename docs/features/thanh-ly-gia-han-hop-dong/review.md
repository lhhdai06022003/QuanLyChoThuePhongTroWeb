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
| S5 | Thấp | Biên lai của dòng cấn trừ chỉ bị ẩn nút, gọi thẳng URL vẫn in được | Chưa sửa (Q13 chỉ yêu cầu ẩn) |
| S6 | Thấp | Gia hạn sớm: thành viên vào sau ngày bắt đầu mới nhận `NgayChuyenDi` sớm hơn `NgayVao` | Chưa sửa (trường hợp biên) |
| S7 | Thấp | Màn thanh lý: AJAX lỗi thì nút Xác nhận bật lại dù chưa tích xác nhận nợ (server vẫn chặn) | Chưa sửa |
| S8 | — | `appsettings.example.json:52` còn nhắc `ContractAutoCloseJob` | Chưa sửa, cần người dùng duyệt (quy tắc giữ `appsettings*`) |

Lỗi tìm thấy khi thử giao diện: liên kết KH5 mở danh sách hóa đơn không lọc, vì `InvoiceViewService.GetEmployeeListAsync` bỏ sót `HopDongDaKetThucConNo` khi dựng lại bộ lọc. Đã sửa; `TenantRentalHistoryTests.InvoiceList_FilterEndedContractDebt` được làm chặt (thêm hóa đơn nợ của hợp đồng đang hoạt động, phải bị loại), đỏ trước khi sửa, xanh sau khi sửa.

## 3. Điểm đã soát kỹ

- **Tiền:** cấn trừ = min(cọc, tổng nợ cả chuỗi), phân bổ kỳ cũ trước; `decimal` xuyên suốt; hóa đơn/cọc lẻ bị chặn; `DaThu` đọc sau khi khóa `hoa_don` FOR UPDATE, cùng khóa với các luồng thu tiền (thu tay, minh chứng, SePay, VNPay); nợ đổi giữa lúc mở và lúc xác nhận bị từ chối (`TongNoLucMo`); còn nợ bắt buộc `XacNhanConNo`; lượt `ChoThanhToan` bị hủy, `DangDoiChieu` chặn thanh lý; dòng cấn trừ không hủy được.
- **`CanTruTienCoc` không lọt luồng khác:** thu tay chỉ nhận 0/1, SePay/VNPay/minh chứng ghi cứng `ChuyenKhoan`.
- **Không còn đường tự kết thúc hợp đồng:** job, use case, đăng ký DI đã gỡ; form sửa không còn `DaKetThuc`, server từ chối.
- **Phân quyền:** `actorId` là tham số đầu, fail closed; thao tác ghi kiểm `ContractSettle`/`ContractRenew`; người thuê ngoài chi nhánh trả 404; 3 POST mới có antiforgery.
- **Ranh giới tầng, XSS, UTC, xóa mềm:** đạt (chi tiết ở `.bangiao/danh-gia.md` của phiên, không lưu trong Git).

## 4. Kiểm chứng (lượt cuối, 05/11/2026)

| Lệnh hoặc thao tác | Kết quả |
|---|---|
| `dotnet build QuanLyChoThuePhongTroWeb.sln` | PASS |
| `dotnet test` Domain.UnitTests | PASS 140/140 |
| `dotnet test` Application.UnitTests | PASS 2524/2524 |
| `dotnet test` Infrastructure.IntegrationTests (DB `quanlyphongtro_test`) | PASS 698 + 1 skip có từ trước |
| `dotnet test` Web.IntegrationTests (cùng DB, chạy riêng) | PASS 743/743 |
| `dotnet ef migrations has-pending-model-changes` | PASS |
| `git diff --check` | PASS |
| Thử giao diện trên preview cổng 5009, DB phát triển, chỉ xem | PASS sau khi sửa lỗi KH5: danh sách hợp đồng, bộ lọc nhanh, tìm theo CCCD, Chi tiết, Thanh lý (chặn đúng khi tháng trả phòng chưa có hóa đơn công bố), Gia hạn (mặc định, mã `-GH1`), PDF biên bản (200, `application/pdf`), Lịch sử thuê, KH5, cảnh báo nợ cũ khi lập hợp đồng |

## 5. Chưa kiểm chứng

- Bấm **Thanh lý** và **Gia hạn** thật trên DB phát triển (luồng cuối tháng ở spec §9); chưa xem bố cục bên trong PDF biên bản.
- Máy chủ chạy múi giờ UTC (code mới không phụ thuộc; code cũ ở `HopDongService` còn `ToUniversalTime()`).
- Đồng thời thật: hai lượt thanh lý cùng lúc, thanh lý cùng lúc với webhook SePay/VNPay, gia hạn cùng lúc với lập hóa đơn. Mới có bằng chứng khóa dòng chạy được và bấm hai lần tuần tự bị từ chối.
- `ContractExpiryAlertJob` chạy thật với file `sent_contract_alerts.json`. Lần chạy đầu sẽ gửi thông báo cho mọi hợp đồng đã quá hạn từ trước (đúng spec §10 #2).
