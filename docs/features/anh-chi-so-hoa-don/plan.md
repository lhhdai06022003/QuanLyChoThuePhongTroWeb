# Ảnh chỉ số, OCR và phát hành hóa đơn — Implementation Plan (phương án 1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Hoàn thành luồng ảnh điện/nước → OCR gợi ý → nhân viên duyệt → nháp → Admin chốt → công bố/email theo `spec.md` cùng thư mục, trên schema hiện có.

**Architecture:** Giữ `Role.NhanVien`; mọi nhân viên có phân công `IsActive` đúng chi nhánh cùng quyền thao tác nhân viên, còn Admin chốt/trả lại. Application tập trung kiểm tra role/chi nhánh, điều phối workflow và gọi `IEmailService` sau khi công bố đã commit; Infrastructure triển khai EF/Cloudinary/Gemini/MailKit, Web chuyển request/response. Dùng các cột ảnh, chỉ số, ghi chú và hóa đơn hiện có; không tạo migration trong giai đoạn này. Bảng quyền từng nhân viên và việc thay hai JSON state store của job nhắc email thuộc giai đoạn sau.

**Tech Stack:** .NET 8, ASP.NET Core MVC/API, EF Core/PostgreSQL, Cloudinary, Gemini REST, MailKit, xUnit.

---

## 0. Chuẩn bị và ranh giới

- [x] Đọc `spec.md`, `README.md`, `docs/roadmap-60-ngay.md` và kiểm tra `git status --short`. Giữ nguyên hai ERD đang sửa sẵn và mọi thay đổi ngoài phạm vi.
- [x] Chạy test theo lát cắt: viết test thất bại có ý nghĩa, chạy để thấy thất bại, triển khai tối thiểu, chạy lại, kiểm tra diff. Chỉ commit file thuộc lát cắt sau khi review.
- [x] Cấu hình `QLCTPT_TEST_CONNECTION_STRING` cho PostgreSQL riêng có tên database kết thúc `_test` hoặc `_integration_test`. Không dùng database phát triển. Không ghi secret vào tài liệu/log.
- [x] Giữ nguyên schema giai đoạn này. Migration nháp `AddMeterReadingProposalAndAudit` được EF báo `Pending` trên database Web đang cấu hình; đã gỡ hai tệp migration và khôi phục snapshot. Chưa kiểm tra các database khác; không áp dụng migration đó.

## 1. Đối chiếu schema hiện có, không tạo migration

**Files**
- Read: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/AnhChiSoDongHo.cs`
- Read: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/DichVuDienNuocCuaPhong.cs`
- Read: EF configurations, migration nền, `ApplicationDbContextModelSnapshot.cs` và integration tests mapping hiện có

- [x] Xác nhận mapping của `GiaTriAIGoiY`, `GiaTriXacNhan`, `NguoiXacNhanId`, `NgayXacNhan`, `GhiChuXacNhan`, cờ ảnh chính thức, `ChiSoDienMoi`, `ChiSoNuocMoi`, `GhiChuDuyet`, `NguoiDuyetId` và `NgayDuyet`; xác nhận unique index ảnh chính thức hiện có.
- [x] Ghi rõ giới hạn: không có cột số khách đề xuất tách biệt, nguồn/người/thời điểm riêng từng loại khi nhập tay hoặc bảng lịch sử mọi lần sửa chỉ số. Lý do nhập tay dùng `GhiChuXacNhan` khi có ảnh hoặc `GhiChuDuyet` khi không có ảnh; không suy diễn dữ liệu mà schema không lưu.
- [x] Đã kiểm tra và gỡ migration nháp `AddMeterReadingProposalAndAudit` cùng phần thay đổi snapshot tương ứng. Database Web đang cấu hình báo `Pending` trước khi gỡ; EF hiện không có pending model changes. Không tạo migration thay thế trong plan này.
- [x] Chạy integration tests mapping hiện có trên PostgreSQL test; không sửa migration nền hoặc database phát triển. *(Infrastructure integration tests 128/128 PASS trên `quanlyphongtro_test`; EF model không có pending changes.)*

## 2. Domain workflow và bất biến

**Files**
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/AnhChiSoDongHo.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/DichVuDienNuocCuaPhong.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Domain/Entities/HoaDon.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/MeterReadingWorkflowTests.cs`
- Modify: `tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests/InvoiceWorkflowTests.cs`

- [x] Viết test gợi ý AI không đổi số chính thức; số 0 hợp lệ, số âm và độ tin cậy ngoài 0–1 bị chặn; ảnh mới chưa tự thay ảnh chính thức.
- [x] Chạy `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests --filter FullyQualifiedName~MeterReadingWorkflowTests`; kỳ vọng test mới thất bại trước sửa.
- [x] Bổ sung method retry để ảnh `KhongDocDuoc`/`Loi` chưa xác nhận có thể trở lại `DangXuLy`; xác nhận/thay ảnh theo bất biến hiện có. Ảnh mới chỉ thay ảnh cũ sau khi nhân viên duyệt. Không dùng entity để truy vấn quyền hoặc DB.
- [x] Viết test `Nhap → ChoDuyet → DaChot → DaGui`, trả lại `ChoDuyet → Nhap` có lý do, chốt thẳng từ `Nhap` hoặc lặp bị từ chối, `DaHuy` là cuối. Sửa method Domain và test cũ theo bất biến mới.
- [x] Chạy toàn `Domain.UnitTests`; kỳ vọng pass.

## 3. Kiểm tra role và phân công chi nhánh (phương án 1)

**Files**
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Abstractions/Security/IEmployeeAccessService.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Common/Security/EmployeeActionCodes.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/NhanViens/Persistence/IEmployeeBranchStore.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/NhanViens/Services/EmployeeAccessService.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/EmployeeBranchStore.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/DependencyInjection.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/DependencyInjection.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/EmployeeAccessTests.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/EmployeeBranchStoreTests.cs`

- [x] Định nghĩa một điểm kiểm tra `CanPerformAsync(actorId, chiNhanhId, action)` trong Application. Giữ mã hành động cố định cho upload, duyệt chỉ số, tạo/gửi nháp, công bố, hủy và chốt; hiện mọi hành động nhân viên được phép với mọi phân công `IsActive` đúng chi nhánh, trừ chốt/trả lại chỉ Admin. Chưa đọc grant hay tạo bảng quyền.
- [x] Test Admin được thao tác mọi chi nhánh và chốt/trả lại; nhân viên `IsActive` đúng chi nhánh được cùng thực hiện mọi thao tác nhân viên, nhân viên chi nhánh khác hoặc phân công bị thu hồi bị từ chối; khách không qua được cổng nhân viên. Nhân viên được tự duyệt ảnh mình gửi.
- [x] Store chỉ trả thông tin tài khoản và phân công còn hiệu lực; Application kiểm tra role/hành động bằng ActorId từ phiên đăng nhập, suy chi nhánh từ phòng/hợp đồng đã tải từ DB. Không tin `ChiNhanhId` hoặc role do client gửi. Controller/API chỉ xử lý HTTP, mọi mutation đều đi qua cùng điểm kiểm tra.
- [x] Chạy `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests --filter FullyQualifiedName~EmployeeAccessTests` và integration test truy vấn phân công; kỳ vọng cùng nhân viên được phép ở chi nhánh 1 nhưng bị chặn ở chi nhánh 2.

### Mốc nghiệm thu Mục 0–3 — hoàn thành ngày 26/09/2026

- [x] Domain 56/56, Application 158/158, Infrastructure 128/128 và Web 34/34 tests PASS.
- [x] Build toàn solution đạt 0 warning/0 error; EF không có pending model changes; `git diff --check` PASS.
- [x] Sáu finding review L1–L6 đã được khắc phục và kiểm chứng; dừng đúng trước Mục 4.

## 4. Hợp đồng ảnh và OCR *(hoàn thành sau review lần 3 và thử tay ngày 27/09/2026)*

**Files**
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/DTOs/MeterWorkflowDtos.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Persistence/IMeterImageStore.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Services/IMeterReadingWorkflowService.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/MeterImageStore.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Abstractions/Services/IMeterImageStorageService.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Abstractions/Services/IMeterOcrService.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/DTOs/MeterOcrResult.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Infrastructure/ExternalServices/Storage/CloudinaryMeterImageStorageService.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Infrastructure/ExternalServices/AiAssistants/GeminiMeterOcrService.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/ExternalServices/GeminiMeterOcrServiceTests.cs`

- [x] DTO upload nhận phòng/kỳ/loại và `UploadFile`; DTO phản hồi tách `GiaTriAIGoiY` và `GiaTriXacNhan`. Không tạo trường số khách đề xuất riêng hoặc đưa `IFormFile`/EF/HTTP vào Application.
- [x] Test HTTP handler giả cho Gemini: ảnh rõ/không đọc được, giá trị 0, âm, phản hồi trống, timeout, HTTP lỗi; không gọi mạng thật. Adapter trả kết quả có cấu trúc, không gọi DbContext, không tự sinh độ tin cậy.
- [x] Lưu ảnh và `AnhChiSoDongHo` trước, tự chạy OCR rồi Application ghi `GiaTriAIGoiY`/lỗi. OCR lỗi vẫn giữ ảnh. Retry của nhân viên cập nhật OCR trên ảnh chưa xác nhận; khách có thể tải ảnh mới để xử lý ảnh/OCR sai.
- [x] Storage trả URL/PublicId, đọc lại ảnh tin cậy cho OCR retry và xử lý tệp mồ côi khi DB lưu thất bại. Đăng ký adapter trong `AddInfrastructure`; chạy test adapter.

## 5. Duyệt ảnh và số chính thức *(hoàn thành sau review lần 3 và thử tay ngày 27/09/2026)*

**Files**
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Services/MeterReadingWorkflowService.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Services/DienNuocService.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Services/IDienNuocService.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/DienNuocs/Persistence/IDienNuocStore.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/DienNuocStore.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/MeterReadingWorkflowTests.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/MeterImageStoreTests.cs`

- [x] Test khách đúng/sai hợp đồng, nhân viên đúng/sai chi nhánh hoặc mất phân công, ảnh lỗi còn xem được, ảnh khách tải không tự thành số chính thức, nhập tay thiếu lý do bị từ chối.
- [x] Trong transaction xác nhận ảnh: khóa bản ghi kỳ/ảnh cùng loại, bỏ cờ chính thức cũ, xác nhận ảnh mới, cập nhật số chính thức. Lưu người/ngày xác nhận trên ảnh bằng trường hiện có; unique index chặn hai ảnh chính thức khi request đồng thời.
- [x] Nhập tay không tạo ảnh; bỏ cờ ảnh chính thức cũ cùng transaction. Bắt buộc lý do trong `GhiChuXacNhan` nếu xác nhận trên ảnh không đọc được, hoặc `GhiChuDuyet` nếu không có ảnh; không xóa lý do khi duyệt kỳ. Chặn giá trị âm, nhỏ hơn kỳ trước và kỳ đã khóa. Chỉ duyệt cả kỳ khi hai loại có ảnh chính thức hoặc chỉ số thủ công được cung cấp trong thao tác duyệt; không dùng số 0 mặc định làm bằng chứng xác nhận.
- [x] Chuyển endpoint `SaveChotDienNuoc` cũ sang use case có kiểm tra role và chi nhánh để không đi tắt. Chạy Application và PostgreSQL tests của mục này.
- [x] Use case sửa số của ảnh đã xác nhận kèm lý do (`CorrectConfirmedImageAsync`): nhân viên đúng chi nhánh hoặc Admin sửa ảnh chính thức, kiểm tra kỳ sau, hóa đơn khóa, số hợp lệ, cập nhật kỳ và tính lại hóa đơn nháp.

## 6. Nháp và phát hành *(hoàn thành đợt 1, review độc lập và thử tay ngày 27/09/2026)*

**Files**
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/HoaDonService.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/IHoaDonService.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Persistence/IHoaDonStore.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/HoaDonStore.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/IInvoiceIssuanceService.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/InvoiceIssuanceService.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/InvoiceDraftWorkflowTests.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests/Persistence/InvoiceDraftConcurrencyTests.cs`

- [x] Test preview thiếu điện/nước duyệt, trùng hợp đồng/kỳ, hai request đồng thời; Admin hoặc mọi nhân viên `IsActive` đúng chi nhánh được tạo hàng loạt.
- [x] Sửa `PhatSinhHoaDonAsync` tạo `Nhap` từ kỳ `DaDuyet`, dùng calculator và giữ dòng tiền hiện có. Sửa số trước chốt chỉ tính lại điện/nước của `Nhap`; `ChoDuyet` phải được Admin trả lại trước khi sửa.
- [x] Test nhân viên `IsActive` đúng chi nhánh được gửi duyệt, công bố và hủy đủ điều kiện; chỉ Admin chốt/trả lại. Nhân viên khác chi nhánh bị từ chối. Chặn sửa sau chốt qua `UpdateHoaDon` và mọi endpoint cũ.
- [x] Hủy chỉ khi chưa có khoản tiền hoặc yêu cầu/minh chứng đang đối soát; ghi `DaHuy + IsDeleted` và lịch sử. Chặn thao tác gửi lại email cho bản hủy; email đã gửi không thể thu hồi. Cấp mã `-R1/-R2` trong transaction, không thêm FK thay thế. Test PostgreSQL chống trùng và lịch sử bản hủy.

> **Ghi chú nghiệm thu đợt 1:** Phần "công bố" ở dòng 3 và "chặn gửi lại email cho bản hủy" ở dòng 4 thuộc Mục 7–8 (đợt 2); đợt 1 chỉ làm nháp → chờ duyệt → chốt/trả lại, hủy và sự cố cộng hóa đơn theo tháng `NgayXuLy`. Kiểm chứng độc lập: build 0/0; Domain 71, Application 283, Web 47, Infrastructure 205 (5/5 lần) PASS trên `quanlyphongtro_test`; EF không có thay đổi model. Người dùng thử tay đạt 5 bước trên DB dev.

## 6B. Phân công nhân viên theo chi nhánh và khóa màn quản trị (đầu đợt 2 — đợt 2A) *(hoàn thành, review độc lập và thử tay ngày 28/09/2026)*

> Theo `spec.md` §12, chốt ngày 27/09/2026. Không đổi schema; dùng bảng `nhan_vien_chi_nhanh` hiện có.

- [x] Domain: `NhanVienChiNhanh.ThuHoi` và `PhanCongLai` giữ invariant (thu hồi cần lý do, không thu hồi lặp; phân công lại xóa thông tin thu hồi). Test Domain đỏ trước rồi xanh.
- [x] Application: `IEmployeeBranchAssignmentService` (danh sách, chi tiết, danh sách chi nhánh, phân công, thu hồi). Chỉ Admin đang hoạt động trong DB; đích phải là `NhanVien` chưa xóa; chi nhánh chưa xóa. Phân công lại dùng lại dòng cũ; lệnh lặp không đổi dữ liệu. Test với fake store.
- [x] Infrastructure: khóa `nguoi_dung` của nhân viên đích bằng `FOR UPDATE` trong transaction rồi đọc/ghi dòng phân công; truy vấn chiếu cho danh sách/chi tiết. Test PostgreSQL hai lệnh phân công đồng thời chỉ tạo một dòng (dò `pg_locks`, không `Task.Delay`).
- [x] Khóa `NguoiDungController` (trừ `DangNhap`/`DangXuat`) và `ChiNhanhsController` chỉ Admin; menu ẩn mục quản trị với nhân viên; `OnValidatePrincipal` kiểm tra lại tài khoản (tồn tại, chưa xóa, `IsActive`, role trùng claim) qua use case Application. Báo người B về thay đổi phiên đăng nhập dùng chung.
- [x] Web: trang `/QuanLyNhaTro/PhanCongChiNhanh` (bảng nhân viên, lọc chi nhánh, modal phân công/thu hồi có lý do), nút "Chi nhánh" ở màn Quản lý tài khoản, banner "chưa được phân công" trên màn Điện nước/Hóa đơn/Sự cố. Test Web: nhân viên bị chặn (chuyển về DangNhap, xem spec §12.3) ở mọi route quản trị, antiforgery, `DangNhap` ẩn danh, phiên cũ mất hiệu lực sau khi khóa/đổi role.
- [x] Kèm hai P3 của đợt 1: mốc tháng dùng khoảng nửa mở `[đầu tháng, đầu tháng sau)` ở cả ba chỗ tính tháng (có test mốc `23:59:59.5` giờ VN); `GetContractIdsForTenantRoomPeriodsAsync` dùng tham số `periods` hoặc đổi tên đúng hành vi.
- [x] Thử tay trên DB dev theo `spec.md` §12.6 rồi mới tick mục này.

> Nghiệm thu 28/09/2026: Domain 85, Application 312, Infrastructure 210 (3/3 lần), Web 90 (DB `quanlyphongtro_test`); EF không báo thay đổi model. Thử tay đủ 5 bước §12.6. Sau thử tay: bỏ ô lý do khi phân công và sửa lỗi modal Bootstrap chặn gõ lý do thu hồi (`data-bs-focus="false"`). Q1: tài khoản khách vãng lai bị kiểm tra phiên từ chối — đã ghi để báo người B. Việc thấp dời sang đầu đợt 2B: thêm ca "Admin gọi được" cho `CreateApi`/`EditApi`/`ThemChiNhanhMoi`/`CapNhatChiNhanh`/`ThuHoi`, ca nhân viên gọi `RevokeAsync`, khẳng định số log trong test phục hồi phiên.

## 7. Công bố trên cổng *(hoàn thành đợt 2B, review độc lập và thử tay ngày 29/09/2026)*

> **Ghi chú đợt 1:** Đã làm trước trong đợt 1: lọc cổng khách (danh sách, chi tiết, VietQR không lộ nháp hay đã hủy chưa gửi), chặn ThuTien khi chưa DaGui.
>
> **Đợt 2B (Mục 7–8):** kế hoạch chi tiết tại `docs/KeHoachHienTai/2026-09-28-ke-hoach-dot-2b-cong-bo-hoa-don.md`. Quyết định 28/09/2026: hạn thanh toán là cuối ngày giờ VN, gỡ modal "Gửi email hàng loạt" cũ, link thông báo `/KhachThue/HoaDon?hoaDonId=`, hạn có trong email (spec §5.7–5.9, §6). Chỉ tick Mục 7–8 sau khi người dùng thử tay đạt.

**Files**
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/InvoiceIssuanceService.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/HoaDonController.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachThue/Controllers/HoaDonController.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/Areas/InvoiceVisibilityTests.cs`

- [x] Test khách chỉ thấy `DaGui` và bản `DaHuy` từng công bố ở danh sách, GetById, PDF; không thấy `Nhap`, `ChoDuyet`, `DaChot`. Bản hủy không có VietQR/thao tác thanh toán.
- [x] Transaction công bố cho từng hóa đơn: khóa hàng hóa đơn hoặc cập nhật có điều kiện `DaChot → DaGui`, rồi tạo một hàng `ThongBao` cho khách trên cổng, không có cột email. Một lệnh gửi hàng loạt nhận danh sách `HoaDonId`; lỗi từng hóa đơn được trả riêng, không hoàn tác hóa đơn khác. Sau commit, Application gửi email trực tiếp nếu có địa chỉ và trả kết quả ngay. Với `DaGui`, trả "đã công bố", không đổi `NgayGui`, không tạo thông báo/lịch sử mới hoặc tự gửi email lần nữa.
- [x] Khóa đường tạo yêu cầu thanh toán của module 2 chỉ với bản hiệu lực đã gửi. Chạy test Application/Web cho SMTP lỗi sau commit nhưng cổng vẫn hiển thị.

## 8. Gửi email hóa đơn trực tiếp, không lưu trạng thái *(hoàn thành đợt 2B, review độc lập và thử tay ngày 29/09/2026)*

> **Ghi chú đợt 1:** Đã làm trước trong đợt 1: sửa truy vấn nhắc nợ `GetOverdueInvoicesAsync` (chỉ lấy `DaGui`, `!IsDeleted`, chưa thanh toán, theo hạn `HanThanhToan` hoặc `NgayGui`).

**Files**
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/IInvoiceIssuanceService.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Application/Features/HoaDons/Services/InvoiceIssuanceService.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/HoaDonController.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Infrastructure/Persistence/Features/HoaDonStore.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Application.UnitTests/InvoiceEmailDispatchTests.cs`
- Create: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/Areas/InvoiceBulkSendTests.cs`

- [x] Viết test lô có email thành công, email lỗi và hóa đơn không có địa chỉ: kết quả trả riêng từng `HoaDonId`; tất cả hóa đơn hợp lệ vẫn `DaGui` và có thông báo cổng. Không ghi kết quả SMTP, số lần thử hoặc lỗi vào DB.
- [x] Application tạo PDF từ bản hóa đơn đã chốt và gọi `IEmailService` sau commit công bố, rồi trả kết quả email tức thời. Lỗi tạo PDF, SMTP trả thất bại hoặc ném exception được bắt theo từng hóa đơn; không biến toàn lô thành lỗi HTTP chung vì cổng đã công bố. Controller `SendEmail` cũ chuyển qua Application Service và kiểm tra role/chi nhánh, trạng thái hóa đơn.
- [x] Hai request "Gửi hóa đơn" đồng thời chỉ một request chuyển `DaChot → DaGui`; request lặp trên `DaGui` không tự gửi thêm email. Tạo thao tác riêng "Gửi lại email" cho Admin/nhân viên đúng chi nhánh trên bản `DaGui` còn hiệu lực; mỗi lần bấm đều thử gửi trực tiếp, trả kết quả ngay và báo có thể gửi trùng. Không có nút gửi lại cho `DaHuy`.
- [x] Sửa `GetOverdueInvoicesAsync` để job nhắc nợ hiện có chỉ lấy `TrangThaiPhatHanh = DaGui`, `!IsDeleted`, chưa thanh toán; quá hạn theo `HanThanhToan`, hoặc `NgayGui` khi thiếu hạn. Hai JSON state store/job nhắc email giữ nguyên ở giai đoạn này; không đưa migration/chuyển dữ liệu JSON vào phạm vi.
- [x] Test SMTP giả cho thành công, thất bại, exception, thiếu PDF/địa chỉ, lệnh lặp, gửi lại thủ công và timeout sau công bố; xác nhận không có cột/trạng thái email mới. Không gửi email thật trong automated tests.

> **Nghiệm thu đợt 2B (29/09/2026, Mục 7–8):** File thực tế khác danh sách **Files** ở trên: use case mới `InvoicePublicationService` + `IInvoicePublicationStore`/`InvoicePublicationStore` (không mở rộng `InvoiceIssuanceService`); test ở `InvoicePublicationTests` (Application, Web), `InvoicePublicationStoreTests`, `InvoicePublicationConcurrencyTests`, `InvoiceDueDateCalculatorTests`. Hai vòng `/ship`, reviewer lần 2 CHỐT; sửa thêm T1/T2. Build 0 lỗi; Domain 100, Application 376, Infrastructure 225 (3/3 lần), `InvoicePublicationConcurrencyTests` 7/7 (5/5 lần), Web 114 trên `quanlyphongtro_test`; EF không có thay đổi model. Người dùng thử tay đạt. Còn mở (thấp): T3, T4, T5, L5. Báo cáo: `docs/KeHoachHienTai/2026-09-29-bao-cao-dot-2b-cong-bo-hoa-don.md`.

## 9. MVC/API và giao diện

**Files**
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/DienNuocController.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Controllers/HoaDonController.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachThue/Controllers/DienNuocController.cs`
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/DienNuoc/Index.cshtml`
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml`
- Create: `src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachThue/Views/DienNuoc/Index.cshtml`
- Modify: `src/QuanLyChoThuePhongTroWeb.Web/Areas/KhachThue/Views/HoaDon/Index.cshtml`
- Create: `src/QuanLyChoThuePhongTroWeb.Web/Api/V1/MeterReadingsController.cs`
- Create: `src/QuanLyChoThuePhongTroWeb.Web/Api/V1/InvoiceIssuanceController.cs`
- Modify: `tests/QuanLyChoThuePhongTroWeb.Web.IntegrationTests/Areas/AreaRoutingTests.cs`

- [ ] Web nhận `IFormFile`, chuyển `UploadFile`; JPEG/PNG tối đa 5 MB, kiểm tra nội dung. Màn khách cho tải ảnh mới khi ảnh/OCR lỗi; màn nhân viên tách số OCR và số chính thức, duyệt/nhập tay kèm lý do; màn Admin chốt/trả lại hóa đơn. Không tạo ô số khách đề xuất hoặc màn cấp quyền chức năng riêng từng nhân viên (phương án 2) trong đợt này; màn phân công chi nhánh làm ở Mục 6B.
- [ ] Màn hóa đơn hiển thị preview, tạo/gửi hàng loạt và kết quả email tức thời theo từng `HoaDonId`. Sau khi tải lại trang chỉ hiển thị trạng thái công bố trên cổng, không suy ra email đã gửi hay thất bại. Nút "Gửi lại email" trên `DaGui` có cảnh báo khả năng gửi trùng; bản hủy không có nút này. Nút theo role/chi nhánh và trạng thái; Application vẫn quyết định quyền.
- [ ] API v1 gọi cùng service, 400 cho input sai, 403/404 cho không có quyền/tài nguyên, 409 cho xung đột; bảo vệ POST với auth/CSRF phù hợp cookie hiện có.
- [ ] Test route, antiforgery, phân công đúng/sai chi nhánh, Admin-only chốt/trả lại, ảnh sai định dạng, khách xem nháp qua URL. Test lô có một SMTP lỗi và một thành công vẫn công bố cả hai; bấm "Gửi hóa đơn" lặp/hai request đồng thời chỉ có một thông báo/sự kiện công bố và không tự gửi email lần nữa; "Gửi lại email" là thao tác riêng, có quyền và chặn bản hủy.
- [ ] Màn nhân viên có thao tác sửa số ảnh đã xác nhận kèm lý do (dùng `CorrectConfirmedImageAsync`).

## 10. Kiểm chứng và bàn giao

- [ ] Chạy `dotnet build QuanLyChoThuePhongTroWeb.sln`.
- [ ] Chạy `dotnet test tests/QuanLyChoThuePhongTroWeb.Domain.UnitTests` và `dotnet test tests/QuanLyChoThuePhongTroWeb.Application.UnitTests`.
- [ ] Với `QLCTPT_TEST_CONNECTION_STRING` trỏ DB riêng hậu tố `_test`/`_integration_test`, chạy `dotnet test QuanLyChoThuePhongTroWeb.sln`. Thiếu cấu hình không được ghi là pass.
- [ ] Chạy lại `dotnet ef migrations has-pending-model-changes --project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj --startup-project src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj` ở bước bàn giao; kỳ vọng không có pending model changes so với schema nền.
- [ ] Thử tay: khách đổi ảnh, OCR lỗi, nhân viên đúng/sai chi nhánh hoặc mất phân công, nhập tay có lý do, sửa nháp, Admin chốt/trả lại, SMTP lỗi một phần trong lô, bấm gửi lặp và gửi lại email thủ công, tải lại trang không còn kết quả email, hủy chưa thu tiền, tạo `-R1/-R2` và xem bản hủy. Lưu kết quả vào `docs/features/anh-chi-so-hoa-don/review/`.
- [ ] Chạy `git diff --check`, xem diff/status; giữ nguyên hai ERD sửa sẵn. Đưa quy tắc chi nhánh, giới hạn schema hiện có và phần giao với module 2 cho người B review trước khi gộp.

## 11. Phát triển sau — phương án 2, không thực hiện trong plan này

Giữ interface kiểm tra ở mục 3 để thay phần tra cứu bằng bảng `nhan_vien_quyen_chi_nhanh` theo đặc tả mục 10 của `spec.md`. Giai đoạn sau mới tạo migration bảng quyền, UI Admin cấp/thu hồi, kiểm thử quyền A không suy ra B và kế hoạch cấp grant cho nhân viên hiện hữu. Không đưa các việc này vào tiêu chí nghiệm thu hiện tại; giai đoạn này không có migration.

## 12. Phát triển sau — thay hai file JSON của job nhắc email

Thiết kế riêng cơ chế chống gửi lặp và retry cho `JsonContractExpiryAlertStateStore`/`JsonInvoiceReminderStateStore`; kiểm tra lỗi khóa chung khách/Admin và truy vấn ngày nhắc. Đợt này chỉ sửa điều kiện truy vấn nhắc nợ để loại hóa đơn chưa công bố/đã hủy. Không tuyên bố hai file JSON đã được thay thế.
